using UnityEngine;

#if UNITY_6000_6_OR_NEWER
using Unity.U2D.Physics;
#else
using UnityEngine.LowLevelPhysics2D;
#endif

public class monde_settings : MonoBehaviour
{
    [Header("Réglage Vitesse")]
    [Range(0f, 10f)]
    public float gameSpeed = 1f;

    [Header("Réglage Gravité (tout le monde)")]
    public Vector2 gravite = new Vector2(0f, -9.81f);

    [Header("Précision de la simulation")]
    [Tooltip("Sous-pas par pas de simulation. Plus haut = contacts et joints plus précis, plus coûteux.")]
    [Min(1)] public int simulationSubSteps = 5;

    [Header("Collisions")]
    [Tooltip("Collision continue dynamique vs statique.")]
    public bool continuousAllowed = true;

    void Start() => AppliquerReglagesMonde();

    void Update()
    {
        gameSpeed = Mathf.Clamp(gameSpeed, 0f, 10f);
        Time.timeScale = gameSpeed;
        // NE PAS toucher à fixedDeltaTime : on garde le pas physique constant,
        // Unity rejouera juste FixedUpdate plus (ou moins) souvent par seconde réelle.

        AppliquerGravite();
    }

    // Appliqué aussi quand tu modifies les valeurs dans l'Inspector pendant le Play.
    void OnValidate()
    {
        if (Application.isPlaying) AppliquerReglagesMonde();
    }

    void AppliquerGravite()
    {
        // Monde low-level par défaut
        PhysicsWorld monde = PhysicsWorld.defaultWorld;
        if (monde.gravity != gravite)
            monde.gravity = gravite;

        // Physique 2D classique (au cas où)
        if (Physics2D.gravity != gravite)
            Physics2D.gravity = gravite;
    }

    void AppliquerReglagesMonde()
    {
        var world = PhysicsWorld.defaultWorld;
        world.simulationSubSteps = simulationSubSteps;
        world.continuousAllowed = continuousAllowed;
    }

    // Si ton PhysicsManager2 utilise un monde créé à la main (pas le defaultWorld),
    // appelle ça avec ton monde : monde_settings.AppliquerGraviteSur(monde, gravite);
    public static void AppliquerGraviteSur(PhysicsWorld monde, Vector2 g)
    {
        monde.gravity = g;
    }
}