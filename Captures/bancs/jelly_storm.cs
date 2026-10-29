// Play (Space) : un tir violent par appel (direction tournante), puis relevé de la gelée de tous les gumori dessinés : part de
// l'aire en triangles retournés et hors du corps (pire gumori), centre de la gelée hors du corps.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
int i = l.Shots.Count;
if (l.Rack.Count > 0) { float ang = (i * 137.5f) % 360f; l.Launch(i % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * l.maxSpeed); }
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var looks = (System.Collections.IDictionary)typeof(GumoriSkin).GetField("_looks", bf).GetValue(skin);
float worstInv = 0f, worstOut = 0f; int centerOut = 0, count = 0;
foreach (System.Collections.DictionaryEntry e in looks)
{
    var blob = (Gumori)e.Key; var look = e.Value;
    if (blob == null || blob.CapsuleCount < 3) continue;
    var q = (Vector2[])look.GetType().GetField("jelly").GetValue(look);
    int m = (int)look.GetType().GetField("jellyM").GetValue(look);
    if (q == null || m != blob.CapsuleCount) continue;
    int L = 3; count++;
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
    worstInv = Mathf.Max(worstInv, inverted / total); worstOut = Mathf.Max(worstOut, outside / total);
    if (!Inside(q[0])) centerOut++;
}
return $"{count} gumori | pire retourné {100f * worstInv:0.0}% | pire hors du corps {100f * worstOut:0.0}% | centre dehors {centerOut}";
