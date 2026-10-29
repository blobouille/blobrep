// Comme skin_motion.cs, sans les sondes de GumoriSkin (pour mesurer une ancienne version du script).
// chronométré (comme une image par pas à 60 Hz). Paramètres (skin_motion.txt) : "gap pas".
var args = System.IO.File.ReadAllText("Captures/bancs/skin_motion.txt").Trim().Split(' ');
int gap = int.Parse(args[0]), total = int.Parse(args[1]);
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var late = typeof(GumoriSkin).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var watch = new System.Diagnostics.Stopwatch(); int frames = 0; double worst = 0;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var lv = d.GetComponent<SpaceLevel>();
var jars = UnityEngine.Object.FindAnyObjectByType<GumoriJars>();
var one = new System.Collections.Generic.List<uint> { 0u };
float g = 4f;
float Fold(Gumori b)
{
    int n = b.CapsuleCount; Vector2 c = b.Center; float total = 0f, bad = 0f;
    for (int i = 0; i < n; i++) { Vector2 p = b.Capsule(i).position - c, q = b.Capsule((i + 1) % n).position - c; float a = p.x * q.y - p.y * q.x; total += a; if (a < 0f) bad -= a; }
    return total > 1e-6f ? bad / total : 0f;
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
Time.timeScale = 1f;
LevelRestart.Restart();
while (lv.Result == GumoriGoal.State.Playing && LevelSimulation.Step < total)
{
    if (LevelSimulation.Step % gap == gap - 1 && l.ShotsLeft > 0 && l.Rack.Count > 0) Shoot();
    LevelSimulation.FastForward(one, 1);
    long t0 = watch.ElapsedTicks;
    watch.Start(); late.Invoke(skin, null); watch.Stop();
    worst = System.Math.Max(worst, (watch.ElapsedTicks - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
    frames++;
}
return $"{frames} images : habillage {watch.Elapsed.TotalMilliseconds / frames:0.000} ms par image (pire {worst:0.00}), {d.Count} gumori à la fin";
