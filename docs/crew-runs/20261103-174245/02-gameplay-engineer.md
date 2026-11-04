# 02-gameplay-engineer

## Prompt

Build the task in docs/tasks/004-wooden-sword-starter.md. Read only the files the spec names. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Don't commit. Report in the five-line format from CLAUDE.md.

## Report

1. A new game now equips a Common Wooden Sword, rolled from the authored definition. `ProfileController` has `starterWeaponDefinition` in place of `starterWeapon`, and `EquipStarterWeapon()` runs only from `StartFresh()`. It logs a warning if the definition is unassigned. I didn't touch `LoadFromSave`, so existing saves keep their loadout.
2. Files: `Assets/Scripts/Progression/StarterWeapon.cs` (new), `Assets/Tests/EditMode/StarterWeaponTests.cs` (new), `Assets/Scripts/Inventory/ProfileController.cs`, `Assets/Scenes/SampleScene.unity` (field set via Unity MCP), the task spec (Status is `review`), plus the two new `.meta` files.
3. Tests: the full EditMode suite passed through Unity MCP, 161 of 161. The Editor was open, so I didn't use the headless script. SampleScene is still the open scene and is saved.
4. Still open for Miles: delete `savegame.json` and press Play, to check the Profile shows the sword and fights deal damage. Then press Play with an existing save, to check the loadout is unchanged and no extra sword is added.
5. No questions.
