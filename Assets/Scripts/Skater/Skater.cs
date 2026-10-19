using System.Collections.Generic;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

// Skateur (démo technique). Le joueur contrôle la PLANCHE : seul corps physique vivant, elle porte la masse de tout
// l'ensemble (centre de gravité bas = stable au sol). Le personnage (bonhomme bâton) est une pose recalculée à chaque pas
// sur la planche : rendu et hitbox seulement (requêtes de recouvrement, il ne pousse rien). Flèches : au sol, bascule de
// la planche (arrière = tail au sol, le frottement freine ; avant = nose, trop loin = chute) ; en l'air, rotation autour du
// centre de gravité commun personnage + planche. Tête ou torse qui touche, ou obstacle : mort, le personnage devient un
// ragdoll physique qui part avec sa vitesse.
// Repère local de l'objet = celui de la planche : origine au sol entre les roues, +X = nose.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Skateboard))]
public class Skater : MonoBehaviour, ICameraTarget, ILevelPhysics, ILevelTick, ITrainingBody, IContactFxSource
{
    [Header("Personnage : tailles")]
    [Tooltip("Taille du personnage en m (absolu) : toutes les longueurs ci-dessous sont des fractions de cette valeur.\n" +
             "≈ hauteur debout jambes tendues, des chevilles au sommet de la tête.")]
    [Min(0.1f)] public float height = 1.35f;
    [Tooltip("Longueur de la cuisse (hanche → genou), relative à 'height'.")]
    [Range(0.05f, 0.5f)] public float thighLength = 0.26f;
    [Tooltip("Longueur du tibia (genou → cheville), relative à 'height'.")]
    [Range(0.05f, 0.5f)] public float shinLength = 0.25f;
    [Tooltip("Longueur du torse (bassin → base du cou), relative à 'height'.")]
    [Range(0.05f, 0.6f)] public float torsoLength = 0.31f;
    [Tooltip("Diamètre de la tête, relatif à 'height'.")]
    [Range(0.02f, 0.4f)] public float headDiameter = 0.17f;
    [Tooltip("Longueur de l'humérus (épaule → coude), relative à 'height'.")]
    [Range(0.03f, 0.4f)] public float upperArmLength = 0.17f;
    [Tooltip("Longueur de l'avant-bras main comprise (coude → bout des doigts), relative à 'height'.")]
    [Range(0.03f, 0.4f)] public float forearmLength = 0.18f;
    [Tooltip("Distance entre la base du cou et les épaules, relative à 'height'.")]
    [Range(0f, 0.2f)] public float shoulderDrop = 0.04f;
    [Tooltip("Épaisseur (diamètre) des membres et du torse, en m (absolu). Fin = bonhomme bâton. Sert aussi de hitbox.")]
    [Min(0.005f)] public float limbThickness = 0.05f;
    [Tooltip("Hauteur de la cheville au-dessus du plateau, en m (absolu).")]
    [Min(0f)] public float footHeight = 0.03f;
    [Tooltip("Écart entre les pieds, relatif à l'empattement de la planche (1 = un pied au-dessus de chaque truck).")]
    [Range(0.2f, 1.6f)] public float stance = 1f;

    [Header("Masse")]
    [Tooltip("Masse du personnage en kg. Vivant, elle est portée par la planche (centre de gravité de la planche) : elle compte\n" +
             "contre les corps dynamiques (vitre, morceaux). En l'air, elle place le centre de rotation. Mort, elle est répartie entre\n" +
             "les membres du ragdoll comme un humain. Blob standard : 0,785 kg.")]
    [Min(0.01f)] public float mass = 1f;
    [Tooltip("Masse de la planche seule (plateau, trucks, roues) en kg.")]
    [Min(0.001f)] public float boardMass = 0.4f;

    [Header("Pose (visuelle et hitbox)")]
    [Tooltip("Vitesse de changement de pose (1/s) : 6 = une pose complète en ≈ 1/6 s.")]
    [Min(0.1f)] public float poseSpeed = 6f;
    [Tooltip("Accroupi au sol, sans flèche : extension de la jambe la plus tendue (1 = tendue, 0,7 = très fléchie).")]
    [Range(0.4f, 1f)] public float standCrouch = 0.97f;
    [Tooltip("Accroupi pendant une bascule (manual, frein) : la jambe du pied le plus haut presque droite.")]
    [Range(0.4f, 1f)] public float leanCrouch = 0.98f;
    [Tooltip("Accroupi en l'air (groupé pour tourner).")]
    [Range(0.4f, 1f)] public float airCrouch = 0.72f;
    [Tooltip("Déplacement des hanches pendant une bascule, relatif au demi-écart des pieds (1 = hanches au-dessus du pied).")]
    [Range(0f, 2f)] public float hipShift = 1.3f;
    [Tooltip("Pendant une bascule voulue (flèche au sol) : part de la bascule de la planche compensée par le corps (1 = il reste droit par rapport au sol, 0 = il bascule avec). Sans flèche, il suit la planche.")]
    [Range(0f, 1f)] public float counterTilt = 0.9f;
    [Tooltip("Compensation maximale, en degrés : au-delà, il bascule avec la planche (chute vers l'avant).")]
    [Range(0f, 90f)] public float maxCounterTilt = 45f;
    [Tooltip("Inclinaison du torse vers l'avant (sens du déplacement), en degrés, sans flèche.")]
    [Range(-30f, 60f)] public float torsoLean = 5f;
    [Tooltip("Inclinaison du torse vers l'avant ajoutée pendant une bascule arrière (manual, frein), en degrés.")]
    [Range(-30f, 60f)] public float torsoLeanBack = -5f;
    [Tooltip("Inclinaison du torse vers l'avant ajoutée pendant une bascule avant, en degrés.")]
    [Range(-30f, 60f)] public float torsoLeanForward = 25f;
    [Tooltip("Ouverture des bras au repos (degrés depuis la verticale, vers l'extérieur).")]
    [Range(-30f, 180f)] public float armRest = 12f;
    [Tooltip("Ouverture des bras pendant une bascule (équilibre), en degrés.")]
    [Range(-30f, 180f)] public float armBalance = 75f;
    [Tooltip("Ouverture des bras en l'air, en degrés.")]
    [Range(-30f, 180f)] public float armAir = 110f;
    [Tooltip("Flexion des coudes, en degrés.")]
    [Range(0f, 150f)] public float elbowBend = 15f;

    [Header("Poids")]
    [Tooltip("Le centre de gravité de la planche (qui porte la masse du personnage) suit horizontalement celui du personnage, multiplié par ce facteur : quand il recule ses hanches, le poids passe sur le tail (il frotte et freine), quand il avance, sur le nose. 0 = centre de gravité fixe au milieu de la planche.")]
    [Range(0f, 2f)] public float weightShift = 1f;

    [Header("Commandes")]
    [Tooltip("Touche de rotation anti-horaire (vers la gauche). Enregistrée par LevelInput comme les flippers.")]
    public FlipperKey rotateLeftKey = FlipperKey.LeftArrow;
    [Tooltip("Touche de rotation horaire (vers la droite).")]
    public FlipperKey rotateRightKey = FlipperKey.RightArrow;
    [Tooltip("En l'air : vitesse de rotation maximale de l'ensemble personnage + planche, en degrés par seconde.")]
    [Min(0f)] public float airSpinSpeed = 540f;
    [Tooltip("En l'air, touche appuyée : accélération de la rotation, en degrés/s².")]
    [Min(0f)] public float airSpinAccel = 5000f;
    [Tooltip("En l'air, touche relâchée : freinage de la rotation, en degrés/s² (0 = la rotation continue librement).")]
    [Min(0f)] public float airReleaseAccel = 1500f;
    [Tooltip("Au sol : vitesse de bascule maximale de la planche (flèche tenue), en degrés par seconde.")]
    [Min(0f)] public float groundTiltSpeed = 150f;
    [Tooltip("Au sol : accélération de la bascule, en degrés/s².")]
    [Min(0f)] public float groundTiltAccel = 3000f;
    [Tooltip("Au sol : réactivité de la bascule (1/s) : vitesse visée = écart à l'angle visé × ce gain.")]
    [Min(0f)] public float groundTiltGain = 8f;
    [Tooltip("Bascule arrière : angle visé au-delà du contact du tail avec le sol, en degrés. Plus = tail plus appuyé = frottement\n" +
             "plus fort (le frottement du plateau freine, rien d'autre).")]
    [Range(0f, 30f)] public float tailPress = 4f;
    [Tooltip("Bascule avant : angle visé, en degrés. Au-delà du contact du nose (≈ 30° avec la planche par défaut), il passe par-dessus et tombe.")]
    [Range(0f, 120f)] public float forwardTiltMax = 45f;
    [Tooltip("Bascule avant tenue avec le bout avant (nose, ou tail en fakie) au sol pendant cette durée (s) : il se casse la figure.")]
    [Min(0f)] public float noseFallTime = 0.3f;
    [Tooltip("Pas de simulation sans contact avant de passer aux commandes « en l'air » (petits sauts sur les bosses).")]
    [Range(0, 30)] public int airDelaySteps = 4;
    [Tooltip("Vitesse le long de la planche (km/h) au-dessus de laquelle le sens de marche est mis à jour : il décide quel bout\n" +
             "est l'arrière (frein) et lequel est l'avant.")]
    [SpeedKmh, Min(0f)] public float directionSpeed = 0.3f;
    [Tooltip("Vitesse au départ (km/h), pour tester. Le niveau, pas le skateur, doit ensuite donner la vitesse.")]
    [SpeedKmh] public Vector2 initialVelocity = Vector2.zero;
    [Tooltip("Mode « bullet » (fastCollisionsAllowed) de la planche : collision continue aussi contre les flippers (kinematic).")]
    public bool fastCollisions = false;

    [Header("Mort")]
    [Tooltip("La tête qui touche le décor = mort (le verre ne compte pas).")]
    public bool headIsDeadly = true;
    [Tooltip("Le torse qui touche le décor = mort (le verre ne compte pas).")]
    public bool torsoIsDeadly = true;
    [Tooltip("Planche sans contact avec le sol (roues ou dessous) et presque immobile pendant cette durée (s) = mort.\n" +
             "Évite de rester coincé vivant (planche bloquée sur la tranche). 0 = jamais.")]
    [Min(0f)] public float stuckTime = 1f;
    [Tooltip("Vitesse de la planche (km/h) en dessous de laquelle il est considéré immobile pour 'stuckTime'.")]
    [SpeedKmh, Min(0f)] public float stuckSpeed = 0.5f;
    [Tooltip("Mort : frottement des articulations du ragdoll (N·m, absolu). 0 = pantin totalement libre.")]
    [Min(0f)] public float deadJointFriction = 0.005f;

    [Header("Debug")]
    [Tooltip("Couleur du personnage dans le rendu et l'aperçu Scene view.")]
    public Color riderColor = Color.white;
    [Tooltip("Dessine les articulations du ragdoll (rendu debug de Box2D).")]
    public bool drawJoints = false;

    public static event System.Action<Skater, Vector2> Died;

    static readonly List<Skater> _active = new List<Skater>();
    public static IReadOnlyList<Skater> Active => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearActive() => _active.Clear();

    const int Board = 0, Torso = 1, Head = 2, ThighF = 3, ShinF = 4, ThighB = 5, ShinB = 6, UpperF = 7, ForeF = 8, UpperB = 9, ForeB = 10;
    public const int BodyCount = 11;
    const int Neck = 0, HipF = 1, KneeF = 2, HipB = 3, KneeB = 4, ShoulderF = 5, ElbowF = 6, ShoulderB = 7, ElbowB = 8;
    const int JointCount = 9;
    // Fractions de 'mass' par membre (index = corps, planche exclue).
    static readonly float[] MassShare = { 0f, 0.50f, 0.08f, 0.10f, 0.055f, 0.10f, 0.055f, 0.03f, 0.025f, 0.03f, 0.025f };
    // Catégorie des formes du skateur (planche, ragdoll) : la hitbox ne se détecte pas elle-même. Contacts inchangés.
    public const int CategoryBit = 26;
    const float TouchSlop = 0.01f;

    Skateboard _board;
    // [0] = planche, [1..10] = membres du ragdoll (créés à la mort seulement).
    NativeArray<PhysicsBody> _bodies;
    readonly PhysicsHingeJoint[] _joints = new PhysicsHingeJoint[JointCount];
    readonly int[] _hazardTouches = new int[BodyCount];
    readonly bool[] _attached = new bool[BodyCount];
    readonly List<PhysicsShape> _shapes = new List<PhysicsShape>();
    readonly List<int> _shapeBody = new List<int>();
    readonly List<int> _shapeSide = new List<int>();
    readonly List<bool> _shapeWheel = new List<bool>();
    PhysicsShape _tailShape, _noseShape;
    Vector2 _boardCom;
    float _comShift;
    bool _created;

    // Pose (repère de la planche) : origine et angle (radians) de chaque membre.
    readonly Vector2[] _posePos = new Vector2[BodyCount];
    readonly float[] _poseAngle = new float[BodyCount];
    Vector2 _poseCom;
    readonly float[] _poseIn = new float[6];
    bool _poseValid;
    PhysicsQuery.QueryFilter _sceneryFilter, _hazardFilter;
    readonly CapsuleGeometry[] _worldLimbs = new CapsuleGeometry[BodyCount];
    // Membres de la pose dans le repère de la planche (cercle de la tête : center1 = center2).
    readonly CapsuleGeometry[] _localLimbs = new CapsuleGeometry[BodyCount];

    int _lead;
    int _airSteps;
    int _stuckSteps;
    int _noseSteps;
    float _lean, _airBlend, _tilt;
    float _tailStop, _noseStop;
    float _groundAngle, _groundRate;
    bool _hadGround;
    bool _wasAir;
    Vector2 _pivotVelocity;

    public bool IsBroken { get; private set; }
    public bool Grounded => _airSteps == 0;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    float Len(float fraction) => fraction * height;
    float Spacing => stance * _board.wheelbase;
    float LimbRadius => 0.5f * limbThickness;
    float HeadRadius => 0.5f * Len(headDiameter);

    public Vector2 CameraPosition
    {
        get
        {
            if (!_created) return transform.position;
            if (IsBroken) return _bodies[Torso].position;
            return _bodies[Board].transform.TransformPoint(_posePos[Torso]);
        }
    }

    // ---------------------------------------------------------------- Mode entraînement, effets

    public CapsuleGeometry GhostShape => new CapsuleGeometry { radius = LimbRadius };

    // Fantôme en bonhomme bâton : chaque corps avec sa forme (planche = trait du plateau, tête = cercle, membres = capsules).
    public CapsuleGeometry GhostShapeOf(int index)
    {
        if (index == Board)
        {
            var b = _board != null ? _board : GetComponent<Skateboard>();
            float y = 0.5f * (b.DeckBottom + b.DeckTop), half = 0.5f * b.length;
            return new CapsuleGeometry { center1 = new Vector2(-half, y), center2 = new Vector2(half, y), radius = 0.5f * b.thickness };
        }
        return LimbGeometry(index);
    }

    // Forme d'un membre dans son repère (origine = articulation proche du torse).
    CapsuleGeometry LimbGeometry(int index)
    {
        if (index == Head)
            return new CapsuleGeometry { center1 = new Vector2(0f, HeadRadius), center2 = new Vector2(0f, HeadRadius), radius = HeadRadius };
        float l = SegmentLength(index);
        return new CapsuleGeometry { center1 = Vector2.zero, center2 = new Vector2(0f, index == Torso ? l : -l), radius = LimbRadius };
    }

    public void GetBodyStates(List<BodyState> states)
    {
        states.Clear();
        if (!_created) return;
        var board = _bodies[Board];
        var t = board.transform;
        Vector2 v = board.linearVelocity;
        float w = board.angularVelocity;
        states.Add(new BodyState { position = t.position, angle = t.rotation.radians, velocity = v, angularVelocity = w });
        for (int i = Torso; i < BodyCount; i++)
        {
            if (IsBroken)
            {
                var b = _bodies[i];
                var bt = b.transform;
                states.Add(new BodyState { position = bt.position, angle = bt.rotation.radians, velocity = b.linearVelocity, angularVelocity = b.angularVelocity });
                continue;
            }
            Vector2 p = t.TransformPoint(_posePos[i]);
            states.Add(new BodyState { position = p, angle = t.rotation.radians + _poseAngle[i], velocity = board.GetWorldPointVelocity(p), angularVelocity = w });
        }
    }

    // Replace le skateur dans un état mémorisé (mode entraînement, rejeu qui ne retombe plus au même endroit) : planche posée,
    // et ragdoll recréé aux positions mémorisées si 'broken' (sans événement). Déterministe, pas identique à un vrai passage.
    public void RestoreBodyStates(IReadOnlyList<BodyState> states, bool broken)
    {
        if (states.Count != BodyCount) return;
        int order = _active.IndexOf(this);
        DestroyPhysics();
        CreatePhysics();
        if (order >= 0) { _active.Remove(this); _active.Insert(order, this); }
        var board = _bodies[Board];
        board.transform = new PhysicsTransform(states[0].position, PhysicsRotate.FromRadians(states[0].angle));
        board.linearVelocity = states[0].velocity;
        board.angularVelocity = states[0].angularVelocity;
        if (!broken) return;
        for (int i = Torso; i < BodyCount; i++) _spawn[i] = states[i];
        Kill(default, false, true);
    }

    // Contacts pour les effets (étincelles) : la planche sans ses roues (tail qui frotte, plateau) ; mort, aussi le ragdoll.
    public void GetSurfaceContacts(List<SurfaceContact> result)
    {
        result.Clear();
        if (!_created) return;
        int count = IsBroken ? BodyCount : 1;
        for (int i = 0; i < count; i++)
        {
            var body = _bodies[i];
            Vector2 velocity = body.linearVelocity;
            var contacts = body.GetContacts(Allocator.Temp);
            for (int k = 0; k < contacts.Length; k++)
            {
                var manifold = contacts[k].manifold;
                if (manifold.pointCount == 0) continue;
                bool isA = contacts[k].shapeA.body == body;
                var mine = isA ? contacts[k].shapeA : contacts[k].shapeB;
                int index = ShapeIndexOf(mine);
                if (index >= 0 && _shapeWheel[index]) continue;
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

    // ---------------------------------------------------------------- Création

    void OnEnable()
    {
        if (Application.isPlaying) CreatePhysics();
    }

    void OnDisable()
    {
        DestroyPhysics();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void OnDestroy() => DestroyPhysics();

    void DestroyPhysics()
    {
        _active.Remove(this);
        if (!_created) return;
        for (int i = 0; i < BodyCount; i++)
            if (_bodies[i].isValid) _bodies[i].Destroy();
        _bodies.Dispose();
        _shapes.Clear();
        _shapeBody.Clear();
        _shapeSide.Clear();
        _shapeWheel.Clear();
        System.Array.Clear(_joints, 0, JointCount);
        _created = false;
    }

    void CreatePhysics()
    {
        if (_created) return;
        _board = GetComponent<Skateboard>();

        _lead = 1;
        _airSteps = 0;
        _stuckSteps = 0;
        _noseSteps = 0;
        _lean = 0f;
        _airBlend = 0f;
        _tilt = 0f;
        _groundRate = 0f;
        _hadGround = false;
        _wasAir = false;
        IsBroken = false;
        System.Array.Clear(_hazardTouches, 0, BodyCount);
        System.Array.Clear(_attached, 0, BodyCount);
        _tailStop = StopAngle(-1);
        _noseStop = StopAngle(1);
        _poseValid = false;
        NeutralPose();
        _sceneryFilter = PhysicsQuery.QueryFilter.defaultFilter;
        _sceneryFilter.hitCategories = new PhysicsMask { bitMask = ~(1UL << CategoryBit) };
        _hazardFilter = PhysicsQuery.QueryFilter.defaultFilter;
        _hazardFilter.hitCategories = new PhysicsMask { bitMask = 1UL << BlobHazard.CategoryBit };

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        bodyDef.position = transform.position;
        bodyDef.rotation = PhysicsRotate.FromRadians(transform.eulerAngles.z * Mathf.Deg2Rad);
        bodyDef.fastCollisionsAllowed = fastCollisions;
        bodyDef.linearVelocity = initialVelocity;
        _bodies = new NativeArray<PhysicsBody>(BodyCount, Allocator.Persistent);
        var board = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        _bodies[Board] = board;
        CreateBoardShapes(board);
        _boardCom = board.localCenterOfMass;
        _comShift = 0f;
        _created = true;
        _active.Add(this);
    }

    // Angle de bascule (radians) auquel le bout 'side' touche le sol, la roue de ce côté restant posée (la planche pivote
    // autour de l'axe de la roue) : hauteur du bout = r + dy·cos θ − dx·sin θ = 0.
    float StopAngle(int side)
    {
        Vector2 tip = _board.KickTip(side);
        float r = _board.WheelRadius;
        float dx = Mathf.Max(0.001f, Mathf.Abs(tip.x) - 0.5f * _board.wheelbase);
        float dy = tip.y - r;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Atan2(dy, dx) + Mathf.Asin(Mathf.Min(1f, r / length));
    }

    readonly List<Vector2[]> _polygons = new List<Vector2[]>();
    readonly List<Vector3> _circles = new List<Vector3>();

    PhysicsShapeDefinition SkaterShapeDefinition()
    {
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var filter = shapeDef.contactFilter;
        filter.groupIndex = -2; // négatif : la planche et les membres d'un même skateur ne collisionnent pas entre eux
        filter.categories = new PhysicsMask { bitMask = filter.categories.bitMask | (1UL << CategoryBit) };
        shapeDef.contactFilter = filter;
        return shapeDef;
    }

    // Planche : toute la masse vivante (planche + personnage), centre de gravité au niveau de la planche.
    void CreateBoardShapes(PhysicsBody board)
    {
        var shapeDef = SkaterShapeDefinition();
        _board.BuildGeometry(_polygons, _circles);
        float area = 0f;
        foreach (var p in _polygons) area += PolygonArea(p);
        foreach (var c in _circles) area += Mathf.PI * c.z * c.z;
        shapeDef.density = (boardMass + mass) / Mathf.Max(1e-6f, area);

        int tailIndex = _board.KickIndex(-1), noseIndex = _board.KickIndex(1);
        int polygonCount = _polygons.Count;
        SetMaterial(ref shapeDef, _board.deckFriction, _board.bounciness, _board.color);
        for (int i = 0; i < polygonCount; i++)
        {
            // Côté de chaque forme : relevés, puis trucks arrière / avant (ordre de BuildGeometry).
            int side = i == tailIndex || i == polygonCount - 2 ? -1 : i == noseIndex || i == polygonCount - 1 ? 1 : 0;
            AddShape(board.CreateShape(PolygonGeometry.Create(_polygons[i], 0f), shapeDef), Board, side, false);
        }
        _tailShape = tailIndex >= 0 ? _shapes[tailIndex] : default;
        _noseShape = noseIndex >= 0 ? _shapes[noseIndex] : default;
        SetMaterial(ref shapeDef, _board.wheelFriction, _board.bounciness, _board.color);
        for (int i = 0; i < _circles.Count; i++)
        {
            var c = _circles[i];
            AddShape(board.CreateShape(new CircleGeometry { center = new Vector2(c.x, c.y), radius = c.z }, shapeDef), Board, i == 0 ? -1 : 1, true);
        }
    }

    void AddShape(PhysicsShape shape, int body, int side, bool wheel)
    {
        _shapes.Add(shape);
        _shapeBody.Add(body);
        _shapeSide.Add(side);
        _shapeWheel.Add(wheel);
    }

    static void SetMaterial(ref PhysicsShapeDefinition def, float friction, float bounciness, Color color)
    {
        var material = def.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        material.customColor = color;
        def.surfaceMaterial = material;
    }

    static float PolygonArea(Vector2[] p)
    {
        float a = 0f;
        for (int i = 0; i < p.Length; i++)
        {
            var u = p[i];
            var v = p[(i + 1) % p.Length];
            a += u.x * v.y - v.x * u.y;
        }
        return Mathf.Abs(0.5f * a);
    }

    float SegmentLength(int body)
    {
        switch (body)
        {
            case Torso: return Len(torsoLength);
            case ThighF: case ThighB: return Len(thighLength);
            case ShinF: case ShinB: return Len(shinLength);
            case UpperF: case UpperB: return Len(upperArmLength);
            case ForeF: case ForeB: return Len(forearmLength);
        }
        return 0f;
    }

    int ShapeIndexOf(PhysicsShape shape)
    {
        for (int i = 0; i < _shapes.Count; i++)
            if (_shapes[i] == shape) return i;
        return -1;
    }

    // ---------------------------------------------------------------- Simulation

    public void LevelTick()
    {
        if (!_created) return;
        if (LevelSimulation.Step > 0) ReadEvents();
        if (IsBroken)
        {
            ImpaleTouching();
            return;
        }
        Control();
        if (!IsBroken) CheckHitbox();
    }

    // Événements de contact du dernier pas : obstacles touchés (comptés par corps, pour l'empalement du ragdoll) et mort si la
    // planche touche un obstacle. Indépendant de l'ordre des événements.
    void ReadEvents()
    {
        var world = PhysicsWorld.defaultWorld;
        bool boardHit = false;
        foreach (var e in world.contactBeginEvents)
        {
            int i = ShapeIndex(e.shapeA, e.shapeB, out var other);
            // Forme détruite depuis (vitre brisée pendant ce pas) : un destructible, jamais un obstacle.
            if (i < 0 || !other.isValid || !(other.userData.objectValue is BlobHazard)) continue;
            int body = _shapeBody[i];
            _hazardTouches[body]++;
            if (body == Board) boardHit = true;
        }
        foreach (var e in world.contactEndEvents)
        {
            int i = ShapeIndex(e.shapeA, e.shapeB, out var other);
            if (i < 0 || !other.isValid || !(other.userData.objectValue is BlobHazard)) continue;
            int body = _shapeBody[i];
            _hazardTouches[body] = Mathf.Max(0, _hazardTouches[body] - 1);
        }
        if (boardHit && !IsBroken) Kill(_bodies[Board].position, true, false);
    }

    int ShapeIndex(PhysicsShape a, PhysicsShape b, out PhysicsShape other)
    {
        for (int i = 0; i < _shapes.Count; i++)
        {
            if (_shapes[i] == a) { other = b; return i; }
            if (_shapes[i] == b) { other = a; return i; }
        }
        other = default;
        return -1;
    }

    void Control()
    {
        float dt = Time.fixedDeltaTime;
        var board = _bodies[Board];
        var xf = board.transform;
        float bc = xf.rotation.cos, bs = xf.rotation.sin;
        var axis = new Vector2(bc, bs);
        var up = new Vector2(-bs, bc);

        bool touching = GroundContact(board, up, out var normal, out float measured, out int endTouch);
        _airSteps = touching ? 0 : _airSteps + 1;
        bool air = _airSteps > airDelaySteps;

        Vector2 velocity = board.linearVelocity;
        _stuckSteps = touching || velocity.sqrMagnitude > stuckSpeed * stuckSpeed ? 0 : _stuckSteps + 1;
        if (stuckTime > 0f && _stuckSteps * dt >= stuckTime)
        {
            Kill(CameraPosition, true, false);
            return;
        }

        if (touching)
        {
            float along = Vector2.Dot(velocity, axis);
            if (along > directionSpeed) _lead = 1;
            else if (along < -directionSpeed) _lead = -1;
        }

        int spin = (LevelInput.IsDown(rotateLeftKey) ? 1 : 0) - (LevelInput.IsDown(rotateRightKey) ? 1 : 0);
        int intent = air ? 0 : spin * _lead; // +1 = bascule arrière (frein), -1 = avant
        float omega = board.angularVelocity * Mathf.Deg2Rad;
        _noseSteps = intent < 0 && endTouch == _lead ? _noseSteps + 1 : 0;
        if (_noseSteps * dt >= noseFallTime && noseFallTime > 0f)
        {
            Kill(CameraPosition, true, false);
            return;
        }

        if (touching)
        {
            // Rotation du sol sous la planche (courbe) : la bascule se mesure par rapport à lui.
            float groundAngle = Mathf.Atan2(normal.y, normal.x);
            float raw = _hadGround ? Mathf.DeltaAngle(_groundAngle * Mathf.Rad2Deg, groundAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt : 0f;
            _groundRate = _hadGround ? Mathf.Lerp(_groundRate, raw, 0.3f) : 0f;
            _groundAngle = groundAngle;
            _hadGround = true;
            if (intent != 0) GroundTilt(board, normal, measured, intent, endTouch, omega - _groundRate, dt);
        }
        else _hadGround = false;

        UpdatePose(intent, air, touching ? measured : 0f, dt);
        ShiftWeight(board, touching ? measured : 0f);
        if (air) AirSpin(board, xf, spin, omega, dt);
        _wasAir = air;
    }

    // Planche au contact d'une surface sous elle (point touchant, normale vers le haut de la planche). Lecture seule.
    // Par la forme touchée, pas par la position (les points de contact datent du début du pas, décalés de v × dt).
    // 'measured' : bascule de la planche par rapport au sol (radians, anti-horaire) quand un seul côté touche, 0 sinon.
    // Normale : celle de la forme de plus petit index qui touche (indépendant de l'ordre des contacts de Box2D).
    // 'endTouch' : -1 si le relevé arrière (tail) touche, +1 si c'est le nose, 0 sinon (les deux ou aucun).
    bool GroundContact(PhysicsBody board, Vector2 up, out Vector2 normal, out float measured, out int endTouch)
    {
        int sides = 0, firstShape = int.MaxValue;
        bool tail = false, nose = false;
        normal = up;
        var contacts = board.GetContacts(Allocator.Temp);
        for (int k = 0; k < contacts.Length; k++)
        {
            var m = contacts[k].manifold;
            if (m.pointCount == 0) continue;
            bool isA = contacts[k].shapeA.body == board;
            Vector2 n = isA ? -m.normal : m.normal;
            if (Vector2.Dot(n, up) < 0.3f) continue;
            bool touch = false;
            for (int p = 0; p < m.pointCount; p++)
                if (m[p].separation <= TouchSlop) touch = true;
            if (!touch) continue;
            var mine = isA ? contacts[k].shapeA : contacts[k].shapeB;
            if (mine == _tailShape) tail = true;
            else if (mine == _noseShape) nose = true;
            int index = ShapeIndexOf(mine);
            int side = index >= 0 ? _shapeSide[index] : 0;
            sides |= side < 0 ? 1 : side > 0 ? 2 : 3;
            if (index >= 0 && index < firstShape) { firstShape = index; normal = n; }
        }
        contacts.Dispose();
        endTouch = tail == nose ? 0 : tail ? -1 : 1;
        measured = sides == 1 || sides == 2 ? Mathf.Atan2(normal.x * up.y - normal.y * up.x, Vector2.Dot(normal, up)) : 0f;
        return sides != 0;
    }

    // Flèche tenue au sol : la planche pivote vers l'angle visé sur la roue qui reste au sol (celle du côté déjà posé, sinon du
    // côté qui descend) : rotation + montée ou descente de son centre de gravité selon la normale du sol, jamais de vitesse le long du
    // sol (aucune propulsion). 'rate' : vitesse de bascule par rapport au sol (rad/s, anti-horaire).
    // Bout déjà au sol du côté qui descend : rotation pure, elle presse ce bout contre le sol (frottement = frein).
    void GroundTilt(PhysicsBody board, Vector2 normal, float measured, int intent, int endTouch, float rate, float dt)
    {
        float stop = _lead > 0 ? _tailStop : _noseStop;
        float goal = (intent > 0 ? stop + tailPress * Mathf.Deg2Rad : -forwardTiltMax * Mathf.Deg2Rad) * _lead;
        float maxRate = groundTiltSpeed * Mathf.Deg2Rad;
        float desired = Mathf.Clamp((goal - measured) * groundTiltGain, -maxRate, maxRate);
        float maxDelta = groundTiltAccel * Mathf.Deg2Rad * dt;
        float dw = Mathf.Clamp(desired - rate, -maxDelta, maxDelta);
        if (dw == 0f) return;
        int pivotSide = Mathf.Abs(measured) > Mathf.Deg2Rad ? (measured > 0f ? -1 : 1) : (dw > 0f ? -1 : 1);
        float lever = _boardCom.x - pivotSide * 0.5f * _board.wheelbase;
        board.angularVelocity += dw * Mathf.Rad2Deg;
        if (endTouch != pivotSide) board.linearVelocity += dw * lever * normal;
    }

    // En l'air : rotation autour du centre de gravité commun personnage + planche. Ce point suit une trajectoire balistique
    // (vitesse mémorisée au décollage + gravité) ; la planche tourne autour de lui.
    void AirSpin(PhysicsBody board, PhysicsTransform xf, int spin, float omega, float dt)
    {
        Vector2 pivot = xf.TransformPoint(_poseCom);
        Vector2 com = xf.TransformPoint(_boardCom);
        Vector2 r = com - pivot;
        if (!_wasAir) _pivotVelocity = board.linearVelocity + omega * new Vector2(r.y, -r.x);
        else _pivotVelocity += PhysicsWorld.defaultWorld.gravity * dt;

        float accel = (spin != 0 ? airSpinAccel : airReleaseAccel) * Mathf.Deg2Rad;
        float target = spin * airSpinSpeed * Mathf.Deg2Rad;
        if (accel > 0f) omega += Mathf.Clamp(target - omega, -accel * dt, accel * dt);
        board.angularVelocity = omega * Mathf.Rad2Deg;
        board.linearVelocity = _pivotVelocity + omega * new Vector2(-r.y, r.x);
    }

    // Poids du personnage porté par la planche : son centre de gravité (resté bas) se place sous celui du personnage, le long de
    // la normale du sol ('measured' : bascule de la planche par rapport au sol) : buste droit sur une planche basculée = poids
    // sur le bout qui touche.
    void ShiftWeight(PhysicsBody board, float measured)
    {
        Vector2 com = _poseCom;
        float x = com.x - (com.y - _boardCom.y) * Mathf.Tan(Mathf.Clamp(measured, -1f, 1f));
        float shift = weightShift * (x - _boardCom.x);
        if (Mathf.Abs(shift - _comShift) < 1e-4f) return;
        _comShift = shift;
        var mc = board.massConfiguration;
        mc.center = new Vector2(_boardCom.x + shift, _boardCom.y);
        board.massConfiguration = mc;
    }

    // Centre de gravité personnage + planche dans le repère de la planche (pose actuelle).
    Vector2 PoseCenterOfMass()
    {
        Vector2 sum = boardMass * _boardCom;
        for (int i = Torso; i < BodyCount; i++)
            sum += MassShare[i] * mass * 0.5f * (_localLimbs[i].center1 + _localLimbs[i].center2);
        return sum / (boardMass + mass);
    }

    void UpdatePose(int intent, bool air, float measured, float dt)
    {
        float step = poseSpeed * dt;
        _lean = Mathf.MoveTowards(_lean, air ? 0f : intent, step);
        _airBlend = Mathf.MoveTowards(_airBlend, air ? 1f : 0f, step);
        // Bascule voulue : corps droit par rapport au sol (jusqu'à une limite : au-delà, il bascule avec la planche). Sans flèche,
        // il suit la planche : son poids revient de lui-même vers le milieu quand elle se cabre seule.
        float lean = Mathf.Abs(_lean);
        float maxComp = maxCounterTilt * Mathf.Deg2Rad;
        float comp = air ? 0f : Mathf.Clamp(measured * counterTilt * lean, -maxComp, maxComp);
        _tilt = Mathf.MoveTowards(_tilt, comp, 2f * groundTiltSpeed * Mathf.Deg2Rad * dt);

        float crouch = Mathf.Lerp(Mathf.Lerp(standCrouch, leanCrouch, lean), airCrouch, _airBlend);
        float arms = Mathf.Lerp(Mathf.Lerp(armRest, armBalance, lean), armAir, _airBlend);
        float torso = torsoLean + (_lean > 0f ? _lean * torsoLeanBack : -_lean * torsoLeanForward);
        float hipX = -_lead * _lean * hipShift * 0.5f * Spacing;
        SolvePose(hipX, crouch, -_lead * torso * Mathf.Deg2Rad - _tilt, _tilt, arms, elbowBend);
        _poseCom = PoseCenterOfMass();
    }

    void NeutralPose() => SolvePose(0f, standCrouch, -torsoLean * Mathf.Deg2Rad, 0f, armRest, elbowBend);

    // Pose dans le repère de la planche. 'hipGroundX' : position des hanches le long du sol (repère tourné de -tiltComp),
    // 'crouch' : extension de la jambe du pied le plus haut, 'torsoAngle' (radians, depuis la normale de la planche),
    // bras et coudes en degrés. Remplit _posePos / _poseAngle.
    void SolvePose(float hipGroundX, float crouch, float torsoAngle, float tiltComp, float armDeg, float elbowDeg)
    {
        // Mêmes entrées que la dernière fois (roulage sans flèche) : pose inchangée, rien à recalculer.
        if (_poseValid && hipGroundX == _poseIn[0] && crouch == _poseIn[1] && torsoAngle == _poseIn[2] && tiltComp == _poseIn[3] &&
            armDeg == _poseIn[4] && elbowDeg == _poseIn[5]) return;
        _poseValid = true;
        _poseIn[0] = hipGroundX; _poseIn[1] = crouch; _poseIn[2] = torsoAngle; _poseIn[3] = tiltComp; _poseIn[4] = armDeg; _poseIn[5] = elbowDeg;
        float lt = Len(thighLength), ls = Len(shinLength), lto = Len(torsoLength);
        float spacing = Spacing;
        Vector2 ankleF = _board.FootPoint(1, spacing, footHeight);
        Vector2 ankleB = _board.FootPoint(-1, spacing, footHeight);

        float gc = Mathf.Cos(tiltComp), gs = Mathf.Sin(tiltComp);
        var groundUp = new Vector2(gs, gc);
        var p0 = new Vector2(gc * hipGroundX, -gs * hipGroundX);
        // La jambe du pied le plus haut (planche basculée) est tendue à 'crouch', l'autre se plie (portée maximale au plus).
        float reach = crouch * (lt + ls), full = 0.995f * (lt + ls);
        bool frontHigh = Vector2.Dot(ankleF - ankleB, groundUp) >= 0f;
        float tF = HipHeight(p0, groundUp, ankleF, frontHigh ? reach : full);
        float tB = HipHeight(p0, groundUp, ankleB, frontHigh ? full : reach);
        Vector2 hip = p0 + Mathf.Min(tF, tB) * groundUp;

        Leg(hip, ankleF, 1, lt, ls, out var kneeF, out float thighF, out float shinF);
        Leg(hip, ankleB, -1, lt, ls, out var kneeB, out float thighB, out float shinB);

        var torsoUp = new Vector2(-Mathf.Sin(torsoAngle), Mathf.Cos(torsoAngle));
        Vector2 neck = hip + lto * torsoUp;
        Vector2 shoulder = hip + (lto - Len(shoulderDrop)) * torsoUp;
        float arm = armDeg * Mathf.Deg2Rad, elbow = elbowDeg * Mathf.Deg2Rad;
        float upperF = torsoAngle + arm, upperB = torsoAngle - arm;

        _posePos[Board] = Vector2.zero; _poseAngle[Board] = 0f;
        _posePos[Torso] = hip; _poseAngle[Torso] = torsoAngle;
        _posePos[Head] = neck; _poseAngle[Head] = torsoAngle;
        _posePos[ThighF] = hip; _poseAngle[ThighF] = thighF;
        _posePos[ShinF] = kneeF; _poseAngle[ShinF] = shinF;
        _posePos[ThighB] = hip; _poseAngle[ThighB] = thighB;
        _posePos[ShinB] = kneeB; _poseAngle[ShinB] = shinB;
        _posePos[UpperF] = shoulder; _poseAngle[UpperF] = upperF;
        _posePos[ForeF] = shoulder + Len(upperArmLength) * Down(upperF); _poseAngle[ForeF] = upperF + elbow;
        _posePos[UpperB] = shoulder; _poseAngle[UpperB] = upperB;
        _posePos[ForeB] = shoulder + Len(upperArmLength) * Down(upperB); _poseAngle[ForeB] = upperB - elbow;

        for (int i = Torso; i < BodyCount; i++)
        {
            var g = LimbGeometry(i);
            float a = _poseAngle[i], c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 p = _posePos[i];
            g.center1 = p + new Vector2(c * g.center1.x - s * g.center1.y, s * g.center1.x + c * g.center1.y);
            g.center2 = p + new Vector2(c * g.center2.x - s * g.center2.y, s * g.center2.x + c * g.center2.y);
            _localLimbs[i] = g;
        }
    }

    // Hauteur le long de 'w' (depuis p0) où la hanche est à 'reach' de la cheville 'a' (au plus près si hors de portée).
    static float HipHeight(Vector2 p0, Vector2 w, Vector2 a, float reach)
    {
        Vector2 d = a - p0;
        float along = Vector2.Dot(d, w);
        float perp2 = d.sqrMagnitude - along * along;
        return along + Mathf.Sqrt(Mathf.Max(0f, reach * reach - perp2));
    }

    // Cinématique inverse à deux os : genou du côté 'side' (+1 vers le nose, -1 vers le tail). Angles des membres (radians),
    // convention « segment vers le bas » : angle 0 = segment vertical vers le bas.
    static void Leg(Vector2 hip, Vector2 ankle, int side, float lt, float ls, out Vector2 knee, out float thigh, out float shin)
    {
        Vector2 d = ankle - hip;
        float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(lt - ls) + 1e-4f, lt + ls - 1e-4f);
        float cosA = (lt * lt + dist * dist - ls * ls) / (2f * lt * dist);
        float alpha = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));
        thigh = Mathf.Atan2(d.x, -d.y) + side * alpha;
        knee = hip + lt * Down(thigh);
        Vector2 s = ankle - knee;
        shin = Mathf.Atan2(s.x, -s.y);
    }

    static Vector2 Down(float angle) => new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));

    // ---------------------------------------------------------------- Hitbox et mort

    // Membre 'i' de la pose actuelle en coordonnées monde (les requêtes de Box2D prennent la géométrie en monde).
    CapsuleGeometry WorldLimb(Vector2 origin, float cos, float sin, int i)
    {
        var g = _localLimbs[i];
        Vector2 a = g.center1, b = g.center2;
        g.center1 = origin + new Vector2(cos * a.x - sin * a.y, sin * a.x + cos * a.y);
        g.center2 = origin + new Vector2(cos * b.x - sin * b.y, sin * b.x + cos * b.y);
        return g;
    }

    CapsuleGeometry WorldLimb(PhysicsTransform xf, int i) => WorldLimb(xf.position, xf.rotation.cos, xf.rotation.sin, i);

    // Tête et torse contre le décor (hors skateur et hors verre) ; tous les membres contre les obstacles, après un test de
    // boîte englobante bon marché. Seul le « touché ou non » compte : indépendant de l'ordre des résultats.
    void CheckHitbox()
    {
        var world = PhysicsWorld.defaultWorld;
        var xf = _bodies[Board].transform;
        Vector2 origin = xf.position;
        float cos = xf.rotation.cos, sin = xf.rotation.sin;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        for (int i = Torso; i < BodyCount; i++)
        {
            var g = WorldLimb(origin, cos, sin, i);
            _worldLimbs[i] = g;
            min = Vector2.Min(min, Vector2.Min(g.center1, g.center2) - new Vector2(g.radius, g.radius));
            max = Vector2.Max(max, Vector2.Max(g.center1, g.center2) + new Vector2(g.radius, g.radius));
        }

        if ((headIsDeadly && TouchesScenery(world, _worldLimbs[Head], true, _sceneryFilter)) ||
            (torsoIsDeadly && TouchesScenery(world, _worldLimbs[Torso], false, _sceneryFilter)))
        {
            Kill(CameraPosition, true, false);
            return;
        }

        var hazards = _hazardFilter;
        if (!world.TestOverlapAABB(new PhysicsAABB { lowerBound = min, upperBound = max }, hazards)) return;
        for (int i = Torso; i < BodyCount; i++)
        {
            var g = _worldLimbs[i];
            bool hit = i == Head ? world.TestOverlapGeometry(new CircleGeometry { center = g.center1, radius = g.radius }, hazards)
                                 : world.TestOverlapGeometry(g, hazards);
            if (!hit) continue;
            Kill(CameraPosition, true, false);
            return;
        }
    }

    static bool TouchesScenery(PhysicsWorld world, CapsuleGeometry g, bool circle, PhysicsQuery.QueryFilter filter)
    {
        var c = new CircleGeometry { center = g.center1, radius = g.radius };
        if (circle ? !world.TestOverlapGeometry(c, filter) : !world.TestOverlapGeometry(g, filter)) return false;
        var results = circle ? world.OverlapGeometry(c, filter, Allocator.Temp)
                             : world.OverlapGeometry(g, filter, Allocator.Temp);
        bool deadly = false;
        // Le verre n'est jamais mortel (la planche le brise, le corps le traverse).
        for (int k = 0; k < results.Length && !deadly; k++)
            if (results[k].shape.isValid && !(results[k].shape.userData.objectValue is GlassPane)) deadly = true;
        results.Dispose();
        return deadly;
    }

    // États de départ des membres du ragdoll (monde) : pose actuelle, ou état mémorisé (mode entraînement).
    readonly BodyState[] _spawn = new BodyState[BodyCount];

    // Mort : le personnage devient un ragdoll (membres dynamiques reliés par des charnières libres) qui part avec la vitesse
    // de la planche en chaque point ; la planche garde sa vitesse et ne porte plus que sa propre masse.
    void Kill(Vector2 point, bool notify, bool fromStates)
    {
        IsBroken = true;
        var board = _bodies[Board];
        var xf = board.transform;
        if (!fromStates)
            for (int i = Torso; i < BodyCount; i++)
            {
                Vector2 p = xf.TransformPoint(_posePos[i]);
                _spawn[i] = new BodyState { position = p, angle = xf.rotation.radians + _poseAngle[i], velocity = board.GetWorldPointVelocity(p), angularVelocity = board.angularVelocity };
            }

        var mc = board.massConfiguration;
        float share = boardMass / (boardMass + mass);
        mc.mass *= share;
        mc.rotationalInertia *= share;
        board.massConfiguration = mc;

        CreateRagdoll();
        if (notify) Died?.Invoke(this, point);
    }

    void CreateRagdoll()
    {
        var world = PhysicsWorld.defaultWorld;
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        var shapeDef = SkaterShapeDefinition();
        SetMaterial(ref shapeDef, 0.6f, 0f, riderColor);
        for (int i = Torso; i < BodyCount; i++)
        {
            bodyDef.position = _spawn[i].position;
            bodyDef.rotation = PhysicsRotate.FromRadians(_spawn[i].angle);
            bodyDef.linearVelocity = _spawn[i].velocity;
            bodyDef.angularVelocity = _spawn[i].angularVelocity;
            var body = world.CreateBody(bodyDef);
            _bodies[i] = body;
            var g = LimbGeometry(i);
            float area = i == Head ? Mathf.PI * g.radius * g.radius : Mathf.PI * g.radius * g.radius + 2f * g.radius * SegmentLength(i);
            shapeDef.density = MassShare[i] * mass / area;
            var shape = i == Head ? body.CreateShape(new CircleGeometry { center = g.center1, radius = g.radius }, shapeDef)
                                  : body.CreateShape(g, shapeDef);
            AddShape(shape, i, 0, false);
        }

        float lt = Len(torsoLength);
        Vector2 shoulder = new Vector2(0f, lt - Len(shoulderDrop));
        _joints[Neck] = Hinge(world, Torso, new Vector2(0f, lt), Head, -40f, 40f, true);
        _joints[HipF] = Hinge(world, Torso, Vector2.zero, ThighF, -110f, 110f, true);
        _joints[KneeF] = Hinge(world, ThighF, new Vector2(0f, -Len(thighLength)), ShinF, -165f, 3f, true);
        _joints[HipB] = Hinge(world, Torso, Vector2.zero, ThighB, -110f, 110f, true);
        _joints[KneeB] = Hinge(world, ThighB, new Vector2(0f, -Len(thighLength)), ShinB, -3f, 165f, true);
        _joints[ShoulderF] = Hinge(world, Torso, shoulder, UpperF, 0f, 0f, false);
        _joints[ElbowF] = Hinge(world, UpperF, new Vector2(0f, -Len(upperArmLength)), ForeF, -3f, 160f, true);
        _joints[ShoulderB] = Hinge(world, Torso, shoulder, UpperB, 0f, 0f, false);
        _joints[ElbowB] = Hinge(world, UpperB, new Vector2(0f, -Len(upperArmLength)), ForeB, -160f, 3f, true);
    }

    PhysicsHingeJoint Hinge(PhysicsWorld world, int a, Vector2 anchorA, int b, float lower, float upper, bool limit)
    {
        var jd = PhysicsHingeJointDefinition.defaultDefinition;
        jd.bodyA = _bodies[a];
        jd.bodyB = _bodies[b];
        jd.localAnchorA = new PhysicsTransform(anchorA, PhysicsRotate.identity);
        jd.localAnchorB = new PhysicsTransform(Vector2.zero, PhysicsRotate.identity);
        jd.enableLimit = limit;
        jd.lowerAngleLimit = lower;
        jd.upperAngleLimit = upper;
        jd.enableMotor = deadJointFriction > 0f;
        jd.motorSpeed = 0f;
        jd.maxMotorTorque = deadJointFriction;
        jd.worldDrawing = drawJoints;
        return world.CreateJoint(jd);
    }

    struct HazardContact
    {
        public BlobHazard hazard;
        public PhysicsShape shape;
        public Vector2 point;
    }

    HazardContact[] _hazardContacts = new HazardContact[8];

    // Après la mort, chaque membre qui touche un obstacle le lui signale (piques : empalement), ordre fixe comme le BLOB.
    void ImpaleTouching()
    {
        for (int i = Torso; i < BodyCount; i++)
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
            for (int k = 0; k < count && !_attached[i]; k++)
                if (_hazardContacts[k].hazard.OnCapsuleTouched(_bodies[i], _hazardContacts[k].shape, _hazardContacts[k].point)) _attached[i] = true;
            System.Array.Clear(_hazardContacts, 0, count);
        }
    }

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

    // ---------------------------------------------------------------- Rendu

    // Personnage vivant : dessiné sur la planche à chaque image (rendu debug, rien dans la simulation). Mort, le ragdoll est
    // dessiné par le rendu debug de Box2D comme les autres corps.
    void LateUpdate()
    {
        if (!Application.isPlaying || !_created || IsBroken) return;
        var world = PhysicsWorld.defaultWorld;
        var xf = _bodies[Board].transform;
        const PhysicsWorld.DrawFillOptions fill = PhysicsWorld.DrawFillOptions.Interior | PhysicsWorld.DrawFillOptions.Outline;
        for (int i = Torso; i < BodyCount; i++)
        {
            var g = WorldLimb(xf, i);
            if (i == Head) world.DrawGeometry(new CircleGeometry { center = g.center1, radius = g.radius }, PhysicsTransform.identity, riderColor, 0f, fill);
            else world.DrawGeometry(g, PhysicsTransform.identity, riderColor, 0f, fill);
        }
    }

    // ---------------------------------------------------------------- Aperçu

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        _board = GetComponent<Skateboard>();
        if (_board == null) return;
        _poseValid = false;
        NeutralPose();
        var m = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);
        Gizmos.matrix = m;
        Gizmos.color = riderColor;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
#endif
        var identity = PhysicsTransform.identity;
        for (int i = Torso; i < BodyCount; i++)
        {
            var g = WorldLimb(identity, i);
            if (i == Head)
            {
                SkaterGizmos.Circle(g.center1, g.radius);
#if UNITY_EDITOR
                _pickArea.AddCircle(g.center1, g.radius);
#endif
                continue;
            }
            SkaterGizmos.Capsule(g.center1, g.center2, g.radius);
#if UNITY_EDITOR
            var n = new Vector2(-(g.center2 - g.center1).y, (g.center2 - g.center1).x).normalized * g.radius;
            _pickArea.AddConvex(new[] { g.center1 - n, g.center2 - n, g.center2 + n, g.center1 + n });
#endif
        }
        Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
        _pickArea.Draw(m);
#endif
    }
}
