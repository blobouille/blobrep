// 2026-10-27 : Vanille (crème ivoire, cerise rouge) devient la version de base du niveau crème, à la place de Chantilly ; la
// vitrine ne garde que Braise bleue (les autres variantes retirées). Ne touche que GumoriSkin.designs / variants et la couleur
// crème de GumoriFusion.levelColors (les autres réglages de la scène, ceux de Braise bleue compris, restent). Scène SAUVÉE.
var log = new System.Text.StringBuilder();
foreach (var path in new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless 1.unity", "Assets/Scenes/Slime/space niveau 1.unity", "Assets/Scenes/Slime/carte.unity" })
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
    var vanille = GumoriDesign.Levels().Find(x => x.family == GumoriFamily.Cream);
    foreach (var skin in UnityEngine.Object.FindObjectsByType<GumoriSkin>(FindObjectsInactive.Include))
    {
        int i = skin.designs.FindIndex(x => x.family == GumoriFamily.Cream);
        string was = i >= 0 ? skin.designs[i].name : "-";
        if (i >= 0) skin.designs[i] = vanille;
        int before = skin.variants.Count;
        skin.variants.RemoveAll(x => x.name != "Braise bleue");
        UnityEditor.EditorUtility.SetDirty(skin);
        log.AppendLine($"{path} : crème {was} → Vanille ; variantes {before} → {skin.variants.Count}");
    }
    foreach (var fusion in UnityEngine.Object.FindObjectsByType<GumoriFusion>(FindObjectsInactive.Include))
    {
        if (fusion.levelColors.Length > 5) fusion.levelColors[5] = new Color(0.965f, 0.896f, 0.769f);
        UnityEditor.EditorUtility.SetDirty(fusion);
    }
    log.AppendLine("  sauvé=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
}
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/space endless 1.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
return log.ToString();
