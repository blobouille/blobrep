using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Élément neutre : zone d'accélération. Le blob en sort toujours pareil, quelle que soit son entrée :
// au milieu du bout de la zone, à 'exitSpeed' dans le sens de la flèche, sans vitesse de côté ni rotation
// (accéléré s'il est lent, freiné s'il est rapide, recentré s'il est décalé) : ce n'est pas un multiplicateur.
// Aucun corps physique : test géométrique dans LevelTick, avant chaque pas.
[ExecuteAlways]
[DisallowMultipleComponent]
public class BoostZone : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Zone")]
    [Tooltip("Longueur de la zone dans le sens de la flèche (+X local), en unités Unity (absolu). Zone centrée sur l'objet.\n" +
             "Tourner le GameObject pour orienter la flèche.")]
    [Min(0.1f)] public float length = 2.24f;
    [Tooltip("Hauteur de la zone, perpendiculaire à la flèche, en unités Unity (absolu). 1 = diamètre du blob standard.")]
    [Min(0.1f)] public float height = 1.06f;

    [Header("Vitesse")]
    [Tooltip("Vitesse dans le sens de la flèche à la sortie de la zone, en m/s (absolu). Atteinte à la sortie quelle que soit\n" +
             "la vitesse d'entrée : accélération (ou freinage) constante sur la distance restant à parcourir.")]
    [Min(0f)] public float exitSpeed = 30f;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view.")]
    public Color previewColor = new Color(0.3f, 1f, 0.3f, 1f);

    // Un groupe = un blob entier (capsule -1) tant qu'il n'est pas cassé, puis chaque capsule libre.
    struct Tracked
    {
        public BLOB blob;
        public int capsule;
        public float entryX;
        public float acceleration;
        public float speed;
        public bool seen;
    }

    readonly List<Tracked> _tracked = new List<Tracked>();
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    Vector2 Axis
    {
        get
        {
            float a = transform.eulerAngles.z * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }

    void OnDisable()
    {
        _tracked.Clear();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    public void LevelTick()
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            var t = _tracked[i];
            t.seen = false;
            _tracked[i] = t;
        }

        var blobs = BLOB.Active;
        for (int b = 0; b < blobs.Count; b++)
        {
            var blob = blobs[b];
            if (!blob.IsBroken) Affect(blob, -1);
            else
                for (int c = 0; c < blob.CapsuleCount; c++) Affect(blob, c);
        }

        for (int i = _tracked.Count - 1; i >= 0; i--)
            if (!_tracked[i].seen) _tracked.RemoveAt(i);
    }

    void Affect(BLOB blob, int capsule)
    {
        int first = capsule < 0 ? 0 : capsule;
        int end = capsule < 0 ? blob.CapsuleCount : capsule + 1;
        if (end <= first) return;

        Vector2 position = Vector2.zero, velocity = Vector2.zero;
        for (int i = first; i < end; i++)
        {
            var body = blob.Capsule(i);
            position += body.position;
            velocity += body.linearVelocity;
        }
        position /= end - first;
        velocity /= end - first;

        Vector2 axis = Axis, normal = new Vector2(-axis.y, axis.x);
        Vector2 d = position - (Vector2)transform.position;
        float x = Vector2.Dot(d, axis), y = Vector2.Dot(d, normal);
        float halfLength = 0.5f * length;
        if (Mathf.Abs(x) > halfLength || Mathf.Abs(y) > 0.5f * height) return;

        int index = Find(blob, capsule);
        if (index < 0)
        {
            // Accélération constante choisie à l'entrée pour atteindre exitSpeed pile au bout de la distance restante
            // (v² = v0² + 2·a·d). Entré à contresens : repart de 0 vers la sortie (la zone n'a qu'un sens).
            float v0 = Mathf.Max(0f, Vector2.Dot(velocity, axis));
            float remaining = halfLength - x;
            _tracked.Add(new Tracked
            {
                blob = blob,
                capsule = capsule,
                entryX = x,
                acceleration = remaining > 1e-4f ? Mathf.Abs(exitSpeed * exitSpeed - v0 * v0) / (2f * remaining) : float.PositiveInfinity,
                speed = v0
            });
            index = _tracked.Count - 1;
        }

        float dt = Time.fixedDeltaTime;
        var t = _tracked[index];
        t.seen = true;
        t.speed = Mathf.MoveTowards(t.speed, exitSpeed, t.acceleration * dt);
        bool exiting = x + t.speed * dt >= halfLength;
        if (exiting) t.speed = exitSpeed;
        _tracked[index] = t;

        // Recentrage : écart latéral y ∝ τ² (τ = temps restant avant la sortie), donc vitesse latérale = -2·y/τ.
        // Écart et vitesse latérale arrivent ensemble à zéro au bout de la zone.
        float lateral = 0f;
        if (!exiting)
        {
            float timeLeft = 2f * (halfLength - x) / Mathf.Max(1e-3f, t.speed + exitSpeed);
            lateral = timeLeft > 2f * dt ? -2f * y / timeLeft : -y / dt;
        }
        // Le pas suivant ajoute gravité × dt : on l'enlève d'avance.
        Vector2 target = axis * t.speed + normal * lateral - (Vector2)PhysicsWorld.defaultWorld.gravity * dt;

        // Même vitesse pour toutes les capsules : la rotation et les oscillations du blob ne sortent pas de la zone.
        for (int i = first; i < end; i++)
        {
            var body = blob.Capsule(i);
            body.linearVelocity = target;
            body.angularVelocity = 0f;
        }
    }

    int Find(BLOB blob, int capsule)
    {
        for (int i = 0; i < _tracked.Count; i++)
            if (_tracked[i].blob == blob && _tracked[i].capsule == capsule) return i;
        return -1;
    }

    // Rectangle local (centré, flèche vers +X).
    public Vector2[] Corners()
    {
        float hx = 0.5f * length, hy = 0.5f * height;
        return new[] { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) };
    }

    // Contour + chevrons dans le sens de la flèche, en local. Même dessin dans la Scene view (gizmos) et en jeu (rendu debug).
    void BuildOutline(List<Vector2> lines)
    {
        lines.Clear();
        var corners = Corners();
        for (int i = 0; i < 4; i++) { lines.Add(corners[i]); lines.Add(corners[(i + 1) % 4]); }

        float hx = 0.5f * length;
        float chevron = Mathf.Min(0.35f * height, 0.25f * length);
        int count = Mathf.Max(1, Mathf.FloorToInt(length / Mathf.Max(0.5f, 1.5f * chevron)));
        for (int i = 0; i < count; i++)
        {
            float cx = -hx + (i + 0.5f) * length / count + 0.5f * chevron;
            lines.Add(new Vector2(cx - chevron, chevron)); lines.Add(new Vector2(cx, 0f));
            lines.Add(new Vector2(cx - chevron, -chevron)); lines.Add(new Vector2(cx, 0f));
        }
    }

    readonly List<Vector2> _lines = new List<Vector2>();

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        BuildOutline(_lines);
        var m = LocalToWorld;
        var world = PhysicsWorld.defaultWorld;
        for (int i = 0; i < _lines.Count; i += 2)
            world.DrawLine(m.MultiplyPoint3x4(_lines[i]), m.MultiplyPoint3x4(_lines[i + 1]), previewColor, 0f);
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        BuildOutline(_lines);
        Gizmos.matrix = LocalToWorld;
        Gizmos.color = previewColor;
        for (int i = 0; i < _lines.Count; i += 2) Gizmos.DrawLine(_lines[i], _lines[i + 1]);
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddConvex(Corners());
        _pickArea.Draw(LocalToWorld);
#endif
    }
}
