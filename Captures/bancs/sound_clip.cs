// Saturation : part des échantillons au-delà de 0,9, et sources en cours (clip, volume, hauteur, position dans le clip).
var a = new float[8192];
AudioListener.GetOutputData(a, 0);
int hot = 0; float peak = 0f;
foreach (var x in a) { if (Mathf.Abs(x) > 0.9f) hot++; peak = Mathf.Max(peak, Mathf.Abs(x)); }
var sb = new System.Text.StringBuilder($"saturés {hot}/8192 crête {peak:0.00} | ");
foreach (var s in Resources.FindObjectsOfTypeAll<AudioSource>())
    if (s.isPlaying) sb.Append($"{(s.clip != null ? s.clip.name : "-")} v{s.volume:0.00} p{s.pitch:0.00} t{s.time:0.00} ; ");
return sb.ToString();
