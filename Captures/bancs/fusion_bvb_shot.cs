// Image d'une fusion du cas bleu – violet – bleu (fusion_bvb.cs) : rejoue la partie jusqu'à N pas après la naissance, puis fige
// le temps (timeScale 0) pour capture_game_view. Paramètres (fusion_bvb_shot.txt) : "inclinaison_du_tir pas_après_naissance".
var args = System.IO.File.ReadAllText("Captures/bancs/fusion_bvb_shot.txt").Trim().Split(' ');
float tilt = float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
int after = int.Parse(args[1]);
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var one = new System.Collections.Generic.List<uint> { 0u };
void Wait(int n) { for (int k = 0; k < n; k++) LevelSimulation.FastForward(one, 1); }
int Pick(int level)
{
    int best = -1;
    for (int i = 0; i < l.Rack.Count; i++)
        if ((level < 0 || f.LevelOf(l.Rack[i]) == level) && (best < 0 || l.Rack[i].Center.x > l.Rack[best].Center.x)) best = i;
    return best;
}
Time.timeScale = 1f;
LevelRestart.Restart();
Wait(180);
l.Launch(Pick(1), new Vector2(4f, 0f));
Wait(150);
int pink = Pick(2);
l.Launch(pink >= 0 ? pink : Pick(-1), new Vector2(4f, 0f));
Wait(150);
int born = -1;
System.Action<Gumori, int, Vector2> on = (m, lv, p) => { if (born < 0) born = LevelSimulation.Step; };
f.Merged += on;
l.Launch(Pick(3), new Vector2(-Mathf.Cos(tilt * Mathf.Deg2Rad), Mathf.Sin(tilt * Mathf.Deg2Rad)) * l.maxSpeed);
for (int k = 0; k < 400 && (born < 0 || LevelSimulation.Step < born + after); k++) Wait(1);
f.Merged -= on;
Time.timeScale = 0f;
return $"né au pas {born}, figé au pas {LevelSimulation.Step}";
