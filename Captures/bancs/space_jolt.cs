// Coup d'énergie après fusion : pour chaque fusion, hausse max de l'énergie cinétique (½ v² × aire) des slimes hors réserve
// proches, sur 1 s, par rapport à juste avant la fusion. growTime lu dans grow.txt.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var f = d.GetComponent<BlobFusion>();
var foam = d.GetComponent<SlimeFoam>();
foam.growTime = float.Parse(System.IO.File.ReadAllText("Captures/bancs/grow.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
d.seed = 2;
LevelRestart.Restart();
Vector2 C(BLOB b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(BLOB b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var watch = new System.Collections.Generic.List<(int until, Vector2 at, float r, float peak)>();
System.Action<BLOB, int, Vector2> onMerge = (b, lv, at) => watch.Add((LevelSimulation.Step + 60, at, 0.5f * b.diameter + 2f, 0f));
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
var last = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
var peaks = new System.Collections.Generic.List<float>();
while (LevelSimulation.Step < 1200)
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
    var now = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
    foreach (var b in d.Blobs) now[b] = C(b);
    for (int w = watch.Count - 1; w >= 0; w--)
    {
        var ev = watch[w];
        float e = 0f;
        foreach (var b in d.Blobs)
        {
            if (InRack(b) || !last.ContainsKey(b) || (now[b] - ev.at).magnitude > ev.r) continue;
            Vector2 v = (now[b] - last[b]) / Time.fixedDeltaTime;
            e += 0.5f * v.sqrMagnitude * b.diameter * b.diameter;
        }
        ev.peak = Mathf.Max(ev.peak, e);
        if (LevelSimulation.Step >= ev.until) { peaks.Add(ev.peak); watch.RemoveAt(w); } else watch[w] = ev;
    }
    last = now;
}
f.Merged -= onMerge;
peaks.Sort();
float sum = 0f; foreach (var p in peaks) sum += p;
return $"growTime {foam.growTime} | fusions {peaks.Count} | pic d'énergie près d'une fusion : moyen {sum / Mathf.Max(1, peaks.Count):F1}, médian {peaks[peaks.Count / 2]:F1}, max {peaks[peaks.Count - 1]:F1}";
