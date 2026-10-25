// Cacahuète contre Absorption (2026-10-25), en Play, scène ouverte : la partie scriptée d'audit_ref.cs avec la méthode de fusion
// lue dans Captures/bancs/fusion_style.txt (0 = Cacahuète, 1 = Absorption ; non sauvegardé dans la scène). Mesure, à chaque
// naissance, le chevauchement du nouvel anneau avec les capsules des AUTRES gumori (enfoncement = somme des épaisseurs − distance
// entre centres de capsules) : plus il est grand, plus Box2D repousse les voisins d'un coup (coincements, sauts).
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var sc = d.GetComponent<GumoriScore>();
var es = d.GetComponent<GumoriEscape>();
var un = d.GetComponent<GumoriUntangle>();
var l = d.GetComponent<GumoriLauncher>();
var goal = d.GetComponent<GumoriGoal>();
if (goal != null) goal.useGoal = false;
d.seed = 2;
f.style = System.IO.File.ReadAllText("Captures/bancs/fusion_style.txt").Trim() == "1" ? FusionStyle.Absorb : FusionStyle.Peanut;
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var one = new System.Collections.Generic.List<uint> { 0u };
int births = 0, overlapped = 0, enclosed = 0;
float worst = 0f, sum = 0f;
System.Action<Gumori, int, Vector2> onMerged = (m, lv, p) =>
{
    births++;
    float deepest = 0f;
    foreach (var g in d.InPlay)
    {
        if (g == m || (g.Center - m.Center).magnitude > 0.5f * (g.diameter + m.diameter) + 1f) continue;
        if (m.Contains(g.Center)) enclosed++;
        for (int i = 0; i < g.CapsuleCount; i++)
        {
            Vector2 c = g.Capsule(i).position;
            for (int k = 0; k < m.CapsuleCount; k++)
            {
                float pen = g.CapsuleThickness + m.CapsuleThickness - (m.Capsule(k).position - c).magnitude;
                if (pen > deepest) deepest = pen;
            }
        }
    }
    if (deepest > 0.05f) overlapped++;
    sum += deepest;
    if (deepest > worst) worst = deepest;
};
f.Merged += onMerged;
LevelRestart.Restart();
var sw = System.Diagnostics.Stopwatch.StartNew();
int total = l != null ? 1800 : 3600;
while (LevelSimulation.Step < total)
{
    int i = LevelSimulation.Step;
    uint mk = 0;
    if (l == null)
    {
        if (i % 70 < 2) mk |= SP;
        int phase = (i / 90) % 3;
        if (phase == 0) mk |= RI; else if (phase == 1) mk |= LE;
    }
    else if (i % 40 == 39 && l.Rack.Count > 0)
    {
        int s = i / 40;
        float ang = -90f + ((s * 37) % 140 - 70);
        float speed = 2f + (s * 13) % 4;
        l.Launch((s * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    one[0] = mk;
    LevelSimulation.FastForward(one, 1);
}
f.Merged -= onMerged;
double ms = sw.Elapsed.TotalMilliseconds / total;
ulong h = 1469598103934665603UL;
void Mix(float x) => h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(x)) * 1099511628211UL;
foreach (var b in d.InPlay)
    for (int k = 0; k < b.CapsuleCount; k++)
    {
        var c = b.Capsule(k);
        Mix(c.position.x); Mix(c.position.y); Mix(c.linearVelocity.x); Mix(c.linearVelocity.y); Mix(c.angularVelocity);
    }
return $"{f.style} merges={f.Merges} score={sc.Score} escapes={(es != null ? es.Escapes : -1)} untangles={(un != null ? un.Untangled : -1)} " +
       $"chevauchement à la naissance : moyen {100f * sum / Mathf.Max(1, births):F1} cm, pire {100f * worst:F1} cm, > 5 cm {overlapped}/{births}, voisins enfermés à la naissance {enclosed} hash={h:X16} ({ms:F3} ms/pas)";
