// Recopie les valeurs par défaut du script SlimeMap dans le composant de la scène (couleurs, caméra, décor).
var m = UnityEngine.Object.FindAnyObjectByType<SlimeMap>();
var tmp = new GameObject("tmp") { hideFlags = HideFlags.HideAndDontSave };
tmp.SetActive(false);
var d = tmp.AddComponent<SlimeMap>();
m.grass = d.grass; m.grassDark = d.grassDark; m.grassLight = d.grassLight;
m.stone = d.stone; m.mortar = d.mortar; m.pathEdge = d.pathEdge;
m.skyTop = d.skyTop; m.skyHorizon = d.skyHorizon; m.mist = d.mist;
m.nodeLocked = d.nodeLocked; m.nodeOpen = d.nodeOpen; m.nodeDone = d.nodeDone;
m.fogRange = d.fogRange; m.pitch = d.pitch; m.fieldOfView = d.fieldOfView; m.distance = d.distance;
m.curvature = d.curvature; m.curveStart = d.curveStart; m.hillDistance = d.hillDistance; m.inkWidth = d.inkWidth;
m.treeDensity = d.treeDensity; m.flowerDensity = d.flowerDensity; m.grassDensity = d.grassDensity;
UnityEngine.Object.DestroyImmediate(tmp);
m.previewLevel = int.Parse(System.IO.File.ReadAllText("Captures/bancs/level.txt"));
m.enabled = false; m.enabled = true;
UnityEditor.EditorUtility.SetDirty(m);
return "ok";
