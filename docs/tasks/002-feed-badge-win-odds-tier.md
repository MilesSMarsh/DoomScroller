# 002 — Feed card badge shows the simulated win-odds tier

**Owner:** ui-engineer
**Size:** S
**Status:** todo
**GDD:** docs/gdd/GDD.md#what-the-player-sees, docs/gdd/GDD.md#where-combat-runs (badge row), Semester scope item 4
**Depends on:** 001 (done)

## Goal
Each feed card's badge shows the tier word from `WinOddsEstimator` (Likely / Risky /
Long shot) instead of the heuristic win% and close-call%.

## Acceptance criteria
- [ ] `FeedController` computes card odds with `WinOddsEstimator.Estimate` (default run
      count) both when a card is created and in `RefreshAllChances`; it no longer calls
      `CombatResolver.PredictWinChance` / `PredictCloseCallChance`.
- [ ] `FeedCardView` takes a `WinOddsResult` (or `WinOddsTier`) and shows only the tier
      word: "Likely", "Risky", "Long shot". No % anywhere on the card; the close-call
      text is removed.
- [ ] Tier-to-label mapping lives in one static method, covered by an EditMode test
      (all three tiers).
- [ ] Badge colour is a plain per-tier placeholder (three distinct colours, no
      gradient); final colours are for ui-stylist. The locked-card dimming still applies.
- [ ] Each estimate gets its own `System.Random` from a single feed-owned source, so
      cards don't share a seed.
- [ ] Non-fight cards (Treasure, Tavern, Resource, Landing, WanderingMerchant, Dungeon)
      keep whatever badge visibility they have today.
- [ ] EditMode suite passes.
- [ ] (manual, Miles) In the Editor, cards show tier words; equipping/unequipping in
      Profile updates them on return; no visible hitch when the feed loads or refreshes.

## Context
- Memory: memory/systems.md (Combat, Feed entries), memory/ui-style.md
- May read: `Assets/Scripts/Feed/FeedController.cs`, `Assets/Scripts/Feed/FeedCardView.cs`,
  `Assets/Scripts/Combat/WinOddsEstimator.cs`, `Assets/Scripts/Combat/WinOddsTier.cs`,
  `Assets/Scripts/Combat/CombatResolver.cs` (current Predict* signatures),
  `Assets/Scripts/Progression/PlayerHealth.cs` (to get the start-HP int),
  `Assets/Tests/EditMode/TestFixtures.cs`
- May write: `Assets/Scripts/Feed/FeedController.cs`, `Assets/Scripts/Feed/FeedCardView.cs`,
  a new `Assets/Tests/EditMode/FeedBadgeTests.cs` (+ `.meta`)

## Out of scope
- Final badge colours, fonts, sizing (ui-stylist follow-up).
- Revive toggle and its effect on odds; Dungeon/boss odds.
- Removing `CombatResolver.PredictWinChance` / `PredictCloseCallChance` (leave them).
- Moving the sim off the main thread or caching across sessions.

## Notes
- `Estimate` takes `int playerStartHp`; the heuristic took `profileController.Health`.
  Use the player's current max/start HP the fight would begin with.
- 64 sims per card on every refresh may be slow on device; if it's visibly slow in the
  Editor, report it rather than lowering the run count.
- Question for Miles: should non-fight cards hide the badge, or keep showing it as today?
