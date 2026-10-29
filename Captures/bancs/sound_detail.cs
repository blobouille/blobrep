// Détail du mouvement de chaque gumori vu par GumoriSkin.
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var sb = new System.Text.StringBuilder();
foreach (var m in skin.Motions) sb.Append($"[id{m.id} L{m.level} r{m.radius:0.00} c{m.center} étir {m.stretch:0.00} wob {m.wobble:0.00} v {m.speed:0.00}] ");
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
if (l != null) sb.Append(" tirs " + l.Shots.Count + " slots " + l.SlotCount);
var snd = UnityEngine.Object.FindAnyObjectByType<GumoriSound>();
var f = typeof(GumoriSound).GetField("_synth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
sb.Append(" synth " + (f.GetValue(snd) != null));
return sb.ToString();
