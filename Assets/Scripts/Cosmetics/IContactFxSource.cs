using System.Collections.Generic;
using UnityEngine;

// Contact d'un corps de balle avec une surface, vu par les effets visuels (lecture seule, aucune influence sur la physique).
public struct SurfaceContact
{
    public Vector2 point;
    // Normale de la surface, dirigée vers la balle.
    public Vector2 normal;
    // Vitesse du centre de la balle moins celle de la surface au point de contact (m/s). Le centre, pas le point :
    // une balle qui roule sans glisser a un point de contact immobile.
    public Vector2 relativeVelocity;
    // Impulsion normale du dernier pas (N·s) : force d'appui × durée du pas.
    public float normalImpulse;
    // Vitesse d'approche le long de la normale avant le pas (m/s, positive = choc).
    public float approachSpeed;
    // Contact apparu au dernier pas.
    public bool isNew;
}

// Balle dont les effets visuels lisent les contacts (BLOB aujourd'hui ; plus tard blob fantôme, blob d'un ami…).
public interface IContactFxSource
{
    void GetSurfaceContacts(List<SurfaceContact> contacts);
}
