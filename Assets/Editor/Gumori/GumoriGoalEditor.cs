using UnityEditor;
using UnityEngine;

// Difficulté du niveau sous l'inspecteur de GumoriGoal : coups nécessaires sans chaîne (fusions parfaites) contre coups donnés.
[CustomEditor(typeof(GumoriGoal))]
public class GumoriGoalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var goal = (GumoriGoal)target;
        int targetPoints = goal.autoTarget ? goal.ComputeTarget() : goal.targetScore;
        int needed = goal.MovesNeeded(targetPoints);
        string text;
        if (needed == 0)
            text = $"Objectif {targetPoints:N0} : impossible sans chaîne en {goal.maxDrops} coups (même en fusionnant parfaitement).";
        else
        {
            float margin = (float)goal.maxDrops / needed - 1f;
            string label = margin >= goal.easyExtraMoves ? "Facile" : margin >= goal.mediumExtraMoves ? "Moyen" : margin >= goal.hardExtraMoves ? "Difficile" : "Plus que difficile";
            text = $"Objectif {targetPoints:N0} : {needed} coups nécessaires sans chaîne (fusions parfaites), {goal.maxDrops} donnés\n" +
                   $"→ {margin * 100f:+0.#;-0.#;0} % de coups en plus : {label}" +
                   (goal.comboShare > 0f ? $"\n(dont {goal.comboShare * 100f:0} % de l'objectif à faire en chaînes)" : "");
        }
        EditorGUILayout.HelpBox(text, needed == 0 ? MessageType.Warning : MessageType.Info);
    }
}
