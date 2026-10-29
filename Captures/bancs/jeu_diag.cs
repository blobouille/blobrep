// Diagnostic des scènes Space (lecture seule, test ouverte en additif sans sauver) : hiérarchie du « Jeu » et références clés.
var s = new System.Text.StringBuilder();
void W(Transform t, int d)
{
    s.Append(new string(' ', d * 2) + t.name + (t.gameObject.activeSelf ? "" : " [inactif]") + " :");
    foreach (var c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) s.Append(" " + c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : "")); else if (c == null) s.Append(" MANQUANT");
    s.AppendLine();
    if (d < 1 || !t.name.StartsWith("Gumori ")) foreach (Transform ch in t) if (!ch.name.StartsWith("Gumori ")) W(ch, d + 1);
}
var opened = new System.Collections.Generic.List<UnityEngine.SceneManagement.Scene>();
var t = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath("Assets/Scenes/Slime/test.unity");
if (!t.isLoaded) { t = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Slime/test.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive); opened.Add(t); }
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    s.AppendLine("== " + sc.path);
    foreach (var r in sc.GetRootGameObjects())
    {
        W(r.transform, 0);
        foreach (var d in r.GetComponentsInChildren<GumoriDropper>(true))
            s.AppendLine($"  [dropper] template={(d.template ? d.template.name + (d.template.gameObject.scene == sc ? "" : " AUTRE SCÈNE") : "NULL")} enfants Gumori={d.GetComponentsInChildren<Gumori>(true).Length}");
        foreach (var l in r.GetComponentsInChildren<GumoriLauncher>(true)) s.AppendLine($"  [launcher] actif={l.isActiveAndEnabled} maxRack={l.maxRack}");
    }
}
foreach (var sc in opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sc, true);
return s.ToString();
