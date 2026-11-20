#!/usr/bin/env bash
# Exports the iOS Xcode project to Builds/iOS/ (Unity batch mode). Run on the Mac, then
# tools/build-ios-xcode.sh builds it.
# Usage: tools/build-ios.sh [--release] [--server-url <url>]
#   --release     non-development build (no plain-http ATS exception) for TestFlight
#   --server-url  bake the server base URL into the build (default: http://localhost:8088)
# Env: UNITY_PATH (Editor binary), APPLE_TEAM_ID (optional, written into the Xcode project).
# Exits 2 if the Editor has the project open.
set -euo pipefail

if [ -n "${UNITY_PATH:-}" ]; then UNITY="$UNITY_PATH"
elif [ "$(uname)" = "Darwin" ]; then UNITY="/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity"
else UNITY="$HOME/Unity/Hub/Editor/6000.5.10f1/Editor/Unity"; fi
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/doomscroller-build"
mkdir -p "$OUT"
LOG="$OUT/build-ios.log"

METHOD="DoomScroller.Editor.BuildScript.BuildIosDev"
URL_ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --release) METHOD="DoomScroller.Editor.BuildScript.BuildIosRelease" ;;
    --server-url) [ $# -ge 2 ] || { echo "--server-url needs a value" >&2; exit 64; }
                  URL_ARGS=(-serverUrl "$2"); shift ;;
    *) echo "Unknown argument: $1" >&2; exit 64 ;;
  esac
  shift
done

if [ -f "$PROJECT/Temp/UnityLockfile" ] && pgrep -f "Unity.*-projectpath.*DoomScroller" >/dev/null 2>&1; then
  echo "Unity Editor has this project open — close it and retry." >&2
  exit 2
fi

rc=0
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod "$METHOD" -logFile "$LOG" ${URL_ARGS[@]+"${URL_ARGS[@]}"} >/dev/null 2>&1 || rc=$?

if [ "$rc" -ne 0 ] || ! grep -q "BUILD SUCCEEDED" "$LOG"; then
  echo "iOS export failed (exit $rc). Errors from $LOG:" >&2
  grep -E "error CS|BUILD FAILED|Compilation failed|Scripts have compiler errors|Error building Player|\\[ERROR\\]" "$LOG" | sort -u | head -30 >&2
  exit 1
fi

echo "Xcode project: $PROJECT/Builds/iOS/Unity-iPhone.xcodeproj"
grep "BUILD SUCCEEDED" "$LOG"
