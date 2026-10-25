// Coût CPU par image des scripts de la carte (moyenne sur 200 appels) et du pas fixe de l'avatar.
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var map = UnityEngine.Object.FindAnyObjectByType<GumoriMap>();
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var menu = UnityEngine.Object.FindAnyObjectByType<GumoriMapMenu>();
var av = UnityEngine.Object.FindAnyObjectByType<GumoriMapAvatar>();
var mapLate = typeof(GumoriMap).GetMethod("LateUpdate", flags);
var skinLate = typeof(GumoriSkin).GetMethod("LateUpdate", flags);
var menuUpdate = typeof(GumoriMapMenu).GetMethod("Update", flags);
double Time(System.Action a)
{
    for (int i = 0; i < 10; i++) a();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < 200; i++) a();
    return sw.Elapsed.TotalMilliseconds / 200.0;
}
double m = Time(() => mapLate.Invoke(map, null));
double s = Time(() => skinLate.Invoke(skin, null));
double u = Time(() => menuUpdate.Invoke(menu, null));
double t = Time(() => { av.LevelTick(); });
var sw2 = System.Diagnostics.Stopwatch.StartNew();
map.enabled = false; map.enabled = true;
double build = sw2.Elapsed.TotalMilliseconds;
return $"GumoriMap.LateUpdate {m:F3} ms, GumoriSkin (avatar) {s:F3} ms, menu {u:F3} ms par image ; pas fixe avatar+blob {t * 1000:F1} µs ; construction complète de la carte {build:F0} ms";
