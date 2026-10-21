using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Étape 1 du jeu de slimes : lâcher des blobs d'en haut dans un réservoir, rien d'autre.
// Flèches = déplacer le point de lâcher, Espace = lâcher (tenu = en rafale), R = tout vider (recommencer).
// Chaque blob lâché est une copie du blob modèle (objet désactivé de la scène) : on règle ses paramètres sur le modèle.
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

    [Header("Lâcher")]
    [Tooltip("Demi-largeur de la zone de lâcher, en m, centrée sur l'objet (le point de lâcher reste dedans).")]
    [Min(0f)] public float halfWidth = 3f;
    [Tooltip("Vitesse du point de lâcher, en km/h, flèche tenue.")]
    [SpeedKmh] [Min(0f)] public float moveSpeed = 5f;
    [Tooltip("Délai (s) entre deux lâchers quand la touche est tenue (rafale). Un appui bref lâche un seul blob.")]
    [Min(0.01f)] public float dropInterval = 0.3f;
    public FlipperKey dropKey = FlipperKey.Space;
    public FlipperKey leftKey = FlipperKey.LeftArrow;
    public FlipperKey rightKey = FlipperKey.RightArrow;

    [Header("Affichage")]
    [Tooltip("Coin haut gauche : nombre de blobs, de corps physiques, coût de la physique par pas et images par seconde.")]
    public bool showStats = true;
    public Color previewColor = new Color(1f, 1f, 1f, 0.5f);

    readonly List<BLOB> _blobs = new List<BLOB>();
    float _offset;     // position du point de lâcher par rapport à l'objet, en m
    float _cooldown;
    bool _wasDown;

    // Mesures (affichage) : coût de Box2D par pas, moyenne glissante.
    float _stepMs, _fps;

    int _spawned;

    public int Count => _blobs.Count;
    // Blobs vivants, dans l'ordre de création.
    public IReadOnlyList<BLOB> Blobs => _blobs;

    void OnEnable()
    {
        _spawned = 0;
        _offset = 0f;
        _cooldown = 0f;
        _wasDown = false;
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
        _offset = Mathf.Clamp(_offset + move * moveSpeed * dt, -halfWidth, halfWidth);

        bool down = LevelInput.IsDown(dropKey);
        _cooldown -= dt;
        if (down && (!_wasDown || _cooldown <= 0f)) Drop();
        _wasDown = down;

        // Les blobs créés ici ne sont pas dans la liste de LevelSimulation : leur pas est fait ici, dans l'ordre de création.
        for (int i = 0; i < _blobs.Count; i++) _blobs[i].LevelTick();

        float ms = PhysicsWorld.defaultWorld.profile.simulationStep;
        _stepMs = _stepMs == 0f ? ms : Mathf.Lerp(_stepMs, ms, 0.05f);
    }

    void Drop()
    {
        _cooldown = dropInterval;
        if (template == null || (maxBlobs > 0 && _blobs.Count >= maxBlobs)) return;
        Spawn(DropPoint, template.diameter, template.sides, template.debugColor);
    }

    // Copie du blob modèle avec une autre taille / couleur, ajoutée en fin de liste (ordre de création = ordre des pas).
    public BLOB Spawn(Vector2 position, float diameter, int sides, Color color)
    {
        var go = Instantiate(template.gameObject, position, Quaternion.identity, transform);
        go.name = "Blob " + (++_spawned);
        var blob = go.GetComponent<BLOB>();
        blob.diameter = diameter;
        blob.sides = sides;
        blob.debugColor = color;
        go.SetActive(true); // son OnEnable crée sa physique à cet endroit
        _blobs.Add(blob);
        return blob;
    }

    // Retire un blob : sa physique est détruite tout de suite, son objet en fin d'image.
    public void Remove(BLOB blob)
    {
        if (!_blobs.Remove(blob)) return;
        blob.enabled = false;
        Destroy(blob.gameObject);
    }

    Vector2 DropPoint => (Vector2)transform.position + new Vector2(_offset, 0f);

    void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) _fps = _fps == 0f ? 1f / dt : Mathf.Lerp(_fps, 1f / dt, 0.05f);
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
#else
        if (Input.GetKeyDown(KeyCode.R)) LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
#endif
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || template == null) return;
        var world = PhysicsWorld.defaultWorld;
        world.DrawCircle(DropPoint, 0.5f * template.diameter, previewColor, 0f, PhysicsWorld.DrawFillOptions.Outline);
        Vector2 a = (Vector2)transform.position + new Vector2(-halfWidth, 0f), b = (Vector2)transform.position + new Vector2(halfWidth, 0f);
        world.DrawLine(a, b, previewColor, 0f);
    }

    void OnGUI()
    {
        if (!showStats || !Application.isPlaying) return;
        int bodies = 0;
        for (int i = 0; i < _blobs.Count; i++) bodies += _blobs[i] != null ? _blobs[i].CapsuleCount : 0;
        GUI.Label(new Rect(10, 10, 600, 90),
            $"Blobs : {_blobs.Count}   corps : {bodies}\n" +
            $"Physique : {_stepMs:F2} ms par pas = {_stepMs * 12f:F0} % du temps réel (120 pas/s ; au-delà de 100 %, le jeu ralentit)\n" +
            $"Images/s : {_fps:F0}\n" +
            "Flèches = viser, Espace = lâcher (tenu = rafale), R = vider");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = previewColor;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * halfWidth, p + Vector3.right * halfWidth);
        float r = template != null ? 0.5f * template.diameter : 0.5f;
        const int N = 24;
        Vector3 prev = p + new Vector3(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float t = i * Mathf.PI * 2f / N;
            Vector3 cur = p + new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
