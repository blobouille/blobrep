// Coût de la carte : temps des scripts (Update / LateUpdate / FixedUpdate), du rendu, et statistiques de dessin.
string[] names = { "BehaviourUpdate", "LateBehaviourUpdate", "FixedBehaviourUpdate", "Inl_UniversalRenderTotal", "UnityEngine.CoreModule.dll!UnityEngine.Rendering::RenderPipelineManager.DoRenderLoop_Internal()", "RenderPipelineManager.DoRenderLoop_Internal" };
var sb = new System.Text.StringBuilder();
foreach (var n in names)
{
    var r = UnityEngine.Profiling.Recorder.Get(n);
    if (!r.isValid) { sb.AppendLine(n + " : invalide"); continue; }
    if (!r.enabled) { r.enabled = true; sb.AppendLine(n + " : activé"); continue; }
    sb.AppendLine($"{n} : {r.elapsedNanoseconds / 1e6:F3} ms ({r.sampleBlockCount} blocs)");
}
sb.AppendLine($"dessins {UnityEditor.UnityStats.drawCalls} triangles {UnityEditor.UnityStats.triangles} sommets {UnityEditor.UnityStats.vertices}");
sb.AppendLine($"intervalle {UnityEngine.Rendering.OnDemandRendering.renderFrameInterval}");
return sb.ToString();
