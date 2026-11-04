# 02-qa-tester

## Prompt

Verify docs/tasks/005-potion-item-data.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

1. Tests added: 12 in `PotionItemDataTests` covering the two potion assets (ids, titles, categories, rarity, no icon, Revive `potionAmount` 30), `.meta` presence, the starting loadout (Revive and Restoration stay known after being spent, Tavern-style refill tops Revive up to 3 and stops when gold runs short), and snapshot round-trip relinking to the same asset.
2. Files touched: `Assets/Tests/EditMode/PotionItemDataTests.cs` and its `.meta` (new). No game code, content, or scene edits.
3. Result: full EditMode suite 175/175 passed via Unity MCP (the headless script exited 2 because the Editor has the project open); the new fixture ran 12/12. Open scene is `Assets/Scenes/SampleScene.unity`, not dirty. Automated criteria pass.
4. Not done: economy-balance-analyst sign-off on Restoration `potionAmount` (25) and both `goldValue`s (Revive 40, Restoration 20), which are placeholders and must clear before merge. Manual checks for Miles: fresh save shows 3 Revive and 3 Restoration in Potions; Tavern shows a refill price or "Fully Stocked"; Alchemist no longer says "No revive potions discovered yet"; a losing fight with Revive carried continues once. `startingInventory` GUIDs match the asset `.meta` files, but I did not confirm the Inspector values in the Editor.
5. Questions for Miles: none.

RESULT: PASS
