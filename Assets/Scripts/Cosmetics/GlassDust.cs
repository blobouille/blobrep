using Unity.Mathematics;
using UnityEngine;

// Poussière et éclats fins d'une vitre (GlassPane sur le même objet). Purement visuel : la physique décide où, combien et à
// quelle vitesse (événements GlassPane.Dust : éclats trop petits pour être des corps, morceaux pulvérisés ; GlassPane.Shattered :
// bouffée au point d'impact). Les événements arrivent pendant le pas : mis en file, émis dans LateUpdate. Rien pendant le rejeu
// accéléré ; purge au redémarrage et au retour de sauvegarde. Un seul ParticleSystem par vitre, alimenté par Emit.
[DisallowMultipleComponent]
public class GlassDust : MonoBehaviour
{
    [Header("Quantité")]
    [Tooltip("Particules par m² de verre réduit en poussière. Repère : un éclat de 3 × 3 cm ≈ 0,001 m².\n" +
             "Une vitre de 2 m × 8 cm traversée à 54 km/h donne ≈ 0,02 m² de poussière.")]
    [Min(0f)] public float particlesPerArea = 6000f;
    [Tooltip("Particules par éclat de poussière au minimum (un éclat visible même minuscule) et au maximum.")]
    public Vector2Int particlesPerShard = new Vector2Int(1, 14);
    [Tooltip("Bouffée au point d'impact à la casse : particules par unité d'intensité (énergie / énergie de casse).")]
    [Min(0f)] public float impactPuff = 12f;
    [Tooltip("Nombre maximal de particules de la bouffée d'impact.")]
    [Min(0)] public int impactPuffMax = 80;

    [Header("Mouvement")]
    [Tooltip("Part de la vitesse de l'éclat transmise à ses particules (1 = elles partent avec lui).")]
    [Range(0f, 2f)] public float inherit = 1f;
    [Tooltip("Vitesse aléatoire ajoutée (km/h), dans toutes les directions : le nuage s'élargit.")]
    [SpeedKmh] [Min(0f)] public float scatter = 1.5f;
    [Tooltip("Freinage dans l'air (1/s) : le nuage ralentit et reste en suspension. 0 = aucun.")]
    [Min(0f)] public float drag = 2.5f;
    [Tooltip("Multiplicateur de gravité des particules (1 = gravité du monde, 0 = flottent).")]
    public float gravity = 0.4f;
    [Tooltip("Durée de vie (s, temps de jeu), tirée entre min (x) et max (y).")]
    public Vector2 lifetime = new Vector2(0.35f, 1.1f);
    [Tooltip("Diamètre des particules (unités Unity, absolu), tiré entre min (x) et max (y).")]
    public Vector2 size = new Vector2(0.02f, 0.07f);

    [Header("Rendu")]
    [Tooltip("Couleur au cours de la vie (début → fin, alpha compris).")]
    public Gradient color = DefaultGradient();
    [Tooltip("Étirement dans le sens de la vitesse (s) : longueur ajoutée = vitesse × valeur. Donne des traînées aux éclats rapides.")]
    [Min(0f)] public float stretch = 0.008f;
    [Tooltip("Nombre maximal de particules vivantes pour cette vitre : plafond du coût.")]
    [Range(16, 4000)] public int maxParticles = 800;
    [Tooltip("Matériau. Vide = créé au lancement (Sprites/Default + disque flou généré), présent dans toute build.")]
    public Material material;
    [Tooltip("Ordre d'affichage : plus grand = dessiné par-dessus.")]
    public int sortingOrder = 11;
    [Tooltip("Graine de l'aléa visuel. Indépendant de la simulation.")]
    public uint seed = 1;

    struct Pending { public Vector2 position, velocity; public float amount; public bool puff; }
    const int QueueSize = 512;
    readonly Pending[] _queue = new Pending[QueueSize];
    int _queued;

    GlassPane _pane;
    ParticleSystem _system;
    Material _ownMaterial;
    static Texture2D _disc;
    Unity.Mathematics.Random _random;
    bool _dirty;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        _pane = GetComponent<GlassPane>();
        if (_pane == null)
        {
            Debug.LogWarning("GlassDust : aucune GlassPane sur cet objet.", this);
            enabled = false;
            return;
        }
        _random = new Unity.Mathematics.Random(math.max(1u, seed));
        if (_system == null) CreateSystem();
        _system.gameObject.SetActive(true);
        _queued = 0;
        GlassPane.Dust += OnDust;
        GlassPane.Shattered += OnShattered;
        LevelRestart.Restarted += Clear;
        LevelSimulation.Jumped += Clear;
    }

    void OnDisable()
    {
        GlassPane.Dust -= OnDust;
        GlassPane.Shattered -= OnShattered;
        LevelRestart.Restarted -= Clear;
        LevelSimulation.Jumped -= Clear;
        if (_system != null) _system.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_system != null) Destroy(_system.gameObject);
        if (_ownMaterial != null) Destroy(_ownMaterial);
    }

    void OnDust(GlassPane pane, Vector2 position, Vector2 velocity, float area)
    {
        if (pane != _pane || LevelSimulation.FastForwarding || _queued >= QueueSize) return;
        _queue[_queued++] = new Pending { position = position, velocity = velocity, amount = area };
    }

    void OnShattered(GlassPane pane, Vector2 point, float intensity)
    {
        if (pane != _pane || LevelSimulation.FastForwarding || _queued >= QueueSize) return;
        _queue[_queued++] = new Pending { position = point, amount = math.max(0.5f, intensity), puff = true };
    }

    void Clear()
    {
        _queued = 0;
        if (_system != null) _system.Clear();
    }

    void LateUpdate()
    {
        if (_queued == 0 || _system == null) return;
        if (_dirty) ApplySettings();
        for (int i = 0; i < _queued; i++)
        {
            var e = _queue[i];
            if (e.puff)
                Emit(e.position, Vector2.zero, math.min(impactPuffMax, (int)math.round(impactPuff * e.amount)), 1.5f);
            else
            {
                int count = math.clamp((int)math.round(e.amount * particlesPerArea), particlesPerShard.x, particlesPerShard.y);
                Emit(e.position, e.velocity * inherit, count, 1f);
            }
        }
        _queued = 0;
    }

    void Emit(Vector2 point, Vector2 baseVelocity, int count, float scatterScale)
    {
        count = math.min(count, maxParticles - _system.particleCount);
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            float a = _random.NextFloat(0f, 2f * math.PI);
            Vector2 v = baseVelocity * _random.NextFloat(0.7f, 1.1f)
                      + new Vector2(math.cos(a), math.sin(a)) * scatter * scatterScale * _random.NextFloat(0.2f, 1f);
            p.position = new Vector3(point.x, point.y, 0f);
            p.velocity = new Vector3(v.x, v.y, 0f);
            p.startLifetime = _random.NextFloat(lifetime.x, math.max(lifetime.x, lifetime.y));
            p.startSize = _random.NextFloat(size.x, math.max(size.x, size.y));
            _system.Emit(p, 1);
        }
    }

    void CreateSystem()
    {
        var go = new GameObject("GlassDust (" + name + ")") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        _system = go.AddComponent<ParticleSystem>();
        _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startColor = Color.white;
        var emission = _system.emission;
        emission.enabled = false;
        var shape = _system.shape;
        shape.enabled = false;
        var sizeOverLifetime = _system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (material == null)
        {
            _ownMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = Disc() };
            _ownMaterial.hideFlags = HideFlags.DontSave;
        }
        renderer.sharedMaterial = material != null ? material : _ownMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.alignment = ParticleSystemRenderSpace.View;

        ApplySettings();
        _system.Play();
    }

    void OnValidate() => _dirty = true;

    void ApplySettings()
    {
        _dirty = false;
        var main = _system.main;
        main.maxParticles = maxParticles;
        // gravityModifier multiplie la gravité 3D (Physics.gravity) : recalée sur la gravité verticale du monde 2D.
        float g3 = Physics.gravity.y;
        main.gravityModifier = math.abs(g3) > 1e-4f ? gravity * Unity.U2D.Physics.PhysicsWorld.defaultWorld.gravity.y / g3 : 0f;
        var limit = _system.limitVelocityOverLifetime;
        limit.enabled = drag > 0f;
        limit.drag = drag;
        limit.multiplyDragByParticleSize = false;
        limit.multiplyDragByParticleVelocity = false;
        var col = _system.colorOverLifetime;
        col.enabled = true;
        col.color = color;
        var renderer = _system.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.renderMode = stretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = stretch;
        renderer.lengthScale = 1f;
    }

    static Texture2D Disc()
    {
        if (_disc != null) return _disc;
        const int N = 32;
        _disc = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
            float a = math.saturate(1f - math.sqrt(dx * dx + dy * dy));
            pixels[y * N + x] = new Color32(255, 255, 255, (byte)(255f * a * a));
        }
        _disc.SetPixels32(pixels);
        _disc.Apply(false, true);
        return _disc;
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.92f, 1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.8f, 0.95f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
