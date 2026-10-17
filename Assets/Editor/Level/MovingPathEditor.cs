using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MovingPath))]
public class MovingPathEditor : Editor
{
    static PathShape _shape = PathShape.Circle;
    static float _radius = 3f;
    static float _rotation;
    static int _sides = 6;
    static float _scaleFactor = 1.25f;
    static float _turnAngle = 45f;

    [MenuItem("GameObject/2D Object/Moving Path", false, 40)]
    static void Create()
    {
        var go = new GameObject("Moving Path");
        Undo.RegisterCreatedObjectUndo(go, "Create Moving Path");
        var curve = go.AddComponent<CurvePath2D>();
        curve.thickness = 0.05f;
        PathShapes.Apply(curve, PathShape.Circle, 3f, 0f, 6);
        go.AddComponent<MovingPath>();
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        if (Selection.activeTransform != null) go.transform.SetParent(Selection.activeTransform, true);
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        var path = (MovingPath)target;
        serializedObject.Update();
        if (path.evenSpacing) DrawPropertiesExcluding(serializedObject, "positions");
        else DrawPropertiesExcluding(serializedObject, "phase");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Éléments (enfants)", EditorStyles.boldLabel);
        DrawCount(path);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Scie")) AddChild<CircularSaw>(path, "Circular Saw");
            if (GUILayout.Button("+ Piques")) AddChild<SpikeRow>(path, "Spike Row");
            if (GUILayout.Button("+ Boost")) AddChild<BoostZone>(path, "Boost Zone");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Forme de la trajectoire", EditorStyles.boldLabel);
        DrawShapes(path);

        EditorGUILayout.HelpBox(
            "• Chaque enfant actif est un élément (scie, piques, boost…), tous à la même vitesse\n" +
            "• Déplacer un enfant dans la Scene view : il est ramené sur la trajectoire et sa position de départ suit\n" +
            "• Forme : points et tangentes de la courbe (composant Curve Path 2D), ou formes toutes faites ci-dessus\n" +
            "• Les enfants sont affichés à leur position de départ (état au redémarrage)",
            MessageType.Info);
    }

    static void DrawCount(MovingPath path)
    {
        int count = path.transform.childCount;
        using (new EditorGUI.DisabledScope(count == 0))
        {
            EditorGUI.BeginChangeCheck();
            int wanted = Mathf.Clamp(EditorGUILayout.DelayedIntField(
                new GUIContent("Nombre", "Nombre d'enfants. Augmenter duplique le dernier enfant, diminuer supprime les derniers."),
                count), 1, 200);
            if (EditorGUI.EndChangeCheck() && count > 0) SetCount(path, wanted);
        }
        if (count == 0) EditorGUILayout.HelpBox("Aucun élément : ajouter un enfant (boutons ci-dessous ou glisser un objet sous la trajectoire).", MessageType.None);
    }

    static void SetCount(MovingPath path, int wanted)
    {
        var t = path.transform;
        Undo.SetCurrentGroupName("Moving Path count");
        while (t.childCount < wanted)
        {
            var last = t.GetChild(t.childCount - 1).gameObject;
            var copy = Instantiate(last, t);
            copy.name = last.name;
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate path element");
        }
        while (t.childCount > wanted) Undo.DestroyObjectImmediate(t.GetChild(t.childCount - 1).gameObject);
        path.SyncPositions();
    }

    static void AddChild<T>(MovingPath path, string name) where T : Component
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Add path element");
        go.transform.SetParent(path.transform, false);
        go.AddComponent<T>();
        Undo.RecordObject(path, "Add path element");
        path.SyncPositions();
    }

    static void DrawShapes(MovingPath path)
    {
        var curve = path.Curve;
        _shape = (PathShape)EditorGUILayout.EnumPopup(new GUIContent("Forme", "Forme fermée centrée sur l'objet."), _shape);
        _radius = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Rayon", "Rayon du cercle circonscrit, en unités Unity (absolu)."), _radius));
        _rotation = EditorGUILayout.FloatField(new GUIContent("Rotation", "Rotation de la forme, en degrés (positif = anti-horaire)."), _rotation);
        if (_shape == PathShape.Polygon)
            _sides = EditorGUILayout.IntSlider(new GUIContent("Côtés", "Nombre de côtés du polygone régulier."), _sides, 3, 64);
        if (GUILayout.Button("Appliquer la forme (remplace les points)"))
        {
            Undo.RecordObject(curve, "Path shape");
            PathShapes.Apply(curve, _shape, _radius, _rotation, _sides);
            EditorUtility.SetDirty(curve);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _scaleFactor = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Échelle ×", "Multiplie la taille de la courbe (points et tangentes) autour de l'objet."), _scaleFactor));
            if (GUILayout.Button("Appliquer", GUILayout.Width(80)))
            {
                Undo.RecordObject(curve, "Scale path");
                PathShapes.Scale(curve, _scaleFactor);
                EditorUtility.SetDirty(curve);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            _turnAngle = EditorGUILayout.FloatField(new GUIContent("Tourner de (°)", "Tourne la courbe autour de l'objet, en degrés (positif = anti-horaire)."), _turnAngle);
            if (GUILayout.Button("Appliquer", GUILayout.Width(80)))
            {
                Undo.RecordObject(curve, "Rotate path");
                PathShapes.Rotate(curve, _turnAngle);
                EditorUtility.SetDirty(curve);
            }
        }
        if (GUI.changed) SceneView.RepaintAll();
    }
}
