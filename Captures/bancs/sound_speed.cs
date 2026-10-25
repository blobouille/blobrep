// Vitesses (km/h) de chaque gumori vu par GumoriSkin, souffles internes de GumoriSynth, autres sources audio actives.
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var sb = new System.Text.StringBuilder();
foreach (var m in skin.Motions) sb.Append($"{m.id}:{m.speed * 3.6f:0.0} ");
var snd = UnityEngine.Object.FindAnyObjectByType<GumoriSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var synth = (GumoriSynth)typeof(GumoriSound).GetField("_synth", F).GetValue(snd);
var air = (float[])typeof(GumoriSynth).GetField("_air", F).GetValue(synth);
sb.Append("| air interne ");
foreach (var x in air) sb.Append(x.ToString("0.0000")).Append(' ');
sb.Append("| sources ");
foreach (var src in Resources.FindObjectsOfTypeAll<AudioSource>()) if (src.isPlaying) sb.Append(src.gameObject.name).Append(':').Append(src.clip != null ? src.clip.name : "-").Append(' ');
return sb.ToString();
