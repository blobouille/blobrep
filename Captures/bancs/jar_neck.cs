// Play, scène avec un bocal (JarShape2D) : un gumori posé au fond du bocal, son pareil poussé dans le goulot depuis dehors.
// Pour chaque inclinaison de poussée : pas à cheval sur la paroi (un côté de l'anneau d'un gumori croise la ligne médiane du verre) sur 4 s après la fusion.
// Paramètres : jar_neck.txt "niveau vitesse".
var args = System.IO.File.Exists("Captures/bancs/jar_neck.txt") ? System.IO.File.ReadAllText("Captures/bancs/jar_neck.txt").Trim().Split(' ') : new string[0];
int level = args.Length > 0 ? int.Parse(args[0]) : 1;
float speed = args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 2.5f;
var jar = UnityEngine.Object.FindAnyObjectByType<JarShape2D>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var launcher = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var one = new System.Collections.Generic.List<uint> { 0u };
Vector2 o = jar.transform.position;
// Intérieur du bocal : sous le haut du goulot et à moins du rayon de l'axe (ventre), ou dans le goulot.
bool Inside(Vector2 p) { var q = p - o; if (q.y < -0.01f || q.y > jar.InnerTop) return false; float yNeck = jar.bottomRound * jar.radius + jar.bellyHeight + jar.shoulderHeight; return q.y > yNeck ? Mathf.Abs(q.x) < jar.InnerNeckHalf + 0.02f : (q - new Vector2(0f, Mathf.Min(q.y, jar.bottomRound * jar.radius))).magnitude < jar.radius + 0.02f || (Mathf.Abs(q.x) < jar.radius && q.y > jar.bottomRound * jar.radius); }
var wall = new System.Collections.Generic.List<Vector2>(); foreach (var c in jar.Path.center) wall.Add(jar.transform.TransformPoint(c));
bool Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 e) { float d1 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x), d2 = (b.x - a.x) * (e.y - a.y) - (b.y - a.y) * (e.x - a.x), d3 = (e.x - c.x) * (a.y - c.y) - (e.y - c.y) * (a.x - c.x), d4 = (e.x - c.x) * (b.y - c.y) - (e.y - c.y) * (b.x - c.x); return d1 * d2 < 0f && d3 * d4 < 0f; }
var s = $"niveau {level} (Ø {f.LevelDiameter(level):0.00} m) goulot {jar.neckWidth:0.00} m ; ";
foreach (float tilt in new[] { -25f, -10f, 0f, 10f, 25f })
{
    LevelRestart.Restart();
    LevelSimulation.FastForward(one, 1); LevelSimulation.FastForward(one, 1);

    float yNeckTop = o.y + jar.InnerTop;
    var inner = d.SpawnLevel(o + new Vector2(0f, 0.5f * f.LevelDiameter(level) + 0.05f), level);
    var outer = d.SpawnLevel(new Vector2(o.x, yNeckTop + 0.5f * f.LevelDiameter(level) + 0.1f), level);
    float a = (tilt - 90f) * Mathf.Deg2Rad;
    for (int k = 0; k < 240 && GumoriDropper.IsInPlay(outer); k++) { outer.AddVelocity(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * 0.05f); LevelSimulation.FastForward(one, 1); }
    bool merged = !GumoriDropper.IsInPlay(outer);
    int straddle = 0, steps = 0; int mergedAt = LevelSimulation.Step;
    for (int k = 0; k < 240; k++)
    {
        LevelSimulation.FastForward(one, 1);
        foreach (var g in d.InPlay)
        {
            if (launcher != null) { bool rack = false; foreach (var r in launcher.Rack) if (r == g) rack = true; if (rack) continue; }
            int cross = 0; for (int i = 0; i < g.CapsuleCount; i++) { Vector2 p = g.Capsule(i).position, q = g.Capsule((i + 1) % g.CapsuleCount).position; for (int w = 1; w < wall.Count; w++) if (Cross(p, q, wall[w - 1], wall[w])) cross++; }
            if (cross > 0) straddle++;
        }
        steps++;
    }
    s += $"[{tilt:+0;-0;0}° fusion={merged} à cheval {straddle}/{steps} pas] ";
}
return s;
