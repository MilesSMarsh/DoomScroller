# 013 — Game loads and saves the profile through the server

**Owner:** gameplay-engineer
**Size:** M
**Status:** review
**GDD:** docs/gdd/GDD.md#technical-strategy (online-only), #persistence-and-save-rules
**Depends on:** 012

## Goal
The game signs in, loads the profile from the server at launch and saves every change
back to it; there is no local save file and regen runs on server time.

## Acceptance criteria
- [x] `ProfileController` startup: sign in, `LoadProfile`, then `LoadFromSave` (or
      `StartFresh` for a new player); gameplay input waits until it's loaded.
- [x] Saves go through `SaveProfile`, debounced (at most one in flight; a change during a
      save queues one more) instead of a disk write on every `Changed` event.
      `OnApplicationPause`/`Quit` still flush.
- [x] `SaveSystem` disk read/write is no longer called by the game. **Save break
      (explicit):** an existing local `savegame.json` is ignored, not uploaded.
- [x] Current max HP comes from the server: after load and after every save, the client
      applies the server's `healthCurrentMax` (012 owns it) instead of its own value.
- [x] `PlayerHealth` regen uses server time: an offset from the server UTC returned by
      `LoadProfile` is applied to its clock, so changing the device clock changes nothing.
- [x] On sign-in/load/save failure, `ProfileController` raises a `ConnectionLost` event
      (the screen is 014) and does not start a fresh profile over the server one.
- [x] EditMode tests with a fake `IServerClient`: new player starts fresh and saves;
      existing profile loads; failure raises `ConnectionLost` and never calls
      `StartFresh`; regen with a device clock 1 hour ahead of server time credits no
      extra HP; debounce sends one save for a burst of changes.
- [ ] (manual, Miles) With the dev server running: play, quit, delete the app's
      persistent data folder, play again; gold, gear and HP are back.

## Context
- Memory: memory/systems.md (Save and persistence; Health, loss and regen; Profile)
- May read: `Inventory/ProfileController.cs`, `Progression/SaveSystem.cs`,
  `Progression/SaveData.cs`, `Progression/PlayerHealth.cs`,
  `Assets/Scripts/Network/IServerClient.cs` (and its fake, if any)
- May write: `ProfileController.cs`, `PlayerHealth.cs`, `SaveSystem.cs` (only to remove
  callers' dependency or mark it unused), new `Assets/Tests/EditMode/ProfileSyncTests.cs`

## Out of scope
- The connection-lost screen (014), fights on the server (016/017), dungeon state.

## Notes
- Decisions (Miles): the client keeps rewards, shops and crafting and uploads the whole
  save (Q6); the save goes up as today's `SaveData` JSON, no reshaping (Q7). A 409 on
  save means the server changed the profile (a fight in 016/017): reload it with
  `LoadProfile`, never overwrite.
- Question for Miles: Nothing blocks gameplay input yet (it only exposes `IsLoaded`/`Loaded`); the loading overlay belongs with 014, OK? (answered below)
  - Answered (Miles, 2026-11-15): ok
- Manual check FAILED (Miles, 2026-11-15): "With the dev server running: play, quit, delete the app's…" — The game resets my save every time I load into it
- [ ] (manual, Miles) Re-run the manual check against the rebuilt dev server (`tools/run-server-dev.sh`); the earlier failure had profile-less players on the server, so saves never landed. Console now warns `[ProfileSync]` on any load/save failure.
