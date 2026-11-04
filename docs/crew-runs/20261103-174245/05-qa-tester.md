# 05-qa-tester

## Prompt

Verify docs/tasks/004-wooden-sword-starter.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

1. **Tests added:** none. Every automated criterion in 004 already has coverage in `StarterWeaponTests.cs` (asset exists, fields, same seed, null → null, SaveData round-trip) and `StarterWeaponEquipTests.cs` (fresh-game equip, null definition warns and leaves loadout empty).
2. **Files touched:** none by QA. Only the test run and a read of SampleScene's `starterWeaponDefinition` guid (`1cc64e9a…`, matches the Wooden Sword asset).
3. **Result:** EditMode 163/163 passed, 0 failed, 0 skipped, run via the Unity MCP `run_tests` job (headless script exited 2 because the Editor has the project open). SampleScene is the open scene and not dirty. The first MCP connection (`unity-mcp` `refresh_unity`) timed out twice, so compile was confirmed by the test run, not an explicit refresh.
4. **Manual checks for Miles:** (a) delete `savegame.json` from `persistentDataPath`, press Play, confirm a Common Wooden Sword is equipped and fights deal damage; (b) with an existing save, press Play, confirm the loadout is unchanged and no extra Wooden Sword appears.
5. **Questions:** the Editor showed a modal-dialog warning ("not in automated mode"). If the Editor has a popup open, clear it before the next MCP run.

RESULT: PASS
