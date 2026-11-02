# Content Data

How authored data is structured and where it lives. All content is ScriptableObject
assets loaded by folder with `Resources.LoadAll` — add content by adding assets, never
by hardcoding a catalog. Create assets with the `DoomScroller/...` Create menu or the
editor windows below; every asset needs its `.meta`.

| Type (script) | Folder under `Assets/Resources/` | Naming |
|---|---|---|
| `EncounterData` (`Feed/EncounterData.cs`) | `Encounters/` | `Enemy_*`, `Dungeon_*`, `Ambush_*`, … |
| `WeaponDefinition` (`Inventory/WeaponDefinition.cs`) | `WeaponDefinitions/` | |
| `EquipmentDefinition` (`Inventory/EquipmentDefinition.cs`) | `EquipmentDefinitions/` | |
| `AbilityDefinition` / `PassiveDefinition` | `Abilities/` | `Ability_*` |
| `MaterialDefinition` | `MaterialDefinitions/` | |
| `RecipeDefinition` | `RecipeDefinitions/` | |
| `ItemData` (static items, e.g. potions) | `Items/` | |
| `WeaponUpgradeConfig` (single) | `WeaponUpgradeConfig.asset` | |

## EncounterData
One asset per feed card. Key fields: `title`, `type` (`EncounterType`), `artwork`,
`foregroundArt`, `cardColor`, `description`, `baseXpReward`, `goldReward`, `lootTable`,
enemy combat stats `enemyHealth` / `enemyAttackDamage` / `enemyAttackSpeed` (authored
per encounter, separate from the difficulty number that currently drives the badge),
`damageTypeModifiers`, `dungeonRooms`
(`DungeonRoomData[]`, Dungeon only), `resourceWeaknesses` and `resourceThresholds`
(Resource only). Enemies are not separate assets — an enemy is an Enemy/Ambush
encounter, or a Fight room inside a dungeon.

## Weapons and abilities
A `WeaponDefinition` is a named weapon with hand-authored per-rarity stats and a *pool*
of possible abilities/passives. Generation rolls a rarity-sized subset each time; it is
never a fixed final list.

## Editor tools
- `Assets/Editor/EncounterCreatorWindow.cs` — author encounters.
- `Assets/Editor/EquipmentMenuWindow.cs` — author weapons, equipment, abilities.

## Not yet defined
Regions, solo/world/raid bosses, Events cards, keeper dialogue, the economy table, and
first-session card ordering have no data format yet. Tone and naming come from
`memory/narrative.md` once Miles writes it.
