// Diagnostic de l'absorption (Space) : pour les 4 premiers gumori fusionnés dont l'anneau se croise, dessine en SVG ses capsules
// à la naissance (bleu), à 1/3 du temps avant le croisement (gris) et au premier croisement (rouge), avec les voisins proches
// (vert). Écrit Captures/fusion/trace_<n>.svg. Même partie scriptée que fusion_compare.cs.
var culture = System.Globalization.CultureInfo.CurrentCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture; // points SVG : « 1.5 » et pas « 1,5 »
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var l = d.GetComponent<GumoriLauncher>();
var goal = d.GetComponent<GumoriGoal>();
if (goal != null) goal.useGoal = false;
d.seed = System.IO.File.Exists("Captures/bancs/fusion_trace.txt") ? int.Parse(System.IO.File.ReadAllText("Captures/bancs/fusion_trace.txt").Trim()) : 2;
f.style = FusionStyle.Absorb; f.inheritMotion = 1f; f.absorbStiffness = 1f; f.absorbSmoothing = 4; f.absorbNeck = 0.5f; f.absorbTime = 0.9f;
var foam0 = d.GetComponent<GumoriFoam>(); if (foam0 != null) { foam0.foldAngle = 60f; foam0.pressureFrequency = 8f; }
var one = new System.Collections.Generic.List<uint> { 0u };
var born = new System.Collections.Generic.Dictionary<Gumori, (int step, Vector2[] ring)>();
var history = new System.Collections.Generic.Dictionary<Gumori, System.Collections.Generic.List<Vector2[]>>();
var done = new System.Collections.Generic.HashSet<Gumori>();
var log = new System.Text.StringBuilder();
int written = 0;
Vector2[] Snap(Gumori g) { var r = new Vector2[g.CapsuleCount]; for (int i = 0; i < r.Length; i++) r[i] = g.Capsule(i).position; return r; }
bool Crossed(Vector2[] p)
{
    int n = p.Length;
    for (int i = 0; i < n; i++)
        for (int j = i + 2; j < n; j++)
        {
            if (i == 0 && j == n - 1) continue;
            Vector2 a = p[i], b = p[(i + 1) % n], c = p[j], e = p[(j + 1) % n];
            float d1 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x), d2 = (b.x - a.x) * (e.y - a.y) - (b.y - a.y) * (e.x - a.x);
            float d3 = (e.x - c.x) * (a.y - c.y) - (e.y - c.y) * (a.x - c.x), d4 = (e.x - c.x) * (b.y - c.y) - (e.y - c.y) * (b.x - c.x);
            if (d1 * d2 < 0f && d3 * d4 < 0f) return true;
        }
    return false;
}
string Poly(Vector2[] p, Vector2 o, string color, float w)
{
    var sb = new System.Text.StringBuilder($"<polygon fill='none' stroke='{color}' stroke-width='{w}' points='");
    foreach (var q in p) sb.Append($"{(q.x - o.x) * 100f + 300f:F1},{300f - (q.y - o.y) * 100f:F1} ");
    sb.Append("'/>");
    foreach (var q in p) sb.Append($"<circle cx='{(q.x - o.x) * 100f + 300f:F1}' cy='{300f - (q.y - o.y) * 100f:F1}' r='2' fill='{color}'/>");
    return sb.ToString();
}
System.Action<Gumori, int, Vector2> onMerged = (m, lv, p) => { born[m] = (LevelSimulation.Step, Snap(m)); history[m] = new System.Collections.Generic.List<Vector2[]>(); };
f.Merged += onMerged;
LevelRestart.Restart();
while (LevelSimulation.Step < 1800 && written < 4)
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
        var now = Snap(g);
        history[g].Add(now);
        if (!Crossed(now)) continue;
        done.Add(g);
        var b = born[g];
        Vector2 o = g.Center;
        var svg = new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='600' height='600'><rect width='600' height='600' fill='white'/>");
        foreach (var other in d.InPlay)
            if (other != g && (other.Center - o).magnitude < 3f) svg.Append(Poly(Snap(other), o, "#4a4", 1f));
        svg.Append(Poly(b.ring, o, "#36c", 1.5f));
        var h = history[g];
        svg.Append(Poly(h[Mathf.Max(0, h.Count * 2 / 3 - 1)], o, "#999", 1f));
        svg.Append(Poly(now, o, "#d22", 2f));
        svg.Append("</svg>");
        System.IO.File.WriteAllText($"Captures/fusion/trace_{written}.svg", svg.ToString());
        log.Append($"trace_{written} : niveau {f.LevelOf(g)}, {g.CapsuleCount} capsules, croisé {LevelSimulation.Step - b.step} pas après la naissance ; ");
        written++;
    }
}
f.Merged -= onMerged;
System.Globalization.CultureInfo.CurrentCulture = culture;
return log.ToString();
