using System.Collections.Generic;
using UnityEngine;

// Bombes (Space, niveaux) : boules rigides de la couleur d'un niveau, avec un compte à rebours en TIRS. Posées sur le terrain
// au départ, ou arrivées dans la réserve à un rang donné (on les lance comme un gumori ; leur compte ne tourne qu'une fois
// lancées). À 0, elles explosent 'explodeDelay' s après le tir : un gumori de LEUR couleur qui les touche les absorbe (points en
// plus), sinon l'onde pousse les gumori et fêle la paroi cassable (CurveCollider2D.breakable) ; un morceau qui cède ouvre un
// trou. Grosse bombe = compte plus long, onde plus large et plus forte. Un gumori sorti de la boîte est retiré et coûte ses
// points. Tout se passe pendant les pas (tirs, fusions, LevelTick) : même partie = mêmes explosions.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Mécaniques/GumoriBombs")]
public class GumoriBombs : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [System.Serializable]
    public class Placed
    {
        [Tooltip("Position de départ (m), relative à cet objet.")]
        public Vector2 position;
        [Tooltip("Niveau = couleur à amener pour l'absorber, et taille / force de l'explosion.")]
        [Min(0)] public int level = 2;
        [Tooltip("Tirs avant l'explosion (0 = selon le niveau : réglages du compte à rebours).")]
        [Min(0)] public int fuse = 0;
    }

    [System.Serializable]
    public class InRack
    {
        [Tooltip("Rang d'arrivée dans la réserve (0 = premier gumori de la réserve au départ) : cette arrivée est une bombe.")]
        [Min(0)] public int arrival = 4;
        [Min(0)] public int level = 2;
        [Tooltip("Tirs avant l'explosion, comptés une fois lancée (0 = selon le niveau).")]
        [Min(0)] public int fuse = 0;
    }

    [Header("Bombes")]
    [Tooltip("Lâcher (liste des gumori). Vide = le premier de la scène.")]
    public GumoriDropper dropper;
    [Tooltip("Bombes posées sur le terrain au départ.")]
    public List<Placed> placed = new List<Placed>();
    [Tooltip("Bombes qui arrivent dans la réserve, à la place d'un gumori.")]
    public List<InRack> inRack = new List<InRack>();

    [Header("Compte à rebours")]
    [Tooltip("Tirs avant l'explosion d'une bombe de niveau 0 (compte par défaut).")]
    [Min(1)] public int baseFuse = 3;
    [Tooltip("Tirs en plus par niveau : les grosses bombes mettent plus longtemps (et il faut fabriquer leur couleur).")]
    [Min(0f)] public float fusePerLevel = 1f;
    [Tooltip("Délai (s) entre le tir qui amène le compte à 0 et l'explosion : le gumori tiré a le temps d'arriver.")]
    [Min(0f)] public float explodeDelay = 1.5f;

    [Header("Corps")]
    [Tooltip("Diamètre de la bombe / diamètre du gumori de son niveau.")]
    [Range(0.4f, 1.2f)] public float sizeScale = 0.85f;
    [Tooltip("Raideur de la bombe (× joints du gumori modèle) : elle reste ronde.")]
    [Min(1f)] public float stiffness = 6f;

    [Header("Absorption")]
    [Tooltip("Écart max (m) entre la bombe et un gumori de sa couleur, à l'explosion, pour qu'il l'absorbe.")]
    [Min(0f)] public float reach = 0.3f;
    [Tooltip("Points d'une absorption, × points d'une fusion vers le niveau de la bombe + 1.")]
    [Min(0f)] public float absorbBonus = 2f;

    [Header("Explosion")]
    [Tooltip("Rayon de l'onde (m) d'une bombe de niveau 0, depuis son centre.")]
    [Min(0f)] public float blastRadius = 1.8f;
    [Tooltip("Rayon en plus par niveau (m).")]
    [Min(0f)] public float blastRadiusPerLevel = 0.3f;
    [Tooltip("Vitesse donnée par l'onde au plus près (km/h), diminue jusqu'à 0 au bord de l'onde.")]
    [SpeedKmh] [Min(0f)] public float blastSpeed = 3f;
    [Tooltip("Vitesse en plus par niveau (km/h).")]
    [SpeedKmh] [Min(0f)] public float blastSpeedPerLevel = 0.5f;
    [Tooltip("Fêlures données à la paroi : 1, + 1 tous les … niveaux de la bombe.")]
    [Min(1)] public int damageEvery = 4;

    [Header("Fuites")]
    [Tooltip("Paroi de la boîte (cassable). Vide = la courbe du lâcher. Un gumori dont le centre sort de son cadre est retiré.")]
    public CurveCollider2D box;
    [Tooltip("Points perdus par gumori sorti, × points d'une fusion vers son niveau + 1.")]
    [Min(0f)] public float leakPenalty = 1f;

    public class Bomb
    {
        public Gumori blob;
        public int level, fuse;
        public bool armed;
        public int explodeStep = -1;
    }

    // Pendant le pas, aussi pendant le rejeu accéléré (à ignorer côté effets).
    public event System.Action<Bomb> Ticked;                    // compte baissé d'un tir
    public event System.Action<Bomb, Gumori, Vector2> Absorbed; // bombe, gumori qui l'absorbe, lieu
    public event System.Action<Bomb, Vector2, float> Exploded;  // bombe, centre, rayon
    public event System.Action<Gumori, int> Leaked;             // gumori sorti, points perdus

    readonly List<Bomb> _bombs = new List<Bomb>();
    GumoriFusion _fusion;
    GumoriLauncher _launcher;
    GumoriScore _score;
    bool _started;
    Rect _frame;

    public IReadOnlyList<Bomb> Bombs => _bombs;

    void Prepare()
    {
        if (dropper == null) dropper = FindAnyObjectByType<GumoriDropper>();
        if (dropper == null) return;
        if (_fusion == null) _fusion = dropper.Part<GumoriFusion>();
        if (_launcher == null) _launcher = dropper.Part<GumoriLauncher>();
        if (_score == null) _score = dropper.Part<GumoriScore>();
        if (box == null && dropper.reservoir != null) box = dropper.reservoir.GetComponent<CurveCollider2D>();
    }

    void OnEnable()
    {
        Prepare();
        _bombs.Clear();
        _started = false;
        if (!Application.isPlaying) return;
        if (_launcher != null) _launcher.Launched += OnLaunched;
    }

    void OnDisable()
    {
        if (_launcher != null) _launcher.Launched -= OnLaunched;
        _bombs.Clear();
    }

    public int Fuse(int level, int custom) => custom > 0 ? custom : baseFuse + Mathf.RoundToInt(fusePerLevel * level);
    public float BombDiameter(int level) => sizeScale * (_fusion != null ? _fusion.LevelDiameter(level) : 1f);
    public float BombRadius(int level) => 0.5f * BombDiameter(level);
    public float BlastRadius(int level) => blastRadius + blastRadiusPerLevel * level;
    int FusionPoints(int level) => (_score != null ? _score.pointsPerMerge : 10) * (level + 1) * (level + 2) / 2;

    // Réserve (GumoriLauncher.Arrive) : niveau de la bombe à ce rang d'arrivée, -1 = un gumori normal.
    public int RackBombLevel(int arrival)
    {
        for (int i = 0; i < inRack.Count; i++)
            if (inRack[i].arrival == arrival) return Clamp(inRack[i].level);
        return -1;
    }

    public Gumori SpawnRackBomb(int arrival, Vector2 at)
    {
        for (int i = 0; i < inRack.Count; i++)
            if (inRack[i].arrival == arrival) return Spawn(at, Clamp(inRack[i].level), inRack[i].fuse, false).blob;
        return null;
    }

    int Clamp(int level) => _fusion != null ? Mathf.Clamp(level, 0, _fusion.MaxLevel) : Mathf.Max(0, level);

    Bomb Spawn(Vector2 at, int level, int fuse, bool armed)
    {
        var blob = dropper.Spawn(at, BombDiameter(level), _fusion != null ? _fusion.LevelSides(0) : 16,
            _fusion != null ? _fusion.LevelColor(level) : Color.white, stiffness);
        blob.isBomb = true;
        var bomb = new Bomb { blob = blob, level = level, fuse = Fuse(level, fuse), armed = armed };
        _bombs.Add(bomb);
        return bomb;
    }

    // Tir (pendant le pas) : une bombe lancée s'arme ; les autres bombes armées perdent un tir.
    void OnLaunched(Gumori shot, Vector2 velocity)
    {
        for (int i = 0; i < _bombs.Count; i++)
        {
            var b = _bombs[i];
            if (b.blob == shot) { b.armed = true; continue; }
            if (!b.armed || b.explodeStep >= 0) continue;
            b.fuse = Mathf.Max(0, b.fuse - 1);
            if (b.fuse == 0) b.explodeStep = LevelSimulation.Step + Mathf.RoundToInt(explodeDelay / Time.fixedDeltaTime);
            Ticked?.Invoke(b);
        }
    }

    public void LevelTick()
    {
        if (dropper == null) return;
        if (!_started)
        {
            _started = true;
            MeasureBox();
            for (int i = 0; i < placed.Count; i++)
                Spawn((Vector2)transform.position + placed[i].position, Clamp(placed[i].level), placed[i].fuse, true);
        }
        int step = LevelSimulation.Step;
        for (int i = 0; i < _bombs.Count; i++)
        {
            var b = _bombs[i];
            if (!GumoriDropper.IsInPlay(b.blob)) { _bombs.RemoveAt(i--); continue; }
            if (b.explodeStep < 0 || step < b.explodeStep) continue;
            Detonate(b);
            _bombs.RemoveAt(i--);
        }
        Leaks();
    }

    void Detonate(Bomb bomb)
    {
        Vector2 at = bomb.blob.Center;
        var absorber = FindAbsorber(bomb);
        if (absorber != null)
        {
            if (_score != null) _score.AddPoints(Mathf.RoundToInt(absorbBonus * FusionPoints(bomb.level)), absorber.Center + Vector2.up * (0.5f * absorber.diameter + 0.4f), absorber.debugColor);
            Absorbed?.Invoke(bomb, absorber, at);
            dropper.Remove(bomb.blob);
            return;
        }
        float radius = BlastRadius(bomb.level), speed = blastSpeed + blastSpeedPerLevel * bomb.level;
        var list = dropper.InPlay;
        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            if (g == bomb.blob) continue;
            Vector2 d = g.Center - at;
            float dist = d.magnitude;
            if (dist >= radius) continue;
            Vector2 dir = dist > 1e-4f ? d / dist : Vector2.up;
            g.AddVelocity(dir * (speed * (1f - dist / radius)));
        }
        int damage = 1 + bomb.level / Mathf.Max(1, damageEvery);
        var walls = CurveCollider2D.Breakables;
        for (int i = 0; i < walls.Count; i++) walls[i].Damage(at, radius, damage);
        Exploded?.Invoke(bomb, at, radius);
        dropper.Remove(bomb.blob);
    }

    // Premier gumori (ordre de création) de la couleur de la bombe qui la touche à 'reach' près.
    Gumori FindAbsorber(Bomb bomb)
    {
        if (_fusion == null) return null;
        var list = dropper.InPlay;
        Vector2 c = bomb.blob.Center;
        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            if (g.isBomb || _fusion.LevelOf(g) != bomb.level) continue;
            float limit = 0.5f * (g.diameter + bomb.blob.diameter) + reach + 0.5f;
            if ((g.Center - c).sqrMagnitude > limit * limit) continue;
            float touch = g.CapsuleRadius + bomb.blob.CapsuleRadius + reach;
            for (int p = 0; p < g.CapsuleCount; p++)
                for (int q = 0; q < bomb.blob.CapsuleCount; q++)
                    if ((g.Capsule(p).position - bomb.blob.Capsule(q).position).sqrMagnitude <= touch * touch) return g;
        }
        return null;
    }

    void MeasureBox()
    {
        _frame = default;
        if (box == null || box.WorldLine == null || box.WorldLine.Length == 0) return;
        var line = box.WorldLine;
        Vector2 min = line[0], max = line[0];
        for (int i = 1; i < line.Length; i++) { min = Vector2.Min(min, line[i]); max = Vector2.Max(max, line[i]); }
        _frame = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // Gumori dont le centre est sorti du cadre de la boîte (plus son rayon) : retiré, ses points perdus.
    void Leaks()
    {
        if (_frame.width <= 0f) return;
        var list = dropper.InPlay;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var g = list[i];
            Vector2 c = g.Center;
            float m = 0.5f * g.diameter + 0.2f;
            if (c.x > _frame.xMin - m && c.x < _frame.xMax + m && c.y > _frame.yMin - m && c.y < _frame.yMax + m) continue;
            int lost = 0;
            if (!g.isBomb && _fusion != null)
            {
                lost = Mathf.RoundToInt(leakPenalty * FusionPoints(_fusion.LevelOf(g)));
                if (_score != null && lost > 0) _score.AddPoints(-lost, ClampToFrame(c), g.debugColor);
            }
            Leaked?.Invoke(g, lost);
            dropper.Remove(g);
        }
    }

    Vector2 ClampToFrame(Vector2 p) => new Vector2(Mathf.Clamp(p.x, _frame.xMin + 0.5f, _frame.xMax - 0.5f), Mathf.Clamp(p.y, _frame.yMin + 0.5f, _frame.yMax - 0.5f));

    // Aperçu (hors Play) : bombe posée à sa taille et sa couleur, onde en pâle, compte à rebours.
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Prepare();
        for (int i = 0; i < placed.Count; i++)
        {
            var p = placed[i];
            Vector2 c = (Vector2)transform.position + p.position;
            Color col = _fusion != null ? _fusion.LevelColor(p.level) : Color.white;
            Gizmos.color = col;
            DrawCircle(c, BombRadius(p.level));
            DrawCircle(c, BombRadius(p.level) * 0.92f);
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.25f);
            DrawCircle(c, BlastRadius(p.level));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(c, $"  {Fuse(p.level, p.fuse)}", UnityEditor.EditorStyles.whiteLargeLabel);
#endif
        }
    }

    static void DrawCircle(Vector2 c, float r)
    {
        const int N = 32;
        Vector3 prev = c + new Vector2(r, 0f);
        for (int i = 1; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            Vector3 cur = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
