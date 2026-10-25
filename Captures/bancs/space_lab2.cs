// Banc sans tir : lit de 14 slimes posé, puis 6 paires de slimes identiques posées l'une contre l'autre sur la pile (vitesse
// nulle) : elles fusionnent au milieu de la foule. Énergie cinétique (½ v² × d², vitesse = déplacement réel) des slimes hors
// réserve : pic dans les 1,5 s après la pose, moins le niveau d'avant. growTime lu dans grow.txt.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var foam = d.GetComponent<GumoriFoam>();
foam.growTime = float.Parse(System.IO.File.ReadAllText("Captures/bancs/grow.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
d.seed = 2;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
Vector2 C(Gumori b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(Gumori b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var last = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
float Energy()
{
    float e = 0f;
    var now = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
    foreach (var b in d.InPlay)
    {
        Vector2 c = C(b); now[b] = c;
        if (InRack(b) || !last.ContainsKey(b)) continue;
        Vector2 v = (c - last[b]) / Time.fixedDeltaTime;
        e += 0.5f * v.sqrMagnitude * b.diameter * b.diameter;
    }
    last = now;
    return e;
}
void Run(int n) { for (int i = 0; i < n; i++) { LevelSimulation.FastForward(one, 1); Energy(); } }
int[] bed = { 2, 1, 3, 0, 2, 1, 3, 2, 1, 0, 2, 3, 1, 2 };
for (int i = 0; i < bed.Length; i++) d.SpawnLevel(new Vector2(-3.4f + (i % 7) * 1.13f, 0.6f + (i / 7) * 2.0f), bed[i]);
Run(240);
float off = float.Parse(System.IO.File.ReadAllText("Captures/bancs/off.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
float[] xs = { -2.5f + off, 1.5f - off, -0.5f + off, 2.5f - off, -1.5f + off, 0.5f - off };
int[] lv = { 0, 1, 0, 2, 1, 0 };
var sb = new System.Text.StringBuilder();
float sum = 0f, max = 0f;
int merges0 = f.Merges;
for (int p = 0; p < xs.Length; p++)
{
    float top = -10f;
    foreach (var b in d.InPlay) if (!InRack(b)) top = Mathf.Max(top, C(b).y + 0.5f * b.diameter);
    float r = 0.5f * f.LevelDiameter(lv[p]);
    float before = 0f;
    for (int i = 0; i < 10; i++) { LevelSimulation.FastForward(one, 1); before = Mathf.Max(before, Energy()); }
    d.SpawnLevel(new Vector2(xs[p] - r * 0.98f, top + r), lv[p]);
    d.SpawnLevel(new Vector2(xs[p] + r * 0.98f, top + r), lv[p]);
    float peak = 0f;
    for (int i = 0; i < 90; i++) { LevelSimulation.FastForward(one, 1); peak = Mathf.Max(peak, Energy()); }
    float jolt = Mathf.Max(0f, peak - before);
    sum += jolt; max = Mathf.Max(max, jolt);
    sb.Append($"{jolt:F1} ");
}
return $"growTime {foam.growTime} | fusions {f.Merges - merges0} | coup par paire : {sb}| moyen {sum / xs.Length:F1} max {max:F1}";
