// Rejoue deux fois les tirs enregistrés de la partie en cours, pas à pas, et compare les hash.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var shots = new System.Collections.Generic.List<GumoriLauncher.Shot>(l.Shots);
if (shots.Count == 0) return "aucun tir";
if (shots.Count > 15) shots.RemoveRange(15, shots.Count - 15);
int end = shots[shots.Count - 1].step + 300;
var one = new System.Collections.Generic.List<uint> { 0u };
string Run()
{
    LevelRestart.Restart();
    int next = 0;
    while (LevelSimulation.Step < end)
    {
        while (next < shots.Count && shots[next].step == LevelSimulation.Step) { l.Launch(shots[next].slot, shots[next].velocity); next++; }
        LevelSimulation.FastForward(one, 1);
    }
    ulong h = 1469598103934665603UL;
    foreach (var b in d.InPlay)
        for (int k = 0; k < b.CapsuleCount; k++)
        {
            var p = b.Capsule(k).position;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
            h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
        }
    return $"tirs={l.Shots.Count}/{shots.Count} blobs={d.Count} score={d.GetComponent<GumoriScore>().Score} hash={h:X16}";
}
return Run() + " | " + Run();
