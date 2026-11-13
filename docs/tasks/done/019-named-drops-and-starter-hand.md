# 019 — Named drops only; starter sword equips to a hand

**Owner:** gameplay-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#progression
**Depends on:** none

## Goal
Every dropped or sold weapon/gear piece is a named, authored item (no "Plain RightHand" /
"Plain LeftHand" / "Plain Melee"), and a new game starts with the Hallow Wood Sword in a
hand slot, not the helmet slot.

## Acceptance criteria
- [x] `RewardGenerator.RollLootTable` on a table with empty `weapons`/`equipment`/`materials`
      pools returns items generated from the authored catalogs (`Resources/WeaponDefinitions`,
      `Resources/EquipmentDefinitions`) — every result has `sourceDefinition` set (weapons) or
      a title equal to `TitleFor`/`equipmentName` of an authored definition; no title
      contains a slot name ("Head", "LeftHand", "RightHand", …). EditMode test.
- [x] `RewardGenerator.GenerateItem(rarity, rng, categories)` (used by Market and Wandering
      Merchant for Armor/Shield/Talisman) picks an authored `EquipmentDefinition` of an
      allowed category via `EquipmentGenerator.GenerateFromDefinition`; the old
      "{rarity} {slot}" path only runs when no definition of that category exists.
      EditMode test with a Shield category.
- [x] Archetype `WeaponGenerator.Generate` is no longer reached from loot tables while any
      `WeaponDefinition` exists (empty-pool fallback uses `GenerateFromRandomDefinition`).
- [x] Weapons from `WeaponGenerator.GenerateFromDefinition` and `Generate` have
      `slotType = RightHand` (not the enum default `Head`). EditMode test.
- [x] `PlayerLoadout.Equip(item)` (single-arg) never places an item in a slot outside
      `ItemCategoryInfo.SlotsFor(item.category)`; if `item.slotType` isn't allowed it uses
      the first allowed slot. Covers weapons already in old saves with `slotType = Head`.
      EditMode test.
- [x] `StarterWeaponEquipTests`: after `EquipStarterWeapon`, the sword is in `RightHand`
      and `Head` is empty (extend the existing test).
- [ ] All EditMode tests pass (`tools/run-editmode-tests.sh`). — 11 pre-existing CombatCore failures (task not built yet)
- [x] (manual, Miles) Fresh save: sword shows in a hand slot; clear a dungeon and kill a
      Rogue Slime — drops have real names.

## Context
- Memory: memory/systems.md (Loot and items; Profile and progression)
- May read: `Assets/Scripts/Progression/RewardGenerator.cs`, `WeaponGenerator.cs`,
  `EquipmentGenerator.cs`, `MarketGenerator.cs`, `WanderingMerchantGenerator.cs`,
  `StarterWeapon.cs`; `Assets/Scripts/Inventory/PlayerLoadout.cs`, `ItemCategoryInfo.cs`,
  `EquipmentDefinition.cs`, `EquipmentSlotType.cs`, `ProfileController.cs` (EquipStarterWeapon
  only); `Assets/Scripts/Feed/LootTable.cs`; `Assets/Tests/EditMode/RewardGeneratorTests.cs`,
  `WeaponGeneratorTests.cs`, `PlayerLoadoutTests.cs`, `StarterWeaponEquipTests.cs`
- May write: the scripts above and those test files (add an `EquipmentGenerator`
  random-from-catalog helper if useful, mirroring `GenerateFromRandomDefinition`)

## Out of scope
- Filling the empty loot tables with curated pools (dungeon rooms/finals, Enemy_RogueSlime)
  — content-designer follow-up if Miles wants per-encounter drops.
- Drop rates, economy table, new definitions, UI changes.

## Notes
- Cause 1: `RollOneItem` falls back to `GenerateItem` when all three pools are empty;
  `GenerateItem` titles non-weapons `"{rarity} {slot}"` and rolls archetype weapons
  (`"{rarity} Melee"`). Empty tables today: all 6 `Dungeon_*` (rooms + final) and
  `Enemy_RogueSlime`. Resource encounters have empty tables but don't call `RollLootTable`.
- Cause 2: `GenerateFromDefinition` never sets `slotType`, so it stays `Head` (0) and
  `Equip(item)` uses it. Same bug affects any looted named weapon equipped via `Equip(item)`.
- Save shape unchanged; old weapons with `slotType = Head` already equipped in Head stay
  there until re-equipped (see question).
- Questions for Miles: should loading an old save move a weapon found in a non-hand slot (answered below)
  into a hand, or is fixing new saves enough?
  - Answered (Miles, 2026-11-12): items should only ever be equipped into there associated slot. Fix old saves.
- Approved for merge (Miles, 2026-11-12).
