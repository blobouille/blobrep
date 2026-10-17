using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Obstacle à piques : un pique est le même objet partout (rangée, scie…), seul son placement change.
// Chaque pique a sa taille individuelle ; les capsules du blob cassé qui touchent un pique s'y empalent.
public abstract class SpikedHazard : BlobHazard
{
    [Header("Piques")]
    [Tooltip("Taille individuelle de chaque pique (multiplie sa largeur et sa hauteur), dans l'ordre des piques.\n" +
             "La liste suit automatiquement le nombre de piques ; les nouveaux piques valent 1.")]
    public float[] sizes = { 1f, 1f, 1f, 1f, 1f };

    [Header("Empalement")]
    [Tooltip("Les capsules du blob cassé qui touchent un pique s'y accrochent et s'enfoncent dedans (elles suivent l'obstacle s'il bouge).")]
    public bool impale = true;
    [Tooltip("Profondeur d'enfoncement du point de contact dans le pique, en unités Unity (absolu).")]
    [Min(0f)] public float impaleDepth = 0.12f;
    [Tooltip("Raideur de l'accroche, en Hz. Plus haut = la capsule s'enfonce plus vite et bouge moins une fois plantée.")]
    [Min(0.1f)] public float impaleFrequency = 6f;
    [Tooltip("Amortissement de l'accroche (ratio, 1 = critique). Plus bas = la capsule oscille avant de se figer.")]
    [Min(0f)] public float impaleDamping = 0.7f;

    public abstract int SpikeCount { get; }

    // Pique i en local, taille individuelle comprise : centre de la base, direction de la pointe (unitaire), largeur, hauteur.
    public abstract void SpikeFrame(int i, out Vector2 baseCenter, out Vector2 up, out float width, out float height);

    // Décalage latéral de la pointe, en fraction de la demi-largeur (0 = pique symétrique).
    protected virtual float SpikeLean => 0f;

    // Formes qui ne sont pas des piques (ex. disque de la scie) : pas d'empalement dessus.
    protected virtual void BuildBodyGeometry(List<Vector2[]> polygons, List<Vector3> circles) { }

    void OnValidate()
    {
        SyncSizes();
    }

    public void SyncSizes()
    {
        int n = SpikeCount;
        if (sizes != null && sizes.Length == n) return;
        var old = sizes ?? new float[0];
        sizes = new float[n];
        for (int i = 0; i < n; i++) sizes[i] = i < old.Length ? Mathf.Max(0.01f, old[i]) : 1f;
    }

    public float SizeOf(int i) => sizes != null && i < sizes.Length ? Mathf.Max(0.01f, sizes[i]) : 1f;

    public Vector2 TipOf(int i)
    {
        SpikeFrame(i, out var c, out var up, out float w, out float h);
        return c + up * h + Side(up) * (SpikeLean * 0.5f * w);
    }

    static Vector2 Side(Vector2 up) => new Vector2(up.y, -up.x);

    public sealed override void BuildGeometry(List<Vector2[]> polygons, List<Vector3> circles)
    {
        for (int i = 0; i < SpikeCount; i++)
        {
            SpikeFrame(i, out var c, out var up, out float w, out _);
            var side = Side(up) * (0.5f * w);
            polygons.Add(new[] { c - side, c + side, TipOf(i) });
        }
        BuildBodyGeometry(polygons, circles);
    }

    // Les piques sont les premiers polygones : index de forme 0 .. SpikeCount-1.
    public override bool OnCapsuleTouched(PhysicsBody capsule, PhysicsShape hazardShape, Vector2 point)
    {
        int spike = hazardShape.userData.intValue - 1;
        if (!impale || spike < 0 || spike >= SpikeCount) return false;

        var body = Body;
        Vector2 dir = point - capsule.position;
        if (dir.sqrMagnitude > 1e-8f) dir.Normalize();
        else
        {
            SpikeFrame(spike, out _, out var up, out _, out _);
            dir = -body.rotation.RotateVector(up);
        }

        var jd = PhysicsFixedJointDefinition.defaultDefinition;
        jd.bodyA = body;
        jd.bodyB = capsule;
        jd.localAnchorA = new PhysicsTransform(body.GetLocalPoint(point + dir * impaleDepth),
                                               body.rotation.InverseMultiplyRotation(capsule.rotation));
        jd.localAnchorB = new PhysicsTransform(capsule.GetLocalPoint(point), PhysicsRotate.identity);
        jd.linearFrequency = impaleFrequency;
        jd.linearDamping = impaleDamping;
        jd.angularFrequency = impaleFrequency;
        jd.angularDamping = impaleDamping;
        jd.collideConnected = false;
        PhysicsWorld.defaultWorld.CreateJoint(jd);
        return true;
    }
}
