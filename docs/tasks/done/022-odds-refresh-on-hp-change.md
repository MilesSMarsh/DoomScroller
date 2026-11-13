# 022 — Feed odds refresh when max HP changes

**Owner:** gameplay-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#combat (every fight starts at full current max HP); Semester scope item 4
**Depends on:** none

## Goal
The win-odds badge on visible feed cards always reflects the HP the fight would start
with: after a defeat penalty, regen tick, Inn heal, Restoration potion or Constitution
point, the tiers update without needing an equip change.

## Acceptance criteria
- [x] Visible cards' odds are re-estimated when `PlayerHealth.CurrentMaxHealth` changes,
      not only on `Loadout.Changed` (today `FeedController` only subscribes to the latter).
- [x] No re-estimate when `PlayerHealth.Changed` fires but `CurrentMaxHealth` is the same
      as at the last refresh (e.g. a true-max change clamped to the same current value).
      The decision lives in a small plain C# helper (no MonoBehaviour) so it is testable.
- [x] EditMode tests cover the helper: refresh on decrease, refresh on increase, no
      refresh on an unchanged value, first value always counts as a change.
- [x] EditMode test: `WinOddsEstimator.Estimate` with a lower start HP against the same
      encounter/seed gives a win fraction <= the higher-HP result (guards the input the
      refresh relies on; skip if an existing test already covers it — say which).
- [x] `FeedController` unsubscribes from `Health.Changed` when destroyed (as it should
      for `Loadout.Changed` — fix that too if missing).
- [x] All EditMode tests pass via `tools/run-editmode-tests.sh`.
- [x] (manual, Miles) Lose a fight, return to the feed: a borderline card's badge drops
      a tier (e.g. Likely → Risky) without touching the Profile.

## Context
- Memory: memory/systems.md (Feed, Health loss and regen, Combat → Win odds)
- May read: `Assets/Scripts/Feed/FeedController.cs`, `Assets/Scripts/Feed/FeedCardView.cs`,
  `Assets/Scripts/Progression/PlayerHealth.cs`, `Assets/Scripts/Combat/WinOddsEstimator.cs`,
  `Assets/Scripts/Inventory/ProfileController.cs` (how Health is exposed),
  `Assets/Tests/EditMode/WinOddsEstimatorTests.cs`, `Assets/Tests/EditMode/FeedDrawNextTests.cs`
- May write: `Assets/Scripts/Feed/FeedController.cs`, one new helper under
  `Assets/Scripts/Feed/` (namespace `DoomScroller.Feed`), new/updated tests under
  `Assets/Tests/EditMode/` (with `.meta` files)

## Out of scope
- Changing loss %, regen rate, floor or tier thresholds (GDD values differ; balance work).
- Badge colours (ui-stylist), Dungeon odds, per-card Revive toggle.
- Moving odds estimation to the server or any change to `CombatCore/`.
- Throttling/perf work for the 64-run sims beyond skipping unchanged values.

## Notes
- `PlayerHealth.Changed` already fires only on real changes (defeat, regen tick,
  `FullyHeal`, `RestoreMaxHealth`, `SetTrueMaxHealth`); regen ticks every 30 s, so a
  refresh runs at most that often while regenerating.
- Reuse `RefreshAllChances()`; don't duplicate the estimate path.
- Files touched by merged task 009 and planned task 017 (`CombatPlaybackView`,
  `CombatResolver`) are not in scope — don't edit them.

Questions for Miles: none (answered below)
  - Answered (Miles, 2026-11-13): none
- Manual check FAILED (Miles, 2026-11-13): "Lose a fight, return to the feed: a borderline card's badge drops…" — wasnt able to determine I dont think the cards are balanced in a way that lends itself to let me test this
- Approved for merge (Miles, 2026-11-13).
