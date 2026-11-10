"""Writes runs/<run>/report.md: what each round's checks and critic caught, and the final
content in readable form. Can be re-run alone: python3 report.py runs/<run>"""
from __future__ import annotations

import json
import sys
from pathlib import Path

STAGES = ["weapons", "equipment", "recipes", "loot", "region", "encounters"]


def _load(p: Path):
    return json.loads(p.read_text()) if p.exists() else None


def _cell(s) -> str:
    return str(s).replace("|", "\\|").replace("\n", " ")


def _render_final(stage, data) -> list[str]:
    L = []
    if stage == "region":
        r, b = data["region"], data["soloBoss"]
        d = r["difficulty"]
        weak = ", ".join("%s ×%s" % (m["type"], m["multiplier"]) for m in b["damageTypeModifiers"])
        L += [f"**{r['name']}** — *{r['tagline']}*", "", r["description"], "",
              f"- Difficulty: HP {d['enemyHealth']}, damage {d['enemyAttackDamage']}, XP {d['baseXpReward']}",
              f"- Cards: {', '.join(r['existingCards'])}",
              f"- Mix: {r['cardMix']}",
              f"- Hidden satire (designer note): {r['hiddenSatire']}", "",
              f"**Solo boss: {b['name']}, {b['epithet']}** (`{b['id']}`)", "",
              f"> {b['description']}", "",
              f"- Intro: “{b['introLine']}”", f"- Victory: “{b['victoryLine']}”",
              f"- Defeat: “{b['defeatLine']}”",
              f"- HP {b['enemyHealth']} · damage {b['enemyAttackDamage']} · speed {b['enemyAttackSpeed']} · "
              f"regains {b['hpRegenPercentPerHour']}%/h · XP {b['baseXpReward']} · gold {b['goldReward']}",
              f"- Weaknesses: {weak or 'none'}",
              f"- Loot: {', '.join(b['lootWeapons'] + b.get('lootEquipment', []) + b['lootMaterials'])} (rarity {b['percentByRarity']})",
              f"- Key item: **{b['keyItem']['name']}**: {b['keyItem']['description']}"]
    elif stage == "weapons":
        L += ["| Weapon | Region | Type | Damage Plain→Exalted | Speed | Abilities pool | Passives pool | Flavor |",
              "|---|---|---|---|---|---|---|---|"]
        for w in sorted(data["weapons"], key=lambda w: (w["weaponType"], w.get("region", ""))):
            star = " ★ starter" if w.get("isStarter") else ""
            L.append(f"| **{_cell(w['weaponName'])}** (`{w['id']}`){star} | {w.get('region', '')} | {w['weaponType']} / {w['damageType']}"
                     f"{' / 2H' if w['isTwoHanded'] else ''} | {w['baseDamageByRarity']} | {w['attackSpeed']} | "
                     f"{', '.join(a.replace('Ability_', '') for a in w['possibleAbilities'])} | "
                     f"{', '.join(a.replace('Passive_', '') for a in w['possiblePassives'])} | {_cell(w['flavorText'])} |")
    elif stage == "equipment":
        L += ["| Piece | Region | Slot | Power Plain→Exalted | Passives pool | Flavor |", "|---|---|---|---|---|---|"]
        for q in data["equipment"]:
            L.append(f"| **{_cell(q['equipmentName'])}** (`{q['id']}`) | {q['region']} | {q['category']} / {q['slotType']} | "
                     f"{q['powerByRarity']} | {', '.join(a.replace('Passive_', '') for a in q['possiblePassives'])} | {_cell(q['flavorText'])} |")
    elif stage == "recipes":
        L += ["| Recipe | Crafts | Gold | Ingredients | Needs first |", "|---|---|---|---|---|"]
        for r in data["recipes"]:
            ing = ", ".join(f"{i['count']} {i['material']}" for i in r["ingredients"])
            L.append(f"| `{r['id']}` | {r['outputRarity']} {r['output']} | {r['goldCost']} | {ing} | {', '.join(r['prerequisites']) or '—'} |")
    elif stage == "loot":
        L += ["| Card | New weapon drops | New equipment drops | Why |", "|---|---|---|---|"]
        for r in data["rewired"]:
            L.append(f"| `{r['encounter']}` | {', '.join(r['lootWeapons']) or '—'} | {', '.join(r['lootEquipment']) or '—'} | {_cell(r['reason'])} |")
    else:
        L += ["| Card | Type | Text | Stats | Loot |", "|---|---|---|---|---|"]
        for e in data["encounters"]:
            if e["type"] in ("Enemy", "Ambush"):
                stats = f"HP {e['enemyHealth']} · dmg {e['enemyAttackDamage']} · spd {e['enemyAttackSpeed']} · XP {e['baseXpReward']}"
            elif e["type"] == "Resource":
                stats = " / ".join(f"{t['damageRequired']}→{t['amount']} {t['material']}" for t in e["resourceThresholds"])
            else:
                stats = f"XP {e['baseXpReward']} · gold {e['goldReward']}"
            loot = ", ".join(e["lootWeapons"] + e.get("lootEquipment", []) + e["lootMaterials"]) if e["type"] != "Resource" else "—"
            L.append(f"| **{_cell(e['title'])}** (`{e['id']}`) | {e['type']} | {_cell(e['description'])} | {stats} | {loot} |")
    return L


def write(run_dir: Path):
    run_dir = Path(run_dir)
    log = _load(run_dir / "run.json") or {"stages": {}}
    L = [f"# Content pipeline run `{run_dir.name}`", ""]
    if log.get("draftBrief"):
        L += ["> **Test run:** `memory/narrative.md` was not written yet, so this output is not "
              "grounded in the narrative brief and is not for the game.", ""]
    L += ["| Stage | Status | Rounds | Check errors per round | Critic must-fix per round |",
          "|---|---|---|---|---|"]
    for s in STAGES:
        st = log["stages"].get(s)
        if st:
            rs = st["rounds"]
            L.append(f"| {s} | **{st['status']}** | {len(rs)} | {' → '.join(str(r['errors']) for r in rs)} | "
                     f"{' → '.join(str(r['criticMustFix']) for r in rs)} |")
    for s in STAGES:
        st = log["stages"].get(s)
        if not st:
            continue
        L += ["", f"## {s.capitalize()}", ""]
        if st.get("reusedFrom"):
            L += [f"Reused from `{st['reusedFrom']}`.", ""]
        for r in st["rounds"]:
            n = r["round"]
            checks = _load(run_dir / s / f"round-{n}.checks.json") or []
            crit = _load(run_dir / s / f"round-{n}.critique.json") or {"issues": []}
            L += [f"### Round {n}: {'passed' if r['passed'] else 'sent back'}", ""]
            errs = [c for c in checks if c["severity"] == "error"]
            if errs:
                L += ["Code checks:", ""] + [f"- `{c['rule']}` {c['target']}: {c['message']}" for c in errs] + [""]
            if crit["issues"]:
                L += [f"Critic ({crit['verdict']}): {crit['summary']}", "",
                      "| Severity | Category | Target | Problem | Fix |", "|---|---|---|---|---|"]
                L += [f"| {i['severity']} | {i['category']} | {_cell(i['target'])} | {_cell(i['problem'])} | {_cell(i['fix'])} |"
                      for i in sorted(crit["issues"], key=lambda i: ["blocker", "major", "minor"].index(i["severity"]))]
                L.append("")
            elif crit.get("summary"):
                L += [f"Critic ({crit['verdict']}): {crit['summary']}", ""]
            if crit.get("soundsLikeTheGame"):
                L += [f"*Sounds like the game:* {crit['soundsLikeTheGame']}", ""]
        final = _load(run_dir / s / "final.json")
        if final:
            L += ["### Final", ""] + _render_final(s, final)
    (run_dir / "report.md").write_text("\n".join(L) + "\n", encoding="utf-8")


if __name__ == "__main__":
    write(Path(sys.argv[1]))
