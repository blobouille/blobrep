using UnityEngine;

public enum GadgetType { Grapple }

// Capacité active du blob (grappin…), posée sur l'objet du BLOB avec ses réglages. Inactive tant qu'il ne l'a pas reçue
// (GadgetZone) : c'est le GadgetHolder du blob qui l'équipe / la retire, et la remet à zéro à chaque redémarrage.
[RequireComponent(typeof(GadgetHolder))]
public abstract class BlobGadget : MonoBehaviour
{
    [Header("Gadget")]
    [Tooltip("Coché : le blob a déjà ce gadget au départ du niveau (pratique pour tester sans zone de don).")]
    public bool equippedAtStart = false;

    public abstract GadgetType Type { get; }
    public bool Equipped { get; private set; }

    internal void SetEquipped(bool value)
    {
        if (Equipped == value) return;
        Equipped = value;
        if (value) OnEquipped();
        else OnUnequipped();
    }

    internal void ResetEquipped() => Equipped = false;

    protected virtual void OnEquipped() { }
    protected virtual void OnUnequipped() { }
}
