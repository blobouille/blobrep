// Play : géométrie des bocaux (ligne médiane du verre) et gumori en jeu.
var s = "";
foreach (var jar in UnityEngine.Object.FindObjectsByType<JarShape2D>(FindObjectsInactive.Exclude))
{
    s += $"{jar.name} pos {jar.transform.position} r {jar.radius} neck {jar.neckWidth} innerTop {jar.InnerTop} neckHalf {jar.InnerNeckHalf} | ";
    var c = jar.Path.center; s += "pts " + c.Count + ": ";
    for (int i = 0; i < c.Count; i += Mathf.Max(1, c.Count / 30)) { var p = jar.transform.TransformPoint(c[i]); s += $"({p.x:0.00},{p.y:0.00}) "; }
}
var f = UnityEngine.Object.FindAnyObjectByType<GumoriFusion>();
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
s += $" | style {f.style} ; inplay {d.InPlay.Count}: ";
foreach (var g in d.InPlay) s += $"[Ø{g.diameter:0.00} {g.Center.x:0.00},{g.Center.y:0.00} n{g.CapsuleCount}] ";
return s;
