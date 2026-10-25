using UnityEngine;

// Applique la partie choisie (GumoriSession) au réservoir, avant que GumoriDropper et GumoriGoal démarrent : Aventure = graine, coups
// et objectif du niveau de la campagne ; Sans fin = pas d'objectif, graine tirée au hasard pour chaque nouvelle partie (affichée).
// Lancé sans session (Play direct dans l'éditeur) : rien n'est changé, ce sont les réglages de la scène. Fin de niveau : résultat
// enregistré (étoiles, niveau suivant débloqué), boutons Continuer / Rejouer / Carte. Hors simulation : n'écrit jamais pendant un pas.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(GumoriDropper), typeof(GumoriGoal), typeof(GumoriScore))]
public class GumoriLevelSetup : MonoBehaviour
{
    [Tooltip("Campagne (niveaux de l'Aventure).")]
    public GumoriCampaign campaign;

    GumoriDropper _dropper;
    GumoriGoal _goal;
    GumoriScore _score;
    GumoriLevel _level;
    bool _reported;

    GumoriSession.Mode Mode => GumoriSession.CurrentMode;

    void Awake()
    {
        _dropper = GetComponent<GumoriDropper>();
        _goal = GetComponent<GumoriGoal>();
        _score = GetComponent<GumoriScore>();
        Apply();
    }

    void OnEnable()
    {
        if (_goal != null) _goal.Finished += OnFinished;
        LevelRestart.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        if (_goal != null) _goal.Finished -= OnFinished;
        LevelRestart.Restarted -= OnRestarted;
        if (Mode == GumoriSession.Mode.Endless && _score != null) GumoriSession.ReportEndless(_score.Score);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && Mode == GumoriSession.Mode.Endless && _score != null) GumoriSession.ReportEndless(_score.Score);
    }

    void OnRestarted() => _reported = false;

    void Apply()
    {
        if (Mode == GumoriSession.Mode.Adventure)
        {
            _level = campaign != null ? campaign.Level(GumoriSession.LevelIndex) : null;
            if (_level == null) return;
            _dropper.seed = _level.seed;
            _dropper.maxDropLevel = _level.maxDropLevel;
            _dropper.smallBias = _level.smallBias;
            _goal.useGoal = true;
            _goal.maxDrops = _level.maxDrops;
            _goal.autoTarget = _level.autoTarget;
            _goal.difficulty = _level.difficulty;
            _goal.targetScore = _level.targetScore;
            _goal.comboShare = _level.comboShare;
        }
        else if (Mode == GumoriSession.Mode.Endless)
        {
            _dropper.seed = GumoriSession.EndlessSeed;
            _goal.useGoal = false;
        }
    }

    void OnFinished(GumoriGoal.State state)
    {
        if (LevelSimulation.FastForwarding || _reported || Mode != GumoriSession.Mode.Adventure || _level == null) return;
        _reported = true;
        bool won = state == GumoriGoal.State.Won;
        int stars = won ? campaign.Stars(_level, _goal.LeftAtWin) : 0;
        GumoriSession.ReportLevel(GumoriSession.LevelIndex, campaign.LevelCount, won, _score.Score, stars);
    }

    void Restart() => LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);

    void NewEndlessGame()
    {
        GumoriSession.ReportEndless(_score.Score);
        GumoriSession.NewEndlessSeed();
        Apply();
        Restart();
    }

    void OnGUI()
    {
        if (Mode == GumoriSession.Mode.None) return;
        float u = GumoriUi.Unit, w = Screen.width, h = Screen.height;
        // Barre du bas (sous le bocal) : boutons à gauche, infos à droite (le haut est pris par les coups et le score).
        var info = new Rect(w - 230f * u, h - 52f * u, 220f * u, 24f * u);
        var button = new Rect(10f * u, h - 56f * u, 120f * u, 46f * u);

        if (Mode == GumoriSession.Mode.Endless)
        {
            int best = Mathf.Max(GumoriSession.EndlessBest, _score.Score);
            GumoriUi.Label(info, $"Sans fin · record {best:N0}", 16f, GumoriUi.Sky, 1f, TextAnchor.MiddleRight);
            GumoriUi.Label(new Rect(info.x, info.y + 22f * u, info.width, 20f * u), $"graine {GumoriSession.EndlessSeed}", 12f, new Color(1f, 1f, 1f, 0.7f), 1f, TextAnchor.MiddleRight);
            if (GumoriUi.Button(button, "Menu", GumoriUi.Pink, 18f))
            {
                GumoriSession.TitleDone = false;
                GumoriSession.BackToMap();
            }
            if (GumoriUi.Button(new Rect(button.xMax + 8f * u, button.y, 140f * u, button.height), "Nouvelle", GumoriUi.Mint, 18f)) NewEndlessGame();
            return;
        }

        if (_level == null) return;
        int zone = campaign.ZoneOf(GumoriSession.LevelIndex, out _);
        GumoriUi.Label(info, $"Niveau {GumoriSession.LevelIndex + 1}", 18f, GumoriUi.Gold, 1f, TextAnchor.MiddleRight);
        if (zone >= 0) GumoriUi.Label(new Rect(info.x, info.y + 22f * u, info.width, 20f * u), campaign.zones[zone].name, 12f, new Color(1f, 1f, 1f, 0.7f), 1f, TextAnchor.MiddleRight);

        float age = _goal.PanelAge;
        if (age < 0f)
        {
            if (_goal.Result == GumoriGoal.State.Playing && GumoriUi.Button(button, "Carte", GumoriUi.Pink, 18f))
                GumoriSession.BackToMap();
            return;
        }

        bool won = _goal.Result == GumoriGoal.State.Won;
        float cx = w * 0.5f, cy = Screen.height * 0.4f;
        if (won)
        {
            int stars = campaign.Stars(_level, _goal.LeftAtWin);
            for (int i = 0; i < 3; i++)
            {
                float t = age - 0.35f - 0.25f * i;
                if (t < 0f) continue;
                float s = GumoriUi.Pop(t) * (i == 1 ? 88f : 70f) * u;
                float x = cx + (i - 1) * 84f * u, y = cy - 120f * u - (i == 1 ? 14f * u : 0f);
                GumoriUi.Star(new Rect(x - s * 0.5f, y - s * 0.5f, s, s), i < stars);
            }
        }

        float a = Mathf.Clamp01((age - 0.8f) * 3f);
        var left = new Rect(cx - 205f * u, cy + 125f * u, 190f * u, 64f * u);
        var right = new Rect(cx + 15f * u, cy + 125f * u, 190f * u, 64f * u);
        if (won)
        {
            if (GumoriUi.Button(left, "Rejouer", GumoriUi.Sky, 24f, a)) Restart();
            if (GumoriUi.Button(right, "Continuer", GumoriUi.Mint, 24f, a)) GumoriSession.BackToMap();
        }
        else
        {
            if (GumoriUi.Button(left, "Carte", GumoriUi.Pink, 24f, a)) GumoriSession.BackToMap();
            if (GumoriUi.Button(right, "Rejouer", GumoriUi.Mint, 24f, a)) Restart();
        }
    }
}
