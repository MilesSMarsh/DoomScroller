## Task: {count} new encounter cards for region 1

Write {count} new feed cards (`EncounterData`) for region 1. Use the approved region, boss
and new weapons in "Earlier stages". Aim for roughly: mostly Enemy cards, plus one
Resource node and one Treasure or Ambush, unless {count} is too small for that.

- `id` is `<Type>_<PascalName>`; `title` is the display name (short, fits one line).
- `description`: one sentence ≤120 chars of card text in the voice of the existing cards.
- Enemy/Ambush: `enemyHealth`, `enemyAttackDamage`, `baseXpReward` inside the region's
  `difficulty` range; `enemyAttackSpeed` 0.5–1.2; weaknesses that fit the creature.
- Loot tables (`percentByRarity` Plain…Exalted, item counts, `lootWeapons`,
  `lootEquipment`, `lootMaterials`) use existing ids or this run's new gear; prefer
  Green Valley gear on region 1 cards.
- Resource: never fights back; set `resourceTimerSeconds`, `resourceWeaknesses` and
  rising `resourceThresholds` of existing materials, like the existing Resource cards,
  with 0 XP and 0 gold.
- Treasure: no fight; a loot table only.
