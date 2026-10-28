// Ajoute GumoriLevelStrip (bandeau des stades) à l'objet « Habillage » des scènes Space du jeu libre. SAUVE.
var log = "";
foreach (var name in new[] { "space endless 1", "test" })
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Scenes/Slime/{name}.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
    var hab = GameObject.Find("Habillage");
    if (hab.GetComponent<GumoriLevelStrip>() == null) hab.AddComponent<GumoriLevelStrip>();
    log += name + " : sauvé " + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene) + " ; ";
}
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/space endless 1.unity");
return log;
