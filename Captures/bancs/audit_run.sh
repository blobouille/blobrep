#!/bin/bash
# Joue audit_ref.cs deux fois dans le réservoir et dans « space endless 1 » (ouvre la scène, Play, partie, Stop).
cd "$(dirname "$0")/../.."
for s in reservoir "space endless 1"; do
  unity command eval "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Slime/$s.unity\"); return \"ok\";" >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1
  sleep 6
  echo "== $s"
  for k in 1 2; do
    unity command --timeout 60 eval_file Captures/bancs/audit_ref.cs 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|rror.*' | head -3
  done
  unity command editor_stop >/dev/null 2>&1
  sleep 3
done
