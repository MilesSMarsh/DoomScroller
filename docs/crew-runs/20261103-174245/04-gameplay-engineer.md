# 04-gameplay-engineer

## Prompt

QA found failures on docs/tasks/004-wooden-sword-starter.md. Fix the game code (not the tests, unless a test is wrong; say so if it is). QA report:

Five-line report:

Added two EditMode tests in the new `StarterWeaponEquipTests.cs`, covering `EquipStarterWeapon` with a definition (equips a Common Wooden Sword) and without one (warning, empty loadout). The existing `StarterWeaponTests.cs` already covers the factory, seed, null, and save round-trip criteria, and the SampleScene wiring checks out in the diff.
Files touched: `Assets/Tests/EditMode/StarterWeaponEquipTests.cs` and its `.meta`. No game code changed. SampleScene is still the open scene and not dirty.
Result: Unity MCP run, 163 tests, 162 passed, 1 failed (the headless script exited 2 because the Editor has the project open).
Failure: criterion "EquipStarterWeapon equips the generated item" → `StarterWeaponEquipTests.EquipStarterWeapon_WithDefinition_EquipsCommonWoodenSword` → nothing gets equipped. `PlayerLoadout.Equip` returns early unless the item is already in the inventory, and `ProfileController.EquipStarterWeapon` never calls `AddToInventory`, so a fresh game still starts unarmed. Fix: call `_loadout.AddToInventory(weapon)` before `_loadout.Equip(weapon)`, then send back to gameplay-engineer.
Manual for Miles (after the fix): delete `savegame.json` and press Play. Expect a Common Wooden Sword equipped and fights dealing damage. With an existing save, expect the loadout unchanged and no extra sword.

RESULT: FAIL

Report in the five-line format from CLAUDE.md.

## Report

1. **Fixed:** `ProfileController.EquipStarterWeapon` now calls `_loadout.AddToInventory(weapon)` before `Equip`. `Equip` returns early for items that aren't in the inventory, so a fresh game was starting unarmed. My earlier "tests passed" report missed this because none of my tests covered `EquipStarterWeapon`.
2. **File touched:** `Assets/Scripts/Inventory/ProfileController.cs`. The QA test is correct, so I left it alone.
3. **Tests:** the full EditMode suite passed through Unity MCP, 163 of 163, including `EquipStarterWeapon_WithDefinition_EquipsCommonWoodenSword`. SampleScene is still the open scene and isn't dirty.
4. **Still open for Miles:**
   - Delete `savegame.json` and press Play. You should see a Common Wooden Sword equipped, and fights should deal damage.
   - Press Play with an existing save. The loadout should be unchanged, with no extra sword.
5. **Spec status:** I didn't change it, so it stays at `review`. No questions.
