using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PinballBumper))]
public class PinballBumperEditor : Editor
{
    [MenuItem("GameObject/2D Object/Pinball Bumper", false, 21)]
    static void Create()
    {
        var go = new GameObject("Pinball Bumper");
        Undo.RegisterCreatedObjectUndo(go, "Create Pinball Bumper");
        go.AddComponent<PinballBumper>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Poignée ronde sur la position active : déplacement (travel)\n" +
            "• Tourner l'objet pour orienter la barre ; le déplacement est perpendiculaire à la barre",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var bumper = (PinballBumper)target;
        bumper.BuildShape(out _, out var c2, out _);
        using (new Handles.DrawingScope(bumper.previewActiveColor, bumper.LocalToWorld))
        {
            Vector3 handle = new Vector3(0.5f * c2.x, bumper.travel, 0f);
            float hs = HandleUtility.GetHandleSize(bumper.LocalToWorld.MultiplyPoint3x4(handle)) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider(handle, Vector3.up, hs, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bumper, "Bumper travel");
                bumper.travel = dragged.y;
            }
        }
    }
}
