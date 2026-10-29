// Valeurs d'inspecteur de tous les composants de la scène ouverte (hors Play), une ligne par champ sérialisé, triées par
// chemin dans la hiérarchie : à comparer avant / après une refonte (renommer une classe ne doit changer aucune valeur).
// Écrit dans Captures/bancs/values_<scène>_<argument>.txt (argument lu dans Captures/bancs/values_tag.txt).
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
string tag = System.IO.File.Exists("Captures/bancs/values_tag.txt") ? System.IO.File.ReadAllText("Captures/bancs/values_tag.txt").Trim() : "x";
var lines = new System.Collections.Generic.List<string>();
string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
foreach (var root in scene.GetRootGameObjects())
    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
    {
        if (c == null) { lines.Add("MISSING SCRIPT"); continue; }
        var so = new UnityEditor.SerializedObject(c);
        var it = so.GetIterator();
        bool enter = true;
        string owner = Path(c.transform) + " [" + c.GetType().Name.Replace("GumoriDropper", "GumoriDropper").Replace("GumoriFusion", "GumoriFusion").Replace("GumoriLauncher", "GumoriLauncher").Replace("Gumori", "Gumori").Replace("Slime", "Gumori") + "]";
        while (it.Next(enter))
        {
            enter = it.propertyType == UnityEditor.SerializedPropertyType.Generic;
            if (it.propertyPath == "m_Script") continue;
            string v;
            switch (it.propertyType)
            {
                case UnityEditor.SerializedPropertyType.Float: v = it.floatValue.ToString("R"); break;
                case UnityEditor.SerializedPropertyType.Integer: case UnityEditor.SerializedPropertyType.Enum: v = it.intValue.ToString(); break;
                case UnityEditor.SerializedPropertyType.Boolean: v = it.boolValue.ToString(); break;
                case UnityEditor.SerializedPropertyType.String: v = it.stringValue; break;
                case UnityEditor.SerializedPropertyType.Color: v = it.colorValue.ToString("R"); break;
                case UnityEditor.SerializedPropertyType.Vector2: v = it.vector2Value.ToString("R"); break;
                case UnityEditor.SerializedPropertyType.Vector3: v = it.vector3Value.ToString("R"); break;
                case UnityEditor.SerializedPropertyType.ObjectReference: v = it.objectReferenceValue != null ? it.objectReferenceValue.name : "null"; break;
                case UnityEditor.SerializedPropertyType.AnimationCurve: v = string.Join(";", System.Array.ConvertAll(it.animationCurveValue.keys, k => k.time.ToString("R") + ":" + k.value.ToString("R"))); break;
                default: continue;
            }
            lines.Add(owner + " " + it.propertyPath + " = " + v);
        }
    }
lines.Sort(System.StringComparer.Ordinal);
string file = $"Captures/bancs/values_{System.IO.Path.GetFileNameWithoutExtension(scene.path).Replace(' ', '_')}_{tag}.txt";
System.IO.File.WriteAllLines(file, lines);
return file + " : " + lines.Count + " valeurs";
