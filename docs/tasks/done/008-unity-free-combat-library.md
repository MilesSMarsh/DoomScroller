# 008 — Unity-free shared combat library

**Owner:** gameplay-engineer
**Size:** L
**Status:** done
**GDD:** docs/gdd/GDD.md#where-combat-runs (One shared combat library), #combat
**Depends on:** 007

## Goal
The fight simulation compiles as a plain C# assembly with no Unity references, driven by
plain-data inputs, so the same code can resolve fights on the server and run the badge
and replay on the device.

## Acceptance criteria
- [x] New assembly `Assets/Scripts/CombatCore/DoomScroller.CombatCore.asmdef`
      (`noEngineReferences: true`, namespace `DoomScroller.CombatCore`) holds the
      simulator. Nothing in it references `UnityEngine` (no `Mathf`, `Debug`, `Color`,
      `ScriptableObject`); a `grep UnityEngine` over the folder returns nothing.
- [x] Inputs are plain serializable data: a `FightInput` with the player side (start HP,
      defense, basic attacks per hand, abilities with cooldown/damage/effects, passives,
      attribute values), the enemy side (HP, attack damage, attack speed, damage-type
      multipliers), the seed (`int`), and the armed Revive (`null` or restore percent).
      No `ItemData`/`PlayerLoadout`/`GameSettings` references inside CombatCore.
- [x] Output is a `FightResult`: outcome (win/loss), the full ordered beat list
      (`CombatBeat` moves into CombatCore), whether the Revive fired and at which beat,
      and final player HP. Revive fires automatically inside the simulation (one per
      fight), per the GDD; the sim is resolved in full in one call.
- [x] An adapter in the main assembly (`Combat/FightInputBuilder.cs`) builds `FightInput`
      from `PlayerLoadout` + `PlayerAttributes` + `EncounterData`/`DungeonStep` + start
      HP. `CombatResolver`, `WinOddsEstimator`, dungeon and resource-node callers use it;
      the badge's numbers do not change for the same seeds.
- [x] Manual ability casting and the mid-fight Revive prompt are removed (Miles,
      decision Q5). Abilities always auto-cast. The armed Revive passed in `FightInput`
      is "a Revive is carried and `AutoConsumeRevivePotions` is on" until the per-card
      toggle exists; it fires automatically inside the sim. `CastManualAbility`, the
      Revive dialog in `CombatPlaybackView`, `GameSettings.ManualAbilityCasting` and its
      toggle in `ProfileView` are gone; old saves with `manualAbilityCasting` still load
      (the field is ignored or removed; `JsonUtility` skips unknown fields).
- [x] Before editing, the agent shows Miles a file plan (files moved, created, changed,
      deleted) and waits for his go-ahead.
- [x] EditMode tests: same `FightInput` + seed gives an identical `FightResult` twice;
      `FightInput` and `FightResult` round-trip through `JsonUtility` with equal values
      (they will cross the network in 016);
      existing combat/badge tests (`AbilityEffectTests`, `AttackRateAndCooldownTests`,
      `CombatEdgeCaseTests`, `WeaponDamageTests`, `WinOddsEstimatorTests`, `FeedBadge*`)
      pass unchanged or with only namespace/constructor updates. Full suite passes via
      `tools/run-editmode-tests.sh`.
- [x] (manual, Miles) A feed fight, a dungeon fight and a resource node still play and
      look the same in the Editor.

## Context
- Memory: memory/systems.md (Combat)
- May read: `Assets/Scripts/Combat/*`, `Inventory/PlayerLoadout.cs`,
  `Inventory/ItemData.cs`, `Inventory/WeaponAbilityData.cs`, `Inventory/ProfileView.cs`,
  `Inventory/ProfileController.cs`, `Progression/SaveData.cs`,
  `Inventory/StatusEffectData.cs`, `Inventory/AbilityDefinition.cs`,
  `Feed/DamageTypeModifier.cs`, `Progression/GameSettings.cs`,
  `Progression/PlayerAttributes.cs`, `Encounters/DungeonStep.cs`,
  `Encounters/CombatResolutionView.cs`, `Encounters/DungeonStepPageView.cs`,
  `Encounters/ResourceEncounterView.cs`, `Assets/Scripts/DoomScroller.asmdef`,
  the combat tests named above plus `StoneSwordIntegrationTests.cs`, GDD sections above
- May write: `Assets/Scripts/CombatCore/` (new), `Assets/Scripts/Combat/*`, the callers
  above (only to switch to the adapter), the tests above, the two asmdefs (add the
  CombatCore reference); for removing manual casting only: `Progression/GameSettings.cs`,
  `Progression/SaveData.cs`, `Inventory/ProfileView.cs`, `Inventory/ProfileController.cs`,
  `Inventory/WeaponAbilityData.cs`

## Out of scope
- Any server code (010+), playing a precomputed result (009), the armed-Revive card
  toggle UI, 2x speed/skip, enemy abilities.

## Notes
- Large refactor: the agent shows Miles its file plan before editing anything.
- Today `CombatSimulator` uses `UnityEngine.Mathf` and reads `PlayerLoadout`/`ItemData`
  (ScriptableObjects) and `GameSettings.ManualAbilityCasting` directly;
  `CombatPlaybackView` drives the sim step by step (`RunUntilDeathOrVictory`,
  `CastManualAbility`, `Revive` after a dialog). A server-resolved fight can't take
  input mid-fight, so Miles removed both (Q5). Until 009 lands, `CombatPlaybackView` may
  still step the sim, but with no player input.
- `Mathf.RoundToInt` is banker's rounding; use `Math.Round(x, MidpointRounding.ToEven)`
  to keep results identical. Server and device must produce the same beats from the
  same input, so avoid anything platform-dependent (no `HashCode`, no dictionary
  iteration order in the sim loop).
- `StatusEffectData`/`WeaponAbilityData` may already be plain `[Serializable]` classes;
  reuse them in CombatCore if they carry no Unity types.
- Server decision (Q1): the server is ASP.NET Core on .NET 10 and compiles the CombatCore
  source as-is, so keep CombatCore to C# 9 and APIs available in both Unity's .NET
  Standard 2.1 profile and .NET 10 (no Unity-only or .NET 10-only APIs).
- Approved for merge (Miles, 2026-11-12).
- Approval withdrawn (Claude, 2026-11-12): nothing was built. The gameplay-engineer was
  waiting on the file plan (docs/crew-runs/20261112-182039/01-gameplay-engineer.md), so
  QA's failures were the missing code. Manual check reset.
- Answered question for Miles: approve the gameplay-engineer's file plan (run log above)? Also
  (a) put new tests in a separate `DoomScroller.CombatCore.Tests` assembly, or the existing
  EditMode one? (b) keep a `Step`/`RunForDuration` API on the core sim until 009 lands?
  "Go, default answers" = separate test assembly, and yes to keeping Step.
  - Answered (Miles, 2026-11-12): Go default answers
- Manual check FAILED (Miles, 2026-11-12): "A feed fight, a dungeon fight and a resource node still play and…" — was not given a prompt for potion revive when i lost a fight
- Approved for merge (Miles, 2026-11-12).
