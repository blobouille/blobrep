using UnityEngine;
using UnityEngine.Serialization;
using Unity.U2D.Physics;

// Seul endroit qui règle le monde physique (gravité, sous-pas, threads, collision continue, dessin debug) et la vitesse du jeu.
[DisallowMultipleComponent]
public class WorldSettings : MonoBehaviour
{
    [Header("Réglage Vitesse")]
    [Tooltip("Vitesse du jeu (Time.timeScale). Ne change PAS le pas physique : seulement le nombre de pas par seconde réelle, la simulation reste identique.")]
    [Range(0f, 10f)] public float gameSpeed = 1f;

    [Header("Réglage Gravité (tout le monde)")]
    [Tooltip("Gravité du monde physique, en m/s².")]
    [FormerlySerializedAs("gravite")] public Vector2 gravity = new Vector2(0f, -9.81f);

    [Header("Précision de la simulation")]
    [Tooltip("Sous-pas par pas de simulation. Plus haut = contacts et joints plus précis, plus coûteux.")]
    [Min(1)] public int simulationSubSteps = 5;
    [Tooltip("Threads utilisés par Box2D pour chaque pas. 1 = le plus rapide pour un petit monde (mesuré : 33 % plus rapide\n" +
             "qu'avec 12) ; plus de threads ne sert qu'avec des centaines de corps actifs.\n" +
             "Le résultat est identique au bit près quel que soit ce nombre.")]
    [Range(1, 16)] public int simulationWorkers = 1;

    [Header("Collisions")]
    [Tooltip("Collision continue des corps dynamiques contre les corps statiques (évite de traverser les murs à grande vitesse).")]
    public bool continuousAllowed = true;

    [Header("Rendu debug")]
    [Tooltip("Éléments du monde physique dessinés en Play (rendu debug).")]
    public PhysicsWorld.DrawOptions drawOptions = PhysicsWorld.DrawOptions.AllShapes;

    void OnEnable() => Apply();

    void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    void Apply()
    {
        Time.timeScale = gameSpeed;

        var world = PhysicsWorld.defaultWorld;
        world.gravity = gravity;
        world.simulationSubSteps = simulationSubSteps;
        world.simulationWorkers = simulationWorkers;
        world.continuousAllowed = continuousAllowed;
        world.drawOptions = drawOptions;
    }
}
