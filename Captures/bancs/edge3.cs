var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var b = d.Count > 0 ? d.InPlay[d.Count - 1] : null;
if (b == null) return "aucun";
float minX = float.MaxValue, maxX = float.MinValue, w = 0f, vxs = 0f, vys = 0f;
for (int i = 0; i < b.CapsuleCount; i++) { var c = b.Capsule(i); minX = Mathf.Min(minX, c.position.x - b.CapsuleRadius); maxX = Mathf.Max(maxX, c.position.x + b.CapsuleRadius); w += c.angularVelocity; vxs += c.linearVelocity.x; vys += c.linearVelocity.y; }
var hold = UnityEngine.Object.FindAnyObjectByType<GumoriWallHold>(); return "holds " + hold.Holds + " gap " + d.wallGap + " step " + LevelSimulation.Step + " bords " + minX.ToString("F2") + " " + maxX.ToString("F2") + " rot moy " + (w / b.CapsuleCount).ToString("F0") + " deg/s v " + (vxs / b.CapsuleCount).ToString("F2") + "," + (vys / b.CapsuleCount).ToString("F2");
