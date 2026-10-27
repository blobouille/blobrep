// Diagnostic des bocaux de la scène ouverte : réglages, sens des chaînes (aire signée), corps en Play.
var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + " play=" + Application.isPlaying + "\n";
foreach (var j in UnityEngine.Object.FindObjectsByType<JarShape2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    var p = j.Path; var c = j.GetComponent<CurveCollider2D>();
    s += $"{j.name} actif={j.isActiveAndEnabled} parent={(j.transform.parent ? j.transform.parent.name : "-")} pos={j.transform.position} rot={j.transform.eulerAngles.z} scale={j.transform.lossyScale} drive={j.drive} flip={p.flipSide} mode={p.collisionMode} closed={p.closed} pts={p.points.Count}\n";
    s += $"  r={j.radius} neck={j.neckWidth} belly={j.bellyHeight} round={j.bottomRound} wall={j.wallThickness}\n";
    s += "  collider=" + (c ? c.enabled + " body=" + (c.Body.isValid ? c.Body.type + " " + c.Body.position + " shapes=" + c.Body.shapeCount : "invalide") : "AUCUN") + "\n";
    foreach (var ch in p.BuildCollisionChains()) { float a = 0; for (int i = 0; i < ch.points.Length; i++) { var u = ch.points[i]; var v = ch.points[(i + 1) % ch.points.Length]; a += u.x * v.y - v.x * u.y; } s += $"  chaîne n={ch.points.Length} loop={ch.loop} aire={a / 2:0.000}\n"; }
}
foreach (var r in UnityEngine.Object.FindObjectsByType<CurveCollider2D>(FindObjectsSortMode.None)) { var p = r.GetComponent<CurvePath2D>(); foreach (var ch in p.BuildCollisionChains()) { float a = 0; for (int i = 0; i < ch.points.Length; i++) { var u = ch.points[i]; var v = ch.points[(i + 1) % ch.points.Length]; a += u.x * v.y - v.x * u.y; } s += $"[{r.name}] aire={a / 2:0.000} loop={ch.loop}\n"; } }
return s;
