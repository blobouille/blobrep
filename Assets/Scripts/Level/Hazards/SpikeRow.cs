using UnityEngine;

[DisallowMultipleComponent]
public class SpikeRow : SpikedHazard
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
    [Tooltip("Inclinaison des piques : décalage latéral de la pointe, en fraction de la demi-largeur. 0 = symétrique, positif = vers +X local.")]
    [Range(-1f, 1f)] public float spikeLean = 0f;

    public override int SpikeCount => count;
    protected override float SpikeLean => spikeLean;

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

    public override void SpikeFrame(int i, out Vector2 baseCenter, out Vector2 up, out float width, out float height)
    {
        Layout(i, out float left, out width, out height);
        baseCenter = new Vector2(left + 0.5f * width, 0f);
        up = Vector2.up;
    }
}
