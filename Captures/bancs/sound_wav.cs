// Écoute hors jeu : rend les sons ponctuels de GumoriSound dans Captures/sons/apercu.wav (mono 44,1 kHz), avec les mêmes
// transpositions qu'en jeu. Ordre : chaîne de 6 fusions (bloup + couches, puis doum à l'éclat), gros gumori (grondement),
// chocs doux puis forts (petit puis gros), tir, cascade de bulles, accord de fête.
var snd = UnityEngine.Object.FindAnyObjectByType<GumoriSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Func<string, AudioClip> C = n => (AudioClip)typeof(GumoriSound).GetField(n, F).GetValue(snd);
System.Func<string, AudioClip[]> CA = n => (AudioClip[])typeof(GumoriSound).GetField(n, F).GetValue(snd);
var mDeg = typeof(GumoriSound).GetMethod("MergeDegree", F);
var mShift = typeof(GumoriSound).GetMethod("Shift", F);
System.Func<int, int, int> Deg = (l, c) => (int)mDeg.Invoke(snd, new object[] { l, c });
System.Func<int, float> Shift = n => (float)mShift.Invoke(snd, new object[] { n });
const int R = 44100;
var outp = new float[R * 22];
void Add(AudioClip clip, float at, float vol, float semis)
{
    var d = new float[clip.samples];
    clip.GetData(d, 0);
    float step = Mathf.Clamp(Mathf.Pow(2f, semis / 12f), 0.1f, 3f);
    int i0 = Mathf.RoundToInt(at * R);
    for (int i = 0; ; i++)
    {
        float src = i * step;
        int k = (int)src;
        if (k + 1 >= d.Length || i0 + i >= outp.Length) break;
        outp[i0 + i] += vol * snd.master * (d[k] + (d[k + 1] - d[k]) * (src - k));
    }
}
var bloop = C("_bloop"); var doum = C("_doum");
float t = 0.2f;
for (int combo = 1; combo <= 6; combo++)
{
    int n = Deg(2, combo);
    Add(bloop, t, snd.mergeVolume, Shift(n));
    if (combo >= 2) Add(bloop, t, snd.mergeVolume * 0.45f, Shift(n) - 12f);
    if (combo >= 3) Add(bloop, t, snd.mergeVolume * 0.3f, Shift(n + 2));
    Add(doum, t + 0.4f, snd.comboVolume * (combo == 1 ? 0.75f : 1f), Shift(n));
    if (combo >= 3) Add(C("_bubbles"), t + 0.4f, snd.bigVolume * Mathf.Min(0.8f, 0.25f + 0.1f * combo), snd.key + (combo - 3 < 5 ? new[] { 0, 2, 4, 7, 9 }[combo - 3] : 12));
    t += 0.9f;
}
t += 0.6f;
{ int n = Deg(8, 1); Add(bloop, t, snd.mergeVolume, Shift(n)); Add(doum, t + 0.4f, snd.comboVolume * 0.75f, Shift(n)); Add(C("_boom"), t + 0.4f, snd.bigVolume * 0.93f, -(8 - snd.boomLevel) * 0.8f); t += 2.2f; }
var soft = CA("_softSplats"); var wet = CA("_splats");
for (int i = 0; i < 3; i++) { Add(soft[i], t, snd.impactVolume * 0.4f, 2f - 1 * snd.semitonesPerLevel); t += 0.45f; }
for (int i = 0; i < 3; i++) { Add(wet[i], t, snd.impactVolume, 2f - 7 * snd.semitonesPerLevel); t += 0.5f; }
t += 0.4f;
Add(C("_whoosh"), t, snd.launchVolume, 0f); t += 0.8f;
Add(C("_whoosh"), t, snd.launchVolume * 0.6f, -2f); t += 1.0f;
Add(C("_fanfare"), t, snd.bigVolume, snd.key); t += 2.8f;
int len = Mathf.Min(outp.Length, Mathf.RoundToInt(t * R));
float peak = 0f;
for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(outp[i]));
var bytes = new byte[44 + len * 2];
void W(int at, string s) { for (int i = 0; i < s.Length; i++) bytes[at + i] = (byte)s[i]; }
void I32(int at, int v) { System.BitConverter.GetBytes(v).CopyTo(bytes, at); }
void I16(int at, short v) { System.BitConverter.GetBytes(v).CopyTo(bytes, at); }
W(0, "RIFF"); I32(4, 36 + len * 2); W(8, "WAVE"); W(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, 1); I32(24, R); I32(28, R * 2); I16(32, 2); I16(34, 16);
W(36, "data"); I32(40, len * 2);
float g = peak > 0.95f ? 0.95f / peak : 1f;
for (int i = 0; i < len; i++) I16(44 + i * 2, (short)Mathf.RoundToInt(Mathf.Clamp(outp[i] * g, -1f, 1f) * 32767f));
System.IO.File.WriteAllBytes("Captures/sons/apercu.wav", bytes);
return $"{len / (float)R:0.0} s, crête {20f * Mathf.Log10(peak):0.0} dB, gain {g:0.00}";
