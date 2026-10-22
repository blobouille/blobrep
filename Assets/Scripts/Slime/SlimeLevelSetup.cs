using UnityEngine;

// Applique la partie choisie (SlimeSession) au réservoir, avant que BlobDropper et SlimeGoal démarrent : Aventure = graine, coups
// et objectif du niveau de la campagne ; Sans fin = pas d'objectif, graine tirée au hasard pour chaque nouvelle partie (affichée).
// Lancé sans session (Play direct dans l'éditeur) : rien n'est changé, ce sont les réglages de la scène. Fin de niveau : résultat
// enregistré (étoiles, niveau suivant débloqué), boutons Continuer / Rejouer / Carte. Hors simulation : n'écrit jamais pendant un pas.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(BlobDropper), typeof(SlimeGoal), typeof(SlimeScore))]
public class SlimeLevelSetup : MonoBehaviour
{
    [Tooltip("Campagne (niveaux de l'Aventure).")]
    public SlimeCampaign campaign;

    BlobDropper _dropper;
    SlimeGoal _goal;
    SlimeScore _score;
    SlimeLevel _level;
    bool _reported;

    SlimeSession.Mode Mode => SlimeSession.CurrentMode;

    void Awake()
    {
        _dropper = GetComponent<BlobDropper>();
        _goal = GetComponent<SlimeGoal>();
        _score = GetComponent<SlimeScore>();
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
        if (Mode == SlimeSession.Mode.Endless && _score != null) SlimeSession.ReportEndless(_score.Score);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && Mode == SlimeSession.Mode.Endless && _score != null) SlimeSession.ReportEndless(_score.Score);
    }

    void OnRestarted() => _reported = false;

    void Apply()
    {
        if (Mode == SlimeSession.Mode.Adventure)
        {
            _level = campaign != null ? campaign.Level(SlimeSession.LevelIndex) : null;
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
        else if (Mode == SlimeSession.Mode.Endless)
        {
            _dropper.seed = SlimeSession.EndlessSeed;
            _goal.useGoal = false;
        }
    }

    void OnFinished(SlimeGoal.State state)
    {
        if (LevelSimulation.FastForwarding || _reported || Mode != SlimeSession.Mode.Adventure || _level == null) return;
        _reported = true;
        bool won = state == SlimeGoal.State.Won;
        int stars = won ? campaign.Stars(_level, _goal.LeftAtWin) : 0;
        SlimeSession.ReportLevel(SlimeSession.LevelIndex, campaign.LevelCount, won, _score.Score, stars);
    }

    void Restart() => LevelSimulation.RunBeforeNextStep(LevelRestart.Restart);

    void NewEndlessGame()
    {
        SlimeSession.ReportEndless(_score.Score);
        SlimeSession.NewEndlessSeed();
        Apply();
        Restart();
    }

    void OnGUI()
    {
        if (Mode == SlimeSession.Mode.None) return;
        float u = SlimeUi.Unit, w = Screen.width, h = Screen.height;
        // Barre du bas (sous le bocal) : boutons à gauche, infos à droite (le haut est pris par les coups et le score).
        var info = new Rect(w - 230f * u, h - 52f * u, 220f * u, 24f * u);
        var button = new Rect(10f * u, h - 56f * u, 120f * u, 46f * u);

        if (Mode == SlimeSession.Mode.Endless)
        {
            int best = Mathf.Max(SlimeSession.EndlessBest, _score.Score);
            SlimeUi.Label(info, $"Sans fin · record {best:N0}", 16f, SlimeUi.Sky, 1f, TextAnchor.MiddleRight);
            SlimeUi.Label(new Rect(info.x, info.y + 22f * u, info.width, 20f * u), $"graine {SlimeSession.EndlessSeed}", 12f, new Color(1f, 1f, 1f, 0.7f), 1f, TextAnchor.MiddleRight);
            if (SlimeUi.Button(button, "Menu", SlimeUi.Pink, 18f))
            {
                SlimeSession.TitleDone = false;
                SlimeSession.BackToMap();
            }
            if (SlimeUi.Button(new Rect(button.xMax + 8f * u, button.y, 140f * u, button.height), "Nouvelle", SlimeUi.Mint, 18f)) NewEndlessGame();
            return;
        }

        if (_level == null) return;
        int zone = campaign.ZoneOf(SlimeSession.LevelIndex, out _);
        SlimeUi.Label(info, $"Niveau {SlimeSession.LevelIndex + 1}", 18f, SlimeUi.Gold, 1f, TextAnchor.MiddleRight);
        if (zone >= 0) SlimeUi.Label(new Rect(info.x, info.y + 22f * u, info.width, 20f * u), campaign.zones[zone].name, 12f, new Color(1f, 1f, 1f, 0.7f), 1f, TextAnchor.MiddleRight);

        float age = _goal.PanelAge;
        if (age < 0f)
        {
            if (_goal.Result == SlimeGoal.State.Playing && SlimeUi.Button(button, "Carte", SlimeUi.Pink, 18f))
                SlimeSession.BackToMap();
            return;
        }

        bool won = _goal.Result == SlimeGoal.State.Won;
        float cx = w * 0.5f, cy = Screen.height * 0.4f;
        if (won)
        {
            int stars = campaign.Stars(_level, _goal.LeftAtWin);
            for (int i = 0; i < 3; i++)
            {
                float t = age - 0.35f - 0.25f * i;
                if (t < 0f) continue;
                float s = SlimeUi.Pop(t) * (i == 1 ? 88f : 70f) * u;
                float x = cx + (i - 1) * 84f * u, y = cy - 120f * u - (i == 1 ? 14f * u : 0f);
                SlimeUi.Star(new Rect(x - s * 0.5f, y - s * 0.5f, s, s), i < stars);
            }
        }

        float a = Mathf.Clamp01((age - 0.8f) * 3f);
        var left = new Rect(cx - 205f * u, cy + 125f * u, 190f * u, 64f * u);
        var right = new Rect(cx + 15f * u, cy + 125f * u, 190f * u, 64f * u);
        if (won)
        {
            if (SlimeUi.Button(left, "Rejouer", SlimeUi.Sky, 24f, a)) Restart();
            if (SlimeUi.Button(right, "Continuer", SlimeUi.Mint, 24f, a)) SlimeSession.BackToMap();
        }
        else
        {
            if (SlimeUi.Button(left, "Carte", SlimeUi.Pink, 24f, a)) SlimeSession.BackToMap();
            if (SlimeUi.Button(right, "Rejouer", SlimeUi.Mint, 24f, a)) Restart();
        }
    }
}
