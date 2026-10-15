using System.Collections.Generic;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

// Remet le niveau exactement dans son état de départ, sans recharger la scène :
// toute la physique (ILevelPhysics) est détruite puis recréée dans l'ordre de la hiérarchie,
// et les systèmes hors physique (caméra, plus tard timer, musique…) se remettent à zéro via 'Restarted'.
// Le premier départ passe aussi par ici (au chargement de la scène) : chaque essai part du même état.
public static class LevelRestart
{
    public static event System.Action Restarted;

    static readonly List<MonoBehaviour> _physics = new List<MonoBehaviour>();
    static Scene _cachedScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Restarted = null;
        _physics.Clear();
        _cachedScene = default;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Appelé après les OnEnable de la scène et avant le premier FixedUpdate.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cachedScene = default;
        Restart();
    }

    // À appeler avant tout autre FixedUpdate du pas (voir RespawnOnKey, ordre d'exécution prioritaire).
    public static void Restart()
    {
        var active = SceneManager.GetActiveScene();
        if (_cachedScene != active) Collect(active);

        var enabled = new List<MonoBehaviour>(_physics.Count);
        foreach (var c in _physics)
            if (c != null && c.isActiveAndEnabled) enabled.Add(c);

        for (int i = enabled.Count - 1; i >= 0; i--) enabled[i].enabled = false;
        ResetWorld();
        for (int i = 0; i < enabled.Count; i++) enabled[i].enabled = true;

        Restarted?.Invoke();
    }

    // Sans Reset, le monde garde un historique interne (identifiants de contacts, îlots…) et deux essais divergent.
    static void ResetWorld()
    {
        var world = PhysicsWorld.defaultWorld;
        var bodies = world.GetBodies(Allocator.Temp);
        int left = bodies.Length;
        bodies.Dispose();

        if (left == 0)
        {
            world.Reset();
            return;
        }
        Debug.LogWarning($"LevelRestart : {left} corps physiques n'appartiennent pas à un ILevelPhysics. " +
                         "Le monde n'est pas réinitialisé : le redémarrage peut ne pas être identique au départ.");
    }

    static void Collect(Scene scene)
    {
        _cachedScene = scene;
        _physics.Clear();
        foreach (var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (c is ILevelPhysics) _physics.Add(c);
        _physics.Sort((a, b) => CompareHierarchy(a, b));
    }

    // Ordre stable : position dans la hiérarchie, puis ordre des composants sur l'objet.
    static int CompareHierarchy(Component a, Component b)
    {
        var pa = HierarchyPath(a.transform);
        var pb = HierarchyPath(b.transform);
        for (int i = 0; i < Mathf.Min(pa.Count, pb.Count); i++)
            if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        if (pa.Count != pb.Count) return pa.Count.CompareTo(pb.Count);
        return ComponentIndex(a).CompareTo(ComponentIndex(b));
    }

    static List<int> HierarchyPath(Transform t)
    {
        var path = new List<int>();
        for (; t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
        return path;
    }

    static int ComponentIndex(Component c)
    {
        var all = c.GetComponents<Component>();
        return System.Array.IndexOf(all, c);
    }
}
