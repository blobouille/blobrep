var paths = new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless.unity", "Assets/Scenes/Slime/carte.unity" };
var log = "";
foreach (var p in paths)
{
    var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(p, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    int n = 0;
    foreach (var root in sc.GetRootGameObjects())
        foreach (var s in root.GetComponentsInChildren<SlimeSkin>(true))
        {
            var so = new UnityEditor.SerializedObject(s);
            so.FindProperty("rimWidth").floatValue = 0.074f;
            so.FindProperty("rimOpacity").floatValue = 0.538f;
            so.FindProperty("rimLighten").floatValue = 0.35f;
            so.ApplyModifiedProperties();
            n++;
        }
    if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sc, true);
    log += p + " : " + n + " habillage(s) ; ";
}
return log;
