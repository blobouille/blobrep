// Pose (sans sauver) une trajectoire horizontale + un bocal dessus dans la scène ouverte ; rend la géométrie du bocal.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var bd = UnityEngine.Object.FindAnyObjectByType<GumoriBackdrop>();
var go = new GameObject("Test trajectoire");
var curve = go.AddComponent<CurvePath2D>();
curve.thickness = 0.05f;
curve.points = new System.Collections.Generic.List<CurvePoint> { new CurvePoint(new Vector2(-2.5f, 0), HandleMode.Vector), new CurvePoint(new Vector2(2.5f, 0), HandleMode.Vector) };
curve.RecomputeHandles(); curve.Rebuild();
var mp = go.AddComponent<MovingPath>();
mp.speed = 0.6f;
go.transform.position = new Vector3(0, -2.6f, 0);
var jar = new GameObject("Bocal");
jar.AddComponent<CurvePath2D>(); jar.AddComponent<CurveCollider2D>(); var js = jar.AddComponent<JarShape2D>(); js.Apply();
jar.transform.SetParent(go.transform, false);
mp.SyncPositions();
var p = jar.GetComponent<CurvePath2D>();
return scene.path + " backdrop.reservoir=" + (bd != null && bd.reservoir != null ? bd.reservoir.name : "null") + " pts=" + p.points.Count + " center=" + p.center.Count + " chains=" + p.BuildCollisionChains().Count + " selfX=" + p.leftSelfIntersects + p.rightSelfIntersects;
