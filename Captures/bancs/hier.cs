var sb = new System.Text.StringBuilder();
void Walk(UnityEngine.Transform t, string ind) {
  var comps = t.GetComponents<UnityEngine.Component>();
  var names = new System.Collections.Generic.List<string>();
  foreach (var c in comps) if (c != null && !(c is UnityEngine.Transform)) names.Add(c.GetType().Name + (c is UnityEngine.Behaviour b && !b.enabled ? "(off)" : ""));
  sb.AppendLine(ind + t.name + (t.gameObject.activeSelf ? "" : " [inactif]") + " @" + t.position + " : " + string.Join(", ", names));
  for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), ind + "  ");
}
foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Walk(r.transform, "");
var box = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>().box;
sb.AppendLine("box pts " + box.center.Count + " thick " + box.thickness);
sb.AppendLine(UnityEngine.JsonUtility.ToJson(box));
return sb.ToString();
