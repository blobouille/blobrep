using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[DisallowMultipleComponent]
public class CircularSaw : BlobHazard
{
    [Header("Forme")]
    [Tooltip("Diamètre de la scie, pointes des dents comprises, en unités Unity (absolu). 1 = taille d'un blob standard.")]
    [Min(0.1f)] public float diameter = 2f;
    [Tooltip("Nombre de dents réparties sur le tour.")]
    [Range(3, 64)] public int teethCount = 16;
    [Tooltip("Profondeur des dents (du disque à la pointe), en unités Unity (absolu). Limitée à 90 % du rayon.")]
    [Min(0.01f)] public float toothDepth = 0.2f;
    [Tooltip("Inclinaison des dents : décalage de la pointe, en fraction de l'écart entre deux dents.\n" +
             "0 = dents symétriques, positif = penchées dans le sens anti-horaire.")]
    [Range(-1f, 1f)] public float toothLean = 0.35f;

    [Header("Rotation")]
    [Tooltip("Vitesse de rotation de la scie, en degrés par seconde. Positif = anti-horaire, 0 = immobile.\n" +
             "Modifiable en Play. Corps kinematic : la scie entraîne ce qu'elle touche sans être freinée.")]
    public float rotationSpeed = 360f;

    protected override PhysicsBody.BodyType BodyType => PhysicsBody.BodyType.Kinematic;

    protected override void OnBodyCreated()
    {
        var body = Body;
        body.sleepingAllowed = false;
        body.angularVelocity = rotationSpeed;
    }

    void FixedUpdate()
    {
        var body = Body;
        if (Application.isPlaying && body.isValid) body.angularVelocity = rotationSpeed;
    }

    public float OuterRadius => 0.5f * diameter;
    public float CoreRadius => OuterRadius - Mathf.Min(toothDepth, 0.9f * OuterRadius);

    public override Vector2 FocusPoint(Vector2 contactPoint) => transform.position;

    public override void BuildGeometry(List<Vector2[]> polygons, List<Vector3> circles)
    {
        float core = CoreRadius;
        circles.Add(new Vector3(0f, 0f, core));

        float pitch = 2f * Mathf.PI / teethCount;
        // Base des dents légèrement sous le bord du disque : pas d'interstice entre dent et disque.
        float baseRadius = core * Mathf.Cos(0.5f * pitch) * 0.98f;
        for (int i = 0; i < teethCount; i++)
        {
            float a0 = i * pitch;
            float a1 = a0 + pitch;
            float tip = a0 + 0.5f * pitch + toothLean * 0.5f * pitch;
            polygons.Add(new[]
            {
                Polar(baseRadius, a0),
                Polar(OuterRadius, tip),
                Polar(baseRadius, a1)
            });
        }
    }

    static Vector2 Polar(float r, float a) => new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));
}
