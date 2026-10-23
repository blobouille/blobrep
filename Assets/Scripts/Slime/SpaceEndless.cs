using UnityEngine;

// Mode « Space Endless » (scène `Slime/space endless`) : réservoir sans fin, sans objectif, graine tirée au hasard à chaque
// partie (affichée ; la partie reste rejouable au bit près avec elle). Les réglages « espace » (slimes très mous, gravité
// faible) sont ceux de la scène elle-même : rien n'est changé dans le réservoir normal. Hors simulation.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-600)]
[RequireComponent(typeof(BlobDropper))]
public class SpaceEndless : MonoBehaviour
{
    [Tooltip("Tirer une graine au hasard à chaque nouvelle partie (lancement, N). R rejoue la même graine. Décoché = graine de\n" +
             "BlobDropper.")]
    public bool randomSeed = true;

    BlobDropper _dropper;
    int _best;

    const string BestKey = "slime.space.best";

    void Awake()
    {
        _dropper = GetComponent<BlobDropper>();
        var goal = GetComponent<SlimeGoal>();
        if (goal != null) goal.useGoal = false;
        var setup = GetComponent<SlimeLevelSetup>();
        if (setup != null) setup.enabled = false;
        _best = PlayerPrefs.GetInt(BestKey, 0);
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
        if (randomSeed) _dropper.seed = new System.Random(System.Environment.TickCount).Next(1, 1000000);
    }

    void SaveBest()
    {
        var score = GetComponent<SlimeScore>();
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
        float u = SlimeUi.Unit;
        var r = new Rect(16f * u, Screen.height - 64f * u, Screen.width - 32f * u, 48f * u);
        SlimeUi.Label(r, $"Space Endless · graine {_dropper.seed} · record {_best:N0}   (N = nouvelle)", 22f * u, Color.white, 1f, TextAnchor.MiddleRight);
    }
}
