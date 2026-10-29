var menu = UnityEngine.Object.FindAnyObjectByType<GumoriMapMenu>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var stateType = typeof(GumoriMapMenu).GetNestedType("State", System.Reflection.BindingFlags.NonPublic);
typeof(GumoriMapMenu).GetMethod("SetState", flags).Invoke(menu, new object[] { System.Enum.Parse(stateType, "Map") });
var map = UnityEngine.Object.FindAnyObjectByType<GumoriMap>();
map.Scroll(float.Parse(System.IO.File.ReadAllText("Captures/bancs/scroll.txt").Trim(), System.Globalization.CultureInfo.InvariantCulture));
return "ok " + map.FocusZ;
