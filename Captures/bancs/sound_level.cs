// Niveau du mélange réellement entendu (sortie de l'AudioListener, ~0,19 s) en dBFS, sons ponctuels en cours, souffles.
var a = new float[8192]; var b = new float[8192];
AudioListener.GetOutputData(a, 0); AudioListener.GetOutputData(b, 1);
double s = 0; float peak = 0f;
for (int i = 0; i < a.Length; i++) { s += a[i] * a[i] + b[i] * b[i]; peak = Mathf.Max(peak, Mathf.Max(Mathf.Abs(a[i]), Mathf.Abs(b[i]))); }
float rms = Mathf.Sqrt((float)(s / (2 * a.Length)));
var snd = UnityEngine.Object.FindAnyObjectByType<GumoriSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var synth = (GumoriSynth)typeof(GumoriSound).GetField("_synth", F).GetValue(snd);
var sources = (AudioSource[])typeof(GumoriSound).GetField("_sources", F).GetValue(snd);
var sb = new System.Text.StringBuilder();
sb.Append($"rms {20f * Mathf.Log10(rms + 1e-9f):0.0} crête {20f * Mathf.Log10(peak + 1e-9f):0.0} | ");
foreach (var src in sources) if (src.isPlaying) sb.Append(src.clip.name).Append(' ');
sb.Append("| souffle ");
if (synth != null) for (int v = 0; v < GumoriSynth.MaxVoices; v++) sb.Append($"{synth.air[v]:0.00}@{synth.cutoff[v]:0} ");
return sb.ToString();
