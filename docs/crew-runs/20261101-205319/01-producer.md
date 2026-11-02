# 01-producer

## Prompt

Miles approved and merged docs/tasks/003-no-ambush-at-floor.md. Update memory/systems.md (and memory/content.md if content changed) to describe the current state, set the spec's status to done and move it to docs/tasks/done/. Report in the five-line format from CLAUDE.md.

## Report

1. **What changed:** Task 003 is closed. In `memory/systems.md`, the Feed entry now describes the Ambush draw rule: no new Ambush cards are drawn while the player is at the max-HP floor, skipped ones stay in the bag, and Ambushes already on screen stay put. I removed that item from the Feed gap list. The Health entry now mentions `IsAtFloor`. The spec's status is set to done and it's moved to `docs/tasks/done/`.
2. **Files touched:** `memory/systems.md` and `docs/tasks/done/003-no-ambush-at-floor.md` (moved with `git mv`). `memory/content.md` is unchanged because this task added no content.
3. **Test result:** n/a for this update. The merge commit records 155/155 EditMode tests passing.
4. **Left undone:** Nothing is committed yet. Your in-Editor check from the spec hasn't been recorded: drop to the floor, scroll about 40 cards and confirm no new Ambush appears.
5. **Question:** About 200 data assets under `Assets/Resources/` aren't tracked by git (Abilities, Encounters, MaterialDefinitions, RecipeDefinitions, WeaponDefinitions, with their `.meta` files), and no spec covers them. Are they waiting to be committed? If so, should they get a content spec and a `memory/content.md` update?
