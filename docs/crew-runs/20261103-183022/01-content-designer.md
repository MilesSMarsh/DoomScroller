# 01-content-designer

## Prompt

Build the task in docs/tasks/006-resources-naming-cleanup.md. Read only the files the spec names. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Don't commit. Report in the five-line format from CLAUDE.md.

## Report

1. **Changed:** I renamed all 10 assets through `AssetDatabase.RenameAsset`, and every GUID is identical before and after. I wrote the three approved descriptions into `Enemy_Wolf`, `Enemy_RogueSlime` and `Enemy_BonewroughtSentinel`, and I changed only the `description` field. Display and identity fields are untouched.
2. **Files touched:**
   - The 10 renamed assets under `Assets/Resources/`.
   - Path strings in `StarterWeaponTests.cs`, `StoneSwordIntegrationTests.cs` and `ItemSnapshotIconTests.cs`.
   - A fourth file, `StarterWeaponEquipTests.cs`, also loaded `Wooden Sword` by path, so I updated that string too.
3. **Tests:** The EditMode suite passes, 175 of 175, through Unity MCP. It failed once before I fixed the fourth test file. `SampleScene` is still the open scene and has no unsaved changes.
4. **Left undone:** The dangling-GUID count is 7 before and 7 after, not the 0 the spec requires. All 7 are in `Assets/Settings/Renderer2D.asset` and the same GUIDs appear in both scans, so this task didn't cause them. Nothing under `Assets/Resources/` dangles. I searched `Assets`, `Packages` and `Library/PackageCache` for matching `.meta` files. The Renderer2D references are probably package scripts that aren't installed. The manual Play check in `SampleScene` is still for you to do.
5. **Question for Miles:** Should I leave the 7 Renderer2D references alone, or should someone find which package they came from? The spec's "after must be 0" can't be met without that.
