using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lâcher des slimes d'en haut dans un réservoir. Flèches = déplacer le point de lâcher, Espace = lâcher (tenu = en rafale),
// R = tout vider (recommencer). Chaque slime est une copie du blob modèle (objet désactivé de la scène), à la taille de son
// niveau (BlobFusion), tiré dans une suite aléatoire à graine fixe ; on voit celui à lâcher et le suivant.
// Entrées par LevelInput et créations dans LevelTick (ordre fixe) : déterministe. Affiche le nombre de blobs et le coût du pas.
[ExecuteAlways]
[DisallowMultipleComponent]
public class BlobDropper : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Blob")]
    [Tooltip("Blob modèle : un objet DÉSACTIVÉ de la scène portant un BLOB. Chaque blob lâché en est une copie (mêmes réglages).")]
    public BLOB template;
    [Tooltip("Nombre max de blobs (0 = sans limite).")]
    [Min(0)] public int maxBlobs = 0;

    [Header("Suite des slimes")]
    [Tooltip("Graine de la suite aléatoire des slimes à lâcher : même graine = même suite, à chaque partie et sur tout appareil\n" +
             "(défi du jour, replays). Changer la graine = autre suite.")]
    public int seed = 1;
    [Tooltip("Plus gros niveau qu'on peut recevoir à lâcher : 0 = toujours le premier slime, 3 = du 1er au 4e.\n" +
             "Tailles, couleurs et raideurs viennent de BlobFusion (taille du niveau n = modèle × (1 + (growth − 1) × n)).")]
    [Min(0)] public int maxDropLevel = 3;
    [Tooltip("Préférence pour les petits : 0 = tous les niveaux aussi fréquents ; 0,5 = chaque niveau 2 × plus rare que le\n" +
             "précédent ; 1 = toujours le premier.")]
    [Range(0f, 1f)] public float smallBias = 0f;

    [Header("Lâcher")]
    [Tooltip("Demi-largeur intérieure du réservoir à la hauteur du lâcher, en m : de l'axe de l'objet à la face intérieure de la\n" +
             "paroi (verre dessiné compris). Le point de lâcher s'arrête à cette distance − rayon du slime − 'wallGap' : un slime\n" +
             "lâché au bord longe la paroi sans la toucher, quelle que soit sa taille.")]
    [Min(0f)] public float halfWidth = 3.8f;
    [Tooltip("Espace laissé entre le bord du slime et la paroi quand on lâche tout au bord, en m.")]
    [Min(0f)] public float wallGap = 0.15f;
    [Tooltip("Vitesse du point de lâcher, en km/h, flèche tenue.")]
    [SpeedKmh] [Min(0f)] public float moveSpeed = 5f;
    [Tooltip("Délai minimum (s) entre deux lâchers, appuis répétés comme touche tenue (rafale). Un appui pendant ce délai\n" +
             "est ignoré.")]
    [Min(0.01f)] public float dropInterval = 0.3f;
    public FlipperKey dropKey = FlipperKey.Space;
    public FlipperKey leftKey = FlipperKey.LeftArrow;
    public FlipperKey rightKey = FlipperKey.RightArrow;

    [Header("Affichage")]
    [Tooltip("Coin haut gauche : nombre de blobs, de corps physiques, coût de la physique par pas et images par seconde.")]
    public bool showStats = true;
    [Tooltip("Dessin de debug : slime à lâcher et suivant (cercles), zone de lâcher et ligne de visée.\n" +
             "À décocher quand SlimeSkin dessine les slimes.")]
    public bool debugDraw = true;
    [Tooltip("Dessine les formes physiques des slimes (capsules) par-dessus l'habillage. Touche H en jeu pour basculer.")]
    public bool showHitboxes = false;
    public Color hitboxColor = new Color(1f, 1f, 1f, 0.9f);
    public Color previewColor = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("Position de l'aperçu du slime suivant (celui d'après le prochain), en m par rapport à l'objet.")]
    public Vector2 nextPreviewOffset = new Vector2(6f, -2f);

    readonly List<BLOB> _blobs = new List<BLOB>();
    BlobFusion _fusion;
    uint _rng;
    int _current, _next; // niveaux du prochain slime lâché et du suivant

    // Niveau du prochain slime lâché, et de celui d'après.
    public int CurrentLevel => _current;
    public int NextLevel => _next;
    float _offset;     // position du point de lâcher par rapport à l'objet, en m
    float _cooldown;

    // Mesures (affichage) : coût de Box2D par pas, moyenne glissante.
    float _stepMs, _fps;

    int _spawned;
    GUIStyle _centered;

    // Slime lâché et son niveau (pendant LevelTick : les effets s'en servent, sans écrire dans la simulation).
    public event System.Action<BLOB, int> Dropped;

    // Nombre de lâchers depuis le départ ; limite donnée par SlimeGoal (0 = sans limite).
    public int Drops { get; private set; }
    [System.NonSerialized] public int dropLimit;
    public int DropsLeft => dropLimit > 0 ? Mathf.Max(0, dropLimit - Drops) : int.MaxValue;

    public int Count => _blobs.Count;
    // Blobs vivants, dans l'ordre de création.
    public IReadOnlyList<BLOB> Blobs => _blobs;

    void OnEnable()
    {
        _fusion = GetComponent<BlobFusion>();
        _rng = StartRng();
        _current = RollLevel();
        _next = RollLevel();
        _spawned = 0;
        Drops = 0;
        _offset = 0f;
        _cooldown = 0f;
        _stepMs = 0f;
    }

    // Recommencer (R) : chaque blob détruit sa physique tout de suite (avant la remise à zéro du monde), puis son objet.
    void OnDisable()
    {
        for (int i = 0; i < _blobs.Count; i++)
        {
            if (_blobs[i] == null) continue;
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

        // Les blobs créés ici ne sont pas dans la liste de LevelSimulation : leur pas est fait ici, dans l'ordre de création.
        for (int i = 0; i < _blobs.Count; i++) _blobs[i].LevelTick();

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
    int RollLevel() => Roll(ref _rng);

    uint StartRng()
    {
        uint rng = (uint)seed * 2654435761u ^ 0x9E3779B9u;
        return rng == 0u ? 1u : rng;
    }

    // Niveaux des 'count' premiers slimes lâchés (même graine, même tirage qu'en jeu), sans toucher à la partie en cours.
    public void PredictSequence(int count, System.Collections.Generic.List<int> levels)
    {
        if (_fusion == null) _fusion = GetComponent<BlobFusion>();
        levels.Clear();
        uint rng = StartRng();
        for (int i = 0; i < count; i++) levels.Add(Roll(ref rng));
    }

    int Roll(ref uint rng)
    {
        int max = _fusion != null ? Mathf.Min(maxDropLevel, _fusion.MaxLevel) : 0;
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

    // Zone de lâcher pour ce niveau : le bord du slime s'arrête à 'wallGap' de la paroi.
    float HalfWidth(int level) => Mathf.Max(0f, halfWidth - 0.5f * LevelDiameter(level) - wallGap);

    float LevelDiameter(int level) => _fusion != null ? _fusion.LevelDiameter(level) : template.diameter;
    Color LevelColor(int level) => _fusion != null ? _fusion.LevelColor(level) : template.debugColor;

    // Slime d'un niveau (taille, capsules, couleur et raideur définis par BlobFusion ; sans BlobFusion : le modèle).
    public BLOB SpawnLevel(Vector2 position, int level)
    {
        if (_fusion == null) return Spawn(position, template.diameter, template.sides, template.debugColor);
        return Spawn(position, _fusion.LevelDiameter(level), _fusion.LevelSides(level), _fusion.LevelColor(level), _fusion.LevelStiffness(level));
    }

    // Copie du blob modèle avec une autre taille / couleur, ajoutée en fin de liste (ordre de création = ordre des pas).
    // 'stiffness' multiplie les fréquences des joints du modèle (flexion et étirement).
    public BLOB Spawn(Vector2 position, float diameter, int sides, Color color, float stiffness = 1f)
    {
        var go = Instantiate(template.gameObject, position, Quaternion.identity, transform);
        go.name = "Blob " + (++_spawned);
        var blob = go.GetComponent<BLOB>();
        blob.diameter = diameter;
        blob.sides = sides;
        blob.debugColor = color;
        blob.jointFrequency *= stiffness;
        blob.jointLinearFrequency *= stiffness;
        go.SetActive(true); // son OnEnable crée sa physique à cet endroit
        _blobs.Add(blob);
        return blob;
    }

    // Retire tous les blobs (fin de niveau), du dernier créé au premier.
    public void RemoveAll()
    {
        for (int i = _blobs.Count - 1; i >= 0; i--) Remove(_blobs[i]);
    }

    // Retire un blob : sa physique est détruite tout de suite, son objet en fin d'image.
    public void Remove(BLOB blob)
    {
        if (!_blobs.Remove(blob)) return;
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
        if (!debugDraw || DropsLeft == 0) return;
        DrawSlime(world, DropPoint, _current);
        DrawSlime(world, NextPreviewPoint, _next);
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

    public Vector2 NextPreviewPoint => (Vector2)transform.position + nextPreviewOffset;

    void DrawSlime(PhysicsWorld world, Vector2 center, int level)
    {
        var color = LevelColor(level);
        float r = 0.5f * LevelDiameter(level);
        world.DrawCircle(center, r, new Color(color.r, color.g, color.b, 0.35f), 0f, PhysicsWorld.DrawFillOptions.Interior);
        world.DrawCircle(center, r, color, 0f, PhysicsWorld.DrawFillOptions.Outline);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        var cam = Camera.main;
        if (cam != null && DropsLeft > 1)
        {
            Vector2 p = NextPreviewPoint - new Vector2(0f, 0.5f * LevelDiameter(_next) + 0.2f);
            Vector3 s = cam.WorldToScreenPoint(p);
            GUI.Label(new Rect(s.x - 50f, Screen.height - s.y, 100f, 22f), "Suivant", _centered ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
        if (!showStats) return;
        int bodies = 0;
        for (int i = 0; i < _blobs.Count; i++) bodies += _blobs[i] != null ? _blobs[i].CapsuleCount : 0;
        GUI.Label(new Rect(10, 10, 600, 90),
            $"Blobs : {_blobs.Count}   corps : {bodies}\n" +
            $"Physique : {_stepMs:F2} ms par pas = {_stepMs * 12f:F0} % du temps réel (120 pas/s ; au-delà de 100 %, le jeu ralentit)\n" +
            $"Images/s : {_fps:F0}\n" +
            "Flèches = viser, Espace = lâcher (tenu = rafale), R = vider, H = formes physiques");
    }

    void OnDrawGizmos()
    {
        if (template == null) return;
        if (_fusion == null) _fusion = GetComponent<BlobFusion>();
        Gizmos.color = previewColor;
        Vector3 p = transform.position;
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
