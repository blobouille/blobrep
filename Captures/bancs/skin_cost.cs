// Coût de l'habillage (GumoriSkin.LateUpdate appelé à la main, image figée en Play) : ms par image, meilleur de 5 séries de 50.
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var m = typeof(GumoriSkin).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var frameField = typeof(GumoriSkin).GetField("_frame", bf);
double best = double.MaxValue;
for (int s = 0; s < 5; s++)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < 50; i++) { m.Invoke(skin, null); }
    best = System.Math.Min(best, sw.Elapsed.TotalMilliseconds / 50);
}
int count = ((System.Collections.IDictionary)typeof(GumoriSkin).GetField("_looks", bf).GetValue(skin)).Count;
return $"habillage {best:0.000} ms par image, {count} gumori suivis";
