// SlimeSynth seul : coût d'un tampon de 1024 images stéréo (4 souffles actifs) et niveau de sortie (crête, RMS).
var go = new GameObject("banc synth") { hideFlags = HideFlags.HideAndDontSave };
var synth = go.AddComponent<SlimeSynth>();
typeof(SlimeSynth).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(synth, null);
for (int v = 0; v < SlimeSynth.MaxVoices; v++) { synth.air[v] = 0.25f; synth.cutoff[v] = 300f + 150f * v; synth.pan[v] = 0.3f * v - 0.4f; }
var m = typeof(SlimeSynth).GetMethod("OnAudioFilterRead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var data = new float[2048];
var args = new object[] { data, 2 };
float peak = 0f; double sum = 0; int n = 0;
for (int i = 0; i < 100; i++) { m.Invoke(synth, args); if (i >= 20) foreach (var x in data) { peak = Mathf.Max(peak, Mathf.Abs(x)); sum += x * x; n++; } }
double best = 1e9;
var sw = new System.Diagnostics.Stopwatch();
for (int k = 0; k < 20; k++) { sw.Restart(); m.Invoke(synth, args); sw.Stop(); best = System.Math.Min(best, sw.Elapsed.TotalMilliseconds); }
UnityEngine.Object.DestroyImmediate(go);
float rate = AudioSettings.outputSampleRate;
return $"tampon : {best:0.000} ms ({best / (1024.0 / rate * 1000.0) * 100.0:0.0} % d'un cœur) ; crête {20 * Mathf.Log10(peak):0.0} dB, rms {10 * System.Math.Log10(sum / n):0.0} dB";
