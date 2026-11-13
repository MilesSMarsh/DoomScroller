# 006 — Resources naming clean-up and placeholder enemy descriptions

**Owner:** content-designer
**Size:** S
**Status:** done
**GDD:** n/a (content hygiene; conventions in memory/content.md)
**Depends on:** 004

## Goal
Every authored asset under `Assets/Resources/` follows its folder's naming convention with
no spaces in its file name, and no enemy card shows placeholder description text.

## Acceptance criteria
- [ ] Renamed (file name / `m_Name` only):
      - `Abilities/Passive_New` → `Passive_DamageUp`
      - `Abilities/Passive_New 1` → `Passive_WeakenEnemy`
      - `Abilities/Passive_New 2` → `Passive_IncreaseAttackSpeed`
      - `Encounters/Wolf` → `Enemy_Wolf`
      - `WeaponDefinitions/Stone Sword` → `StoneSword`
      - `WeaponDefinitions/Wooden Sword` → `WoodenSword`
      - `EquipmentDefinitions/Leather Helmet` → `LeatherHelmet`
      - `RecipeDefinitions/Recipe_Leather Helmet` → `Recipe_LeatherHelmet`
      - `Encounters/Enemy_Standard` → `Enemy_RogueSlime`
      - `Encounters/Enemy_Hard` → `Enemy_BonewroughtSentinel`
- [ ] Every rename goes through Unity (`AssetDatabase.RenameAsset` or Unity MCP
      `manage_asset`), never `git mv`/shell. Each `.meta` GUID is identical before and after
      (record the 10 GUIDs before, compare after).
- [ ] Display/identity fields are unchanged: `passiveName`, `title`, `weaponName`,
      `equipmentName`, `recipeId`. Saves key off these, so old saves still load.
- [x] Zero dangling GUIDs (scope narrowed by Miles 2026-11-03 to authored content:
      `Assets/Resources/**/*.asset`, `Assets/Scenes/*.unity`, `*.prefab`; the pre-existing
      URP refs in `Assets/Settings/Renderer2D.asset` are a separate clean-up): every `guid:` referenced from those files resolves to a `.meta` in `Assets/` or `Packages/` (ignore built-in
      all-zero/`0000000000000000e000000000000000`/`f000…` GUIDs). Report the count before
      and after; after must be 0 (004 removes the stale `starterWeapon` reference).
- [ ] Tests that load these assets by path still pass: update only the path string
      literals (and matching comments) in `Assets/Tests/EditMode/StarterWeaponTests.cs`,
      `StoneSwordIntegrationTests.cs`, `ItemSnapshotIconTests.cs`. Full suite passes via
      `tools/run-editmode-tests.sh`.
- [ ] Descriptions for `Enemy_Wolf` (now "wild wolf"), `Enemy_RogueSlime` (now
      "50 XP + Common Gear") and `Enemy_BonewroughtSentinel` (now empty): write the text
      Miles approved (see Notes) into each asset's `description`, nothing else. Do not
      start this step until the drafts in Notes are marked approved.
- [x] (manual, Miles) open SampleScene, Play: feed loads, Wolf card and starter weapon
      appear, no missing-reference warnings in the console.

## Context
- Memory: memory/content.md, memory/narrative.md
- May read: the 10 assets above and their `.meta`s, `Assets/Resources/Encounters/Enemy_*.asset`
  (description style), the 3 test files above
- May write: the 10 assets (rename only), `description` on the three enemy cards (after
  approval), path strings in the 3 test files

## Out of scope
- Renaming any other asset, or any field other than `description` on the three cards.
- Updating memory/content.md (producer does it after merge).

## Notes
- `memory/narrative.md` is still unwritten and says new-text tasks are blocked until it
  is. So the descriptions are drafts in the style of the existing `Enemy_*` cards
  (one short sincere sentence, e.g. "A venomous spider the size of a dog."), and Miles
  approves or rewrites them. The renames don't wait on this.
- Nothing in `Assets/Scripts/` loads these by name; `Resources.LoadAll` picks them up by
  folder.
- Test-file edits are normally qa-tester's; allowed here for the path strings only,
  because the renames break them.
- Decided by Miles: rename `Enemy_Standard` → `Enemy_RogueSlime` and `Enemy_Hard` →
  `Enemy_BonewroughtSentinel`, through Unity so GUIDs survive; nothing references either
  by path. Draft descriptions for all three cards and get his approval before writing.
- Description drafts (status: **approved by Miles, 2026-11-03** — write exactly as below):
  - Wolf: "A lean grey wolf, hungry and hunting alone."
  - Rogue Slime: "A wandering ooze that dissolves whatever it swallows."
  - Bonewrought Sentinel: "A guardian of fused bone that never leaves its post."
- Approved for merge (Miles, 2026-11-04).
