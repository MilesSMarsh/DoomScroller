#!/usr/bin/env python3
"""Doom Scroller dynamic content pipeline.

    python3 tools/content-pipeline/pipeline.py                 # all stages, add to the game
    python3 tools/content-pipeline/pipeline.py --replace weapons,equipment   # re-author those
    python3 tools/content-pipeline/pipeline.py --stages region,encounters
    python3 tools/content-pipeline/install.py tools/content-pipeline/runs/<run>   # into Unity

Each stage: the generator agent reads the game's own docs (narrative brief, GDD sections,
content guide) and a catalog parsed from the real Unity assets, then drafts content as
JSON. Code checks verify hard rules; a separate critic agent reviews tone, lore and GDD
fit. Anything an error or a blocker/major critique flags goes back to the generator,
up to --rounds times. Every round is saved under runs/<timestamp>/ and summarised in
report.md.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
from pathlib import Path

import llm
from catalog import REPO, Catalog
from checks import CHECKS
from schemas import CRITIQUE, STAGE_SCHEMAS

HERE = Path(__file__).resolve().parent
PROMPTS = HERE / "prompts"
STAGES = ["weapons", "equipment", "recipes", "loot", "region", "encounters"]
REPLACEABLE = {"weapons": "weapon", "equipment": "equipment"}
BRIEF = REPO / "memory" / "narrative.md"
GDD = REPO / "docs" / "gdd" / "GDD.md"
CONTENT_GUIDE = REPO / "memory" / "content.md"
GDD_SECTIONS = ["Executive Summary", "Feed generation", "Encounter types", "Combat",
                "Progression", "Boss types"]


# ---------------------------------------------------------------- source material

def gdd_sections(names=GDD_SECTIONS) -> str:
    text = GDD.read_text(encoding="utf-8")
    out = []
    for name in names:
        m = re.search(rf"^(#{{2,3}}) {re.escape(name)}\s*$", text, re.M)
        if not m:
            raise SystemExit(f"GDD section {name!r} not found in {GDD}")
        level = len(m.group(1))
        end = re.compile(rf"^#{{2,{level}}} ", re.M).search(text, m.end())
        out.append(text[m.start():end.start() if end else len(text)].strip())
    return "\n\n".join(out)


def brief_is_draft(text: str) -> bool:
    body = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    filled = [s for s in re.split(r"^## ", body, flags=re.M)[1:] if len(s.strip().splitlines()) > 1]
    return "Not written yet" in text or len(filled) < 3


def source_block(cat: Catalog, brief: str) -> str:
    return "\n\n".join([
        "# NARRATIVE BRIEF (memory/narrative.md, owned by the creative director)\n" + brief,
        "# GAME DESIGN DOCUMENT (relevant sections of docs/gdd/GDD.md)\n" + gdd_sections(),
        "# CONTENT DATA GUIDE (memory/content.md)\n" + CONTENT_GUIDE.read_text(encoding="utf-8"),
        "# CATALOG: everything authored in the game today, parsed from Assets/Resources\n"
        "```json\n" + json.dumps(cat.summary(), separators=(",", ":")) + "\n```",
    ])


def earlier_block(ctx: dict) -> str:
    parts = [f"## {s}\n```json\n{json.dumps(ctx[s], indent=1)}\n```" for s in STAGES if s in ctx]
    return "# EARLIER STAGES (approved in this run)\n" + ("\n".join(parts) if parts else "none")


# ---------------------------------------------------------------- agents

def equipment_plan(args) -> dict:
    return {k: int(v) for k, v in (x.split(":") for x in args.equipment_plan.split(","))}


def weapon_count(args) -> int:
    return args.per_type * 3 if args.per_type else args.weapons


def task_text(stage: str, args) -> str:
    plan = equipment_plan(args)
    count = {"weapons": weapon_count(args), "encounters": args.encounters,
             "equipment": sum(plan.values())}.get(stage, "")
    replace_note = ""
    if REPLACEABLE.get(stage) in args.replace_kinds:
        replace_note = ("This list **replaces every existing " + stage + "**: the old ones are being "
                        "deleted, so this is the game's whole " + stage + " list.\n\n")
    per_type = args.per_type or max(1, args.weapons // 3)
    return ((PROMPTS / f"task_{stage}.md").read_text(encoding="utf-8")
            .replace("{count}", str(count)).replace("{per_type}", str(per_type))
            .replace("{plan}", ", ".join(f"{n} {s}" for s, n in plan.items()))
            .replace("{replace_note}", replace_note)
            .replace("{pool_limits}", pool_limits()))


def pool_limits() -> str:
    cat = Catalog()
    parts = []
    for wt in ("Melee", "Ranged", "Magic"):
        a = sum(1 for x in cat.abilities.values() if x["kind"] == "ability" and wt in x["allowedWeaponTypes"])
        p = sum(1 for x in cat.abilities.values() if x["kind"] == "passive" and wt in x["allowedWeaponTypes"])
        parts.append(f"{wt}: {a} abilities, {p} passives")
    return "The catalog has only so many to choose from (" + "; ".join(parts) + ")."


def check_ctx(ctx, args) -> dict:
    return {**ctx, "weapon_count": weapon_count(args), "weapons_per_type": args.per_type,
            "require_starter": "weapon" in args.replace_kinds,
            "equipment_plan": equipment_plan(args), "encounter_count": args.encounters}


def generate(stage, sources, ctx, args, prev=None, checks=None, critique=None):
    parts = [sources, earlier_block(ctx), task_text(stage, args)]
    if prev is not None:
        parts.append(
            "# REVISION REQUEST\nYour previous draft is below, followed by what the automated "
            "checks and the critic found. Return the complete revised set. Fix every error and "
            "every blocker/major issue; keep entries and lines nobody flagged unless a fix "
            "requires changing them.\n\n## Previous draft\n```json\n" + json.dumps(prev, indent=1) +
            "\n```\n## Automated check results\n```json\n" + json.dumps(checks, indent=1) +
            "\n```\n## Critic review\n```json\n" + json.dumps(critique, indent=1) + "\n```")
    return llm.call((PROMPTS / "generator.md").read_text(encoding="utf-8"), "\n\n".join(parts),
                    STAGE_SCHEMAS[stage], args.gen_model)


def criticise(stage, sources, ctx, args, draft, checks, previous=None):
    prior = []
    if previous is not None:
        prior = ["# PREVIOUS REVIEW (your critique of the last draft; this draft is the revision)\n```json\n"
                 + json.dumps(previous, indent=1) + "\n```"]
    prompt = "\n\n".join(prior + [
        sources, earlier_block(ctx),
        "# WHAT THE DESIGNER WAS ASKED\n" + task_text(stage, args),
        "# DRAFT FORMAT (JSON Schema; every field here maps to a real Unity data field or a "
        "designer note, so don't flag a field as nonexistent)\n```json\n"
        + json.dumps(STAGE_SCHEMAS[stage], separators=(",", ":")) + "\n```",
        "# AUTOMATED CHECK RESULTS (already sent to the designer)\n```json\n" + json.dumps(checks, indent=1) + "\n```",
        f"# DRAFT TO REVIEW ({stage})\n```json\n" + json.dumps(draft, indent=1) + "\n```",
    ])
    return llm.call((PROMPTS / "critic.md").read_text(encoding="utf-8"), prompt, CRITIQUE,
                    args.critic_model)


def must_fix(critique) -> list:
    return [i for i in critique["issues"] if i["severity"] in ("blocker", "major")]


# ---------------------------------------------------------------- loop

def run_stage(stage, cat, sources, ctx, args, run_dir: Path, log):
    sdir = run_dir / stage
    sdir.mkdir(parents=True, exist_ok=True)
    draft = checks = critique = None
    rounds = []
    for rnd in range(1, args.rounds + 1):
        print(f"[{stage}] round {rnd}: generating…", flush=True)
        draft, use_g = generate(stage, sources, ctx, args, draft, checks, critique)
        checks = CHECKS[stage](draft, cat, check_ctx(ctx, args))
        errors = [c for c in checks if c["severity"] == "error"]
        print(f"[{stage}] round {rnd}: {len(errors)} check errors, "
              f"{len(checks) - len(errors)} warnings; critic reviewing…", flush=True)
        critique, use_c = criticise(stage, sources, ctx, args, draft, checks,
                                    previous=critique if rnd > 1 else None)
        fix = must_fix(critique)
        if fix:
            critique["verdict"] = "revise"
        for name, data in (("draft", draft), ("checks", checks), ("critique", critique)):
            (sdir / f"round-{rnd}.{name}.json").write_text(json.dumps(data, indent=2, ensure_ascii=False))
        passed = not errors and not fix
        rounds.append({"round": rnd, "errors": len(errors), "warnings": len(checks) - len(errors),
                       "criticMustFix": len(fix), "criticMinor": len(critique["issues"]) - len(fix),
                       "passed": passed, "usage": [use_g, use_c]})
        print(f"[{stage}] round {rnd}: critic → {critique['verdict']} "
              f"({len(fix)} must-fix, {len(critique['issues']) - len(fix)} minor)", flush=True)
        if passed:
            break
    status = "approved" if rounds[-1]["passed"] else "needs-review"
    (sdir / "final.json").write_text(json.dumps(draft, indent=2, ensure_ascii=False))
    log["stages"][stage] = {"status": status, "rounds": rounds}
    return draft, status


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--stages", default=",".join(STAGES), help="comma list from: " + ", ".join(STAGES))
    p.add_argument("--weapons", type=int, default=3, help="new weapons to generate")
    p.add_argument("--per-type", type=int, default=0,
                   help="weapons per category (Melee/Ranged/Magic); overrides --weapons")
    p.add_argument("--equipment-plan", default="Head:2,Chest:2,Legs:2,Boots:2,LeftHand:2,Talisman1:2",
                   help="equipment pieces per slot (LeftHand = shields)")
    p.add_argument("--replace", default="",
                   help="delete and re-author these kinds on install: weapons,equipment "
                        "(old items and their recipes are hidden from the agents; cards that "
                        "dropped them get rewired by the loot stage)")
    p.add_argument("--encounters", type=int, default=5, help="new encounter cards to generate")
    p.add_argument("--rounds", type=int, default=3, help="max generate→check→critique rounds per stage")
    p.add_argument("--gen-model", default="sonnet")
    p.add_argument("--critic-model", default="opus")
    p.add_argument("--from-run", help="reuse approved earlier stages from this run dir")
    p.add_argument("--out", help="run directory (default runs/<timestamp>)")
    p.add_argument("--allow-draft-brief", action="store_true",
                   help="run even though memory/narrative.md isn't written (testing only)")
    p.add_argument("--keep-going", action="store_true",
                   help="continue to later stages when one ends needs-review")
    args = p.parse_args(argv)

    stages = [s.strip() for s in args.stages.split(",") if s.strip()]
    bad = [s for s in stages if s not in STAGES]
    if bad:
        p.error(f"unknown stage(s) {bad}")
    replace = [s.strip() for s in args.replace.split(",") if s.strip()]
    if any(r not in REPLACEABLE for r in replace):
        p.error(f"--replace takes {sorted(REPLACEABLE)}")
    args.replace_kinds = {REPLACEABLE[r] for r in replace}
    for r in replace:
        if r not in stages:
            p.error(f"--replace {r} needs the {r} stage")

    brief = BRIEF.read_text(encoding="utf-8")
    draft_brief = brief_is_draft(brief)
    if draft_brief and not args.allow_draft_brief:
        sys.exit("memory/narrative.md is not written yet (needs tone, premise, naming rules…).\n"
                 "The pipeline generates from the brief; without it the output is generic "
                 "fantasy. Write the brief, or pass --allow-draft-brief for a test run.")

    cat = Catalog(replace=args.replace_kinds)
    if "loot" in stages and not cat.rewire:
        stages.remove("loot")   # nothing drops replaced items, so nothing to rewire
    sources = source_block(cat, brief)
    run_dir = Path(args.out) if args.out else HERE / "runs" / dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir.mkdir(parents=True, exist_ok=True)
    log = {"started": dt.datetime.now().isoformat(timespec="seconds"), "draftBrief": draft_brief,
           "args": {k: v for k, v in vars(args).items() if k != "replace_kinds"},
           "replace": sorted(args.replace_kinds), "stages": {}}

    ctx = {}
    if args.from_run:
        prev = Path(args.from_run)
        prev_log = json.loads((prev / "run.json").read_text())
        for s in STAGES:
            if s not in stages and prev_log["stages"].get(s, {}).get("status") == "approved":
                ctx[s] = json.loads((prev / s / "final.json").read_text())
                log["stages"][s] = {**prev_log["stages"][s], "reusedFrom": str(prev)}
                (run_dir / s).mkdir(exist_ok=True)
                for f in (prev / s).glob("*.json"):
                    (run_dir / s / f.name).write_text(f.read_text())

    code = 0
    try:
        for stage in [s for s in STAGES if s in stages]:
            ctx[stage], status = run_stage(stage, cat, sources, ctx, args, run_dir, log)
            if status != "approved":
                code = 1
                print(f"[{stage}] not approved after {args.rounds} rounds — flagged for Miles.")
                if not args.keep_going:
                    break
    except llm.LLMError as e:
        print(f"LLM call failed: {e}", file=sys.stderr)
        code = 2
    finally:
        log["finished"] = dt.datetime.now().isoformat(timespec="seconds")
        (run_dir / "run.json").write_text(json.dumps(log, indent=2))
        import report
        report.write(run_dir)
        print(f"Run saved to {run_dir}\nReport: {run_dir / 'report.md'}")
    return code


if __name__ == "__main__":
    sys.exit(main())
