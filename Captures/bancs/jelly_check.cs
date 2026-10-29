// Gelée élastique (2026-10-27), image figée en Play : pour chaque gumori dessiné, part de l'aire de la gelée (maillage
// grossier de SolveJelly) en triangles RETOURNÉS (pli), et part HORS du corps (centre du triangle hors du polygone des
// capsules élargi de leur épaisseur, approché par le polygone de la surface).
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var looks = (System.Collections.IDictionary)typeof(GumoriSkin).GetField("_looks", bf).GetValue(skin);
var sb = new System.Text.StringBuilder();
float worstOut = 0f; string svg = null;
string Svg(Vector2[] q, int m, int L)
{
    Vector2 lo = q[0], hi = q[0]; for (int i = 0; i < 1 + (L + 1) * m; i++) { lo = Vector2.Min(lo, q[i]); hi = Vector2.Max(hi, q[i]); }
    float s = 560f / Mathf.Max(hi.x - lo.x, hi.y - lo.y);
    string P(Vector2 p) => $"{(20f + (p.x - lo.x) * s).ToString(System.Globalization.CultureInfo.InvariantCulture)},{(580f - (p.y - lo.y) * s).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    var o = new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='600' height='600'><rect width='600' height='600' fill='white'/>");
    void Line(Vector2 a, Vector2 b, string col, float w) => o.Append($"<line x1='{P(a).Split(',')[0]}' y1='{P(a).Split(',')[1]}' x2='{P(b).Split(',')[0]}' y2='{P(b).Split(',')[1]}' stroke='{col}' stroke-width='{w}'/>");
    for (int j = 1; j <= L + 1; j++) for (int k = 0; k < m; k++) { int a = 1 + (j - 1) * m + k, b = 1 + (j - 1) * m + (k + 1) % m; Line(q[a], q[b], j > L ? "#000" : "#36c", j > L ? 2f : 1f); Line(j == 1 ? q[0] : q[a - m], q[a], "#aaa", 0.7f); }
    return o.Append("</svg>").ToString();
}
foreach (System.Collections.DictionaryEntry e in looks)
{
    var blob = (Gumori)e.Key; var look = e.Value;
    if (blob == null || blob.CapsuleCount < 3) continue;
    var q = (Vector2[])look.GetType().GetField("jelly").GetValue(look);
    int m = (int)look.GetType().GetField("jellyM").GetValue(look);
    if (q == null || m != blob.CapsuleCount) continue;
    int L = 3;
    var surf = new Vector2[m];
    for (int k = 0; k < m; k++) surf[k] = q[1 + L * m + k];
    bool Inside(Vector2 p) { bool ins = false; for (int k = 0; k < m; k++) { Vector2 a = surf[k], b = surf[(k + 1) % m]; if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) ins = !ins; } return ins; }
    float total = 0f, inverted = 0f, outside = 0f;
    void Tri(int a, int b, int c)
    {
        float area = 0.5f * ((q[b].x - q[a].x) * (q[c].y - q[a].y) - (q[b].y - q[a].y) * (q[c].x - q[a].x));
        total += Mathf.Abs(area);
        if (area < 0f) inverted -= area;
        if (!Inside((q[a] + q[b] + q[c]) / 3f)) outside += Mathf.Abs(area);
    }
    for (int k = 0; k < m; k++) Tri(0, 1 + k, 1 + (k + 1) % m);
    for (int j = 1; j <= L; j++) for (int k = 0; k < m; k++) { int a = 1 + (j - 1) * m + k, b = 1 + (j - 1) * m + (k + 1) % m; Tri(a, a + m, b + m); Tri(a, b + m, b); }
    float fan = 0f, fanTotal = 0f; Vector2 c0 = blob.Center;
    for (int k = 0; k < m; k++) { Vector2 p = surf[k] - c0, r = surf[(k + 1) % m] - c0; float a = p.x * r.y - p.y * r.x; fanTotal += Mathf.Abs(a); if (a < 0f) fan -= a; }
    if (fan / fanTotal < 0.01f && inverted / total < 0.01f && outside / total < 0.01f) continue;
    if (fan / fanTotal < 0.01f && svg == null) svg = Svg(q, m, L);
    sb.Append($"[{blob.Center.x:0.0},{blob.Center.y:0.0}] éventail retourné {100f * fan / fanTotal:0}% | gelée retournée {100f * inverted / total:0.0}%, hors du corps {100f * outside / total:0.0}%\n");
}
if (svg != null) System.IO.File.WriteAllText("Captures/fusion/gelee.svg", svg);
return sb.Length == 0 ? "aucun gumori plié" : sb.ToString();
