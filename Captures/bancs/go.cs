// Test de la carte en Play : quitte l'écran titre et envoie l'avatar au niveau lu dans go.txt.
var menu = UnityEngine.Object.FindAnyObjectByType<SlimeMapMenu>();
var av = UnityEngine.Object.FindAnyObjectByType<SlimeMapAvatar>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var stateType = typeof(SlimeMapMenu).GetNestedType("State", System.Reflection.BindingFlags.NonPublic);
SlimeSession.TitleDone = true;
typeof(SlimeMapMenu).GetMethod("SetState", flags).Invoke(menu, new object[] { System.Enum.Parse(stateType, "Map") });
int level = int.Parse(System.IO.File.ReadAllText("Captures/bancs/go.txt").Trim());
typeof(SlimeMapMenu).GetField("_pendingPanel", flags).SetValue(menu, level);
av.GoTo(level);
return $"avatar {av.Level} moving={av.Moving}";
