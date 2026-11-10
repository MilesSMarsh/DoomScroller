"""Reads the game's authored content straight from the Unity assets under
Assets/Resources/ so the generator and the checks see what is actually in the game.

Stdlib only: a small parser for the subset of YAML Unity writes for ScriptableObjects.

Replace mode (Catalog(replace={"weapon", "equipment"})): the listed kinds are about to be
deleted and re-authored, so they (and the recipes that craft them) are hidden from the
agents and from name-collision checks, and the cards that drop them are recorded in
`rewire` so the loot stage can point them at the new items.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
RESOURCES = REPO / "Assets" / "Resources"
BANDS = Path(__file__).resolve().parent / "bands.json"

ENCOUNTER_TYPES = ["Treasure", "Enemy", "Dungeon", "Tavern", "Resource", "Landing",
                   "WanderingMerchant", "Ambush"]
DAMAGE_TYPES = ["Slash", "Blunt", "Pierce", "Arcane", "Elemental"]
WEAPON_TYPES = ["Melee", "Ranged", "Magic"]
ATTRIBUTES = ["Constitution", "Agility", "Spirit", "Expertise"]
RARITIES = ["Plain", "Tempered", "Runed", "Relic", "Exalted"]
EFFECT_TYPES = ["DamageOverTime", "WeakenDamage", "SlowAttackSpeed", "HasteAttackSpeed",
                "EmpowerDamage", "ArmorUp"]
ITEM_CATEGORIES = ["Armor", "Weapon", "Shield", "Talisman", "Potion", "Material"]
SLOTS = ["Head", "Chest", "Legs", "Boots", "LeftHand", "RightHand", "Talisman1", "Talisman2"]
RECIPE_KINDS = ["FixedItem", "Weapon", "Equipment", "Material"]


# ---------------------------------------------------------------- Unity YAML subset

def _scalar(text: str):
    text = text.strip()
    if text == "[]":
        return []
    if text.startswith("{") and text.endswith("}"):
        return {k: v.strip() for k, v in re.findall(r"(\w+):\s*([^,}]*)", text)}
    if text.startswith('"') and text.endswith('"'):
        return json.loads(text)
    if text.startswith("'") and text.endswith("'"):
        return text[1:-1].replace("''", "'")
    return text


def _map(toks, i, ind):
    out = {}
    while i < len(toks) and toks[i][0] == ind and not toks[i][1].startswith("- "):
        key, _, rest = toks[i][1].partition(":")
        rest = rest.strip()
        i += 1
        if rest:
            # Unity wraps long plain/quoted strings onto deeper-indented lines.
            while i < len(toks) and toks[i][0] > ind:
                rest += " " + toks[i][1]
                i += 1
            out[key] = _scalar(rest)
        elif i < len(toks) and (toks[i][0] > ind or
                                (toks[i][0] == ind and toks[i][1].startswith("- "))):
            out[key], i = _node(toks, i, toks[i][0])
        else:
            out[key] = ""
    return out, i


def _list(toks, i, ind):
    out = []
    while i < len(toks) and toks[i][0] == ind and toks[i][1].startswith("- "):
        content = toks[i][1][2:]
        if re.match(r"^[A-Za-z_]\w*:( |$)", content):
            toks[i] = (ind + 2, content)
            item, i = _map(toks, i, ind + 2)
            out.append(item)
        else:
            out.append(_scalar(content))
            i += 1
    return out, i


def _node(toks, i, ind):
    if toks[i][1].startswith("- "):
        return _list(toks, i, ind)
    return _map(toks, i, ind)


def parse_asset(path: Path) -> dict:
    raw = path.read_text(encoding="utf-8").splitlines()
    start = next(k for k, line in enumerate(raw) if line.startswith("MonoBehaviour:"))
    toks = [(len(line) - len(line.lstrip(" ")), line.strip())
            for line in raw[start + 1:] if line.strip()]
    value, _ = _node(toks, 0, toks[0][0])
    return value


def read_guid(asset_path: Path) -> str:
    meta = asset_path.with_name(asset_path.name + ".meta").read_text()
    return re.search(r"^guid:\s*(\w+)", meta, re.M).group(1)


def _num(v, default=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def _int_array(hexstr) -> list[int]:
    """Unity serializes int/enum arrays as little-endian int32 hex, e.g. 0000000002000000."""
    if isinstance(hexstr, list):
        return [int(_num(x)) for x in hexstr]
    hexstr = (hexstr or "").strip()
    return [int.from_bytes(bytes.fromhex(hexstr[k:k + 8]), "little", signed=True)
            for k in range(0, len(hexstr), 8)]


def _refs(lst):
    return [r.get("guid") for r in (lst or []) if isinstance(r, dict) and r.get("guid")]


def _ref(r):
    return r.get("guid") if isinstance(r, dict) and r.get("guid") else None


def _round(x):
    return int(x) if float(x).is_integer() else round(float(x), 3)


def load_bands() -> dict:
    return json.loads(BANDS.read_text()) if BANDS.exists() else {}


# ---------------------------------------------------------------- catalog

class Catalog:
    """Everything authored in Assets/Resources, keyed by asset id (file stem)."""

    def __init__(self, resources: Path = RESOURCES, replace=()):
        self.resources = resources
        self.replace = set(replace)
        self.guid_to_id: dict[str, str] = {}
        self.id_to_guid: dict[str, str] = {}
        self.path_of: dict[str, Path] = {}
        self.script_ref: dict[str, dict] = {}   # kind -> m_Script + class identifier
        raw = {kind: self._load(folder, kind) for kind, folder in [
            ("encounter", "Encounters"), ("weapon", "WeaponDefinitions"),
            ("ability", "Abilities"), ("material", "MaterialDefinitions"),
            ("equipment", "EquipmentDefinitions"), ("recipe", "RecipeDefinitions")]}
        self.abilities = self._abilities(raw["ability"])
        self.materials = {k: {"id": k, "name": d.get("materialName") or k,
                              "goldValue": _round(_num(d.get("goldValue")))}
                          for k, d in raw["material"].items()}
        all_weapons = {k: self._weapon(k, d) for k, d in raw["weapon"].items()}
        all_equipment = {k: self._equipment(k, d) for k, d in raw["equipment"].items()}
        all_recipes = {k: self._recipe(k, d) for k, d in raw["recipe"].items()}
        # What replace mode will delete: the items, and recipes that craft them.
        self.replaced = {
            "weapon": set(all_weapons) if "weapon" in self.replace else set(),
            "equipment": set(all_equipment) if "equipment" in self.replace else set(),
        }
        self.replaced["recipe"] = {
            k for k, r in all_recipes.items()
            if (r["outputKind"] == "Weapon" and r["output"] in self.replaced["weapon"])
            or (r["outputKind"] == "Equipment" and r["output"] in self.replaced["equipment"])}
        self.weapons = {k: v for k, v in all_weapons.items() if k not in self.replaced["weapon"]}
        self.equipment = {k: v for k, v in all_equipment.items() if k not in self.replaced["equipment"]}
        self.recipes = {k: v for k, v in all_recipes.items() if k not in self.replaced["recipe"]}
        self._weapon_band_source = list(all_weapons.values())
        self.rewire: dict[str, dict] = {}
        self.encounters = {k: self._encounter(k, d) for k, d in raw["encounter"].items()}

    def _load(self, folder, kind):
        out = {}
        for path in sorted((self.resources / folder).glob("*.asset")):
            data = parse_asset(path)
            guid = read_guid(path)
            self.guid_to_id[guid] = path.stem
            self.id_to_guid[path.stem] = guid
            self.path_of[path.stem] = path
            sub = kind
            if kind == "ability":
                sub = "passive" if "effect" in data else "ability"
            if sub not in self.script_ref and data.get("m_Script"):
                self.script_ref[sub] = {"guid": data["m_Script"]["guid"],
                                        "classId": data.get("m_EditorClassIdentifier", "")}
            out[path.stem] = data
        return out

    def _ids(self, guids):
        return [self.guid_to_id.get(g, g) for g in guids]

    def _abilities(self, raw):
        out = {}
        for k, d in raw.items():
            allowed = [WEAPON_TYPES[t] for t in _int_array(d.get("allowedWeaponTypes"))
                       if 0 <= t < len(WEAPON_TYPES)]
            if "effect" in d:
                e = d["effect"]
                out[k] = {"id": k, "kind": "passive", "name": e.get("passiveName"),
                          "description": e.get("description"), "allowedWeaponTypes": allowed,
                          "effect": EFFECT_TYPES[int(_num(e.get("effectType")))],
                          "magnitude": _round(_num(e.get("magnitude")))}
            else:
                a = d.get("data", {})
                out[k] = {"id": k, "kind": "ability", "name": a.get("abilityName"),
                          "description": a.get("description"), "allowedWeaponTypes": allowed,
                          "powerBonus": _round(_num(a.get("powerBonus"))),
                          "cooldownSeconds": _round(_num(a.get("cooldownSeconds")))}
        return out

    def _weapon(self, k, d):
        stats = d.get("statsByRarity") or []
        return {
            "id": k, "name": d.get("weaponName"),
            "weaponType": WEAPON_TYPES[int(_num(d.get("weaponType")))],
            "damageType": DAMAGE_TYPES[int(_num(d.get("damageType")))],
            "isTwoHanded": d.get("isTwoHanded") == "1",
            "attackSpeed": _round(_num(d.get("attackSpeed"), 1)),
            "baseDamageByRarity": [int(_num(s.get("baseDamage"))) for s in stats],
            "scaling": sorted({ATTRIBUTES[int(_num(a.get("attribute")))]
                               for s in stats for a in (s.get("attributeScalingEntries") or [])}),
            "possibleAbilities": self._ids(_refs(d.get("possibleAbilities"))),
            "possiblePassives": self._ids(_refs(d.get("possiblePassives"))),
        }

    def _equipment(self, k, d):
        return {"id": k, "name": d.get("equipmentName"),
                "category": ITEM_CATEGORIES[int(_num(d.get("category")))],
                "slotType": SLOTS[int(_num(d.get("slotType")))],
                "powerByRarity": _int_array(d.get("powerByRarity")),
                "possiblePassives": self._ids(_refs(d.get("possiblePassives")))}

    def _recipe(self, k, d):
        kind = RECIPE_KINDS[int(_num(d.get("outputKind")))]
        field = {"Weapon": "outputWeapon", "Equipment": "outputEquipment",
                 "Material": "outputMaterial", "FixedItem": "outputItem"}[kind]
        out = _ref(d.get(field))
        return {"id": k, "outputKind": kind, "output": self.guid_to_id.get(out, out),
                "outputRarity": RARITIES[int(_num(d.get("outputRarity")))],
                "goldCost": int(_num(d.get("goldCost"))),
                "ingredients": [{"material": self.guid_to_id.get(_ref(i.get("material")), "?"),
                                 "count": int(_num(i.get("count")))} for i in d.get("ingredients") or []],
                "prerequisites": self._ids(_refs(d.get("prerequisiteRecipes")))}

    def _loot(self, k, loot):
        weapons = self._ids(_refs(loot.get("weapons")))
        equipment = self._ids(_refs(loot.get("equipment")))
        gone_w = [w for w in weapons if w in self.replaced["weapon"]]
        gone_e = [e for e in equipment if e in self.replaced["equipment"]]
        if gone_w or gone_e:
            self.rewire[k] = {"replacedWeapons": gone_w, "replacedEquipment": gone_e}
        return {
            "percentByRarity": [_round(_num(p)) for p in loot.get("percentByRarity") or []],
            "minItemCount": int(_num(loot.get("minItemCount"), 1)),
            "maxItemCount": int(_num(loot.get("maxItemCount"), 1)),
            "weapons": [w for w in weapons if w not in self.replaced["weapon"]],
            "equipment": [e for e in equipment if e not in self.replaced["equipment"]],
            "materials": self._ids(_refs(loot.get("materials"))),
        }

    def _encounter(self, k, d):
        etype = ENCOUNTER_TYPES[int(_num(d.get("type")))]
        e = {"id": k, "title": d.get("title"), "type": etype,
             "description": d.get("description") or d.get("rewardSummary"),
             "baseXpReward": int(_num(d.get("baseXpReward"))),
             "goldReward": int(_num(d.get("goldReward")))}
        if etype in ("Enemy", "Ambush", "Treasure", "Dungeon"):
            e["loot"] = self._loot(k, d.get("lootTable") or {})
        if etype in ("Enemy", "Ambush"):
            e.update(enemyHealth=int(_num(d.get("enemyHealth"))),
                     enemyAttackDamage=int(_num(d.get("enemyAttackDamage"))),
                     enemyAttackSpeed=_round(_num(d.get("enemyAttackSpeed"))),
                     damageTypeModifiers=[{"type": DAMAGE_TYPES[int(_num(m.get("type")))],
                                           "multiplier": _round(_num(m.get("multiplier")))}
                                          for m in d.get("damageTypeModifiers") or []])
        if etype == "Resource":
            e.update(resourceTimerSeconds=_round(_num(d.get("resourceTimerSeconds"), 10)),
                     resourceWeaknesses=[{"type": DAMAGE_TYPES[int(_num(m.get("type")))],
                                          "multiplier": _round(_num(m.get("multiplier")))}
                                         for m in d.get("resourceWeaknesses") or []],
                     resourceThresholds=[{"damageRequired": int(_num(t.get("damageRequired"))),
                                          "material": self.guid_to_id.get(
                                              (t.get("rewardMaterial") or {}).get("guid"), "?"),
                                          "rarity": RARITIES[int(_num(t.get("rewardRarity")))],
                                          "amount": int(_num(t.get("rewardAmount"), 1))}
                                         for t in d.get("resourceThresholds") or []])
        if etype == "Dungeon":
            e["roomCount"] = len(d.get("dungeonRooms") or [])
        return e

    # ------------------------------------------------------------ derived facts

    def fight_cards(self):
        return [e for e in self.encounters.values() if e["type"] in ("Enemy", "Ambush")]

    def stat_band(self):
        """The current feed's power band (all existing content is region 1 today)."""
        f = self.fight_cards()

        def rng(key):
            vals = [e[key] for e in f]
            return [min(vals), max(vals)]
        return {"enemyHealth": rng("enemyHealth"), "enemyAttackDamage": rng("enemyAttackDamage"),
                "enemyAttackSpeed": rng("enemyAttackSpeed"), "baseXpReward": rng("baseXpReward")}

    def weapon_damage_band(self):
        """Per weapon type: [min, max] base damage at each rarity. Read from bands.json when
        present (it survives the weapons being replaced), else measured from the assets."""
        saved = load_bands().get("weaponDamage")
        if saved:
            return saved
        out = {}
        for wt in WEAPON_TYPES:
            ws = [w for w in self._weapon_band_source
                  if w["weaponType"] == wt and len(w["baseDamageByRarity"]) == 5]
            if ws:
                out[wt] = [[min(w["baseDamageByRarity"][r] for w in ws),
                            max(w["baseDamageByRarity"][r] for w in ws)] for r in range(5)]
        speeds = [w["attackSpeed"] for w in self._weapon_band_source]
        out["attackSpeed"] = [min(speeds), max(speeds)]
        return out

    def equipment_power_band(self):
        return load_bands().get("equipmentPower")

    def recipe_band(self):
        return load_bands().get("recipes")

    def all_names(self):
        names = {}
        for e in self.encounters.values():
            names[_norm(e["title"] or "")] = e["id"]
        for w in self.weapons.values():
            names[_norm(w["name"] or "")] = w["id"]
        for q in self.equipment.values():
            names[_norm(q["name"] or "")] = q["id"]
        for a in self.abilities.values():
            names[_norm(a["name"] or "")] = a["id"]
        for m in self.materials.values():
            names[_norm(m["name"])] = m["id"]
        names.pop("", None)
        return names

    def summary(self) -> dict:
        """The compact view of the game's content handed to the agents."""
        out = {
            "encounters": list(self.encounters.values()),
            "weapons": list(self.weapons.values()),
            "equipment": list(self.equipment.values()),
            "recipes": list(self.recipes.values()),
            "abilitiesAndPassives": list(self.abilities.values()),
            "materials": list(self.materials.values()),
            "currentPowerBand": self.stat_band(),
            "weaponDamageBand": self.weapon_damage_band(),
        }
        if self.equipment_power_band():
            out["equipmentPowerBand"] = self.equipment_power_band()
        if self.recipe_band():
            out["recipeBand"] = self.recipe_band()
        if self.replace:
            out["beingReplaced"] = {
                "note": "These kinds are being deleted and re-authored in this run; the old "
                        "items are gone. Cards in cardsToRewire dropped them and need new loot.",
                "kinds": sorted(self.replace), "cardsToRewire": self.rewire}
        return out


def _norm(name: str) -> str:
    return re.sub(r"[^a-z0-9]", "", name.lower())


if __name__ == "__main__":
    print(json.dumps(Catalog().summary(), indent=1)[:6000])
