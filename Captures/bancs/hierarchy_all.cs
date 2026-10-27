// Hiérarchie (objets + composants) de toutes les scènes Slime, ouvertes en additif puis refermées sans sauver.
var s = new System.Text.StringBuilder();
void W(Transform t, int d)
{
    s.Append(new string(' ', d * 2) + t.name + " :");
    foreach (var c in t.GetComponents<Component>()) s.Append(" " + (c == null ? "MANQUANT" : c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : "")));
    s.AppendLine(t.gameObject.activeSelf ? "" : " [inactif]");
    foreach (Transform ch in t) W(ch, d + 1);
}
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
s.AppendLine("ouverte : " + active.path + " modifiée=" + active.isDirty);
foreach (var path in new[] { "Assets/Scenes/Slime/reservoir.unity", "Assets/Scenes/Slime/space endless 1.unity", "Assets/Scenes/Slime/space niveau 1.unity", "Assets/Scenes/Slime/carte.unity" })
{
    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
    bool opened = !sc.isLoaded;
    if (opened) sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    s.AppendLine("== " + path);
    foreach (var r in sc.GetRootGameObjects()) W(r.transform, 0);
    if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sc, true);
}
return s.ToString();
