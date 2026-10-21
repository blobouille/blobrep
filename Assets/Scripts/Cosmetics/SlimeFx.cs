using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Effets à l'écran : à chaque fusion, gouttes de gelée projetées, onde lumineuse, étincelles et éclair ; plus la chaîne est longue
// et le slime gros, plus c'est fort (tremblement de l'écran, ralenti, grosse annonce « ×4 Gluant ! », pulsation du fond,
// confettis pour un nouveau record). Éclaboussures aux chocs (SlimeSkin.Impact), pointillés de visée sous le slime à lâcher.
// Purement visuel : événements de la simulation mis en file, tout le reste dans LateUpdate ; rien pendant le rejeu accéléré.
// Le ralenti change Time.timeScale (vitesse du temps réel seulement : les pas de simulation restent identiques).
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(950)]
public class SlimeFx : MonoBehaviour
{
    [Header("Liens")]
    [Tooltip("Vide = ceux de la scène.")]
    public BlobDropper dropper;
    public SlimeSkin skin;
    public SlimeBackdrop backdrop;
    public Shader shader;

    [Header("Fusion")]
    [Tooltip("Gouttes de gelée projetées par une fusion (+ 2 par niveau, + 4 par maillon de chaîne).")]
    [Min(0)] public int droplets = 10;
    [Tooltip("Vitesse des gouttes, en km/h.")]
    [SpeedKmh] [Min(0f)] public float dropletSpeed = 5f;
    [Tooltip("Étincelles d'une fusion (+ 1 par niveau, + 3 par maillon de chaîne).")]
    [Min(0)] public int sparkles = 5;
    [Tooltip("Taille de l'onde lumineuse, en rayons du nouveau slime.")]
    [Min(0f)] public float ringSize = 2.6f;

    [Header("Chaînes et gros slimes")]
    [Tooltip("Tremblement max de l'écran, en m.")]
    [Min(0f)] public float shake = 0.35f;
    [Tooltip("Ralenti à partir de ce maillon de chaîne (0 = jamais).")]
    [Min(0)] public int slowMotionCombo = 4;
    [Tooltip("Ralenti aussi à la création de ce niveau ou plus (0 = jamais).")]
    [Min(0)] public int slowMotionLevel = 9;
    [Tooltip("Vitesse du temps au plus fort du ralenti (1 = pas de ralenti).")]
    [Range(0.05f, 1f)] public float slowMotionScale = 0.35f;
    [Tooltip("Durée (s, temps réel) du ralenti, retour progressif compris.")]
    [Min(0f)] public float slowMotionTime = 0.7f;
    [Tooltip("Annonce au centre de l'écran à partir de ce maillon de chaîne (0 = jamais).")]
    [Min(0)] public int bannerCombo = 2;
    [Tooltip("Confettis : nouveau plus gros niveau de la partie à partir de ce niveau, et niveau max (0 = jamais).")]
    [Min(0)] public int confettiLevel = 5;

    [Header("Chocs")]
    [Tooltip("Vitesse de choc (km/h) à partir de laquelle un slime éclabousse.")]
    [SpeedKmh] [Min(0f)] public float splashSpeed = 4f;

    [Header("Visée")]
    [Tooltip("Pointillés sous le slime à lâcher.")]
    public bool aimDots = true;
    [Tooltip("Longueur des pointillés, en m.")]
    [Min(0f)] public float aimLength = 14f;

    struct Particle
    {
        public Vector2 position, velocity;
        public float size, endSize, life, age, rotation, spin, gravity, drag, param, additive;
        public Vector4 color;
        public int type;
    }

    struct Banner
    {
        public string big, small;
        public Color color;
        public float start, power;
    }

    BlobFusion _fusion;
    SlimeScore _score;
    Material _material;
    Mesh _mesh;
    readonly List<Particle> _particles = new List<Particle>();
    readonly List<SlimeScore.Hit> _hits = new List<SlimeScore.Hit>();
    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector4> _uv0 = new List<Vector4>(), _uv1 = new List<Vector4>(), _uv2 = new List<Vector4>();
    readonly List<int> _tris = new List<int>();
    System.Random _rand = new System.Random(11);
    Banner _banner;
    bool _hasBanner;
    float _trauma, _flash, _slowTimer = -1f;
    Color _flashColor;
    Vector3 _shakeOffset;
    Transform _shaken;
    bool _ownsTimeScale;
    GUIStyle _bigStyle, _smallStyle;

    static readonly string[] Words = { "", "", "Joli !", "Super !", "Gluant !", "Énorme !", "Slimetastique !", "INARRÊTABLE !" };

    void OnEnable()
    {
        Prepare();
        _mesh = new Mesh { name = "Effets slimes", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
        _mesh.MarkDynamic();
        if (shader == null) shader = Shader.Find("Slime/Fx");
        if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3020 };
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        if (!Application.isPlaying) return;
        if (_score != null) _score.Scored += OnScored;
        if (skin != null) skin.Impact += OnImpact;
        LevelRestart.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (_score != null) _score.Scored -= OnScored;
        if (skin != null) skin.Impact -= OnImpact;
        LevelRestart.Restarted -= OnRestarted;
        RestoreCamera();
        if (_ownsTimeScale) Time.timeScale = 1f;
        _ownsTimeScale = false;
        _particles.Clear();
        _hits.Clear();
        if (_mesh != null) DestroyImmediate(_mesh);
        if (_material != null) DestroyImmediate(_material);
    }

    void Prepare()
    {
        if (dropper == null) dropper = FindAnyObjectByType<BlobDropper>();
        if (skin == null) skin = FindAnyObjectByType<SlimeSkin>();
        if (backdrop == null) backdrop = FindAnyObjectByType<SlimeBackdrop>();
        if (dropper != null)
        {
            if (_fusion == null) _fusion = dropper.GetComponent<BlobFusion>();
            if (_score == null) _score = dropper.GetComponent<SlimeScore>();
        }
    }

    void OnRestarted()
    {
        _particles.Clear();
        _hits.Clear();
        _hasBanner = false;
        _trauma = 0f;
    }

    void OnScored(SlimeScore.Hit hit)
    {
        if (!LevelSimulation.FastForwarding) _hits.Add(hit);
    }

    float Rand() => (float)_rand.NextDouble();
    float Rand(float a, float b) => a + (b - a) * Rand();

    void Emit(int type, Vector2 position, Vector2 velocity, float size, float endSize, float life, Vector4 color, float additive,
        float gravity = 0f, float drag = 0f, float param = 0f, float spin = 0f, float delay = 0f)
    {
        if (_particles.Count >= 2500) return;
        _particles.Add(new Particle
        {
            type = type, position = position, velocity = velocity, size = size, endSize = endSize, life = life, age = -delay,
            color = color, additive = additive, gravity = gravity, drag = drag, param = param, spin = spin, rotation = Rand(0f, Mathf.PI * 2f)
        });
    }

    static Vector4 Hdr(Color c, float k) => new Vector4(c.r * k, c.g * k, c.b * k, 1f);

    void Merge(SlimeScore.Hit hit)
    {
        var design = skin != null ? skin.Design(hit.level) : null;
        Color body = design != null ? design.body : (hit.blob != null ? hit.blob.debugColor : Color.white);
        Color accent = design != null ? design.accent : Color.white;
        float radius = _fusion != null ? 0.5f * _fusion.LevelDiameter(hit.level) : 1f;
        int combo = Mathf.Max(1, hit.combo);
        float power = 1f + 0.12f * hit.level + 0.35f * (combo - 1);
        Vector2 p = hit.position;

        Emit(4, p, Vector2.zero, 1.1f * radius, 2f * radius * Mathf.Min(power, 1.8f), 0.3f, Hdr(Color.Lerp(body, Color.white, 0.4f), 1.1f), 1f);
        Emit(2, p, Vector2.zero, 0.8f * radius, ringSize * radius * (1f + 0.15f * (combo - 1)), 0.5f, Hdr(Color.Lerp(body, Color.white, 0.35f), 2.5f), 1f, param: 0.2f);
        if (combo >= 3 || hit.level >= 6)
            Emit(2, p, Vector2.zero, 0.5f * radius, ringSize * 1.5f * radius, 0.7f, Hdr(accent, 2f), 1f, param: 0.1f, delay: 0.1f);

        int drops = Mathf.Min(80, droplets + 2 * hit.level + 4 * (combo - 1));
        float speed = dropletSpeed * Mathf.Sqrt(radius / 0.5f) * (1f + 0.15f * (combo - 1));
        for (int i = 0; i < drops; i++)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 v = dir * speed * Rand(0.5f, 1.2f) + Vector2.up * Rand(1f, 3f);
            float size = Rand(0.05f, 0.13f) * Mathf.Sqrt(radius / 0.5f);
            Color c = Color.Lerp(body, Color.white, Rand(0f, 0.25f));
            Emit(0, p + dir * radius * Rand(0.3f, 0.9f), v, size, 0f, Rand(0.6f, 1.1f), new Vector4(c.r, c.g, c.b, 1f), 0f, gravity: 14f, drag: 0.6f);
        }

        int stars = Mathf.Min(60, sparkles + hit.level + 3 * (combo - 1));
        for (int i = 0; i < stars; i++)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Color c = Color.Lerp(accent, Color.white, 0.5f);
            Emit(1, p + dir * radius * Rand(0.2f, 1f), dir * Rand(1.5f, 4.5f) * (1f + 0.1f * hit.level), Rand(0.18f, 0.4f) * (1f + 0.05f * hit.level), 0f,
                Rand(0.4f, 0.8f), Hdr(c, Rand(2f, 3.5f)), 1f, gravity: -1f, drag: 2.5f, spin: Rand(-6f, 6f));
        }

        _trauma = Mathf.Min(1f, _trauma + 0.12f + 0.05f * hit.level + 0.12f * (combo - 1));
        if (backdrop != null) backdrop.Pulse(body, Mathf.Min(0.9f, 0.18f + 0.08f * hit.level + 0.12f * (combo - 1)));
        if (hit.level >= 8 || combo >= 5) { _flash = Mathf.Max(_flash, 0.35f); _flashColor = body; }

        if (bannerCombo > 0 && combo >= bannerCombo)
        {
            _banner = new Banner
            {
                big = "×" + combo,
                small = Words[Mathf.Min(combo, Words.Length - 1)],
                color = Color.Lerp(body, Color.white, 0.15f),
                start = Time.unscaledTime,
                power = Mathf.Min(1.6f, 0.8f + 0.12f * combo)
            };
            _hasBanner = true;
        }

        bool slow = (slowMotionCombo > 0 && combo >= slowMotionCombo) || (slowMotionLevel > 0 && hit.level >= slowMotionLevel);
        if (slow && slowMotionScale < 1f && slowMotionTime > 0f && (_ownsTimeScale || Mathf.Approximately(Time.timeScale, 1f))) _slowTimer = 0f;

        int maxLevel = _fusion != null ? _fusion.MaxLevel : 99;
        if (confettiLevel > 0 && ((hit.record && hit.level >= confettiLevel) || hit.level >= maxLevel)) Confetti(hit.level >= maxLevel ? 160 : 90);
    }

    void Confetti(int count)
    {
        var cam = Camera.main;
        if (cam == null || skin == null) return;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        for (int i = 0; i < count; i++)
        {
            var d = skin.Design(_rand.Next(0, skin.designs.Count));
            Color col = d != null ? Color.Lerp(d.body, Color.white, 0.1f) : Color.white;
            Vector2 p = c + new Vector2(Rand(-w, w), h + Rand(0.2f, 3f));
            Emit(3, p, new Vector2(Rand(-2f, 2f), Rand(-6f, -1f)), Rand(0.12f, 0.2f), Rand(0.1f, 0.18f), Rand(2.2f, 3.5f),
                new Vector4(col.r * 1.2f, col.g * 1.2f, col.b * 1.2f, 1f), 0f, gravity: 4f, drag: 1.2f, spin: Rand(-10f, 10f), delay: Rand(0f, 0.4f));
        }
    }

    void OnImpact(BLOB blob, Vector2 point, Vector2 normal, float speed)
    {
        if (speed < splashSpeed || skin == null || _fusion == null) return;
        var design = skin.Design(_fusion.LevelOf(blob));
        Color body = design != null ? design.body : blob.debugColor;
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        float radius = 0.5f * blob.diameter;
        int count = Mathf.Clamp(Mathf.RoundToInt((speed - splashSpeed) * 1.5f) + 3, 3, 14);
        for (int i = 0; i < count; i++)
        {
            float side = Rand(-1f, 1f);
            Vector2 v = (tangent * side * 1.3f + normal * Rand(0.4f, 1f)) * Mathf.Min(speed * 0.45f, 6f) * Rand(0.6f, 1.1f);
            Emit(0, point + tangent * side * radius * 0.4f, v, Rand(0.04f, 0.09f) * Mathf.Sqrt(radius / 0.5f), 0f, Rand(0.35f, 0.6f),
                new Vector4(body.r, body.g, body.b, 1f), 0f, gravity: 14f, drag: 0.8f);
        }
        _trauma = Mathf.Min(1f, _trauma + Mathf.Min(0.15f, 0.01f * speed * radius));
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        Prepare();
        for (int i = 0; i < _hits.Count; i++) Merge(_hits[i]);
        _hits.Clear();

        float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.age += dt;
            if (p.age >= p.life) { _particles.RemoveAt(i); continue; }
            if (p.age >= 0f)
            {
                p.velocity += Vector2.down * (p.gravity * dt);
                p.velocity *= Mathf.Exp(-p.drag * dt);
                p.position += p.velocity * dt;
                p.rotation += p.spin * dt;
            }
            _particles[i] = p;
        }

        UpdateSlowMotion(udt);
        UpdateShake(udt);
        _flash = Mathf.Max(0f, _flash - udt * 2.5f);
        Build();
    }

    void UpdateSlowMotion(float udt)
    {
        if (_slowTimer < 0f) return;
        _slowTimer += udt;
        float u = _slowTimer / slowMotionTime;
        if (u >= 1f)
        {
            Time.timeScale = 1f;
            _slowTimer = -1f;
            _ownsTimeScale = false;
            return;
        }
        float k = u < 0.15f ? u / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.15f) / 0.85f);
        Time.timeScale = Mathf.Lerp(1f, slowMotionScale, k);
        _ownsTimeScale = true;
    }

    void UpdateShake(float udt)
    {
        RestoreCamera();
        _trauma = Mathf.Max(0f, _trauma - udt * 1.8f);
        var cam = Camera.main;
        if (cam == null || _trauma <= 0f || shake <= 0f) return;
        float t = Time.unscaledTime * 28f, amount = shake * _trauma * _trauma;
        _shakeOffset = new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f, 0f) * amount;
        _shaken = cam.transform;
        _shaken.position += _shakeOffset;
    }

    void RestoreCamera()
    {
        if (_shaken != null) _shaken.position -= _shakeOffset;
        _shaken = null;
        _shakeOffset = Vector3.zero;
    }

    void Build()
    {
        _verts.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _tris.Clear();

        if (aimDots && dropper != null && skin != null && _fusion != null)
        {
            var d = skin.Design(dropper.CurrentLevel);
            Color c = d != null ? d.body : Color.white;
            float r = 0.5f * _fusion.LevelDiameter(dropper.CurrentLevel);
            Vector2 start = dropper.DropPoint + Vector2.down * (r + 0.2f);
            float spacing = 0.4f, offset = Mathf.Repeat(Time.time * 0.8f, spacing);
            for (float y = offset; y < aimLength; y += spacing)
            {
                float fade = 1f - y / aimLength;
                Quad(6, start + Vector2.down * y, 0.05f, 0f, new Vector4(c.r, c.g, c.b, 0.55f * fade), 0.3f, 0f);
            }
        }

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            if (p.age < 0f) continue;
            float u = p.age / p.life;
            float size = p.type == 0 ? p.size * (1f - u * u) : Mathf.Lerp(p.size, p.endSize, p.type == 2 || p.type == 4 ? 1f - (1f - u) * (1f - u) : u);
            Vector4 col = p.color;
            col.w *= p.type == 0 || p.type == 3 ? Mathf.Clamp01((1f - u) * 4f) : (p.type == 1 ? Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.4f + 0.05f)) : 1f - u);
            float param = p.type == 2 ? Mathf.Lerp(p.param, p.param * 0.2f, u) : p.type == 3 ? 0.5f + 0.5f * Mathf.Sin(p.rotation * 2f) : p.param;
            Quad(p.type, p.position, size, p.rotation, col, p.additive, param, p.type == 3 ? Mathf.Abs(Mathf.Cos(p.rotation * 1.3f)) : 1f);
        }

        _mesh.Clear();
        if (_verts.Count == 0) return;
        _mesh.SetVertices(_verts);
        _mesh.SetUVs(0, _uv0);
        _mesh.SetUVs(1, _uv1);
        _mesh.SetUVs(2, _uv2);
        _mesh.SetIndices(_tris, MeshTopology.Triangles, 0, false);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }

    // Quadrilatère centré, tourné de 'rotation' ; 'squash' écrase la hauteur (confetti qui tourne sur lui-même).
    void Quad(int type, Vector2 center, float size, float rotation, Vector4 color, float additive, float param, float squash = 1f)
    {
        float c = Mathf.Cos(rotation) * size, s = Mathf.Sin(rotation) * size;
        Vector2 ax = new Vector2(c, s), ay = new Vector2(-s, c) * squash;
        int v = _verts.Count;
        for (int k = 0; k < 4; k++)
        {
            float x = k == 0 || k == 3 ? -1f : 1f, y = k < 2 ? -1f : 1f;
            Vector2 p = center + ax * x + ay * y;
            _verts.Add(new Vector3(p.x, p.y, 0f));
            _uv0.Add(new Vector4(x, y, type, param));
            _uv1.Add(color);
            _uv2.Add(new Vector4(additive, 0f, 0f, 0f));
        }
        _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 2);
        _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 3);
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_material == null || _mesh == null || _mesh.vertexCount == 0) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        var rp = new RenderParams(_material) { camera = cam, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f) };
        Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (_flash > 0f)
        {
            GUI.color = new Color(Mathf.Lerp(_flashColor.r, 1f, 0.6f), Mathf.Lerp(_flashColor.g, 1f, 0.6f), Mathf.Lerp(_flashColor.b, 1f, 0.6f), _flash);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (!_hasBanner) return;
        float t = Time.unscaledTime - _banner.start;
        const float Life = 1.4f;
        if (t > Life) { _hasBanner = false; return; }

        float pop = t < 0.12f ? Mathf.Lerp(0.2f, 1.3f, t / 0.12f) : 1f + 0.3f * Mathf.Exp(-(t - 0.12f) * 7f) * Mathf.Cos((t - 0.12f) * 22f);
        float alpha = t > Life - 0.3f ? (Life - t) / 0.3f : 1f;
        float wobble = 9f * Mathf.Sin(t * 18f) * Mathf.Exp(-t * 4f);
        float lift = 30f * t;
        float h = Screen.height;
        _bigStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _smallStyle ??= new GUIStyle(_bigStyle);
        _bigStyle.fontSize = Mathf.RoundToInt(h * 0.1f * _banner.power);
        _smallStyle.fontSize = Mathf.RoundToInt(h * 0.045f * _banner.power);

        var pivot = new Vector2(Screen.width * 0.5f, h * 0.24f - lift);
        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(wobble, pivot);
        GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), pivot);
        var bigRect = new Rect(pivot.x - 400f, pivot.y - h * 0.08f, 800f, h * 0.12f);
        var smallRect = new Rect(pivot.x - 400f, pivot.y + h * 0.03f, 800f, h * 0.07f);
        Outlined(bigRect, _banner.big, _bigStyle, _banner.color, alpha, 4f);
        Outlined(smallRect, _banner.small, _smallStyle, Color.white, alpha, 3f);
        GUI.matrix = saved;
    }

    static void Outlined(Rect r, string text, GUIStyle style, Color color, float alpha, float width)
    {
        var dark = new Color(0.08f, 0.04f, 0.12f, alpha);
        style.normal.textColor = dark;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * width, r.y + Mathf.Sin(a) * width + 2f, r.width, r.height), text, style);
        }
        color.a = alpha;
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }
}
