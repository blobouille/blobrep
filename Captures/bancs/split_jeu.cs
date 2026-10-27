// Refonte du 2026-10-27 : range les composants de l'objet « Lâcher » (tout le jeu sur un seul objet) dans des sous-objets,
// dans l'ordre où ils tournaient (ordre de la hiérarchie = ordre des LevelTick). Retire GumoriSoftMerge, et GumoriWallHold
// (coupé) des scènes Space. Valeurs copiées, références des autres objets recâblées. Scène lue dans split_jeu.txt, SAUVÉE.

var path = System.IO.File.ReadAllText("Captures/bancs/split_jeu.txt").Trim();
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
var dropper = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>(FindObjectsInactive.Include);
var root = dropper.gameObject;
var log = new System.Text.StringBuilder(path + "\n");
bool space = root.GetComponent<GumoriLauncher>() != null;

foreach (var c in root.GetComponents<GumoriSoftMerge>()) { log.AppendLine("retiré SoftMerge (actif=" + c.enabled + ")"); UnityEngine.Object.DestroyImmediate(c); }
if (space) foreach (var c in root.GetComponents<GumoriWallHold>()) { log.AppendLine("retiré WallHold (actif=" + c.enabled + ")"); UnityEngine.Object.DestroyImmediate(c); }

var groups = new (string name, System.Type[] types)[]
{
    ("Fusion", new[] { typeof(GumoriFusion), typeof(GumoriEscape) }),
    ("Règles", new[] { typeof(GumoriScore), typeof(GumoriGoal), typeof(GumoriLevelSetup), typeof(SpaceEndless), typeof(SpaceLevel) }),
    ("Réserve", new[] { typeof(GumoriLauncher) }),
    ("Corps", new[] { typeof(GumoriWallHold), typeof(GumoriFoam), typeof(GumoriUntangle) }),
};

var moved = new System.Collections.Generic.List<(Component from, Component to)>();
foreach (var (name, types) in groups)
{
    GameObject child = null;
    foreach (var t in types)
    {
        var old = root.GetComponent(t);
        if (old == null) continue;
        if (child == null)
        {
            child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
        }
        var now = child.AddComponent(t);
        UnityEditor.EditorUtility.CopySerialized(old, now);
        moved.Add((old, now));
        log.AppendLine($"{t.Name} → {name}");
    }
}
foreach (var (from, to) in moved) ((Behaviour)to).enabled = ((Behaviour)from).enabled;

int rewired = 0;
foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
{
    var so = new UnityEditor.SerializedObject(mb);
    var it = so.GetIterator();
    bool changed = false;
    while (it.Next(true))
    {
        if (it.propertyType != UnityEditor.SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
        foreach (var (from, to) in moved)
            if (it.objectReferenceValue == from) { it.objectReferenceValue = to; changed = true; rewired++; log.AppendLine($"recâblé {mb.GetType().Name}.{it.propertyPath}"); }
    }
    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
}

for (int i = moved.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(moved[i].from);
root.name = "Jeu";

var left = "";
foreach (var c in root.GetComponents<Component>()) left += " " + c.GetType().Name;
log.AppendLine("reste sur Jeu :" + left + " ; recâblages " + rewired);
log.AppendLine("sauvé=" + UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene));
return log.ToString();
