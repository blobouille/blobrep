var src = "Assets/Scenes/Slime/space endless 1.unity";
var dst = "Assets/Scenes/Slime/space niveau 1.unity";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(dst) == null && !UnityEditor.AssetDatabase.CopyAsset(src, dst)) return "copie ratée";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(dst);
var dropper = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var go = dropper.gameObject;
var endless = go.GetComponent<SpaceEndless>();
if (endless != null) UnityEngine.Object.DestroyImmediate(endless);
dropper.seed = 2;
var level = go.GetComponent<SpaceLevel>() ?? go.AddComponent<SpaceLevel>();
level.maxShots = 20;

var jarsGo = UnityEngine.GameObject.Find("Bocaux") ?? new UnityEngine.GameObject("Bocaux");
jarsGo.transform.position = UnityEngine.Vector3.zero;
var jars = jarsGo.GetComponent<GumoriJars>() ?? jarsGo.AddComponent<GumoriJars>();
jars.dropper = dropper;
jars.jars = new System.Collections.Generic.List<GumoriJars.Jar> {
  new GumoriJars.Jar { position = new UnityEngine.Vector2(-3.3f, 0.95f), level = 3, hits = 1 },
  new GumoriJars.Jar { position = new UnityEngine.Vector2(3.4f, 0.85f), level = 2, hits = 1 },
  new GumoriJars.Jar { position = new UnityEngine.Vector2(0f, 7.9f), level = 4, hits = 2 },
};
level.jars = jars;

var millGo = UnityEngine.GameObject.Find("Moulin") ?? new UnityEngine.GameObject("Moulin");
millGo.transform.position = new UnityEngine.Vector3(0f, 3.6f, 0f);
var mill = millGo.GetComponent<GumoriMill>() ?? millGo.AddComponent<GumoriMill>();
mill.blades = 2; mill.length = 1.7f; mill.thickness = 0.2f; mill.hubRadius = 0.35f; mill.speed = 25f; mill.startAngle = 20f;

var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var art = skin.GetComponent<GumoriLevelArt>() ?? skin.gameObject.AddComponent<GumoriLevelArt>();
art.jars = jars;
// Ordre : Bocaux et Moulin juste après le réservoir (avant Lâcher) : physique créée dans cet ordre.
var res = UnityEngine.GameObject.Find("Réservoir");
int idx = res.transform.GetSiblingIndex();
jarsGo.transform.SetSiblingIndex(idx + 1);
millGo.transform.SetSiblingIndex(idx + 2);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok " + scene.path;
