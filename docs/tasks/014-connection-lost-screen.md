# 014 — "Can't reach the server" screen

**Owner:** ui-engineer
**Size:** S
**Status:** todo (blocked on 013)
**GDD:** docs/gdd/GDD.md#technical-strategy (online-only)
**Depends on:** 013

## Goal
When the game can't reach the server, the player sees a blocking message with Retry
instead of a game that looks playable but isn't saving.

## Acceptance criteria
- [ ] A full-screen greybox overlay on the existing canvas, shown on
      `ProfileController.ConnectionLost` (and on the startup load before the profile
      exists); it blocks input to the feed and Village. Text: "Can't reach the server",
      a Retry button that re-runs the failed step, and a spinner/"Connecting..." state
      while retrying.
- [ ] Hides on success; the feed keeps its scroll position.
- [ ] Uses shared style components only (no new colours/fonts); greybox per conventions.
- [ ] (manual, Miles) Stop the dev server mid-session: the overlay appears on the next
      save; restart it and tap Retry: the overlay hides and play continues.

## Context
- Memory: memory/systems.md (Save and persistence), memory/ui-style.md
- May read: `Inventory/ProfileController.cs` (event only), `Assets/Scripts/UI/`
  (existing overlay helpers), `Inventory/ProfileView.cs` (overlay construction pattern)
- May write: new `Assets/Scripts/UI/ConnectionLostView.cs`, scene wiring in
  `Assets/Scenes/SampleScene.unity` if needed

## Out of scope
- Final look (ui-stylist pass later if Miles wants), offline play.
