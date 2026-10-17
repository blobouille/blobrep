using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Zone de don (ou de retrait) d'un gadget : cercle sans corps physique, le blob la traverse. Dès qu'une capsule touche
// le cercle, le blob reçoit le gadget (ou le perd). Test géométrique dans LevelTick, avant chaque pas.
[ExecuteAlways]
[DisallowMultipleComponent]
public class GadgetZone : MonoBehaviour, ILevelPhysics, ILevelTick
{
    public enum ZoneMode { Give, Remove }

    [Header("Zone")]
    [Tooltip("Rayon du cercle, en unités Unity (absolu), centré sur l'objet. Aucune collision : le blob le traverse.\n" +
             "Le blob est concerné dès que le bord d'une de ses capsules entre dans le cercle.")]
    [Min(0.05f)] public float radius = 0.75f;

    [Header("Effet")]
    [Tooltip("Give : le blob reçoit le gadget (s'il a déjà le nombre max de gadgets, le plus ancien est retiré).\n" +
             "Remove : le blob perd le gadget.")]
    public ZoneMode mode = ZoneMode.Give;
    [Tooltip("Gadget donné ou retiré. Le blob doit porter le composant de ce gadget (ex. GrappleGadget), sinon rien ne se passe.")]
    public GadgetType gadget = GadgetType.Grapple;
    [Tooltip("Mode Remove : coché = retire tous les gadgets du blob, décoché = seulement 'gadget'.")]
    public bool removeAll = true;

    [Header("Debug")]
    [Tooltip("Couleur du cercle d'une zone de don.")]
    public Color giveColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Couleur du cercle d'une zone de retrait.")]
    public Color removeColor = new Color(0.65f, 0.65f, 0.75f, 1f);

    Vector2 _center;
    float _radius;
    ZoneMode _mode;
    GadgetType _gadget;
    bool _removeAll;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    Color CurrentColor => mode == ZoneMode.Give ? giveColor : removeColor;

    void OnEnable()
    {
        _center = transform.position;
        _radius = radius;
        _mode = mode;
        _gadget = gadget;
        _removeAll = removeAll;
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    public void LevelTick()
    {
        var holders = GadgetHolder.Active;
        for (int h = 0; h < holders.Count; h++)
        {
            var holder = holders[h];
            if (_mode == ZoneMode.Give ? holder.Has(_gadget) : (_removeAll ? holder.Equipped.Count == 0 : !holder.Has(_gadget)))
                continue;
            var blob = holder.Blob;
            if (blob == null || blob.CapsuleCount == 0) continue;
            if (!Touches(blob)) continue;

            if (_mode == ZoneMode.Give) holder.Give(_gadget);
            else if (_removeAll) holder.RemoveAll();
            else holder.Remove(_gadget);
        }
    }

    bool Touches(BLOB blob)
    {
        var shape = blob.CapsuleShape;
        float reach = _radius + shape.radius + 0.5f * Vector2.Distance(shape.center1, shape.center2);
        // Blob intact : on écarte d'abord par la première capsule (le rejeu accéléré enchaîne des milliers de pas).
        if (!blob.IsBroken)
        {
            float far = _radius + 2f * blob.diameter;
            if ((blob.Capsule(0).position - _center).sqrMagnitude > far * far) return false;
        }
        float reach2 = reach * reach;
        for (int i = 0; i < blob.CapsuleCount; i++)
            if ((blob.Capsule(i).position - _center).sqrMagnitude <= reach2) return true;
        return false;
    }

    // Contour + symbole (+ pour un don, × pour un retrait), en local. Même dessin dans la Scene view et en jeu.
    void BuildOutline(List<Vector2> lines)
    {
        lines.Clear();
        const int N = 40;
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
            lines.Add(new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius);
            lines.Add(new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius);
        }
        float s = 0.3f * radius;
        if (mode == ZoneMode.Give)
        {
            lines.Add(new Vector2(-s, 0f)); lines.Add(new Vector2(s, 0f));
            lines.Add(new Vector2(0f, -s)); lines.Add(new Vector2(0f, s));
        }
        else
        {
            lines.Add(new Vector2(-s, -s)); lines.Add(new Vector2(s, s));
            lines.Add(new Vector2(-s, s)); lines.Add(new Vector2(s, -s));
        }
    }

    readonly List<Vector2> _lines = new List<Vector2>();

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        BuildOutline(_lines);
        Vector2 c = transform.position;
        var world = PhysicsWorld.defaultWorld;
        var color = CurrentColor;
        for (int i = 0; i < _lines.Count; i += 2) world.DrawLine(c + _lines[i], c + _lines[i + 1], color, 0f);
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        BuildOutline(_lines);
        Vector3 c = transform.position;
        Gizmos.color = CurrentColor;
        for (int i = 0; i < _lines.Count; i += 2) Gizmos.DrawLine(c + (Vector3)_lines[i], c + (Vector3)_lines[i + 1]);

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddCircle(Vector2.zero, radius);
        _pickArea.Draw(Matrix4x4.Translate(c));
#endif
    }
}
