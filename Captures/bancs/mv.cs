UnityEditor.AssetDatabase.MoveAsset("Assets/Captures/carte_1.png", "Assets/Temp/carte_1.png");
UnityEditor.AssetDatabase.DeleteAsset("Assets/Captures");
return System.IO.File.Exists("Assets/Temp/carte_1.png");
