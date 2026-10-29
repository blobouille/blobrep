using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Carte du monde (mode Aventure) : une zone de la campagne en diorama de papier découpé (façon Paper Mario / Candy Crush /
// Don't Starve), calculé au lancement à graine fixe (aucun modèle ni texture importés). Sol peint à plat (herbe à taches, chemin
// de grandes dalles qui serpente vers le haut de l'écran, un jeton de carton par niveau avec son numéro), et tout le reste en
// cartes plates face à la caméra (shader Slime/MapCard) : collines, arbres, buissons, fleurs, herbe, rochers, champignons,
// lampadaires, maison, mare, moulin, ferme, porte de fin de zone, papillons. Le monde se courbe au loin (on ne voit pas le
// bout) et des nuages de papier cachent ce qui n'est pas découvert ; ils s'écartent quand un niveau se débloque. Tout est
// construit une fois et animé dans les shaders. Caméra en perspective inclinée. Purement visuel.
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[AddComponentMenu("Gumori/Carte/GumoriMap")]
public class GumoriMap : MonoBehaviour
{
    [Header("Campagne")]
    public GumoriCampaign campaign;
    [Tooltip("Zone de la campagne affichée (0 = la première).")]
    [Min(0)] public int zone;

    [Header("Chemin")]
    [Tooltip("Distance entre deux niveaux, en m (vers le haut de l'écran).")]
    [Min(1f)] public float spacing = 3.4f;
    [Tooltip("Écart du chemin à gauche et à droite, en m.")]
    [Min(0f)] public float wander = 1.3f;
    [Tooltip("Largeur du chemin, en m.")]
    [Min(0.3f)] public float pathWidth = 1.15f;
    [Tooltip("Longueur du chemin avant le premier niveau, en m.")]
    [Min(1f)] public float startLength = 5f;
    [Tooltip("Longueur du chemin après le dernier niveau, en m.")]
    [Min(1f)] public float endLength = 10f;

    [Header("Décor")]
    [Tooltip("Graine du décor : même graine = même plaine.")]
    public int decorSeed = 7;
    [Tooltip("Demi-largeur du sol, en m.")]
    [Min(4f)] public float halfWidth = 12f;
    [Tooltip("Distance (m) des collines de papier au chemin, de chaque côté.")]
    [Min(1f)] public float hillDistance = 4.6f;
    [Range(0f, 3f)] public float treeDensity = 1f;
    [Range(0f, 3f)] public float flowerDensity = 1f;
    [Range(0f, 3f)] public float grassDensity = 1f;
    [Tooltip("Épaisseur du trait d'encre des découpes, en m.")]
    [Min(0f)] public float inkWidth = 0.035f;

    [Header("Monde courbé")]
    [Tooltip("Le sol descend de courbure × distance² (m) au-delà du point visé : on ne voit pas le bout (façon Animal Crossing).")]
    [Min(0f)] public float curvature = 0.03f;
    [Tooltip("Distance (m) devant le point visé où la courbure commence.")]
    [Min(0f)] public float curveStart = 3f;

    [Header("Caméra")]
    public Camera cam;
    [Tooltip("Inclinaison de la caméra vers le sol, en degrés (90 = vue de dessus).")]
    [Range(20f, 89f)] public float pitch = 42f;
    [Range(15f, 90f)] public float fieldOfView = 42f;
    [Tooltip("Distance de la caméra au point visé, en m.")]
    [Min(2f)] public float distance = 15f;
    [Tooltip("Le point suivi (avatar) est placé ce nombre de m sous le point visé : plus de chemin visible devant lui.")]
    public float lookAhead = 2.2f;
    [Tooltip("Part du décalage gauche / droite du chemin suivie par la caméra (0 = toujours centrée).")]
    [Range(0f, 1f)] public float followSide = 0.35f;
    [Tooltip("Temps (s) pour rattraper le point suivi.")]
    [Min(0.01f)] public float followTime = 0.4f;
    [Tooltip("Hors Play : niveau visé par la caméra (aperçu dans l'éditeur).")]
    [Min(0)] public int previewLevel;

    [Header("Couleurs")]
    public Color grass = new Color(0.47f, 0.80f, 0.33f);
    public Color grassDark = new Color(0.33f, 0.66f, 0.28f);
    public Color grassLight = new Color(0.62f, 0.88f, 0.40f);
    public Color stone = new Color(0.99f, 0.91f, 0.74f);
    public Color mortar = new Color(0.84f, 0.70f, 0.52f);
    public Color pathEdge = new Color(0.74f, 0.55f, 0.36f);
    public Color skyTop = new Color(0.42f, 0.70f, 1.00f);
    public Color skyHorizon = new Color(0.86f, 0.94f, 1.00f);
    [Tooltip("Couleur de la brume qui couvre ce qui n'est pas découvert.")]
    public Color mist = new Color(0.93f, 0.92f, 1.00f);
    public Color nodeLocked = new Color(0.66f, 0.66f, 0.74f);
    public Color nodeOpen = new Color(1.00f, 0.50f, 0.72f);
    public Color nodeDone = new Color(1.00f, 0.70f, 0.18f);
    [Tooltip("Distance à la caméra (m) où la brume de l'horizon commence et devient complète.")]
    public Vector2 fogRange = new Vector2(20f, 42f);

    [Header("Rendu")]
    public Shader groundShader;
    public Shader cardShader;
    public Shader spriteShader;
    [Tooltip("Quand rien ne bouge, une image sur N seulement (2 = 30 images/s au lieu de 60) : économise la batterie.")]
    [Range(1, 4)] public int idleFrameInterval = 2;
    [Tooltip("Hors Play : tout le décor découvert (aperçu de toute la zone).")]
    public bool previewRevealed = true;

    public const int MaxNodes = 32;
    const float Cell = 1f, NodeRadius = 0.5f, NodeBase = 0.07f;
    // Files de rendu : ciel, sol, ombres, découpes, étoiles (avant les gumori, 3000).
    const int SkyQueue = 1000, GroundQueue = 2000, ShadowQueue = 2950, CardQueue = 2970, StarQueue = 2990;

    // Sortes de découpes (shader Slime/MapCard).
    public enum Kind { Tree = 1, Pine, Bush, Flower, Tuft, Mushroom, Rock, Lamp, House, Windmill, Barn, Hay, Gate, Sign, Hill, Cloud, Butterfly }

    struct Card
    {
        public Vector3 pos;
        public Kind kind;
        public float scale, seed, variant, sway, dir, shadow;
        public Color main, second;
    }

    // Boîte de chaque forme (unités d'échelle, depuis le pied) : x min, x max, y max.
    static Vector3 Bounds(Kind k) => k switch
    {
        Kind.Tree => new Vector3(-1.05f, 1.05f, 2.25f),
        Kind.Pine => new Vector3(-0.7f, 0.7f, 2.08f),
        Kind.Bush => new Vector3(-0.62f, 0.72f, 0.8f),
        Kind.Flower => new Vector3(-0.12f, 0.12f, 0.34f),
        Kind.Tuft => new Vector3(-0.17f, 0.17f, 0.29f),
        Kind.Mushroom => new Vector3(-0.19f, 0.19f, 0.29f),
        Kind.Rock => new Vector3(-0.34f, 0.36f, 0.34f),
        Kind.Lamp => new Vector3(-0.16f, 0.16f, 1.55f),
        Kind.House => new Vector3(-0.88f, 0.88f, 1.6f),
        Kind.Windmill => new Vector3(-1.3f, 1.3f, 2.85f),
        Kind.Barn => new Vector3(-0.86f, 0.86f, 1.48f),
        Kind.Hay => new Vector3(-0.32f, 0.32f, 0.62f),
        Kind.Gate => new Vector3(-1.2f, 1.2f, 2.6f),
        Kind.Sign => new Vector3(-0.45f, 0.45f, 0.9f),
        Kind.Hill => new Vector3(-1.05f, 1.1f, 0.66f),
        Kind.Cloud => new Vector3(-1.25f, 1.25f, 1.25f),
        _ => new Vector3(-0.2f, 0.2f, 0.25f),
    };

    // ---- État ----
    readonly List<float> _px = new List<float>(), _pz = new List<float>(), _ps = new List<float>();
    float[] _nodeS = new float[0];
    Vector3[] _nodePos = new Vector3[0];
    readonly List<Vector4> _keepOut = new List<Vector4>(); // x, z, rayon : décor interdit (repères, jetons)
    int _count, _first;
    float _length, _zMin, _zMax;
    bool _layoutDirty = true, _meshDirty = true;

    readonly List<Card> _cards = new List<Card>();
    Mesh _ground, _cardMesh, _sprites, _dynamic;
    Material _groundMat, _cardMat, _sky, _star, _shadow;
    int _cardsFrom = -100;
    readonly Vector4[] _nodes = new Vector4[MaxNodes];
    readonly GumoriMapMesh _builder = new GumoriMapMesh();

    float _focusZ, _focusVel, _targetZ;
    float _reveal, _revealFrom, _revealTo, _revealStart, _revealTime = -1f;
    int _shownUnlocked = -1;
    Vector3 _shadowPos;
    float _shadowRadius, _shadowAlpha;
    float _time;

    public int LevelCount => _count;
    public int FirstLevel => _first;
    public float PathLength => _length;
    public float FocusZ => _focusZ;
    public string ZoneName => campaign != null && zone < campaign.zones.Count ? campaign.zones[zone].name : "";

    static readonly int BendId = Shader.PropertyToID("_SlimeMapBend"), FogId = Shader.PropertyToID("_SlimeMapFog"),
        FogRangeId = Shader.PropertyToID("_SlimeMapFogRange"), MistId = Shader.PropertyToID("_SlimeMapMist"),
        RevealId = Shader.PropertyToID("_SlimeMapReveal"), NodesId = Shader.PropertyToID("_SlimeMapNodes"),
        StoneId = Shader.PropertyToID("_SlimeMapStone"), MortarId = Shader.PropertyToID("_SlimeMapMortar"),
        EdgeId = Shader.PropertyToID("_SlimeMapEdge"), LockedId = Shader.PropertyToID("_SlimeMapLocked"),
        OpenId = Shader.PropertyToID("_SlimeMapOpen"), DoneId = Shader.PropertyToID("_SlimeMapDone"),
        GrassId = Shader.PropertyToID("_SlimeMapGrass"), GrassLightId = Shader.PropertyToID("_SlimeMapGrassLight"),
        GrassDarkId = Shader.PropertyToID("_SlimeMapGrassDark"), InfoId = Shader.PropertyToID("_SlimeMapInfo"),
        TimeId = Shader.PropertyToID("_SlimeMapTime"), SkyTopId = Shader.PropertyToID("_SlimeMapSkyTop"),
        SkyHorizonId = Shader.PropertyToID("_SlimeMapSkyHorizon"), ZTestId = Shader.PropertyToID("_ZTest");

    void OnEnable()
    {
        if (groundShader == null) groundShader = Shader.Find("Slime/Map");
        if (cardShader == null) cardShader = Shader.Find("Slime/MapCard");
        if (spriteShader == null) spriteShader = Shader.Find("Slime/MapSprite");
        _groundMat = NewMaterial(groundShader, GroundQueue);
        _cardMat = NewMaterial(cardShader, CardQueue);
        _sky = NewMaterial(spriteShader, SkyQueue);
        _star = NewMaterial(spriteShader, StarQueue);
        _shadow = NewMaterial(spriteShader, ShadowQueue);
        if (_sky != null) _sky.SetFloat(ZTestId, (float)CompareFunction.Always);
        if (_star != null) _star.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
        if (_shadow != null) _shadow.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
        _layoutDirty = _meshDirty = true;
        _cardsFrom = -100;
        _shownUnlocked = -1;
        _revealTime = -1f;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        EnsureBuilt();
        _focusZ = _targetZ = Application.isPlaying ? NodeZ(0) : NodeZ(previewLevel);
        _reveal = RevealFor(LocalUnlocked);
        if (!Application.isPlaying) UpdateCamera(0f);
    }

    static Material NewMaterial(Shader shader, int queue) =>
        shader == null ? null : new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = queue };

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        foreach (var o in new Object[] { _ground, _cardMesh, _sprites, _dynamic, _groundMat, _cardMat, _sky, _star, _shadow })
            if (o != null) DestroyImmediate(o);
        _ground = _cardMesh = _sprites = _dynamic = null;
        if (Application.isPlaying) OnDemandRendering.renderFrameInterval = 1;
    }

    void OnValidate()
    {
        _layoutDirty = _meshDirty = true;
        _cardsFrom = -100;
    }

    // ---- Disposition : chemin et niveaux ----

    public void EnsureBuilt()
    {
        if (_layoutDirty) Layout();
        if (_meshDirty) BuildStatic();
    }

    void Layout()
    {
        _layoutDirty = false;
        _meshDirty = true;
        _count = 30;
        _first = 0;
        if (campaign != null && zone < campaign.zones.Count)
        {
            _count = Mathf.Min(MaxNodes, campaign.zones[zone].levels.Count);
            for (int z = 0; z < zone; z++) _first += campaign.zones[z].levels.Count;
        }

        var rng = new System.Random(decorSeed);
        int n = _count;
        var ctrl = new List<Vector2> { new Vector2(0f, 0f) };
        for (int i = 0; i < n; i++)
        {
            float x = wander * Mathf.Sin(i * 1.05f + 0.7f) + wander * 0.25f * ((float)rng.NextDouble() * 2f - 1f);
            if (i == 0) x *= 0.4f;
            ctrl.Add(new Vector2(x, startLength + i * spacing));
        }
        ctrl.Add(new Vector2(ctrl[ctrl.Count - 1].x * 0.3f, startLength + (n - 1) * spacing + endLength));

        _px.Clear(); _pz.Clear(); _ps.Clear();
        _nodeS = new float[n];
        _nodePos = new Vector3[n];
        float s = 0f;
        Vector2 prev = ctrl[0];
        const int perSegment = 20;
        for (int k = 0; k < ctrl.Count - 1; k++)
        {
            Vector2 p0 = ctrl[Mathf.Max(0, k - 1)], p1 = ctrl[k], p2 = ctrl[k + 1], p3 = ctrl[Mathf.Min(ctrl.Count - 1, k + 2)];
            if (k == 0) p0 = p1 - (p2 - p1);
            if (k == ctrl.Count - 2) p3 = p2 + (p2 - p1);
            for (int j = 0; j < perSegment; j++)
            {
                if (k > 0 && j == 0 && k - 1 < n) _nodeS[k - 1] = s;
                Vector2 p = CatmullRom(p0, p1, p2, p3, (float)j / perSegment);
                s += (p - prev).magnitude;
                prev = p;
                _px.Add(p.x); _pz.Add(p.y); _ps.Add(s);
            }
        }
        Vector2 last = ctrl[ctrl.Count - 1];
        s += (last - prev).magnitude;
        _px.Add(last.x); _pz.Add(last.y); _ps.Add(s);
        _length = s;
        for (int i = 0; i < n; i++)
        {
            var p = PathXZ(_nodeS[i]);
            _nodePos[i] = new Vector3(p.x, 0f, p.y);
        }
        _zMin = -8f;
        _zMax = last.y + 26f;

        _keepOut.Clear();
        for (int i = 0; i < n; i++) _keepOut.Add(new Vector4(_nodePos[i].x, _nodePos[i].z, 0.95f, 0f));
        _landmarks.Clear();
        AddLandmark(Landmark.House, -1, 1.9f, 1.1f);
        AddLandmark(Landmark.Pond, 3, 2.6f, 1.6f);
        AddLandmark(Landmark.Windmill, 9, 2.7f, 1.4f);
        AddLandmark(Landmark.BigTree, 14, 2.5f, 1.2f);
        AddLandmark(Landmark.Farm, 23, 2.6f, 1.6f);
        AddLandmark(Landmark.Gate, n, 0f, 1.3f);
    }

    enum Landmark { House, Pond, Windmill, BigTree, Farm, Gate }

    struct LandmarkAt
    {
        public Landmark kind;
        public Vector3 pos;
        public float side;
    }

    readonly List<LandmarkAt> _landmarks = new List<LandmarkAt>();

    // Repère à côté du niveau 'node' (-1 = départ, n = fin), du côté où le chemin laisse le plus de place.
    void AddLandmark(Landmark kind, int node, float offset, float radius)
    {
        if (kind != Landmark.Gate && node >= _count) return;
        float z;
        if (node < 0) z = startLength * 0.5f;
        else if (node >= _count) z = _nodePos[_count - 1].z + endLength * 0.4f;
        else z = _nodePos[node].z + spacing * 0.5f;
        float x = PathXAtZ(z);
        float side = kind == Landmark.Gate ? 0f : x > 0f ? -1f : 1f;
        var pos = new Vector3(x + side * offset, 0f, z);
        _landmarks.Add(new LandmarkAt { kind = kind, pos = pos, side = side });
        if (kind != Landmark.Gate) _keepOut.Add(new Vector4(pos.x, pos.z, radius, 1f));
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
    }

    int SampleAt(float s)
    {
        int lo = 0, hi = _ps.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_ps[mid] <= s) lo = mid; else hi = mid;
        }
        return lo;
    }

    Vector2 PathXZ(float s)
    {
        if (_ps.Count == 0) return Vector2.zero;
        s = Mathf.Clamp(s, 0f, _length);
        int i = SampleAt(s);
        int j = Mathf.Min(i + 1, _ps.Count - 1);
        float t = _ps[j] > _ps[i] ? (s - _ps[i]) / (_ps[j] - _ps[i]) : 0f;
        return new Vector2(Mathf.Lerp(_px[i], _px[j], t), Mathf.Lerp(_pz[i], _pz[j], t));
    }

    float PathXAtZ(float z)
    {
        if (_pz.Count == 0) return 0f;
        if (z <= _pz[0]) return _px[0];
        int lo = 0, hi = _pz.Count - 1;
        if (z >= _pz[hi]) return _px[hi];
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_pz[mid] <= z) lo = mid; else hi = mid;
        }
        float t = (_pz[hi] - _pz[lo]) > 1e-5f ? (z - _pz[lo]) / (_pz[hi] - _pz[lo]) : 0f;
        return Mathf.Lerp(_px[lo], _px[hi], t);
    }

    // Distance (m) du point (x, z) à l'axe du chemin.
    float PathDistance(float x, float z)
    {
        if (_pz.Count < 2) return 99f;
        int lo = 0, hi = _pz.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_pz[mid] <= z - 3f) lo = mid; else hi = mid;
        }
        float best = float.MaxValue;
        var p = new Vector2(x, z);
        for (int i = lo; i < _pz.Count - 1; i++)
        {
            if (_pz[i] > z + 3f) break;
            var a = new Vector2(_px[i], _pz[i]);
            var ab = new Vector2(_px[i + 1], _pz[i + 1]) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
        }
        if (best == float.MaxValue) best = Mathf.Min((p - new Vector2(_px[0], _pz[0])).sqrMagnitude, (p - new Vector2(_px[_px.Count - 1], _pz[_pz.Count - 1])).sqrMagnitude);
        return Mathf.Sqrt(best);
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ---- Accès (avatar, menu) ----

    public float NodeS(int local) => _nodeS.Length == 0 ? 0f : _nodeS[Mathf.Clamp(local, 0, _nodeS.Length - 1)];
    public float NodeZ(int local) => _nodePos.Length == 0 ? 0f : _nodePos[Mathf.Clamp(local, 0, _nodePos.Length - 1)].z;
    public Vector3 NodePosition(int local) => _nodePos.Length == 0 ? Vector3.zero : _nodePos[Mathf.Clamp(local, 0, _nodePos.Length - 1)];

    // Point du chemin à la distance s (sol, avant courbure), à la hauteur des jetons (l'avatar monte dessus).
    public Vector3 PathPoint(float s)
    {
        var p = PathXZ(s);
        return new Vector3(p.x, PadLift(s), p.y);
    }

    public Vector3 PathDirection(float s)
    {
        Vector2 a = PathXZ(s - 0.2f), b = PathXZ(s + 0.2f);
        Vector2 d = (b - a).normalized;
        return new Vector3(d.x, 0f, d.y);
    }

    float PadLift(float s)
    {
        for (int i = 0; i < _nodeS.Length; i++)
            if (Mathf.Abs(s - _nodeS[i]) < NodeRadius) return NodeBase;
        return 0f;
    }

    // Même courbure que les shaders (le dessin de l'avatar la suit).
    public Vector3 Bend(Vector3 world)
    {
        float dz = Mathf.Max(0f, world.z - (_focusZ + curveStart));
        world.y -= curvature * dz * dz;
        return world;
    }

    // Niveau (local) sous un point de l'écran, -1 si aucun.
    public int NodeAtScreen(Vector2 screen)
    {
        if (cam == null) return -1;
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < _count; i++)
        {
            Vector3 c = Bend(_nodePos[i] + Vector3.up * 0.2f);
            Vector3 sc = cam.WorldToScreenPoint(c);
            if (sc.z <= 0f) continue;
            float r = (cam.WorldToScreenPoint(c + cam.transform.right * 0.75f) - sc).magnitude;
            float d = ((Vector2)sc - screen).magnitude;
            if (d < r && d < bestD) { best = i; bestD = d; }
        }
        return best;
    }

    // ---- Caméra ----

    // Point suivi (z, m) : la caméra le rattrape en douceur.
    public void Follow(float z) => _targetZ = z + lookAhead;
    public void Scroll(float dz) => _targetZ = _focusZ = ClampFocus(_focusZ + dz);
    public void SnapTo(float z)
    {
        _targetZ = _focusZ = ClampFocus(z + lookAhead);
        _focusVel = 0f;
    }

    public float MetersPerPixel => cam == null ? 0.01f : 2f * distance * Mathf.Tan(0.5f * fieldOfView * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight) / Mathf.Max(0.3f, Mathf.Sin(pitch * Mathf.Deg2Rad));

    float ClampFocus(float z)
    {
        float max = _count > 0 ? NodeZ(Mathf.Clamp(Mathf.CeilToInt(_reveal) - 1, 0, _count - 1)) + spacing * 0.6f : 0f;
        if (_count > 0 && _reveal >= _count) max = NodeZ(_count - 1) + endLength * 0.4f;
        return Mathf.Clamp(z, NodeZ(0) - 1.5f, Mathf.Max(NodeZ(0), max));
    }

    void UpdateCamera(float dt)
    {
        if (Application.isPlaying)
        {
            _targetZ = ClampFocus(_targetZ);
            _focusZ = Mathf.SmoothDamp(_focusZ, _targetZ, ref _focusVel, followTime, Mathf.Infinity, dt);
        }
        else _focusZ = NodeZ(Mathf.Min(previewLevel, Mathf.Max(0, _count - 1))) + lookAhead;
        if (cam == null) return;
        float fx = PathXAtZ(_focusZ) * followSide;
        var focus = new Vector3(fx, 0f, _focusZ);
        float p = pitch * Mathf.Deg2Rad;
        cam.transform.SetPositionAndRotation(focus + new Vector3(0f, Mathf.Sin(p), -Mathf.Cos(p)) * distance, Quaternion.Euler(pitch, 0f, 0f));
        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = distance + fogRange.y + 20f;
    }

    public bool CameraMoving => Mathf.Abs(_targetZ - _focusZ) > 0.01f || Mathf.Abs(_focusVel) > 0.01f;

    // ---- Progression affichée ----

    int LocalUnlocked => Mathf.Clamp(GumoriSession.Unlocked - _first, 0, _count);

    // Niveaux débloqués affichés (le menu retient l'ancien pendant l'animation de retour), -1 = progression réelle.
    public int ShownUnlocked
    {
        get => _shownUnlocked >= 0 ? _shownUnlocked : LocalUnlocked;
        set => _shownUnlocked = value;
    }

    // Bandes de nuages écartées : niveaux visibles = débloqués + le suivant (verrouillé, en avant-goût).
    public float RevealFor(int unlocked) => Mathf.Min(_count, unlocked + 1);

    public void SetReveal(float value)
    {
        _reveal = value;
        _revealTime = -1f;
    }

    public void AnimateReveal(float to, float duration)
    {
        _revealFrom = _reveal;
        _revealTo = to;
        _revealStart = _time;
        _revealTime = Mathf.Max(0.01f, duration);
    }

    public bool Revealing => _revealTime > 0f;

    // Jeton qui saute (changement d'état) maintenant + 'delay' s ; avant, il garde son état précédent.
    public void PopNode(int local, float delay = 0f)
    {
        if (local < 0 || local >= MaxNodes) return;
        _popAt[local] = _time + delay;
    }

    readonly float[] _popAt = new float[MaxNodes];

    void UpdateNodes()
    {
        int unlocked = Application.isPlaying ? ShownUnlocked : (previewRevealed ? _count : LocalUnlocked);
        for (int i = 0; i < MaxNodes; i++)
        {
            if (i >= _count) { _nodes[i] = Vector4.zero; continue; }
            int stars = Application.isPlaying || !previewRevealed ? GumoriSession.Stars(_first + i) : 0;
            float state = i < unlocked ? (stars > 0 ? 2f : 1f) : 0f;
            float current = i == unlocked - 1 && stars == 0 ? 1f : 0f;
            _nodes[i] = new Vector4(state, stars, current, _popAt[i]);
        }
    }

    // Ombre de l'avatar (posée au sol) : position, rayon, opacité.
    public void SetShadow(Vector3 ground, float radius, float alpha)
    {
        _shadowPos = ground;
        _shadowRadius = radius;
        _shadowAlpha = alpha;
    }

    // ---- Image ----

    void LateUpdate()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        _time = Application.isPlaying ? Time.time : (float)(System.DateTime.Now.TimeOfDay.TotalSeconds % 3600.0);
        EnsureBuilt();
        if (_revealTime > 0f)
        {
            float t = Mathf.Clamp01((_time - _revealStart) / _revealTime);
            _reveal = Mathf.Lerp(_revealFrom, _revealTo, t * t * (3f - 2f * t));
            if (t >= 1f) _revealTime = -1f;
        }
        if (!Application.isPlaying) _reveal = previewRevealed ? _count : RevealFor(LocalUnlocked);
        UpdateCamera(dt);
        UpdateNodes();
        int from = Mathf.Max(0, Mathf.FloorToInt(_reveal) - 1);
        if (from != _cardsFrom) BuildCards(from);
        SetGlobals();
        BuildDynamic();
    }

    // Quelque chose bouge (caméra, nuages, jetons) : pas d'économie d'images.
    public bool Animating
    {
        get
        {
            if (CameraMoving || Revealing) return true;
            for (int i = 0; i < _count; i++)
                if (_time - _popAt[i] < 1.5f && _time >= _popAt[i] - 3f) return true;
            return false;
        }
    }

    void SetGlobals()
    {
        Shader.SetGlobalVector(BendId, new Vector4(curvature, _focusZ + curveStart, 0f, 0f));
        Shader.SetGlobalColor(FogId, skyHorizon);
        Shader.SetGlobalVector(FogRangeId, new Vector4(fogRange.x, fogRange.y, 0f, 0f));
        Shader.SetGlobalColor(MistId, new Color(mist.r, mist.g, mist.b, 0.9f));
        Shader.SetGlobalVector(RevealId, new Vector4(RevealZ(_reveal), _reveal, spacing * 0.8f, 0f));
        Shader.SetGlobalVectorArray(NodesId, _nodes);
        Shader.SetGlobalColor(StoneId, stone);
        Shader.SetGlobalColor(MortarId, mortar);
        Shader.SetGlobalColor(EdgeId, pathEdge);
        Shader.SetGlobalColor(LockedId, nodeLocked);
        Shader.SetGlobalColor(OpenId, nodeOpen);
        Shader.SetGlobalColor(DoneId, nodeDone);
        Shader.SetGlobalColor(GrassId, grass);
        Shader.SetGlobalColor(GrassLightId, grassLight);
        Shader.SetGlobalColor(GrassDarkId, grassDark);
        Shader.SetGlobalVector(InfoId, new Vector4(0.5f * pathWidth, _first, NodeRadius, 0f));
        Shader.SetGlobalFloat(TimeId, _time);
        Shader.SetGlobalColor(SkyTopId, skyTop);
        Shader.SetGlobalColor(SkyHorizonId, skyHorizon);
    }

    // z où commence la brume : moitié du chemin entre le dernier niveau visible et le suivant.
    float RevealZ(float reveal)
    {
        if (_count == 0) return 0f;
        if (reveal >= _count) return NodeZ(_count - 1) + endLength * 0.5f;
        float k = Mathf.Max(0f, reveal - 0.5f);
        int i = Mathf.FloorToInt(k);
        float f = k - i;
        float z0 = i < _count ? NodeZ(i) : NodeZ(_count - 1) + spacing;
        float z1 = i + 1 < _count ? NodeZ(i + 1) : z0 + spacing;
        return Mathf.Lerp(z0, z1, f);
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera c)
    {
        if (c.cameraType != CameraType.Game && c.cameraType != CameraType.SceneView) return;
        if (_groundMat == null) return;
        if (!Application.isPlaying)
        {
            EnsureBuilt();
            if (_cardsFrom == -100) { _reveal = previewRevealed ? _count : RevealFor(LocalUnlocked); BuildCards(Mathf.Max(0, Mathf.FloorToInt(_reveal) - 1)); }
            UpdateNodes();
            SetGlobals();
        }
        var huge = new Bounds(Vector3.zero, Vector3.one * 100000f);
        int layer = gameObject.layer;
        if (_sky != null && _sprites != null) Graphics.RenderMesh(new RenderParams(_sky) { camera = c, worldBounds = huge, layer = layer }, _sprites, 0, Matrix4x4.identity);
        if (_ground != null) Graphics.RenderMesh(new RenderParams(_groundMat) { camera = c, worldBounds = huge, layer = layer }, _ground, 0, Matrix4x4.identity);
        if (_shadow != null)
        {
            var sp = new RenderParams(_shadow) { camera = c, worldBounds = huge, layer = layer };
            if (_sprites != null && _sprites.subMeshCount > 2) Graphics.RenderMesh(sp, _sprites, 2, Matrix4x4.identity);
            if (_dynamic != null) Graphics.RenderMesh(sp, _dynamic, 0, Matrix4x4.identity);
        }
        if (_cardMat != null && _cardMesh != null) Graphics.RenderMesh(new RenderParams(_cardMat) { camera = c, worldBounds = huge, layer = layer }, _cardMesh, 0, Matrix4x4.identity);
        if (_star != null && _sprites != null && _sprites.subMeshCount > 1) Graphics.RenderMesh(new RenderParams(_star) { camera = c, worldBounds = huge, layer = layer }, _sprites, 1, Matrix4x4.identity);
    }

    // ---- Construction ----

    float R(System.Random rng, float a, float b) => a + (b - a) * (float)rng.NextDouble();

    Color Var(System.Random rng, Color c, float amount = 0.06f)
    {
        float k = 1f + amount * ((float)rng.NextDouble() * 2f - 1f);
        return new Color(c.r * k, c.g * k, c.b * k, 1f);
    }

    void BuildStatic()
    {
        _meshDirty = false;
        if (_px.Count < 2) return;
        var b = _builder;
        b.Clear();
        BuildGround(b);
        BuildPath(b);
        for (int i = 0; i < _count; i++) BuildNode(b, i);
        foreach (var l in _landmarks)
            if (l.kind == Landmark.Pond) BuildPond(b, l.pos, new System.Random(decorSeed + 77));
        _ground = b.ToMesh(_ground, "Carte (sol)");

        _cards.Clear();
        var rng = new System.Random(decorSeed * 7919 + 13);
        PlaceHills(rng);
        PlaceDecor(rng);
        PlaceLamps(rng);
        PlaceLandmarks(rng);
        BuildSprites();
        _cardsFrom = -100;
    }

    void BuildGround(GumoriMapMesh b)
    {
        int cols = Mathf.CeilToInt(2f * halfWidth / Cell) + 1;
        int rows = Mathf.CeilToInt((_zMax - _zMin) / Cell) + 1;
        b.Set(GumoriMapMesh.Ground, Vector3.zero);
        int start = b.Count;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
                b.V(new Vector3(-halfWidth + i * Cell, 0f, _zMin + j * Cell), Vector3.up, Color.white);
        for (int j = 0; j < rows - 1; j++)
            for (int i = 0; i < cols - 1; i++)
            {
                int k = start + j * cols + i;
                b.Q(k, k + cols, k + cols + 1, k + 1);
            }
    }

    void BuildPath(GumoriMapMesh b)
    {
        float hw = 0.5f * pathWidth;
        int prevIndex = -1;
        for (int i = 0; i < _ps.Count; i++)
        {
            Vector3 dir = PathDirection(_ps[i]);
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            var c = new Vector3(_px[i], 0.02f, _pz[i]);
            b.Set(GumoriMapMesh.Path, c, 0f, -1f, _ps[i]);
            int l = b.V(c - side * hw, Vector3.up, Color.white);
            b.Set(GumoriMapMesh.Path, c, 0f, 1f, _ps[i]);
            b.V(c + side * hw, Vector3.up, Color.white);
            if (prevIndex >= 0) b.Q(prevIndex, l, l + 1, prevIndex + 1);
            prevIndex = l;
        }
    }

    // Jeton de carton : tranche (sombre) et dessus (couleur selon l'état, numéro).
    void BuildNode(GumoriMapMesh b, int i)
    {
        var p = _nodePos[i];
        b.Set(GumoriMapMesh.Node, p, 0f, i, 0f);
        b.Cylinder(p + Vector3.up * 0.01f, p + Vector3.up * NodeBase, NodeRadius, NodeRadius, Color.white, 24);
        b.Set(GumoriMapMesh.Node, p, 0f, i, 1f);
        b.Disc(p + Vector3.up * NodeBase, NodeRadius, NodeRadius, Color.white, 28);
    }

    void BuildPond(GumoriMapMesh b, Vector3 p, System.Random rng)
    {
        const float rx = 1.5f, rz = 1.0f;
        b.Set(GumoriMapMesh.Water, p, 0f, rx, rz);
        b.Disc(p + Vector3.up * 0.015f, rx, rz, Color.white, 32);
        for (int k = 0; k < 3; k++)
        {
            var q = p + new Vector3(R(rng, -0.7f, 0.7f), 0.02f, R(rng, -0.45f, 0.45f));
            float r = R(rng, 0.14f, 0.2f);
            b.Set(GumoriMapMesh.Decal, q, 0f, r, r * 0.8f);
            b.Disc(q, r, r * 0.8f, new Color(0.38f, 0.74f, 0.33f), 14);
        }
    }

    void Add(Kind kind, Vector3 pos, float scale, Color main, Color second, float seed, float variant = 0f, float sway = 0f, float shadow = -1f, float dir = 0f)
    {
        _cards.Add(new Card
        {
            pos = pos, kind = kind, scale = scale, main = main, second = second, seed = seed, variant = variant, sway = sway, dir = dir,
            shadow = shadow >= 0f ? shadow : Bounds(kind).y * scale * 0.75f,
        });
    }

    // Collines de papier des deux côtés, sur deux rangs (le plus loin un peu plus sombre).
    void PlaceHills(System.Random rng)
    {
        Color[] greens = { new Color(0.46f, 0.78f, 0.34f), new Color(0.40f, 0.72f, 0.36f), new Color(0.54f, 0.82f, 0.36f) };
        for (float z = _zMin + 2f; z < _zMax - 6f; z += 2.3f)
            for (int side = -1; side <= 1; side += 2)
                for (int row = 1; row >= 0; row--)
                {
                    float zz = z + R(rng, -0.6f, 0.6f) + row * 1.1f;
                    float x = PathXAtZ(zz) * 0.4f + side * (hillDistance + row * 1.6f + R(rng, 0f, 1.1f));
                    var col = Var(rng, greens[rng.Next(greens.Length)]) * (row == 1 ? 0.88f : 1f);
                    col.a = 1f;
                    Add(Kind.Hill, new Vector3(x, 0f, zz), R(rng, 1.5f, 2.3f) + row * 0.5f, col, Color.white, (float)rng.NextDouble(), 0f, 0f, 0f);
                }
    }

    // Décor dispersé à graine fixe : arbres (ronds, fruitiers, en fleurs, sapins), buissons, rochers, champignons, fleurs, herbe.
    void PlaceDecor(System.Random rng)
    {
        float hw = 0.5f * pathWidth;
        Color[] leaves = { new Color(0.36f, 0.72f, 0.32f), new Color(0.48f, 0.80f, 0.30f), new Color(0.28f, 0.62f, 0.36f) };
        Color[] petals = { Color.white, new Color(1f, 0.85f, 0.25f), new Color(1f, 0.55f, 0.75f), new Color(0.72f, 0.6f, 1f), new Color(1f, 0.38f, 0.38f), new Color(0.5f, 0.75f, 1f) };
        Color[] fruits = { new Color(1f, 0.28f, 0.28f), new Color(1f, 0.6f, 0.12f) };
        const float step = 0.7f;
        for (float z = _zMin + 2f; z < _zMax - 4f; z += step)
            for (float x = -5f; x <= 5f; x += step)
            {
                float px = x + PathXAtZ(z) * 0.4f + R(rng, -0.3f, 0.3f), pz = z + R(rng, -0.3f, 0.3f);
                float r = (float)rng.NextDouble();
                float seed = (float)rng.NextDouble();
                float d = PathDistance(px, pz);
                if (d < hw + 0.18f || Blocked(px, pz, 0f)) continue;
                float forest = Mathf.PerlinNoise(px * 0.16f + 71f, pz * 0.16f + 13f);
                float meadow = Mathf.PerlinNoise(px * 0.32f + 3f, pz * 0.32f + 91f);
                var pos = new Vector3(px, 0f, pz);
                float treeChance = (0.06f + 0.35f * Smooth01((forest - 0.42f) / 0.25f)) * treeDensity;
                if (d > 2.0f && r < treeChance && !Blocked(px, pz, 0.8f))
                {
                    float kindRoll = (float)rng.NextDouble();
                    if (kindRoll < 0.2f) Add(Kind.Pine, pos, R(rng, 0.7f, 1.0f), Var(rng, new Color(0.22f, 0.55f, 0.42f)), Color.white, seed, 0f, 0.012f);
                    else if (kindRoll < 0.3f) Add(Kind.Tree, pos, R(rng, 0.65f, 0.9f), Var(rng, new Color(1f, 0.68f, 0.82f)), Color.white, seed, 0f, 0.015f);
                    else Add(Kind.Tree, pos, R(rng, 0.65f, 0.95f), Var(rng, leaves[rng.Next(leaves.Length)]), fruits[rng.Next(fruits.Length)], seed, kindRoll > 0.75f ? 1f : 0f, 0.015f);
                }
                else if (d > 1.0f && r < treeChance + 0.05f)
                    Add(Kind.Bush, pos, R(rng, 0.65f, 0.95f), Var(rng, new Color(0.34f, 0.68f, 0.3f)), rng.NextDouble() < 0.5 ? new Color(0.95f, 0.25f, 0.35f) : new Color(0.45f, 0.45f, 1f), seed, rng.NextDouble() < 0.45 ? 1f : 0f, 0.02f);
                else if (r > 0.975f)
                    Add(Kind.Rock, pos, R(rng, 0.7f, 1.3f), Var(rng, new Color(0.68f, 0.7f, 0.78f)), Color.white, seed);
                else if (r > 0.955f && d < 4f)
                    Add(Kind.Mushroom, pos, R(rng, 0.8f, 1.2f), rng.NextDouble() < 0.75 ? new Color(0.95f, 0.26f, 0.26f) : new Color(0.72f, 0.5f, 0.95f), Color.white, seed);
                else
                {
                    float flowerChance = flowerDensity * (0.08f + 0.5f * Smooth01((meadow - 0.5f) / 0.2f));
                    if (r < flowerChance)
                        Add(Kind.Flower, pos, R(rng, 0.9f, 1.25f), petals[rng.Next(petals.Length)], new Color(1f, 0.82f, 0.25f), seed, 0f, 0.15f, 0.06f);
                    else if (r < flowerChance + 0.22f * grassDensity)
                        Add(Kind.Tuft, pos, R(rng, 0.8f, 1.3f), Var(rng, new Color(0.36f, 0.68f, 0.28f)), Color.white, seed, 0f, 0.25f, 0f);
                }
            }
        // Bordure du chemin : touffes, fleurs et petits buissons serrés de chaque côté (façon Candy Crush).
        for (float s = 0.5f; s < _length - 0.5f; s += 0.55f)
        {
            Vector3 c = PathPoint(s), dir = PathDirection(s);
            var nrm = new Vector3(dir.z, 0f, -dir.x);
            for (int side = -1; side <= 1; side += 2)
            {
                float off = hw + R(rng, 0.25f, 0.75f);
                var pos = new Vector3(c.x, 0f, c.z) + nrm * (side * off) + dir * R(rng, -0.2f, 0.2f);
                float r = (float)rng.NextDouble(), seed = (float)rng.NextDouble();
                if (Blocked(pos.x, pos.z, 0f) || PathDistance(pos.x, pos.z) < hw + 0.18f) continue;
                if (r < 0.3f * flowerDensity) Add(Kind.Flower, pos, R(rng, 0.9f, 1.2f), petals[rng.Next(petals.Length)], new Color(1f, 0.82f, 0.25f), seed, 0f, 0.15f, 0.06f);
                else if (r < 0.5f * grassDensity) Add(Kind.Tuft, pos, R(rng, 0.8f, 1.2f), Var(rng, new Color(0.36f, 0.68f, 0.28f)), Color.white, seed, 0f, 0.25f, 0f);
                else if (r < 0.58f) Add(Kind.Bush, pos + nrm * (side * 0.35f), R(rng, 0.5f, 0.7f), Var(rng, new Color(0.38f, 0.72f, 0.3f)), new Color(0.95f, 0.25f, 0.35f), seed, rng.NextDouble() < 0.4 ? 1f : 0f, 0.02f);
            }
        }
        // Papillons.
        for (float z = _zMin + 4f; z < _zMax - 6f; z += R(rng, 4f, 8f))
        {
            float x = PathXAtZ(z) + R(rng, -2f, 2f);
            Add(Kind.Butterfly, new Vector3(x, 0f, z), 1f, petals[2 + rng.Next(4)], Color.white, (float)rng.NextDouble(), 0f, 0f, 0f);
        }
    }

    bool Blocked(float x, float z, float margin)
    {
        for (int i = 0; i < _keepOut.Count; i++)
        {
            var k = _keepOut[i];
            float r = k.z + margin;
            if ((x - k.x) * (x - k.x) + (z - k.y) * (z - k.y) < r * r) return true;
        }
        return false;
    }

    // Lampadaires à rayures à côté d'un niveau sur deux, côté alterné (à côté du jeton : rien ne le cache).
    void PlaceLamps(System.Random rng)
    {
        Color[] stripes = { new Color(1f, 0.45f, 0.65f), new Color(0.4f, 0.7f, 1f), new Color(0.55f, 0.85f, 0.4f) };
        for (int i = 0; i < _count; i += 2)
        {
            float s = _nodeS[i];
            Vector3 c = PathPoint(s), dir = PathDirection(s);
            float side = (i / 2) % 2 == 0 ? 1f : -1f;
            var pos = new Vector3(c.x, 0f, c.z) + new Vector3(dir.z, 0f, -dir.x) * (side * (0.5f * pathWidth + 0.75f));
            if (Blocked(pos.x, pos.z, 0f)) continue;
            Add(Kind.Lamp, pos, 1f, stripes[(i / 2) % stripes.Length], new Color(1f, 0.9f, 0.45f), (float)rng.NextDouble(), 0f, 0f, 0.18f);
        }
    }

    void PlaceLandmarks(System.Random rng)
    {
        foreach (var l in _landmarks)
        {
            var p = l.pos;
            float seed = (float)rng.NextDouble();
            switch (l.kind)
            {
                case Landmark.House:
                    Add(Kind.House, p, 0.95f, new Color(0.92f, 0.38f, 0.4f), new Color(0.62f, 0.86f, 1f), seed);
                    Add(Kind.Sign, new Vector3(PathXAtZ(1.4f) - l.side * 1.0f, 0f, 1.4f), 1f, new Color(0.84f, 0.6f, 0.38f), new Color(0.45f, 0.7f, 1f), seed);
                    break;
                case Landmark.Pond:
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI * 2f / 6f + R(rng, -0.25f, 0.25f);
                        var q = p + new Vector3(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.1f);
                        if (k % 2 == 0) Add(Kind.Rock, q, R(rng, 0.7f, 1.0f), Var(rng, new Color(0.7f, 0.72f, 0.8f)), Color.white, (float)rng.NextDouble());
                        else Add(Kind.Tuft, q, 1.6f, new Color(0.3f, 0.58f, 0.3f), Color.white, (float)rng.NextDouble(), 0f, 0.2f, 0f);
                    }
                    break;
                case Landmark.Windmill:
                    Add(Kind.Windmill, p, 1.25f, new Color(0.62f, 0.4f, 0.75f), new Color(0.98f, 0.95f, 0.88f), seed);
                    break;
                case Landmark.BigTree:
                    Add(Kind.Tree, p, 1.7f, new Color(0.38f, 0.74f, 0.3f), new Color(1f, 0.3f, 0.3f), seed, 1f, 0.01f);
                    Add(Kind.Mushroom, p + new Vector3(-l.side * 0.9f, 0f, -0.7f), 1.6f, new Color(0.95f, 0.26f, 0.26f), Color.white, seed);
                    break;
                case Landmark.Farm:
                    Add(Kind.Barn, p + new Vector3(l.side * 0.6f, 0f, 0.8f), 1.15f, new Color(0.88f, 0.32f, 0.3f), Color.white, seed);
                    for (int k = 0; k < 3; k++)
                        Add(Kind.Hay, p + new Vector3(-l.side * 0.3f + (k - 1) * 0.65f, 0f, -0.6f + R(rng, -0.2f, 0.2f)), R(rng, 0.85f, 1.05f), new Color(0.98f, 0.82f, 0.4f), Color.white, (float)rng.NextDouble());
                    break;
                case Landmark.Gate:
                    Add(Kind.Gate, p, 1f, new Color(0.8f, 0.8f, 0.9f), new Color(1f, 0.45f, 0.65f), seed);
                    break;
            }
        }
    }

    // Maillage des découpes, triées du plus loin au plus près (une seule passe, transparence correcte), avec les nuages des
    // bandes pas encore découvertes (à partir de 'from' : celles d'avant sont déjà écartées).
    void BuildCards(int from)
    {
        _cardsFrom = from;
        var all = new List<Card>(_cards.Count + 300);
        float frontier = from + 7 < _count ? NodeZ(from + 7) : float.MaxValue;
        foreach (var c in _cards)
            if (c.pos.z < frontier) all.Add(c);
        for (int k = from; k <= from + 6; k++)
        {
            bool end = k >= _count;
            float zc = end ? NodeZ(_count - 1) + spacing * (k - _count + 1) : NodeZ(k);
            var rng = new System.Random(decorSeed * 31 + k * 977);
            for (int row = 0; row < 3; row++)
            {
                float z = zc + (row - 1f) * spacing * 0.33f;
                for (float x = -7f; x <= 7f; x += 1.75f)
                {
                    float cx = x + PathXAtZ(z) * 0.4f + R(rng, -0.35f, 0.35f), cz = z + R(rng, -0.2f, 0.2f);
                    all.Add(new Card
                    {
                        pos = new Vector3(cx, 0f, cz), kind = Kind.Cloud, scale = R(rng, 1.35f, 1.8f), main = Color.white, second = Color.white,
                        seed = (float)rng.NextDouble(), variant = end ? 1000f : k, dir = cx >= PathXAtZ(cz) ? 1f : -1f,
                    });
                }
            }
        }
        all.Sort((a, b) => b.pos.z.CompareTo(a.pos.z));

        var verts = new List<Vector3>(all.Count * 4);
        var cols = new List<Color>(all.Count * 4);
        var uv0 = new List<Vector4>(all.Count * 4);
        var uv1 = new List<Vector4>(all.Count * 4);
        var uv2 = new List<Vector4>(all.Count * 4);
        var tris = new List<int>(all.Count * 6);
        foreach (var c in all)
        {
            Vector3 bounds = Bounds(c.kind);
            float ink = c.kind == Kind.Flower || c.kind == Kind.Tuft || c.kind == Kind.Butterfly ? inkWidth * 0.45f : inkWidth;
            float margin = ink * 2.4f + 0.02f;
            float x0 = bounds.x * c.scale - margin, x1 = bounds.y * c.scale + margin, y0 = -margin, y1 = bounds.z * c.scale + margin;
            int s = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                verts.Add(c.pos);
                cols.Add(c.main.linear);
                uv0.Add(new Vector4(k == 0 || k == 3 ? x0 : x1, k < 2 ? y0 : y1, (float)c.kind, c.seed));
                Color second = c.second.linear;
                uv1.Add(new Vector4(second.r, second.g, second.b, ink));
                uv2.Add(new Vector4(c.sway, c.variant, c.dir, c.scale));
            }
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }
        if (_cardMesh == null) _cardMesh = new Mesh { name = "Carte (découpes)", hideFlags = HideFlags.HideAndDontSave };
        _cardMesh.Clear();
        _cardMesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _cardMesh.SetVertices(verts);
        _cardMesh.SetColors(cols);
        _cardMesh.SetUVs(0, uv0);
        _cardMesh.SetUVs(1, uv1);
        _cardMesh.SetUVs(2, uv2);
        _cardMesh.SetTriangles(tris, 0, false);
        _cardMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
    }

    // Ciel (sous-maillage 0), étoiles au-dessus des jetons et halo du niveau actuel (1), ombres des découpes au sol (2).
    void BuildSprites()
    {
        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var uv0 = new List<Vector4>();
        var uv1 = new List<Vector4>();
        var uv2 = new List<Vector4>();
        var sky = new List<int>();
        var stars = new List<int>();
        var shadows = new List<int>();
        void Quad(List<int> tris, Vector3 center, float kind, Vector2 size, float node, float slot, Vector2 offset, Color color)
        {
            int s = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                verts.Add(center);
                cols.Add(color);
                uv0.Add(new Vector4(k == 0 || k == 3 ? -1f : 1f, k < 2 ? -1f : 1f, kind, 0f));
                uv1.Add(new Vector4(size.x, size.y, node, slot));
                uv2.Add(new Vector4(offset.x, offset.y, 0f, 0f));
            }
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }
        Quad(sky, Vector3.zero, 0f, Vector2.one, 0f, 0f, Vector2.zero, Color.white);
        for (int i = _count - 1; i >= 0; i--)
        {
            var top = _nodePos[i] + Vector3.up * NodeBase;
            Quad(stars, _nodePos[i] + Vector3.up * 0.005f, 3f, new Vector2(0.95f, 0.95f), i, 0f, Vector2.zero, Color.white);
            for (int slot = 0; slot < 3; slot++)
            {
                var off = new Vector2((slot - 1) * 0.36f, slot == 1 ? 0.62f : 0.5f);
                Quad(stars, top, 1f, Vector2.one * (slot == 1 ? 0.2f : 0.17f), i, slot, off, Color.white);
            }
        }
        var shadowColor = new Color(0.02f, 0.06f, 0.05f, 0.22f);
        foreach (var c in _cards)
            if (c.shadow > 0f) Quad(shadows, c.pos + Vector3.up * 0.01f, 2f, new Vector2(c.shadow, c.shadow * 0.45f), 0f, 0f, Vector2.zero, shadowColor);
        if (_sprites == null) _sprites = new Mesh { name = "Carte (ciel, étoiles, ombres)", hideFlags = HideFlags.HideAndDontSave };
        _sprites.Clear();
        _sprites.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _sprites.SetVertices(verts);
        _sprites.SetColors(cols);
        _sprites.SetUVs(0, uv0);
        _sprites.SetUVs(1, uv1);
        _sprites.SetUVs(2, uv2);
        _sprites.subMeshCount = 3;
        _sprites.SetTriangles(sky, 0, false);
        _sprites.SetTriangles(stars, 1, false);
        _sprites.SetTriangles(shadows, 2, false);
        _sprites.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
    }

    readonly List<Vector3> _dv = new List<Vector3>();
    readonly List<Color> _dc = new List<Color>();
    readonly List<Vector4> _d0 = new List<Vector4>(), _d1 = new List<Vector4>(), _d2 = new List<Vector4>();
    readonly List<int> _dt = new List<int>();

    // Ombre de l'avatar (ellipse douce au sol).
    void BuildDynamic()
    {
        if (_dynamic == null) _dynamic = new Mesh { name = "Carte (ombre de l'avatar)", hideFlags = HideFlags.HideAndDontSave };
        _dv.Clear(); _dc.Clear(); _d0.Clear(); _d1.Clear(); _d2.Clear(); _dt.Clear();
        if (_shadowAlpha > 0.001f)
        {
            for (int k = 0; k < 4; k++)
            {
                _dv.Add(_shadowPos);
                _dc.Add(new Color(0.02f, 0.03f, 0.1f, _shadowAlpha));
                _d0.Add(new Vector4(k == 0 || k == 3 ? -1f : 1f, k < 2 ? -1f : 1f, 2f, 0f));
                _d1.Add(new Vector4(_shadowRadius, _shadowRadius * 0.8f, 0f, 0f));
                _d2.Add(Vector4.zero);
            }
            _dt.Add(0); _dt.Add(1); _dt.Add(2); _dt.Add(0); _dt.Add(2); _dt.Add(3);
        }
        _dynamic.Clear();
        _dynamic.SetVertices(_dv);
        _dynamic.SetColors(_dc);
        _dynamic.SetUVs(0, _d0);
        _dynamic.SetUVs(1, _d1);
        _dynamic.SetUVs(2, _d2);
        _dynamic.SetTriangles(_dt, 0, false);
        _dynamic.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
    }

    void OnDrawGizmos()
    {
        if (_px.Count < 2) return;
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.8f);
        for (int i = 0; i < _px.Count - 1; i += 2)
            Gizmos.DrawLine(new Vector3(_px[i], 0.05f, _pz[i]), new Vector3(_px[Mathf.Min(i + 2, _px.Count - 1)], 0.05f, _pz[Mathf.Min(i + 2, _pz.Count - 1)]));
    }
}
