# 01-producer

## Prompt

Request from Miles: Find one small Must-scope gap between the GDD and the build

Write ONE small task spec in docs/tasks/ (follow docs/tasks/README.md), Must scope only. Report in the five-line format from CLAUDE.md. Then end with exactly two lines:
SPEC: <repo-relative path of the spec>
AGENT: <gameplay-engineer | ui-engineer | content-designer>

## Report

1. I wrote spec 003 for one Must gap (Semester scope item 5): the GDD says no Ambushes appear while the player is at the 20% max-HP floor, but the feed's shuffle-bag draw (`FeedController.DrawNext`) never checks the floor. The fix is a small static draw rule plus `PlayerHealth.IsAtFloor`, with EditMode tests.
2. Files touched: `docs/tasks/003-no-ambush-at-floor.md` (new).
3. Tests: none run, since this is only a spec.
4. Left undone: the GDD says a loss costs 10% and regen is 1% every 36 s, but the code uses 25% and 10 HP every 30 s. I noted this as a separate task and didn't write a spec for it. `memory/systems.md` is unchanged until the merge.
5. Question for Miles: when a player drops to the floor, should Ambush cards already in the feed ahead of them be swapped out? The spec assumes no.

SPEC: docs/tasks/003-no-ambush-at-floor.md
AGENT: gameplay-engineer
