using System.Collections.Generic;

// Entrées du joueur pour le pas en cours : un bit par FlipperKey. Lues au clavier en jeu normal,
// rejouées depuis un enregistrement quand on revient à un point de sauvegarde.
public static class LevelInput
{
    public static uint Current { get; private set; }

    // Entrées de chaque pas depuis le dernier redémarrage : Log[i] = entrées du pas i.
    static readonly List<uint> _log = new List<uint>();
    public static IReadOnlyList<uint> Log => _log;

    static readonly FlipperKey[] _keys = (FlipperKey[])System.Enum.GetValues(typeof(FlipperKey));

    public static bool IsDown(FlipperKey key) => (Current & (1u << (int)key)) != 0;

    public static uint SampleKeyboard()
    {
        uint mask = 0;
        foreach (var k in _keys)
            if (PinballFlipper.IsDown(k)) mask |= 1u << (int)k;
        return mask;
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
