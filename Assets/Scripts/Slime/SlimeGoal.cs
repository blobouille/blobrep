using UnityEngine;

// Niveau à objectif : un nombre de coups (lâchers) et un score à atteindre, qui vont ensemble. Score atteint = plus de lâcher,
// la chaîne en cours se finit, puis niveau réussi (les slimes sont retirés : SlimeFinale les fait se diviser et s'envoler). Coups épuisés sans l'atteindre : on
// attend que la pile se calme (plus de fusion pendant 'settleTime'), puis raté. Tout est compté pendant le pas (lâchers et
// fusions arrivent dans LevelTick) : même partie = même résultat. Désactivé = partie libre, sans limite.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobDropper), typeof(SlimeScore))]
public class SlimeGoal : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Objectif")]
    [Tooltip("Coché : nombre de coups limité et score à atteindre. Décoché : partie libre (lâchers sans limite).\n" +
             "Changer en Play s'applique au prochain redémarrage (R).")]
    public bool useGoal = true;
    [Tooltip("Nombre de slimes qu'on peut lâcher dans le niveau.")]
    [Min(1)] public int maxDrops = 30;
    [Tooltip("Points à atteindre pour réussir le niveau. Calculé (et réécrit) quand 'autoTarget' est coché.")]
    [Min(1)] public int targetScore = 3000;

    public enum Difficulty { Easy, Medium, Hard }

    [Header("Difficulté (objectif calculé)")]
    [Tooltip("Coché : l'objectif est calculé à partir des coups et de la difficulté. Base = points SANS AUCUNE CHAÎNE en fusionnant\n" +
             "parfaitement les slimes de la suite (graine du lâcher), sur les coups nécessaires ; la difficulté donne des coups en\n" +
             "plus. Décoché : 'targetScore' tel quel (l'inspecteur montre alors sa difficulté).")]
    public bool autoTarget = true;
    public Difficulty difficulty = Difficulty.Medium;
    [Tooltip("Coups en plus des coups strictement nécessaires, par difficulté (0,33 = 33 % de plus). Facile.")]
    [Min(0f)] public float easyExtraMoves = 0.33f;
    [Tooltip("Coups en plus des coups strictement nécessaires (0,165 = 16,5 % de plus). Moyen.")]
    [Min(0f)] public float mediumExtraMoves = 0.165f;
    [Tooltip("Coups en plus des coups strictement nécessaires (0,05 = 5 % de plus). Difficile.")]
    [Min(0f)] public float hardExtraMoves = 0.05f;
    [Tooltip("Part de l'objectif à aller chercher avec des chaînes : 0 = atteignable sans aucune chaîne (les chaînes sont du\n" +
             "bonus) ; 0,2 = objectif 20 % au-dessus des points sans chaîne.")]
    [Range(0f, 1f)] public float comboShare = 0f;
    [Tooltip("Après le dernier coup : temps (s) sans fusion avant de juger le niveau (les chaînes en cours comptent encore).")]
    [Min(0.1f)] public float settleTime = 2.5f;
    [Tooltip("Niveau réussi : temps (s) laissé à la fête (slimes qui se divisent et s'envolent) avant le panneau de fin.")]
    [Min(0f)] public float celebrationTime = 3.5f;

    public enum State { Playing, Won, Lost }

    BlobDropper _dropper;
    SlimeScore _score;
    BlobFusion _fusion;
    bool _active;
    int _lastEventStep, _lastMergeStep, _leftAtWin;
    float _reachedTime = -10f, _endTime;
    GUIStyle _big, _small, _title;

    int _target;
    readonly System.Collections.Generic.List<int> _sequence = new System.Collections.Generic.List<int>();

    public bool Active => _active;
    // Objectif de la partie en cours (calculé au départ si 'autoTarget').
    public int Target => _target;

    public float ExtraMoves => ExtraMovesFor(difficulty);

    // Points SANS CHAÎNE des 'count' premiers slimes de la suite, fusionnés parfaitement : chaque paire de même niveau fusionne
    // (comme une retenue en binaire, l'ordre ne change pas le total), jusqu'au niveau max. Fusion vers le niveau n = points × n(n+1)/2.
    public static int PerfectPoints(System.Collections.Generic.List<int> levels, int count, int maxLevel, int pointsPerMerge)
    {
        var counts = new int[maxLevel + 1];
        for (int i = 0; i < count && i < levels.Count; i++) counts[Mathf.Min(levels[i], maxLevel)]++;
        int points = 0;
        for (int level = 0; level < maxLevel; level++)
        {
            int merges = counts[level] / 2;
            counts[level + 1] += merges;
            int created = level + 1;
            points += merges * pointsPerMerge * created * (created + 1) / 2;
        }
        return points;
    }

    bool Prepare()
    {
        if (_dropper == null) _dropper = GetComponent<BlobDropper>();
        if (_score == null) _score = GetComponent<SlimeScore>();
        if (_fusion == null) _fusion = GetComponent<BlobFusion>();
        return _dropper != null && _score != null && _fusion != null && _dropper.template != null;
    }

    // Objectif calculé : points sans chaîne avec tous les coups, divisés par (1 + coups en plus), + part des chaînes. Compté en
    // points et pas en coups : les points parfaits montent par paliers (un coup de plus ne complète pas toujours une fusion),
    // en coups deux difficultés tombaient sur le même objectif.
    public int ComputeTarget()
    {
        if (!Prepare()) return targetScore;
        _dropper.PredictSequence(maxDrops, _sequence);
        return AutoTarget(_sequence, maxDrops, _fusion.MaxLevel, _score.pointsPerMerge, ExtraMoves, comboShare);
    }

    // Difficulté d'un objectif : nombre de coups minimum pour l'atteindre sans chaîne (avec la part des chaînes), 0 si
    // impossible même avec tous les coups ; marge = coups donnés / coups nécessaires − 1.
    public int MovesNeeded(int target)
    {
        if (!Prepare()) return 0;
        _dropper.PredictSequence(maxDrops, _sequence);
        return MovesNeeded(_sequence, maxDrops, target, _fusion.MaxLevel, _score.pointsPerMerge, comboShare);
    }

    // Mêmes calculs sans réservoir (inspecteur de la campagne), sur la suite prédite ('sequence', au moins 'maxDrops' slimes).
    public static int AutoTarget(System.Collections.Generic.List<int> sequence, int maxDrops, int maxLevel, int pointsPerMerge, float extraMoves, float comboShare)
        => Mathf.Max(pointsPerMerge, Mathf.RoundToInt(PerfectPoints(sequence, maxDrops, maxLevel, pointsPerMerge) / (1f + extraMoves) * (1f + comboShare)));

    public static int MovesNeeded(System.Collections.Generic.List<int> sequence, int maxDrops, int target, int maxLevel, int pointsPerMerge, float comboShare)
    {
        for (int k = 1; k <= maxDrops; k++)
            if (PerfectPoints(sequence, k, maxLevel, pointsPerMerge) * (1f + comboShare) >= target) return k;
        return 0;
    }

    public float ExtraMovesFor(Difficulty d) => d == Difficulty.Easy ? easyExtraMoves : d == Difficulty.Hard ? hardExtraMoves : mediumExtraMoves;

    void OnValidate()
    {
        if (autoTarget) targetScore = ComputeTarget();
    }
    public State Result { get; private set; }
    public bool Reached { get; private set; }
    // Coups restants au moment où l'objectif a été atteint (étoiles).
    public int LeftAtWin => _leftAtWin;
    // Temps (s) depuis l'apparition du panneau de fin ; négatif tant qu'il n'est pas affiché.
    public float PanelAge => !_active || Result == State.Playing ? -1f : Time.time - _endTime - (Result == State.Won ? celebrationTime : 0f);

    // Fin du niveau (pendant LevelTick ; aussi pendant le rejeu accéléré : à ignorer côté effets). Réussi : appelé AVANT de
    // retirer les slimes (on peut encore lire leur position et leur niveau).
    public event System.Action<State> Finished;

    void OnEnable()
    {
        _dropper = GetComponent<BlobDropper>();
        _score = GetComponent<SlimeScore>();
        _fusion = GetComponent<BlobFusion>();
        _active = useGoal;
        _target = autoTarget ? ComputeTarget() : targetScore;
        _dropper.dropLimit = _active ? maxDrops : 0;
        _dropper.Dropped += OnDropped;
        if (_fusion != null) _fusion.Merged += OnMerged;
        Result = State.Playing;
        Reached = false;
        _lastEventStep = _lastMergeStep = 0;
        _reachedTime = -10f;
    }

    void OnDisable()
    {
        if (_dropper != null) { _dropper.Dropped -= OnDropped; _dropper.dropLimit = 0; }
        if (_fusion != null) _fusion.Merged -= OnMerged;
    }

    void OnDropped(BLOB blob, int level) => _lastEventStep = LevelSimulation.Step;
    void OnMerged(BLOB blob, int level, Vector2 position) => _lastEventStep = _lastMergeStep = LevelSimulation.Step;

    public void LevelTick()
    {
        if (!_active || Result != State.Playing) return;
        if (!Reached && _score.Score >= _target)
        {
            Reached = true;
            _reachedTime = Time.time;
            _leftAtWin = _dropper.DropsLeft;
            _dropper.dropLimit = Mathf.Max(1, _dropper.Drops);
        }
        if (Reached)
        {
            // La chaîne en cours se finit (ses points comptent) : plus de fusion pendant une fenêtre de chaîne + une fusion.
            float wait = _score.comboWindow + (_fusion != null ? _fusion.mergeTime : 0f);
            if (LevelSimulation.Step - _lastMergeStep < Mathf.RoundToInt(wait / Time.fixedDeltaTime)) return;
            _endTime = Time.time;
            Result = State.Won;
            Finished?.Invoke(Result);
            _dropper.RemoveAll();
            return;
        }
        if (_dropper.DropsLeft > 0) return;
        if (LevelSimulation.Step - _lastEventStep < Mathf.RoundToInt(settleTime / Time.fixedDeltaTime)) return;
        Result = State.Lost;
        _endTime = Time.time;
        Finished?.Invoke(Result);
    }

    void OnGUI()
    {
        if (!Application.isPlaying || !_active) return;
        _big ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _small ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        float cx = Screen.width * 0.5f;
        int left = Result == State.Won ? _leftAtWin : _dropper.DropsLeft;
        Color movesColor = left <= 3 && Result == State.Playing ? Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : Color.white;
        Outlined(new Rect(cx - 150f, 6f, 300f, 36f), left.ToString(), _big, movesColor, 1f);
        Outlined(new Rect(cx - 150f, 38f, 300f, 20f), left > 1 ? "coups restants" : "coup restant", _small, new Color(1f, 1f, 1f, 0.8f), 1f);

        // Barre de progression vers l'objectif.
        float progress = Mathf.Clamp01((float)_score.Score / Mathf.Max(1, _target));
        var bar = new Rect(cx - 130f, 64f, 260f, 14f);
        var saved = GUI.color;
        GUI.color = new Color(0.05f, 0.02f, 0.1f, 0.75f);
        GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
        GUI.color = Reached ? new Color(1f, 0.85f, 0.3f) : new Color(0.45f, 0.9f, 1f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
        GUI.color = saved;
        Outlined(new Rect(cx - 150f, 80f, 300f, 20f), $"{_score.Score:N0} / {_target:N0}", _small, Reached ? new Color(1f, 0.9f, 0.4f) : Color.white, 1f);

        float sinceReached = Time.time - _reachedTime;
        if (Reached && (Result == State.Playing || Time.time - _endTime < celebrationTime))
        {
            float pop = sinceReached < 0.15f ? Mathf.Lerp(0.4f, 1.3f, sinceReached / 0.15f) : 1f + 0.3f * Mathf.Exp(-(sinceReached - 0.15f) * 8f);
            pop *= 1f + 0.04f * Mathf.Sin(sinceReached * 9f);
            _title.fontSize = Mathf.RoundToInt(46f * pop);
            Outlined(new Rect(cx - 300f, Screen.height * 0.3f, 600f, 80f), "Objectif atteint !", _title, new Color(1f, 0.9f, 0.4f), Result == State.Playing ? 1f : Mathf.Clamp01((celebrationTime - (Time.time - _endTime)) * 3f));
        }

        if (Result == State.Playing) return;
        float t = Time.time - _endTime - (Result == State.Won ? celebrationTime : 0f);
        if (t < 0f) return;
        GUI.color = new Color(0.03f, 0.01f, 0.08f, Mathf.Clamp01(t * 3f) * 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = saved;
        float scale = t < 0.2f ? Mathf.Lerp(0.3f, 1.15f, t / 0.2f) : 1f + 0.15f * Mathf.Exp(-(t - 0.2f) * 8f);
        _title.fontSize = Mathf.RoundToInt(56f * scale);
        bool won = Result == State.Won;
        float cy = Screen.height * 0.4f;
        Outlined(new Rect(cx - 300f, cy - 50f, 600f, 90f), won ? "Niveau réussi !" : "Raté…", _title, won ? new Color(1f, 0.88f, 0.35f) : new Color(1f, 0.55f, 0.6f), 1f);
        _title.fontSize = 24;
        Outlined(new Rect(cx - 300f, cy + 40f, 600f, 40f), $"{_score.Score:N0} points  (objectif {_target:N0})", _title, Color.white, Mathf.Clamp01((t - 0.3f) * 3f));
        Outlined(new Rect(cx - 300f, cy + 80f, 600f, 30f), "R = rejouer", _small, new Color(1f, 1f, 1f, 0.8f), Mathf.Clamp01((t - 0.6f) * 3f));
    }

    static void Outlined(Rect r, string text, GUIStyle style, Color color, float alpha)
    {
        style.normal.textColor = new Color(0.08f, 0.04f, 0.12f, alpha * 0.9f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * 2f, r.y + Mathf.Sin(a) * 2f + 1f, r.width, r.height), text, style);
        }
        color.a *= alpha;
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }
}
