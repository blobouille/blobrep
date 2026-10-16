using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BLOB : MonoBehaviour, ICameraTarget, ILevelPhysics, ILevelTick, ITrainingBody
{
    [Header("Forme & Apparence")]
    [Tooltip("Nombre de capsules qui forment l'anneau du blob. Plus = plus rond et plus souple, mais plus coûteux.")]
    [Range(3, 32)] public int sides = 16;
    [Tooltip("Diamètre extérieur du blob au repos (bord extérieur des capsules), en unités Unity (absolu).\n" +
             "1 = même taille qu'une Ball2D de diamètre 1 (standard du projet).")]
    [Min(0.01f)] public float diameter = 1f;
    [Tooltip("Couleur des capsules dans le rendu debug de la physique et dans l'aperçu Scene view.")]
    public Color debugColor = Color.cyan;

    [Header("Paramètres des Capsules")]
    [Tooltip("Rayon (épaisseur) de chaque capsule, RELATIF au rayon de l'anneau (centre → axe des capsules).\n" +
             "Le diamètre extérieur reste égal à 'diameter' : plus les capsules sont épaisses, plus l'anneau est petit.")]
    [Min(0.001f)] public float capsuleRadius = 0.5f;
    [Tooltip("Espace vide entre deux capsules voisines, en unités Unity (absolu). 0 = capsules bout à bout.")]
    [Min(0f)] public float capsuleSpacing = 0f;

    [Header("Joints & Physique")]
    [Tooltip("Densité globale du blob, comme s'il était un disque plein de diamètre 'diameter' :\n" +
             "masse totale = density × π × (diameter/2)², répartie entre les capsules.\n" +
             "Même diamètre et même densité qu'une Ball2D = même masse.")]
    [Min(0.001f)] public float density = 1f;
    [Tooltip("Raideur angulaire des joints entre capsules, en Hz. Plus haut = blob plus rigide en flexion.")]
    [Min(0f)] public float jointFrequency = 48f;
    [Tooltip("Amortissement angulaire des joints (ratio, 1 = critique). Plus haut = moins d'oscillations en flexion.")]
    [Min(0f)] public float jointDamping = 1f;
    [Tooltip("Raideur linéaire des joints entre capsules, en Hz. Plus haut = l'anneau s'étire moins.")]
    [Min(0f)] public float jointLinearFrequency = 48f;
    [Tooltip("Amortissement linéaire des joints (ratio, 1 = critique). Plus haut = moins d'oscillations en étirement.")]
    [Min(0f)] public float jointLinearDamping = 1f;
    [Tooltip("Friction des capsules (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.03f;
    [Tooltip("Rebond des capsules : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0)] public float bounciness = 0.5f;
    [Tooltip("Mode « bullet » (fastCollisionsAllowed) sur chaque capsule : collision continue aussi contre les corps\n" +
             "dynamiques et kinematic (flippers), pas seulement contre les murs statiques. Plus coûteux.")]
    public bool fastCollisions = false;

    public static event System.Action<BLOB, BlobHazard, Vector2> Broken;

    // Blobs dont la physique existe, dans l'ordre de création (ordre de la hiérarchie via LevelRestart).
    static readonly List<BLOB> _active = new List<BLOB>();
    public static IReadOnlyList<BLOB> Active => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearActive() => _active.Clear();

    NativeList<PhysicsBody> _bodies;
    // _joints[i] relie la capsule i-1 à la capsule i.
    NativeList<PhysicsJoint> _joints;
    bool[] _attached;

    public bool IsBroken { get; private set; }
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public Vector2 CameraPosition
    {
        get
        {
            if (!_bodies.IsCreated || _bodies.Length == 0) return transform.position;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < _bodies.Length; i++) sum += _bodies[i].position;
            return sum / _bodies.Length;
        }
    }

    public int CapsuleCount => _bodies.IsCreated ? _bodies.Length : 0;
    public PhysicsBody Capsule(int i) => _bodies[i];

    public CapsuleGeometry GhostShape => CapsuleShape;
    public CapsuleGeometry CapsuleShape => new CapsuleGeometry
    {
        center1 = new Vector2(0f, -0.5f * CapsuleLength),
        center2 = new Vector2(0f, 0.5f * CapsuleLength),
        radius = CapsuleWorldRadius
    };

    public void GetCapsulePoses(List<PhysicsTransform> poses)
    {
        poses.Clear();
        if (!_bodies.IsCreated) return;
        for (int i = 0; i < _bodies.Length; i++) poses.Add(_bodies[i].transform);
    }

    public void GetBodyStates(List<BodyState> states)
    {
        states.Clear();
        if (!_bodies.IsCreated) return;
        for (int i = 0; i < _bodies.Length; i++)
        {
            var b = _bodies[i];
            states.Add(new BodyState { position = b.position, angle = b.rotation.radians, velocity = b.linearVelocity, angularVelocity = b.angularVelocity });
        }
    }

    // Replace le blob dans un état mémorisé (mode entraînement, quand le rejeu ne retombe plus au même endroit).
    // Blob recréé puis capsules posées : déterministe pour un même état, mais pas identique à un vrai passage.
    // Les capsules empalées ne le sont plus ; 'broken' casse les joints sans événement (pas de focus caméra).
    public void RestoreBodyStates(IReadOnlyList<BodyState> states, bool broken)
    {
        if (states.Count != sides) return;
        int order = _active.IndexOf(this);
        DestroyBlob();
        SpawnBlob(transform.position);
        if (order >= 0) { _active.Remove(this); _active.Insert(order, this); }

        for (int i = 0; i < sides; i++)
        {
            var body = _bodies[i];
            body.transform = new PhysicsTransform(states[i].position, PhysicsRotate.FromRadians(states[i].angle));
            body.linearVelocity = states[i].velocity;
            body.angularVelocity = states[i].angularVelocity;
        }
        if (broken)
        {
            for (int j = 0; j < _joints.Length; j++) BreakJoint(j);
            IsBroken = true;
        }
    }

    float RingRadius => 0.5f * diameter / (1f + capsuleRadius);
    float SegmentLength => PhysicsMath.TAU * RingRadius / sides;
    float CapsuleLength => math.max(0f, SegmentLength - capsuleSpacing);
    float CapsuleWorldRadius => capsuleRadius * RingRadius;
    float CapsuleArea => math.PI * CapsuleWorldRadius * CapsuleWorldRadius + 2f * CapsuleWorldRadius * CapsuleLength;
    float CapsuleDensity => density * math.PI * 0.25f * diameter * diameter / (sides * CapsuleArea);

    void OnEnable()
    {
        if (Application.isPlaying) SpawnBlob(transform.position);
    }

    void OnDisable()
    {
        DestroyBlob();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void DestroyBlob()
    {
        _active.Remove(this);
        if (_joints.IsCreated) _joints.Dispose();
        if (!_bodies.IsCreated) return;
        PhysicsBody.DestroyBatch(_bodies.AsArray());
        _bodies.Dispose();
    }

    // Contacts de l'état issu du dernier pas, parcourus dans un ordre fixe (capsules puis contacts Box2D).
    public void LevelTick()
    {
        if (!_bodies.IsCreated) return;

        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_attached[i]) continue;
            var contacts = _bodies[i].GetContacts(Allocator.Temp);
            for (int k = 0; k < contacts.Length; k++)
            {
                var manifold = contacts[k].manifold;
                if (manifold.pointCount == 0) continue;
                var hazardShape = contacts[k].shapeA;
                var hazard = hazardShape.userData.objectValue as BlobHazard;
                if (hazard == null)
                {
                    hazardShape = contacts[k].shapeB;
                    hazard = hazardShape.userData.objectValue as BlobHazard;
                }
                if (hazard == null) continue;
                Hit(i, hazard, hazardShape, manifold[0].point);
            }
            contacts.Dispose();
        }
    }

    void Hit(int capsule, BlobHazard hazard, PhysicsShape hazardShape, Vector2 point)
    {
        if (!IsBroken)
        {
            for (int j = 0; j < _joints.Length; j++) BreakJoint(j);
            IsBroken = true;
            Broken?.Invoke(this, hazard, point);
        }

        if (!_attached[capsule] && hazard.OnCapsuleTouched(_bodies[capsule], hazardShape, point)) _attached[capsule] = true;
    }

    void BreakJoint(int j)
    {
        var joint = _joints[j];
        if (!joint.isValid) return;
        joint.Destroy();
        _joints[j] = default;
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

        var capsule = CapsuleShape;

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        bodyDef.fastCollisionsAllowed = fastCollisions;

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.density = CapsuleDensity;
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

        _joints = new NativeList<PhysicsJoint>(sides, Allocator.Persistent);
        var prev = _bodies[_bodies.Length - 1];
        for (int i = 0; i < sides; ++i)
        {
            jointDef.bodyA = prev;
            jointDef.bodyB = _bodies[i];
            _joints.Add(world.CreateJoint(jointDef));
            prev = _bodies[i];
        }
        IsBroken = false;
        _attached = new bool[sides];
        _active.Add(this);
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

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddCircle(Vector2.zero, 0.5f * diameter);
        _pickArea.Draw(Matrix4x4.Translate(transform.position));
#endif
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
