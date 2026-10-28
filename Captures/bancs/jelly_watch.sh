#!/bin/bash
# Gelée en Play (scène ouverte, Space) : partie de N images avec un tir à pleine vitesse toutes les 0,5 s, coût et plis
# (Assets/Temp/JellyWatch). Usage : jelly_watch.sh [nom] [images]. Pires cas : Captures/gelee/pire_*.png.
cd "$(dirname "$0")/../.."
free=${1:-gelee}; frames=${2:-1500}
rm -f Captures/gelee/pire_*.png
unity command eval 'UnityEditor.EditorApplication.isPlaying = true; return "ok";' >/dev/null 2>&1; sleep 10
unity command eval "JellyWatch.Begin($frames); return \"ok\";" 2>&1 | tail -1 | grep -o 'rror.*' 
for i in $(seq 1 $((frames / 50))); do unity command eval_file Captures/bancs/jelly_fire.cs >/dev/null 2>&1; sleep 0.5; done
for i in $(seq 1 20); do r=$(unity command eval 'return JellyWatch.Report;' 2>&1 | tail -1 | grep -o '"result":"[^"]*"'); case "$r" in *images*) echo "$free $r"; break;; esac; sleep 2; done
unity command eval 'UnityEditor.EditorApplication.isPlaying = false; return "ok";' >/dev/null 2>&1; sleep 3
