#!/usr/bin/env bash
# Builds the exported Xcode project (Builds/iOS) with xcodebuild. Runs on the Mac.
# Usage: tools/build-ios-xcode.sh dev|testflight [teamId]
#   dev         development-signed build for a registered iPhone -> Builds/iOS-out/dev/
#   testflight  archive + export for upload -> Builds/iOS-out/testflight/ (.ipa)
#               (export the project with `tools/build-ios.sh --release` first)
# Env: APPLE_TEAM_ID (or 2nd argument). Signing uses Xcode automatic signing with the
# certificates already in Miles's keychain. Optional App Store Connect API key for
# headless signing: ASC_KEY_PATH, ASC_KEY_ID, ASC_ISSUER_ID (never commit these).
set -euo pipefail

MODE="${1:-}"
TEAM="${2:-${APPLE_TEAM_ID:-}}"
case "$MODE" in dev|testflight) ;; *) echo "Usage: $0 dev|testflight [teamId]" >&2; exit 64 ;; esac
[ -n "$TEAM" ] || { echo "Set APPLE_TEAM_ID or pass the team id as 2nd argument." >&2; exit 64; }

PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
XPROJ="$PROJECT/Builds/iOS/Unity-iPhone.xcodeproj"
OUT="$PROJECT/Builds/iOS-out/$MODE"
LOG="${TMPDIR:-/tmp}/doomscroller-build/xcode-$MODE.log"
mkdir -p "$OUT" "$(dirname "$LOG")"
[ -d "$XPROJ" ] || { echo "No Xcode project at $XPROJ — run tools/build-ios.sh first." >&2; exit 1; }

AUTH=()
if [ -n "${ASC_KEY_PATH:-}" ] && [ -n "${ASC_KEY_ID:-}" ] && [ -n "${ASC_ISSUER_ID:-}" ]; then
  AUTH=(-authenticationKeyPath "$ASC_KEY_PATH" -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID")
fi

ARCHIVE="$OUT/DoomScroller.xcarchive"
if [ "$MODE" = dev ]; then METHOD=development; else METHOD=app-store-connect; fi

cat > "$OUT/ExportOptions.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>$METHOD</string>
  <key>teamID</key><string>$TEAM</string>
  <key>signingStyle</key><string>automatic</string>
</dict></plist>
PLIST

rc=0
{
  xcodebuild -project "$XPROJ" -scheme Unity-iPhone -configuration "$([ "$MODE" = dev ] && echo Debug || echo Release)" \
    -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" archive \
    DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} &&
  xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportPath "$OUT" \
    -exportOptionsPlist "$OUT/ExportOptions.plist" -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"}
} >"$LOG" 2>&1 || rc=$?

if [ "$rc" -ne 0 ]; then
  echo "xcodebuild failed (exit $rc). Errors from $LOG:" >&2
  grep -E "error:|\*\* .* FAILED \*\*" "$LOG" | sort -u | head -30 >&2
  exit 1
fi
echo "Output: $OUT ($(ls "$OUT"/*.ipa 2>/dev/null | head -1))"
