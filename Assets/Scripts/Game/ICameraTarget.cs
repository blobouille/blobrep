using UnityEngine;

public interface ICameraTarget
{
    Vector2 CameraPosition { get; }

    // Vitesse du point suivi (anticipation et dézoom de la caméra, compteur de vitesse). Faux = non fournie : moyenne des
    // corps de l'ITrainingBody (blob : sa rotation s'annule).
    bool TryGetCameraVelocity(out Vector2 velocity)
    {
        velocity = default;
        return false;
    }
}
