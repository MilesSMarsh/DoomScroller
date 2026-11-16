#!/usr/bin/env python3
"""Goal-oriented coding agent for Doom Scroller (class assignment 05).

Reads the GDD, scans the codebase, finds features the GDD requires that the code lacks,
scores them, writes the code for the highest-priority buildable gap one file at a time,
re-scans after every write, and stops when every buildable requirement is met.

    python3 tools/gdd-agent/agent.py --plan          # scan + gaps + priorities, no writes
    python3 tools/gdd-agent/agent.py                 # run the loop (writes code)
    python3 tools/gdd-agent/agent.py --max-iters 5 --model sonnet

Code generation goes through Claude Code's headless mode (`claude -p`, see
tools/content-pipeline/llm.py), so it runs on the existing Claude Code login. Every run
writes a log to tools/gdd-agent/runs/<timestamp>/.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
GDD = ROOT / "docs" / "gdd" / "GDD.md"
FEATURES = HERE / "features.json"
CONVENTIONS = ROOT / ".claude" / "rules" / "unity-conventions.md"
sys.path.insert(0, str(ROOT / "tools" / "content-pipeline"))

TYPE_RE = re.compile(r"\b(?:class|struct|interface|enum|record)\s+([A-Z][A-Za-z0-9_]*)")


# ---------- 1. Read the GDD ----------

def read_gdd() -> dict[str, str]:
    """Heading -> section text (until the next heading of the same or higher level)."""
    lines = GDD.read_text().splitlines()
    sections: dict[str, str] = {}
    heads = [(i, len(m.group(1)), m.group(2).strip())
             for i, l in enumerate(lines) if (m := re.match(r"(#{1,6})\s+(.*)", l))]
    for n, (i, level, title) in enumerate(heads):
        end = next((j for j, lv, _ in heads[n + 1:] if lv <= level), len(lines))
        sections[title] = "\n".join(lines[i:end]).strip()
    return sections


def must_list_order(sections: dict[str, str]) -> dict[int, str]:
    """The numbered 'Must, in build order' items from the GDD's Semester scope."""
    text = sections.get("Semester scope", "")
    return {int(m.group(1)): m.group(2).strip() for m in re.finditer(r"^(\d+)\.\s+(.+)$", text, re.M)}


# ---------- 2. Scan the codebase ----------

@dataclass
class Scan:
    types: dict[str, set[str]] = field(default_factory=dict)   # root -> declared type names
    files: dict[str, str] = field(default_factory=dict)        # repo-relative path -> text

    def has_type(self, name: str, root: str = "Assets/Scripts") -> bool:
        if root == "Assets/Scripts":  # tests live beside scripts for this check
            return any(name in self.types.get(r, set()) for r in ("Assets/Scripts", "Assets/Tests"))
        return name in self.types.get(root, set())


def scan() -> Scan:
    s = Scan()
    for root, pattern in (("Assets/Scripts", "*.cs"), ("Assets/Tests", "*.cs"), ("Server/src", "*.cs")):
        names: set[str] = set()
        for p in (ROOT / root).rglob(pattern):
            if "/obj/" in str(p) or "/bin/" in str(p):
                continue
            text = p.read_text(errors="replace")
            s.files[str(p.relative_to(ROOT))] = text
            names |= set(TYPE_RE.findall(text))
        s.types[root] = names
    return s


def check(c: dict, path: str, sc: Scan) -> bool:
    """One 'done_when' / capability check against the scan."""
    if "type" in c:
        return sc.has_type(c["type"], c.get("root", "Assets/Scripts"))
    text = sc.files.get(path, "")
    if "absent_text" in c:
        return bool(text) and c["absent_text"] not in text
    if "text" in c:
        return c["text"] in text
    raise ValueError(f"unknown check {c}")


# ---------- 3. Gap detection ----------

@dataclass
class Gap:
    feature: dict
    step: dict
    step_index: int
    missing_needs: list[str]
    gdd_text: str
    score: float = 0.0
    why: list[str] = field(default_factory=list)

    @property
    def buildable(self) -> bool:
        return not self.missing_needs


def find_gaps(cfg: dict, sections: dict[str, str], sc: Scan) -> tuple[list[Gap], list[str]]:
    """Returns (open gaps, ids of features already complete). One gap per feature: its
    first unfinished step, since steps build on each other."""
    caps = {k: check(v, "", sc) for k, v in cfg["capabilities"].items()}
    gaps, complete = [], []
    for f in cfg["features"]:
        sec = sections.get(f["gdd_section"])
        if sec is None:
            raise SystemExit(f"features.json cites GDD section '{f['gdd_section']}' which the GDD lacks")
        if not any(k.lower() in GDD.read_text().lower() for k in f["gdd_keywords"]):
            raise SystemExit(f"feature {f['id']}: none of its keywords appear in the GDD")
        todo = [(i, st) for i, st in enumerate(f["steps"])
                if not all(check(c, st["path"], sc) for c in st["done_when"])]
        if not todo:
            complete.append(f["id"])
            continue
        i, st = todo[0]
        gaps.append(Gap(f, st, i, [n for n in f["needs"] if not caps.get(n)], sec))
    return gaps, complete


# ---------- 4. Priority scoring ----------

TIER = {"must": 100, "should": 40, "stretch": 10}
SIZE = {"S": 15, "M": 5, "L": -15}


def priority_score(g: Gap, must_items: dict[int, str]) -> float:
    """Higher = build sooner.
    - GDD tier: Must >> Should >> Stretch (don't build Should/Stretch early).
    - Build order: earlier items in the GDD's Must list score higher (+3 per place).
    - Blocked: a feature whose prerequisite capability is missing can't be built now (-200).
    - Size: one agent step writes one file, so small, self-contained features are favoured.
    - Momentum: a feature already part-built gets +25 so the agent finishes what it started.
    - Player-facing features get +5 (the semester demo is the game, not the plumbing).
    """
    f = g.feature
    why = [f"tier {f['tier']} +{TIER[f['tier']]}"]
    score = TIER[f["tier"]]
    if f.get("scope"):
        bonus = 3 * (len(must_items) + 1 - f["scope"])
        score += bonus
        why.append(f"GDD build order #{f['scope']} +{bonus}")
    if g.missing_needs:
        score -= 200
        why.append(f"blocked (needs {', '.join(g.missing_needs)}) -200")
    score += SIZE[f["size"]]
    why.append(f"size {f['size']} {SIZE[f['size']]:+d}")
    if g.step_index > 0:
        score += 25
        why.append("already started +25")
    if f.get("player_facing"):
        score += 5
        why.append("player-facing +5")
    g.why = why
    return score


# ---------- 5. Code-writing step ----------

SYSTEM = (
    "You are the code-writing step of a goal-oriented coding agent for a Unity 6 C# mobile "
    "game, Doom Scroller. You write exactly one complete file that compiles against the "
    "existing code shown to you. Follow the project conventions given. Use only APIs that "
    "appear in the context files or the .NET/Unity standard library. No placeholder "
    "comments like 'TODO implement'; write the real code. Return JSON only."
)
SCHEMA = {
    "type": "object",
    "properties": {
        "path": {"type": "string"},
        "content": {"type": "string"},
        "summary": {"type": "string"},
    },
    "required": ["path", "content", "summary"],
}


def write_step(g: Gap, sc: Scan, model: str, log_dir: Path) -> Path:
    from llm import call  # tools/content-pipeline/llm.py: `claude -p` with a JSON schema

    st = g.step
    ctx = "\n\n".join(f"// ===== {p} =====\n{sc.files.get(p) or (ROOT / p).read_text()}"
                      for p in st.get("context_files", []) if (ROOT / p).exists())
    done = [s["path"] for s in g.feature["steps"][:g.step_index]]
    done_ctx = "\n\n".join(f"// ===== {p} (written earlier in this feature) =====\n{(ROOT / p).read_text()}"
                           for p in done if (ROOT / p).exists())
    prompt = (
        f"# Feature (from the GDD): {g.feature['title']}\n\n## GDD section\n{g.gdd_text}\n\n"
        f"## Project conventions\n{CONVENTIONS.read_text()}\n\n"
        f"## Your file\nWrite `{st['path']}` (step {g.step_index + 1} of {len(g.feature['steps'])}).\n"
        f"{st.get('brief', '')}\nIt is accepted when: {json.dumps(st['done_when'])}\n\n"
        f"## Existing code\n{done_ctx}\n\n{ctx}\n\n"
        f"Return the full file content for `{st['path']}`."
    )
    (log_dir / f"prompt-{g.feature['id']}-{g.step_index + 1}.md").write_text(prompt)
    out, usage = call(SYSTEM, prompt, SCHEMA, model)
    if out["path"].strip() != st["path"]:
        print(f"  ! model returned path {out['path']!r}; writing to the planned {st['path']}")
    dest = ROOT / st["path"]
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(out["content"].rstrip() + "\n")
    (log_dir / f"reply-{g.feature['id']}-{g.step_index + 1}.json").write_text(json.dumps({**out, "usage": usage}, indent=2))
    print(f"  wrote {st['path']} ({len(out['content'].splitlines())} lines): {out['summary']}")
    return dest


def run_tests() -> str:
    """Headless EditMode tests. Unity allows one process per project, so this reports
    'editor-open' when the Editor holds the project; verify there instead."""
    proc = subprocess.run([str(ROOT / "tools" / "run-editmode-tests.sh")], cwd=ROOT,
                          capture_output=True, text=True)
    return {0: "passed", 2: "editor-open"}.get(proc.returncode, "FAILED:\n" + proc.stdout[-1500:])


# ---------- 6. The loop ----------

def plan(cfg, sections, must_items, sc) -> tuple[list[Gap], list[str]]:
    gaps, complete = find_gaps(cfg, sections, sc)
    for g in gaps:
        g.score = priority_score(g, must_items)
    gaps.sort(key=lambda g: g.score, reverse=True)
    return gaps, complete


def print_plan(gaps: list[Gap], complete: list[str]) -> None:
    print(f"  complete: {', '.join(complete) or 'none'}")
    for g in gaps:
        flag = "  " if g.buildable else "x "
        print(f"  {flag}{g.score:6.0f}  {g.feature['id']:<24} next: {g.step['path']}")
        print(f"            {'; '.join(g.why)}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--plan", action="store_true", help="scan and print priorities only; write nothing")
    ap.add_argument("--max-iters", type=int, default=6)
    ap.add_argument("--model", default="sonnet")
    ap.add_argument("--include-should", action="store_true",
                    help="also build Should/Stretch features (default: Must only, per the GDD)")
    ap.add_argument("--no-tests", action="store_true", help="skip the headless test run after each write")
    args = ap.parse_args()

    cfg = json.loads(FEATURES.read_text())
    sections = read_gdd()
    must_items = must_list_order(sections)
    print(f"GDD: {len(sections)} sections, {len(must_items)} Must items in build order")

    log_dir = HERE / "runs" / dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    log_dir.mkdir(parents=True, exist_ok=True)
    attempts: dict[str, int] = {}
    for it in range(1, args.max_iters + 1):
        sc = scan()
        gaps, complete = plan(cfg, sections, must_items, sc)
        print(f"\n== iteration {it}: scanned {len(sc.files)} C# files ==")
        print_plan(gaps, complete)
        in_scope = [g for g in gaps if args.include_should or g.feature["tier"] == "must"]
        todo = [g for g in in_scope if g.buildable and attempts.get(g.step["path"], 0) < 2]
        if not todo:
            blocked = [g.feature["id"] for g in in_scope if not g.buildable]
            deferred = [g.feature["id"] for g in gaps if g not in in_scope]
            print("\nDONE: every buildable Must requirement in the GDD is met."
                  + (f" Blocked until their prerequisites exist: {', '.join(blocked)}." if blocked else "")
                  + (f" Deferred (Should/Stretch): {', '.join(deferred)}." if deferred else ""))
            return 0
        if args.plan:
            return 0
        g = todo[0]
        attempts[g.step["path"]] = attempts.get(g.step["path"], 0) + 1
        print(f"\n-> building {g.feature['id']} step {g.step_index + 1}/{len(g.feature['steps'])}: {g.step['path']}")
        write_step(g, sc, args.model, log_dir)
        after = scan()
        ok = all(check(c, g.step["path"], after) for c in g.step["done_when"])
        print(f"  re-scan: step {'satisfied' if ok else 'NOT satisfied (will retry)'}")
        if ok and not args.no_tests and g.step["path"].startswith("Assets/"):
            print(f"  tests: {run_tests()}")
    print(f"\nStopped after {args.max_iters} iterations with work left.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
