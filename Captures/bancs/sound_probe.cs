// Son de la gelée : mouvement vu par GumoriSkin (tremblement, étirement, vitesse) et cibles des voix de GumoriSynth.
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var synth = UnityEngine.Object.FindAnyObjectByType<GumoriSynth>();
var sb = new System.Text.StringBuilder();
float maxW = 0f, sumW = 0f, maxS = 0f, maxV = 0f;
foreach (var m in skin.Motions)
{
    maxW = Mathf.Max(maxW, m.wobble); sumW += m.wobble; maxS = Mathf.Max(maxS, m.stretch); maxV = Mathf.Max(maxV, m.speed);
}
int n = skin.Motions.Count;
sb.Append($"n {n} wob max {maxW:0.00} moy {(n > 0 ? sumW / n : 0):0.00} étir max {maxS:0.00} vit max {maxV * 3.6f:0.0} km/h | ");
var ws = new System.Collections.Generic.List<float>();
foreach (var m in skin.Motions) ws.Add(m.wobble);
ws.Sort();
sb.Append("wob triés ");
foreach (var w in ws) sb.Append(w.ToString("0.00")).Append(' ');
if (synth != null)
{
    sb.Append("| voix ");
    for (int v = 0; v < GumoriSynth.MaxVoices; v++) sb.Append($"[{synth.tone[v]:0.00}/{synth.air[v]:0.00} {synth.freq[v]:0}Hz o{synth.open[v]:0.0}] ");
    sb.Append($"nappe {synth.pad:0.000} duck {synth.duck:0.00}");
}
return sb.ToString();
