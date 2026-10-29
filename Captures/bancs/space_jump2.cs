// Vitesse RÉELLE (déplacement du centre d'un pas à l'autre) du slime né et de ses voisins dans la seconde après chaque fusion.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var soft = d.GetComponent<GumoriSoftMerge>();
soft.enabled = System.IO.File.ReadAllText("Captures/bancs/soft.txt").Trim() == "1";
d.seed = 2;
LevelRestart.Restart();
Vector2 C(Gumori b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(Gumori b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var watch = new System.Collections.Generic.List<(Gumori blob, int until)>();
var last = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
System.Action<Gumori, int, Vector2> onMerge = (b, lv, at) =>
{
    int until = LevelSimulation.Step + 60;
    watch.Add((b, until));
    foreach (var x in d.InPlay) if (x != b && (C(x) - at).magnitude < 0.5f * (x.diameter + b.diameter) + 0.4f) watch.Add((x, until));
};
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
float worstUp = 0f, worst = 0f; int fast = 0, samples = 0;
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
    for (int w = watch.Count - 1; w >= 0; w--)
    {
        var (b, until) = watch[w];
        bool alive = false; foreach (var x in d.InPlay) if (x == b) alive = true;
        if (!alive || LevelSimulation.Step > until || InRack(b)) { watch.RemoveAt(w); last.Remove(b); continue; }
        Vector2 c = C(b);
        if (last.TryGetValue(b, out var p))
        {
            Vector2 v = (c - p) / Time.fixedDeltaTime;
            worst = Mathf.Max(worst, v.magnitude); worstUp = Mathf.Max(worstUp, v.y);
            samples++; if (v.magnitude > 2f) fast++;
        }
        last[b] = c;
    }
}
f.Merged -= onMerge;
soft.enabled = true;
return $"douce {System.IO.File.ReadAllText("Captures/bancs/soft.txt").Trim()} | vitesse réelle max {worst:F2} m/s, vers le haut max {worstUp:F2} | pas à plus de 2 m/s : {fast}/{samples}";
