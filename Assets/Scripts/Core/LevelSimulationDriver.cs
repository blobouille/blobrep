using Unity.U2D.Physics;
using UnityEngine;

// Créé automatiquement en Play par LevelSimulation (ne pas l'ajouter à la main) : fait avancer la simulation à chaque pas fixe.
[DefaultExecutionOrder(-2000)]
[AddComponentMenu("")]
public class LevelSimulationDriver : MonoBehaviour
{
    void Awake()
    {
        var world = PhysicsWorld.defaultWorld;
        world.simulationType = PhysicsWorld.SimulationType.Script;
    }

    void FixedUpdate() => LevelSimulation.FixedStep();

    void OnDestroy()
    {
        var world = PhysicsWorld.defaultWorld;
        if (world.isValid) world.simulationType = PhysicsWorld.SimulationType.FixedUpdate;
    }
}
