using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrapplePoint))]
public class GrapplePointEditor : Editor
{
    [MenuItem("GameObject/2D Object/Grapple Point", false, 32)]
    static void Create() => GadgetEditorUtil.Create<GrapplePoint>("Grapple Point");

    void OnSceneGUI()
    {
        var point = (GrapplePoint)target;
        float r = GadgetEditorUtil.RadiusHandle(point.transform.position, point.radius, point.color);
        if (r == point.radius) return;
        Undo.RecordObject(point, "Grapple point radius");
        point.radius = Mathf.Max(0.02f, r);
    }
}
