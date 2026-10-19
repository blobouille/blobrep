using System.Collections.Generic;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

// Gadget grappin (à la Spider-Man), posé sur l'objet du BLOB. Touche appuyée : le filament part vers le point d'accroche
// (GrapplePoint) le plus proche en vue, s'y accroche et tient tant que la touche reste enfoncée. Relâchée : le filament
// se détache et se rembobine dans le blob ; une fois revenu, on peut retirer.
// La corde accrochée garde sa longueur : quand le blob se rapproche du point, elle se détend et pend (simulée à chaque pas).
// Un pique ou une scie la tranche : le bout côté point reste pendu au point, le reste revient au blob.
// Aucun corps créé tant qu'on ne tire pas : un blob qui porte le grappin sans s'en servir simule exactement pareil.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BLOB))]
public class GrappleGadget : BlobGadget, ILevelPhysics, ILevelTick
{
    public enum RopeAttach { NearestCapsule, Center }
    public enum RopeMode { Rigid, Elastic }
    public enum FilamentReturn { PassThrough, Collide }
    public enum RopeVisual { Soft, Straight }

    [Header("Commande")]
    [Tooltip("Touche du grappin (même système que les flippers, enregistrée à chaque pas : rejeu exact).\n" +
             "Même touche qu'un flipper = un seul geste pour les deux.")]
    public FlipperKey key = FlipperKey.LeftArrow;
    [Tooltip("Coché : tant que la touche est tenue, le grappin tire dès qu'un point devient visible.\n" +
             "Décoché : il faut un nouvel appui (un appui sans point visible ne fait rien).")]
    public bool fireWhileHeld = false;

    [Header("Visée")]
    [Tooltip("Portée : distance max entre le centre du blob et le centre du point d'accroche, en m (absolu).")]
    [Min(0.1f)] public float range = 8f;
    [Tooltip("Coché : le point doit être visible depuis le centre du blob (aucun mur, obstacle ou flipper entre les deux).\n" +
             "Les corps dynamiques (balles, filament) ne cachent rien.")]
    public bool requireLineOfSight = true;
    [Tooltip("Coché : seuls les points dans le cadre de l'écran (voir 'screenHalfSize') peuvent être visés. Vérifié au tir seulement :\n" +
             "un point qui sort de l'écran pendant l'aller ou une fois accroché ne lâche pas.")]
    public bool screenOnly = true;
    [Tooltip("Demi-largeur et demi-hauteur du cadre de l'écran, centré sur le blob, en m (absolu).\n" +
             "Valeur fixe, pas lue sur la caméra : la simulation ne doit dépendre ni de l'appareil (format d'écran) ni du rendu.\n" +
             "Caméra actuelle (orthographicSize 9, 16:9) : (16, 9).")]
    public Vector2 screenHalfSize = new Vector2(16f, 9f);
    [Tooltip("Champ de vision, en degrés, centré sur la direction de la vitesse du blob. 360 = tout autour.\n" +
             "Sous 3,6 km/h, la direction n'a pas de sens : tout autour. Parmi les points visibles, le plus proche est choisi.")]
    [Range(0f, 360f)] public float viewAngle = 360f;

    [Header("Tir")]
    [Tooltip("Vitesse du bout du filament à l'aller, en km/h (absolu). Très grand = accroche quasi instantanée.\n" +
             "Relâcher la touche pendant l'aller annule le tir (le filament revient).")]
    [SpeedKmh] [Min(1f)] public float shotSpeed = 80f;

    [Header("Corde")]
    [Tooltip("NearestCapsule : la corde tire la capsule face au point (joint Box2D) ; le blob s'étire comme une balle tenue par un fil.\n" +
             "Center : la corde tire le centre du blob, répartie sur toutes les capsules ; le blob garde sa forme.")]
    public RopeAttach attach = RopeAttach.NearestCapsule;
    [Tooltip("Rigid : corde de longueur max fixe (= distance au moment de l'accroche), molle si le blob se rapproche.\n" +
             "Elastic : ressort qui tire au-delà de sa longueur au repos, jamais ne pousse (voir « Corde élastique »).")]
    public RopeMode ropeMode = RopeMode.Rigid;
    [Tooltip("Raccourcit la corde tant que la touche est tenue, en km/h (absolu). 0 = longueur fixe.")]
    [SpeedKmh] [Min(0f)] public float reelInSpeed = 0f;
    [Tooltip("Longueur minimale de la corde, en m (absolu) : le rembobinage et l'enroulement s'arrêtent là.")]
    [Min(0.05f)] public float minLength = 0.5f;
    [Tooltip("Coché : la corde s'enroule sur le décor (murs, flippers…) qu'elle rencontre et se déroule quand le blob\n" +
             "repasse de l'autre côté. Décoché : elle traverse tout, seul le point d'accroche compte.\n" +
             "Avec 'spikesCut', elle ne s'enroule pas sur les piques et les scies : ils la tranchent.")]
    public bool wrap = false;
    [Tooltip("Écart entre la surface et le pivot d'enroulement, en m (absolu). Évite que la corde frotte la paroi.")]
    [Min(0.001f)] public float wrapOffset = 0.05f;
    [Tooltip("Coché : un pique ou une scie qui touche la corde accrochée la tranche. Le bout côté point reste pendu au point\n" +
             "(décor, sans effet sur le jeu), le bout côté blob revient comme au relâchement.")]
    public bool spikesCut = true;
    [Tooltip("Passes de calcul de la forme de la corde par pas. Plus = corde tendue plus droite et détente plus juste, plus coûteux.")]
    [Range(1, 16)] public int ropeIterations = 2;
    [Tooltip("Longueur d'un maillon de la corde accrochée, en m (absolu). Plus court = détente plus souple à l'oeil, plus coûteux\n" +
             "(chaque pas accroché calcule tous les maillons).")]
    [Min(0.05f)] public float ropeSegmentLength = 0.4f;
    [Tooltip("Vibration quand la corde molle se tend : écart max de la corde à la ligne droite, en m (absolu). 0 = aucune vibration.")]
    [Min(0f)] public float twangAmplitude = 0.15f;
    [Tooltip("Fréquence de cette vibration, en Hz.")]
    [Min(0f)] public float twangFrequency = 8f;
    [Tooltip("Amortissement de cette vibration, en 1/s. Plus haut = elle s'éteint plus vite.")]
    [Min(0f)] public float twangDamping = 6f;
    [Tooltip("Soft : la corde dessinée telle qu'elle est simulée (molle, elle pend ; tendue, elle vibre).\n" +
             "Straight : trait droit du point au blob. Rendu seulement : même physique (la coupe par les piques suit toujours\n" +
             "la vraie corde, molle comprise).")]
    public RopeVisual ropeVisual = RopeVisual.Soft;

    [Header("Corde élastique")]
    [Tooltip("Raideur du ressort, en Hz : fréquence d'oscillation du blob au bout de la corde. Plus haut = plus raide.")]
    [Min(0.01f)] public float springFrequency = 3f;
    [Tooltip("Amortissement du ressort (ratio, 1 = critique : pas de rebond).")]
    [Min(0f)] public float springDamping = 0.3f;
    [Tooltip("Longueur au repos, relative à la distance au moment de l'accroche. 1 = le ressort tire dès qu'il s'allonge ;\n" +
             "0,6 = il commence par ramener le blob vers le point.")]
    [Range(0.05f, 1f)] public float restLengthRatio = 0.6f;
    [Tooltip("Allongement max, relatif à la distance au moment de l'accroche : au-delà la corde devient rigide. 0 = sans limite.")]
    [Min(0f)] public float maxLengthRatio = 1.5f;

    [Header("Retour du filament")]
    [Tooltip("PassThrough : le filament revient en traversant tout (calculé par le script, aucun corps).\n" +
             "Collide : le filament est une chaîne de petits corps Box2D qui heurte le décor (il peut s'accrocher, voir 'maxReturnTime').")]
    public FilamentReturn returnMode = FilamentReturn.PassThrough;
    [Tooltip("Vitesse de rembobinage au moment du relâchement, en km/h (absolu).")]
    [SpeedKmh] [Min(0.1f)] public float reelSpeed = 10f;
    [Tooltip("Accélération du rembobinage, en m/s² (absolu) : comme un mètre ruban, il va de plus en plus vite.")]
    [Min(0f)] public float reelAcceleration = 120f;
    [Tooltip("Vitesse max du rembobinage, en km/h (absolu).")]
    [SpeedKmh] [Min(0.1f)] public float maxReelSpeed = 60f;
    [Tooltip("Longueur d'un maillon du filament qui revient, en m (absolu). Plus court = vagues plus fines, plus coûteux.")]
    [Min(0.05f)] public float segmentLength = 0.25f;
    [Tooltip("Gravité du filament et de la corde molle, relative à celle du monde. 0 = flotte, 1 = tombe comme le reste.")]
    [Min(0f)] public float filamentGravity = 0.5f;
    [Tooltip("Amortissement du filament et de la corde, en 1/s : freine leurs vagues. 0 = fouette sans fin.")]
    [Min(0f)] public float filamentDamping = 2f;
    [Tooltip("Mode Collide : rayon des maillons, en m (absolu).")]
    [Min(0.005f)] public float filamentRadius = 0.03f;
    [Tooltip("Durée max du retour, en s : au-delà, le reste du filament rentre d'un coup (filament coincé dans le décor).")]
    [Min(0.1f)] public float maxReturnTime = 1.5f;

    [Header("Debug")]
    [Tooltip("Couleur du filament.")]
    public Color ropeColor = new Color(0.95f, 0.95f, 0.8f, 1f);
    [Tooltip("Couleur du repère du point visé (gadget équipé, filament rentré).")]
    public Color targetColor = new Color(1f, 0.6f, 0.6f, 1f);

    enum State { Stowed, Shooting, Attached, Retracting }
    const int MaxParticles = 256;
    const int WrapRefineSteps = 6;
    const int MaxRemnants = 8;
    const int MaxRemnantCatchUp = 240;
    const int RemnantIterations = 4;

    // Bout de corde tranché, pendu à son point : décor seulement (animé dans LateUpdate, aucun effet sur le jeu).
    sealed class Remnant
    {
        public GrapplePoint point;
        public readonly Vector2[] p = new Vector2[MaxParticles];
        public readonly Vector2[] prev = new Vector2[MaxParticles];
        public int count;
        public float segment;
        public int step;
    }

    public override GadgetType Type => GadgetType.Grapple;

    BLOB _blob;
    State _state;
    bool _wasDown;

    GrapplePoint _target;
    Vector2 _tip;

    int _capsule; // NearestCapsule : capsule tirée ; -1 en mode Center
    float _length; // Rigid : longueur max ; Elastic : longueur au repos (depuis le point d'accroche, pivots compris)
    float _maxLength; // Elastic : longueur max (0 = aucune)
    readonly List<Vector2> _pivots = new List<Vector2>(); // [0] = point d'accroche ; le dernier = ancre actuelle
    readonly List<float> _pivotSides = new List<float>(); // _pivotSides[k - 1] = sens d'enroulement du pivot k
    float _pivotsLength;
    Vector2 _lastAttachPoint;
    PhysicsBody _anchorBody;
    PhysicsDistanceJoint _joint;
    float _jointFree = -1f;

    // Corde accrochée, de l'ancre actuelle (_r[0]) au blob (_r[_rCount - 1]).
    Vector2[] _r;
    Vector2[] _rPrev;
    Vector2[] _tmpA;
    Vector2[] _tmpB;
    int _rCount;
    float _ropeSegment;
    bool _ropeTaut;
    float[] _twang; // écart perpendiculaire de chaque maillon au moment de se tendre (vibration)
    bool _twangActive;
    float _twangA, _twangV;

    // Filament qui revient, du blob (_first) au bout (_count - 1).
    Vector2[] _p;
    Vector2[] _prev;
    PhysicsBody[] _links;
    int _first, _count;
    int _kinematicLink;
    float _segment;
    float _reel;
    float _returnTime;
    Vector2 _gravity;
    Vector2 _reelPoint;

    // Forme complète de la corde, du blob au point d'accroche (relâchement, coupe).
    readonly List<Vector2> _poly = new List<Vector2>();
    readonly List<Vector2> _polyVel = new List<Vector2>();

    Remnant[] _remnants;
    int _remnantCount;

    readonly List<GrapplePoint> _candidates = new List<GrapplePoint>();
    readonly List<float> _candidateDist = new List<float>();

    bool IsCollide => returnMode == FilamentReturn.Collide;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        _blob = GetComponent<BLOB>();
        _state = State.Stowed;
        _wasDown = false;
        _target = null;
        _pivots.Clear();
        _pivotSides.Clear();
        _p ??= new Vector2[MaxParticles];
        _prev ??= new Vector2[MaxParticles];
        _links ??= new PhysicsBody[MaxParticles];
        _r ??= new Vector2[MaxParticles];
        _rPrev ??= new Vector2[MaxParticles];
        _tmpA ??= new Vector2[MaxParticles];
        _tmpB ??= new Vector2[MaxParticles];
        _twang ??= new float[MaxParticles];
        _first = _count = 0;
        _rCount = 0;
        _remnantCount = 0;
    }

    void OnDisable()
    {
        DestroyRope();
        DestroyLinks();
        _state = State.Stowed;
    }

    public void LevelTick()
    {
        bool down = LevelInput.IsDown(key);
        bool pressed = down && !_wasDown;
        _wasDown = down;
        if (_state == State.Stowed && !Equipped) return;
        if (_blob == null || _blob.CapsuleCount == 0) return;

        switch (_state)
        {
            case State.Stowed:
                if (down && (pressed || fireWhileHeld) && !_blob.IsBroken) TryFire();
                break;
            case State.Shooting: TickShot(down); break;
            case State.Attached: TickAttached(down); break;
            case State.Retracting: TickReturn(); break;
        }
    }

    // ---------- Visée ----------

    void TryFire()
    {
        BlobCenter(out var c, out var v);
        var target = FindTarget(c, v);
        if (target == null) return;
        _target = target;
        _tip = c;
        _state = State.Shooting;
    }

    GrapplePoint FindTarget(Vector2 c, Vector2 v)
    {
        _candidates.Clear();
        _candidateDist.Clear();
        float range2 = range * range;
        bool cone = viewAngle < 360f && v.sqrMagnitude > 1f;
        float cosHalf = Mathf.Cos(0.5f * viewAngle * Mathf.Deg2Rad);
        Vector2 dir = cone ? v.normalized : Vector2.zero;

        var points = GrapplePoint.Active;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 d = points[i].Position - c;
            float dist2 = d.sqrMagnitude;
            if (dist2 > range2) continue;
            if (screenOnly && (Mathf.Abs(d.x) > screenHalfSize.x || Mathf.Abs(d.y) > screenHalfSize.y)) continue;
            if (cone && Vector2.Dot(d, dir) < cosHalf * Mathf.Sqrt(dist2)) continue;
            int at = _candidates.Count;
            while (at > 0 && _candidateDist[at - 1] > dist2) at--;
            _candidates.Insert(at, points[i]);
            _candidateDist.Insert(at, dist2);
        }
        for (int i = 0; i < _candidates.Count; i++)
            if (!requireLineOfSight || !Blocked(c, _candidates[i].Position, 0f, false, out _, out _)) return _candidates[i];
        return null;
    }

    // Premier corps non dynamique (décor, obstacle, flipper) entre 'from' et 'to', au-delà de 'skip' m de 'from'.
    // 'ignoreSpikes' : les piques et scies ne comptent pas (la corde les traverse pour être tranchée).
    static bool Blocked(Vector2 from, Vector2 to, float skip, bool ignoreSpikes, out Vector2 point, out Vector2 normal)
    {
        point = normal = default;
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 1e-5f) return false;
        var hits = PhysicsWorld.defaultWorld.CastRay(new PhysicsQuery.CastRayInput(from, delta), PhysicsQuery.QueryFilter.Everything,
                                                     PhysicsQuery.WorldCastMode.AllSorted, Allocator.Temp);
        bool found = false;
        for (int i = 0; i < hits.Length && !found; i++)
        {
            var hit = hits[i];
            if (hit.fraction * length <= skip) continue;
            if (hit.shape.body.type == PhysicsBody.BodyType.Dynamic) continue;
            if (ignoreSpikes && hit.shape.userData.objectValue is SpikedHazard) continue;
            point = hit.point;
            normal = hit.normal;
            found = true;
        }
        hits.Dispose();
        return found;
    }

    // ---------- Aller ----------

    void TickShot(bool down)
    {
        if (!down || !Equipped || _blob.IsBroken)
        {
            BlobCenter(out var c, out var v);
            _poly.Clear();
            _polyVel.Clear();
            _poly.Add(c); _polyVel.Add(v);
            _poly.Add(_tip); _polyVel.Add(Vector2.zero);
            BeginReturn();
            return;
        }
        Vector2 goal = _target.Position;
        Vector2 d = goal - _tip;
        float step = shotSpeed * Time.fixedDeltaTime;
        if (d.sqrMagnitude <= step * step) Attach();
        else _tip += d * (step / d.magnitude);
    }

    // ---------- Accroché ----------

    void Attach()
    {
        Vector2 anchor = _target.Position;
        _tip = anchor;
        _pivots.Clear();
        _pivotSides.Clear();
        _pivots.Add(anchor);
        _pivotsLength = 0f;
        RemoveRemnantsAt(_target);

        Vector2 b;
        if (attach == RopeAttach.NearestCapsule)
        {
            _capsule = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _blob.CapsuleCount; i++)
            {
                float d2 = (_blob.Capsule(i).position - anchor).sqrMagnitude;
                if (d2 < best) { best = d2; _capsule = i; }
            }
            b = _blob.Capsule(_capsule).position;
        }
        else
        {
            _capsule = -1;
            BlobCenter(out b, out _);
        }
        _lastAttachPoint = b;

        float dist = Mathf.Max(minLength, Vector2.Distance(anchor, b));
        if (ropeMode == RopeMode.Rigid)
        {
            _length = dist;
            _maxLength = 0f;
        }
        else
        {
            _length = Mathf.Max(minLength, dist * restLengthRatio);
            _maxLength = maxLengthRatio > 0f ? Mathf.Max(_length, dist * maxLengthRatio) : 0f;
        }

        if (_capsule >= 0) CreateJoint(anchor);
        RebuildRope(anchor, b, _length);
        _state = State.Attached;
    }

    void CreateJoint(Vector2 anchor)
    {
        var world = PhysicsWorld.defaultWorld;
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        bodyDef.position = anchor;
        _anchorBody = world.CreateBody(bodyDef);

        var def = PhysicsDistanceJointDefinition.defaultDefinition;
        def.bodyA = _anchorBody;
        def.bodyB = _blob.Capsule(_capsule);
        def.localAnchorA = Vector2.zero;
        def.localAnchorB = Vector2.zero;
        def.collideConnected = false;
        def.worldDrawing = false; // la corde est dessinée par LateUpdate
        def.enableSpring = true;
        def.enableLimit = true;
        def.minDistanceLimit = 0f;
        if (ropeMode == RopeMode.Rigid)
        {
            def.springFrequency = 0f;
            def.distance = _length;
            def.maxDistanceLimit = _length;
        }
        else
        {
            def.springFrequency = springFrequency;
            def.springDamping = springDamping;
            def.springUpperForce = 0f;
            def.distance = _length;
            def.maxDistanceLimit = _maxLength > 0f ? _maxLength : 1e6f;
        }
        _joint = world.CreateJoint(def);
        _jointFree = _length;
    }

    void TickAttached(bool down)
    {
        if (!down || !Equipped || _blob.IsBroken || (_capsule >= 0 && !_joint.isValid))
        {
            Release();
            return;
        }

        Vector2 b, v = default;
        if (_capsule >= 0) b = _blob.Capsule(_capsule).position;
        else BlobCenter(out b, out v);

        float dt = Time.fixedDeltaTime;
        if (reelInSpeed > 0f) _length = Mathf.Max(minLength, _length - reelInSpeed * dt);
        bool anchorMoved = wrap && UpdateWrap(b);
        _lastAttachPoint = b;

        float free = Mathf.Max(0.05f, _length - _pivotsLength);
        float freeMax = _maxLength > 0f ? Mathf.Max(free, _maxLength - _pivotsLength) : 0f;
        if (_capsule >= 0) UpdateJoint(free, freeMax);
        else PullCenter(b, v, free, freeMax);

        Vector2 anchor = _pivots[_pivots.Count - 1];
        if (anchorMoved) RebuildRope(anchor, b, free);
        else StepRope(anchor, b, free);

        if (spikesCut) CheckCut(b);
    }

    void UpdateJoint(float free, float freeMax)
    {
        if (free == _jointFree) return;
        _jointFree = free;
        _joint.distance = free;
        if (ropeMode == RopeMode.Rigid) _joint.maxDistanceLimit = free;
        else if (freeMax > 0f) _joint.maxDistanceLimit = freeMax;
        _joint.WakeBodies();
    }

    // Mode Center : même corde, appliquée en vitesse au centre du blob (toutes les capsules reçoivent la même correction).
    void PullCenter(Vector2 c, Vector2 v, float free, float freeMax)
    {
        Vector2 anchor = _pivots[_pivots.Count - 1];
        Vector2 d = c - anchor;
        float dist = d.magnitude;
        if (dist < 1e-4f) return;
        Vector2 n = d / dist;
        float vr = Vector2.Dot(v, n);
        float dt = Time.fixedDeltaTime;

        float dv = 0f;
        if (ropeMode == RopeMode.Rigid)
        {
            dv = Mathf.Min(0f, LimitSpeed(free, dist, dt) - vr);
        }
        else
        {
            float stretch = dist - free;
            if (stretch > 0f)
            {
                // Ressort implicite (stable quelle que soit la raideur), qui ne fait que tirer.
                float w = 2f * Mathf.PI * springFrequency;
                float den = 1f + 2f * springDamping * w * dt + w * w * dt * dt;
                dv = Mathf.Min(0f, (-dt * w * w * stretch - vr * (2f * springDamping * w * dt + w * w * dt * dt)) / den);
            }
            if (freeMax > 0f) dv = Mathf.Min(dv, LimitSpeed(freeMax, dist, dt) - vr);
        }
        if (dv >= 0f) return;

        Vector2 delta = n * dv;
        for (int i = 0; i < _blob.CapsuleCount; i++)
        {
            var body = _blob.Capsule(i);
            body.linearVelocity += delta;
        }
    }

    // Vitesse radiale max pour ne pas dépasser 'limit' au pas suivant (au-delà : retour progressif, moitié de l'écart par pas).
    static float LimitSpeed(float limit, float dist, float dt) => (dist <= limit ? limit - dist : 0.5f * (limit - dist)) / dt;

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Enroulement : si le décor coupe la corde entre l'ancre et le blob, un pivot est ajouté près du coin (recherche entre
    // la position précédente du blob, encore en vue, et l'actuelle). Déroulement : le blob repasse de l'autre côté du pivot.
    bool UpdateWrap(Vector2 b)
    {
        bool changed = false;
        while (_pivots.Count > 1)
        {
            int k = _pivots.Count - 1;
            Vector2 a = _pivots[k - 1], p = _pivots[k];
            if (Cross(p - a, b - p) * _pivotSides[k - 1] >= 0f) break;
            _pivotsLength -= Vector2.Distance(a, p);
            _pivots.RemoveAt(k);
            _pivotSides.RemoveAt(k - 1);
            changed = true;
        }

        Vector2 last = _pivots[_pivots.Count - 1];
        float skip = 2f * wrapOffset;
        if (_length - _pivotsLength > minLength && Blocked(last, b, skip, spikesCut, out var point, out var normal))
        {
            Vector2 lo = _lastAttachPoint, hi = b;
            if (!Blocked(last, lo, skip, spikesCut, out _, out _))
                for (int i = 0; i < WrapRefineSteps; i++)
                {
                    Vector2 mid = 0.5f * (lo + hi);
                    if (Blocked(last, mid, skip, spikesCut, out var p2, out var n2)) { hi = mid; point = p2; normal = n2; }
                    else lo = mid;
                }

            Vector2 pivot = point + normal * wrapOffset;
            float side = Cross(pivot - last, b - pivot);
            if ((pivot - last).sqrMagnitude > 1e-4f && side != 0f)
            {
                _pivotsLength += Vector2.Distance(last, pivot);
                _pivots.Add(pivot);
                _pivotSides.Add(Mathf.Sign(side));
                changed = true;
            }
        }

        if (changed && _capsule >= 0 && _anchorBody.isValid)
        {
            _anchorBody.position = _pivots[_pivots.Count - 1];
            _jointFree = -1f;
        }
        return changed;
    }

    // ---------- Forme de la corde accrochée ----------

    int RopeCount(float free) => Mathf.Clamp(Mathf.CeilToInt(free / ropeSegmentLength), 1, MaxParticles - 1) + 1;

    // Corde droite et immobile de 'anchor' à 'b' (accroche, changement de pivot : la corde est alors tendue).
    void RebuildRope(Vector2 anchor, Vector2 b, float free)
    {
        _rCount = RopeCount(free);
        _ropeSegment = free / (_rCount - 1);
        _ropeTaut = true;
        _twangActive = false;
        for (int i = 0; i < _rCount; i++)
        {
            _r[i] = Vector2.Lerp(anchor, b, i / (float)(_rCount - 1));
            _rPrev[i] = _r[i];
        }
    }

    // Garde la forme actuelle avec un autre nombre de maillons (la corde raccourcit).
    void ResampleRope(int count)
    {
        Resample(_r, _rPrev, _rCount, _tmpA, _tmpB, count);
        System.Array.Copy(_tmpA, _r, count);
        System.Array.Copy(_tmpB, _rPrev, count);
        _rCount = count;
    }

    static void Resample(Vector2[] src, Vector2[] srcPrev, int srcCount, Vector2[] dst, Vector2[] dstPrev, int count)
    {
        float total = 0f;
        for (int i = 1; i < srcCount; i++) total += Vector2.Distance(src[i - 1], src[i]);
        int k = 1;
        float before = 0f, segLen = Vector2.Distance(src[0], src[1]);
        for (int j = 0; j < count; j++)
        {
            float s = total * j / (count - 1);
            while (s > before + segLen && k < srcCount - 1)
            {
                before += segLen;
                k++;
                segLen = Vector2.Distance(src[k - 1], src[k]);
            }
            float t = segLen > 1e-6f ? Mathf.Clamp01((s - before) / segLen) : 1f;
            dst[j] = Vector2.Lerp(src[k - 1], src[k], t);
            dstPrev[j] = Vector2.Lerp(srcPrev[k - 1], srcPrev[k], t);
        }
    }

    // Corde de longueur 'free' tenue entre l'ancre et le blob : Verlet (gravité, inertie) puis passes aller-retour depuis
    // chaque bout (FABRIK) qui remettent chaque maillon à sa longueur : plus courte que 'free' entre les bouts, la corde ploie et pend.
    // Calcul en flottants (les opérateurs de Vector2 coûtent cher à chaque pas du rejeu accéléré).
    void StepRope(Vector2 anchor, Vector2 b, float free)
    {
        int count = RopeCount(free);
        if (count != _rCount) ResampleRope(count);
        int last = _rCount - 1;
        float seg = free / last;
        _ropeSegment = seg;
        var r = _r;
        var rp = _rPrev;

        // Tendue : ligne droite + vibration amortie de la forme qu'elle avait en se tendant (écart perpendiculaire plafonné
        // à 'twangAmplitude'), oscillateur en additions / multiplications seulement. L'ancienne position reste dans _rPrev :
        // la détente qui suit garde l'élan.
        float ax = anchor.x, ay = anchor.y, dx = b.x - ax, dy = b.y - ay;
        float dist2 = dx * dx + dy * dy;
        if (dist2 >= TautRatio * TautRatio * free * free)
        {
            float inv = 1f / last;
            float len = Mathf.Sqrt(dist2);
            float nx = len > 1e-6f ? -dy / len : 0f, ny = len > 1e-6f ? dx / len : 0f;
            float twangDt = Time.fixedDeltaTime;
            if (!_ropeTaut) StartTwang(ax, ay, nx, ny, last);
            else if (_twangActive)
            {
                float w = 2f * Mathf.PI * twangFrequency;
                _twangV -= (w * w * _twangA + 2f * twangDamping * _twangV) * twangDt;
                _twangA += _twangV * twangDt;
                if (_twangA * _twangA < 1e-4f && _twangV * _twangV < 1e-4f * w * w) _twangActive = false;
            }
            float amp = _twangActive ? _twangA : 0f;
            for (int i = 0; i <= last; i++)
            {
                float y = amp * _twang[i];
                rp[i] = r[i];
                r[i].x = ax + dx * (i * inv) + nx * y;
                r[i].y = ay + dy * (i * inv) + ny * y;
            }
            return;
        }
        _ropeTaut = false;

        float dt = Time.fixedDeltaTime;
        float keep = Mathf.Max(0f, 1f - filamentDamping * dt);
        Vector2 gravity = PhysicsWorld.defaultWorld.gravity;
        float gx = gravity.x * filamentGravity * dt * dt, gy = gravity.y * filamentGravity * dt * dt;
        for (int i = 1; i < last; i++)
        {
            float cx = r[i].x, cy = r[i].y;
            r[i].x = cx + (cx - rp[i].x) * keep + gx;
            r[i].y = cy + (cy - rp[i].y) * keep + gy;
            rp[i].x = cx;
            rp[i].y = cy;
        }
        rp[0] = r[0];
        rp[last] = r[last];

        for (int it = 0; it < ropeIterations; it++)
        {
            float px = ax, py = ay;
            for (int i = 1; i <= last; i++)
            {
                float x = r[i].x, y = r[i].y, ex = x - px, ey = y - py, len2 = ex * ex + ey * ey;
                if (len2 > 1e-12f)
                {
                    float k = seg / Mathf.Sqrt(len2);
                    x = px + ex * k;
                    y = py + ey * k;
                    r[i].x = x;
                    r[i].y = y;
                }
                px = x;
                py = y;
            }
            px = b.x;
            py = b.y;
            r[last] = b;
            for (int i = last - 1; i >= 0; i--)
            {
                float x = r[i].x, y = r[i].y, ex = x - px, ey = y - py, len2 = ex * ex + ey * ey;
                if (len2 > 1e-12f)
                {
                    float k = seg / Mathf.Sqrt(len2);
                    x = px + ex * k;
                    y = py + ey * k;
                    r[i].x = x;
                    r[i].y = y;
                }
                px = x;
                py = y;
            }
        }
        r[0] = anchor;
    }

    const float TautRatio = 0.999f;

    void StartTwang(float ax, float ay, float nx, float ny, int last)
    {
        _ropeTaut = true;
        float max = 0f;
        for (int i = 0; i <= last; i++)
        {
            float y = (_r[i].x - ax) * nx + (_r[i].y - ay) * ny;
            _twang[i] = y;
            float a = y < 0f ? -y : y;
            if (a > max) max = a;
        }
        _twang[0] = 0f;
        _twang[last] = 0f;
        _twangActive = max > 1e-3f && twangAmplitude > 0f && twangFrequency > 0f;
        float scale = max > twangAmplitude ? twangAmplitude / max : 1f;
        for (int i = 1; i < last; i++) _twang[i] *= scale;
        _twangA = 1f;
        _twangV = 0f;
    }

    static void Limit(Vector2[] p, int i, int from, float seg)
    {
        Vector2 d = p[i] - p[from];
        float len2 = d.sqrMagnitude;
        if (len2 > seg * seg) p[i] = p[from] + d * (seg / Mathf.Sqrt(len2));
    }

    // Forme complète, du blob au point d'accroche, avec la vitesse de chaque point (corde accrochée).
    void BuildPoly(Vector2 b, Vector2 bVelocity)
    {
        float dt = Time.fixedDeltaTime;
        _poly.Clear();
        _polyVel.Clear();
        _poly.Add(b); _polyVel.Add(bVelocity);
        for (int i = _rCount - 2; i >= 1; i--) { _poly.Add(_r[i]); _polyVel.Add((_r[i] - _rPrev[i]) / dt); }
        for (int k = _pivots.Count - 1; k >= 0; k--) { _poly.Add(_pivots[k]); _polyVel.Add(Vector2.zero); }
    }

    void AttachPointState(out Vector2 b, out Vector2 v)
    {
        if (_capsule >= 0 && _blob.CapsuleCount > _capsule)
        {
            var body = _blob.Capsule(_capsule);
            b = body.position;
            v = body.linearVelocity;
        }
        else BlobCenter(out b, out v);
    }

    void Release()
    {
        AttachPointState(out var b, out var v);
        BuildPoly(b, v);
        BeginReturn();
    }

    void DestroyRope()
    {
        if (_joint.isValid) _joint.Destroy();
        _joint = default;
        if (_anchorBody.isValid) _anchorBody.Destroy();
        _anchorBody = default;
    }

    // ---------- Coupe par les piques ----------

    static PhysicsQuery.QueryFilter HazardFilter
    {
        get
        {
            var filter = PhysicsQuery.QueryFilter.Everything;
            filter.hitCategories = new PhysicsMask { bitMask = 1UL << BlobHazard.CategoryBit };
            return filter;
        }
    }

    // Tri bon marché d'abord (boîte de toute la corde, puis de chaque maillon), lancer de rayon seulement près d'un obstacle.
    void CheckCut(Vector2 b)
    {
        float minX = b.x, minY = b.y, maxX = b.x, maxY = b.y;
        var r = _r;
        for (int i = 0; i < _rCount; i++)
        {
            float x = r[i].x, y = r[i].y;
            if (x < minX) minX = x; else if (x > maxX) maxX = x;
            if (y < minY) minY = y; else if (y > maxY) maxY = y;
        }
        for (int k = 0; k + 1 < _pivots.Count; k++)
        {
            Vector2 p = _pivots[k];
            if (p.x < minX) minX = p.x; else if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; else if (p.y > maxY) maxY = p.y;
        }
        var world = PhysicsWorld.defaultWorld;
        var filter = HazardFilter;
        var box = new PhysicsAABB { lowerBound = new Vector2(minX, minY), upperBound = new Vector2(maxX, maxY) };
        if (!world.TestOverlapAABB(box, filter)) return;

        _r[_rCount - 1] = b;
        BuildPoly(b, Vector2.zero);
        for (int i = 0; i + 1 < _poly.Count; i++)
        {
            Vector2 a = _poly[i], c = _poly[i + 1];
            if (!world.TestOverlapAABB(new PhysicsAABB { lowerBound = Vector2.Min(a, c), upperBound = Vector2.Max(a, c) }, filter)) continue;
            var hits = world.CastRay(new PhysicsQuery.CastRayInput(a, c - a), filter, PhysicsQuery.WorldCastMode.AllSorted, Allocator.Temp);
            bool cut = false;
            Vector2 point = default;
            for (int h = 0; h < hits.Length && !cut; h++)
                if (hits[h].shape.userData.objectValue is SpikedHazard) { cut = true; point = hits[h].point; }
            hits.Dispose();
            if (!cut) continue;
            Cut(i, point);
            return;
        }
    }

    // Coupe sur le segment 'segment' de _poly : côté point → bout pendu, côté blob → retour.
    void Cut(int segment, Vector2 point)
    {
        AddRemnant(segment, point);

        AttachPointState(out _, out var v);
        _polyVel[0] = v;
        Vector2 velocity = Vector2.Lerp(_polyVel[segment], _polyVel[segment + 1], 0.5f);
        _poly.RemoveRange(segment + 1, _poly.Count - segment - 1);
        _polyVel.RemoveRange(segment + 1, _polyVel.Count - segment - 1);
        _poly.Add(point);
        _polyVel.Add(velocity);
        BeginReturn();
    }

    void AddRemnant(int segment, Vector2 point)
    {
        _remnants ??= new Remnant[MaxRemnants];
        if (_remnantCount == MaxRemnants)
        {
            var oldest = _remnants[0];
            System.Array.Copy(_remnants, 1, _remnants, 0, MaxRemnants - 1);
            _remnants[MaxRemnants - 1] = oldest;
            _remnantCount--;
        }
        var r = _remnants[_remnantCount] ??= new Remnant();
        _remnantCount++;

        // Points du point d'accroche jusqu'à la coupe ; maillons de 'segmentLength'.
        int src = 0;
        for (int i = _poly.Count - 1; i > segment && src < MaxParticles - 1; i--, src++)
        {
            _tmpA[src] = _poly[i];
            _tmpB[src] = _poly[i];
        }
        _tmpA[src] = point;
        _tmpB[src] = point;
        src++;

        float total = 0f;
        for (int i = 1; i < src; i++) total += Vector2.Distance(_tmpA[i - 1], _tmpA[i]);
        int count = Mathf.Clamp(Mathf.CeilToInt(total / segmentLength) + 1, 2, MaxParticles);
        if (src < 2) { _tmpA[1] = _tmpA[0]; _tmpB[1] = _tmpB[0]; src = 2; }
        Resample(_tmpA, _tmpB, src, r.p, r.prev, count);
        r.point = _target;
        r.count = count;
        r.segment = Mathf.Max(1e-3f, total / (count - 1));
        r.step = LevelSimulation.Step;
    }

    void RemoveRemnantsAt(GrapplePoint point)
    {
        int kept = 0;
        for (int i = 0; i < _remnantCount; i++)
        {
            var r = _remnants[i];
            if (r.point == point) continue;
            _remnants[i] = _remnants[kept];
            _remnants[kept++] = r;
        }
        _remnantCount = kept;
    }

    // ---------- Retour ----------

    // Filament posé le long de _poly (du blob au bout), avec la vitesse de chaque point.
    void BeginReturn()
    {
        DestroyRope();
        DestroyLinks();

        float total = 0f;
        for (int i = 1; i < _poly.Count; i++) total += Vector2.Distance(_poly[i - 1], _poly[i]);
        int count = Mathf.Clamp(Mathf.CeilToInt(total / segmentLength) + 1, 2, MaxParticles);
        _segment = Mathf.Max(1e-3f, total / (count - 1));
        float dt = Time.fixedDeltaTime;

        int k = 1;
        float before = 0f, segLen = _poly.Count > 1 ? Vector2.Distance(_poly[0], _poly[1]) : 0f;
        for (int j = 0; j < count; j++)
        {
            Vector2 pos, vel;
            if (_poly.Count < 2) { pos = _poly[0]; vel = _polyVel[0]; }
            else
            {
                float s = j * _segment;
                while (s > before + segLen && k < _poly.Count - 1)
                {
                    before += segLen;
                    k++;
                    segLen = Vector2.Distance(_poly[k - 1], _poly[k]);
                }
                float t = segLen > 1e-6f ? Mathf.Clamp01((s - before) / segLen) : 1f;
                pos = Vector2.Lerp(_poly[k - 1], _poly[k], t);
                vel = Vector2.Lerp(_polyVel[k - 1], _polyVel[k], t);
            }
            _p[j] = pos;
            _prev[j] = pos - vel * dt;
        }

        _first = 0;
        _count = count;
        _kinematicLink = -1;
        _reel = reelSpeed;
        _returnTime = 0f;
        _reelPoint = _poly[0];
        _gravity = PhysicsWorld.defaultWorld.gravity * filamentGravity;
        _pivots.Clear();
        _pivotSides.Clear();
        _rCount = 0;
        _state = State.Retracting;

        if (IsCollide) CreateLinks(dt);
    }

    void CreateLinks(float dt)
    {
        var world = PhysicsWorld.defaultWorld;
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Dynamic;
        bodyDef.gravityScale = filamentGravity;
        bodyDef.linearDamping = filamentDamping;

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.density = 1f;
        var material = shapeDef.surfaceMaterial;
        material.friction = 0.2f;
        material.bounciness = 0f;
        material.customColor = ropeColor;
        shapeDef.surfaceMaterial = material;
        var filter = shapeDef.contactFilter;
        filter.groupIndex = -1; // même groupe que les capsules des blobs : ne les touche pas
        shapeDef.contactFilter = filter;
        var circle = new CircleGeometry { center = Vector2.zero, radius = filamentRadius };

        var jointDef = PhysicsDistanceJointDefinition.defaultDefinition;
        jointDef.enableSpring = true;
        jointDef.springFrequency = 0f;
        jointDef.enableLimit = true;
        jointDef.minDistanceLimit = 0f;
        jointDef.distance = _segment;
        jointDef.maxDistanceLimit = _segment;
        jointDef.collideConnected = false;
        jointDef.worldDrawing = false;

        for (int i = 0; i < _count; i++)
        {
            bodyDef.position = _p[i];
            bodyDef.linearVelocity = (_p[i] - _prev[i]) / dt;
            _links[i] = world.CreateBody(bodyDef);
            _links[i].CreateShape(circle, shapeDef);
            if (i == 0) continue;
            jointDef.bodyA = _links[i - 1];
            jointDef.bodyB = _links[i];
            world.CreateJoint(jointDef);
        }
    }

    void DestroyLinks()
    {
        for (int i = 0; i < _count; i++)
            if (_links != null && _links[i].isValid) _links[i].Destroy();
        if (_links != null) for (int i = 0; i < _count; i++) _links[i] = default;
        _first = _count = 0;
    }

    Vector2 LinkPosition(int i) => IsCollide ? _links[i].position : _p[i];

    // Le bout côté blob est tiré vers son centre (il suit le blob + rembobinage) ; le reste suit par la corde.
    // Chaque maillon arrivé au centre est avalé.
    void TickReturn()
    {
        float dt = Time.fixedDeltaTime;
        _returnTime += dt;
        if (_returnTime > maxReturnTime)
        {
            EndReturn();
            return;
        }
        _reel = Mathf.Min(maxReelSpeed, _reel + reelAcceleration * dt);

        Vector2 r = BlobCenterPosition();
        Vector2 follow = r - _reelPoint;
        _reelPoint = r;

        float absorb = Mathf.Max(_reel * dt, 0.05f);
        while (_first < _count && (LinkPosition(_first) - r).sqrMagnitude <= absorb * absorb)
        {
            if (IsCollide) { _links[_first].Destroy(); _links[_first] = default; }
            _first++;
        }
        if (_first >= _count)
        {
            EndReturn();
            return;
        }

        Vector2 root = LinkPosition(_first) + follow;
        Vector2 to = r - root;
        float d = to.magnitude;
        Vector2 move = follow + (d > 1e-6f ? to * (Mathf.Min(d, _reel * dt) / d) : Vector2.zero);

        if (IsCollide)
        {
            // Kinematic : la chaîne ne freine pas le bout tiré, elle le suit.
            var body = _links[_first];
            if (_kinematicLink != _first)
            {
                body.type = PhysicsBody.BodyType.Kinematic;
                _kinematicLink = _first;
            }
            body.linearVelocity = move / dt;
            return;
        }

        // Verlet + « follow the leader » : chaque maillon est ramené à un maillon de son prédécesseur (filament inextensible).
        float keep = Mathf.Max(0f, 1f - filamentDamping * dt);
        Vector2 g = _gravity * (dt * dt);
        _prev[_first] = _p[_first];
        _p[_first] += move;
        for (int i = _first + 1; i < _count; i++)
        {
            Vector2 cur = _p[i];
            _p[i] = cur + (cur - _prev[i]) * keep + g;
            _prev[i] = cur;
        }
        for (int i = _first + 1; i < _count; i++) Limit(_p, i, i - 1, _segment);
    }

    void EndReturn()
    {
        DestroyLinks();
        _state = State.Stowed;
    }

    // ---------- Blob ----------

    void BlobCenter(out Vector2 c, out Vector2 v)
    {
        c = v = Vector2.zero;
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++)
        {
            var body = _blob.Capsule(i);
            c += body.position;
            v += body.linearVelocity;
        }
        c /= n;
        v /= n;
    }

    Vector2 BlobCenterPosition()
    {
        Vector2 c = Vector2.zero;
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++) c += _blob.Capsule(i).position;
        return c / n;
    }

    // ---------- Rendu ----------

    void LateUpdate()
    {
        if (!Application.isPlaying || _blob == null || _blob.CapsuleCount == 0) return;
        var world = PhysicsWorld.defaultWorld;
        DrawRemnants(world);

        switch (_state)
        {
            case State.Stowed:
                if (!Equipped) return;
                BlobCenter(out var c, out var v);
                world.DrawCircle(c, 0.12f * _blob.diameter, ropeColor, 0f, PhysicsWorld.DrawFillOptions.All);
                var target = FindTarget(c, v);
                if (target != null) world.DrawCircle(target.Position, target.radius * 1.6f, targetColor, 0f, PhysicsWorld.DrawFillOptions.Outline);
                break;
            case State.Shooting:
                world.DrawLine(BlobCenterPosition(), _tip, ropeColor, 0f);
                break;
            case State.Attached:
                for (int k = 0; k + 1 < _pivots.Count; k++) world.DrawLine(_pivots[k], _pivots[k + 1], ropeColor, 0f);
                Vector2 b = _capsule >= 0 ? _blob.Capsule(_capsule).position : BlobCenterPosition();
                if (ropeVisual == RopeVisual.Straight)
                {
                    world.DrawLine(_pivots[_pivots.Count - 1], b, ropeColor, 0f);
                    break;
                }
                // La corde est calculée avant le pas, le blob a bougé pendant : décalage réparti de l'ancre (0) au blob (tout).
                int last = _rCount - 1;
                Vector2 shift = b - _r[last];
                _draw.Clear();
                for (int i = 0; i <= last; i++) _draw.Add(_r[i] + shift * (i / (float)last));
                DrawSmooth(world, _draw);
                break;
            case State.Retracting:
                _draw.Clear();
                _draw.Add(BlobCenterPosition());
                for (int i = _first; i < _count; i++)
                    if (!IsCollide || _links[i].isValid) _draw.Add(LinkPosition(i));
                DrawSmooth(world, _draw);
                break;
        }
    }

    // Bouts tranchés : pendus à leur point, animés pas à pas (rattrapent les pas écoulés, plafonnés après un saut).
    void DrawRemnants(PhysicsWorld world)
    {
        if (_remnantCount == 0) return;
        float dt = Time.fixedDeltaTime;
        float keep = Mathf.Max(0f, 1f - filamentDamping * dt);
        Vector2 g = world.gravity * (filamentGravity * dt * dt);
        for (int k = 0; k < _remnantCount; k++)
        {
            var r = _remnants[k];
            Vector2 anchor = r.point != null ? r.point.Position : r.p[0];
            int steps = Mathf.Min(LevelSimulation.Step - r.step, MaxRemnantCatchUp);
            r.step = LevelSimulation.Step;
            for (int s = 0; s < steps; s++)
            {
                for (int i = 1; i < r.count; i++)
                {
                    Vector2 cur = r.p[i];
                    r.p[i] = cur + (cur - r.prev[i]) * keep + g;
                    r.prev[i] = cur;
                }
                // Relaxation des deux bouts de chaque maillon (seul le premier est tenu) : un simple « suivi du premier »
                // ajoutait de l'énergie et faisait tournoyer le bout.
                for (int it = 0; it < RemnantIterations; it++)
                {
                    r.p[0] = anchor;
                    for (int i = 1; i < r.count; i++)
                    {
                        Vector2 d = r.p[i] - r.p[i - 1];
                        float len = d.magnitude;
                        if (len < 1e-6f) continue;
                        Vector2 c = d * ((len - r.segment) / len);
                        if (i == 1) r.p[i] -= c;
                        else { r.p[i - 1] += 0.5f * c; r.p[i] -= 0.5f * c; }
                    }
                }
                r.p[0] = anchor;
            }
            _draw.Clear();
            for (int i = 0; i < r.count; i++) _draw.Add(r.p[i]);
            DrawSmooth(world, _draw);
        }
    }

    readonly List<Vector2> _draw = new List<Vector2>();

    // Dessin lissé (coins coupés au quart, Chaikin) : la corde paraît souple sans maillons plus courts dans la simulation.
    void DrawSmooth(PhysicsWorld world, List<Vector2> points)
    {
        int n = points.Count;
        if (n < 2) return;
        if (n == 2) { world.DrawLine(points[0], points[1], ropeColor, 0f); return; }
        Vector2 prev = points[0];
        for (int i = 0; i + 1 < n; i++)
        {
            Vector2 a = points[i], b = points[i + 1];
            Vector2 q = i == 0 ? a : Vector2.Lerp(a, b, 0.25f);
            Vector2 r = i + 2 == n ? b : Vector2.Lerp(a, b, 0.75f);
            world.DrawLine(prev, q, ropeColor, 0f);
            world.DrawLine(q, r, ropeColor, 0f);
            prev = r;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;
        Gizmos.color = targetColor;
        Vector3 c = transform.position;
        const int N = 64;
        Vector3 prev = c + new Vector3(range, 0f, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector3(Mathf.Cos(a) * range, Mathf.Sin(a) * range, 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        if (screenOnly)
        {
            Vector3 h = screenHalfSize;
            Gizmos.DrawLine(c + new Vector3(-h.x, -h.y), c + new Vector3(h.x, -h.y));
            Gizmos.DrawLine(c + new Vector3(h.x, -h.y), c + new Vector3(h.x, h.y));
            Gizmos.DrawLine(c + new Vector3(h.x, h.y), c + new Vector3(-h.x, h.y));
            Gizmos.DrawLine(c + new Vector3(-h.x, h.y), c + new Vector3(-h.x, -h.y));
        }
    }
}
