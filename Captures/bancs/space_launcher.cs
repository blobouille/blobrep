// Scène space endless SEULEMENT : boîte fermée plus haute, lanceur, lâcher d'origine neutralisé (touches inutilisées, aperçus coupés).
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!scene.path.Contains("space endless")) return "PAS la scène space endless : " + scene.path;
var top = float.Parse(System.IO.File.ReadAllText("Captures/bancs/space_top.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture);
var d = UnityEngine.Object.FindAnyObjectByType<BlobDropper>();
var curve = d.reservoir;
var skin = UnityEngine.Object.FindAnyObjectByType<SlimeSkin>();
var fx = UnityEngine.Object.FindAnyObjectByType<SlimeFx>();
var hold = d.GetComponent<SlimeWallHold>();
UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { d, curve, skin, fx, hold, d.gameObject }, "Space lanceur");
for (int i = 0; i < curve.points.Count; i++)
{
    var p = curve.points[i];
    if (p.position.y > 1f) { p.position.y = top; curve.points[i] = p; }
}
curve.closed = true;
curve.Rebuild();
d.dropKey = FlipperKey.RightShift; d.leftKey = FlipperKey.RightShift; d.rightKey = FlipperKey.RightShift;
d.debugDraw = false; d.showStats = false; d.nextPreviewViewport = new Vector2(3f, 3f);
if (skin != null) { skin.drawPreviews = false; skin.watchDrop = false; }
if (fx != null) fx.aimDots = false;
if (hold != null) hold.wallTop = 100f;
var l = d.GetComponent<BlobLauncher>() ?? UnityEditor.Undo.AddComponent<BlobLauncher>(d.gameObject);
l.box = curve;
foreach (var o in new UnityEngine.Object[] { d, curve, skin, fx, hold, l }) if (o != null) UnityEditor.EditorUtility.SetDirty(o);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var comps = new System.Text.StringBuilder();
foreach (var c in d.GetComponents<MonoBehaviour>()) comps.Append(c.GetType().Name + " ");
return "ok haut " + top + " fermée " + curve.closed + " | " + comps;
