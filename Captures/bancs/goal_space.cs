// Play, scène Space : GumoriGoal compté en TIRS. SpaceEndless coupé, objectif à 'cible' points en 'tirs' tirs, tirs
// tout droit vers le bas depuis la réserve toutes les 1,5 s ; relevé : tirs restants, score, résultat.
// Paramètres : goal_space.txt "tirs cible".
var args = System.IO.File.Exists("Captures/bancs/goal_space.txt") ? System.IO.File.ReadAllText("Captures/bancs/goal_space.txt").Trim().Split(' ') : new string[0];
int shots = args.Length > 0 ? int.Parse(args[0]) : 5;
int target = args.Length > 1 ? int.Parse(args[1]) : 100000;
var goal = UnityEngine.Object.FindAnyObjectByType<GumoriGoal>();
var endless = UnityEngine.Object.FindAnyObjectByType<SpaceEndless>();
var launcher = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var score = UnityEngine.Object.FindAnyObjectByType<GumoriScore>();
bool hadEndless = endless != null && endless.enabled, hadGoal = goal.useGoal, hadAuto = goal.autoTarget; int hadMax = goal.maxDrops, hadTarget = goal.targetScore;
if (endless != null) endless.enabled = false;
goal.useGoal = true; goal.autoTarget = false; goal.maxDrops = shots; goal.targetScore = target;
LevelRestart.Restart();
var one = new System.Collections.Generic.List<uint> { 0u };
var s = $"actif {goal.Active} objectif {goal.Target} ; ";
for (int k = 0; k < 60 * 30 && goal.Result == GumoriGoal.State.Playing; k++)
{
    if (k % 90 == 30) { s += $"[pas {k}: restants {launcher.ShotsLeft}] "; launcher.Launch(0, new Vector2(0f, -3f)); }
    LevelSimulation.FastForward(one, 1);
}
s += $" → tirs joués {launcher.Shots.Count}, restants {launcher.ShotsLeft}, score {score.Score}, résultat {goal.Result}, tirés à la fin {goal.LeftAtWin}";
goal.useGoal = hadGoal; goal.autoTarget = hadAuto; goal.maxDrops = hadMax; goal.targetScore = hadTarget;
if (endless != null) endless.enabled = hadEndless;
LevelRestart.Restart();
return s;
