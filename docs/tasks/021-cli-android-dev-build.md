# 021 — Command-line Android dev build

**Owner:** release-engineer
**Size:** S
**Status:** on-hold
**GDD:** docs/gdd/GDD.md#semester-scope (Must 1: command-line build and test pipeline), #constraints
**Depends on:** none

## Goal
One shell command produces an installable Android development APK of the current game,
so Miles and QA can put builds on a phone without clicking through the Editor.

## Acceptance criteria
- [ ] `tools/build-android.sh` runs Unity in batch mode (same `UNITY_PATH` default and
      Editor-open lock check as `tools/run-editmode-tests.sh`, exit 2 when locked) and
      calls a static build method; exits 0 only when the build succeeds, and prints the
      APK path plus a short error summary from the log on failure.
- [ ] Build method lives in an Editor-only script (e.g. `Assets/Scripts/Editor/BuildScript.cs`,
      namespace `DoomScroller.Editor`, in the existing `DoomScroller.Editor` asmdef). It
      builds the scenes enabled in `EditorBuildSettings` (not a hardcoded list), as a
      Development build, to `Builds/Android/DoomScroller-dev.apk`.
- [ ] Unity's default debug keystore signs it; no keystore, password or certificate is
      committed.
- [ ] Android player settings are buildable from the command line: ARM64 target with a
      scripting backend that supports it (IL2CPP). Any `ProjectSettings` change is listed
      in the report.
- [ ] `Builds/` is git-ignored.
- [ ] `CLAUDE.md` "Build (Android / iOS)" line updated to name the command (Android only).
- [ ] Existing EditMode suite still passes (`tools/run-editmode-tests.sh`).
- [x] (manual, Miles) The APK installs on an Android phone and reaches the feed.

## Context
- Memory: memory/systems.md (Build and test section only)
- May read: `tools/run-editmode-tests.sh`, `ProjectSettings/ProjectSettings.asset`,
  `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scripts/Editor/` (asmdef +
  `EncounterDataExporter.cs` as a style reference), `.gitignore`, `CLAUDE.md`
- May write: `tools/build-android.sh`, `Assets/Scripts/Editor/BuildScript.cs` (+ `.meta`),
  `ProjectSettings/ProjectSettings.asset` (Android player settings only), `.gitignore`,
  `CLAUDE.md` (Build line only)

## Out of scope
iOS (Xcode export / signing), release AAB and upload keystore, store metadata,
version-number bumping, CI, pointing the build at a non-localhost server.

## Notes
- Android build support module is installed at
  `~/Unity/Hub/Editor/6000.5.10f1/Editor/Data/PlaybackEngines/AndroidPlayer`.
- `ProjectSettings.asset` currently has an empty `scriptingBackend` (Mono default) with
  `AndroidTargetArchitectures: 2` (ARM64), which Mono can't build; expect to set
  Android → IL2CPP. Bundle id is already `com.doomscroller.app`.
- Only one Unity process per project: don't run while the Editor or tests are running.

Questions for Miles: (answered below)
  - Answered (Miles, 2026-11-13): no question given
- Device builds fall back to `http://localhost:8088` (no `ServerSettings` asset), so the
  phone can't reach the server. OK to leave for a follow-up task (LAN dev URL), or
  should this build take a server URL argument?
- Note (Miles, 2026-11-13): I do not have an android to test on
- Note (Miles, 2026-11-13): no question given
- Approved for merge (Miles, 2026-11-13). lets hold on this task until I can get an android to test
- On hold (Miles, 2026-11-13): no Android device to test on. The built code is parked on
  branch `hold/021-android-dev-build` (not merged). To resume: merge that branch, then
  answer the server-URL question above before building for a phone.
