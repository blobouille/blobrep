using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Cible")]
    [Tooltip("Objet à suivre. Un BLOB ou une Ball2D est suivi par sa position physique réelle (centre du blob / de la balle) ;\n" +
             "tout autre objet est suivi par la position de son Transform.")]
    public GameObject target;
    [Tooltip("Décalage de la caméra par rapport à la cible, en unités Unity (absolu). Ex. (2, 0) = la cible est vue 2 unités à gauche du centre.")]
    public Vector2 offset = Vector2.zero;

    [Header("Axes")]
    [Tooltip("Suivre la cible horizontalement.")]
    public bool followX = true;
    [Tooltip("Suivre la cible verticalement.")]
    public bool followY = true;

    [Header("Lissage")]
    [Tooltip("Temps (en secondes) pour rattraper environ 63 % de l'écart avec la cible. 0 = caméra collée à la cible, sans retard.")]
    [Min(0f)] public float smoothTime = 0f;
    [Tooltip("Zone morte : demi-taille (en unités Unity, absolu) du rectangle centré sur la caméra dans lequel la cible\n" +
             "peut bouger sans que la caméra ne la suive. (0, 0) = suivi permanent.")]
    public Vector2 deadZone = Vector2.zero;

    [Header("Focus destruction")]
    [Tooltip("Quand la cible (un BLOB) est déstructurée par un obstacle, la caméra se centre sur l'obstacle et zoome.\n" +
             "Après 'slowDuration', elle dézoome en restant centrée sur l'impact ; suivi normal au redémarrage du niveau.")]
    public bool focusOnBreak = true;
    [Tooltip("Taille orthographique de la caméra pendant le focus (demi-hauteur visible, en unités Unity). Plus petit = zoom plus fort.")]
    [Min(0.1f)] public float focusSize = 2.5f;
    [Tooltip("Temps (en secondes réelles, insensible au ralenti) pour faire environ 63 % du trajet et du zoom vers le focus.")]
    [Min(0f)] public float focusSmoothTime = 0.12f;
    [Tooltip("Temps (en secondes réelles) pour faire environ 63 % du dézoom vers la taille de départ, une fois la durée du ralenti\n" +
             "écoulée. La caméra reste centrée sur le point d'impact.")]
    [Min(0f)] public float dezoomSmoothTime = 0.35f;

    [Header("Ralenti à la destruction")]
    [Tooltip("Ralentit le temps au moment de la destruction (via monde_settings). La simulation reste identique : seul le nombre\n" +
             "de pas physiques par seconde réelle diminue.")]
    public bool slowMotion = true;
    [Tooltip("Vitesse du temps pendant le ralenti, multipliée par 'gameSpeed' de monde_settings. 0,3 = 3× plus lent.")]
    [Range(0.01f, 1f)] public float slowFactor = 0.2f;
    [Tooltip("Durée du ralenti, en secondes réelles. C'est aussi la durée du zoom avant le dézoom (même sans ralenti).")]
    [Min(0f)] public float slowDuration = 0.5f;

    GameObject _cachedTarget;
    ICameraTarget _cameraTarget;
    Camera _camera;
    float _baseSize;
    bool _focusing;
    Vector2 _focusPoint;
    float _slowEndTime = -1f;
    float _zoomEndTime = -1f;
    monde_settings _settings;

    void Start()
    {
        _camera = GetComponent<Camera>();
        _baseSize = _camera.orthographicSize;
        SnapToTarget();
    }

    void OnEnable()
    {
        BLOB.Broken += OnBlobBroken;
        LevelRestart.Restarted += OnLevelRestarted;
        LevelSimulation.Jumped += OnLevelRestarted;
    }

    void OnDisable()
    {
        BLOB.Broken -= OnBlobBroken;
        LevelRestart.Restarted -= OnLevelRestarted;
        LevelSimulation.Jumped -= OnLevelRestarted;
        EndSlowMotion();
    }

    void OnLevelRestarted()
    {
        _focusing = false;
        EndSlowMotion();
        if (_camera == null) return;
        _camera.orthographicSize = _baseSize;
        SnapToTarget();
    }

    void OnBlobBroken(BLOB blob, BlobHazard hazard, Vector2 contactPoint)
    {
        if (!focusOnBreak || target != blob.gameObject || LevelSimulation.FastForwarding) return;
        _focusing = true;
        _focusPoint = hazard.FocusPoint(contactPoint);
        _zoomEndTime = Time.unscaledTime + slowDuration;

        if (!slowMotion) return;
        if (_settings == null) _settings = FindFirstObjectByType<monde_settings>();
        if (_settings == null)
        {
            Debug.LogWarning("CameraFollow : pas de monde_settings dans la scène, ralenti ignoré.", this);
            return;
        }
        _settings.SetTimeScaleMultiplier(slowFactor);
        _slowEndTime = Time.unscaledTime + slowDuration;
    }

    void EndSlowMotion()
    {
        if (_slowEndTime < 0f) return;
        _slowEndTime = -1f;
        if (_settings != null) _settings.SetTimeScaleMultiplier(1f);
    }

    [ContextMenu("Snap To Target")]
    public void SnapToTarget()
    {
        if (TryGetTargetPosition(out var p)) MoveTo(Desired(p, transform.position, Vector2.zero));
    }

    void LateUpdate()
    {
        if (_slowEndTime >= 0f && Time.unscaledTime >= _slowEndTime) EndSlowMotion();

        float f = Smooth(focusSmoothTime);
        bool zoomed = _focusing && Time.unscaledTime < _zoomEndTime;
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, zoomed ? focusSize : _baseSize, zoomed || !_focusing ? f : Smooth(dezoomSmoothTime));

        if (_focusing)
        {
            MoveTo(Vector2.Lerp(transform.position, _focusPoint, f));
            return;
        }

        if (!TryGetTargetPosition(out var p)) return;

        Vector2 current = transform.position;
        Vector2 desired = Desired(p, current, deadZone);
        float t = smoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / smoothTime) : 1f;
        MoveTo(Vector2.Lerp(current, desired, t));
    }

    static float Smooth(float time) => time > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / time) : 1f;

    Vector2 Desired(Vector2 targetPos, Vector2 current, Vector2 dead)
    {
        Vector2 goal = targetPos + offset;
        Vector2 delta = goal - current;
        delta.x = followX ? Mathf.Sign(delta.x) * Mathf.Max(0f, Mathf.Abs(delta.x) - dead.x) : 0f;
        delta.y = followY ? Mathf.Sign(delta.y) * Mathf.Max(0f, Mathf.Abs(delta.y) - dead.y) : 0f;
        return current + delta;
    }

    void MoveTo(Vector2 p)
    {
        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    bool TryGetTargetPosition(out Vector2 position)
    {
        position = default;
        if (target == null) return false;

        if (_cachedTarget != target)
        {
            _cachedTarget = target;
            _cameraTarget = target.GetComponent<ICameraTarget>();
        }

        position = _cameraTarget != null ? _cameraTarget.CameraPosition : (Vector2)target.transform.position;
        return true;
    }
}
