using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[DisallowMultipleComponent]
public class SpikeRow : BlobHazard
{
    [Header("Rangée")]
    [Tooltip("Nombre de piques. Rangée alignée sur l'axe X local, centrée sur l'objet, pointes vers +Y local\n" +
             "(tourner le GameObject pour orienter la rangée).")]
    [Min(1)] public int count = 5;
    [Tooltip("Écart entre les bases de deux piques voisins, en unités Unity, multiplié par 'globalScale'. 0 = piques jointifs.")]
    [Min(0f)] public float spacing = 0f;

    [Header("Taille")]
    [Tooltip("Taille de tous les piques en même temps : multiplie largeur, hauteur et écart.")]
    [Min(0.01f)] public float globalScale = 1f;
    [Tooltip("Largeur de la base d'un pique, en unités Unity, multipliée par 'globalScale' et par sa taille individuelle.")]
    [Min(0.01f)] public float spikeWidth = 0.4f;
    [Tooltip("Hauteur d'un pique (base → pointe), en unités Unity, multipliée par 'globalScale' et par sa taille individuelle.")]
    [Min(0.01f)] public float spikeHeight = 0.5f;
    [Tooltip("Taille individuelle de chaque pique (multiplie sa largeur et sa hauteur), de gauche à droite.\n" +
             "La liste suit automatiquement 'count' ; les nouveaux piques valent 1.")]
    public float[] sizes = { 1f, 1f, 1f, 1f, 1f };

    [Header("Empalement")]
    [Tooltip("Les capsules du blob cassé qui touchent un pique s'y accrochent et s'enfoncent dedans.")]
    public bool impale = true;
    [Tooltip("Profondeur d'enfoncement du point de contact dans le pique, en unités Unity (absolu).")]
    [Min(0f)] public float impaleDepth = 0.12f;
    [Tooltip("Raideur de l'accroche, en Hz. Plus haut = la capsule s'enfonce plus vite et bouge moins une fois plantée.")]
    [Min(0.1f)] public float impaleFrequency = 6f;
    [Tooltip("Amortissement de l'accroche (ratio, 1 = critique). Plus bas = la capsule oscille avant de se figer.")]
    [Min(0f)] public float impaleDamping = 0.7f;

    public override bool OnCapsuleTouched(PhysicsBody capsule, Vector2 point)
    {
        if (!impale) return false;

        Vector2 dir = point - capsule.position;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : -(Vector2)transform.up;

        var jd = PhysicsFixedJointDefinition.defaultDefinition;
        jd.bodyA = Body;
        jd.bodyB = capsule;
        jd.localAnchorA = new PhysicsTransform(Body.GetLocalPoint(point + dir * impaleDepth),
                                               Body.rotation.InverseMultiplyRotation(capsule.rotation));
        jd.localAnchorB = new PhysicsTransform(capsule.GetLocalPoint(point), PhysicsRotate.identity);
        jd.linearFrequency = impaleFrequency;
        jd.linearDamping = impaleDamping;
        jd.angularFrequency = impaleFrequency;
        jd.angularDamping = impaleDamping;
        jd.collideConnected = false;
        PhysicsWorld.defaultWorld.CreateJoint(jd);
        return true;
    }

    void OnValidate()
    {
        SyncSizes();
    }

    public void SyncSizes()
    {
        if (sizes != null && sizes.Length == count) return;
        var old = sizes ?? new float[0];
        sizes = new float[count];
        for (int i = 0; i < count; i++) sizes[i] = i < old.Length ? Mathf.Max(0.01f, old[i]) : 1f;
    }

    public float SizeOf(int i) => sizes != null && i < sizes.Length ? Mathf.Max(0.01f, sizes[i]) : 1f;

    // Base gauche (x), largeur et hauteur de chaque pique, en local.
    public void Layout(int i, out float left, out float width, out float height)
    {
        float gap = spacing * globalScale;
        float total = 0f;
        for (int k = 0; k < count; k++) total += spikeWidth * globalScale * SizeOf(k);
        total += gap * (count - 1);

        left = -0.5f * total;
        for (int k = 0; k < i; k++) left += spikeWidth * globalScale * SizeOf(k) + gap;
        width = spikeWidth * globalScale * SizeOf(i);
        height = spikeHeight * globalScale * SizeOf(i);
    }

    public Vector2 TipOf(int i)
    {
        Layout(i, out float left, out float width, out float height);
        return new Vector2(left + 0.5f * width, height);
    }

    public override void BuildGeometry(List<Vector2[]> polygons, List<Vector3> circles)
    {
        for (int i = 0; i < count; i++)
        {
            Layout(i, out float left, out float width, out float height);
            polygons.Add(new[]
            {
                new Vector2(left, 0f),
                new Vector2(left + width, 0f),
                new Vector2(left + 0.5f * width, height)
            });
        }
    }
}
