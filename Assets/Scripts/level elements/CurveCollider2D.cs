using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CurvePath2D))]
public class CurveCollider2D : MonoBehaviour, ILevelPhysics
{
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
        Build(path.BuildCollisionChains());
    }

    void OnDisable() => Cleanup();

    void Build(List<CollisionChain> chains)
    {
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var chainDef = PhysicsChainDefinition.defaultDefinition;
        var material = chainDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        chainDef.surfaceMaterial = material;

        foreach (var c in chains)
        {
            var points = new List<Vector2>(c.points.Length + 2);
            foreach (var p in c.points) points.Add(transform.TransformPoint(p));

            if (c.loop)
            {
                chainDef.isLoop = true;
            }
            else
            {
                // Chaîne ouverte Box2D : le premier et le dernier point sont des "ghosts" sans collision.
                int n = points.Count;
                points.Insert(0, points[0] + (points[0] - points[1]));
                points.Add(points[n] + (points[n] - points[n - 1]));
                chainDef.isLoop = false;
            }

            _body.CreateChain(points.ToArray(), chainDef);
        }
    }

    void Cleanup()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
    }
}
