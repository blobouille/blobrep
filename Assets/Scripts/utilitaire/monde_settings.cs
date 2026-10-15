using UnityEngine;
using Unity.U2D.Physics;

[DisallowMultipleComponent]
public class monde_settings : MonoBehaviour
{
    [Header("Réglage Vitesse")]
    [Tooltip("Vitesse du jeu (Time.timeScale). Ne change PAS le pas physique : seulement le nombre de pas par seconde réelle, la simulation reste identique.")]
    [Range(0f, 10f)] public float gameSpeed = 1f;

    [Header("Réglage Gravité (tout le monde)")]
    [Tooltip("Gravité du monde physique, en unités Unity par seconde².")]
    public Vector2 gravite = new Vector2(0f, -9.81f);

    [Header("Précision de la simulation")]
    [Tooltip("Sous-pas par pas de simulation. Plus haut = contacts et joints plus précis, plus coûteux.")]
    [Min(1)] public int simulationSubSteps = 5;

    [Header("Collisions")]
    [Tooltip("Collision continue des corps dynamiques contre les corps statiques (évite de traverser les murs à grande vitesse).")]
    public bool continuousAllowed = true;

    [Header("Rendu debug")]
    [Tooltip("Éléments du monde physique dessinés en Play (rendu debug, seul rendu du jeu pour l'instant).")]
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
        world.gravity = gravite;
        world.simulationSubSteps = simulationSubSteps;
        world.continuousAllowed = continuousAllowed;
        world.drawOptions = drawOptions;
    }
}
