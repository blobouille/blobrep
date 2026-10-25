// Banc de fusion contrôlé : lit de slimes au fond, deux petits lâchés dessus (fusion puis chaîne dans la pile).
// Mesure la vitesse réelle vers le haut (déplacement d'un pas à l'autre) des slimes hors réserve.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
d.seed = 2;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
void Run(int steps) { for (int i = 0; i < steps; i++) LevelSimulation.FastForward(one, 1); }
int[] bed = { 2, 1, 3, 1, 2, 1, 3, 2 };
for (int i = 0; i < bed.Length; i++) d.SpawnLevel(new Vector2(-3.4f + i * 0.97f, 0.8f + (i % 2) * 1.6f), bed[i]);
Run(150);
d.SpawnLevel(new Vector2(-0.45f, 6.5f), 1);
d.SpawnLevel(new Vector2(0.45f, 6.5f), 1);
int merges0 = f.Merges;
Vector2 C(Gumori b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(Gumori b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var last = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
float up = 0f, any = 0f; int fastSteps = 0;
for (int s = 0; s < 240; s++)
{
    Run(1);
    foreach (var b in d.InPlay)
    {
        if (InRack(b)) { last.Remove(b); continue; }
        Vector2 c = C(b);
        if (last.TryGetValue(b, out var p) && f.Merges > merges0)
        {
            Vector2 v = (c - p) / Time.fixedDeltaTime;
            up = Mathf.Max(up, v.y); any = Mathf.Max(any, v.magnitude);
            if (v.y > 1.5f) fastSteps++;
        }
        last[b] = c;
    }
}
return $"fusions {f.Merges - merges0} | montée max {up:F2} m/s | vitesse max {any:F2} m/s | pas (slime) à > 1,5 m/s vers le haut : {fastSteps}";
