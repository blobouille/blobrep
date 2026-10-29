#!/bin/bash
# Images de la fusion bleu – violet – bleu (« space niveau 1 », en Play) : Captures/fusion/bvb_<inclinaison>_<pas>.png
# Usage : fusion_bvb_shot.sh [inclinaison du tir en degrés] [pas après la naissance…]
cd "$(dirname "$0")/../.."
tilt=${1:-6}; shift
mkdir -p Captures/fusion
for n in ${@:--2 0 5 10 15 20 30 45}; do
  echo "$tilt $n" > Captures/bancs/fusion_bvb_shot.txt
  unity command --timeout 60 eval_file Captures/bancs/fusion_bvb_shot.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*'
  sleep 1.5
  unity command capture_game_view --source screen --save_path "Captures/fusion/bvb_${tilt}_$n.png" >/dev/null 2>&1
done
unity command eval 'Time.timeScale = 1f; return "ok";' >/dev/null 2>&1
mv Assets/Captures/fusion/*.png Captures/fusion/ 2>/dev/null; rm -rf Assets/Captures Assets/Captures.meta; ls Captures/fusion | grep bvb
