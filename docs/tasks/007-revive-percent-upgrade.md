# 007 — Revive HP as an upgradable percentage; Revive carry cap back to 3

**Owner:** gameplay-engineer
**Size:** M
**Status:** approved
**GDD:** docs/gdd/GDD.md#combat (Revive potions), #progression (Alchemist)
**Depends on:** 005

## Goal
A Revive brings the player back at a percentage of the HP they started the fight with
(30% to start), and the Alchemist's upgrade raises that percentage by 10 points per level
up to 100%, instead of raising how many Revives can be carried, which goes back to the
GDD's cap of 3.

## Acceptance criteria
- [x] Revive HP is a percentage of the fight's start HP (`CombatSimulator.PlayerStartHp`,
      i.e. current max HP when the fight began, not true max). The Revive asset's
      `potionAmount` (30, from 005) is the base percent.
- [x] `PlayerLoadout` exposes the effective percent = base + level × 10, capped at 100,
      and the HP it restores for a given fight-start HP: rounded up, at least 1.
      `CombatPlaybackView` passes `sim.PlayerStartHp` to it and the result to `sim.Revive`.
- [x] The Alchemist has one Revive upgrade track: +10 percentage points per level,
      7 levels (30% → 100%). `MaxUpgradeLevel` becomes 7. The "Flask Capacity" section,
      `TryUpgradeReviveCapacity` and the capacity-level dictionary are removed; status
      text shows the current and next percent ("Maxed" at 100%).
- [x] Revive carry cap is 3: `GetRefillCapacity` returns 3 for every potion type and no
      longer reads a capacity level. Tavern refill never tops Revives above 3.
- [x] `GetBestRevivePotion` still picks by effective strength; Potions tab Revive row
      reads "Revives at N% HP" instead of a flat HP number.
- [x] **Save break (explicit):** this task changes the save shape and does not keep old
      saves compatible. `PotionUpgradeEntry.capacityLevel` is deleted; `qualityLevel` is
      read as the percent-upgrade level (old flat-HP levels are not converted). No
      migration, no refund of spent capacity/quality upgrades, no trimming of extra
      Revives. Miles approved this because no real player saves exist yet. Testers
      should delete `savegame.json` (Application.persistentDataPath) before playtesting.
      The report must repeat the break in its "left undone" line.
- [x] Upgrade gold/material costs and the Tavern's per-level refill surcharge stay as
      today's constants (15 gold + 10/level, 2 materials + 1/level), extended to 7 levels,
      and are marked placeholder in the report.
- [x] EditMode tests (`PlayerLoadoutTests.cs`): percent at level 0 is 30; each level adds
      10; level 7 is 100 and further upgrades are refused; HP is computed from fight-start
      HP, rounds up and is never 0 (e.g. 30% of 1 HP = 1, 30% of 55 = 17); refill
      capacity is 3 for Revives at every upgrade level; save/load round-trips the
      percent level. Full suite passes via `tools/run-editmode-tests.sh`.
- [x] (manual, Miles) After deleting the old save: Alchemist shows one Revive upgrade
      with a % readout; buying it raises the % on the Potions tab; Tavern stops
      refilling at 3; losing a fight with a Revive brings the player back at roughly the
      shown % of the HP they started the fight with.

## Context
- Memory: memory/systems.md (Combat; Health, loss and regen; Village and shops)
- May read: `Assets/Scripts/Inventory/PlayerLoadout.cs`, `Inventory/ItemData.cs`,
  `Inventory/PotionInventoryView.cs`, `Encounters/VillageAlchemistView.cs`,
  `Encounters/VillageTavernView.cs`, `Combat/CombatPlaybackView.cs` (revive block ~line
  300), `Combat/CombatSimulator.cs` (`Revive`, `PlayerStartHp`), `Progression/SaveData.cs`,
  `Assets/Tests/EditMode/PlayerLoadoutTests.cs`, GDD sections above
- May write: the C# files above (not `ItemData.cs`, `CombatSimulator.cs`), and
  `PlayerLoadoutTests.cs`

## Out of scope
- Pricing: costs and per-level surcharge are set by economy-balance-analyst in the
  economy table (GDD Semester scope item 2) after this lands.
- Save migration or refunds (not needed; see the save-break criterion).
- Armed Revive card toggle, one-Revive-per-dungeon-run rule, Restoration as % of true max.
- Alchemist screen styling (ui-stylist only if Miles wants a pass after the section is
  removed).

## Notes
- Today: `PlayerLoadout` has two Alchemist tracks per Revive type, capacity (+1 carried
  per level, `MaxUpgradeLevel` 5) and quality (+10 flat HP per level,
  `ReviveQualityBonusPerLevel`). Reuse the quality track as the percent track (rename
  the constant to a percent step); drop capacity. `BaseRefillCapacity` is already 3.
  `MaxUpgradeLevel` is only read by `VillageAlchemistView`.
- Percent math belongs in `PlayerLoadout` (testable), not in the views.
- Miles' answers (resolved): base is fight-start HP; step +10, cap 100%, 7 levels;
  costs stay placeholder; old save shape may break, no migration or refunds.
- Approved for merge (Miles, 2026-11-12).
