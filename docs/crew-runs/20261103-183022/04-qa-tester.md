# 04-qa-tester

## Prompt

Verify docs/tasks/006-resources-naming-cleanup.md against its acceptance criteria. Add EditMode tests for uncovered criteria, then run the full suite. If the Unity Editor has the project open, run tests through Unity MCP and afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene. Report in the five-line format from CLAUDE.md. Then end with exactly one line: RESULT: PASS or RESULT: FAIL

## Report

1. **Added:** 1 test, `StaleOldPaths_NotReferencedByScriptsOrTests`. The existing `ResourcesNamingTests.cs` (about 60 cases) already covered renames, GUIDs, identity fields, and descriptions. I checked its hardcoded GUIDs and identity values against git HEAD and they match.
2. **Files touched:** `Assets/Tests/EditMode/ResourcesNamingTests.cs`. No game code touched.
3. **Result:** Headless script refused (Editor has the project open), so I ran through Unity MCP: 235 tests, 234 pass, 1 fail. `refresh_unity` timed out twice, but `run_tests` executed. SampleScene is still open and clean.
4. **Failure:** Criterion "Zero dangling GUIDs" → `References_AllAssetGuids_ResolveToAProjectOrPackageMeta` → 7 unresolved GUIDs, all referenced from `Assets/Settings/Renderer2D.asset` (URP probe-volume debug shaders). This predates 006: the file is unchanged since the initial check-in and none of the 10 renamed GUIDs are involved, so 006 leaves the count where it was. The criterion still requires 0 after. Fixing it means clearing those fields in `Renderer2D.asset`, which is outside 006's scope.
5. **Manual for Miles:** Open SampleScene and Play: feed loads, Wolf card and starter weapon appear, no missing-reference warnings. **Question:** should the `Renderer2D.asset` cleanup be pulled into 006 or split out?

RESULT: FAIL
