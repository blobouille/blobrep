using System.Collections.Generic;
using UnityEngine;

// Compteur de vitesse affiché en IMGUI (OnGUI) : aucun asset ni Canvas, lecture seule de la physique.
[DisallowMultipleComponent]
public class SpeedMeter : MonoBehaviour
{
    const float KmhPerMs = 3.6f;
    const float MphPerMs = 2.236936f;

    [Header("Cible")]
    [Tooltip("Balle dont on affiche la vitesse (BLOB, Ball2D…). Vide = cible de la CameraFollow de la caméra principale,\n" +
             "sinon le premier BLOB actif.")]
    public GameObject target;

    [Header("Unité")]
    [Tooltip("Coché : vitesse en miles par heure (mph). Décoché : en kilomètres par heure (km/h). 1 unité Unity = 1 m.")]
    public bool useMph = false;

    [Header("Affichage")]
    [Tooltip("Temps (en secondes réelles) pour que l'affichage rattrape environ 63 % d'un changement de vitesse.\n" +
             "0 = valeur brute du dernier pas physique (chiffres qui sautent).")]
    [Min(0f)] public float smoothTime = 0.1f;
    [Tooltip("Hauteur du texte, RELATIVE à la hauteur de l'écran (0,05 = 5 % de la hauteur).")]
    [Range(0.01f, 0.2f)] public float textHeight = 0.05f;
    [Tooltip("Marge entre le texte et le coin haut droit de l'écran, RELATIVE à la hauteur de l'écran.")]
    [Range(0f, 0.2f)] public float margin = 0.02f;
    [Tooltip("Couleur du texte.")]
    public Color textColor = Color.white;

    readonly List<BodyState> _states = new List<BodyState>();
    GameObject _cachedTarget;
    ITrainingBody _body;
    ICameraTarget _cameraTarget;
    float _displayed;
    GUIStyle _style;

    // Vitesse en m/s : celle donnée par l'ICameraTarget de la cible (skateur : planche, que ses rotations ne changent pas),
    // sinon moyenne des vecteurs vitesse des corps (la rotation du blob ne compte pas).
    public float SpeedMetersPerSecond
    {
        get
        {
            var body = ResolveTarget();
            if (_cameraTarget != null && _cameraTarget.TryGetCameraVelocity(out var velocity)) return velocity.magnitude;
            if (body == null) return 0f;
            body.GetBodyStates(_states);
            if (_states.Count == 0) return 0f;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < _states.Count; i++) sum += _states[i].velocity;
            return (sum / _states.Count).magnitude;
        }
    }

    ITrainingBody ResolveTarget()
    {
        var go = target;
        if (go == null && Camera.main != null && Camera.main.TryGetComponent(out CameraFollow follow)) go = follow.target;
        if (go == null && BLOB.Active.Count > 0) go = BLOB.Active[0].gameObject;
        if (go != _cachedTarget)
        {
            _cachedTarget = go;
            _body = go != null ? go.GetComponent<ITrainingBody>() : null;
            _cameraTarget = go != null ? go.GetComponent<ICameraTarget>() : null;
        }
        return _body;
    }

    void LateUpdate()
    {
        float speed = SpeedMetersPerSecond * (useMph ? MphPerMs : KmhPerMs);
        float t = smoothTime > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothTime) : 1f;
        _displayed = Mathf.Lerp(_displayed, speed, t);
    }

    void OnGUI()
    {
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
        _style.fontSize = Mathf.Max(1, Mathf.RoundToInt(textHeight * Screen.height));
        _style.normal.textColor = textColor;

        float m = margin * Screen.height;
        var rect = new Rect(m, m, Screen.width - 2f * m, _style.fontSize * 1.5f);
        GUI.Label(rect, $"{Mathf.RoundToInt(_displayed)} {(useMph ? "mph" : "km/h")}", _style);
    }
}
