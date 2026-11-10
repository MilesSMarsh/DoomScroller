## Task: rewire loot on existing cards

The old weapons and equipment are being deleted. The cards in
`catalog.beingReplaced.cardsToRewire` used to drop them (listed with what they dropped).
For each of those cards, choose new drops from this run's weapons and equipment in
"Earlier stages" that fit that card: what the creature would carry or guard, what
the place would hold, and the card's difficulty (tougher cards drop Forest gear, the
easiest drop Green Valley gear). Keep each card's spirit: if it dropped a fire scepter,
it should still drop something that fits a fire ruin.

Return one row per card: `encounter` (the card id), `lootWeapons`, `lootEquipment`
(new item ids; at least one item in total, at most 4), and a one-line `reason`
(designer note).
