#!/usr/bin/env python3
"""Installs an approved pipeline run into the game.

    python3 tools/content-pipeline/install.py tools/content-pipeline/runs/<run> [--dry-run]

- weapons    → Assets/Resources/WeaponDefinitions/<id>.asset (+ .meta)
- equipment  → Assets/Resources/EquipmentDefinitions/<id>.asset (+ .meta)
- recipes    → Assets/Resources/RecipeDefinitions/<id>.asset (+ .meta)
- loot       → rewrites the weapon/equipment drop lists of existing cards
- encounters → Assets/Resources/Encounters/<id>.asset (+ .meta); the feed picks them up
- region     → docs/content/regions/<id>.json (the feed generator, GDD Must #5, will
               define the region/boss asset format; until then this is the source data)

If the run was made with --replace, the old assets of those kinds (and the recipes that
craft them) are deleted, and scene/prefab references to the old starter weapon are
pointed at the new one. Every change is computed first; if any reference to a deleted
asset would remain, nothing is written. Only approved stages install (--force
overrides); new files never overwrite existing ones.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path

from catalog import (ATTRIBUTES, DAMAGE_TYPES, ENCOUNTER_TYPES, ITEM_CATEGORIES, RARITIES,
                     RECIPE_KINDS, REPO, RESOURCES, SLOTS, WEAPON_TYPES, Catalog)
from checks import CHECKS

STAGES = ["weapons", "equipment", "recipes", "loot", "region", "encounters"]
SCRIPTS = {"weapon": ("Inventory/WeaponDefinition.cs", "DoomScroller.Inventory.WeaponDefinition"),
           "equipment": ("Inventory/EquipmentDefinition.cs", "DoomScroller.Inventory.EquipmentDefinition"),
           "recipe": ("Inventory/RecipeDefinition.cs", "DoomScroller.Inventory.RecipeDefinition"),
           "encounter": ("Feed/EncounterData.cs", "DoomScroller.Feed.EncounterData")}

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: {classid}
"""


def header(kind, name) -> str:
    rel, cls = SCRIPTS[kind]
    meta = (REPO / "Assets" / "Scripts" / (rel + ".meta")).read_text()
    guid = re.search(r"^guid:\s*(\w+)", meta, re.M).group(1)
    return HEADER.format(script=guid, name=name, classid=f"DoomScroller::{cls}")


def q(s: str) -> str:
    return "'" + s.replace("'", "''") + "'"


def f(x) -> str:
    return str(int(x)) if float(x).is_integer() else repr(float(x))


def color(c) -> str:
    return f"{{r: {f(c['r'])}, g: {f(c['g'])}, b: {f(c['b'])}, a: 1}}"


def ref(guid) -> str:
    return f"{{fileID: 11400000, guid: {guid}, type: 2}}" if guid else "{fileID: 0}"


def ref_list(key, guids, indent="  ") -> str:
    if not guids:
        return f"{indent}{key}: []\n"
    return f"{indent}{key}:\n" + "".join(f"{indent}- {ref(g)}\n" for g in guids)


def int_array(values) -> str:
    """Unity's hex encoding of an int[] (little-endian int32)."""
    return "".join(int(v).to_bytes(4, "little", signed=True).hex() for v in values)


def mod_list(key, mods) -> str:
    if not mods:
        return f"  {key}: []\n"
    return f"  {key}:\n" + "".join(
        f"  - type: {DAMAGE_TYPES.index(m['type'])}\n    multiplier: {f(m['multiplier'])}\n" for m in mods)


def weapon_yaml(w, guids: dict, cat: Catalog = None) -> str:
    if isinstance(guids, Catalog):          # older call style: weapon_yaml(w, cat)
        guids = guids.id_to_guid
    y = header("weapon", w["id"])
    y += (f"  weaponName: {q(w['weaponName'])}\n"
          f"  weaponType: {WEAPON_TYPES.index(w['weaponType'])}\n"
          f"  damageType: {DAMAGE_TYPES.index(w['damageType'])}\n"
          f"  isTwoHanded: {int(w['isTwoHanded'])}\n"
          f"  attackSpeed: {f(w['attackSpeed'])}\n"
          f"  itemColor: {color(w['itemColor'])}\n"
          f"  icon: {{fileID: 0}}\n  statsByRarity:\n")
    for dmg in w["baseDamageByRarity"]:
        y += (f"  - baseDamage: {int(dmg)}\n    attributeScalingEntries:\n"
              f"    - attribute: {ATTRIBUTES.index(w['scalingAttribute'])}\n"
              f"      coefficient: {f(w['scalingCoefficient'])}\n")
    y += ref_list("possibleAbilities", [guids[a] for a in w["possibleAbilities"]])
    y += ref_list("possiblePassives", [guids[a] for a in w["possiblePassives"]])
    return y


def equipment_yaml(e, guids: dict) -> str:
    y = header("equipment", e["id"])
    y += (f"  equipmentName: {q(e['equipmentName'])}\n"
          f"  category: {ITEM_CATEGORIES.index(e['category'])}\n"
          f"  slotType: {SLOTS.index(e['slotType'])}\n"
          f"  icon: {{fileID: 0}}\n"
          f"  itemColor: {color(e['itemColor'])}\n"
          f"  powerByRarity: {int_array(e['powerByRarity'])}\n")
    y += ref_list("possiblePassives", [guids[p] for p in e["possiblePassives"]])
    return y


def recipe_yaml(r, guids: dict) -> str:
    y = header("recipe", r["id"])
    out = guids[r["output"]]
    y += (f"  recipeId: {uuid.uuid4()}\n"
          f"  outputKind: {RECIPE_KINDS.index(r['outputKind'])}\n"
          f"  outputItem: {{fileID: 0}}\n"
          f"  outputWeapon: {ref(out if r['outputKind'] == 'Weapon' else None)}\n"
          f"  outputEquipment: {ref(out if r['outputKind'] == 'Equipment' else None)}\n"
          f"  outputMaterial: {{fileID: 0}}\n"
          f"  outputRarity: {RARITIES.index(r['outputRarity'])}\n"
          f"  goldCost: {int(r['goldCost'])}\n")
    y += "  ingredients:\n" + "".join(
        f"  - material: {ref(guids[i['material']])}\n    count: {int(i['count'])}\n" for i in r["ingredients"])
    y += ref_list("prerequisiteRecipes", [guids[p] for p in r["prerequisites"]])
    return y


def encounter_yaml(e, cat_or_none, guids: dict) -> str:
    y = header("encounter", e["id"])
    y += (f"  title: {q(e['title'])}\n"
          f"  type: {ENCOUNTER_TYPES.index(e['type'])}\n"
          f"  artwork: {{fileID: 0}}\n  foregroundArt: {{fileID: 0}}\n"
          f"  cardColor: {color(e['cardColor'])}\n"
          f"  description: {q(e['description'])}\n"
          f"  baseXpReward: {int(e['baseXpReward'])}\n"
          f"  goldReward: {int(e['goldReward'])}\n"
          f"  lootTable:\n    percentByRarity:\n")
    loot = e["type"] != "Resource"
    pct = e["percentByRarity"] if loot else [0] * 5
    y += "".join(f"    - {f(p)}\n" for p in pct)
    y += (f"    minItemCount: {int(e['minItemCount']) if loot else 1}\n"
          f"    maxItemCount: {int(e['maxItemCount']) if loot else 1}\n")
    y += ref_list("weapons", [guids[w] for w in e["lootWeapons"]] if loot else [], "    ")
    y += ref_list("equipment", [guids[q_] for q_ in e.get("lootEquipment", [])] if loot else [], "    ")
    y += ref_list("materials", [guids[m] for m in e["lootMaterials"]] if loot else [], "    ")
    fight = e["type"] in ("Enemy", "Ambush")
    y += (f"  enemyHealth: {int(e['enemyHealth']) if fight else 80}\n"
          f"  enemyAttackDamage: {int(e['enemyAttackDamage']) if fight else 10}\n"
          f"  enemyAttackSpeed: {f(e['enemyAttackSpeed']) if fight else '0.8'}\n")
    y += mod_list("damageTypeModifiers", e["damageTypeModifiers"] if fight else [])
    y += "  dungeonRooms: []\n"
    res = e["type"] == "Resource"
    y += f"  resourceTimerSeconds: {f(e['resourceTimerSeconds']) if res else '10'}\n"
    y += mod_list("resourceWeaknesses", e["resourceWeaknesses"] if res else [])
    if res and e["resourceThresholds"]:
        y += "  resourceThresholds:\n" + "".join(
            f"  - damageRequired: {int(t['damageRequired'])}\n"
            f"    rewardMaterial: {ref(guids[t['material']])}\n"
            f"    rewardRarity: {RARITIES.index(t['rarity'])}\n"
            f"    rewardAmount: {int(t['amount'])}\n" for t in e["resourceThresholds"])
    else:
        y += "  resourceThresholds: []\n"
    return y


def patch_loot_list(text: str, key: str, guids: list) -> str:
    """Replace a card's top-level lootTable `<key>:` list (4-space indent), leaving
    dungeon-room loot tables (deeper indent) alone."""
    block = re.compile(rf"^    {key}:(?: \[\])?\n(?:    - .*\n)*", re.M)
    if not block.search(text):
        raise ValueError(f"no top-level lootTable {key} list")
    return block.sub(lambda _: ref_list(key, guids, "    "), text, count=1)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("run_dir")
    p.add_argument("--dry-run", action="store_true", help="validate and list changes, write nothing")
    p.add_argument("--force", action="store_true", help="install stages that ended needs-review")
    args = p.parse_args(argv)

    run = Path(args.run_dir)
    log = json.loads((run / "run.json").read_text())
    if log.get("draftBrief") and not args.force:
        sys.exit("This run was made without the narrative brief (test run); not installing.")
    replace = set(log.get("replace", []))
    cat = Catalog(replace=replace)
    data = {}
    for s in STAGES:
        st = log["stages"].get(s)
        if not st:
            continue
        if st["status"] != "approved" and not args.force:
            sys.exit(f"Stage {s} is {st['status']}; review it or pass --force.")
        data[s] = json.loads((run / s / "final.json").read_text())

    # Re-validate against the assets as they are now.
    errors = []
    for s, d in data.items():
        errors += [c for c in CHECKS[s](d, cat, data) if c["severity"] == "error" and c["rule"] not in ("count", "region-spread")]
    if errors:
        for c in errors:
            print(f"  {c['target']}: {c['message']}")
        sys.exit("Validation failed against current assets; nothing installed.")

    # ---- plan every change in memory
    guids = dict(cat.id_to_guid)
    deleted: dict[str, str] = {}                    # guid -> id
    deletes: list[Path] = []
    for kind in ("weapon", "equipment", "recipe"):
        for i in sorted(cat.replaced.get(kind, ())):
            path = cat.path_of[i]
            deleted[cat.id_to_guid[i]] = i
            deletes += [path, path.with_name(path.name + ".meta")]
            guids.pop(i, None)
    writes: dict[Path, str] = {}

    def new_asset(folder, item_id, text_fn):
        g = uuid.uuid4().hex
        guids[item_id] = g
        path = RESOURCES / folder / f"{item_id}.asset"
        return path, g, text_fn

    pending = []
    for w in data.get("weapons", {}).get("weapons", []):
        pending.append(new_asset("WeaponDefinitions", w["id"], lambda w=w: weapon_yaml(w, guids)))
    for e in data.get("equipment", {}).get("equipment", []):
        pending.append(new_asset("EquipmentDefinitions", e["id"], lambda e=e: equipment_yaml(e, guids)))
    for r in data.get("recipes", {}).get("recipes", []):
        pending.append(new_asset("RecipeDefinitions", r["id"], lambda r=r: recipe_yaml(r, guids)))
    for e in data.get("encounters", {}).get("encounters", []):
        pending.append(new_asset("Encounters", e["id"], lambda e=e: encounter_yaml(e, None, guids)))
    for path, g, text_fn in pending:          # all guids known now, so refs resolve
        writes[path] = text_fn()
        writes[path.with_name(path.name + ".meta")] = META.format(guid=g)

    for row in data.get("loot", {}).get("rewired", []):
        path = cat.path_of[row["encounter"]]
        text = path.read_text(encoding="utf-8")
        old = {k: [g for g in re.findall(r"guid: (\w+)", m.group(0))]
               for k in ("weapons", "equipment")
               for m in [re.search(rf"^    {k}:(?: \[\])?\n(?:    - .*\n)*", text, re.M)] if m}
        keep_w = [g for g in old.get("weapons", []) if g not in deleted]
        keep_e = [g for g in old.get("equipment", []) if g not in deleted]
        text = patch_loot_list(text, "weapons", keep_w + [guids[w] for w in row["lootWeapons"]])
        text = patch_loot_list(text, "equipment", keep_e + [guids[e] for e in row["lootEquipment"]])
        writes[path] = text

    # Scenes/prefabs: point references to a deleted weapon at the new starter.
    starter = next((w for w in data.get("weapons", {}).get("weapons", []) if w.get("isStarter")), None)
    deleted_weapons = {g for g, i in deleted.items() if i in cat.replaced.get("weapon", ())}
    for path in list((REPO / "Assets").rglob("*.unity")) + list((REPO / "Assets").rglob("*.prefab")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        hit = [g for g in deleted_weapons if g in text]
        if hit and starter:
            for g in hit:
                text = text.replace(g, guids[starter["id"]])
            writes[path] = text

    if "region" in data:
        path = REPO / "docs" / "content" / "regions" / f"{data['region']['region']['id']}.json"
        writes[path] = json.dumps({**data["region"], "sourceRun": run.name}, indent=2, ensure_ascii=False) + "\n"

    # ---- safety: nothing new overwrites, and no reference to a deleted asset survives
    delete_set = {d.resolve() for d in deletes}
    clash = [str(pth) for pth in writes if pth.exists() and pth.suffix in (".asset", ".meta", ".json")
             and pth.resolve() not in delete_set and not any(pth == cat.path_of.get(r["encounter"])
                                                             for r in data.get("loot", {}).get("rewired", []))]
    if clash:
        sys.exit("Refusing to overwrite:\n  " + "\n  ".join(clash))
    dangling = []
    for path in list((REPO / "Assets").rglob("*.asset")) + list((REPO / "Assets").rglob("*.unity")) \
            + list((REPO / "Assets").rglob("*.prefab")):
        if path.resolve() in delete_set:
            continue
        text = writes.get(path) or path.read_text(encoding="utf-8", errors="ignore")
        for g, i in deleted.items():
            if g in text:
                dangling.append(f"{path.relative_to(REPO)} → {i}")
    for path, text in writes.items():
        if path.suffix == ".asset" and not path.exists():
            for g, i in deleted.items():
                if g in text:
                    dangling.append(f"{path.relative_to(REPO)} (new) → {i}")
    if dangling:
        sys.exit("These would still reference deleted assets; nothing installed:\n  " + "\n  ".join(dangling))

    # ---- apply
    verb = "would " if args.dry_run else ""
    for d in deletes:
        print(f"{verb}delete {d.relative_to(REPO)}")
        if not args.dry_run:
            d.unlink()
    for pth, text in writes.items():
        print(f"{verb}write  {pth.relative_to(REPO)}")
        if not args.dry_run:
            pth.parent.mkdir(parents=True, exist_ok=True)
            pth.write_text(text, encoding="utf-8")
    if starter:
        print(f"Starter weapon is now {starter['weaponName']} ({starter['id']}). Add the old starter's "
              f"name to ItemSnapshot.FormerWeaponNames so old saves relink.")
    if not args.dry_run:
        print("Done. Refresh the Unity Editor (or run tests) to import the changes.")


if __name__ == "__main__":
    main()
