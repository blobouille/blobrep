#!/bin/bash
# Bac à sable de la gelée : un tour complet du scénario (≈ 27 s) relevé par Assets/Temp/JellyWatch (triangles retournés,
# centre hors du corps, coût), pires cas photographiés dans Captures/gelee/<nom>/. Usage : lab_watch.sh nom ["code C# avant"]
cd "$(dirname "$0")/../.."
name=${1:-lab}; pre=${2:-}
rm -f Captures/gelee/pire_*
unity command eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/space bac à sable.unity"); return "ok";' >/dev/null 2>&1
unity command eval 'UnityEditor.EditorApplication.isPlaying = true; return "ok";' >/dev/null 2>&1; sleep 8
unity command eval "$pre LevelRestart.Restart(); JellyWatch.Begin(1650); return \"ok\";" 2>&1 | tail -1 | grep -oE '"result":"[^"]*"|rror.*'
for i in $(seq 1 40); do r=$(unity command eval 'return JellyWatch.Report;' 2>&1 | tail -1 | grep -o '"result":"[^"]*"'); case "$r" in *images*) echo "$name $r"; break;; esac; sleep 2; done
unity command eval 'UnityEditor.EditorApplication.isPlaying = false; return "ok";' >/dev/null 2>&1; sleep 3
mkdir -p "Captures/gelee/$name"; rm -f "Captures/gelee/$name"/*; mv Captures/gelee/pire_* "Captures/gelee/$name/" 2>/dev/null
