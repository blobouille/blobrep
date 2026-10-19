using UnityEngine;
using Unity.U2D.Physics;

// Levier bumper : barre de la longueur d'un flipper, deux boules du rayon de la grosse boule du flipper (capsule).
// À l'appui, elle se déplace en ligne droite (perpendiculaire à la barre) de 'travel', puis revient au relâchement.
// Même touche et même pilotage que les flippers (kinematic piloté en vitesse, dernier pas pile sur la cible).
[ExecuteAlways]
[DisallowMultipleComponent]
public class PinballBumper : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Forme")]
    [Tooltip("Échelle globale : multiplie la longueur et le rayon (même valeur que 'size' des flippers).")]
    [Min(0.01f)] public float size = 1.68f;
    [Tooltip("Distance entre les centres des deux boules, en unités Unity, multipliée par 'size' (comme 'length' des flippers).\n" +
             "La première boule est centrée sur l'objet, la seconde vers +X local.")]
    [Min(0.05f)] public float length = 1.0f;
    [Tooltip("Rayon des deux boules, en unités Unity, multiplié par 'size' (comme 'baseRadius' des flippers).")]
    [Min(0.01f)] public float radius = 0.15f;

    [Header("Commande")]
    [Tooltip("Touche qui active le bumper. Même système que les flippers : une touche les déclenche tous ensemble.")]
    public FlipperKey key = FlipperKey.LeftArrow;
    [Tooltip("Déplacement depuis la position de repos, en unités Unity (absolu), perpendiculaire à la barre.\n" +
             "Positif = vers +Y local, négatif = vers -Y local.")]
    public float travel = 0.5f;
    [Tooltip("Vitesse du déplacement, en km/h (absolu). Défaut = vitesse du bout d'un flipper officiel\n" +
             "(500 °/s × 1,68 m ≈ 14,66 m/s ≈ 52,8 km/h).")]
    [SpeedKmh] [Min(0.01f)] public float speed = 14.66f;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view en position de repos.")]
    public Color previewColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [Tooltip("Couleur de l'aperçu Scene view en position active et du trajet.")]
    public Color previewActiveColor = new Color(1f, 0.6f, 0.1f, 0.6f);

    PhysicsBody _body;
    Vector2 _rest;
    Vector2 _direction;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public void BuildShape(out Vector2 c1, out Vector2 c2, out float r)
    {
        r = radius * size;
        c1 = Vector2.zero;
        c2 = new Vector2(length * size, 0f);
    }

    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    Vector2 Direction
    {
        get
        {
            float a = transform.eulerAngles.z * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
        }
    }

    void OnEnable()
    {
        if (Application.isPlaying) CreatePhysics();
    }

    void OnDisable()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void CreatePhysics()
    {
        _rest = transform.position;
        _direction = Direction;

        var def = PhysicsBodyDefinition.defaultDefinition;
        def.type = PhysicsBody.BodyType.Kinematic;
        def.position = _rest;
        def.rotation = PhysicsRotate.FromRadians(transform.eulerAngles.z * Mathf.Deg2Rad);
        _body = PhysicsWorld.defaultWorld.CreateBody(def);
        _body.sleepingAllowed = false;

        BuildShape(out var c1, out var c2, out var r);
        _body.CreateShape(new CapsuleGeometry { center1 = c1, center2 = c2, radius = r });
    }

    // Vitesse = reste à parcourir / pas, plafonnée : le dernier pas tombe exactement sur la cible.
    public void LevelTick()
    {
        if (!_body.isValid) return;
        float target = LevelInput.IsDown(key) ? travel : 0f;
        float current = Vector2.Dot(_body.position - _rest, _direction);
        float v = Mathf.Clamp((target - current) / Time.fixedDeltaTime, -speed, speed);
        _body.linearVelocity = _direction * v;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        BuildShape(out var c1, out var c2, out var r);
        var m = LocalToWorld;
        Vector3 offset = Vector3.up * travel;

        Gizmos.matrix = m;
        Gizmos.color = previewColor;
        DrawCapsule(c1, c2, r, Vector3.zero);
        Gizmos.color = previewActiveColor;
        DrawCapsule(c1, c2, r, offset);
        Gizmos.DrawLine((Vector3)c1, (Vector3)c1 + offset);
        Gizmos.DrawLine((Vector3)c2, (Vector3)c2 + offset);
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddCircle(c1, r);
        _pickArea.AddCircle(c2, r);
        _pickArea.AddConvex(new[] { c1 + Vector2.down * r, c2 + Vector2.down * r, c2 + Vector2.up * r, c1 + Vector2.up * r });
        _pickArea.Draw(m);
#endif
    }

    static void DrawCapsule(Vector2 c1, Vector2 c2, float r, Vector3 offset)
    {
        Vector3 a = (Vector3)c1 + offset, b = (Vector3)c2 + offset;
        Gizmos.DrawLine(a + Vector3.up * r, b + Vector3.up * r);
        Gizmos.DrawLine(a + Vector3.down * r, b + Vector3.down * r);
        DrawCircle(a, r);
        DrawCircle(b, r);
    }

    static void DrawCircle(Vector3 c, float r)
    {
        const int N = 32;
        Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
