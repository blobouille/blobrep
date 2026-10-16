using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Serialization;

// Élément neutre : zone d'accélération (à la Trackmania). Chaque capsule du blob dans la zone est poussée dans le sens
// de la flèche avec une accélération constante, jusqu'au plafond 'maxSpeed' ; la zone ne freine jamais et ne touche pas
// au reste du mouvement. Plus le blob reste dans la zone, plus il gagne : la sortie dépend de la façon de la prendre.
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

    [Header("Poussée")]
    [Tooltip("Accélération dans le sens de la flèche, en m/s² (absolu), appliquée à chaque capsule dont le centre est dans la zone.\n" +
             "Gain de vitesse = accélération × temps passé dans la zone (ex. 150 m/s² pendant 0,1 s = +15 m/s).")]
    [Min(0f)] public float acceleration = 150f;
    [Tooltip("Plafond de vitesse dans le sens de la flèche, en m/s (absolu) : la zone n'accélère plus au-delà, sans jamais freiner.")]
    [FormerlySerializedAs("exitSpeed")]
    [Min(0f)] public float maxSpeed = 30f;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view.")]
    public Color previewColor = new Color(0.3f, 1f, 0.3f, 1f);

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
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    public void LevelTick()
    {
        Vector2 axis = Axis, normal = new Vector2(-axis.y, axis.x);
        Vector2 origin = transform.position;
        float halfLength = 0.5f * length, halfHeight = 0.5f * height;
        float gain = acceleration * Time.fixedDeltaTime;

        var blobs = BLOB.Active;
        for (int b = 0; b < blobs.Count; b++)
            for (int i = 0; i < blobs[b].CapsuleCount; i++)
            {
                var body = blobs[b].Capsule(i);
                Vector2 d = body.position - origin;
                if (Mathf.Abs(Vector2.Dot(d, axis)) > halfLength || Mathf.Abs(Vector2.Dot(d, normal)) > halfHeight) continue;

                Vector2 v = body.linearVelocity;
                float along = Vector2.Dot(v, axis);
                if (along >= maxSpeed) continue;
                body.linearVelocity = v + axis * (Mathf.Min(along + gain, maxSpeed) - along);
            }
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
