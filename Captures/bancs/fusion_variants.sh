#!/bin/bash
# Joue fusion_compare.cs une fois par jeu de réglages (arguments entre guillemets, ex. "1 inherit=0"), dans une scène.
# Usage : SCENE="space endless 1" fusion_variants.sh "0" "1" "1 stiff=1"
cd "$(dirname "$0")/../.."
scene=${SCENE:-space endless 1}
unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/$scene.unity\"); return \"ok\";" >/dev/null 2>&1
unity command editor_play >/dev/null 2>&1
sleep 6
for v in "$@"; do
  echo "$v" > Captures/bancs/fusion_style.txt
  echo "== $v"
  unity command --timeout 60 eval_file Captures/bancs/fusion_compare.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*' | head -3 | sed 's/chevauchement à la naissance/chev./'
done
unity command editor_stop >/dev/null 2>&1
sleep 2
