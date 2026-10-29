// Pour chaque fusion : montée réelle max (déplacement d'un pas à l'autre) du né et des voisins pendant 1 s, et le contexte.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var u = d.GetComponent<GumoriUntangle>();
var e = d.GetComponent<GumoriEscape>();
d.seed = 2;
LevelRestart.Restart();
Vector2 C(Gumori b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(Gumori b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var events = new System.Collections.Generic.List<(int step, Gumori born, int level, float y, System.Collections.Generic.List<Gumori> near, int unt, int esc, float upBorn, float upNear, string who, bool rackBorn)>();
System.Action<Gumori, int, Vector2> onMerge = (b, lv, at) =>
{
    var near = new System.Collections.Generic.List<Gumori>();
    foreach (var x in d.InPlay) if (x != b && (C(x) - at).magnitude < 0.5f * (x.diameter + b.diameter) + 0.4f) near.Add(x);
    events.Add((LevelSimulation.Step, b, lv, at.y, near, u.Untangled, e.Escapes, 0f, 0f, "", false));
};
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
var last = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
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
    var now = new System.Collections.Generic.Dictionary<Gumori, Vector2>();
    foreach (var b in d.InPlay) now[b] = C(b);
    for (int k = 0; k < events.Count; k++)
    {
        var ev = events[k];
        if (LevelSimulation.Step > ev.step + 60) continue;
        if (ev.born != null && now.ContainsKey(ev.born) && last.ContainsKey(ev.born))
        {
            float vy = (now[ev.born].y - last[ev.born].y) / Time.fixedDeltaTime;
            if (vy > ev.upBorn) { ev.upBorn = vy; }
            ev.rackBorn |= InRack(ev.born);
        }
        foreach (var x in ev.near)
            if (x != null && now.ContainsKey(x) && last.ContainsKey(x) && !InRack(x))
            {
                float vy = (now[x].y - last[x].y) / Time.fixedDeltaTime;
                if (vy > ev.upNear) { ev.upNear = vy; ev.who = x.name + " niv " + f.LevelOf(x); }
            }
        events[k] = ev;
    }
    last = now;
}
f.Merged -= onMerge;
var sb = new System.Text.StringBuilder();
foreach (var ev in events)
    if (ev.upBorn > 2.5f || ev.upNear > 2.5f)
        sb.Append($"[pas {ev.step} niv {ev.level} y {ev.y:F1} né monte {ev.upBorn:F1} (réserve {ev.rackBorn}) voisin monte {ev.upNear:F1} {ev.who} | démêlages +{u.Untangled - ev.unt} évasions +{e.Escapes - ev.esc}]\n");
return $"fusions {events.Count}\n" + sb;
