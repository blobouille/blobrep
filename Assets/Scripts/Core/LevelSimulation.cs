using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Seul moteur de la simulation : à chaque pas fixe, entrées → LevelTick de chaque élément (ordre de la hiérarchie)
// → PhysicsWorld.Simulate. Le même pas sert au jeu normal et au rejeu accéléré (points de sauvegarde) :
// rejouer les entrées enregistrées depuis un redémarrage redonne exactement le même état.
public static class LevelSimulation
{
    // Nombre de pas simulés depuis le dernier redémarrage.
    public static int Step { get; private set; }
    public static bool FastForwarding { get; private set; }

    // Après un rejeu accéléré (retour à un point de sauvegarde) : caméra, etc. se recalent.
    public static event System.Action Jumped;

    static readonly List<System.Action> _pending = new List<System.Action>();
    static readonly List<ILevelTick> _ticks = new List<ILevelTick>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Step = 0;
        FastForwarding = false;
        Jumped = null;
        _pending.Clear();
        _ticks.Clear();
        LevelInput.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateDriver()
    {
        // Détruit en sortie de Play (pas de HideAndDontSave : il survivrait et s'ajouterait à chaque Play).
        if (Object.FindAnyObjectByType<LevelSimulationDriver>() != null) return;
        var go = new GameObject("LevelSimulation") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<LevelSimulationDriver>();
    }

    // Action exécutée au début du prochain pas, avant les entrées (redémarrage, retour à une sauvegarde…).
    public static void RunBeforeNextStep(System.Action action) => _pending.Add(action);

    internal static void OnRestarted(IReadOnlyList<MonoBehaviour> physics)
    {
        Step = 0;
        LevelInput.Clear();
        _ticks.Clear();
        foreach (var c in physics)
            if (c != null && c.isActiveAndEnabled && c is ILevelTick t) _ticks.Add(t);
    }

    internal static void FixedStep()
    {
        var world = PhysicsWorld.defaultWorld;
        world.simulationType = PhysicsWorld.SimulationType.Script;

        for (int i = 0; i < _pending.Count; i++) _pending[i]();
        _pending.Clear();

        StepWith(LevelInput.SampleKeyboard());
    }

    static void StepWith(uint input)
    {
        LevelInput.Apply(input);
        for (int i = 0; i < _ticks.Count; i++) _ticks[i].LevelTick();
        PhysicsWorld.defaultWorld.Simulate(Time.fixedDeltaTime);
        Step++;
    }

    // Rejoue 'count' pas avec les entrées enregistrées, le plus vite possible (à appeler juste après un redémarrage).
    public static void FastForward(IReadOnlyList<uint> inputs, int count)
    {
        FastForwarding = true;
        try
        {
            for (int i = 0; i < count; i++) StepWith(inputs[i]);
        }
        finally
        {
            FastForwarding = false;
        }
        Jumped?.Invoke();
    }
}

