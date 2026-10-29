#!/bin/bash
# Captures au ralenti du fondu de design pendant une fusion (GumoriSkin.fadeDesign) : Captures/fusion/fondu_<n>.png
# Usage : fade_shot.sh [scène] [méthode 0 = Cacahuète, 1 = Absorption] (défauts : reservoir, 0).
cd "$(dirname "$0")/../.."
scene=${1:-reservoir}
mkdir -p Captures/fusion
unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/$scene.unity\"); return \"ok\";" >/dev/null 2>&1
unity command editor_play >/dev/null 2>&1
sleep 6
unity command eval 'UnityEngine.Object.FindAnyObjectByType<GumoriSkin>().fadeDesign = true; return "ok";' >/dev/null 2>&1
echo ${2:-0} > Captures/bancs/fusion_style.txt
unity command --timeout 60 eval_file Captures/bancs/fusion_shot.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*'
unity command eval "Time.timeScale = ${SLOW:-0.025}f; return \"ok\";" >/dev/null 2>&1
for n in 1 2 3 4 5 6 7 8; do
  unity command capture_game_view --source screen --save_path "Captures/fusion/fondu_${scene// /_}_$n.png" >/dev/null 2>&1
  sleep ${GAP:-0.3}
done
unity command eval "Time.timeScale = 1f; return \"ok\";" >/dev/null 2>&1
unity command editor_stop >/dev/null 2>&1
mv Assets/Captures/fusion/*.png Captures/fusion/ 2>/dev/null; rm -rf Assets/Captures Assets/Captures.meta; ls Captures/fusion | grep fondu
