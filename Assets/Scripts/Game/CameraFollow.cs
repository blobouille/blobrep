using System.Collections.Generic;
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

    [Header("Anticipation")]
    [Tooltip("La caméra se décale dans la direction où va la cible (selon sa vitesse) pour montrer ce qui arrive devant elle.\n" +
             "Rendu seulement : la physique n'est que lue.")]
    public bool lookAhead = true;
    [Tooltip("Temps d'anticipation, en secondes : on montre où sera la cible dans ce temps. Décalage = (vitesse − vitesse morte) × ce temps, plafonné.\n" +
             "Ex. 0,25 s à 50 km/h ≈ 3 m devant.")]
    [Min(0f)] public float lookAheadTime = 0.25f;
    [Tooltip("Décalage d'anticipation maximal, en unités Unity (absolu). À garder sous la demi-hauteur de la caméra pour ne jamais perdre la cible.")]
    [Min(0f)] public float lookAheadMaxDistance = 3f;
    [Tooltip("Vitesse morte, en km/h : en dessous, aucune anticipation (la caméra reste centrée quand la cible roule doucement).")]
    [SpeedKmh] [Min(0f)] public float lookAheadDeadSpeed = 2f;
    [Tooltip("Poids de l'anticipation par axe (X horizontal, Y vertical), RELATIF : 1 = plein, 0 = aucune.\n" +
             "Moins à la verticale évite que la caméra plonge à chaque rebond.")]
    public Vector2 lookAheadAxisWeight = new Vector2(1f, 0.6f);
    [Tooltip("Inertie de la direction suivie quand la cible ACCÉLÈRE, en secondes (temps pour environ 63 % du changement).\n" +
             "Plus grand = la caméra ignore les rebonds brefs.")]
    [Min(0f)] public float lookAheadSmoothTime = 0.35f;
    [Tooltip("Inertie quand la cible RALENTIT ou change de sens, en secondes (63 %). Plus grand = retour plus doux.")]
    [Min(0f)] public float lookAheadReturnTime = 0.8f;
    [Tooltip("Vitesse maximale de glissement du décalage, en km/h (temps de jeu). Limite les grands balayages d'écran\n" +
             "lors d'un demi-tour. 0 = sans limite.")]
    [SpeedKmh] [Min(0f)] public float lookAheadMaxShiftSpeed = 6f;

    [Header("Dézoom selon la vitesse")]
    [Tooltip("La caméra dézoome quand la cible va vite, pour voir plus loin autour d'elle. Rendu seulement.")]
    public bool speedZoom = true;
    [Tooltip("Vitesse (km/h) à partir de laquelle le dézoom commence. En dessous : taille de départ de la caméra.")]
    [SpeedKmh] [Min(0f)] public float speedZoomStartSpeed = 10f;
    [Tooltip("Vitesse (km/h) à laquelle le dézoom est maximal (transition douce entre les deux vitesses).")]
    [SpeedKmh] [Min(0f)] public float speedZoomFullSpeed = 30f;
    [Tooltip("Dézoom maximal, RELATIF à la taille de départ de la caméra : 1,3 = on voit 30 % plus loin dans chaque direction.")]
    [Range(1f, 3f)] public float speedZoomMaxFactor = 1.3f;
    [Tooltip("Temps (en secondes de jeu) pour faire environ 63 % du dézoom quand la cible accélère.")]
    [Min(0f)] public float speedZoomOutTime = 0.8f;
    [Tooltip("Temps (en secondes de jeu) pour faire environ 63 % du retour au zoom normal quand la cible ralentit.\n" +
             "Plus long que le dézoom pour ne pas pomper à chaque rebond.")]
    [Min(0f)] public float speedZoomInTime = 2f;

    [Header("Focus destruction")]
    [Tooltip("Quand la cible (un BLOB) est déstructurée par un obstacle, la caméra se centre sur le point de contact et zoome.\n" +
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
    readonly List<BodyState> _states = new List<BodyState>();
    ITrainingBody _trainingBody;
    Vector2 _lastTargetPos;
    bool _hasLastTargetPos;
    Vector2 _smoothVelocity;
    Vector2 _lookOffset;
    float _speedZoom = 1f;

    void Start()
    {
        _camera = GetComponent<Camera>();
        _baseSize = _camera.orthographicSize;
        SnapToTarget();
    }

    void OnEnable()
    {
        BLOB.Broken += OnBlobBroken;
        Skater.Died += OnSkaterDied;
        LevelRestart.Restarted += OnLevelRestarted;
        LevelSimulation.Jumped += OnLevelRestarted;
    }

    void OnDisable()
    {
        BLOB.Broken -= OnBlobBroken;
        Skater.Died -= OnSkaterDied;
        LevelRestart.Restarted -= OnLevelRestarted;
        LevelSimulation.Jumped -= OnLevelRestarted;
        EndSlowMotion();
    }

    void OnLevelRestarted()
    {
        _focusing = false;
        EndSlowMotion();
        if (_camera == null) return;
        ResetMotion();
        _camera.orthographicSize = _baseSize * _speedZoom;
        SnapToTarget();
    }

    void OnBlobBroken(BLOB blob, BlobHazard hazard, Vector2 contactPoint) => Focus(blob.gameObject, contactPoint);

    void OnSkaterDied(Skater skater, Vector2 contactPoint) => Focus(skater.gameObject, contactPoint);

    void Focus(GameObject broken, Vector2 contactPoint)
    {
        if (!focusOnBreak || target != broken || LevelSimulation.FastForwarding) return;
        _focusing = true;
        _focusPoint = contactPoint;
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
        if (TryGetTargetPosition(out var p)) MoveTo(Desired(p + _lookOffset, transform.position, Vector2.zero));
    }

    void LateUpdate()
    {
        if (_slowEndTime >= 0f && Time.unscaledTime >= _slowEndTime) EndSlowMotion();

        float f = Smooth(focusSmoothTime);
        bool zoomed = _focusing && Time.unscaledTime < _zoomEndTime;
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, zoomed ? focusSize : _baseSize * _speedZoom, zoomed || !_focusing ? f : Smooth(dezoomSmoothTime));

        if (_focusing)
        {
            MoveTo(Vector2.Lerp(transform.position, _focusPoint, f));
            return;
        }

        if (!TryGetTargetPosition(out var p)) return;
        UpdateMotion();

        Vector2 current = transform.position;
        Vector2 desired = Desired(p + _lookOffset, current, deadZone);
        float t = smoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / smoothTime) : 1f;
        MoveTo(Vector2.Lerp(current, desired, t));
    }

    // Après un redémarrage ou un retour de sauvegarde : décalage et dézoom directement à leur valeur d'équilibre, sans balayage.
    void ResetMotion()
    {
        _hasLastTargetPos = false;
        Vector2 v = TryGetTargetVelocity(out var tv, 0f) ? tv : Vector2.zero;
        _smoothVelocity = lookAhead ? v : Vector2.zero;
        _lookOffset = LookAheadGoal();
        _speedZoom = SpeedZoomGoal(v.magnitude);
    }

    void UpdateMotion()
    {
        float dt = Time.deltaTime;
        if (!lookAhead)
        {
            _lookOffset = Vector2.zero;
            _smoothVelocity = Vector2.zero;
        }
        if (dt <= 0f || !TryGetTargetVelocity(out var v, dt)) return;

        float zoomGoal = SpeedZoomGoal(v.magnitude);
        float zoomTime = zoomGoal > _speedZoom ? speedZoomOutTime : speedZoomInTime;
        _speedZoom = Mathf.Lerp(_speedZoom, zoomGoal, zoomTime > 0f ? 1f - Mathf.Exp(-dt / zoomTime) : 1f);

        if (!lookAhead) return;

        bool accelerating = v.sqrMagnitude >= _smoothVelocity.sqrMagnitude && Vector2.Dot(v, _smoothVelocity) >= 0f;
        float time = accelerating ? lookAheadSmoothTime : lookAheadReturnTime;
        _smoothVelocity = Vector2.Lerp(_smoothVelocity, v, time > 0f ? 1f - Mathf.Exp(-dt / time) : 1f);

        Vector2 goal = LookAheadGoal();
        _lookOffset = lookAheadMaxShiftSpeed > 0f ? Vector2.MoveTowards(_lookOffset, goal, lookAheadMaxShiftSpeed * dt) : goal;
    }

    float SpeedZoomGoal(float speed)
    {
        if (!speedZoom) return 1f;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(speedZoomStartSpeed, speedZoomFullSpeed, speed));
        return Mathf.Lerp(1f, speedZoomMaxFactor, t);
    }

    Vector2 LookAheadGoal()
    {
        if (!lookAhead) return Vector2.zero;
        float speed = _smoothVelocity.magnitude;
        float distance = Mathf.Min(lookAheadMaxDistance, Mathf.Max(0f, speed - lookAheadDeadSpeed) * lookAheadTime);
        if (distance <= 0f) return Vector2.zero;
        Vector2 o = _smoothVelocity * (distance / speed);
        return new Vector2(o.x * lookAheadAxisWeight.x, o.y * lookAheadAxisWeight.y);
    }

    // Vitesse du centre de la cible : celle donnée par son ICameraTarget (skateur : planche, que ses rotations ne changent pas),
    // sinon moyenne des corps d'un ITrainingBody (rotation du blob ignorée), sinon déplacement depuis l'image précédente.
    bool TryGetTargetVelocity(out Vector2 velocity, float dt)
    {
        velocity = default;
        if (!TryGetTargetPosition(out var p)) return false;
        if (_cameraTarget != null && _cameraTarget.TryGetCameraVelocity(out velocity)) return true;
        if (_trainingBody != null)
        {
            _trainingBody.GetBodyStates(_states);
            if (_states.Count == 0) return false;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < _states.Count; i++) sum += _states[i].velocity;
            velocity = sum / _states.Count;
            return true;
        }
        bool ok = _hasLastTargetPos && dt > 0f;
        if (ok) velocity = (p - _lastTargetPos) / dt;
        _lastTargetPos = p;
        _hasLastTargetPos = true;
        return ok;
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
            _trainingBody = target.GetComponent<ITrainingBody>();
            _hasLastTargetPos = false;
        }

        position = _cameraTarget != null ? _cameraTarget.CameraPosition : (Vector2)target.transform.position;
        return true;
    }
}
