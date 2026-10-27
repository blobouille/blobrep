// Contact entre les capsules d'un même gumori (selfCollision, 2026-10-27) : partie du bot du niveau à bocaux (visée de
// fold_shot.cs), avec ou sans. Mesure, tous les 2 pas et pour chaque gumori hors fusion : anneau qui se recoupe (retourné sur
// lui-même), et enfoncement des parois (deux capsules NON voisines dont les axes sont à moins de 2 épaisseurs : profondeur en
// épaisseurs). Coût par pas, hash (2 essais). Paramètres (self_bench.txt) : "0|1 gap pas". Remet le réglage du modèle après.
var args = System.IO.File.ReadAllText("Captures/bancs/self_bench.txt").Trim().Split(' ');
bool on = args[0] == "1"; int gap = int.Parse(args[1]), total = int.Parse(args[2]);
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var lv = d.GetComponent<SpaceLevel>();
var jars = UnityEngine.Object.FindAnyObjectByType<GumoriJars>();
var one = new System.Collections.Generic.List<uint> { 0u };
float g = 4f;
bool was = d.template.selfCollision;
d.template.selfCollision = on;
bool Crossed(Gumori b)
{
    int n = b.CapsuleCount;
    for (int i = 0; i < n; i++)
    {
        Vector2 a = b.Capsule(i).position, c = b.Capsule((i + 1) % n).position;
        for (int j = i + 2; j < n; j++)
        {
            if (i == 0 && j == n - 1) continue;
            Vector2 p = b.Capsule(j).position, q = b.Capsule((j + 1) % n).position;
            float d1 = (c.x - a.x) * (p.y - a.y) - (c.y - a.y) * (p.x - a.x), d2 = (c.x - a.x) * (q.y - a.y) - (c.y - a.y) * (q.x - a.x);
            float d3 = (q.x - p.x) * (a.y - p.y) - (q.y - p.y) * (a.x - p.x), d4 = (q.x - p.x) * (c.y - p.y) - (q.y - p.y) * (c.x - p.x);
            if (d1 * d2 < 0f && d3 * d4 < 0f) return true;
        }
    }
    return false;
}
void Shoot()
{
    int bestSlot = -1; Vector2 bestTarget = default; float bestCost = float.MaxValue;
    for (int s = 0; s < l.Rack.Count; s++)
    {
        var gm = l.Rack[s]; if (gm == null || gm.CapsuleCount == 0) continue;
        int lvl = f.LevelOf(gm);
        foreach (var t in d.InPlay)
        {
            bool inRack = false; foreach (var r in l.Rack) if (r == t) inRack = true;
            if (inRack || f.LevelOf(t) != lvl) continue;
            float near = float.MaxValue;
            foreach (var st in jars.States) if (!st.broken) near = Mathf.Min(near, Vector2.Distance(st.center, t.Center) - st.radius);
            float cost = near + 0.2f * Vector2.Distance(gm.Center, t.Center);
            if (cost < bestCost) { bestCost = cost; bestSlot = s; bestTarget = t.Center; }
        }
    }
    if (bestSlot < 0)
    {
        bestSlot = 0; float nd = float.MaxValue;
        foreach (var st in jars.States) if (!st.broken) { float dd = Vector2.Distance(st.center, l.Rack[0].Center); if (dd < nd) { nd = dd; bestTarget = st.center + Vector2.up * (st.radius + 0.6f); } }
    }
    var from = l.Rack[bestSlot].Center;
    float dist = Vector2.Distance(from, bestTarget);
    float speed = Mathf.Clamp(dist * 0.9f, 1.5f, 4.4f);
    float time = dist / speed;
    l.Launch(bestSlot, ((bestTarget + Vector2.up * (0.5f * g * time * time)) - from).normalized * speed);
}
string Run()
{
    LevelRestart.Restart();
    var sw = new System.Diagnostics.Stopwatch();
    int samples = 0, crossed = 0, deep = 0; float worst = 0f, sumDepth = 0f; int depthCount = 0;
    while (lv.Result == GumoriGoal.State.Playing && LevelSimulation.Step < total)
    {
        if (LevelSimulation.Step % gap == gap - 1 && l.ShotsLeft > 0 && l.Rack.Count > 0) Shoot();
        sw.Start(); LevelSimulation.FastForward(one, 1); sw.Stop();
        if (LevelSimulation.Step % 2 != 0) continue;
        foreach (var b in d.InPlay)
        {
            if (f.IsMerging(b)) continue;
            int n = b.CapsuleCount; float r = b.CapsuleThickness, depth = 0f;
            samples++;
            if (Crossed(b)) crossed++;
            for (int i = 0; i < n; i++)
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    float dist = (b.Capsule(i).position - b.Capsule(j).position).magnitude;
                    depth = Mathf.Max(depth, (2f * r - dist) / r);
                }
            if (depth > 0.05f) { sumDepth += depth; depthCount++; }
            if (depth > 1f) deep++;
            worst = Mathf.Max(worst, depth);
        }
    }
    ulong h = 1469598103934665603UL;
    foreach (var b in d.InPlay) for (int k = 0; k < b.CapsuleCount; k++) { var p = b.Capsule(k).position; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL; }
    return $"{(on ? "AVEC" : "SANS")} pas={LevelSimulation.Step} fusions={f.Merges} bocaux={jars.Remaining}/{jars.Total} res={lv.Result} | anneaux retournés {100f * crossed / Mathf.Max(1, samples):0.00}% des relevés, parois enfoncées de plus d'1 épaisseur {100f * deep / Mathf.Max(1, samples):0.0}%, enfoncement moyen (quand > 0) {sumDepth / Mathf.Max(1, depthCount):0.00}, pire {worst:0.00} épaisseur | {sw.Elapsed.TotalMilliseconds / Mathf.Max(1, LevelSimulation.Step):0.000} ms/pas hash={h:X16}";
}
var res = Run() + "\n" + Run();
d.template.selfCollision = was;
return res;
