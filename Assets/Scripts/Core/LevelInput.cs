using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Touches du jeu, un bit chacune dans les entrées enregistrées. Valeurs enregistrées dans les scènes par leur numéro : en
// ajouter seulement à la FIN.
public enum GameKey { LeftArrow, RightArrow, UpArrow, DownArrow, Space }

// Entrées du joueur pour le pas en cours : un bit par GameKey. Lues au clavier en jeu normal,
// rejouées depuis un enregistrement (rejeu accéléré).
public static class LevelInput
{
    public static uint Current { get; private set; }

    // Entrées de chaque pas depuis le dernier redémarrage : Log[i] = entrées du pas i.
    static readonly List<uint> _log = new List<uint>();
    public static IReadOnlyList<uint> Log => _log;

    public static bool IsDown(GameKey key) => (Current & (1u << (int)key)) != 0;

    public static uint SampleKeyboard()
    {
        uint mask = 0;
        for (int k = 0; k <= (int)GameKey.Space; k++)
            if (KeyHeld((GameKey)k)) mask |= 1u << k;
        return mask;
    }

    static bool KeyHeld(GameKey k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case GameKey.LeftArrow: return kb.leftArrowKey.isPressed;
            case GameKey.RightArrow: return kb.rightArrowKey.isPressed;
            case GameKey.UpArrow: return kb.upArrowKey.isPressed;
            case GameKey.DownArrow: return kb.downArrowKey.isPressed;
            case GameKey.Space: return kb.spaceKey.isPressed;
        }
        return false;
#else
        switch (k)
        {
            case GameKey.LeftArrow: return Input.GetKey(KeyCode.LeftArrow);
            case GameKey.RightArrow: return Input.GetKey(KeyCode.RightArrow);
            case GameKey.UpArrow: return Input.GetKey(KeyCode.UpArrow);
            case GameKey.DownArrow: return Input.GetKey(KeyCode.DownArrow);
            case GameKey.Space: return Input.GetKey(KeyCode.Space);
        }
        return false;
#endif
    }

    internal static void Apply(uint mask)
    {
        Current = mask;
        _log.Add(mask);
    }

    internal static void Clear()
    {
        Current = 0;
        _log.Clear();
    }
}
