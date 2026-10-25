// Partie scriptée de 1200 pas : hauteur max dont montent le slime né et ses voisins dans la seconde après chaque fusion.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var f = d.GetComponent<BlobFusion>();
var soft = d.GetComponent<SlimeSoftMerge>();
soft.enabled = System.IO.File.ReadAllText("Captures/bancs/soft.txt").Trim() == "1";
d.seed = 2;
LevelRestart.Restart();
Vector2 C(BLOB b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(BLOB b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var watch = new System.Collections.Generic.List<(BLOB blob, float y0, int until)>();
int merges = 0;
var logged = new System.Collections.Generic.List<BLOB>(); var log = new System.Text.StringBuilder();
System.Action<BLOB, int, Vector2> onMerge = (b, lv, at) =>
{
    merges++;
    int until = LevelSimulation.Step + 60;
    watch.Add((b, at.y, until));
    foreach (var x in d.Blobs)
    {
        if (x == b) continue;
        float reach = 0.5f * (x.diameter + b.diameter) + 0.4f;
        Vector2 cx = C(x);
        if ((cx - at).magnitude < reach) watch.Add((x, cx.y, until));
    }
};
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
float worst = 0f, sum = 0f; int big = 0, total = 0;
var best = new System.Collections.Generic.Dictionary<BLOB, float>();
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
    for (int w = watch.Count - 1; w >= 0; w--)
    {
        var (b, y0, until) = watch[w];
        bool alive = false; foreach (var x in d.Blobs) if (x == b) alive = true;
        if (!alive || LevelSimulation.Step > until || InRack(b))
        {
            watch.RemoveAt(w);
            continue;
        }
        float rise = C(b).y - y0;
        if (rise > 0.5f && !logged.Contains(b)) { logged.Add(b); log.Append($"[pas {LevelSimulation.Step} {b.name} niv {f.LevelOf(b)} montée {rise:F2} né {(watch.FindIndex(q => q.blob == b) >= 0)} ] "); }
        worst = Mathf.Max(worst, rise);
        if (LevelSimulation.Step == until) { total++; sum += Mathf.Max(0f, rise); if (rise > 0.5f) big++; }
    }
}
f.Merged -= onMerge;
soft.enabled = true;
var esc = d.GetComponent<SlimeEscape>(); var unt = d.GetComponent<SlimeUntangle>();
return log + $" évasions? démêlages {unt.Untangled} | douce {(soft.enabled ? "" : "")}{System.IO.File.ReadAllText("Captures/bancs/soft.txt").Trim()} | fusions {merges} | montée max {worst:F2} m | slimes montés de plus de 0,5 m : {big}/{total}";
