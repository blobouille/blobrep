// Play (Space) : même bot que merge_latency.cs. À chaque pas, compare les paires de même niveau qui se touchent VRAIMENT
// (contacts Box2D des capsules, manifold non vide) aux paires comptées par GumoriFusion (_touching, tenu par les événements) :
// « ratées » = se touchent mais pas comptées (ne fusionneront pas), « fantômes » = comptées sans se toucher.
// Paramètres : merge_latency.txt "gap tirs".
var args = System.IO.File.Exists("Captures/bancs/merge_latency.txt") ? System.IO.File.ReadAllText("Captures/bancs/merge_latency.txt").Trim().Split(' ') : new string[0];
int gap = args.Length > 0 ? int.Parse(args[0]) : 90;
int maxShots = args.Length > 1 ? int.Parse(args[1]) : 25;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var touching = (System.Collections.IDictionary)typeof(GumoriFusion).GetField("_touching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f);
var one = new System.Collections.Generic.List<uint> { 0u };
LevelRestart.Restart();
int missed = 0, ghost = 0, missedFusing = 0, longest = 0;
var run = new System.Collections.Generic.Dictionary<(Gumori, Gumori), int>();
var episode = new System.Collections.Generic.Dictionary<(Gumori, Gumori), (int start, bool fusing)>();
var fusingPairs = new System.Collections.Generic.HashSet<(Gumori, Gumori)>(); int epNoMerge = 0, epNoMergeFusing = 0, epMerged = 0; var epLog = new System.Text.StringBuilder();
var log = new System.Text.StringBuilder();
int shots = 0;
while (shots < maxShots && LevelSimulation.Step < 12000)
{
    for (int k = 0; k < gap; k++)
    {
        LevelSimulation.FastForward(one, 1);
        var real = new System.Collections.Generic.HashSet<(Gumori, Gumori)>();
        foreach (var g in d.InPlay)
            for (int i = 0; i < g.CapsuleCount; i++)
            {
                var cs = g.Capsule(i).GetContacts(Unity.Collections.Allocator.Temp);
                foreach (var c in cs)
                {
                    if (c.manifold.pointCount == 0) continue;
                    var a = Gumori.FromShape(c.shapeA); var b = Gumori.FromShape(c.shapeB);
                    if (a == null || b == null || a == b || a.diameter != b.diameter || a.sides != b.sides) continue;
                    real.Add(a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a));
                }
                cs.Dispose();
            }
        foreach (var key in real) { bool fz = f.IsMerging(key.Item1) || f.IsMerging(key.Item2); if (fz) fusingPairs.Add(key); if (episode.TryGetValue(key, out var ep)) episode[key] = (ep.start, ep.fusing || fz); else episode[key] = (LevelSimulation.Step, fz); }
        { var ended = new System.Collections.Generic.List<(Gumori, Gumori)>(); foreach (var kv in episode) if (!real.Contains(kv.Key)) ended.Add(kv.Key);
          foreach (var key in ended) { var ep = episode[key]; episode.Remove(key); bool alive = GumoriDropper.IsInPlay(key.Item1) && GumoriDropper.IsInPlay(key.Item2); if (!alive) { epMerged++; continue; } epNoMerge++; if (ep.fusing) epNoMergeFusing++; else epLog.Append($" [pas {ep.start}-{LevelSimulation.Step} niv {f.LevelOf(key.Item1)}]"); } }
        foreach (var key in real)
        {
            if (touching.Contains(key)) { run.Remove(key); continue; }
            bool fusing = f.IsMerging(key.Item1) || f.IsMerging(key.Item2);
            if (fusing) { missedFusing++; continue; }
            missed++;
            run.TryGetValue(key, out int r); run[key] = r + 1; if (r + 1 > longest) longest = r + 1;
            if (r == 30) log.Append($" [pas {LevelSimulation.Step} : niv {f.LevelOf(key.Item1)} collés depuis 30 pas, pas comptés]");
        }
        foreach (System.Collections.DictionaryEntry e in touching) { var key = ((Gumori, Gumori))e.Key; if (!real.Contains(key) && GumoriDropper.IsInPlay(key.Item1) && GumoriDropper.IsInPlay(key.Item2)) ghost++; }
        var gone = new System.Collections.Generic.List<(Gumori, Gumori)>(); foreach (var kv in run) if (!real.Contains(kv.Key)) gone.Add(kv.Key); foreach (var g in gone) run.Remove(g);
    }
    if (l.Rack.Count == 0) continue;
    int bs = -1; Vector2 bt = default; float bc = float.MaxValue;
    for (int s = 0; s < l.Rack.Count; s++)
    {
        var gm = l.Rack[s]; if (gm == null || gm.CapsuleCount == 0) continue;
        foreach (var t in d.InPlay)
        {
            bool inRack = false; foreach (var r in l.Rack) if (r == t) inRack = true;
            if (inRack || t.diameter != gm.diameter) continue;
            float c = Vector2.Distance(gm.Center, t.Center);
            if (c < bc) { bc = c; bs = s; bt = t.Center; }
        }
    }
    if (bs < 0) { bs = 0; bt = new Vector2(0f, 1f); }
    var from = l.Rack[bs].Center; float dist = Vector2.Distance(from, bt); float speed = Mathf.Clamp(dist * 0.9f, 1.5f, 4.4f); float time = dist / speed;
    var aim = bt + Vector2.up * (0.5f * 4f * time * time);
    l.Launch(bs, (aim - from).normalized * speed);
    shots++;
}
int never = 0, ok = 0; foreach (var key in fusingPairs) { if (GumoriDropper.IsInPlay(key.Item1) && GumoriDropper.IsInPlay(key.Item2)) never++; else ok++; }
return $"pas {LevelSimulation.Step} fusions {f.Merges} (dont emmêlés {f.TangleMerges}) démêlages {UnityEngine.Object.FindAnyObjectByType<GumoriUntangle>().Untangled} ; paires collées NON comptées (hors fusion) {missed} pas-paires, plus long {longest} pas ; collées pendant une fusion {missedFusing} ; fantômes {ghost} ; épisodes de contact : fusionnés {epMerged}, séparés SANS fusion {epNoMerge} (dont avec un des deux en fusion {epNoMergeFusing})  ; touchés pendant une fusion : fusionnés ensuite {ok}, JAMAIS fusionnés {never}; autres :{epLog}\n" + log;
