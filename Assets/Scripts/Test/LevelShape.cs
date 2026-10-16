using System.Collections.Generic;
using UnityEngine;
using Unity.U2D.Physics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LevelShape : MonoBehaviour, ILevelPhysics
{
    public enum Preset { Cercle, Rectangle, Triangle, Personnalisee }

    [Header("Preset")]
    [Tooltip("Forme des bordures. Personnalisee utilise 'customPoints'.")]
    public Preset preset = Preset.Rectangle;

    [Header("Dimensions")]
    [Tooltip("Cercle : rayon, en unités Unity (multiplié par l'échelle du Transform).")]
    [Min(0.01f)] public float radius = 2f;
    [Tooltip("Rectangle : largeur. Triangle : longueur d'un côté. En unités Unity (multiplié par l'échelle du Transform).")]
    [Min(0.01f)] public float length = 4f;
    [Tooltip("Rectangle : hauteur, en unités Unity (multiplié par l'échelle du Transform).")]
    [Min(0.01f)] public float height = 3f;
    [Tooltip("Épaisseur des murs (diamètre des capsules), en unités Unity (absolu).")]
    [Min(0.01f)] public float thickness = 0.2f;
    [Tooltip("Cercle : nombre de segments utilisés pour l'approximer.")]
    [Range(3, 64)] public int circleSegments = 24;
    [Tooltip("Personnalisee : points en espace local.")]
    public Vector2[] customPoints = { new Vector2(-2f, -1f), new Vector2(2f, -1f), new Vector2(0f, 2f) };
    [Tooltip("Personnalisee : coché = le dernier point est relié au premier.")]
    public bool closed = true;

    [Header("Physique")]
    [Tooltip("Friction des murs (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.3f;
    [Tooltip("Rebond des murs : 0 = aucun rebond, 1 = rebond parfait.")]
    [Range(0f, 1f)] public float bounciness = 0f;
    [Tooltip("Couleur des murs dans le rendu debug de la physique.")]
    public Color debugColor = Color.gray;

    [Header("Preview (Scene view)")]
    [Tooltip("Couleur de l'aperçu Scene view.")]
    public Color previewColor = Color.yellow;

    PhysicsBody _body;

    void OnEnable()
    {
        if (Application.isPlaying) Build();
    }

    void OnDisable()
    {
        Clear();
    }

    [ContextMenu("Rebuild Level Shape")]
    public void Rebuild()
    {
        if (!Application.isPlaying) return;
        Clear();
        Build();
    }

    void GetLocalPoints(List<Vector2> pts, out bool isClosed)
    {
        isClosed = true;

        switch (preset)
        {
            case Preset.Rectangle:
            {
                float hl = length * 0.5f;
                float hh = height * 0.5f;
                pts.Add(new Vector2(-hl, -hh));
                pts.Add(new Vector2(hl, -hh));
                pts.Add(new Vector2(hl, hh));
                pts.Add(new Vector2(-hl, hh));
                break;
            }
            case Preset.Triangle:
            {
                float h = length * Mathf.Sqrt(3f) * 0.5f;
                pts.Add(new Vector2(0f, 2f * h / 3f));
                pts.Add(new Vector2(-length * 0.5f, -h / 3f));
                pts.Add(new Vector2(length * 0.5f, -h / 3f));
                break;
            }
            case Preset.Cercle:
            {
                for (int i = 0; i < circleSegments; ++i)
                {
                    float a = (Mathf.PI * 2f) * i / circleSegments;
                    pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
                break;
            }
            case Preset.Personnalisee:
            {
                if (customPoints != null) pts.AddRange(customPoints);
                isClosed = closed;
                break;
            }
        }
    }

    List<(Vector2 a, Vector2 b)> GetWorldEdges()
    {
        var pts = new List<Vector2>();
        GetLocalPoints(pts, out bool isClosed);
        for (int i = 0; i < pts.Count; ++i) pts[i] = transform.TransformPoint(pts[i]);

        var edges = new List<(Vector2, Vector2)>();
        if (pts.Count < 2) return edges;

        for (int i = 0; i < pts.Count - 1; ++i)
            edges.Add((pts[i], pts[i + 1]));
        if (isClosed && pts.Count > 2)
            edges.Add((pts[pts.Count - 1], pts[0]));

        return edges;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.color = previewColor;
        float half = thickness * 0.5f;

        foreach (var (a, b) in GetWorldEdges())
        {
            Vector2 n = Vector2.Perpendicular((b - a).normalized) * half;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(a + n, b + n);
            Gizmos.DrawLine(a - n, b - n);
            Gizmos.DrawWireSphere(a, half);
        }
    }

    void Build()
    {
        var edges = GetWorldEdges();
        if (edges.Count == 0) return;

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        material.customColor = debugColor;
        shapeDef.surfaceMaterial = material;

        float half = thickness * 0.5f;
        foreach (var (a, b) in edges)
            _body.CreateShape(new CapsuleGeometry { center1 = a, center2 = b, radius = half }, shapeDef);
    }

    void Clear()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
    }
}
