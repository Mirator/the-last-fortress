#!/bin/sh
# Usage: build.sh <log-path> <output-dir under the Unity project, e.g. Builds/Plan023-after>
# Builds the release player in batch mode, prints the build report, reverts batch ProjectSettings noise.
cd "$(dirname "$0")/../../.." || exit 1
E="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
P="$(pwd -W)/game/The Last Fortress"
"$E" -batchmode -quit -projectPath "$P" -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows -releaseOutput "$2" -logFile "$1"
echo "exit=$?"
cat "game/The Last Fortress/$2/build-report.txt" 2>/dev/null || grep -m3 "Build Finished\|Build succeeded\|Result" "$1"
grep -m5 "error CS" "$1"
git checkout -- "game/The Last Fortress/ProjectSettings/EditorBuildSettings.asset" "game/The Last Fortress/ProjectSettings/ProjectSettings.asset" "game/The Last Fortress/ProjectSettings/GraphicsSettings.asset" "game/The Last Fortress/Assets/Settings/PC_RPAsset.asset" 2>/dev/null
