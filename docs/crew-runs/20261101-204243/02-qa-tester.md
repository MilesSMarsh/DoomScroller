# 02-qa-tester

## Prompt

Verify docs/tasks/003-no-ambush-at-floor.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

Five-line report:
1. **Tests added:** 9 in new `FeedDrawNextTests.cs`, covering the criteria the existing `FeedDrawRulesTests` didn't reach: `DrawNext` at and off the floor, Ambush kept in the bag, refill when everything is undrawable, the warn-and-return path with no drawable entry, and `IsAtFloor` edge cases. Existing `FeedDrawRulesTests` already covered the `IsAtFloor` thresholds and the per-type rule.
2. **Files touched:** `Assets/Tests/EditMode/FeedDrawNextTests.cs` and its `.meta` (new). No game code changed. The spec names `FeedDrawRulesTests.cs` for tests; the new file is a deviation, not a gap.
3. **Result:** EditMode via Unity MCP, 155 total, 155 passed, 0 failed. The 20 tests in the two floor fixtures also passed on a targeted run. Console showed no errors, and SampleScene is still the open, clean scene.
4. **Undone:** The manual Editor check (lose to the floor, scroll ~40 cards, no Ambush until regen) is still open for Miles. The new tests reach the private `DrawNext` by reflection on an uninitialized `FeedController`, so renaming its fields will break them.
5. **Questions for Miles:** None.

RESULT: PASS
