# 023 — Debug: set HP to a borderline odds value

**Owner:** ui-engineer
**Size:** S
**Status:** approved
**GDD:** docs/gdd/GDD.md#combat (win odds badge); Semester scope item 4
**Depends on:** 022 (done)

## Goal
Miles can reliably see a feed card's win-odds badge drop a tier after a defeat penalty
in the Editor (task 022's failed manual check), without changing any real content or
balance values.

## Acceptance criteria
- [x] A plain C# helper (no MonoBehaviour, `DoomScroller.Feed`) finds, for one fight
      encounter + current loadout/attributes, the lowest current max HP in
      `[Floor, TrueMaxHealth]` at which `WinOddsEstimator.Estimate` still gives the
      card's tier at true max, such that the HP after one `ApplyDefeatPenalty`
      (25% of true max, clamped to floor) gives a lower tier. Returns "none" if no such
      HP exists for that card. Uses the estimator's existing seeded path (no new sim).
- [x] EditMode tests cover the helper with a fixture encounter: returns an HP whose tier
      equals the true-max tier and whose post-penalty tier is lower; returns none for an
      encounter that is Likely (or Long shot) across the whole HP range.
- [x] Editor-only menu item `DoomScroller/Debug/Borderline Odds HP` (Play Mode only,
      greyed out otherwise): tries each visible fight card top to bottom, sets
      `CurrentMaxHealth` to the first borderline HP found (fires `PlayerHealth.Changed`
      so the badges refresh), and logs the card title, chosen HP and the expected tier
      drop. Logs a warning and changes nothing if no visible card qualifies.
- [x] Editor-only menu item `DoomScroller/Debug/Apply Defeat Penalty` (Play Mode only)
      calls `PlayerHealth.ApplyDefeatPenalty()` — the same call a lost fight makes — so
      the check doesn't depend on actually losing a fight.
- [x] Both menu items live under `Assets/Scripts/Editor/` (excluded from player builds);
      any runtime hook they need is `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. No change to
      encounter/enemy/item assets, tier thresholds, penalty or regen values.
- [x] The debug HP change is saved like any other HP change (it's the debug save); a
      Profile → Inn heal or regen restores it. No new save fields.
- [x] All EditMode tests pass via `tools/run-editmode-tests.sh`.
- [x] In-game debug panel (Miles' answer: in game, not the Editor menu): a small "Debug"
      button on the main canvas opens a panel with two buttons, "Borderline Odds HP" and
      "Apply Defeat Penalty", doing exactly what the two menu items do, and a text line
      showing the result (card, chosen HP, expected tier drop, or "no card qualifies").
      Only present in the Editor and development builds (`#if UNITY_EDITOR ||
      DEVELOPMENT_BUILD`); absent from release builds. Built from the UI kit in
      `memory/ui-style.md` (placeholder styling is fine). The logic stays in the plain C#
      helper; the panel only calls it. The Editor menu items may stay.
- [x] (manual, Miles) Press Play, open the in-game Debug panel and press "Borderline Odds
      HP"; the panel (and Console) names a card and its expected drop (e.g. "Bog Troll: Likely -> Risky at
      62 HP"). Find that card in the feed: it still shows the higher tier. Within 30 s
      (before the next regen tick), press "Apply Defeat Penalty" in the panel:
      that card's badge changes to the lower tier without opening the Profile.
      Setup (borderline HP) is granted by the menu item, so no item or lucky draw is
      needed.
- [x] (manual, Miles, optional) After step 1, fight and lose a different card (a Long
      shot one), return to the feed: the named card, if still in the feed, shows the
      lower tier.

## Context
- Memory: memory/systems.md (Feed, Combat → Win odds, Health loss and regen)
- May read: `Assets/Scripts/Feed/FeedController.cs`, `Assets/Scripts/Feed/FeedCardView.cs`,
  `Assets/Scripts/Feed/MaxHealthChangeTracker.cs`,
  `Assets/Scripts/Progression/PlayerHealth.cs`, `Assets/Scripts/Combat/WinOddsEstimator.cs`,
  `Assets/Scripts/Combat/FightInputBuilder.cs`, `Assets/Scripts/Inventory/ProfileController.cs`,
  `Assets/Scripts/Editor/` (asmdef + `EncounterDataExporter.cs` as a menu-item example),
  `Assets/Tests/EditMode/WinOddsEstimatorTests.cs`
- May write: one new helper under `Assets/Scripts/Feed/`, one new Editor script under
  `Assets/Scripts/Editor/`, a minimal debug accessor in `FeedController.cs` (visible
  fight cards) and/or `PlayerHealth.cs` (debug set of current max HP, firing `Changed`),
  new tests under `Assets/Tests/EditMode/` (all with `.meta` files)

## Out of scope
- Any balance change to real encounters, tiers, defeat penalty or regen.
- A general debug/cheat panel beyond these two buttons; device-build testing.
- Pausing regen, server-side HP (tasks 016/017).

## Notes
- If `PlayerHealth.LoadState` already sets current max but doesn't fire `Changed`, add a
  separate debug setter rather than changing `LoadState`'s behaviour.
- Borderline search can be a binary search over HP (tier is near-monotonic in HP; 022
  added a test guarding this), but verify the post-penalty tier directly rather than
  assuming.
- Regen (10 HP / 30 s) will tick current max back up and can restore the higher tier;
  that is expected and a second visible proof of the refresh.
- Question for Miles: Is an Editor menu item acceptable for this, or do you want the debug options on an in-game dev panel you can reach in Play Mode (bigger task)? (answered below)
  - Answered (Miles, 2026-11-13): yes in game
- Changes requested (Miles, 2026-11-13, via Claude): Miles answered "yes in game" after the
  build finished, so the Editor-only menu isn't what he wanted. Add the in-game Debug panel
  (criterion above); the helper, tests and runtime hooks already built stay.
- May also write (for the panel): one new UI script under `Assets/Scripts/UI/` and the
  panel/button in `Assets/Scenes/SampleScene.unity` or a prefab; read `memory/ui-style.md`.
- Approved for merge (Miles, 2026-11-13).
