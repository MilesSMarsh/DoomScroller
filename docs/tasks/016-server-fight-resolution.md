# 016 — Server resolves feed fights with server-drawn seeds

**Owner:** backend-engineer
**Size:** L
**Status:** todo (blocked on 012, 015)
**GDD:** docs/gdd/GDD.md#where-combat-runs, #persistence-and-save-rules (Fights,
Dropped connections, Revive potions), #combat
**Depends on:** 008, 012, 015

## Goal
When the player taps into a feed fight, the server draws the seed, runs the real
simulation and saves the result before the replay starts, so force-quitting,
reinstalling or a dropped connection never rerolls a fight.

## Acceptance criteria
- [ ] `POST /fights` with a client-generated fight id, encounter id and armed-Revive flag
      (until the per-card toggle exists the client sends its `AutoConsumeRevivePotions`
      setting; the server arms a Revive only if the flag is set and the stored profile
      carries one).
      The server builds `FightInput` from the stored profile (equipped items, attributes,
      current max HP, carried Revive and its percent level) and the cached encounter
      data, never from client-sent stats.
- [ ] The server draws the seed, runs CombatCore, and in one Postgres transaction
      inserts the `fight_records` row (player, fight id, encounter id, seed, outcome,
      revive fired, result) and updates the profile JSONB with the changes it owns:
      max-HP loss on defeat (current rule from `PlayerHealth`), Revive consumed only if
      it fired, revision bumped. Then it returns the `FightResult` and the new profile
      revision.
- [ ] Same fight id again returns the stored result, unchanged (idempotent, no reroll).
- [ ] `GET /fights/pending` returns a committed fight whose result the client hasn't
      acknowledged, so a reconnecting client replays it.
- [ ] Unknown encounter id or a non-fight encounter type → 400 with a reason; the
      profile is untouched.
- [ ] Server tests: same stored input + seed gives the same result as the Unity-side
      CombatCore test fixture; repeat fight id doesn't reroll; loss lowers max HP once;
      Revive consumed only when fired; 1000 fights run in under a second on the dev
      machine (log the number).
- [ ] Client: `IServerClient.CommitFight`/`GetPendingFight` with fake-transport tests.

## Context
- Memory: memory/systems.md (Combat; Health, loss and regen; Save and persistence)
- May read: `Server/`, `Assets/Scripts/CombatCore/`, `Combat/FightInputBuilder.cs`,
  `Progression/SaveData.cs`, `Progression/PlayerHealth.cs`, `Inventory/PlayerLoadout.cs`
  (Revive percent), `Server/data/encounters.json`, `Assets/Scripts/Network/`
- May write: `Server/`, `Assets/Scripts/Network/`, `ServerClientTests.cs`

## Out of scope
- Rewards/loot on the server (client keeps rolling them in this slice; Miles, Q6),
  dungeon and resource-node fights, Ambush rules, live bosses, hosting/deploy (Q4).

## Notes
- Building `FightInput` on the server from `SaveData` means the server needs the same
  item-to-input mapping as `FightInputBuilder`; put that mapping in CombatCore (plain
  data from `ItemSnapshot` fields) so it is written once. If that needs a change to
  008's output, raise it rather than duplicating it.
- Stateless handler: no in-memory state between requests besides cached encounter data.
- Decisions (Miles): the server owns fights, seeds, HP loss and Revive use; rewards,
  shops and crafting stay on the client (Q6). ASP.NET Core (.NET 10) compiles CombatCore
  from `Assets/Scripts/CombatCore/` via the 010 link (Q1). Fight records go in the
  `fight_records` table 012 created; the profile stays one JSONB document (Q2, Q7).
