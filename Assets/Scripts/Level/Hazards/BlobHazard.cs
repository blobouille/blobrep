using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[ExecuteAlways]
public abstract class BlobHazard : MonoBehaviour, ILevelPhysics, IPathRider
{
    [Header("Matériau")]
    [Tooltip("Friction de la surface de l'obstacle (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.4f;
    [Tooltip("Rebond de la surface de l'obstacle : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0f)] public float bounciness = 0f;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view.")]
    public Color previewColor = new Color(1f, 0.25f, 0.2f, 1f);

    // Catégorie ajoutée aux formes d'obstacle pour les requêtes (corde du grappin). Les masques de contact restant « tout »,
    // les contacts sont inchangés.
    public const int CategoryBit = 30;

    PhysicsBody _body;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    protected PhysicsBody Body => _body;
    protected virtual PhysicsBody.BodyType BodyType => PhysicsBody.BodyType.Static;
    protected virtual void OnBodyCreated() { }

    // Appelé par le BLOB (ordre fixe) pour chaque capsule libre qui touche l'obstacle, une fois le blob cassé.
    // Retourne vrai si la capsule est désormais attachée à l'obstacle : elle n'est plus signalée ensuite.
    public virtual bool OnCapsuleTouched(PhysicsBody capsule, PhysicsShape hazardShape, Vector2 point) => false;

    // Géométrie locale (repère du Transform, échelle ignorée) : polygones convexes et cercles (x, y, rayon).
    public abstract void BuildGeometry(List<Vector2[]> polygons, List<Vector3> circles);

    public virtual Vector2 FocusPoint(Vector2 contactPoint) => contactPoint;

    // Enfant d'une trajectoire (MovingPath) : corps kinematic piloté par elle.
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

    public Matrix4x4 LocalToWorld => Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one);

    void OnEnable()
    {
        if (Application.isPlaying) CreatePhysics();
    }

    void OnDisable()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void CreatePhysics()
    {
        var polygons = new List<Vector2[]>();
        var circles = new List<Vector3>();
        BuildGeometry(polygons, circles);

        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bool onPath = OnPath;
        bodyDef.type = onPath ? PhysicsBody.BodyType.Kinematic : BodyType;
        bodyDef.position = (Vector2)transform.position;
        bodyDef.rotation = PhysicsRotate.FromRadians(transform.eulerAngles.z * Mathf.Deg2Rad);
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        if (onPath) _body.sleepingAllowed = false;

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        material.customColor = previewColor;
        shapeDef.surfaceMaterial = material;
        shapeDef.contactEvents = true; // le BLOB ne relit que les capsules qui touchent un obstacle (sans effet sur la simulation)
        var filter = shapeDef.contactFilter;
        filter.categories = new PhysicsMask { bitMask = filter.categories.bitMask | (1UL << CategoryBit) };
        shapeDef.contactFilter = filter;

        // intValue : 1 + index du polygone dans BuildGeometry (0 pour les cercles).
        foreach (var c in circles)
            _body.CreateShape(new CircleGeometry { center = new Vector2(c.x, c.y), radius = c.z }, shapeDef).userData =
                new PhysicsUserData { objectValue = this, intValue = 0 };
        for (int i = 0; i < polygons.Count; i++)
            _body.CreateShape(PolygonGeometry.Create(polygons[i], 0f), shapeDef).userData =
                new PhysicsUserData { objectValue = this, intValue = i + 1 };

        OnBodyCreated();
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        var polygons = new List<Vector2[]>();
        var circles = new List<Vector3>();
        BuildGeometry(polygons, circles);

        Gizmos.matrix = LocalToWorld;
        Gizmos.color = previewColor;
        foreach (var c in circles) DrawCircle(new Vector2(c.x, c.y), c.z);
        foreach (var p in polygons)
            for (int i = 0; i < p.Length; i++)
                Gizmos.DrawLine(p[i], p[(i + 1) % p.Length]);
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        foreach (var c in circles) _pickArea.AddCircle(new Vector2(c.x, c.y), c.z);
        foreach (var p in polygons) _pickArea.AddConvex(p);
        _pickArea.Draw(LocalToWorld);
#endif
    }

    static void DrawCircle(Vector2 c, float r)
    {
        const int N = 48;
        Vector3 prev = c + new Vector2(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
