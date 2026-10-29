using System.Collections.Generic;
using UnityEngine;

// Bandeau des stades : tous les niveaux de gumori, du plus petit au plus gros, côte à côte à leurs tailles relatives, avec les
// designs en jeu (GumoriSkin.designs : plus tard, ceux choisis par le joueur). Posé en haut ou en bas de l'écran (caméra
// orthographique), recalculé à chaque image : suit le format du téléphone. Aperçu hors Play (Scene view et Game view : régler
// la Game view au format d'un téléphone pour juger la place). Purement visuel.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Habillage/GumoriLevelStrip")]
public class GumoriLevelStrip : MonoBehaviour
{
    public enum Align { Centre, Sol, Plafond }

    [Header("Aperçu")]
    [Tooltip("Dessiné aussi hors Play (Scene view et Game view), pour choisir sa place et ses réglages sans lancer le jeu.")]
    public bool showInEditor = true;

    [Header("Place")]
    [Tooltip("Coché : juste sous la boîte (ou au-dessus avec 'above') quand l'écran a la place, sinon à 'viewportY'.")]
    public bool besideBox = true;
    [Tooltip("Avec 'besideBox' : au-dessus de la boîte au lieu de dessous.")]
    public bool above;
    [Tooltip("Écart entre la boîte et le bandeau (m).")]
    [Min(0f)] public float boxGap = 0.3f;
    [Tooltip("Hauteur du milieu du bandeau à l'écran : 0 = bas, 1 = haut.")]
    [Range(0f, 1f)] public float viewportY = 0.06f;
    [Tooltip("Largeur max occupée, en fraction de la largeur de l'écran.")]
    [Range(0.2f, 1f)] public float widthFraction = 0.92f;
    [Tooltip("Hauteur max (diamètre du plus gros), en fraction de la hauteur de l'écran.")]
    [Range(0.02f, 0.3f)] public float heightFraction = 0.08f;
    [Tooltip("Écart entre deux gumori, en fraction du diamètre du plus gros.")]
    [Range(0f, 1f)] public float gap = 0.15f;

    [Header("Tailles")]
    [Tooltip("Différence de taille entre les stades : 1 = vraies tailles relatives du jeu, 0 = tous à la même taille,\n" +
             "≈ 0,3 = légère différence (le plus gros garde sa taille, les petits grossissent).")]
    [Range(0f, 1f)] public float sizeContrast = 1f;
    [Tooltip("Alignement vertical : Centre = milieux sur une ligne, Sol = tous posés sur le même sol, Plafond = hauts alignés.")]
    public Align align = Align.Centre;

    [Header("Liens")]
    public GumoriSkin skin;
    public GumoriFusion fusion;
    [Tooltip("Boîte du jeu (sa largeur borne celle du bandeau). Vide = la courbe de GumoriLauncher.")]
    public CurvePath2D box;

    readonly List<GumoriSkin.VirtualSlime> _slimes = new List<GumoriSkin.VirtualSlime>();
    readonly List<float> _diameters = new List<float>();

    void OnEnable()
    {
        if (skin == null) skin = FindAnyObjectByType<GumoriSkin>();
        if (fusion == null) fusion = FindAnyObjectByType<GumoriFusion>();
        if (box == null) { var l = FindAnyObjectByType<GumoriLauncher>(); if (l != null) box = l.box; }
        if (skin != null) { skin.DrawExtras += OnDraw; skin.DrawEditorExtras += OnDraw; }
    }

    void OnDisable()
    {
        if (skin != null) { skin.DrawExtras -= OnDraw; skin.DrawEditorExtras -= OnDraw; }
        _slimes.Clear();
    }

    void OnDraw(float dt)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic || fusion == null || skin == null) return;
        if (!Application.isPlaying && !showInEditor) return;
        int count = fusion.MaxLevel + 1;
        _diameters.Clear();
        float sum = 0f, biggest = 0f;
        for (int level = 0; level < count; level++) biggest = Mathf.Max(biggest, fusion.LevelDiameter(level));
        for (int level = 0; level < count; level++)
        {
            // Rapport au plus gros élevé à 'sizeContrast' : 1 = inchangé, 0 = tous égaux au plus gros.
            float d = biggest * Mathf.Pow(fusion.LevelDiameter(level) / biggest, sizeContrast);
            _diameters.Add(d);
            sum += d;
        }
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 cam2 = cam.transform.position;
        // Largeur dispo : celle de l'écran, bornée par celle de la boîte (bords extérieurs).
        float width = 2f * w * widthFraction, boxMid = cam2.x, boxTop = 0f, boxBottom = 0f;
        if (box != null && box.left.Count == 0) box.Rebuild();
        bool hasBox = box != null && box.left.Count > 0;
        if (hasBox)
        {
            float minX = float.MaxValue, maxX = float.MinValue; boxTop = float.MinValue; boxBottom = float.MaxValue;
            for (int s = 0; s < 2; s++)
                foreach (var p in s == 0 ? box.left : box.right)
                {
                    Vector2 q = box.transform.TransformPoint(p);
                    minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
                    boxTop = Mathf.Max(boxTop, q.y); boxBottom = Mathf.Min(boxBottom, q.y);
                }
            width = Mathf.Min(width, maxX - minX);
            boxMid = 0.5f * (minX + maxX);
        }
        // Échelle : tient dans la largeur (diamètres + écarts) et dans la hauteur voulues.
        float span = sum + gap * biggest * (count - 1);
        float scale = Mathf.Min(width / span, 2f * h * heightFraction / biggest);
        float half = 0.5f * biggest * scale;
        float y = cam2.y + (2f * viewportY - 1f) * h;
        float x = cam2.x - 0.5f * scale * span;
        if (besideBox && hasBox)
        {
            float beside = above ? boxTop + boxGap + half : boxBottom - boxGap - half;
            // Seulement si le bandeau entier tient à l'écran à cet endroit.
            if (beside - half >= cam2.y - h && beside + half <= cam2.y + h) { y = beside; x = boxMid - 0.5f * scale * span; }
        }
        while (_slimes.Count < count) _slimes.Add(skin.NewVirtual(_slimes.Count, 0.5f));
        for (int level = 0; level < count; level++)
        {
            float r = 0.5f * _diameters[level] * scale;
            var v = _slimes[level];
            if (Mathf.Abs(v.radius - r) > 1e-4f) { v = skin.NewVirtual(level, r); _slimes[level] = v; }
            float dy = align == Align.Sol ? r - half : align == Align.Plafond ? half - r : 0f;
            v.center = new Vector2(x + r, y + dy);
            skin.DrawVirtual(v, dt);
            x += 2f * r + gap * biggest * scale;
        }
    }
}
