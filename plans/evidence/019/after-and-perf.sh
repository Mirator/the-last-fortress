#!/bin/sh
# After-captures and paired stress runs for plan 019 (Editor closed). Run from the repository root.
B="game/The Last Fortress/Builds/Release"
T="game/The Last Fortress/TestResults/019/after"
M="plans/evidence/019/motion"
for r in "1280 720" "1920 1080"; do
  set -- $r
  sh plans/evidence/019/capture.sh "$B" "$T" $1 $2 hero-motion > /dev/null 2>&1
  cp "$T/${1}x${2}-hero-motion.mp4" "$M/after-${1}x${2}-hero-motion.mp4"; cp "$T/${1}x${2}-hero-log.txt" "$M/after-${1}x${2}-hero-log.txt"
  sh plans/evidence/019/capture.sh "$B" "$T" $1 $2 hero-camera > /dev/null 2>&1
  ffmpeg -loglevel error -y -framerate 30 -i "$T/${1}x${2}-camera-%03d.png" -c:v libx264 -pix_fmt yuv420p -crf 23 "$M/after-${1}x${2}-hero-camera.mp4"
  cp "$T/${1}x${2}-camera-log.txt" "$M/after-${1}x${2}-camera-log.txt"
done
P="plans/evidence/019/perf"; mkdir -p "$P"; PW="$(cd "$P" && pwd -W)"
n=0
for round in 1 2; do
  for r in "1920 1080" "1280 720"; do
    set -- $r
    for view in strategic hero; do
      n=$((n+1)); extra=""; [ "$view" = hero ] && extra="-hero-camera"
      "$B/TheLastFortress.exe" -stress -stress-hero $extra -measure "$PW/$n-$view-${1}x${2}.txt" -screen-width $1 -screen-height $2 -screen-fullscreen 0 -logFile "$PW/$n-$view-${1}x${2}.log"
      grep -h "median_ms\|^hero=\|gate_pass" "$P/$n-$view-${1}x${2}.txt" | tr '\n' ' '; echo " <- $n $view ${1}x${2}"
    done
  done
done
