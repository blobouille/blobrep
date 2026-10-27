using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

public enum PathEndMode { PingPong, Teleport }

// Trajectoire : les enfants de l'objet (scies, piques, boosts… mélangés librement, un enfant = un élément) suivent la courbe
// (CurvePath2D du même objet), tous à la même vitesse. Position = distance parcourue depuis le départ du niveau, remise à zéro
// par LevelRestart : le rejeu accéléré (sauvegardes, replays) redonne exactement le même état.
// Au départ, la trajectoire pose le Transform de chaque enfant ; ensuite un enfant avec un corps (IPathRider, ex. BlobHazard)
// pilote son corps en vitesse, les autres (ex. BoostZone) suivent par leur Transform. Hors Play, les enfants sont posés à leur
// position de départ : l'aperçu est l'état au départ / au redémarrage.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CurvePath2D))]
public class MovingPath : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Mouvement")]
    [Tooltip("Vitesse des éléments le long de la trajectoire, en km/h (absolu), la même pour tous. 0 = immobiles.")]
    [SpeedKmh] [Min(0f)] public float speed = 3f;
    [Tooltip("Coché : les éléments parcourent la trajectoire dans l'autre sens (sens inverse du tracé de la courbe).")]
    public bool reverse;
    [Tooltip("Trajectoire ouverte seulement (une trajectoire fermée tourne en boucle) :\n" +
             "• PingPong : aller-retour d'un bout à l'autre\n" +
             "• Teleport : arrivé au bout, l'élément réapparaît à l'autre bout et repart dans le même sens")]
    public PathEndMode endMode = PathEndMode.PingPong;

    [Header("Orientation")]
    [Tooltip("Coché : les éléments tournent pour suivre la trajectoire (+X local de l'élément = sens du tracé de la courbe).\n" +
             "Décoché : translation seule, chaque élément garde sa rotation. Une scie garde toujours sa propre rotation.")]
    public bool alignToPath;
    [Tooltip("Avec 'alignToPath' : angle ajouté à la direction de la trajectoire, en degrés (ex. 180 = piques de l'autre côté).")]
    public float alignAngle;

    [Header("Placement")]
    [Tooltip("Coché : les éléments (enfants actifs, ordre de la hiérarchie) sont répartis à intervalles égaux sur un cycle.\n" +
             "Décoché : chaque élément a sa propre position de départ ('positions').")]
    public bool evenSpacing = true;
    [Tooltip("Espacement régulier : position de départ du premier élément, en fraction d'un cycle (0 à 1).\n" +
             "Un cycle = un tour (fermée), un aller (Teleport) ou un aller-retour (PingPong : 0 à 0,5 = aller, 0,5 à 1 = retour).\n" +
             "Aussi réglable en déplaçant un enfant dans la Scene view.")]
    [Range(0f, 1f)] public float phase;
    [Tooltip("Sans espacement régulier : position de départ de chaque enfant (ordre de la hiérarchie), en fraction d'un cycle\n" +
             "(voir 'phase'). Aussi réglable en déplaçant l'enfant dans la Scene view : il est ramené sur la trajectoire.")]
    public float[] positions = new float[0];

    [Header("Debug")]
    [Tooltip("Couleur de la trajectoire et des flèches de sens (Scene view et jeu).")]
    public Color previewColor = new Color(1f, 0.85f, 0.2f, 0.8f);
    [Tooltip("Coché : la trajectoire est aussi dessinée en jeu (rendu debug).")]
    public bool showInGame = true;

    const float PlaceTolerance = 1e-4f;

    CurvePath2D _curve;
    readonly List<Vector2> _points = new List<Vector2>(); // polyligne en coordonnées monde
    readonly List<float> _distances = new List<float>();  // distance cumulée à chaque point
    readonly List<float> _angles = new List<float>();     // direction de chaque segment, en degrés
    Transform[] _children;
    IPathRider[][] _childRiders;
    float[] _offsets;
    // Édition : dernière position posée par la trajectoire, pour reconnaître un enfant déplacé à la main (recherche seulement).
    readonly Dictionary<Transform, Vector2> _placed = new Dictionary<Transform, Vector2>();
    float _length;
    bool _closed;
    float _travel;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public CurvePath2D Curve => _curve != null ? _curve : (_curve = GetComponent<CurvePath2D>());
    public float Length => _length;
    bool Loops => _closed || endMode == PathEndMode.Teleport;
    // Longueur d'un cycle, en m : un tour, un aller (Teleport) ou un aller-retour (PingPong).
    public float CycleLength => Loops ? _length : 2f * _length;

    // Jeu : polyligne, enfants et décalages figés au départ (chaque redémarrage), rien n'est recalculé à chaque pas
    // (le rejeu accéléré des sauvegardes enchaîne des milliers de pas). Une modification en Play s'applique au redémarrage.
    void OnEnable()
    {
        _travel = 0f;
        _children = null;
        if (Application.isPlaying && RebuildPolyline())
        {
            PlaceAtStart(false);
            CacheChildren();
        }
    }

    void CacheChildren()
    {
        float cycle = CycleLength;
        int count = ActiveChildCount(), rank = 0;
        _children = new Transform[count];
        _childRiders = new IPathRider[count][];
        _offsets = new float[count];
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            _offsets[rank] = StartFraction(i, rank, count) * cycle;
            _children[rank] = child;
            _childRiders[rank++] = child.GetComponents<IPathRider>();
        }
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void OnValidate() => SyncPositions();
    void OnTransformChildrenChanged() => SyncPositions();

    public void SyncPositions()
    {
        int n = transform.childCount;
        if (positions != null && positions.Length == n) return;
        var old = positions ?? new float[0];
        positions = new float[n];
        for (int i = 0; i < n; i++) positions[i] = i < old.Length ? old[i] : (float)i / n;
    }

    // Polyligne de la courbe en coordonnées monde (même échantillonnage que l'aperçu de CurvePath2D).
    public bool RebuildPolyline()
    {
        var curve = Curve;
        if (curve.center.Count < 2) { curve.RecomputeHandles(); curve.Rebuild(); }
        _points.Clear();
        _distances.Clear();
        _length = 0f;
        if (curve.center.Count < 2) return false;

        var m = transform.localToWorldMatrix;
        foreach (var p in curve.center) _points.Add(m.MultiplyPoint3x4(p));
        _closed = curve.closed && _points.Count > 2;
        if (_closed) _points.Add(_points[0]);

        _distances.Add(0f);
        _angles.Clear();
        for (int i = 1; i < _points.Count; i++)
        {
            Vector2 seg = _points[i] - _points[i - 1];
            _distances.Add(_distances[i - 1] + seg.magnitude);
            _angles.Add(Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg);
        }
        _length = _distances[_distances.Count - 1];
        return _length > 1e-4f;
    }

    // Pose à la distance 'd' (m) depuis le début de la courbe ; angle = direction du tracé (+ alignAngle).
    public PathPose PoseAt(float d)
    {
        d = Mathf.Clamp(d, 0f, _length);
        int lo = 0, hi = _points.Count - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_distances[mid] <= d) lo = mid; else hi = mid - 1;
        }
        Vector2 a = _points[lo];
        float seg = _distances[lo + 1] - _distances[lo];
        float t = seg > 1e-6f ? (d - _distances[lo]) / seg : 0f;
        return new PathPose(a + (_points[lo + 1] - a) * t, _angles[lo] + alignAngle);
    }

    // Position dans le cycle (m, 0 à CycleLength) → distance sur la courbe.
    float DistanceAt(float u) => Loops || u <= _length ? u : 2f * _length - u;

    static float Wrap(float x, float cycle)
    {
        float r = x - cycle * Mathf.Floor(x / cycle);
        return r >= cycle ? 0f : r;
    }

    // Position de départ de l'enfant (index dans la hiérarchie), en fraction de cycle.
    // 'rank' / 'count' : rang parmi les enfants actifs et leur nombre (espacement régulier).
    public float StartFraction(int childIndex, int rank, int count)
    {
        if (evenSpacing) return Wrap(phase + (float)rank / Mathf.Max(1, count), 1f);
        return positions != null && childIndex < positions.Length ? Wrap(positions[childIndex], 1f) : 0f;
    }

    int ActiveChildCount()
    {
        int n = 0;
        for (int i = 0; i < transform.childCount; i++) if (transform.GetChild(i).gameObject.activeSelf) n++;
        return n;
    }

    public void LevelTick()
    {
        if (_children == null) return;
        float cycle = CycleLength;
        float step = speed * Time.fixedDeltaTime;
        float dir = reverse ? -1f : 1f;
        float now = _travel, next = _travel + step;
        _travel = Wrap(next, cycle);
        bool teleportMode = !_closed && endMode == PathEndMode.Teleport;

        for (int i = 0; i < _children.Length; i++)
        {
            var child = _children[i];
            if (child == null) continue;

            float offset = _offsets[i];
            float uNow = Wrap(offset + dir * now, cycle), uNext = Wrap(offset + dir * next, cycle);
            bool teleport = teleportMode && step > 0f && (dir > 0f ? uNext < uNow : uNext > uNow);
            if (teleport) uNow = Mathf.Clamp(uNext - dir * step, 0f, _length);

            var poseNow = PoseAt(DistanceAt(uNow));
            var poseNext = PoseAt(DistanceAt(uNext));
            var riders = _childRiders[i];
            if (riders.Length == 0) SetTransform(child, poseNow);
            for (int r = 0; r < riders.Length; r++) riders[r].PathTick(poseNow, poseNext, alignToPath, teleport);
        }
    }

    void SetTransform(Transform child, PathPose pose)
    {
        child.position = new Vector3(pose.position.x, pose.position.y, child.position.z);
        if (alignToPath) child.rotation = Quaternion.Euler(0f, 0f, pose.angle);
    }

    // Pose les enfants actifs à leur position de départ. 'fromEditor' : seulement s'ils n'y sont pas déjà (pas de scène
    // modifiée pour rien) ; un enfant déjà posé puis déplacé à la main est ramené sur la trajectoire (sa position de départ suit).
    void PlaceAtStart(bool fromEditor)
    {
        float cycle = CycleLength;
        int count = ActiveChildCount(), rank = 0;
        if (fromEditor)
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                int r = rank++;
                if (_placed.TryGetValue(child, out var placed) &&
                    ((Vector2)child.position - placed).sqrMagnitude > PlaceTolerance * PlaceTolerance)
                    ProjectChild(child, i, r, count);
            }

        rank = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;

            var pose = PoseAt(DistanceAt(StartFraction(i, rank++, count) * cycle));
            if (!fromEditor || OffPose(child, pose))
            {
                SetTransform(child, pose);
                MarkDirty(child);
            }
            if (fromEditor) _placed[child] = child.position;
        }
    }

    bool OffPose(Transform child, PathPose pose)
    {
        if (((Vector2)child.position - pose.position).sqrMagnitude > PlaceTolerance * PlaceTolerance) return true;
        return alignToPath && Mathf.Abs(Mathf.DeltaAngle(child.eulerAngles.z, pose.angle)) > 0.01f;
    }

    // Point de la trajectoire le plus proche de l'enfant → nouvelle position de départ (ou nouvelle phase).
    void ProjectChild(Transform child, int childIndex, int rank, int count)
    {
        Vector2 p = child.position;
        float best = float.MaxValue, d = 0f;
        for (int i = 0; i < _points.Count - 1; i++)
        {
            Vector2 a = _points[i], ab = _points[i + 1] - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float dist = (a + ab * t - p).sqrMagnitude;
            if (dist < best) { best = dist; d = _distances[i] + t * (_distances[i + 1] - _distances[i]); }
        }

        float cycle = CycleLength;
        float current = StartFraction(childIndex, rank, count) * cycle;
        float u = d;
        if (!Loops)
        {
            float back = 2f * _length - d;
            if (CycleGap(back, current, cycle) < CycleGap(d, current, cycle)) u = back;
        }

        float fraction = Wrap(u / cycle, 1f);
        if (evenSpacing) phase = Wrap(fraction - (float)rank / Mathf.Max(1, count), 1f);
        else positions[childIndex] = fraction;
        MarkDirty(this);
    }

    static float CycleGap(float a, float b, float cycle)
    {
        float g = Mathf.Abs(Wrap(a - b, cycle));
        return Mathf.Min(g, cycle - g);
    }

    static void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(o);
#endif
    }

    void Update()
    {
        if (Application.isPlaying) return;
        SyncPositions();
        if (RebuildPolyline()) PlaceAtStart(true);
    }

    // Tracé + flèches dans le sens de départ, en coordonnées monde. Même dessin dans la Scene view et en jeu.
    void BuildOutline(List<Vector2> lines)
    {
        lines.Clear();
        for (int i = 0; i < _points.Count - 1; i++) { lines.Add(_points[i]); lines.Add(_points[i + 1]); }

        const float Spacing = 1.5f, Size = 0.2f;
        int arrows = Mathf.Max(1, Mathf.FloorToInt(_length / Spacing));
        float dir = reverse ? -1f : 1f;
        for (int k = 0; k < arrows; k++)
        {
            var pose = PoseAt((k + 0.5f) * _length / arrows);
            float a = (pose.angle - alignAngle) * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dir, side = new Vector2(-fwd.y, fwd.x);
            Vector2 tip = pose.position + fwd * (0.5f * Size);
            lines.Add(tip); lines.Add(tip - fwd * Size + side * Size);
            lines.Add(tip); lines.Add(tip - fwd * Size - side * Size);
        }
    }

    readonly List<Vector2> _lines = new List<Vector2>();

    void LateUpdate()
    {
        if (!Application.isPlaying || !showInGame || _length <= 0f) return;
        BuildOutline(_lines);
        var world = PhysicsWorld.defaultWorld;
        for (int i = 0; i < _lines.Count; i += 2) world.DrawLine(_lines[i], _lines[i + 1], previewColor, 0f);
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying || !RebuildPolyline()) return;

        BuildOutline(_lines);
        Gizmos.color = previewColor;
        for (int i = 0; i < _lines.Count; i += 2) Gizmos.DrawLine(_lines[i], _lines[i + 1]);

#if UNITY_EDITOR
        const float PickHalfWidth = 0.15f;
        var left = new List<Vector2>(_points.Count);
        var right = new List<Vector2>(_points.Count);
        for (int i = 0; i < _points.Count; i++)
        {
            Vector2 t = _points[Mathf.Min(i + 1, _points.Count - 1)] - _points[Mathf.Max(i - 1, 0)];
            Vector2 n = new Vector2(-t.y, t.x).normalized * PickHalfWidth;
            left.Add(_points[i] + n);
            right.Add(_points[i] - n);
        }
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddStrip(left, right, false);
        _pickArea.Draw(Matrix4x4.identity);
#endif
    }
}
