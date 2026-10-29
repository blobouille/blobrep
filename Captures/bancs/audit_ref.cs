// Référence de l'audit du code (2026-10-25) : partie scriptée identique avant / après une refonte, en Play, dans la scène
// ouverte. Terrestre (pas de GumoriLauncher) : entrées fixes de det.cs sur 3600 pas. Space : un tir tous les 40 pas (space_fly)
// sur 1800 pas. Hash des bits des positions ET vitesses de toutes les capsules, + score, fusions, sorties.
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var sc = UnityEngine.Object.FindAnyObjectByType<GumoriScore>();
var es = UnityEngine.Object.FindAnyObjectByType<GumoriEscape>();
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var goal = UnityEngine.Object.FindAnyObjectByType<GumoriGoal>();
if (goal != null) goal.useGoal = false; // sinon l'objectif atteint retire tous les slimes (hash vide)
d.seed = 2; // Space tire une graine au hasard à chaque Play
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var one = new System.Collections.Generic.List<uint> { 0u };
string Run()
{
    LevelRestart.Restart();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    int total = l != null ? 1800 : 3600;
    while (LevelSimulation.Step < total)
    {
        int i = LevelSimulation.Step;
        uint m = 0;
        if (l == null)
        {
            if (i % 70 < 2) m |= SP;
            int phase = (i / 90) % 3;
            if (phase == 0) m |= RI; else if (phase == 1) m |= LE;
        }
        else if (i % 40 == 39 && l.Rack.Count > 0)
        {
            int s = i / 40;
            float ang = -90f + ((s * 37) % 140 - 70);
            float speed = 2f + (s * 13) % 4;
            l.Launch((s * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
        }
        one[0] = m;
        LevelSimulation.FastForward(one, 1);
    }
    double ms = sw.Elapsed.TotalMilliseconds / total;
    ulong h = 1469598103934665603UL;
    void Mix(float x) => h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(x)) * 1099511628211UL;
    foreach (var b in d.InPlay)
        for (int k = 0; k < b.CapsuleCount; k++)
        {
            var c = b.Capsule(k);
            Mix(c.position.x); Mix(c.position.y); Mix(c.linearVelocity.x); Mix(c.linearVelocity.y); Mix(c.angularVelocity);
        }
    return $"seed={d.seed} blobs={d.Count} drops={d.Drops} merges={f.Merges} score={sc.Score} escapes={(es != null ? es.Escapes : -1)} hash={h:X16} ({ms:F3} ms/pas)";
}
return Run(); // une partie par appel (l'éditeur coupe une commande après 5 s) : appeler deux fois et comparer
