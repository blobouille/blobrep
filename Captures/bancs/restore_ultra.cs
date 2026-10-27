// 2026-10-27 : remet dans la vitrine des scènes les ultra rares retirés par erreur (Cristal étincelant, Fée, Braise d'or,
// Nébuleuse étoilée), tels que définis dans GumoriDesign.Variants(). Ne touche à rien d'autre. Scènes SAUVÉES.
var log = new System.Text.StringBuilder();
foreach (var path in new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless 1.unity", "Assets/Scenes/Slime/space niveau 1.unity", "Assets/Scenes/Slime/carte.unity" })
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
    foreach (var skin in UnityEngine.Object.FindObjectsByType<GumoriSkin>(FindObjectsInactive.Include))
    {
        foreach (var v in GumoriDesign.Variants())
            if (!skin.variants.Exists(x => x.name == v.name)) skin.variants.Add(v);
        UnityEditor.EditorUtility.SetDirty(skin);
        log.AppendLine($"{path} : {string.Join(", ", skin.variants.ConvertAll(x => x.name))}");
    }
    log.AppendLine("  sauvé=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
}
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/space endless 1.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
return log.ToString();
