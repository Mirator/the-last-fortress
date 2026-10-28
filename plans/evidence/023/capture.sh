#!/bin/sh
# Usage: capture.sh <build-dir> <out-dir>  Runs -capture-set 023 offscreen at 1280x720 and 1920x1080 (repository root).
cd "$(dirname "$0")/../../.." || exit 1
BUILD="$1"; OUT="$2"
mkdir -p "$OUT"; OUTW="$(cd "$OUT" && pwd -W)"
for r in "1280 720" "1920 1080"; do
  set -- $r
  "$BUILD/TheLastFortress.exe" -capture "$OUTW" -capture-set 023 -saves "$OUTW/saves-$1" -offscreen -screen-width $1 -screen-height $2 -screen-fullscreen 0 -logFile "$OUTW/player-${1}x${2}.log"
  echo "$1x$2 exit=$? frames=$(ls "$OUT" | grep -c "^${1}x${2}-023-.*png")"
done
