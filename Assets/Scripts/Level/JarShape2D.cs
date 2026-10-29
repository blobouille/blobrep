using System.Collections.Generic;
using UnityEngine;

// Bocal : génère la courbe (CurvePath2D du même objet) d'un bocal ouvert en haut, à paroi épaisse. Toutes les cotes sont
// INTÉRIEURES (place réelle pour les gumori) ; la ligne médiane de la courbe est décalée d'une demi-épaisseur vers
// l'extérieur. Collision en contour (Outline) : la paroi est solide dedans ET dehors. Origine = milieu du fond intérieur.
// Décoché 'drive' : la courbe n'est plus réécrite, ses points s'éditent à la main (CurvePath2D).
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CurvePath2D))]
[AddComponentMenu("Gumori/Mécaniques/JarShape2D")]
public class JarShape2D : MonoBehaviour
{
    [Header("Ventre")]
    [Tooltip("Rayon intérieur du ventre (m) : demi-largeur de la place à l'intérieur.")]
    [Min(0.1f)] public float radius = 1.2f;
    [Tooltip("Hauteur intérieure de la partie droite du ventre (m), entre le fond arrondi et l'épaule.")]
    [Min(0f)] public float bellyHeight = 1f;
    [Tooltip("Arrondi du fond : 0 = fond plat à coins vifs, 1 = demi-cercle (fond aussi profond que le rayon).")]
    [Range(0f, 1f)] public float bottomRound = 0.5f;

    [Header("Goulot")]
    [Tooltip("Largeur intérieure du goulot (m) : seuls les gumori plus étroits passent (un gumori mou peut se faufiler un peu en dessous).")]
    [Min(0.05f)] public float neckWidth = 1f;
    [Tooltip("Hauteur intérieure de l'épaule (m) : transition du ventre au goulot. 0 = épaule plate.")]
    [Min(0f)] public float shoulderHeight = 0.5f;
    [Tooltip("Hauteur du goulot droit (m), au-dessus de l'épaule.")]
    [Min(0f)] public float neckHeight = 0.35f;
    [Tooltip("Évasement de l'ouverture (m) : le haut du goulot s'écarte de cette distance de chaque côté (aide à viser). 0 = droit.")]
    [Min(0f)] public float lipFlare = 0.1f;

    [Header("Paroi")]
    [Tooltip("Épaisseur de la paroi de verre (m), pleine (CurveCollider2D.solidWall). Écrite dans la courbe (CurvePath2D.thickness).")]
    [Min(0.02f)] public float wallThickness = 0.12f;
    [Tooltip("Coché : la courbe est recalculée depuis ces réglages. Décoché : la courbe garde ses points (édition à la main).")]
    public bool drive = true;

    [Header("Debug")]
    [Tooltip("Coché : contour de la paroi dessiné en jeu (rendu debug), en attendant son habillage.")]
    public bool showInGame = true;
    public Color previewColor = new Color(0.7f, 0.9f, 1f, 0.9f);

    const float Kappa = 0.5522847f;

    void LateUpdate()
    {
        if (!Application.isPlaying || !showInGame) return;
        var path = Path;
        var collider = GetComponent<CurveCollider2D>();
        Matrix4x4 m = transform.localToWorldMatrix;
        if (collider != null && collider.Body.isValid && collider.Body.type == Unity.U2D.Physics.PhysicsBody.BodyType.Kinematic)
        {
            var body = collider.Body;
            m = Matrix4x4.TRS((Vector2)body.position, Quaternion.Euler(0f, 0f, body.rotation.degrees), Vector3.one)
                * Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, transform.eulerAngles.z), Vector3.one).inverse * m;
        }
        var world = Unity.U2D.Physics.PhysicsWorld.defaultWorld;
        for (int side = 0; side < 2; side++)
        {
            var line = side == 0 ? path.left : path.right;
            for (int i = 1; i < line.Count; i++) world.DrawLine(m.MultiplyPoint3x4(line[i - 1]), m.MultiplyPoint3x4(line[i]), previewColor, 0f);
        }
        int n = path.left.Count - 1;
        if (n > 0)
        {
            world.DrawLine(m.MultiplyPoint3x4(path.left[0]), m.MultiplyPoint3x4(path.right[0]), previewColor, 0f);
            world.DrawLine(m.MultiplyPoint3x4(path.left[n]), m.MultiplyPoint3x4(path.right[n]), previewColor, 0f);
        }
    }

    CurvePath2D _path;
    public CurvePath2D Path => _path != null ? _path : (_path = GetComponent<CurvePath2D>());

    public float InnerNeckHalf => 0.5f * Mathf.Min(neckWidth, 2f * radius);
    public float InnerTop => bottomRound * radius + bellyHeight + shoulderHeight + neckHeight;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        if (!drive) return;
        var path = Path;
        path.points = Build();
        path.closed = false;
        path.thickness = wallThickness;
        path.collisionMode = CollisionMode.Outline;
        path.flipSide = false;
        // Paroi pleine : une chaîne (une seule face) se laisse traverser par un gumori tiré à travers le goulot.
        if (TryGetComponent(out CurveCollider2D collider)) collider.solidWall = true;
        path.RecomputeHandles();
        path.Rebuild();
    }

    // Ligne médiane de la paroi, de l'ouverture gauche à l'ouverture droite en passant par le fond (fond intérieur en y = 0).
    public List<CurvePoint> Build()
    {
        float h = 0.5f * wallThickness;
        float r = radius + h;
        float n = Mathf.Min(InnerNeckHalf + h, r);
        float corner = bottomRound * (radius + h);
        float flat = r - corner;
        float yCorner = -h + corner;
        float yShoulder = bottomRound * radius + bellyHeight;
        float yNeck = yShoulder + shoulderHeight;
        float yTop = yNeck + neckHeight;
        float belly = Mathf.Max(0f, yShoulder - yCorner);
        float sh = 0.6f * shoulderHeight;

        var right = new List<CurvePoint>();
        Vector2 bottomTangent = new Vector2(flat > 1e-3f ? flat / 3f : corner * Kappa, 0f);
        right.Add(Free(new Vector2(0f, -h), -bottomTangent, bottomTangent));
        bool sharp = corner <= 1e-3f;
        if (flat > 1e-3f) right.Add(Free(new Vector2(flat, -h), new Vector2(-flat / 3f, 0f), sharp ? new Vector2(0f, belly / 3f) : new Vector2(corner * Kappa, 0f)));
        if (!sharp) right.Add(Free(new Vector2(r, yCorner), new Vector2(0f, -corner * Kappa), new Vector2(0f, belly > 1e-3f ? belly / 3f : sh)));
        if (belly > 1e-3f) right.Add(Free(new Vector2(r, yShoulder), new Vector2(0f, -belly / 3f), new Vector2(0f, sh)));
        right.Add(Free(new Vector2(n, yNeck), new Vector2(0f, -sh), new Vector2(0f, neckHeight / 3f)));
        right.Add(Free(new Vector2(n + lipFlare, yTop), new Vector2(-lipFlare, -neckHeight) / 3f, Vector2.zero));

        var pts = new List<CurvePoint>();
        for (int i = right.Count - 1; i >= 1; i--) pts.Add(Mirror(right[i]));
        pts.Add(right[0]);
        for (int i = 1; i < right.Count; i++) pts.Add(right[i]);
        return pts;
    }

    static CurvePoint Free(Vector2 p, Vector2 hin, Vector2 hout) =>
        new CurvePoint(p, HandleMode.Free) { handleIn = hin, handleOut = hout };


    // Symétrique gauche, parcouru dans l'autre sens : tangentes échangées.
    static CurvePoint Mirror(CurvePoint p) => new CurvePoint(new Vector2(-p.position.x, p.position.y), HandleMode.Free)
    {
        handleIn = new Vector2(-p.handleOut.x, p.handleOut.y),
        handleOut = new Vector2(-p.handleIn.x, p.handleIn.y),
    };
}
