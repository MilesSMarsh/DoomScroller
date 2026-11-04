# 017 — Feed fights commit through the server

**Owner:** gameplay-engineer
**Size:** M
**Status:** todo (blocked on 009, 014, 016)
**GDD:** docs/gdd/GDD.md#combat, #where-combat-runs, #persistence-and-save-rules
**Depends on:** 009, 014, 016

## Goal
Tapping into a feed fight asks the server for the result and replays it; quitting
mid-replay and reopening replays the same fight.

## Acceptance criteria
- [ ] `CombatResolutionView` (Enemy and Ambush cards) calls `CommitFight` on tap-in and
      plays the returned `FightResult` through 009's playback. No device-side seed for
      these fights. The armed-Revive flag sent is `AutoConsumeRevivePotions` (no card
      toggle yet; Miles, Q5).
- [ ] The local profile applies the server's HP loss and Revive use (reloading the
      returned profile state or applying the same rule) and takes the new revision
      before the next save; rewards still roll on the device and save via `SaveProfile`
      (client keeps the economy; Miles, Q6).
- [ ] On launch, `GetPendingFight` runs after the profile loads; a pending fight opens
      its replay before the feed is usable, then is acknowledged.
- [ ] Network failure on tap-in shows 014's screen; Retry resends the same fight id.
- [ ] Badge (`WinOddsEstimator`) stays on the device and unchanged.
- [ ] EditMode tests with a fake client: tap-in sends one commit with a stable fight id
      across retries; a pending fight at launch is replayed then acknowledged; a server
      loss result applies the HP loss exactly once.
- [ ] (manual, Miles) Lose a fight, force-quit during the replay, reopen: the same fight
      replays and HP dropped once. Win a fight with a Revive carried: Revive kept.

## Context
- Memory: memory/systems.md (Combat; Feed; Save and persistence)
- May read: `Encounters/CombatResolutionView.cs`, `Combat/CombatPlaybackView.cs`,
  `Inventory/ProfileController.cs`, `Assets/Scripts/Network/IServerClient.cs`,
  `Encounters/EncounterFlowController.cs`
- May write: those files except `IServerClient.cs`, new
  `Assets/Tests/EditMode/ServerFightFlowTests.cs`

## Out of scope
- Dungeon and resource-node fights (still device-resolved; gap), 2x/skip, armed-Revive
  card toggle, hold-to-commit.
