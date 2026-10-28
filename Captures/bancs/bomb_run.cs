// Play, scène « bombes (essai) » : un tir toutes les 1,5 s (vers une bombe, sinon de biais) pendant N pas (bomb_run.txt, défaut 2700), relevé des
// rebours, explosions, absorptions, morceaux cédés, gumori sortis, score ; hash final (déterminisme : lancer 2 fois).
var bombs = UnityEngine.Object.FindAnyObjectByType<GumoriBombs>();
var launcher = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var score = UnityEngine.Object.FindAnyObjectByType<GumoriScore>();
var dropper = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
LevelRestart.Restart();
var log = new System.Text.StringBuilder();
System.Action<GumoriBombs.Bomb, UnityEngine.Vector2, float> ex = (b, at, r) => log.Append($"[pas {LevelSimulation.Step} BOUM niv {b.level} r {r:0.0}] ");
System.Action<GumoriBombs.Bomb, Gumori, UnityEngine.Vector2> ab = (b, g, at) => log.Append($"[pas {LevelSimulation.Step} absorbée niv {b.level}] ");
System.Action<Gumori, int> lk = (g, p) => log.Append($"[sorti −{p}] ");
bombs.Exploded += ex; bombs.Absorbed += ab; bombs.Leaked += lk;
var one = new System.Collections.Generic.List<uint> { 0u };
int shots = 0;
int stop = System.IO.File.Exists("Captures/bancs/bomb_run.txt") ? int.Parse(System.IO.File.ReadAllText("Captures/bancs/bomb_run.txt").Trim()) : 60 * 45;
for (int k = 0; k < stop; k++)
{
    if (k % 90 == 60 && launcher.Rack.Count > 0)
    {
        UnityEngine.Vector2 from = launcher.Rack[0].Center, aim = from + UnityEngine.Vector2.down * 5f;
        if (bombs.Bombs.Count > 0 && bombs.Bombs[0].blob != null) aim = bombs.Bombs[0].blob.Center;
        var v = (aim - from).normalized * 4f;
        if (shots % 3 == 2) v = new UnityEngine.Vector2(shots % 2 == 0 ? 3f : -3f, -2.5f);
        launcher.Launch(0, v); shots++;
    }
    LevelSimulation.FastForward(one, 1);
}
bombs.Exploded -= ex; bombs.Absorbed -= ab; bombs.Leaked -= lk;
int broken = 0; foreach (var w in CurveCollider2D.Breakables) broken += w.BrokenCount;
var fuses = ""; foreach (var b in bombs.Bombs) fuses += $"{b.level}:{b.fuse}{(b.armed ? "" : "r")} ";
uint h = 2166136261u;
foreach (var g in dropper.InPlay) for (int i = 0; i < g.CapsuleCount; i++) { var p = g.Capsule(i).position; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.x)) * 16777619u; h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(p.y)) * 16777619u; }
return $"{log} | tirs {launcher.Shots.Count}, gumori {dropper.InPlay.Count}, bombes restantes {fuses}, morceaux cédés {broken}, score {score.Score}, hash {h:X8}";
