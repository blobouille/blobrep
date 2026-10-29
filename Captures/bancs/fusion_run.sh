#!/bin/bash
# Joue fusion_compare.cs avec les deux méthodes (Cacahuète, Absorption), deux fois chacune, dans le réservoir et « space endless 1 ».
cd "$(dirname "$0")/../.."
for s in reservoir "space endless 1"; do
  unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/$s.unity\"); return \"ok\";" >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1
  sleep 6
  echo "== $s"
  for style in 0 1; do
    echo $style > Captures/bancs/fusion_style.txt
    for k in 1 2; do
      unity command --timeout 60 eval_file Captures/bancs/fusion_compare.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*' | head -3
    done
  done
  unity command editor_stop >/dev/null 2>&1
  sleep 3
done
