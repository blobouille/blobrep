using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BLOB : MonoBehaviour, ICameraTarget, ILevelPhysics, ILevelTick, ITrainingBody, IContactFxSource
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

    [Header("Lest (action du joueur)")]
    [Tooltip("Touche tenue = le blob s'alourdit : sa gravité est multipliée par 'heavyGravity'.")]
    public FlipperKey heavyKey = FlipperKey.DownArrow;
    [Tooltip("Gravité du blob touche tenue, en multiple de la gravité du monde (1 = sans effet).\n" +
             "Au sol : plus d'appui, donc plus d'accélération dans les descentes et plus de freinage dans les montées.\n" +
             "En l'air : chute plus rapide (on raccourcit un saut, on prend de la vitesse avant une descente).")]
    [Min(1f)] public float heavyGravity = 3f;
    [Tooltip("Temps (s) pour passer de la gravité normale à 'heavyGravity' quand on appuie. 0 = immédiat.")]
    [Min(0f)] public float heavyRampTime = 0.1f;
    [Tooltip("Temps (s) pour revenir à la gravité normale au relâchement. 0 = immédiat.")]
    [Min(0f)] public float heavyReleaseTime = 0.05f;
    [Tooltip("Vitesse de chute (km/h, moyenne du blob dans le sens de la gravité) au-delà de laquelle le lest n'ajoute plus rien.\n" +
             "0 = sans limite.")]
    [SpeedKmh] [Min(0f)] public float heavyMaxFallSpeed = 0f;

    public static event System.Action<BLOB, BlobHazard, Vector2> Broken;

    // Catégorie propre aux capsules (sans le bit 0) : les morceaux de verre en mode Ignore / Pulverize l'excluent.
    // Tout le reste accepte toutes les catégories : contacts inchangés.
    public const int CategoryBit = 28;

    // Blobs dont la physique existe, dans l'ordre de création (ordre de la hiérarchie via LevelRestart).
    static readonly List<BLOB> _active = new List<BLOB>();
    public static IReadOnlyList<BLOB> Active => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearActive()
    {
        _active.Clear();
        _groupCounter = 0;
    }

    static int _groupCounter;

    NativeList<PhysicsBody> _bodies;
    // _joints[i] relie la capsule i-1 à la capsule i.
    NativeList<PhysicsJoint> _joints;
    bool[] _attached;
    PhysicsShape[] _shapes;
    int[] _hazardTouches; // contacts touchants (avec points) entre chaque capsule et une forme d'obstacle
    int[] _touches;       // contacts touchants de chaque capsule, toutes formes confondues
    float[] _stretch;     // allongement de chaque capsule (SetCapsuleStretch)
    float[] _shapeStretch; // allongement de sa forme (suit par pas)
    float _capsuleRadius;  // rayon actuel des formes (SetCapsuleRadius)
    int _group;
    int _touchingCapsules;
    int _eventsStep = -1;

    public bool IsBroken { get; private set; }
    // Change à chaque recréation des capsules (redémarrage, replacement du mode entraînement).
    public int Generation { get; private set; }
    // Lest : 0 = gravité normale, 1 = 'heavyGravity'.
    public float Heaviness { get; private set; }
    float _gravityScale = 1f;
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
    public int JointCount => _joints.IsCreated ? _joints.Length : 0;
    // Joint(j) relie la capsule j-1 à la capsule j (ancre A au bout +Y de j-1, ancre B au bout -Y de j). Invalide une fois cassé.
    public PhysicsJoint Joint(int j) => _joints[j];
    // Rayon de l'anneau (centre → axe des capsules) au repos, en m.
    public float RestRingRadius => RingRadius;
    // Position de l'ancre A des joints sur la capsule j-1 (sans rotation au repos).
    public Vector2 JointAnchorA => new Vector2(0f, 0.5f * SegmentLength);

    // Forme voulue de l'anneau, en une écriture (une par joint, une forme de capsule de temps en temps) :
    // - stretch[i] (m, ≥ 0) : allongement de la capsule i ; l'écart entre ses deux joints grandit d'autant (les joints l'écartent
    //   de ses voisines). Sa forme suit par pas de 'StretchShapeStep' : changer une forme reconstruit ses contacts dans Box2D
    //   (à chaque pas pour 32 capsules : ≈ +125 µs). Masse et inertie inchangées (Box2D ne les recalcule pas).
    // - bend[j] (rad) : angle de repos du joint j (capsule j-1 → j), 0 = d'origine.
    // Tout à 0 = blob d'origine, exactement. Les capsules d'un blob cassé gardent leur forme (joints détruits).
    public float CapsuleStretch(int i) => _stretch[i];

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
                var geometry = CapsuleShape;
                geometry.center1 = new Vector2(0f, -0.5f * (CapsuleLength + extra));
                geometry.center2 = new Vector2(0f, 0.5f * (CapsuleLength + extra));
                geometry.radius = _capsuleRadius;
                shape.capsuleGeometry = geometry;
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

    // Blob propriétaire d'une forme (null si ce n'est pas une capsule de blob ou si elle est détruite).
    public static BLOB FromShape(PhysicsShape shape) => shape.isValid ? shape.userData.objectValue as BLOB : null;

    // Groupe de collision (négatif : ses capsules ne se touchent pas entre elles). Donner à un blob le groupe d'un autre = ils se
    // traversent (sortir un slime enfermé dans un autre) ; OwnGroup = le sien.
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

    // Épaisseur réelle des capsules (rayon, en m) au repos.
    public float CapsuleThickness => CapsuleWorldRadius;
    // Rayon actuel des capsules (m) : celui du repos, ou celui donné par SetCapsuleRadius (fusion en cours).
    public float CapsuleRadius => _capsuleRadius;

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
            if (!shape.isValid) continue;
            var geometry = CapsuleShape;
            geometry.center1 = new Vector2(0f, -0.5f * (CapsuleLength + _shapeStretch[i]));
            geometry.center2 = new Vector2(0f, 0.5f * (CapsuleLength + _shapeStretch[i]));
            geometry.radius = radius;
            shape.capsuleGeometry = geometry;
        }
    }

    // Contacts du dernier pas (événements de Box2D, lus une fois par pas) : la capsule i touche-t-elle quelque chose ?
    public bool IsTouching(int i)
    {
        RefreshContacts();
        return _touches[i] > 0;
    }

    public int TouchingCapsules
    {
        get
        {
            RefreshContacts();
            return _touchingCapsules;
        }
    }

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

    public void GetSurfaceContacts(List<SurfaceContact> result)
    {
        result.Clear();
        if (!_bodies.IsCreated || _bodies.Length == 0) return;
        Vector2 center = Vector2.zero;
        for (int i = 0; i < _bodies.Length; i++) center += _bodies[i].linearVelocity;
        center /= _bodies.Length;

        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_attached[i]) continue;
            var body = _bodies[i];
            Vector2 velocity = IsBroken ? body.linearVelocity : center;
            var contacts = body.GetContacts(Allocator.Temp);
            for (int k = 0; k < contacts.Length; k++)
            {
                var manifold = contacts[k].manifold;
                if (manifold.pointCount == 0) continue;
                bool isA = contacts[k].shapeA.body == body;
                var other = isA ? contacts[k].shapeB.body : contacts[k].shapeA.body;
                Vector2 normal = isA ? -manifold.normal : manifold.normal;
                for (int p = 0; p < manifold.pointCount; p++)
                {
                    var mp = manifold[p];
                    result.Add(new SurfaceContact
                    {
                        point = mp.point,
                        normal = normal,
                        relativeVelocity = velocity - other.GetWorldPointVelocity(mp.point),
                        normalImpulse = mp.normalImpulse,
                        approachSpeed = -mp.normalVelocity,
                        isNew = !mp.persisted
                    });
                }
            }
            contacts.Dispose();
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

    struct HazardContact
    {
        public BlobHazard hazard;
        public PhysicsShape shape;
        public Vector2 point;
    }

    HazardContact[] _hazardContacts = new HazardContact[8];

    // Contacts de l'état issu du dernier pas, dans un ordre fixe : capsules, puis contacts triés par position
    // (l'ordre renvoyé par Box2D suit ses identifiants internes, qui changent d'un essai à l'autre).
    // Seules les capsules qui touchent un obstacle relisent leurs contacts (rejeu accéléré des sauvegardes plus rapide).
    public void LevelTick()
    {
        if (!_bodies.IsCreated) return;
        UpdateHeavy();
        RefreshContacts();

        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_attached[i] || _hazardTouches[i] == 0) continue;
            int count = 0;
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
                if (count == _hazardContacts.Length) System.Array.Resize(ref _hazardContacts, 2 * count);
                _hazardContacts[count++] = new HazardContact { hazard = hazard, shape = hazardShape, point = manifold[0].point };
            }
            contacts.Dispose();

            for (int a = 1; a < count; a++)
            {
                var c = _hazardContacts[a];
                int b = a - 1;
                for (; b >= 0 && Compare(_hazardContacts[b], c) > 0; b--) _hazardContacts[b + 1] = _hazardContacts[b];
                _hazardContacts[b + 1] = c;
            }
            for (int k = 0; k < count; k++)
            {
                Hit(i, _hazardContacts[k].hazard, _hazardContacts[k].shape, _hazardContacts[k].point);
                _hazardContacts[k] = default;
            }
        }
    }

    // Gravité propre de chaque capsule (gravityScale, appliquée par Box2D comme la gravité du monde : aucune déformation).
    // Réécrite seulement quand elle change.
    void UpdateHeavy()
    {
        float dt = Time.fixedDeltaTime;
        if (!IsBroken && LevelInput.IsDown(heavyKey))
            Heaviness = heavyRampTime > 0f ? Mathf.Min(1f, Heaviness + dt / heavyRampTime) : 1f;
        else
            Heaviness = heavyReleaseTime > 0f ? Mathf.Max(0f, Heaviness - dt / heavyReleaseTime) : 0f;

        float scale = 1f + (heavyGravity - 1f) * Heaviness;
        if (scale > 1f && heavyMaxFallSpeed > 0f && FallSpeed() >= heavyMaxFallSpeed) scale = 1f;
        if (scale == _gravityScale) return;
        _gravityScale = scale;
        for (int i = 0; i < _bodies.Length; i++)
        {
            var body = _bodies[i];
            body.gravityScale = scale;
        }
    }

    float FallSpeed()
    {
        Vector2 g = PhysicsWorld.defaultWorld.gravity;
        if (g.sqrMagnitude < 1e-8f) return 0f;
        Vector2 v = Vector2.zero;
        for (int i = 0; i < _bodies.Length; i++) v += _bodies[i].linearVelocity;
        return Vector2.Dot(v / _bodies.Length, g.normalized);
    }

    // Position, puis forme de l'obstacle, puis ordre de création des obstacles (égalités : sommets partagés entre piques).
    static int Compare(in HazardContact a, in HazardContact b)
    {
        int c = a.point.x.CompareTo(b.point.x);
        if (c != 0) return c;
        c = a.point.y.CompareTo(b.point.y);
        if (c != 0) return c;
        c = a.shape.userData.intValue.CompareTo(b.shape.userData.intValue);
        if (c != 0 || a.hazard == b.hazard) return c;
        var physics = LevelRestart.Physics;
        for (int k = 0; k < physics.Count; k++)
        {
            if (physics[k] == a.hazard) return -1;
            if (physics[k] == b.hazard) return 1;
        }
        return 0;
    }

    // Les capsules signalent leurs contacts (contactEvents, sans effet sur la simulation) : Box2D émet un début quand un contact
    // obtient des points (manifold.pointCount > 0, même condition que LevelTick) et une fin quand il les perd ou disparaît.
    // Événements du dernier pas, lus une fois par pas ; au pas 0 (redémarrage), ceux d'avant concernent des formes détruites.
    // Compteurs entiers : indépendants de l'ordre des événements.
    void RefreshContacts()
    {
        int step = LevelSimulation.Step;
        if (_eventsStep == step || !_bodies.IsCreated) return;
        _eventsStep = step;
        if (step == 0) return;
        var world = PhysicsWorld.defaultWorld;
        foreach (var e in world.contactBeginEvents) Touch(e.shapeA, e.shapeB, 1);
        foreach (var e in world.contactEndEvents) Touch(e.shapeA, e.shapeB, -1);
    }

    void Touch(PhysicsShape a, PhysicsShape b, int delta)
    {
        for (int i = 0; i < _shapes.Length; i++)
        {
            PhysicsShape other;
            if (_shapes[i] == a) other = b;
            else if (_shapes[i] == b) other = a;
            else continue;

            int before = _touches[i];
            _touches[i] = Mathf.Max(0, before + delta);
            if (before == 0 && _touches[i] > 0) _touchingCapsules++;
            else if (before > 0 && _touches[i] == 0) _touchingCapsules--;
            // Obstacles (et vitres) : les seules formes qui signalaient leurs contacts avant. Forme détruite : compteur gardé
            // (au pire une relecture inutile, jamais un obstacle manqué).
            if (other.isValid && other.contactEvents) _hazardTouches[i] = Mathf.Max(0, _hazardTouches[i] + delta);
            return;
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
        filter.categories = new PhysicsMask { bitMask = 1UL << CategoryBit };
        filter.contacts = new PhysicsMask { bitMask = ~(1UL << GlassPane.LooseFragmentBit) };
        // Négatif : les capsules d'un même blob ne collisionnent pas entre elles. Un groupe par blob (avant : -1 pour tous,
        // deux blobs se traversaient). Seule l'égalité compte : la valeur ne change pas la simulation.
        _group = -(++_groupCounter);
        filter.groupIndex = _group;
        shapeDef.contactFilter = filter;
        shapeDef.contactEvents = true;

        _shapes = new PhysicsShape[sides];
        _hazardTouches = new int[sides];
        _touches = new int[sides];
        _stretch = new float[sides];
        _shapeStretch = new float[sides];
        _capsuleRadius = CapsuleWorldRadius;
        _touchingCapsules = 0;
        _eventsStep = LevelSimulation.Step; // événements encore en attente : ceux des anciennes capsules
        Generation++;
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
        IsBroken = false;
        Heaviness = 0f;
        _gravityScale = 1f;
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
