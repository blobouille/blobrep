// Dessine le contour (paroi épaisse, chaînes de collision) de chaque bocal de la scène, avec 3 réglages, dans Captures/niveau/bocal_formes.png.
var jar = UnityEngine.Object.FindAnyObjectByType<JarShape2D>();
var path = jar.Path;
int W = 900, H = 400; float scale = 70f;
var tex = new Texture2D(W, H);
var px = new Color[W * H]; for (int i = 0; i < px.Length; i++) px[i] = new Color(0.08f, 0.08f, 0.12f); tex.SetPixels(px);
void Line(Vector2 a, Vector2 b, Color c, Vector2 o) { int n = (int)((b - a).magnitude * scale * 2) + 1; for (int i = 0; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n) * scale + o; tex.SetPixel((int)p.x, (int)p.y, c); } }
var settings = new (float r, float neck, float round, float belly)[] { (1.2f, 1f, 0.5f, 1f), (1f, 0.6f, 1f, 0.5f), (1.4f, 2.8f, 0f, 1.5f) };
var log = "";
var keep = (jar.radius, jar.neckWidth, jar.bottomRound, jar.bellyHeight);
for (int k = 0; k < settings.Length; k++)
{
    jar.radius = settings[k].r; jar.neckWidth = settings[k].neck; jar.bottomRound = settings[k].round; jar.bellyHeight = settings[k].belly; jar.Apply();
    var o = new Vector2(150 + k * 300, 40);
    foreach (var ch in path.BuildCollisionChains()) for (int i = 0; i < ch.points.Length; i++) Line(ch.points[i], ch.points[(i + 1) % ch.points.Length], Color.cyan, o);
    for (int i = 1; i < path.center.Count; i++) Line(path.center[i - 1], path.center[i], new Color(1, 0.5f, 0.2f, 1), o);
    Line(new Vector2(-jar.InnerNeckHalf, jar.InnerTop), new Vector2(jar.InnerNeckHalf, jar.InnerTop), Color.yellow, o);
    Line(new Vector2(-jar.radius, 0), new Vector2(jar.radius, 0), Color.green, o);
    log += $"[{k}] pts {path.center.Count} selfX {path.leftSelfIntersects}/{path.rightSelfIntersects}; ";
}
jar.radius = keep.Item1; jar.neckWidth = keep.Item2; jar.bottomRound = keep.Item3; jar.bellyHeight = keep.Item4; jar.Apply();
tex.Apply();
System.IO.File.WriteAllBytes("Captures/niveau/bocal_formes.png", tex.EncodeToPNG());
return log;
