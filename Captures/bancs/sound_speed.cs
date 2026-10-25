// Vitesses (km/h) de chaque gumori vu par SlimeSkin, souffles internes de SlimeSynth, autres sources audio actives.
var skin = UnityEngine.Object.FindAnyObjectByType<SlimeSkin>();
var sb = new System.Text.StringBuilder();
foreach (var m in skin.Motions) sb.Append($"{m.id}:{m.speed * 3.6f:0.0} ");
var snd = UnityEngine.Object.FindAnyObjectByType<SlimeSound>();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var synth = (SlimeSynth)typeof(SlimeSound).GetField("_synth", F).GetValue(snd);
var air = (float[])typeof(SlimeSynth).GetField("_air", F).GetValue(synth);
sb.Append("| air interne ");
foreach (var x in air) sb.Append(x.ToString("0.0000")).Append(' ');
sb.Append("| sources ");
foreach (var src in Resources.FindObjectsOfTypeAll<AudioSource>()) if (src.isPlaying) sb.Append(src.gameObject.name).Append(':').Append(src.clip != null ? src.clip.name : "-").Append(' ');
return sb.ToString();
