using UnityEngine;
using UnityEngine.InputSystem;
using Unity.U2D.Physics;

// Outil de debug : déplacer un corps à la souris rend la simulation NON déterministe.
[DisallowMultipleComponent]
public class LowLevelRaycastGrabber : MonoBehaviour
{
    [Header("Réglages")]
    [Tooltip("Distance max (unités Unity) entre la souris et le centre d'un corps dynamique pour l'attraper.")]
    [Min(0.01f)] public float grabRadius = 0.5f;
    [Tooltip("Vitesse à laquelle le corps rejoint la souris (fraction de l'écart rattrapée par seconde).")]
    [Min(0f)] public float pullSpeed = 25.0f;
    [Tooltip("Dessine une ligne entre le corps attrapé et la souris (Scene view).")]
    public bool drawLine = true;
    [Tooltip("Couleur de la ligne de debug.")]
    public Color lineColor = Color.cyan;

    PhysicsBody _target;
    bool _dragging;
    PhysicsBody.BodyType _originalType;
    Vector2 _mouseWorld;
    bool _pressed;
    bool _released;

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        _mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.value);
        _pressed |= mouse.leftButton.wasPressedThisFrame;
        _released |= mouse.leftButton.wasReleasedThisFrame;

        if (_dragging && drawLine)
            Debug.DrawLine(_target.position, _mouseWorld, lineColor);
    }

    void FixedUpdate()
    {
        if (_pressed) TryGrab();
        if (_released) Release();
        _pressed = _released = false;

        if (!_dragging || !_target.isValid) return;

        float dt = Time.fixedDeltaTime;
        Vector2 targetPos = Vector2.Lerp(_target.position, _mouseWorld, dt * pullSpeed);
        _target.linearVelocity = (targetPos - (Vector2)_target.position) / dt;
        _target.position = targetPos;
    }

    void TryGrab()
    {
        _dragging = false;
        using var bodies = PhysicsWorld.defaultWorld.GetBodies();
        foreach (var body in bodies)
        {
            if (body.type != PhysicsBody.BodyType.Dynamic) continue;
            if (Vector2.Distance(body.position, _mouseWorld) > grabRadius) continue;

            _target = body;
            _dragging = true;
            _originalType = body.type;
            body.type = PhysicsBody.BodyType.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            break;
        }
    }

    void Release()
    {
        if (_dragging && _target.isValid) _target.type = _originalType;
        _dragging = false;
    }
}
