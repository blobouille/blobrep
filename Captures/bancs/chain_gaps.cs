// Plus petit écart entre deux points de chaîne de chaque CurveCollider2D des scènes de jeu (ouvertes en additif, sans sauver).
var s = "";
foreach (var path in new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless 1.unity", "Assets/Scenes/Slime/space niveau 1.unity", "Assets/Scenes/Slime/carte.unity" })
{
    var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    foreach (var root in sc.GetRootGameObjects())
        foreach (var c in root.GetComponentsInChildren<CurveCollider2D>(true))
        {
            var p = c.GetComponent<CurvePath2D>(); p.Rebuild();
            float mn = 99;
            foreach (var ch in p.BuildCollisionChains()) for (int i = 0; i < ch.points.Length; i++) mn = Mathf.Min(mn, ((Vector2)c.transform.TransformPoint(ch.points[i]) - (Vector2)c.transform.TransformPoint(ch.points[(i + 1) % ch.points.Length])).magnitude);
            s += $"{System.IO.Path.GetFileNameWithoutExtension(path)}/{c.name}: min {mn:0.0000}\n";
        }
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sc, true);
}
return s;
