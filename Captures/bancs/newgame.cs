var setup = UnityEngine.Object.FindAnyObjectByType<SlimeLevelSetup>();
var d = setup.GetComponent<BlobDropper>();
int before = d.seed;
typeof(SlimeLevelSetup).GetMethod("NewEndlessGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(setup, null);
return $"avant {before} après {d.seed} session {SlimeSession.EndlessSeed}";
