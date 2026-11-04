# 009 — Fight screen replays a precomputed FightResult

**Owner:** gameplay-engineer
**Size:** M
**Status:** todo (blocked on 008)
**GDD:** docs/gdd/GDD.md#combat, #where-combat-runs (Fight replay)
**Depends on:** 008

## Goal
The fight screen plays a finished `FightResult` beat by beat, so it can show a fight the
server resolved, and plays the same thing whether the result came from the device or
the server.

## Acceptance criteria
- [ ] `CombatPlaybackView.Play` takes a `FightResult` (plus display info: names, sprites,
      start HP, enemy max HP) instead of a live `CombatSimulator`; it no longer calls
      `RunUntilDeathOrVictory`, `CastManualAbility` or `Revive`.
- [ ] Revive is shown from the result: at the beat where it fired, the "REVIVED!" popup
      plays and the HP bar jumps to the restored HP. The carried Revive is consumed on
      the loadout only if `FightResult` says it fired (GDD persistence rule).
- [ ] Ability cooldown bars still animate from beat timing; outcome and close-call flags
      (`LastOutcomeWasVictory`, `WasCloseCall`) come from the result.
- [ ] Callers (`CombatResolutionView`, `DungeonStepPageView`) resolve locally via
      CombatCore for now (seed from `System.Random` on the device) and pass the result.
- [ ] EditMode tests: a helper that maps a `FightResult` to the playback's HP timeline
      (pure function in the main assembly) gives correct HP after each beat, including
      across a Revive. Full suite passes.
- [ ] (manual, Miles) Feed and dungeon fights look as before; a losing fight with a
      Revive carried shows the revive and continues.

## Context
- Memory: memory/systems.md (Combat)
- May read: `Combat/CombatPlaybackView.cs`, `Combat/CombatResolver.cs`,
  `Assets/Scripts/CombatCore/*`, `Encounters/CombatResolutionView.cs`,
  `Encounters/DungeonStepPageView.cs`, `Inventory/PlayerLoadout.cs` (Revive methods)
- May write: those files except CombatCore, plus a new test file
  `Assets/Tests/EditMode/FightReplayTests.cs`

## Out of scope
- Network calls (017), 2x speed/skip, armed-Revive card toggle, restyling.

## Notes
- No UI layout changes, so no ui-stylist step. 008 removes manual casting and the Revive
  dialog (Miles, Q5); delete any leftover tap-to-cast widget behaviour here but keep the
  cooldown display.
