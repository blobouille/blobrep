using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CurvePath2D))]
public class CurveCollider2D : MonoBehaviour
{
    public enum Backend { LowLevel, LowLevelQuads }

    [Header("Collision")]
    [Tooltip("LowLevelQuads : un quad plein par segment, remplit toute l'épaisseur (recommandé).\n" +
             "LowLevel : chaînes Box2D à sens unique, selon 'collisionMode' de CurvePath2D.")]
    public Backend backend = Backend.LowLevelQuads;
    [Tooltip("Quads dont l'aire est inférieure à cette valeur (unités² monde) sont ignorés : Box2D refuse les polygones dégénérés.")]
    [Min(0)] public float minQuadArea = 1e-5f;

    [Header("Matériau")]
    [Tooltip("Friction de la surface (0 = glisse parfaitement).")]
    [Min(0)] public float friction = 0.4f;
    [Tooltip("Rebond de la surface : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0)] public float bounciness = 0f;

    PhysicsBody _body;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        var path = GetComponent<CurvePath2D>();
        path.Rebuild();

        switch (backend)
        {
            case Backend.LowLevel:      BuildLowLevel(path.BuildCollisionChains()); break;
            case Backend.LowLevelQuads: BuildLowLevelQuads(path); break;
        }
    }

    void OnDisable() => Cleanup();

    PhysicsShape.SurfaceMaterial Material(PhysicsShape.SurfaceMaterial m)
    {
        m.friction = friction;
        m.bounciness = bounciness;
        return m;
    }

    PhysicsBody CreateStaticBody()
    {
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        return PhysicsWorld.defaultWorld.CreateBody(bodyDef);
    }

    void BuildLowLevelQuads(CurvePath2D path)
    {
        _body = CreateStaticBody();

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.surfaceMaterial = Material(shapeDef.surfaceMaterial);

        int n = path.center.Count;
        if (n < 2) return;
        int segs = path.closed ? n : n - 1;
        var quad = new Vector2[4];

        for (int i = 0; i < segs; i++)
        {
            int j = (i + 1) % n;
            quad[0] = transform.TransformPoint(path.left[i]);
            quad[1] = transform.TransformPoint(path.left[j]);
            quad[2] = transform.TransformPoint(path.right[j]);
            quad[3] = transform.TransformPoint(path.right[i]);

            if (Mathf.Abs(Area(quad)) < minQuadArea) continue;

            // Create calcule l'enveloppe convexe : un quad croisé (virage très serré) reste valide.
            _body.CreateShape(PolygonGeometry.Create(quad), shapeDef);
        }
    }

    static float Area(Vector2[] q)
    {
        float a = 0f;
        for (int i = 0; i < q.Length; i++)
        {
            var p = q[i]; var r = q[(i + 1) % q.Length];
            a += p.x * r.y - r.x * p.y;
        }
        return a * 0.5f;
    }

    void BuildLowLevel(List<CollisionChain> chains)
    {
        _body = CreateStaticBody();

        var chainDef = PhysicsChainDefinition.defaultDefinition;
        chainDef.surfaceMaterial = Material(chainDef.surfaceMaterial);

        foreach (var c in chains)
        {
            var world2D = new List<Vector2>(c.points.Length + 2);
            foreach (var p in c.points) world2D.Add(transform.TransformPoint(p));

            if (c.loop)
            {
                chainDef.isLoop = true;
            }
            else
            {
                // Chaîne ouverte Box2D : le premier et le dernier point sont des "ghosts" sans collision.
                int n = world2D.Count;
                world2D.Insert(0, world2D[0] + (world2D[0] - world2D[1]));
                world2D.Add(world2D[n] + (world2D[n] - world2D[n - 1]));
                chainDef.isLoop = false;
            }

            _body.CreateChain(world2D.ToArray(), chainDef);
        }
    }

    void Cleanup()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
    }
}
