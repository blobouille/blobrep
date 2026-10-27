using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Éléments de niveau Space dessinés : bocaux (gumori enfermé qui appelle à l'aide, bulle de verre à couvercle, fêlures, éclats
// quand il se brise) et moulins (pales en sucre d'orge). Lit l'état de GumoriJars / GumoriMill, n'écrit jamais : purement visuel.
// Shader Slime/Fx (types 8 bocal, 9 barre), dessiné devant les gumori.
[DisallowMultipleComponent]
[DefaultExecutionOrder(850)]
[AddComponentMenu("Gumori/Habillage/GumoriLevelArt")]
public class GumoriLevelArt : MonoBehaviour
{
    [Header("Références (trouvées seules si vides)")]
    public GumoriJars jars;
    public GumoriSkin skin;
    public GumoriFx fx;
    public GumoriSound sound;
    public GumoriLauncher launcher;
    public Shader shader;

    [Header("Bocaux")]
    [Tooltip("Couleur et opacité du verre.")]
    public Color glass = new Color(0.75f, 0.92f, 1f, 0.9f);
    [Tooltip("Couleur du couvercle.")]
    public Color lid = new Color(1f, 0.62f, 0.75f);
    [Tooltip("Taille du gumori enfermé, en fraction de sa vraie taille (il flotte dans le verre).")]
    [Range(0.5f, 1f)] public float trappedScale = 0.9f;
    [Tooltip("Durée (s) du tremblement d'un bocal fêlé.")]
    [Min(0f)] public float crackShake = 0.35f;
    [Tooltip("Nombre d'éclats de verre quand un bocal se brise.")]
    [Range(0, 60)] public int shards = 22;

    [Header("Moulins")]
    [Tooltip("Couleur du moyeu.")]
    public Color hub = new Color(0.98f, 0.85f, 0.45f);

    struct Shard { public Vector2 position, velocity; public float age, life, size, rotation, spin; }

    readonly List<GumoriSkin.VirtualSlime> _trapped = new List<GumoriSkin.VirtualSlime>();
    readonly List<float> _crackTime = new List<float>();
    readonly List<Shard> _shards = new List<Shard>();
    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector4> _uv0 = new List<Vector4>(), _uv1 = new List<Vector4>(), _uv2 = new List<Vector4>();
    readonly List<int> _tris = new List<int>();
    GumoriMill[] _mills = new GumoriMill[0];
    GumoriFusion _fusion;
    Material _material;
    Mesh _mesh;
    System.Random _rand = new System.Random(7);

    float Rand(float a, float b) => a + (b - a) * (float)_rand.NextDouble();

    void OnEnable()
    {
        if (jars == null) jars = FindAnyObjectByType<GumoriJars>();
        if (skin == null) skin = FindAnyObjectByType<GumoriSkin>();
        if (fx == null) fx = FindAnyObjectByType<GumoriFx>();
        if (sound == null) sound = FindAnyObjectByType<GumoriSound>();
        if (launcher == null) launcher = FindAnyObjectByType<GumoriLauncher>();
        if (shader == null) shader = Shader.Find("Slime/Fx");
        _mills = FindObjectsByType<GumoriMill>(FindObjectsSortMode.None);
        if (jars != null && jars.dropper != null) _fusion = jars.dropper.Part<GumoriFusion>();
        if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3010 };
        _mesh = new Mesh { name = "Éléments du niveau", hideFlags = HideFlags.HideAndDontSave };
        _mesh.MarkDynamic();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        if (skin != null) skin.DrawExtras += OnDrawExtras;
        if (jars != null) { jars.Cracked += OnCracked; jars.Freed += OnFreed; }
        LevelRestart.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (skin != null) skin.DrawExtras -= OnDrawExtras;
        if (jars != null) { jars.Cracked -= OnCracked; jars.Freed -= OnFreed; }
        LevelRestart.Restarted -= OnRestarted;
        if (_material != null) DestroyImmediate(_material);
        if (_mesh != null) DestroyImmediate(_mesh);
        _trapped.Clear();
    }

    void OnRestarted()
    {
        _trapped.Clear();
        _crackTime.Clear();
        _shards.Clear();
    }

    void Sync()
    {
        if (jars == null || skin == null) return;
        var states = jars.States;
        while (_trapped.Count < states.Count)
        {
            var s = states[_trapped.Count];
            _trapped.Add(skin.NewVirtual(s.level, (s.radius - jars.glass) * trappedScale));
            _crackTime.Add(-10f);
        }
    }

    void OnCracked(int jar, int hitsLeft)
    {
        if (LevelSimulation.FastForwarding) return;
        Sync();
        if (jar < _crackTime.Count) _crackTime[jar] = Time.time;
        if (hitsLeft > 0 && sound != null) sound.Split(jars.States[jar].level, 3, jars.States[jar].center.x);
    }

    void OnFreed(int jar, Gumori blob)
    {
        if (LevelSimulation.FastForwarding) return;
        var s = jars.States[jar];
        for (int i = 0; i < shards; i++)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            _shards.Add(new Shard
            {
                position = s.center + dir * s.radius * Rand(0.7f, 1f), velocity = dir * Rand(2f, 5f) + Vector2.up * Rand(0f, 2f),
                life = Rand(0.5f, 0.9f), size = Rand(0.08f, 0.16f), rotation = Rand(0f, 6.3f), spin = Rand(-12f, 12f)
            });
        }
        if (fx != null) fx.Split(s.center, s.level, s.radius);
        if (sound != null) sound.Split(s.level, 0, s.center.x);
    }

    // Gumori enfermés : dessinés par GumoriSkin (même design et visage que les vrais), qui flottent doucement dans le verre.
    void OnDrawExtras(float dt)
    {
        if (jars == null || skin == null) return;
        Sync();
        var states = jars.States;
        Vector2 lookAt = launcher != null && launcher.Rack.Count > 0 && launcher.Rack[0] != null ? launcher.Rack[0].Center : Vector2.up * 20f;
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            if (s.broken) continue;
            var v = _trapped[i];
            v.level = s.level;
            v.center = s.center + Shake(i, s.radius) + new Vector2(0f, 0.04f * s.radius * Mathf.Sin(Time.time * 1.7f + i * 2.1f));
            v.trapped = true;
            v.lookAt = lookAt;
            skin.DrawVirtual(v, dt);
        }
    }

    Vector2 Shake(int jar, float radius)
    {
        if (jar >= _crackTime.Count || crackShake <= 0f) return Vector2.zero;
        float t = Time.time - _crackTime[jar];
        if (t < 0f || t > crackShake) return Vector2.zero;
        float k = 1f - t / crackShake;
        return new Vector2(Mathf.Sin(t * 70f), Mathf.Cos(t * 53f)) * (0.06f * radius * k);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || _mesh == null) return;
        float dt = Time.deltaTime;
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var p = _shards[i];
            p.age += dt;
            if (p.age >= p.life) { _shards.RemoveAt(i); continue; }
            p.velocity += Vector2.down * (10f * dt);
            p.position += p.velocity * dt;
            p.rotation += p.spin * dt;
            _shards[i] = p;
        }
        Build();
    }

    void Build()
    {
        _verts.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _tris.Clear();
        Vector4 glassColor = glass, lidColor = new Vector4(lid.r, lid.g, lid.b, 1f);

        for (int m = 0; m < _mills.Length; m++)
        {
            var mill = _mills[m];
            if (mill == null || !mill.isActiveAndEnabled) continue;
            Vector4 c = new Vector4(mill.color.r, mill.color.g, mill.color.b, 1f);
            float angle = mill.Angle;
            bool bar = mill.blades == 2;
            for (int i = 0; i < (bar ? 1 : mill.blades); i++)
            {
                Vector2 dir = mill.BladeDirection(i, angle);
                float half = bar ? mill.length : 0.5f * (mill.length + mill.thickness);
                Vector2 center = bar ? mill.Hub : mill.Hub + dir * (half - mill.thickness);
                Quad(9, center, dir * half, new Vector2(-dir.y, dir.x) * mill.thickness, c, half / mill.thickness);
            }
            Quad(9, mill.Hub, Vector2.right * mill.hubRadius, Vector2.up * mill.hubRadius, new Vector4(hub.r, hub.g, hub.b, 1f), 1f);
            Quad(6, mill.Hub, Vector2.right * mill.hubRadius * 0.3f, Vector2.up * mill.hubRadius * 0.3f, new Vector4(0.08f, 0.04f, 0.12f, 1f), 0f);
        }

        if (jars != null)
        {
            var states = jars.States;
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s.broken) continue;
                Vector2 c = s.center + Shake(i, s.radius);
                float crack = s.hits > 0 ? (float)(s.hits - s.hitsLeft) / s.hits * 0.85f : 0f;
                Quad(8, c, Vector2.right * s.radius, Vector2.up * s.radius, glassColor, crack);
                Vector2 top = c + Vector2.up * (s.radius * 0.97f);
                Quad(9, top, Vector2.right * s.radius * 0.42f, Vector2.up * s.radius * 0.13f, lidColor, 0.42f / 0.13f);
            }
        }

        for (int i = 0; i < _shards.Count; i++)
        {
            var p = _shards[i];
            float u = p.age / p.life, cs = Mathf.Cos(p.rotation) * p.size, sn = Mathf.Sin(p.rotation) * p.size;
            Quad(3, p.position, new Vector2(cs, sn), new Vector2(-sn, cs) * 0.6f, new Vector4(glass.r, glass.g, glass.b, glass.a * Mathf.Clamp01((1f - u) * 3f)), 0.5f);
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

    // Quadrilatère de demi-axes 'ax' et 'ay' (coordonnées locales -1..1).
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

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_material == null || _mesh == null || _mesh.vertexCount == 0) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        Graphics.RenderMesh(new RenderParams(_material) { camera = cam, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f) },
            _mesh, 0, Matrix4x4.identity);
    }
}
