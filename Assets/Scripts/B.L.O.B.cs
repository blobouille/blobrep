using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BLOB : MonoBehaviour
{
    [Header("Forme & Apparence")]
    [Tooltip("Nombre de capsules qui forment l'anneau du blob. Plus = plus rond et plus souple, mais plus coûteux.")]
    [Range(3, 32)] public int sides = 16;
    [Tooltip("Rayon de l'anneau (distance du centre aux capsules), en unités Unity. Sert aussi d'échelle pour 'capsuleRadius'.")]
    [Min(0.01f)] public float initialScale = 0.15f;
    [Tooltip("Couleur des capsules dans le rendu debug de la physique et dans l'aperçu Scene view.")]
    public Color debugColor = Color.cyan;

    [Header("Paramètres des Capsules")]
    [Tooltip("Rayon (épaisseur) de chaque capsule, RELATIF à 'initialScale' : rayon réel = capsuleRadius × initialScale.")]
    [Min(0.001f)] public float capsuleRadius = 0.5f;
    [Tooltip("Espace vide entre deux capsules voisines, en unités Unity (absolu). 0 = capsules bout à bout.")]
    [Min(0f)] public float capsuleSpacing = 0f;

    [Header("Joints & Physique")]
    [Tooltip("Densité de chaque capsule : plus elle est haute, plus le blob est lourd.")]
    [Min(0.001f)] public float density = 1f;
    [Tooltip("Raideur angulaire des joints entre capsules, en Hz. Plus haut = blob plus rigide en flexion.")]
    [Min(0f)] public float jointFrequency = 64f;
    [Tooltip("Amortissement angulaire des joints (ratio, 1 = critique). Plus haut = moins d'oscillations en flexion.")]
    [Min(0f)] public float jointDamping = 1f;
    [Tooltip("Raideur linéaire des joints entre capsules, en Hz. Plus haut = l'anneau s'étire moins.")]
    [Min(0f)] public float jointLinearFrequency = 64f;
    [Tooltip("Amortissement linéaire des joints (ratio, 1 = critique). Plus haut = moins d'oscillations en étirement.")]
    [Min(0f)] public float jointLinearDamping = 1f;
    [Tooltip("Friction des capsules (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.03f;
    [Tooltip("Rebond des capsules : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0)] public float bounciness = 0.5f;

    NativeList<PhysicsBody> _bodies;

    float RingRadius => initialScale;
    float SegmentLength => PhysicsMath.TAU * RingRadius / sides;
    float CapsuleLength => math.max(0f, SegmentLength - capsuleSpacing);
    float CapsuleWorldRadius => capsuleRadius * initialScale;

    void OnEnable()
    {
        if (Application.isPlaying) SpawnBlob(transform.position);
    }

    void OnDisable()
    {
        DestroyBlob();
    }

    [ContextMenu("Respawn Blob")]
    public void Respawn()
    {
        if (!Application.isPlaying) return;
        DestroyBlob();
        SpawnBlob(transform.position);
    }

    void DestroyBlob()
    {
        if (!_bodies.IsCreated) return;
        PhysicsBody.DestroyBatch(_bodies.AsArray());
        _bodies.Dispose();
    }

    void OnDestroy()
    {
        DestroyBlob();
    }

    void SegmentPose(int i, Vector2 center, out Vector2 position, out float angle)
    {
        angle = i * PhysicsMath.TAU / sides;
        position = new Vector2(RingRadius * math.cos(angle) + center.x, RingRadius * math.sin(angle) + center.y);
    }

    void SpawnBlob(Vector2 center)
    {
        var world = PhysicsWorld.defaultWorld;
        _bodies = new NativeList<PhysicsBody>(sides, Allocator.Persistent);

        var capsule = new CapsuleGeometry
        {
            center1 = new Vector2(0f, -0.5f * CapsuleLength),
            center2 = new Vector2(0f, 0.5f * CapsuleLength),
            radius = CapsuleWorldRadius
        };

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.density = density;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        material.customColor = debugColor;
        shapeDef.surfaceMaterial = material;
        var filter = shapeDef.contactFilter;
        filter.categories = 1;
        filter.contacts = PhysicsMask.All;
        filter.groupIndex = -1; // négatif : les capsules d'un même blob ne collisionnent pas entre elles
        shapeDef.contactFilter = filter;

        for (int i = 0; i < sides; ++i)
        {
            SegmentPose(i, center, out var pos, out var angle);
            bodyDef.position = pos;
            bodyDef.rotation = PhysicsRotate.FromRadians(angle);

            var body = world.CreateBody(bodyDef);
            body.CreateShape(capsule, shapeDef);
            _bodies.Add(body);
        }

        // Ancres sur la longueur théorique : le joint couvre l'espace vide entre capsules.
        var jointDef = PhysicsFixedJointDefinition.defaultDefinition;
        jointDef.angularFrequency = jointFrequency;
        jointDef.angularDamping = jointDamping;
        jointDef.linearFrequency = jointLinearFrequency;
        jointDef.linearDamping = jointLinearDamping;
        jointDef.localAnchorA = new Vector2(0f, 0.5f * SegmentLength);
        jointDef.localAnchorB = new Vector2(0f, -0.5f * SegmentLength);

        var prev = _bodies[_bodies.Length - 1];
        for (int i = 0; i < sides; ++i)
        {
            jointDef.bodyA = prev;
            jointDef.bodyB = _bodies[i];
            world.CreateJoint(jointDef);
            prev = _bodies[i];
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.color = debugColor;
        float r = CapsuleWorldRadius;
        float half = 0.5f * CapsuleLength;

        for (int i = 0; i < sides; ++i)
        {
            SegmentPose(i, transform.position, out var pos, out var angle);
            var axis = new Vector2(-math.sin(angle), math.cos(angle));
            var normal = new Vector2(math.cos(angle), math.sin(angle));
            Vector2 a = pos - axis * half;
            Vector2 b = pos + axis * half;

            Gizmos.DrawLine(a + normal * r, b + normal * r);
            Gizmos.DrawLine(a - normal * r, b - normal * r);
            DrawCircle(a, r);
            DrawCircle(b, r);
        }
    }

    static void DrawCircle(Vector2 c, float r)
    {
        const int N = 16;
        Vector3 prev = c + new Vector2(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
