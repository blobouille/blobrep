using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoostZone))]
public class BoostZoneEditor : Editor
{
    [MenuItem("GameObject/2D Object/Boost Zone", false, 30)]
    static void Create()
    {
        var go = new GameObject("Boost Zone");
        Undo.RegisterCreatedObjectUndo(go, "Create Boost Zone");
        go.AddComponent<BoostZone>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Poignée ronde au bout de la flèche : longueur\n" +
            "• Poignée ronde en haut : hauteur\n" +
            "• Tourner l'objet pour orienter la flèche",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var zone = (BoostZone)target;
        using (new Handles.DrawingScope(zone.previewColor, zone.LocalToWorld))
        {
            Vector3 end = new Vector3(0.5f * zone.length, 0f, 0f);
            float hs = HandleUtility.GetHandleSize(zone.LocalToWorld.MultiplyPoint3x4(end)) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 draggedEnd = Handles.Slider(end, Vector3.right, hs, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Boost zone length");
                zone.length = Mathf.Max(0.1f, 2f * draggedEnd.x);
            }

            Vector3 top = new Vector3(0f, 0.5f * zone.height, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 draggedTop = Handles.Slider(top, Vector3.up, hs, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Boost zone height");
                zone.height = Mathf.Max(0.1f, 2f * draggedTop.y);
            }
        }
    }
}
