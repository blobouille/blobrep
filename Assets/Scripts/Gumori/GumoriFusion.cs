using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Fusion des gumori : deux gumori de même taille qui se touchent deviennent un seul gumori du niveau suivant (taille linéaire).
// Mitose à l'envers : un seul anneau remplace les deux, posé en cacahuète sur eux et tenu dans cette forme par les angles de
// repos de ses joints, ramenés vers le cercle en 'mergeTime' : le col s'ouvre, les bouts rentrent, les capsules s'épaississent.
// Contacts entre gumori comptés par événements (sommes entières : indépendant de leur ordre), fusions dans l'ordre de création.
public enum GrowthMode { [InspectorName("Arithmétique (+)")] Arithmetic, [InspectorName("Multiplication (×)")] Geometric }

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(GumoriDropper))]
public class GumoriFusion : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Fusion")]
    [Tooltip("Comment la taille grandit d'un niveau à l'autre.\n" +
             "Arithmétique : on AJOUTE la même longueur à chaque niveau (growth).\n" +
             "Multiplication : on MULTIPLIE le diamètre par le même facteur à chaque niveau (geometricGrowth) : petits écarts\n" +
             "entre les petits, grands écarts entre les gros.")]
    public GrowthMode growthMode = GrowthMode.Arithmetic;
    [Tooltip("Mode arithmétique : chaque niveau ajoute (growth − 1) × le diamètre du premier gumori.\n" +
             "Diamètre du niveau n = modèle × (1 + (growth − 1) × n). Ex. 1,25 avec un modèle de 1 m : 1 ; 1,25 ; 1,5 ; 1,75… ; 3,5 m au niveau 10.")]
    [Min(1f)] public float growth = 2f;
    [Tooltip("Mode multiplication : diamètre du niveau n = modèle × geometricGrowth^n.\n" +
             "Ex. 1,168 avec un modèle de 1 m : 1 ; 1,17 ; 1,36 ; 1,59… ; 4,7 m au niveau 10 (même taille max que l'arithmétique à 1,37).\n" +
             "Attention : ça grimpe vite (1,25 → 9,3 m au niveau 10, plus grand que le réservoir).")]
    [Min(1.01f)] public float geometricGrowth = 1.168f;
    [Tooltip("Nombre max de capsules d'un gumori fusionné. Chaque fusion double le nombre de capsules (capsules de même longueur :\n" +
             "les deux anneaux se recousent exactement) jusqu'à ce maximum. Plus = plus rond, plus coûteux.")]
    [Range(3, 32)] public int maxSides = 32;
    [Tooltip("Nombre max de fusions successives : au-delà, deux gumori de même taille ne fusionnent plus. 0 = sans limite.")]
    [Min(0)] public int maxLevel = 10;
    [Tooltip("Tenue des gros gumori face à la gravité, comparée au gumori lâché :\n" +
             "1 = tous se comportent pareil (même affaissement relatif, juste plus gros) ;\n" +
             "> 1 = plus ils sont gros, plus ils sont durs (2 = comme sans ajustement) ; < 1 = plus ils sont gros, plus ils sont mous ;\n" +
             "0 = aucun ajustement (mêmes joints que le modèle : à raideur égale, un gumori 2 × plus gros s'affaisse 2 × moins).")]
    [Min(0f)] public float sizeStiffness = 1f;

    [Header("Animation (mitose à l'envers)")]
    [Tooltip("Durée (s) pour que les deux gumori n'en fassent plus qu'un : la cacahuète est TENUE par les joints, puis ramenée\n" +
             "vers le cercle en suivant 'mergeCurve' ; ses capsules s'épaississent en même temps.\n" +
             "Pendant ce temps il ne peut pas refusionner (il le fera à la fin s'il touche encore un gumori de sa taille).")]
    [Min(0.01f)] public float mergeTime = 0.8f;
    [Tooltip("Avancement de la fusion dans le temps : 0 = cacahuète, 1 = rond. Horizontale = durée (0 → 1 = 'mergeTime').\n" +
             "Ex. : lente au début puis rapide = le col hésite puis s'ouvre d'un coup.")]
    public AnimationCurve mergeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Raideur en flexion pendant la fusion, en fraction de celle du gumori (remonte à 1 avec l'avancement).\n" +
             "1 = la forme suit exactement la courbe ; plus bas = plus mou, il s'affaisse et tremble en s'arrondissant.")]
    [Range(0.01f, 1f)] public float startStiffness = 1f;
    [Tooltip("Largeur du col de la cacahuète au moment de la fusion (demi-largeur, en fraction du rayon des deux gumori).\n" +
             "Petit = deux boules à peine soudées qui s'ouvrent ; 1 = presque un ovale dès le départ.")]
    [Range(0.05f, 1f)] public float waist = 0.4f;

    [Header("Couleurs")]
    [Tooltip("Couleur des niveaux : [0] = niveau 1 (après 1 fusion), [1] = niveau 2… (la dernière se répète).\n" +
             "Niveau 0 : couleur du gumori modèle. Les gumori lâchés plus gros prennent la couleur de leur niveau.")]
    public Color[] levelColors =
    {
        // Familles de couleur des gumori (mêmes que GumoriSkin, en linéaire), à partir du niveau 1 (le niveau 0, orange, est
        // la couleur du modèle) : violet, rose, bleu, jaune, rouge, crème, chocolat, vert, noir, spécial.
        new Color(0.352f, 0.065f, 0.730f), new Color(1.000f, 0.371f, 0.638f), new Color(0.050f, 0.238f, 0.855f),
        new Color(1.000f, 0.658f, 0.058f), new Color(0.745f, 0.030f, 0.050f), new Color(1.000f, 0.863f, 0.896f),
        new Color(0.195f, 0.060f, 0.018f), new Color(0.258f, 0.672f, 0.042f), new Color(0.027f, 0.019f, 0.040f),
        new Color(0.991f, 0.991f, 1.000f)
    };

    struct Fusing
    {
        public Gumori blob;
        public float time, radius0, radius1, written;
        public float stretch; // allongement des capsules (m) qui donne à l'anneau le périmètre de la cacahuète
        public float[] bend; // angle de repos de chaque joint (rad) qui tient la cacahuète
    }

    struct Candidate
    {
        public Gumori a, b;
        public int first, second;
    }

    GumoriDropper _dropper;
    // Contacts en cours entre deux gumori de même taille (clé rangée par instance : sert seulement de clé, jamais d'ordre).
    readonly Dictionary<(Gumori, Gumori), int> _touching = new Dictionary<(Gumori, Gumori), int>();
    readonly List<(Gumori, Gumori)> _keys = new List<(Gumori, Gumori)>();
    readonly List<Candidate> _candidates = new List<Candidate>();
    readonly List<Fusing> _fusing = new List<Fusing>();
    readonly Vector2[] _loop = new Vector2[LoopSamples];
    readonly float[] _arc = new float[LoopSamples + 1];
    readonly float[] _bend = new float[32], _stretch = new float[32], _angles = new float[32];
    int _eventsStep = -1;

    public int Merges { get; private set; }
    // Fusion faite (pendant le pas) : gumori créé, son niveau (1 = première fusion), point de rencontre des deux gumori.
    public event System.Action<Gumori, int, Vector2> Merged;

    // Définition des niveaux, la même pour les gumori lâchés et fusionnés (diamètres calculés de la même façon : égaux au bit
    // près, c'est ce qui permet de reconnaître deux gumori de même niveau). Niveau 0 = gumori modèle.
    public int MaxLevel => maxLevel > 0 ? maxLevel : 30;

    public float LevelDiameter(int level)
    {
        float diameter = Template.diameter;
        if (growthMode == GrowthMode.Geometric) return diameter * Mathf.Pow(geometricGrowth, level);
        return diameter * (1f + (growth - 1f) * level);
    }

    public int LevelSides(int level)
    {
        int sides = Template.sides;
        for (int i = 0; i < level && sides < maxSides; i++) sides = Mathf.Min(maxSides, 2 * sides);
        return sides;
    }

    public Color LevelColor(int level)
    {
        if (level <= 0 || levelColors == null || levelColors.Length == 0) return Template.debugColor;
        return levelColors[Mathf.Min(level - 1, levelColors.Length - 1)];
    }

    public float LevelStiffness(int level) => SizeStiffness(LevelDiameter(level) / Template.diameter);

    public int LevelOf(Gumori blob) => Level(blob);

    Gumori Template => (_dropper != null ? _dropper : GetComponent<GumoriDropper>()).template;

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _touching.Clear();
        _fusing.Clear();
        _eventsStep = LevelSimulation.Step;
        Merges = 0;
    }

    void OnDisable()
    {
        _touching.Clear();
        _fusing.Clear();
    }

    public void LevelTick()
    {
        if (_dropper == null || _dropper.template == null) return;
        ReadContacts();
        Animate();
        MergeTouching();
    }

    void ReadContacts()
    {
        int step = LevelSimulation.Step;
        if (_eventsStep == step) return;
        _eventsStep = step;
        if (step == 0) return;
        var world = PhysicsWorld.defaultWorld;
        foreach (var e in world.contactBeginEvents) Touch(e.shapeA, e.shapeB, 1);
        foreach (var e in world.contactEndEvents) Touch(e.shapeA, e.shapeB, -1);
    }

    void Touch(PhysicsShape shapeA, PhysicsShape shapeB, int delta)
    {
        var a = Gumori.FromShape(shapeA);
        if (a == null) return;
        var b = Gumori.FromShape(shapeB);
        if (b == null || a == b || a.diameter != b.diameter || a.sides != b.sides) return;
        var key = a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
        _touching.TryGetValue(key, out int count);
        count += delta;
        if (count > 0) _touching[key] = count;
        else _touching.Remove(key);
    }

    // Gumori fusionné, selon l'avancement e (0 → 1) : angles de repos des joints de la cacahuète à 0 (= cercle), raideur de
    // 'startStiffness' à 1 (géométriquement), épaisseur des capsules de l'ancienne à la sienne (écrite par pas de 1 cm :
    // changer une forme reconstruit ses contacts).
    void Animate()
    {
        float dt = Time.fixedDeltaTime;
        for (int i = _fusing.Count - 1; i >= 0; i--)
        {
            var f = _fusing[i];
            if (f.blob == null || !f.blob.isActiveAndEnabled) { _fusing.RemoveAt(i); continue; } // retiré (fin de niveau)
            f.time += dt;
            float u = Mathf.Clamp01(f.time / mergeTime);
            float e = u >= 1f ? 1f : Mathf.Clamp01(mergeCurve != null ? mergeCurve.Evaluate(u) : u);
            int m = f.bend.Length;
            for (int j = 0; j < m; j++) { _bend[j] = f.bend[j] * (1f - e); _stretch[j] = f.stretch * (1f - e); }
            f.blob.SetShape(_stretch, _bend);
            f.blob.SetBendStiffness(u >= 1f ? 1f : Mathf.Pow(startStiffness, 1f - e));
            float radius = Mathf.Lerp(f.radius0, f.radius1, e);
            if (u >= 1f || Mathf.Abs(radius - f.written) >= 0.01f)
            {
                f.written = u >= 1f ? f.radius1 : radius;
                f.blob.SetCapsuleRadius(f.written);
            }
            if (u >= 1f) _fusing.RemoveAt(i);
            else _fusing[i] = f;
        }
    }

    // Le gumori est-il en train de prendre forme après une fusion (cacahuète ramenée au cercle) ?
    public bool IsMerging(Gumori blob) => IsFusing(blob);

    bool IsFusing(Gumori blob)
    {
        for (int i = 0; i < _fusing.Count; i++)
            if (_fusing[i].blob == blob) return true;
        return false;
    }

    // Paires prêtes rangées par ordre de création des gumori, puis fusionnées une à une (un gumori ne fusionne qu'une fois par pas).
    void MergeTouching()
    {
        if (_touching.Count == 0) return;
        _candidates.Clear();
        _keys.Clear();
        foreach (var pair in _touching)
        {
            var (a, b) = pair.Key;
            int ia = GumoriDropper.IsInPlay(a) ? a.Index : -1, ib = GumoriDropper.IsInPlay(b) ? b.Index : -1;
            if (ia < 0 || ib < 0) { _keys.Add(pair.Key); continue; } // gumori fusionné ou retiré
            if (maxLevel > 0 && Level(a) >= maxLevel) continue;
            if (IsFusing(a) || IsFusing(b)) continue;
            _candidates.Add(ia < ib
                ? new Candidate { a = a, b = b, first = ia, second = ib }
                : new Candidate { a = b, b = a, first = ib, second = ia });
        }
        for (int k = 0; k < _keys.Count; k++) _touching.Remove(_keys[k]);
        if (_candidates.Count == 0) return;
        _candidates.Sort((x, y) => x.first != y.first ? x.first.CompareTo(y.first) : x.second.CompareTo(y.second));

        for (int k = 0; k < _candidates.Count; k++)
        {
            var c = _candidates[k];
            if (c.a.isActiveAndEnabled && c.b.isActiveAndEnabled) Merge(c.a, c.b);
        }
    }

    int Level(Gumori blob)
    {
        float ratio = blob.diameter / _dropper.template.diameter;
        if (growthMode == GrowthMode.Geometric)
            return geometricGrowth <= 1f ? 0 : Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(ratio) / Mathf.Log(geometricGrowth)));
        if (growth <= 1f) return 0;
        return Mathf.RoundToInt((ratio - 1f) / (growth - 1f));
    }

    void Merge(Gumori a, Gumori b)
    {
        int n = a.CapsuleCount;
        if (n == 0 || b.CapsuleCount != n) return;

        Vector2 centerA = Vector2.zero, centerB = Vector2.zero, velocity = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            var ca = a.Capsule(i);
            var cb = b.Capsule(i);
            centerA += ca.position;
            centerB += cb.position;
            velocity += ca.linearVelocity + cb.linearVelocity;
        }
        centerA /= n;
        centerB /= n;
        velocity /= 2 * n;

        BuildPeanut(centerA, centerB, a.RestRingRadius);
        const int count = LoopSamples;
        float length = _arc[count];

        float radius0 = a.CapsuleThickness;
        int level = Level(a) + 1;
        _dropper.Remove(a);
        _dropper.Remove(b);

        var merged = _dropper.SpawnLevel(0.5f * (centerA + centerB), level);
        int m = merged.CapsuleCount;
        float step = length / m;
        for (int k = 0; k < m; k++)
        {
            float s = (k + 0.5f) * step;
            Vector2 tangent = PointAt(s + 0.5f * step, count) - PointAt(s - 0.5f * step, count);
            var body = merged.Capsule(k);
            _angles[k] = Mathf.Atan2(tangent.y, tangent.x) - 0.5f * Mathf.PI;
            body.transform = new PhysicsTransform(PointAt(s, count), PhysicsRotate.FromRadians(_angles[k]));
            body.linearVelocity = velocity;
            body.angularVelocity = 0f;
        }

        // La cacahuète est tenue telle quelle : capsules allongées à son périmètre (sinon, si 'growth' < 2, les joints la contractent
        // d'un coup), et angles de repos des joints = ses angles. Les joints précontraints visent (angle relatif) = angle de repos,
        // l'anneau fermé en ajoute 2π/m à tous : angle de repos = angle de la cacahuète − 2π/m. Ramenés à 0 en 'mergeTime'.
        float stretch = Mathf.Max(0f, step - 2f * Mathf.PI * merged.RestRingRadius / m);
        var bend = new float[m];
        for (int j = 0; j < m; j++)
        {
            float turn = Mathf.DeltaAngle(_angles[j == 0 ? m - 1 : j - 1] * Mathf.Rad2Deg, _angles[j] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            bend[j] = turn - 2f * Mathf.PI / m;
            _stretch[j] = stretch;
        }
        merged.SetCapsuleRadius(radius0);
        merged.SetShape(_stretch, bend);
        merged.SetBendStiffness(startStiffness);
        _fusing.Add(new Fusing { blob = merged, radius0 = radius0, radius1 = merged.CapsuleThickness, written = radius0, bend = bend, stretch = stretch });
        Merges++;
        Merged?.Invoke(merged, level, 0.5f * (centerA + centerB));
    }

    // Affaissement relatif sous la gravité ≈ g / (fréquence² × taille) (joints de Box2D normalisés par la masse) : à fréquence
    // égale, un gumori s fois plus gros est s fois plus dur. Dureté relative voulue = s^(sizeStiffness − 1)
    // → fréquence × s^((sizeStiffness − 2) / 2). Similitude exacte seulement à nombre de capsules égal.
    float SizeStiffness(float scale)
    {
        if (sizeStiffness <= 0f || scale <= 0f) return 1f;
        return Mathf.Pow(scale, 0.5f * (sizeStiffness - 2f));
    }

    const int LoopSamples = 128;

    // Cacahuète : union adoucie des deux cercles (axe des capsules, rayon 'ringRadius', centrés sur les deux gumori), avec un col
    // de demi-largeur 'waist' × rayon entre eux. Échantillonnée par rayons depuis le milieu (bord le plus éloigné de chaque rayon) :
    // la boucle ne peut pas se croiser et tourne dans le sens trigonométrique, comme les anneaux.
    // (Recoudre les capsules réelles se croisait quand un gumori écrasé par sa chute s'était replié : anneau bloqué en 8.)
    void BuildPeanut(Vector2 centerA, Vector2 centerB, float ringRadius)
    {
        Vector2 middle = 0.5f * (centerA + centerB);
        float half = 0.5f * Vector2.Distance(centerA, centerB);
        float neck = Mathf.Sqrt(half * half + waist * waist * ringRadius * ringRadius) - ringRadius;
        float sharpness = neck > 0.01f * ringRadius ? Mathf.Log(2f) / neck : 50f / ringRadius;
        float far = half + 2f * ringRadius;
        float march = 0.02f * ringRadius;

        for (int i = 0; i < LoopSamples; i++)
        {
            float angle = i * 2f * Mathf.PI / LoopSamples;
            var ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float t = far;
            while (t > 0f && Distance(middle + ray * t, centerA, centerB, ringRadius, sharpness) > 0f) t -= march;
            float lo = Mathf.Max(0f, t), hi = t + march;
            for (int k = 0; k < 16; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (Distance(middle + ray * mid, centerA, centerB, ringRadius, sharpness) > 0f) hi = mid;
                else lo = mid;
            }
            _loop[i] = middle + ray * (0.5f * (lo + hi));
        }
        _arc[0] = 0f;
        for (int i = 1; i <= LoopSamples; i++) _arc[i] = _arc[i - 1] + Vector2.Distance(_loop[i - 1], _loop[i % LoopSamples]);
    }

    // Distance signée (approchée) au bord de l'union adoucie des deux cercles : négative dedans.
    static float Distance(Vector2 p, Vector2 centerA, Vector2 centerB, float radius, float sharpness)
    {
        float da = Vector2.Distance(p, centerA) - radius;
        float db = Vector2.Distance(p, centerB) - radius;
        return Mathf.Min(da, db) - Mathf.Log(1f + Mathf.Exp(-sharpness * Mathf.Abs(da - db))) / sharpness;
    }

    // Point de la boucle à l'abscisse curviligne s (m, prise modulo sa longueur).
    Vector2 PointAt(float s, int count)
    {
        float length = _arc[count];
        s -= Mathf.Floor(s / length) * length;
        int i = 0;
        while (i < count - 1 && _arc[i + 1] < s) i++;
        float segment = _arc[i + 1] - _arc[i];
        float t = segment > 0f ? (s - _arc[i]) / segment : 0f;
        return Vector2.Lerp(_loop[i], _loop[(i + 1) % count], t);
    }
}
