// Retire les shiny automatiques de SlimeSkin, vitrine en tableau (familles × paliers), gerbe de cristaux (outil ponctuel).
const fs = require('fs');
const path = require('path');
const file = path.join(__dirname, '..', '..', 'Assets', 'Scripts', 'Cosmetics', 'SlimeSkin.cs');
let s = fs.readFileSync(file, 'utf8');
const rep = (x, y) => { if (!s.includes(x)) throw new Error('missing: ' + x.slice(0, 90)); s = s.split(x).join(y); };

rep(`    [Tooltip("Vitrine : une ligne avec la version shiny de chaque niveau.")]
    public bool galleryShiny = true;
    [Tooltip("Vitrine : les autres gumori de la collection (variantes).")]
    public bool galleryVariants = true;
    [Tooltip("Coin haut gauche de la vitrine, en m.")]
    public Vector2 galleryOrigin = new Vector2(-17f, 15f);
    [Tooltip("Largeur max d'une ligne de la vitrine, en m.")]
    [Min(1f)] public float galleryWidth = 12f;`,
`    [Tooltip("Vitrine : les autres gumori de la collection, rangés sous leur famille, une rangée par palier (commun, rare,\\n" +
             "ultra rare).")]
    public bool galleryVariants = true;
    [Tooltip("Coin haut gauche de la vitrine, en m.")]
    public Vector2 galleryOrigin = new Vector2(-17f, 15f);`);

rep(`        public SlimeDesign design; // design imposé (vitrine), sinon celui du niveau
        public bool shiny;`, `        public SlimeDesign design; // design imposé (vitrine), sinon celui du niveau`);

rep(`    struct GalleryEntry
    {
        public SlimeDesign design;
        public int level;
        public bool shiny, newRow;
    }
    readonly List<GalleryEntry> _gallery = new List<GalleryEntry>();`,
`    struct GalleryEntry
    {
        public SlimeDesign design;
        public int level;
        public Vector2 center;
        public float radius;
    }
    readonly List<GalleryEntry> _gallery = new List<GalleryEntry>();
    float[] _galleryColumns = new float[0];
    int[] _galleryUsed = new int[0];`);

rep(`    SlimeDesign Resolve(Look look)
    {
        var d = look.design ?? Design(look.level);
        return d != null && look.shiny ? d.Shiny : d;
    }`, `    SlimeDesign Resolve(Look look) => look.design ?? Design(look.level);`);

rep(`    void OnValidate()
    {
        foreach (var d in designs) d?.ResetShinyCache();
        foreach (var d in variants) d?.ResetShinyCache();
    }

`, '');

// Vitrine : de "// Vitrine : une ligne" jusqu'à "    GUIStyle _label;"
const a = s.indexOf('    // Vitrine : une ligne par niveau'), b = s.indexOf('    GUIStyle _label;');
if (a < 0 || b < 0) throw new Error('vitrine introuvable');
s = s.slice(0, a) + String.raw`    // Vitrine en tableau : une colonne par famille (taille de son niveau), première rangée = les versions de base, puis une
    // rangée par palier (commun, rare, ultra rare) ; deux variantes d'une même famille et d'un même palier = rangée de plus.
    void BuildGallery()
    {
        _gallery.Clear();
        int max = Mathf.Min(_fusion.MaxLevel, designs.Count - 1);
        if (_galleryColumns.Length < max + 1) { _galleryColumns = new float[max + 1]; _galleryUsed = new int[max + 1]; }
        float x = 0f, rowHeight = 0f;
        for (int l = 0; l <= max; l++)
        {
            float d = _fusion.LevelDiameter(l);
            _galleryColumns[l] = x + 0.5f * d;
            x += d + 0.5f;
            rowHeight = Mathf.Max(rowHeight, d);
        }
        float rowStep = rowHeight + 1.6f, y = 0f;
        for (int l = 0; l <= max; l++) AddGallery(Design(l), l, y, rowHeight);
        if (!galleryVariants) return;
        for (int tier = 0; tier <= (int)GumoriRarity.UltraRare; tier++)
        {
            System.Array.Clear(_galleryUsed, 0, _galleryUsed.Length);
            int rows = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (v == null || (int)v.rarity != tier) continue;
                int l = Mathf.Min((int)v.family, max);
                int row = _galleryUsed[l]++;
                rows = Mathf.Max(rows, row + 1);
                AddGallery(v, l, y + rowStep * (1 + row), rowHeight);
            }
            y += rowStep * rows;
        }
        if (_gallery.Count > _galleryLooks.Length) _gallery.RemoveRange(_galleryLooks.Length, _gallery.Count - _galleryLooks.Length);
    }

    // Posé sur le bas de sa rangée (comme sur une étagère).
    void AddGallery(SlimeDesign design, int level, float rowTop, float rowHeight)
    {
        if (design == null) return;
        float r = 0.5f * _fusion.LevelDiameter(level);
        _gallery.Add(new GalleryEntry
        {
            design = design, level = level, radius = r,
            center = galleryOrigin + new Vector2(_galleryColumns[level], -(rowTop + rowHeight - r))
        });
    }

    void DrawGallery(float dt)
    {
        if (_fusion == null) return;
        BuildGallery();
        float t = Time.realtimeSinceStartup;
        for (int i = 0; i < _gallery.Count; i++)
        {
            var e = _gallery[i];
            var look = _galleryLooks[i] ??= NewLook(e.level, e.radius);
            look.level = e.level;
            look.design = e.design;
            look.happy = i % 3 == 1 ? 1f : 0f;
            EmitRound(look, e.center, e.radius, dt, 1f, t + i);
        }
    }

    static readonly string[] RarityNames = { "commun", "rare", "ultra rare", "spécial" };

    void OnGUI()
    {
        if (!Application.isPlaying || !galleryInPlay || _fusion == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        for (int i = 0; i < _gallery.Count; i++)
        {
            var e = _gallery[i];
            Vector3 s = cam.WorldToScreenPoint(e.center + Vector2.down * (e.radius + 0.3f));
            GUI.Label(new Rect(s.x - 80f, Screen.height - s.y - 10f, 160f, 20f), $"{e.design.name} · {RarityNames[(int)e.design.rarity]}", _label);
        }
    }

` + s.slice(b);

// Cristaux : une seule fonction pour la gerbe de 3 et la grande gerbe de 5.
rep(`            case SlimeHead.Crystals:
                if (front) return;
                for (int i = 0; i < 3; i++)
                {
                    float theta = 92f + (i - 1) * 21f;
                    float len = i == 1 ? 0.66f : (i == 0 ? 0.45f : 0.4f), w = i == 1 ? 0.1f : 0.08f;
                    Anchor(theta, n, rot, frame, out var p, out var nor);
                    float tilt = (theta - 92f) * 0.6f * Mathf.Deg2Rad;
                    _poly.Clear();
                    _poly.Add(new Vector2(-w, -0.15f)); _poly.Add(new Vector2(w, -0.15f)); _poly.Add(new Vector2(w * 1.1f, len * 0.62f));
                    _poly.Add(new Vector2(0f, len)); _poly.Add(new Vector2(-w * 1.1f, len * 0.62f));
                    for (int j = 0; j < _poly.Count; j++) _poly[j] = Rotate(_poly[j] + new Vector2(0f, 0.15f), tilt) - new Vector2(0f, 0.15f);
                    Polygon(p, nor, size, Rotate(new Vector2(0f, len * 0.3f + 0.15f), tilt) - new Vector2(0f, 0.15f), col, d.glow + 0.5f, aa);
                }`,
`            case SlimeHead.Crystals:
                if (front) return;
                for (int i = 0; i < 3; i++)
                    Crystal(92f + (i - 1) * 21f, i == 1 ? 0.66f : (i == 0 ? 0.45f : 0.4f), i == 1 ? 0.1f : 0.08f, n, rot, frame, size, col, d.glow + 0.5f, aa);
                break;
            case SlimeHead.CrystalCluster:
                if (front) return;
                for (int i = 0; i < 5; i++)
                    Crystal(92f + (i - 2) * 17f, ClusterLength[i], i == 2 ? 0.1f : 0.075f, n, rot, frame, size,
                        i % 2 == 0 ? col : Color.Lerp(col, Color.white, 0.35f), d.glow + 0.5f, aa);`);

rep(`    // Feuille : base 'b', inclinaison 'tilt'`, `    static readonly float[] ClusterLength = { 0.36f, 0.52f, 0.78f, 0.5f, 0.34f };

    // Cristal planté au bord à l'angle 'theta' (degrés, repère du corps), longueur et demi-largeur en rayons, penché vers
    // l'extérieur de la gerbe.
    void Crystal(float theta, float len, float w, int n, float rot, float frame, float size, Color col, float glow, float aa)
    {
        Anchor(theta, n, rot, frame, out var p, out var nor);
        float tilt = (theta - 92f) * 0.6f * Mathf.Deg2Rad;
        _poly.Clear();
        _poly.Add(new Vector2(-w, -0.15f)); _poly.Add(new Vector2(w, -0.15f)); _poly.Add(new Vector2(w * 1.1f, len * 0.62f));
        _poly.Add(new Vector2(0f, len)); _poly.Add(new Vector2(-w * 1.1f, len * 0.62f));
        for (int j = 0; j < _poly.Count; j++) _poly[j] = Rotate(_poly[j] + new Vector2(0f, 0.15f), tilt) - new Vector2(0f, 0.15f);
        Polygon(p, nor, size, Rotate(new Vector2(0f, len * 0.3f + 0.15f), tilt) - new Vector2(0f, 0.15f), col, glow, aa);
    }

    // Feuille : base 'b', inclinaison 'tilt'`);

fs.writeFileSync(file, s);
console.log('ok');
