// Partie scriptée de 1200 pas ; compte les slimes à l'envers (aire signée du polygone des capsules < 0) tous les 50 pas.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
d.seed = 2;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
int flips = 0, worst = 0, pos = 0, neg = 0, foldSum = 0;
int Folds() { int f = 0; foreach (var b in d.InPlay) { int n = b.CapsuleCount; for (int k = 0; k < n; k++) { Vector2 a = b.Capsule((k + n - 1) % n).position, p = b.Capsule(k).position, q = b.Capsule((k + 1) % n).position; Vector2 e1 = p - a, e2 = q - p; if (Mathf.Atan2(e1.x * e2.y - e1.y * e2.x, Vector2.Dot(e1, e2)) * Mathf.Rad2Deg < -60f) f++; } } return f; }
int Count()
{
    int c = 0;
    foreach (var b in d.InPlay)
    {
        int n = b.CapsuleCount; float a = 0f;
        for (int k = 0; k < n; k++) { Vector2 p = b.Capsule(k).position, q = b.Capsule((k + 1) % n).position; a += p.x * q.y - q.x * p.y; }
        if (a < 0f) { c++; neg++; } else pos++;
    }
    return c;
}
while (LevelSimulation.Step < 1200)
{
    int s = LevelSimulation.Step;
    if (s % 40 == 39 && l.Rack.Count > 0)
    {
        int i = s / 40;
        float ang = -90f + ((i * 37) % 140 - 70);
        float speed = 3f + (i * 13) % 2;
        l.Launch((i * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
    if (s % 50 == 49) { int c = Count(); flips += c; worst = Mathf.Max(worst, c); foldSum += Folds(); }
}
int folds = 0; foreach (var b in d.InPlay) { int n = b.CapsuleCount; for (int k = 0; k < n; k++) { Vector2 a = b.Capsule((k + n - 1) % n).position, p = b.Capsule(k).position, q = b.Capsule((k + 1) % n).position; Vector2 e1 = p - a, e2 = q - p; float ang = Mathf.Atan2(e1.x * e2.y - e1.y * e2.x, Vector2.Dot(e1, e2)) * Mathf.Rad2Deg; if (ang < -60f) folds++; } }
return $"plis relevés {foldSum}, fin {folds} | slimes à l'envers : total des relevés {flips}, max {worst}, fin {Count()} | relevés aire>0 {pos} aire<0 {neg} | slimes {d.Count}";
