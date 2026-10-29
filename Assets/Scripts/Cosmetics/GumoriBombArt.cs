using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Bombes et paroi cassable dessinées : bombe ronde de la couleur de son niveau (contour d'encre, mèche, étincelle une fois
// lancée, clignote avant d'exploser) avec son compte à rebours en gros chiffre ; fêlures des morceaux de paroi touchés, éclats
// quand un morceau cède ; explosion (GumoriFx.Blast) et absorption. Lit l'état de GumoriBombs / CurveCollider2D, n'écrit
// jamais : purement visuel. Shader Slime/Fx, dessiné devant les gumori.
[DisallowMultipleComponent]
[DefaultExecutionOrder(860)]
[AddComponentMenu("Gumori/Habillage/GumoriBombArt")]
public class GumoriBombArt : MonoBehaviour
{
    [Header("Références (trouvées seules si vides)")]
    public GumoriBombs bombs;
    public GumoriFx fx;
    public GumoriSound sound;
    public GumoriSkin skin;
    public Shader shader;

    [Header("Bombes")]
    [Tooltip("Couleur de l'encre (contour, mèche).")]
    public Color ink = new Color(0.1f, 0.06f, 0.14f);
    [Tooltip("Taille du chiffre, en fraction du diamètre de la bombe.")]
    [Range(0.3f, 1.5f)] public float digitSize = 0.8f;

    [Header("Paroi")]
    [Tooltip("Couleur des fêlures.")]
    public Color crack = new Color(1f, 1f, 1f, 0.95f);
    [Tooltip("Nombre d'éclats quand un morceau de paroi cède.")]
    [Range(0, 60)] public int shards = 18;

    struct Shard { public Vector2 position, velocity; public float age, life, size, rotation, spin; }

    readonly List<Shard> _shards = new List<Shard>();
    readonly List<CurveCollider2D> _hooked = new List<CurveCollider2D>();
    readonly Dictionary<GumoriBombs.Bomb, float> _tickTime = new Dictionary<GumoriBombs.Bomb, float>();
    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector4> _uv0 = new List<Vector4>(), _uv1 = new List<Vector4>(), _uv2 = new List<Vector4>();
    readonly List<int> _tris = new List<int>();
    Material _material;
    Mesh _mesh;
    GUIStyle _digit;
    System.Random _rand = new System.Random(11);

    float Rand(float a, float b) => a + (b - a) * (float)_rand.NextDouble();

    void OnEnable()
    {
        if (bombs == null) bombs = FindAnyObjectByType<GumoriBombs>();
        if (fx == null) fx = FindAnyObjectByType<GumoriFx>();
        if (sound == null) sound = FindAnyObjectByType<GumoriSound>();
        if (skin == null) skin = FindAnyObjectByType<GumoriSkin>();
        if (shader == null) shader = Shader.Find("Slime/Fx");
        if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3010 };
        _mesh = new Mesh { name = "Bombes", hideFlags = HideFlags.HideAndDontSave };
        _mesh.MarkDynamic();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        if (bombs != null) { bombs.Ticked += OnTicked; bombs.Exploded += OnExploded; bombs.Absorbed += OnAbsorbed; }
        LevelRestart.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (bombs != null) { bombs.Ticked -= OnTicked; bombs.Exploded -= OnExploded; bombs.Absorbed -= OnAbsorbed; }
        for (int i = 0; i < _hooked.Count; i++) if (_hooked[i] != null) _hooked[i].PieceHit -= OnPieceHit;
        _hooked.Clear();
        LevelRestart.Restarted -= OnRestarted;
        if (_material != null) DestroyImmediate(_material);
        if (_mesh != null) DestroyImmediate(_mesh);
    }

    void OnRestarted()
    {
        _shards.Clear();
        _tickTime.Clear();
    }

    void OnTicked(GumoriBombs.Bomb bomb)
    {
        if (LevelSimulation.FastForwarding) return;
        _tickTime[bomb] = Time.time;
    }

    void OnExploded(GumoriBombs.Bomb bomb, Vector2 at, float radius)
    {
        _tickTime.Remove(bomb);
        if (LevelSimulation.FastForwarding) return;
        if (fx != null) fx.Blast(at, radius, BodyColor(bomb));
        if (sound != null) sound.Split(bomb.level + 4, 0, at.x);
    }

    void OnAbsorbed(GumoriBombs.Bomb bomb, Gumori absorber, Vector2 at)
    {
        _tickTime.Remove(bomb);
        if (LevelSimulation.FastForwarding) return;
        if (fx != null) fx.Split(absorber.Center, bomb.level, 0.5f * absorber.diameter);
        if (sound != null) sound.Split(bomb.level, 0, at.x);
    }

    void OnPieceHit(int piece, int hitsLeft)
    {
        if (LevelSimulation.FastForwarding || hitsLeft > 0) return;
        foreach (var wall in _hooked)
        {
            if (wall == null || piece >= wall.Pieces.Count || wall.Pieces[piece].hitsLeft != 0) continue;
            var p = wall.Pieces[piece];
            for (int i = 0; i < shards; i++)
            {
                Vector2 at = wall.PointAt(Rand(p.from, p.to));
                float a = Rand(0f, Mathf.PI * 2f);
                _shards.Add(new Shard
                {
                    position = at, velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(1.5f, 4.5f),
                    life = Rand(0.5f, 1f), size = Rand(0.07f, 0.15f), rotation = Rand(0f, 6.3f), spin = Rand(-12f, 12f)
                });
            }
            if (sound != null) sound.Split(2, 0, wall.PointAt(0.5f * (p.from + p.to)).x);
        }
    }

    void Hook()
    {
        var walls = CurveCollider2D.Breakables;
        for (int i = 0; i < walls.Count; i++)
            if (!_hooked.Contains(walls[i])) { walls[i].PieceHit += OnPieceHit; _hooked.Add(walls[i]); }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || _mesh == null) return;
        Hook();
        float dt = Time.deltaTime;
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var p = _shards[i];
            p.age += dt;
            if (p.age >= p.life) { _shards.RemoveAt(i); continue; }
            p.velocity += Vector2.down * (8f * dt);
            p.position += p.velocity * dt;
            p.rotation += p.spin * dt;
            _shards[i] = p;
        }
        Build();
    }

    // Rayon dessiné : distance moyenne des capsules au centre + leur épaisseur (suit la vraie taille, pression comprise).
    static float DrawnRadius(Gumori blob, out Vector2 center)
    {
        center = blob.Center;
        int n = blob.CapsuleCount;
        if (n == 0) return 0.5f * blob.diameter;
        float r = 0f;
        for (int k = 0; k < n; k++) r += (blob.Capsule(k).position - center).magnitude;
        return r / n + blob.CapsuleRadius;
    }

    void Build()
    {
        _verts.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _tris.Clear();
        Vector4 inkColor = new Vector4(ink.r, ink.g, ink.b, 1f);

        if (bombs != null)
        {
            var list = bombs.Bombs;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (!GumoriDropper.IsInPlay(b.blob)) continue;
                float r = DrawnRadius(b.blob, out Vector2 c);
                Color body = BodyColor(b);
                if (b.explodeStep >= 0) body = Color.Lerp(body, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
                float wobble = 1f + 0.08f * Pulse(b);
                Vector2 up = Vector2.up;
                // Mèche (derrière le corps) puis étincelle au bout.
                Vector2 wick = c + up * (r * 1.05f);
                Quad(3, wick, up * (r * 0.28f), new Vector2(r * 0.07f, 0f), inkColor, 0.4f);
                Quad(0, c, Vector2.right * (r * 1.09f * wobble), up * (r * 1.09f * wobble), inkColor, 0f);
                Quad(0, c, Vector2.right * (r * wobble), up * (r * wobble), new Vector4(body.r, body.g, body.b, 1f), 0f);
                if (b.armed)
                {
                    float s = r * (0.35f + 0.12f * Mathf.Sin(Time.time * 37f + i));
                    Vector2 tip = c + up * (r * 1.32f);
                    Quad(1, tip, Vector2.right * s, up * s, new Vector4(2.6f, 1.8f, 0.7f, 1f), 0f);
                }
            }
        }

        var walls = CurveCollider2D.Breakables;
        Vector4 crackColor = crack;
        for (int w = 0; w < walls.Count; w++)
        {
            var wall = walls[w];
            var pieces = wall.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.broken || p.hitsLeft >= wall.pieceHits) continue;
                int lines = 2 + 3 * (wall.pieceHits - p.hitsLeft);
                var rand = new System.Random(i * 7919 + w);
                for (int k = 0; k < lines; k++)
                {
                    float along = Mathf.Lerp(p.from, p.to, 0.15f + 0.7f * (float)rand.NextDouble());
                    Vector2 at = wall.PointAt(along);
                    float a = (float)rand.NextDouble() * Mathf.PI * 2f, len = 0.12f + 0.25f * (float)rand.NextDouble();
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Quad(3, at, dir * len, new Vector2(-dir.y, dir.x) * 0.03f, crackColor, 1f);
                }
            }
        }

        for (int i = 0; i < _shards.Count; i++)
        {
            var p = _shards[i];
            float u = p.age / p.life, cs = Mathf.Cos(p.rotation) * p.size, sn = Mathf.Sin(p.rotation) * p.size;
            Quad(3, p.position, new Vector2(cs, sn), new Vector2(-sn, cs) * 0.6f, new Vector4(0.75f, 0.92f, 1f, 0.9f * Mathf.Clamp01((1f - u) * 3f)), 0.5f);
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

    // Couleur de la famille du niveau (design en jeu), comme les gumori dessinés.
    Color BodyColor(GumoriBombs.Bomb bomb)
    {
        var design = skin != null ? skin.Design(bomb.level) : null;
        return design != null ? design.body : bomb.blob.debugColor;
    }

    // Rebond (0..1) juste après qu'un tir a fait baisser le compte.
    float Pulse(GumoriBombs.Bomb bomb) => _tickTime.TryGetValue(bomb, out float t) ? Mathf.Exp(-(Time.time - t) * 7f) * Mathf.Cos((Time.time - t) * 25f) : 0f;

    void Quad(int type, Vector2 center, Vector2 ax, Vector2 ay, Vector4 color, float param)
    {
        int v = _verts.Count;
        for (int k = 0; k < 4; k++)
        {
            float x = k == 0 || k == 3 ? -1f : 1f, y = k < 2 ? -1f : 1f;
            Vector2 p = center + ax * x + ay * y;
            _verts.Add(new Vector3(p.x, p.y, 0f));
            _uv0.Add(new Vector4(x, y, type, param));
            _uv1.Add(color);
            _uv2.Add(Vector4.zero);
        }
        _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 2);
        _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 3);
    }

    // Compte à rebours : gros chiffre au centre de la bombe (plus pâle tant qu'elle est dans la réserve, rouge au dernier tir).
    void OnGUI()
    {
        if (!Application.isPlaying || bombs == null || Event.current.type != EventType.Repaint) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float pixelsPerMeter = Screen.height / (2f * cam.orthographicSize);
        _digit ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var list = bombs.Bombs;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (!GumoriDropper.IsInPlay(b.blob)) continue;
            float r = DrawnRadius(b.blob, out Vector2 c);
            Vector3 s = cam.WorldToScreenPoint(c);
            float size = 2f * r * pixelsPerMeter * digitSize * (1f + 0.3f * Mathf.Max(0f, Pulse(b)));
            _digit.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            var rect = new Rect(s.x - size, Screen.height - s.y - size, 2f * size, 2f * size);
            Color col = b.fuse <= 1 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            GumoriUi.Outlined(rect, b.fuse.ToString(), _digit, col, b.armed ? 1f : 0.7f, Mathf.Max(1.5f, size * 0.08f));
        }
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_material == null || _mesh == null || _mesh.vertexCount == 0) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        Graphics.RenderMesh(new RenderParams(_material) { camera = cam, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f) },
            _mesh, 0, Matrix4x4.identity);
    }
}
