var sb = new System.Text.StringBuilder();
sb.AppendLine($"screen {Screen.width}x{Screen.height} play={Application.isPlaying} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
var map = UnityEngine.Object.FindAnyObjectByType<SlimeMap>();
var av = UnityEngine.Object.FindAnyObjectByType<SlimeMapAvatar>();
var menu = UnityEngine.Object.FindAnyObjectByType<SlimeMapMenu>();
if (map != null) sb.AppendLine($"map levels={map.LevelCount} focus={map.FocusZ:F2} unlocked={map.ShownUnlocked} cam={map.cam.transform.position}");
if (av != null) sb.AppendLine($"avatar level={av.Level} moving={av.Moving} s={av.PathS:F2}");
if (menu != null)
{
    var f = typeof(SlimeMapMenu).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    sb.AppendLine("menu state=" + f.GetValue(menu));
}
sb.AppendLine($"session mode={SlimeSession.CurrentMode} level={SlimeSession.LevelIndex} unlocked={SlimeSession.Unlocked} title={SlimeSession.TitleDone} interval={UnityEngine.Rendering.OnDemandRendering.renderFrameInterval}");
var goal = UnityEngine.Object.FindAnyObjectByType<SlimeGoal>();
if (goal != null)
{
    var d = goal.GetComponent<BlobDropper>();
    sb.AppendLine($"goal active={goal.Active} target={goal.Target} maxDrops={goal.maxDrops} diff={goal.difficulty} seed={d.seed} drops={d.Drops} result={goal.Result} score={goal.GetComponent<SlimeScore>().Score}");
}
return sb.ToString();
