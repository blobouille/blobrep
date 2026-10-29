var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var c = UnityEngine.Object.FindAnyObjectByType<CurvePath2D>();
UnityEditor.Undo.RecordObject(d, "Réservoir du lâcher");
d.reservoir = c;
UnityEditor.EditorUtility.SetDirty(d);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
var m = typeof(GumoriDropper).GetMethod("MeasureWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
return "reservoir " + c.name + " paroi intérieure " + m.Invoke(d, null) + " gap " + d.wallGap;
