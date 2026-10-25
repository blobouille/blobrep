// Remplit le réservoir : redémarrage, N pas d'entrées fixes (lâchers + visée qui balaie), puis forme moyenne des slimes au repos.
var d = UnityEngine.Object.FindAnyObjectByType<BlobDropper>();
var sc = d.GetComponent<SlimeScore>();
int steps = int.Parse(System.IO.File.ReadAllText("Captures/bancs/space_steps.txt").Trim());
uint SP = 1u << (int)FlipperKey.Space, RI = 1u << (int)FlipperKey.RightArrow, LE = 1u << (int)FlipperKey.LeftArrow;
var inputs = new System.Collections.Generic.List<uint>();
for (int i = 0; i < steps; i++)
{
    uint k = 0;
    if (i % 50 < 2) k |= SP;
    int phase = (i / 70) % 4;
    if (phase == 0) k |= RI; else if (phase == 2) k |= LE;
    inputs.Add(k);
}
d.seed = 2;
LevelRestart.Restart();
var sw = System.Diagnostics.Stopwatch.StartNew();
LevelSimulation.FastForward(inputs, inputs.Count);
sw.Stop();
ulong h = 1469598103934665603UL;
float ratio = 0f; int n = 0; float top = -99f;
foreach (var b in d.Blobs)
{
    float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
    for (int k = 0; k < b.CapsuleCount; k++)
    {
        var p = b.Capsule(k).position;
        x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
        h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
        h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
    }
    ratio += (y1 - y0) / Mathf.Max(0.01f, x1 - x0); n++;
    top = Mathf.Max(top, y1);
}
return $"seed={d.seed} blobs={d.Count} drops={d.Drops} score={sc.Score} h/l moyen={(n > 0 ? ratio / n : 0f):F2} haut pile={top:F1} cout={sw.Elapsed.TotalMilliseconds / steps:F3} ms/pas hash={h:X16}";
