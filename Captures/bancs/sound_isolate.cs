// Isole la saturation : joue un suspect, attend 150 ms (le fil audio tourne), mesure la sortie. Suspect lu dans isolate.txt.
var s = UnityEngine.Object.FindAnyObjectByType<GumoriSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var syn = (GumoriSynth)typeof(GumoriSound).GetField("_synth", F).GetValue(s);
string what = System.IO.File.ReadAllText("Captures/bancs/isolate.txt").Trim();
if (what == "souffle") { syn.air[3] = 0.2f; syn.cutoff[3] = 780f; syn.pan[3] = 0f; }
else
{
    var c = (AudioClip)typeof(GumoriSound).GetField(what, F).GetValue(s);
    typeof(GumoriSound).GetMethod("Play", F).Invoke(s, new object[] { c, 0.5f, 0f, 0f });
}
System.Threading.Thread.Sleep(150);
var a = new float[8192];
AudioListener.GetOutputData(a, 0);
int hot = 0; float peak = 0f; int nan = 0;
foreach (var x in a) { if (float.IsNaN(x)) nan++; else { if (Mathf.Abs(x) > 0.9f) hot++; peak = Mathf.Max(peak, Mathf.Abs(x)); } }
syn.air[3] = 0f;
return $"{what} : saturés {hot}/8192 NaN {nan} crête {peak:0.00}";
