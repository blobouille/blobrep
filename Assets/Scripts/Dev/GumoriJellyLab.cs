using Unity.U2D.Physics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Bac à sable de la gelée (scène `Slime/space bac à sable`) : un seul gumori, aux réglages de la scène, et un DOIGT (cercle
// Box2D kinematic) qui l'écrase, le pique, le plaque contre la paroi, ou l'attrape et l'étire. Souris : clic gauche = appuyer,
// clic droit = attraper et tirer. Sans souris : scénario en boucle (`autoPlay`). Outil de test (et premier essai du futur
// « écraser son gumori avec les doigts ») : le doigt n'est PAS une entrée enregistrée, rien ici n'est déterministe.
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Dev/GumoriJellyLab")]
public class GumoriJellyLab : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Gumori")]
    [Tooltip("Niveau du gumori posé (taille, capsules et raideur de GumoriFusion).")]
    [Min(0)] public int level = 6;
    [Tooltip("Où il apparaît (m).")]
    public Vector2 spawn = new Vector2(0f, 3f);

    [Header("Doigt")]
    [Tooltip("Rayon du doigt (m).")]
    [Min(0.05f)] public float fingerRadius = 0.35f;
    [Tooltip("Vitesse max du doigt (km/h).")]
    [SpeedKmh] [Min(0.1f)] public float fingerSpeed = 30f;
    [Tooltip("Attraper : capsules à moins de cette distance du doigt (m) au moment du clic, puis tirées vers lui.")]
    [Min(0f)] public float grabRange = 0.5f;
    [Tooltip("Raideur de la prise (1/s) : vitesse des capsules attrapées = écart au doigt × raideur.")]
    [Min(0f)] public float grabStiffness = 12f;

    [Header("Scénario")]
    [Tooltip("Sans souris : le doigt rejoue en boucle écrasement, piqûre, étirement, coin, gifle.")]
    public bool autoPlay = true;
    [Tooltip("Vitesse du scénario (1 = normale).")]
    [Min(0.05f)] public float scenarioSpeed = 1f;
    [Tooltip("Caméra principale cadrée sur le gumori (taille orthographique = diamètre × ce facteur).")]
    [Min(0f)] public float zoom = 1.4f;

    GumoriDropper _dropper;
    Gumori _gumori;
    PhysicsBody _finger;
    Vector2 _target, _parked;
    bool _press, _grab, _mouse;
    float _time;
    readonly System.Collections.Generic.List<int> _held = new System.Collections.Generic.List<int>();
    readonly System.Collections.Generic.List<Vector2> _holdOffset = new System.Collections.Generic.List<Vector2>();

    public Gumori Gumori => _gumori;
    public Vector2 Finger => _finger.isValid ? _finger.position : _parked;
    public string Phase { get; private set; } = "";

    void OnEnable()
    {
        _dropper = FindAnyObjectByType<GumoriDropper>();
        _gumori = null;
        _time = 0f;
        _held.Clear(); _holdOffset.Clear();
        if (!Application.isPlaying) return;
        _parked = spawn + new Vector2(0f, 30f);
        var def = PhysicsBodyDefinition.defaultDefinition;
        def.type = PhysicsBody.BodyType.Kinematic;
        def.position = _parked;
        _finger = PhysicsWorld.defaultWorld.CreateBody(def);
        _finger.sleepingAllowed = false;
        var shape = PhysicsShapeDefinition.defaultDefinition;
        var material = shape.surfaceMaterial;
        material.friction = 0.6f;
        material.bounciness = 0f;
        material.customColor = new Color(1f, 1f, 1f, 0.6f);
        shape.surfaceMaterial = material;
        _finger.CreateShape(new CircleGeometry { center = Vector2.zero, radius = fingerRadius }, shape);
    }

    void OnDisable()
    {
        if (_finger.isValid) _finger.Destroy();
        _finger = default;
    }

    public void LevelTick()
    {
        if (_dropper == null || !_finger.isValid) return;
        if (_gumori == null || !GumoriDropper.IsInPlay(_gumori))
        {
            for (int i = _dropper.InPlay.Count - 1; i >= 0; i--) _dropper.Remove(_dropper.InPlay[i]);
            _gumori = _dropper.SpawnLevel(spawn, level);
            _time = 0f;
        }
        float dt = Time.fixedDeltaTime;
        _time += dt * scenarioSpeed;
        if (!_mouse)
        {
            if (autoPlay) Scenario();
            else _press = _grab = false;
        }

        Vector2 p = _finger.position, goal = _press || _grab ? _target : _parked;
        Vector2 v = (goal - p) / dt;
        if (!_press && !_grab && (goal - p).sqrMagnitude > 4f) { _finger.position = goal; v = Vector2.zero; }
        _finger.linearVelocity = Vector2.ClampMagnitude(v, fingerSpeed);

        // Prise : les capsules attrapées suivent le doigt (chacune à son écart du moment de la prise).
        if (_grab && _held.Count == 0)
            for (int k = 0; k < _gumori.CapsuleCount; k++)
            {
                Vector2 c = _gumori.Capsule(k).position;
                if ((c - p).sqrMagnitude < grabRange * grabRange) { _held.Add(k); _holdOffset.Add(c - p); }
            }
        if (!_grab) { _held.Clear(); _holdOffset.Clear(); }
        for (int i = 0; i < _held.Count; i++)
        {
            var body = _gumori.Capsule(_held[i]);
            body.linearVelocity = Vector2.ClampMagnitude((p + _holdOffset[i] - body.position) * grabStiffness, fingerSpeed);
        }
    }

    // Scénario en boucle, en diamètres du gumori autour de son point d'apparition (le sol est sous lui).
    void Scenario()
    {
        float d = _gumori != null ? _gumori.diameter : 1f;
        Vector2 c = _gumori != null ? _gumori.Center : spawn;
        float t = _time % 26f;
        _press = false; _grab = false;
        if (t < 2f) { Phase = "repos"; return; }
        if (t < 6f)
        {
            Phase = "écrasement";
            float s = Mathf.Clamp01((t - 2f) / 1.5f), back = Mathf.Clamp01((t - 4.5f) / 1f);
            _press = true;
            _target = new Vector2(c.x, Floor(c, d) + d * (1.3f - 0.95f * s + 0.95f * back) + fingerRadius);
            return;
        }
        if (t < 9f)
        {
            Phase = "piqûre";
            float s = Mathf.PingPong((t - 6f) * 1.4f, 1f);
            _press = true;
            _target = new Vector2(c.x - d * (1.2f - 0.95f * s), c.y + 0.1f * d);
            return;
        }
        if (t < 14f)
        {
            Phase = "étirement";
            float s = Mathf.Clamp01((t - 9.5f) / 2f), back = Mathf.Clamp01((t - 12.5f) / 0.2f);
            _grab = back < 1f;
            if (t < 9.5f) { _grab = false; _press = false; _target = c + new Vector2(0f, 0.5f * d); return; }
            if (_held.Count == 0) _target = c + new Vector2(0f, 0.5f * d);
            else _target += Vector2.up * (1.6f * d / 2f) * Time.fixedDeltaTime * scenarioSpeed * (s < 1f ? 1f : 0f);
            return;
        }
        if (t < 20f)
        {
            Phase = "coin";
            float s = Mathf.Clamp01((t - 15f) / 2.5f), back = Mathf.Clamp01((t - 18.5f) / 1f);
            _press = true;
            float wall = _dropper.MeasureWalls();
            _target = new Vector2(Mathf.Lerp(-wall + 2f * d, -wall + 0.45f * d, s - back * s), c.y);
            return;
        }
        if (t < 24f)
        {
            Phase = "gifle";
            float s = (t - 20.5f) / 0.5f;
            _press = s > 0f && s < 1f;
            _target = new Vector2(c.x + d * Mathf.Lerp(-1.4f, 1.4f, s), c.y + 0.15f * d);
            return;
        }
        Phase = "repos";
    }

    float Floor(Vector2 c, float d)
    {
        // Bas du gumori : point le plus bas de ses capsules.
        float low = c.y;
        if (_gumori != null) for (int k = 0; k < _gumori.CapsuleCount; k++) low = Mathf.Min(low, _gumori.Capsule(k).position.y);
        return low - (_gumori != null ? _gumori.CapsuleRadius : 0f);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        var cam = Camera.main;
        if (cam == null) return;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 screen = mouse.position.ReadValue();
        bool left = mouse.leftButton.isPressed, right = mouse.rightButton.isPressed;
#else
        Vector2 screen = Input.mousePosition;
        bool left = Input.GetMouseButton(0), right = Input.GetMouseButton(1);
#endif
        _mouse = left || right;
        if (!_mouse) return;
        _press = left;
        _grab = right;
        _target = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || zoom <= 0f || _gumori == null || !GumoriDropper.IsInPlay(_gumori)) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 c = _gumori.Center;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _gumori.diameter * zoom, 0.1f);
        var pos = cam.transform.position;
        cam.transform.position = Vector3.Lerp(pos, new Vector3(c.x, c.y, pos.z), 0.1f);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        GUI.Label(new Rect(12, 12, 600, 24), $"Bac à sable — {Phase}   (clic gauche : appuyer, clic droit : attraper)");
    }
}
