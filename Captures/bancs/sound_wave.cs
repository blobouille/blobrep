// Forme d'onde de la sortie : 24 échantillons espacés, NaN / infinis, valeurs extrêmes ; plus l'état du synthé.
var a = new float[8192];
AudioListener.GetOutputData(a, 0);
int nan = 0, inf = 0; float mn = 0f, mx = 0f;
foreach (var x in a) { if (float.IsNaN(x)) nan++; else if (float.IsInfinity(x)) inf++; else { mn = Mathf.Min(mn, x); mx = Mathf.Max(mx, x); } }
var sb = new System.Text.StringBuilder($"NaN {nan} inf {inf} min {mn:0.000} max {mx:0.000} | ");
for (int i = 0; i < 8192; i += 341) sb.Append(a[i].ToString("0.00")).Append(' ');
var s = UnityEngine.Object.FindAnyObjectByType<SlimeSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var syn = (SlimeSynth)typeof(SlimeSound).GetField("_synth", F).GetValue(s);
sb.Append("| _air ");
foreach (var x in (float[])typeof(SlimeSynth).GetField("_air", F).GetValue(syn)) sb.Append(x.ToString("0.000")).Append(' ');
sb.Append("_lp2 ");
foreach (var x in (float[])typeof(SlimeSynth).GetField("_lp2", F).GetValue(syn)) sb.Append(x.ToString("0.000")).Append(' ');
sb.Append("_duck ").Append(((float)typeof(SlimeSynth).GetField("_duck", F).GetValue(syn)).ToString("0.000"));
sb.Append(" _pad ").Append(((float)typeof(SlimeSynth).GetField("_pad", F).GetValue(syn)).ToString("0.000"));
return sb.ToString();
