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
        float dt = Time.fixedDeltaTime;
        float target = LevelInput.IsDown(key) ? travel : 0f;
        Vector2 position = _body.position;
        float current = Vector2.Dot(position - _rest, _direction);
        float v = Mathf.Clamp((target - current) / dt, -speed, speed);
        if (v != 0f) KickBodiesInPath(position, _direction * v, dt);
        _body.linearVelocity = _direction * v;
    }

    // Box2D ne voit un contact qu'à moins de 0,02 m et ne fait pas de collision continue contre un kinematic : à 0,46 m par
    // pas, le bumper traversait la paroi du blob. Chaque corps dynamique qu'il atteindra pendant ce pas reçoit donc d'avance
    // le choc d'un corps de masse infinie (sans rebond ni frottement) au point de contact : il part à la vitesse du bumper
    // et reste devant lui. Indépendant de l'ordre des résultats de Box2D (extrêmes départagés par la position).
    void KickBodiesInPath(Vector2 position, Vector2 velocity, float dt)
    {
        BuildShape(out var c1, out var c2, out var r);
        var rot = _body.rotation;
        var capsule = new CapsuleGeometry
        {
            center1 = position + new Vector2(rot.cos * c1.x - rot.sin * c1.y, rot.sin * c1.x + rot.cos * c1.y),
            center2 = position + new Vector2(rot.cos * c2.x - rot.sin * c2.y, rot.sin * c2.x + rot.cos * c2.y),
            radius = r
        };
        Vector2 move = velocity.normalized;
        Vector2 bar = new Vector2(-_direction.y, _direction.x);
        var hits = PhysicsWorld.defaultWorld.CastGeometry(capsule, velocity * dt, PhysicsQuery.QueryFilter.defaultFilter,
                                                          PhysicsQuery.WorldCastMode.All, Unity.Collections.Allocator.Temp);
        for (int i = 0; i < hits.Length; i++)
        {
            var body = hits[i].shape.isValid ? hits[i].shape.body : default;
            if (!body.isValid || body.type != PhysicsBody.BodyType.Dynamic || FirstHitOf(hits, body) != i) continue;

            // Une seule poussée par corps : au milieu des deux points de contact extrêmes le long de la barre (planche à plat
            // sur ses deux roues = translation pure, sans rotation parasite).
            int lo = i, hi = i;
            for (int j = i + 1; j < hits.Length; j++)
            {
                if (!hits[j].shape.isValid || hits[j].shape.body != body) continue;
                if (Before(hits[j].point, hits[lo].point, bar)) lo = j;
                if (Before(hits[hi].point, hits[j].point, bar)) hi = j;
            }
            Vector2 point = 0.5f * (hits[lo].point + hits[hi].point);
            Vector2 normal = Outward(hits[lo].normal, move) + Outward(hits[hi].normal, move);
            normal = normal.sqrMagnitude > 1e-8f ? normal.normalized : move;

            float deficit = Vector2.Dot(velocity - body.GetWorldPointVelocity(point), normal);
            if (deficit <= 0f) continue;
            float arm = Cross(point - body.worldCenterOfMass, normal);
            float inertia = body.rotationalInertia;
            float k = 1f / body.mass + (inertia > 0f ? arm * arm / inertia : 0f);
            body.ApplyLinearImpulse(normal * (deficit / k), point, true);
        }
        hits.Dispose();
    }

    static int FirstHitOf(Unity.Collections.NativeArray<PhysicsQuery.WorldCastResult> hits, PhysicsBody body)
    {
        for (int i = 0; i < hits.Length; i++)
            if (hits[i].shape.isValid && hits[i].shape.body == body) return i;
        return -1;
    }

    static bool Before(Vector2 a, Vector2 b, Vector2 axis)
    {
        float da = Vector2.Dot(a, axis), db = Vector2.Dot(b, axis);
        return da != db ? da < db : a.x != b.x ? a.x < b.x : a.y < b.y;
    }

    // Normale du contact orientée dans le sens du mouvement (le signe rendu par le lancer n'est pas garanti).
    static Vector2 Outward(Vector2 n, Vector2 move) => Vector2.Dot(n, move) < 0f ? -n : n;

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

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
