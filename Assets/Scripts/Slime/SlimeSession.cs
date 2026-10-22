using UnityEngine;
using UnityEngine.SceneManagement;

// Partie choisie et progression du joueur. Hors simulation : rien ici n'entre dans un pas (la graine du mode Sans fin est
// tirée AVANT la partie, qui reste rejouable au bit près avec cette graine). Progression dans PlayerPrefs.
public static class SlimeSession
{
    public enum Mode { None, Adventure, Endless }

    public const string MapScene = "carte", LevelScene = "reservoir";
#if UNITY_EDITOR
    // Écrit par l'inspecteur de la campagne (« Jouer ce niveau ») avant d'entrer en Play : niveau à lancer.
    public const string DevLevelKey = "slime.dev.level";
#endif

    const string UnlockedKey = "slime.unlocked", StarsKey = "slime.stars.", BestKey = "slime.best.", EndlessBestKey = "slime.endless.best";

    // None = réservoir lancé directement (réglages de la scène).
    public static Mode CurrentMode { get; private set; }
    public static int LevelIndex { get; private set; }
    public static int EndlessSeed { get; private set; }
    // Niveau où se tient l'avatar sur la carte (-1 = le dernier débloqué).
    public static int AvatarLevel = -1;
    // Écran titre déjà passé (on ne le remontre pas en revenant d'un niveau).
    public static bool TitleDone;

    // Dernier niveau joué, pour l'animation au retour sur la carte (-1 = aucun) ; consommé par la carte.
    public static int LastPlayed { get; private set; } = -1;
    public static bool LastWon { get; private set; }
    public static int LastStars { get; private set; }
    public static bool LastUnlocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        CurrentMode = Mode.None;
        LevelIndex = 0;
        EndlessSeed = 0;
        TitleDone = false;
        AvatarLevel = -1;
        ClearCache();
        ClearResult();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DevLaunch()
    {
        if (!PlayerPrefs.HasKey(DevLevelKey)) return;
        CurrentMode = Mode.Adventure;
        LevelIndex = PlayerPrefs.GetInt(DevLevelKey);
        TitleDone = true;
        PlayerPrefs.DeleteKey(DevLevelKey);
    }
#endif

    // ---- Progression ----

    // Lus une fois puis gardés en mémoire (la carte les lit à chaque image ; PlayerPrefs est lent, surtout sur téléphone).
    static int _unlocked = -1, _endlessBest = -1;
    static readonly int[] _stars = new int[256], _best = new int[256];

    static SlimeSession() => ClearCache();

    static void ClearCache()
    {
        _unlocked = _endlessBest = -1;
        System.Array.Fill(_stars, -1);
        System.Array.Fill(_best, -1);
    }

    static int Cached(int[] cache, string key, int index)
    {
        if (index < 0) return 0;
        if (index >= cache.Length) return PlayerPrefs.GetInt(key + index, 0);
        if (cache[index] < 0) cache[index] = PlayerPrefs.GetInt(key + index, 0);
        return cache[index];
    }

    // Nombre de niveaux débloqués (au moins 1).
    public static int Unlocked
    {
        get
        {
            if (_unlocked < 0) _unlocked = Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));
            return _unlocked;
        }
    }

    public static int Stars(int index) => Cached(_stars, StarsKey, index);
    public static int BestScore(int index) => Cached(_best, BestKey, index);

    public static int EndlessBest
    {
        get
        {
            if (_endlessBest < 0) _endlessBest = PlayerPrefs.GetInt(EndlessBestKey, 0);
            return _endlessBest;
        }
    }

    public static void ResetProgress()
    {
        int count = Mathf.Max(64, Unlocked + 1);
        PlayerPrefs.DeleteKey(UnlockedKey);
        for (int i = 0; i < count; i++)
        {
            PlayerPrefs.DeleteKey(StarsKey + i);
            PlayerPrefs.DeleteKey(BestKey + i);
        }
        PlayerPrefs.DeleteKey(EndlessBestKey);
        PlayerPrefs.Save();
        ClearCache();
        ClearResult();
    }

    // Fin d'un niveau de la campagne : étoiles et meilleur score gardés, niveau suivant débloqué si réussi.
    public static void ReportLevel(int index, int levelCount, bool won, int score, int stars)
    {
        LastPlayed = index;
        LastWon = won;
        LastStars = 0;
        LastUnlocked = false;
        if (!won) return;
        LastStars = stars;
        if (stars > Stars(index)) PlayerPrefs.SetInt(StarsKey + index, stars);
        if (score > BestScore(index)) PlayerPrefs.SetInt(BestKey + index, score);
        if (index + 1 < levelCount && index + 2 > Unlocked)
        {
            PlayerPrefs.SetInt(UnlockedKey, index + 2);
            LastUnlocked = true;
        }
        PlayerPrefs.Save();
        ClearCache();
    }

    public static void ReportEndless(int score)
    {
        if (score <= EndlessBest) return;
        PlayerPrefs.SetInt(EndlessBestKey, score);
        PlayerPrefs.Save();
        ClearCache();
    }

    public static void ClearResult()
    {
        LastPlayed = -1;
        LastWon = false;
        LastStars = 0;
        LastUnlocked = false;
    }

    // ---- Navigation ----

    public static void PlayLevel(int index)
    {
        CurrentMode = Mode.Adventure;
        LevelIndex = index;
        TitleDone = true;
        ClearResult();
        Load(LevelScene);
    }

    public static void PlayEndless()
    {
        CurrentMode = Mode.Endless;
        NewEndlessSeed();
        ClearResult();
        Load(LevelScene);
    }

    // Graine d'une nouvelle partie Sans fin : tirée à l'horloge (volontairement différente à chaque fois), hors simulation,
    // avant la partie ; la partie elle-même ne dépend que de cette graine.
    public static void NewEndlessSeed() => EndlessSeed = new System.Random(System.Environment.TickCount).Next(1, 1000000);

    public static void BackToMap() => Load(MapScene);

    static void Load(string scene)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
