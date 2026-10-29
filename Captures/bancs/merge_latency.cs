// Play (Space) : un bot lance toutes les 'gap' pas un gumori de la réserve sur un pareil. À chaque pas, pour chaque paire de
// même niveau qui se touche « géométriquement » (centres de capsules à moins de 2 × épaisseur × 1,15) : pas jusqu'à la fusion,
// ou séparation sans fusion et pourquoi (paire absente des contacts de GumoriFusion / un des deux encore en fusion).
// Paramètres : merge_latency.txt "gap tirs".
var args = System.IO.File.Exists("Captures/bancs/merge_latency.txt") ? System.IO.File.ReadAllText("Captures/bancs/merge_latency.txt").Trim().Split(' ') : new string[0];
int gap = args.Length > 0 ? int.Parse(args[0]) : 90;
int maxShots = args.Length > 1 ? int.Parse(args[1]) : 25;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var touchField = typeof(GumoriFusion).GetField("_touching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var touching = (System.Collections.IDictionary)touchField.GetValue(f);
var one = new System.Collections.Generic.List<uint> { 0u };
LevelRestart.Restart();
var start = new System.Collections.Generic.Dictionary<(Gumori, Gumori), int>();
var info = new System.Collections.Generic.Dictionary<(Gumori, Gumori), string>();
var lat = new System.Collections.Generic.List<int>();
var log = new System.Text.StringBuilder();
int bounces = 0, bounceNotTracked = 0, bounceFusing = 0, trackedSteps = 0, untrackedSteps = 0;
bool Geo(Gumori a, Gumori b)
{
    float r = 2f * a.CapsuleThickness * 1.15f;
    if ((a.Center - b.Center).magnitude > a.diameter + 0.5f) return false;
    for (int i = 0; i < a.CapsuleCount; i++) { Vector2 p = a.Capsule(i).position; for (int j = 0; j < b.CapsuleCount; j++) if ((p - (Vector2)b.Capsule(j).position).sqrMagnitude < r * r) return true; }
    return false;
}
int shots = 0;
while (shots < maxShots && LevelSimulation.Step < 12000)
{
    for (int k = 0; k < gap; k++)
    {
        LevelSimulation.FastForward(one, 1);
        int step = LevelSimulation.Step;
        var list = d.InPlay;
        var seen = new System.Collections.Generic.HashSet<(Gumori, Gumori)>();
        for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++)
        {
            var a = list[i]; var b = list[j];
            if (a.diameter != b.diameter || a.sides != b.sides) continue;
            if (!Geo(a, b)) continue;
            var key = a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
            seen.Add(key);
            bool tracked = touching.Contains(key);
            bool fusing = f.IsMerging(a) || f.IsMerging(b);
            if (!fusing) { if (tracked) trackedSteps++; else untrackedSteps++; }
            if (!start.ContainsKey(key)) { start[key] = step; info[key] = ""; }
            info[key] = (tracked ? "T" : "-") + (fusing ? "F" : "") ;
        }
        var ended = new System.Collections.Generic.List<(Gumori, Gumori)>();
        foreach (var kv in start) if (!seen.Contains(kv.Key)) ended.Add(kv.Key);
        foreach (var key in ended)
        {
            bool merged = !GumoriDropper.IsInPlay(key.Item1) || !GumoriDropper.IsInPlay(key.Item2);
            int dur = step - start[key];
            if (merged) lat.Add(dur);
            else
            {
                bounces++;
                string last = info[key];
                if (last.Contains("F")) bounceFusing++; else if (!last.StartsWith("T")) bounceNotTracked++;
                log.Append($" [rebond pas {start[key]} durée {dur} dernier={last} niv {f.LevelOf(key.Item1)}]");
            }
            start.Remove(key); info.Remove(key);
        }
    }
    if (l.Rack.Count == 0) continue;
    int bs = -1; Vector2 bt = default; float bc = float.MaxValue;
    for (int s = 0; s < l.Rack.Count; s++)
    {
        var gm = l.Rack[s]; if (gm == null || gm.CapsuleCount == 0) continue;
        foreach (var t in d.InPlay)
        {
            bool inRack = false; foreach (var r in l.Rack) if (r == t) inRack = true;
            if (inRack || t.diameter != gm.diameter) continue;
            float c = Vector2.Distance(gm.Center, t.Center);
            if (c < bc) { bc = c; bs = s; bt = t.Center; }
        }
    }
    if (bs < 0) { bs = 0; bt = new Vector2(0f, 1f); }
    var from = l.Rack[bs].Center; float dist = Vector2.Distance(from, bt); float speed = Mathf.Clamp(dist * 0.9f, 1.5f, 4.4f); float time = dist / speed;
    var aim = bt + Vector2.up * (0.5f * 4f * time * time);
    l.Launch(bs, (aim - from).normalized * speed);
    shots++;
}
lat.Sort();
string Q(float q) => lat.Count == 0 ? "-" : lat[Mathf.Min(lat.Count - 1, (int)(q * lat.Count))].ToString();
return $"fusions {f.Merges} ; contact→fusion (pas) médiane {Q(0.5f)} p90 {Q(0.9f)} max {Q(0.999f)} ; séparés sans fusion {bounces} (dont pendant une fusion {bounceFusing}, absents des contacts {bounceNotTracked}) ; pas en contact non vus par la fusion {untrackedSteps} / vus {trackedSteps}\n" + log;
