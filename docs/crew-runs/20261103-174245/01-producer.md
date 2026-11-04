# 01-producer

## Prompt

Request from Miles: Fix the broken starter weapon. ProfileController.starterWeapon (ItemData) in SampleScene points at the deleted Weapon_TrainingSword, so a new game starts unarmed. Miles chose Wooden Sword (Assets/Resources/WeaponDefinitions/Wooden Sword.asset), a WeaponDefinition, not an ItemData. Per .claude/rules/unity-conventions.md, the starter must be generated with WeaponGenerator.GenerateFromDefinition (Common rarity) from a serialized WeaponDefinition reference, not from the archetype Generate. Only a new game gets it, so existing saves keep their loadout. Re-point the field in SampleScene to Wooden Sword via Unity MCP and save the scene. Include EditMode tests for the generation path. Spec it as task 004 for gameplay-engineer.

Write ONE small task spec in docs/tasks/ (follow docs/tasks/README.md), Must scope only. Report in the five-line format from CLAUDE.md. Then end with exactly two lines:
SPEC: <repo-relative path of the spec>
AGENT: <gameplay-engineer | ui-engineer | content-designer>

## Report

1. **What changed:** I wrote task 004 for gameplay-engineer (size S). It replaces the `ItemData starterWeapon` field with a `WeaponDefinition` field. A new plain-C# `StarterWeapon.Create` makes a Common Wooden Sword through `GenerateFromDefinition`, and only a new game calls it. The spec also adds EditMode tests and has the agent point the field at Wooden Sword in SampleScene via MCP and save the scene.
2. **Files touched:** `docs/tasks/004-wooden-sword-starter.md`.
3. **Test result:** n/a. This step only writes the spec.
4. **Left undone:** I didn't edit `memory/systems.md`, so the "starts with no weapon" gap note stays until the fix is merged. Wooden Sword has no icon set, which the spec leaves out of scope.
5. **Questions for Miles:** none.

SPEC: docs/tasks/004-wooden-sword-starter.md
AGENT: gameplay-engineer
