var sb = new System.Text.StringBuilder();
foreach (var name in new[] { "Slime/Map", "Slime/MapSprite", "Slime/MapCard" })
{
    var s = Shader.Find(name);
    if (s == null) { sb.AppendLine(name + " introuvable"); continue; }
    var msgs = UnityEditor.ShaderUtil.GetShaderMessages(s);
    sb.AppendLine(name + " : " + msgs.Length + " messages");
    foreach (var m in msgs) if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) sb.AppendLine("  ERREUR l." + m.line + " " + m.message);
}
return sb.ToString();
