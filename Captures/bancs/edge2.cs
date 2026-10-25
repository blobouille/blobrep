var d = UnityEngine.Object.FindAnyObjectByType<BlobDropper>();
var b = d.Count > 0 ? d.Blobs[d.Count - 1] : null;
if (b == null) return "aucun";
float minX = float.MaxValue, minY = float.MaxValue, vx = 0f;
for (int i = 0; i < b.CapsuleCount; i++) { var c = b.Capsule(i); minX = Mathf.Min(minX, c.position.x - b.CapsuleRadius); minY = Mathf.Min(minY, c.position.y - b.CapsuleRadius); vx += c.linearVelocity.x; }
return "step " + LevelSimulation.Step + " bord gauche " + minX.ToString("F2") + " bas " + minY.ToString("F2") + " vx " + (vx / b.CapsuleCount).ToString("F2") + " diam " + b.diameter;
