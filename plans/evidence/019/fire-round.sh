#!/bin/sh
# Usage: fire-round.sh <n>  Build, capture the fire set at 1280x720, make a contact sheet.
cd "$(dirname "$0")/../../.." || exit 1
E="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"; "$E" -batchmode -quit -projectPath "$(pwd -W)/game/The Last Fortress" -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows -releaseOutput Builds/Release-fire -logFile "$(pwd -W)/plans/evidence/019/fire/Build-$1.log"; grep "result=\|errors=" "game/The Last Fortress/Builds/Release-fire/build-report.txt"; grep -m3 "error CS" "plans/evidence/019/fire/Build-$1.log"
rm -f .git/index.lock 2>/dev/null
git checkout -- "game/The Last Fortress/Assets/Settings/PC_RPAsset.asset" "game/The Last Fortress/ProjectSettings/EditorBuildSettings.asset" "game/The Last Fortress/ProjectSettings/GraphicsSettings.asset" "game/The Last Fortress/ProjectSettings/ProjectSettings.asset" 2>/dev/null
T="game/The Last Fortress/TestResults/019/fire-$1"; rm -rf "$T"
sh plans/evidence/019/capture.sh "game/The Last Fortress/Builds/Release-fire" "$T" 1280 720 fire > /dev/null 2>&1
python - "$T" "plans/evidence/019/fire/round-$1.png" <<'PY'
import sys, glob
from PIL import Image
d, out = sys.argv[1], sys.argv[2]
names = [f"{d}/1280x720-fire-{v}-{i}.png" for v in ("strategic", "hero") for i in (1, 3)]
ims = [Image.open(n) for n in names]
sheet = Image.new('RGB', (1280, 1080))
for k, im in enumerate(ims): sheet.paste(im.resize((640, 360)), ((k % 2) * 640, (k // 2) * 360))
for k, im in enumerate(ims[2:]): sheet.paste(im.crop((480, 300, 800, 480)).resize((640, 360)), (k * 640, 720))  # hero view, 2x
sheet.save(out)
print("sheet", out)
PY
