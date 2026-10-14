using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.U2D.Physics;

public class BLOB : MonoBehaviour
{
    [Header("Forme & Apparence")]
    [Range(3, 32)] public int sides = 16;
    public float initialScale = 0.15f;
    public Color debugColor = Color.cyan;

    [Header("Paramètres des Capsules")]
    [Tooltip("Gère l'épaisseur (le rayon) de chaque capsule")]
    public float capsuleRadius = 0.5f;

    [Tooltip("Définit l'espacement 'x' entre chaque capsule")]
    public float capsuleSpacing = 0f;

    [Header("Joints & Physique")]
    [Tooltip("Densité de chaque capsule : plus elle est haute, plus le blob est lourd")]
    [Min(0.001f)] public float density = 1f;
    public float jointFrequency = 64f;
    public float jointDamping = 1f;
    public float jointLinearFrequency = 64f;
    public float jointLinearDamping = 1f;
    public float friction = 0.03f;
    [Min(0)] public float bounciness = 0.5f;

    // Listes natives pour stocker les composants du blob
    private NativeList<PhysicsBody> m_Bodies;
    private NativeList<PhysicsShape> m_Shapes;

    private void Start()
    {
        var world = PhysicsWorld.defaultWorld;
        world.drawOptions = PhysicsWorld.DrawOptions.AllShapes;

        SpawnBlob(transform.position);
    }

    private void OnDestroy()
    {
        // Nettoyage obligatoire avec Unity.U2D.Physics (sinon fuites de mémoire)
        if (m_Bodies.IsCreated)
        {
            PhysicsBody.DestroyBatch(m_Bodies.AsArray());
            m_Bodies.Dispose();
        }
        if (m_Shapes.IsCreated)
        {
            m_Shapes.Dispose();
        }
    }

    [ContextMenu("Respawn Blob")]
    public void Respawn()
    {
        OnDestroy();
        SpawnBlob(transform.position);
    }

    private void SpawnBlob(Vector2 center)
    {
        var world = PhysicsWorld.defaultWorld;

        m_Bodies = new NativeList<PhysicsBody>(sides, Allocator.Persistent);
        m_Shapes = new NativeList<PhysicsShape>(sides, Allocator.Persistent);

        // --- 1. CALCULS GÉOMÉTRIQUES ---
        float blobRadius = 1.0f * initialScale;
        float deltaAngle = PhysicsMath.TAU / sides;

        // Longueur théorique de l'arc pour un segment (garde la taille du blob)
        float segmentLength = PhysicsMath.TAU * blobRadius / sides;

        // Longueur réelle de la capsule : on soustrait l'espacement demandé
        float actualCapsuleLength = math.max(0f, segmentLength - capsuleSpacing);

        var capsuleGeometry = new CapsuleGeometry
        {
            center1 = new Vector2(0f, -0.5f * actualCapsuleLength),
            center2 = new Vector2(0f, 0.5f * actualCapsuleLength),
            radius = capsuleRadius * initialScale
        };

        // --- 2. CONFIGURATION PHYSIQUE ---
        var bodyDef = new PhysicsBodyDefinition { type = PhysicsBody.BodyType.Dynamic };
        var shapeDef = new PhysicsShapeDefinition
        {
            density = density,
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
                groupIndex = -1 // Valeur négative = les parties de ce blob s'ignorent entre elles
            }
        };

        // --- 3. CRÉATION DES CORPS (Segments du cercle) ---
        float angle = 0f;
        for (int i = 0; i < sides; ++i)
        {
            bodyDef.position = new Vector2(blobRadius * math.cos(angle) + center.x, blobRadius * math.sin(angle) + center.y);
            bodyDef.rotation = PhysicsRotate.FromRadians(angle);

            var body = world.CreateBody(bodyDef);
            
            m_Bodies.Add(body);

            var shape = body.CreateShape(capsuleGeometry, shapeDef);
            m_Shapes.Add(shape);

            angle += deltaAngle;
        }

        // --- 4. CRÉATION DES ARTICULATIONS (Lier les segments) ---
        // Les ancres utilisent la segmentLength théorique : le lien invisible couvre l'espace vide.
        var fixedDefinition = new PhysicsFixedJointDefinition
        {
            angularFrequency = jointFrequency,
            angularDamping = jointDamping,
            linearFrequency = jointLinearFrequency,
            linearDamping = jointLinearDamping,
            localAnchorA = new Vector2(0f, 0.5f * segmentLength),
            localAnchorB = new Vector2(0f, -0.5f * segmentLength)
        };

        // Chaque segment est attaché au précédent (le premier au dernier pour fermer la boucle)
        var prevBody = m_Bodies[m_Bodies.Length - 1];
        for (int i = 0; i < sides; ++i)
        {
            var currentBody = m_Bodies[i];

            fixedDefinition.bodyA = prevBody;
            fixedDefinition.bodyB = currentBody;
            world.CreateJoint(fixedDefinition);

            prevBody = currentBody;
        }
    }
}