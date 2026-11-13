# 020 — Locked reward page can't be dragged into view

**Owner:** ui-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#combat
**Depends on:** none

## Goal
During a fight and after a defeat, the player can't drag the fight screen sideways to
peek at the reward page. Once a fight is won, swiping to the reward page works as now.

## Acceptance criteria
- [ ] While the fight is playing and after a defeat, a horizontal drag doesn't move the
      pager content toward the reward page at all (no peek, no snap-back).
- [ ] After a victory, dragging between the fight page and the reward page works as it
      does today (follows the finger, snaps on release, 80 px swipe threshold).
- [ ] A Treasure card (pinned on page 1) can't be dragged right to show the empty fight
      page.
- [ ] The drag offset clamp is a pure static helper (e.g.
      `ClampDragX(rawX, minPage, maxPage, pageWidth)`), and `OnDrag` and `OnEndDrag` share
      one reachable-page-range calculation. EditMode tests cover: locked (max = 0) clamps
      any leftward drag to 0; unlocked allows down to `-pageWidth`; Treasure
      (min = max = 1) pins to `-pageWidth` both ways.
- [x] (manual, Miles) In Play: drag left during a fight and after a loss, and the screen
      doesn't move. Win a fight and the swipe to the chest works. Open a Treasure card and
      dragging right does nothing.

## Context
- Memory: memory/systems.md (Combat, Encounters sections)
- May read: `Assets/Scripts/Encounters/CombatResolutionView.cs`,
  `Assets/Tests/EditMode/DoomScroller.Tests.EditMode.asmdef`
- May write: `Assets/Scripts/Encounters/CombatResolutionView.cs`, a new test file under
  `Assets/Tests/EditMode/` (with `.meta`)

## Out of scope
- `DungeonView` paging (a separate pager, not reported).
- Any change to win/lose flow, Leave button rules, or the reward chest.
- Rubber-band/overscroll feel. A hard clamp is enough.

## Notes
- Cause: `OnDrag` sets `pos.x = -_currentPage * _pageWidth + delta.x` without clamping,
  and only `OnEndDrag` limits the page through `_minPage` and `_canReachRewardPage`.
  Clamp the live position to `[-maxPage * _pageWidth, -_minPage * _pageWidth]` with the
  same `maxPage` as `OnEndDrag`.
- Specs 009 and 017 also edit this file later. Keep the change local to the drag handlers
  and the new helper.
- Approved for merge (Miles, 2026-11-12).
