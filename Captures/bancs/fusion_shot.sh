#!/bin/bash
# Série de captures d'une fusion au ralenti, pour chaque méthode : Captures/fusion/<scène>_<méthode>_<n>.png
# Usage : fusion_shot.sh [scène] (défaut : reservoir).
cd "$(dirname "$0")/../.."
scene=${1:-reservoir}
mkdir -p Captures/fusion
for style in 0 1; do
  name=$([ $style = 0 ] && echo cacahuete || echo absorption)
  unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/$scene.unity\"); return \"ok\";" >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1
  sleep 6
  echo $style > Captures/bancs/fusion_style.txt
  unity command --timeout 60 eval_file Captures/bancs/fusion_shot.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*'
  for n in 1 2 3 4 5 6 7 8; do
    unity command capture_game_view --source screen --save_path "Captures/fusion/${scene// /_}_${name}_$n.png" >/dev/null 2>&1
    sleep 0.6
  done
  unity command eval "Time.timeScale = 1f; return \"ok\";" >/dev/null 2>&1
  unity command editor_stop >/dev/null 2>&1
  sleep 3
done
mv Assets/Captures/fusion/*.png Captures/fusion/ 2>/dev/null; rm -rf Assets/Captures Assets/Captures.meta; ls Captures/fusion | head -40
