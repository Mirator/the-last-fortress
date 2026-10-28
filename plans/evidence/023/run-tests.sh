#!/bin/sh
# Usage: run-tests.sh <out-dir> <name> <EditMode|PlayMode> [filter]
# Runs one Unity batch test pass, prints the XML totals, then reverts the ProjectSettings noise batch runs write.
cd "$(dirname "$0")/../../.." || exit 1
E="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
P="$(pwd -W)/game/The Last Fortress"
OUT="$1"; mkdir -p "$OUT"; OUTW="$(cd "$OUT" && pwd -W)"
if [ -n "$4" ]; then F="-testFilter"; FV="$4"; else F=""; FV=""; fi
"$E" -batchmode -nographics -projectPath "$P" -runTests -testPlatform "$3" $F "$FV" -testResults "$OUTW/$2.xml" -logFile "$OUTW/$2.log"
echo "exit=$?"
grep -o '<test-run [^>]*' "$OUT/$2.xml" | grep -o 'testcasecount="[0-9]*"\|result="[^"]*"\|passed="[0-9]*"\|failed="[0-9]*"\|skipped="[0-9]*"' | tr '\n' ' '; echo
grep -o '<test-case [^>]*result="Failed"[^>]*' "$OUT/$2.xml" | grep -o 'fullname="[^"]*"'
git checkout -- "game/The Last Fortress/ProjectSettings/EditorBuildSettings.asset" "game/The Last Fortress/ProjectSettings/ProjectSettings.asset" "game/The Last Fortress/ProjectSettings/GraphicsSettings.asset" "game/The Last Fortress/Assets/Settings/PC_RPAsset.asset" 2>/dev/null
