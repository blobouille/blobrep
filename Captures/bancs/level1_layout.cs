var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!scene.path.EndsWith("space niveau 1.unity")) return "mauvaise scène " + scene.path;
var jars = UnityEngine.Object.FindAnyObjectByType<GumoriJars>();
jars.jars = new System.Collections.Generic.List<GumoriJars.Jar> {
  new GumoriJars.Jar { position = new UnityEngine.Vector2(-3.3f, 0.95f), level = 3, hits = 1 },
  new GumoriJars.Jar { position = new UnityEngine.Vector2(0f, 1.05f), level = 4, hits = 2 },
  new GumoriJars.Jar { position = new UnityEngine.Vector2(3.4f, 0.85f), level = 2, hits = 1 },
};
var mill = UnityEngine.Object.FindAnyObjectByType<GumoriMill>();
mill.transform.position = new UnityEngine.Vector3(0f, 4.7f, 0f);
UnityEditor.EditorUtility.SetDirty(jars); UnityEditor.EditorUtility.SetDirty(mill.transform);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok";
