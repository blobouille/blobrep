using System.Collections.Generic;
using UnityEngine;

// Points : chaque fusion rapporte selon le niveau créé (nombres triangulaires, comme Suika : 1, 3, 6, 10, 15… × 'pointsPerMerge'),
// et les fusions qui s'enchaînent vite (réaction en chaîne) sont multipliées. Compté pendant le pas (événement de GumoriFusion,
// temps en pas de simulation) : même partie = même score, au bit près. Affichage seulement dans OnGUI.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Règles/GumoriScore")]
public class GumoriScore : MonoBehaviour, ILevelPhysics
{
    [Header("Points")]
    [Tooltip("Points de la première fusion (niveau 1). Niveau n : × n(n+1)/2 (niveau 2 = × 3, niveau 3 = × 6, niveau 4 = × 10…).")]
    [Min(1)] public int pointsPerMerge = 10;
    [Tooltip("Délai max (s) entre deux fusions pour continuer la chaîne. Chaque fusion de la chaîne relance ce délai.")]
    [Min(0f)] public float comboWindow = 1.2f;
    [Tooltip("Bonus par fusion enchaînée, en % : 50 = ×1, ×1,5, ×2, ×2,5…")]
    [Min(0)] public int comboPercent = 50;

    [Header("Affichage")]
    [Tooltip("Durée (s) des points qui s'envolent au-dessus d'une fusion.")]
    [Min(0.1f)] public float popupTime = 1.2f;

    struct Popup
    {
        public Vector2 position;
        public string text;
        public Color color;
        public float born, size;
    }

    GumoriFusion _fusion;
    int _lastMergeStep = int.MinValue;
    readonly List<Popup> _popups = new List<Popup>();
    GUIStyle _scoreStyle, _smallStyle, _popupStyle;
    float _shownScore, _bump;
    int _lastShown;

    public struct Hit
    {
        public Gumori blob;
        public int level, points, combo;
        public Vector2 position;
        public bool record; // plus gros niveau atteint jusqu'ici dans la partie
    }

    // Chaque fusion comptée (pendant LevelTick, après mise à jour du score) : effets et sons s'en servent, sans écrire dans la
    // simulation. Aussi pendant le rejeu accéléré (LevelSimulation.FastForwarding) : à ignorer côté effets.
    public event System.Action<Hit> Scored;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Best { get; private set; }
    public int BiggestLevel { get; private set; }

    void OnEnable()
    {
        _fusion = GumoriDropper.Find<GumoriFusion>(this);
        _fusion.Merged += OnMerged;
        Score = 0;
        Combo = 0;
        BiggestLevel = 0;
        _lastMergeStep = int.MinValue;
        _popups.Clear();
    }

    void OnDisable()
    {
        if (_fusion != null) _fusion.Merged -= OnMerged;
    }

    void OnMerged(Gumori merged, int level, Vector2 position)
    {
        int step = LevelSimulation.Step;
        int window = Mathf.RoundToInt(comboWindow / Time.fixedDeltaTime);
        Combo = _lastMergeStep != int.MinValue && step - _lastMergeStep <= window ? Combo + 1 : 1;
        _lastMergeStep = step;

        int points = pointsPerMerge * level * (level + 1) / 2;
        points = points * (100 + (Combo - 1) * comboPercent) / 100;
        Score += points;
        if (Score > Best) Best = Score;
        bool record = level > BiggestLevel;
        if (record) BiggestLevel = level;
        Scored?.Invoke(new Hit { blob = merged, level = level, points = points, combo = Combo, position = position, record = record });

        if (LevelSimulation.FastForwarding) return;
        _popups.Add(new Popup
        {
            position = position + Vector2.up * (0.5f * merged.diameter + 0.4f), // au-dessus du nouveau gumori : la fusion reste visible
            text = Combo > 1 ? $"+{points}  ×{(100 + (Combo - 1) * comboPercent) / 100f:0.#}" : $"+{points}",
            color = merged.debugColor,
            born = Time.time,
            size = 18f + 3f * Mathf.Min(level, 10) + 4f * Mathf.Min(Combo - 1, 5)
        });
    }

    // Points donnés ou retirés hors fusion (bombe absorbée, gumori sorti de la boîte), pendant le pas ; jamais sous 0.
    public void AddPoints(int points, Vector2 position, Color color)
    {
        Score = Mathf.Max(0, Score + points);
        if (Score > Best) Best = Score;
        if (LevelSimulation.FastForwarding) return;
        _popups.Add(new Popup
        {
            position = position, text = points >= 0 ? $"+{points}" : $"−{-points}",
            color = points >= 0 ? color : new Color(1f, 0.35f, 0.35f), born = Time.time, size = 26f
        });
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        _scoreStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        _popupStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        // Score qui défile jusqu'à sa valeur, avec un rebond à chaque gain.
        if (Event.current.type == EventType.Repaint)
        {
            float dt = Time.unscaledDeltaTime;
            _shownScore = Score < _shownScore ? Score : Mathf.MoveTowards(_shownScore, Score, Mathf.Max(30f, (Score - _shownScore) * 6f) * dt);
            if (Score > _lastShown) _bump = 1f;
            _lastShown = Score;
            _bump = Mathf.Max(0f, _bump - dt * 3f);
        }
        float bump = 1f + 0.25f * _bump * _bump;
        var area = new Rect(Screen.width - 330f, 10f, 320f, 44f);
        var saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(bump, bump), new Vector2(area.xMax, area.center.y));
        GumoriUi.Outlined(area, Mathf.RoundToInt(_shownScore).ToString("N0"), _scoreStyle, Color.Lerp(Color.white, new Color(1f, 0.9f, 0.4f), _bump), 1f, 2.5f);
        GUI.matrix = saved;
        _smallStyle ??= new GUIStyle(_scoreStyle) { fontSize = 14, fontStyle = FontStyle.Normal };
        _smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(Screen.width - 330f, 54f, 320f, 22f), $"Meilleur : {Best:N0}   Plus gros : niveau {BiggestLevel}", _smallStyle);

        var cam = Camera.main;
        if (cam == null) return;
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var p = _popups[i];
            float age = (Time.time - p.born) / popupTime;
            if (age >= 1f || age < 0f) { _popups.RemoveAt(i); continue; }
            Vector3 s = cam.WorldToScreenPoint(p.position + Vector2.up * (1.5f * (1f - (1f - age) * (1f - age))));
            float pop = age < 0.1f ? Mathf.Lerp(0.3f, 1.35f, age / 0.1f) : 1f + 0.35f * Mathf.Exp(-(age - 0.1f) * 12f);
            _popupStyle.fontSize = Mathf.RoundToInt(p.size * pop);
            var rect = new Rect(s.x - 150f, Screen.height - s.y - 30f, 300f, 60f);
            GumoriUi.Outlined(rect, p.text, _popupStyle, Color.Lerp(p.color, Color.white, 0.25f), 1f - age * age * age, 2f);
        }
    }
}
