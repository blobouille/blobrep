var sb = new System.Text.StringBuilder();
var vol = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
foreach (var c in vol.sharedProfile.components)
{
    sb.Append(c.GetType().Name + " active=" + c.active + " : ");
    foreach (var p in c.parameters) if (p.overrideState) sb.Append(p.GetType().Name.Replace("Parameter","") + "=" + p.ToString() + "; ");
    sb.AppendLine();
}
var cam = Camera.main;
sb.AppendLine("cam " + cam.transform.position + " " + cam.transform.eulerAngles + " fov " + cam.fieldOfView + " hdr " + cam.allowHDR);
return sb.ToString();
