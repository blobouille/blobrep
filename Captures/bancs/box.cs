var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
var c = UnityEngine.Object.FindAnyObjectByType<CurvePath2D>();
var sb = new System.Text.StringBuilder();
sb.AppendLine("dropper " + d.transform.position + " half " + d.halfWidth + " gap " + d.wallGap);
sb.AppendLine("curve " + c.name + " pos " + c.transform.position + " scale " + c.transform.lossyScale + " thick " + c.thickness + " mode " + c.collisionMode + " flip " + c.flipSide);
foreach (var p in c.points) sb.AppendLine("  " + c.transform.TransformPoint(p.position));
c.Rebuild();
float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
foreach (var p in c.center) { var w = c.transform.TransformPoint(p); minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y); }
sb.AppendLine("center bounds x " + minX + ".." + maxX + " y " + minY + ".." + maxY);
return sb.ToString();
