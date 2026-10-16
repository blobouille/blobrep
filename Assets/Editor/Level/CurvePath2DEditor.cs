using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CurvePath2D))]
public class CurvePath2DEditor : Editor
{
    int selected = -1;
    static bool showSamples;

    const float PickPx = 14f;

    static readonly Color[] ModeColors =
    {
        new Color(0.85f, 0.85f, 0.85f), // Free
        new Color(1.0f, 0.5f, 0.8f),    // Aligned
        new Color(0.4f, 0.9f, 0.4f),    // Vector
        new Color(1.0f, 0.9f, 0.3f),    // Auto
    };

    void OnEnable() { Undo.undoRedoPerformed += OnUndo; }
    void OnDisable() { Undo.undoRedoPerformed -= OnUndo; }
    void OnUndo() { var p = (CurvePath2D)target; if (p) { p.Rebuild(); SceneView.RepaintAll(); } }

    [MenuItem("GameObject/2D Object/Curve Path 2D", false, 10)]
    static void Create()
    {
        var go = new GameObject("Curve Path 2D");
        Undo.RegisterCreatedObjectUndo(go, "Create Curve Path 2D");
        var path = go.AddComponent<CurvePath2D>();   // Reset() crée la ligne droite
        go.AddComponent<CurveCollider2D>();          // collider présent dès le spawn
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var pv = sv.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0); }
        Selection.activeGameObject = go;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var path = (CurvePath2D)target;

        if (path.leftSelfIntersects || path.rightSelfIntersects)
            EditorGUILayout.HelpBox("Épaisseur supérieure au rayon de courbure à certains endroits : " +
                "le bord intérieur se replie. Réduire l'épaisseur ou adoucir la courbe.", MessageType.Warning);

        EditorGUILayout.HelpBox(
            "Scene View :\n" +
            "• Ctrl+clic sur la courbe : insère un point (forme conservée)\n" +
            "• Ctrl+clic ailleurs : prolonge la ligne\n" +
            "• Clic / glisser : sélectionner / déplacer points et tangentes\n" +
            "• Suppr (ou Retour arrière) : supprime le point sélectionné\n" +
            "• Barre d'outils : mode de tangente (Free / Aligned / Vector / Auto)",
            MessageType.Info);

        if (GUILayout.Button("Recalculer")) { path.RecomputeHandles(); path.Rebuild(); SceneView.RepaintAll(); }

        using (new EditorGUI.DisabledScope(!CanDelete(path)))
            if (GUILayout.Button(CanDelete(path) ? $"Supprimer le point {selected}" : "Supprimer le point sélectionné"))
            {
                DeleteSelected(path);
                SceneView.RepaintAll();
            }
    }

    void OnSceneGUI()
    {
        var path = (CurvePath2D)target;
        if (path.points.Count < 2) return;
        var e = Event.current;

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        DrawToolbar(path);

        Handles.matrix = path.transform.localToWorldMatrix; // tout en espace local
        if (e.type == EventType.Repaint) DrawPreview(path);
        HandleInput(path, e);
        DrawHandles(path);
        Handles.matrix = Matrix4x4.identity;
    }

    void DrawToolbar(CurvePath2D path)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 470, 24));
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        bool has = selected >= 0 && selected < path.points.Count;
        HandleMode cur = has ? path.points[selected].mode : HandleMode.Free;
        foreach (HandleMode m in System.Enum.GetValues(typeof(HandleMode)))
        {
            bool on = GUILayout.Toggle(has && cur == m, m.ToString(), EditorStyles.toolbarButton);
            if (on && has && cur != m) SetMode(path, selected, m);
        }

        EditorGUI.BeginChangeCheck();
        bool closed = GUILayout.Toggle(path.closed, "Fermée", EditorStyles.toolbarButton);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Toggle closed");
            path.closed = closed; Commit(path);
        }
        showSamples = GUILayout.Toggle(showSamples, "Points", EditorStyles.toolbarButton);

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    void SetMode(CurvePath2D path, int i, HandleMode m)
    {
        Undo.RecordObject(path, "Handle mode");
        var p = path.points[i];
        p.mode = m;
        if (m == HandleMode.Aligned)
        {
            float len = p.handleIn.magnitude; if (len < 1e-4f) len = p.handleOut.magnitude;
            Vector2 d = p.handleOut.sqrMagnitude > 1e-8f ? p.handleOut.normalized : Vector2.right;
            p.handleIn = -d * len;
        }
        path.points[i] = p;
        Commit(path);
    }

    void DrawPreview(CurvePath2D path)
    {
        Handles.color = new Color(0.4f, 0.8f, 1f);
        DrawLine(path.left, path.closed, 2f);
        DrawLine(path.right, path.closed, 2f);
        if (!path.closed && path.left.Count > 1)
        {
            Handles.DrawAAPolyLine(2f, path.left[0], path.right[0]);
            Handles.DrawAAPolyLine(2f, path.left[path.left.Count - 1], path.right[path.right.Count - 1]);
        }
        Handles.color = new Color(1, 1, 1, 0.25f);
        DrawLine(path.center, path.closed, 1f);

        Handles.color = new Color(0.2f, 1f, 0.3f);
        foreach (var ch in path.BuildCollisionChains())
        {
            var pts = new Vector3[ch.points.Length + (ch.loop ? 1 : 0)];
            for (int i = 0; i < ch.points.Length; i++) pts[i] = ch.points[i];
            if (ch.loop) pts[pts.Length - 1] = ch.points[0];
            Handles.DrawAAPolyLine(3.5f, pts);
            if (showSamples)
                foreach (var q in ch.points)
                    Handles.DotHandleCap(0, q, Quaternion.identity, HandleUtility.GetHandleSize(q) * 0.015f, EventType.Repaint);
        }
    }

    static void DrawLine(System.Collections.Generic.List<Vector2> l, bool closed, float w)
    {
        int n = l.Count; if (n < 2) return;
        var a = new Vector3[n + (closed ? 1 : 0)];
        for (int i = 0; i < n; i++) a[i] = l[i];
        if (closed) a[n] = l[0];
        Handles.DrawAAPolyLine(w, a);
    }

    void HandleInput(CurvePath2D path, Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (e.control)
            {
                Undo.RecordObject(path, "Add curve point");
                if (TryNearestOnCurve(path, e.mousePosition, out int seg, out float t)) InsertPoint(path, seg, t);
                else AppendPoint(path, MouseToLocal(path, e.mousePosition));
                Commit(path); e.Use(); return;
            }

            int best = -1; float bd = PickPx;
            for (int i = 0; i < path.points.Count; i++)
            {
                float d = Vector2.Distance(e.mousePosition, GUIPoint(path, path.points[i].position));
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0) { selected = best; Repaint(); }
        }

        // Suppr passe par la commande "SoftDelete" d'Unity (sinon c'est le GameObject qui est supprimé)
        bool deleteCommand = (e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand)
                             && (e.commandName == "SoftDelete" || e.commandName == "Delete");
        bool deleteKey = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace);
        if ((deleteCommand || deleteKey) && CanDelete(path))
        {
            if (e.type != EventType.ValidateCommand) DeleteSelected(path);
            e.Use();
        }
    }

    bool CanDelete(CurvePath2D path) => selected >= 0 && selected < path.points.Count && path.points.Count > 2;

    void DeleteSelected(CurvePath2D path)
    {
        Undo.RecordObject(path, "Delete curve point");
        path.points.RemoveAt(selected);
        selected = Mathf.Min(selected, path.points.Count - 1);
        Commit(path);
        Repaint();
    }

    // Handles.matrix est déjà localToWorld pendant OnSceneGUI : WorldToGUIPoint l'applique lui-même
    static Vector2 GUIPoint(CurvePath2D path, Vector2 local) =>
        HandleUtility.WorldToGUIPoint(local);

    static Vector2 MouseToLocal(CurvePath2D path, Vector2 mouse)
    {
        var r = HandleUtility.GUIPointToWorldRay(mouse);
        Vector3 o = path.transform.InverseTransformPoint(r.origin);
        Vector3 d = path.transform.InverseTransformDirection(r.direction);
        if (Mathf.Abs(d.z) < 1e-6f) return o;
        return o + d * (-o.z / d.z);
    }

    static bool TryNearestOnCurve(CurvePath2D path, Vector2 mouse, out int seg, out float tBest)
    {
        seg = -1; tBest = 0; float best = PickPx;
        const int N = 64;
        for (int s = 0; s < path.SegmentCount; s++)
        {
            path.GetSegment(s, out var a, out var b, out var c, out var d);
            for (int k = 1; k < N; k++)
            {
                float t = k / (float)N;
                float dist = Vector2.Distance(mouse, GUIPoint(path, CurvePath2D.Bezier(a, b, c, d, t)));
                if (dist < best) { best = dist; seg = s; tBest = t; }
            }
        }
        return seg >= 0;
    }

    void InsertPoint(CurvePath2D path, int seg, float t)
    {
        // De Casteljau : la forme de la courbe est strictement conservée
        path.GetSegment(seg, out var a, out var b, out var c, out var d);
        Vector2 ab = Vector2.Lerp(a, b, t), bc = Vector2.Lerp(b, c, t), cd = Vector2.Lerp(c, d, t);
        Vector2 abc = Vector2.Lerp(ab, bc, t), bcd = Vector2.Lerp(bc, cd, t);
        Vector2 m = Vector2.Lerp(abc, bcd, t);

        int i0 = seg, i1 = (seg + 1) % path.points.Count;
        var p0 = path.points[i0]; p0.handleOut = ab - a; p0.mode = Freeze(p0.mode); path.points[i0] = p0;
        var p1 = path.points[i1]; p1.handleIn = cd - d; p1.mode = Freeze(p1.mode); path.points[i1] = p1;

        var np = new CurvePoint(m, HandleMode.Aligned) { handleIn = abc - m, handleOut = bcd - m };
        path.points.Insert(seg + 1, np);
        selected = seg + 1;
    }

    // Auto/Vector seraient recalculés et déformeraient la courbe : on les fige
    static HandleMode Freeze(HandleMode m) =>
        m == HandleMode.Auto ? HandleMode.Aligned : m == HandleMode.Vector ? HandleMode.Free : m;

    void AppendPoint(CurvePath2D path, Vector2 pos)
    {
        path.points.Add(new CurvePoint(pos, HandleMode.Auto));
        selected = path.points.Count - 1;
    }

    static void Commit(CurvePath2D path)
    {
        path.RecomputeHandles();
        path.Rebuild();
        EditorUtility.SetDirty(path);
    }

    void DrawHandles(CurvePath2D path)
    {
        int n = path.points.Count;
        for (int i = 0; i < n; i++)
        {
            var p = path.points[i];
            bool hasPrev = path.closed || i > 0, hasNext = path.closed || i < n - 1;
            float size = HandleUtility.GetHandleSize(p.position);
            Color mc = ModeColors[(int)p.mode];

            for (int side = 0; side < 2; side++)
            {
                bool isOut = side == 1;
                if (isOut ? !hasNext : !hasPrev) continue;
                Vector2 off = isOut ? p.handleOut : p.handleIn;
                Vector2 hp = p.position + off;

                Handles.color = mc;
                Handles.DrawLine(p.position, hp);
                EditorGUI.BeginChangeCheck();
                Vector3 nh = Handles.FreeMoveHandle(hp, size * 0.05f, Vector3.zero, Handles.CircleHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Move handle");
                    p = path.points[i];
                    Vector2 o = (Vector2)nh - p.position;
                    if (isOut) p.handleOut = o; else p.handleIn = o;
                    AfterHandleDrag(ref p, isOut);
                    path.points[i] = p; selected = i;
                    Commit(path);
                }
            }

            Handles.color = i == selected ? Color.white : mc;
            EditorGUI.BeginChangeCheck();
            Vector3 np = Handles.FreeMoveHandle(p.position, size * (i == selected ? 0.09f : 0.07f),
                                                Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path, "Move point");
                p = path.points[i]; p.position = (Vector2)np;
                path.points[i] = p; selected = i;
                Commit(path);
            }
        }
    }

    // Comportement Blender : bouger une tangente Auto -> Aligned ; Vector -> Free
    static void AfterHandleDrag(ref CurvePoint p, bool outMoved)
    {
        if (p.mode == HandleMode.Auto) p.mode = HandleMode.Aligned;
        else if (p.mode == HandleMode.Vector) p.mode = HandleMode.Free;

        if (p.mode == HandleMode.Aligned)
        {
            Vector2 moved = outMoved ? p.handleOut : p.handleIn;
            float other = (outMoved ? p.handleIn : p.handleOut).magnitude;
            if (other < 1e-4f) other = moved.magnitude;
            Vector2 opposite = moved.sqrMagnitude > 1e-8f ? -moved.normalized * other : Vector2.zero;
            if (outMoved) p.handleIn = opposite; else p.handleOut = opposite;
        }
    }
}