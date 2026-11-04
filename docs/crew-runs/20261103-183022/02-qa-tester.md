# 02-qa-tester

## Prompt

Verify docs/tasks/006-resources-naming-cleanup.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

Tests added: `ResourcesNamingTests.cs` (59 cases): renames, GUID preservation, identity fields, the three approved descriptions, and a dangling-GUID scan. Files touched: `Assets/Tests/EditMode/ResourcesNamingTests.cs` and its `.meta`. Full EditMode run through Unity MCP: 234 ran, 1 failed (the others passed; the job summary doesn't give an exact pass count). SampleScene is still the open scene.
Failure: "Zero dangling GUIDs" → `References_AllAssetGuids_ResolveToAProjectOrPackageMeta` → 7 GUIDs referenced from `Assets/Settings/Renderer2D.asset` resolve to nothing in Assets/, Packages/, or the package cache. That file is unchanged from HEAD, so the renames didn't cause it. I counted package-cache GUIDs (UGUI/URP) as resolved; a strict reading of "Assets/ or Packages/" would also fail on those, so that needs your call.
Manual for Miles: open SampleScene and Play; check the Wolf card, starter weapon, and no missing-reference warnings. Also confirm the Sentinel: its re-saved `lootTable` is all-zero and its `rewardSummary` ("200 XP + Rare Gear") is now the empty description.

RESULT: FAIL
