using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpikeRow))]
public class SpikeRowEditor : Editor
{
    [MenuItem("GameObject/2D Object/Spike Row", false, 11)]
    static void Create()
    {
        var go = new GameObject("Spike Row");
        Undo.RegisterCreatedObjectUndo(go, "Create Spike Row");
        go.AddComponent<SpikeRow>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Poignée sur la pointe d'un pique : sa taille individuelle\n" +
            "• Poignée carrée à droite : taille de tous (globalScale)\n" +
            "• Poignée ronde à droite : nombre de piques",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var row = (SpikeRow)target;
        using (new Handles.DrawingScope(row.previewColor, row.LocalToWorld))
        {
            SpikeHandles.Draw(row);

            row.Layout(row.count - 1, out float left, out float width, out _);
            Vector3 right = new Vector3(left + width, 0f, 0f);
            float hs = HandleUtility.GetHandleSize(row.LocalToWorld.MultiplyPoint3x4(right));

            Vector3 scaleHandle = right + new Vector3(0.3f * hs, 0.3f * hs, 0f);
            EditorGUI.BeginChangeCheck();
            float scale = Handles.ScaleValueHandle(row.globalScale, scaleHandle, Quaternion.identity, hs * 0.6f, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(row, "Spike row scale");
                row.globalScale = Mathf.Max(0.01f, scale);
            }

            Vector3 countHandle = right + new Vector3(0.3f * hs, 0f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider(countHandle, Vector3.right, hs * 0.07f, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                // Rangée centrée : un pique de plus ne décale le bord droit que d'un demi-pas.
                float halfPitch = 0.5f * (row.spikeWidth + row.spacing) * row.globalScale;
                int delta = Mathf.RoundToInt((dragged.x - countHandle.x) / halfPitch);
                if (delta != 0)
                {
                    Undo.RecordObject(row, "Spike count");
                    row.count = Mathf.Max(1, row.count + delta);
                    row.SyncSizes();
                }
            }
        }
    }
}
