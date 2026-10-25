var d = UnityEngine.Object.FindAnyObjectByType<BlobDropper>();
var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var sb = new System.Text.StringBuilder();
sb.Append("paroi " + d.WallHalfWidth + " gap " + d.wallGap + " | ");
if (d.Count == 0 && Mathf.Abs((float)typeof(BlobDropper).GetField("_offset", f).GetValue(d)) > 0.5f)
{
    typeof(BlobDropper).GetMethod("Drop", f).Invoke(d, null);
    return sb + "lâché à " + d.DropPoint.x;
}
if (d.Count == 0)
{
    typeof(BlobDropper).GetField("_offset", f).SetValue(d, float.Parse(System.IO.File.ReadAllText("Captures/bancs/side.txt").Trim()));
    typeof(BlobDropper).GetField("_cooldown", f).SetValue(d, 0f);
    if (System.IO.File.Exists("Captures/bancs/lvl.txt")) typeof(BlobDropper).GetField("_current", f).SetValue(d, int.Parse(System.IO.File.ReadAllText("Captures/bancs/lvl.txt").Trim()));
    return sb + "visé à gauche, relancer pour lâcher";
}
if (d.Count == 1 && (float)typeof(BlobDropper).GetField("_offset", f).GetValue(d) > -50f && d.Blobs[0] == null) return "?";
float minX = float.MaxValue;
var b = d.Blobs[d.Count - 1];
for (int i = 0; i < b.CapsuleCount; i++) minX = Mathf.Min(minX, b.Capsule(i).position.x - b.CapsuleRadius);
return sb + "blobs " + d.Count + " offset " + typeof(BlobDropper).GetField("_offset", f).GetValue(d) + " bord gauche du slime " + minX + " (paroi à " + (-d.WallHalfWidth) + ")";
