#!/bin/bash
# Empreinte des trois scènes de jeu (audit_ref.cs : terrestre et space endless 1 ; level_bot.cs : space niveau 1), deux
# parties chacune. À lancer avant / après une refonte : tout doit être identique au bit près.
cd "$(dirname "$0")/../.."
[ "$1" = niveau ] || bash Captures/bancs/audit_run.sh
unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/space niveau 1.unity\"); return \"ok\";" >/dev/null 2>&1
unity command editor_play >/dev/null 2>&1
sleep 6
echo "== space niveau 1"
echo "120 1 10" > Captures/bancs/level_bot.txt
for k in 1 2; do
  unity command --timeout 120 eval_file Captures/bancs/level_bot.cs 2>&1 | tail -1 | grep -o 'res=[^\]*\|rror.*' | head -3
done
unity command editor_stop >/dev/null 2>&1
sleep 3
unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/space endless 1.unity\"); return \"ok\";" >/dev/null 2>&1
