// Relie GumoriDropper.reservoir et GumoriLauncher.box à la courbe de la boîte (celle du décor GumoriBackdrop), scène ouverte.
var back = UnityEngine.Object.FindAnyObjectByType<GumoriBackdrop>();
var curve = back != null ? back.reservoir : null;
if (curve == null) return "pas de courbe";
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
string before = (d.reservoir == null ? "res null" : d.reservoir.name) + " / " + (l == null ? "no launcher" : l.box == null ? "box null" : l.box.name);
UnityEditor.Undo.RecordObject(d, "Lien boîte"); d.reservoir = curve; UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(d);
if (l != null) { UnityEditor.Undo.RecordObject(l, "Lien boîte"); l.box = curve; UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(l); }
var s = UnityEngine.Object.FindAnyObjectByType<GumoriLevelStrip>();
if (s != null) { s.enabled = false; s.enabled = true; }
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(d.gameObject.scene);
return d.gameObject.scene.name + " : avant " + before + " → " + curve.name;
