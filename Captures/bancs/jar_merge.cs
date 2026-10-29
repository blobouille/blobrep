// Play, scène avec un bocal (16) : cas signalé le 2026-10-28. Un gumori A enfoncé dans le goulot depuis le haut (étiré par les
// parois, à moitié dedans), son pareil B lâché dessus (décalé de dx) : ils fusionnent. Relevé des pas où un gumori est à cheval
// sur le verre (un côté de son anneau croise la ligne médiane du verre, ou un point du verre est dans son anneau), et SVG des
// anneaux + verre à quelques instants (Captures/fusion/jar_merge_<dx>_<pas>.svg).
// Paramètres : jar_merge.txt "niveau enfoncement(pas) dx..." (dx en m).
var args = System.IO.File.Exists("Captures/bancs/jar_merge.txt") ? System.IO.File.ReadAllText("Captures/bancs/jar_merge.txt").Trim().Split(' ') : new string[0];
var inv = System.Globalization.CultureInfo.InvariantCulture;
int level = args.Length > 0 ? int.Parse(args[0]) : 4;
int push = args.Length > 1 ? int.Parse(args[1]) : 60;
var shifts = new System.Collections.Generic.List<float>();
for (int i = 2; i < args.Length; i++) shifts.Add(float.Parse(args[i], inv));
if (shifts.Count == 0) shifts.AddRange(new[] { -0.6f, -0.3f, 0f, 0.3f, 0.6f });
var jar = UnityEngine.Object.FindAnyObjectByType<JarShape2D>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var launcher = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var one = new System.Collections.Generic.List<uint> { 0u };
Vector2 o = jar.transform.position;
var wall = new System.Collections.Generic.List<Vector2>(); foreach (var c in jar.Path.center) wall.Add(jar.transform.TransformPoint(c));
bool Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 e) { float d1 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x), d2 = (b.x - a.x) * (e.y - a.y) - (b.y - a.y) * (e.x - a.x), d3 = (e.x - c.x) * (a.y - c.y) - (e.y - c.y) * (a.x - c.x), d4 = (e.x - c.x) * (b.y - c.y) - (e.y - c.y) * (b.x - c.x); return d1 * d2 < 0f && d3 * d4 < 0f; }
bool InRack(Gumori g) { if (launcher == null) return false; foreach (var r in launcher.Rack) if (r == g) return true; return false; }
bool Straddles(Gumori g)
{
    int n = g.CapsuleCount;
    for (int i = 0; i < n; i++) { Vector2 p = g.Capsule(i).position, q = g.Capsule((i + 1) % n).position; for (int w = 1; w < wall.Count; w++) if (Cross(p, q, wall[w - 1], wall[w])) return true; }
    foreach (var w in wall) if (g.Contains(w)) return true;
    return false;
}
// Anneau physique : les deux bouts de chaque capsule, à la suite (les cordes du né), coupe-t-il le verre ?
bool StraddlesEnds(Gumori g)
{
    var pts = new System.Collections.Generic.List<Vector2>();
    for (int i = 0; i < g.CapsuleCount; i++) { var body = g.Capsule(i); var geo = g.CapsuleShapeAt(i).capsuleGeometry; float an = body.rotation.radians, cs = Mathf.Cos(an), sn = Mathf.Sin(an); Vector2 o2 = body.position; pts.Add(o2 + new Vector2(cs * geo.center1.x - sn * geo.center1.y, sn * geo.center1.x + cs * geo.center1.y)); pts.Add(o2 + new Vector2(cs * geo.center2.x - sn * geo.center2.y, sn * geo.center2.x + cs * geo.center2.y)); }
    for (int i = 0; i < pts.Count; i++) { Vector2 p = pts[i], q = pts[(i + 1) % pts.Count]; for (int w = 1; w < wall.Count; w++) if (Cross(p, q, wall[w - 1], wall[w])) return true; }
    return false;
}
string P(Vector2 p) => (300f + (p.x - o.x) * 100f).ToString("0.0", inv) + "," + (560f - (p.y - o.y) * 100f).ToString("0.0", inv);
void Svg(string path, Gumori focus)
{
    var sb = new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='600' height='600'><rect width='600' height='600' fill='white'/><polyline fill='none' stroke='#888' stroke-width='6' points='");
    foreach (var w in wall) sb.Append(P(w) + " ");
    sb.Append("'/>");
    foreach (var g in d.InPlay)
    {
        if (InRack(g)) continue;
        sb.Append($"<polygon fill='{(g == focus ? "#8af" : "#fc8")}' fill-opacity='0.5' stroke='{(Straddles(g) ? "#e00" : "#036")}' stroke-width='2' points='");
        for (int i = 0; i < g.CapsuleCount; i++) sb.Append(P(g.Capsule(i).position) + " ");
        sb.Append("'/>");
    }
    System.IO.Directory.CreateDirectory("Captures/fusion");
    System.IO.File.WriteAllText(path, sb.Append("</svg>").ToString());
}
float diameter = f.LevelDiameter(level);
var s = $"niveau {level} (Ø {diameter:0.00} m) goulot {jar.neckWidth:0.00} m, repli de secours {f.StitchFallbacks} ; ";
foreach (float dx in shifts)
{
    LevelRestart.Restart();
    LevelSimulation.FastForward(one, 1); LevelSimulation.FastForward(one, 1);
    float top = o.y + jar.InnerTop;
    var a = d.SpawnLevel(new Vector2(o.x, top + 0.5f * diameter + 0.05f), level);
    for (int k = 0; k < push; k++) { a.AddVelocity(new Vector2(0f, -2.5f) * 0.05f); LevelSimulation.FastForward(one, 1); }
    for (int k = 0; k < 30; k++) LevelSimulation.FastForward(one, 1);
    var b = d.SpawnLevel(new Vector2(o.x + dx, top + 1.5f * diameter + 0.2f), level);
    int before = f.Merges;
    string parents = "";
    for (int k = 0; k < 300 && f.Merges == before; k++) { parents = $"A{(Straddles(a) ? "X" : "-")}B{(Straddles(b) ? "X" : "-")}"; if (k % 4 == 0) Svg($"Captures/fusion/jar_merge_{dx.ToString("0.00", inv)}_avant.svg", b); b.AddVelocity(new Vector2(0f, -2.5f) * 0.05f); LevelSimulation.FastForward(one, 1); }
    if (f.Merges == before) { s += $"[dx {dx:0.00} pas de fusion] "; continue; }
    Gumori born = null; foreach (var g in d.InPlay) if (g != a && g != b && !InRack(g) && g.diameter > diameter * 1.01f) born = g;
    int straddle = 0, first = -1, ends = 0;
    string tag = dx.ToString("0.00", inv);
    for (int k = 0; k <= 240; k++)
    {
        if (k == 0 || k == 5 || k == 20 || k == 60 || k == 240) Svg($"Captures/fusion/jar_merge_{tag}_{k:000}.svg", born);
        if (born != null && GumoriDropper.IsInPlay(born) && Straddles(born)) { straddle++; if (first < 0) first = k; }
        if (born != null && GumoriDropper.IsInPlay(born) && StraddlesEnds(born)) ends++;
        LevelSimulation.FastForward(one, 1);
    }
    s += $"[dx {dx:0.00} parents {parents} : né à cheval {straddle}/241 pas (bouts des capsules {ends}), dès le pas {first}] ";
}
return s + $" repli de secours {f.StitchFallbacks}, fusions remises (paroi) {f.WallWaits}";
