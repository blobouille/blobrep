using UnityEngine;

// Synthèse en temps réel (fil audio) pour GumoriSound : souffle des gumori qui filent, et nappe d'ambiance (coupée par défaut).
// Le fil principal écrit des CIBLES à chaque image ; elles sont lissées ici (aucun craquement). Ajouté par GumoriSound en
// Play, après une AudioSource qui joue du silence en boucle, jamais enregistré.
// Souffle : bruit d'air SANS hauteur (pas de ton : un ton tenu qui ondule sonnait comme un moustique, écarté le 2026-10-24),
// filtré grave ; plus le gumori va vite, plus il s'ouvre ; plus il est gros, plus il est grave.
[AddComponentMenu("")]
public class GumoriSynth : MonoBehaviour
{
    public const int MaxVoices = 4;

    // Cibles : volume du souffle, fréquence de coupure (Hz), panoramique (−1 à 1).
    public readonly float[] air = new float[MaxVoices], cutoff = new float[MaxVoices], pan = new float[MaxVoices];
    // Volume de la nappe, sa note de base (Hz), atténuation pendant les fusions (1 = aucune).
    public float pad, padRoot = 130.81f, duck = 1f;

    const int Table = 2048;
    static readonly float[] Sine = BuildSine();
    static readonly float[] PadRatio = { 1f, 1.0016f, 1.5f, 1.4983f, 2.25f, 2.2462f, 0.5f };
    static readonly float[] PadGain = { 0.5f, 0.5f, 0.32f, 0.32f, 0.16f, 0.16f, 0.35f };

    readonly float[] _air = new float[MaxVoices], _cut = new float[MaxVoices], _pan = new float[MaxVoices],
        _lp1 = new float[MaxVoices], _lp2 = new float[MaxVoices], _g = new float[MaxVoices], _norm = new float[MaxVoices],
        _gl = new float[MaxVoices], _gr = new float[MaxVoices];
    readonly float[] _padPhase = new float[PadRatio.Length];
    float _rate = 48000f, _kFast, _kPad, _pad, _duck = 1f, _breath;
    uint _noise = 0x9E3779B9u;
    AudioClip _silence;

    static float[] BuildSine()
    {
        var t = new float[Table + 1];
        for (int i = 0; i <= Table; i++) t[i] = Mathf.Sin(2f * Mathf.PI * i / Table);
        return t;
    }

    // Phase en tours, positive.
    static float Sin(float phase)
    {
        float x = phase * Table;
        int i = (int)x;
        float f = x - i;
        i &= Table - 1;
        return Sine[i] + (Sine[i + 1] - Sine[i]) * f;
    }

    void Start()
    {
        _rate = AudioSettings.outputSampleRate;
        _kFast = 1f - Mathf.Exp(-1f / (0.06f * _rate));
        _kPad = 1f - Mathf.Exp(-1f / (1.5f * _rate));
        for (int v = 0; v < MaxVoices; v++) _cut[v] = 200f;
        var source = GetComponent<AudioSource>();
        if (source == null) return;
        _silence = AudioClip.Create("silence", 1024, 1, Mathf.Max(8000, (int)_rate), false);
        _silence.hideFlags = HideFlags.HideAndDontSave;
        source.clip = _silence;
        source.loop = true;
        source.priority = 0;
        source.volume = 1f;
        source.Play();
    }

    void OnDestroy()
    {
        if (_silence != null) Destroy(_silence);
    }

    float Noise()
    {
        _noise ^= _noise << 13;
        _noise ^= _noise >> 17;
        _noise ^= _noise << 5;
        return (_noise >> 8) * (2f / 16777216f) - 1f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        int frames = data.Length / channels;
        float inv = 1f / _rate;
        float padTarget = pad, duckTarget = duck, padRoot0 = padRoot;
        // Coupure et placement : lents, une fois par tampon. Le bruit filtré plus bas a moins d'énergie : compensé.
        for (int v = 0; v < MaxVoices; v++)
        {
            _cut[v] += (Mathf.Clamp(cutoff[v], 60f, 2000f) - _cut[v]) * 0.25f;
            _g[v] = 1f - Mathf.Exp(-2f * Mathf.PI * _cut[v] * inv);
            _norm[v] = 5f * Mathf.Sqrt(400f / _cut[v]);
            _pan[v] += (pan[v] - _pan[v]) * 0.3f;
            _gl[v] = 0.7071f * Mathf.Sqrt(1f - _pan[v]);
            _gr[v] = 0.7071f * Mathf.Sqrt(1f + _pan[v]);
        }
        for (int i = 0; i < frames; i++)
        {
            float l = 0f, r = 0f;
            for (int v = 0; v < MaxVoices; v++)
            {
                float target = air[v];
                if (_air[v] < 1e-5f && target < 1e-5f) continue;
                _air[v] += (target - _air[v]) * _kFast;
                float g = _g[v];
                _lp1[v] += g * (Noise() - _lp1[v]);
                _lp2[v] += g * (_lp1[v] - _lp2[v]);
                float s = _lp2[v] * _norm[v] * _air[v];
                l += s * _gl[v];
                r += s * _gr[v];
            }

            _pad += (padTarget - _pad) * _kPad;
            if (_pad > 1e-5f)
            {
                _breath += 0.055f * inv;
                if (_breath >= 1f) _breath -= 1f;
                float breath = _pad * (0.78f + 0.22f * Sin(_breath));
                float pl = 0f, pr = 0f;
                for (int k = 0; k < PadRatio.Length; k++)
                {
                    float ph = _padPhase[k] + padRoot0 * PadRatio[k] * inv;
                    ph -= (int)ph;
                    _padPhase[k] = ph;
                    float s = PadGain[k] * (Sin(ph) + 0.2f * Sin(2f * ph));
                    if ((k & 1) == 0) pl += s; else pr += s;
                    if (k == PadRatio.Length - 1) pr += s;
                }
                l += pl * breath;
                r += pr * breath;
            }

            if (float.IsNaN(l) || float.IsNaN(r))
            {
                System.Array.Clear(_lp1, 0, MaxVoices);
                System.Array.Clear(_lp2, 0, MaxVoices);
                l = r = 0f;
            }
            _duck += (duckTarget - _duck) * _kFast;
            l = SoftClip(l * _duck);
            r = SoftClip(r * _duck);
            int o = i * channels;
            if (channels == 1) data[o] = 0.5f * (l + r);
            else
            {
                data[o] = l;
                data[o + 1] = r;
                for (int c = 2; c < channels; c++) data[o + c] = 0f;
            }
        }
    }

    static float SoftClip(float x)
    {
        x = Mathf.Clamp(x, -3f, 3f);
        return x * (27f + x * x) / (27f + 9f * x * x);
    }
}
