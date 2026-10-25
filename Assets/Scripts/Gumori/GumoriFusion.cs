using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Fusion des gumori : deux gumori de même taille qui se touchent deviennent un seul gumori du niveau suivant (taille linéaire).
// Mitose à l'envers : un seul anneau remplace les deux, posé en cacahuète sur eux et tenu dans cette forme par les angles de
// repos de ses joints, ramenés vers le cercle en 'mergeTime' : le col s'ouvre, les bouts rentrent, les capsules s'épaississent.
// Absorption (autre méthode) : l'anneau part de la forme RÉELLE des deux gumori (contour de leur union, col arrondi) et
// garde le mouvement de chaque morceau, puis s'arrondit : pas de saut de forme, rien de neuf qui déborde sur les voisins.
// Contacts entre gumori comptés par événements (sommes entières : indépendant de leur ordre), fusions dans l'ordre de création.
public enum GrowthMode { [InspectorName("Arithmétique (+)")] Arithmetic, [InspectorName("Multiplication (×)")] Geometric }
public enum FusionStyle { [InspectorName("Cacahuète (bulle d'un coup)")] Peanut, [InspectorName("Absorption (forme réelle)")] Absorb }

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
    [Tooltip("Un gumori encore en train de fusionner peut refusionner dès qu'il touche un gumori de sa taille (fusion dès le choc).\n" +
             "Décoché : il attend la fin de sa fusion ; un gumori lancé sur lui rebondit, et ne fusionne qu'à la fin s'ils se\n" +
             "touchent encore (mesuré en Space : 280 à 400 chocs refusés par partie avec l'absorption, qui dure plus longtemps).")]
    public bool mergeWhileFusing = true;
    [Tooltip("Tenue des gros gumori face à la gravité, comparée au gumori lâché :\n" +
             "1 = tous se comportent pareil (même affaissement relatif, juste plus gros) ;\n" +
             "> 1 = plus ils sont gros, plus ils sont durs (2 = comme sans ajustement) ; < 1 = plus ils sont gros, plus ils sont mous ;\n" +
             "0 = aucun ajustement (mêmes joints que le modèle : à raideur égale, un gumori 2 × plus gros s'affaisse 2 × moins).")]
    [Min(0f)] public float sizeStiffness = 1f;

    [Header("Méthode")]
    [Tooltip("Cacahuète : un anneau neuf posé d'un coup sur deux cercles idéaux (plus gros que des gumori écrasés : il peut\n" +
             "déborder sur les voisins), puis ramené au cercle.\n" +
             "Absorption : l'anneau neuf part exactement de la forme réelle des deux gumori (écrasés, en mouvement), garde\n" +
             "leur élan morceau par morceau, et ils se fondent l'un dans l'autre jusqu'au cercle.")]
    public FusionStyle style = FusionStyle.Peanut;

    [Header("Animation (mitose à l'envers)")]
    [Tooltip("Durée (s) pour que les deux gumori n'en fassent plus qu'un : la cacahuète est TENUE par les joints, puis ramenée\n" +
             "vers le cercle en suivant 'mergeCurve' ; ses capsules s'épaississent en même temps.\n" +
             "Pendant ce temps il peut refusionner seulement si 'mergeWhileFusing' est coché.")]
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

    [Header("Absorption (méthode 'Absorption')")]
    [Tooltip("Durée (s) pour que les deux gumori se fondent en un seul rond. Refusion pendant ce temps : voir 'mergeWhileFusing'.")]
    [Min(0.01f)] public float absorbTime = 0.9f;
    [Tooltip("Avancement de l'absorption dans le temps : 0 = forme réelle des deux gumori, 1 = rond. Horizontale = durée\n" +
             "(0 → 1 = 'absorbTime'). Ex. : lente au début = ils restent un instant collés avant de se fondre.")]
    public AnimationCurve absorbCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Raideur en flexion au début de l'absorption, en fraction de celle du gumori (remonte à 1 avec l'avancement).\n" +
             "Bas = gelée très molle qui ondule et tremble en se fondant ; 1 = la forme suit exactement la courbe.")]
    [Range(0.01f, 1f)] public float absorbStiffness = 1f;
    [Tooltip("Arrondi du creux entre les deux gumori au départ (en fraction de leur rayon). 0 = le creux reste pointu (forme\n" +
             "exacte) ; plus grand = le pont de gelée entre eux est déjà plus large.")]
    [Range(0f, 1.5f)] public float absorbNeck = 0.5f;
    [Tooltip("Lissage de la forme visée par les joints au départ (passes de lissage de leurs angles ; les capsules partent quand\n" +
             "même de la forme réelle). La forme réelle a des coins vifs et des zigzags que la gelée molle replie : deux capsules\n" +
             "voisines passent l'une sur l'autre et l'habillage se chevauche. 0 = forme exacte ; plus = coins adoucis dès le départ.")]
    [Range(0, 16)] public int absorbSmoothing = 4;
    [Tooltip("Élan gardé : 1 = chaque morceau du nouvel anneau garde la vitesse de la partie des deux gumori où il naît\n" +
             "(tremblements, écrasement, rotation continuent sans à-coup) ; 0 = toute la gelée part à la vitesse moyenne.")]
    [Range(0f, 1f)] public float inheritMotion = 1f;

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
        public float[] stretches; // absorption : allongement de chaque capsule (sa corde sur la forme réelle), sinon null
        public float[] bend; // angle de repos de chaque joint (rad) qui tient la cacahuète
        public bool absorb; // méthode 'Absorption' (ses propres durée, courbe, raideur)
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
    readonly Vector2[] _oldPosition = new Vector2[64], _oldVelocity = new Vector2[64], _newVelocity = new Vector2[32], _smoothVelocity = new Vector2[32];
    (Vector2 x0, Vector2 x1) _parentVelocity;
    readonly float[] _oldSpin = new float[64], _rays = new float[LoopSamples], _blur = new float[LoopSamples],
        _limit = new float[LoopSamples];
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

    // Durée de la fusion avec la méthode choisie (fenêtres de surveillance, éclat, fin de niveau).
    public float MergeDuration => style == FusionStyle.Absorb ? absorbTime : mergeTime;

    Gumori Template => (_dropper != null ? _dropper : GetComponent<GumoriDropper>()).template;

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _touching.Clear();
        _fusing.Clear();
        _eventsStep = LevelSimulation.Step;
        Merges = 0;
        TurnFixes = 0;
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
            float duration = f.absorb ? absorbTime : mergeTime;
            var curve = f.absorb ? absorbCurve : mergeCurve;
            float u = Mathf.Clamp01(f.time / duration);
            float e = u >= 1f ? 1f : Mathf.Clamp01(curve != null ? curve.Evaluate(u) : u);
            int m = f.bend.Length;
            for (int j = 0; j < m; j++) { _bend[j] = f.bend[j] * (1f - e); _stretch[j] = (f.stretches != null ? f.stretches[j] : f.stretch) * (1f - e); }
            f.blob.SetShape(_stretch, _bend);
            f.blob.SetBendStiffness(u >= 1f ? 1f : Mathf.Pow(f.absorb ? absorbStiffness : startStiffness, 1f - e));
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
            if (!mergeWhileFusing && (IsFusing(a) || IsFusing(b))) continue;
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
            Vector2 pa = ca.position, pb = cb.position, va = ca.linearVelocity, vb = cb.linearVelocity;
            centerA += pa;
            centerB += pb;
            velocity += va + vb;
            _oldPosition[i] = pa; _oldPosition[n + i] = pb;
            _oldVelocity[i] = va; _oldVelocity[n + i] = vb;
            _oldSpin[i] = ca.angularVelocity; _oldSpin[n + i] = cb.angularVelocity;
        }
        centerA /= n;
        centerB /= n;
        velocity /= 2 * n;

        bool absorb = style == FusionStyle.Absorb && BuildUnion(a, b, n, 0.5f * (centerA + centerB));
        if (!absorb) BuildPeanut(centerA, centerB, a.RestRingRadius);
        const int count = LoopSamples;
        float length = _arc[count];

        float radius0 = a.CapsuleThickness;
        int level = Level(a) + 1;
        _dropper.Remove(a);
        _dropper.Remove(b);

        var merged = _dropper.SpawnLevel(0.5f * (centerA + centerB), level);
        int m = merged.CapsuleCount;
        float step = length / m;
        if (absorb) ParentVelocities(n);
        float restSegment = 2f * Mathf.PI * merged.RestRingRadius / m;
        float[] chords = absorb ? new float[m] : null;
        for (int k = 0; k < m; k++)
        {
            var body = merged.Capsule(k);
            if (absorb)
            {
                // Capsule posée sur la corde entre deux points du contour, à sa longueur : ses bouts tombent exactement sur ceux
                // de ses voisines (posées au milieu de l'arc, sur un contour irrégulier, les joints naissaient faussés et se
                // refermaient d'un coup : anneau plié).
                Vector2 from = PointAt(k * step, count), to = PointAt((k + 1) * step, count), chord = to - from;
                _angles[k] = Mathf.Atan2(chord.y, chord.x) - 0.5f * Mathf.PI;
                body.transform = new PhysicsTransform(0.5f * (from + to), PhysicsRotate.FromRadians(_angles[k]));
                chords[k] = chord.magnitude - restSegment;
                int near = Nearest(0.5f * (from + to), 2 * n);
                _newVelocity[k] = _oldVelocity[near] - (near < n ? _parentVelocity.x0 : _parentVelocity.x1);
                continue;
            }
            float s = (k + 0.5f) * step;
            Vector2 tangent = PointAt(s + 0.5f * step, count) - PointAt(s - 0.5f * step, count);
            _angles[k] = Mathf.Atan2(tangent.y, tangent.x) - 0.5f * Mathf.PI;
            body.transform = new PhysicsTransform(PointAt(s, count), PhysicsRotate.FromRadians(_angles[k]));
            {
                body.linearVelocity = velocity;
                body.angularVelocity = 0f;
            }
        }
        if (absorb) InheritMotion(merged, m, n, velocity);

        // La cacahuète est tenue telle quelle : capsules allongées à son périmètre (sinon, si 'growth' < 2, les joints la contractent
        // d'un coup), et angles de repos des joints = ses angles. Les joints précontraints visent (angle relatif) = angle de repos,
        // l'anneau fermé en ajoute 2π/m à tous : angle de repos = angle de la cacahuète − 2π/m. Ramenés à 0 en 'mergeTime'.
        float stretch = step - 2f * Mathf.PI * merged.RestRingRadius / m;
        // Absorption : la forme réelle de deux gumori écrasés l'un contre l'autre peut être PLUS COURTE que le nouvel anneau au
        // repos ; capsules raccourcies d'autant (bloquées à 0, elles naissaient comprimées et l'anneau flambait en zigzag).
        if (!absorb) stretch = Mathf.Max(0f, stretch);
        var bend = new float[m];
        for (int j = 0; j < m; j++)
        {
            float turn = Mathf.DeltaAngle(_angles[j == 0 ? m - 1 : j - 1] * Mathf.Rad2Deg, _angles[j] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            bend[j] = turn - 2f * Mathf.PI / m;
            _stretch[j] = absorb ? chords[j] : stretch;
        }
        if (absorb) SmoothTurns(bend, m, absorbSmoothing);
        if (absorb) CloseTurns(bend, m);
        merged.SetCapsuleRadius(radius0);
        merged.SetShape(_stretch, bend);
        merged.SetBendStiffness(absorb ? absorbStiffness : startStiffness);
        _fusing.Add(new Fusing { absorb = absorb, blob = merged, radius0 = radius0, radius1 = merged.CapsuleThickness, written = radius0, bend = bend, stretch = stretch, stretches = chords });
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
    static readonly Vector2[] RayTable = BuildRayTable();

    static Vector2[] BuildRayTable()
    {
        var rays = new Vector2[LoopSamples];
        for (int i = 0; i < LoopSamples; i++)
        {
            float angle = i * 2f * Mathf.PI / LoopSamples;
            rays[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return rays;
    }

    static Vector2 Ray(int i) => RayTable[i];

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

    // Contour réel de l'union des deux gumori (polygones de l'axe de leurs capsules, rangés dans _oldPosition) : pour chaque rayon
    // depuis le milieu, le bord le plus éloigné (boucle sans croisement, sens trigonométrique, comme la cacahuète), mais jamais
    // au-delà du premier autre gumori croisé ('NeighborLimits') : un voisin logé dans un creux (gumori très mous qui s'enveloppent)
    // reste dehors. Rayons sans bord (milieu hors des deux) bouchés entre leurs voisins ; creux du col arrondi : rayon gardé au moins
    // égal à sa moyenne locale (pondérée en triangle sur ≈ 'absorbNeck' × rayon de part et d'autre), dans la place libre seulement.
    // Faux si trop peu de rayons touchent (la cacahuète sert alors).
    bool BuildUnion(Gumori a, Gumori b, int n, Vector2 middle)
    {
        float ringRadius = a.RestRingRadius, thickness = a.CapsuleThickness;
        NeighborLimits(a, b, n, middle, thickness);
        int hits = 0;
        float mean = 0f;
        for (int i = 0; i < LoopSamples; i++)
        {
            var ray = Ray(i);
            float best = -1f, limit = _limit[i] + 0.25f * thickness;
            bool crossed = false;
            for (int ring = 0; ring < 2; ring++)
                for (int k = 0; k < n; k++)
                {
                    Vector2 p = _oldPosition[ring * n + k], q = _oldPosition[ring * n + (k + 1) % n];
                    Vector2 edge = q - p, w = p - middle;
                    float denom = ray.x * edge.y - ray.y * edge.x;
                    if (Mathf.Abs(denom) < 1e-9f) continue;
                    float t = (w.x * edge.y - w.y * edge.x) / denom;
                    float u = (w.x * ray.y - w.y * ray.x) / denom;
                    if (t <= 0f || u < 0f || u > 1f) continue;
                    crossed = true;
                    if (t > best && t <= limit) best = t;
                }
            if (best <= 0f && crossed) best = Mathf.Max(0.1f * ringRadius, _limit[i]);
            _rays[i] = best;
            if (best > 0f) { hits++; mean += best; }
        }
        if (hits < LoopSamples / 2) return false;
        mean /= hits;
        if (hits < LoopSamples)
        {
            for (int i = 0; i < LoopSamples; i++)
            {
                if (_rays[i] > 0f) continue;
                int back = 1, ahead = 1;
                while (_rays[(i - back + LoopSamples) % LoopSamples] <= 0f) back++;
                while (_rays[(i + ahead) % LoopSamples] <= 0f) ahead++;
                float filled = Mathf.Lerp(_rays[(i - back + LoopSamples) % LoopSamples], _rays[(i + ahead) % LoopSamples], back / (float)(back + ahead));
                _blur[i] = Mathf.Max(0.1f * ringRadius, Mathf.Min(filled, _limit[i]));
            }
            for (int i = 0; i < LoopSamples; i++) if (_rays[i] <= 0f) _rays[i] = _blur[i];
        }

        // Coins vifs et zigzags adoucis (passes 1-2-1 sur les rayons, sans jamais dépasser la limite d'un voisin).
        for (int p = 0; p < 2 * absorbSmoothing; p++)
        {
            for (int i = 0; i < LoopSamples; i++) _blur[i] = _rays[i];
            for (int i = 0; i < LoopSamples; i++)
            {
                float r = 0.25f * _blur[(i + LoopSamples - 1) % LoopSamples] + 0.5f * _blur[i] + 0.25f * _blur[(i + 1) % LoopSamples];
                _rays[i] = Mathf.Max(0.1f * ringRadius, Mathf.Min(r, Mathf.Max(_blur[i], _limit[i])));
            }
        }

        int width = Mathf.Clamp(Mathf.RoundToInt(absorbNeck * ringRadius / mean * LoopSamples / (2f * Mathf.PI)), 0, LoopSamples / 4);
        for (int i = 0; i < LoopSamples; i++)
        {
            float sum = _rays[i] * (width + 1), weight = width + 1;
            for (int k = 1; k <= width; k++)
            {
                float wk = width + 1 - k;
                sum += wk * (_rays[(i + k) % LoopSamples] + _rays[(i - k + LoopSamples) % LoopSamples]);
                weight += 2f * wk;
            }
            _blur[i] = Mathf.Max(_rays[i], Mathf.Min(sum / weight, _limit[i]));
        }
        for (int i = 0; i < LoopSamples; i++) _loop[i] = middle + Ray(i) * _blur[i];
        _arc[0] = 0f;
        for (int i = 1; i <= LoopSamples; i++) _arc[i] = _arc[i - 1] + Vector2.Distance(_loop[i - 1], _loop[i % LoopSamples]);
        return true;
    }

    // Vitesse moyenne de chacun des deux anciens gumori.
    void ParentVelocities(int n)
    {
        Vector2 va = Vector2.zero, vb = Vector2.zero;
        for (int i = 0; i < n; i++) { va += _oldVelocity[i]; vb += _oldVelocity[n + i]; }
        _parentVelocity = (va / n, vb / n);
    }

    // Élan gardé : chaque capsule prend le mouvement PROPRE (vitesse moins celle de son gumori : tremblement, écrasement,
    // rotation) de la capsule d'origine la plus proche, lissé le long de l'anneau, ajouté à la vitesse moyenne. La vitesse
    // d'approche entre les deux (choc d'un gumori lancé) est absorbée par la fusion : gardée, la moitié rapide de l'anneau
    // passait à travers l'autre (ses capsules ne se heurtent pas entre elles) et le froissait en plis.
    void InheritMotion(Gumori merged, int m, int n, Vector2 mean)
    {
        for (int p = 0; p < Mathf.Max(2, absorbSmoothing); p++)
        {
            for (int k = 0; k < m; k++) _smoothVelocity[k] = _newVelocity[k];
            for (int k = 0; k < m; k++)
                _newVelocity[k] = 0.25f * _smoothVelocity[(k + m - 1) % m] + 0.5f * _smoothVelocity[k] + 0.25f * _smoothVelocity[(k + 1) % m];
        }
        for (int k = 0; k < m; k++)
        {
            var body = merged.Capsule(k);
            body.linearVelocity = mean + inheritMotion * _newVelocity[k];
            body.angularVelocity = 0f;
        }
    }

    // Angles de repos lissés (passes 1-2-1, anneau fermé : la somme, donc le tour complet, est gardée).
    void SmoothTurns(float[] bend, int m, int passes)
    {
        for (int p = 0; p < passes; p++)
        {
            for (int j = 0; j < m; j++) _angles[j] = bend[j];
            for (int j = 0; j < m; j++) bend[j] = 0.25f * _angles[(j + m - 1) % m] + 0.5f * _angles[j] + 0.25f * _angles[(j + 1) % m];
        }
    }

    // Un anneau fermé tourne d'un tour exactement : la somme des angles de repos doit valoir 0. Un coude presque en demi-tour
    // (forme réelle très tordue) pouvait être compté dans le mauvais sens (± 2π) : la forme visée devenait un 8, et l'anneau
    // s'y enroulait en s'arrondissant (gelée qui se chevauche). Le coude le plus fort est remis dans l'autre sens, puis le
    // petit reste réparti sur tous.
    void CloseTurns(float[] bend, int m)
    {
        float sum = 0f;
        for (int j = 0; j < m; j++) sum += bend[j];
        while (Mathf.Abs(sum) > Mathf.PI)
        {
            int worst = 0;
            for (int j = 1; j < m; j++)
                if (sum > 0f ? bend[j] > bend[worst] : bend[j] < bend[worst]) worst = j;
            float fix = sum > 0f ? -2f * Mathf.PI : 2f * Mathf.PI;
            bend[worst] += fix;
            sum += fix;
            TurnFixes++;
        }
        for (int j = 0; j < m; j++) bend[j] -= sum / m;
    }

    // Coudes remis dans le bon sens par CloseTurns depuis le départ (contrôle).
    public int TurnFixes { get; private set; }

    // Pour chaque rayon, distance (depuis le milieu) où l'axe du nouvel anneau toucherait la première capsule d'un autre gumori
    // (somme des épaisseurs) ; l'infini si rien. Tri grossier par la distance des centres.
    void NeighborLimits(Gumori a, Gumori b, int n, Vector2 middle, float thickness)
    {
        float reach = 0f;
        for (int i = 0; i < 2 * n; i++) reach = Mathf.Max(reach, (_oldPosition[i] - middle).magnitude);
        reach += 2f * thickness;
        for (int i = 0; i < LoopSamples; i++) _limit[i] = float.MaxValue;
        var inPlay = _dropper.InPlay;
        for (int g = 0; g < inPlay.Count; g++)
        {
            var other = inPlay[g];
            if (other == a || other == b) continue;
            float r = 0.5f * other.diameter + other.CapsuleThickness;
            if ((other.Center - middle).sqrMagnitude > (reach + r) * (reach + r)) continue;
            float clearance = thickness + other.CapsuleThickness;
            for (int k = 0; k < other.CapsuleCount; k++)
            {
                Vector2 c = other.Capsule(k).position - middle;
                for (int i = 0; i < LoopSamples; i++)
                {
                    Vector2 ray = Ray(i);
                    float along = c.x * ray.x + c.y * ray.y;
                    if (along <= 0f) continue;
                    float across = c.x * ray.y - c.y * ray.x;
                    if (across * across >= clearance * clearance) continue;
                    float stop = along - Mathf.Sqrt(clearance * clearance - across * across);
                    if (stop < _limit[i]) _limit[i] = stop;
                }
            }
        }
    }


    // Capsule des deux anciens gumori la plus proche de p (la première en cas d'égalité : ordre fixe).
    int Nearest(Vector2 p, int count)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            float d = (_oldPosition[i] - p).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
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
