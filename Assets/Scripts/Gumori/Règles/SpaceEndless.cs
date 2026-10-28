using UnityEngine;

// Mode « Space Endless » (scène `Slime/space endless`) : réservoir sans fin, sans objectif, graine tirée au hasard à chaque
// partie (affichée ; la partie reste rejouable au bit près avec elle). Les réglages « espace » (gumori très mous, gravité
// faible) sont ceux de la scène elle-même : rien n'est changé dans le réservoir normal. Hors simulation.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-600)]
[AddComponentMenu("Gumori/Règles/SpaceEndless")]
public class SpaceEndless : MonoBehaviour
{
    [Tooltip("Tirer une graine au hasard à chaque nouvelle partie (lancement, N). R rejoue la même graine. Décoché = graine de\n" +
             "GumoriDropper.")]
    public bool randomSeed = true;

    GumoriDropper _dropper;
    int _best;

    const string BestKey = "slime.space.best";

    void Awake()
    {
        _dropper = GumoriDropper.Find<GumoriDropper>(this);
        _best = PlayerPrefs.GetInt(BestKey, 0);
        // Unity appelle Awake même composant décoché : décoché = pas de mode Sans fin (objectif et graine de la scène).
        if (!enabled) return;
        var goal = GumoriDropper.Find<GumoriGoal>(this);
        if (goal != null) goal.useGoal = false;
        var setup = GumoriDropper.Find<GumoriLevelSetup>(this);
        if (setup != null) setup.enabled = false;
        NewSeed();
    }

    void OnEnable() => LevelRestart.Restarted += OnRestarted;

    void OnDisable()
    {
        LevelRestart.Restarted -= OnRestarted;
        SaveBest();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveBest();
    }

    void OnRestarted() => SaveBest();

    void NewSeed()
    {
        if (randomSeed) _dropper.seed = GumoriSession.RandomSeed();
    }

    void SaveBest()
    {
        var score = GumoriDropper.Find<GumoriScore>(this);
        if (score == null || score.Score <= _best) return;
        _best = score.Score;
        PlayerPrefs.SetInt(BestKey, _best);
        PlayerPrefs.Save();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame) NewGame();
#endif
    }

    // Nouvelle partie (N) : nouvelle graine puis redémarrage, entre deux pas.
    public void NewGame()
    {
        SaveBest();
        NewSeed();
        LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
    }

    void OnGUI()
    {
        float u = GumoriUi.Unit;
        var r = new Rect(16f * u, Screen.height - 64f * u, Screen.width - 32f * u, 48f * u);
        GumoriUi.Label(r, $"Space Endless · graine {_dropper.seed} · record {_best:N0}   (N = nouvelle)", 22f * u, Color.white, 1f, TextAnchor.MiddleRight);
    }
}
