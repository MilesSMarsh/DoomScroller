# Content Data

How authored data is structured and where it lives. All content is ScriptableObject
assets under `Assets/Resources/`, loaded by folder with `Resources.LoadAll` (path is a
`const` in the loading class) — add content by adding assets, never by hardcoding a
catalog. Create assets with the `DoomScroller/...` Create menu or the editor windows
below; every asset needs its `.meta`. Scripts are in `Assets/Scripts/`.

| Type (script) | Folder | Count | Naming | Loaded by |
|---|---|---|---|---|
| `EncounterData` (`Feed/EncounterData.cs`) | `Encounters/` | 35 | `<Type>_<Name>` | `FeedController` |
| `WeaponDefinition` (`Inventory/WeaponDefinition.cs`) | `WeaponDefinitions/` | 23 | weapon name | `WeaponGenerator`, `SaveData` |
| `AbilityDefinition` (`Inventory/AbilityDefinition.cs`) | `Abilities/` | 20 | `Ability_*` | `WeaponGenerator` |
| `PassiveDefinition` (`Inventory/PassiveDefinition.cs`) | `Abilities/` | 18 | `Passive_*` | `WeaponGenerator` |
| `MaterialDefinition` (`Inventory/MaterialDefinition.cs`) | `MaterialDefinitions/` | 20 | material name | `MarketGenerator`, `PlayerLoadout`, `SaveData` |
| `RecipeDefinition` (`Inventory/RecipeDefinition.cs`) | `RecipeDefinitions/` | 25 | `Recipe_<Output>` | `RecipeCatalog` |
| `EquipmentDefinition` (`Inventory/EquipmentDefinition.cs`) | `EquipmentDefinitions/` | 1 | item name | `SaveData` |
| `ItemData` (`Inventory/ItemData.cs`, static items e.g. potions) | `Items/` | 2 | `Potion_<Effect>` | `SaveData` |
| `WeaponUpgradeConfig` (single) | `WeaponUpgradeConfig.asset` | 1 | | `WeaponUpgrader` |

## EncounterData
One asset per feed card. Current mix: Enemy 12, Resource 9, Dungeon 6, Ambush 3,
Treasure 2, Tavern 1, Landing 1, WanderingMerchant 1. `EncounterType` is serialized by
ordinal — append new values only. Key fields: `title`, `type`, `artwork`,
`foregroundArt`, `cardColor`, `description`, `baseXpReward`, `goldReward`, `lootTable`
(`percentByRarity[5]` — all-zero always rolls Common; min/max item count; optional
`weapons` / `equipment` / `materials` lists to pick from, else a generic roll), enemy stats `enemyHealth` /
`enemyAttackDamage` / `enemyAttackSpeed`, `damageTypeModifiers` (per-`DamageType`
multipliers), `dungeonRooms` (`DungeonRoomData[]`: Fight/Rest/Treasure, Dungeon only),
`resourceTimerSeconds`, `resourceWeaknesses`, `resourceThresholds` (damage required →
material, rarity, amount; Resource only). Enemies are not separate assets — an enemy is
an Enemy/Ambush encounter, or a Fight room inside a dungeon. Landing is shown once at
first open and never drawn by the shuffle bag. Enemy cards carry a one-sentence
`description` (e.g. `Enemy_Wolf` "A lean grey wolf, hungry and hunting alone.");
`Enemy_BonewroughtSentinel` is the tougher fight (110 HP, 200 XP, 1–2 drops from an
authored weapon/equipment/material list, rarity 10/25/40/20/5, centred on Rare;
placeholder values). `Ambush_Bandit` and `Enemy_Frostfang` have no `percentByRarity` at all.

## Weapons, abilities, equipment
A `WeaponDefinition` has `weaponName`, `weaponType` (Melee 9 / Ranged 7 / Magic 7),
`damageType`, `isTwoHanded`, `attackSpeed`, `statsByRarity[5]` (base damage + attribute
scaling), and *pools* `possibleAbilities` / `possiblePassives`. Generation rolls a
rarity-sized subset each time; never a fixed final list. `WoodenSword` is the new-game
starter (referenced by `ProfileController.starterWeaponDefinition` in SampleScene;
a GUID reference, so renaming it inside Unity is safe; deleting it or renaming outside
Unity breaks the starter, and `StarterWeaponTests` load it by path). Abilities and passives list
`allowedWeaponTypes`. `EquipmentDefinition` has slot, `powerByRarity[5]` and a passive pool.

## Potions (`ItemData`)
`category` Potion, `potionEffect` (Revive / MaxHealthRestore), `potionAmount`, unique
`itemId` (save re-links by it), `title`, `goldValue`, `rarity`, `itemColor`.
`Potion_Revive` "Revive Draught" (`potion_revive`, 30 = revive HP %, decided) and
`Potion_Restoration` "Restoration Potion" (`potion_restoration`, 25 HP), both Common,
no icon. Restoration 25 and gold values 40 / 20 are placeholders pending the economy
table. Both are referenced (3 each) by `ProfileController.startingInventory`;
`PotionItemDataTests` load them by folder and expect exactly these two.

## Recipes and materials
`RecipeDefinition`: `recipeId`, `outputKind` (FixedItem / Weapon / Equipment / Material;
now 21 Weapon, 1 Equipment, 3 Material — Rope, SilverIngot, SteelIngot), output ref,
`outputRarity`, `goldCost`, `ingredients` (material + count), `prerequisiteRecipes`
(the recipe tree). `MaterialDefinition`: name, icon, colour, `goldValue`.

## Editor tools
- `Assets/Editor/EncounterCreatorWindow.cs` — author encounters.
- `Assets/Editor/EquipmentMenuWindow.cs` — author weapons, equipment, abilities.

## Known content issues
None. Every file under `Resources/` follows its folder's naming convention, no spaces
(`ResourcesNamingTests` enforces the renamed set).

## Not yet defined
Regions, solo/world/raid bosses, Events cards, keeper dialogue, the economy table, and
first-session card ordering have no data format yet. Tone and naming come from
`memory/narrative.md` once Miles writes it.
