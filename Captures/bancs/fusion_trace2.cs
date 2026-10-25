// Diagnostic de l'absorption (Space) : pour les 10 premiers croisements d'anneau en fusion, pas depuis la naissance, capsules
// qui se croisent (écart le long de l'anneau), plus grand coude réel (°) près du croisement, étalement des vitesses à la
// naissance (m/s autour de la moyenne). Même partie scriptée que fusion_compare.cs, réglages par fusion_style.txt ignorés.
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var l = d.GetComponent<GumoriLauncher>();
d.seed = 5;
f.style = FusionStyle.Absorb;
f.inheritMotion = 1f; f.absorbStiffness = 1f; f.absorbSmoothing = 4; f.absorbNeck = 0.5f; f.absorbTime = 0.9f;
var one = new System.Collections.Generic.List<uint> { 0u };
var born = new System.Collections.Generic.Dictionary<Gumori, (int step, float spread)>();
var done = new System.Collections.Generic.HashSet<Gumori>();
var log = new System.Text.StringBuilder();
int found = 0;
System.Action<Gumori, int, Vector2> onMerged = (m, lv, p) =>
{
    Vector2 v = m.Velocity; float spread = 0f;
    for (int k = 0; k < m.CapsuleCount; k++) spread = Mathf.Max(spread, (m.Capsule(k).linearVelocity - v).magnitude);
    born[m] = (LevelSimulation.Step, spread);
};
f.Merged += onMerged;
LevelRestart.Restart();
while (LevelSimulation.Step < 1800 && found < 10)
{
    int i = LevelSimulation.Step;
    if (i % 40 == 39 && l.Rack.Count > 0)
    {
        int s = i / 40;
        float ang = -90f + ((s * 37) % 140 - 70);
        float speed = 2f + (s * 13) % 4;
        l.Launch((s * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
    foreach (var g in d.InPlay)
    {
        if (!born.ContainsKey(g) || done.Contains(g) || !f.IsMerging(g)) continue;
        int n = g.CapsuleCount;
        for (int a = 0; a < n && !done.Contains(g); a++)
            for (int j = a + 2; j < n; j++)
            {
                if (a == 0 && j == n - 1) continue;
                Vector2 p = g.Capsule(a).position, q = g.Capsule((a + 1) % n).position, c = g.Capsule(j).position, e = g.Capsule((j + 1) % n).position;
                float d1 = (q.x - p.x) * (c.y - p.y) - (q.y - p.y) * (c.x - p.x), d2 = (q.x - p.x) * (e.y - p.y) - (q.y - p.y) * (e.x - p.x);
                float d3 = (e.x - c.x) * (p.y - c.y) - (e.y - c.y) * (p.x - c.x), d4 = (e.x - c.x) * (q.y - c.y) - (e.y - c.y) * (q.x - c.x);
                if (!(d1 * d2 < 0f && d3 * d4 < 0f)) continue;
                float bend = 0f;
                for (int k = a; k <= j + 1; k++)
                {
                    float r0 = g.Capsule((k + n - 1) % n).rotation.angle, r1 = g.Capsule(k % n).rotation.angle;
                    bend = Mathf.Max(bend, Mathf.Abs(Mathf.DeltaAngle(r0 * Mathf.Rad2Deg, r1 * Mathf.Rad2Deg)));
                }
                var b = born[g];
                log.Append($"[niv {f.LevelOf(g)} +{LevelSimulation.Step - b.step} pas, capsules {a}/{j} (écart {Mathf.Min(j - a, n - j + a)}), coude {bend:F0}°, étalement {b.spread:F2} m/s] ");
                done.Add(g); found++;
                break;
            }
    }
}
f.Merged -= onMerged;
return log.ToString();
