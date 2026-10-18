using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GlassPane))]
[CanEditMultipleObjects]
public class GlassPaneEditor : Editor
{
    static GlassPreset _preset = GlassPreset.Window;

    [MenuItem("GameObject/2D Object/Glass Pane", false, 31)]
    static void Create()
    {
        var go = new GameObject("Glass Pane");
        Undo.RegisterCreatedObjectUndo(go, "Create Glass Pane");
        go.AddComponent<GlassPane>();
        go.AddComponent<GlassDust>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.BeginHorizontal();
        _preset = (GlassPreset)EditorGUILayout.EnumPopup(new GUIContent("Préréglage",
            "Remplit tous les réglages de résistance, de motif et de projection (la taille, la graine, les couleurs et\n" +
            "les options des morceaux ne changent pas) :\n" +
            "• Window : vitre ordinaire (valeurs par défaut)\n" +
            "• Tempered : verre trempé, solide mais part entièrement en petits cubes\n" +
            "• Sugar : verre de cinéma, casse au moindre choc presque sans freiner\n" +
            "• Armored : vitre épaisse, plusieurs chocs, gros morceaux, freine fort\n" +
            "• Cinematic : propagation lente et visible, casse totale, gerbe large"), _preset);
        if (GUILayout.Button("Appliquer", GUILayout.Width(80)))
            foreach (var t in targets)
            {
                Undo.RecordObject(t, "Glass preset");
                ((GlassPane)t).ApplyPreset(_preset);
                EditorUtility.SetDirty(t);
            }
        EditorGUILayout.EndHorizontal();

        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Poignée ronde au bout : longueur\n" +
            "• Poignée ronde en haut : épaisseur\n" +
            "• Traits : milieu (le plus fragile) et appuis\n" +
            "Seuls les corps dont les formes ont triggerEvents la cassent (BLOB, morceaux de verre).",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var pane = (GlassPane)target;
        using (new Handles.DrawingScope(pane.glassColor, pane.LocalToWorld))
        {
            Vector3 end = new Vector3(0.5f * pane.length, 0f, 0f);
            float hs = HandleUtility.GetHandleSize(pane.LocalToWorld.MultiplyPoint3x4(end)) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 draggedEnd = Handles.Slider(end, Vector3.right, hs, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pane, "Glass length");
                pane.length = Mathf.Max(0.05f, 2f * draggedEnd.x);
            }

            Vector3 top = new Vector3(0f, 0.5f * pane.thickness, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 draggedTop = Handles.Slider(top, Vector3.up, hs, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pane, "Glass thickness");
                pane.thickness = Mathf.Max(0.01f, 2f * draggedTop.y);
            }
        }
    }
}
