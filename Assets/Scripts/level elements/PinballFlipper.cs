using UnityEngine;
using Unity.U2D.Physics;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum FlipperKey
{
    LeftArrow, RightArrow, UpArrow, DownArrow,
    Space, LeftShift, RightShift, LeftCtrl, RightCtrl,
    A, D, Q, E, Z, X, N, M
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class PinballFlipper : MonoBehaviour, ILevelPhysics
{
    [Header("Forme")]
    [Tooltip("Échelle globale du levier : multiplie la longueur et les deux rayons.")]
    [Min(0.01f)] public float size = 1.68f;
    [Tooltip("Distance entre le centre du grand cercle (axe de rotation) et le centre du petit cercle (bout), en unités Unity, multipliée par 'size'.")]
    [Min(0.05f)] public float length = 1.0f;
    [Tooltip("Rayon du grand cercle (base, centrée sur l'axe de rotation), en unités Unity, multiplié par 'size'.")]
    [Min(0.01f)] public float baseRadius = 0.15f;
    [Tooltip("Rayon du petit cercle (bout du levier), en unités Unity, multiplié par 'size'.")]
    [Min(0.01f)] public float tipRadius = 0.03f;

    [Header("Commande")]
    [Tooltip("Touche qui active le levier. Plusieurs flippers avec la même touche se déclenchent ensemble.")]
    public FlipperKey key = FlipperKey.LeftArrow;
    [Tooltip("Débattement en degrés depuis la position de repos (rotation Z du GameObject).\n" +
             "Positif = sens anti-horaire, négatif = sens horaire.")]
    public float activeAngle = 45f;
    [Tooltip("Vitesse de rotation du levier, en degrés par seconde.")]
    [Min(1f)] public float motorSpeed = 400F;
    [Tooltip("Coché : corps dynamique + charnière à moteur à couple limité (dépend de la masse).\n" +
             "Décoché : corps kinematic piloté en vitesse, sans calcul de couple.")]
    public bool useTorque = false;
    [Tooltip("Couple maximum du moteur (force du levier). Ignoré si 'useTorque' est décoché.")]
    [Min(0f)] public float motorTorque = 400F;
    [Tooltip("Mode « bullet » (fastCollisionsAllowed) du levier : collision continue contre les corps dynamiques.\n" +
             "Box2D ne l'applique qu'aux corps dynamiques (mode 'useTorque') ; en kinematic, activer plutôt\n" +
             "'fastCollisions' sur la balle / le blob.")]
    public bool fastCollisions = false;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view en position de repos.")]
    public Color previewColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [Tooltip("Couleur de l'aperçu Scene view en position active et de l'arc de débattement.")]
    public Color previewActiveColor = new Color(1f, 0.6f, 0.1f, 0.6f);
    [Tooltip("Logge les coordonnées monde exactes (valeurs + bits) des centres de la base et du bout.\n" +
             "Logs : création, appui, relâchement, immobilisation.")]
    public bool logCoords = false;

    PhysicsBody _anchor;
    PhysicsBody _flipper;
    PhysicsHingeJoint _joint;
    bool _created;
    bool _builtWithTorque;
    bool _lastDown;
    bool _wasMoving;

    public void BuildShape(out Vector2 c1, out float r1, out Vector2 c2, out float r2, out Vector2[] quad)
    {
        r1 = baseRadius * size;
        r2 = tipRadius * size;
        float L = Mathf.Max(length * size, Mathf.Abs(r1 - r2) + 0.001f);
        c1 = Vector2.zero;
        c2 = new Vector2(L, 0f);

        float s = (r1 - r2) / L;
        float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));

        quad = new[]
        {
            c1 + r1 * new Vector2(s, -c),
            c2 + r2 * new Vector2(s, -c),
            c2 + r2 * new Vector2(s,  c),
            c1 + r1 * new Vector2(s,  c)
        };
    }

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
        flipperDef.fastCollisionsAllowed = fastCollisions;
        flipperDef.rotation = PhysicsRotate.FromRadians(restRad);

        if (_builtWithTorque)
        {
            var anchorDef = PhysicsBodyDefinition.defaultDefinition;
            anchorDef.type = PhysicsBody.BodyType.Static;
            anchorDef.position = new Vector2(p.x, p.y);
            anchorDef.rotation = PhysicsRotate.FromRadians(restRad);
            _anchor = world.CreateBody(anchorDef);

            flipperDef.type = PhysicsBody.BodyType.Dynamic;
            _flipper = world.CreateBody(flipperDef);
            _flipper.sleepingAllowed = false;
        }
        else
        {
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
        _lastDown = false;
        _wasMoving = false;
        LogCircles("init");
    }

    void DestroyPhysics()
    {
        if (!_created) return;
        if (_flipper.isValid) _flipper.Destroy();
        if (_anchor.isValid) _anchor.Destroy();
        _created = false;
    }

    float Dir() => activeAngle >= 0f ? 1f : -1f;
    float RestSpeed() => -Dir() * motorSpeed;
    float ActiveSpeed() => Dir() * motorSpeed;

    void FixedUpdate()
    {
        if (!Application.isPlaying || !_created) return;

        if (_builtWithTorque != useTorque)
        {
            DestroyPhysics();
            CreatePhysics();
        }

        bool down = IsDown(key);
        DebugCoords(down);

        if (_builtWithTorque)
        {
            _joint.motorSpeed = down ? ActiveSpeed() : RestSpeed();
            _joint.maxMotorTorque = motorTorque;
        }
        else
        {
            DriveKinematic(down);
        }
    }

    // Vitesse = reste à parcourir / pas, plafonnée : le dernier pas tombe exactement sur la cible.
    void DriveKinematic(bool down)
    {
        float restDeg = transform.eulerAngles.z;

        var rot = _flipper.rotation;
        float currentDeg = Mathf.Atan2(rot.sin, rot.cos) * Mathf.Rad2Deg;
        float relative = Mathf.DeltaAngle(restDeg, currentDeg);

        float target = down ? activeAngle : 0f;
        float remaining = target - relative;

        _flipper.angularVelocity = Mathf.Clamp(remaining / Time.fixedDeltaTime, -motorSpeed, motorSpeed);
    }

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

    void DebugCoords(bool down)
    {
        if (!logCoords) return;

        if (down != _lastDown)
        {
            LogCircles(down ? "press" : "release");
            _lastDown = down;
        }

        bool moving = !Mathf.Approximately(_flipper.angularVelocity, 0f);
        if (_wasMoving && !moving) LogCircles("repos");
        _wasMoving = moving;
    }

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

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        BuildShape(out var c1, out var r1, out var c2, out var r2, out var quad);
        float rest = transform.eulerAngles.z;
        Vector3 pivot = transform.position;

        DrawPose(pivot, rest, c1, r1, c2, r2, quad, previewColor);
        DrawPose(pivot, rest + activeAngle, c1, r1, c2, r2, quad, previewActiveColor);

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
