using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

public class LevelShape : MonoBehaviour
{
    public enum Preset { Cercle, Rectangle, Triangle, Personnalisee }

    [Header("Preset")]
    public Preset preset = Preset.Rectangle;

    [Header("Dimensions")]
    [Tooltip("Rayon du cercle")]
    [Min(0.01f)] public float radius = 2f;

    [Tooltip("Rectangle : largeur. Triangle : longueur d'un côté")]
    [Min(0.01f)] public float length = 4f;

    [Tooltip("Rectangle : hauteur")]
    [Min(0.01f)] public float height = 3f;

    [Tooltip("Épaisseur des murs (diamètre des capsules)")]
    [Min(0.01f)] public float thickness = 0.2f;

    [Tooltip("Nombre de segments pour approximer le cercle")]
    [Range(3, 64)] public int circleSegments = 24;

    [Tooltip("Points locaux utilisés par le preset Personnalisee")]
    public Vector2[] customPoints = { new Vector2(-2f, -1f), new Vector2(2f, -1f), new Vector2(0f, 2f) };

    [Tooltip("Personnalisee : coché = la forme se referme (dernier point relié au premier)")]
    public bool closed = true;

    [Header("Physique")]
    public float friction = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Rebond du matériau : 0 = aucun rebond, 1 = rebond parfait")]
    public float bounciness = 0f;

    public Color debugColor = Color.gray;

    [Header("Preview (Scene view)")]
    public Color previewColor = Color.yellow;

    private NativeList<PhysicsBody> m_Bodies;

    private void Start()
    {
        var world = PhysicsWorld.defaultWorld;
        world.drawOptions = PhysicsWorld.DrawOptions.AllShapes;

        Build();
    }

    private void OnDestroy()
    {
        Clear();
    }

    [ContextMenu("Rebuild Level Shape")]
    public void Rebuild()
    {
        Clear();
        Build();
    }

    // -------------------------------------------------------------
    // GÉOMÉTRIE (utilisée à la fois par le preview et par le runtime)
    // -------------------------------------------------------------

    /// <summary>Retourne les points locaux de la forme et indique si elle est fermée.</summary>
    private List<Vector2> GetLocalPoints(out bool isClosed)
    {
        var pts = new List<Vector2>();
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
                // Triangle équilatéral dont le côté vaut "length", centré sur son centre de gravité
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

        return pts;
    }

    /// <summary>Liste des segments (a, b) de la forme, en coordonnées locales.</summary>
    private List<(Vector2 a, Vector2 b)> GetEdges()
    {
        var pts = GetLocalPoints(out bool isClosed);
        var edges = new List<(Vector2, Vector2)>();

        if (pts.Count < 2) return edges;

        for (int i = 0; i < pts.Count - 1; ++i)
            edges.Add((pts[i], pts[i + 1]));

        if (isClosed && pts.Count > 2)
            edges.Add((pts[pts.Count - 1], pts[0]));

        return edges;
    }

    // -------------------------------------------------------------
    // PREVIEW (Scene view)
    // -------------------------------------------------------------

    private void OnDrawGizmos()
    {
        var edges = GetEdges();
        if (edges.Count == 0) return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = previewColor;

        float half = thickness * 0.5f;

        foreach (var (a, b) in edges)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * half;

            // Ligne centrale
            Gizmos.DrawLine(a, b);

            // Contours d'épaisseur de chaque côté
            Gizmos.DrawLine(a + n, b + n);
            Gizmos.DrawLine(a - n, b - n);

            // Cercles aux extrémités pour visualiser les capsules
            Gizmos.DrawWireSphere(a, half);
        }

        Gizmos.matrix = previous;
    }

    // -------------------------------------------------------------
    // RUNTIME (création des corps Box2D au Play)
    // -------------------------------------------------------------

    private void Build()
    {
        var world = PhysicsWorld.defaultWorld;
        var edges = GetEdges();

        m_Bodies = new NativeList<PhysicsBody>(1, Allocator.Persistent);

        if (edges.Count == 0) return;

        var bodyDef = new PhysicsBodyDefinition
        {
            type = PhysicsBody.BodyType.Static,
            position = (Vector2)transform.position,
            rotation = PhysicsRotate.FromRadians(transform.eulerAngles.z * Mathf.Deg2Rad)
        };

        var shapeDef = new PhysicsShapeDefinition
        {
            surfaceMaterial = new PhysicsShape.SurfaceMaterial
            {
                friction = friction,
                bounciness = bounciness,
                customColor = debugColor
            },
            contactFilter = new PhysicsShape.ContactFilter
            {
                categories = 1,
                contacts = PhysicsMask.All,
                groupIndex = 0
            }
        };

        // Un seul corps statique pour toute la forme
        var body = world.CreateBody(bodyDef);
        m_Bodies.Add(body);

        float half = thickness * 0.5f;

        foreach (var (a, b) in edges)
        {
            var capsule = new CapsuleGeometry
            {
                center1 = a,
                center2 = b,
                radius = half
            };

            body.CreateShape(capsule, shapeDef);
        }
    }

    private void Clear()
    {
        if (m_Bodies.IsCreated)
        {
            PhysicsBody.DestroyBatch(m_Bodies.AsArray());
            m_Bodies.Dispose();
        }
    }
}