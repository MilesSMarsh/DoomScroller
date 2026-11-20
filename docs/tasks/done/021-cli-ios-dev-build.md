# 021 — Command-line iOS dev build

**Owner:** release-engineer
**Size:** S
**Status:** done
**GDD:** docs/gdd/GDD.md#semester-scope (Must 1: command-line build and test pipeline), #constraints
**Depends on:** none

## Goal
Shell commands produce an iOS development build of the current game (Unity exports the
Xcode project, Xcode builds it), so Miles and QA can put builds on an iPhone directly or
through TestFlight without clicking through the Editor.

## Acceptance criteria
- [ ] `tools/build-ios.sh` runs Unity in batch mode (same `UNITY_PATH` default and
      Editor-open lock check as `tools/run-editmode-tests.sh`, exit 2 when locked) and
      calls a static build method; exits 0 only when the export succeeds, and prints the
      Xcode project path plus a short error summary from the log on failure.
- [ ] Build method lives in an Editor-only script (e.g. `Assets/Scripts/Editor/BuildScript.cs`,
      namespace `DoomScroller.Editor`, in the existing `DoomScroller.Editor` asmdef). It
      builds the scenes enabled in `EditorBuildSettings` (not a hardcoded list), as a
      Development build, exporting the Xcode project to `Builds/iOS/`.
- [ ] `tools/build-ios-xcode.sh` (runs on the Mac) builds the exported project with
      `xcodebuild`: a development-signed build installable on a registered iPhone, and
      an `archive` + export for TestFlight upload. Team id comes from an env var or
      argument; no certificate, provisioning profile, password or API key is committed.
- [ ] iOS player settings are buildable from the command line (bundle id
      `com.doomscroller.app`, IL2CPP, ARM64, minimum iOS version set). Any
      `ProjectSettings` change is listed in the report.
- [ ] `tools/build-ios.sh --server-url <url>` bakes that URL into the build (e.g.
      `http://192.168.1.20:8088` for the dev server on Miles's PC, later a hosted URL);
      without the flag the build keeps today's default. The Editor's own setting is
      unchanged after a build (no leftover asset edits in git).
- [ ] Development builds allow plain `http://` (App Transport Security exception) so the
      LAN dev server works; non-development (TestFlight/App Store) builds don't.
- [ ] `Builds/` is git-ignored.
- [ ] `CLAUDE.md` "Build" line updated to name the commands.
- [ ] Existing EditMode suite still passes (`tools/run-editmode-tests.sh`).
- [x] (manual, Miles) The dev build installs on an iPhone and reaches the feed.

## Context
- Memory: memory/systems.md (Build and test section only)
- May read: `tools/run-editmode-tests.sh`, `ProjectSettings/ProjectSettings.asset`,
  `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scripts/Editor/` (asmdef +
  `EncounterDataExporter.cs` as a style reference), `.gitignore`, `CLAUDE.md`, the
  `BuildScript.cs` on branch `hold/021-android-dev-build` (reuse its scene list, lock
  check and log summary; drop the Android parts)
- May write: `tools/build-ios.sh`, `tools/build-ios-xcode.sh`,
  `Assets/Scripts/Editor/BuildScript.cs` (+ `.meta`), `ProjectSettings/ProjectSettings.asset`
  (iOS player settings only), `Assets/Scripts/Network/ServerSettings.cs` (only if needed
  to set the URL at build time), `.gitignore`, `CLAUDE.md` (Build line only)

## Out of scope
App Store release build and store metadata, version-number bumping, CI, Game Center
capability (026), hosting a dev server.

## Notes
- Unity must have the iOS Build Support module installed; the Xcode step needs a Mac
  with Xcode and Miles's Apple Developer team.
- Only one Unity process per project: don't run while the Editor or tests are running.
- Device builds fall back to `http://localhost:8088` (no `ServerSettings` asset) unless
  `--server-url` is given; the dev server must be reachable from the phone (same Wi-Fi,
  PC's LAN IP, port 8088 open).
- Question for Miles: where does the build run? Is the iOS Build Support module installed for the Linux Editor (export on Linux, then copy `Builds/iOS/` to the Mac for Xcode), or should both steps run on the Mac? (answered below)
  - Answered (Miles, 2026-11-17): ios build will run on a mac
- Question for Miles: OK to leave the server URL for a follow-up task (LAN or hosted dev URL), or should this build take a server URL argument? (answered below)
  - Answered (Miles, 2026-11-17): take a `--server-url` argument; allow plain http in development builds only
- Note (Miles, 2026-11-19): shelve this check until i get an apple developer account
- Approved for merge (Miles, 2026-11-19).
- Shelved (Miles, 2026-11-19): the TestFlight check (archive uploads and installs through TestFlight) waits until Miles has an Apple Developer account; re-add it as a manual check then.
