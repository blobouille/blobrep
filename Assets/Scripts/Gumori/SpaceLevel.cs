using UnityEngine;

// Niveau Space à objectif : libérer tous les bocaux (GumoriJars) en 'maxShots' tirs au plus. Dernier bocal brisé = plus de
// tir, la chaîne en cours se finit, puis réussi (les gumori sont retirés : GumoriFinale les fait se diviser et s'envoler).
// Tirs épuisés avec des bocaux entiers : on attend que la pile se calme (plus de fusion pendant 'settleTime'), puis raté.
// Jugé pendant les pas : même partie = même résultat. Remplace SpaceEndless dans la scène (graine fixe du lâcher).
[DisallowMultipleComponent]
[DefaultExecutionOrder(-600)]
[RequireComponent(typeof(GumoriDropper), typeof(GumoriLauncher))]
public class SpaceLevel : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Objectif")]
    [Tooltip("Bocaux à libérer. Vide = les premiers de la scène.")]
    public GumoriJars jars;
    [Tooltip("Nombre de tirs permis.")]
    [Min(1)] public int maxShots = 20;
    [Tooltip("Étoiles : part des tirs encore en main quand le dernier bocal se brise, pour 2 et 3 étoiles.")]
    [Range(0f, 1f)] public float twoStars = 0.15f;
    [Range(0f, 1f)] public float threeStars = 0.3f;
    [Tooltip("Après le dernier tir : temps (s) sans fusion avant de juger le niveau.")]
    [Min(0.1f)] public float settleTime = 3f;
    [Tooltip("Niveau réussi : temps (s) laissé à la fête avant le panneau de fin.")]
    [Min(0f)] public float celebrationTime = 3.5f;

    [Header("Affichage")]
    [Tooltip("Nom du niveau, montré au départ.")]
    public string title = "Les bocaux";
    [Tooltip("Consigne montrée au départ (lisible en une seconde).")]
    [TextArea] public string hint = "Fusionne contre un bocal pour le briser !";
    [Tooltip("Durée (s) de la consigne au départ (cachée dès le premier tir).")]
    [Min(0f)] public float hintTime = 4f;

    GumoriDropper _dropper;
    GumoriLauncher _launcher;
    GumoriFusion _fusion;
    GumoriScore _score;
    int _lastEventStep, _lastMergeStep, _leftAtWin, _shownRemaining;
    float _startTime, _endTime, _brokeTime = -10f;
    bool _cleared;

    public GumoriGoal.State Result { get; private set; }
    public int LeftAtWin => _leftAtWin;
    public int Stars => Result != GumoriGoal.State.Won ? 0 : _leftAtWin >= Mathf.CeilToInt(threeStars * maxShots) ? 3 : _leftAtWin >= Mathf.CeilToInt(twoStars * maxShots) ? 2 : 1;

    // Fin du niveau (pendant LevelTick ; aussi pendant le rejeu accéléré). Réussi : appelé AVANT de retirer les gumori.
    public event System.Action<GumoriGoal.State> Finished;

    void Awake()
    {
        var goal = GetComponent<GumoriGoal>();
        if (goal != null) goal.useGoal = false;
        var setup = GetComponent<GumoriLevelSetup>();
        if (setup != null) setup.enabled = false;
    }

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _launcher = GetComponent<GumoriLauncher>();
        _fusion = GetComponent<GumoriFusion>();
        _score = GetComponent<GumoriScore>();
        if (jars == null) jars = FindAnyObjectByType<GumoriJars>();
        _launcher.shotLimit = maxShots;
        _launcher.Launched += OnLaunched;
        if (_fusion != null) _fusion.Merged += OnMerged;
        Result = GumoriGoal.State.Playing;
        _cleared = false;
        _lastEventStep = _lastMergeStep = 0;
        _startTime = Time.time;
        _brokeTime = -10f;
        _shownRemaining = -1;
    }

    void OnDisable()
    {
        if (_launcher != null) { _launcher.Launched -= OnLaunched; _launcher.shotLimit = 0; }
        if (_fusion != null) _fusion.Merged -= OnMerged;
    }

    void OnLaunched(Gumori blob, Vector2 velocity) => _lastEventStep = LevelSimulation.Step;
    void OnMerged(Gumori blob, int level, Vector2 at) => _lastEventStep = _lastMergeStep = LevelSimulation.Step;

    public void LevelTick()
    {
        if (Result != GumoriGoal.State.Playing || jars == null) return;
        if (!_cleared && jars.Remaining == 0)
        {
            _cleared = true;
            _leftAtWin = _launcher.ShotsLeft;
            _launcher.shotLimit = Mathf.Max(1, _launcher.Shots.Count);
        }
        if (_cleared)
        {
            float wait = (_score != null ? _score.comboWindow : 1f) + (_fusion != null ? _fusion.MergeDuration : 0f);
            if (LevelSimulation.Step - _lastMergeStep < Mathf.RoundToInt(wait / Time.fixedDeltaTime)) return;
            End(GumoriGoal.State.Won);
            _dropper.RemoveAll();
            return;
        }
        if (_launcher.ShotsLeft > 0) return;
        if (LevelSimulation.Step - _lastEventStep < Mathf.RoundToInt(settleTime / Time.fixedDeltaTime)) return;
        End(GumoriGoal.State.Lost);
    }

    void End(GumoriGoal.State state)
    {
        Result = state;
        _endTime = Time.time;
        Finished?.Invoke(state);
    }

    void OnGUI()
    {
        if (!Application.isPlaying || jars == null) return;
        float u = GumoriUi.Unit, cx = Screen.width * 0.5f;
        int remaining = jars.Remaining, total = jars.Total;
        if (_shownRemaining >= 0 && remaining < _shownRemaining) _brokeTime = Time.time;
        _shownRemaining = remaining;

        // Haut de l'écran : bocaux restants (gros, le seul objectif) puis tirs restants.
        float pop = 1f + 0.35f * Mathf.Exp(-(Time.time - _brokeTime) * 6f);
        string jarsText = remaining == 0 ? "Tous libérés !" : $"{total - remaining} / {total} bocaux";
        GumoriUi.Label(new Rect(cx - 200f * u, 8f * u, 400f * u, 44f * u), jarsText, 30f * pop, remaining == 0 ? GumoriUi.Gold : Color.white);
        int left = _cleared ? _leftAtWin : _launcher.ShotsLeft;
        Color shotsColor = left <= 3 && Result == GumoriGoal.State.Playing && !_cleared
            ? Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(1f, 1f, 1f, 0.85f);
        GumoriUi.Label(new Rect(cx - 200f * u, 50f * u, 400f * u, 28f * u), left > 1 ? $"{left} tirs" : $"{left} tir", 20f, shotsColor);

        float age = Time.time - _startTime;
        if (age < hintTime && Result == GumoriGoal.State.Playing && _launcher.Shots.Count == 0)
        {
            float a = Mathf.Clamp01(age * 3f) * Mathf.Clamp01((hintTime - age) * 2f);
            GumoriUi.Label(new Rect(cx - 260f * u, Screen.height * 0.36f, 520f * u, 50f * u), title, 34f * GumoriUi.Pop(age), GumoriUi.Gold, a);
            GumoriUi.Label(new Rect(cx - 260f * u, Screen.height * 0.36f + 48f * u, 520f * u, 40f * u), hint, 20f, Color.white, a);
        }

        if (Result == GumoriGoal.State.Playing) return;
        bool won = Result == GumoriGoal.State.Won;
        float t = Time.time - _endTime - (won ? celebrationTime : 0f);
        if (t < 0f) return;
        GumoriUi.Dim(Mathf.Clamp01(t * 3f) * 0.55f);
        float cy = Screen.height * 0.4f;
        GumoriUi.Label(new Rect(cx - 260f * u, cy - 50f * u, 520f * u, 80f * u), won ? "Niveau réussi !" : "Raté…", 48f * GumoriUi.Pop(t),
            won ? new Color(1f, 0.88f, 0.35f) : new Color(1f, 0.55f, 0.6f));
        if (won)
            for (int i = 0; i < 3; i++)
            {
                float st = t - 0.35f - 0.25f * i;
                if (st < 0f) continue;
                float s = GumoriUi.Pop(st) * (i == 1 ? 88f : 70f) * u;
                float x = cx + (i - 1) * 84f * u, y = cy - 120f * u - (i == 1 ? 14f * u : 0f);
                GumoriUi.Star(new Rect(x - s * 0.5f, y - s * 0.5f, s, s), i < Stars);
            }
        else
            GumoriUi.Label(new Rect(cx - 260f * u, cy + 30f * u, 520f * u, 36f * u), remaining > 1 ? $"{remaining} bocaux encore fermés" : "1 bocal encore fermé", 20f, Color.white, Mathf.Clamp01((t - 0.3f) * 3f));
        var button = new Rect(cx - 95f * u, cy + 90f * u, 190f * u, 64f * u);
        if (GumoriUi.Button(button, "Rejouer", GumoriUi.Mint, 24f, Mathf.Clamp01((t - 0.6f) * 3f)))
            LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);
    }
}
