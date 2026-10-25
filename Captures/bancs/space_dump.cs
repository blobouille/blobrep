var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var sb = new System.Text.StringBuilder();
sb.Append("pas " + LevelSimulation.Step + " réserve " + l.Rack.Count + " gravité " + Unity.U2D.Physics.PhysicsWorld.defaultWorld.gravity + "\n");
foreach (var b in d.InPlay)
{
    Vector2 c = Vector2.zero, v = Vector2.zero;
    for (int k = 0; k < b.CapsuleCount; k++) { c += b.Capsule(k).position; v += b.Capsule(k).linearVelocity; }
    c /= b.CapsuleCount; v /= b.CapsuleCount;
    bool inRack = false;
    foreach (var r in l.Rack) if (r == b) inRack = true;
    sb.Append($"{b.name} niv {f.LevelOf(b)} c ({c.x:F2},{c.y:F2}) v ({v.x:F2},{v.y:F2}) réserve {inRack} fusion {f.IsMerging(b)}\n");
}
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
sb.Append("arrivées " + typeof(GumoriLauncher).GetField("_arrivals", flags).GetValue(l) + " prochaine " + typeof(GumoriLauncher).GetField("_nextArrival", flags).GetValue(l) + " temps " + typeof(GumoriLauncher).GetField("_time", flags).GetValue(l) + " plafond " + typeof(GumoriLauncher).GetField("_ceiling", flags).GetValue(l) + " inner " + typeof(GumoriLauncher).GetField("_inner", flags).GetValue(l));
return sb.ToString();
