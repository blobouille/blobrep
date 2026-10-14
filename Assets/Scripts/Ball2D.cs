using UnityEngine;


public class Ball2D : MonoBehaviour
{
    [Header("Balle")]
    [Min(0.01f)] public float rayon = 0.5f;
    public Unity.U2D.Physics.PhysicsBody.BodyType typeCorps = Unity.U2D.Physics.PhysicsBody.BodyType.Kinematic;

    [Header("Mouvement")]
    public Vector2 vitesseLineaire = Vector2.zero;
    public float vitesseAngulaire = 0f; // rad/s

    [Header("Collisions")]
    [Tooltip("Mode 'bullet' de Box2D v3 (fastCollisionsAllowed). Décoché par défaut. " +
             "Attention : même décoché, la balle reste en collision continue contre les formes STATIQUES (la courbe).")]
    public bool collisionsRapides = false;

    [Header("Matériau")]
    [Min(0.0001f)] public float densite = 1f;
    [Range(0f, 2f)] public float friction = 0.6f;
    [Range(0f, 1f)] public float rebond = 0.3f;
    [Min(0f)] public float resistanceRoulement = 0f;

    private Unity.U2D.Physics.PhysicsBody _corps;

    private void Start()
    {
        Unity.U2D.Physics.PhysicsWorld monde = Unity.U2D.Physics.PhysicsWorld.defaultWorld;

        // TEST (à décommenter) : coupe la collision continue pour tout le monde.
        // Le nom exact peut varier selon ta version : tape "monde." et cherche "continuous" dans l'autocomplétion.
        // monde.continuousAllowed = false;

        Unity.U2D.Physics.PhysicsBodyDefinition defCorps = Unity.U2D.Physics.PhysicsBodyDefinition.defaultDefinition;
        defCorps.type = typeCorps;
        defCorps.position = (Vector2)transform.position;
        defCorps.linearVelocity = vitesseLineaire;
        defCorps.angularVelocity = vitesseAngulaire;
        defCorps.fastCollisionsAllowed = collisionsRapides;
        _corps = monde.CreateBody(defCorps);

        Unity.U2D.Physics.PhysicsShapeDefinition defShape = Unity.U2D.Physics.PhysicsShapeDefinition.defaultDefinition;
        defShape.density = densite;

        Unity.U2D.Physics.PhysicsShape.SurfaceMaterial materiau = defShape.surfaceMaterial;
        materiau.friction = friction;
        materiau.bounciness = rebond;
        materiau.rollingResistance = resistanceRoulement;
        defShape.surfaceMaterial = materiau;

        Unity.U2D.Physics.CircleGeometry geometrie = new Unity.U2D.Physics.CircleGeometry { center = Vector2.zero, radius = rayon };
        _corps.CreateShape(geometrie, defShape);
    }

    private void OnDestroy()
    {
        if (_corps.isValid)
            _corps.Destroy();
    }
}