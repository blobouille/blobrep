var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var phase = System.IO.File.ReadAllText("Captures/bancs/lab_phase.txt").Trim();
if (phase == "0")
{
    d.seed = 2;
    LevelRestart.Restart();
    int[] bed = { 2, 1, 3, 1, 2, 1, 3, 2 };
    for (int i = 0; i < bed.Length; i++) d.SpawnLevel(new Vector2(-3.4f + i * 0.97f, 0.8f + (i % 2) * 1.6f), bed[i]);
    return "lit posé";
}
d.SpawnLevel(new Vector2(-0.6f, 4.2f), 2);
d.SpawnLevel(new Vector2(0.6f, 4.2f), 2);
return "deux niveau 2 lâchés";
