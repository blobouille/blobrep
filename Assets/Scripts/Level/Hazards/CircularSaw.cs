using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[DisallowMultipleComponent]
public class CircularSaw : SpikedHazard, ILevelTick
{
    [Header("Forme")]
    [Tooltip("Diamètre de la scie, pointes des dents comprises (dents de taille 1), en unités Unity (absolu). 1 = taille d'un blob standard.")]
    [Min(0.1f)] public float diameter = 2f;
    [Tooltip("Nombre de dents (piques) réparties sur le tour.")]
    [Range(3, 64)] public int teethCount = 15;
    [Tooltip("Profondeur des dents (du disque à la pointe), en unités Unity (absolu). Limitée à 90 % du rayon.")]
    [Min(0.01f)] public float toothDepth = 0.2f;
    [Tooltip("Inclinaison des dents : décalage latéral de la pointe, en fraction de la demi-largeur de la dent.\n" +
             "0 = dents symétriques, positif = penchées dans le sens anti-horaire.")]
    [Range(-1f, 1f)] public float toothLean = 0f;

    [Header("Rotation")]
    [Tooltip("Vitesse de rotation de la scie, en degrés par seconde. Positif = anti-horaire, 0 = immobile.\n" +
             "Modifiable en Play. Corps kinematic : la scie entraîne ce qu'elle touche sans être freinée.")]
    public float rotationSpeed = 360f;

    public float OuterRadius => 0.5f * diameter;
    public float CoreRadius => OuterRadius - Mathf.Min(toothDepth, 0.9f * OuterRadius);

    public override int SpikeCount => teethCount;
    protected override float SpikeLean => -toothLean;
    protected override PhysicsBody.BodyType BodyType => PhysicsBody.BodyType.Kinematic;

    public override Vector2 FocusPoint(Vector2 contactPoint) => Body.isValid ? Body.position : (Vector2)transform.position;

    protected override void OnBodyCreated()
    {
        var body = Body;
        body.sleepingAllowed = false;
        body.angularVelocity = rotationSpeed;
    }

    public void LevelTick()
    {
        var body = Body;
        if (body.isValid) body.angularVelocity = rotationSpeed;
    }

    public override void SpikeFrame(int i, out Vector2 baseCenter, out Vector2 up, out float width, out float height)
    {
        float pitch = 2f * Mathf.PI / teethCount;
        float mid = (i + 0.5f) * pitch;
        // Base de la dent légèrement sous le bord du disque : pas d'interstice entre dent et disque.
        float baseRadius = CoreRadius * 0.98f;
        float chordCenter = baseRadius * Mathf.Cos(0.5f * pitch);
        float size = SizeOf(i);

        up = new Vector2(Mathf.Cos(mid), Mathf.Sin(mid));
        baseCenter = up * chordCenter;
        width = 2f * baseRadius * Mathf.Sin(0.5f * pitch) * size;
        height = (OuterRadius - chordCenter) * size;
    }

    protected override void BuildBodyGeometry(List<Vector2[]> polygons, List<Vector3> circles)
    {
        circles.Add(new Vector3(0f, 0f, CoreRadius));
    }
}
