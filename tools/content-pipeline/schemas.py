"""JSON Schemas the generator and critic must answer in (passed to `claude -p --json-schema`).
Field names follow the Unity data types (EncounterData, WeaponDefinition) so install.py can
write assets without guessing."""
from catalog import ATTRIBUTES, DAMAGE_TYPES, RARITIES, WEAPON_TYPES

# Regions new gear is themed to: the two Must-scope regions in the brief's World table.
GEAR_REGIONS = ["Green Valley", "Forest"]

S = {"type": "string"}
I = {"type": "integer"}
N = {"type": "number"}
B = {"type": "boolean"}


def arr(items, n=None):
    a = {"type": "array", "items": items}
    if n:
        a["minItems"] = a["maxItems"] = n
    return a


def obj(props):
    return {"type": "object", "properties": props, "required": list(props),
            "additionalProperties": False}


RANGE = arr(N, 2)
COLOR = obj({"r": N, "g": N, "b": N})
MODIFIER = obj({"type": {"enum": DAMAGE_TYPES}, "multiplier": N})

REGION = obj({
    "region": obj({
        "id": {"type": "string", "pattern": "^Region_[A-Z][A-Za-z]+$"},
        "name": S,
        "tagline": S,
        "description": S,
        "hiddenSatire": S,
        "difficulty": obj({"enemyHealth": RANGE, "enemyAttackDamage": RANGE,
                           "baseXpReward": RANGE}),
        "existingCards": arr(S),
        "cardMix": S,
    }),
    "soloBoss": obj({
        "id": {"type": "string", "pattern": "^Boss_[A-Z][A-Za-z]+$"},
        "name": S, "epithet": S, "description": S,
        "introLine": S, "victoryLine": S, "defeatLine": S,
        "enemyHealth": I, "enemyAttackDamage": I, "enemyAttackSpeed": N,
        "hpRegenPercentPerHour": N,
        "damageTypeModifiers": arr(MODIFIER),
        "baseXpReward": I, "goldReward": I,
        "percentByRarity": arr(N, 5),
        "lootWeapons": arr(S), "lootEquipment": arr(S), "lootMaterials": arr(S),
        "keyItem": obj({"name": S, "description": S}),
    }),
})

WEAPON = obj({
    "id": {"type": "string", "pattern": "^[A-Z][A-Za-z]+$"},
    "weaponName": S,
    "flavorText": S,
    "dropsFrom": S,
    "region": {"enum": GEAR_REGIONS},
    "isStarter": B,
    "weaponType": {"enum": WEAPON_TYPES},
    "damageType": {"enum": DAMAGE_TYPES},
    "isTwoHanded": B,
    "attackSpeed": N,
    "itemColor": COLOR,
    "baseDamageByRarity": arr(I, 5),
    "scalingAttribute": {"enum": ATTRIBUTES},
    "scalingCoefficient": N,
    "possibleAbilities": arr(S),
    "possiblePassives": arr(S),
})
WEAPONS = obj({"weapons": arr(WEAPON)})

EQUIPMENT = obj({"equipment": arr(obj({
    "id": {"type": "string", "pattern": "^[A-Z][A-Za-z]+$"},
    "equipmentName": S,
    "flavorText": S,
    "region": {"enum": GEAR_REGIONS},
    "category": {"enum": ["Armor", "Shield", "Talisman"]},
    "slotType": {"enum": ["Head", "Chest", "Legs", "Boots", "LeftHand", "Talisman1"]},
    "itemColor": COLOR,
    "powerByRarity": arr(I, 5),
    "possiblePassives": arr(S),
}))})

RECIPES = obj({"recipes": arr(obj({
    "id": {"type": "string", "pattern": "^Recipe_[A-Z][A-Za-z]+$"},
    "outputKind": {"enum": ["Weapon", "Equipment"]},
    "output": S,
    "outputRarity": {"enum": RARITIES},
    "goldCost": I,
    "ingredients": arr(obj({"material": S, "count": I})),
    "prerequisites": arr(S),
}))})

LOOT = obj({"rewired": arr(obj({
    "encounter": S,
    "lootWeapons": arr(S),
    "lootEquipment": arr(S),
    "reason": S,
}))})

ENCOUNTER = obj({
    "id": {"type": "string", "pattern": "^(Enemy|Ambush|Resource|Treasure)_[A-Z][A-Za-z]+$"},
    "title": S,
    "type": {"enum": ["Enemy", "Ambush", "Resource", "Treasure"]},
    "description": S,
    "cardColor": COLOR,
    "baseXpReward": I, "goldReward": I,
    "percentByRarity": arr(N, 5),
    "minItemCount": I, "maxItemCount": I,
    "lootWeapons": arr(S), "lootEquipment": arr(S), "lootMaterials": arr(S),
    "enemyHealth": I, "enemyAttackDamage": I, "enemyAttackSpeed": N,
    "damageTypeModifiers": arr(MODIFIER),
    "resourceTimerSeconds": N,
    "resourceWeaknesses": arr(MODIFIER),
    "resourceThresholds": arr(obj({"damageRequired": I, "material": S,
                                   "rarity": {"enum": RARITIES}, "amount": I})),
})
ENCOUNTERS = obj({"encounters": arr(ENCOUNTER)})

CRITIQUE = obj({
    "verdict": {"enum": ["approve", "revise"]},
    "summary": S,
    "soundsLikeTheGame": S,
    "issues": arr(obj({
        "target": S,
        "severity": {"enum": ["blocker", "major", "minor"]},
        "category": {"enum": ["tone", "lore", "naming", "gdd-rule", "balance",
                              "consistency", "duplication", "clarity"]},
        "problem": S,
        "fix": S,
    })),
})

STAGE_SCHEMAS = {"weapons": WEAPONS, "equipment": EQUIPMENT, "recipes": RECIPES,
                 "loot": LOOT, "region": REGION, "encounters": ENCOUNTERS}
