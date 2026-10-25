// Pour chaque fusion : montée réelle max (déplacement d'un pas à l'autre) du né et des voisins pendant 1 s, et le contexte.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
var f = d.GetComponent<BlobFusion>();
var u = d.GetComponent<SlimeUntangle>();
var e = d.GetComponent<SlimeEscape>();
d.seed = 2;
LevelRestart.Restart();
Vector2 C(BLOB b) { Vector2 c = Vector2.zero; for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position; return c / Mathf.Max(1, b.CapsuleCount); }
bool InRack(BLOB b) { foreach (var r in l.Rack) if (r == b) return true; return false; }
var events = new System.Collections.Generic.List<(int step, BLOB born, int level, float y, System.Collections.Generic.List<BLOB> near, int unt, int esc, float upBorn, float upNear, string who, bool rackBorn)>();
System.Action<BLOB, int, Vector2> onMerge = (b, lv, at) =>
{
    var near = new System.Collections.Generic.List<BLOB>();
    foreach (var x in d.Blobs) if (x != b && (C(x) - at).magnitude < 0.5f * (x.diameter + b.diameter) + 0.4f) near.Add(x);
    events.Add((LevelSimulation.Step, b, lv, at.y, near, u.Untangled, e.Escapes, 0f, 0f, "", false));
};
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
var last = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
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
    var now = new System.Collections.Generic.Dictionary<BLOB, Vector2>();
    foreach (var b in d.Blobs) now[b] = C(b);
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
