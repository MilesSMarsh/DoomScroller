# 012 — Server-stored player profile

**Owner:** backend-engineer
**Size:** M
**Status:** todo (blocked on 011)
**GDD:** docs/gdd/GDD.md#technical-strategy, #persistence-and-save-rules,
#scaling-and-cost-control (Managed database first)
**Depends on:** 011

## Goal
The player's profile lives on the server instead of in a file on the device, so progress
follows the account and survives a reinstall.

## Acceptance criteria
- [ ] Server: `GET /profile` returns the signed-in player's profile document and server
      UTC time (empty/new marker for a first sign-in); `PUT /profile` stores the whole
      uploaded save. The document is today's `SaveData` JSON shape stored as one Postgres
      `JSONB` column, one row per player, with a revision number; a write with a stale
      revision is rejected (409) so two devices can't silently overwrite each other.
- [ ] Postgres also gets the `fight_records` table 016 writes to (player id, fight id,
      encounter id, seed, outcome, revive fired, result JSON, acknowledged, created
      UTC; unique on player + fight id). This task only creates it.
- [ ] Server owns the regen clock: the stored `healthLastRegenUtcTicks` is set from
      server time on write, never trusted from the client.
- [ ] Server owns current max HP (Miles, 2026-11-03: "protect max HP"). On `PUT /profile`
      the server ignores the client's `healthCurrentMax` and keeps its stored value, after
      applying regen from server time (regen amount/interval are server config matching
      `ProfileController` defaults, capped at `healthTrueMax`, floor 20% of true max).
      Only a first sign-in (new profile) takes the client's value, clamped to
      `healthTrueMax`. `GET /profile` returns the server's value. 016 is the only other
      writer (HP loss / Revive). Server tests: an upload raising `healthCurrentMax` is
      not stored; regen over elapsed server time is applied and capped.
- [ ] Server rejects documents over a size limit and malformed JSON (400).
- [ ] Client: `IServerClient` gains `LoadProfile`/`SaveProfile` (string JSON in/out plus
      revision and server time). No change to `ProfileController` yet (013).
- [ ] Server tests: round-trip a real `SaveData` JSON produced by the Unity side
      (commit one sample under `Server/tests/fixtures/`); stale revision gets 409;
      player A can't read player B's profile.
- [ ] EditMode tests: the client methods against a fake transport.

## Context
- Memory: memory/systems.md (Save and persistence; Health, loss and regen)
- May read: `Server/`, `Assets/Scripts/Network/`, `Progression/SaveData.cs`,
  `Progression/SaveSystem.cs`, `Progression/PlayerHealth.cs` (regen fields), GDD above
- May write: `Server/`, `Assets/Scripts/Network/`, `ServerClientTests.cs`

## Out of scope
- Server-authoritative rewards/shops/crafting (they stay on the client, which uploads
  the whole save; Miles, Q6), dungeon run state, daily reset, account deletion,
  relational tables for items/gold.

## Notes
- Decisions (Miles): managed Postgres, profile as one JSONB document plus a
  fight-records table, Docker Postgres locally (Q2); go now with today's save shape as
  one document, proper tables after the economy table (Q7); client keeps rewards,
  shops and crafting and uploads the whole save, server owns fights, seeds, HP loss and
  Revive use (Q6).
- The revision check is what keeps the server's fight changes: 016 bumps the revision
  when it applies HP loss or Revive use, so a client save that hasn't taken those in
  gets 409. Current max HP is server-owned (see criteria). `healthTrueMax` (from
  Constitution) and gold are still client-reported; that is the logged
  "client-authoritative economy (cheatable)" gap, not something to fix here.
- Storing `SaveData` as one JSON document avoids a migration every time the save shape
  changes (007 already breaks it).
