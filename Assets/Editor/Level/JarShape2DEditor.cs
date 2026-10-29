using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JarShape2D))]
public class JarShape2DEditor : Editor
{
    [MenuItem("GameObject/2D Object/Bocal", false, 11)]
    static void CreateFromMenu() => Selection.activeGameObject = Create(Selection.activeTransform);

    // Bocal (courbe + collision + forme) ; sous 'parent' s'il y en a un (ex. une trajectoire), sinon au centre de la Scene view.
    public static GameObject Create(Transform parent)
    {
        var go = new GameObject("Bocal");
        Undo.RegisterCreatedObjectUndo(go, "Create Bocal");
        go.AddComponent<CurvePath2D>();
        go.AddComponent<CurveCollider2D>();
        go.AddComponent<JarShape2D>().Apply();
        if (parent != null) go.transform.SetParent(parent, false);
        else
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        }
        return go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var jar = (JarShape2D)target;
        EditorGUILayout.HelpBox(
            $"Ouverture intérieure : {jar.InnerNeckHalf * 2f:0.00} m · ventre : {jar.radius * 2f:0.00} m · hauteur intérieure : {jar.InnerTop:0.00} m\n" +
            "Poignées dans la Scene view : rayon (côté), goulot (haut), hauteur du ventre (épaule).\n" +
            "Pour le faire bouger : le mettre enfant d'une trajectoire (GameObject > 2D Object > Moving Path).",
            MessageType.Info);
    }

    void OnSceneGUI()
    {
        var jar = (JarShape2D)target;
        if (!jar.drive) return;
        var t = jar.transform;
        Handles.matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        float size = HandleUtility.GetHandleSize(t.position) * 0.08f;
        Handles.color = new Color(1f, 0.6f, 0.2f);

        float yBelly = jar.bottomRound * jar.radius;
        float yShoulder = yBelly + jar.bellyHeight;
        float yTop = jar.InnerTop;

        EditorGUI.BeginChangeCheck();
        Vector3 r = Handles.Slider(new Vector3(jar.radius, 0.5f * (yBelly + yShoulder)), Vector3.right, size, Handles.DotHandleCap, 0f);
        Vector3 neck = Handles.Slider(new Vector3(jar.InnerNeckHalf, yTop), Vector3.right, size, Handles.DotHandleCap, 0f);
        Vector3 belly = Handles.Slider(new Vector3(0f, yShoulder), Vector3.up, size, Handles.DotHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(new Object[] { jar, jar.Path }, "Bocal");
            jar.radius = Mathf.Max(0.1f, r.x);
            jar.neckWidth = Mathf.Max(0.05f, 2f * neck.x);
            jar.bellyHeight = Mathf.Max(0f, belly.y - yBelly);
            jar.Apply();
            EditorUtility.SetDirty(jar);
            EditorUtility.SetDirty(jar.Path);
        }
        Handles.matrix = Matrix4x4.identity;
    }
}
