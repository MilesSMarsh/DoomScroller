## Task: the equipment list ({count})

{replace_note}Write {count} `EquipmentDefinition`s, exactly: {plan}. Split each slot between
**Green Valley** and **Forest** (set `region`), themed to that region's places,
creatures and materials like the weapons in "Earlier stages".

- `category` / `slotType`: Armor goes in Head, Chest, Legs or Boots; Shield in
  LeftHand; Talisman in Talisman1 (the game lets it equip in either talisman slot).
- `equipmentName`: distinct, no rarity word, no hyphens, ≤35 characters; follow the
  brief's naming rules. `flavorText`: one sentence ≤120 chars.
- `powerByRarity` (Plain…Exalted) must rise each tier and stay inside
  `equipmentPowerBand`; Forest pieces sit higher in the band than Green Valley ones,
  and Chest pieces higher than Boots.
- `possiblePassives`: a pool of at least 4 existing passive ids that fit the piece
  (sturdy armor → defensive passives; a talisman → spirit or arcane).
