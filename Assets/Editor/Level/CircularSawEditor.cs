using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CircularSaw))]
public class CircularSawEditor : Editor
{
    [MenuItem("GameObject/2D Object/Circular Saw", false, 12)]
    static void Create()
    {
        var go = new GameObject("Circular Saw");
        Undo.RegisterCreatedObjectUndo(go, "Create Circular Saw");
        go.AddComponent<CircularSaw>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Poignée extérieure : diamètre\n" +
            "• Poignée intérieure : profondeur des dents\n" +
            "• Poignée sur la pointe d'une dent : sa taille individuelle",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var saw = (CircularSaw)target;

        using (new Handles.DrawingScope(saw.previewColor, saw.LocalToWorld))
        {
            SpikeHandles.Draw(saw);

            float hs = HandleUtility.GetHandleSize(saw.transform.position);

            Vector3 outer = new Vector3(saw.OuterRadius, 0f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 o = Handles.Slider(outer, Vector3.right, hs * 0.07f, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(saw, "Saw diameter");
                saw.diameter = Mathf.Max(0.1f, 2f * o.x);
            }

            Vector3 core = new Vector3(0f, -saw.CoreRadius, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 c = Handles.Slider(core, Vector3.up, hs * 0.06f, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(saw, "Saw tooth depth");
                saw.toothDepth = Mathf.Clamp(saw.OuterRadius + c.y, 0.01f, 0.9f * saw.OuterRadius);
            }
        }
    }
}
