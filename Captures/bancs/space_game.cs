// Partie scriptée par morceaux (space_game.txt = "début fin") : un tir tous les 40 pas, rang / angle / force tirés d'un compteur.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var parts = System.IO.File.ReadAllText("Captures/bancs/space_game.txt").Trim().Split(' ');
int from = int.Parse(parts[0]), to = int.Parse(parts[1]);
if (from == 0) { d.seed = 2; LevelRestart.Restart(); }
var one = new System.Collections.Generic.List<uint> { 0u };
var sw = System.Diagnostics.Stopwatch.StartNew();
int steps = 0;
while (LevelSimulation.Step < to)
{
    int s = LevelSimulation.Step;
    if (s % 40 == 39 && l.Rack.Count > 0)
    {
        int i = s / 40;
        int slot = (i * 7) % l.Rack.Count;
        float ang = -90f + ((i * 37) % 140 - 70);
        float speed = 1f + (i * 13) % 4;
        l.Launch(slot, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
    steps++;
}
sw.Stop();
ulong h = 1469598103934665603UL;
foreach (var b in d.Blobs)
    for (int k = 0; k < b.CapsuleCount; k++)
    {
        var p = b.Capsule(k).position;
        h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
        h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
    }
return $"pas {LevelSimulation.Step} tirs {l.Shots.Count} réserve {l.Rack.Count} slimes {d.Count} score {d.GetComponent<SlimeScore>().Score} cout {sw.Elapsed.TotalMilliseconds / Mathf.Max(1, steps):F2} ms/pas hash {h:X16}";
