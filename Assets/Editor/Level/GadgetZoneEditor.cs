using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GadgetZone))]
public class GadgetZoneEditor : Editor
{
    [MenuItem("GameObject/2D Object/Gadget Zone", false, 31)]
    static void Create() => GadgetEditorUtil.Create<GadgetZone>("Gadget Zone");

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Scene View : poignée ronde à droite = rayon.\nLe blob doit porter le composant du gadget (ex. GrappleGadget).", MessageType.Info);
    }

    void OnSceneGUI()
    {
        var zone = (GadgetZone)target;
        float r = GadgetEditorUtil.RadiusHandle(zone.transform.position, zone.radius, zone.mode == GadgetZone.ZoneMode.Give ? zone.giveColor : zone.removeColor);
        if (r == zone.radius) return;
        Undo.RecordObject(zone, "Gadget zone radius");
        zone.radius = Mathf.Max(0.05f, r);
    }
}
