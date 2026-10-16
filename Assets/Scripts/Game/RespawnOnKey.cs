using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RespawnOnKey : MonoBehaviour
{
    [Header("Recommencer le niveau")]
    [Tooltip("Touche qui remet tout le niveau dans son état de départ (blob, balles, flippers, obstacles, caméra), instantanément.\n" +
             "Efface aussi les points de sauvegarde du mode entraînement.")]
    public Key key = Key.R;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[key].wasPressedThisFrame) LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
    }
}
