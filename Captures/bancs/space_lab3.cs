// Banc sans tir : lit de 14 slimes posé, puis 6 paires de slimes identiques posées l'une contre l'autre sur la pile (vitesse
// nulle) : elles fusionnent au milieu de la foule. Énergie cinétique (½ v² × d², vitesse = déplacement réel) des slimes hors
// réserve : pic dans les 1,5 s après la pose, moins le niveau d'avant. growTime lu dans grow.txt.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var f = d.GetComponent<BlobFusion>();
var foam = d.GetComponent<SlimeFoam>();
foam.growTime = float.Parse(System.IO.File.ReadAllText("Captures/bancs/grow.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
d.seed = 2;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
Vector2 C(BLOB b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(BLOB b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var last = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
float Energy()
{
    float e = 0f;
    var now = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
    foreach (var b in d.Blobs)
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
float[] xs = { -2.5f, 1.5f, -0.5f, 2.5f, -1.5f, 0.5f };
int[] lv = { 0, 1, 0, 2, 1, 0 };
var sb = new System.Text.StringBuilder(); var tl = new System.Text.StringBuilder(); var prev = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
float sum = 0f, max = 0f;
int merges0 = f.Merges;
for (int p = 0; p < xs.Length; p++)
{
    float top = -10f;
    foreach (var b in d.Blobs) if (!InRack(b)) top = Mathf.Max(top, C(b).y + 0.5f * b.diameter);
    float r = 0.5f * f.LevelDiameter(lv[p]);
    float before = 0f;
    for (int i = 0; i < 10; i++) { LevelSimulation.FastForward(one, 1); before = Mathf.Max(before, Energy()); }
    d.SpawnLevel(new Vector2(xs[p] - r * 0.98f, top + r), lv[p]);
    d.SpawnLevel(new Vector2(xs[p] + r * 0.98f, top + r), lv[p]);
    float peak = 0f;
    for (int i = 0; i < (p == 3 ? 170 : 90); i++) { LevelSimulation.FastForward(one, 1); float en = Energy(); peak = Mathf.Max(peak, en); if (p == 3 && en > 3f) { BLOB fb = null; float fv = 0f; foreach (var b in d.Blobs) { if (InRack(b) || !prev.ContainsKey(b)) continue; float vv = (C(b) - prev[b]).magnitude / Time.fixedDeltaTime; if (vv > fv) { fv = vv; fb = b; } } tl.Append($"t{i}:E{en:F0} {fb?.name} niv{(fb != null ? f.LevelOf(fb) : -1)} {fv:F1}m/s fus{(fb != null && f.IsMerging(fb) ? 1 : 0)} / "); } prev = new System.Collections.Generic.Dictionary<BLOB, Vector2>(); foreach (var b in d.Blobs) prev[b] = C(b); }
    float jolt = Mathf.Max(0f, peak - before);
    sum += jolt; max = Mathf.Max(max, jolt);
    sb.Append($"{jolt:F1} ");
}
return tl + " | " + $"growTime {foam.growTime} | fusions {f.Merges - merges0} | coup par paire : {sb}| moyen {sum / xs.Length:F1} max {max:F1}";
