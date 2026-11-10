## Task: the weapon list ({count})

{replace_note}Write {count} `WeaponDefinition`s: **{per_type} Melee, {per_type} Ranged and
{per_type} Magic**. Split each category between the brief's first two regions,
**Green Valley** (the starting region: farmland, animals, slimes, goblins) and **Forest**
(dense old woodland: tree people, golems, large predators, skeletons), and set `region`.
Within a category, make each weapon a distinct fantasy (different damage types, one- vs
two-handed, speed, ability kit), not reskins of each other. Tie each to its region's
places, creatures and materials.

Exactly one weapon has `isStarter: true`: the weapon every new adventurer starts with
in the Green Valley opening. A humble, one-handed Green Valley weapon at the bottom of
the damage band. Everything else has `isStarter: false`.

- `id` is the PascalCase asset name (no spaces). `weaponName` is the distinct name
  only, *without* a rarity word: the rarity (Plain…Exalted) is rolled when the weapon
  drops and the game shows it first ("Tempered Hallow Wood Sword"), so store
  "Hallow Wood Sword". Keep it ≤26 characters so the full title fits 35. Follow the
  brief's weapon naming rules: distinct, never generic ("Sword", "Bow").
- `flavorText`: one sentence ≤120 chars, for the designer and future item tooltips.
- `dropsFrom`: a short note on where it comes from (a creature, a place, crafting).
- `baseDamageByRarity` (Plain…Exalted) must rise each tier and stay inside
  `weaponDamageBand` for its weapon type; `attackSpeed` inside the band's range. Forest
  weapons sit higher in the band than Green Valley ones.
- `attackSpeed` is **attacks per second** (higher is faster), so damage per second is
  about baseDamage × attackSpeed. Keep that roughly even within a category at each
  rarity: a slow two-hander hits harder per swing, a fast dagger hits lighter.
- `possibleAbilities` / `possiblePassives` are *pools* the game rolls a rarity-sized
  subset from (Plain 0 … Exalted 4), so each needs at least 4 entries, and 5–6 where
  the catalog allows (so Exalted rolls still vary), picked from existing catalog ids
  whose `allowedWeaponTypes` include this weapon's type and whose effects match the
  weapon's fantasy. {pool_limits} With so few, pools will overlap; make each weapon
  distinct through its whole identity (damage type, hands, speed, scaling, the abilities
  it leads with), and keep the strongest abilities (e.g. Oblivion) off Green Valley gear.
- `scalingAttribute` / `scalingCoefficient` (the old weapons used 0.5).
