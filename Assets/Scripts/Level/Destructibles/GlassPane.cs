using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

public enum GlassPreset { Window, Tempered, Sugar, Armored, Cinematic }

public enum GlassBlobInteraction { Collide, Ignore, Pulverize }

// Élément destructible : vitre (rectangle de verre vu de profil). Un seul corps tant qu'elle est intacte : fixe (statique)
// ou libre (dynamique : elle tombe, se cogne et se brise par son propre élan).
// Tout corps peut la casser. Énergie du choc = ½ × masse réduite × (vitesse relative dans l'axe du choc)² × fragilité au point.
// - Blob : prévu AVANT le contact (dans LevelTick) : la vitre casse avant qu'il la touche, il traverse les morceaux.
// - Tout autre corps (balle, morceau, sol sous une vitre qui tombe…) : lu sur les contacts du dernier pas ; si la vitre cède,
//   les impulsions qu'elle a données pendant ce pas sont annulées (elle n'a pas résisté) avant de créer les morceaux.
// L'impacteur paie l'énergie de rupture et l'élan cédé aux éclats. Choc trop faible : rebond, fissures, affaiblissement.
// Charge (posé, roulement) : force d'appui, plus forte au milieu (flexion). Motif de casse déterministe (GlassFracture).
[ExecuteAlways]
[DisallowMultipleComponent]
public class GlassPane : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Vitre")]
    [Tooltip("Longueur de la vitre (axe X local), en unités Unity (absolu). Les deux bouts sont les appuis (cadre).\n" +
             "Tourner le GameObject pour la poser debout (à traverser) ou couchée (pont sur lequel on roule).")]
    [Min(0.05f)] public float length = 9.25f;
    [Tooltip("Épaisseur de la vitre (axe Y local), en unités Unity (absolu). Collision réelle (pas seulement visuelle).")]
    [Min(0.01f)] public float thickness = 0.36f;
    [Tooltip("Décoché : vitre fixe (corps statique, tenue par son cadre).\n" +
             "Coché : vitre libre (corps dynamique de densité 'density') : elle tombe, glisse, se cogne, et se brise selon son propre\n" +
             "mouvement et sa masse (une vitre qui tombe sur le sol, sur un obstacle…). Ses morceaux gardent son élan.")]
    public bool freeBody = false;

    [Header("Résistance aux chocs")]
    [Tooltip("Énergie de choc qui casse le MILIEU de la vitre, en joules : ½ × masse réduite × (vitesse relative dans l'axe du choc)².\n" +
             "Masse réduite = m1 m2 / (m1 + m2) (contre un corps fixe : la masse de celui qui bouge).\n" +
             "Repères : blob standard (≈ 0,79 kg) : 25 J ≈ 29 km/h de face, ≈ 1,6 m de chute avec la gravité de poc niveau 1.")]
    [Min(0.01f)] public float breakEnergy = 25f;
    [Tooltip("Fragilité aux bouts (appuis), relative au milieu (1). Entre les deux : profil de flexion 4u(1−u), le milieu est le\n" +
             "point le plus fragile. 0,35 = un bout demande ≈ 3 fois plus d'énergie que le milieu. 1 = partout pareil.")]
    [Range(0.05f, 1f)] public float edgeFragility = 0.35f;
    [Tooltip("Part de l'énergie de casse à partir de laquelle un choc qui ne casse pas laisse des fissures et affaiblit la vitre.\n" +
             "0,35 = un choc à 35 % de l'énergie de casse fissure déjà.")]
    [Range(0.01f, 1f)] public float crackThreshold = 0.35f;
    [Tooltip("Affaiblissement par choc qui fissure : la résistance perd 'fatigue' × (énergie / énergie de casse).\n" +
             "0 = les fissures sont décoratives ; 0,6 = deux ou trois chocs moyens finissent par la casser.")]
    [Range(0f, 1f)] public float fatigue = 0.6f;
    [Tooltip("Masse prise pour un corps kinematic (flipper, futur marteau…) dans le calcul de l'énergie, en kg.\n" +
             "Un corps kinematic ne ralentit pas en cassant la vitre.")]
    [Min(0.01f)] public float kinematicMass = 10f;

    [Header("Résistance à la charge (poids, roulement)")]
    [Tooltip("Force d'appui (poids + poussée, en newtons) qui casse la vitre au milieu ; plus forte vers les bouts (même profil\n" +
             "que les chocs). Repère : le blob standard pèse ≈ 0,79 kg × gravité (≈ 16 N dans poc niveau 1).\n" +
             "Vitre libre : seuls les corps mobiles posés dessus comptent (pas le sol qui la porte).")]
    [Min(0.01f)] public float loadStrength = 50f;
    [Tooltip("Fluage : au-dessus de 'crackThreshold' × 'loadStrength', la vitre se fissure et s'affaiblit tant qu'elle est chargée,\n" +
             "de 'creepRate' × (charge / résistance) par seconde : un blob arrêté dessus finit par passer au travers. 0 = jamais.")]
    [Min(0f)] public float creepRate = 0.6f;

    [Header("Motif de casse")]
    [Tooltip("Nombre de fissures radiales qui partent de l'impact, pour un choc juste suffisant. Elles se ramifient ensuite\n" +
             "en s'éloignant (voir 'branching').")]
    [Range(4, 32)] public int radialCracks = 9;
    [Tooltip("Fissures radiales ajoutées chaque fois que l'énergie du choc double (choc plus violent = plus de morceaux).")]
    [Min(0f)] public float radialCracksPerDoubling = 3f;
    [Tooltip("Rayon du premier anneau de fissures autour de l'impact, en m (absolu), pour un choc juste suffisant.\n" +
             "Plus petit = éclats plus fins près de l'impact.")]
    [Min(0.01f)] public float firstRingRadius = 0.06f;
    [Tooltip("Resserrement des anneaux avec la violence : premier anneau divisé par (1 + valeur × nombre de doublements d'énergie).")]
    [Min(0f)] public float ringTightening = 0.4f;
    [Tooltip("Rapport entre deux écarts d'anneaux successifs (> 1) : les morceaux s'allongent en s'éloignant de l'impact.")]
    [Range(1f, 3f)] public float ringGrowth = 1.25f;
    [Tooltip("Écart maximal entre deux anneaux, en m (absolu) : loin de l'impact, les anneaux restent à cet écart et les radiales\n" +
             "se ramifient, comme dans du vrai verre (morceaux de taille à peu près constante).")]
    [Min(0.02f)] public float maxRingGap = 0.45f;
    [Tooltip("Portée des fissures concentriques pour un choc juste suffisant, en m (absolu). Au-delà, seules les radiales continuent :\n" +
             "grands morceaux. Une portée plus grande que la vitre = elle part entièrement en morceaux.")]
    [Min(0.01f)] public float crackReach = 1.5f;
    [Tooltip("Portée ajoutée (m) chaque fois que l'énergie du choc double.")]
    [Min(0f)] public float reachPerDoubling = 1.2f;
    [Tooltip("Probabilité qu'un segment d'anneau soit fissuré (0 à 1). Moins = morceaux allongés le long des radiales, en quinconce.")]
    [Range(0f, 1f)] public float ringContinuity = 0.6f;
    [Tooltip("Ondulation des fissures radiales (0 = droites, 1 = très sinueuses).")]
    [Range(0f, 1f)] public float waviness = 0.6f;
    [Tooltip("Probabilité que les radiales se ramifient quand l'écart entre elles devient trop grand (0 = jamais, 1 = toujours).")]
    [Range(0f, 1f)] public float branching = 0.75f;
    [Tooltip("Courbure des fissures concentriques (0 = droites, 1 = bien bombées, vers l'intérieur ou l'extérieur).")]
    [Range(0f, 1f)] public float curvature = 0.6f;
    [Tooltip("Irrégularité de l'angle des fissures radiales (0 = étoile parfaite, 1 = très irrégulière).")]
    [Range(0f, 1f)] public float angleJitter = 0.7f;
    [Tooltip("Irrégularité du rayon des anneaux (0 = cercles parfaits, 1 = très irréguliers).")]
    [Range(0f, 1f)] public float radiusJitter = 0.5f;
    [Tooltip("Vitesse de propagation des fissures, en km/h. 0 = instantanée (verre réel : ≈ 5 400 km/h).\n" +
             "Plus lent = effet de mise en scène : les morceaux se détachent à mesure que le front passe. Ce qui touche\n" +
             "l'impacteur se détache toujours tout de suite.")]
    [SpeedKmh] [Min(0f)] public float crackSpeed = 0f;
    [Tooltip("Vitre fixe : coché, les grands morceaux au-delà des fissures concentriques qui touchent un bout restent en place (fêlés,\n" +
             "tenus par le cadre). Décoché : tout tombe. Sans effet sur une vitre libre.")]
    public bool supportedEnds = true;
    [Tooltip("Résistance des morceaux restés en place, en part de 'breakEnergy' et 'loadStrength' : un choc ou une charge au-dessus\n" +
             "les détache.")]
    [Range(0.01f, 1f)] public float crackedStrength = 0.3f;
    [Tooltip("Graine du motif de casse de cette vitre. Même graine + même impact = même casse au bit près.")]
    public uint seed = 1;

    [Header("Morceaux")]
    [Tooltip("Densité du verre (2D, kg/m²). Le blob a 1 : même masse à surface égale (verre réel ≈ 2,5).\n" +
             "Plus lourd = les morceaux freinent davantage l'impacteur. Donne aussi la masse d'une vitre libre.")]
    [Min(0.01f)] public float density = 1f;
    [Tooltip("Friction des morceaux et de la vitre (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.4f;
    [Tooltip("Rebond des morceaux et de la vitre (0 = aucun, 1 = parfait).")]
    [Min(0f)] public float bounciness = 0.05f;
    [Tooltip("Taille minimale d'un morceau (racine de sa surface, m). En dessous : poussière, sans corps physique (effet\n" +
             "visuel seulement, GlassDust), mais son élan est quand même pris à l'impacteur.")]
    [Min(0f)] public float minFragmentSize = 0.05f;
    [Tooltip("Nombre maximal de morceaux physiques (plafond du coût). Au-delà, le motif est simplifié (moins de radiales, anneaux\n" +
             "plus larges) jusqu'à tenir dans le plafond.")]
    [Range(1, 300)] public int maxFragments = 90;
    [Tooltip("Les morceaux se heurtent entre eux (toutes vitres confondues). Décoché : ils se traversent (moins coûteux).")]
    public bool fragmentsCollideTogether = true;
    [Tooltip("Morceaux et blob :\n" +
             "• Collide : vrais chocs (ils freinent, gênent, peuvent le porter)\n" +
             "• Ignore : le blob les traverse sans effet\n" +
             "• Pulverize : le blob les réduit en poussière en passant (il perd l'élan échangé avec eux)")]
    public GlassBlobInteraction blobInteraction = GlassBlobInteraction.Collide;
    [Tooltip("Pulverize : distance (m) autour de la vitre dans laquelle le blob pulvérise les morceaux. Plus loin, aucun test (coût nul).")]
    [Min(0f)] public float pulverizeZone = 3f;
    [Tooltip("Vitesse (km/h) sous laquelle un morceau s'endort (ne coûte plus rien). Plus haut = s'endort plus vite.")]
    [SpeedKmh] [Min(0f)] public float sleepSpeed = 0.08f;
    [Tooltip("Durée de vie des morceaux après leur détachement, en secondes de jeu. 0 = ils restent jusqu'au redémarrage.")]
    [Min(0f)] public float cleanupDelay = 0f;
    [Tooltip("Morceaux tombés à plus de cette distance (m) sous la vitre retirés : hors écran, dans le vide, ils ne s'endormiraient\n" +
             "jamais et coûteraient à chaque pas (donc à chaque retour de sauvegarde). 0 = jamais. Un morceau vérifié par pas, à tour de rôle.")]
    [Min(0f)] public float removeBelow = 30f;

    [Header("Projection")]
    [Tooltip("Vitesse des éclats dans le sens de l'impacteur (vitesse relative à la vitre), en part de cette vitesse, au point\n" +
             "d'impact (décroît avec la distance). > 1 = les éclats partent devant lui. Limitée par l'élan qu'il peut céder.")]
    [Min(0f)] public float momentumTransfer = 1.1f;
    [Tooltip("Marge (m) autour de la trajectoire de l'impacteur : les éclats dans sa largeur sont emportés à 'momentumTransfer' ×\n" +
             "sa vitesse ; à 'burstRadius' de plus sur le côté, à 37 %. Donne aussi la portée de l'éclatement (avec sa largeur).\n" +
             "Petit = trou net à sa taille ; grand = il entraîne beaucoup de verre (et ralentit plus).")]
    [Min(0.01f)] public float burstRadius = 0.3f;
    [Tooltip("Vitesse d'éclatement vers l'extérieur de l'impact (km/h) par doublement d'énergie au-delà du seuil.")]
    [SpeedKmh] [Min(0f)] public float burstSpeed = 2.5f;
    [Tooltip("Dispersion de la direction des éclats, en degrés de part et d'autre (cône du nuage de débris).")]
    [Range(0f, 90f)] public float spreadAngle = 25f;
    [Tooltip("Variation de la vitesse des éclats (0 = tous pareils, 0,35 = ±35 %).")]
    [Range(0f, 1f)] public float speedJitter = 0.35f;
    [Tooltip("Rotation maximale des éclats proches de l'impact, en radians/s.")]
    [Min(0f)] public float maxSpin = 25f;
    [Tooltip("Part de l'énergie de casse (au point touché) prise à l'impacteur en plus de l'élan des éclats.\n" +
             "1 = un impacteur juste assez fort s'arrête dans la vitre ; 0 = il ne paie que l'élan des éclats.")]
    [Range(0f, 1f)] public float energyAbsorbed = 0.5f;
    [Tooltip("Part de l'élan donné aux éclats que l'impacteur paie vraiment. 1 = conservation exacte : une vitre épaisse et lourde\n" +
             "arrête presque tout (ex. balle de 0,79 kg dans 36 cm de verre). Moins = traversée plus franche, les éclats partent quand même.")]
    [Range(0f, 1f)] public float momentumCost = 0.5f;

    [Header("Détection du blob")]
    [Tooltip("Distance (m, absolu) autour de la vitre à partir de laquelle le choc du blob est prévu avant le contact (lecture de\n" +
             "ses capsules) : doit dépasser son déplacement en un pas (0,6 m ≈ 260 km/h à 120 Hz). Plus loin, une seule lecture par pas.")]
    [Min(0.05f)] public float detectionMargin = 0.6f;

    [Header("Debug")]
    [Tooltip("Couleur de la vitre et des morceaux (rendu debug et aperçu Scene view).")]
    public Color glassColor = new Color(0.65f, 0.9f, 1f, 1f);
    [Tooltip("Opacité du remplissage des morceaux (0 = contour seul).")]
    [Range(0f, 1f)] public float fillAlpha = 0.25f;
    [Tooltip("Couleur des fissures.")]
    public Color crackColor = new Color(1f, 1f, 1f, 1f);

    // Morceaux qui ignorent le blob : catégorie à part (sans le bit 0), que le BLOB exclut aussi (mode de filtre « Either »).
    public const int LooseFragmentBit = 27;

    // Poussière : éclats trop petits pour être des corps, morceaux pulvérisés. (vitre, position, vitesse, surface m²)
    public static event System.Action<GlassPane, Vector2, Vector2, float> Dust;
    // Casse : (vitre, point d'impact, intensité = énergie / énergie de casse, ≥ 1 ; 0 pour une casse sous charge)
    public static event System.Action<GlassPane, Vector2, float> Shattered;

    static readonly List<GlassPane> _active = new List<GlassPane>();
    public static IReadOnlyList<GlassPane> Active => _active;
    static int _dispatchedStep = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatics()
    {
        _active.Clear();
        _dispatchedStep = -1;
        _probeStep = -1;
        _probes.Clear();
        Dust = null;
        Shattered = null;
    }

    struct Impactor
    {
        public BLOB blob;
        public PhysicsBody body;
        public bool dynamic;
        public float mass;
        public float coreRadius;
        public Vector2 point; // monde
    }

    struct ContactPoint
    {
        public Vector2 point;
        public Vector2 normal;  // de la vitre vers l'autre corps
        public Vector2 impulse; // donnée à l'autre corps pendant le dernier pas
        public float approach;  // vitesse d'approche avant le pas (m/s)
        public PhysicsBody other;
        public bool persisted;
        public int cell;
    }

    struct RecentHit { public PhysicsBody body; public int until; public float damageBefore; }

    const byte CellNone = 0, CellStatic = 1, CellFragment = 2;
    const int Never = int.MaxValue;
    const int MaxCrackLines = 256;
    const int MaxContacts = 64;
    const int ImpactCooldown = 15;
    // Un point de contact qui approchait à cette vitesse (m/s) avant le pas est un choc ; plus lent, un appui (charge).
    // Box2D peut créer le contact un pas avant de le résoudre : « nouveau / installé » ne suffit pas à distinguer un choc.
    const float ImpactSpeed = 0.5f;
    const float TouchSlop = 0.01f; // m : au-delà de cette séparation, un point de contact ne touche pas encore

    PhysicsBody _body;
    bool _hasBody;
    PhysicsShape _solid;
    bool _broken;
    float _damage;
    int _hits;
    int _touches;
    readonly List<RecentHit> _recent = new List<RecentHit>(8);
    readonly ContactPoint[] _contacts = new ContactPoint[MaxContacts];
    int _contactCount;

    // Morceaux (recopiés de GlassFracture.Shared à la casse ; tableaux agrandis seulement si besoin)
    int _cellCount, _partCount;
    int[] _cellPartStart, _cellPartCount, _cellRelease;
    Vector2[] _cellCentroid, _cellVelocity;
    float[] _cellArea, _cellInner, _cellRadius, _cellSpin;
    bool[] _cellOuter, _cellTouchesEnd, _cellDust, _launch;
    byte[] _cellState;
    PhysicsBody[] _cellBody;
    Vector2[] _partVerts;
    byte[] _partEdges;
    int[] _partVertCount;
    PolygonGeometry[] _partGeom;
    PhysicsShape[] _partShape;

    int _staticCells, _pendingCells, _liveFragments, _cleanupStep, _fallCursor;
    Vector2 _origin;
    int _shatterStep;
    float _coreRadius;

    readonly Vector2[] _crackLines = new Vector2[2 * MaxCrackLines];
    int _crackLineCount;
    int _lastCreepCrackStep;
    readonly Vector2[] _verts = new Vector2[GlassFracture.MaxVerts];

    Vector2 _pos, _half;
    float _cos, _sin;
    Vector2 _preVelocity; // vitre libre : vitesse avant le dernier pas
    float _preAngular;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public bool IsBroken => _broken;
    public float Damage => _damage;
    public Vector2 HalfSize => new Vector2(0.5f * length, 0.5f * thickness);
    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    Vector2 ToLocal(Vector2 w) { var d = w - _pos; return new Vector2(d.x * _cos + d.y * _sin, -d.x * _sin + d.y * _cos); }
    Vector2 ToLocalVec(Vector2 w) => new Vector2(w.x * _cos + w.y * _sin, -w.x * _sin + w.y * _cos);
    Vector2 ToWorldVec(Vector2 l) => new Vector2(l.x * _cos - l.y * _sin, l.x * _sin + l.y * _cos);
    Vector2 ToWorld(Vector2 l) => _pos + ToWorldVec(l);
    float Angle => math.atan2(_sin, _cos);

    float Fragility(float localX)
    {
        float u = math.saturate((localX + _half.x) / (2f * _half.x));
        return edgeFragility + (1f - edgeFragility) * 4f * u * (1f - u);
    }

    // ---------- Préréglages (valeurs complètes ; Window = valeurs par défaut du script) ----------

    public void ApplyPreset(GlassPreset preset)
    {
        breakEnergy = 25f; edgeFragility = 0.35f; crackThreshold = 0.35f; fatigue = 0.6f; loadStrength = 50f; creepRate = 0.6f;
        radialCracks = 9; radialCracksPerDoubling = 3f; firstRingRadius = 0.06f; ringTightening = 0.4f; ringGrowth = 1.25f;
        maxRingGap = 0.45f; crackReach = 1.5f; reachPerDoubling = 1.2f; ringContinuity = 0.6f; waviness = 0.6f; branching = 0.75f;
        curvature = 0.6f; angleJitter = 0.7f; radiusJitter = 0.5f; crackSpeed = 0f; supportedEnds = true; crackedStrength = 0.3f;
        density = 1f; minFragmentSize = 0.05f; maxFragments = 90;
        momentumTransfer = 1.1f; burstRadius = 0.3f; burstSpeed = 2.5f; spreadAngle = 25f; speedJitter = 0.35f; maxSpin = 25f;
        energyAbsorbed = 0.5f; momentumCost = 0.5f;

        switch (preset)
        {
            case GlassPreset.Tempered: // verre trempé : solide, mais part entièrement en petits cubes
                breakEnergy = 45f; edgeFragility = 0.6f; crackThreshold = 0.6f; fatigue = 0.3f; loadStrength = 80f; creepRate = 0f;
                radialCracks = 14; firstRingRadius = 0.04f; ringGrowth = 1.15f; maxRingGap = 0.16f; crackReach = 100f;
                ringContinuity = 0.95f; waviness = 0.25f; curvature = 0.2f; branching = 1f; supportedEnds = false;
                minFragmentSize = 0.04f; maxFragments = 200; momentumTransfer = 1.1f; burstSpeed = 3.5f; energyAbsorbed = 0.4f;
                break;
            case GlassPreset.Sugar: // verre de cinéma : casse au moindre choc, presque sans freiner
                breakEnergy = 3f; edgeFragility = 0.5f; crackThreshold = 0.5f; fatigue = 0.8f; loadStrength = 12f; creepRate = 1.5f;
                radialCracks = 7; firstRingRadius = 0.08f; maxRingGap = 0.6f; crackReach = 1.2f;
                density = 0.8f; momentumTransfer = 1.05f; burstSpeed = 1f; energyAbsorbed = 0.2f; momentumCost = 0.3f;
                break;
            case GlassPreset.Armored: // vitre épaisse : plusieurs chocs, gros morceaux, freine fort
                breakEnergy = 160f; edgeFragility = 0.25f; crackThreshold = 0.15f; fatigue = 0.35f; loadStrength = 300f; creepRate = 0.2f;
                radialCracks = 6; radialCracksPerDoubling = 2f; firstRingRadius = 0.15f; ringGrowth = 1.6f; maxRingGap = 0.9f;
                crackReach = 1f; reachPerDoubling = 0.6f; ringContinuity = 0.45f; branching = 0.4f;
                density = 3f; minFragmentSize = 0.08f; maxFragments = 40; momentumTransfer = 1f; burstSpeed = 1f; energyAbsorbed = 0.9f; momentumCost = 1f;
                break;
            case GlassPreset.Cinematic: // mise en scène : propagation lente et visible, casse totale, gerbe large
                crackSpeed = 12f; crackReach = 100f; supportedEnds = false; radialCracks = 12; firstRingRadius = 0.05f;
                maxRingGap = 0.35f; maxFragments = 160; burstSpeed = 4f; spreadAngle = 35f; maxSpin = 35f;
                break;
        }
    }

    // ---------- Cycle de vie ----------

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        _dispatchedStep = -1;
        _probeStep = -1;
        float a = transform.eulerAngles.z * Mathf.Deg2Rad;
        _pos = transform.position;
        _cos = math.cos(a);
        _sin = math.sin(a);
        _half = HalfSize;
        ResetState();

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = freeBody ? PhysicsBody.BodyType.Dynamic : PhysicsBody.BodyType.Static;
        bodyDef.position = _pos;
        bodyDef.rotation = PhysicsRotate.FromRadians(a);
        bodyDef.sleepThreshold = sleepSpeed;
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        _hasBody = true;

        _solid = _body.CreateShape(Box(_half), SolidDefinition(true));
        _solid.userData = new PhysicsUserData { objectValue = this, intValue = 0 };
        _active.Add(this);
    }

    void OnDisable()
    {
        _active.Remove(this);
        if (_cellBody != null)
            for (int c = 0; c < _cellCount; c++)
                if (_cellState[c] == CellFragment && _cellBody[c].isValid) _cellBody[c].Destroy();
        if (_body.isValid) _body.Destroy();
        _body = default;
        _hasBody = false;
        ResetState();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void ResetState()
    {
        _broken = false;
        _damage = 0f;
        _hits = 0;
        _touches = 0;
        _recent.Clear();
        _staticCells = _pendingCells = _liveFragments = 0;
        _cleanupStep = Never;
        _fallCursor = 0;
        _crackLineCount = 0;
        _lastCreepCrackStep = -Never;
        _preVelocity = Vector2.zero;
        _preAngular = 0f;
        if (_cellState != null)
        {
            System.Array.Clear(_cellState, 0, _cellState.Length);
            System.Array.Clear(_cellBody, 0, _cellBody.Length);
        }
        if (_partShape != null) System.Array.Clear(_partShape, 0, _partShape.Length);
        _cellCount = _partCount = 0;
    }

    // Vitre et morceaux. contactEvents : la charge et les chocs ne sont lus que quand la vitre est touchée.
    PhysicsShapeDefinition SolidDefinition(bool pane)
    {
        var def = PhysicsShapeDefinition.defaultDefinition;
        var material = def.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        material.customColor = glassColor;
        def.surfaceMaterial = material;
        def.density = density;
        def.contactEvents = pane;
        return def;
    }

    static PolygonGeometry Box(Vector2 half)
    {
        System.Span<Vector2> v = stackalloc Vector2[4];
        v[0] = new Vector2(-half.x, -half.y); v[1] = new Vector2(half.x, -half.y);
        v[2] = new Vector2(half.x, half.y); v[3] = new Vector2(-half.x, half.y);
        return PolygonGeometry.Create(v, 0f);
    }

    void ReadPose()
    {
        var t = _body.transform;
        _pos = t.position;
        float a = t.rotation.radians;
        _cos = math.cos(a);
        _sin = math.sin(a);
    }

    // ---------- Pas de simulation ----------

    public void LevelTick()
    {
        DispatchEvents();
        int step = LevelSimulation.Step;
        if (_hasBody)
        {
            // Vitre libre endormie : elle ne bouge pas, pose et vitesse déjà connues (une seule lecture).
            bool moving = freeBody && _body.awake;
            if (moving) ReadPose();
            if (BLOB.Active.Count > 0) PredictBlobs();
            if (_hasBody && _touches > 0) ProcessContacts();
            if (_hasBody && _pendingCells > 0) ReleaseDue(step);
            if (moving && _hasBody) { _preVelocity = _body.linearVelocity; _preAngular = _body.angularVelocity; }
            else if (freeBody) { _preVelocity = Vector2.zero; _preAngular = 0f; }
        }
        if (_liveFragments > 0)
        {
            if (blobInteraction == GlassBlobInteraction.Pulverize) PulverizeTouching();
            if (step >= _cleanupStep) Cleanup(step);
            if (removeBelow > 0f && _liveFragments > 0) RemoveFallen();
        }
    }

    // Début / fin de contact touchant des formes de la vitre (intacte : intValue 0, cellules en place : 1 + cellule), lus une fois
    // par pas pour toutes les vitres. Au pas 0 (redémarrage), ceux d'avant concernent des formes détruites : ignorés.
    static void DispatchEvents()
    {
        int step = LevelSimulation.Step;
        if (_dispatchedStep == step) return;
        _dispatchedStep = step;
        if (step == 0) return;
        var world = PhysicsWorld.defaultWorld;
        foreach (var e in world.contactBeginEvents) { Touch(e.shapeA, 1); Touch(e.shapeB, 1); }
        foreach (var e in world.contactEndEvents) { Touch(e.shapeA, -1); Touch(e.shapeB, -1); }
    }

    static void Touch(PhysicsShape s, int delta)
    {
        if (!s.isValid) return;
        var data = s.userData;
        if (data.intValue >= 0 && data.objectValue is GlassPane p) p._touches = math.max(0, p._touches + delta);
    }

    // ---------- Blob : choc prévu avant le contact ----------

    void PredictBlobs()
    {
        float dt = Time.fixedDeltaTime;
        var blobs = BLOB.Active;
        for (int b = 0; b < blobs.Count; b++)
        {
            var blob = blobs[b];
            int n = blob.CapsuleCount;
            if (n == 0 || blob.IsBroken) continue; // cassé : capsules libres, traitées comme tout corps (contacts)
            Vector2 c0 = ToLocal(BlobProbe(b));
            float zone = blob.diameter + detectionMargin;
            if (math.abs(c0.x) > _half.x + zone || math.abs(c0.y) > _half.y + zone) continue;

            var cap = blob.CapsuleShape;
            float radius = cap.radius + 0.5f * (cap.center2 - cap.center1).magnitude;
            Vector2 gv = Vector2.zero, com = Vector2.zero;
            float gw = 0f;
            if (freeBody) { gv = _body.linearVelocity; gw = _body.angularVelocity; com = _body.worldCenterOfMass; }

            // Capsule qui touche la première (à égalité : la première dans l'ordre du blob).
            float bestT = float.MaxValue;
            int bestCell = -1;
            Vector2 bestPoint = default, bestNormal = default;
            bool hit = false;
            for (int i = 0; i < n; i++)
            {
                var body = blob.Capsule(i);
                Vector2 p = body.position;
                Vector2 v = body.linearVelocity;
                if (freeBody) v -= gv + new Vector2(-(p.y - com.y), p.x - com.x) * gw;
                Vector2 lp = ToLocal(p), d = ToLocalVec(v) * dt;
                if (!_broken)
                {
                    if (PredictBox(radius, lp, d, out float t, out var point, out var normal) && t < bestT)
                    {
                        bestT = t; bestPoint = point; bestNormal = normal; hit = true;
                    }
                }
                else if (PredictCells(radius, lp, d, out int cell, out var point, out var normal))
                {
                    bestCell = cell; bestPoint = point; bestNormal = normal; hit = true;
                    break;
                }
            }
            if (!hit) continue;

            var imp = BlobImpactor(blob);
            Vector2 vRel = MeanVelocity(blob) - GlassVelocityAt(imp.point = ToWorld(bestPoint));
            if (!_broken) Impact(imp, bestPoint, bestNormal, vRel, -1);
            else Impact(imp, bestPoint, bestNormal, vRel, bestCell);
            if (!_hasBody) return;
        }
    }

    // Position d'une capsule de chaque blob, lue une fois par pas pour toutes les vitres.
    static readonly List<Vector2> _probes = new List<Vector2>(4);
    static int _probeStep = -1;

    static Vector2 BlobProbe(int index)
    {
        var blobs = BLOB.Active;
        if (_probeStep != LevelSimulation.Step || _probes.Count != blobs.Count)
        {
            _probeStep = LevelSimulation.Step;
            _probes.Clear();
            for (int b = 0; b < blobs.Count; b++) _probes.Add(blobs[b].CapsuleCount > 0 ? blobs[b].Capsule(0).position : Vector2.zero);
        }
        return _probes[index];
    }

    Impactor BlobImpactor(BLOB blob) => new Impactor
    {
        blob = blob,
        body = blob.Capsule(0),
        dynamic = true,
        mass = blob.density * math.PI * 0.25f * blob.diameter * blob.diameter,
        coreRadius = 0.5f * blob.diameter
    };

    static Vector2 MeanVelocity(BLOB blob)
    {
        Vector2 v = Vector2.zero;
        int n = blob.CapsuleCount;
        for (int i = 0; i < n; i++) v += blob.Capsule(i).linearVelocity;
        return v / n;
    }

    Vector2 GlassVelocityAt(Vector2 world) => freeBody && _hasBody ? _body.GetWorldPointVelocity(world) : Vector2.zero;

    bool PredictBox(float radius, Vector2 lp, Vector2 d, out float tMin, out Vector2 point, out Vector2 n)
    {
        tMin = 0f;
        point = n = default;
        float ex = _half.x + radius, ey = _half.y + radius;
        if (math.abs(lp.x) < ex && math.abs(lp.y) < ey) return false; // déjà au contact : géré par les contacts

        float tMax = 1f;
        int axisHit = -1;
        float side = 0f;
        for (int a = 0; a < 2; a++)
        {
            float e = a == 0 ? ex : ey;
            if (math.abs(d[a]) < 1e-9f)
            {
                if (math.abs(lp[a]) > e) return false;
                continue;
            }
            float t1 = (-e - lp[a]) / d[a], t2 = (e - lp[a]) / d[a];
            float tn = math.min(t1, t2), tf = math.max(t1, t2);
            if (tn > tMin || axisHit < 0 && tn >= tMin) { tMin = tn; axisHit = a; side = d[a] > 0f ? -1f : 1f; }
            tMax = math.min(tMax, tf);
            if (tMin > tMax) return false;
        }
        if (axisHit < 0 || tMin > 1f) return false;

        n = axisHit == 0 ? new Vector2(side, 0f) : new Vector2(0f, side);
        point = lp + d * tMin - n * radius;
        point = new Vector2(math.clamp(point.x, -_half.x, _half.x), math.clamp(point.y, -_half.y, _half.y));
        return true;
    }

    // Vitre déjà cassée : seules les cellules restées en place (fêlées ou pas encore atteintes par le front) arrêtent un corps.
    bool PredictCells(float radius, Vector2 lp, Vector2 d, out int cell, out Vector2 point, out Vector2 normal)
    {
        Vector2 q = lp + d;
        for (int c = 0; c < _cellCount; c++)
        {
            if (_cellState[c] != CellStatic) continue;
            if (CellDistance(c, q, out _) > radius) continue;
            if (CellDistance(c, lp, out Vector2 closest) <= radius) continue;
            cell = c;
            point = closest;
            normal = (lp - closest).normalized;
            return true;
        }
        cell = -1;
        point = normal = default;
        return false;
    }

    // ---------- Choc (blob ou autre corps) ----------

    float GlassMass => freeBody && _hasBody ? _body.mass : float.PositiveInfinity;

    float EffectiveMass(in Impactor imp)
    {
        float mo = imp.dynamic ? imp.mass : imp.body.isValid && imp.body.type == PhysicsBody.BodyType.Kinematic ? kinematicMass : float.PositiveInfinity;
        float mg = GlassMass;
        if (float.IsPositiveInfinity(mo)) return mg;
        if (float.IsPositiveInfinity(mg)) return mo;
        return mo * mg / (mo + mg);
    }

    // cell < 0 : vitre intacte. Retourne vrai si la vitre (ou la cellule) a cédé.
    bool Impact(in Impactor imp, Vector2 point, Vector2 normal, Vector2 vRel, int cell, int contactIndex = -1)
    {
        Vector2 nWorld = ToWorldVec(normal);
        float vn = -Vector2.Dot(vRel, nWorld);
        if (contactIndex >= 0) vn = math.max(vn, _contacts[contactIndex].approach);
        if (vn <= 0f) return false;
        float mEff = EffectiveMass(imp);
        if (float.IsPositiveInfinity(mEff) || mEff <= 0f) return false;
        float f = Fragility(point.x);

        if (cell >= 0)
        {
            float cracked = breakEnergy * crackedStrength;
            float ec = 0.5f * mEff * vn * vn * f / cracked;
            if (ec < 1f) return false;
            if (contactIndex >= 0) RelativeAfterUndo(imp, nWorld, contactIndex, cell, ref vRel, ref vn);
            for (int c = 0; c < _cellCount; c++)
                _launch[c] = _cellState[c] == CellStatic &&
                             (c == cell || (_cellCentroid[c] - point).magnitude < imp.coreRadius + _cellRadius[c]);
            var rng = new Unity.Mathematics.Random(math.hash(new uint4(seed, math.asuint(point.x), math.asuint(point.y), (uint)++_hits)) | 1u);
            Launch(point, vRel, imp, mEff, nWorld, vn, math.min(math.log2(ec), 6f), cracked / f, ref rng);
            for (int c = 0; c < _cellCount; c++)
                if (_launch[c]) Release(c);
            DestroyBodyIfEmpty();
            return true;
        }

        // Le blob souple arrive capsule après capsule : un même choc est vu à plusieurs pas. Pendant ImpactCooldown pas
        // (prolongés tant qu'il insiste), il est jugé sur la résistance d'avant ce choc et n'affaiblit plus la vitre.
        var key = imp.body;
        int step = LevelSimulation.Step;
        int recent = -1;
        for (int i = _recent.Count - 1; i >= 0; i--)
            if (_recent[i].until <= step) _recent.RemoveAt(i);
        for (int i = 0; i < _recent.Count; i++)
            if (_recent[i].body == key) { recent = i; break; }
        float damage = recent >= 0 ? _recent[recent].damageBefore : _damage;

        float strength = breakEnergy * (1f - damage);
        float e = 0.5f * mEff * vn * vn * f / strength;
        if (e >= 1f)
        {
            if (contactIndex >= 0) RelativeAfterUndo(imp, nWorld, contactIndex, -1, ref vRel, ref vn);
            Shatter(point, imp, vRel, mEff, nWorld, vn, e, strength / f);
            return true;
        }
        if (recent >= 0) _recent[recent] = new RecentHit { body = key, until = step + ImpactCooldown, damageBefore = damage };
        else if (e >= crackThreshold)
        {
            _recent.Add(new RecentHit { body = key, until = step + ImpactCooldown, damageBefore = _damage });
            Weaken(point, e);
        }
        return false;
    }

    // Choc lu sur les contacts : la vitre cède, ses impulsions du dernier pas sont reprises, puis la vitesse relative est relue
    // (celle d'avant le rebond).
    void RelativeAfterUndo(in Impactor imp, Vector2 nWorld, int contactIndex, int cell, ref Vector2 vRel, ref float vn)
    {
        UndoContactImpulses(cell);
        vRel = OtherVelocityAt(imp.body, imp.point) - GlassVelocityAt(imp.point);
        vn = math.max(-Vector2.Dot(vRel, nWorld), _contacts[contactIndex].approach);
    }

    // Choc qui ne casse pas : fissures dessinées et vitre affaiblie.
    void Weaken(Vector2 point, float e)
    {
        _damage = math.min(0.95f, _damage + fatigue * e);
        _hits++;
        AddCrackMark(point, e);
    }

    // ---------- Contacts du dernier pas : chocs des autres corps, charge ----------

    void ProcessContacts()
    {
        float dt = Time.fixedDeltaTime;
        var contacts = _body.GetContacts(Allocator.Temp);
        _contactCount = 0;
        for (int k = 0; k < contacts.Length && _contactCount < MaxContacts; k++)
        {
            var contact = contacts[k];
            var manifold = contact.manifold;
            if (manifold.pointCount == 0) continue;
            bool glassIsA = contact.shapeA.body == _body;
            var mine = glassIsA ? contact.shapeA : contact.shapeB;
            var other = glassIsA ? contact.shapeB.body : contact.shapeA.body;
            Vector2 mn = manifold.normal; // de A vers B
            Vector2 tangent = new Vector2(mn.y, -mn.x);
            int cell = _broken ? mine.userData.intValue - 1 : -1;
            for (int p = 0; p < manifold.pointCount && _contactCount < MaxContacts; p++)
            {
                var mp = manifold[p];
                if (mp.separation > TouchSlop) continue; // point spéculatif (pas encore au contact)
                Vector2 onB = mn * mp.normalImpulse + tangent * mp.tangentImpulse;
                _contacts[_contactCount++] = new ContactPoint
                {
                    point = mp.point,
                    normal = glassIsA ? mn : -mn,
                    impulse = glassIsA ? onB : -onB,
                    approach = -mp.normalVelocity,
                    other = other,
                    persisted = mp.persisted,
                    cell = cell
                };
            }
        }
        contacts.Dispose();
        if (_contactCount == 0) { _touches = 0; return; }
        SortContacts();

        // Chocs : points qui approchaient vite, d'autres corps que le blob (le blob est jugé avant le contact).
        float bestE = 0f;
        int best = -1;
        for (int i = 0; i < _contactCount; i++)
        {
            ref var cp = ref _contacts[i];
            if (cp.approach < ImpactSpeed || IsBlobCapsule(cp.other)) continue;
            var imp = BodyImpactor(cp.other, cp.point);
            float mEff = EffectiveMass(imp);
            if (float.IsPositiveInfinity(mEff)) continue;
            float strength = _broken ? breakEnergy * crackedStrength : breakEnergy * (1f - _damage);
            float e = 0.5f * mEff * cp.approach * cp.approach * Fragility(ToLocal(cp.point).x) / strength;
            if (e > bestE) { bestE = e; best = i; }
        }
        if (best >= 0 && bestE >= crackThreshold * 0.5f)
        {
            var cp = _contacts[best];
            var imp = BodyImpactor(cp.other, cp.point);
            Vector2 vRel = OtherVelocityAt(cp.other, cp.point) - GlassVelocityAt(cp.point);
            if (Impact(imp, ToLocal(cp.point), ToLocalVec(cp.normal), vRel, _broken ? cp.cell : -1, best) || !_hasBody) return;
        }

        // Charge : points d'appui (sans vitesse d'approche). Somme en entiers (millinewtons) et point retenu départagé
        // par sa position : indépendant de l'ordre des contacts de Box2D. Vitre libre : le sol qui la porte ne compte pas.
        long stressSum = 0;
        float maxForce = 0f, cellStress = 0f;
        Vector2 at = Vector2.zero, cellAt = Vector2.zero;
        int atCell = -1;
        for (int i = 0; i < _contactCount; i++)
        {
            ref var cp = ref _contacts[i];
            if (cp.approach >= ImpactSpeed) continue;
            if (freeBody && cp.other.type == PhysicsBody.BodyType.Static) continue;
            float force = cp.impulse.magnitude / dt;
            Vector2 local = ToLocal(cp.point);
            float s = force * Fragility(local.x);
            if (_broken)
            {
                if (cp.cell >= 0 && s > cellStress) { cellStress = s; cellAt = local; atCell = cp.cell; }
                continue;
            }
            stressSum += (long)math.round(s * 1000f);
            if (force > maxForce) { maxForce = force; at = local; }
        }

        if (_broken)
        {
            if (atCell >= 0 && _cellState[atCell] == CellStatic && cellStress >= loadStrength * crackedStrength)
            {
                UndoContactImpulses(atCell);
                for (int c = 0; c < _cellCount; c++) _launch[c] = c == atCell;
                var rng = new Unity.Mathematics.Random(math.hash(new uint3(seed, (uint)atCell, (uint)++_hits)) | 1u);
                Launch(cellAt, Vector2.zero, default, 0f, Vector2.up, 0f, 0f, 0f, ref rng);
                Release(atCell);
                DestroyBodyIfEmpty();
            }
            return;
        }

        float stress = stressSum / 1000f;
        float ratio = stress / (loadStrength * (1f - _damage));
        if (ratio >= 1f || _damage >= 0.95f)
        {
            UndoContactImpulses(-1);
            Shatter(at, default, Vector2.zero, 0f, ToWorldVec(Vector2.up), 0f, 1f, 0f);
            return;
        }
        if (ratio >= crackThreshold && creepRate > 0f)
        {
            _damage = math.min(0.95f, _damage + creepRate * dt * ratio);
            int step = LevelSimulation.Step;
            if (step - _lastCreepCrackStep >= 30)
            {
                _lastCreepCrackStep = step;
                AddCrackMark(at, math.min(1f, _damage + 0.2f));
            }
        }
    }

    // Ordre indépendant de Box2D (ses identifiants changent d'un essai à l'autre) : tri par position du point.
    void SortContacts()
    {
        for (int i = 1; i < _contactCount; i++)
        {
            var c = _contacts[i];
            int j = i - 1;
            while (j >= 0 && (_contacts[j].point.x > c.point.x || _contacts[j].point.x == c.point.x && _contacts[j].point.y > c.point.y))
            {
                _contacts[j + 1] = _contacts[j];
                j--;
            }
            _contacts[j + 1] = c;
        }
    }

    // La vitre a cédé pendant ce pas (toute la vitre, ou une cellule) : ce qu'elle a fait aux corps qui la touchaient est repris.
    // Vitre libre : elle retrouve sa vitesse et sa rotation d'avant le pas (+ la gravité du pas). Corps en choc : sa vitesse normale
    // d'approche d'avant le pas lui est rendue (une fois par corps). Corps en appui : l'impulsion du pas est reprise.
    void UndoContactImpulses(int cell)
    {
        if (freeBody && _hasBody)
        {
            _body.linearVelocity = _preVelocity + PhysicsWorld.defaultWorld.gravity * Time.fixedDeltaTime;
            _body.angularVelocity = _preAngular;
        }
        for (int i = 0; i < _contactCount; i++)
        {
            ref var cp = ref _contacts[i];
            if (cell >= 0 && cp.cell != cell) continue;
            if (cp.other.type != PhysicsBody.BodyType.Dynamic) continue;
            if (cp.approach < ImpactSpeed)
            {
                cp.other.ApplyLinearImpulse(-cp.impulse, cp.point, true);
                continue;
            }
            bool first = true;
            for (int j = 0; j < i && first; j++)
                if (_contacts[j].approach >= ImpactSpeed && _contacts[j].other == cp.other && (cell < 0 || _contacts[j].cell == cell)) first = false;
            if (!first) continue;
            Vector2 vo = cp.other.linearVelocity;
            float current = Vector2.Dot(vo - GlassVelocityAt(cp.point), cp.normal);
            float change = -cp.approach - current;
            if (change < 0f) cp.other.linearVelocity = vo + cp.normal * change;
        }
    }

    static bool IsBlobCapsule(PhysicsBody body)
    {
        var blobs = BLOB.Active;
        for (int b = 0; b < blobs.Count; b++)
        {
            var blob = blobs[b];
            if (blob.IsBroken) continue;
            for (int i = 0; i < blob.CapsuleCount; i++)
                if (blob.Capsule(i) == body) return true;
        }
        return false;
    }

    Impactor BodyImpactor(PhysicsBody body, Vector2 point)
    {
        bool dynamic = body.type == PhysicsBody.BodyType.Dynamic;
        float core = 0.25f;
        if (dynamic)
        {
            var aabb = body.GetAABB();
            core = 0.5f * math.max(aabb.upperBound.x - aabb.lowerBound.x, aabb.upperBound.y - aabb.lowerBound.y);
        }
        return new Impactor { body = body, dynamic = dynamic, mass = dynamic ? body.mass : 0f, coreRadius = core, point = point };
    }

    static Vector2 OtherVelocityAt(PhysicsBody body, Vector2 point) =>
        body.type == PhysicsBody.BodyType.Static ? Vector2.zero : body.GetWorldPointVelocity(point);

    // ---------- Casse ----------

    // e : énergie / énergie de casse (≥ 1). localBreak : énergie (J) qu'il fallait pour casser à ce point.
    void Shatter(Vector2 point, in Impactor imp, Vector2 vRel, float mEff, Vector2 nWorld, float vn, float e, float localBreak)
    {
        _broken = true;
        _hits++;
        _shatterStep = LevelSimulation.Step;
        float k = math.clamp(math.log2(e), 0f, 6f);
        float m = 0.5f * math.min(_half.x, _half.y);
        _origin = new Vector2(math.clamp(point.x, -_half.x + m, _half.x - m), math.clamp(point.y, -_half.y + m, _half.y - m));
        _coreRadius = imp.coreRadius;

        var settings = new GlassFracture.Settings
        {
            rays = (int)math.round(radialCracks + radialCracksPerDoubling * k),
            firstRing = firstRingRadius / (1f + ringTightening * k),
            ringGrowth = ringGrowth,
            maxRingGap = maxRingGap,
            reach = crackReach + reachPerDoubling * k,
            ringContinuity = ringContinuity,
            angleJitter = angleJitter,
            radiusJitter = radiusJitter,
            waviness = waviness,
            branching = branching,
            curvature = curvature,
            seed = math.hash(new uint4(seed, math.asuint(_origin.x), math.asuint(_origin.y), (uint)_hits)) | 1u
        };
        var fracture = GlassFracture.Shared;
        for (int tries = 0; ; tries++)
        {
            fracture.Build(_half, _origin, settings);
            if (CountFragments(fracture) <= maxFragments || tries >= 16) break;
            settings.rays = math.max(4, settings.rays - math.max(1, settings.rays / 6));
            settings.firstRing *= 1.2f;
            settings.maxRingGap *= 1.2f;
            settings.branching *= 0.8f;
        }
        CopyFracture(fracture);

        if (_solid.isValid) _solid.Destroy();
        _solid = default;
        _touches = 0;
        _crackLineCount = 0;

        // Moment du détachement : tout de suite sous l'impacteur, ensuite au passage du front de fissure.
        float dt = Time.fixedDeltaTime;
        int step = LevelSimulation.Step;
        bool held = supportedEnds && !freeBody;
        for (int c = 0; c < _cellCount; c++)
        {
            _cellDust[c] = math.sqrt(_cellArea[c]) < minFragmentSize;
            if (held && _cellOuter[c] && _cellTouchesEnd[c] && !_cellDust[c]) _cellRelease[c] = Never;
            else if (crackSpeed <= 0f || _cellDust[c]) _cellRelease[c] = step;
            else _cellRelease[c] = step + (int)math.ceil(math.max(0f, _cellInner[c] - _coreRadius) / crackSpeed / dt);
            _launch[c] = _cellRelease[c] != Never;
        }
        var rng = new Unity.Mathematics.Random(settings.seed);
        Launch(_origin, vRel, imp, mEff, nWorld, vn, k, localBreak, ref rng);

        var shapeDef = SolidDefinition(true);
        shapeDef.worldDrawing = false; // cellules en place dessinées par LateUpdate (sans les coutures des parties convexes)
        for (int c = 0; c < _cellCount; c++)
        {
            if (_cellRelease[c] == step) { Release(c); continue; }
            int created = 0;
            for (int p = _cellPartStart[c], end = p + _cellPartCount[c]; p < end; p++)
            {
                var geometry = PolygonGeometry.Create(new System.ReadOnlySpan<Vector2>(_partVerts, p * GlassFracture.MaxVerts, _partVertCount[p]), 0f);
                _partGeom[p] = geometry;
                if (!geometry.isValid) continue;
                var shape = _body.CreateShape(geometry, shapeDef);
                shape.userData = new PhysicsUserData { objectValue = this, intValue = c + 1 };
                _partShape[p] = shape;
                created++;
            }
            if (created == 0) { _cellState[c] = CellNone; continue; }
            _cellState[c] = CellStatic;
            _staticCells++;
            if (_cellRelease[c] != Never) _pendingCells++;
        }
        DestroyBodyIfEmpty();
        Shattered?.Invoke(this, ToWorld(point), imp.dynamic || imp.mass > 0f || mEff > 0f ? e : 0f);
    }

    int CountFragments(GlassFracture f)
    {
        int n = 0;
        for (int c = 0; c < f.CellCount; c++)
            if (math.sqrt(f.Area[c]) >= minFragmentSize) n++;
        return n;
    }

    void CopyFracture(GlassFracture f)
    {
        int cells = f.CellCount, parts = f.PartCount;
        if (_cellState == null || _cellState.Length < cells)
        {
            int n = math.max(cells, 64);
            _cellPartStart = new int[n]; _cellPartCount = new int[n]; _cellRelease = new int[n];
            _cellCentroid = new Vector2[n]; _cellVelocity = new Vector2[n];
            _cellArea = new float[n]; _cellInner = new float[n]; _cellRadius = new float[n]; _cellSpin = new float[n];
            _cellOuter = new bool[n]; _cellTouchesEnd = new bool[n]; _cellDust = new bool[n]; _launch = new bool[n];
            _cellState = new byte[n];
            _cellBody = new PhysicsBody[n];
        }
        if (_partVertCount == null || _partVertCount.Length < parts)
        {
            int n = math.max(parts, 128);
            _partVerts = new Vector2[n * GlassFracture.MaxVerts];
            _partEdges = new byte[n * GlassFracture.MaxVerts];
            _partVertCount = new int[n];
            _partGeom = new PolygonGeometry[n];
            _partShape = new PhysicsShape[n];
        }
        _cellCount = cells;
        _partCount = parts;
        System.Array.Copy(f.CellPartStart, _cellPartStart, cells);
        System.Array.Copy(f.CellPartCount, _cellPartCount, cells);
        System.Array.Copy(f.Centroid, _cellCentroid, cells);
        System.Array.Copy(f.Area, _cellArea, cells);
        System.Array.Copy(f.InnerRadius, _cellInner, cells);
        System.Array.Copy(f.Radius, _cellRadius, cells);
        System.Array.Copy(f.Outer, _cellOuter, cells);
        System.Array.Copy(f.TouchesEnd, _cellTouchesEnd, cells);
        System.Array.Copy(f.PartVerts, _partVerts, parts * GlassFracture.MaxVerts);
        System.Array.Copy(f.PartEdges, _partEdges, parts * GlassFracture.MaxVerts);
        System.Array.Copy(f.PartVertCount, _partVertCount, parts);
        System.Array.Clear(_cellState, 0, cells);
        System.Array.Clear(_cellBody, 0, cells);
        System.Array.Clear(_partShape, 0, parts);
    }

    // Vitesses des cellules marquées (_launch) et réaction sur l'impacteur : énergie de rupture + élan cédé aux éclats.
    // Vitesse d'un éclat = vitesse de la vitre en ce point (vitre libre) + part de la vitesse relative de l'impacteur + éclatement.
    void Launch(Vector2 origin, Vector2 vRel, in Impactor imp, float mEff, Vector2 nWorld, float vn, float k, float localBreak,
                ref Unity.Mathematics.Random rng)
    {
        float speed = vRel.magnitude;
        Vector2 dir = speed > 1e-4f ? vRel / speed : -nWorld;
        float spread = spreadAngle * Mathf.Deg2Rad;
        float momentum = 0f;
        Vector2 gv = Vector2.zero, com = Vector2.zero;
        float gw = 0f;
        if (freeBody && _hasBody) { gv = _body.linearVelocity; gw = _body.angularVelocity; com = _body.worldCenterOfMass; }

        for (int c = 0; c < _cellCount; c++)
        {
            if (!_launch[c]) continue;
            Vector2 rel = _cellCentroid[c] - origin;
            float dist = rel.magnitude;
            // Emportés : les éclats sur la trajectoire de l'impacteur (sa largeur + burstRadius). Éclatement : selon la distance.
            Vector2 relWorld = ToWorldVec(rel);
            float lateral = math.abs(relWorld.x * dir.y - relWorld.y * dir.x);
            float depth = Vector2.Dot(relWorld, dir);
            float inPath = math.exp(-math.max(0f, lateral - imp.coreRadius) / burstRadius)
                         * (depth < -imp.coreRadius ? math.exp((depth + imp.coreRadius) / burstRadius) : 1f);
            float fall = math.exp(-dist / (imp.coreRadius + burstRadius));
            Vector2 along = vRel * (momentumTransfer * inPath);
            float angle = rng.NextFloat(-1f, 1f) * spread;
            float cs = math.cos(angle), sn = math.sin(angle);
            along = new Vector2(along.x * cs - along.y * sn, along.x * sn + along.y * cs) * (1f + speedJitter * rng.NextFloat(-1f, 1f));
            Vector2 radialDir = dist > 1e-5f ? ToWorldVec(rel / dist) : dir;
            Vector2 world = ToWorld(_cellCentroid[c]);
            Vector2 baseVelocity = gv + new Vector2(-(world.y - com.y), world.x - com.x) * gw;
            _cellVelocity[c] = along;
            _cellSpin[c] = gw + rng.NextFloat(-1f, 1f) * maxSpin * (0.2f + 0.8f * math.max(fall, inPath)) * math.min(1f, 0.1f + speed / 10f);
            momentum += density * _cellArea[c] * math.max(0f, Vector2.Dot(along, dir));
            _cellVelocity[c] += radialDir * (burstSpeed * k * fall) + baseVelocity;
        }

        if (!imp.dynamic || imp.mass <= 0f || mEff <= 0f) return;

        // L'impacteur paie l'énergie de rupture, puis l'élan des éclats (plafonné à ce qu'il a encore).
        float vnAfter = math.sqrt(math.max(0f, vn * vn - 2f * energyAbsorbed * localBreak / mEff));
        Vector2 delta = nWorld * ((vn - vnAfter) * mEff / imp.mass);
        float budget = imp.mass * math.max(0f, Vector2.Dot(vRel + delta, dir));
        float scale = momentum > budget ? budget / momentum : 1f;
        if (scale < 1f)
            for (int c = 0; c < _cellCount; c++)
            {
                if (!_launch[c]) continue;
                float a = Vector2.Dot(_cellVelocity[c] - gv, dir);
                if (a > 0f) _cellVelocity[c] -= dir * (a * (1f - scale));
            }
        delta -= dir * (momentum * scale * momentumCost / imp.mass);

        if (imp.blob != null)
        {
            for (int i = 0; i < imp.blob.CapsuleCount; i++)
            {
                var b = imp.blob.Capsule(i);
                b.linearVelocity += delta;
            }
        }
        else if (imp.body.isValid) imp.body.ApplyLinearImpulse(delta * imp.mass, imp.point, true);
    }

    void ReleaseDue(int step)
    {
        for (int c = 0; c < _cellCount; c++)
            if (_cellState[c] == CellStatic && _cellRelease[c] <= step) Release(c);
        DestroyBodyIfEmpty();
    }

    // Cellule → morceau dynamique (plusieurs formes convexes) ou poussière si trop petite.
    void Release(int c)
    {
        int first = _cellPartStart[c], end = first + _cellPartCount[c];
        if (_cellState[c] == CellStatic)
        {
            for (int p = first; p < end; p++)
            {
                if (_partShape[p].isValid) _partShape[p].Destroy();
                _partShape[p] = default;
            }
            _staticCells--;
            if (_cellRelease[c] != Never) _pendingCells--;
        }
        _cellState[c] = CellNone;

        Vector2 center = ToWorld(_cellCentroid[c]);
        if (_cellDust[c])
        {
            Dust?.Invoke(this, center, _cellVelocity[c], _cellArea[c]);
            return;
        }

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        bodyDef.position = center;
        bodyDef.rotation = PhysicsRotate.FromRadians(Angle);
        bodyDef.linearVelocity = _cellVelocity[c];
        bodyDef.angularVelocity = _cellSpin[c];
        bodyDef.sleepThreshold = sleepSpeed;
        var body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var shapeDef = SolidDefinition(false);
        shapeDef.worldDrawing = false; // dessiné par LateUpdate (contour du morceau, sans les coutures)
        var filter = shapeDef.contactFilter;
        if (blobInteraction != GlassBlobInteraction.Collide)
        {
            filter.categories = new PhysicsMask { bitMask = 1UL << LooseFragmentBit };
            filter.contacts = new PhysicsMask { bitMask = ~(1UL << BLOB.CategoryBit) };
        }
        if (!fragmentsCollideTogether) filter.groupIndex = -2;
        shapeDef.contactFilter = filter;

        int created = 0;
        for (int p = first; p < end; p++)
        {
            int n = _partVertCount[p];
            for (int i = 0; i < n; i++) _verts[i] = _partVerts[p * GlassFracture.MaxVerts + i] - _cellCentroid[c];
            var geometry = PolygonGeometry.Create(new System.ReadOnlySpan<Vector2>(_verts, 0, n), 0f);
            _partGeom[p] = geometry;
            if (!geometry.isValid) continue;
            body.CreateShape(geometry, shapeDef).userData = new PhysicsUserData { objectValue = this, intValue = -(c + 2) };
            created++;
        }
        if (created == 0)
        {
            body.Destroy();
            Dust?.Invoke(this, center, _cellVelocity[c], _cellArea[c]);
            return;
        }

        _cellBody[c] = body;
        _cellState[c] = CellFragment;
        _liveFragments++;
        if (cleanupDelay > 0f)
        {
            _cellRelease[c] = LevelSimulation.Step + (int)math.ceil(cleanupDelay / Time.fixedDeltaTime);
            _cleanupStep = math.min(_cleanupStep, _cellRelease[c]);
        }
    }

    void DestroyBodyIfEmpty()
    {
        if (_staticCells > 0 || !_hasBody) return;
        _body.Destroy();
        _body = default;
        _hasBody = false;
        _touches = 0;
    }

    // Distance (repère local) d'un point à une cellule (union de parties convexes) ; 0 à l'intérieur.
    float CellDistance(int c, Vector2 p, out Vector2 closest)
    {
        float best = float.MaxValue;
        closest = p;
        for (int part = _cellPartStart[c], end = part + _cellPartCount[c]; part < end; part++)
        {
            int n = _partVertCount[part], baseIndex = part * GlassFracture.MaxVerts;
            bool inside = true;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = _partVerts[baseIndex + i], b = _partVerts[baseIndex + (i + 1) % n];
                Vector2 ab = b - a;
                if (ab.x * (p.y - a.y) - ab.y * (p.x - a.x) < 0f) inside = false;
                float t = math.saturate(Vector2.Dot(p - a, ab) / math.max(1e-12f, ab.sqrMagnitude));
                Vector2 q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d < best) { best = d; closest = q; }
            }
            if (inside) { closest = p; return 0f; }
        }
        return math.sqrt(best);
    }

    // ---------- Morceaux : pulvérisation, nettoyage ----------

    void PulverizeTouching()
    {
        var blobs = BLOB.Active;
        for (int b = 0; b < blobs.Count; b++)
        {
            var blob = blobs[b];
            int n = blob.CapsuleCount;
            if (n == 0) continue;
            float zone = _half.magnitude + pulverizeZone + blob.diameter;
            if ((blob.Capsule(0).position - _pos).sqrMagnitude > zone * zone) continue;

            Vector2 center = Vector2.zero, velocity = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                var capsule = blob.Capsule(i);
                center += capsule.position;
                velocity += capsule.linearVelocity;
            }
            center /= n;
            velocity /= n;
            float mass = blob.density * math.PI * 0.25f * blob.diameter * blob.diameter;
            Vector2 start = velocity;
            float rb = 0.5f * blob.diameter;

            for (int c = 0; c < _cellCount; c++)
            {
                if (_cellState[c] != CellFragment) continue;
                var body = _cellBody[c];
                float r = rb + 0.6f * _cellRadius[c];
                Vector2 p = body.position;
                if ((p - center).sqrMagnitude > r * r) continue;

                Vector2 fv = body.linearVelocity;
                float m = density * _cellArea[c];
                velocity += (fv - velocity) * (m / (mass + m)); // choc mou : élan partagé
                Dust?.Invoke(this, p, velocity, _cellArea[c]);
                body.Destroy();
                _cellBody[c] = default;
                _cellState[c] = CellNone;
                _liveFragments--;
            }
            Vector2 delta = velocity - start;
            if (delta != Vector2.zero)
                for (int i = 0; i < n; i++)
                {
                    var capsule = blob.Capsule(i);
                    capsule.linearVelocity += delta;
                }
        }
    }

    void Cleanup(int step)
    {
        _cleanupStep = Never;
        for (int c = 0; c < _cellCount; c++)
        {
            if (_cellState[c] != CellFragment) continue;
            if (_cellRelease[c] > step) { _cleanupStep = math.min(_cleanupStep, _cellRelease[c]); continue; }
            if (_cellBody[c].isValid) _cellBody[c].Destroy();
            _cellBody[c] = default;
            _cellState[c] = CellNone;
            _liveFragments--;
        }
    }

    // Un morceau par pas, à tour de rôle (une lecture de position).
    void RemoveFallen()
    {
        for (int tries = 0; tries < _cellCount; tries++)
        {
            int c = _fallCursor;
            _fallCursor = (_fallCursor + 1) % _cellCount;
            if (_cellState[c] != CellFragment) continue;
            if (_cellBody[c].position.y < transform.position.y - removeBelow)
            {
                _cellBody[c].Destroy();
                _cellBody[c] = default;
                _cellState[c] = CellNone;
                _liveFragments--;
            }
            return;
        }
    }

    // ---------- Fissures (dessin) ----------

    // Étoile de fissures (radiales brisées + bouts d'anneau) autour d'un point, en segments locaux. Déterministe.
    void AddCrackMark(Vector2 point, float intensity)
    {
        var rng = new Unity.Mathematics.Random(math.hash(new uint4(seed, math.asuint(point.x), math.asuint(point.y), (uint)_hits)) | 1u);
        int rays = 3 + (int)math.round(5f * intensity);
        float size = math.max(0.05f, crackReach * 0.6f * intensity);
        float step = 2f * math.PI / rays, start = rng.NextFloat(0f, 2f * math.PI);
        for (int r = 0; r < rays; r++)
        {
            float a = start + step * (r + 0.4f * rng.NextFloat(-1f, 1f));
            float len = size * rng.NextFloat(0.5f, 1f);
            Vector2 prev = point;
            for (int s = 1; s <= 4; s++)
            {
                float b = a + 0.3f * rng.NextFloat(-1f, 1f);
                Vector2 next = point + new Vector2(math.cos(b), math.sin(b)) * (len * s / 4f);
                AddCrackLine(prev, next);
                prev = next;
            }
            if (rng.NextFloat() < 0.6f)
            {
                float ring = len * rng.NextFloat(0.25f, 0.6f);
                Vector2 p0 = point + new Vector2(math.cos(a), math.sin(a)) * ring;
                Vector2 pm = point + new Vector2(math.cos(a + 0.5f * step), math.sin(a + 0.5f * step)) * ring * rng.NextFloat(0.9f, 1.1f);
                Vector2 p1 = point + new Vector2(math.cos(a + step), math.sin(a + step)) * ring;
                AddCrackLine(p0, pm);
                AddCrackLine(pm, p1);
            }
        }
    }

    void AddCrackLine(Vector2 a, Vector2 b)
    {
        if (_crackLineCount >= MaxCrackLines) return;
        if (!ClipToPane(ref a, ref b)) return;
        _crackLines[2 * _crackLineCount] = a;
        _crackLines[2 * _crackLineCount + 1] = b;
        _crackLineCount++;
    }

    bool ClipToPane(ref Vector2 a, ref Vector2 b)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        for (int axis = 0; axis < 2; axis++)
        {
            float h = _half[axis];
            if (math.abs(d[axis]) < 1e-9f) { if (math.abs(a[axis]) > h) return false; continue; }
            float ta = (-h - a[axis]) / d[axis], tb = (h - a[axis]) / d[axis];
            t0 = math.max(t0, math.min(ta, tb));
            t1 = math.min(t1, math.max(ta, tb));
            if (t0 > t1) return false;
        }
        Vector2 start = a;
        a = start + d * t0;
        b = start + d * t1;
        return true;
    }

    // Rendu : fissures, cellules encore en place (selon le front) et morceaux, contour du morceau sans les coutures internes.
    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (freeBody && _hasBody) ReadPose();
        var world = PhysicsWorld.defaultWorld;
        for (int i = 0; i < _crackLineCount; i++)
            world.DrawLine(ToWorld(_crackLines[2 * i]), ToWorld(_crackLines[2 * i + 1]), crackColor, 0f);
        if (_cellCount == 0) return;

        var fill = new Color(glassColor.r, glassColor.g, glassColor.b, fillAlpha);
        float front = _coreRadius + (LevelSimulation.Step - _shatterStep) * Time.fixedDeltaTime * crackSpeed;
        var paneTransform = new PhysicsTransform(_pos, PhysicsRotate.FromRadians(Angle));
        for (int c = 0; c < _cellCount; c++)
        {
            byte state = _cellState[c];
            if (state == CellNone) continue;
            int first = _cellPartStart[c], end = first + _cellPartCount[c];
            if (state == CellStatic)
            {
                bool pending = _cellRelease[c] != Never;
                for (int p = first; p < end; p++)
                {
                    if (fillAlpha > 0f && _partGeom[p].isValid)
                        world.DrawGeometry(_partGeom[p], paneTransform, fill, 0f, PhysicsWorld.DrawFillOptions.Interior);
                    DrawEdges(world, p, Vector2.zero, _pos, _cos, _sin, true, pending, front);
                }
                continue;
            }
            var body = _cellBody[c];
            var t = body.transform;
            float a = t.rotation.radians;
            float cs = math.cos(a), sn = math.sin(a);
            for (int p = first; p < end; p++)
            {
                if (fillAlpha > 0f && _partGeom[p].isValid)
                    world.DrawGeometry(_partGeom[p], t, fill, 0f, PhysicsWorld.DrawFillOptions.Interior);
                DrawEdges(world, p, _cellCentroid[c], t.position, cs, sn, false, false, 0f);
            }
        }
    }

    void DrawEdges(PhysicsWorld world, int part, Vector2 offset, Vector2 position, float cs, float sn, bool inPlace, bool pending, float front)
    {
        int n = _partVertCount[part], baseIndex = part * GlassFracture.MaxVerts;
        for (int i = 0; i < n; i++)
        {
            byte kind = _partEdges[baseIndex + i];
            if (kind == GlassFracture.EdgeInternal) continue;
            Vector2 a = _partVerts[baseIndex + i], b = _partVerts[baseIndex + (i + 1) % n];
            if (pending && kind == GlassFracture.EdgeCrack && ((a - _origin).magnitude > front || (b - _origin).magnitude > front)) continue;
            Vector2 la = a - offset, lb = b - offset;
            Vector2 wa = position + new Vector2(la.x * cs - la.y * sn, la.x * sn + la.y * cs);
            Vector2 wb = position + new Vector2(lb.x * cs - lb.y * sn, lb.x * sn + lb.y * cs);
            world.DrawLine(wa, wb, inPlace && kind == GlassFracture.EdgeCrack ? crackColor : glassColor, 0f);
        }
    }

    // ---------- Aperçu Scene view ----------

    public Vector2[] Corners()
    {
        var h = HalfSize;
        return new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        var corners = Corners();
        Gizmos.matrix = LocalToWorld;
        Gizmos.color = glassColor;
        for (int i = 0; i < 4; i++) Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        // Repères de fragilité : milieu (le plus fragile) et appuis (vitre fixe).
        var h = HalfSize;
        Gizmos.color = new Color(glassColor.r, glassColor.g, glassColor.b, 0.5f);
        float tick = math.max(0.1f, 1.5f * h.y);
        Gizmos.DrawLine(new Vector3(0f, -tick), new Vector3(0f, tick));
        if (!freeBody)
        {
            Gizmos.DrawLine(new Vector3(-h.x, -tick), new Vector3(-h.x, tick));
            Gizmos.DrawLine(new Vector3(h.x, -tick), new Vector3(h.x, tick));
        }
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddConvex(corners);
        _pickArea.Draw(LocalToWorld);
#endif
    }
}
