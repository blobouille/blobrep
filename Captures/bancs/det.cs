// Déterminisme : deux parties identiques (redémarrage + 1500 pas d'entrées fixes) doivent donner le même hash au bit près.
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var sc = d.GetComponent<GumoriScore>();
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var inputs = new System.Collections.Generic.List<uint>();
for (int i = 0; i < 1500; i++)
{
    uint m = 0;
    if (i % 70 < 2) m |= SP;
    int phase = (i / 90) % 3;
    if (phase == 0) m |= RI; else if (phase == 1) m |= LE;
    inputs.Add(m);
}
string Run()
{
    LevelRestart.Restart();
    LevelSimulation.FastForward(inputs, inputs.Count);
    ulong h = 1469598103934665603UL;
    foreach (var b in d.InPlay)
        for (int k = 0; k < b.CapsuleCount; k++)
        {
            var p = b.Capsule(k).position;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
        }
    return $"seed={d.seed} blobs={d.Count} drops={d.Drops} score={sc.Score} hash={h:X16}";
}
return Run() + "\n" + Run();
