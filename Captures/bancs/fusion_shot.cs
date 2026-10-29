// Prépare une capture de fusion au ralenti (en Play, scène ouverte) : méthode lue dans fusion_style.txt (0 = Cacahuète,
// 1 = Absorption), partie scriptée d'audit_ref.cs jusqu'à 3 pas avant la première fusion vers le niveau ≥ 3 (2e partie : les
// mêmes pas), puis temps ralenti (×0,12). Les captures se font ensuite par capture_game_view (fusion_shot.sh).
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var l = d.GetComponent<GumoriLauncher>();
var goal = d.GetComponent<GumoriGoal>();
if (goal != null) goal.useGoal = false;
d.seed = 2;
f.style = System.IO.File.ReadAllText("Captures/bancs/fusion_style.txt").Trim() == "1" ? FusionStyle.Absorb : FusionStyle.Peanut;
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var one = new System.Collections.Generic.List<uint> { 0u };
int target = -1;
System.Action<Gumori, int, Vector2> onMerged = (m, lv, p) => { if (target < 0 && lv >= 3) target = LevelSimulation.Step; };
void Play(int until)
{
    while (LevelSimulation.Step < until && (until != int.MaxValue || target < 0) && LevelSimulation.Step < 3600)
    {
        int i = LevelSimulation.Step;
        uint mk = 0;
        if (l == null)
        {
            if (i % 70 < 2) mk |= SP;
            int phase = (i / 90) % 3;
            if (phase == 0) mk |= RI; else if (phase == 1) mk |= LE;
        }
        else if (i % 40 == 39 && l.Rack.Count > 0)
        {
            int s = i / 40;
            float ang = -90f + ((s * 37) % 140 - 70);
            float speed = 2f + (s * 13) % 4;
            l.Launch((s * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
        }
        one[0] = mk;
        LevelSimulation.FastForward(one, 1);
    }
}
f.Merged += onMerged;
LevelRestart.Restart();
Play(int.MaxValue);
f.Merged -= onMerged;
if (target < 0) return "aucune fusion vers le niveau 3";
LevelRestart.Restart();
Play(target - 3);
Time.timeScale = 0.12f;
return $"{f.style} : fusion au pas {target}, arrêté au pas {LevelSimulation.Step}, ralenti ×0,12";
