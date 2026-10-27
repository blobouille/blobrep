// Tremblement du centre de la gelée (GumoriSkin.FanCenter) sur une partie du bot (level_bot.txt) : centre déplacé de
// combien, et de combien il saute d'un pas à l'autre RELATIVEMENT au corps (= glissement du motif / visage). fan_jitter.txt :
// "sp|surf lissage_s" : sp = contour lissé (Catmull-Rom, 3 points par capsule), surf = points des capsules.
var args = System.IO.File.Exists("Captures/bancs/level_bot.txt") ? System.IO.File.ReadAllText("Captures/bancs/level_bot.txt").Trim().Split(' ') : new string[0];
int gap = args.Length > 0 ? int.Parse(args[0]) : 96;
int passes = 1;
int stopAt = args.Length > 2 ? int.Parse(args[2]) : 999;
int extra = args.Length > 3 ? int.Parse(args[3]) : 0;
float slowAfter = args.Length > 4 ? float.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 1f;
var jargs = System.IO.File.ReadAllText("Captures/bancs/fan_jitter.txt").Trim().Split(" "); bool useSp = jargs[0] == "sp"; float tau = float.Parse(jargs[1], System.Globalization.CultureInfo.InvariantCulture);
var fc = typeof(GumoriSkin).GetMethod("FanCenter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var cr = typeof(GumoriSkin).GetMethod("CatmullRom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var lv = d.GetComponent<SpaceLevel>();
var jars = UnityEngine.Object.FindAnyObjectByType<GumoriJars>();
var mill = UnityEngine.Object.FindAnyObjectByType<GumoriMill>();
var one = new System.Collections.Generic.List<uint> { 0u };
float g = 4f;
var sb = new System.Text.StringBuilder();
string Run(bool log)
{
    LevelRestart.Restart();
    var sw = new System.Diagnostics.Stopwatch();
    float worst = 0f; long pairs = 0, over = 0; int samples = 0, moved = 0; float maxOff = 0f; var jumps = new System.Collections.Generic.List<float>(); var prevOff = new System.Collections.Generic.Dictionary<Gumori, UnityEngine.Vector2>(); var smooth = new System.Collections.Generic.Dictionary<Gumori, UnityEngine.Vector2>(); var shotSteps = new System.Collections.Generic.List<int>(); int cracks = 0; string crackLog = "";
    System.Action<int,int> onCrack = (i, left) => { cracks++; crackLog += $" [{LevelSimulation.Step}:bocal{i} reste{left}]"; };
    jars.Cracked += onCrack;
    float lastAngle = mill.Angle, turned = 0f; int stalls = 0;
    void Step()
    {
        sw.Start(); LevelSimulation.FastForward(one, 1); sw.Stop();
        float a = mill.Angle; float da = UnityEngine.Mathf.DeltaAngle(lastAngle * UnityEngine.Mathf.Rad2Deg, a * UnityEngine.Mathf.Rad2Deg); lastAngle = a; turned += da;
        if (UnityEngine.Mathf.Abs(da) < 0.05f) stalls++;
        foreach (var gm in d.InPlay)
        {
            int m = gm.CapsuleCount; float rc = gm.CapsuleRadius; var cc = gm.Center;
            var ring = new UnityEngine.Vector2[m]; for (int i = 0; i < m; i++) ring[i] = gm.Capsule(i).position;
            var surf = new UnityEngine.Vector2[m];
            for (int i = 0; i < m; i++) { var t = ring[(i + 1) % m] - ring[(i + m - 1) % m]; var nn = new UnityEngine.Vector2(t.y, -t.x); float ln = nn.magnitude; surf[i] = ring[i] + (ln > 1e-6f ? nn / ln : UnityEngine.Vector2.zero) * rc; }
            UnityEngine.Vector2[] poly = surf;
            if (useSp) { poly = new UnityEngine.Vector2[3 * m]; for (int i = 0; i < m; i++) for (int j = 0; j < 3; j++) poly[3 * i + j] = (UnityEngine.Vector2)cr.Invoke(null, new object[] { surf[(i + m - 1) % m], surf[i], surf[(i + 1) % m], surf[(i + 2) % m], j / 3f }); }
            var x = (UnityEngine.Vector2)fc.Invoke(null, new object[] { poly, poly.Length, cc, 0f, 10f / (0.5f * gm.diameter) });
            var off = x - cc;
            if (!smooth.TryGetValue(gm, out var so)) so = off;
            so = UnityEngine.Vector2.Lerp(off, so, tau > 0f ? UnityEngine.Mathf.Exp(-(1f / 60f) / tau) : 0f);
            if (prevOff.TryGetValue(gm, out var po)) { float jump = (so - po).magnitude; jumps.Add(jump); }
            prevOff[gm] = so; smooth[gm] = so;
            samples++; if (so.magnitude > 0.001f) moved++; if (so.magnitude > maxOff) maxOff = so.magnitude;
        }
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
        shots++; shotSteps.Add(LevelSimulation.Step);
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
    jumps.Sort(); float mean = 0f; foreach (var j in jumps) mean += j; mean /= UnityEngine.Mathf.Max(1, jumps.Count);
    return $"centre déplacé {100f * moved / UnityEngine.Mathf.Max(1, samples):0.0} % du temps (max {maxOff:0.00} m) ; saut par pas : moyen {1000f * mean:0.00} mm, 99 % {1000f * (jumps.Count > 0 ? jumps[(int)(0.99f * (jumps.Count - 1))] : 0f):0.0} mm, 99,9 % {1000f * (jumps.Count > 0 ? jumps[(int)(0.999f * (jumps.Count - 1))] : 0f):0.0} mm, max {1000f * (jumps.Count > 0 ? jumps[jumps.Count - 1] : 0f):0} mm | ecart max={worst:0.00} >1,02:{over}/{pairs} res={lv.Result} pas={LevelSimulation.Step} tirs={l.Shots.Count} reste={l.ShotsLeft} bocaux={jars.Remaining}/{jars.Total} fusions={f.Merges} gumori={d.Count} moulin={turned:0}° cale={stalls}pas ms/pas={sw.Elapsed.TotalMilliseconds / UnityEngine.Mathf.Max(1, LevelSimulation.Step):0.000} hash={h:X16}" + (log ? "\n  " + crackLog : "");
}
// Réglages des gumori de la scène gardés tels quels (limite d'étirement, capsules…).
for (int p = 0; p < passes; p++) sb.AppendLine(Run(false));
UnityEngine.Time.timeScale = slowAfter;
return sb.ToString();
