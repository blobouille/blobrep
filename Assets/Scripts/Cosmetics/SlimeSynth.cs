using UnityEngine;

// Synthèse en temps réel (fil audio) pour SlimeSound : voix de gelée qui suivent le mouvement des gumori, et nappe
// d'ambiance. Le fil principal écrit des CIBLES à chaque image ; elles sont lissées ici à chaque échantillon (aucun
// craquement). Ajouté par SlimeSound en Play, après une AudioSource qui joue du silence en boucle, jamais enregistré.
// Voix : son riche (4 harmoniques) dans un filtre passe-bas résonant, fermé quand le gumori est rond (grave et doux), ouvert
// quand il s'étire (« wouh ») ; plus un bruit filtré : clapotis mouillé quand il tremble, souffle quand il file.
[AddComponentMenu("")]
public class SlimeSynth : MonoBehaviour
{
    public const int MaxVoices = 6;

    // Cibles : volume du ton (tremblement), volume du souffle (vitesse), fréquence (Hz), ouverture du filtre (0–1),
    // panoramique (−1 à 1), saut de fréquence sans glisser (voix qui change de gumori).
    public readonly float[] tone = new float[MaxVoices], air = new float[MaxVoices], freq = new float[MaxVoices],
        open = new float[MaxVoices], pan = new float[MaxVoices];
    public readonly bool[] snap = new bool[MaxVoices];
    // Part de clapotis dans les voix, volume de la nappe, sa note de base (Hz), atténuation pendant les fusions (1 = aucune).
    public float slosh = 0.3f, pad, padRoot = 130.81f, duck = 1f;

    const int Table = 2048;
    static readonly float[] Sine = BuildSine();
    static readonly float[] PadRatio = { 1f, 1.0016f, 1.5f, 1.4983f, 2.25f, 2.2462f, 0.5f };
    static readonly float[] PadGain = { 0.5f, 0.5f, 0.32f, 0.32f, 0.16f, 0.16f, 0.35f };

    readonly float[] _tone = new float[MaxVoices], _air = new float[MaxVoices], _freq = new float[MaxVoices],
        _open = new float[MaxVoices], _pan = new float[MaxVoices], _phase = new float[MaxVoices],
        _low = new float[MaxVoices], _band = new float[MaxVoices], _nLow = new float[MaxVoices], _nBand = new float[MaxVoices],
        _gl = new float[MaxVoices], _gr = new float[MaxVoices];
    readonly float[] _padPhase = new float[PadRatio.Length];
    float _rate = 48000f, _kFast, _kSlow, _kPad, _pad, _duck = 1f, _breath;
    uint _noise = 0x9E3779B9u;
    AudioClip _silence;

    static float[] BuildSine()
    {
        var t = new float[Table + 1];
        for (int i = 0; i <= Table; i++) t[i] = Mathf.Sin(2f * Mathf.PI * i / Table);
        return t;
    }

    // Phase en tours, positive (jusqu'à quelques tours : les harmoniques).
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
        _kFast = 1f - Mathf.Exp(-1f / (0.035f * _rate));
        _kSlow = 1f - Mathf.Exp(-1f / (0.12f * _rate));
        _kPad = 1f - Mathf.Exp(-1f / (1.5f * _rate));
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
        float inv = 1f / _rate, w = 2f * Mathf.PI * inv;
        float padTarget = pad, duckTarget = duck, sloshAmount = slosh, padRoot0 = padRoot;
        for (int v = 0; v < MaxVoices; v++)
        {
            _pan[v] += (pan[v] - _pan[v]) * 0.3f;
            _gl[v] = 0.7071f * Mathf.Sqrt(1f - _pan[v]);
            _gr[v] = 0.7071f * Mathf.Sqrt(1f + _pan[v]);
        }
        for (int i = 0; i < frames; i++)
        {
            float l = 0f, r = 0f;
            for (int v = 0; v < MaxVoices; v++)
            {
                float tTone = tone[v], tAir = air[v];
                if (_tone[v] < 1e-5f && _air[v] < 1e-5f && tTone < 1e-5f && tAir < 1e-5f) continue;
                if (snap[v]) { _freq[v] = freq[v]; _open[v] = open[v]; snap[v] = false; }
                _tone[v] += (tTone - _tone[v]) * _kFast;
                _air[v] += (tAir - _air[v]) * _kFast;
                _freq[v] += (freq[v] - _freq[v]) * _kSlow;
                _open[v] += (open[v] - _open[v]) * _kSlow;

                float ph = _phase[v] + _freq[v] * inv;
                ph -= (int)ph;
                _phase[v] = ph;
                float src = 0.5f * (Sin(ph) + 0.55f * Sin(2f * ph) + 0.3f * Sin(3f * ph) + 0.15f * Sin(4f * ph));
                float g = Mathf.Min(0.9f, w * _freq[v] * (1.1f + 5f * _open[v]));
                _low[v] += g * _band[v];
                _band[v] += g * (src - _low[v] - 0.7f * _band[v]);
                float s = _low[v] * _tone[v];

                float gn = Mathf.Min(0.9f, w * (180f + 700f * Mathf.Min(1f, 2f * _air[v]) + 400f * _open[v]));
                _nLow[v] += gn * _nBand[v];
                _nBand[v] += gn * (Noise() - _nLow[v] - 1.2f * _nBand[v]);
                s += _nBand[v] * (sloshAmount * _tone[v] + _air[v]) * 0.6f;

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
