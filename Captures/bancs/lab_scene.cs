// Crée la scène bac à sable de la gelée : copie de `space endless 1` (sur disque), réserve coupée, un GumoriJellyLab.
string src = "Assets/Scenes/Slime/space endless 1.unity", dst = "Assets/Scenes/Slime/space bac à sable.unity";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(dst) == null && !UnityEditor.AssetDatabase.CopyAsset(src, dst)) return "copie impossible";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(dst);
string log = "";
foreach (var root in scene.GetRootGameObjects())
{
    foreach (var l in root.GetComponentsInChildren<GumoriLauncher>(true)) { l.gameObject.SetActive(false); log += "réserve coupée ; "; }
}
var lab = UnityEngine.Object.FindAnyObjectByType<GumoriJellyLab>(FindObjectsInactive.Include);
if (lab == null) { var go = new GameObject("Bac à sable"); lab = go.AddComponent<GumoriJellyLab>(); log += "bac à sable ajouté ; "; }
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
if (skin != null) skin.gallery = false;
var strip = UnityEngine.Object.FindAnyObjectByType<GumoriLevelStrip>();
if (strip != null) strip.enabled = false;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return log + "sauvée " + dst;
