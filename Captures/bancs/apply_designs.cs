// Applique les designs par défaut (nouvel ordre des familles) et les couleurs de niveau à toutes les scènes du jeu de slimes.
// Seulement l'habillage : GumoriSkin.designs / variants et GumoriFusion.levelColors. Rien d'autre n'est touché.
var paths = new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless.unity",
    "Assets/Scenes/Slime/space endless 1.unity", "Assets/Scenes/Slime/carte.unity" };
var colors = new Color[]
{
    new Color(0.352f, 0.065f, 0.730f), new Color(1.000f, 0.371f, 0.638f), new Color(0.050f, 0.238f, 0.855f),
    new Color(1.000f, 0.658f, 0.058f), new Color(0.745f, 0.030f, 0.050f), new Color(1.000f, 0.863f, 0.896f),
    new Color(0.195f, 0.060f, 0.018f), new Color(0.258f, 0.672f, 0.042f), new Color(0.027f, 0.019f, 0.040f),
    new Color(0.991f, 0.991f, 1.000f)
};
var log = new System.Text.StringBuilder();
foreach (var path in paths)
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(path);
    bool wasOpen = scene.IsValid() && scene.isLoaded;
    bool userDirty = wasOpen && scene.isDirty; // modifications de l'utilisateur pas enregistrées : ne pas enregistrer à sa place
    if (!wasOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    int skins = 0, fusions = 0;
    foreach (var root in scene.GetRootGameObjects())
    {
        foreach (var skin in root.GetComponentsInChildren<GumoriSkin>(true))
        {
            UnityEditor.Undo.RecordObject(skin, "Designs gumori");
            skin.designs = GumoriSkin.DefaultDesigns();
            skin.variants = GumoriDesign.Variants();
            UnityEditor.EditorUtility.SetDirty(skin);
            skins++;
        }
        foreach (var fusion in root.GetComponentsInChildren<GumoriFusion>(true))
        {
            UnityEditor.Undo.RecordObject(fusion, "Couleurs gumori");
            fusion.levelColors = (Color[])colors.Clone();
            UnityEditor.EditorUtility.SetDirty(fusion);
            fusions++;
        }
    }
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    bool saved = !userDirty && UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    if (!wasOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    log.AppendLine($"{path}: {skins} habillage(s), {fusions} fusion(s), enregistrée {saved}");
}
return log.ToString();
