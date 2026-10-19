using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// État d'un corps physique mémorisé dans un point de sauvegarde (fantôme, replacement si le rejeu diverge).
[System.Serializable]
public struct BodyState
{
    public Vector2 position;
    public float angle;
    public Vector2 velocity;
    public float angularVelocity;
}

// Balle suivie par le mode entraînement (BLOB, Ball2D…) : fantôme à chaque point de sauvegarde, et replacement exact
// quand le rejeu des entrées ne retombe plus au même endroit (niveau modifié avant le point).
public interface ITrainingBody
{
    // Forme de chaque corps dans son repère local (cercle = capsule de longueur nulle), pour dessiner le fantôme.
    CapsuleGeometry GhostShape { get; }
    // Forme du corps 'index' quand ils n'ont pas tous la même (skateur : planche, tête, membres). Par défaut : GhostShape.
    CapsuleGeometry GhostShapeOf(int index) => GhostShape;
    bool IsBroken { get; }
    void GetBodyStates(List<BodyState> states);
    void RestoreBodyStates(IReadOnlyList<BodyState> states, bool broken);
}
