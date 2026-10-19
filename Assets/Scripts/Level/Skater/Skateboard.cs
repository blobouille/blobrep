using System.Collections.Generic;
using UnityEngine;

// Géométrie de la planche du skateur (source unique pour l'aperçu et la collision). La physique est créée par le Skater.
// Repère local : origine au sol, au milieu des deux roues (planche à plat), +X = nose, +Y = vers le haut.
[ExecuteAlways]
[DisallowMultipleComponent]
public class Skateboard : MonoBehaviour
{
    [Header("Planche")]
    [Tooltip("Longueur totale de la planche, d'un bout à l'autre le long du plateau (relevés compris), en m (absolu).")]
    [Min(0.1f)] public float length = 0.78f;
    [Tooltip("Épaisseur du plateau, en m (absolu).")]
    [Min(0.005f)] public float thickness = 0.035f;

    [Header("Relevés (kicks)")]
    [Tooltip("Relevé à l'avant (nose, côté +X local).")]
    public bool noseKick = true;
    [Tooltip("Relevé à l'arrière (tail, côté -X local). C'est lui qui frotte au sol pour freiner.")]
    public bool tailKick = true;
    [Tooltip("Angle des relevés par rapport au plateau, en degrés.")]
    [Range(0f, 60f)] public float kickAngle = 18f;
    [Tooltip("Longueur de chaque relevé le long du plateau, en m (absolu), comprise dans 'length'.")]
    [Min(0f)] public float kickLength = 0.13f;

    [Header("Roues et trucks")]
    [Tooltip("Diamètre des roues, en m (absolu). Plus grand = passe mieux les bosses et les jointures de courbe.")]
    [Min(0.01f)] public float wheelDiameter = 0.1f;
    [Tooltip("Empattement : distance entre les axes des roues avant et arrière, en m (absolu).")]
    [Min(0.02f)] public float wheelbase = 0.4f;
    [Tooltip("Hauteur des trucks : distance entre l'axe des roues et le dessous du plateau, en m (absolu).")]
    [Min(0f)] public float truckHeight = 0.03f;

    [Header("Matériau")]
    [Tooltip("Friction des roues. 0 = roue libre parfaite (la roue glisse sans frotter, comme si elle roulait sans résistance).")]
    [Min(0f)] public float wheelFriction = 0f;
    [Tooltip("Friction du plateau, des relevés et des trucks : freinage quand le tail frotte au sol.")]
    [Min(0f)] public float deckFriction = 0.5f;
    [Tooltip("Rebond de toute la planche : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0f)] public float bounciness = 0f;

    [Header("Debug")]
    [Tooltip("Couleur de la planche dans le rendu debug et l'aperçu Scene view.")]
    public Color color = new Color(0.95f, 0.75f, 0.3f, 1f);

#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public float WheelRadius => 0.5f * wheelDiameter;
    public float DeckBottom => WheelRadius + truckHeight;
    public float DeckTop => DeckBottom + thickness;
    float FlatHalf => 0.5f * Mathf.Max(0.02f, length - KickCount * kickLength);
    int KickCount => (noseKick ? 1 : 0) + (tailKick ? 1 : 0);

    public Vector2 WheelCenter(int side) => new Vector2(side * 0.5f * wheelbase, WheelRadius);

    // Point de la planche où pose le pied (cheville au-dessus du plateau) : 'side' = +1 avant, -1 arrière.
    public Vector2 FootPoint(int side, float spacing, float footHeight) => new Vector2(side * 0.5f * spacing, DeckTop + footHeight);

    // Bout du relevé (dessous), côté 'side' : point qui touche le sol quand la planche bascule.
    public Vector2 KickTip(int side)
    {
        bool kick = side > 0 ? noseKick : tailKick;
        float a = kickAngle * Mathf.Deg2Rad;
        float half = FlatHalf;
        if (!kick) return new Vector2(side * half, DeckBottom);
        return new Vector2(side * (half + kickLength * Mathf.Cos(a)), DeckBottom + kickLength * Mathf.Sin(a));
    }

    // Polygones convexes (plateau, relevés, trucks) et roues (x, y, rayon), repère local de la planche.
    // Les deux roues sont les deux cercles : arrière puis avant.
    // Index du relevé 'side' (-1 tail, +1 nose) dans les polygones de BuildGeometry, -1 s'il n'existe pas.
    public int KickIndex(int side)
    {
        bool tail = tailKick && kickLength > 0f, nose = noseKick && kickLength > 0f;
        if (side < 0) return tail ? 1 : -1;
        return nose ? (tail ? 2 : 1) : -1;
    }

    public void BuildGeometry(List<Vector2[]> polygons, List<Vector3> circles)
    {
        polygons.Clear();
        circles.Clear();
        float half = FlatHalf;
        float yb = DeckBottom, yt = DeckTop;
        polygons.Add(new[] { new Vector2(-half, yb), new Vector2(half, yb), new Vector2(half, yt), new Vector2(-half, yt) });

        float a = kickAngle * Mathf.Deg2Rad;
        for (int side = -1; side <= 1; side += 2)
        {
            if (!(side > 0 ? noseKick : tailKick) || kickLength <= 0f) continue;
            var d = new Vector2(side * Mathf.Cos(a), Mathf.Sin(a));
            var n = new Vector2(-d.y, d.x) * side;
            var b0 = new Vector2(side * half, yb);
            var b1 = b0 + kickLength * d;
            var quad = new[] { b0, b1, b1 + thickness * n, b0 + thickness * n };
            if (side < 0) System.Array.Reverse(quad);
            polygons.Add(quad);
        }

        float tw = 0.4f * wheelDiameter;
        for (int side = -1; side <= 1; side += 2)
        {
            var c = WheelCenter(side);
            polygons.Add(new[] { new Vector2(c.x - 0.25f * tw, c.y), new Vector2(c.x + 0.25f * tw, c.y),
                                 new Vector2(c.x + tw, yb), new Vector2(c.x - tw, yb) });
        }
        for (int side = -1; side <= 1; side += 2)
        {
            var c = WheelCenter(side);
            circles.Add(new Vector3(c.x, c.y, WheelRadius));
        }
    }

    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    void OnDisable()
    {
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    readonly List<Vector2[]> _polygons = new List<Vector2[]>();
    readonly List<Vector3> _circles = new List<Vector3>();

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        BuildGeometry(_polygons, _circles);
        Gizmos.matrix = LocalToWorld;
        Gizmos.color = color;
        foreach (var p in _polygons)
            for (int i = 0; i < p.Length; i++) Gizmos.DrawLine(p[i], p[(i + 1) % p.Length]);
        foreach (var c in _circles) SkaterGizmos.Circle(new Vector2(c.x, c.y), c.z);
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        foreach (var p in _polygons) _pickArea.AddConvex(p);
        foreach (var c in _circles) _pickArea.AddCircle(new Vector2(c.x, c.y), c.z);
        _pickArea.Draw(LocalToWorld);
#endif
    }
}

static class SkaterGizmos
{
    public static void Circle(Vector2 c, float r)
    {
        const int N = 24;
        Vector3 prev = c + new Vector2(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }

    public static void Capsule(Vector2 a, Vector2 b, float r)
    {
        var d = b - a;
        var n = d.sqrMagnitude > 1e-12f ? new Vector2(-d.y, d.x).normalized * r : Vector2.zero;
        Gizmos.DrawLine(a + n, b + n);
        Gizmos.DrawLine(a - n, b - n);
        Circle(a, r);
        Circle(b, r);
    }
}
