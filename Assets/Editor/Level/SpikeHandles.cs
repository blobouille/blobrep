using UnityEditor;
using UnityEngine;

// Poignées communes à tous les obstacles à piques : une par pointe, glissée le long du pique = sa taille individuelle.
public static class SpikeHandles
{
    // À appeler dans un Handles.DrawingScope dont la matrice est hazard.LocalToWorld.
    public static void Draw(SpikedHazard hazard)
    {
        hazard.SyncSizes();
        for (int i = 0; i < hazard.SpikeCount; i++)
        {
            hazard.SpikeFrame(i, out var c, out var up, out _, out float height);
            Vector3 tip = hazard.TipOf(i);
            float size = HandleUtility.GetHandleSize(hazard.LocalToWorld.MultiplyPoint3x4(tip)) * 0.05f;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(tip, up, size, Handles.DotHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) continue;

            float unitHeight = height / hazard.SizeOf(i);
            float newHeight = Vector2.Dot((Vector2)moved - c, up);
            Undo.RecordObject(hazard, "Spike size");
            hazard.sizes[i] = Mathf.Max(0.01f, newHeight / unitHeight);
        }
    }
}
