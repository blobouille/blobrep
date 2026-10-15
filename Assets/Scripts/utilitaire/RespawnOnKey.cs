using UnityEngine;
using UnityEngine.InputSystem;

// Passe avant tous les autres FixedUpdate : le pas qui suit un redémarrage est identique au premier pas du niveau.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class RespawnOnKey : MonoBehaviour
{
    [Header("Recommencer le niveau")]
    [Tooltip("Touche qui remet tout le niveau dans son état de départ (blob, balles, flippers, obstacles, caméra), instantanément.")]
    public Key key = Key.R;

    bool _requested;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[key].wasPressedThisFrame) _requested = true;
    }

    void FixedUpdate()
    {
        if (!_requested) return;
        _requested = false;
        LevelRestart.Restart();
    }
}
