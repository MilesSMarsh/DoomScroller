# 001 — Simulator-based win odds and badge tier (logic only)

**Owner:** gameplay-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#where-combat-runs (badge row), docs/gdd/GDD.md#what-the-player-sees, Semester scope item 4
**Depends on:** none

## Goal
The win-odds badge should come from running the real fight simulator many times, not
from a DPS heuristic, and map to the GDD's three tiers: **Likely**, **Risky**,
**Long shot**. This task builds that estimator as pure, tested logic. Wiring it into the
feed card is a follow-up spec.

## Acceptance criteria
- [x] New plain C# class `WinOddsEstimator` in `Assets/Scripts/Combat/` (namespace
      `DoomScroller.Combat`, no MonoBehaviour) with a method that takes the loadout,
      attributes, player start HP, an `EncounterData`, a run count and a `System.Random`
      (or int seed), and returns a result holding the win fraction (0–1) and a tier.
- [x] Each practice run builds its simulator through the existing
      `CombatResolver.CreateSimulator` and calls `RunUntilDeathOrVictory`; a win is
      `IsEnemyDead`. No fight math is duplicated.
- [x] Each run gets its own `System.Random` derived from the injected source, so the same
      seed + inputs always give the same result.
- [x] Practice runs ignore the player's settings: pass a fresh `GameSettings` (auto-cast
      abilities, no revive). No revive is applied in this task.
- [x] Default run count is a named constant in the GDD's 50–100 range (64 suggested).
- [x] New enum `WinOddsTier { Likely, Risky, LongShot }`; mapping lives in one static
      method with named threshold constants (suggested defaults: Likely >= 0.70,
      Risky >= 0.35, else Long shot — Miles/combat-balance-analyst to confirm).
- [x] Non-fight encounter types (Treasure, Tavern, Resource, Landing, WanderingMerchant, Dungeon — Dungeon for now, per Miles)
      return win fraction 1 and `Likely` without running the simulator, matching the
      type list in `CombatResolver.PredictWinChance` plus WanderingMerchant.
- [x] Existing `CombatResolver.PredictWinChance` / `PredictCloseCallChance` and all
      callers are unchanged.
- [x] EditMode tests in `Assets/Tests/EditMode/WinOddsEstimatorTests.cs` cover:
      a clearly overpowered loadout returns `Likely` and a fraction near 1; a clearly
      hopeless loadout returns `LongShot` and a fraction near 0; same seed twice gives an
      identical result; tier boundaries map exactly at the threshold constants; a
      Treasure encounter returns 1 / `Likely`; run count of 0 or less is rejected
      (exception) or clamped — pick one and test it.
- [x] EditMode suite passes (run via Unity MCP, 97/97; not the headless script) with the new tests and all existing ones.

## Context
- Memory: memory/systems.md (Combat entry), memory/content.md (EncounterData section)
- May read: `Assets/Scripts/Combat/CombatResolver.cs`, `Assets/Scripts/Combat/CombatSimulator.cs`,
  `Assets/Scripts/Progression/GameSettings.cs`, `Assets/Scripts/Feed/EncounterData.cs`,
  `Assets/Scripts/Feed/EncounterType.cs`, `Assets/Tests/EditMode/TestFixtures.cs`,
  `Assets/Tests/EditMode/CombatEdgeCaseTests.cs` (test style reference)
- May write: `Assets/Scripts/Combat/WinOddsEstimator.cs`, `Assets/Scripts/Combat/WinOddsTier.cs`
  (or the enum inside the estimator file), `Assets/Tests/EditMode/WinOddsEstimatorTests.cs`,
  plus their `.meta` files; `TestFixtures.cs` only to add a small helper (e.g. an
  `EncounterData` builder) if needed.

## Out of scope
- Changing `FeedController`, `FeedCardView` or any UI (badge label/colour is a follow-up
  ui-engineer spec).
- Revive toggle and its effect on the badge.
- Removing `UnityEngine` from `CombatSimulator` (shared-library work, separate task).
- Dungeon steps and boss fights; close-call chance.
- Performance tuning for many cards on device (follow-up, manual check by Miles).

## Notes
- `CombatSimulator` takes `GameSettings`; `ManualAbilityCasting` must stay false for
  practice runs or abilities never fire.
- `CombatResolver.CreateSimulator` returns null for Treasure; the estimator should
  short-circuit non-fight types before calling it.
- Decided (Miles): tiers confirmed at 0.70 / 0.35. The card shows only the tier word,
  no % (for the future UI task). Dungeon skips the sim for now (returns 1 / Likely;
  revisit later). Null `EncounterData` throws `ArgumentNullException`; null
  loadout/attributes throw only for fight types.
