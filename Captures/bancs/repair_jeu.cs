// Répare le « Jeu » des scènes Space (2026-10-28) : GumoriDropper disparu remis depuis la version commitée (copie dans
// Assets/Temp/ref_<scène>.unity, ignorée par git), modèle relié au « Blob modèle (désactivé) » de la scène, références
// vides de type GumoriDropper recâblées, sous-objets remis dans l'ordre des LevelTick (Fusion, Règles, Réserve, Corps). SAUVE.
UnityEditor.AssetDatabase.Refresh();
var log = new System.Text.StringBuilder();
foreach (var name in new[] { "space endless 1", "test" })
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Scenes/Slime/{name}.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
    var refScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Temp/ref_{name}.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
    GumoriDropper old = null;
    foreach (var r in refScene.GetRootGameObjects()) { old = r.GetComponentInChildren<GumoriDropper>(true); if (old) break; }
    GameObject jeu = null, model = null;
    foreach (var r in scene.GetRootGameObjects()) { if (r.name == "Jeu") jeu = r; if (r.GetComponent<Gumori>() && !r.activeSelf) model = r; }
    var d = jeu.GetComponent<GumoriDropper>() ?? jeu.AddComponent<GumoriDropper>();
    UnityEditor.EditorUtility.CopySerialized(old, d);
    d.template = model.GetComponent<Gumori>();
    int rewired = 0;
    foreach (var r in scene.GetRootGameObjects())
        foreach (var mb in r.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var so = new UnityEditor.SerializedObject(mb); var it = so.GetIterator(); bool ch = false;
            while (it.Next(true))
                if (it.propertyType == UnityEditor.SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.type.Contains("GumoriDropper")) { it.objectReferenceValue = d; ch = true; rewired++; }
            if (ch) so.ApplyModifiedPropertiesWithoutUndo();
        }
    string[] order = { "Fusion", "Règles", "Réserve", "Corps" };
    for (int i = 0; i < order.Length; i++) { var c = jeu.transform.Find(order[i]); if (c) c.SetSiblingIndex(i); }
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(refScene, true);
    string kids = ""; foreach (Transform c in jeu.transform) kids += " " + c.name;
    log.AppendLine($"{name} : dropper remis (seed {d.seed}, modèle {d.template.name}), recâblés {rewired}, ordre :{kids}, sauvé {UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)}");
}
return log.ToString();
