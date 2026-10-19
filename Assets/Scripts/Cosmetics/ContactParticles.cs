using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Particules aux contacts d'une balle (roulement + chocs). Purement visuel : lit l'état du dernier pas dans LateUpdate,
// n'écrit jamais dans la physique. Un seul ParticleSystem par balle, alimenté par Emit (aucune émission automatique).
[DisallowMultipleComponent]
public class ContactParticles : MonoBehaviour
{
    [Header("Roulement")]
    [Tooltip("Particules émises par seconde de simulation et par point de contact, à intensité maximale\n" +
             "(vitesse ≥ 'fullSpeed' et appui ≥ 'fullForce').")]
    [Min(0f)] public float rollRate = 120f;
    [Tooltip("Vitesse de la balle le long de la surface (km/h, absolu) en dessous de laquelle rien n'est émis.")]
    [SpeedKmh] [Min(0f)] public float minSpeed = 1f;
    [Tooltip("Vitesse le long de la surface (km/h, absolu) à partir de laquelle l'émission est maximale.")]
    [SpeedKmh] [Min(0.01f)] public float fullSpeed = 14f;
    [Tooltip("Force d'appui d'un point de contact (N, absolu) à partir de laquelle l'émission est maximale.\n" +
             "Repère : un blob de diamètre 1 et densité 1 pèse ≈ 7,7 N, réparti sur ses points de contact.")]
    [Min(0.01f)] public float fullForce = 3f;

    [Header("Chocs")]
    [Tooltip("Vitesse d'impact (km/h, le long de la normale) en dessous de laquelle un nouveau contact ne fait pas de gerbe.")]
    [SpeedKmh] [Min(0f)] public float impactMinSpeed = 3f;
    [Tooltip("Particules par m/s d'impact au-dessus de 'impactMinSpeed', pour un nouveau point de contact.")]
    [Min(0f)] public float impactParticlesPerSpeed = 2f;
    [Tooltip("Nombre maximal de particules d'une gerbe d'impact (par point de contact).")]
    [Min(0)] public int impactMaxBurst = 24;

    [Header("Mouvement des particules")]
    [Tooltip("Part de la vitesse de la balle le long de la surface transmise aux particules (0 = restent sur place,\n" +
             "1 = suivent la balle). Une valeur moyenne donne une traînée derrière la balle.")]
    [Range(0f, 1f)] public float carry = 0.35f;
    [Tooltip("Vitesse d'éjection le long de la normale de la surface (km/h), à intensité maximale.")]
    [SpeedKmh] [Min(0f)] public float kick = 3.5f;
    [Tooltip("Dispersion aléatoire de la direction d'éjection, en degrés de part et d'autre.")]
    [Range(0f, 90f)] public float spread = 35f;
    [Tooltip("Multiplicateur de gravité des particules (1 = gravité du monde, 0 = flottent).")]
    public float gravity = 1f;
    [Tooltip("Durée de vie des particules (s, temps de jeu : ralenties par le ralenti), tirée entre min (x) et max (y).")]
    public Vector2 lifetime = new Vector2(0.2f, 0.45f);
    [Tooltip("Diamètre des particules (unités Unity, absolu) tiré entre min (x) et max (y), multiplié par l'intensité.")]
    public Vector2 size = new Vector2(0.1f, 0.24f);

    [Header("Rendu")]
    [Tooltip("Couleur au cours de la vie de la particule (début → fin, alpha compris).")]
    public Gradient color = DefaultGradient();
    [Tooltip("Étirement des particules dans le sens de leur vitesse (s) : longueur ajoutée = vitesse × valeur. 0 = points ronds.")]
    [Min(0f)] public float stretch = 0.03f;
    [Tooltip("Nombre maximal de particules vivantes pour cette balle : plafond du coût, l'excédent n'est pas émis.")]
    [Range(16, 2000)] public int maxParticles = 400;
    [Tooltip("Matériau des particules. Vide = matériau créé au lancement (shader Sprites/Default + disque flou généré), présent dans toute build.")]
    public Material material;
    [Tooltip("Ordre d'affichage : plus grand = dessiné par-dessus.")]
    public int sortingOrder = 10;
    [Tooltip("Graine de l'aléa visuel (direction, taille, durée de vie). Indépendant de la simulation.")]
    public uint seed = 1;

    // Au-delà, l'écart de pas est un saut (rejeu accéléré, chargement) : pas de rafale de rattrapage.
    const int MaxStepsPerFrame = 8;

    readonly List<SurfaceContact> _contacts = new List<SurfaceContact>();
    IContactFxSource _source;
    ParticleSystem _system;
    Material _ownMaterial;
    static Texture2D _disc;
    Unity.Mathematics.Random _random;
    int _lastStep = -1;
    bool _dirty;

    void OnEnable()
    {
        _source = GetComponent<IContactFxSource>();
        if (_source == null)
        {
            Debug.LogWarning("ContactParticles : aucun IContactFxSource (BLOB…) sur cet objet.", this);
            enabled = false;
            return;
        }
        _random = new Unity.Mathematics.Random(math.max(1u, seed));
        if (_system == null) CreateSystem();
        _system.gameObject.SetActive(true);
        _lastStep = -1;
        LevelRestart.Restarted += Clear;
        LevelSimulation.Jumped += Clear;
    }

    void OnDisable()
    {
        LevelRestart.Restarted -= Clear;
        LevelSimulation.Jumped -= Clear;
        if (_system != null) _system.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_system != null) Destroy(_system.gameObject);
        if (_ownMaterial != null) Destroy(_ownMaterial);
    }

    void Clear()
    {
        if (_system != null) _system.Clear();
        _lastStep = -1;
    }

    void LateUpdate()
    {
        int step = LevelSimulation.Step;
        int steps = step - _lastStep;
        bool jump = _lastStep < 0 || steps < 0 || steps > MaxStepsPerFrame;
        _lastStep = step;
        if (jump || steps == 0 || LevelSimulation.FastForwarding) return;

        if (_dirty) ApplySettings();
        _source.GetSurfaceContacts(_contacts);
        float dt = steps * Time.fixedDeltaTime;
        for (int i = 0; i < _contacts.Count; i++) Emit(_contacts[i], dt);
    }

    void Emit(in SurfaceContact c, float dt)
    {
        Vector2 n = c.normal;
        Vector2 tangentVelocity = c.relativeVelocity - Vector2.Dot(c.relativeVelocity, n) * n;
        float speed = tangentVelocity.magnitude;
        float force = c.normalImpulse / Time.fixedDeltaTime;
        float intensity = math.saturate((speed - minSpeed) / math.max(0.01f, fullSpeed - minSpeed)) * math.saturate(force / fullForce);

        int count = StochasticRound(rollRate * intensity * dt);
        if (count > 0) EmitBurst(c.point, n, tangentVelocity * carry, kick * intensity, spread, 0.4f + 0.6f * intensity, count);

        if (c.isNew && c.approachSpeed > impactMinSpeed)
        {
            float over = c.approachSpeed - impactMinSpeed;
            int burst = math.min(impactMaxBurst, StochasticRound(over * impactParticlesPerSpeed));
            if (burst > 0) EmitBurst(c.point, n, tangentVelocity * carry, 0.5f * c.approachSpeed, 70f, 1f, burst);
        }
    }

    void EmitBurst(Vector2 point, Vector2 normal, Vector2 baseVelocity, float ejectSpeed, float spreadDegrees, float sizeScale, int count)
    {
        count = math.min(count, maxParticles - _system.particleCount);
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        float normalAngle = math.atan2(normal.y, normal.x);
        for (int i = 0; i < count; i++)
        {
            float a = normalAngle + math.radians(_random.NextFloat(-spreadDegrees, spreadDegrees));
            Vector2 dir = new Vector2(math.cos(a), math.sin(a));
            Vector2 v = baseVelocity * _random.NextFloat(0.6f, 1f) + dir * ejectSpeed * _random.NextFloat(0.4f, 1f);
            p.position = new Vector3(point.x, point.y, 0f);
            p.velocity = new Vector3(v.x, v.y, 0f);
            float life = _random.NextFloat(lifetime.x, math.max(lifetime.x, lifetime.y));
            p.startLifetime = life;
            p.startSize = _random.NextFloat(size.x, math.max(size.x, size.y)) * sizeScale;
            _system.Emit(p, 1);
        }
    }

    int StochasticRound(float x)
    {
        int n = (int)x;
        return n + (_random.NextFloat() < x - n ? 1 : 0);
    }

    void CreateSystem()
    {
        var go = new GameObject("ContactParticles (" + name + ")") { hideFlags = HideFlags.DontSave };
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
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

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
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0.3f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
