using System.Collections.Generic;
using UnityEngine;

// Sound design procédural : tous les sons sont calculés (aucun fichier audio). DA : grave, rond, apaisant, satisfaisant ;
// peu d'aigus. Tous les sons accordés sont dans UNE gamme (pentatonique majeure de `key`) : ensemble, ils sonnent toujours
// juste, jamais un brouhaha.
// - lâcher : petit « plop » de bulle ; tir (Space) : « fwoump » de souffle, plus fort si le tir est rapide ;
// - choc : « splotch » sourd quand il est doux, plus mouillé quand il est violent ; plus grave si le gumori est gros ;
// - fusion : « bloup » (plus grave pour les gros) ; la CHAÎNE fait monter sa note d'un cran par maillon et lui ajoute des
//   couches (octave grave, puis harmonie), façon série de kills ; à l'éclat (SlimeFx) un « doum » grave et doux sur la même
//   note ; gros gumori : grondement ; longue chaîne : cascade de bulles ; record ou niveau max : accord de fête ;
// - gelée (SlimeSynth, temps réel) : chaque gumori qui bouge (trajet, tremblements une fois posé) a une voix grave qui suit
//   ce qu'on voit (forme lue par SlimeSkin) : plus il tremble, plus fort ; plus il s'étire, plus le son s'ouvre ; plus il file
//   vite, plus il souffle ; note selon sa taille ; seuls les plus agités sonnent ;
// - ambiance : nappe grave qui respire, un peu plus présente quand tout bouge.
// Purement audio : événements de la simulation mis en file, joués hors simulation ; rien pendant le rejeu accéléré.
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
    [Tooltip("« Bloup » de la fusion (et ses couches quand la chaîne monte).")]
    [Range(0f, 1f)] public float mergeVolume = 0.85f;
    [Tooltip("« Doum » grave à l'éclat de la fusion (sa note monte avec la chaîne).")]
    [Range(0f, 1f)] public float comboVolume = 0.45f;
    [Tooltip("Grondement des gros gumori, cascade de bulles (longue chaîne), accord de fête (record).")]
    [Range(0f, 1f)] public float bigVolume = 0.7f;
    [Tooltip("Souffle d'un tir (scène Space).")]
    [Range(0f, 1f)] public float launchVolume = 0.5f;

    [Header("Hauteur et espace")]
    [Tooltip("Tonalité de tous les sons accordés (fusions, gelée, nappe), en demi-tons au-dessus de do.")]
    [Range(-6, 6)] public int key;
    [Tooltip("Notes de la gamme plus grave par niveau pour les sons accordés : un gros gumori sonne plus grave.")]
    [Range(0f, 2f)] public float stepsPerLevel = 0.7f;
    [Tooltip("Demi-tons plus grave par niveau pour les bruits (plop, splotch).")]
    [Min(0f)] public float semitonesPerLevel = 1.5f;
    [Tooltip("Variation aléatoire de hauteur des bruits (demi-tons) pour qu'un même son ne se répète pas à l'identique.\n" +
             "Les sons accordés restent justes.")]
    [Range(0f, 3f)] public float jitter = 0.7f;
    [Tooltip("Son placé à gauche / à droite selon la position à l'écran (0 = centré).")]
    [Range(0f, 1f)] public float stereo = 0.5f;
    [Tooltip("Vitesse de choc (km/h) en dessous de laquelle un impact est silencieux (les petits tassements de la pile).")]
    [SpeedKmh] [Min(0f)] public float softImpact = 3.5f;
    [Tooltip("Vitesse de choc (km/h) d'un impact au volume max.")]
    [SpeedKmh] [Min(0.1f)] public float loudImpact = 10f;
    [Tooltip("Grondement à partir de ce niveau.")]
    [Min(0)] public int boomLevel = 6;
    [Tooltip("Voix simultanées max des sons ponctuels (au-delà, la plus ancienne est coupée).")]
    [Range(4, 32)] public int voices = 16;

    [Header("Gelée qui bouge (temps réel)")]
    [Tooltip("Volume des voix de gelée. 0 = coupé.")]
    [Range(0f, 1f)] public float wobbleVolume = 0.5f;
    [Tooltip("Tremblement (changement de forme par seconde, 1/s) en dessous duquel un gumori est silencieux : les petits\n" +
             "frémissements d'une pile au repos.")]
    [Min(0f)] public float wobbleGate = 0.12f;
    [Tooltip("Tremblement (1/s) au volume max.")]
    [Min(0.01f)] public float wobbleFull = 2.5f;
    [Tooltip("Vitesse (km/h) d'un gumori qui file au souffle max (trajet). 0 = la vitesse ne fait aucun son.")]
    [SpeedKmh] [Min(0f)] public float travelFull = 8f;
    [Tooltip("Gumori qui sonnent en même temps (les plus agités, les gros d'abord).")]
    [Range(1, SlimeSynth.MaxVoices)] public int wobbleVoices = 4;
    [Tooltip("Part de clapotis mouillé dans les voix de gelée.")]
    [Range(0f, 1f)] public float slosh = 0.3f;
    [Tooltip("Baisse des voix de gelée et de la nappe pendant un son de fusion (pour qu'il ressorte), 0 = aucune.")]
    [Range(0f, 1f)] public float duckDepth = 0.45f;

    [Header("Ambiance")]
    [Tooltip("Nappe grave et douce, dans la gamme des fusions ; elle respire et gonfle un peu quand tout bouge. 0 = coupée.")]
    [Range(0f, 1f)] public float ambience = 0.15f;

    const int Rate = 44100;
    static readonly int[] Penta = { 0, 2, 4, 7, 9 };

    struct Request
    {
        public int kind, level, combo;
        public float x, strength;
        public bool record;
    }

    struct Candidate
    {
        public int index;
        public float score;
    }

    BlobFusion _fusion;
    SlimeScore _score;
    SlimeFx _fx;
    BlobLauncher _launcher;
    AudioClip _plop, _bloop, _doum, _boom, _bubbles, _fanfare, _whoosh;
    AudioClip[] _splats, _softSplats;
    AudioSource[] _sources;
    float[] _started;
    GameObject _voices;
    AudioListener _listener;
    SlimeSynth _synth;
    readonly List<Request> _requests = new List<Request>();
    readonly List<Candidate> _candidates = new List<Candidate>();
    readonly int[] _owner = new int[SlimeSynth.MaxVoices];
    readonly bool[] _kept = new bool[SlimeSynth.MaxVoices];
    readonly float[] _freeAt = new float[SlimeSynth.MaxVoices];
    static readonly System.Comparison<Candidate> ByScore = (a, b) => b.score.CompareTo(a.score);
    System.Random _rand = new System.Random(5);
    int _impactsThisFrame, _splitsThisFrame;
    float _duckTime;

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
        _launcher = FindAnyObjectByType<BlobLauncher>();
        if (_launcher != null) _launcher.Launched += OnLaunched;

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
        var live = new GameObject("Gelée (SlimeSound)") { hideFlags = HideFlags.HideAndDontSave };
        live.transform.SetParent(_voices.transform);
        var liveSource = live.AddComponent<AudioSource>();
        liveSource.playOnAwake = false;
        liveSource.spatialBlend = 0f;
        _synth = live.AddComponent<SlimeSynth>();
        System.Array.Clear(_owner, 0, _owner.Length);
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null) _listener = Camera.main.gameObject.AddComponent<AudioListener>();
    }

    void OnDisable()
    {
        if (dropper != null) dropper.Dropped -= OnDropped;
        if (_score != null) _score.Scored -= OnScored;
        if (skin != null) skin.Impact -= OnImpact;
        if (_fx != null) _fx.Popped -= OnPopped;
        if (_launcher != null) _launcher.Launched -= OnLaunched;
        if (_voices != null) Destroy(_voices);
        if (_listener != null) Destroy(_listener);
        foreach (var clip in new[] { _plop, _bloop, _doum, _boom, _bubbles, _fanfare, _whoosh }) if (clip != null) Destroy(clip);
        if (_splats != null) foreach (var clip in _splats) Destroy(clip);
        if (_softSplats != null) foreach (var clip in _softSplats) Destroy(clip);
        _requests.Clear();
        _synth = null;
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

    void OnLaunched(BLOB blob, Vector2 velocity)
    {
        if (LevelSimulation.FastForwarding || blob == null) return;
        float max = _launcher != null && _launcher.maxSpeed > 0f ? _launcher.maxSpeed : 4.4f;
        _requests.Add(new Request
        {
            kind = 2, level = _fusion != null ? _fusion.LevelOf(blob) : 0, x = blob.transform.position.x,
            strength = Mathf.Clamp01(velocity.magnitude / max)
        });
    }

    // Éclat de la fusion (pendant LateUpdate de SlimeFx, hors simulation) : joué tout de suite.
    void OnPopped(SlimeScore.Hit hit, Vector2 position) => Burst(hit.level, hit.combo, hit.record, position.x);

    // ---- Notes ----

    // Degré n de la gamme pentatonique (do ré mi sol la), en demi-tons au-dessus du do de référence (négatif = plus grave).
    static int Degree(int n)
    {
        int octave = n >= 0 ? n / 5 : -((-n + 4) / 5);
        return 12 * octave + Penta[n - 5 * octave];
    }

    // Degré du « bloup » : mi pour un petit gumori seul, plus grave pour les gros, un cran plus haut par maillon de chaîne.
    int MergeDegree(int level, int combo) => 2 - Mathf.RoundToInt(level * stepsPerLevel) + Mathf.Clamp(combo - 1, 0, 8);

    // Transposition (demi-tons) d'un son accordé sur mi (bloup, doum) vers le degré n.
    float Shift(int n) => Degree(n) - 4 + key;

    float Jitter() => jitter * ((float)_rand.NextDouble() * 2f - 1f);
    float Detune() => 0.08f * ((float)_rand.NextDouble() * 2f - 1f);

    void Duck() => _duckTime = 0.7f;

    void Burst(int level, int combo, bool record, float x)
    {
        int maxLevel = _fusion != null ? _fusion.MaxLevel : 99;
        combo = Mathf.Max(1, combo);
        int n = MergeDegree(level, combo);
        Play(_doum, comboVolume * (combo == 1 ? 0.75f : 1f), Shift(n) + Detune(), x);
        if (boomLevel > 0 && level >= boomLevel) Play(_boom, bigVolume * Mathf.Min(1f, 0.45f + 0.06f * level), -(level - boomLevel) * 0.8f, x);
        if (combo >= 3) Play(_bubbles, bigVolume * Mathf.Min(0.8f, 0.25f + 0.1f * combo), key + Degree(Mathf.Min(combo - 3, 5)), x);
        if ((record && level >= 4) || level >= maxLevel) Play(_fanfare, bigVolume, key, 0f);
        Duck();
    }

    // Division d'un gumori (fin de niveau réussi) : « bloup » qui monte d'un cran à chaque génération. Peu par image (des
    // dizaines de gumori se divisent ensemble).
    public void Split(int level, int generation, float x)
    {
        if (_splitsThisFrame >= 3 || _fusion == null) return;
        _splitsThisFrame++;
        Play(_bloop, mergeVolume * 0.6f, Shift(MergeDegree(level, 1) + 2 + generation) + Detune(), x);
    }

    public void PlayFanfare() => Play(_fanfare, bigVolume, key, 0f);

    void OnImpact(BLOB blob, Vector2 point, Vector2 normal, float speed)
    {
        if (speed < softImpact || _impactsThisFrame >= 3 || _fusion == null) return;
        _impactsThisFrame++;
        float strength = Mathf.Clamp01((speed - softImpact) / Mathf.Max(0.1f, loudImpact - softImpact));
        var set = strength < 0.45f ? _softSplats : _splats;
        Play(set[_rand.Next(set.Length)], impactVolume * (0.2f + 0.8f * strength),
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
            if (r.kind == 2)
            {
                Play(_whoosh, launchVolume * (0.3f + 0.7f * r.strength), -r.level * semitonesPerLevel * 0.5f + Jitter() * 0.5f, r.x);
                continue;
            }
            // Série : même bloup, un cran plus haut par maillon, avec des couches en plus (octave grave, puis harmonie).
            int n = MergeDegree(r.level, r.combo);
            Play(_bloop, mergeVolume, Shift(n) + Detune(), r.x);
            if (r.combo >= 2) Play(_bloop, mergeVolume * 0.45f, Shift(n) - 12f + Detune(), r.x);
            if (r.combo >= 3) Play(_bloop, mergeVolume * 0.3f, Shift(n + 2) + Detune(), r.x);
            Duck();
            if (_fx == null || !_fx.isActiveAndEnabled) Burst(r.level, r.combo, r.record, r.x);
        }
        _requests.Clear();
    }

    void LateUpdate() => UpdateJelly();

    // ---- Gelée (temps réel) ----

    // Choisit les gumori les plus agités (les gros d'abord) et donne leurs cibles aux voix de SlimeSynth. Une voix garde son
    // gumori tant qu'il reste parmi les choisis ; libérée, elle se tait avant d'en prendre un autre.
    void UpdateJelly()
    {
        if (_synth == null) return;
        float now = Time.unscaledTime;
        _duckTime = Mathf.Max(0f, _duckTime - Time.unscaledDeltaTime);
        _synth.duck = 1f - duckDepth * Mathf.SmoothStep(0f, 1f, _duckTime / 0.35f);
        _synth.slosh = slosh;
        _synth.padRoot = 130.81f * Mathf.Pow(2f, key / 12f);

        var motions = skin != null ? skin.Motions : null;
        int count = motions != null ? motions.Count : 0;
        float span = Mathf.Max(0.01f, wobbleFull - wobbleGate), total = 0f;
        _candidates.Clear();
        for (int i = 0; i < count; i++)
        {
            var m = motions[i];
            float energy = Mathf.Clamp01((m.wobble - wobbleGate) / span) + Travel(m.speed);
            total += energy;
            if (energy > 0.01f) _candidates.Add(new Candidate { index = i, score = energy * (0.6f + m.radius) });
        }
        _candidates.Sort(ByScore);
        int chosen = Mathf.Min(_candidates.Count, Mathf.Min(wobbleVoices, SlimeSynth.MaxVoices));

        float sum = 0f;
        for (int c = 0; c < chosen; c++)
        {
            var m = motions[_candidates[c].index];
            sum += Mathf.Pow(Mathf.Clamp01((m.wobble - wobbleGate) / span), 1.3f) + 0.5f * Travel(m.speed);
        }
        float norm = wobbleVolume * master * (sum > 1f ? 1f / Mathf.Sqrt(sum) : 1f);

        System.Array.Clear(_kept, 0, _kept.Length);
        for (int c = 0; c < chosen; c++)
        {
            var m = motions[_candidates[c].index];
            int v = System.Array.IndexOf(_owner, m.id);
            if (v < 0)
            {
                for (int k = 0; k < SlimeSynth.MaxVoices; k++)
                    if (_owner[k] == 0 && now >= _freeAt[k]) { v = k; break; }
                if (v < 0) continue;
                _owner[v] = m.id;
                _synth.snap[v] = true;
            }
            _kept[v] = true;
            float w = Mathf.Clamp01((m.wobble - wobbleGate) / span), travel = Travel(m.speed);
            float size = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(m.radius / 2f));
            int n = MergeDegree(m.level, 1);
            _synth.freq[v] = 164.81f * Mathf.Pow(2f, Shift(n) / 12f) * (1f + 0.08f * m.stretch);
            _synth.open[v] = Mathf.Clamp01(0.5f * m.stretch + 0.6f * w + 0.25f * travel);
            _synth.tone[v] = norm * size * Mathf.Pow(w, 1.3f);
            _synth.air[v] = norm * size * 0.5f * travel;
            _synth.pan[v] = Pan(m.center.x);
        }
        for (int v = 0; v < SlimeSynth.MaxVoices; v++)
        {
            if (_kept[v]) continue;
            if (_owner[v] != 0) { _owner[v] = 0; _freeAt[v] = now + 0.12f; }
            _synth.tone[v] = 0f;
            _synth.air[v] = 0f;
        }
        _synth.pad = 0.3f * ambience * master * (0.75f + 0.25f * Mathf.Clamp01(total / 3f));
    }

    float Travel(float speed) => travelFull > 0f ? Mathf.Clamp01((speed - 0.25f) / travelFull) : 0f;

    float Pan(float x)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return 0f;
        return Mathf.Clamp((x - cam.transform.position.x) / (cam.orthographicSize * cam.aspect), -1f, 1f) * stereo;
    }

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
        source.panStereo = Pan(x);
        source.Play();
        _started[pick] = Time.unscaledTime;
    }

    // ---- Synthèse ----

    void Synthesize()
    {
        _plop = Clip("plop", Plop());
        _splats = new[] { Clip("splotch 1", Splat(11u, 1f)), Clip("splotch 2", Splat(23u, 1f)), Clip("splotch 3", Splat(37u, 1f)) };
        _softSplats = new[] { Clip("splotch doux 1", Splat(13u, 0.2f)), Clip("splotch doux 2", Splat(29u, 0.2f)), Clip("splotch doux 3", Splat(41u, 0.2f)) };
        _bloop = Clip("bloup", Bloop());
        _doum = Clip("doum", Doum());
        _boom = Clip("grondement", Boom());
        _bubbles = Clip("bulles", Bubbles());
        _fanfare = Clip("fête", Fanfare());
        _whoosh = Clip("fwoump", Whoosh());
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

    // Choc : coup sourd grave + gelée écrasée (bruit filtré) ; 'wet' 0 = doux et étouffé, 1 = mouillé.
    static float[] Splat(uint seed, float wet)
    {
        var d = Buffer(0.32f);
        var noise = new Noise(seed);
        var body = new Filter();
        var squish = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float n = noise.Next();
            body.Run(n, 170f + (700f + 1300f * wet) * Mathf.Exp(-t / 0.03f), 0.6f);
            squish.Run(n, 450f + 250f * wet + 250f * Mathf.Sin(2f * Mathf.PI * 34f * t), 0.3f);
            float f = 48f + 95f * Mathf.Exp(-t / 0.035f);
            phase += 2f * Mathf.PI * f / Rate;
            float s = squish.band * Env(t - 0.02f, 0.01f, 0.05f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 28f * t));
            d[i] = body.low * Env(t, 0.001f, 0.06f) * (0.7f + 0.4f * wet) + Mathf.Sin(phase) * Env(t, 0.002f, 0.08f) * 1.1f
                   + s * 0.45f * wet;
        }
        return d;
    }

    // Accordé sur mi 5 (fin de la montée) : Shift() le transpose dans la gamme.
    static float[] Bloop()
    {
        const float Tune = 659.26f / 680f;
        var d = Buffer(0.55f);
        float phase = 0f, sub = 0f;
        float[] bubbles = { 0.10f, 0.16f, 0.23f };
        float[] bubblePhase = new float[3];
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float f = (210f + 470f * (1f - Mathf.Exp(-t / 0.05f))) * Tune;
            phase += 2f * Mathf.PI * f / Rate;
            sub += 2f * Mathf.PI * (110f + 50f * (1f - Mathf.Exp(-t / 0.04f))) * Tune / Rate;
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

    // « Doum » : note grave et ronde (mi 3) qui tombe un peu, harmoniques pour qu'on l'entende aussi sur un téléphone.
    static float[] Doum()
    {
        var d = Buffer(0.9f);
        var noise = new Noise(7u);
        var soft = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float f = 164.81f * (1f + 0.18f * Mathf.Exp(-t / 0.03f));
            phase += 2f * Mathf.PI * f / Rate;
            soft.Run(noise.Next(), 600f, 0.8f);
            d[i] = (Mathf.Sin(phase) + 0.4f * Mathf.Sin(2f * phase) * Env(t, 0f, 0.18f) + 0.15f * Mathf.Sin(3f * phase) * Env(t, 0f, 0.1f))
                   * Env(t, 0.006f, 0.32f) + soft.low * Env(t, 0.001f, 0.012f) * 0.6f;
        }
        return d;
    }

    static float[] Boom()
    {
        var d = Buffer(1.6f);
        var noise = new Noise(91u);
        var whump = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            phase += 2f * Mathf.PI * (38f + 60f * Mathf.Exp(-t / 0.18f)) / Rate;
            whump.Run(noise.Next(), 120f + 700f * Mathf.Exp(-t / 0.05f), 0.7f);
            d[i] = Mathf.Sin(phase) * Env(t, 0.004f, 0.5f) + 0.25f * Mathf.Sin(2f * phase) * Env(t, 0.004f, 0.3f)
                   + whump.low * Env(t, 0.003f, 0.18f) * 0.6f;
        }
        return d;
    }

    // Cascade de bulles d'eau qui montent (do 4 à do 5, dans la gamme) : la fête d'une longue chaîne, sans aigus perçants.
    static float[] Bubbles()
    {
        var d = Buffer(1.1f);
        var rand = new System.Random(17);
        const int Count = 14;
        for (int k = 0; k < Count; k++)
        {
            float start = 0.55f * Mathf.Pow((float)k / Count, 1.2f) + 0.02f * (float)rand.NextDouble();
            float freq = 261.63f * Mathf.Pow(2f, Degree(rand.Next(6) + k / 5) / 12f);
            float amp = 0.35f * (1f - 0.5f * k / Count), decay = 0.05f + 0.04f * (float)rand.NextDouble();
            int i0 = Mathf.RoundToInt(start * Rate);
            float phase = 0f;
            for (int i = i0; i < d.Length; i++)
            {
                float t = (float)(i - i0) / Rate;
                float env = Env(t, 0.002f, decay);
                if (env < 1e-4f && t > 0.01f) break;
                phase += 2f * Mathf.PI * freq * (1f + 0.6f * (1f - Mathf.Exp(-t / 0.012f))) / Rate;
                d[i] += amp * env * Mathf.Sin(phase);
            }
        }
        return d;
    }

    // Accord doux qui s'ouvre (do mi sol do + basse), notes égrenées : une fête calme.
    static float[] Fanfare()
    {
        var d = Buffer(2.6f);
        float[] notes = { 130.81f, 261.63f, 329.63f, 392f, 523.25f };
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k == 0 ? 0f : 0.08f * (k - 1);
            int i0 = Mathf.RoundToInt(start * Rate);
            float amp = k == 0 ? 0.7f : 0.45f;
            for (int i = i0; i < d.Length; i++)
            {
                float t = (float)(i - i0) / Rate;
                float ph = 2f * Mathf.PI * notes[k] * t;
                d[i] += amp * Env(t, 0.06f, 0.9f) * (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph) + 0.08f * Mathf.Sin(3f * ph));
            }
        }
        return d;
    }

    // Tir : souffle grave qui s'ouvre et se referme, petit coup sourd au départ.
    static float[] Whoosh()
    {
        var d = Buffer(0.5f);
        var noise = new Noise(53u);
        var air = new Filter();
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            air.Run(noise.Next(), 200f + 650f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f)), 0.9f);
            phase += 2f * Mathf.PI * (60f + 40f * Mathf.Exp(-t / 0.03f)) / Rate;
            d[i] = air.band * Env(t, 0.05f, 0.14f) * 1.4f + Mathf.Sin(phase) * Env(t, 0.003f, 0.07f) * 0.7f;
        }
        return d;
    }
}
