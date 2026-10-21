using System.Collections.Generic;
using UnityEngine;

// Points : chaque fusion rapporte selon le niveau créé (nombres triangulaires, comme Suika : 1, 3, 6, 10, 15… × 'pointsPerMerge'),
// et les fusions qui s'enchaînent vite (réaction en chaîne) sont multipliées. Compté pendant le pas (événement de BlobFusion,
// temps en pas de simulation) : même partie = même score, au bit près. Affichage seulement dans OnGUI.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobFusion))]
public class SlimeScore : MonoBehaviour, ILevelPhysics
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

    BlobFusion _fusion;
    int _lastMergeStep = int.MinValue;
    readonly List<Popup> _popups = new List<Popup>();
    GUIStyle _scoreStyle, _smallStyle, _popupStyle;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Best { get; private set; }
    public int BiggestLevel { get; private set; }

    void OnEnable()
    {
        _fusion = GetComponent<BlobFusion>();
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

    void OnMerged(BLOB merged, int level, Vector2 position)
    {
        int step = LevelSimulation.Step;
        int window = Mathf.RoundToInt(comboWindow / Time.fixedDeltaTime);
        Combo = _lastMergeStep != int.MinValue && step - _lastMergeStep <= window ? Combo + 1 : 1;
        _lastMergeStep = step;

        int points = pointsPerMerge * level * (level + 1) / 2;
        points = points * (100 + (Combo - 1) * comboPercent) / 100;
        Score += points;
        if (Score > Best) Best = Score;
        if (level > BiggestLevel) BiggestLevel = level;

        if (LevelSimulation.FastForwarding) return;
        _popups.Add(new Popup
        {
            position = position,
            text = Combo > 1 ? $"+{points}  ×{(100 + (Combo - 1) * comboPercent) / 100f:0.#}" : $"+{points}",
            color = merged.debugColor,
            born = Time.time,
            size = 18f + 3f * Mathf.Min(level, 10) + 4f * Mathf.Min(Combo - 1, 5)
        });
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        _scoreStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        _popupStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        var area = new Rect(Screen.width - 330f, 10f, 320f, 44f);
        GUI.Label(area, Score.ToString("N0"), _scoreStyle);
        _smallStyle ??= new GUIStyle(_scoreStyle) { fontSize = 14, fontStyle = FontStyle.Normal };
        GUI.Label(new Rect(Screen.width - 330f, 54f, 320f, 22f), $"Meilleur : {Best:N0}   Plus gros : niveau {BiggestLevel}", _smallStyle);

        var cam = Camera.main;
        if (cam == null) return;
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var p = _popups[i];
            float age = (Time.time - p.born) / popupTime;
            if (age >= 1f || age < 0f) { _popups.RemoveAt(i); continue; }
            Vector3 s = cam.WorldToScreenPoint(p.position + Vector2.up * (1.5f * age));
            _popupStyle.fontSize = Mathf.RoundToInt(p.size * (1f + 0.3f * Mathf.Max(0f, 1f - 6f * age)));
            var color = p.color;
            color.a = 1f - age * age;
            var shadow = new Color(0f, 0f, 0f, color.a * 0.7f);
            var rect = new Rect(s.x - 150f, Screen.height - s.y - 30f, 300f, 60f);
            _popupStyle.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), p.text, _popupStyle);
            _popupStyle.normal.textColor = color;
            GUI.Label(rect, p.text, _popupStyle);
        }
    }
}
