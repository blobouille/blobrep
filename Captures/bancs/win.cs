// Test : force la victoire du niveau en cours (7 coups restants) comme si GumoriGoal venait de la déclarer.
var goal = UnityEngine.Object.FindAnyObjectByType<GumoriGoal>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var t = typeof(GumoriGoal);
t.GetField("_leftAtWin", flags).SetValue(goal, 7);
t.GetField("_endTime", flags).SetValue(goal, Time.time - goal.celebrationTime - 0.2f);
t.GetProperty("Reached").SetValue(goal, true);
t.GetProperty("Result").SetValue(goal, GumoriGoal.State.Won);
var ev = (System.MulticastDelegate)t.GetField("Finished", flags).GetValue(goal);
ev?.DynamicInvoke(GumoriGoal.State.Won);
return $"unlocked={GumoriSession.Unlocked} stars0={GumoriSession.Stars(0)} last={GumoriSession.LastPlayed} won={GumoriSession.LastWon} newUnlock={GumoriSession.LastUnlocked}";
