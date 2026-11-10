"""Offline tests (no LLM calls): python3 -m unittest discover tools/content-pipeline/tests"""
import copy
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import catalog  # noqa: E402
import install  # noqa: E402
import pipeline  # noqa: E402
from checks import (check_encounters, check_equipment, check_loot, check_recipes,  # noqa: E402
                    check_region, check_weapons)

CAT = catalog.Catalog()

WEAPON = {
    "id": "TestThornbow", "weaponName": "Testing Thornbow", "flavorText": "A bow of green thorn.",
    "dropsFrom": "a test", "weaponType": "Ranged", "damageType": "Pierce", "isTwoHanded": True,
    "attackSpeed": 1.0, "itemColor": {"r": 0.2, "g": 0.6, "b": 0.3},
    "baseDamageByRarity": [2, 4, 8, 16, 32], "scalingAttribute": "Agility", "scalingCoefficient": 0.5,
    "possibleAbilities": ["Ability_PiercingShot", "Ability_Volley", "Ability_Hamstring", "Ability_Execute"],
    "possiblePassives": ["Passive_Agile", "Passive_Sureshot", "Passive_Crippling", "Passive_Vicious"],
}
ENEMY = {
    "id": "Enemy_TestGrub", "title": "Testing Grub", "type": "Enemy",
    "description": "A pale grub the size of a dog, chewing on a boot.",
    "cardColor": {"r": 0.4, "g": 0.4, "b": 0.3}, "baseXpReward": 60, "goldReward": 12,
    "percentByRarity": [60, 30, 10, 0, 0], "minItemCount": 1, "maxItemCount": 1,
    "lootWeapons": ["TestThornbow"], "lootMaterials": ["Fibers"],
    "enemyHealth": 60, "enemyAttackDamage": 7, "enemyAttackSpeed": 0.8,
    "damageTypeModifiers": [{"type": "Slash", "multiplier": 1.5}],
    "resourceTimerSeconds": 10, "resourceWeaknesses": [], "resourceThresholds": [],
}
RESOURCE = {
    **ENEMY, "id": "Resource_TestBramble", "title": "Testing Bramble", "type": "Resource",
    "description": "A bramble heavy with tough fibre.", "baseXpReward": 0, "goldReward": 0,
    "lootWeapons": [], "lootMaterials": [], "damageTypeModifiers": [],
    "resourceWeaknesses": [{"type": "Slash", "multiplier": 2}],
    "resourceThresholds": [{"damageRequired": 20, "material": "Fibers", "rarity": "Plain", "amount": 3},
                           {"damageRequired": 60, "material": "Wood", "rarity": "Plain", "amount": 2}],
}


def rules(issues, severity="error"):
    return {i["rule"] for i in issues if i["severity"] == severity}


class CatalogTests(unittest.TestCase):
    def test_reads_every_asset(self):
        self.assertEqual(len(CAT.encounters), len(list((catalog.RESOURCES / "Encounters").glob("*.asset"))))
        self.assertIn("WoodenSword", CAT.weapons)
        self.assertEqual(CAT.encounters["Enemy_Wolf"]["enemyHealth"], 20)
        self.assertEqual(CAT.abilities["Passive_Agile"]["allowedWeaponTypes"], ["Melee", "Ranged", "Magic"])

    def test_resource_thresholds_resolve_materials(self):
        th = CAT.encounters["Resource_IronVein"]["resourceThresholds"]
        self.assertEqual(th[0]["material"], "IronOre")


class CheckTests(unittest.TestCase):
    def test_good_weapon_passes(self):
        self.assertEqual(rules(check_weapons({"weapons": [WEAPON]}, CAT, {})), set())

    def test_bad_weapon_caught(self):
        w = copy.deepcopy(WEAPON)
        w["weaponName"] = "Iron Sword"                       # existing name
        w["baseDamageByRarity"] = [2, 2, 8, 16, 90]          # flat tier + over band
        w["possibleAbilities"] = ["Ability_Frostbind", "Ability_Nope"]  # Magic-only + unknown
        w["flavorText"] = "TODO write this"
        got = rules(check_weapons({"weapons": [w]}, CAT, {}))
        self.assertTrue({"unique-name", "rarity-curve", "damage-band", "pool-allowed",
                         "pool-exists", "pool-size", "no-placeholder"} <= got, got)

    def test_encounters_with_new_weapon(self):
        ctx = {"weapons": {"weapons": [WEAPON]}}
        self.assertEqual(rules(check_encounters({"encounters": [ENEMY, RESOURCE]}, CAT, ctx)), set())

    def test_unreferenced_weapon_and_bad_ids(self):
        e = copy.deepcopy(ENEMY)
        e["id"] = "Ambush_TestGrub"                          # id/type mismatch
        e["lootWeapons"] = []
        e["enemyHealth"] = 9999
        e["description"] = "One. Two."
        got = rules(check_encounters({"encounters": [e]}, CAT, {"weapons": {"weapons": [WEAPON]}}))
        self.assertTrue({"id-convention", "weapon-has-source", "region-band", "one-sentence"} <= got, got)

    def test_brief_gate_limits(self):
        e = copy.deepcopy(ENEMY)
        e["title"] = "Grisk Goblin of the Long Valley Road"   # 36 chars, 7 words
        e["description"] = "A goblin that will follow travellers for days."
        got = rules(check_encounters({"encounters": [e]}, CAT, {"weapons": {"weapons": [WEAPON]}}))
        self.assertTrue({"title-length", "banned-word", "enemy-name-words"} <= got, got)
        e["title"] = "Grisk Goblin Sentry Captain"           # 27 chars, 4 words: length ok
        self.assertNotIn("title-length", rules(check_encounters({"encounters": [e]}, CAT, {})))

    def test_title_of_35_chars_allowed(self):
        e = dict(ENEMY, id="Treasure_TestHoard", type="Treasure", title="A" * 35)
        self.assertNotIn("title-length", rules(check_encounters({"encounters": [e]}, CAT, {})))

    def test_info_cards_exempt_from_banned_words(self):
        from checks import _text_rules
        out = []
        _text_rules(out, "Info_Test", {"type": "Info", "description": "Scroll the feed to travel."})
        self.assertNotIn("banned-word", rules(out))
        out = []
        _text_rules(out, "Enemy_Test", {"type": "Enemy", "description": "Scroll the feed to travel."})
        self.assertIn("banned-word", rules(out))

    def test_feed_allowed_as_verb_only(self):
        from checks import _text_rules
        for text, banned in [("A tangle of webbed brambles where giant spiders nest and feed.", False),
                             ("A crow that feeds on carrion.", False),
                             ("Wolves feed where the herds graze.", False),
                             ("A wanderer lost in the feed.", True),
                             ("Gold left behind by your feed.", True)]:
            out = []
            _text_rules(out, "Enemy_T", {"type": "Enemy", "description": text})
            self.assertEqual("banned-word" in rules(out), banned, text)

    def test_weapon_naming_rules(self):
        w = dict(WEAPON, weaponName="Tempered Thorn-Bow")
        got = rules(check_weapons({"weapons": [w]}, CAT, {}))
        self.assertTrue({"no-rarity-in-name", "no-hyphen"} <= got, got)

    def test_region_boss_must_exceed_band(self):
        region = {
            "region": {"id": "Region_Test", "name": "Testing Vale", "tagline": "t", "description": "d",
                       "hiddenSatire": "s", "difficulty": {"enemyHealth": [20, 110], "enemyAttackDamage": [5, 12],
                                                           "baseXpReward": [50, 200]},
                       "existingCards": ["Enemy_Wolf", "Resource_IronVein", "Treasure_GlitteringChest",
                                         "Enemy_HollowCrownRevenant"], "cardMix": "m"},
            "soloBoss": {"id": "Boss_Test", "name": "Testing Tyrant", "epithet": "e", "description": "A test.",
                         "introLine": "i", "victoryLine": "v", "defeatLine": "d", "enemyHealth": 100,
                         "enemyAttackDamage": 12, "enemyAttackSpeed": 0.9, "hpRegenPercentPerHour": 5,
                         "damageTypeModifiers": [], "baseXpReward": 300, "goldReward": 50,
                         "percentByRarity": [0, 0, 50, 40, 10], "lootWeapons": ["IronSword"],
                         "lootMaterials": [], "keyItem": {"name": "Testing Key", "description": "k"}},
        }
        got = rules(check_region(region, CAT, {}))
        self.assertIn("boss-above-band", got)     # 100 HP ≤ band top 110
        self.assertIn("card-in-band", got)        # Revenant (250 HP) outside the band


class InstallTests(unittest.TestCase):
    def test_yaml_round_trips_and_matches_unity_fields(self):
        guids = dict(CAT.id_to_guid, TestThornbow="ab" * 16)
        with tempfile.TemporaryDirectory() as d:
            wp, ep, rp = Path(d, "w.asset"), Path(d, "e.asset"), Path(d, "r.asset")
            wp.write_text(install.weapon_yaml(WEAPON, CAT))
            ep.write_text(install.encounter_yaml(ENEMY, CAT, guids))
            rp.write_text(install.encounter_yaml(RESOURCE, CAT, guids))
            w, e, r = (catalog.parse_asset(p) for p in (wp, ep, rp))
        self.assertEqual(w["weaponName"], "Testing Thornbow")
        self.assertEqual([s["baseDamage"] for s in w["statsByRarity"]], ["2", "4", "8", "16", "32"])
        self.assertEqual(len(w["possiblePassives"]), 4)
        self.assertEqual(e["lootTable"]["weapons"][0]["guid"], "ab" * 16)
        self.assertEqual(r["resourceThresholds"][1]["rewardMaterial"]["guid"], CAT.id_to_guid["Wood"])
        # Same serialized field set Unity writes for real assets.
        real_w = catalog.parse_asset(catalog.RESOURCES / "WeaponDefinitions" / "FrostHuntersBow.asset")
        real_e = catalog.parse_asset(catalog.RESOURCES / "Encounters" / "Enemy_Wolf.asset")
        self.assertEqual(set(w), set(real_w))
        self.assertEqual(set(e), set(real_e))
        self.assertEqual(set(e["lootTable"]), set(real_e["lootTable"]))

    def test_quotes_escape(self):
        w = dict(WEAPON, weaponName="Hunter's 'Thorn'")
        with tempfile.TemporaryDirectory() as d:
            p = Path(d, "w.asset")
            p.write_text(install.weapon_yaml(w, CAT))
            self.assertEqual(catalog.parse_asset(p)["weaponName"], "Hunter's 'Thorn'")


EQUIP = {"id": "TestReedHelm", "equipmentName": "Testing Reed Helm", "flavorText": "Woven reeds over a leather cap.",
         "region": "Green Valley", "category": "Armor", "slotType": "Head", "itemColor": {"r": 0.5, "g": 0.5, "b": 0.3},
         "powerByRarity": [6, 10, 16, 24, 36],
         "possiblePassives": ["Passive_Thickhide", "Passive_Bulwark", "Passive_Reinforced", "Passive_Ironwilled"]}
STARTER = dict(WEAPON, id="TestSpade", weaponName="Testing Spade", region="Green Valley", isStarter=True,
               weaponType="Melee", damageType="Blunt", isTwoHanded=False,
               possibleAbilities=["Ability_Slash", "Ability_Cleave", "Ability_Sunder", "Ability_Riposte"],
               possiblePassives=["Passive_Agile", "Passive_Vicious", "Passive_Hefty", "Passive_DamageUp"])
GV_WEAPON = dict(WEAPON, region="Green Valley", isStarter=False)
RECIPE = {"id": "Recipe_TestThornbow", "outputKind": "Weapon", "output": "TestThornbow", "outputRarity": "Plain",
          "goldCost": 15, "ingredients": [{"material": "Wood", "count": 3}, {"material": "Rope", "count": 2}],
          "prerequisites": ["Recipe_Rope"]}
RECIPE_EQ = {"id": "Recipe_TestReedHelm", "outputKind": "Equipment", "output": "TestReedHelm", "outputRarity": "Tempered",
             "goldCost": 26, "ingredients": [{"material": "Fibers", "count": 4}, {"material": "WolfPelt", "count": 2}],
             "prerequisites": ["Recipe_TestThornbow"]}


class GearStageTests(unittest.TestCase):
    CTX = {"weapons": {"weapons": [GV_WEAPON, STARTER]}, "equipment": {"equipment": [EQUIP]}}

    def test_good_equipment_passes(self):
        self.assertEqual(rules(check_equipment({"equipment": [EQUIP]}, CAT, {})), set())

    def test_bad_equipment_caught(self):
        e = dict(EQUIP, slotType="LeftHand", powerByRarity=[6, 5, 16, 24, 400], possiblePassives=["Ability_Slash"])
        got = rules(check_equipment({"equipment": [e]}, CAT, {"equipment_plan": {"Head": 2}}))
        self.assertTrue({"slot-category", "rarity-curve", "power-band", "pool-exists", "pool-size", "count"} <= got, got)

    def test_good_recipes_pass(self):
        self.assertEqual(rules(check_recipes({"recipes": [RECIPE, RECIPE_EQ]}, CAT, self.CTX)), set())

    def test_recipe_rules(self):
        bad = dict(RECIPE, outputRarity="Exalted", goldCost=999, prerequisites=["Recipe_TestReedHelm"])
        eq = dict(RECIPE_EQ, prerequisites=["Recipe_TestThornbow"])
        starter = dict(RECIPE, id="Recipe_TestSpade", output="TestSpade")
        got = rules(check_recipes({"recipes": [bad, eq, starter]}, CAT, self.CTX))
        self.assertTrue({"craft-rarity", "gold-band", "prereq-cycle", "recipe-output"} <= got, got)
        got = rules(check_recipes({"recipes": [RECIPE]}, CAT, self.CTX))
        self.assertIn("recipe-coverage", got)   # the helm has no recipe

    def test_starter_rules(self):
        two = dict(GV_WEAPON, id="TestOther", weaponName="Testing Other", isStarter=True)
        got = rules(check_weapons({"weapons": [STARTER, two]}, CAT, {"require_starter": True}))
        self.assertIn("starter", got)
        self.assertNotIn("starter", rules(check_weapons({"weapons": [STARTER, GV_WEAPON]}, CAT, {"require_starter": True})))

    def test_loot_rewire_covers_every_card(self):
        cat = catalog.Catalog(replace={"weapon", "equipment"})
        self.assertIn("Enemy_BonewroughtSentinel", cat.rewire)
        rows = [{"encounter": c, "lootWeapons": ["TestThornbow"], "lootEquipment": [], "reason": "fits"}
                for c in cat.rewire]
        self.assertEqual(rules(check_loot({"rewired": rows}, cat, self.CTX)), set())
        got = rules(check_loot({"rewired": rows[1:] + [dict(rows[0], lootWeapons=["Nope"])]}, cat, self.CTX))
        self.assertIn("loot-exists", got)
        self.assertIn("rewire-coverage", rules(check_loot({"rewired": rows[1:]}, cat, self.CTX)))

    def test_replace_mode_hides_old_gear_but_keeps_material_recipes(self):
        cat = catalog.Catalog(replace={"weapon", "equipment"})
        self.assertEqual(cat.weapons, {})
        self.assertEqual(sorted(cat.recipes), ["Recipe_Rope", "Recipe_SilverIngot", "Recipe_SteelIngot"])
        self.assertTrue(cat.weapon_damage_band()["Melee"])   # from bands.json, survives deletion


class InstallGearTests(unittest.TestCase):
    def test_equipment_and_recipe_round_trip(self):
        guids = dict(CAT.id_to_guid, TestThornbow="ab" * 16, TestReedHelm="cd" * 16)
        with tempfile.TemporaryDirectory() as d:
            ep, rp = Path(d, "e.asset"), Path(d, "r.asset")
            ep.write_text(install.equipment_yaml(EQUIP, guids))
            rp.write_text(install.recipe_yaml(dict(RECIPE, prerequisites=["Recipe_Rope"]), guids))
            e, r = catalog.parse_asset(ep), catalog.parse_asset(rp)
        self.assertEqual(catalog._int_array(e["powerByRarity"]), [6, 10, 16, 24, 36])
        self.assertEqual(e["slotType"], "0")
        self.assertEqual(r["outputWeapon"]["guid"], "ab" * 16)
        self.assertEqual(r["prerequisiteRecipes"][0]["guid"], CAT.id_to_guid["Recipe_Rope"])
        real = catalog.parse_asset(catalog.RESOURCES / "RecipeDefinitions" / "Recipe_SteelIngot.asset")
        self.assertEqual(set(r), set(real))

    def test_patch_loot_list_only_touches_top_level(self):
        text = (catalog.RESOURCES / "Encounters" / "Dungeon_EmberforgeRuins.asset").read_text()
        out = install.patch_loot_list(text, "weapons", ["ef" * 16])
        self.assertIn("    weapons:\n    - {fileID: 11400000, guid: " + "ef" * 16, out)
        self.assertEqual(out.count("      weapons: []"), text.count("      weapons: []"))
        out = install.patch_loot_list(out, "equipment", [])
        self.assertIn("    equipment: []", out)


class PipelineTests(unittest.TestCase):
    def test_gdd_sections_found(self):
        text = pipeline.gdd_sections()
        for name in pipeline.GDD_SECTIONS:
            self.assertIn(name, text)

    def test_template_brief_is_draft(self):
        self.assertTrue(pipeline.brief_is_draft("# Narrative Brief\n> Not written yet.\n## Tone\n<!-- x -->\n"))
        filled = "\n".join(f"## {h}\nSome real text here.\nMore." for h in ("Tone", "Premise", "Naming rules"))
        self.assertFalse(pipeline.brief_is_draft(filled))


if __name__ == "__main__":
    unittest.main()
