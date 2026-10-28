#!/bin/sh
# Usage: build.sh <log-path>  Builds Builds/Release in batch mode, prints the build report, reverts batch ProjectSettings noise.
cd "$(dirname "$0")/../../.." || exit 1
E="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
P="$(pwd -W)/game/The Last Fortress"
"$E" -batchmode -quit -projectPath "$P" -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows -logFile "$1"
echo "exit=$?"
cat "game/The Last Fortress/Builds/Release/build-report.txt"
grep -m5 "error CS" "$1"
git checkout -- "game/The Last Fortress/ProjectSettings/EditorBuildSettings.asset" "game/The Last Fortress/ProjectSettings/ProjectSettings.asset" "game/The Last Fortress/ProjectSettings/GraphicsSettings.asset" "game/The Last Fortress/Assets/Settings/PC_RPAsset.asset" 2>/dev/null
