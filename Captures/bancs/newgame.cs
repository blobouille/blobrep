var setup = UnityEngine.Object.FindAnyObjectByType<GumoriLevelSetup>();
var d = setup.GetComponent<GumoriDropper>();
int before = d.seed;
typeof(GumoriLevelSetup).GetMethod("NewEndlessGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(setup, null);
return $"avant {before} après {d.seed} session {GumoriSession.EndlessSeed}";
