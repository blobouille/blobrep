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
var opts = System.IO.File.ReadAllText("Captures/bancs/fusion_style.txt").Trim().Split(' ');
f.style = opts[0] == "1" ? FusionStyle.Absorb : FusionStyle.Peanut;
// Réglages essayés sans toucher la scène : inherit=, stiff=, smooth=, neck=, time= (ex. « 1 inherit=0 stiff=1 »).
// Repartent des défauts du script à chaque appel (sinon l'essai précédent restait appliqué dans la même session de Play).
var foam = d.GetComponent<GumoriFoam>(); if (foam != null) { foam.foldAngle = 60f; foam.pressureFrequency = 8f; }
d.seed = 2; f.inheritMotion = 1f; f.absorbStiffness = 1f; f.absorbSmoothing = 4; f.absorbNeck = 0.5f; f.absorbTime = 0.9f;
foreach (var o in opts)
{
    var kv = o.Split('=');
    if (kv.Length != 2) continue;
    float v = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
    if (kv[0] == "inherit") f.inheritMotion = v; else if (kv[0] == "stiff") f.absorbStiffness = v; else if (kv[0] == "smooth") f.absorbSmoothing = (int)v;
    else if (kv[0] == "neck") f.absorbNeck = v; else if (kv[0] == "time") f.absorbTime = v; else if (kv[0] == "seed") d.seed = (int)v;
    else if (kv[0] == "fold") d.GetComponent<GumoriFoam>().foldAngle = v; else if (kv[0] == "press") d.GetComponent<GumoriFoam>().pressureFrequency = v;
}
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var one = new System.Collections.Generic.List<uint> { 0u };
int births = 0, overlapped = 0, enclosed = 0, crossedBirths = 0;
float worst = 0f, sum = 0f;
System.Action<Gumori, int, Vector2> onMerged = (m, lv, p) =>
{
    births++;
    if (Crossed(m)) crossedBirths++;
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
// Anneau croisé : polygone des capsules d'un gumori en train de fusionner qui se recoupe (boucle, pli : l'habillage se chevauche),
// compté à chaque pas pour chaque gumori en fusion.
int crossedSteps = 0;
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
LevelRestart.Restart();
var sw = System.Diagnostics.Stopwatch.StartNew();
long mergeTicks = 0, fusingTicks = 0, restTicks = 0, maxMergeTicks = 0; int mergeSteps = 0, fusingSteps = 0; bool anyMerging = false;
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
    int before = f.Merges;
    long t0 = sw.ElapsedTicks;
    LevelSimulation.FastForward(one, 1);
    long dtk = sw.ElapsedTicks - t0;
    if (f.Merges != before) { mergeTicks += dtk; mergeSteps++; maxMergeTicks = System.Math.Max(maxMergeTicks, dtk); }
    else if (anyMerging) fusingTicks += dtk; else restTicks += dtk;
    anyMerging = false; foreach (var g in d.InPlay) if (f.IsMerging(g)) { anyMerging = true; break; }
    if (anyMerging) fusingSteps++;
    foreach (var g in d.InPlay) if (f.IsMerging(g) && Crossed(g)) crossedSteps++;
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
       $"chevauchement à la naissance : moyen {100f * sum / Mathf.Max(1, births):F1} cm, pire {100f * worst:F1} cm, > 5 cm {overlapped}/{births}, voisins enfermés à la naissance {enclosed}, pas à anneau croisé {crossedSteps} (dès la naissance {crossedBirths}), coudes retournés {f.TurnFixes} hash={h:X16} ({ms:F3} ms/pas ; pas de fusion {1000.0 * mergeTicks / System.Diagnostics.Stopwatch.Frequency / System.Math.Max(1, mergeSteps):F2} ms, pire {1000.0 * maxMergeTicks / System.Diagnostics.Stopwatch.Frequency:F2} ; pendant {1000.0 * fusingTicks / System.Diagnostics.Stopwatch.Frequency / System.Math.Max(1, fusingSteps):F3} ms ; repos {1000.0 * restTicks / System.Diagnostics.Stopwatch.Frequency / System.Math.Max(1, total - mergeSteps - fusingSteps):F3} ms)";
