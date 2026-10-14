// PinballFlipper.cs
// Levier de flipper paramétrable, basé sur l'API physique 2D bas niveau de Unity (Box2D v3).
//
// - Unity 6.6+ : namespace Unity.U2D.Physics. Pour 6.3 / 6.4, le using bascule
//   automatiquement sur UnityEngine.LowLevelPhysics2D.
// - Place le composant sur un GameObject vide : la POSITION du GameObject = l'axe de rotation,
//   la ROTATION Z du GameObject = la direction du levier au repos.
// - Le levier s'étend le long de l'axe X local du GameObject.
// - Forme : un grand cercle (la base, centré sur l'axe) + un petit cercle plus loin (le bout),
//   reliés par leurs tangentes extérieures.
// - Plusieurs flippers avec la même touche se déclenchent ensemble.
// - useTorque = true  : corps dynamique + charnière à moteur (couple limité, dépend de la masse).
// - useTorque = false : corps kinematic piloté en vitesse angulaire (pas de couple, pas de masse).
//   Le pilotage se fait dans FixedUpdate avec Time.fixedDeltaTime (= pas réel de la simulation).

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_6000_6_OR_NEWER
using Unity.U2D.Physics;
#else
using UnityEngine.LowLevelPhysics2D;
#endif

public enum FlipperKey
{
    LeftArrow, RightArrow, UpArrow, DownArrow,
    Space, LeftShift, RightShift, LeftCtrl, RightCtrl,
    A, D, Q, E, Z, X, N, M
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class PinballFlipper : MonoBehaviour
{
    [Header("Forme")]
    [Tooltip("Échelle globale du levier (multiplie longueur et rayons).")]
    [Min(0.01f)] public float size = 1f;
    [Tooltip("Distance entre le centre du grand cercle (axe) et le centre du petit cercle (bout).")]
    [Min(0.05f)] public float length = 1.0f;
    [Tooltip("Rayon du grand cercle : la base, là où il y a la rotation.")]
    [Min(0.01f)] public float baseRadius = 0.15f;
    [Tooltip("Rayon du petit cercle : le bout du levier.")]
    [Min(0.01f)] public float tipRadius = 0.07f;

    [Header("Commande")]
    public FlipperKey key = FlipperKey.LeftArrow;
    [Tooltip("Angle de débattement en degrés, depuis la position de repos.\n" +
             "Positif = sens anti-horaire, négatif = sens horaire.")]
    public float activeAngle = 45f;
    [Tooltip("Vitesse du moteur en degrés par seconde.")]
    [Min(1f)] public float motorSpeed = 1000F;

    [Tooltip("Coché : corps dynamique + moteur à couple limité (comportement d'origine).\n" +
             "Décoché : corps kinematic piloté en vitesse, sans aucun calcul de couple.")]
    public bool useTorque = false;
    [Tooltip("Couple maximum du moteur (force du levier). Ignoré si 'useTorque' est décoché.")]
    [Min(0f)] public float motorTorque = 400F;

    [Header("Debug")]
    public Color previewColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    public Color previewActiveColor = new Color(1f, 0.6f, 0.1f, 0.6f);

    [Header("Debug coords")]
    [Tooltip("Logge les coordonnées monde exactes (valeurs + bits) du centre de la base et du centre du bout.\n" +
             "Logs : init, press, release, repos (une fois quand le levier s'immobilise).")]
    public bool logCoords = false;

    // --- état runtime ---
    PhysicsBody _anchor;
    PhysicsBody _flipper;
    PhysicsHingeJoint _joint;
    bool _created;
    bool _builtWithTorque; // mode utilisé à la création, pour recréer si on change la case en Play

    // --- état debug coords ---
    bool _lastDown;
    bool _wasMoving;

    // ------------------------------------------------------------------
    // Géométrie : 2 cercles + 1 quadrilatère entre leurs tangentes extérieures.
    // Coordonnées locales, axe de rotation en (0,0), bout vers +X.
    // ------------------------------------------------------------------
    public void BuildShape(out Vector2 c1, out float r1, out Vector2 c2, out float r2, out Vector2[] quad)
    {
        r1 = baseRadius * size;
        r2 = tipRadius * size;
        float L = Mathf.Max(length * size, Mathf.Abs(r1 - r2) + 0.001f);
        c1 = Vector2.zero;
        c2 = new Vector2(L, 0f);

        // Tangente extérieure : normale n = (s, c) avec s = (r1 - r2) / L
        float s = (r1 - r2) / L;
        float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));

        quad = new[]
        {
            c1 + r1 * new Vector2(s, -c),   // bas, côté base
            c2 + r2 * new Vector2(s, -c),   // bas, côté bout
            c2 + r2 * new Vector2(s,  c),   // haut, côté bout
            c1 + r1 * new Vector2(s,  c)    // haut, côté base
        };
    }

    // ------------------------------------------------------------------
    // Création / destruction de la simulation
    // ------------------------------------------------------------------
    void OnEnable()
    {
        if (Application.isPlaying) CreatePhysics();
    }

    void OnDisable()
    {
        DestroyPhysics();
    }

    void CreatePhysics()
    {
        if (_created) return;

        _builtWithTorque = useTorque;

        var world = PhysicsWorld.defaultWorld;
        Vector3 p = transform.position;
        float restRad = transform.eulerAngles.z * Mathf.Deg2Rad;

        var flipperDef = PhysicsBodyDefinition.defaultDefinition;
        flipperDef.position = new Vector2(p.x, p.y);
        flipperDef.rotation = PhysicsRotate.FromRadians(restRad);

        if (_builtWithTorque)
        {
            // --- MODE COUPLE : support statique + levier dynamique + charnière à moteur ---
            var anchorDef = PhysicsBodyDefinition.defaultDefinition;
            anchorDef.type = PhysicsBody.BodyType.Static;
            anchorDef.position = new Vector2(p.x, p.y);
            anchorDef.rotation = PhysicsRotate.FromRadians(restRad);
            _anchor = world.CreateBody(anchorDef);

            flipperDef.type = PhysicsBody.BodyType.Dynamic;
            _flipper = world.CreateBody(flipperDef);
            // Un levier piloté par moteur ne doit jamais s'endormir.
            _flipper.sleepingAllowed = false;
            // Collision continue contre les corps dynamiques (la balle) : évite de la traverser à haute vitesse.
            _flipper.fastCollisionsAllowed = true;
        }
        else
        {
            // --- MODE VITESSE : un seul corps kinematic, pas de joint, pas de support ---
            flipperDef.type = PhysicsBody.BodyType.Kinematic;
            _flipper = world.CreateBody(flipperDef);
            _flipper.sleepingAllowed = false;
        }

        BuildShape(out var c1, out var r1, out var c2, out var r2, out var quad);
        _flipper.CreateShape(new CircleGeometry { center = c1, radius = r1 });
        _flipper.CreateShape(new CircleGeometry { center = c2, radius = r2 });
        _flipper.CreateShape(PolygonGeometry.Create(quad));

        if (_builtWithTorque)
        {
            // Charnière : limites = [repos, repos + activeAngle] dans le sens choisi.
            var jd = PhysicsHingeJointDefinition.defaultDefinition;
            jd.bodyA = _anchor;
            jd.bodyB = _flipper;
            jd.enableLimit = true;
            jd.lowerAngleLimit = Mathf.Min(0f, activeAngle);
            jd.upperAngleLimit = Mathf.Max(0f, activeAngle);
            jd.enableMotor = true;
            jd.maxMotorTorque = motorTorque;
            jd.motorSpeed = RestSpeed();
            _joint = world.CreateJoint(jd);
        }

        _created = true;

        // Référence : état exact à la création (après _created = true, sinon LogCircles ne fait rien).
        _lastDown = false;
        _wasMoving = false;
        LogCircles("init");
    }

    void DestroyPhysics()
    {
        if (!_created) return;
        if (_flipper.isValid) _flipper.Destroy();   // détruit aussi shapes + joint
        if (_anchor.isValid) _anchor.Destroy();
        _created = false;
    }

    float Dir() => activeAngle >= 0f ? 1f : -1f;
    float RestSpeed() => -Dir() * motorSpeed;   // retour vers le repos
    float ActiveSpeed() => Dir() * motorSpeed;  // montée vers l'angle actif

    void Update()
    {
        if (!Application.isPlaying || !_created) return;

        // La case a changé en Play : on recrée le levier dans le bon mode.
        if (_builtWithTorque != useTorque)
        {
            DestroyPhysics();
            CreatePhysics();
        }

        // Mode couple : on pilote le moteur de la charnière ici.
        if (_builtWithTorque)
        {
            _joint.motorSpeed = IsDown(key) ? ActiveSpeed() : RestSpeed();
            _joint.maxMotorTorque = motorTorque;
        }
    }

    // Mode kinematic : piloté au pas réel de la simulation (Time.fixedDeltaTime).
    void FixedUpdate()
    {
        if (!Application.isPlaying || !_created) return;

        DebugCoords();

        if (_builtWithTorque) return;

        DriveKinematic(IsDown(key));
    }

    // Pilote le corps kinematic en vitesse angulaire (degrés/s), sans couple.
    // Vitesse = reste à parcourir / pas de simulation, plafonnée à motorSpeed :
    // vitesse constante pendant le trajet, et le dernier pas arrive exactement sur la cible (pas de dépassement).
    void DriveKinematic(bool down)
    {
        float restDeg = transform.eulerAngles.z;

        var rot = _flipper.rotation;
        float currentDeg = Mathf.Atan2(rot.sin, rot.cos) * Mathf.Rad2Deg;
        float relative = Mathf.DeltaAngle(restDeg, currentDeg); // angle depuis le repos, dans ]-180, 180]

        float target = down ? activeAngle : 0f;
        float remaining = target - relative;

        float velocity = Mathf.Clamp(remaining / Time.fixedDeltaTime, -motorSpeed, motorSpeed);
        _flipper.angularVelocity = velocity;
    }

    // ------------------------------------------------------------------
    // Debug : coordonnées exactes des deux cercles (base + bout) dans le monde
    // ------------------------------------------------------------------
    static string Bits(float f) => System.BitConverter.SingleToInt32Bits(f).ToString("X8");

    void LogCircles(string tag)
    {
        if (!logCoords || !_created) return;

        BuildShape(out var c1, out var r1, out var c2, out var r2, out _);
        Vector2 pos = _flipper.position;
        var rot = _flipper.rotation;

        Vector2 w1 = pos + new Vector2(rot.cos * c1.x - rot.sin * c1.y, rot.sin * c1.x + rot.cos * c1.y);
        Vector2 w2 = pos + new Vector2(rot.cos * c2.x - rot.sin * c2.y, rot.sin * c2.x + rot.cos * c2.y);

        Debug.Log($"[{name} | {tag}] t={Time.fixedTime:R}\n" +
                  $" base : ({w1.x:R}, {w1.y:R})  bits=({Bits(w1.x)},{Bits(w1.y)})  r={r1:R}\n" +
                  $" bout : ({w2.x:R}, {w2.y:R})  bits=({Bits(w2.x)},{Bits(w2.y)})  r={r2:R}\n" +
                  $" rot  : cos={rot.cos:R} sin={rot.sin:R}  angVel={_flipper.angularVelocity:R}");
    }

    // Appelé à chaque FixedUpdate : logge sur changement d'état de la touche,
    // et une seule fois quand le levier s'immobilise (pas de spam à chaque pas).
    void DebugCoords()
    {
        if (!logCoords) return;

        bool d = IsDown(key);
        if (d != _lastDown)
        {
            LogCircles(d ? "press" : "release");
            _lastDown = d;
        }

        bool moving = !Mathf.Approximately(_flipper.angularVelocity, 0f);
        if (_wasMoving && !moving) LogCircles("repos");
        _wasMoving = moving;
    }

    // ------------------------------------------------------------------
    // Entrées clavier (nouveau Input System si actif, sinon Input classique)
    // ------------------------------------------------------------------
    public static bool IsDown(FlipperKey k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case FlipperKey.LeftArrow:  return kb.leftArrowKey.isPressed;
            case FlipperKey.RightArrow: return kb.rightArrowKey.isPressed;
            case FlipperKey.UpArrow:    return kb.upArrowKey.isPressed;
            case FlipperKey.DownArrow:  return kb.downArrowKey.isPressed;
            case FlipperKey.Space:      return kb.spaceKey.isPressed;
            case FlipperKey.LeftShift:  return kb.leftShiftKey.isPressed;
            case FlipperKey.RightShift: return kb.rightShiftKey.isPressed;
            case FlipperKey.LeftCtrl:   return kb.leftCtrlKey.isPressed;
            case FlipperKey.RightCtrl:  return kb.rightCtrlKey.isPressed;
            case FlipperKey.A: return kb.aKey.isPressed;
            case FlipperKey.D: return kb.dKey.isPressed;
            case FlipperKey.Q: return kb.qKey.isPressed;
            case FlipperKey.E: return kb.eKey.isPressed;
            case FlipperKey.Z: return kb.zKey.isPressed;
            case FlipperKey.X: return kb.xKey.isPressed;
            case FlipperKey.N: return kb.nKey.isPressed;
            case FlipperKey.M: return kb.mKey.isPressed;
        }
        return false;
#else
        return Input.GetKey(ToKeyCode(k));
#endif
    }

#if !ENABLE_INPUT_SYSTEM
    static KeyCode ToKeyCode(FlipperKey k)
    {
        switch (k)
        {
            case FlipperKey.LeftArrow:  return KeyCode.LeftArrow;
            case FlipperKey.RightArrow: return KeyCode.RightArrow;
            case FlipperKey.UpArrow:    return KeyCode.UpArrow;
            case FlipperKey.DownArrow:  return KeyCode.DownArrow;
            case FlipperKey.Space:      return KeyCode.Space;
            case FlipperKey.LeftShift:  return KeyCode.LeftShift;
            case FlipperKey.RightShift: return KeyCode.RightShift;
            case FlipperKey.LeftCtrl:   return KeyCode.LeftControl;
            case FlipperKey.RightCtrl:  return KeyCode.RightControl;
            case FlipperKey.A: return KeyCode.A;
            case FlipperKey.D: return KeyCode.D;
            case FlipperKey.Q: return KeyCode.Q;
            case FlipperKey.E: return KeyCode.E;
            case FlipperKey.Z: return KeyCode.Z;
            case FlipperKey.X: return KeyCode.X;
            case FlipperKey.N: return KeyCode.N;
            case FlipperKey.M: return KeyCode.M;
        }
        return KeyCode.None;
    }
#endif

    // ------------------------------------------------------------------
    // Prévisualisation dans la Scene view (hors Play), avec la même géométrie
    // que celle envoyée à Box2D. En Play, le debug drawing de Box2D prend le relais.
    // ------------------------------------------------------------------
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        BuildShape(out var c1, out var r1, out var c2, out var r2, out var quad);
        float rest = transform.eulerAngles.z;
        Vector3 pivot = transform.position;

        DrawPose(pivot, rest, c1, r1, c2, r2, quad, previewColor);
        DrawPose(pivot, rest + activeAngle, c1, r1, c2, r2, quad, previewActiveColor);

        // Arc de débattement du bout
        Gizmos.color = previewActiveColor;
        float reach = c2.x;
        int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(activeAngle) / 5f));
        Vector3 prev = pivot + Rot(rest) * new Vector3(reach, 0f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float ang = rest + activeAngle * i / steps;
            Vector3 cur = pivot + Rot(ang) * new Vector3(reach, 0f, 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }

    static Quaternion Rot(float deg) => Quaternion.Euler(0f, 0f, deg);

    static void DrawPose(Vector3 pivot, float angleDeg, Vector2 c1, float r1, Vector2 c2, float r2,
                         Vector2[] quad, Color col)
    {
        Gizmos.color = col;
        var q = Rot(angleDeg);
        DrawCircle(pivot, q, c1, r1);
        DrawCircle(pivot, q, c2, r2);
        // Seules les tangentes sont utiles : bas (0→1) et haut (2→3)
        Gizmos.DrawLine(pivot + q * (Vector3)quad[0], pivot + q * (Vector3)quad[1]);
        Gizmos.DrawLine(pivot + q * (Vector3)quad[2], pivot + q * (Vector3)quad[3]);
    }

    static void DrawCircle(Vector3 pivot, Quaternion q, Vector2 center, float r)
    {
        const int N = 32;
        Vector3 prev = pivot + q * new Vector3(center.x + r, center.y, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = pivot + q * new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}