// GumoriSkin.FanCenter sur des contours tracés à la main (V épais, U, ovale) : centre moyen contre centre choisi, triangles
// de l'éventail retournés (= peau tendue hors du corps) et aire couverte hors du polygone (échantillonnage sur grille).
// Réglages par défaut de GumoriSkin (fanTolerance 0, fanStiffness 10) pour un gumori de rayon 0,95 m.
var mi = typeof(GumoriSkin).GetMethod("FanCenter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
UnityEngine.Vector2[] Band(System.Func<float, UnityEngine.Vector2> spine, float half, int n)
{
    // Bande épaisse autour d'une courbe ouverte, contour dans le sens trigonométrique, bouts arrondis.
    var pts = new System.Collections.Generic.List<UnityEngine.Vector2>();
    int m = n / 2 - 6;
    UnityEngine.Vector2 Nor(float t) { var d = spine(UnityEngine.Mathf.Min(1f, t + 0.001f)) - spine(UnityEngine.Mathf.Max(0f, t - 0.001f)); d.Normalize(); return new UnityEngine.Vector2(-d.y, d.x); }
    for (int i = 0; i <= m; i++) { float t = (float)i / m; pts.Add(spine(t) - Nor(t) * half); }
    var e1 = spine(1f); var n1 = Nor(1f);
    for (int i = 1; i < 6; i++) { float a = UnityEngine.Mathf.PI * i / 6; pts.Add(e1 - n1 * UnityEngine.Mathf.Cos(a) * half + new UnityEngine.Vector2(n1.y, -n1.x) * UnityEngine.Mathf.Sin(a) * half); }
    for (int i = m; i >= 0; i--) { float t = (float)i / m; pts.Add(spine(t) + Nor(t) * half); }
    var e0 = spine(0f); var n0 = Nor(0f);
    for (int i = 1; i < 6; i++) { float a = UnityEngine.Mathf.PI * i / 6; pts.Add(e0 + n0 * UnityEngine.Mathf.Cos(a) * half - new UnityEngine.Vector2(n0.y, -n0.x) * UnityEngine.Mathf.Sin(a) * half); }
    return pts.ToArray();
}
bool Inside(UnityEngine.Vector2[] p, UnityEngine.Vector2 q)
{
    bool inside = false; var a = p[p.Length - 1];
    foreach (var b in p) { if ((b.y > q.y) != (a.y > q.y) && q.x < (a.x - b.x) * (q.y - b.y) / (a.y - b.y) + b.x) inside = !inside; a = b; }
    return inside;
}
string Report(string name, UnityEngine.Vector2[] p)
{
    int n = p.Length;
    var c = UnityEngine.Vector2.zero; foreach (var q in p) c += q; c /= n;
    var x = (UnityEngine.Vector2)mi.Invoke(null, new object[] { p, n, c, -0f * 0.95f, 10f / 0.95f });
    string One(UnityEngine.Vector2 o)
    {
        int bad = 0; for (int k = 0; k < n; k++) { var a = p[k] - o; var b = p[(k + 1) % n] - o; if (a.x * b.y - a.y * b.x < 0f) bad++; }
        // Aire hors du corps couverte par au moins un triangle de l'éventail.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var q in p) { minX = UnityEngine.Mathf.Min(minX, q.x); minY = UnityEngine.Mathf.Min(minY, q.y); maxX = UnityEngine.Mathf.Max(maxX, q.x); maxY = UnityEngine.Mathf.Max(maxY, q.y); }
        int outside = 0, total = 0; float step = 0.02f;
        for (float gx = minX; gx < maxX; gx += step) for (float gy = minY; gy < maxY; gy += step)
        {
            var g = new UnityEngine.Vector2(gx, gy);
            if (Inside(p, g)) { total++; continue; }
            for (int k = 0; k < n; k++)
            {
                var a = p[k]; var b = p[(k + 1) % n];
                float d1 = (a.x - o.x) * (g.y - o.y) - (a.y - o.y) * (g.x - o.x), d2 = (b.x - a.x) * (g.y - a.y) - (b.y - a.y) * (g.x - a.x), d3 = (o.x - b.x) * (g.y - b.y) - (o.y - b.y) * (g.x - b.x);
                if ((d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0)) { outside++; break; }
            }
        }
        return $"retournés {bad}/{n}, peau hors du corps {100f * outside / total:0.0} % de l'aire, centre dedans={Inside(p, o)}";
    }
    return $"{name}\n  avant : {One(c)}\n  après : {One(x)}  (déplacé de {(x - c).magnitude:0.000} m)";
}
var sb = new System.Text.StringBuilder();
sb.AppendLine(Report("V (comme la capture)", Band(t => t < 0.5f ? UnityEngine.Vector2.Lerp(new UnityEngine.Vector2(-1.8f, -1.3f), new UnityEngine.Vector2(0f, 0.6f), t * 2f) : UnityEngine.Vector2.Lerp(new UnityEngine.Vector2(0f, 0.6f), new UnityEngine.Vector2(1.8f, -1.3f), t * 2f - 1f), 0.28f, 72)));
sb.AppendLine(Report("V fermé", Band(t => t < 0.5f ? UnityEngine.Vector2.Lerp(new UnityEngine.Vector2(-0.8f, -1.6f), new UnityEngine.Vector2(0f, 0.6f), t * 2f) : UnityEngine.Vector2.Lerp(new UnityEngine.Vector2(0f, 0.6f), new UnityEngine.Vector2(0.8f, -1.6f), t * 2f - 1f), 0.28f, 72)));
sb.AppendLine(Report("U", Band(t => new UnityEngine.Vector2(-1.2f * UnityEngine.Mathf.Cos(UnityEngine.Mathf.PI * t), -1.2f * UnityEngine.Mathf.Sin(UnityEngine.Mathf.PI * t)), 0.25f, 72)));
sb.AppendLine(Report("banane", Band(t => new UnityEngine.Vector2(-1.2f * UnityEngine.Mathf.Cos(UnityEngine.Mathf.PI * (0.25f + 0.5f * t)), 1.2f * UnityEngine.Mathf.Sin(UnityEngine.Mathf.PI * (0.25f + 0.5f * t))), 0.3f, 72)));
var ov = new UnityEngine.Vector2[72]; for (int k = 0; k < 72; k++) { float a = 2f * UnityEngine.Mathf.PI * k / 72; ov[k] = new UnityEngine.Vector2(1.3f * UnityEngine.Mathf.Cos(a), 0.6f * UnityEngine.Mathf.Sin(a) + 0.08f * UnityEngine.Mathf.Sin(5 * a)); }
sb.AppendLine(Report("ovale qui tremble", ov));
return sb.ToString();
