using System.Collections.Generic;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

// Personnage sur la planche, optionnel : composant décoché = planche seule. Il ne fait PAS le mouvement : tant qu'il tient sur
// la planche, celle-ci (Skateboard) est le seul corps physique et il ne la pousse jamais. Lui est une posture calculée à
// chaque pas, en trois couches :
// 1. Physique de l'animation : la masse du corps (hanches) sur ses jambes, ressort-amortisseur dans l'axe des jambes et le long
//    de la planche, entraînée par les mouvements réels de la planche (Box2D). Réception, looping : les genoux plient ; choc
//    contre un trottoir : le corps continue sa course. Un choc trop fort (butée des jambes ou du buste) le fait tomber.
// 2. Posture : posture clé (preset) choisie selon la situation et rejointe sans à-coup, puis cinématique inverse du bonhomme
//    bâton sur ces hanches (jambes vers les pieds posés, buste, bras). Le centre de gravité commun de chaque preset, précalculé,
//    est le centre de rotation de la planche en l'air (seule influence du personnage sur elle).
// 3. Hitbox : requêtes de recouvrement (tête / torse contre le décor, membres contre les obstacles), il ne pousse rien.
// Il tombe en ragdoll Box2D (qui part avec la vitesse de son corps, pas celle de la planche) sur un choc trop fort, en se
// cognant, ou en perdant l'équilibre (wheelie tenu, si autorisé) : il bascule d'abord autour de son pied, puis lâche.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Skateboard))]
public class Skater : MonoBehaviour, ILevelPhysics, ILevelTick
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
    [Tooltip("Écart entre les pieds, relatif à l'empattement de la planche (1 = un pied au-dessus de chaque truck), plafonné par\n" +
             "'maxStance'.")]
    [Range(0.2f, 1.6f)] public float stance = 0.8f;
    [Tooltip("Écart maximal entre les pieds, relatif à 'height' : sur une grande planche (Skateboard.scale), les pieds ne suivent\n" +
             "plus l'empattement au-delà (les jambes n'y arriveraient pas).")]
    [Range(0.2f, 1f)] public float maxStance = 0.6f;

    [Header("Masse")]
    [Tooltip("Masse du personnage en kg, répartie entre les membres du ragdoll quand il tombe. Vivant, elle ne pèse PAS sur la\n" +
             "planche (le mouvement est celui de la planche seule).")]
    [Min(0.01f)] public float mass = 1f;

    // Posture clé (preset) : la situation choisit la posture visée, le personnage la rejoint sans à-coup. Son centre de gravité et
    // son inertie sont calculés une fois au départ sur la posture figée.
    [System.Serializable]
    public struct Pose
    {
        [Tooltip("Extension de la jambe la plus tendue (1 = tendue, 0,6 = groupé).")]
        [Range(0.4f, 1f)] public float crouch;
        [Tooltip("Inclinaison du torse vers l'avant (sens du déplacement), en degrés.")]
        [Range(-30f, 90f)] public float torsoLean;
        [Tooltip("Ouverture des bras depuis le torse, vers l'extérieur, en degrés (0 = le long du corps).")]
        [Range(-30f, 180f)] public float arms;
        [Tooltip("Hanches déplacées vers l'avant (sens du déplacement), relatif au demi-écart des pieds (1 = au-dessus du pied avant,\n" +
                 "négatif = vers l'arrière).")]
        [Range(-2f, 2f)] public float hipShift;

        public Pose(float crouch, float torsoLean, float arms, float hipShift)
        {
            this.crouch = crouch;
            this.torsoLean = torsoLean;
            this.arms = arms;
            this.hipShift = hipShift;
        }

        public static Pose Lerp(in Pose a, in Pose b, float t) => new Pose(Mathf.Lerp(a.crouch, b.crouch, t),
            Mathf.Lerp(a.torsoLean, b.torsoLean, t), Mathf.Lerp(a.arms, b.arms, t), Mathf.Lerp(a.hipShift, b.hipShift, t));
    }

    [Header("Postures (presets)")]
    [Tooltip("Durée d'une transition entre deux postures (s, ≈ 95 % du chemin), sans à-coup (ressort critique). Le centre de\n" +
             "gravité qui fait tourner la planche en l'air glisse au même rythme que le dessin.")]
    [Min(0.02f)] public float poseTime = 0.25f;
    [Tooltip("Roule lentement : debout.")]
    public Pose slowPose = new Pose(0.97f, 5f, 12f, 0f);
    [Tooltip("Roule vite (au-dessus de 'fastPoseSpeed') : accroupi, penché vers l'avant.")]
    public Pose fastPose = new Pose(0.78f, 30f, 5f, 0f);
    [Tooltip("Vitesse (km/h) au-dessus de laquelle il prend la posture « vite » au sol.")]
    [SpeedKmh, Min(0f)] public float fastPoseSpeed = 6.95f;
    [Tooltip("Marge (km/h) : il ne reprend la posture « lent » qu'en dessous de 'fastPoseSpeed' moins cette marge (pas de\n" +
             "clignotement autour du seuil).")]
    [SpeedKmh, Min(0f)] public float fastPoseMargin = 1.1f;
    [Tooltip("Charge du saut (touche de saut tenue au sol) : posture en fin de charge, mélangée à la posture au sol selon la charge.")]
    public Pose chargePose = new Pose(0.68f, 10f, 20f, 0f);
    [Tooltip("En l'air, planche non attrapée : jambes un peu fléchies, bras écartés pour l'équilibre. Référence de la vitesse de\n" +
             "rotation en l'air (Skateboard.airSpinSpeed).")]
    public Pose airPose = new Pose(0.85f, 5f, 110f, 0f);
    [Tooltip("En l'air, planche attrapée (touche de saut tenue) : groupé, centre de gravité près de la planche.")]
    public Pose grabPose = new Pose(0.6f, 45f, 15f, 0f);
    [Tooltip("Bascule arrière au sol (frein sur le tail) : hanches vers le tail, bras d'équilibre.")]
    public Pose tailPose = new Pose(0.98f, 0f, 75f, -1.3f);
    [Tooltip("Bascule avant au sol (nose) : hanches vers le nose, buste penché.")]
    public Pose nosePose = new Pose(0.98f, 30f, 75f, 1.3f);
    [Tooltip("Pendant une bascule voulue (flèche au sol) : part de la bascule de la planche compensée par le corps (1 = il reste droit par rapport au sol, 0 = il bascule avec). Sans flèche, il suit la planche.")]
    [Range(0f, 1f)] public float counterTilt = 0.9f;
    [Tooltip("Compensation maximale, en degrés : au-delà, il bascule avec la planche.")]
    [Range(0f, 90f)] public float maxCounterTilt = 45f;
    [Tooltip("Flexion des coudes, en degrés.")]
    [Range(0f, 150f)] public float elbowBend = 15f;

    [Header("Corps sur les jambes (physique de l'animation)")]
    [Tooltip("Raideur des jambes, en Hz : fréquence propre du corps qui rebondit sur ses jambes. Bas = genoux qui plient beaucoup\n" +
             "sous les g (réceptions, loopings, bosses), haut = jambes raides.")]
    [Min(0.1f)] public float legFrequency = 1.8f;
    [Tooltip("Amortissement des jambes (1 = critique : pas de rebond).")]
    [Min(0f)] public float legDamping = 0.6f;
    [Tooltip("Flexion maximale des jambes (descente des hanches sous la posture voulue), relative à 'height'.")]
    [Range(0f, 0.5f)] public float maxCompression = 0.22f;
    [Tooltip("Raideur du corps le long de la planche, en Hz (hanches qui partent en avant quand la planche freine).")]
    [Min(0.1f)] public float leanFrequency = 2.5f;
    [Tooltip("Amortissement le long de la planche (1 = critique).")]
    [Min(0f)] public float leanDamping = 0.7f;
    [Tooltip("Déplacement maximal des hanches le long de la planche, relatif à 'height'.")]
    [Range(0f, 0.5f)] public float maxLean = 0.15f;
    [Tooltip("Inclinaison du buste (degrés) quand les hanches sont au déplacement maximal le long de la planche.")]
    [Range(0f, 90f)] public float leanTorsoPitch = 35f;
    [Tooltip("Adhérence des pieds sur la planche (coefficient de frottement) : la planche ne peut freiner ou pousser le corps le\n" +
             "long d'elle-même que de 'footGrip' × la charge sur les jambes. Au-delà (trottoir, mur), le corps continue sur sa lancée.")]
    [Min(0f)] public float footGrip = 0.8f;
    [Tooltip("Le corps suit la rotation de la planche avec ce rythme (1/s) : une rotation lente (looping, bascule) est suivie,\n" +
             "une rotation brutale (planche qui se rabat à la réception) ne lui est pas imposée : chevilles et genoux l'absorbent.")]
    [Min(0.1f)] public float bodyRotationFollow = 8f;
    [Tooltip("En l'air, il se tient à la planche : son corps se recale sur la posture voulue à ce rythme (1/s).")]
    [Min(0f)] public float airFollow = 25f;
    [Tooltip("En l'air : écart (km/h) entre la vitesse de la planche et une chute libre au-delà duquel c'est un choc (réception,\n" +
             "obstacle) transmis au corps, même si Box2D n'a pas encore de contact (collision continue à grande vitesse).")]
    [SpeedKmh, Min(0f)] public float airImpactSpeed = 0.5f;
    [Tooltip("Raideur des bras, en Hz : bas = bras ballants qui suivent les secousses.")]
    [Min(0.1f)] public float armFrequency = 2.5f;
    [Tooltip("Amortissement des bras (1 = critique).")]
    [Min(0f)] public float armDamping = 0.35f;
    [Tooltip("Balancement des bras, en degrés par m/s² d'accélération du corps par rapport à la planche.")]
    [Min(0f)] public float armSwing = 1.5f;

    [Header("Chute")]
    [Tooltip("Wheelie arrière : coché = tail tenu au sol (flèche tenue) pendant 'tailFallTime' le fait tomber en arrière.\n" +
             "Décoché = le tail freine aussi longtemps qu'on veut.")]
    public bool canFallBackward = false;
    [Tooltip("Durée (s) de tail tenu au sol avant de perdre l'équilibre (si 'canFallBackward').")]
    [Min(0f)] public float tailFallTime = 0.8f;
    [Tooltip("Wheelie avant : coché = nose tenu au sol (flèche tenue) pendant 'noseFallTime' le fait tomber en avant.\n" +
             "Décoché = le nose frotte aussi longtemps qu'on veut.")]
    public bool canFallForward = true;
    [Tooltip("Durée (s) de nose tenu au sol avant de perdre l'équilibre (si 'canFallForward').")]
    [Min(0f)] public float noseFallTime = 0.3f;
    [Tooltip("Perte d'équilibre : vitesse de bascule de départ autour du pied, en degrés/s (ensuite la gravité l'accélère).")]
    [Min(0f)] public float fallStartSpeed = 60f;
    [Tooltip("Perte d'équilibre : angle de bascule (degrés) auquel il lâche la planche et devient un ragdoll.")]
    [Range(5f, 120f)] public float fallDetachAngle = 50f;
    [Tooltip("Réception : vitesse (km/h) à laquelle le corps arrive en butée des jambes au-delà de laquelle elles cèdent (chute).\n" +
             "Une réception à plat de très haut l'écrase ; dans la pente, la vitesse dans l'axe des jambes reste faible.")]
    [SpeedKmh, Min(0f)] public float landingCrashSpeed = 4f;
    [Tooltip("Choc le long de la planche (trottoir, mur bas) : vitesse (km/h) du corps en butée au-delà de laquelle il est\n" +
             "projeté (il continue sur sa lancée, la planche reste).")]
    [SpeedKmh, Min(0f)] public float impactCrashSpeed = 3f;
    [Tooltip("Coché : la poussée d'un élément mobile du niveau (corps kinematic : flipper, bumper) plie les jambes à fond mais ne\n" +
             "fait jamais tomber. Décoché : même règle que les autres chocs (un bumper à 53 km/h écrase les jambes : chute).")]
    public bool safeLaunchers = true;
    [Tooltip("La tête qui touche le décor = chute (le verre ne compte pas).")]
    public bool headIsDeadly = true;
    [Tooltip("Le torse qui touche le décor = chute (le verre ne compte pas).")]
    public bool torsoIsDeadly = true;
    [Tooltip("Planche sans contact avec le sol et presque immobile pendant cette durée (s) = chute (planche bloquée). 0 = jamais.")]
    [Min(0f)] public float stuckTime = 1f;
    [Tooltip("Vitesse de la planche (km/h) en dessous de laquelle elle est considérée immobile pour 'stuckTime'.")]
    [SpeedKmh, Min(0f)] public float stuckSpeed = 0.5f;
    [Tooltip("Ragdoll : frottement des articulations (N·m, absolu). 0 = pantin totalement libre.")]
    [Min(0f)] public float deadJointFriction = 0.005f;

    [Header("En l'air : centre de gravité commun")]
    [Tooltip("Coché : en l'air, la planche tourne autour du centre de gravité commun (planche + personnage, presque celui du\n" +
             "personnage), qui suit sa parabole : il tourne sur lui-même, la planche autour de lui. Décoché : autour de celui de la\n" +
             "planche (le personnage balaie un grand cercle). Au sol, rien ne change : la planche seule fait le mouvement.")]
    public bool spinAroundCommonCenter = true;
    [Tooltip("Masse du personnage (kg) pour ce centre et l'inertie en l'air (planche ≈ 2,2 kg). Il ne pèse pas sur la planche au\n" +
             "sol ; le ragdoll utilise 'mass'.")]
    [Min(0.01f)] public float bodyMass = 60f;
    [Tooltip("Effet de l'inertie de la posture sur la rotation en l'air. 1 = physique : vitesse de rotation possible ∝ 1 / inertie\n" +
             "(groupé ≈ 1,7 fois plus vite que bras écartés), et élan conservé quand il change de posture (se grouper accélère la\n" +
             "rotation, se déplier la ralentit). 0 = aucun effet.")]
    [Range(0f, 1f)] public float inertiaEffect = 1f;

    [Header("Debug")]
    [Tooltip("Couleur du personnage dans le rendu et l'aperçu Scene view.")]
    public Color riderColor = Color.white;
    [Tooltip("Dessine les articulations du ragdoll (rendu debug de Box2D).")]
    public bool drawJoints = false;
    [Tooltip("Dessine le centre de gravité commun (rond barré, tourne avec la planche) et, pendant une transition, celui de la\n" +
             "posture visée (petit rond).")]
    public bool drawCenterOfMass = true;
    [Tooltip("Couleur du centre de gravité dessiné.")]
    public Color centerColor = new Color(1f, 0.2f, 0.2f, 1f);

    public static event System.Action<Skater, Vector2> Died;

    const int Torso = 0, Head = 1, ThighF = 2, ShinF = 3, ThighB = 4, ShinB = 5, UpperF = 6, ForeF = 7, UpperB = 8, ForeB = 9;
    public const int LimbCount = 10;
    const int Neck = 0, HipF = 1, KneeF = 2, HipB = 3, KneeB = 4, ShoulderF = 5, ElbowF = 6, ShoulderB = 7, ElbowB = 8;
    const int JointCount = 9;
    // Fractions de 'mass' par membre.
    static readonly float[] MassShare = { 0.50f, 0.08f, 0.10f, 0.055f, 0.10f, 0.055f, 0.03f, 0.025f, 0.03f, 0.025f };

    enum Mode { Riding, Falling, Ragdoll }

    Skateboard _board;
    Mode _mode;
    bool _ready;
    NativeArray<PhysicsBody> _bodies;
    readonly PhysicsHingeJoint[] _joints = new PhysicsHingeJoint[JointCount];
    readonly int[] _hazardTouches = new int[LimbCount];
    readonly bool[] _attached = new bool[LimbCount];
    readonly List<PhysicsShape> _shapes = new List<PhysicsShape>();
    PhysicsQuery.QueryFilter _sceneryFilter, _hazardFilter;

    // Posture (repère de la planche) : origine et angle (radians) de chaque membre, et capsules correspondantes.
    readonly Vector2[] _posePos = new Vector2[LimbCount];
    readonly float[] _poseAngle = new float[LimbCount];
    readonly CapsuleGeometry[] _localLimbs = new CapsuleGeometry[LimbCount];
    readonly CapsuleGeometry[] _worldLimbs = new CapsuleGeometry[LimbCount];

    // Presets (sens de marche +X) : centre de gravité du personnage (repère de la planche) et inertie propre par kg, calculés une
    // fois au départ (Init) sur la posture figée ; planche lue au même moment.
    const int PoseSlow = 0, PoseFast = 1, PoseCharge = 2, PoseAir = 3, PoseGrab = 4, PoseTail = 5, PoseNose = 6, PoseCount = 7;
    readonly Pose[] _presets = new Pose[PoseCount];
    readonly Vector2[] _presetCom = new Vector2[PoseCount];
    readonly float[] _presetInertia = new float[PoseCount];
    Vector2 _boardComLocal;
    float _boardMass, _boardInertia, _airInertia;
    // Posture courante, qui rejoint la posture visée en ressort critique (dessin, centre de gravité et inertie ensemble), et
    // vitesses de ce ressort ; 'fast' = posture « vite » choisie (avec marge).
    Pose _pose, _poseVel;
    Vector2 _riderCom, _riderComVel;
    float _riderInertia, _riderInertiaVel, _poseK, _poseC;
    bool _fast;
    // Centre de gravité commun (repère de la planche, sens de marche appliqué), celui de la posture visée, et l'inertie de
    // l'ensemble autour de lui (kg·m²).
    Vector2 _centerLocal, _targetCenterLocal;
    float _centerInertia;
    // Bascule voulue (lissée) et compensation du corps.
    float _lean, _tilt;
    // Corps sur les jambes : hanches en monde (position, vitesse) ; décalage affiché (repère de la planche) et vitesse relative.
    Vector2 _hipPos, _hipVel, _offset, _relVel, _lastBoardVel;
    // État de la planche lu une fois par pas, et constantes préparées au départ (Prepare).
    Vector2 _boardVel, _boardCom, _gravity;
    float _boardOmega, _gravityMagnitude, _dt, _legK, _legC, _leanK, _leanC, _armK, _armC, _airK, _rotK;
    // Rotation suivie par le corps (rad/s) : celle de la planche, lissée (voir bodyRotationFollow).
    float _bodyOmega;
    bool _hipValid;
    float _armSwing, _armSwingVel;
    int _noseSteps, _tailSteps, _stuckSteps;
    // Perte d'équilibre : pied pivot (repère de la planche), angle relatif à la planche, vitesse de rotation en monde (rad/s).
    Vector2 _fallPivot;
    float _fallAngle, _fallOmega;
    int _fallSide;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public bool IsBroken => _mode == Mode.Ragdoll;
    // La planche obéit aux commandes tant qu'il tient dessus.
    public bool Controllable => _mode == Mode.Riding;
    public bool HasPose => _ready || _mode == Mode.Ragdoll;
    // Centre commun connu (sur la planche ou figé pendant une perte d'équilibre) : centre de la caméra.
    internal bool HasCenter => _ready && _mode != Mode.Ragdoll;
    // Centre de rotation en l'air (repère de la planche) : centre de gravité commun, ou celui de la planche si décoché.
    internal Vector2 PivotLocal => spinAroundCommonCenter && _ready ? _centerLocal : _board.CenterOfMass;
    // Rotation de son corps (rad/s) : celle de la planche, suivie avec retard au sol (voir bodyRotationFollow).
    internal float BodyOmega => _bodyOmega;

    // Décollage : le centre commun part avec la planche. Au sol le personnage ne pèse pas, son mouvement sur la planche n'a donc
    // pas d'élan à transmettre : son glissement repart de zéro (sinon il ajouterait ≈ 1 m/s au saut).
    internal void StopCenterSlide()
    {
        _riderComVel = Vector2.zero;
        _riderInertiaVel = 0f;
    }
    internal bool SpinsAroundCenter => spinAroundCommonCenter && _ready && _mode == Mode.Riding;
    internal float CenterInertia => _centerInertia;
    // Vitesse de rotation possible en l'air, relative à la posture « en l'air » (inertie de référence / inertie actuelle).
    public float AirSpinFactor => _mode == Mode.Riding && _ready ? InertiaFactor(_airInertia, _centerInertia) : 1f;

    // Rapport d'inertie 'from' / 'to' (conservation de l'élan de rotation), atténué par 'inertiaEffect'.
    internal float InertiaFactor(float from, float to)
    {
        if (inertiaEffect <= 0f || to <= 0f) return 1f;
        float r = from / to;
        return inertiaEffect >= 1f ? r : Mathf.Pow(r, inertiaEffect);
    }

    float Len(float fraction) => fraction * height;
    float Spacing => Mathf.Min(stance * _board.Wheelbase, maxStance * height);
    float LimbRadius => 0.5f * limbThickness;
    float HeadRadius => 0.5f * Len(headDiameter);

    public Vector2 CameraPosition
    {
        get
        {
            if (_mode == Mode.Ragdoll) return _bodies[Torso].position;
            return _board.Body.transform.TransformPoint(_posePos[Torso]);
        }
    }

    internal Vector2 CameraVelocity => _mode == Mode.Ragdoll ? _bodies[Torso].linearVelocity : _board.Body.linearVelocity;

    // ---------------------------------------------------------------- Mode entraînement, effets

    public CapsuleGeometry GhostShapeOf(int index) => LimbGeometry(index);

    // Forme d'un membre dans son repère (origine = articulation proche du torse).
    CapsuleGeometry LimbGeometry(int index)
    {
        if (index == Head)
            return new CapsuleGeometry { center1 = new Vector2(0f, HeadRadius), center2 = new Vector2(0f, HeadRadius), radius = HeadRadius };
        float l = SegmentLength(index);
        return new CapsuleGeometry { center1 = Vector2.zero, center2 = new Vector2(0f, index == Torso ? l : -l), radius = LimbRadius };
    }

    internal void AppendBodyStates(List<BodyState> states)
    {
        var board = _board.Body;
        var t = board.transform;
        for (int i = 0; i < LimbCount; i++)
        {
            if (_mode == Mode.Ragdoll)
            {
                var b = _bodies[i];
                var bt = b.transform;
                states.Add(new BodyState { position = bt.position, angle = bt.rotation.radians, velocity = b.linearVelocity, angularVelocity = b.angularVelocity });
                continue;
            }
            Vector2 p = t.TransformPoint(_posePos[i]);
            states.Add(new BodyState { position = p, angle = t.rotation.radians + _poseAngle[i], velocity = board.GetWorldPointVelocity(p), angularVelocity = board.angularVelocity });
        }
    }

    // Replace le personnage (mode entraînement) : debout sur la planche, ou ragdoll recréé aux positions mémorisées si 'broken'.
    internal void RestoreBodyStates(IReadOnlyList<BodyState> states, int offset, bool broken)
    {
        DestroyRagdoll();
        ResetState();
        Init();
        if (!broken) return;
        for (int i = 0; i < LimbCount; i++) _spawn[i] = states[offset + i];
        Kill(default, false, true);
    }

    internal void AppendSurfaceContacts(List<SurfaceContact> result)
    {
        if (_mode != Mode.Ragdoll) return;
        for (int i = 0; i < LimbCount; i++) _board.AppendSurfaceContacts(_bodies[i], result, false);
    }

    // ---------------------------------------------------------------- Création

    void OnEnable()
    {
        _board = GetComponent<Skateboard>();
        if (!Application.isPlaying) return;
        _board.SetRider(this);
        ResetState();
        _sceneryFilter = PhysicsQuery.QueryFilter.defaultFilter;
        _sceneryFilter.hitCategories = new PhysicsMask { bitMask = ~(1UL << Skateboard.CategoryBit) };
        _hazardFilter = PhysicsQuery.QueryFilter.defaultFilter;
        _hazardFilter.hitCategories = new PhysicsMask { bitMask = 1UL << BlobHazard.CategoryBit };
        Prepare();
        if (_board.Created) Init();
    }

    // Constantes du pas (une modification d'inspecteur en Play s'applique au redémarrage suivant).
    void Prepare()
    {
        _dt = Time.fixedDeltaTime;
        _gravity = PhysicsWorld.defaultWorld.gravity;
        _gravityMagnitude = _gravity.magnitude;
        float wl = 2f * Mathf.PI * legFrequency, wt = 2f * Mathf.PI * leanFrequency, wa = 2f * Mathf.PI * armFrequency;
        _legK = wl * wl; _legC = 2f * legDamping * wl;
        _leanK = wt * wt; _leanC = 2f * leanDamping * wt;
        _armK = wa * wa; _armC = 2f * armDamping * wa;
        _airK = 1f - Mathf.Exp(-airFollow * _dt);
        _rotK = 1f - Mathf.Exp(-bodyRotationFollow * _dt);
        // Ressort critique : ≈ 95 % du chemin en 4,74 / ω0.
        float wp = 4.74f / poseTime;
        _poseK = wp * wp; _poseC = 2f * wp;
    }

    void OnDisable()
    {
        if (_board != null) _board.SetRider(null);
        DestroyRagdoll();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void OnDestroy() => DestroyRagdoll();

    void ResetState()
    {
        _mode = Mode.Riding;
        _ready = false;
        _poseValid = false;
        _hipValid = false;
        _lean = _tilt = 0f;
        _poseVel = default;
        _riderComVel = Vector2.zero;
        _riderInertiaVel = 0f;
        _fast = false;
        _offset = _relVel = Vector2.zero;
        _armSwing = _armSwingVel = 0f;
        _noseSteps = _tailSteps = _stuckSteps = 0;
        _fallAngle = _fallOmega = 0f;
        System.Array.Clear(_hazardTouches, 0, LimbCount);
        System.Array.Clear(_attached, 0, LimbCount);
    }

    // Posture de départ (debout, hanches au repos, même vitesse que la planche).
    void Init()
    {
        var board = _board.Body;
        var xf = board.transform;
        PreparePresets();
        _pose = slowPose;
        _riderCom = _presetCom[PoseSlow];
        _riderInertia = _presetInertia[PoseSlow];
        Center(_board.Lead, _riderCom, _riderInertia, out _centerLocal, out _centerInertia);
        _targetCenterLocal = _centerLocal;
        Vector2 hip = IntentHip(_board.Lead * slowPose.hipShift * 0.5f * Spacing, slowPose.crouch, 0f);
        SolvePose(hip, -slowPose.torsoLean * Mathf.Deg2Rad * _board.Lead, slowPose.arms, elbowBend);
        _hipPos = xf.TransformPoint(hip);
        _hipVel = board.GetWorldPointVelocity(_hipPos);
        _lastBoardVel = board.linearVelocity;
        _bodyOmega = board.angularVelocity * Mathf.Deg2Rad;
        _hipValid = true;
        _ready = true;
    }

    void DestroyRagdoll()
    {
        if (!_bodies.IsCreated) return;
        for (int i = 0; i < LimbCount; i++)
            if (_bodies[i].isValid) _bodies[i].Destroy();
        _bodies.Dispose();
        _shapes.Clear();
        System.Array.Clear(_joints, 0, JointCount);
    }

    float SegmentLength(int limb)
    {
        switch (limb)
        {
            case Torso: return Len(torsoLength);
            case ThighF: case ThighB: return Len(thighLength);
            case ShinF: case ShinB: return Len(shinLength);
            case UpperF: case UpperB: return Len(upperArmLength);
            case ForeF: case ForeB: return Len(forearmLength);
        }
        return 0f;
    }

    // ---------------------------------------------------------------- Simulation

    public void LevelTick()
    {
        if (_board == null || !_board.Created) return;
        if (!_ready && _mode != Mode.Ragdoll) Init();
        if (_mode == Mode.Ragdoll)
        {
            if (LevelSimulation.Step > 0) ReadRagdollEvents();
            ImpaleTouching();
            return;
        }
        if (_mode == Mode.Falling) Fall();
        else Ride();
    }

    void Ride()
    {
        float dt = _dt;
        var board = _board.Body;
        var xf = board.transform;
        _boardVel = board.linearVelocity;
        _boardOmega = board.angularVelocity * Mathf.Deg2Rad;
        _boardCom = board.worldCenterOfMass;
        bool touching = _board.Grounded, air = _board.InAir;

        float slow = stuckSpeed * stuckSpeed;
        _stuckSteps = touching || _boardVel.sqrMagnitude > slow ? 0 : _stuckSteps + 1;
        if (stuckTime > 0f && _stuckSteps * dt >= stuckTime) { Kill(CameraPosition, true, false); return; }
        if (_board.HazardHit) { Kill(_board.HazardPoint, true, false); return; }

        // Wheelie tenu, bout au sol : perte d'équilibre (si autorisée) du côté qui touche.
        int intent = _board.Intent, lead = _board.Lead, end = _board.EndTouch;
        _noseSteps = intent < 0 && end == lead ? _noseSteps + 1 : 0;
        _tailSteps = intent > 0 && end == -lead ? _tailSteps + 1 : 0;

        // Bascule voulue : corps droit par rapport au sol (jusqu'à une limite : au-delà, il bascule avec la planche). Sans flèche,
        // il suit la planche.
        _lean = Mathf.MoveTowards(_lean, air ? 0f : intent, dt / poseTime);
        float lean = Mathf.Abs(_lean);
        float maxComp = maxCounterTilt * Mathf.Deg2Rad;
        float comp = air ? 0f : Mathf.Clamp(_board.Tilt * counterTilt * lean, -maxComp, maxComp);
        _tilt = Mathf.MoveTowards(_tilt, comp, 2f * _board.groundTiltSpeed * Mathf.Deg2Rad * dt);

        UpdatePose(air, intent, lead);
        float torso = _pose.torsoLean, arms = _pose.arms;
        Vector2 anchor = IntentHip(lead * _pose.hipShift * 0.5f * Spacing, _pose.crouch, _tilt);

        float pull = BodyOnLegs(xf, anchor, touching, air, dt);
        if (_mode != Mode.Riding) return;

        // Bras ballants : ressort vers la posture voulue, secoués par le corps (en avant quand la planche le retient).
        _armSwingVel += (-_armK * _armSwing - _armC * _armSwingVel - armSwing * Mathf.Deg2Rad * pull) * dt;
        _armSwing = Mathf.Clamp(_armSwing + _armSwingVel * dt, -1.5f, 1.5f);

        float pitch = maxLean > 0f ? -leanTorsoPitch * Mathf.Deg2Rad * _offset.x / Len(maxLean) : 0f;
        SolvePose(anchor + _offset, -lead * torso * Mathf.Deg2Rad - _tilt + pitch, arms, elbowBend, _armSwing);

        if (canFallForward && noseFallTime > 0f && _noseSteps * dt >= noseFallTime) StartFall(lead);
        else if (canFallBackward && tailFallTime > 0f && _tailSteps * dt >= tailFallTime) StartFall(-lead);
        CheckHitbox(xf);
    }

    // Vitesse d'un point de la planche, depuis son état lu une fois par pas (un appel au moteur par point coûte plus cher).
    Vector2 BoardPointVelocity(Vector2 p) => _boardVel + _boardOmega * new Vector2(_boardCom.y - p.y, p.x - _boardCom.x);

    // Masse du corps (hanches) sur ses jambes, en monde : gravité + ressort-amortisseur vers la posture voulue (dans l'axe de la
    // planche et dans l'axe des jambes, avec la précharge qui porte le poids quand la planche touche le sol). Le long de la planche,
    // les pieds ne retiennent le corps que par frottement ('footGrip' × charge). La planche, elle, n'en sait rien.
    // Butées : jambes au maximum de flexion, hanches au maximum le long de la planche ; y arriver trop vite = chute.
    // Rend l'accélération imposée au corps le long de la planche (pour les bras).
    float BodyOnLegs(PhysicsTransform xf, Vector2 anchorLocal, bool touching, bool air, float dt)
    {
        Vector2 axis = new Vector2(xf.rotation.cos, xf.rotation.sin);
        Vector2 nrm = new Vector2(-axis.y, axis.x);
        Vector2 anchor = xf.TransformPoint(anchorLocal);
        Vector2 vA = BoardPointVelocity(anchor);
        Vector2 gravity = _gravity;
        Vector2 boardVel = _boardVel;
        if (!_hipValid) { _hipPos = anchor; _hipVel = vA; _lastBoardVel = boardVel; _hipValid = true; }

        // Saut : c'est lui qui saute, son corps part avec la planche. En l'air, la planche qui tourne autour du centre commun change
        // de vitesse à chaque pas : ce n'est pas un choc.
        Vector2 kick = boardVel - _lastBoardVel - gravity * dt - _board.AirSteerVelocity;
        _lastBoardVel = boardVel;
        if (_board.JumpedThisStep)
        {
            _hipVel += _board.JumpVelocity;
            kick -= _board.JumpVelocity;
        }
        // Écart de la planche à une chute libre : un contact l'a freinée ou poussée (réception, obstacle), même si Box2D ne
        // montre pas encore de contact (collision continue à grande vitesse : la planche est arrêtée sans contact ce pas-là).
        bool impact = kick.sqrMagnitude > airImpactSpeed * airImpactSpeed;

        float boardOmega = _boardOmega;
        // En l'air, il se tient à la planche (la rotation en l'air est une commande, pas de la physique).
        if (air && !impact)
        {
            _offset -= _offset * _airK;
            _relVel = Vector2.zero;
            _hipPos = anchor + _offset.x * axis + _offset.y * nrm;
            _hipVel = vA;
            _bodyOmega = boardOmega;
            return 0f;
        }
        // Choc en l'air : son corps garde la vitesse d'avant le choc (celle du pas précédent, la gravité est ajoutée plus bas),
        // pas celle d'un point de la planche que le choc vient peut-être de faire tourner.
        bool loaded = touching || impact;

        // Vitesse de référence du corps : celle des pieds, plus la rotation SUIVIE par le corps (pas celle de la planche : quand
        // elle se rabat brutalement, les chevilles pivotent, le corps ne balaie pas avec elle).
        _bodyOmega += (boardOmega - _bodyOmega) * _rotK;
        Vector2 feet = xf.TransformPoint(new Vector2(0f, _board.DeckTop + footHeight));
        Vector2 arm = anchor - feet;
        Vector2 vRef = BoardPointVelocity(feet) + _bodyOmega * new Vector2(-arm.y, arm.x);

        Vector2 rel = _hipPos - anchor, rv = _hipVel - vRef;
        float dx = Vector2.Dot(rel, axis), dy = Vector2.Dot(rel, nrm);
        float ux = Vector2.Dot(rv, axis), uy = Vector2.Dot(rv, nrm);
        float ay = -_legK * dy - _legC * uy + (loaded ? _gravityMagnitude : 0f);
        float grip = footGrip * Mathf.Max(0f, ay);
        float ax = Mathf.Clamp(-_leanK * dx - _leanC * ux, -grip, grip);
        _hipVel += (gravity + ax * axis + ay * nrm) * dt;

        rv = _hipVel - vRef;
        ux = Vector2.Dot(rv, axis);
        uy = Vector2.Dot(rv, nrm);
        // Position relative au pas suivant (le point d'accroche suit la planche).
        Vector2 next = rel + (_hipVel - vA) * dt;
        dx = Vector2.Dot(next, axis);
        dy = Vector2.Dot(next, nrm);
        float compression = Len(maxCompression), extension = 0.02f, leanMax = Len(maxLean);
        _relVel = new Vector2(ux, uy);
        bool safe = safeLaunchers && _board.TouchingKinematic;
        if (dy < -compression)
        {
            if (-uy > landingCrashSpeed && !safe) { Crash(); return 0f; }
            dy = -compression;
            uy = Mathf.Max(uy, 0f);
        }
        else if (dy > extension)
        {
            dy = extension;
            uy = Mathf.Min(uy, 0f);
        }
        if (Mathf.Abs(dx) > leanMax)
        {
            if (Mathf.Abs(ux) > impactCrashSpeed && !safe) { Crash(); return 0f; }
            dx = Mathf.Sign(dx) * leanMax;
            if (ux * dx > 0f) ux = 0f;
        }
        _hipVel = vRef + ux * axis + uy * nrm;
        _hipPos = anchor + vA * dt + dx * axis + dy * nrm;
        _offset = new Vector2(dx, dy);
        _relVel = new Vector2(ux, uy);
        return ax;
    }

    // Choc trop fort : ragdoll qui part avec la vitesse de son corps.
    void Crash() => Kill(CameraPosition, true, false);

    // Perte d'équilibre : il bascule autour du pied du côté 'side' (repère de la planche, +1 = vers +X) ; la planche est libre.
    void StartFall(int side)
    {
        _mode = Mode.Falling;
        _fallSide = side;
        _fallPivot = _board.FootPoint(side, Spacing, footHeight);
        _fallAngle = 0f;
        _fallOmega = _board.Body.angularVelocity * Mathf.Deg2Rad - side * fallStartSpeed * Mathf.Deg2Rad;
        CapturePose();
    }

    readonly Vector2[] _fallBasePos = new Vector2[LimbCount];
    readonly float[] _fallBaseAngle = new float[LimbCount];
    Vector2 _fallCom;

    void CapturePose()
    {
        System.Array.Copy(_posePos, _fallBasePos, LimbCount);
        System.Array.Copy(_poseAngle, _fallBaseAngle, LimbCount);
        _fallCom = PoseCenterOfMass();
    }

    // Pendule inversé autour du pied pivot (le corps tourne d'un bloc, l'autre pied quitte la planche), sous la gravité.
    void Fall()
    {
        float dt = _dt;
        var board = _board.Body;
        var xf = board.transform;
        Vector2 pivot = xf.TransformPoint(_fallPivot);
        float fc = Mathf.Cos(_fallAngle), fs = Mathf.Sin(_fallAngle);
        Vector2 local = _fallCom - _fallPivot;
        Vector2 com = xf.TransformPoint(_fallPivot + new Vector2(fc * local.x - fs * local.y, fs * local.x + fc * local.y));
        Vector2 r = com - pivot;
        Vector2 g = _gravity;
        float l2 = Mathf.Max(1e-4f, r.sqrMagnitude);
        _fallOmega += (r.x * g.y - r.y * g.x) / l2 * dt;
        _fallAngle += (_fallOmega - board.angularVelocity * Mathf.Deg2Rad) * dt;
        ApplyFallPose();
        if (Mathf.Abs(_fallAngle) >= fallDetachAngle * Mathf.Deg2Rad) { Kill(CameraPosition, true, false); return; }
        CheckHitbox(xf);
    }

    // Corps figé à la perte d'équilibre, tourné autour du pied pivot ; les bras partent vers l'avant de la chute (se rattraper).
    void ApplyFallPose()
    {
        _poseValid = false;
        float reach = Mathf.Clamp01(Mathf.Abs(_fallAngle) / (0.5f * fallDetachAngle * Mathf.Deg2Rad));
        float target = _fallSide * 70f * Mathf.Deg2Rad, elbow = elbowBend * Mathf.Deg2Rad, upper = Len(upperArmLength);
        float uF = Mathf.Lerp(_fallBaseAngle[UpperF], target, reach), uB = Mathf.Lerp(_fallBaseAngle[UpperB], target, reach);
        Vector2 shoulder = _fallBasePos[UpperF];
        float fc = Mathf.Cos(_fallAngle), fs = Mathf.Sin(_fallAngle);
        for (int i = 0; i < LimbCount; i++)
        {
            Vector2 p = _fallBasePos[i];
            float a = _fallBaseAngle[i];
            switch (i)
            {
                case UpperF: a = uF; break;
                case UpperB: a = uB; break;
                case ForeF: p = shoulder + upper * Down(uF); a = uF + elbow; break;
                case ForeB: p = shoulder + upper * Down(uB); a = uB + elbow; break;
            }
            Vector2 d = p - _fallPivot;
            _posePos[i] = _fallPivot + new Vector2(fc * d.x - fs * d.y, fs * d.x + fc * d.y);
            _poseAngle[i] = a + _fallAngle;
        }
        UpdateLocalLimbs();
    }

    // Centre de gravité du personnage dans le repère de la planche (posture actuelle).
    Vector2 PoseCenterOfMass()
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < LimbCount; i++) sum += MassShare[i] * 0.5f * (_localLimbs[i].center1 + _localLimbs[i].center2);
        return sum;
    }

    // Inertie de la posture actuelle autour de 'center', par kg du personnage (segments : barres ; tête : disque).
    float PoseInertia(Vector2 center)
    {
        float sum = 0f;
        for (int i = 0; i < LimbCount; i++)
        {
            var g = _localLimbs[i];
            Vector2 mid = 0.5f * (g.center1 + g.center2);
            float own = i == Head ? 0.5f * g.radius * g.radius : (g.center2 - g.center1).sqrMagnitude / 12f;
            sum += MassShare[i] * ((mid - center).sqrMagnitude + own);
        }
        return sum;
    }

    // Presets : posture figée (sans le corps sur ses jambes, sens de marche +X), son centre de gravité et son inertie. Une fois
    // par départ : rien n'est recalculé sur les membres en mouvement.
    void PreparePresets()
    {
        _presets[PoseSlow] = slowPose; _presets[PoseFast] = fastPose; _presets[PoseCharge] = chargePose;
        _presets[PoseAir] = airPose; _presets[PoseGrab] = grabPose; _presets[PoseTail] = tailPose; _presets[PoseNose] = nosePose;
        for (int i = 0; i < PoseCount; i++)
        {
            var p = _presets[i];
            _poseValid = false;
            SolvePose(IntentHip(p.hipShift * 0.5f * Spacing, p.crouch, 0f), -p.torsoLean * Mathf.Deg2Rad, p.arms, elbowBend);
            _presetCom[i] = PoseCenterOfMass();
            _presetInertia[i] = PoseInertia(_presetCom[i]);
        }
        _poseValid = false;
        var body = _board.Body;
        _boardMass = body.mass;
        _boardInertia = body.massConfiguration.rotationalInertia;
        _boardComLocal = _board.CenterOfMass;
        Center(1, _presetCom[PoseAir], _presetInertia[PoseAir], out _, out _airInertia);
    }

    // Centre de gravité commun (planche + personnage, repère de la planche) et inertie de l'ensemble autour de lui, pour un
    // centre du personnage donné en sens de marche +X.
    void Center(int lead, Vector2 riderCom, float riderInertia, out Vector2 center, out float inertia)
    {
        Vector2 cr = new Vector2(lead * riderCom.x, riderCom.y);
        float mb = _boardMass, mr = bodyMass;
        center = (mb * _boardComLocal + mr * cr) / (mb + mr);
        inertia = _boardInertia + mb * (_boardComLocal - center).sqrMagnitude + mr * (riderInertia + (cr - center).sqrMagnitude);
    }

    // Posture visée selon la situation (preset ; la charge du saut s'y mélange), rejointe en ressort critique : dessin, centre de
    // gravité et inertie avancent ensemble, sans à-coup (le centre fait tourner la planche en l'air).
    void UpdatePose(bool air, int intent, int lead)
    {
        int p;
        if (air) p = _board.GrabHeld ? PoseGrab : PoseAir;
        else if (intent > 0) p = PoseTail;
        else if (intent < 0) p = PoseNose;
        else
        {
            float speed2 = _boardVel.sqrMagnitude, exit = Mathf.Max(0f, fastPoseSpeed - fastPoseMargin);
            if (_fast ? speed2 < exit * exit : speed2 > fastPoseSpeed * fastPoseSpeed) _fast = !_fast;
            p = _fast ? PoseFast : PoseSlow;
        }
        Pose target = _presets[p];
        Vector2 com = _presetCom[p];
        float inertia = _presetInertia[p];
        float charge = air ? 0f : _board.JumpCharge;
        if (charge > 0f)
        {
            target = Pose.Lerp(target, _presets[PoseCharge], charge);
            com = Vector2.Lerp(com, _presetCom[PoseCharge], charge);
            inertia = Mathf.Lerp(inertia, _presetInertia[PoseCharge], charge);
        }
        _pose.crouch = Follow(_pose.crouch, ref _poseVel.crouch, target.crouch);
        _pose.torsoLean = Follow(_pose.torsoLean, ref _poseVel.torsoLean, target.torsoLean);
        _pose.arms = Follow(_pose.arms, ref _poseVel.arms, target.arms);
        _pose.hipShift = Follow(_pose.hipShift, ref _poseVel.hipShift, target.hipShift);
        _riderCom.x = Follow(_riderCom.x, ref _riderComVel.x, com.x);
        _riderCom.y = Follow(_riderCom.y, ref _riderComVel.y, com.y);
        _riderInertia = Follow(_riderInertia, ref _riderInertiaVel, inertia);
        Center(lead, _riderCom, _riderInertia, out _centerLocal, out _centerInertia);
        Center(lead, com, inertia, out _targetCenterLocal, out _);
    }

    float Follow(float x, ref float v, float target)
    {
        v += (_poseK * (target - x) - _poseC * v) * _dt;
        return x + v * _dt;
    }

    // ---------------------------------------------------------------- Posture

    // Hanches voulues (repère de la planche) : 'hipGroundX' = position le long du sol (repère tourné de -tiltComp), 'crouch' =
    // extension de la jambe du pied le plus haut.
    Vector2 IntentHip(float hipGroundX, float crouch, float tiltComp)
    {
        float lt = Len(thighLength), ls = Len(shinLength);
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
        return p0 + Mathf.Min(tF, tB) * groundUp;
    }

    // Posture complète dans le repère de la planche : hanches ramenées à portée des deux pieds, jambes par cinématique inverse,
    // torse ('torsoAngle' en radians depuis la normale de la planche), bras et coudes en degrés.
    // Entrées de la dernière posture calculée : recalcul sauté si rien n'a bougé de plus de PoseTolerance (roulage stable, une
    // fois les ressorts posés). Décision prise sur des valeurs de la simulation : déterministe.
    readonly float[] _poseIn = new float[6];
    bool _poseValid;
    const float PoseTolerance = 1e-4f;

    bool PoseUnchanged(Vector2 hip, float torsoAngle, float armDeg, float elbowDeg, float armSwingRad)
    {
        if (_poseValid && Mathf.Abs(hip.x - _poseIn[0]) < PoseTolerance && Mathf.Abs(hip.y - _poseIn[1]) < PoseTolerance &&
            Mathf.Abs(torsoAngle - _poseIn[2]) < PoseTolerance && Mathf.Abs(armDeg - _poseIn[3]) < 0.01f &&
            Mathf.Abs(elbowDeg - _poseIn[4]) < 0.01f && Mathf.Abs(armSwingRad - _poseIn[5]) < PoseTolerance) return true;
        _poseValid = true;
        _poseIn[0] = hip.x; _poseIn[1] = hip.y; _poseIn[2] = torsoAngle; _poseIn[3] = armDeg; _poseIn[4] = elbowDeg; _poseIn[5] = armSwingRad;
        return false;
    }

    void SolvePose(Vector2 hip, float torsoAngle, float armDeg, float elbowDeg, float armSwingRad = 0f)
    {
        if (PoseUnchanged(hip, torsoAngle, armDeg, elbowDeg, armSwingRad)) return;
        float lt = Len(thighLength), ls = Len(shinLength), lto = Len(torsoLength);
        float spacing = Spacing;
        Vector2 ankleF = _board.FootPoint(1, spacing, footHeight);
        Vector2 ankleB = _board.FootPoint(-1, spacing, footHeight);
        float full = 0.995f * (lt + ls);
        for (int pass = 0; pass < 2; pass++)
        {
            hip = Reach(hip, ankleF, full);
            hip = Reach(hip, ankleB, full);
        }

        Leg(hip, ankleF, 1, lt, ls, out var kneeF, out float thighF, out float shinF);
        Leg(hip, ankleB, -1, lt, ls, out var kneeB, out float thighB, out float shinB);

        var torsoUp = new Vector2(-Mathf.Sin(torsoAngle), Mathf.Cos(torsoAngle));
        Vector2 neck = hip + lto * torsoUp;
        Vector2 shoulder = hip + (lto - Len(shoulderDrop)) * torsoUp;
        float arm = armDeg * Mathf.Deg2Rad, elbow = elbowDeg * Mathf.Deg2Rad;
        float upperF = torsoAngle + arm + armSwingRad, upperB = torsoAngle - arm + armSwingRad;

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
        UpdateLocalLimbs();
    }

    void UpdateLocalLimbs()
    {
        for (int i = 0; i < LimbCount; i++)
        {
            var g = LimbGeometry(i);
            float a = _poseAngle[i], c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 p = _posePos[i];
            g.center1 = p + new Vector2(c * g.center1.x - s * g.center1.y, s * g.center1.x + c * g.center1.y);
            g.center2 = p + new Vector2(c * g.center2.x - s * g.center2.y, s * g.center2.x + c * g.center2.y);
            _localLimbs[i] = g;
        }
    }

    static Vector2 Reach(Vector2 hip, Vector2 ankle, float max)
    {
        Vector2 d = hip - ankle;
        float l = d.magnitude;
        return l > max ? ankle + d * (max / l) : hip;
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

    // ---------------------------------------------------------------- Hitbox et chute

    CapsuleGeometry WorldLimb(Vector2 origin, float cos, float sin, int i)
    {
        var g = _localLimbs[i];
        Vector2 a = g.center1, b = g.center2;
        g.center1 = origin + new Vector2(cos * a.x - sin * a.y, sin * a.x + cos * a.y);
        g.center2 = origin + new Vector2(cos * b.x - sin * b.y, sin * b.x + cos * b.y);
        return g;
    }

    // Tête et torse contre le décor (hors skateur et hors verre) ; tous les membres contre les obstacles, après un test de
    // boîte englobante bon marché. Seul le « touché ou non » compte : indépendant de l'ordre des résultats.
    void CheckHitbox(PhysicsTransform xf)
    {
        var world = PhysicsWorld.defaultWorld;
        Vector2 origin = xf.position;
        float cos = xf.rotation.cos, sin = xf.rotation.sin;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < LimbCount; i++)
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

        if (!world.TestOverlapAABB(new PhysicsAABB { lowerBound = min, upperBound = max }, _hazardFilter)) return;
        for (int i = 0; i < LimbCount; i++)
        {
            var g = _worldLimbs[i];
            bool hit = i == Head ? world.TestOverlapGeometry(new CircleGeometry { center = g.center1, radius = g.radius }, _hazardFilter)
                                 : world.TestOverlapGeometry(g, _hazardFilter);
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

    // États de départ des membres du ragdoll (monde) : posture actuelle, ou état mémorisé (mode entraînement).
    readonly BodyState[] _spawn = new BodyState[LimbCount];

    // Chute : le personnage devient un ragdoll (membres dynamiques reliés par des charnières libres) qui part avec la vitesse de
    // son corps : celle de la planche plus son mouvement propre (corps qui continuait sur sa lancée, bascule en cours).
    void Kill(Vector2 point, bool notify, bool fromStates)
    {
        if (!fromStates)
        {
            var board = _board.Body;
            var xf = board.transform;
            Vector2 pivot = xf.TransformPoint(_fallPivot);
            Vector2 pivotVelocity = board.GetWorldPointVelocity(pivot);
            Vector2 hip = xf.TransformPoint(_posePos[Torso]);
            for (int i = 0; i < LimbCount; i++)
            {
                Vector2 p = xf.TransformPoint(_posePos[i]);
                Vector2 v;
                float w;
                if (_mode == Mode.Falling)
                {
                    Vector2 r = p - pivot;
                    v = pivotVelocity + _fallOmega * new Vector2(-r.y, r.x);
                    w = _fallOmega * Mathf.Rad2Deg;
                }
                else
                {
                    // Son corps : vitesse des hanches et rotation suivie (pas forcément celle de la planche).
                    Vector2 r = p - hip;
                    v = _hipVel + _bodyOmega * new Vector2(-r.y, r.x);
                    w = _bodyOmega * Mathf.Rad2Deg;
                }
                _spawn[i] = new BodyState { position = p, angle = xf.rotation.radians + _poseAngle[i], velocity = v, angularVelocity = w };
            }
        }
        _mode = Mode.Ragdoll;
        CreateRagdoll();
        if (notify) Died?.Invoke(this, point);
    }

    void CreateRagdoll()
    {
        var world = PhysicsWorld.defaultWorld;
        _bodies = new NativeArray<PhysicsBody>(LimbCount, Allocator.Persistent);
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var filter = shapeDef.contactFilter;
        filter.groupIndex = -2; // négatif : la planche et le ragdoll d'un même skateur ne collisionnent pas entre eux
        filter.categories = new PhysicsMask { bitMask = filter.categories.bitMask | (1UL << Skateboard.CategoryBit) };
        shapeDef.contactFilter = filter;
        Skateboard.SetMaterial(ref shapeDef, 0.6f, 0f, riderColor);
        for (int i = 0; i < LimbCount; i++)
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
            _shapes.Add(shape);
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

    // Obstacles touchés par les membres du ragdoll (comptés par membre, pour l'empalement). Indépendant de l'ordre des événements.
    void ReadRagdollEvents()
    {
        var world = PhysicsWorld.defaultWorld;
        foreach (var e in world.contactBeginEvents)
        {
            int i = LimbOf(e.shapeA, e.shapeB, out var other);
            if (i < 0 || !other.isValid || !(other.userData.objectValue is BlobHazard)) continue;
            _hazardTouches[i]++;
        }
        foreach (var e in world.contactEndEvents)
        {
            int i = LimbOf(e.shapeA, e.shapeB, out var other);
            if (i < 0 || !other.isValid || !(other.userData.objectValue is BlobHazard)) continue;
            _hazardTouches[i] = Mathf.Max(0, _hazardTouches[i] - 1);
        }
    }

    int LimbOf(PhysicsShape a, PhysicsShape b, out PhysicsShape other)
    {
        for (int i = 0; i < _shapes.Count; i++)
        {
            if (_shapes[i] == a) { other = b; return i; }
            if (_shapes[i] == b) { other = a; return i; }
        }
        other = default;
        return -1;
    }

    struct HazardContact
    {
        public BlobHazard hazard;
        public PhysicsShape shape;
        public Vector2 point;
    }

    HazardContact[] _hazardContacts = new HazardContact[8];

    // Après la chute, chaque membre qui touche un obstacle le lui signale (piques : empalement), ordre fixe.
    void ImpaleTouching()
    {
        for (int i = 0; i < LimbCount; i++)
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

    // Personnage sur la planche : dessiné à chaque image (rendu debug, rien dans la simulation). En ragdoll, il est dessiné par
    // le rendu debug de Box2D comme les autres corps.
    void LateUpdate()
    {
        if (!Application.isPlaying || _board == null || !_board.Created || !_ready || _mode == Mode.Ragdoll) return;
        var world = PhysicsWorld.defaultWorld;
        var xf = _board.Body.transform;
        Vector2 origin = xf.position;
        float cos = xf.rotation.cos, sin = xf.rotation.sin;
        const PhysicsWorld.DrawFillOptions fill = PhysicsWorld.DrawFillOptions.Interior | PhysicsWorld.DrawFillOptions.Outline;
        for (int i = 0; i < LimbCount; i++)
        {
            var g = WorldLimb(origin, cos, sin, i);
            if (i == Head) world.DrawGeometry(new CircleGeometry { center = g.center1, radius = g.radius }, PhysicsTransform.identity, riderColor, 0f, fill);
            else world.DrawGeometry(g, PhysicsTransform.identity, riderColor, 0f, fill);
        }
        if (drawCenterOfMass) DrawCenter(world, xf);
    }

    // Centre de gravité commun : rond barré qui tourne avec la planche ; petit rond = centre de la posture visée (transition).
    void DrawCenter(PhysicsWorld world, PhysicsTransform xf)
    {
        const float r = 0.07f;
        const PhysicsWorld.DrawFillOptions outline = PhysicsWorld.DrawFillOptions.Outline;
        Vector2 c = xf.TransformPoint(_centerLocal), t = xf.TransformPoint(_targetCenterLocal);
        Vector2 ax = r * new Vector2(xf.rotation.cos, xf.rotation.sin), ay = new Vector2(-ax.y, ax.x);
        world.DrawGeometry(new CircleGeometry { center = c, radius = r }, PhysicsTransform.identity, centerColor, 0f, outline);
        world.DrawLine(c - ax, c + ax, centerColor, 0f);
        world.DrawLine(c - ay, c + ay, centerColor, 0f);
        if ((t - c).sqrMagnitude > 1e-6f)
        {
            var faded = centerColor;
            faded.a *= 0.5f;
            world.DrawGeometry(new CircleGeometry { center = t, radius = 0.4f * r }, PhysicsTransform.identity, faded, 0f, outline);
        }
    }

    // ---------------------------------------------------------------- Aperçu

    void OnDrawGizmos()
    {
        if (Application.isPlaying || !isActiveAndEnabled) return;
        _board = GetComponent<Skateboard>();
        if (_board == null) return;
        _poseValid = false;
        SolvePose(IntentHip(0f, slowPose.crouch, 0f), -slowPose.torsoLean * Mathf.Deg2Rad, slowPose.arms, elbowBend);
        var m = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);
        Gizmos.matrix = m;
        if (drawCenterOfMass)
        {
            // Aperçu : centre commun de la posture « lent », planche approchée (plateau à mi-épaisseur, roues à leur axe).
            float mb = _board.deckMass + 2f * _board.wheelMass;
            Vector2 cb = new Vector2(0f, (_board.deckMass * 0.5f * (_board.DeckBottom + _board.DeckTop) + 2f * _board.wheelMass * _board.WheelRadius) / mb);
            Vector2 c = (mb * cb + bodyMass * PoseCenterOfMass()) / (mb + bodyMass);
            Gizmos.color = centerColor;
            SkaterGizmos.Circle(c, 0.07f);
            Gizmos.DrawLine(c - new Vector2(0.07f, 0f), c + new Vector2(0.07f, 0f));
            Gizmos.DrawLine(c - new Vector2(0f, 0.07f), c + new Vector2(0f, 0.07f));
        }
        Gizmos.color = riderColor;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
#endif
        for (int i = 0; i < LimbCount; i++)
        {
            var g = _localLimbs[i];
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
