var name = System.IO.File.ReadAllText("Captures/bancs/shot.txt").Trim();
ScreenCapture.CaptureScreenshot("Assets/Temp/" + name + ".png");
UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
return "ok " + name;
