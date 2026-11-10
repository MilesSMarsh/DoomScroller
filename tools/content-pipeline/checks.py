"""Deterministic consistency checks: rules a script can verify exactly (references exist,
numbers sit in the game's power band, names don't collide, ids follow conventions).
The critic agent handles what a script can't judge (tone, lore, fit with the GDD).

Each check returns a list of {"target", "severity": "error"|"warning", "rule", "message"}.
Any error sends the draft back for revision.
"""
from __future__ import annotations

import re

from catalog import RARITIES, WEAPON_TYPES, Catalog, _norm

PLACEHOLDER = re.compile(r"\b(lorem|ipsum|placeholder|tbd|todo|xxx)\b", re.I)
FIGHT = ("Enemy", "Ambush")
# Designer-only notes, never shown to players; they may discuss placeholders freely.
DESIGNER_NOTES = {"cardMix", "hiddenSatire", "dropsFrom"}
# Brief, Naming rules: card titles ≤ 35 characters.
TITLE_MAX = 35
# Brief, Never: these words stay out of player-facing text...
BANNED = re.compile(r"\b(likes?|posts?|follows?|scrolls?|scrolling|viral|content|algorithms?)\b"
                    # "feed" is allowed as a verb ("spiders nest and feed"); as a noun right
                    # after a determiner/possessive it's banned. The critic judges subtler cases.
                    r"|\b(?:the|a|an|your|my|our|their|his|her|its|this|every)\s+feeds?\b",
                    re.I)
# ...except on info cards (intro/tutorial only), the one type the brief exempts.
INFO_TYPES = {"Info"}
NAME_FIELDS = ("title", "name", "weaponName", "epithet")


def _issue(out, target, severity, rule, message):
    out.append({"target": target, "severity": severity, "rule": rule, "message": message})


def _strings(node, path=""):
    if isinstance(node, str):
        yield path, node
    elif isinstance(node, dict):
        for k, v in node.items():
            yield from _strings(v, f"{path}.{k}" if path else k)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from _strings(v, f"{path}[{i}]")


def _text_rules(out, target, item, short_fields=(), sentence_fields=()):
    info_card = item.get("type") in INFO_TYPES
    for path, text in _strings(item):
        if path in DESIGNER_NOTES:
            continue
        if PLACEHOLDER.search(text):
            _issue(out, target, "error", "no-placeholder", f"{path} looks like placeholder text: {text!r}")
        if not text.strip() and not path.endswith(("lootWeapons", "lootMaterials")):
            _issue(out, target, "error", "no-empty-text", f"{path} is empty")
        m = BANNED.search(text)
        if m and not info_card:
            _issue(out, target, "error", "banned-word", f"{path} uses {m.group(0)!r}, banned in player-facing text: {text!r}")
    for f in NAME_FIELDS:
        if "-" in item.get(f, ""):
            _issue(out, target, "error", "no-hyphen", f"{f} {item[f]!r}: names never use hyphens")
    for f in short_fields:
        if len(item.get(f, "")) > TITLE_MAX:
            _issue(out, target, "error", "title-length", f"{f} is {len(item[f])} chars; titles are ≤{TITLE_MAX}")
    for f in sentence_fields:
        text = item.get(f, "")
        if len(text) > 120:
            _issue(out, target, "error", "card-text-length", f"{f} is {len(text)} chars; card text is one sentence ≤120")
        if len(re.findall(r"[.!?](\s|$)", text.strip())) > 1:
            _issue(out, target, "error", "one-sentence", f"{f} should be one sentence: {text!r}")


def _name_rules(out, target, name, cat: Catalog, seen: dict):
    key = _norm(name)
    if key in cat.all_names():
        _issue(out, target, "error", "unique-name", f"{name!r} collides with existing {cat.all_names()[key]}")
    if key in seen and seen[key] != target:
        _issue(out, target, "error", "unique-name", f"{name!r} is also used by {seen[key]} in this batch")
    seen[key] = target


def _in(v, lo, hi):
    return lo <= v <= hi


def _modifiers(out, target, mods, field):
    types = [m["type"] for m in mods]
    if len(types) != len(set(types)):
        _issue(out, target, "error", "modifier-dupes", f"{field} lists a damage type twice")
    for m in mods:
        if not _in(m["multiplier"], 0.25, 3.0):
            _issue(out, target, "error", "modifier-range", f"{field} {m['type']} ×{m['multiplier']} outside 0.25–3")


def _rarity(out, target, pct):
    if len(pct) != 5 or any(p < 0 for p in pct) or sum(pct) <= 0:
        _issue(out, target, "error", "rarity-table", f"percentByRarity must be 5 non-negative values with a positive total, got {pct}")


def _known(cat: Catalog, ctx: dict):
    """Weapon and equipment ids that exist after this run: catalog plus this run's new ones."""
    weapons = set(cat.weapons) | {w["id"] for w in (ctx.get("weapons") or {}).get("weapons", [])}
    equipment = set(cat.equipment) | {q["id"] for q in (ctx.get("equipment") or {}).get("equipment", [])}
    return weapons, equipment


def _loot_refs(out, target, item, cat, ctx, wkey="lootWeapons", ekey="lootEquipment", mkey="lootMaterials"):
    weapons, equipment = _known(cat, ctx)
    for w in item.get(wkey, []):
        if w not in weapons:
            _issue(out, target, "error", "loot-exists", f"{wkey}: unknown weapon {w!r}")
    for q in item.get(ekey, []):
        if q not in equipment:
            _issue(out, target, "error", "loot-exists", f"{ekey}: unknown equipment {q!r}")
    for m in item.get(mkey, []):
        if m not in cat.materials:
            _issue(out, target, "error", "loot-exists", f"{mkey}: unknown material {m!r}")


# ---------------------------------------------------------------- stages

def check_region(data: dict, cat: Catalog, ctx: dict) -> list:
    out, seen = [], {}
    r, b = data["region"], data["soloBoss"]
    band = cat.stat_band()
    _text_rules(out, r["id"], r)
    _text_rules(out, b["id"], b, short_fields=("name",), sentence_fields=("description",))
    _name_rules(out, r["id"], r["name"], cat, seen)
    _name_rules(out, b["id"], b["name"], cat, seen)
    _name_rules(out, b["id"] + ".keyItem", b["keyItem"]["name"], cat, seen)
    if "-" in b["keyItem"]["name"]:
        _issue(out, b["id"], "error", "no-hyphen", "keyItem name: names never use hyphens")

    d = r["difficulty"]
    for key in ("enemyHealth", "enemyAttackDamage", "baseXpReward"):
        lo, hi = d[key]
        if lo >= hi:
            _issue(out, r["id"], "error", "band-order", f"difficulty.{key} min {lo} must be below max {hi}")
        if lo < band[key][0] or hi > band[key][1]:
            _issue(out, r["id"], "error", "band-from-game",
                   f"difficulty.{key} {d[key]} must sit inside the current game's range {band[key]}")
    if not r["existingCards"]:
        _issue(out, r["id"], "error", "card-pool", "existingCards is empty")
    types = set()
    for cid in r["existingCards"]:
        e = cat.encounters.get(cid)
        if not e:
            _issue(out, r["id"], "error", "card-exists", f"existingCards lists unknown encounter {cid!r}")
            continue
        types.add(e["type"])
        if e["type"] == "Landing":
            _issue(out, r["id"], "error", "card-pool", "Landing is shown once at first open, never in a region pool")
        if e["type"] in FIGHT:
            for key in ("enemyHealth", "enemyAttackDamage"):
                lo, hi = d[key]
                if not _in(e[key], lo, hi):
                    _issue(out, r["id"], "error", "card-in-band",
                           f"{cid} has {key} {e[key]}, outside the region band {d[key]}")
    for need in ("Enemy", "Resource", "Treasure"):
        if need not in types:
            _issue(out, r["id"], "warning", "card-mix", f"no existing {need} card in the pool (GDD card mix)")

    # GDD: a solo boss sits outside the feed's power band.
    if b["enemyHealth"] <= d["enemyHealth"][1]:
        _issue(out, b["id"], "error", "boss-above-band",
               f"boss HP {b['enemyHealth']} must exceed the region's top HP {d['enemyHealth'][1]}")
    if b["enemyHealth"] > 4 * d["enemyHealth"][1]:
        _issue(out, b["id"], "warning", "boss-scale", f"boss HP {b['enemyHealth']} is over 4× the region's top card")
    if b["enemyAttackDamage"] < d["enemyAttackDamage"][1]:
        _issue(out, b["id"], "error", "boss-above-band",
               f"boss damage {b['enemyAttackDamage']} is below the region's top card {d['enemyAttackDamage'][1]}")
    if not _in(b["enemyAttackSpeed"], 0.5, 1.5):
        _issue(out, b["id"], "error", "attack-speed", f"enemyAttackSpeed {b['enemyAttackSpeed']} outside 0.5–1.5")
    if not _in(b["hpRegenPercentPerHour"], 1, 25):
        _issue(out, b["id"], "error", "boss-regen", "hpRegenPercentPerHour must be 1–25 (GDD: slow regain)")
    if b["baseXpReward"] <= d["baseXpReward"][1]:
        _issue(out, b["id"], "warning", "boss-reward", "boss XP should beat the region's best card")
    _modifiers(out, b["id"], b["damageTypeModifiers"], "damageTypeModifiers")
    _rarity(out, b["id"], b["percentByRarity"])
    _loot_refs(out, b["id"], b, cat, ctx)
    if not b["lootWeapons"] and not b.get("lootEquipment") and not b["lootMaterials"]:
        _issue(out, b["id"], "error", "loot-exists", "boss loot is empty")
    return out


def check_weapons(data: dict, cat: Catalog, ctx: dict) -> list:
    out, seen, ids = [], {}, set()
    want = ctx.get("weapon_count")
    weapons = data["weapons"]
    per_type = ctx.get("weapons_per_type")
    if per_type:
        for wt in WEAPON_TYPES:
            got = [w for w in weapons if w["weaponType"] == wt]
            if len(got) != per_type:
                _issue(out, "weapons", "error", "count", f"asked for {per_type} {wt} weapons, got {len(got)}")
            elif len({w["region"] for w in got}) < 2:
                _issue(out, "weapons", "error", "region-spread", f"{wt} weapons should span both Green Valley and Forest")
    elif want and len(weapons) != want:
        _issue(out, "weapons", "error", "count", f"asked for {want} weapons, got {len(weapons)}")
    starters = [w for w in weapons if w.get("isStarter")]
    if ctx.get("require_starter") and len(starters) != 1:
        _issue(out, "weapons", "error", "starter", f"exactly one weapon must be the new-game starter (isStarter), got {len(starters)}")
    for s in starters:
        if s["region"] != "Green Valley":
            _issue(out, s["id"], "error", "starter", "the starter is used in the Green Valley opening; region must be Green Valley")
        if s["isTwoHanded"]:
            _issue(out, s["id"], "warning", "starter", "a two-handed starter locks the off hand for new players")
    band = cat.weapon_damage_band()
    for wt in WEAPON_TYPES:                 # attackSpeed is attacks/second
        dps = sorted((w["baseDamageByRarity"][2] * w["attackSpeed"], w["id"]) for w in weapons
                     if w["weaponType"] == wt and len(w["baseDamageByRarity"]) == 5)
        if len(dps) >= 3:
            mid = dps[len(dps) // 2][0]
            for v, i in dps:
                if mid and not 0.7 <= v / mid <= 1.3:
                    _issue(out, i, "warning", "dps-spread",
                           f"Runed damage × attackSpeed = {v:.1f}, vs the {wt} median {mid:.1f} (±30%)")
    for w in weapons:
        t = w["id"]
        if t in cat.weapons or t in ids:
            _issue(out, t, "error", "unique-id", f"asset id {t} already exists")
        ids.add(t)
        _text_rules(out, t, w, short_fields=("weaponName",), sentence_fields=("flavorText",))
        if len(w["weaponName"]) > TITLE_MAX - len("Tempered "):
            _issue(out, t, "error", "title-length",
                   f"weaponName is {len(w['weaponName'])} chars; with the longest rarity word (\"Tempered \") the title must stay ≤{TITLE_MAX}, so the name is ≤{TITLE_MAX - 9}")
        first = w["weaponName"].split()[0] if w["weaponName"].split() else ""
        if first in RARITIES:
            _issue(out, t, "error", "no-rarity-in-name",
                   f"weaponName starts with the rarity word {first!r}; the game adds the rolled rarity, store the distinct name only")
        _name_rules(out, t, w["weaponName"], cat, seen)
        dmg = w["baseDamageByRarity"]
        if any(b <= a for a, b in zip(dmg, dmg[1:])):
            _issue(out, t, "error", "rarity-curve", f"baseDamageByRarity must rise each tier: {dmg}")
        for r, v in enumerate(dmg):
            lo, hi = band[w["weaponType"]][r]
            if not _in(v, lo, hi):
                _issue(out, t, "error", "damage-band", f"tier {r} damage {v} outside existing {w['weaponType']} range {lo}–{hi}")
        slo, shi = band["attackSpeed"]
        if not _in(w["attackSpeed"], slo, shi):
            _issue(out, t, "error", "attack-speed", f"attackSpeed {w['attackSpeed']} outside existing {slo}–{shi}")
        # Fast two-handers and slow daggers break the damage-per-second budget.
        if w["isTwoHanded"] and w["attackSpeed"] > 1.2:
            _issue(out, t, "warning", "two-hand-speed", "two-handed weapons are usually ≤1.2 attack speed")
        if not _in(w["scalingCoefficient"], 0.25, 1.0):
            _issue(out, t, "error", "scaling", f"scalingCoefficient {w['scalingCoefficient']} outside 0.25–1.0 (existing weapons use 0.5)")
        c = w["itemColor"]
        if not all(_in(c[k], 0, 1) for k in "rgb"):
            _issue(out, t, "error", "color", "itemColor channels must be 0–1")
        for field, kind in (("possibleAbilities", "ability"), ("possiblePassives", "passive")):
            pool = w[field]
            if len(pool) < 4:
                _issue(out, t, "error", "pool-size", f"{field} needs ≥4 entries (an Exalted roll draws 4), has {len(pool)}")
            if len(pool) != len(set(pool)):
                _issue(out, t, "error", "pool-dupes", f"{field} repeats an entry")
            for pid in pool:
                a = cat.abilities.get(pid)
                if not a or a["kind"] != kind:
                    _issue(out, t, "error", "pool-exists", f"{field}: {pid!r} is not an existing {kind}")
                elif w["weaponType"] not in a["allowedWeaponTypes"]:
                    _issue(out, t, "error", "pool-allowed", f"{pid} can't roll on {w['weaponType']} weapons (allowed: {a['allowedWeaponTypes']})")
    return out


def check_encounters(data: dict, cat: Catalog, ctx: dict) -> list:
    out, seen, ids = [], {}, set()
    want = ctx.get("encounter_count")
    encs = data["encounters"]
    if want and len(encs) != want:
        _issue(out, "encounters", "error", "count", f"asked for {want} encounters, got {len(encs)}")
    region = (ctx.get("region") or {}).get("region")
    band = region["difficulty"] if region else {k: v for k, v in cat.stat_band().items()}
    new_weapons = {w["id"] for w in (ctx.get("weapons") or {}).get("weapons", [])}
    for e in encs:
        t = e["id"]
        if not t.startswith(e["type"] + "_"):
            _issue(out, t, "error", "id-convention", f"id must be {e['type']}_<Name> (memory/content.md)")
        if t in cat.encounters or t in ids:
            _issue(out, t, "error", "unique-id", f"asset id {t} already exists")
        ids.add(t)
        _text_rules(out, t, e, short_fields=("title",), sentence_fields=("description",))
        if e["type"] in FIGHT and len(e["title"].split()) > 3:
            _issue(out, t, "error", "enemy-name-words", f"enemy names are up to three words: {e['title']!r}")
        _name_rules(out, t, e["title"], cat, seen)
        c = e["cardColor"]
        if not all(_in(c[k], 0, 1) for k in "rgb"):
            _issue(out, t, "error", "color", "cardColor channels must be 0–1")
        if e["type"] in FIGHT:
            for key in ("enemyHealth", "enemyAttackDamage", "baseXpReward"):
                lo, hi = band[key]
                if not _in(e[key], lo, hi):
                    _issue(out, t, "error", "region-band", f"{key} {e[key]} outside the region band {band[key]}")
            if not _in(e["enemyAttackSpeed"], 0.5, 1.2):
                _issue(out, t, "error", "attack-speed", f"enemyAttackSpeed {e['enemyAttackSpeed']} outside 0.5–1.2")
            _modifiers(out, t, e["damageTypeModifiers"], "damageTypeModifiers")
        if e["type"] in FIGHT + ("Treasure",):
            _rarity(out, t, e["percentByRarity"])
            if not 1 <= e["minItemCount"] <= e["maxItemCount"] <= 3:
                _issue(out, t, "error", "item-count", "need 1 ≤ minItemCount ≤ maxItemCount ≤ 3")
            _loot_refs(out, t, e, cat, ctx)
        if e["type"] == "Resource":
            th = e["resourceThresholds"]
            if len(th) < 2:
                _issue(out, t, "error", "resource-tiers", "Resource nodes need ≥2 reward thresholds")
            reqs = [x["damageRequired"] for x in th]
            if reqs != sorted(set(reqs)):
                _issue(out, t, "error", "resource-tiers", f"damageRequired must strictly rise: {reqs}")
            for x in th:
                if x["material"] not in cat.materials:
                    _issue(out, t, "error", "loot-exists", f"threshold material {x['material']!r} doesn't exist")
            if not _in(e["resourceTimerSeconds"], 5, 20):
                _issue(out, t, "error", "resource-timer", "resourceTimerSeconds outside 5–20")
            if e["baseXpReward"] or e["goldReward"]:
                _issue(out, t, "warning", "resource-reward", "existing Resource nodes give 0 XP/gold; rewards come from thresholds")
            _modifiers(out, t, e["resourceWeaknesses"], "resourceWeaknesses")
    # Without a recipes stage, a new weapon is only obtainable if some card drops it.
    if "recipes" not in ctx:
        used = {w for e in encs for w in e["lootWeapons"]}
        boss = (ctx.get("region") or {}).get("soloBoss") or {}
        rewired = {w for r in (ctx.get("loot") or {}).get("rewired", []) for w in r["lootWeapons"]}
        for w in new_weapons - used - rewired - set(boss.get("lootWeapons", [])):
            _issue(out, "encounters", "error", "weapon-has-source", f"new weapon {w} isn't in any loot table, so it can never drop")
    return out


EQUIPMENT_SLOTS = {"Armor": {"Head", "Chest", "Legs", "Boots"}, "Shield": {"LeftHand"}, "Talisman": {"Talisman1"}}


def check_equipment(data: dict, cat: Catalog, ctx: dict) -> list:
    out, seen, ids = [], {}, set()
    items = data["equipment"]
    plan = ctx.get("equipment_plan")   # {"Head": 2, ...}
    if plan:
        for slot, n in plan.items():
            got = [q for q in items if q["slotType"] == slot]
            if len(got) != n:
                _issue(out, "equipment", "error", "count", f"asked for {n} {slot} pieces, got {len(got)}")
            elif n >= 2 and len({q["region"] for q in got}) < 2:
                _issue(out, "equipment", "error", "region-spread", f"{slot} pieces should span both Green Valley and Forest")
    band = cat.equipment_power_band()
    for q in items:
        t = q["id"]
        if t in cat.equipment or t in ids or t in cat.id_to_guid and t not in cat.replaced.get("equipment", ()):
            _issue(out, t, "error", "unique-id", f"asset id {t} already exists")
        ids.add(t)
        _text_rules(out, t, q, short_fields=("equipmentName",), sentence_fields=("flavorText",))
        if "-" in q["equipmentName"]:
            _issue(out, t, "error", "no-hyphen", "equipmentName: names never use hyphens")
        if q["equipmentName"].split() and q["equipmentName"].split()[0] in RARITIES:
            _issue(out, t, "error", "no-rarity-in-name", "equipmentName starts with a rarity word")
        _name_rules(out, t, q["equipmentName"], cat, seen)
        if q["slotType"] not in EQUIPMENT_SLOTS[q["category"]]:
            _issue(out, t, "error", "slot-category", f"{q['category']} can't go in slot {q['slotType']} (allowed {sorted(EQUIPMENT_SLOTS[q['category']])})")
        p = q["powerByRarity"]
        if any(b <= a for a, b in zip(p, p[1:])):
            _issue(out, t, "error", "rarity-curve", f"powerByRarity must rise each tier: {p}")
        if band:
            for r, v in enumerate(p):
                lo, hi = band[r]
                if not _in(v, lo, hi):
                    _issue(out, t, "error", "power-band", f"tier {r} power {v} outside {lo}–{hi} (bands.json)")
        c = q["itemColor"]
        if not all(_in(c[k], 0, 1) for k in "rgb"):
            _issue(out, t, "error", "color", "itemColor channels must be 0–1")
        pool = q["possiblePassives"]
        if len(pool) < 4:
            _issue(out, t, "error", "pool-size", f"possiblePassives needs ≥4 entries (an Exalted roll draws 4), has {len(pool)}")
        if len(pool) != len(set(pool)):
            _issue(out, t, "error", "pool-dupes", "possiblePassives repeats an entry")
        for pid in pool:
            a = cat.abilities.get(pid)
            if not a or a["kind"] != "passive":
                _issue(out, t, "error", "pool-exists", f"possiblePassives: {pid!r} is not an existing passive")
    return out


# Region tier sets what rarity a recipe may craft: early gear is crafted low, Exalted never.
CRAFT_RARITY = {"Green Valley": {"Plain", "Tempered"}, "Forest": {"Tempered", "Runed", "Relic"}}


def check_recipes(data: dict, cat: Catalog, ctx: dict) -> list:
    out = []
    recipes = data["recipes"]
    new_w = {w["id"]: w for w in (ctx.get("weapons") or {}).get("weapons", [])}
    new_e = {q["id"]: q for q in (ctx.get("equipment") or {}).get("equipment", [])}
    by_id = {r["id"]: r for r in recipes}
    if len(by_id) != len(recipes):
        _issue(out, "recipes", "error", "unique-id", "two recipes share an id")
    outputs = [r["output"] for r in recipes]
    need = {i for i, w in new_w.items() if not w.get("isStarter")} | set(new_e)
    for i in sorted(need - set(outputs)):
        _issue(out, i, "error", "recipe-coverage", f"{i} has no recipe")
    for i in sorted({o for o in outputs if outputs.count(o) > 1}):
        _issue(out, i, "error", "recipe-coverage", f"{i} has more than one recipe")
    band = cat.recipe_band() or {}
    existing = {k: v for k, v in cat.recipes.items()}
    for r in recipes:
        t = r["id"]
        if t in existing:
            _issue(out, t, "error", "unique-id", f"recipe id {t} already exists")
        item = new_w.get(r["output"]) if r["outputKind"] == "Weapon" else new_e.get(r["output"])
        if not item:
            _issue(out, t, "error", "recipe-output", f"output {r['output']!r} is not a new {r['outputKind'].lower()} from this run")
            continue
        if item.get("isStarter"):
            _issue(out, t, "error", "recipe-output", "the starter weapon is given at new game, not crafted")
        if t != f"Recipe_{r['output']}":
            _issue(out, t, "error", "id-convention", f"recipe id must be Recipe_{r['output']} (memory/content.md)")
        if r["outputRarity"] not in CRAFT_RARITY[item["region"]]:
            _issue(out, t, "error", "craft-rarity", f"{item['region']} gear crafts at {sorted(CRAFT_RARITY[item['region']])}, not {r['outputRarity']}")
        b = band.get(r["outputRarity"])
        if b:
            lo, hi = b["goldCost"]
            if not _in(r["goldCost"], int(lo * 0.75), int(hi * 1.25 + 0.5)):
                _issue(out, t, "error", "gold-band", f"goldCost {r['goldCost']} outside {r['outputRarity']} range {lo}–{hi} (±25%)")
            total = sum(i["count"] for i in r["ingredients"])
            lo, hi = b["totalIngredients"]
            if not _in(total, max(2, int(lo * 0.75)), int(hi * 1.25 + 0.5)):
                _issue(out, t, "error", "ingredient-band", f"{total} total ingredients outside {r['outputRarity']} range {lo}–{hi} (±25%)")
        if not 1 <= len(r["ingredients"]) <= 4:
            _issue(out, t, "error", "ingredients", "recipes use 1–4 different materials")
        for i in r["ingredients"]:
            if i["material"] not in cat.materials:
                _issue(out, t, "error", "ingredient-exists", f"material {i['material']!r} doesn't exist")
            if not 1 <= i["count"] <= 20:
                _issue(out, t, "error", "ingredients", f"{i['material']} count {i['count']} outside 1–20")
        for p in r["prerequisites"]:
            pre = by_id.get(p) or existing.get(p)
            if not pre:
                _issue(out, t, "error", "prereq-exists", f"prerequisite {p!r} is neither in this batch nor an existing recipe")
            elif RARITIES.index(pre["outputRarity"]) > RARITIES.index(r["outputRarity"]):
                _issue(out, t, "error", "prereq-order", f"prerequisite {p} crafts a higher rarity than this recipe")
    # No cycles in the recipe tree.
    def visit(i, stack):
        if i in stack:
            return True
        r = by_id.get(i)
        return bool(r) and any(visit(p, stack | {i}) for p in r["prerequisites"])
    for i in by_id:
        if visit(i, frozenset()):
            _issue(out, i, "error", "prereq-cycle", "prerequisites form a cycle")
            break
    return out


def check_loot(data: dict, cat: Catalog, ctx: dict) -> list:
    out = []
    rows = {r["encounter"]: r for r in data["rewired"]}
    for cid in sorted(set(cat.rewire) - set(rows)):
        _issue(out, cid, "error", "rewire-coverage", f"{cid} dropped replaced items and needs new loot")
    for cid, r in rows.items():
        if cid not in cat.rewire:
            _issue(out, cid, "error", "rewire-coverage", f"{cid} isn't a card that needs rewiring")
            continue
        _text_rules(out, cid, {"reason": r["reason"]})
        _loot_refs(out, cid, r, cat, ctx)
        if not r["lootWeapons"] and not r["lootEquipment"]:
            _issue(out, cid, "error", "rewire-empty", "give the card at least one new weapon or equipment drop")
        if len(r["lootWeapons"]) + len(r["lootEquipment"]) > 4:
            _issue(out, cid, "warning", "rewire-size", "more than 4 named drops dilutes each one")
    return out


CHECKS = {"weapons": check_weapons, "equipment": check_equipment, "recipes": check_recipes,
          "loot": check_loot, "region": check_region, "encounters": check_encounters}
