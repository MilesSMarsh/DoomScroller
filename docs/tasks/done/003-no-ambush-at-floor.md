# 003 — No Ambushes drawn while the player is at the max-HP floor

**Owner:** gameplay-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#feed-generation (Ambush bullet), docs/gdd/GDD.md#combat (Rewards scale with max HP), Semester scope item 5
**Depends on:** none

## Goal
A player sitting at the 20% max-HP floor is never served a new Ambush card, so a
debuffed player can't be forced into fights they can't escape.

## Acceptance criteria
- [x] `PlayerHealth` exposes `public bool IsAtFloor` (true when `CurrentMaxHealth <= Floor`).
- [x] The draw rule lives in a plain static method outside the MonoBehaviour (e.g.
      `FeedDrawRules.IsDrawable(EncounterType type, bool playerAtFloor)` in
      `Assets/Scripts/Feed/`, namespace `DoomScroller.Feed`): Ambush is not drawable at
      the floor; every other type is unaffected.
- [x] `FeedController.DrawNext` uses that rule with `profileController.Health.IsAtFloor`.
      A skipped Ambush stays in the shuffle bag (not discarded), so it can be drawn
      later in the same pass once the player is off the floor.
- [x] If every remaining bag entry is undrawable, the bag refills/reshuffles and draws a
      drawable entry; no infinite loop when the pool has no drawable entry at all (log a
      warning and return any entry rather than hang).
- [x] EditMode tests (new `Assets/Tests/EditMode/FeedDrawRulesTests.cs`): `IsAtFloor`
      true/false around the floor; Ambush blocked at floor, allowed above; Enemy,
      Treasure, Resource, Dungeon, WanderingMerchant, Tavern allowed at floor.
- [x] Full EditMode suite passes.
- [x] (manual, Miles) In the Editor, lose fights down to the floor, scroll ~40 cards:
      no new Ambush appears; regen above the floor and Ambushes return.

## Context
- Memory: memory/systems.md (Feed, Health entries)
- May read: `Assets/Scripts/Feed/FeedController.cs`, `Assets/Scripts/Feed/EncounterType.cs`,
  `Assets/Scripts/Progression/PlayerHealth.cs`, `Assets/Tests/EditMode/TestFixtures.cs`
- May write: `Assets/Scripts/Progression/PlayerHealth.cs`,
  `Assets/Scripts/Feed/FeedController.cs`, new `Assets/Scripts/Feed/FeedDrawRules.cs`,
  new `Assets/Tests/EditMode/FeedDrawRulesTests.cs` (+ `.meta` files)

## Out of scope
- Removing Ambush cards already in the live window when the player hits the floor.
- Swipe pressure, regions, first-session rules, streaks.
- Changing loss % or regen rate (GDD 10% / 1% per 36 s vs. code 25% / 10 HP per 30 s —
  separate task).
- Making a loss at the floor cost nothing (already true: penalty clamps to the floor).

## Notes
- `DrawNext` is a shuffle bag over `_pool`; keep the "each entry once per pass" property
  for drawable entries.
- Decided by Miles: Ambushes already on screen ahead of the player are NOT swapped out
  when they drop to the floor; the rule only affects new draws.
- Manual check passed (Miles, 2026-11-10): thiss works correctly however it is anoying to immedietly get ambushed upon healing perhaps ambushes only show above 60% hp
