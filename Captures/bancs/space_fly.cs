// Gumori qui « s'envolent » (Space, en Play) : partie scriptée (tirs fixes), chaque montée > 2 m/s d'un gumori hors réserve,
// avec ce qui se passait (sortie SlimeEscape, démêlage SlimeUntangle, fusion récente), et le bilan des sorties / démêlages
// (montée totale, fin par délai encore à cheval). Paramètres : Captures/bancs/fly.txt = "graine pas".
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var f = d.GetComponent<BlobFusion>();
var u = d.GetComponent<SlimeUntangle>();
var e = d.GetComponent<SlimeEscape>();
var args = System.IO.File.ReadAllText("Captures/bancs/fly.txt").Trim().Split(' ');
d.seed = int.Parse(args[0]);
int total = int.Parse(args[1]);
LevelRestart.Restart();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var escList = (System.Collections.IList)typeof(SlimeEscape).GetField("_escapes", bf).GetValue(e);
var untList = (System.Collections.IList)typeof(SlimeUntangle).GetField("_active", bf).GetValue(u);
var escBlob = escList.GetType().GetGenericArguments()[0].GetField("blob");
var untA = untList.GetType().GetGenericArguments()[0].GetField("a");
var untB = untList.GetType().GetGenericArguments()[0].GetField("b");
Vector2 C(BLOB b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(BLOB b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var lastTag = new System.Collections.Generic.Dictionary<BLOB, (string tag, int step)>();
var born = new System.Collections.Generic.Dictionary<BLOB, int>();
System.Action<BLOB, int, Vector2> onMerge = (b, lv, at) => born[b] = LevelSimulation.Step;
f.Merged += onMerge;
var escStart = new System.Collections.Generic.Dictionary<BLOB, float>(); var escStep = new System.Collections.Generic.Dictionary<BLOB, int>();
var untStart = new System.Collections.Generic.Dictionary<BLOB, float>();
var sb = new System.Text.StringBuilder();
int flights = 0, escN = 0, untN = 0, escTimeout = 0, untTimeout = 0;
float escRiseMax = 0, untRiseMax = 0;
var one = new System.Collections.Generic.List<uint> { 0u };
var last = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
var flying = new System.Collections.Generic.HashSet<BLOB>();
bool Overlap(BLOB a, BLOB b)
{
    float g = a.CapsuleThickness + b.CapsuleThickness;
    for (int i = 0; i < a.CapsuleCount; i++)
        for (int j = 0; j < b.CapsuleCount; j++)
            if ((a.Capsule(i).position - b.Capsule(j).position).sqrMagnitude < g * g) return true;
    return false;
}
var escRing = escList.GetType().GetGenericArguments()[0].GetField("ring");
var prevEsc = new System.Collections.Generic.List<(BLOB a, BLOB b)>();
var prevUnt = new System.Collections.Generic.List<(BLOB a, BLOB b)>();
while (LevelSimulation.Step < total)
{
    int s = LevelSimulation.Step;
    if (s % 40 == 39 && l.Rack.Count > 0)
    {
        int i = s / 40;
        float ang = -90f + ((i * 37) % 140 - 70);
        float speed = 2f + (i * 13) % 4;
        l.Launch((i * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
    var curEsc = new System.Collections.Generic.List<(BLOB a, BLOB b)>();
    foreach (var x in escList) curEsc.Add(((BLOB)escBlob.GetValue(x), (BLOB)escRing.GetValue(x)));
    var curUnt = new System.Collections.Generic.List<(BLOB a, BLOB b)>();
    foreach (var x in untList) curUnt.Add(((BLOB)untA.GetValue(x), (BLOB)untB.GetValue(x)));
    foreach (var p in curEsc) { lastTag[p.a] = ("sortie", LevelSimulation.Step); if (!escStart.ContainsKey(p.a)) { escStart[p.a] = C(p.a).y; escStep[p.a] = LevelSimulation.Step; escN++; sb.Append($"  sortie début pas {LevelSimulation.Step} {p.a.name} niv {f.LevelOf(p.a)} dans {p.b.name} niv {f.LevelOf(p.b)} y {C(p.a).y:F2} centre gros y {C(p.b).y:F2}\n"); } }
    foreach (var p in curUnt) { lastTag[p.a] = ("démêlage", LevelSimulation.Step); lastTag[p.b] = ("démêlage(b)", LevelSimulation.Step); if (!untStart.ContainsKey(p.a)) { untStart[p.a] = C(p.a).y; untN++; } }
    foreach (var p in prevEsc)
        if (!curEsc.Contains(p) && p.a != null && p.a.isActiveAndEnabled)
        {
            float rise = C(p.a).y - escStart[p.a]; escStart.Remove(p.a); escRiseMax = Mathf.Max(escRiseMax, rise);
            if (p.b != null && p.b.isActiveAndEnabled && Overlap(p.a, p.b)) escTimeout++;
            sb.Append($"  sortie finie pas {LevelSimulation.Step} {p.a.name} montée {rise:F2} m durée {LevelSimulation.Step - escStep[p.a]} pas, centre dedans {(p.b != null && p.b.isActiveAndEnabled && e.GetType().GetMethod("Inside", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { p.b, C(p.a) }).Equals(true))}\n");
        }
    foreach (var p in prevUnt)
        if (!curUnt.Contains(p) && p.a != null && p.a.isActiveAndEnabled)
        {
            float rise = untStart.ContainsKey(p.a) ? C(p.a).y - untStart[p.a] : 0; untStart.Remove(p.a); untRiseMax = Mathf.Max(untRiseMax, rise);
            if (p.b != null && p.b.isActiveAndEnabled && Overlap(p.a, p.b)) { untTimeout++; sb.Append($"  démêlage rendu à cheval pas {LevelSimulation.Step} {p.a.name}\n"); }
        }
    prevEsc = curEsc; prevUnt = curUnt;
    var now = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
    foreach (var b in d.Blobs) now[b] = C(b);
    foreach (var b in d.Blobs)
    {
        if (!last.ContainsKey(b) || InRack(b)) continue;
        float vy = (now[b].y - last[b].y) / Time.fixedDeltaTime;
        if (vy > 2f && !flying.Contains(b))
        {
            flying.Add(b); flights++;
            string why = lastTag.TryGetValue(b, out var t) && LevelSimulation.Step - t.step < 30 ? $"{t.tag} il y a {LevelSimulation.Step - t.step} pas" : "-";
            string merge = born.TryGetValue(b, out var bs) && LevelSimulation.Step - bs < 90 ? $"né il y a {LevelSimulation.Step - bs} pas" : "";
            sb.Append($"ENVOL pas {LevelSimulation.Step} {b.name} niv {f.LevelOf(b)} y {now[b].y:F1} vy {vy:F1} | {why} {merge}\n");
        }
        else if (vy < 0.5f) flying.Remove(b);
    }
    last = now;
}
f.Merged -= onMerge;
return $"graine {d.seed} : envols {flights}, sorties {escN} (rendues à cheval {escTimeout}, montée max {escRiseMax:F2} m), démêlages {untN} (rendus à cheval {untTimeout}, montée max {untRiseMax:F2} m)\n" + sb;
