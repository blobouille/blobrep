using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RespawnOnKey : MonoBehaviour
{
    [Header("Respawn (test)")]
    [Tooltip("Touche qui fait réapparaître tous les BLOB puis toutes les Ball2D de la scène à la position de leur Transform.")]
    public Key key = Key.R;

    bool _requested;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[key].wasPressedThisFrame) _requested = true;
    }

    void FixedUpdate()
    {
        if (!_requested) return;
        _requested = false;

        foreach (var blob in SortedByHierarchy(FindObjectsByType<BLOB>())) blob.Respawn();
        foreach (var ball in SortedByHierarchy(FindObjectsByType<Ball2D>())) ball.Respawn();
    }

    // Ordre de création stable (position dans la hiérarchie) : FindObjectsByType ne garantit aucun ordre.
    static List<T> SortedByHierarchy<T>(T[] items) where T : Component
    {
        var list = new List<T>(items);
        list.Sort((a, b) => CompareHierarchy(a.transform, b.transform));
        return list;
    }

    static int CompareHierarchy(Transform a, Transform b)
    {
        var pa = HierarchyPath(a);
        var pb = HierarchyPath(b);
        for (int i = 0; i < Mathf.Min(pa.Count, pb.Count); i++)
            if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        return pa.Count.CompareTo(pb.Count);
    }

    static List<int> HierarchyPath(Transform t)
    {
        var path = new List<int>();
        for (; t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
        return path;
    }
}
