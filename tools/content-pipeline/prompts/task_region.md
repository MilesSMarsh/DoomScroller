## Task: Region 1 and its solo boss

All content in the catalog exists in a single, unsplit feed today. The GDD's feed
generator splits the feed into regions with fixed difficulty ranges; region 1 is where
every new player starts, and its solo boss drops the item that opens region 2.

Design **region 1**. The brief's World table already decides it: its name, look, what
lives there, the solo boss and the key item. Use those; don't rename or replace them.
- A one-line tagline and a 2–3 sentence description of the place in the brief's voice. `hiddenSatire` is a designer-only note (never shown to players) saying what
  feed-culture habit this region quietly mocks.
- `difficulty`: the region's fixed range for enemy HP, enemy attack damage and base XP.
  It must sit inside `currentPowerBand` and suit a first region: the easier part of what
  exists, leaving the harder cards for region 2.
- `existingCards`: the existing encounter ids that belong here (fight cards must fall
  inside your range). Include Resource and Treasure nodes and anything else that fits the
  place; never `Landing_Blank`.
- `cardMix`: one or two sentences on the mix (GDD: mostly Combat, a minority of Treasure
  and Resource, occasional Merchants and Dungeons, rare Ambushes).

Design its **solo boss** (GDD "Boss types"), the creature the World table names, with a
personal name from the brief's approved word list plus a short title (Naming rules): a named, fixed-power enemy above the region's
range that keeps its damage between attempts and slowly regains HP. Give it card text
(`description`, one sentence ≤120 chars), an `introLine` before the fight, a
`victoryLine` when the player wins and a `defeatLine` when they lose (each one short
sentence spoken or shown on screen), stats, weaknesses that fit what it is, rewards,
loot (`lootWeapons` / `lootEquipment` from this run's new gear in "Earlier stages" when
there is any, else existing items; `lootMaterials` from existing materials), and the
`keyItem` that opens region 2 (its name
is in the World table). Per the brief, boss card text hints at the key item.
