// Gumori plié (gelée élastique, 2026-10-27) : partie du bot du niveau à bocaux (même visée que level_bot.cs, un tir tous les
// 'gap' pas) ; passe 1 : cherche le pas où un gumori est le plus plié (part de l'aire en triangles retournés de l'éventail
// depuis son centre moyen, anneau des capsules) ; passe 2 : rejoue jusqu'à ce pas + 'offset' et fige le temps pour
// capture_game_view. Paramètres (fold_shot.txt) : "gap rang offset" (rang 0 = le plus plié, 1 = le 2e pli le plus fort…).
var args = System.IO.File.ReadAllText("Captures/bancs/fold_shot.txt").Trim().Split(' ');
int gap = int.Parse(args[0]), rank = int.Parse(args[1]), offset = int.Parse(args[2]);
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
var folds = new System.Collections.Generic.List<(float fold, int step)>();
int Play(int until)
{
    Time.timeScale = 1f;
    LevelRestart.Restart();
    int lastPeak = -1000;
    while (lv.Result == GumoriGoal.State.Playing && LevelSimulation.Step < Mathf.Min(until, 4000))
    {
        if (LevelSimulation.Step % gap == gap - 1 && l.ShotsLeft > 0 && l.Rack.Count > 0) Shoot();
        LevelSimulation.FastForward(one, 1);
        if (until == int.MaxValue)
        {
            float worst = 0f; foreach (var b in d.InPlay) worst = Mathf.Max(worst, Fold(b));
            if (worst > 0.05f)
            {
                if (LevelSimulation.Step - lastPeak > 60) { folds.Add((worst, LevelSimulation.Step)); lastPeak = LevelSimulation.Step; }
                else if (worst > folds[folds.Count - 1].fold) { folds[folds.Count - 1] = (worst, LevelSimulation.Step); lastPeak = LevelSimulation.Step; }
            }
        }
    }
    return LevelSimulation.Step;
}
Play(int.MaxValue);
folds.Sort((a, b) => b.fold.CompareTo(a.fold));
if (folds.Count <= rank) return $"seulement {folds.Count} plis";
int target = folds[rank].step + offset;
Play(target);
Time.timeScale = 0f;
var list = ""; for (int i = 0; i < Mathf.Min(6, folds.Count); i++) list += $" {folds[i].fold:0.00}@{folds[i].step}";
return $"pli {folds[rank].fold:0.00} au pas {folds[rank].step}, figé au pas {LevelSimulation.Step} ; plis :{list}";
