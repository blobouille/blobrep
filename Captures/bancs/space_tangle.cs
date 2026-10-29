// Partie scriptée de 2400 pas (tir tous les 40 pas), démêlage actif ou non (tangle.txt = 1 / 0) ; paires emmêlées comptées tous les 100 pas.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var u = d.GetComponent<GumoriUntangle>();
bool on = System.IO.File.ReadAllText("Captures/bancs/tangle.txt").Trim() == "1";
u.enabled = on;
d.seed = 2;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
int samples = 0, tangled = 0, worst = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
while (LevelSimulation.Step < 1200)
{
    int s = LevelSimulation.Step;
    if (s % 40 == 39 && l.Rack.Count > 0)
    {
        int i = s / 40;
        float ang = -90f + ((i * 37) % 140 - 70);
        float speed = 2f + (i * 13) % 4;
        l.Launch((i * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
    if (s % 100 == 99) { int t = u.TangledPairs(); tangled += t; worst = Mathf.Max(worst, t); samples++; }
}
sw.Stop();
int final = u.TangledPairs();
u.enabled = true;
return $"démêlage {(on ? "ON" : "OFF")} : paires emmêlées moyenne {(float)tangled / samples:F2} max {worst} fin {final} | démêlages {u.Untangled} | slimes {d.Count} | {sw.Elapsed.TotalMilliseconds / 1200:F2} ms/pas";
