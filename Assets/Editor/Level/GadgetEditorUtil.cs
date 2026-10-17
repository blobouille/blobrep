using UnityEditor;
using UnityEngine;

static class GadgetEditorUtil
{
    public static void Create<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.AddComponent<T>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    // Poignée ronde sur le bord droit du cercle : renvoie le nouveau rayon (inchangé sans glissement).
    public static float RadiusHandle(Vector3 center, float radius, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            Vector3 edge = center + Vector3.right * radius;
            float hs = HandleUtility.GetHandleSize(edge) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider(edge, Vector3.right, hs, Handles.SphereHandleCap, 0f);
            return EditorGUI.EndChangeCheck() ? dragged.x - center.x : radius;
        }
    }
}
