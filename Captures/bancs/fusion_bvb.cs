// Cas bleu – violet – bleu (signalé le 2026-10-27, « space niveau 1 ») : violet de droite viré à droite, puis le rose viré à
// droite ; il reste bleu, violet, bleu dans la réserve ; le bleu de droite est lancé à 100 % vers la gauche. Mesure la fusion
// des deux bleus. Paramètres (fusion_bvb.txt) : "attente_avant_tir_final pas_suivis".
var args = System.IO.File.Exists("Captures/bancs/fusion_bvb.txt") ? System.IO.File.ReadAllText("Captures/bancs/fusion_bvb.txt").Trim().Split(' ') : new string[0];
int wait = args.Length > 0 ? int.Parse(args[0]) : 150;
int follow = args.Length > 1 ? int.Parse(args[1]) : 120;
float tilt = args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var one = new System.Collections.Generic.List<uint> { 0u };
var sb = new System.Text.StringBuilder();
void Wait(int n) { for (int k = 0; k < n; k++) LevelSimulation.FastForward(one, 1); }
string Rack() { var s = ""; for (int i = 0; i < l.Rack.Count; i++) s += $"[{i}] niv{f.LevelOf(l.Rack[i])} x={l.Rack[i].Center.x:0.00}  "; return s; }
int Pick(int level, bool rightmost)
{
    int best = -1;
    for (int i = 0; i < l.Rack.Count; i++)
        if ((level < 0 || f.LevelOf(l.Rack[i]) == level) && (best < 0 || (l.Rack[i].Center.x > l.Rack[best].Center.x) == rightmost)) best = i;
    return best;
}
string Run()
{
    LevelRestart.Restart();
    Wait(180);
    l.Launch(Pick(1, true), new Vector2(4f, 0f));
    Wait(150);
    int pink = Pick(2, true);
    l.Launch(pink >= 0 ? pink : Pick(-1, true), new Vector2(4f, 0f));
    Wait(wait);
    string before = Rack();
    int blue = Pick(3, true);
    int merges = f.Merges, born = -1; Gumori child = null;
    System.Action<Gumori, int, Vector2> on = (m, lv, p) => { if (child == null) { child = m; born = LevelSimulation.Step; } };
    f.Merged += on;
    var shot = l.Rack[blue]; Gumori other = null; foreach (var r in l.Rack) if (r != shot && f.LevelOf(r) == 3) other = r;
    l.Launch(blue, new Vector2(-Mathf.Cos(tilt * Mathf.Deg2Rad), Mathf.Sin(tilt * Mathf.Deg2Rad)) * l.maxSpeed);
    string path = "";
    float minArea = float.MaxValue, birthArea = 0f; int crossed = 0, enclosed = 0; string trace = "";
    for (int k = 0; k < follow; k++)
    {
        Wait(1);
        if (k % 20 == 0 && other != null && shot.CapsuleCount > 0 && other.CapsuleCount > 0) { float gap = float.MaxValue; for (int i = 0; i < shot.CapsuleCount; i++) for (int j = 0; j < other.CapsuleCount; j++) gap = Mathf.Min(gap, (shot.Capsule(i).position - other.Capsule(j).position).magnitude - shot.CapsuleThickness - other.CapsuleThickness); path += $" {k}:({shot.Center.x:0.0},{shot.Center.y:0.0}) écart {gap:0.00}"; }
        if (child == null || child.CapsuleCount == 0) continue;
        int n = child.CapsuleCount; float area = 0f;
        for (int i = 0; i < n; i++) { Vector2 a = child.Capsule(i).position, b = child.Capsule((i + 1) % n).position; area += a.x * b.y - a.y * b.x; }
        area *= 0.5f;
        if (birthArea == 0f) birthArea = area;
        minArea = Mathf.Min(minArea, area);
        if (Crossed(child)) crossed++;
        foreach (var o in d.InPlay) if (o != child && child.Contains(o.Center)) { enclosed++; break; }
        if ((LevelSimulation.Step - born) % 10 == 0) trace += $" {LevelSimulation.Step - born}:{area:0.00}";
    }
    f.Merged -= on;
    float target = child != null ? Mathf.PI * Mathf.Pow(child.RestRingRadius, 2f) : 0f;
    ulong h = 1469598103934665603UL;
    foreach (var b in d.InPlay) for (int k = 0; k < b.CapsuleCount; k++) { var p = b.Capsule(k).position; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL; }
    return $"{f.style} réserve avant tir : {before}\n  fusions {f.Merges - merges}, né niv{(child != null ? f.LevelOf(child) : -1)} ; aire de l'anneau à la naissance {birthArea:0.00} m², min {minArea:0.00}, au repos visé {target:0.00} ; pas à anneau croisé {crossed}, pas avec un voisin dedans {enclosed}, contours de secours {f.StitchFallbacks} ; aire au fil des pas :{trace}\n  tir :{path}\n  hash={h:X16}";
}
bool Crossed(Gumori g)
{
    int n = g.CapsuleCount;
    for (int i = 0; i < n; i++)
    {
        Vector2 a = g.Capsule(i).position, b = g.Capsule((i + 1) % n).position;
        for (int j = i + 2; j < n; j++)
        {
            if (i == 0 && j == n - 1) continue;
            Vector2 c = g.Capsule(j).position, e = g.Capsule((j + 1) % n).position;
            float d1 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x), d2 = (b.x - a.x) * (e.y - a.y) - (b.y - a.y) * (e.x - a.x);
            float d3 = (e.x - c.x) * (a.y - c.y) - (e.y - c.y) * (a.x - c.x), d4 = (e.x - c.x) * (b.y - c.y) - (e.y - c.y) * (b.x - c.x);
            if (d1 * d2 < 0f && d3 * d4 < 0f) return true;
        }
    }
    return false;
}
sb.AppendLine(Run());
return sb.ToString();
