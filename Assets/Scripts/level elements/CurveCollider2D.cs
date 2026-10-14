using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[RequireComponent(typeof(CurvePath2D))]
public class CurveCollider2D : MonoBehaviour
{
    public enum Backend { LowLevel, LowLevelQuads }

    public Backend backend = Backend.LowLevelQuads;
    [Min(0)] public float friction = 0.4f;
    [Min(0)] public float bounciness = 0f;

    [Tooltip("Quads dont l'aire est inférieure à cette valeur sont ignorés (Box2D refuse les polygones dégénérés).")]
    [Min(0)] public float minQuadArea = 1e-5f;

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

    // ------------------------------------------------------------------ Box2D v3 : quads jointifs
    void BuildLowLevelQuads(CurvePath2D path)
    {
        var world = PhysicsWorld.defaultWorld;

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        _body = world.CreateBody(bodyDef);

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.surfaceMaterial = new PhysicsShape.SurfaceMaterial
        {
            friction = friction,
            bounciness = bounciness
        };

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

            // PolygonGeometry.Create calcule l'enveloppe convexe : un quad "tordu"
            // (virage très serré où le miter se croise) reste donc valide.
            var geometry = PolygonGeometry.Create(quad);
            _body.CreateShape(geometry, shapeDef);
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

    // ------------------------------------------------------------------ Box2D v3 : chaînes
    void BuildLowLevel(List<CollisionChain> chains)
    {
        var world = PhysicsWorld.defaultWorld;

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        _body = world.CreateBody(bodyDef);

        var chainDef = PhysicsChainDefinition.defaultDefinition;
        chainDef.surfaceMaterial = new PhysicsShape.SurfaceMaterial
        {
            friction = friction,
            bounciness = bounciness
        };

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
                int n = world2D.Count;
                Vector2 ghost1 = world2D[0] + (world2D[0] - world2D[1]);
                Vector2 ghost2 = world2D[n - 1] + (world2D[n - 1] - world2D[n - 2]);
                world2D.Insert(0, ghost1);
                world2D.Add(ghost2);
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