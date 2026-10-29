#!/bin/bash
# Captures en portrait (540 × 960) d'une partie du bot au ralenti autour d'un moment : Captures/niveau/<nom>_<n>.png
# Usage : level_shots.sh nom tirs_avant [images] [écart s] [ralenti]
cd "$(dirname "$0")/../.."
echo "96 1 $2 ${6:-0} ${5:-0.2}" > Captures/bancs/level_bot.txt
unity command --timeout 300 eval_file Captures/bancs/level_bot.cs 2>&1 | tail -1 | cut -c1-300
unity command eval "Time.timeScale = ${5:-0.2}f; return \"ok\";" >/dev/null 2>&1
for n in $(seq 1 ${3:-6}); do
  echo "$1_$n" > Captures/bancs/shot.txt
  unity command --timeout 60 eval_file Captures/bancs/portrait.cs >/dev/null 2>&1
  sleep ${4:-0.4}
done
unity command eval "Time.timeScale = 1f; return \"ok\";" >/dev/null 2>&1
sleep 1; mv Assets/Temp/$1_*.png Captures/niveau/ 2>/dev/null; ls Captures/niveau | grep "^$1_"
