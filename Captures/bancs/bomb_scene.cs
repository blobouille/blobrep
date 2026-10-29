// Crée la scène d'essai des bombes (copie de « 16 1 ») et liste sa hiérarchie.
var dst = "Assets/Scenes/Slime/bombes (essai).unity";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(dst) == null) UnityEditor.AssetDatabase.CopyAsset("Assets/Scenes/Slime/16 1.unity", dst);
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(dst);
var sb = new System.Text.StringBuilder();
System.Action<UnityEngine.Transform, string> walk = null;
walk = (t, pad) => {
    var comps = new System.Text.StringBuilder();
    foreach (var c in t.GetComponents<UnityEngine.Component>()) if (!(c is UnityEngine.Transform)) comps.Append(c.GetType().Name + (c is UnityEngine.Behaviour b && !b.enabled ? "(off)" : "") + " ");
    sb.AppendLine(pad + t.name + (t.gameObject.activeSelf ? "" : " [inactif]") + " : " + comps + " @" + (UnityEngine.Vector2)t.position);
    foreach (UnityEngine.Transform ch in t) walk(ch, pad + "  ");
};
foreach (var r in scene.GetRootGameObjects()) walk(r.transform, "");
return sb.ToString();
