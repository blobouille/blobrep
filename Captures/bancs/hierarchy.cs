// Hiérarchie de toutes les scènes Slime : objets et composants (lecture seule, ouvre chaque scène en additif sans sauver).
var s = new System.Text.StringBuilder();
void W(Transform t, int d)
{
    s.Append(new string(' ', d * 2) + t.name + " :");
    foreach (var c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) s.Append(" " + c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : ""));
    s.AppendLine(t.gameObject.activeSelf ? "" : " [inactif]");
    foreach (Transform ch in t) W(ch, d + 1);
}
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    s.AppendLine("== " + sc.path);
    foreach (var r in sc.GetRootGameObjects()) W(r.transform, 0);
}
return s.ToString();
