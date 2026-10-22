using Unity.U2D.Physics;
using UnityEngine;

// Avatar de la carte : un vrai blob Box2D (anneau de capsules) qui sautille tout seul jusqu'au niveau visé. Aucun contrôle du
// joueur, aucun saut de gameplay : de petits bonds réguliers, juste pour le voir se déplacer (il s'écrase et tremble à
// l'atterrissage grâce à la physique). La physique tourne dans un plan vu de côté (x = distance le long du chemin / échelle,
// y = hauteur, sol plat) ; le dessin (SlimeSkin, design 'design') est posé sur la carte au point du chemin correspondant, face
// à la caméra, avec son ombre au sol. Placeholder du futur avatar : le Roi.
[DisallowMultipleComponent]
public class SlimeMapAvatar : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Liens")]
    public SlimeMap map;
    [Tooltip("Blob de l'avatar : objet ENFANT portant un BLOB (sa physique est créée à sa position, posée par ce script).")]
    public BLOB blob;
    [Tooltip("Habillage qui dessine l'avatar (une instance de SlimeSkin propre à la carte).")]
    public SlimeSkin skin;

    [Header("Apparence")]
    [Tooltip("Design de slime utilisé (niveau de SlimeSkin : 10 = le Roi, en attendant le vrai avatar).")]
    [Min(0)] public int design = 10;
    [Tooltip("Diamètre de l'avatar sur la carte, en m.")]
    [Min(0.1f)] public float size = 0.8f;

    [Header("Sautillement")]
    [Tooltip("Hauteur d'un bond, en m de physique (diamètre du blob = 1).")]
    [Min(0.05f)] public float hopHeight = 0.5f;
    [Tooltip("Longueur max d'un bond, en m de physique.")]
    [Min(0.2f)] public float hopLength = 1.5f;
    [Tooltip("Au-delà, les bonds s'allongent : un trajet n'en compte jamais plus que ça (les longs trajets restent courts).")]
    [Min(1)] public int maxHops = 7;
    [Tooltip("Pause au sol entre deux bonds, en s (le temps de s'écraser).")]
    [Min(0f)] public float landPause = 0.08f;
    [Tooltip("Étirement au départ du bond : écart de vitesse entre le haut et le bas du blob, par m (1/s).")]
    [Min(0f)] public float takeoffStretch = 2.5f;

    BLOB _blob;
    PhysicsBody _ground;
    float _target, _scale = 1f, _air, _ground0;
    bool _hopping, _moving, _arrived;
    int _level = -1, _goal = -1, _hops;
    Vector2 _center, _velocity;

    // Niveau (local) où se tient l'avatar ; pendant un trajet, celui qu'il vise.
    public int Level => _moving ? _goal : _level;
    public bool Moving => _moving;
    public event System.Action<int> Arrived;

    float Scale => size / Mathf.Max(0.01f, blob != null ? blob.diameter : 1f);

    // Avant le départ du niveau (recréation de la physique) : l'avatar apparaît sur ce niveau.
    public void Place(int level)
    {
        _level = level;
        _goal = -1;
        _moving = false;
    }

    public void GoTo(int level)
    {
        if (map == null) return;
        level = Mathf.Clamp(level, 0, Mathf.Max(0, map.LevelCount - 1));
        if (!_moving && level == _level) { _arrived = true; return; }
        _goal = level;
        _target = map.NodeS(level) / _scale;
        _moving = true;
        _hops = 0;
    }

    void OnEnable()
    {
        _blob = blob;
        if (map == null || _blob == null) return;
        map.EnsureBuilt();
        _scale = Scale;
        if (_level < 0) _level = 0;
        float x = map.NodeS(_level) / _scale;
        _target = x;
        _blob.heavyGravity = 1f;
        _blob.transform.position = new Vector3(x, 0.5f * _blob.diameter + 0.02f, 0f);

        if (!Application.isPlaying) return;
        var def = PhysicsBodyDefinition.defaultDefinition;
        def.type = PhysicsBody.BodyType.Static;
        _ground = PhysicsWorld.defaultWorld.CreateBody(def);
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = 0.8f;
        material.bounciness = 0f;
        shapeDef.surfaceMaterial = material;
        float x0 = -20f, x1 = map.PathLength / _scale + 20f;
        System.Span<Vector2> v = stackalloc Vector2[4];
        v[0] = new Vector2(x0, -2f); v[1] = new Vector2(x1, -2f); v[2] = new Vector2(x1, 0f); v[3] = new Vector2(x0, 0f);
        _ground.CreateShape(PolygonGeometry.Create(v, 0f), shapeDef);
        _ground0 = 0f;
        _hopping = false;
        _air = 0f;
        _moving = false;
        _goal = -1;
        if (skin != null) skin.DrawExtras += Draw;
    }

    void OnDisable()
    {
        if (skin != null) skin.DrawExtras -= Draw;
        if (_ground.isValid) _ground.Destroy();
        _ground = default;
    }

    // Pas fixe (LevelSimulation), avant celui du blob (enfant) : décide des bonds.
    public void LevelTick()
    {
        if (_blob == null || _blob.CapsuleCount < 3) return;
        int n = _blob.CapsuleCount;
        Vector2 c = Vector2.zero, v = Vector2.zero;
        float low = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var body = _blob.Capsule(i);
            Vector2 p = body.position;
            c += p;
            v += body.linearVelocity;
            low = Mathf.Min(low, p.y);
        }
        c /= n;
        v /= n;
        _center = c;
        _velocity = v;
        float dt = Time.fixedDeltaTime;
        bool grounded = low - _blob.CapsuleRadius < _ground0 + 0.04f;
        _air += dt;

        if (_hopping)
        {
            if (!grounded || _air < 0.15f || v.y > 0.5f) return;
            _hopping = false;
            _air = 0f;
            Push(-v.x * 0.9f, 0f, 0f);
            return;
        }
        if (!_moving || !grounded) return;
        float remaining = _target - c.x;
        if (Mathf.Abs(remaining) < 0.08f)
        {
            if (Mathf.Abs(v.x) > 0.05f) Push(-v.x * 0.5f, 0f, 0f);
            if (_air < 0.25f) return;
            _moving = false;
            _level = _goal;
            _arrived = true;
            return;
        }
        if (_air < landPause) return;

        // Bonds réguliers jusqu'au niveau : leur nombre fixe leur longueur (dernier bond pile sur la pastille).
        float g = Mathf.Abs(PhysicsWorld.defaultWorld.gravity.y);
        int hops = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(remaining) / hopLength - 0.01f), 1, maxHops);
        float len = Mathf.Abs(remaining) / hops;
        float h = hopHeight * Mathf.Max(1f, Mathf.Sqrt(len / hopLength));
        float vy = Mathf.Sqrt(2f * g * h);
        float vx = Mathf.Sign(remaining) * len / (2f * vy / g);
        Push(vx - v.x, vy - v.y, takeoffStretch);
        _hopping = true;
        _hops++;
        _air = 0f;
    }

    // Même changement de vitesse pour toutes les capsules (+ étirement : le haut part plus vite que le bas, quantité de
    // mouvement inchangée), la déformation du blob est gardée.
    void Push(float dvx, float dvy, float stretch)
    {
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++)
        {
            var body = _blob.Capsule(i);
            Vector2 vel = body.linearVelocity;
            float extra = stretch * (body.position.y - _center.y);
            body.linearVelocity = new Vector2(vel.x + dvx, vel.y + dvy + extra);
        }
    }

    void Update()
    {
        if (!_arrived) return;
        _arrived = false;
        Arrived?.Invoke(_level);
    }

    // Distance (m de carte) le long du chemin, au centre de l'avatar.
    public float PathS => _center.x * _scale;

    // Dessin, pendant la construction du maillage de SlimeSkin : l'anneau physique posé face à la caméra au point du chemin.
    void Draw(float dt)
    {
        if (map == null || _blob == null || _blob.CapsuleCount < 3 || map.cam == null) return;
        var cam = map.cam;
        float s = _center.x * _scale;
        Vector3 ground = map.PathPoint(s);
        float height = Mathf.Max(0f, _center.y) * _scale;
        Vector3 anchor = map.Bend(ground + Vector3.up * height);
        Transform ct = cam.transform;
        Vector3 right = ct.right, up = ct.up, fwd = ct.forward;
        var m = new Matrix4x4();
        m.SetColumn(0, right * _scale);
        m.SetColumn(1, up * _scale);
        m.SetColumn(2, fwd * _scale);
        Vector3 origin = anchor - (right * _center.x + up * _center.y) * _scale;
        m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
        skin.renderMatrix = m;
        float depth = Mathf.Max(0.5f, Vector3.Dot(anchor - ct.position, fwd));
        float worldPixel = 2f * depth * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
        skin.pixelSize = worldPixel / _scale;

        // Regard : vers où il va (direction du chemin vue à l'écran), sinon vers le joueur.
        Vector2 look = _center;
        if (_moving)
        {
            Vector3 dir = map.PathDirection(s) * Mathf.Sign(_target - _center.x);
            Vector3 a = cam.WorldToScreenPoint(anchor), b = cam.WorldToScreenPoint(anchor + dir);
            Vector2 sd = ((Vector2)(b - a)).normalized;
            look = _center + sd * 2f;
        }
        else look = _center + Vector2.down * 0.3f;
        skin.DrawBlob(_blob, design, look, dt);

        float lift = Mathf.Clamp01(height / (2.5f * size));
        Vector3 shadow = map.PathPoint(s);
        map.SetShadow(shadow + Vector3.up * 0.02f, size * (0.55f - 0.2f * lift), 0.38f * (1f - 0.6f * lift));
    }
}
