using System.Collections.Generic;
using UnityEngine;

// Sound design procédural : tous les sons sont calculés au lancement (aucun fichier audio), à partir de formules.
// - lâcher : petit « plop » de bulle ;
// - choc : « splotch » mouillé (bruit filtré + coup sourd), plus fort si le choc est violent, plus grave si le slime est gros ;
// - fusion : « bloup » qui monte quand les deux slimes se touchent (plus grave pour les gros), puis à l'éclat (SlimeFx) une
//   cloche dont la note monte à chaque maillon de la chaîne (gamme pentatonique : toujours juste) ; gros slime : grondement ;
//   longue chaîne : pluie de paillettes ; nouveau record ou niveau max : fanfare.
// Purement audio : événements de la simulation mis en file, joués dans Update ; rien pendant le rejeu accéléré.
[DisallowMultipleComponent]
[DefaultExecutionOrder(960)]
public class SlimeSound : MonoBehaviour
{
    [Header("Liens")]
    [Tooltip("Vide = ceux de la scène.")]
    public BlobDropper dropper;
    public SlimeSkin skin;

    [Header("Volumes")]
    [Range(0f, 1f)] public float master = 0.8f;
    [Range(0f, 1f)] public float dropVolume = 0.4f;
    [Range(0f, 1f)] public float impactVolume = 0.6f;
    [Range(0f, 1f)] public float mergeVolume = 0.85f;
    [Tooltip("Cloche de la chaîne (sa note monte à chaque fusion enchaînée).")]
    [Range(0f, 1f)] public float comboVolume = 0.45f;
    [Tooltip("Grondement des gros slimes, paillettes, fanfare.")]
    [Range(0f, 1f)] public float bigVolume = 0.7f;

    [Header("Hauteur et espace")]
    [Tooltip("Demi-tons plus grave par niveau : un gros slime sonne plus grave.")]
    [Min(0f)] public float semitonesPerLevel = 1.5f;
    [Tooltip("Variation aléatoire de hauteur (demi-tons) pour qu'un même son ne se répète pas à l'identique.")]
    [Range(0f, 3f)] public float jitter = 0.7f;
    [Tooltip("Son placé à gauche / à droite selon la position à l'écran (0 = centré).")]
    [Range(0f, 1f)] public float stereo = 0.5f;
    [Tooltip("Vitesse de choc (km/h) en dessous de laquelle un impact est silencieux (les petits tassements de la pile).")]
    [SpeedKmh] [Min(0f)] public float softImpact = 3.5f;
    [Tooltip("Vitesse de choc (km/h) d'un impact au volume max.")]
    [SpeedKmh] [Min(0.1f)] public float loudImpact = 10f;
    [Tooltip("Grondement à partir de ce niveau.")]
    [Min(0)] public int boomLevel = 6;
    [Tooltip("Voix simultanées max (au-delà, la plus ancienne est coupée).")]
    [Range(4, 32)] public int voices = 16;

    const int Rate = 44100;
    static readonly int[] Ladder = { 0, 2, 4, 7, 9, 12, 14, 16, 19 };

    struct Request
    {
        public int kind, level, combo;
        public float x, strength;
        public bool record;
    }

    BlobFusion _fusion;
    SlimeScore _score;
    SlimeFx _fx;
    AudioClip _plop, _bloop, _ding, _boom, _sparkle, _fanfare;
    AudioClip[] _splats;
    AudioSource[] _sources;
    float[] _started;
    GameObject _voices;
    AudioListener _listener;
    readonly List<Request> _requests = new List<Request>();
    System.Random _rand = new System.Random(5);
    int _impactsThisFrame, _splitsThisFrame;

    void OnEnable()
    {
        if (dropper == null) dropper = FindAnyObjectByType<BlobDropper>();
        if (skin == null) skin = FindAnyObjectByType<SlimeSkin>();
        if (dropper != null)
        {
            _fusion = dropper.GetComponent<BlobFusion>();
            _score = dropper.GetComponent<SlimeScore>();
            dropper.Dropped += OnDropped;
        }
        if (_score != null) _score.Scored += OnScored;
        if (skin != null) skin.Impact += OnImpact;
        _fx = FindAnyObjectByType<SlimeFx>();
        if (_fx != null) _fx.Popped += OnPopped;

        Synthesize();
        _voices = new GameObject("Voix (SlimeSound)") { hideFlags = HideFlags.HideAndDontSave };
        _sources = new AudioSource[voices];
        _started = new float[voices];
        for (int i = 0; i < voices; i++)
        {
            var source = _voices.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            _sources[i] = source;
        }
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null) _listener = Camera.main.gameObject.AddComponent<AudioListener>();
    }

    void OnDisable()
    {
        if (dropper != null) dropper.Dropped -= OnDropped;
        if (_score != null) _score.Scored -= OnScored;
        if (skin != null) skin.Impact -= OnImpact;
        if (_fx != null) _fx.Popped -= OnPopped;
        if (_voices != null) Destroy(_voices);
        if (_listener != null) Destroy(_listener);
        foreach (var clip in new[] { _plop, _bloop, _ding, _boom, _sparkle, _fanfare }) if (clip != null) Destroy(clip);
        if (_splats != null) foreach (var clip in _splats) Destroy(clip);
        _requests.Clear();
    }

    void OnDropped(BLOB blob, int level)
    {
        if (!LevelSimulation.FastForwarding) _requests.Add(new Request { kind = 0, level = level, x = blob.transform.position.x });
    }

    void OnScored(SlimeScore.Hit hit)
    {
        if (!LevelSimulation.FastForwarding)
            _requests.Add(new Request { kind = 1, level = hit.level, combo = hit.combo, x = hit.position.x, record = hit.record });
    }

    // Éclat de la fusion (pendant LateUpdate de SlimeFx, hors simulation) : joué tout de suite.
    void OnPopped(SlimeScore.Hit hit, Vector2 position) => Burst(hit.level, hit.combo, hit.record, position.x);

    void Burst(int level, int combo, bool record, float x)
    {
        int maxLevel = _fusion != null ? _fusion.MaxLevel : 99;
        combo = Mathf.Max(1, combo);
        Play(_ding, comboVolume * (combo == 1 ? 0.55f : 1f), Ladder[Mathf.Min(combo - 1, Ladder.Length - 1)], x);
        if (boomLevel > 0 && level >= boomLevel) Play(_boom, bigVolume * Mathf.Min(1f, 0.45f + 0.06f * level), -(level - boomLevel) * 0.8f, x);
        if (combo >= 3) Play(_sparkle, bigVolume * Mathf.Min(1f, 0.3f + 0.12f * combo), Ladder[Mathf.Min(combo - 3, 4)], x);
        if ((record && level >= 4) || level >= maxLevel) Play(_fanfare, bigVolume, 0f, 0f);
    }

    // Division d'un slime (fin de niveau réussi) : « bloup » aigu, cloche qui monte à chaque génération. Peu par image (des
    // dizaines de slimes se divisent ensemble).
    public void Split(int level, int generation, float x)
    {
        if (_splitsThisFrame >= 3 || _fusion == null) return;
        _splitsThisFrame++;
        Play(_bloop, mergeVolume * 0.6f, 9f - level * semitonesPerLevel + Jitter(), x);
        Play(_ding, comboVolume * 0.5f, Ladder[Mathf.Min(generation, Ladder.Length - 1)], x);
    }

    public void PlayFanfare() => Play(_fanfare, bigVolume, 0f, 0f);

    void OnImpact(BLOB blob, Vector2 point, Vector2 normal, float speed)
    {
        if (speed < softImpact || _impactsThisFrame >= 3 || _fusion == null) return;
        _impactsThisFrame++;
        float strength = Mathf.Clamp01((speed - softImpact) / Mathf.Max(0.1f, loudImpact - softImpact));
        Play(_splats[_rand.Next(_splats.Length)], impactVolume * (0.15f + 0.85f * strength),
            2f - _fusion.LevelOf(blob) * semitonesPerLevel + Jitter(), point.x);
    }

    void Update()
    {
        _impactsThisFrame = 0;
        _splitsThisFrame = 0;
        for (int i = 0; i < _requests.Count; i++)
        {
            var r = _requests[i];
            if (r.kind == 0)
            {
                Play(_plop, dropVolume, 3f - r.level * semitonesPerLevel * 0.6f + Jitter(), r.x);
                continue;
            }
            Play(_bloop, mergeVolume, 5f - r.level * semitonesPerLevel + Jitter() * 0.5f, r.x);
            if (_fx == null || !_fx.isActiveAndEnabled) Burst(r.level, r.combo, r.record, r.x);
        }
        _requests.Clear();
    }

    float Jitter() => jitter * ((float)_rand.NextDouble() * 2f - 1f);

    void Play(AudioClip clip, float volume, float semitones, float x)
    {
        if (clip == null || volume <= 0f || _sources == null) return;
        int pick = 0;
        float oldest = float.MaxValue;
        for (int i = 0; i < _sources.Length; i++)
        {
            if (!_sources[i].isPlaying) { pick = i; oldest = -1f; break; }
            if (_started[i] < oldest) { oldest = _started[i]; pick = i; }
        }
        var source = _sources[pick];
        source.clip = clip;
        source.volume = volume * master;
        source.pitch = Mathf.Clamp(Mathf.Pow(2f, semitones / 12f), 0.1f, 3f);
        float pan = 0f;
        var cam = Camera.main;
        if (cam != null && cam.orthographic) pan = Mathf.Clamp((x - cam.transform.position.x) / (cam.orthographicSize * cam.aspect), -1f, 1f) * stereo;
        source.panStereo = pan;
        source.Play();
        _started[pick] = Time.unscaledTime;
    }

    // ---- Synthèse ----

    void Synthesize()
    {
        _plop = Clip("plop", Plop());
        _splats = new[] { Clip("splotch 1", Splat(11u)), Clip("splotch 2", Splat(23u)), Clip("splotch 3", Splat(37u)) };
        _bloop = Clip("bloup", Bloop());
        _ding = Clip("cloche", Ding());
        _boom = Clip("grondement", Boom());
        _sparkle = Clip("paillettes", Sparkle());
        _fanfare = Clip("fanfare", Fanfare());
    }

    static AudioClip Clip(string name, float[] data)
    {
        Normalize(data, 0.85f);
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }

    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    static float Env(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        return (attack > 0f ? 1f - Mathf.Exp(-t / attack) : 1f) * Mathf.Exp(-t / decay);
    }

    static void Normalize(float[] d, float peak)
    {
        float max = 1e-6f;
        for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
        float k = peak / max;
        int fade = Mathf.Min(d.Length, Rate / 200);
        for (int i = 0; i < d.Length; i++)
        {
            d[i] *= k;
            int left = d.Length - 1 - i;
            if (left < fade) d[i] *= (float)left / fade;
        }
    }

    struct Noise
    {
        uint _s;
        public Noise(uint seed) { _s = seed == 0u ? 1u : seed; }
        public float Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return (_s >> 8) * (2f / 16777216f) - 1f;
        }
    }

    // Filtre d'état (Chamberlin) : passe-bas et passe-bande, fréquence réglable à chaque échantillon.
    struct Filter
    {
        public float low, band;
        public void Run(float input, float cutoff, float damping)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, Rate / 6.5f) / Rate);
            low += f * band;
            float high = input - low - damping * band;
            band += f * high;
        }
    }

    static float[] Plop()
    {
        var d = Buffer(0.16f);
        var noise = new Noise(3u);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float f = 380f + 540f * (1f - Mathf.Exp(-t / 0.025f));
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = Mathf.Sin(phase) * Env(t, 0.002f, 0.045f) + 0.25f * Mathf.Sin(2f * phase) * Env(t, 0.002f, 0.02f)
                   + 0.15f * noise.Next() * Env(t, 0.0003f, 0.004f);
        }
        return d;
    }

    static float[] Splat(uint seed)
    {
        var d = Buffer(0.32f);
        var noise = new Noise(seed);
        var body = new Filter();
        var wet = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float n = noise.Next();
            body.Run(n, 260f + 2600f * Mathf.Exp(-t / 0.035f), 0.6f);
            wet.Run(n, 900f + 400f * Mathf.Sin(2f * Mathf.PI * 38f * t), 0.3f);
            float f = 55f + 110f * Mathf.Exp(-t / 0.03f);
            phase += 2f * Mathf.PI * f / Rate;
            float squelch = wet.band * Env(t - 0.02f, 0.01f, 0.05f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 30f * t));
            d[i] = body.low * Env(t, 0.001f, 0.06f) * 1.2f + Mathf.Sin(phase) * Env(t, 0.002f, 0.07f) * 0.9f + squelch * 0.5f;
        }
        return d;
    }

    static float[] Bloop()
    {
        var d = Buffer(0.55f);
        float phase = 0f, sub = 0f;
        float[] bubbles = { 0.10f, 0.16f, 0.23f };
        float[] bubblePhase = new float[3];
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float f = 210f + 470f * (1f - Mathf.Exp(-t / 0.05f));
            phase += 2f * Mathf.PI * f / Rate;
            sub += 2f * Mathf.PI * (110f + 50f * (1f - Mathf.Exp(-t / 0.04f))) / Rate;
            float s = Mathf.Sin(phase) * Env(t, 0.004f, 0.15f) + 0.3f * Mathf.Sin(2f * phase) * Env(t, 0.004f, 0.08f)
                      + 0.5f * Mathf.Sin(sub) * Env(t, 0.003f, 0.12f);
            for (int b = 0; b < 3; b++)
            {
                float tb = t - bubbles[b];
                if (tb < 0f) continue;
                bubblePhase[b] += 2f * Mathf.PI * (700f + 250f * b + 900f * (1f - Mathf.Exp(-tb / 0.015f))) / Rate;
                s += 0.22f * Mathf.Sin(bubblePhase[b]) * Env(tb, 0.001f, 0.02f);
            }
            d[i] = s;
        }
        return d;
    }

    // Cloche : partiels légèrement inharmoniques qui s'éteignent chacun à leur rythme, plus une octave basse douce.
    static void Bell(float[] d, float start, float freq, float amp, float length)
    {
        float[] ratio = { 1f, 2f, 3.01f, 4.17f, 5.43f };
        float[] gain = { 1f, 0.5f, 0.22f, 0.12f, 0.07f };
        float[] decay = { 0.9f, 0.55f, 0.35f, 0.22f, 0.15f };
        int i0 = Mathf.Max(0, Mathf.RoundToInt(start * Rate));
        for (int i = i0; i < d.Length; i++)
        {
            float t = (float)(i - i0) / Rate;
            float s = 0.25f * Mathf.Sin(Mathf.PI * freq * t) * Env(t, 0.01f, 0.6f * length);
            for (int p = 0; p < ratio.Length; p++) s += gain[p] * Mathf.Sin(2f * Mathf.PI * freq * ratio[p] * t) * Env(t, 0.002f, decay[p] * length);
            d[i] += amp * s;
        }
    }

    static float[] Ding()
    {
        var d = Buffer(1.4f);
        Bell(d, 0f, 523.25f, 1f, 1f);
        return d;
    }

    static float[] Boom()
    {
        var d = Buffer(1.6f);
        var noise = new Noise(91u);
        var whump = new Filter();
        var air = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            phase += 2f * Mathf.PI * (38f + 60f * Mathf.Exp(-t / 0.18f)) / Rate;
            float n = noise.Next();
            whump.Run(n, 120f + 900f * Mathf.Exp(-t / 0.05f), 0.7f);
            air.Run(n, 5000f, 0.4f);
            d[i] = Mathf.Sin(phase) * Env(t, 0.004f, 0.5f) + whump.low * Env(t, 0.003f, 0.18f) * 0.6f + air.band * Env(t, 0.08f, 0.45f) * 0.12f;
        }
        return d;
    }

    static float[] Sparkle()
    {
        var d = Buffer(1.1f);
        var rand = new System.Random(17);
        const int Count = 22;
        for (int k = 0; k < Count; k++)
        {
            float start = 0.6f * Mathf.Pow((float)k / Count, 1.3f) + 0.02f * (float)rand.NextDouble();
            float freq = 1046.5f * Mathf.Pow(2f, Ladder[rand.Next(Ladder.Length)] / 12f);
            float amp = 0.3f * (1f - 0.6f * k / Count), decay = 0.07f + 0.08f * (float)rand.NextDouble();
            int i0 = Mathf.RoundToInt(start * Rate);
            for (int i = i0; i < d.Length; i++)
            {
                float t = (float)(i - i0) / Rate;
                float env = Env(t, 0.001f, decay);
                if (env < 1e-4f && t > 0.01f) break;
                d[i] += amp * env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t));
            }
        }
        return d;
    }

    static float[] Fanfare()
    {
        var d = Buffer(2.4f);
        int[] notes = { 0, 4, 7, 12 };
        for (int k = 0; k < notes.Length; k++) Bell(d, 0.09f * k, 523.25f * Mathf.Pow(2f, notes[k] / 12f), 0.6f, 0.45f);
        for (int k = 0; k < notes.Length; k++) Bell(d, 0.38f, 523.25f * Mathf.Pow(2f, notes[k] / 12f), 0.45f, 1.4f);
        Bell(d, 0.38f, 261.63f, 0.4f, 1.6f);
        return d;
    }
}
