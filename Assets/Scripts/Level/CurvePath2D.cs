using System;
using System.Collections.Generic;
using UnityEngine;

public enum HandleMode { Free, Aligned, Vector, Auto }
public enum CollisionMode { Centerline, LeftEdge, RightEdge, Outline }

[Serializable]
public struct CurvePoint
{
    public Vector2 position;
    public Vector2 handleIn;   // offset local par rapport à position
    public Vector2 handleOut;  // offset local par rapport à position
    public HandleMode mode;

    public CurvePoint(Vector2 p, HandleMode m)
    {
        position = p; handleIn = handleOut = Vector2.zero; mode = m;
    }
}

public struct CollisionChain
{
    public Vector2[] points; // espace local du CurvePath2D
    public bool loop;
}

/// <summary>
/// Courbe de Bézier cubique par morceaux (style Blender), avec épaisseur uniforme.
/// Une seule source de vérité : l'échantillonnage (center/left/right)
/// sert à la fois à l'aperçu et à la collision.
/// Un seul réglage : "resolution". Les points sont placés là où la courbe tourne,
/// les lignes droites n'en reçoivent presque pas, quelle que soit la résolution.
/// </summary>
[ExecuteAlways]
public class CurvePath2D : MonoBehaviour
{
    [Header("Forme")]
    [Tooltip("Points de contrôle de la courbe (espace local). Éditables dans la Scene view quand l'objet est sélectionné.")]
    public List<CurvePoint> points = new List<CurvePoint>();
    [Tooltip("Coché : le dernier point est relié au premier (courbe fermée).")]
    public bool closed;
    [Tooltip("Épaisseur du mur/rail, en unités Unity (espace local : multipliée par l'échelle du Transform).")]
    [Min(0.01f)] public float thickness = 0.4f;

    [Header("Échantillonnage (partagé aperçu / collision)")]
    [Tooltip("0 = approximation grossière (peu de points), 1 = très précis (beaucoup de points).\n" +
             "Les points sont toujours concentrés dans les virages ; les parties droites en ont très peu.")]
    [Range(0f, 1f)] public float resolution = 0.3f;

    [Tooltip("NETTOYAGE DES PARTIES DROITES (très important pour le nombre de points).\n\n" +
             "Après l'échantillonnage, on supprime tout point dont le retrait déplace la ligne de MOINS que cette valeur (en unités Unity).\n" +
             "• Une portion droite se réduit à ses 2 extrémités, quelle que soit la résolution.\n" +
             "• Les virages sont protégés : un point n'est jamais supprimé si la tangente tourne plus que ce que la résolution autorise.\n\n" +
             "Valeurs :\n" +
             "• 0      = nettoyage désactivé (tous les points de l'échantillonnage sont gardés)\n" +
             "• 0.0005 = très prudent (ne supprime que les points parfaitement alignés)\n" +
             "• 0.002  = bon compromis (défaut)\n" +
             "• 0.01+  = agressif (peut aplatir de très légères ondulations)")]
    [Min(0f)] public float straightTolerance = 0.002f;

    [Tooltip("LIMITE DE MITRE : contrôle l'épaisseur aux angles vifs.\n\n" +
             "Pour garder une épaisseur uniforme, les bords gauche/droit sont décalés le long de la bissectrice de l'angle. " +
             "Plus l'angle est aigu, plus ce décalage s'allonge (jusqu'à l'infini pour un demi-tour) et crée une pointe.\n" +
             "miterLimit plafonne ce facteur d'allongement.\n\n" +
             "• 1 = aucun allongement : l'épaisseur s'amincit aux angles vifs, mais pas de pointe\n" +
             "• 3 = défaut : coins nets jusqu'à ~140° de virage, pointes coupées au-delà\n" +
             "• 6 = coins très pointus, mais risque de pics dans la collision\n\n" +
             "N'a aucun effet sur les courbes douces ni sur les lignes droites.")]
    [Range(1f, 6f)] public float miterLimit = 3f;

    [Header("Collision")]
    [Tooltip("Ligne(s) utilisée(s) pour les chaînes de collision de CurveCollider2D :\n" +
             "• Centerline / LeftEdge / RightEdge : une seule face solide (chaîne à sens unique, voir 'flipSide')\n" +
             "• Outline : contour fermé, toute l'épaisseur est solide des deux côtés")]
    public CollisionMode collisionMode = CollisionMode.Outline;
    [Tooltip("Par défaut la collision est du côté GAUCHE du sens de la courbe (dessus si elle va de gauche à droite). Cocher pour inverser.")]
    public bool flipSide;

    [NonSerialized] public List<Vector2> center = new List<Vector2>();
    [NonSerialized] public List<Vector2> left = new List<Vector2>();
    [NonSerialized] public List<Vector2> right = new List<Vector2>();
    [NonSerialized] public bool leftSelfIntersects, rightSelfIntersects;

    const float CoarseTolerance = 0.2f;     // résolution 0 : écart max courbe/segment (unités)
    const float FineTolerance = 0.0003f;    // résolution 1
    const float CoarseAngle = 30f;          // résolution 0 : rotation max de la tangente entre 2 points (°)
    const float FineAngle = 0.25f;          // résolution 1
    const float MinPointDistance = 0.01f;   // Box2D ignore les points trop proches (linearSlop ~0.005)

    float _tol, _angle;

    void Reset()
    {
        points = new List<CurvePoint>
        {
            new CurvePoint(new Vector2(-2, 0), HandleMode.Vector),
            new CurvePoint(new Vector2( 2, 0), HandleMode.Vector),
        };
        RecomputeHandles();
        Rebuild();
    }

    void OnEnable() { RecomputeHandles(); Rebuild(); }
    void OnValidate() { RecomputeHandles(); Rebuild(); }

    public int SegmentCount => points.Count < 2 ? 0 : (closed ? points.Count : points.Count - 1);

    public void GetSegment(int i, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d)
    {
        var p0 = points[i];
        var p1 = points[(i + 1) % points.Count];
        a = p0.position;
        b = p0.position + p0.handleOut;
        c = p1.position + p1.handleIn;
        d = p1.position;
    }

    public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    public void RecomputeHandles()
    {
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            var p = points[i];
            if (p.mode != HandleMode.Auto && p.mode != HandleMode.Vector) continue;

            bool hasPrev = closed || i > 0;
            bool hasNext = closed || i < n - 1;
            Vector2 toPrev = hasPrev ? points[(i - 1 + n) % n].position - p.position : Vector2.zero;
            Vector2 toNext = hasNext ? points[(i + 1) % n].position - p.position : Vector2.zero;

            if (p.mode == HandleMode.Vector)
            {
                p.handleIn = toPrev / 3f;
                p.handleOut = toNext / 3f;
            }
            else
            {
                Vector2 dir;
                if (!hasPrev) dir = toNext;
                else if (!hasNext) dir = -toPrev;
                else
                {
                    dir = toNext - toPrev;
                    // extremum local en Y -> tangente horizontale (comme Blender "Auto")
                    if (toPrev.y * toNext.y > 0f)
                        dir = new Vector2(dir.x >= 0f ? 1f : -1f, 0f);
                }
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                p.handleOut = dir * (toNext.magnitude / 3f);
                p.handleIn = -dir * (toPrev.magnitude / 3f);
            }
            points[i] = p;
        }
    }

    public void Rebuild()
    {
        center.Clear(); left.Clear(); right.Clear();
        leftSelfIntersects = rightSelfIntersects = false;

        float t = Mathf.Clamp01(resolution);
        _tol = CoarseTolerance * Mathf.Pow(FineTolerance / CoarseTolerance, t);
        _angle = CoarseAngle * Mathf.Pow(FineAngle / CoarseAngle, t);

        if (points.Count >= 2)
        {
            center.Add(points[0].position);
            for (int s = 0; s < SegmentCount; s++)
            {
                GetSegment(s, out var a, out var b, out var c, out var d);
                Subdivide(a, b, c, d, 0, center);
            }

            if (straightTolerance > 0f) Simplify(center);

            if (closed && center.Count > 1) center.RemoveAt(center.Count - 1);
            RemoveClosePoints(center);
            BuildOffsets();
        }
    }

    void Subdivide(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int depth, List<Vector2> o)
    {
        bool ok = depth >= 16;
        if (!ok)
        {
            float flat = Mathf.Max(DistToLine(b, a, d), DistToLine(c, a, d));
            float angle = Vector2.Angle(Dir(b - a, Dir(c - a, d - a)), Dir(d - c, Dir(d - b, d - a)));
            ok = flat <= _tol && angle <= _angle;
        }
        if (ok) { o.Add(d); return; }

        Vector2 ab = (a + b) * .5f, bc = (b + c) * .5f, cd = (c + d) * .5f;
        Vector2 abc = (ab + bc) * .5f, bcd = (bc + cd) * .5f, m = (abc + bcd) * .5f;
        Subdivide(a, ab, abc, m, depth + 1, o);
        Subdivide(m, bcd, cd, d, depth + 1, o);
    }

    // Garde un point seulement si le retirer écarte la polyligne de plus de straightTolerance
    // ou fait trop tourner la tangente : coins francs et virages serrés sont conservés.
    void Simplify(List<Vector2> pts)
    {
        int m = pts.Count;
        if (m < 3) return;

        float tol = straightTolerance;
        float halfAngle = _angle * 0.5f;
        var res = new List<Vector2>(m) { pts[0] };

        int a = 0;
        while (a < m - 1)
        {
            int best = a + 1;
            for (int e = a + 2; e < m; e++)
            {
                if (!ChordOk(pts, a, e, tol, halfAngle)) break;
                best = e;
            }
            res.Add(pts[best]);
            a = best;
        }

        pts.Clear();
        pts.AddRange(res);
    }

    static bool ChordOk(List<Vector2> p, int a, int e, float tol, float halfAngle)
    {
        Vector2 chord = p[e] - p[a];
        for (int k = a + 1; k < e; k++)
        {
            if (DistToLine(p[k], p[a], p[e]) > tol) return false;
            Vector2 seg = p[k + 1] - p[k];
            if (seg.sqrMagnitude > 1e-12f && Vector2.Angle(chord, seg) > halfAngle) return false;
        }
        return true;
    }

    static Vector2 Dir(Vector2 v, Vector2 fallback) => v.sqrMagnitude > 1e-10f ? v : fallback;

    static float DistToLine(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float l = ab.magnitude;
        if (l < 1e-6f) return (p - a).magnitude;
        return Mathf.Abs(ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / l;
    }

    void RemoveClosePoints(List<Vector2> pts)
    {
        float sq = MinPointDistance * MinPointDistance;
        for (int i = pts.Count - 2; i >= 1; i--)
            if ((pts[i] - pts[i - 1]).sqrMagnitude < sq) pts.RemoveAt(i);
        if (pts.Count > 2)
        {
            int last = pts.Count - 1;
            if (closed) { if ((pts[last] - pts[0]).sqrMagnitude < sq) pts.RemoveAt(last); }
            else if ((pts[last] - pts[last - 1]).sqrMagnitude < sq) pts.RemoveAt(last - 1);
        }
    }

    static Vector2 Normal(Vector2 v) { v = new Vector2(-v.y, v.x); return v.normalized; }

    void BuildOffsets()
    {
        int n = center.Count;
        float h = thickness * 0.5f;
        for (int i = 0; i < n; i++)
        {
            bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
            Vector2 p = center[i];
            Vector2 nPrev = hasPrev ? Normal(p - center[(i - 1 + n) % n]) : Vector2.zero;
            Vector2 nNext = hasNext ? Normal(center[(i + 1) % n] - p) : Vector2.zero;
            Vector2 nrm;
            if (!hasPrev) nrm = nNext;
            else if (!hasNext) nrm = nPrev;
            else
            {
                Vector2 m = nPrev + nNext;
                m = m.sqrMagnitude < 1e-6f ? nNext : m.normalized;
                float k = 1f / Mathf.Max(Vector2.Dot(m, nNext), 1f / miterLimit);
                nrm = m * k;
            }
            left.Add(p + nrm * h);
            right.Add(p - nrm * h);
        }

        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            int j = (i + 1) % n;
            Vector2 c = center[j] - center[i];
            if (Vector2.Dot(left[j] - left[i], c) <= 0f) leftSelfIntersects = true;
            if (Vector2.Dot(right[j] - right[i], c) <= 0f) rightSelfIntersects = true;
        }
    }

    public List<CollisionChain> BuildCollisionChains()
    {
        var res = new List<CollisionChain>();
        if (center.Count < 2) return res;

        switch (collisionMode)
        {
            case CollisionMode.Centerline: Add(res, center, closed); break;
            case CollisionMode.LeftEdge: Add(res, left, closed); break;
            case CollisionMode.RightEdge: Add(res, right, closed); break;
            case CollisionMode.Outline:
                if (closed)
                {
                    Add(res, left, true);
                    var r = new List<Vector2>(right); r.Reverse(); Add(res, r, true);
                }
                else
                {
                    var l = new List<Vector2>(left);
                    var r = new List<Vector2>(right); r.Reverse();
                    l.AddRange(r);
                    Add(res, l, true);
                }
                break;
        }
        return res;
    }

    void Add(List<CollisionChain> res, List<Vector2> pts, bool loop)
    {
        var arr = pts.ToArray();
        // Box2D v3 : chaîne à sens unique, le côté solide est à DROITE du sens de parcours.
        // On inverse donc par défaut pour que le côté solide soit le côté gauche (= "dessus").
        if (!flipSide) Array.Reverse(arr);
        res.Add(new CollisionChain { points = arr, loop = loop });
    }

#if UNITY_EDITOR
    [NonSerialized] GizmoPickArea _pickArea;

    void OnDisable()
    {
        _pickArea?.Dispose();
        _pickArea = null;
    }

    // Sélectionnée : l'aperçu est dessiné par CurvePath2DEditor.
    void OnDrawGizmos()
    {
        if (UnityEditor.Selection.activeGameObject == gameObject) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        DrawLoop(left); DrawLoop(right);
        if (!closed && left.Count > 1)
        {
            Gizmos.DrawLine(left[0], right[0]);
            Gizmos.DrawLine(left[left.Count - 1], right[right.Count - 1]);
        }

        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddStrip(left, right, closed);
        _pickArea.Draw(transform.localToWorldMatrix);
    }

    void DrawLoop(List<Vector2> p)
    {
        int n = p.Count; if (n < 2) return;
        for (int i = 0; i < (closed ? n : n - 1); i++)
            Gizmos.DrawLine(p[i], p[(i + 1) % n]);
    }
#endif
}