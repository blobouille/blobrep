var m = UnityEngine.Object.FindAnyObjectByType<GumoriMap>();
m.pitch = 42f; m.fieldOfView = 42f; m.distance = 15f; m.curvature = 0.03f; m.curveStart = 3f; m.fogRange = new Vector2(20f, 42f);
m.previewLevel = int.Parse(System.IO.File.ReadAllText("Captures/bancs/level.txt"));
m.enabled = false; m.enabled = true;
UnityEditor.EditorUtility.SetDirty(m);
UnityEditor.SceneView.RepaintAll();
return m.cam.transform.position.ToString() + " chunks ok";
