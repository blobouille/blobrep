// Partie scriptée de 1200 pas : après chaque fusion, vitesse max (centre) du slime né et de ses voisins pendant 1 s.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
d.seed = 2;
LevelRestart.Restart();
var watch = new System.Collections.Generic.List<(Gumori blob, int until)>();
int merges = 0;
System.Action<Gumori, int, Vector2> onMerge = (b, lv, at) => { watch.Add((b, LevelSimulation.Step + 60)); merges++; };
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
float maxBorn = 0f, maxNear = 0f, sumBorn = 0f; int nBorn = 0;
Vector2 C(Gumori b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
Vector2 V(Gumori b) { Vector2 v = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) v += b.Capsule(k).linearVelocity; return v / Mathf.Max(1, b.CapsuleCount); }
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
        if (!alive || LevelSimulation.Step > until) { watch.RemoveAt(w); continue; }
        bool inRack = false; foreach (var r in l.Rack) if (r == b) inRack = true;
        if (inRack) continue;
        float sp = V(b).magnitude; maxBorn = Mathf.Max(maxBorn, sp); sumBorn += sp; nBorn++;
        Vector2 cb = C(b);
        foreach (var x in d.InPlay)
        {
            if (x == b) continue;
            bool rx = false; foreach (var r in l.Rack) if (r == x) rx = true;
            if (rx) continue;
            if ((C(x) - cb).magnitude < 0.5f * (x.diameter + b.diameter) + 0.3f) maxNear = Mathf.Max(maxNear, V(x).magnitude);
        }
    }
}
f.Merged -= onMerge;
var esc = d.GetComponent<GumoriEscape>();
return $"évasions {esc.Escapes} | fusions {merges} | slime né : vitesse max {maxBorn:F2} m/s, moyenne {(nBorn > 0 ? sumBorn / nBorn : 0f):F2} | voisins : max {maxNear:F2} m/s";
