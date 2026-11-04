# 03-qa-tester

## Prompt

Verify docs/tasks/004-wooden-sword-starter.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

Five-line report:

Added two EditMode tests in the new `StarterWeaponEquipTests.cs`, covering `EquipStarterWeapon` with a definition (equips a Common Wooden Sword) and without one (warning, empty loadout). The existing `StarterWeaponTests.cs` already covers the factory, seed, null, and save round-trip criteria, and the SampleScene wiring checks out in the diff.
Files touched: `Assets/Tests/EditMode/StarterWeaponEquipTests.cs` and its `.meta`. No game code changed. SampleScene is still the open scene and not dirty.
Result: Unity MCP run, 163 tests, 162 passed, 1 failed (the headless script exited 2 because the Editor has the project open).
Failure: criterion "EquipStarterWeapon equips the generated item" → `StarterWeaponEquipTests.EquipStarterWeapon_WithDefinition_EquipsCommonWoodenSword` → nothing gets equipped. `PlayerLoadout.Equip` returns early unless the item is already in the inventory, and `ProfileController.EquipStarterWeapon` never calls `AddToInventory`, so a fresh game still starts unarmed. Fix: call `_loadout.AddToInventory(weapon)` before `_loadout.Equip(weapon)`, then send back to gameplay-engineer.
Manual for Miles (after the fix): delete `savegame.json` and press Play. Expect a Common Wooden Sword equipped and fights dealing damage. With an existing save, expect the loadout unchanged and no extra sword.

RESULT: FAIL
