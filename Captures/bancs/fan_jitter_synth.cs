// Tremblement du centre de la gelée (GumoriSkin.FanCenter) sur une séquence tracée à la main : un gumori de 32 capsules qui se
// plie en V puis se déplie (240 images), capsules qui glissent le long du contour, bruit de 3 mm par image. fan_jitter_synth.txt :
// "sp|surf lissage_s tolérance raideur" : sp = contour lissé (3 points par capsule), surf = points des capsules ; tolérance en
// rayons, raideur 0 = centre jamais déplacé (habillage d'avant).
var jargs = System.IO.File.ReadAllText("Captures/bancs/fan_jitter_synth.txt").Trim().Split(' ');
bool useSp = jargs[0] == "sp"; float tau = float.Parse(jargs[1], System.Globalization.CultureInfo.InvariantCulture);
float tol = jargs.Length > 2 ? float.Parse(jargs[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
float stiff = jargs.Length > 3 ? float.Parse(jargs[3], System.Globalization.CultureInfo.InvariantCulture) : 10f;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var fc = typeof(GumoriSkin).GetMethod("FanCenter", flags);
var cr = typeof(GumoriSkin).GetMethod("CatmullRom", flags);
var rng = new System.Random(7);
UnityEngine.Vector2[] Outline(float bend)
{
    // Bande épaisse autour d'une colonne vertébrale pliée de 'bend' (0 = droite, 1 = V à 75°), bouts arrondis, sens trigo.
    var pts = new System.Collections.Generic.List<UnityEngine.Vector2>();
    float half = 0.28f, L = 1.9f, ang = bend * 1.3f;
    UnityEngine.Vector2 Spine(float t) { var q = UnityEngine.Vector2.zero; int st = 60; float s0 = (t - 0.5f) * 2f * L * 0.5f; for (int i = 0; i < st; i++) { float s = s0 * (i + 0.5f) / st; float th = -ang * (float)System.Math.Tanh(s / 0.25f); q += new UnityEngine.Vector2(UnityEngine.Mathf.Cos(th), UnityEngine.Mathf.Sin(th)) * (s0 / st); } return q; }
    UnityEngine.Vector2 Nor(float t) { var d = Spine(UnityEngine.Mathf.Min(1f, t + 0.002f)) - Spine(UnityEngine.Mathf.Max(0f, t - 0.002f)); d.Normalize(); return new UnityEngine.Vector2(-d.y, d.x); }
    int m = 200;
    for (int i = 0; i <= m; i++) { float t = (float)i / m; pts.Add(Spine(t) - Nor(t) * half); }
    var e1 = Spine(1f); var n1 = Nor(1f); var t1 = new UnityEngine.Vector2(n1.y, -n1.x);
    for (int i = 1; i < 20; i++) { float a = UnityEngine.Mathf.PI * i / 20; pts.Add(e1 - n1 * UnityEngine.Mathf.Cos(a) * half - t1 * UnityEngine.Mathf.Sin(a) * half); }
    for (int i = m; i >= 0; i--) { float t = (float)i / m; pts.Add(Spine(t) + Nor(t) * half); }
    var e0 = Spine(0f); var n0 = Nor(0f); var t0 = new UnityEngine.Vector2(n0.y, -n0.x);
    for (int i = 1; i < 20; i++) { float a = UnityEngine.Mathf.PI * i / 20; pts.Add(e0 + n0 * UnityEngine.Mathf.Cos(a) * half + t0 * UnityEngine.Mathf.Sin(a) * half); }
    return pts.ToArray();
}
UnityEngine.Vector2[] Resample(UnityEngine.Vector2[] o, int m, float phase)
{
    int n = o.Length; var cum = new float[n + 1];
    for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + (o[(i + 1) % n] - o[i]).magnitude;
    float total = cum[n]; var r = new UnityEngine.Vector2[m]; int j = 0;
    for (int k = 0; k < m; k++)
    {
        float s = UnityEngine.Mathf.Repeat((k + phase) / m, 1f) * total;
        j = 0; while (j < n - 1 && cum[j + 1] < s) j++;
        float u = (s - cum[j]) / UnityEngine.Mathf.Max(1e-6f, cum[j + 1] - cum[j]);
        r[k] = UnityEngine.Vector2.Lerp(o[j], o[(j + 1) % n], u) + new UnityEngine.Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.006f;
    }
    return r;
}
bool Inside(UnityEngine.Vector2[] p, UnityEngine.Vector2 q)
{
    bool inside = false; var a = p[p.Length - 1];
    foreach (var b in p) { if ((b.y > q.y) != (a.y > q.y) && q.x < (a.x - b.x) * (q.y - b.y) / (a.y - b.y) + b.x) inside = !inside; a = b; }
    return inside;
}
float skinOut = 0f; int skinN = 0;
float SkinOutside(UnityEngine.Vector2[] p, UnityEngine.Vector2 o)
{
    int n = p.Length; float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
    foreach (var q in p) { minX = UnityEngine.Mathf.Min(minX, q.x); minY = UnityEngine.Mathf.Min(minY, q.y); maxX = UnityEngine.Mathf.Max(maxX, q.x); maxY = UnityEngine.Mathf.Max(maxY, q.y); }
    int outside = 0, total = 0; float step = 0.025f;
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
    return 100f * outside / UnityEngine.Mathf.Max(1, total);
}
int frames = 240, outsideFrames = 0, movedFrames = 0; var jumps = new System.Collections.Generic.List<float>();
UnityEngine.Vector2 smooth = default, prev = default; bool has = false; float maxOff = 0f;
for (int f = 0; f < frames; f++)
{
    float bend = UnityEngine.Mathf.Sin(UnityEngine.Mathf.PI * f / (frames - 1));
    var surf = Resample(Outline(bend), 32, 0.004f * f);
    var c = UnityEngine.Vector2.zero; foreach (var q in surf) c += q; c /= surf.Length;
    UnityEngine.Vector2[] poly = surf;
    if (useSp) { int m = surf.Length; poly = new UnityEngine.Vector2[3 * m]; for (int i = 0; i < m; i++) for (int j = 0; j < 3; j++) poly[3 * i + j] = (UnityEngine.Vector2)cr.Invoke(null, new object[] { surf[(i + m - 1) % m], surf[i], surf[(i + 1) % m], surf[(i + 2) % m], j / 3f }); }
    var off = (UnityEngine.Vector2)fc.Invoke(null, new object[] { poly, poly.Length, c, -tol * 0.95f, stiff / 0.95f }) - c;
    smooth = !has || tau <= 0f ? off : UnityEngine.Vector2.Lerp(off, smooth, UnityEngine.Mathf.Exp(-(1f / 60f) / tau));
    if (has) jumps.Add((smooth - prev).magnitude);
    prev = smooth; has = true;
    if (smooth.magnitude > 0.001f) movedFrames++;
    maxOff = UnityEngine.Mathf.Max(maxOff, smooth.magnitude);
    if (!Inside(surf, c + smooth)) outsideFrames++;
    if (f % 8 == 0) { skinOut += SkinOutside(poly, c + smooth); skinN++; }
}
float mean = 0f; foreach (var j in jumps) mean += j; mean /= jumps.Count;
// « Accélération » du centre : variation du saut d'une image à l'autre (un glissement régulier est doux, un saut ne l'est pas).
float rough = 0f; for (int i = 1; i < jumps.Count; i++) rough = UnityEngine.Mathf.Max(rough, UnityEngine.Mathf.Abs(jumps[i] - jumps[i - 1]));
jumps.Sort();
return $"lissage {tau}s tolérance {tol} raideur {stiff} : peau hors du corps {skinOut / skinN:0.0} % de l aire en moyenne ; centre hors du corps {outsideFrames}/{frames} images, déplacé de {maxOff:0.00} m max ; saut par image moyen {1000f * mean:0.0} mm, max {1000f * jumps[jumps.Count - 1]:0.0} mm ; à-coup max {1000f * rough:0.0} mm";
