using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lâcher des gumori d'en haut dans un réservoir. Flèches = déplacer le point de lâcher, Espace = lâcher (tenu = en rafale),
// R = tout vider (recommencer). Chaque gumori est une copie du gumori modèle (objet désactivé de la scène), à la taille de son
// niveau (GumoriFusion), tiré dans une suite aléatoire à graine fixe ; on voit celui à lâcher et le suivant. Possède la liste
// des gumori en jeu (InPlay, ordre de création), lue par tous les autres scripts.
// Entrées par LevelInput et créations dans LevelTick (ordre fixe) : déterministe. Affiche le nombre de gumori et le coût du pas.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/GumoriDropper")]
public class GumoriDropper : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Gumori")]
    [Tooltip("Gumori modèle : un objet DÉSACTIVÉ de la scène portant un Gumori. Chaque gumori lâché en est une copie (mêmes réglages).")]
    public Gumori template;
    [Tooltip("Nombre max de gumori en jeu (0 = sans limite).")]
    [Min(0)] public int maxBlobs = 0;

    [Header("Suite des slimes")]
    [Tooltip("Graine de la suite aléatoire des gumori à lâcher : même graine = même suite, à chaque partie et sur tout appareil\n" +
             "(défi du jour, replays). Changer la graine = autre suite.")]
    public int seed = 1;
    [Tooltip("Plus gros niveau qu'on peut recevoir à lâcher : 0 = toujours le premier gumori, 3 = du 1er au 4e.\n" +
             "Tailles, couleurs et raideurs viennent de GumoriFusion (taille du niveau n = modèle × (1 + (growth − 1) × n)).")]
    [Min(0)] public int maxDropLevel = 3;
    [Tooltip("Préférence pour les petits : 0 = tous les niveaux aussi fréquents ; 0,5 = chaque niveau 2 × plus rare que le\n" +
             "précédent ; 1 = toujours le premier.")]
    [Range(0f, 1f)] public float smallBias = 0f;

    [Header("Lâcher")]
    [Tooltip("Courbe du réservoir : ses parois donnent la zone de lâcher (face intérieure = axe de la courbe − ½ épaisseur, sur\n" +
             "la moitié haute, côté le plus proche de l'axe de cet objet). Suit tout seul la largeur et l'épaisseur de la box.\n" +
             "Vide = 'halfWidth' à la main.")]
    public CurvePath2D reservoir;
    [Tooltip("Sans courbe de réservoir seulement : demi-largeur intérieure, en m, de l'axe de l'objet à la face intérieure de la\n" +
             "paroi. Le point de lâcher s'arrête à cette distance − rayon du gumori − 'wallGap'.")]
    [Min(0f)] public float halfWidth = 3.8f;
    [Tooltip("Espace laissé entre le bord du gumori et la paroi quand on lâche tout au bord, en m.")]
    [Min(0f)] public float wallGap = 0.15f;
    [Tooltip("Vitesse du point de lâcher, en km/h, flèche tenue.")]
    [SpeedKmh] [Min(0f)] public float moveSpeed = 5f;
    [Tooltip("Délai minimum (s) entre deux lâchers, appuis répétés comme touche tenue (rafale). Un appui pendant ce délai\n" +
             "est ignoré.")]
    [Min(0.01f)] public float dropInterval = 0.3f;
    public GameKey dropKey = GameKey.Space;
    public GameKey leftKey = GameKey.LeftArrow;
    public GameKey rightKey = GameKey.RightArrow;

    [Header("Affichage")]
    [Tooltip("Coin haut gauche : nombre de gumori, de corps physiques, coût de la physique par pas et images par seconde.")]
    public bool showStats = true;
    [Tooltip("Dessin de debug : gumori à lâcher et suivant (cercles), zone de lâcher et ligne de visée.\n" +
             "À décocher quand GumoriSkin dessine les gumori.")]
    public bool debugDraw = true;
    [Tooltip("Dessine les formes physiques des gumori (capsules) par-dessus l'habillage. Touche H en jeu pour basculer.")]
    public bool showHitboxes = false;
    public Color hitboxColor = new Color(1f, 1f, 1f, 0.9f);
    public Color previewColor = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("Sans caméra orthographique seulement : position de l'aperçu du gumori suivant, en m par rapport à l'objet.")]
    public Vector2 nextPreviewOffset = new Vector2(6f, -2f);
    [Tooltip("Position de l'aperçu du gumori suivant À L'ÉCRAN (0 = gauche / bas, 1 = droite / haut) : reste visible quel que\n" +
             "soit le format du téléphone. Purement visuel.")]
    public Vector2 nextPreviewViewport = new Vector2(0.84f, 0.8f);
    [Tooltip("Diamètre max de l'aperçu du gumori suivant, en m (les plus gros sont dessinés réduits à cette taille).")]
    [Min(0.1f)] public float nextPreviewDiameter = 1f;

    readonly List<Gumori> _blobs = new List<Gumori>();
    GumoriFusion _fusion;
    // Mode Space : les gumori arrivent par la réserve (GumoriLauncher), pas par le lâcher : ni aperçu, ni « Suivant », ni gizmo.
    bool _launches;
    uint _rng;
    int _current, _next; // niveaux du prochain gumori lâché et du suivant

    // Niveau du prochain gumori lâché, et de celui d'après.
    public int CurrentLevel => _current;
    public int NextLevel => _next;
    float _offset;     // position du point de lâcher par rapport à l'objet, en m
    float _cooldown;

    // Mesures (affichage) : coût de Box2D par pas, moyenne glissante.
    float _stepMs, _fps;

    int _spawned;
    GUIStyle _centered;

    // Gumori lâché et son niveau (pendant LevelTick : les effets s'en servent, sans écrire dans la simulation).
    public event System.Action<Gumori, int> Dropped;

    // Nombre de lâchers depuis le départ ; limite donnée par GumoriGoal (0 = sans limite).
    public int Drops { get; private set; }
    [System.NonSerialized] public int dropLimit;
    public int DropsLeft => dropLimit > 0 ? Mathf.Max(0, dropLimit - Drops) : int.MaxValue;

    public int Count => _blobs.Count;
    // Gumori en jeu, dans l'ordre de création (Gumori.Index = rang dans cette liste).
    public IReadOnlyList<Gumori> InPlay => _blobs;
    public static bool IsInPlay(Gumori gumori) => gumori != null && gumori.Index >= 0;

    // Les systèmes du jeu sont rangés sous l'objet de ce composant (Fusion, Règles, Réserve, Corps) : on les retrouve par lui.
    public T Part<T>() where T : Component => GetComponentInChildren<T>(true);
    public static T Find<T>(Component from) where T : Component
    {
        var dropper = from.GetComponentInParent<GumoriDropper>(true);
        return dropper != null ? dropper.Part<T>() : null;
    }

    void OnEnable()
    {
        _fusion = Part<GumoriFusion>();
        _launches = Part<GumoriLauncher>() != null;
        _wallHalf = MeasureWalls();
        _rng = StartRng();
        _current = RollLevel();
        _next = RollLevel();
        _spawned = 0;
        Drops = 0;
        _offset = 0f;
        _cooldown = 0f;
        _stepMs = 0f;
    }

    // Recommencer (R) : chaque gumori détruit sa physique tout de suite (avant la remise à zéro du monde), puis son objet.
    void OnDisable()
    {
        for (int i = 0; i < _blobs.Count; i++)
        {
            if (_blobs[i] == null) continue;
            _blobs[i].Index = -1;
            _blobs[i].enabled = false; // son OnDisable détruit sa physique maintenant
            if (Application.isPlaying) Destroy(_blobs[i].gameObject);
            else DestroyImmediate(_blobs[i].gameObject);
        }
        _blobs.Clear();
    }

    public void LevelTick()
    {
        float dt = Time.fixedDeltaTime;
        int move = (LevelInput.IsDown(rightKey) ? 1 : 0) - (LevelInput.IsDown(leftKey) ? 1 : 0);
        float limit = HalfWidth(_current);
        _offset = Mathf.Clamp(_offset + move * moveSpeed * dt, -limit, limit);

        bool down = LevelInput.IsDown(dropKey);
        _cooldown -= dt;
        if (down && _cooldown <= 0f && DropsLeft > 0) Drop();

        float ms = PhysicsWorld.defaultWorld.profile.simulationStep;
        _stepMs = _stepMs == 0f ? ms : Mathf.Lerp(_stepMs, ms, 0.05f);
    }

    void Drop()
    {
        _cooldown = dropInterval;
        if (template == null || (maxBlobs > 0 && _blobs.Count >= maxBlobs)) return;
        var blob = SpawnLevel(DropPoint, _current);
        Drops++;
        Dropped?.Invoke(blob, _current);
        _current = _next;
        _next = RollLevel();
        float limit = HalfWidth(_current);
        _offset = Mathf.Clamp(_offset, -limit, limit);
    }

    // Suite aléatoire déterministe : xorshift32 (entiers, même résultat partout ; pas UnityEngine.Random, partagé et non garanti).
    // Tirée seulement au départ et à chaque lâcher, dans LevelTick : rejouée à l'identique.
    int RollLevel() => Roll(ref _rng, MaxRoll, smallBias);

    int MaxRoll => _fusion != null ? Mathf.Min(maxDropLevel, _fusion.MaxLevel) : 0;

    uint StartRng() => StartRng(seed);

    static uint StartRng(int seed)
    {
        uint rng = (uint)seed * 2654435761u ^ 0x9E3779B9u;
        return rng == 0u ? 1u : rng;
    }

    // Niveaux des 'count' premiers gumori lâchés (même graine, même tirage qu'en jeu), sans toucher à la partie en cours.
    public void PredictSequence(int count, System.Collections.Generic.List<int> levels)
    {
        if (_fusion == null) _fusion = Part<GumoriFusion>();
        PredictSequence(seed, MaxRoll, smallBias, count, levels);
    }

    // Même tirage sans réservoir (inspecteur de la campagne) : 'maxLevel' = min(maxDropLevel, plus gros niveau de GumoriFusion).
    public static void PredictSequence(int seed, int maxLevel, float smallBias, int count, System.Collections.Generic.List<int> levels)
    {
        levels.Clear();
        uint rng = StartRng(seed);
        for (int i = 0; i < count; i++) levels.Add(Roll(ref rng, maxLevel, smallBias));
    }

    static int Roll(ref uint rng, int max, float smallBias)
    {
        rng ^= rng << 13;
        rng ^= rng >> 17;
        rng ^= rng << 5;
        if (max <= 0) return 0;
        float keep = 1f - smallBias, total = 0f, weight = 1f;
        for (int level = 0; level <= max; level++) { total += weight; weight *= keep; }
        float pick = (rng >> 8) * (1f / 16777216f) * total;
        weight = 1f;
        for (int level = 0; level < max; level++)
        {
            if (pick < weight) return level;
            pick -= weight;
            weight *= keep;
        }
        return max;
    }

    // Zone de lâcher pour ce niveau : le bord du gumori s'arrête à 'wallGap' de la paroi.
    float HalfWidth(int level) => Mathf.Max(0f, WallHalfWidth - 0.5f * LevelDiameter(level) - wallGap);

    // Distance de l'axe du lâcher à la face intérieure des parois, en m (mesurée au départ sur la courbe du réservoir).
    public float WallHalfWidth => reservoir != null ? _wallHalf : halfWidth;
    float _wallHalf;

    public float MeasureWalls()
    {
        if (reservoir == null) return halfWidth;
        if (reservoir.center.Count == 0) reservoir.Rebuild();
        var pts = reservoir.center;
        var tr = reservoir.transform;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < pts.Count; i++) { float y = tr.TransformPoint(pts[i]).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        float mid = 0.5f * (minY + maxY), axis = transform.position.x, left = float.MaxValue, right = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 w = tr.TransformPoint(pts[i]);
            if (w.y < mid) continue;
            float d = w.x - axis;
            if (d < 0f) left = Mathf.Min(left, -d); else right = Mathf.Min(right, d);
        }
        float wall = Mathf.Min(left, right);
        return wall < float.MaxValue ? Mathf.Max(0f, wall - 0.5f * reservoir.thickness) : halfWidth;
    }

    float LevelDiameter(int level) => _fusion != null ? _fusion.LevelDiameter(level) : template.diameter;
    Color LevelColor(int level) => _fusion != null ? _fusion.LevelColor(level) : template.debugColor;

    // Gumori d'un niveau (taille, capsules, couleur et raideur définis par GumoriFusion ; sans GumoriFusion : le modèle).
    public Gumori SpawnLevel(Vector2 position, int level)
    {
        if (_fusion == null) return Spawn(position, template.diameter, template.sides, template.debugColor);
        return Spawn(position, _fusion.LevelDiameter(level), _fusion.LevelSides(level), _fusion.LevelColor(level), _fusion.LevelStiffness(level));
    }

    // Copie du gumori modèle avec une autre taille / couleur, ajoutée en fin de liste (ordre de création = ordre des pas).
    // 'stiffness' multiplie les fréquences des joints du modèle (flexion et étirement).
    public Gumori Spawn(Vector2 position, float diameter, int sides, Color color, float stiffness = 1f)
    {
        var go = Instantiate(template.gameObject, position, Quaternion.identity, transform);
        go.name = "Gumori " + (++_spawned);
        var blob = go.GetComponent<Gumori>();
        blob.diameter = diameter;
        blob.sides = sides;
        blob.debugColor = color;
        blob.jointFrequency *= stiffness;
        blob.jointLinearFrequency *= stiffness;
        go.SetActive(true); // son OnEnable crée sa physique à cet endroit
        blob.Index = _blobs.Count;
        _blobs.Add(blob);
        return blob;
    }

    // Retire tous les gumori (fin de niveau), du dernier créé au premier.
    public void RemoveAll()
    {
        for (int i = _blobs.Count - 1; i >= 0; i--) Remove(_blobs[i]);
    }

    // Retire un gumori : sa physique est détruite tout de suite, son objet en fin d'image.
    public void Remove(Gumori blob)
    {
        if (!IsInPlay(blob)) return;
        _blobs.RemoveAt(blob.Index);
        for (int i = blob.Index; i < _blobs.Count; i++) _blobs[i].Index = i;
        blob.Index = -1;
        blob.enabled = false;
        Destroy(blob.gameObject);
    }

    public Vector2 DropPoint => (Vector2)transform.position + new Vector2(_offset, 0f);

    void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) _fps = _fps == 0f ? 1f / dt : Mathf.Lerp(_fps, 1f / dt, 0.05f);
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
        if (kb != null && kb.hKey.wasPressedThisFrame) showHitboxes = !showHitboxes;
#else
        if (Input.GetKeyDown(KeyCode.R)) LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
        if (Input.GetKeyDown(KeyCode.H)) showHitboxes = !showHitboxes;
#endif
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || template == null) return;
        var world = PhysicsWorld.defaultWorld;
        if (showHitboxes) DrawHitboxes(world);
        if (!debugDraw || _launches || DropsLeft == 0) return;
        DrawSlime(world, DropPoint, _current);
        DrawSlime(world, NextPreviewPoint, _next, NextPreviewRadius);
        float limit = HalfWidth(_current);
        Vector2 a = (Vector2)transform.position + new Vector2(-limit, 0f), b = (Vector2)transform.position + new Vector2(limit, 0f);
        world.DrawLine(a, b, previewColor, 0f);
        world.DrawLine(DropPoint, DropPoint + Vector2.down * 30f, new Color(1f, 1f, 1f, 0.12f), 0f);
    }

    void DrawHitboxes(PhysicsWorld world)
    {
        for (int i = 0; i < _blobs.Count; i++)
        {
            var blob = _blobs[i];
            if (blob == null) continue;
            for (int c = 0; c < blob.CapsuleCount; c++)
            {
                var shape = blob.CapsuleShapeAt(c);
                if (shape.isValid) world.DrawGeometry(shape.capsuleGeometry, blob.Capsule(c).transform, hitboxColor, 0f, PhysicsWorld.DrawFillOptions.Outline);
            }
        }
    }

    public Vector2 NextPreviewPoint
    {
        get
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return (Vector2)transform.position + nextPreviewOffset;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            return c + new Vector2((2f * nextPreviewViewport.x - 1f) * w, (2f * nextPreviewViewport.y - 1f) * h);
        }
    }

    public float NextPreviewRadius => 0.5f * Mathf.Min(LevelDiameter(_next), nextPreviewDiameter);

    void DrawSlime(PhysicsWorld world, Vector2 center, int level, float r = -1f)
    {
        var color = LevelColor(level);
        if (r < 0f) r = 0.5f * LevelDiameter(level);
        world.DrawCircle(center, r, new Color(color.r, color.g, color.b, 0.35f), 0f, PhysicsWorld.DrawFillOptions.Interior);
        world.DrawCircle(center, r, color, 0f, PhysicsWorld.DrawFillOptions.Outline);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        var cam = Camera.main;
        if (cam != null && !_launches && DropsLeft > 1)
        {
            Vector2 p = NextPreviewPoint - new Vector2(0f, NextPreviewRadius + 0.2f);
            Vector3 s = cam.WorldToScreenPoint(p);
            GUI.Label(new Rect(s.x - 50f, Screen.height - s.y, 100f, 22f), "Suivant", _centered ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
        if (!showStats) return;
        int bodies = 0;
        for (int i = 0; i < _blobs.Count; i++) bodies += _blobs[i] != null ? _blobs[i].CapsuleCount : 0;
        GUI.Label(new Rect(10, 10, 600, 90),
            $"Gumori : {_blobs.Count}   corps : {bodies}\n" +
            $"Physique : {_stepMs:F2} ms par pas = {_stepMs * 0.1f / Time.fixedDeltaTime:F0} % du temps réel ({1f / Time.fixedDeltaTime:F0} pas/s ; au-delà de 100 %, le jeu ralentit)\n" +
            $"Images/s : {_fps:F0}\n" +
            "Flèches = viser, Espace = lâcher (tenu = rafale), R = vider, H = formes physiques");
    }

    void OnDrawGizmos()
    {
        if (template == null) return;
        if (Part<GumoriLauncher>() != null) return;
        if (_fusion == null) _fusion = Part<GumoriFusion>();
        Gizmos.color = previewColor;
        Vector3 p = transform.position;
        float halfWidth = Application.isPlaying ? WallHalfWidth : MeasureWalls();
        Gizmos.DrawLine(p + Vector3.left * halfWidth, p + Vector3.right * halfWidth);
        Gizmos.DrawLine(p + new Vector3(-halfWidth, -1f), p + new Vector3(-halfWidth, 1f));
        Gizmos.DrawLine(p + new Vector3(halfWidth, -1f), p + new Vector3(halfWidth, 1f));
        // Tailles possibles au lâcher (niveaux 0 à maxDropLevel), concentriques, à leur couleur.
        int max = _fusion != null ? Mathf.Min(maxDropLevel, _fusion.MaxLevel) : 0;
        for (int level = 0; level <= max; level++)
        {
            Gizmos.color = LevelColor(level);
            DrawGizmoCircle(p, 0.5f * LevelDiameter(level));
        }
    }

    static void DrawGizmoCircle(Vector3 center, float r)
    {
        const int N = 32;
        Vector3 prev = center + new Vector3(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float t = i * Mathf.PI * 2f / N;
            Vector3 cur = center + new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
