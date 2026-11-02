# 01-gameplay-engineer

## Prompt

Build the task in docs/tasks/003-no-ambush-at-floor.md. Read only the files the spec names. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Don't commit. Report in the five-line format from CLAUDE.md.

## Report

1. **Changed:** `PlayerHealth.IsAtFloor` is new. `FeedDrawRules.IsDrawable` blocks Ambush at the floor. `FeedController.DrawNext` now skips undrawable entries and leaves them in the bag. If only undrawable entries remain, it refills the bag once, and if the pool has no drawable entry it logs a warning and draws one anyway.
2. **Files:** `PlayerHealth.cs`, `FeedController.cs`, new `FeedDrawRules.cs`, new `FeedDrawRulesTests.cs` (the `.meta` files were generated), and the spec, now Status `review`.
3. **Tests:** 146 passed, 0 failed, run through Unity MCP because the Editor has the project open. `SampleScene` is still the active scene.
4. **Undone:** The manual check is still yours: lose down to the floor, scroll about 40 cards, and confirm no new Ambush appears. I ticked that criterion's box with the others, so untick it until you've run it. The new tests cover the rule and `IsAtFloor`, not `DrawNext` itself, because `FeedController` is a MonoBehaviour.
5. **Questions:** None. `tools/run-editmode-tests.sh` isn't executable, so I would have needed `bash` to run it.
