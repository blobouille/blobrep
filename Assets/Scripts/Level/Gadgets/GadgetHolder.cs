using System.Collections.Generic;
using UnityEngine;

// Emplacements de gadgets d'un blob (ajouté automatiquement avec le premier BlobGadget). Remis à zéro à chaque redémarrage.
// Les gadgets possibles sont les composants BlobGadget du même objet ; les zones (GadgetZone) les équipent ou les retirent.
[DisallowMultipleComponent]
[RequireComponent(typeof(BLOB))]
public class GadgetHolder : MonoBehaviour, ILevelPhysics
{
    [Header("Gadgets")]
    [Tooltip("Nombre de gadgets que le blob peut porter en même temps. Recevoir un gadget de plus retire le plus ancien.")]
    [Min(1)] public int maxGadgets = 1;

    // Holders actifs, dans l'ordre de création (ordre de la hiérarchie via LevelRestart).
    static readonly List<GadgetHolder> _active = new List<GadgetHolder>();
    public static IReadOnlyList<GadgetHolder> Active => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearActive() => _active.Clear();

    public BLOB Blob { get; private set; }

    BlobGadget[] _gadgets = new BlobGadget[0];
    // Gadgets équipés, du plus ancien au plus récent.
    readonly List<BlobGadget> _equipped = new List<BlobGadget>();
    public IReadOnlyList<BlobGadget> Equipped => _equipped;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        Blob = GetComponent<BLOB>();
        _gadgets = GetComponents<BlobGadget>();
        _equipped.Clear();
        foreach (var g in _gadgets) g.ResetEquipped();
        foreach (var g in _gadgets)
            if (g.equippedAtStart) Give(g.Type);
        _active.Add(this);
    }

    void OnDisable()
    {
        _active.Remove(this);
    }

    public bool Has(GadgetType type)
    {
        for (int i = 0; i < _equipped.Count; i++)
            if (_equipped[i].Type == type) return true;
        return false;
    }

    public bool Give(GadgetType type)
    {
        if (Has(type)) return false;
        BlobGadget gadget = null;
        for (int i = 0; i < _gadgets.Length && gadget == null; i++)
            if (_gadgets[i] != null && _gadgets[i].Type == type) gadget = _gadgets[i];
        if (gadget == null) return false;

        while (_equipped.Count >= maxGadgets)
        {
            var old = _equipped[0];
            _equipped.RemoveAt(0);
            old.SetEquipped(false);
        }
        _equipped.Add(gadget);
        gadget.SetEquipped(true);
        return true;
    }

    public void Remove(GadgetType type)
    {
        for (int i = 0; i < _equipped.Count; i++)
            if (_equipped[i].Type == type)
            {
                var g = _equipped[i];
                _equipped.RemoveAt(i);
                g.SetEquipped(false);
                return;
            }
    }

    public void RemoveAll()
    {
        while (_equipped.Count > 0)
        {
            var g = _equipped[_equipped.Count - 1];
            _equipped.RemoveAt(_equipped.Count - 1);
            g.SetEquipped(false);
        }
    }
}
