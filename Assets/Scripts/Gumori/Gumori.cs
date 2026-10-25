using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

// Corps physique d'un gumori : anneau de capsules reliées par des joints fixes (précontraints : ils visent un angle relatif
// nul, seules les différences d'angle comptent). Créé en Play dans OnEnable, à la position de l'objet, détruit dans OnDisable.
// Les capsules portent leur gumori en userData.objectValue (FromShape). Forme imposée par SetShape (fusion), groupe de
// collision par SetCollisionGroup (sortir un gumori enfermé dans un autre).
[ExecuteAlways]
[DisallowMultipleComponent]
public class Gumori : MonoBehaviour, ILevelPhysics
{
    [Header("Forme & Apparence")]
    [Tooltip("Nombre de capsules qui forment l'anneau. Plus = plus rond et plus souple, mais plus coûteux.")]
    [Range(3, 32)] public int sides = 16;
    [Tooltip("Diamètre extérieur au repos (bord extérieur des capsules), en m (absolu). 1 = taille de référence du projet.")]
    [Min(0.01f)] public float diameter = 1f;
    [Tooltip("Couleur des capsules dans le rendu debug de la physique et dans l'aperçu Scene view.")]
    public Color debugColor = Color.cyan;

    [Header("Paramètres des Capsules")]
    [Tooltip("Rayon (épaisseur) de chaque capsule, RELATIF au rayon de l'anneau (centre → axe des capsules).\n" +
             "Le diamètre extérieur reste égal à 'diameter' : plus les capsules sont épaisses, plus l'anneau est petit.")]
    [Min(0.001f)] public float capsuleRadius = 0.5f;
    [Tooltip("Espace vide entre deux capsules voisines, en m (absolu). 0 = capsules bout à bout.")]
    [Min(0f)] public float capsuleSpacing = 0f;

    [Header("Joints & Physique")]
    [Tooltip("Densité globale, comme s'il était un disque plein de diamètre 'diameter' :\n" +
             "masse totale = density × π × (diameter/2)², répartie entre les capsules.")]
    [Min(0.001f)] public float density = 1f;
    [Tooltip("Raideur angulaire des joints entre capsules, en Hz. Plus haut = plus rigide en flexion.")]
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
             "dynamiques et kinematic, pas seulement contre les murs statiques. Plus coûteux.")]
    public bool fastCollisions = false;

    // Catégorie des capsules, et catégorie exclue de leurs contacts (anciens éclats de verre) : gardées telles quelles, les
    // filtres de contact entrent dans la simulation.
    const int CategoryBit = 28, ExcludedBit = 27;

    static int _groupCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetGroups() => _groupCounter = 0;

    NativeList<PhysicsBody> _bodies;
    // _joints[i] relie la capsule i-1 à la capsule i.
    NativeList<PhysicsJoint> _joints;
    PhysicsShape[] _shapes;
    float[] _stretch;      // allongement de chaque capsule (SetShape)
    float[] _shapeStretch; // allongement de sa forme (suit par pas)
    float _capsuleRadius;  // rayon actuel des formes (SetCapsuleRadius)
    int _group;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    // Rang dans la liste des gumori en jeu (GumoriDropper.Active), -1 hors de la liste.
    public int Index { get; internal set; } = -1;

    public int CapsuleCount => _bodies.IsCreated ? _bodies.Length : 0;
    public PhysicsBody Capsule(int i) => _bodies[i];
    public PhysicsShape CapsuleShapeAt(int i) => _shapes[i];
    // Rayon de l'anneau (centre → axe des capsules) au repos, en m.
    public float RestRingRadius => RingRadius;
    // Épaisseur réelle des capsules (rayon, en m) au repos.
    public float CapsuleThickness => CapsuleWorldRadius;
    // Rayon actuel des capsules (m) : celui du repos, ou celui donné par SetCapsuleRadius (fusion en cours).
    public float CapsuleRadius => _capsuleRadius;

    // Gumori propriétaire d'une forme (null si ce n'est pas une capsule de gumori ou si elle est détruite).
    public static Gumori FromShape(PhysicsShape shape) => shape.isValid ? shape.userData.objectValue as Gumori : null;

    // Centre (moyenne des capsules) ; sans physique : la position de l'objet.
    public Vector2 Center
    {
        get
        {
            int n = CapsuleCount;
            if (n == 0) return transform.position;
            Vector2 c = Vector2.zero;
            for (int k = 0; k < n; k++) c += _bodies[k].position;
            return c / n;
        }
    }

    // Vitesse moyenne des capsules.
    public Vector2 Velocity
    {
        get
        {
            int n = CapsuleCount;
            Vector2 v = Vector2.zero;
            for (int k = 0; k < n; k++) v += _bodies[k].linearVelocity;
            return n > 0 ? v / n : v;
        }
    }

    // Même vitesse ajoutée à toutes les capsules : tout l'anneau se déplace, sa forme n'est pas touchée.
    public void AddVelocity(Vector2 dv)
    {
        for (int k = 0; k < CapsuleCount; k++)
        {
            var b = _bodies[k];
            b.linearVelocity += dv;
        }
    }

    // Point dans le polygone des centres des capsules (pair-impair).
    public bool Contains(Vector2 p)
    {
        bool inside = false;
        int n = CapsuleCount;
        if (n == 0) return false;
        Vector2 a = _bodies[n - 1].position;
        for (int i = 0; i < n; i++)
        {
            Vector2 b = _bodies[i].position;
            if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
            a = b;
        }
        return inside;
    }

    // Forme voulue de l'anneau, en une écriture (une par joint, une forme de capsule de temps en temps) :
    // - stretch[i] (m, ≥ 0) : allongement de la capsule i ; l'écart entre ses deux joints grandit d'autant (les joints l'écartent
    //   de ses voisines). Sa forme suit par pas de 'StretchShapeStep' : changer une forme reconstruit ses contacts dans Box2D
    //   (à chaque pas pour 32 capsules : ≈ +125 µs). Masse et inertie inchangées (Box2D ne les recalcule pas).
    // - bend[j] (rad) : angle de repos du joint j (capsule j-1 → j), 0 = d'origine.
    // Tout à 0 = anneau d'origine, exactement.
    const float StretchShapeStep = 0.01f;

    public void SetShape(float[] stretch, float[] bend)
    {
        int n = _bodies.Length;
        for (int i = 0; i < n; i++)
        {
            float extra = Mathf.Max(0f, stretch[i]);
            _stretch[i] = extra;
            var shape = _shapes[i];
            if (shape.isValid && (extra == 0f ? _shapeStretch[i] != 0f : Mathf.Abs(extra - _shapeStretch[i]) >= StretchShapeStep))
            {
                _shapeStretch[i] = extra;
                shape.capsuleGeometry = CapsuleGeometryOf(extra, _capsuleRadius);
            }
        }
        for (int j = 0; j < n; j++)
        {
            var joint = _joints[j];
            if (!joint.isValid) continue;
            int prev = j == 0 ? n - 1 : j - 1;
            float bendJ = bend[j];
            joint.localAnchorA = new PhysicsTransform(new Vector2(0f, 0.5f * (SegmentLength + _stretch[prev])),
                bendJ == 0f ? PhysicsRotate.identity : PhysicsRotate.FromRadians(bendJ));
            joint.localAnchorB = new Vector2(0f, -0.5f * (SegmentLength + _stretch[j]));
        }
    }

    // Groupe de collision (négatif : ses capsules ne se touchent pas entre elles). Donner à un gumori le groupe d'un autre = ils
    // se traversent ; OwnGroup = le sien.
    public int OwnGroup => _group;

    public void SetCollisionGroup(int group)
    {
        for (int i = 0; i < _shapes.Length; i++)
        {
            var shape = _shapes[i];
            if (!shape.isValid) continue;
            var filter = shape.contactFilter;
            if (filter.groupIndex == group) continue;
            filter.groupIndex = group;
            shape.contactFilter = filter;
        }
    }

    // Raideur en flexion des joints en fraction de 'jointFrequency' (1 = réglage). Jamais 0 (= rigide pour Box2D).
    public void SetBendStiffness(float scale)
    {
        for (int j = 0; j < _joints.Length; j++)
        {
            PhysicsFixedJoint joint = _joints[j];
            if (joint.isValid) joint.angularFrequency = jointFrequency * scale;
        }
    }

    // Rayon des capsules (m), allongements gardés. Reconstruit leurs contacts : à éviter à chaque pas. Masse inchangée.
    public void SetCapsuleRadius(float radius)
    {
        _capsuleRadius = radius;
        for (int i = 0; i < _shapes.Length; i++)
        {
            var shape = _shapes[i];
            if (shape.isValid) shape.capsuleGeometry = CapsuleGeometryOf(_shapeStretch[i], radius);
        }
    }

    CapsuleGeometry CapsuleGeometryOf(float stretch, float radius) => new CapsuleGeometry
    {
        center1 = new Vector2(0f, -0.5f * (CapsuleLength + stretch)),
        center2 = new Vector2(0f, 0.5f * (CapsuleLength + stretch)),
        radius = radius
    };

    float RingRadius => 0.5f * diameter / (1f + capsuleRadius);
    float SegmentLength => PhysicsMath.TAU * RingRadius / sides;
    float CapsuleLength => math.max(0f, SegmentLength - capsuleSpacing);
    float CapsuleWorldRadius => capsuleRadius * RingRadius;
    float CapsuleArea => math.PI * CapsuleWorldRadius * CapsuleWorldRadius + 2f * CapsuleWorldRadius * CapsuleLength;
    float CapsuleDensity => density * math.PI * 0.25f * diameter * diameter / (sides * CapsuleArea);

    void OnEnable()
    {
        if (Application.isPlaying) Spawn(transform.position);
    }

    void OnDisable()
    {
        Despawn();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void OnDestroy() => Despawn();

    void Despawn()
    {
        if (_joints.IsCreated) _joints.Dispose();
        if (!_bodies.IsCreated) return;
        PhysicsBody.DestroyBatch(_bodies.AsArray());
        _bodies.Dispose();
    }

    void SegmentPose(int i, Vector2 center, out Vector2 position, out float angle)
    {
        angle = i * PhysicsMath.TAU / sides;
        position = new Vector2(RingRadius * math.cos(angle) + center.x, RingRadius * math.sin(angle) + center.y);
    }

    void Spawn(Vector2 center)
    {
        var world = PhysicsWorld.defaultWorld;
        _bodies = new NativeList<PhysicsBody>(sides, Allocator.Persistent);

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
        filter.categories = new PhysicsMask { bitMask = 1UL << CategoryBit };
        filter.contacts = new PhysicsMask { bitMask = ~(1UL << ExcludedBit) };
        // Négatif : les capsules d'un même gumori ne collisionnent pas entre elles. Un groupe par gumori ; seule l'égalité
        // compte, la valeur ne change pas la simulation.
        _group = -(++_groupCounter);
        filter.groupIndex = _group;
        shapeDef.contactFilter = filter;
        // Début / fin de contact signalés (sans effet sur la simulation) : GumoriFusion les lit.
        shapeDef.contactEvents = true;

        _shapes = new PhysicsShape[sides];
        _stretch = new float[sides];
        _shapeStretch = new float[sides];
        _capsuleRadius = CapsuleWorldRadius;
        var capsule = CapsuleGeometryOf(0f, _capsuleRadius);
        for (int i = 0; i < sides; ++i)
        {
            SegmentPose(i, center, out var pos, out var angle);
            bodyDef.position = pos;
            bodyDef.rotation = PhysicsRotate.FromRadians(angle);

            var body = world.CreateBody(bodyDef);
            _shapes[i] = body.CreateShape(capsule, shapeDef);
            _shapes[i].userData = new PhysicsUserData { objectValue = this };
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
