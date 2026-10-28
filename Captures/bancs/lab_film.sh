#!/bin/bash
# Bac à sable de la gelée : Play sur `space bac à sable`, scénario complet (26 s) filmé une image tous les 6 pas
# → Captures/gelee/<nom>_NNN.png + planche Captures/gelee/planche_<nom>.png. Usage : lab_film.sh [nom]
cd "$(dirname "$0")/../.."
name=${1:-lab}
rm -f Captures/gelee/${name}_*.png
unity command eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/space bac à sable.unity"); return "ok";' >/dev/null 2>&1
unity command eval 'UnityEditor.EditorApplication.isPlaying = true; return "ok";' >/dev/null 2>&1; sleep 9
unity command eval "LevelRestart.Restart(); return \"ok\";" >/dev/null 2>&1; sleep 1
unity command eval "var lab = UnityEngine.Object.FindAnyObjectByType<GumoriJellyLab>(); JellyRec.Stride = 6; JellyRec.Begin(lab.Gumori, 260, \"$name\", lab.Gumori.diameter * 1.1f); return \"ok\";" 2>&1 | tail -1 | grep -o 'rror.*'
for i in $(seq 1 40); do r=$(unity command eval 'return JellyRec.Status;' 2>&1 | tail -1); case "$r" in *fini*) break;; esac; sleep 2; done
unity command eval 'UnityEditor.EditorApplication.isPlaying = false; JellyRec.Stride = 1; return "ok";' >/dev/null 2>&1; sleep 3
magick montage Captures/gelee/${name}_*.png -tile 13x -geometry 120x120+1+1 -background '#222' Captures/gelee/planche_${name}.png
