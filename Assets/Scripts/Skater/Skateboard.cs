using System.Collections.Generic;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

// Planche de skate : géométrie, physique et commandes. C'est elle seule qui fait le MOUVEMENT (un corps Box2D).
// Le personnage (Skater, optionnel, sur le même objet) ne pousse jamais la planche : il la lit pour sa posture, sa hitbox et
// sa chute. Seule influence, en l'air : la planche tourne autour du centre de gravité commun de la posture du personnage, à une
// vitesse qui dépend de son inertie (options du Skater).
// Repère local : origine au sol, au milieu des deux roues (planche à plat), +X = nose, +Y = vers le haut.
[ExecuteAlways]
[DisallowMultipleComponent]
public class Skateboard : MonoBehaviour, ICameraTarget, ILevelPhysics, ILevelTick, ITrainingBody, IContactFxSource
{
    [Header("Planche")]
    [Tooltip("Échelle de la planche, RELATIVE : multiplie toutes ses longueurs (longueur, épaisseur, relevés, roues, empattement,\n" +
             "trucks), proportions et angles gardés. 1 = planche de référence (longueurs ci-dessous telles quelles).\n" +
             "Les masses ne changent pas (réglées à part). Le personnage garde sa taille ; ses pieds suivent l'empattement.\n" +
             "Épaisseur gardée ≥ 2,1 cm (Box2D efface les formes plus fines) : sous ≈ 0,5, le plateau n'est plus à l'échelle.")]
    [Min(0.1f)] public float scale = 1f;
    [Tooltip("Longueur totale de la planche, d'un bout à l'autre le long du plateau (relevés compris), en m à l'échelle 1 (multipliée par 'scale').")]
    [Min(0.1f)] public float length = 1.5f;
    [Tooltip("Épaisseur du plateau, en m à l'échelle 1 (multipliée par 'scale').")]
    [Min(0.005f)] public float thickness = 0.04f;

    [Header("Relevés (kicks)")]
    [Tooltip("Relevé à l'avant (nose, côté +X local).")]
    public bool noseKick = true;
    [Tooltip("Relevé à l'arrière (tail, côté -X local). C'est lui qui frotte au sol pour freiner.")]
    public bool tailKick = true;
    [Tooltip("Angle des relevés par rapport au plateau, en degrés.")]
    [Range(0f, 60f)] public float kickAngle = 18f;
    [Tooltip("Longueur de chaque relevé le long du plateau, en m à l'échelle 1 (multipliée par 'scale'), comprise dans 'length'.")]
    [Min(0f)] public float kickLength = 0.18f;

    [Header("Roues et trucks")]
    [Tooltip("Diamètre des roues, en m à l'échelle 1 (multiplié par 'scale'). Plus grand = passe mieux les bosses et les jointures de courbe.")]
    [Min(0.01f)] public float wheelDiameter = 0.0975f;
    [Tooltip("Empattement : distance entre les axes des roues avant et arrière, en m à l'échelle 1 (multiplié par 'scale').")]
    [Min(0.02f)] public float wheelbase = 0.75f;
    [Tooltip("Hauteur des trucks : distance entre l'axe des roues et le dessous du plateau, en m à l'échelle 1 (multipliée par 'scale').")]
    [Min(0f)] public float truckHeight = 0.096f;

    [Header("Matériau")]
    [Tooltip("Friction des roues. Les roues sont soudées au plateau : elles glissent au lieu de rouler (Box2D plafonne la rotation\n" +
             "d'un corps à 45° par pas : une vraie roue de 10 cm ne dépasserait pas ≈ 17 km/h). 0 = roulement parfait ;\n" +
             "plus = freinage par frottement. Leur rotation n'est que dessinée.")]
    [Min(0f)] public float wheelFriction = 0f;
    [Tooltip("Friction du plateau, des relevés et des trucks : freinage quand le tail frotte au sol.")]
    [Min(0f)] public float deckFriction = 0.5f;
    [Tooltip("Rebond de toute la planche : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0f)] public float bounciness = 0f;

    [Header("Masse")]
    [Tooltip("Masse du plateau (relevés et trucks compris) en kg.")]
    [Min(0.001f)] public float deckMass = 1.8f;
    [Tooltip("Masse de chaque roue en kg (une roue 2D = la paire de roues d'un truck).")]
    [Min(0.001f)] public float wheelMass = 0.2f;
    [Tooltip("Coché : inertie de rotation = masse × (empattement / 2)², comme si la masse était aux trucks (proche d'une vraie\n" +
             "planche). Les deux roues deviennent alors indépendantes pour Box2D : une poussée ou une réception à plat sur les deux\n" +
             "roues ne fait plus tourner la planche. Décoché : inertie calculée sur les formes (mesuré : un bumper la faisait\n" +
             "tourner de 5 à 10 °/s, et le sens dépendait de l'ordre de résolution des contacts).")]
    public bool truckInertia = true;

    [Header("Commandes au sol")]
    [Tooltip("Touche de rotation anti-horaire (vers la gauche). Enregistrée par LevelInput comme les flippers.")]
    public FlipperKey rotateLeftKey = FlipperKey.LeftArrow;
    [Tooltip("Touche de rotation horaire (vers la droite).")]
    public FlipperKey rotateRightKey = FlipperKey.RightArrow;
    [Tooltip("Au sol : vitesse de bascule maximale de la planche (flèche tenue), en degrés par seconde.")]
    [Min(0f)] public float groundTiltSpeed = 150f;
    [Tooltip("Au sol : accélération de la bascule, en degrés/s².")]
    [Min(0f)] public float groundTiltAccel = 3000f;
    [Tooltip("Au sol : réactivité de la bascule (1/s) : vitesse visée = écart à l'angle visé × ce gain.")]
    [Min(0f)] public float groundTiltGain = 8f;
    [Tooltip("Bascule arrière : angle visé au-delà du contact du tail avec le sol, en degrés. Plus = tail plus appuyé = frottement\n" +
             "plus fort (le frottement du plateau freine, rien d'autre).")]
    [Range(0f, 30f)] public float tailPress = 4f;
    [Tooltip("Bascule avant : angle visé au-delà du contact du nose avec le sol, en degrés (le nose frotte comme le tail).")]
    [Range(0f, 30f)] public float nosePress = 4f;
    [Tooltip("Pas de simulation sans contact avant de passer aux commandes « en l'air » (petits sauts sur les bosses).")]
    [Range(0, 30)] public int airDelaySteps = 4;
    [Tooltip("Vitesse le long de la planche (km/h) au-dessus de laquelle le sens de marche est mis à jour : il décide quel bout\n" +
             "est l'arrière (frein) et lequel est l'avant.")]
    [SpeedKmh, Min(0f)] public float directionSpeed = 0.3f;

    [Header("Saut")]
    [Tooltip("Touche de saut : tenue au sol = charge, relâchée = saut. En l'air, tenue = le personnage attrape la planche.")]
    public FlipperKey jumpKey = FlipperKey.UpArrow;
    [Tooltip("Vitesse de saut (km/h) d'un simple appui, ajoutée selon la normale du sol. 9 km/h ≈ 0,3 m de haut.")]
    [SpeedKmh, Min(0f)] public float jumpMinSpeed = 2.5f;
    [Tooltip("Vitesse de saut (km/h) après une charge complète. 22 km/h ≈ 1,8 m de haut.")]
    [SpeedKmh, Min(0f)] public float jumpMaxSpeed = 6f;
    [Tooltip("Durée d'appui (s) pour une charge complète : plus on reste appuyé, plus on saute.")]
    [Min(0.01f)] public float jumpChargeTime = 0.5f;

    [Header("En l'air")]
    [Tooltip("En l'air : vitesse de rotation maximale de la planche (autour de son centre de gravité), en degrés par seconde.")]
    [Min(0f)] public float airSpinSpeed = 540f;
    [Tooltip("En l'air, touche appuyée : accélération de la rotation, en degrés/s².")]
    [Min(0f)] public float airSpinAccel = 5000f;
    [Tooltip("En l'air, touche relâchée : freinage de la rotation, en degrés/s² (0 = la rotation continue librement).")]
    [Min(0f)] public float airReleaseAccel = 1500f;

    [Header("Départ")]
    [Tooltip("Vitesse au départ (km/h), pour tester. Le niveau, pas le skateur, doit ensuite donner la vitesse.")]
    [SpeedKmh] public Vector2 initialVelocity = Vector2.zero;
    [Tooltip("Mode « bullet » (fastCollisionsAllowed) : collision continue aussi contre les flippers (kinematic).")]
    public bool fastCollisions = false;

    [Header("Debug")]
    [Tooltip("Couleur de la planche dans le rendu debug et l'aperçu Scene view.")]
    public Color color = new Color(0.95f, 0.75f, 0.3f, 1f);
    [Tooltip("Dessine deux rayons dans chaque roue pour voir leur rotation (dessinée : les roues ne tournent pas dans Box2D).")]
    public bool drawSpokes = true;
    [Tooltip("Couleur des rayons des roues.")]
    public Color spokeColor = Color.black;

    // Catégorie des formes du skateur (planche, ragdoll) : la hitbox du personnage ne se détecte pas elle-même.
    public const int CategoryBit = 26;
    const float TouchSlop = 0.01f;

    PhysicsBody _body;
    bool _created;
    Skater _rider;
    readonly List<PhysicsShape> _shapes = new List<PhysicsShape>();
    readonly List<int> _shapeSide = new List<int>();
    readonly List<bool> _shapeWheel = new List<bool>();
    readonly PhysicsShape[] _wheelShapes = new PhysicsShape[2];
    readonly Vector2[] _wheelLocal = new Vector2[2];
    readonly float[] _spinAngle = new float[2];
    readonly float[] _spinRate = new float[2];
    PhysicsShape _tailShape, _noseShape;
    Vector2 _com;
    float _tailStop, _noseStop;
    int _airSteps;
    float _groundAngle, _groundRate;
    bool _hadGround;
    bool _charging;
    // En l'air avec le personnage : centre de rotation commun (monde : position, vitesse), inertie de l'ensemble au dernier pas,
    // vitesses attendues de la planche après le pas (un écart = choc) ; 'pivotLost' = choc pendant ce vol. Masse et inertie de
    // la planche seule.
    bool _pivotActive, _pivotLost;
    Vector2 _pivotPos, _pivotVel, _expectedVel, _gravity;
    float _pivotInertia, _expectedOmega, _ownMass, _ownInertia;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    // ---------------------------------------------------------------- État lu par le personnage

    public PhysicsBody Body => _body;
    public bool Created => _created;
    // Contact avec une surface sous la planche (roues ou dessous) au dernier pas.
    public bool Grounded { get; private set; }
    // Sans contact depuis plus de 'airDelaySteps' pas.
    public bool InAir => _airSteps > airDelaySteps;
    public int AirSteps => _airSteps;
    // Sens de marche : +1 = vers le nose, -1 = vers le tail (fakie).
    public int Lead { get; private set; } = 1;
    // Bascule demandée au sol : +1 = vers l'arrière (frein), -1 = vers l'avant, 0 = aucune.
    public int Intent { get; private set; }
    // Bout au sol : -1 = tail, +1 = nose, 0 = aucun ou les deux.
    public int EndTouch { get; private set; }
    public Vector2 GroundNormal { get; private set; } = Vector2.up;
    // Bascule de la planche par rapport au sol (radians, anti-horaire) quand un seul côté touche, 0 sinon.
    public float Tilt { get; private set; }
    // Charge du saut en cours (0 à 1), et saut donné à ce pas.
    public float JumpCharge { get; private set; }
    public bool JumpedThisStep { get; private set; }
    // Vitesse ajoutée par le saut de ce pas (le personnage saute avec la planche).
    public Vector2 JumpVelocity { get; private set; }
    // Touche de saut tenue en l'air (le personnage attrape la planche).
    public bool GrabHeld { get; private set; }
    // Changement de vitesse imposé à ce pas pour tourner autour du centre commun (le personnage ne le compte pas comme un choc).
    public Vector2 AirSteerVelocity { get; private set; }
    // Rotation demandée (+1 anti-horaire, -1 horaire), au sol comme en l'air.
    public int SpinInput { get; private set; }
    // Contact avec un élément mobile du niveau (corps kinematic : flipper, bumper) au dernier pas.
    public bool TouchingKinematic { get; private set; }
    // La planche a touché un obstacle (BlobHazard) à ce pas, et où.
    public bool HazardHit { get; private set; }
    public Vector2 HazardPoint { get; private set; }
    public Vector2 CenterOfMass => _com;

    // Longueurs réelles (× scale) : seules utilisées par la géométrie et la physique. Box2D soude les sommets à moins de 0,02 m
    // (une forme plus fine disparaît : plateau perdu à l'échelle 0,5) : l'épaisseur reste au-dessus.
    const float MinThickness = 0.021f;
    public float Length => length * scale;
    public float Thickness => Mathf.Max(thickness * scale, MinThickness);
    public float KickLength => kickLength * scale;
    public float WheelDiameter => wheelDiameter * scale;
    public float Wheelbase => wheelbase * scale;
    public float TruckHeight => truckHeight * scale;

    public float WheelRadius => 0.5f * WheelDiameter;
    public float DeckBottom => WheelRadius + TruckHeight;
    public float DeckTop => DeckBottom + Thickness;
    float FlatHalf => 0.5f * Mathf.Max(0.02f, Length - KickCount * KickLength);
    int KickCount => (noseKick ? 1 : 0) + (tailKick ? 1 : 0);

    public Vector2 WheelCenter(int side) => new Vector2(side * 0.5f * Wheelbase, WheelRadius);

    // Point de la planche où pose le pied (cheville au-dessus du plateau) : 'side' = +1 avant, -1 arrière.
    public Vector2 FootPoint(int side, float spacing, float footHeight) => new Vector2(side * 0.5f * spacing, DeckTop + footHeight);

    // Bout du relevé (dessous), côté 'side' : point qui touche le sol quand la planche bascule.
    public Vector2 KickTip(int side)
    {
        bool kick = side > 0 ? noseKick : tailKick;
        float a = kickAngle * Mathf.Deg2Rad;
        float half = FlatHalf;
        if (!kick) return new Vector2(side * half, DeckBottom);
        return new Vector2(side * (half + KickLength * Mathf.Cos(a)), DeckBottom + KickLength * Mathf.Sin(a));
    }

    // Index du relevé 'side' (-1 tail, +1 nose) dans les polygones de BuildGeometry, -1 s'il n'existe pas.
    public int KickIndex(int side)
    {
        bool tail = tailKick && kickLength > 0f, nose = noseKick && kickLength > 0f;
        if (side < 0) return tail ? 1 : -1;
        return nose ? (tail ? 2 : 1) : -1;
    }

    // Polygones convexes (plateau, relevés, trucks arrière puis avant) et roues (x, y, rayon : arrière puis avant), repère local.
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
            var b1 = b0 + KickLength * d;
            var quad = new[] { b0, b1, b1 + Thickness * n, b0 + Thickness * n };
            if (side < 0) System.Array.Reverse(quad);
            polygons.Add(quad);
        }

        // Trucks : triangles de l'axe de la roue au plateau (l'ancien trapèze, au sommet de 2 cm, était soudé en triangle par
        // Box2D à l'échelle 1 et pas au-delà : masse et forme changeaient avec l'échelle).
        float tw = 0.4f * WheelDiameter;
        for (int side = -1; side <= 1; side += 2)
        {
            var c = WheelCenter(side);
            polygons.Add(new[] { c, new Vector2(c.x + tw, yb), new Vector2(c.x - tw, yb) });
        }
        for (int side = -1; side <= 1; side += 2)
        {
            var c = WheelCenter(side);
            circles.Add(new Vector3(c.x, c.y, WheelRadius));
        }
    }

    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    internal void SetRider(Skater rider) => _rider = rider;

    bool RiderPresent => _rider != null;

    // ---------------------------------------------------------------- Caméra, mode entraînement, effets

    // Caméra : centre de rotation en l'air (centre de gravité commun avec le personnage, sinon celui de la planche), qu'une
    // rotation ne déplace donc pas. Personnage tombé (ragdoll) : son torse.
    public Vector2 CameraPosition
    {
        get
        {
            if (RiderPresent && _rider.IsBroken) return _rider.CameraPosition;
            if (!_created) return transform.position;
            if (_pivotActive) return _pivotPos;
            if (!RiderPresent || !_rider.HasCenter) return _body.worldCenterOfMass;
            return _body.transform.TransformPoint(_rider.PivotLocal);
        }
    }

    public bool TryGetCameraVelocity(out Vector2 velocity)
    {
        velocity = default;
        if (!_created) return false;
        // Au sol : vitesse de la planche (celle du jeu ; celle du point du centre s'emballerait quand la planche tourne seule).
        if (RiderPresent && _rider.IsBroken) velocity = _rider.CameraVelocity;
        else if (_pivotActive) velocity = _pivotVel;
        else velocity = _body.linearVelocity;
        return true;
    }

    public CapsuleGeometry GhostShape => GhostShapeOf(0);

    public CapsuleGeometry GhostShapeOf(int index)
    {
        if (index > 0 && RiderPresent) return _rider.GhostShapeOf(index - 1);
        float y = 0.5f * (DeckBottom + DeckTop), half = 0.5f * Length;
        return new CapsuleGeometry { center1 = new Vector2(-half, y), center2 = new Vector2(half, y), radius = 0.5f * Thickness };
    }

    public bool IsBroken => RiderPresent && _rider.IsBroken;

    // Corps suivis : la planche, puis les membres du personnage s'il est là.
    public void GetBodyStates(List<BodyState> states)
    {
        states.Clear();
        if (!_created) return;
        var t = _body.transform;
        states.Add(new BodyState { position = t.position, angle = t.rotation.radians, velocity = _body.linearVelocity, angularVelocity = _body.angularVelocity });
        if (RiderPresent) _rider.AppendBodyStates(states);
    }

    // Replace la planche (et le personnage) dans un état mémorisé : mode entraînement, rejeu qui ne retombe plus au même endroit.
    public void RestoreBodyStates(IReadOnlyList<BodyState> states, bool broken)
    {
        int riderCount = RiderPresent ? Skater.LimbCount : 0;
        if (!_created || states.Count != 1 + riderCount) return;
        _body.transform = new PhysicsTransform(states[0].position, PhysicsRotate.FromRadians(states[0].angle));
        _body.linearVelocity = states[0].velocity;
        _body.angularVelocity = states[0].angularVelocity;
        ResetControl();
        if (RiderPresent) _rider.RestoreBodyStates(states, 1, broken);
    }

    // Contacts pour les effets (étincelles) : la planche sans ses roues (tail qui frotte, plateau), puis le ragdoll.
    public void GetSurfaceContacts(List<SurfaceContact> result)
    {
        result.Clear();
        if (!_created) return;
        AppendSurfaceContacts(_body, result, true);
        if (RiderPresent) _rider.AppendSurfaceContacts(result);
    }

    internal void AppendSurfaceContacts(PhysicsBody body, List<SurfaceContact> result, bool skipWheels)
    {
        Vector2 velocity = body.linearVelocity;
        var contacts = body.GetContacts(Allocator.Temp);
        for (int k = 0; k < contacts.Length; k++)
        {
            var manifold = contacts[k].manifold;
            if (manifold.pointCount == 0) continue;
            bool isA = contacts[k].shapeA.body == body;
            var mine = isA ? contacts[k].shapeA : contacts[k].shapeB;
            if (skipWheels && (mine == _wheelShapes[0] || mine == _wheelShapes[1])) continue;
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
        if (!_created) return;
        if (_body.isValid) _body.Destroy();
        _body = default;
        _shapes.Clear();
        _shapeSide.Clear();
        _shapeWheel.Clear();
        System.Array.Clear(_wheelShapes, 0, 2);
        _created = false;
    }

    readonly List<Vector2[]> _polygons = new List<Vector2[]>();
    readonly List<Vector3> _circles = new List<Vector3>();

    void CreatePhysics()
    {
        if (_created) return;
        BuildGeometry(_polygons, _circles);

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        bodyDef.position = transform.position;
        bodyDef.rotation = PhysicsRotate.FromRadians(transform.eulerAngles.z * Mathf.Deg2Rad);
        bodyDef.fastCollisionsAllowed = fastCollisions;
        bodyDef.linearVelocity = initialVelocity;
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var filter = shapeDef.contactFilter;
        filter.groupIndex = -2; // négatif : la planche et le ragdoll d'un même skateur ne collisionnent pas entre eux
        filter.categories = new PhysicsMask { bitMask = filter.categories.bitMask | (1UL << CategoryBit) };
        shapeDef.contactFilter = filter;

        float area = 0f;
        foreach (var p in _polygons) area += PolygonArea(p);
        shapeDef.density = deckMass / Mathf.Max(1e-6f, area);
        SetMaterial(ref shapeDef, deckFriction, bounciness, color);
        int tailIndex = KickIndex(-1), noseIndex = KickIndex(1);
        int polygonCount = _polygons.Count;
        for (int i = 0; i < polygonCount; i++)
        {
            // Côté de chaque forme : relevés, puis trucks arrière / avant (ordre de BuildGeometry).
            int side = i == tailIndex || i == polygonCount - 2 ? -1 : i == noseIndex || i == polygonCount - 1 ? 1 : 0;
            AddShape(_body.CreateShape(PolygonGeometry.Create(_polygons[i], 0f), shapeDef), side, false);
        }
        _tailShape = tailIndex >= 0 ? _shapes[tailIndex] : default;
        _noseShape = noseIndex >= 0 ? _shapes[noseIndex] : default;

        SetMaterial(ref shapeDef, wheelFriction, bounciness, color);
        for (int i = 0; i < 2; i++)
        {
            var c = _circles[i];
            _wheelLocal[i] = new Vector2(c.x, c.y);
            shapeDef.density = wheelMass / (Mathf.PI * c.z * c.z);
            _wheelShapes[i] = _body.CreateShape(new CircleGeometry { center = _wheelLocal[i], radius = c.z }, shapeDef);
            AddShape(_wheelShapes[i], i == 0 ? -1 : 1, true);
        }

        if (truckInertia)
        {
            var mc = _body.massConfiguration;
            mc.rotationalInertia = mc.mass * 0.25f * Wheelbase * Wheelbase;
            _body.massConfiguration = mc;
        }
        _ownMass = _body.mass;
        _ownInertia = _body.massConfiguration.rotationalInertia;
        _gravity = PhysicsWorld.defaultWorld.gravity;
        _com = _body.localCenterOfMass;
        _tailStop = StopAngle(-1);
        _noseStop = StopAngle(1);
        ResetControl();
        _created = true;
    }

    void ResetControl()
    {
        Lead = 1;
        Intent = 0;
        EndTouch = 0;
        Tilt = 0f;
        Grounded = false;
        GroundNormal = Vector2.up;
        _airSteps = 0;
        _groundRate = 0f;
        _hadGround = false;
        _charging = false;
        JumpCharge = 0f;
        JumpedThisStep = false;
        GrabHeld = false;
        SpinInput = 0;
        HazardHit = false;
        AirSteerVelocity = Vector2.zero;
        _pivotActive = _pivotLost = false;
        for (int i = 0; i < 2; i++) { _spinAngle[i] = 0f; _spinRate[i] = 0f; }
    }

    void AddShape(PhysicsShape shape, int side, bool wheel)
    {
        _shapes.Add(shape);
        _shapeSide.Add(side);
        _shapeWheel.Add(wheel);
    }

    internal static void SetMaterial(ref PhysicsShapeDefinition def, float friction, float bounce, Color debugColor)
    {
        var material = def.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounce;
        material.customColor = debugColor;
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

    // Angle de bascule (radians) auquel le bout 'side' touche le sol, la roue de ce côté restant posée (la planche pivote
    // autour de l'axe de la roue) : hauteur du bout = r + dy·cos θ − dx·sin θ = 0.
    public float StopAngle(int side)
    {
        Vector2 tip = KickTip(side);
        float r = WheelRadius;
        float dx = Mathf.Max(0.001f, Mathf.Abs(tip.x) - 0.5f * Wheelbase);
        float dy = tip.y - r;
        float l = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Atan2(dy, dx) + Mathf.Asin(Mathf.Min(1f, r / l));
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
        float dt = Time.fixedDeltaTime;
        if (LevelSimulation.Step > 0) ReadHazardEvents();
        JumpedThisStep = false;
        AirSteerVelocity = Vector2.zero;

        var xf = _body.transform;
        float bc = xf.rotation.cos, bs = xf.rotation.sin;
        var axis = new Vector2(bc, bs);
        var up = new Vector2(-bs, bc);

        bool touching = GroundContact(up, out var normal, out float measured, out int endTouch);
        Grounded = touching;
        GroundNormal = normal;
        Tilt = touching ? measured : 0f;
        EndTouch = endTouch;
        _airSteps = touching ? 0 : _airSteps + 1;
        bool air = InAir;
        if (touching) _pivotLost = false;
        bool controls = !RiderPresent || _rider.Controllable;
        bool pivot = air && controls && RiderPresent && _rider.SpinsAroundCenter;
        if (_pivotActive) EndPivotIfNeeded(pivot, air, axis);

        Vector2 velocity = _body.linearVelocity;
        if (touching)
        {
            float along = Vector2.Dot(velocity, axis);
            if (along > directionSpeed) Lead = 1;
            else if (along < -directionSpeed) Lead = -1;
        }

        int spin = controls ? (LevelInput.IsDown(rotateLeftKey) ? 1 : 0) - (LevelInput.IsDown(rotateRightKey) ? 1 : 0) : 0;
        SpinInput = spin;
        Intent = air ? 0 : spin * Lead;
        float omega = _body.angularVelocity * Mathf.Deg2Rad;

        if (touching)
        {
            // Rotation du sol sous la planche (courbe) : la bascule se mesure par rapport à lui.
            float groundAngle = Mathf.Atan2(normal.y, normal.x);
            float raw = _hadGround ? Mathf.DeltaAngle(_groundAngle * Mathf.Rad2Deg, groundAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt : 0f;
            _groundRate = _hadGround ? Mathf.Lerp(_groundRate, raw, 0.3f) : 0f;
            _groundAngle = groundAngle;
            _hadGround = true;
            if (Intent != 0) GroundTilt(normal, measured, endTouch, omega - _groundRate, dt);
        }
        else _hadGround = false;

        Jump(controls, air, normal, dt);
        if (pivot && !_pivotLost) AirSpinAroundCenter(spin, omega, velocity, dt);
        else if (air && controls) AirSpin(spin, omega, dt);
    }

    // Saut : touche tenue au sol = charge, relâchée = saut selon la normale du sol (sans rotation : appliqué au centre de
    // gravité). Relâcher juste après avoir quitté le sol ('airDelaySteps') saute encore. En l'air, tenue = attraper la planche.
    void Jump(bool controls, bool air, Vector2 normal, float dt)
    {
        bool held = controls && LevelInput.IsDown(jumpKey);
        GrabHeld = held && air;
        if (held && !air)
        {
            _charging = true;
            JumpCharge = Mathf.Min(1f, JumpCharge + dt / jumpChargeTime);
            return;
        }
        if (!held && _charging && !air)
        {
            float speed = Mathf.Lerp(jumpMinSpeed, jumpMaxSpeed, JumpCharge);
            JumpVelocity = speed * normal;
            _body.linearVelocity += JumpVelocity;
            JumpedThisStep = true;
        }
        if (!held || air)
        {
            _charging = false;
            JumpCharge = 0f;
        }
    }

    // Planche au contact d'une surface sous elle (point touchant, normale vers le haut de la planche). Lecture seule.
    // Par la forme touchée, pas par la position (les points de contact datent du début du pas, décalés de v × dt).
    // 'measured' : bascule de la planche par rapport au sol (radians, anti-horaire) quand un seul côté touche, 0 sinon.
    // Normale : celle de la forme de plus petit index qui touche (indépendant de l'ordre des contacts de Box2D).
    // 'endTouch' : -1 si le relevé arrière (tail) touche, +1 si c'est le nose, 0 sinon (les deux ou aucun).
    bool GroundContact(Vector2 up, out Vector2 normal, out float measured, out int endTouch)
    {
        int sides = 0, firstShape = int.MaxValue;
        bool tail = false, nose = false, kinematic = false;
        normal = up;
        var contacts = _body.GetContacts(Allocator.Temp);
        for (int k = 0; k < contacts.Length; k++)
        {
            var m = contacts[k].manifold;
            if (m.pointCount == 0) continue;
            bool isA = contacts[k].shapeA.body == _body;
            if ((isA ? contacts[k].shapeB : contacts[k].shapeA).body.type == PhysicsBody.BodyType.Kinematic) kinematic = true;
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
        TouchingKinematic = kinematic;
        endTouch = tail == nose ? 0 : tail ? -1 : 1;
        measured = sides == 1 || sides == 2 ? Mathf.Atan2(normal.x * up.y - normal.y * up.x, Vector2.Dot(normal, up)) : 0f;
        return sides != 0;
    }

    // Flèche tenue au sol : la planche pivote vers l'angle visé sur la roue qui reste au sol (celle du côté déjà posé, sinon du
    // côté qui descend) : rotation + montée ou descente de son centre de gravité selon la normale du sol, jamais de vitesse le long du
    // sol (aucune propulsion). 'rate' : vitesse de bascule par rapport au sol (rad/s, anti-horaire).
    // Bout déjà au sol du côté qui descend : rotation pure, elle presse ce bout contre le sol (frottement = frein).
    void GroundTilt(Vector2 normal, float measured, int endTouch, float rate, float dt)
    {
        int lead = Lead;
        float goal = Intent > 0 ? ((lead > 0 ? _tailStop : _noseStop) + tailPress * Mathf.Deg2Rad) * lead
                                : -((lead > 0 ? _noseStop : _tailStop) + nosePress * Mathf.Deg2Rad) * lead;
        float maxRate = groundTiltSpeed * Mathf.Deg2Rad;
        float desired = Mathf.Clamp((goal - measured) * groundTiltGain, -maxRate, maxRate);
        float maxDelta = groundTiltAccel * Mathf.Deg2Rad * dt;
        float dw = Mathf.Clamp(desired - rate, -maxDelta, maxDelta);
        if (dw == 0f) return;
        int pivotSide = Mathf.Abs(measured) > Mathf.Deg2Rad ? (measured > 0f ? -1 : 1) : (dw > 0f ? -1 : 1);
        float lever = _com.x - pivotSide * 0.5f * Wheelbase;
        _body.angularVelocity += dw * Mathf.Rad2Deg;
        if (endTouch != pivotSide) _body.linearVelocity += dw * lever * normal;
    }

    // En l'air, planche seule (ou personnage qui ne tourne pas autour du centre commun) : rotation autour du centre de gravité de
    // la planche, sa vitesse n'est pas touchée. Vitesse possible selon l'inertie de la posture du personnage (Skater.AirSpinFactor).
    void AirSpin(int spin, float omega, float dt)
    {
        float factor = RiderPresent ? _rider.AirSpinFactor : 1f;
        _body.angularVelocity = SpinControl(spin, omega, factor, dt) * Mathf.Rad2Deg;
    }

    // Commande de rotation en l'air (rad/s) : flèche = accélère jusqu'à la vitesse max, relâchée = freine. 'factor' = rapport
    // d'inertie de la posture (groupé = plus vite).
    float SpinControl(int spin, float omega, float factor, float dt)
    {
        float accel = (spin != 0 ? airSpinAccel * factor : airReleaseAccel) * Mathf.Deg2Rad;
        float target = spin * airSpinSpeed * factor * Mathf.Deg2Rad;
        if (accel > 0f) omega += Mathf.Clamp(target - omega, -accel * dt, accel * dt);
        return omega;
    }

    const float PivotTolerance = 0.01f;

    // En l'air avec le personnage : la planche tourne autour du centre de gravité commun (planche + personnage, posture en cours,
    // presets précalculés), qui suit sa parabole comme un corps libre. Sa vitesse est imposée à chaque pas pour la placer autour
    // de lui ; quand la posture change, le centre glisse sur elle (sans à-coup) et c'est la planche qui bouge autour de lui. Élan
    // de rotation conservé : se grouper accélère. Au décollage, la rotation est celle de la planche et du corps, pondérées par
    // leur inertie (une secousse de la planche seule, coup de flipper, ne fait pas tourner tout le monde), et la planche garde sa
    // vitesse : le centre part avec la vitesse de son point dans cette rotation. La planche reste légère : un choc (vitesse
    // différente de la prévision, même sans contact signalé)
    // est le sien, absorbé comme par des chevilles souples ; elle tourne ensuite autour de son propre centre jusqu'au prochain
    // décollage (relire sa rotation comme celle de l'ensemble injectait de l'énergie). Le personnage ne compte pas la vitesse
    // imposée comme un choc.
    void AirSpinAroundCenter(int spin, float omega, Vector2 v, float dt)
    {
        var xf = _body.transform;
        Vector2 com = xf.TransformPoint(_com);
        Vector2 pivotLocal = _rider.PivotLocal;
        float inertia = _rider.CenterInertia;
        if (!_pivotActive)
        {
            _pivotPos = xf.TransformPoint(pivotLocal);
            Vector2 r = _pivotPos - com;
            float boardPart = _ownInertia + _ownMass * r.sqrMagnitude;
            omega = (boardPart * omega + (inertia - boardPart) * _rider.BodyOmega) / inertia;
            _pivotVel = v + omega * new Vector2(-r.y, r.x);
            _pivotInertia = inertia;
            _pivotActive = true;
            _rider.StopCenterSlide();
        }
        omega *= _rider.InertiaFactor(_pivotInertia, inertia);
        _pivotInertia = inertia;
        omega = SpinControl(spin, omega, _rider.AirSpinFactor, dt);

        // Pas suivant : le centre suit sa parabole, la planche est placée autour de lui, tournée de ω·dt.
        _pivotVel += _gravity * dt;
        _pivotPos += _pivotVel * dt;
        float bc = xf.rotation.cos, bs = xf.rotation.sin, a = omega * dt, c = Mathf.Cos(a), s = Mathf.Sin(a);
        Vector2 arm = _com - pivotLocal;
        arm = new Vector2(bc * arm.x - bs * arm.y, bs * arm.x + bc * arm.y);
        arm = new Vector2(c * arm.x - s * arm.y, s * arm.x + c * arm.y);
        Vector2 velocity = (_pivotPos + arm - com) / dt - _gravity * dt;
        AirSteerVelocity = velocity - v;
        _body.linearVelocity = velocity;
        _body.angularVelocity = omega * Mathf.Rad2Deg;
        _expectedVel = velocity + _gravity * dt;
        _expectedOmega = omega;
    }

    // Fin du vol autour du centre commun : contact avec le sol, choc en l'air (vitesse différente de la prévision, même sans
    // contact signalé : la planche seule l'a subi, elle tourne ensuite autour de son propre centre jusqu'au prochain décollage),
    // ou personnage qui ne tient plus. La planche, sous le centre, allait plus vite ou moins vite que lui le long de son axe
    // (rotation) : le corps, ≈ 30 fois plus lourd, la ramène à sa vitesse (sinon tourner avant de toucher le sol donnerait de la
    // vitesse gratuite). La réaction du sol dans l'autre axe reste entière.
    void EndPivotIfNeeded(bool pivot, bool air, Vector2 axis)
    {
        Vector2 v = _body.linearVelocity;
        float omega = _body.angularVelocity * Mathf.Deg2Rad;
        if (pivot && (v - _expectedVel).sqrMagnitude <= PivotTolerance * PivotTolerance &&
            Mathf.Abs(omega - _expectedOmega) <= PivotTolerance) return;
        Vector2 dv = -Vector2.Dot(_expectedVel - _pivotVel, axis) * axis;
        _body.linearVelocity = v + dv;
        AirSteerVelocity = dv;
        _pivotActive = false;
        if (air) _pivotLost = true;
    }

    // Obstacle touché par la planche au dernier pas (événements de contact ; seul « touché ou non » compte : indépendant de
    // leur ordre).
    void ReadHazardEvents()
    {
        bool hit = false;
        foreach (var e in PhysicsWorld.defaultWorld.contactBeginEvents)
        {
            PhysicsShape other;
            if (ShapeIndexOf(e.shapeA) >= 0) other = e.shapeB;
            else if (ShapeIndexOf(e.shapeB) >= 0) other = e.shapeA;
            else continue;
            if (other.isValid && other.userData.objectValue is BlobHazard) hit = true;
        }
        HazardHit = hit;
        if (hit) HazardPoint = _body.worldCenterOfMass;
    }

    // ---------------------------------------------------------------- Rendu

    // Rotation des roues, dessinée seulement : une roue au sol tourne à la vitesse de la planche le long de son axe (v / r), en
    // l'air elle garde sa rotation.
    void LateUpdate()
    {
        if (!Application.isPlaying || !_created || !drawSpokes) return;
        var world = PhysicsWorld.defaultWorld;
        float r = WheelRadius;
        var xf = _body.transform;
        Vector2 axis = new Vector2(xf.rotation.cos, xf.rotation.sin);
        for (int i = 0; i < 2; i++)
        {
            Vector2 c = xf.TransformPoint(_wheelLocal[i]);
            using (var contacts = _wheelShapes[i].GetContacts(Allocator.Temp))
                if (contacts.Length > 0)
                    _spinRate[i] = -Vector2.Dot(_body.GetWorldPointVelocity(c), axis) / r;
            _spinAngle[i] = Mathf.Repeat(_spinAngle[i] + _spinRate[i] * Time.deltaTime, 2f * Mathf.PI);
            float angle = xf.rotation.radians + _spinAngle[i];
            Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            Vector2 n = new Vector2(-a.y, a.x);
            world.DrawLine(c - a, c + a, spokeColor, 0f);
            world.DrawLine(c - n, c + n, spokeColor, 0f);
        }
    }

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
