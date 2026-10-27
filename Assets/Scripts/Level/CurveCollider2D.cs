using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CurvePath2D))]
public class CurveCollider2D : MonoBehaviour, ILevelPhysics, IPathRider
{
    [Header("Matériau")]
    [Tooltip("Friction de la surface (0 = glisse parfaitement).")]
    [Min(0)] public float friction = 0.4f;
    [Tooltip("Rebond de la surface : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0)] public float bounciness = 0f;

    PhysicsBody _body;
    const float MinGap = 0.01f;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        var path = GetComponent<CurvePath2D>();
        path.Rebuild();
        Build(path.BuildCollisionChains());
    }

    void OnDisable() => Cleanup();

    public PhysicsBody Body => _body;

    // Enfant d'une trajectoire (MovingPath) : corps kinematic posé sur le Transform et piloté par elle ; sinon statique à
    // l'origine du monde, points en coordonnées monde (inchangé).
    bool OnPath => transform.parent != null && transform.parent.TryGetComponent(out MovingPath path) && path.enabled;

    public void PathTick(PathPose now, PathPose next, bool rotate, bool teleport)
    {
        if (!_body.isValid) return;
        if (teleport)
            _body.transform = new PhysicsTransform(now.position, rotate ? PhysicsRotate.FromDegrees(now.angle) : _body.rotation);
        float dt = Time.fixedDeltaTime;
        _body.linearVelocity = (next.position - _body.position) / dt;
        if (rotate) _body.angularVelocity = Mathf.DeltaAngle(_body.rotation.degrees, next.angle) / dt;
    }

    void Build(List<CollisionChain> chains)
    {
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bool onPath = OnPath;
        bodyDef.type = onPath ? PhysicsBody.BodyType.Kinematic : PhysicsBody.BodyType.Static;
        Matrix4x4 toBody = Matrix4x4.identity;
        if (onPath)
        {
            float angle = transform.eulerAngles.z;
            bodyDef.position = (Vector2)transform.position;
            bodyDef.rotation = PhysicsRotate.FromDegrees(angle);
            toBody = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, angle), Vector3.one).inverse;
        }
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        if (onPath) _body.sleepingAllowed = false;

        var chainDef = PhysicsChainDefinition.defaultDefinition;
        var material = chainDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        chainDef.surfaceMaterial = material;

        foreach (var c in chains)
        {
            var points = new List<Vector2>(c.points.Length + 2);
            // Box2D refuse une chaîne dont deux points se touchent presque (< linearSlop) : points trop proches retirés.
            foreach (var p in c.points)
            {
                Vector2 w = onPath ? toBody.MultiplyPoint3x4(transform.TransformPoint(p)) : transform.TransformPoint(p);
                if (points.Count == 0 || (w - points[points.Count - 1]).sqrMagnitude >= MinGap * MinGap) points.Add(w);
            }
            if (c.loop && points.Count > 3 && (points[0] - points[points.Count - 1]).sqrMagnitude < MinGap * MinGap) points.RemoveAt(points.Count - 1);

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
