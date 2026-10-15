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
    [Min(0f)] public float smoothTime = 0.1f;
    [Tooltip("Zone morte : demi-taille (en unités Unity, absolu) du rectangle centré sur la caméra dans lequel la cible\n" +
             "peut bouger sans que la caméra ne la suive. (0, 0) = suivi permanent.")]
    public Vector2 deadZone = Vector2.zero;

    GameObject _cachedTarget;
    ICameraTarget _cameraTarget;

    void Start()
    {
        SnapToTarget();
    }

    [ContextMenu("Snap To Target")]
    public void SnapToTarget()
    {
        if (TryGetTargetPosition(out var p)) MoveTo(Desired(p, transform.position, Vector2.zero));
    }

    void LateUpdate()
    {
        if (!TryGetTargetPosition(out var p)) return;

        Vector2 current = transform.position;
        Vector2 desired = Desired(p, current, deadZone);
        float t = smoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / smoothTime) : 1f;
        MoveTo(Vector2.Lerp(current, desired, t));
    }

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
