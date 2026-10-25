// Scène space endless SEULEMENT : boîte fermée plus haute, lanceur, lâcher d'origine neutralisé (touches inutilisées, aperçus coupés).
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!scene.path.Contains("space endless")) return "PAS la scène space endless : " + scene.path;
var top = float.Parse(System.IO.File.ReadAllText("Captures/bancs/space_top.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var curve = d.reservoir;
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var fx = UnityEngine.Object.FindAnyObjectByType<GumoriFx>();
var hold = d.GetComponent<GumoriWallHold>();
UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { d, curve, skin, fx, hold, d.gameObject }, "Space lanceur");
for (int i = 0; i < curve.points.Count; i++)
{
    var p = curve.points[i];
    if (p.position.y > 1f) { p.position.y = top; curve.points[i] = p; }
}
curve.closed = true;
curve.Rebuild();
d.dropKey = GameKey.RightShift; d.leftKey = GameKey.RightShift; d.rightKey = GameKey.RightShift;
d.debugDraw = false; d.showStats = false; d.nextPreviewViewport = new Vector2(3f, 3f);
if (skin != null) { skin.drawPreviews = false; skin.watchDrop = false; }
if (fx != null) fx.aimDots = false;
if (hold != null) hold.wallTop = 100f;
var l = d.GetComponent<GumoriLauncher>() ?? UnityEditor.Undo.AddComponent<GumoriLauncher>(d.gameObject);
l.box = curve;
foreach (var o in new UnityEngine.Object[] { d, curve, skin, fx, hold, l }) if (o != null) UnityEditor.EditorUtility.SetDirty(o);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var comps = new System.Text.StringBuilder();
foreach (var c in d.GetComponents<MonoBehaviour>()) comps.Append(c.GetType().Name + " ");
return "ok haut " + top + " fermée " + curve.closed + " | " + comps;
