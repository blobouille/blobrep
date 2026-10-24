using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Habillage des slimes : la gelée suit exactement l'anneau physique (contour lissé entre les capsules), avec motif, visage et
// accessoires dessinés dans un repère qui se déforme avec elle (un slime écrasé a les yeux écrasés). Un design par niveau.
// Vivant : clignements, regard qui suit le slime à lâcher ou une fusion voisine, bouche ouverte en chute, grimace au choc,
// sourire après une fusion, visage qui se remet à l'endroit après avoir roulé. Aperçus du slime à lâcher et du suivant,
// vitrine des designs dans la Scene view.
// Purement visuel : lit l'état dans LateUpdate, n'écrit jamais dans la simulation (déterminisme intact). Un seul maillage pour
// tous les slimes (shader Slime/Body), dessiné pour chaque caméra.
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public class SlimeSkin : MonoBehaviour
{
    [Header("Slimes")]
    [Tooltip("Lâcher (BlobDropper + BlobFusion) dont on habille les slimes. Vide = celui de la scène.")]
    public BlobDropper dropper;
    public Shader shader;
    [Tooltip("Un design par niveau (0 = le plus petit), dans l'ordre des familles de couleur. Au-delà de la liste : le dernier.")]
    public List<SlimeDesign> designs = DefaultDesigns();
    [Tooltip("Autres gumori de la collection (pas encore en jeu) : montrés dans la vitrine, à la taille de leur famille.")]
    public List<SlimeDesign> variants = SlimeDesign.Variants();

    [Header("Allure")]
    [Tooltip("Points max du contour dessiné par slime (lissé entre les capsules). Plus = plus rond, plus coûteux.")]
    [Range(24, 160)] public int outlineSamples = 72;
    [Tooltip("Longueur visée d'un côté du contour, en pixels à l'écran : les petits slimes ont moins de points (moins coûteux).")]
    [Range(2f, 20f)] public float pixelsPerSample = 6f;
    [Tooltip("Épaisseur du liseré sombre du contour, en m (absolu, même épaisseur pour tous).")]
    [Min(0f)] public float outlineWidth = 0.05f;
    [Tooltip("Rebord translucide de la gelée (sa couleur, plus claire, on voit un peu le fond à travers) : épaisseur en fraction\n" +
             "du rayon, donc proportionnelle à la taille du slime. 0 = aucun.")]
    [Range(0f, 0.4f)] public float rimWidth = 0.074f;
    [Tooltip("Opacité du rebord translucide : 1 = opaque comme le reste, plus bas = on voit davantage le fond à travers.")]
    [Range(0f, 1f)] public float rimOpacity = 0.538f;
    [Tooltip("Éclaircissement du rebord vers une gelée claire de la couleur du slime (0 = même couleur que le corps).")]
    [Range(0f, 1f)] public float rimLighten = 0.35f;
    [Tooltip("Taille du visage du premier slime (1 = normal).")]
    [Min(0.1f)] public float faceSize = 1.15f;
    [Tooltip("Visage des gros : taille relative × (diamètre / premier)^-faceShrink. 0 = même proportion pour tous ;\n" +
             "plus grand = visage plus petit sur les gros (ils paraissent plus gros).")]
    [Range(0f, 1f)] public float faceShrink = 0.35f;
    [Tooltip("Temps (s) pour qu'un slime qui a roulé remette son visage à l'endroit. 0 = le visage tourne avec le corps.")]
    [Min(0f)] public float uprightTime = 0.8f;
    [Tooltip("Intervalle moyen (s) entre deux clignements d'yeux.")]
    [Min(0.5f)] public float blinkInterval = 3.5f;
    [Tooltip("Les slimes suivent des yeux le slime à lâcher.")]
    public bool watchDrop = true;

    [Header("Réactions")]
    [Tooltip("Vitesse perdue d'un coup (km/h) qui fait grimacer le slime et déclenche les éclaboussures et le son d'impact.")]
    [SpeedKmh] [Min(0f)] public float impactSpeed = 2.5f;
    [Tooltip("Vitesse de chute (km/h) au-delà de laquelle un slime ouvre grand la bouche.")]
    [SpeedKmh] [Min(0f)] public float fallSpeed = 5f;
    [Tooltip("Durée (s) du sourire après une fusion.")]
    [Min(0f)] public float happyTime = 1.6f;
    [Tooltip("Éclair blanc du nouveau slime à l'éclat de la fusion (SlimeFx) : 0 = aucun, 1 = tout blanc.")]
    [Range(0f, 1f)] public float mergeFlash = 0.45f;
    [Tooltip("Distance (en diamètres du nouveau slime) à laquelle les voisins regardent une fusion, étonnés.")]
    [Min(0f)] public float curiosity = 2f;

    [Header("Aperçus")]
    [Tooltip("Slime à lâcher et suivant dessinés avec leur design.")]
    public bool drawPreviews = true;
    [Tooltip("Vitrine de tous les designs (Scene view, hors Play).")]
    public bool gallery = true;
    [Tooltip("Vitrine aussi en Play, avec les noms.")]
    public bool galleryInPlay;
    [Tooltip("Vitrine : une ligne avec la version shiny de chaque niveau.")]
    public bool galleryShiny = true;
    [Tooltip("Vitrine : les autres gumori de la collection (variantes).")]
    public bool galleryVariants = true;
    [Tooltip("Coin haut gauche de la vitrine, en m.")]
    public Vector2 galleryOrigin = new Vector2(-17f, 15f);
    [Tooltip("Largeur max d'une ligne de la vitrine, en m.")]
    [Min(1f)] public float galleryWidth = 12f;

    // Choc détecté sur un slime : point de contact, normale (vers le slime), vitesse perdue (m/s).
    public event System.Action<BLOB, Vector2, Vector2, float> Impact;

    // Dessin de slimes sans physique (fin de niveau…) : appelé pendant la construction du maillage, temps écoulé en argument ;
    // y appeler DrawVirtual.
    public event System.Action<float> DrawExtras;

    // Slime sans physique, rond, dessiné par DrawVirtual (mêmes design, visage et accessoires que les vrais).
    public class VirtualSlime
    {
        public int level;
        public Vector2 center;
        public float radius, scale = 1f;
        internal Look look;
    }

    public VirtualSlime NewVirtual(int level, float radius) => new VirtualSlime { level = level, radius = radius, look = NewLook(level, radius) };

    // Seulement pendant DrawExtras. Visage heureux, regard vers le haut.
    public void DrawVirtual(VirtualSlime v, float dt)
    {
        v.look.level = v.level;
        v.look.happy = 1f;
        v.look.attention = v.center + Vector2.up * 4f;
        v.look.attentionTime = 1f;
        EmitRound(v.look, v.center, v.radius, dt, v.scale, Time.time);
    }

    // Seulement pendant DrawExtras : un vrai blob qui n'appartient pas à BlobDropper (avatar de la carte), au design 'level',
    // qui regarde vers 'lookAt'.
    public void DrawBlob(BLOB blob, int level, Vector2 lookAt, float dt)
    {
        if (blob == null || blob.CapsuleCount < 3) return;
        var look = GetLook(blob);
        look.level = level;
        look.seen = _frame;
        look.attention = lookAt;
        look.attentionTime = 1f;
        UpdateBlob(blob, look, dt, LevelSimulation.Step);
    }

    // Carte : matrice qui pose le dessin dans le monde 3D, et taille d'un pixel en unités du dessin (caméra en perspective, 0 =
    // calculée pour une caméra orthographique). Défauts : dessin tel quel (réservoir).
    [System.NonSerialized] public Matrix4x4 renderMatrix = Matrix4x4.identity;
    [System.NonSerialized] public float pixelSize;

    const float TAU = Mathf.PI * 2f;

    internal class Look
    {
        public int level, seen, step;
        public float radius, rot, frame, seed;
        public bool hasRot, hasCenter, hasVelocity;
        public Vector2 center, velocity, look, attention;
        public float age, blinkIn, blinkT, happy, wow, squish, flash, pop = 1f, impactCooldown, attentionTime;
        public bool waitPop; // accessoires cachés jusqu'à l'éclat de la fusion
        public SlimeDesign design; // design imposé (vitrine), sinon celui du niveau
        public bool shiny;
        // Vitesses des derniers pas vus (choc = vitesse perdue sur ImpactWindow pas, quelle que soit la cadence d'affichage).
        public readonly Vector2[] pastVelocity = new Vector2[8];
        public readonly int[] pastStep = new int[8];
        public int pastCount, pastHead;
    }

    const int ImpactWindow = 6;

    struct Pending
    {
        public BLOB blob;
        public Vector2 position;
        public bool merge;
    }

    BlobFusion _fusion;
    SlimeFx _fx;
    Material _material;
    static readonly int RimId = Shader.PropertyToID("_Rim");
    Mesh _mesh;
    readonly Dictionary<BLOB, Look> _looks = new Dictionary<BLOB, Look>();
    readonly List<BLOB> _dead = new List<BLOB>();
    readonly List<Pending> _pending = new List<Pending>();
    readonly Look _held = new Look(), _nextLook = new Look();
    readonly Look[] _galleryLooks = new Look[64];
    struct GalleryEntry
    {
        public SlimeDesign design;
        public int level;
        public bool shiny, newRow;
    }
    readonly List<GalleryEntry> _gallery = new List<GalleryEntry>();
    System.Random _rand = new System.Random(7);
    int _frame, _builtFrame = -1, _heldLevel = -1, _nextLevel = -1, _seedCounter;
    bool _searched;
    float _pixel = 0.01f;

    // Sommets entrelacés (un seul envoi par image) ; tableaux agrandis seulement quand il le faut.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 position;
        public Color32 color;
        public Vector4 uv0, uv1, uv2, uv3, uv4, uv5;
    }

    static readonly VertexAttributeDescriptor[] Layout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 4),
    };

    const MeshUpdateFlags Quiet = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers;
    Vertex[] _vb = new Vertex[8192];
    int[] _ib = new int[24576];
    int _vertexCount, _indexCount, _vbCapacity, _ibCapacity;
    readonly List<Vector2> _poly = new List<Vector2>();

    Vector2[] _ring = new Vector2[64], _surf = new Vector2[64], _sp = new Vector2[256], _sn = new Vector2[256];
    Vector2[] _dd = new Vector2[256], _dl = new Vector2[256];
    readonly Dictionary<int, Vector2[]> _units = new Dictionary<int, Vector2[]>();
    Vector4 _k2, _k3, _k4, _k5;
    float _flash;

    public SlimeDesign Design(int level) => designs.Count == 0 ? null : designs[Mathf.Clamp(level, 0, designs.Count - 1)];

    SlimeDesign Resolve(Look look)
    {
        var d = look.design ?? Design(look.level);
        return d != null && look.shiny ? d.Shiny : d;
    }

    public static List<SlimeDesign> DefaultDesigns() => SlimeDesign.Levels();

    void OnValidate()
    {
        foreach (var d in designs) d?.ResetShinyCache();
        foreach (var d in variants) d?.ResetShinyCache();
    }

    void OnEnable()
    {
        Prepare();
        _mesh = new Mesh { name = "Slimes", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
        _mesh.MarkDynamic();
        _vbCapacity = _ibCapacity = 0; // maillage neuf : tampons à recréer (sinon erreur après un OnDisable / OnEnable)
        if (shader == null) shader = Shader.Find("Slime/Body");
        if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3000 };
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        if (Application.isPlaying)
        {
            if (_fusion != null) _fusion.Merged += OnMerged;
            _fx = FindAnyObjectByType<SlimeFx>();
            if (_fx != null) _fx.Popped += OnPopped;
            if (dropper != null) dropper.Dropped += OnDropped;
            LevelRestart.Restarted += OnRestarted;
        }
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (_fusion != null) _fusion.Merged -= OnMerged;
        if (_fx != null) _fx.Popped -= OnPopped;
        if (dropper != null) dropper.Dropped -= OnDropped;
        LevelRestart.Restarted -= OnRestarted;
        _looks.Clear();
        _pending.Clear();
        if (_mesh != null) DestroyImmediate(_mesh);
        if (_material != null) DestroyImmediate(_material);
    }

    void Prepare()
    {
        // En Play, cherché une seule fois (la carte n'a pas de BlobDropper : pas de recherche à chaque image).
        if (dropper == null && !(_searched && Application.isPlaying)) dropper = FindAnyObjectByType<BlobDropper>();
        _searched = true;
        if (_fusion == null && dropper != null) _fusion = dropper.GetComponent<BlobFusion>();
    }

    void OnRestarted()
    {
        _looks.Clear();
        _pending.Clear();
    }

    void OnMerged(BLOB merged, int level, Vector2 position)
    {
        if (!LevelSimulation.FastForwarding) _pending.Add(new Pending { blob = merged, position = position, merge = true });
    }

    // Éclat de la fusion (le nouveau slime est redevenu rond) : éclair, accessoires qui poussent, sourire.
    void OnPopped(SlimeScore.Hit hit, Vector2 position)
    {
        if (hit.blob == null || !_looks.TryGetValue(hit.blob, out var look)) return;
        look.waitPop = false;
        look.pop = 0f;
        look.flash = mergeFlash;
        look.wow = 0f;
        look.happy = happyTime;
    }

    void OnDropped(BLOB blob, int level)
    {
        if (!LevelSimulation.FastForwarding) _pending.Add(new Pending { blob = blob, position = blob.transform.position });
    }

    float Rand() => (float)_rand.NextDouble();

    Look NewLook(int level, float radius)
    {
        var look = new Look { level = level, radius = radius, seed = Mathf.Repeat(++_seedCounter * 0.618034f, 1f) * 50f };
        look.blinkIn = blinkInterval * (0.3f + Rand());
        return look;
    }

    Look GetLook(BLOB blob)
    {
        if (_looks.TryGetValue(blob, out var look)) return look;
        look = NewLook(_fusion != null ? _fusion.LevelOf(blob) : 0, 0.5f * blob.diameter);
        _looks.Add(blob, look);
        return look;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        Prepare();
        BuildPlay();
    }

    void BuildPlay()
    {
        float dt = Time.deltaTime;
        int step = LevelSimulation.Step;
        _frame++;
        UpdatePixel();

        for (int i = 0; i < _pending.Count; i++)
        {
            var p = _pending[i];
            if (p.blob == null) continue;
            var look = GetLook(p.blob);
            if (p.merge)
            {
                // Pendant la fusion : bouche ronde, accessoires cachés jusqu'à l'éclat (sans SlimeFx : tout de suite).
                look.waitPop = _fx != null && _fx.isActiveAndEnabled;
                look.wow = look.waitPop ? 3f : 0.35f;
                look.happy = happyTime;
                look.pop = 0f;
                if (!look.waitPop) look.flash = mergeFlash;
                float reach = curiosity * 2f * look.radius;
                foreach (var other in _looks.Values)
                {
                    if (other == look || (other.center - p.position).sqrMagnitude > reach * reach) continue;
                    other.attention = p.position;
                    other.attentionTime = 1f;
                    other.wow = Mathf.Max(other.wow, 0.3f);
                }
            }
            else _heldPopTime = 0f;
        }
        _pending.Clear();

        Begin();
        if (dropper != null)
        {
            var blobs = dropper.Blobs;
            for (int b = 0; b < blobs.Count; b++)
            {
                var blob = blobs[b];
                if (blob == null || blob.CapsuleCount < 3) continue;
                var look = GetLook(blob);
                look.seen = _frame;
                UpdateBlob(blob, look, dt, step);
            }
            if (drawPreviews) DrawPreviews(dt);
        }
        DrawExtras?.Invoke(dt);
        if (galleryInPlay) DrawGallery(dt);
        End();

        _dead.Clear();
        foreach (var pair in _looks)
            if (pair.Value.seen != _frame) _dead.Add(pair.Key);
        for (int i = 0; i < _dead.Count; i++) _looks.Remove(_dead[i]);
        _builtFrame = Time.frameCount;
    }

    void UpdatePixel()
    {
        if (pixelSize > 0f) { _pixel = pixelSize; return; }
        var cam = Camera.main;
        if (cam != null && cam.orthographic && cam.pixelHeight > 0) _pixel = 2f * cam.orthographicSize / cam.pixelHeight;
    }

    // Anneau réel : positions des capsules, rotation du corps, chocs, puis contour lissé et dessin.
    void UpdateBlob(BLOB blob, Look look, float dt, int step)
    {
        int m = blob.CapsuleCount;
        if (_ring.Length < m) { _ring = new Vector2[m]; _surf = new Vector2[m]; }
        Vector2 c = Vector2.zero;
        for (int i = 0; i < m; i++)
        {
            _ring[i] = blob.Capsule(i).position;
            c += _ring[i];
        }
        c /= m;

        float sx = 0f, sy = 0f;
        var rest = Units(m);
        for (int i = 0; i < m; i++)
        {
            float ca = rest[i].x, sa = rest[i].y;
            Vector2 d = _ring[i] - c;
            sx += ca * d.x + sa * d.y;
            sy += ca * d.y - sa * d.x;
        }
        float rot = Mathf.Atan2(sy, sx);
        if (!look.hasRot) { look.rot = rot; look.frame = 0f; look.hasRot = true; }
        else
        {
            look.frame += Mathf.DeltaAngle(look.rot * Mathf.Rad2Deg, rot * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            look.rot = rot;
        }
        look.frame = Mathf.DeltaAngle(0f, look.frame * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        if (uprightTime > 0f) look.frame *= Mathf.Exp(-dt / uprightTime);

        if (look.step != step || !look.hasCenter)
        {
            int ds = step - look.step;
            if (look.hasCenter && ds > 0 && ds < 60)
            {
                Vector2 v = (c - look.center) / (ds * Time.fixedDeltaTime);
                for (int h = 0; h < look.pastCount; h++)
                {
                    int idx = (look.pastHead - 1 - h + 8) % 8;
                    if (step - look.pastStep[idx] < ImpactWindow) continue;
                    Vector2 dv = v - look.pastVelocity[idx];
                    float hit = dv.magnitude;
                    if (hit > impactSpeed && look.age > 0.12f && look.impactCooldown <= 0f)
                    {
                        Vector2 n = dv / hit;
                        look.squish = 0.28f;
                        look.impactCooldown = 0.25f;
                        Impact?.Invoke(blob, c - n * look.radius, n, hit);
                    }
                    break;
                }
                look.pastVelocity[look.pastHead] = v;
                look.pastStep[look.pastHead] = step;
                look.pastHead = (look.pastHead + 1) % 8;
                look.pastCount = Mathf.Min(8, look.pastCount + 1);
                look.velocity = v;
                look.hasVelocity = true;
            }
            look.center = c;
            look.step = step;
            look.hasCenter = true;
        }

        Animate(look, dt, c);

        float rc = blob.CapsuleRadius;
        for (int i = 0; i < m; i++)
        {
            Vector2 t = _ring[(i + 1) % m] - _ring[(i + m - 1) % m];
            Vector2 n = new Vector2(t.y, -t.x);
            float len = n.magnitude;
            _surf[i] = _ring[i] + (len > 1e-6f ? n / len : Vector2.zero) * rc;
        }
        int k = Mathf.Max(1, Mathf.RoundToInt((float)Samples(blob.diameter) / m));
        int count = m * k;
        EnsureSamples(count);
        for (int i = 0; i < m; i++)
        {
            Vector2 p0 = _surf[(i + m - 1) % m], p1 = _surf[i], p2 = _surf[(i + 1) % m], p3 = _surf[(i + 2) % m];
            for (int j = 0; j < k; j++)
            {
                float u = (float)j / k;
                _sp[i * k + j] = CatmullRom(p0, p1, p2, p3, u);
            }
        }
        EmitSlime(Resolve(look), look, c, count, rot, blob.diameter);
    }

    void EnsureSamples(int count)
    {
        if (_sp.Length >= count) return;
        _sp = new Vector2[count];
        _sn = new Vector2[count];
        _dd = new Vector2[count];
        _dl = new Vector2[count];
    }

    int Samples(float diameter) => Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * diameter / Mathf.Max(1e-4f, _pixel * pixelsPerSample)), 24, Mathf.Max(24, outlineSamples));

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
    }

    // Minuteries, clignements et regard.
    void Animate(Look look, float dt, Vector2 c)
    {
        look.age += dt;
        look.happy -= dt;
        look.wow -= dt;
        look.squish -= dt;
        look.impactCooldown -= dt;
        look.attentionTime -= dt;
        look.flash = Mathf.Max(0f, look.flash - dt / 0.3f);
        if (!look.waitPop) look.pop = Mathf.Min(1f, look.pop + dt / 0.5f);

        look.blinkIn -= dt;
        if (look.blinkIn <= 0f)
        {
            look.blinkT = 0.16f;
            look.blinkIn = blinkInterval * (0.4f + 1.2f * Rand());
        }
        look.blinkT -= dt;

        Vector2 want = Vector2.zero;
        bool has = false;
        Vector2 target = Vector2.zero;
        if (look.attentionTime > 0f) { target = look.attention; has = true; }
        else if (watchDrop && dropper != null && Application.isPlaying) { target = dropper.DropPoint; has = true; }
        if (has)
        {
            Vector2 d = target - c;
            float len = d.magnitude;
            if (len > 1e-3f) want = Rotate(d / len, -look.frame) * (0.045f * Mathf.Clamp01(len / (2f * look.radius)));
        }
        look.look = Vector2.Lerp(look.look, want, 1f - Mathf.Exp(-dt * 8f));
    }

    int Expression(Look look)
    {
        if (look.squish > 0f) return 3;
        if (look.wow > 0f) return 2;
        if (look.happy > 0f) return 1;
        if (look.velocity.y < -fallSpeed) return 2;
        return 0;
    }

    float Blink(Look look) => look.blinkT > 0f ? Mathf.Sin(Mathf.Clamp01(1f - look.blinkT / 0.16f) * Mathf.PI) : 0f;

    // Slimes virtuels (aperçus, vitrine) : cercle qui respire doucement.
    void EmitRound(Look look, Vector2 center, float radius, float dt, float scale, float time)
    {
        look.radius = radius;
        look.center = center;
        Animate(look, dt, center);
        float r = radius * scale;
        int count = Samples(2f * r);
        EnsureSamples(count);
        float breathe = 0.035f * Mathf.Sin(time * 2.6f + look.seed);
        for (int i = 0; i < count; i++)
        {
            Vector2 u = Units(count)[i];
            _sp[i] = center + new Vector2(u.x * r * (1f + breathe), u.y * r * (1f - breathe));
        }
        EmitSlime(Resolve(look), look, center, count, 0f, 2f * r);
    }

    float _heldPopTime = 1f;

    void DrawPreviews(float dt)
    {
        if (_fusion == null) return;
        _heldPopTime += dt;
        float t = Time.time;
        int cur = dropper.CurrentLevel, next = dropper.NextLevel;
        if (cur != _heldLevel) { _held.level = cur; _heldLevel = cur; }
        if (next != _nextLevel) { _nextLook.level = next; _nextLevel = next; }
        float pop = ElasticOut(Mathf.Clamp01(_heldPopTime / 0.45f));
        Vector2 held = dropper.DropPoint + new Vector2(0f, 0.06f * Mathf.Sin(t * 2.2f));
        _held.attention = held + Vector2.down * 5f;
        _held.attentionTime = 1f;
        int left = dropper.DropsLeft;
        if (left > 0) EmitRound(_held, held, 0.5f * _fusion.LevelDiameter(cur), dt, Mathf.Lerp(0.3f, 1f, pop), t);
        _nextLook.attention = held;
        _nextLook.attentionTime = 1f;
        if (left > 1) EmitRound(_nextLook, dropper.NextPreviewPoint, dropper.NextPreviewRadius, dt, Mathf.Lerp(0.5f, 1f, pop), t + 1.3f);
    }

    // Vitrine : une ligne par niveau, puis leurs versions shiny, puis les variantes (à la taille de leur famille).
    void BuildGallery()
    {
        _gallery.Clear();
        int max = Mathf.Min(_fusion.MaxLevel, designs.Count - 1);
        for (int level = 0; level <= max; level++) _gallery.Add(new GalleryEntry { design = Design(level), level = level, newRow = level == 0 });
        if (galleryShiny)
            for (int level = 0; level <= max; level++) _gallery.Add(new GalleryEntry { design = Design(level), level = level, shiny = true, newRow = level == 0 });
        if (galleryVariants)
            for (int i = 0; i < variants.Count; i++)
                if (variants[i] != null)
                    _gallery.Add(new GalleryEntry { design = variants[i], level = Mathf.Min((int)variants[i].family, _fusion.MaxLevel), newRow = i == 0 });
        if (_gallery.Count > _galleryLooks.Length) _gallery.RemoveRange(_galleryLooks.Length, _gallery.Count - _galleryLooks.Length);
    }

    void DrawGallery(float dt)
    {
        if (_fusion == null) return;
        BuildGallery();
        float t = Time.realtimeSinceStartup;
        float x = 0f, y = 0f, row = 0f;
        for (int i = 0; i < _gallery.Count; i++)
        {
            var e = _gallery[i];
            GalleryNext(e, ref x, ref y, ref row, out var center, out float radius);
            var look = _galleryLooks[i] ??= NewLook(e.level, radius);
            look.level = e.level;
            look.design = e.design;
            look.shiny = e.shiny;
            look.happy = i % 3 == 1 ? 1f : 0f;
            EmitRound(look, center, radius, dt, 1f, t + i);
        }
    }

    void GalleryNext(GalleryEntry e, ref float x, ref float y, ref float row, out Vector2 center, out float radius)
    {
        float d = _fusion.LevelDiameter(e.level);
        if (x > 0f && (e.newRow || x + d > galleryWidth)) { x = 0f; y += row + (e.newRow ? 2f : 1.2f); row = 0f; }
        radius = 0.5f * d;
        center = galleryOrigin + new Vector2(x + radius, -y - radius);
        x += d + 0.5f;
        row = Mathf.Max(row, d);
    }

    static readonly string[] RarityNames = { "commun", "rare", "ultra rare", "spécial" };

    void OnGUI()
    {
        if (!Application.isPlaying || !galleryInPlay || _fusion == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        float x = 0f, y = 0f, row = 0f;
        for (int i = 0; i < _gallery.Count; i++)
        {
            var e = _gallery[i];
            GalleryNext(e, ref x, ref y, ref row, out var center, out float radius);
            Vector3 s = cam.WorldToScreenPoint(center + Vector2.down * (radius + 0.3f));
            string text = e.shiny ? $"{e.design.name} ✦ shiny" : $"{e.design.name} · {RarityNames[(int)e.design.rarity]}";
            GUI.Label(new Rect(s.x - 80f, Screen.height - s.y - 10f, 160f, 20f), text, _label);
        }
    }

    GUIStyle _label;

    static float ElasticOut(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * (TAU / 3f)) + 1f;
    }

    static Vector2 Rotate(Vector2 v, float a)
    {
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

    // Directions de n points régulièrement répartis sur le cercle (calculées une fois par nombre de points).
    Vector2[] Units(int n)
    {
        if (_units.TryGetValue(n, out var units)) return units;
        units = new Vector2[n];
        for (int i = 0; i < n; i++) units[i] = Dir(TAU * i / n);
        _units.Add(n, units);
        return units;
    }

    // ---- Maillage ----

    void Begin()
    {
        _vertexCount = 0;
        _indexCount = 0;
    }

    void End()
    {
        if (_mesh == null) return;
        if (_vbCapacity < _vertexCount)
        {
            _vbCapacity = _vb.Length;
            _mesh.SetVertexBufferParams(_vbCapacity, Layout);
        }
        if (_ibCapacity < _indexCount)
        {
            _ibCapacity = _ib.Length;
            _mesh.SetIndexBufferParams(_ibCapacity, IndexFormat.UInt32);
        }
        if (_vertexCount > 0) _mesh.SetVertexBufferData(_vb, 0, 0, _vertexCount, 0, Quiet);
        if (_indexCount > 0) _mesh.SetIndexBufferData(_ib, 0, 0, _indexCount, Quiet);
        _mesh.subMeshCount = 1;
        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, _indexCount) { vertexCount = _vertexCount }, Quiet);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }

    int AddVertex(Vector2 p, Vector4 uv0, Vector4 uv1, Color32 color)
    {
        if (_vertexCount == _vb.Length) System.Array.Resize(ref _vb, 2 * _vb.Length);
        ref var v = ref _vb[_vertexCount];
        v.position = new Vector3(p.x, p.y, 0f);
        v.color = color;
        v.uv0 = uv0;
        v.uv1 = uv1;
        v.uv2 = _k2;
        v.uv3 = _k3;
        v.uv4 = _k4;
        v.uv5 = _k5;
        return _vertexCount++;
    }

    void Tri(int a, int b, int c)
    {
        if (_indexCount + 3 > _ib.Length) System.Array.Resize(ref _ib, 2 * _ib.Length);
        _ib[_indexCount++] = a;
        _ib[_indexCount++] = b;
        _ib[_indexCount++] = c;
    }

    // Un slime : accessoires de derrière, gelée (éventail depuis le centre), liseré du contour, accessoires de devant.
    // _sp : points du contour, régulièrement répartis au repos (point k à l'angle 2πk/n du corps) ; 'rot' = rotation du corps.
    void EmitSlime(SlimeDesign d, Look look, Vector2 c, int n, float rot, float diameter)
    {
        if (d == null) return;
        for (int k = 0; k < n; k++)
        {
            Vector2 t = _sp[(k + 1) % n] - _sp[(k + n - 1) % n];
            Vector2 nor = new Vector2(t.y, -t.x);
            float len = nor.magnitude;
            _sn[k] = len > 1e-7f ? nor / len : Vector2.up;
        }

        float model = _fusion != null ? _fusion.LevelDiameter(0) : 1f;
        float faceScale = faceSize * Mathf.Clamp(Mathf.Pow(Mathf.Max(0.01f, diameter / model), -faceShrink), 0.3f, 1.5f);
        float aa = 1.5f * _pixel;
        _flash = look.flash;
        _k2 = new Vector4(look.look.x, look.look.y, Blink(look), Expression(look));
        _k3 = new Vector4(d.shade.r, d.shade.g, d.shade.b, (int)d.eyes + 8 * (int)d.mouth + 64 * (int)d.attitude);
        _k4 = new Vector4(d.accent.r, d.accent.g, d.accent.b, d.glow);
        _k5 = new Vector4(look.seed, faceScale, d.blush ? 1f : 0f, outlineWidth / (outlineWidth + aa));
        Color32 body = d.body;
        float surface = (int)d.pattern + 32 * (int)d.sheen; // motif + 32 × reflet
        float frame = look.frame;
        float radius = 0.5f * diameter;
        float accScale = ElasticOut(look.pop);

        EmitAccessories(d, false, n, rot, frame, radius, accScale, aa);

        // Directions au repos (contour régulier) tournées par multiplication : repère du décor (rot - frame), de la lumière (rot).
        var rest = Units(n);
        Vector2 rd = Dir(rot - frame), rl = Dir(rot);
        for (int k = 0; k < n; k++)
        {
            Vector2 u = rest[k];
            _dd[k] = new Vector2(rd.x * u.x - rd.y * u.y, rd.y * u.x + rd.x * u.y);
            _dl[k] = new Vector2(rl.x * u.x - rl.y * u.y, rl.y * u.x + rl.x * u.y);
        }
        int center = AddVertex(c, new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0f, _flash, surface), body);
        for (int k = 0; k < n; k++)
            AddVertex(_sp[k], new Vector4(_dd[k].x, _dd[k].y, 0f, 0f), new Vector4(_dl[k].x, _dl[k].y, _flash, surface), body);
        for (int k = 0; k < n; k++) Tri(center, center + 1 + k, center + 1 + (k + 1) % n);

        float w = outlineWidth, ew = _k5.w;
        int r0 = _vertexCount;
        for (int k = 0; k < n; k++)
        {
            Vector2 dd = _dd[k];
            var u1 = new Vector4(_dl[k].x, _dl[k].y, _flash, surface);
            AddVertex(_sp[k] - _sn[k] * w, new Vector4(dd.x, dd.y, 1f, 0f), u1, body);
            AddVertex(_sp[k], new Vector4(dd.x, dd.y, 1f, ew), u1, body);
            AddVertex(_sp[k] + _sn[k] * aa, new Vector4(dd.x, dd.y, 1f, 1f), u1, body);
        }
        for (int k = 0; k < n; k++)
        {
            int a = r0 + 3 * k, b = r0 + 3 * ((k + 1) % n);
            Tri(a, a + 1, b + 1); Tri(a, b + 1, b);
            Tri(a + 1, a + 2, b + 2); Tri(a + 1, b + 2, b + 1);
        }

        EmitAccessories(d, true, n, rot, frame, radius, accScale, aa);
    }

    // ---- Accessoires (coordonnées locales en rayons du slime : x le long du bord, y vers l'extérieur, 0 = surface) ----

    void Anchor(float thetaDeg, int n, float rot, float frame, out Vector2 pos, out Vector2 nor)
    {
        float phi = thetaDeg * Mathf.Deg2Rad - rot + frame;
        float f = Mathf.Repeat(phi / TAU, 1f) * n;
        int k0 = Mathf.FloorToInt(f) % n, k1 = (k0 + 1) % n;
        float u = f - Mathf.Floor(f);
        pos = Vector2.Lerp(_sp[k0], _sp[k1], u);
        nor = Vector2.Lerp(_sn[k0], _sn[k1], u).normalized;
    }

    void EmitAccessories(SlimeDesign d, bool front, int n, float rot, float frame, float radius, float scale, float aa)
    {
        if (scale <= 0.01f) return;
        float size = radius * scale;
        if (!front) EmitBack(d, n, rot, frame, size, aa);
        Color col = d.headColor;
        switch (d.head)
        {
            case SlimeHead.Leaf:
                if (front) return;
                {
                    Anchor(100f, n, rot, frame, out var p, out var nor);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.04f, -0.15f)); _poly.Add(new Vector2(0.04f, -0.15f));
                    _poly.Add(new Vector2(0.045f, 0.26f)); _poly.Add(new Vector2(-0.025f, 0.26f));
                    Polygon(p, nor, size, new Vector2(0f, 0f), col * 0.75f, 0f, aa);
                    float sway = 0.08f * Mathf.Sin(Time.time * 2f);
                    Leaf(p, nor, size, new Vector2(0.01f, 0.24f), -0.75f + sway, 0.62f, 0.2f, col, aa);
                    Leaf(p, nor, size, new Vector2(0f, 0.2f), 0.85f + sway, 0.42f, 0.15f, col * 0.9f, aa);
                }
                break;
            case SlimeHead.Antennae:
                if (front) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 22f * s, n, rot, frame, out var p, out var nor);
                    float wob = 0.03f * Mathf.Sin(Time.time * 3f + s);
                    Vector2 tip = new Vector2((0.07f + wob) * s, 0.36f);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.022f, -0.12f)); _poly.Add(new Vector2(0.022f, -0.12f));
                    _poly.Add(tip + new Vector2(0.016f, 0f)); _poly.Add(tip + new Vector2(-0.016f, 0f));
                    Polygon(p, nor, size, new Vector2(0f, 0.1f), col, 0f, aa);
                    Circle(tip, 0.075f, 12);
                    Polygon(p, nor, size, tip, col * 1.15f, d.glow, aa);
                }
                break;
            case SlimeHead.Ears:
                if (front) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 36f * s, n, rot, frame, out var p, out var nor);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.21f, -0.14f)); _poly.Add(new Vector2(0.21f, -0.14f));
                    _poly.Add(new Vector2(0.13f + 0.03f * s, 0.16f)); _poly.Add(new Vector2(0.04f * s, 0.34f));
                    _poly.Add(new Vector2(-0.12f + 0.03f * s, 0.17f));
                    Polygon(p, nor, size, new Vector2(0f, 0f), col, 0f, aa);
                    for (int i = 0; i < _poly.Count; i++) _poly[i] = new Vector2(_poly[i].x * 0.5f, (_poly[i].y + 0.05f) * 0.55f);
                    Polygon(p, nor, size, new Vector2(0f, 0.02f), new Color(1f, 0.62f, 0.68f), 0f, aa);
                }
                break;
            case SlimeHead.Horns:
                if (front) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 28f * s, n, rot, frame, out var p, out var nor);
                    _poly.Clear();
                    for (int i = 0; i <= 6; i++) { float u = i / 6f; _poly.Add(new Vector2(0.11f * s * u * u + 0.1f * (1f - u) + 0.008f, 0.42f * u - 0.12f)); }
                    for (int i = 6; i >= 0; i--) { float u = i / 6f; _poly.Add(new Vector2(0.11f * s * u * u - 0.1f * (1f - u) - 0.008f, 0.42f * u - 0.12f)); }
                    Polygon(p, nor, size, new Vector2(0.02f * s, 0.02f), col, d.glow * 0.6f, aa);
                }
                break;
            case SlimeHead.Bow:
                if (!front) return;
                {
                    Anchor(62f, n, rot, frame, out var p, out var nor);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        _poly.Clear();
                        _poly.Add(new Vector2(0.03f * s, 0.03f)); _poly.Add(new Vector2(0.30f * s, 0.20f));
                        _poly.Add(new Vector2(0.36f * s, 0.05f)); _poly.Add(new Vector2(0.31f * s, -0.12f));
                        _poly.Add(new Vector2(0.03f * s, -0.03f));
                        Polygon(p, nor, size, new Vector2(0.18f * s, 0.02f), col, 0f, aa);
                    }
                    Circle(new Vector2(0f, 0f), 0.075f, 12);
                    Polygon(p, nor, size, Vector2.zero, col * 0.85f, 0f, aa);
                }
                break;
            case SlimeHead.Crystals:
                if (front) return;
                for (int i = 0; i < 3; i++)
                {
                    float theta = 92f + (i - 1) * 21f;
                    float len = i == 1 ? 0.66f : (i == 0 ? 0.45f : 0.4f), w = i == 1 ? 0.1f : 0.08f;
                    Anchor(theta, n, rot, frame, out var p, out var nor);
                    float tilt = (theta - 92f) * 0.6f * Mathf.Deg2Rad;
                    _poly.Clear();
                    _poly.Add(new Vector2(-w, -0.15f)); _poly.Add(new Vector2(w, -0.15f)); _poly.Add(new Vector2(w * 1.1f, len * 0.62f));
                    _poly.Add(new Vector2(0f, len)); _poly.Add(new Vector2(-w * 1.1f, len * 0.62f));
                    for (int j = 0; j < _poly.Count; j++) _poly[j] = Rotate(_poly[j] + new Vector2(0f, 0.15f), tilt) - new Vector2(0f, 0.15f);
                    Polygon(p, nor, size, Rotate(new Vector2(0f, len * 0.3f + 0.15f), tilt) - new Vector2(0f, 0.15f), col, d.glow + 0.5f, aa);
                }
                break;
            case SlimeHead.Halo:
                if (!front) return;
                {
                    Anchor(90f, n, rot, frame, out var p, out var nor);
                    Vector2 tan = new Vector2(nor.y, -nor.x);
                    Vector2 hc = p + nor * (size * (0.32f + 0.04f * Mathf.Sin(Time.time * 2f)));
                    float ax = 0.44f * size, ay = 0.12f * size, th = 0.07f * size;
                    const int N = 28;
                    var u1 = new Vector4(0f, 0f, _flash, 1.5f);
                    int h0 = _vertexCount;
                    for (int i = 0; i < N; i++)
                    {
                        float a = TAU * i / N;
                        Vector2 e = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        AddVertex(hc + tan * (e.x * (ax - th)) + nor * (e.y * (ay - th)), new Vector4(0f, 0f, 3f, 0f), u1, col);
                        AddVertex(hc + tan * (e.x * (ax + th)) + nor * (e.y * (ay + th)), new Vector4(0f, 0f, 3f, 1f), u1, col);
                    }
                    for (int i = 0; i < N; i++)
                    {
                        int a = h0 + 2 * i, b = h0 + 2 * ((i + 1) % N);
                        Tri(a, a + 1, b + 1); Tri(a, b + 1, b);
                    }
                }
                break;
            case SlimeHead.Crown:
                if (!front) return;
                {
                    Anchor(90f, n, rot, frame, out var p, out var nor);
                    p -= nor * (0.06f * size);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.34f, -0.08f)); _poly.Add(new Vector2(0.34f, -0.08f)); _poly.Add(new Vector2(0.36f, 0.08f));
                    _poly.Add(new Vector2(0.40f, 0.42f)); _poly.Add(new Vector2(0.18f, 0.18f)); _poly.Add(new Vector2(0f, 0.48f));
                    _poly.Add(new Vector2(-0.18f, 0.18f)); _poly.Add(new Vector2(-0.40f, 0.42f)); _poly.Add(new Vector2(-0.36f, 0.08f));
                    Polygon(p, nor, size, new Vector2(0f, -0.04f), col, d.glow, aa);
                    Gem(p, nor, size, new Vector2(0f, 0.01f), 0.06f, new Color(0.95f, 0.15f, 0.32f), aa);
                    Gem(p, nor, size, new Vector2(-0.21f, 0f), 0.045f, new Color(0.2f, 0.55f, 1f), aa);
                    Gem(p, nor, size, new Vector2(0.21f, 0f), 0.045f, new Color(0.2f, 0.55f, 1f), aa);
                    Gem(p, nor, size, new Vector2(-0.40f, 0.42f), 0.035f, Color.white, aa);
                    Gem(p, nor, size, new Vector2(0f, 0.48f), 0.04f, Color.white, aa);
                    Gem(p, nor, size, new Vector2(0.40f, 0.42f), 0.035f, Color.white, aa);
                }
                break;
            case SlimeHead.Flower:
                if (front) return;
                {
                    Anchor(96f, n, rot, frame, out var p, out var nor);
                    float sway = 0.06f * Mathf.Sin(Time.time * 1.8f);
                    Vector2 top = new Vector2(0.03f + sway * 0.5f, 0.42f);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.035f, -0.15f)); _poly.Add(new Vector2(0.035f, -0.15f));
                    _poly.Add(top + new Vector2(0.02f, 0f)); _poly.Add(top + new Vector2(-0.02f, 0f));
                    Polygon(p, nor, size, new Vector2(0f, 0.1f), StemGreen * 0.8f, 0f, aa);
                    Leaf(p, nor, size, new Vector2(0.01f, 0.14f), -0.95f + sway, 0.36f, 0.12f, StemGreen, aa);
                    Leaf(p, nor, size, new Vector2(0f, 0.2f), 1.0f + sway, 0.3f, 0.1f, StemGreen * 0.9f, aa);
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 pc = top + Dir(TAU * i / 5f + Mathf.PI * 0.5f + sway) * 0.1f;
                        Circle(pc, 0.085f, 12);
                        Polygon(p, nor, size, pc, col, 0f, aa);
                    }
                    Circle(top, 0.06f, 12);
                    Polygon(p, nor, size, top, FlowerHeart, 0.3f, aa);
                }
                break;
            case SlimeHead.RoundEars:
                if (front) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 40f * s, n, rot, frame, out var p, out var nor);
                    Circle(new Vector2(0f, 0.06f), 0.2f, 18);
                    Polygon(p, nor, size, new Vector2(0f, 0.06f), col, 0f, aa);
                    Circle(new Vector2(0f, 0.07f), 0.11f, 14);
                    Polygon(p, nor, size, new Vector2(0f, 0.07f), Color.Lerp(col, EarPink, 0.55f), 0f, aa);
                }
                break;
            case SlimeHead.Cherry:
                if (front) return;
                {
                    Anchor(84f, n, rot, frame, out var p, out var nor);
                    float sway = 0.04f * Mathf.Sin(Time.time * 2.2f);
                    _poly.Clear();
                    _poly.Add(new Vector2(-0.015f, 0.18f)); _poly.Add(new Vector2(0.015f, 0.18f));
                    _poly.Add(new Vector2(0.11f + sway, 0.5f)); _poly.Add(new Vector2(0.08f + sway, 0.51f));
                    Polygon(p, nor, size, new Vector2(0.05f, 0.34f), CherryStem, 0f, aa);
                    Circle(new Vector2(0f, 0.14f), 0.16f, 18);
                    Polygon(p, nor, size, new Vector2(0f, 0.14f), col, 0f, aa);
                    Circle(new Vector2(-0.06f, 0.2f), 0.04f, 10);
                    Polygon(p, nor, size, new Vector2(-0.06f, 0.2f), Color.white, 0.4f, aa);
                }
                break;
        }
    }

    // Case « dos » : derrière le corps, de part et d'autre (s = −1 gauche, +1 droite). Dans le repère local d'un point du bord,
    // « vers le haut du slime » vaut −s le long de la tangente.
    void EmitBack(SlimeDesign d, int n, float rot, float frame, float size, float aa)
    {
        Color col = d.backColor;
        switch (d.back)
        {
            case SlimeBack.BatWings:
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 62f * s, n, rot, frame, out var p, out var nor);
                    float flap = 0.16f * Mathf.Sin(Time.time * 5f) * s;
                    _poly.Clear();
                    for (int i = 0; i < BatWing.Length; i++)
                    {
                        Vector2 q = Rotate(BatWing[i] - new Vector2(0f, -0.05f), flap) + new Vector2(0f, -0.05f);
                        _poly.Add(new Vector2(-s * q.x, q.y));
                    }
                    Vector2 f = Rotate(new Vector2(0.12f, 0.15f) - new Vector2(0f, -0.05f), flap) + new Vector2(0f, -0.05f);
                    Polygon(p, nor, size, new Vector2(-s * f.x, f.y), col, d.glow * 0.15f, aa);
                }
                break;
            case SlimeBack.FairyWings:
                for (int s = -1; s <= 1; s += 2)
                {
                    Anchor(90f - 58f * s, n, rot, frame, out var p, out var nor);
                    float flap = 0.14f * Mathf.Sin(Time.time * 7f);
                    Leaf(p, nor, size, new Vector2(0f, -0.08f), s * (0.55f + flap), 0.62f, 0.2f, col, aa);
                    Leaf(p, nor, size, new Vector2(0f, -0.1f), s * (-0.35f - flap * 0.6f), 0.42f, 0.14f, col * 0.92f, aa);
                }
                break;
        }
    }

    // Aile de chauve-souris (x = vers le haut du slime, y = vers l'extérieur), bord inférieur festonné.
    static readonly Vector2[] BatWing =
    {
        new Vector2(-0.10f, -0.14f), new Vector2(0.20f, -0.14f), new Vector2(0.46f, 0.28f), new Vector2(0.44f, 0.64f),
        new Vector2(0.27f, 0.48f), new Vector2(0.17f, 0.62f), new Vector2(0.05f, 0.42f), new Vector2(-0.06f, 0.50f),
        new Vector2(-0.12f, 0.22f)
    };

    static readonly Color StemGreen = SlimeDesign.Hex("#4FA83A"), FlowerHeart = SlimeDesign.Hex("#FFD23F"),
        EarPink = SlimeDesign.Hex("#FFB3C1"), CherryStem = SlimeDesign.Hex("#5E7A2A");

    // Feuille : base 'b', inclinaison 'tilt' (rad, depuis la verticale), longueur et demi-largeur en rayons du slime.
    void Leaf(Vector2 p, Vector2 nor, float size, Vector2 b, float tilt, float length, float width, Color col, float aa)
    {
        Vector2 axis = Rotate(Vector2.up, tilt), side = new Vector2(axis.y, -axis.x);
        _poly.Clear();
        for (int i = 0; i <= 10; i++) { float s = i / 10f; _poly.Add(b + axis * (length * s) + side * (width * Mathf.Pow(Mathf.Sin(Mathf.PI * s), 0.8f))); }
        for (int i = 9; i >= 1; i--) { float s = i / 10f; _poly.Add(b + axis * (length * s) - side * (width * Mathf.Pow(Mathf.Sin(Mathf.PI * s), 0.8f))); }
        Polygon(p, nor, size, b + axis * (0.5f * length), col, 0f, aa);
    }

    void Gem(Vector2 p, Vector2 nor, float size, Vector2 c, float r, Color col, float aa)
    {
        Circle(c, r, 10);
        Polygon(p, nor, size, c, col, 1f, aa);
    }

    void Circle(Vector2 c, float r, int count)
    {
        _poly.Clear();
        for (int i = 0; i < count; i++)
        {
            float a = TAU * i / count;
            _poly.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }

    // Polygone _poly (étoilé autour de 'fan') posé au point p, normale 'nor', échelle 'size' (m par unité) : éventail + 1,5 px
    // d'anticrénelage. Dégradé du pied (sombre) à la pointe (claire).
    void Polygon(Vector2 p, Vector2 nor, float size, Vector2 fan, Color col, float glow, float aa)
    {
        int count = _poly.Count;
        if (count < 3) return;
        Vector2 tan = new Vector2(nor.y, -nor.x);
        float minY = float.MaxValue, maxY = float.MinValue, maxX = 1e-4f, area = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = _poly[i], b = _poly[(i + 1) % count];
            minY = Mathf.Min(minY, a.y); maxY = Mathf.Max(maxY, a.y);
            maxX = Mathf.Max(maxX, Mathf.Abs(a.x - fan.x));
            area += a.x * b.y - a.y * b.x;
        }
        float sign = area >= 0f ? 1f : -1f, spanY = Mathf.Max(1e-4f, maxY - minY);

        int c0 = AddVertex(p + (tan * fan.x + nor * fan.y) * size, new Vector4(0f, 0f, 2f, 0f),
            new Vector4(0f, (fan.y - minY) / spanY, _flash, glow), Shade(col, (fan.y - minY) / spanY));
        for (int i = 0; i < count; i++)
        {
            Vector2 l = _poly[i];
            float ly = (l.y - minY) / spanY;
            AddVertex(p + (tan * l.x + nor * l.y) * size, new Vector4(0f, 0f, 2f, 1f),
                new Vector4((l.x - fan.x) / maxX, ly, _flash, glow), Shade(col, ly));
        }
        for (int i = 0; i < count; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % count);

        int f0 = _vertexCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 prev = _poly[(i + count - 1) % count], cur = _poly[i], next = _poly[(i + 1) % count];
            Vector2 e1 = cur - prev, e2 = next - cur;
            Vector2 n1 = new Vector2(e1.y, -e1.x).normalized, n2 = new Vector2(e2.y, -e2.x).normalized;
            Vector2 nl = (n1 + n2).normalized * sign;
            Vector2 world = p + (tan * cur.x + nor * cur.y) * size;
            Vector2 nw = (tan * nl.x + nor * nl.y).normalized;
            float ly = (cur.y - minY) / spanY;
            AddVertex(world + nw * aa, new Vector4(0f, 0f, 2f, 2f), new Vector4((cur.x - fan.x) / maxX, ly, _flash, glow), Shade(col, ly));
        }
        for (int i = 0; i < count; i++)
        {
            int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % count, fa = f0 + i, fb = f0 + (i + 1) % count;
            Tri(a, fa, fb); Tri(a, fb, b);
        }
    }

    static Color32 Shade(Color c, float y)
    {
        float k = Mathf.Lerp(0.82f, 1.12f, y);
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }

    // ---- Rendu ----

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_material == null || _mesh == null) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        if (!Application.isPlaying && _builtFrame != Time.frameCount)
        {
            _builtFrame = Time.frameCount;
            Prepare();
            UpdatePixel();
            Begin();
            if (gallery && _fusion != null) DrawGallery(0.02f);
            End();
        }
        if (_indexCount == 0) return;
        _material.SetVector(RimId, new Vector4(rimWidth, rimOpacity, rimLighten, 0f));
        var rp = new RenderParams(_material)
        {
            camera = cam,
            layer = gameObject.layer,
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f)
        };
        Graphics.RenderMesh(rp, _mesh, 0, renderMatrix);
    }
}
