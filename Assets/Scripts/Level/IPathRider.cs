using UnityEngine;

public struct PathPose
{
    public Vector2 position;
    public float angle; // degrés

    public PathPose(Vector2 position, float angle) { this.position = position; this.angle = angle; }
}

// Élément posé sur une trajectoire (enfant d'un MovingPath) qui a un corps physique à piloter.
// En jeu, son Transform reste à la position de départ (écrire un Transform à chaque pas coûte cher au rejeu accéléré) :
// sa position est celle du corps. Sans ce contrat, c'est le Transform qui suit (ex. BoostZone).
public interface IPathRider
{
    // Appelé à chaque pas par la trajectoire, avant le pas : corps sur 'now', vitesse pour arriver sur 'next' au pas suivant.
    // 'rotate' : suivre aussi l'angle ; 'teleport' : sauter d'abord sur 'now' (bout d'une trajectoire ouverte).
    void PathTick(PathPose now, PathPose next, bool rotate, bool teleport);
}
