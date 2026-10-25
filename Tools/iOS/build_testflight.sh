#!/usr/bin/env bash
# Printshop Tycoon - one command from source to TestFlight (run on a Mac with Unity 6 + Xcode).
#
#   PT_APPLE_TEAM_ID=XXXXXXXXXX Tools/iOS/build_testflight.sh            # build + archive + upload
#   PT_APPLE_TEAM_ID=XXXXXXXXXX Tools/iOS/build_testflight.sh --no-upload # build + archive only
#
# Requirements (one-time):
#   * Unity 6 with iOS Build Support (Unity Hub)
#   * Xcode with command line tools, signed in with your Apple ID (Xcode > Settings > Accounts)
#   * Apple Developer Program membership (needed for TestFlight)
#   * The app created once in App Store Connect with bundle id dk.reberholt.printshoptycoon
#   * For upload without an interactive login: an App Store Connect API key, exported as
#       PT_ASC_KEY_ID, PT_ASC_ISSUER_ID and PT_ASC_KEY_PATH (path to the .p8 file)
#
# Nothing here publishes the game: an upload only appears in TestFlight / App Store Connect
# and still needs you to add testers (internal testing needs no Apple review).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUNDLE_ID="dk.reberholt.printshoptycoon"
SCHEME="Unity-iPhone"
XCODE_DIR="$ROOT/Builds/iOS"
ARCHIVE="$ROOT/Builds/PrintshopTycoon.xcarchive"
EXPORT_DIR="$ROOT/Builds/ipa"
UPLOAD=1
[[ "${1:-}" == "--no-upload" ]] && UPLOAD=0

: "${PT_APPLE_TEAM_ID:?Set PT_APPLE_TEAM_ID to your 10-character Apple Developer Team ID}"
export PT_BUILD_NUMBER="${PT_BUILD_NUMBER:-$(date +%Y%m%d%H%M)}"   # unique per upload, required by TestFlight

# --- 1. Find Unity (newest 6000.x in Unity Hub) ----------------------------------------------
UNITY="${UNITY_PATH:-}"
if [[ -z "$UNITY" ]]; then
  UNITY="$(ls -d /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
fi
[[ -x "$UNITY" ]] || { echo "Unity 6 not found. Install it via Unity Hub or set UNITY_PATH."; exit 1; }
echo "Unity: $UNITY"
echo "Build number: $PT_BUILD_NUMBER"

# --- 2. Unity -> Xcode project ---------------------------------------------------------------
"$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" \
  -buildTarget iOS \
  -executeMethod PrintshopTycoon.EditorTools.BuildScript.BuildIOS \
  -logFile "$ROOT/Builds/unity-ios.log"
[[ -d "$XCODE_DIR" ]] || { echo "Unity build failed - see Builds/unity-ios.log"; exit 1; }

# --- 3. Xcode archive -------------------------------------------------------------------------
rm -rf "$ARCHIVE"
xcodebuild -project "$XCODE_DIR/Unity-iPhone.xcodeproj" -scheme "$SCHEME" -configuration Release \
  -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" \
  -allowProvisioningUpdates DEVELOPMENT_TEAM="$PT_APPLE_TEAM_ID" CODE_SIGN_STYLE=Automatic \
  archive | tail -n 20

# --- 4. Export .ipa (and upload to TestFlight) -----------------------------------------------
PLIST="$ROOT/Builds/ExportOptions.plist"
sed "s/TEAM_ID_PLACEHOLDER/$PT_APPLE_TEAM_ID/; s/DESTINATION_PLACEHOLDER/$([[ $UPLOAD == 1 ]] && echo upload || echo export)/" \
  "$ROOT/Tools/iOS/ExportOptions.template.plist" > "$PLIST"

AUTH=()
if [[ -n "${PT_ASC_KEY_ID:-}" && -n "${PT_ASC_ISSUER_ID:-}" && -n "${PT_ASC_KEY_PATH:-}" ]]; then
  AUTH=(-authenticationKeyID "$PT_ASC_KEY_ID" -authenticationKeyIssuerID "$PT_ASC_ISSUER_ID" -authenticationKeyPath "$PT_ASC_KEY_PATH")
fi
rm -rf "$EXPORT_DIR"
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$PLIST" -exportPath "$EXPORT_DIR" \
  -allowProvisioningUpdates "${AUTH[@]}" | tail -n 20

if [[ $UPLOAD == 1 ]]; then
  echo "Uploaded to App Store Connect. TestFlight processes it in 5-15 minutes; then add yourself as an internal tester."
else
  echo "IPA exported to $EXPORT_DIR (not uploaded)."
fi
