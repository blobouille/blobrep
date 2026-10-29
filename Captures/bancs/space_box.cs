// Scène space endless SEULEMENT : boîte carrée (9 × 9) à fond en arc de cercle (flèche 2,5 m), mousse, grabber souris coupé.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!scene.path.Contains("space endless")) return "PAS la scène space endless : " + scene.path;
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var curve = d.reservoir;
UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { curve, d.gameObject }, "Space boîte");
float w = 4.5f, sag = 2.5f, wall = 9f;
float R = (w * w + sag * sag) / (2f * sag);
float half = Mathf.Asin(w / R);
float k = 4f / 3f * Mathf.Tan(half / 4f) * R;
var t = new Vector2(Mathf.Cos(half), -Mathf.Sin(half)) * k; // tangente au départ de l'arc, vers le bas
Vector2 tl = new Vector2(-w, sag + wall), bl = new Vector2(-w, sag), bm = Vector2.zero, br = new Vector2(w, sag), tr = new Vector2(w, sag + wall);
curve.points.Clear();
curve.points.Add(new CurvePoint(tl, HandleMode.Free));
var p1 = new CurvePoint(bl, HandleMode.Free); p1.handleOut = t; curve.points.Add(p1);
var p2 = new CurvePoint(bm, HandleMode.Free); p2.handleIn = new Vector2(-k, 0f); p2.handleOut = new Vector2(k, 0f); curve.points.Add(p2);
var p3 = new CurvePoint(br, HandleMode.Free); p3.handleIn = new Vector2(-t.x, t.y); curve.points.Add(p3);
curve.points.Add(new CurvePoint(tr, HandleMode.Free));
curve.closed = true;
curve.Rebuild();
var foam = d.GetComponent<GumoriFoam>();
if (foam == null) foam = UnityEditor.Undo.AddComponent<GumoriFoam>(d.gameObject);
var grabbers = new System.Text.StringBuilder();
foreach (var g in UnityEngine.Object.FindObjectsByType<LowLevelRaycastGrabber>(FindObjectsSortMode.None))
{
    UnityEditor.Undo.RecordObject(g, "Space grabber");
    g.enabled = false;
    UnityEditor.EditorUtility.SetDirty(g);
    grabbers.Append(g.gameObject.name + " ");
}
UnityEditor.EditorUtility.SetDirty(curve);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
float minY = 1e9f, maxY = -1e9f;
foreach (var p in curve.center) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
return $"boîte R={R:F2} k={k:F2} points centre={curve.center.Count} y local {minY:F2}..{maxY:F2} | mousse ok | grabbers coupés : {grabbers}";
