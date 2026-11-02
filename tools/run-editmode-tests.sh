#!/usr/bin/env bash
# Runs the EditMode test suite headlessly and prints a short summary.
# Usage: tools/run-editmode-tests.sh [testFilter]
# Fails fast if the Unity Editor already has this project open (project lock) —
# in that case run tests through the Unity MCP connection instead.
set -euo pipefail

UNITY="${UNITY_PATH:-$HOME/Unity/Hub/Editor/6000.5.10f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/doomscroller-tests"
mkdir -p "$OUT"
RESULTS="$OUT/editmode.xml"
LOG="$OUT/editmode.log"
rm -f "$RESULTS"

if [ -f "$PROJECT/Temp/UnityLockfile" ] && pgrep -f "Unity.*-projectpath.*DoomScroller" >/dev/null 2>&1; then
  echo "Unity Editor has this project open — run tests via MCP instead." >&2
  exit 2
fi

FILTER_ARGS=()
[ -n "${1:-}" ] && FILTER_ARGS=(-testFilter "$1")

"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testResults "$RESULTS" -logFile "$LOG" \
  "${FILTER_ARGS[@]}" >/dev/null 2>&1 || true

if [ ! -f "$RESULTS" ]; then
  echo "No results file — likely a compile error. Errors from $LOG:" >&2
  grep -E "error CS|Compilation failed|Scripts have compiler errors" "$LOG" | sort -u | head -30 >&2
  exit 1
fi

grep -o '<test-run[^>]*>' "$RESULTS" | grep -oE '(result|total|passed|failed|skipped)="[^"]*"' | tr '\n' ' '
echo
# List each failing test with its message.
grep -E '<test-case [^>]*result="Failed"' "$RESULTS" | grep -oE 'fullname="[^"]*"' || true
grep -q '<test-run[^>]*result="Passed"' "$RESULTS"
