// Partie jouée par un bot simple (Space, niveau à bocaux) : tous les 'gap' pas, lance un gumori de la réserve vers un pareil
// proche d'un bocal (sinon vers le bocal), visée corrigée de la gravité. Deux fois : compare les hash. Paramètres : level_bot.txt
// "gap passes".
var args = System.IO.File.Exists("Captures/bancs/level_bot.txt") ? System.IO.File.ReadAllText("Captures/bancs/level_bot.txt").Trim().Split(' ') : new string[0];
int gap = args.Length > 0 ? int.Parse(args[0]) : 96;
int passes = args.Length > 1 ? int.Parse(args[1]) : 2;
int stopAt = args.Length > 2 ? int.Parse(args[2]) : 999;
int extra = args.Length > 3 ? int.Parse(args[3]) : 0;
float slowAfter = args.Length > 4 ? float.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 1f;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var lv = UnityEngine.Object.FindAnyObjectByType<SpaceLevel>();
var jars = UnityEngine.Object.FindAnyObjectByType<GumoriJars>();
var mill = UnityEngine.Object.FindAnyObjectByType<GumoriMill>();
var one = new System.Collections.Generic.List<uint> { 0u };
float g = 4f;
var sb = new System.Text.StringBuilder();
string Run(bool log)
{
    LevelRestart.Restart();
    var sw = new System.Diagnostics.Stopwatch();
    int cracks = 0; string crackLog = "";
    System.Action<int,int> onCrack = (i, left) => { cracks++; crackLog += $" [{LevelSimulation.Step}:bocal{i} reste{left}]"; };
    jars.Cracked += onCrack;
    float lastAngle = mill.Angle, turned = 0f; int stalls = 0;
    void Step()
    {
        sw.Start(); LevelSimulation.FastForward(one, 1); sw.Stop();
        float a = mill.Angle; float da = UnityEngine.Mathf.DeltaAngle(lastAngle * UnityEngine.Mathf.Rad2Deg, a * UnityEngine.Mathf.Rad2Deg); lastAngle = a; turned += da;
        if (UnityEngine.Mathf.Abs(da) < 0.05f) stalls++;
    }
    int shots = 0;
    while (lv.Result == GumoriGoal.State.Playing && LevelSimulation.Step < 20000 && shots < stopAt)
    {
        for (int k = 0; k < gap; k++) Step();
        if (l.ShotsLeft <= 0 || l.Rack.Count == 0) { if (l.ShotsLeft <= 0) { for (int k = 0; k < 400 && lv.Result == GumoriGoal.State.Playing; k++) Step(); break; } continue; }
        int bestSlot = -1; UnityEngine.Vector2 bestTarget = default; float bestCost = float.MaxValue;
        for (int s = 0; s < l.Rack.Count; s++)
        {
            var gm = l.Rack[s]; if (gm == null || gm.CapsuleCount == 0) continue;
            int lvl = f.LevelOf(gm);
            foreach (var t in d.InPlay)
            {
                bool inRack = false; foreach (var r in l.Rack) if (r == t) inRack = true;
                if (inRack || f.LevelOf(t) != lvl) continue;
                float near = float.MaxValue;
                foreach (var st in jars.States) if (!st.broken) near = UnityEngine.Mathf.Min(near, UnityEngine.Vector2.Distance(st.center, t.Center) - st.radius);
                float cost = near + 0.2f * UnityEngine.Vector2.Distance(gm.Center, t.Center);
                if (cost < bestCost) { bestCost = cost; bestSlot = s; bestTarget = t.Center; }
            }
        }
        if (bestSlot < 0)
        {
            bestSlot = 0; float nd = float.MaxValue;
            foreach (var st in jars.States) if (!st.broken) { float dd = UnityEngine.Vector2.Distance(st.center, l.Rack[0].Center); if (dd < nd) { nd = dd; bestTarget = st.center + UnityEngine.Vector2.up * (st.radius + 0.6f); } }
        }
        var from = l.Rack[bestSlot].Center;
        float dist = UnityEngine.Vector2.Distance(from, bestTarget);
        float speed = UnityEngine.Mathf.Clamp(dist * 0.9f, 1.5f, 4.4f);
        float time = dist / speed;
        var aim = bestTarget + UnityEngine.Vector2.up * (0.5f * g * time * time);
        l.Launch(bestSlot, (aim - from).normalized * speed);
        shots++;
    }
    for (int k = 0; k < extra; k++) Step();
    jars.Cracked -= onCrack;
    ulong h = 1469598103934665603UL;
    foreach (var b in d.InPlay)
        for (int k = 0; k < b.CapsuleCount; k++)
        {
            var p = b.Capsule(k).position;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
        }
    h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(mill.Angle)) * 1099511628211UL;
    return $"res={lv.Result} pas={LevelSimulation.Step} tirs={l.Shots.Count} reste={l.ShotsLeft} bocaux={jars.Remaining}/{jars.Total} fusions={f.Merges} gumori={d.Count} moulin={turned:0}° cale={stalls}pas ms/pas={sw.Elapsed.TotalMilliseconds / UnityEngine.Mathf.Max(1, LevelSimulation.Step):0.000} hash={h:X16}" + (log ? "\n  " + crackLog : "");
}
for (int p = 0; p < passes; p++) sb.AppendLine(Run(p == 0));
UnityEngine.Time.timeScale = slowAfter;
return sb.ToString();
