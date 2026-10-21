#!/bin/sh
# Usage: capture.sh <build-dir> <out-dir> <width> <height> [set]  Runs a capture set offscreen in a window, then encodes the
# hero-motion frames to an MP4 next to them (30 fps).
cd "$(dirname "$0")/../../.." || exit 1
BUILD="$1"; OUT="$2"; W="$3"; H="$4"; SET="${5:-hero-motion}"
mkdir -p "$OUT"; OUTW="$(cd "$OUT" && pwd -W)"
"$BUILD/TheLastFortress.exe" -capture "$OUTW" -capture-set "$SET" -saves "$OUTW/saves" -offscreen -screen-width "$W" -screen-height "$H" -screen-fullscreen 0 -logFile "$OUTW/player-${W}x${H}.log"
echo "exit=$?"
ls "$OUT" | grep -c "hero-[0-9]*.png"
if [ "$SET" = "hero-motion" ]; then
  ffmpeg -loglevel error -y -framerate 30 -i "$OUT/${W}x${H}-hero-%03d.png" -c:v libx264 -pix_fmt yuv420p -crf 23 "$OUT/${W}x${H}-hero-motion.mp4" && echo "mp4 ok"
fi
