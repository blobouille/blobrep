// Test : force la victoire du niveau en cours (7 coups restants) comme si SlimeGoal venait de la déclarer.
var goal = UnityEngine.Object.FindAnyObjectByType<SlimeGoal>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var t = typeof(SlimeGoal);
t.GetField("_leftAtWin", flags).SetValue(goal, 7);
t.GetField("_endTime", flags).SetValue(goal, Time.time - goal.celebrationTime - 0.2f);
t.GetProperty("Reached").SetValue(goal, true);
t.GetProperty("Result").SetValue(goal, SlimeGoal.State.Won);
var ev = (System.MulticastDelegate)t.GetField("Finished", flags).GetValue(goal);
ev?.DynamicInvoke(SlimeGoal.State.Won);
return $"unlocked={SlimeSession.Unlocked} stars0={SlimeSession.Stars(0)} last={SlimeSession.LastPlayed} won={SlimeSession.LastWon} newUnlock={SlimeSession.LastUnlocked}";
