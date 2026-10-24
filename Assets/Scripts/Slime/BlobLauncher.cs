using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lancer façon « sobosuba » (Space Endless). La RÉSERVE : une rangée de slimes qui flotte sous le plafond de la boîte fermée,
// attirés vers le milieu ; les nouveaux apparaissent au bord et filent s'y bousculer. On en attrape un (doigt /
// souris), on tire une flèche vers où l'envoyer, on relâche : il part dans cette direction, d'autant plus vite que la flèche
// est longue (doux = se poser sans déranger, fort = pousser et se faufiler). Chaque slime qui quitte la réserve (lancé, ou
// mangé par une fusion) en fait arriver un nouveau (suite à graine de BlobDropper) ; un slime né d'une fusion avec un slime de
// la réserve y reste. On peut donc préparer sa réserve (4 slimes ou plus) en fusionnant dedans.
// BlobDropper garde la liste des slimes, leur pas, la fusion… ; son propre lâcher est neutralisé dans la scène.
// Tirs appliqués dans LevelTick (ordre fixe) : déterministe pour une même liste de tirs. LIMITE : les tirs ne passent pas par
// LevelInput (analogiques) : le rejeu accéléré (FastForward) ne les rejoue pas encore ; ils sont gardés dans 'Shots'.
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobDropper))]
public class BlobLauncher : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Réserve")]
    [Tooltip("Nombre de slimes dans la réserve au départ.")]
    [Range(1, 6)] public int startCount = 3;
    [Tooltip("Taille max de la réserve : on y arrive en fusionnant un slime lancé avec un slime de la réserve. Aucune arrivée\n" +
             "au-delà (sinon la réserve grandit sans fin et pend jusqu'à la pile).")]
    [Range(1, 8)] public int maxRack = 4;
    [Tooltip("Courbe FERMÉE de la boîte : son haut (axe − ½ épaisseur) est le plafond où attend la réserve.")]
    public CurvePath2D box;
    [Tooltip("Hauteur de la ligne de la réserve sous le plafond (m, du plafond au centre des slimes). Elle flotte : il reste de la\n" +
             "place au-dessus pour faire rebondir un tir sur le plafond.")]
    [Min(0f)] public float rackDepth = 1.8f;
    [Tooltip("Ressort vertical (1/s²) qui ramène la réserve sur sa ligne (gravité annulée), et son amortissement (1/s).")]
    [Min(0f)] public float heightSpring = 25f;
    [Min(0f)] public float heightDamping = 4f;
    [Tooltip("Accélération (m/s²) qui tire chaque slime de la réserve vers le MILIEU : ils s'y rejoignent et se bousculent.")]
    [Min(0f)] public float rowPull = 5f;
    [Tooltip("Freinage horizontal de la réserve (1/s). Bas = ils gardent leur élan et se bousculent plus longtemps.")]
    [Min(0f)] public float rowDamping = 1.8f;
    [Tooltip("Vitesse d'arrivée d'un nouveau slime (km/h) : il apparaît au bord (à gauche puis à droite, à tour de rôle) et file\n" +
             "vers le milieu.")]
    [SpeedKmh] [Min(0f)] public float arrivalSpeed = 3f;
    [Tooltip("Délai (s) entre un départ et l'arrivée du slime suivant, et entre deux arrivées.")]
    [Min(0f)] public float arrivalDelay = 0.45f;

    [Header("Tir")]
    [Tooltip("Distance (m) du doigt au bord d'un slime de la réserve pour l'attraper.")]
    [Min(0f)] public float grabRange = 0.8f;
    [Tooltip("Longueur de flèche (m) qui donne la vitesse max.")]
    [Min(0.1f)] public float maxDrag = 5f;
    [Tooltip("Vitesse de tir max, en km/h (flèche à 'maxDrag' ou plus).")]
    [SpeedKmh] [Min(0f)] public float maxSpeed = 16f;
    [Tooltip("Flèche plus courte (m) : tir annulé.")]
    [Min(0f)] public float minDrag = 0.3f;

    [Header("Affichage")]
    public Color arrowColor = new Color(1f, 1f, 1f, 0.9f);

    // Tir enregistré : pas de simulation, rang dans la réserve, vitesse donnée au slime.
    public struct Shot { public int step, slot; public Vector2 velocity; }
    public readonly List<Shot> Shots = new List<Shot>();

    // Slime tiré (pendant le pas) : pour le son et les effets, jamais pour la simulation.
    public event System.Action<BLOB, Vector2> Launched;

    struct Request { public BLOB blob; public int slot; public Vector2 velocity; }

    BlobDropper _dropper;
    BlobFusion _fusion;
    readonly List<int> _sequence = new List<int>();
    readonly List<BLOB> _rack = new List<BLOB>();
    readonly List<Request> _pending = new List<Request>();
    int _drawn, _arrivals;
    float _ceiling, _inner, _time, _nextArrival;

    BLOB _grabbed;
    Vector2 _pointer;

    public IReadOnlyList<BLOB> Rack => _rack;

    void OnEnable()
    {
        _dropper = GetComponent<BlobDropper>();
        _fusion = GetComponent<BlobFusion>();
        _rack.Clear();
        _pending.Clear();
        Shots.Clear();
        _grabbed = null;
        _time = 0f;
        _nextArrival = 0f;
        _drawn = 0;
        _arrivals = startCount;
        int max = _fusion != null ? Mathf.Min(_dropper.maxDropLevel, _fusion.MaxLevel) : 0;
        BlobDropper.PredictSequence(_dropper.seed, max, _dropper.smallBias, 4096, _sequence);
        MeasureBox();
        if (_fusion != null) _fusion.Merged += OnMerged;
    }

    void OnDisable()
    {
        if (_fusion != null) _fusion.Merged -= OnMerged;
        _rack.Clear();
        _pending.Clear();
    }

    void MeasureBox()
    {
        _inner = _dropper.WallHalfWidth;
        _ceiling = transform.position.y;
        if (box == null) return;
        if (box.center.Count == 0) box.Rebuild();
        float top = float.MinValue;
        for (int i = 0; i < box.center.Count; i++) top = Mathf.Max(top, box.transform.TransformPoint(box.center[i]).y);
        _ceiling = top - 0.5f * box.thickness;
    }

    float LevelRadius(int level) => 0.5f * (_fusion != null ? _fusion.LevelDiameter(level) : _dropper.template.diameter);

    // Fusion (pendant le pas de BlobFusion) qui mange un slime de la réserve : le nouveau slime y reste et UN slime arrive.
    // Deux de la réserve entre eux : la réserve garde son nombre ; un slime lancé dans la réserve : elle en gagne un.
    void OnMerged(BLOB merged, int level, Vector2 at)
    {
        if (RemoveDead() == 0) return;
        _rack.Add(merged);
        Leave(1);
    }

    int RemoveDead()
    {
        int gone = 0;
        for (int i = _rack.Count - 1; i >= 0; i--)
            if (!Alive(_rack[i])) { _rack.RemoveAt(i); gone++; }
        return gone;
    }

    void PruneRack()
    {
        int gone = RemoveDead();
        if (gone > 0) Leave(gone);
    }

    void Leave(int count)
    {
        if (_arrivals == 0) _nextArrival = Mathf.Max(_nextArrival, _time + arrivalDelay);
        _arrivals = Mathf.Min(_arrivals + count, Mathf.Max(0, maxRack - _rack.Count));
    }

    public void LevelTick()
    {
        float dt = Time.fixedDeltaTime;
        _time += dt;
        float g = Unity.U2D.Physics.PhysicsWorld.defaultWorld.gravity.y;

        for (int i = 0; i < _pending.Count; i++) Fire(_pending[i]);
        _pending.Clear();
        PruneRack();

        if (_arrivals > 0 && _time >= _nextArrival && Arrive())
        {
            _arrivals--;
            _nextArrival = _time + arrivalDelay;
        }

        for (int i = 0; i < _rack.Count; i++) Hold(_rack[i], g, dt);
    }

    bool Alive(BLOB blob)
    {
        if (blob == null) return false;
        var list = _dropper.Blobs;
        for (int i = 0; i < list.Count; i++) if (list[i] == blob) return blob.CapsuleCount > 0;
        return false;
    }

    // Nouveau slime au bord droit sous le plafond, s'il y a la place ; il glisse vers la rangée.
    bool Arrive()
    {
        if (_dropper.template == null) return false;
        int level = _sequence[_drawn % _sequence.Count];
        float r = LevelRadius(level);
        float y = Mathf.Min(RackY, _ceiling - r - 0.05f);
        // Bord gauche puis droit à tour de rôle ; l'autre bord si celui-ci est occupé.
        float side = _drawn % 2 == 0 ? -1f : 1f;
        var at = new Vector2(transform.position.x + side * (_inner - r - 0.05f), y);
        if (!Free(at, r))
        {
            side = -side;
            at.x = transform.position.x + side * (_inner - r - 0.05f);
            if (!Free(at, r)) return false;
        }
        _drawn++;
        var blob = _dropper.SpawnLevel(at, level);
        var v = new Vector2(-side * arrivalSpeed, 0f);
        for (int k = 0; k < blob.CapsuleCount; k++)
        {
            var b = blob.Capsule(k);
            b.linearVelocity = v;
        }
        _rack.Add(blob);
        return true;
    }

    float RackY => _ceiling - rackDepth;

    bool Free(Vector2 at, float r)
    {
        var list = _dropper.Blobs;
        for (int i = 0; i < list.Count; i++)
            if (Vector2.Distance(Center(list[i]), at) < 0.95f * (r + 0.5f * list[i].diameter)) return false;
        return true;
    }

    // Réserve : gravité annulée, ressort vers sa ligne sous le plafond, attirée vers le milieu (accélération constante,
    // adoucie à moins de 0,5 m) : ils s'y rejoignent et se bousculent. Même correction pour toutes les capsules.
    void Hold(BLOB blob, float g, float dt)
    {
        int n = blob.CapsuleCount;
        if (n == 0) return;
        Vector2 c = Vector2.zero, v = Vector2.zero;
        for (int k = 0; k < n; k++) { var b = blob.Capsule(k); c += b.position; v += b.linearVelocity; }
        c /= n;
        v /= n;
        float toMiddle = Mathf.Clamp((transform.position.x - c.x) / 0.5f, -1f, 1f);
        Vector2 dv = new Vector2(rowPull * toMiddle - rowDamping * v.x,
                                 -g + heightSpring * (RackY - c.y) - heightDamping * v.y) * dt;
        for (int k = 0; k < n; k++)
        {
            var b = blob.Capsule(k);
            b.linearVelocity += dv;
        }
    }

    void Fire(Request request)
    {
        int slot = request.blob != null ? _rack.IndexOf(request.blob) : request.slot;
        if (slot < 0 || slot >= _rack.Count) return;
        var blob = _rack[slot];
        if (!Alive(blob)) return;
        _rack.RemoveAt(slot);
        Leave(1);
        int n = blob.CapsuleCount;
        Vector2 vc = Vector2.zero;
        for (int k = 0; k < n; k++) vc += blob.Capsule(k).linearVelocity;
        vc /= n;
        Vector2 dv = request.velocity - vc;
        for (int k = 0; k < n; k++)
        {
            var b = blob.Capsule(k);
            b.linearVelocity += dv;
        }
        Shots.Add(new Shot { step = LevelSimulation.Step, slot = slot, velocity = request.velocity });
        Launched?.Invoke(blob, request.velocity);
    }

    static Vector2 Center(BLOB blob)
    {
        Vector2 c = Vector2.zero;
        int n = blob.CapsuleCount;
        for (int k = 0; k < n; k++) c += blob.Capsule(k).position;
        return n > 0 ? c / n : (Vector2)blob.transform.position;
    }

    // Tir demandé par un script (rejeu, test) : rang dans la réserve, appliqué au début du prochain pas.
    public void Launch(int slot, Vector2 velocity) => _pending.Add(new Request { slot = slot, velocity = velocity });

    void Update()
    {
        if (!Application.isPlaying) return;
        var cam = Camera.main;
#if ENABLE_INPUT_SYSTEM
        var pointer = Pointer.current;
        if (cam == null || pointer == null) return;
        Vector2 screen = pointer.position.ReadValue();
        bool pressed = pointer.press.wasPressedThisFrame, held = pointer.press.isPressed, released = pointer.press.wasReleasedThisFrame;
#else
        if (cam == null) return;
        Vector2 screen = Input.mousePosition;
        bool pressed = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0), released = Input.GetMouseButtonUp(0);
#endif
        _pointer = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        if (pressed) _grabbed = Pick(_pointer);
        if (_grabbed != null && !_rack.Contains(_grabbed)) _grabbed = null;
        if (_grabbed != null && released)
        {
            Vector2 drag = _pointer - Center(_grabbed);
            float len = drag.magnitude;
            if (len >= minDrag) _pending.Add(new Request { blob = _grabbed, velocity = drag / len * (Mathf.Min(len, maxDrag) / maxDrag * maxSpeed) });
            _grabbed = null;
        }
        else if (!held) _grabbed = null;
    }

    BLOB Pick(Vector2 p)
    {
        BLOB best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < _rack.Count; i++)
        {
            var blob = _rack[i];
            if (!Alive(blob)) continue;
            float d = Vector2.Distance(Center(blob), p) - 0.5f * blob.diameter;
            if (d < grabRange && d < bestD) { bestD = d; best = blob; }
        }
        return best;
    }

    void OnGUI()
    {
        if (!Application.isPlaying || _grabbed == null || !Alive(_grabbed)) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 from = Center(_grabbed);
        Vector2 drag = _pointer - from;
        float len = drag.magnitude;
        if (len < 0.01f) return;
        Vector2 to = from + drag / len * Mathf.Min(len, maxDrag);
        Vector2 a = GuiPoint(cam, from), b = GuiPoint(cam, to);
        float u = SlimeUi.Unit;
        var color = len < minDrag ? new Color(arrowColor.r, arrowColor.g, arrowColor.b, 0.3f) : arrowColor;
        Line(a, b, 5f * u, color);
        Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
        Line(b, b - dir * 18f * u + side * 12f * u, 5f * u, color);
        Line(b, b - dir * 18f * u - side * 12f * u, 5f * u, color);
        float power = Mathf.Clamp01(len / maxDrag);
        SlimeUi.Label(new Rect(b.x + 14f * u, b.y - 14f * u, 120f * u, 28f * u), $"{Mathf.RoundToInt(power * 100f)} %", 18f * u, color, 1f, TextAnchor.MiddleLeft);
    }

    static Vector2 GuiPoint(Camera cam, Vector2 world)
    {
        Vector3 s = cam.WorldToScreenPoint(world);
        return new Vector2(s.x, Screen.height - s.y);
    }

    static void Line(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var matrix = GUI.matrix;
        var old = GUI.color;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 0.5f * width, d.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = old;
    }
}
