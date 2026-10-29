#!/bin/bash
# Images de gumori pliés (niveau à bocaux, en Play) : Captures/fusion/pli_<rang>.png. Usage : fold_shot.sh [gap] [rangs…]
cd "$(dirname "$0")/../.."
gap=${1:-120}; shift
mkdir -p Captures/fusion
for r in ${@:-0 1 2 3}; do
  echo "$gap $r ${OFFSET:-0}" > Captures/bancs/fold_shot.txt
  unity command --timeout 120 eval_file Captures/bancs/fold_shot.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*'
  sleep 1.5
  unity command capture_game_view --source screen --save_path "Captures/fusion/pli_$r.png" >/dev/null 2>&1
done
unity command eval 'Time.timeScale = 1f; return "ok";' >/dev/null 2>&1
mv Assets/Captures/fusion/*.png Captures/fusion/ 2>/dev/null; rm -rf Assets/Captures Assets/Captures.meta
