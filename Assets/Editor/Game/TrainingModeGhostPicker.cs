using UnityEditor;
using UnityEngine;

// Hors Play : clic sur un fantôme du mode entraînement dans la Scene view = point de départ du prochain Play (jaune).
// Re-clic sur le fantôme jaune = départ normal. Le clic n'est pris que s'il touche un fantôme : ailleurs, sélection normale.
[InitializeOnLoad]
static class TrainingModeGhostPicker
{
    static TrainingModeGhostPicker() => SceneView.duringSceneGui += OnSceneGUI;

    static void OnSceneGUI(SceneView view)
    {
        if (Application.isPlaying) return;
        var e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt || e.control || e.shift || e.command) return;

        var training = Object.FindAnyObjectByType<TrainingMode>();
        if (training == null || !training.showGhosts || training.Count == 0) return;

        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Vector2 point = ray.origin;
        if (Mathf.Abs(ray.direction.z) > 1e-6f) point = ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
        if (!training.GhostAt(point, out int index)) return;

        training.Select(index == training.Cursor ? -1 : index);
        e.Use();
        SceneView.RepaintAll();
    }
}
