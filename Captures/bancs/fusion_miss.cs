// Chocs ratés (Space) : à chaque début de contact entre deux gumori de même taille, regarde au pas suivant s'ils ont fusionné.
// Sinon : « bloqué » si l'un des deux était encore en fusion, « rebond » si le contact a commencé et fini dans le même pas
// (Box2D l'a déjà fait rebondir : la fusion, lue au pas suivant, ne le voit plus). Partie scriptée de fusion_compare.cs,
// méthode et réglages lus dans fusion_style.txt (0 / 1).
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var l = d.GetComponent<GumoriLauncher>();
var goal = d.GetComponent<GumoriGoal>();
if (goal != null) goal.useGoal = false;
var opts = System.IO.File.ReadAllText("Captures/bancs/fusion_style.txt").Trim().Split(' ');
f.style = opts[0] == "1" ? FusionStyle.Absorb : FusionStyle.Peanut;
d.seed = 2;
foreach (var o in opts) { var kv = o.Split('='); if (kv.Length == 2 && kv[0] == "seed") d.seed = int.Parse(kv[1]); }
var one = new System.Collections.Generic.List<uint> { 0u };
var pending = new System.Collections.Generic.List<(Gumori a, Gumori b, bool fusing, bool ended)>();
int hits = 0, merged = 0, blocked = 0, bounced = 0, other = 0;
LevelRestart.Restart();
while (LevelSimulation.Step < 1800)
{
    int i = LevelSimulation.Step;
    uint mk = 0;
    if (l == null)
    {
        if (i % 70 < 2) mk |= 1u << (int)GameKey.Space;
        int phase = (i / 90) % 3;
        if (phase == 0) mk |= 1u << (int)GameKey.RightArrow; else if (phase == 1) mk |= 1u << (int)GameKey.LeftArrow;
    }
    else if (i % 40 == 39 && l.Rack.Count > 0)
    {
        int s = i / 40;
        float ang = -90f + ((s * 37) % 140 - 70);
        l.Launch((s * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * 4.44f);
    }
    one[0] = mk;
    LevelSimulation.FastForward(one, 1);
    foreach (var p in pending)
    {
        if (!GumoriDropper.IsInPlay(p.a) || !GumoriDropper.IsInPlay(p.b)) merged++;
        else if (p.fusing) blocked++;
        else if (p.ended) bounced++;
        else other++;
    }
    pending.Clear();
    var world = Unity.U2D.Physics.PhysicsWorld.defaultWorld;
    foreach (var e in world.contactBeginEvents)
    {
        var a = Gumori.FromShape(e.shapeA); var b = Gumori.FromShape(e.shapeB);
        if (a == null || b == null || a == b || a.diameter != b.diameter || a.sides != b.sides) continue;
        bool dup = false;
        foreach (var p in pending) if ((p.a == a && p.b == b) || (p.a == b && p.b == a)) dup = true;
        if (dup) continue;
        bool ended = false;
        foreach (var x in world.contactEndEvents)
        {
            var ea = Gumori.FromShape(x.shapeA); var eb = Gumori.FromShape(x.shapeB);
            if ((ea == a && eb == b) || (ea == b && eb == a)) ended = true;
        }
        hits++;
        pending.Add((a, b, f.IsMerging(a) || f.IsMerging(b), ended));
    }
}
return $"{f.style} graine {d.seed} : chocs entre pareils {hits} (paires, 1er contact du pas), fusionnés au pas suivant {merged}, bloqués (l'un en fusion) {blocked}, " +
       $"rebond dans le même pas {bounced}, autre {other} ; fusions {f.Merges}";
