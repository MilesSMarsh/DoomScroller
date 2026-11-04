# 011 — Player identity for the playtest

**Owner:** backend-engineer
**Size:** M
**Status:** todo (blocked on 010)
**GDD:** docs/gdd/GDD.md#technical-strategy (Game Center / Google Play Games sign-in)
**Depends on:** 010

## Goal
Each install gets a player account on the server without any typing, so the server can
tell players apart and every later request is authenticated.

## Acceptance criteria
- [ ] Server: `POST /auth/anonymous` takes an install id (a GUID the client generates once
      and keeps) and creates or returns that install's player id, stored in a Postgres
      `players` table, and issues a signed token (signing key from env). Every other
      endpoint except `/health` rejects requests without a valid token (401).
- [ ] Client: `Assets/Scripts/Network/` (namespace `DoomScroller.Network`, in the main
      assembly) gets a `ServerClient` with the base URL from a ScriptableObject or
      settings asset (not hardcoded), install-id creation and storage on the device, a
      sign-in call, token storage on the device, and an
      authenticated request helper with a timeout. Calls are async and testable behind
      an interface (`IServerClient`) so EditMode tests can fake it.
- [ ] Server tests: new install gets a new id; same install id returns the same player;
      bad or missing token gets 401.
- [ ] EditMode tests: `ServerClient` attaches the stored token; a fake transport
      returning 401 triggers one re-sign-in then fails cleanly.
- [ ] (manual, Miles) With the dev server running, entering Play mode signs in and the
      server logs one player id that stays the same across Play sessions.

## Context
- Memory: memory/systems.md (Save and persistence)
- May read: `Server/`, `Inventory/ProfileController.cs` (startup order only), GDD above
- May write: `Server/`, `Assets/Scripts/Network/` (new), new test files
  `Assets/Tests/EditMode/ServerClientTests.cs`

## Out of scope
- Game Center / Google Play Games linking (a later task before soft launch), account
  deletion, per-device boss limits.

## Notes
- Decision (Miles, Q3): anonymous sign-in by install id now; Game Center / Google Play
  Games linking comes before soft launch, not in this slice. Keep the `players` table
  ready for a linked platform id later (nullable column or separate table), but build
  no linking.
- `DoomScroller.Network` folder/namespace is approved by Miles.
- Unity web requests (`UnityWebRequest`) need the main thread; keep the transport behind
  the interface so the logic stays testable.
