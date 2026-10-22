using System.Collections.Generic;
using UnityEngine;

// Niveau réussi : chaque slime se divise (la fusion à l'envers) jusqu'au plus petit, avec un éclat et un son à chaque
// division, et les plus petits s'envolent par le haut de l'écran. Purement visuel : la simulation a retiré les vrais slimes
// (SlimeGoal) ; ceux-ci sont dessinés par SlimeSkin, sans physique, avec une graine fixe pour le hasard.
[DisallowMultipleComponent]
public class SlimeFinale : MonoBehaviour
{
    [Header("Références (trouvées seules si vides)")]
    public SlimeGoal goal;
    public SlimeSkin skin;
    public SlimeFx fx;
    public SlimeSound sound;

    [Header("Divisions")]
    [Tooltip("Délai (s) avant la première division.")]
    [Min(0f)] public float startDelay = 0.2f;
    [Tooltip("Écart (s) entre les premières divisions des slimes, du plus haut au plus bas.")]
    [Min(0f)] public float stagger = 0.07f;
    [Tooltip("Temps (s) entre deux divisions d'un même slime.")]
    [Min(0.05f)] public float splitInterval = 0.3f;
    [Tooltip("Vitesse à laquelle les deux moitiés s'écartent, en km/h.")]
    [SpeedKmh] [Min(0f)] public float splitSpeed = 3.5f;
    [Tooltip("Vitesse vers le haut donnée à chaque division, en km/h.")]
    [SpeedKmh] [Min(0f)] public float launchSpeed = 5f;
    [Tooltip("Nombre max de slimes à l'écran : au-delà, ils ne se divisent plus et s'envolent tels quels.")]
    [Min(2)] public int maxPieces = 160;

    [Header("Envol")]
    [Tooltip("Gravité des slimes qui se divisent encore, en m/s² (vers le bas).")]
    [Min(0f)] public float gravity = 9f;
    [Tooltip("Poussée vers le haut des plus petits (qui ne se divisent plus), en m/s².")]
    [Min(0f)] public float lift = 22f;
    [Tooltip("Fond intérieur du réservoir (m) et haut de ses parois (m) : les slimes y rebondissent tant qu'ils sont dedans.")]
    public float floor = 0.2f;
    public float wallTop = 14f;

    class Piece
    {
        public SlimeSkin.VirtualSlime v;
        public Vector2 velocity;
        public float timer, age;
        public int generation;
    }

    readonly List<Piece> _pieces = new List<Piece>();
    readonly List<Piece> _born = new List<Piece>();
    readonly List<(Vector2 position, int level)> _start = new List<(Vector2, int)>();
    BlobDropper _dropper;
    BlobFusion _fusion;
    System.Random _rand = new System.Random(23);

    public bool Running => _pieces.Count > 0;

    void OnEnable()
    {
        if (goal == null) goal = FindAnyObjectByType<SlimeGoal>();
        if (skin == null) skin = FindAnyObjectByType<SlimeSkin>();
        if (fx == null) fx = FindAnyObjectByType<SlimeFx>();
        if (sound == null) sound = FindAnyObjectByType<SlimeSound>();
        if (goal != null)
        {
            goal.Finished += OnFinished;
            _dropper = goal.GetComponent<BlobDropper>();
            _fusion = goal.GetComponent<BlobFusion>();
        }
        if (skin != null) skin.DrawExtras += OnDraw;
        LevelRestart.Restarted += OnRestarted;
    }

    void OnDisable()
    {
        if (goal != null) goal.Finished -= OnFinished;
        if (skin != null) skin.DrawExtras -= OnDraw;
        LevelRestart.Restarted -= OnRestarted;
        _pieces.Clear();
    }

    void OnRestarted() => _pieces.Clear();

    float Rand(float a, float b) => a + (b - a) * (float)_rand.NextDouble();
    float Radius(int level) => _fusion != null ? 0.5f * _fusion.LevelDiameter(level) : 0.5f;

    // Pendant LevelTick, juste avant que les slimes soient retirés : on relève leur place et leur niveau.
    void OnFinished(SlimeGoal.State state)
    {
        if (state != SlimeGoal.State.Won || LevelSimulation.FastForwarding || _dropper == null || skin == null) return;
        _pieces.Clear();
        _start.Clear();
        var blobs = _dropper.Blobs;
        for (int i = 0; i < blobs.Count; i++)
        {
            var b = blobs[i];
            if (b == null || b.CapsuleCount == 0) continue;
            Vector2 c = Vector2.zero;
            for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position;
            _start.Add((c / b.CapsuleCount, _fusion != null ? _fusion.LevelOf(b) : 0));
        }
        _start.Sort((x, y) => y.position.y.CompareTo(x.position.y));
        for (int i = 0; i < _start.Count; i++)
        {
            var (p, level) = _start[i];
            var v = skin.NewVirtual(level, Radius(level));
            v.center = p;
            _pieces.Add(new Piece { v = v, timer = startDelay + i * stagger });
        }
        if (fx != null) fx.Celebrate();
        if (sound != null) sound.PlayFanfare();
    }

    void OnDraw(float dt)
    {
        if (_pieces.Count == 0) return;
        var cam = Camera.main;
        float top = cam != null ? cam.transform.position.y + cam.orthographicSize : 30f;
        float half = _dropper != null ? _dropper.halfWidth : 4f;
        _born.Clear();
        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            var p = _pieces[i];
            var v = p.v;
            p.age += dt;
            p.timer -= dt;
            bool canSplit = v.level > 0 && _pieces.Count + _born.Count < maxPieces;
            if (canSplit && p.timer <= 0f)
            {
                Split(p);
                _pieces.RemoveAt(i);
                continue;
            }

            float accel = canSplit ? -gravity : lift;
            p.velocity.y += accel * dt;
            p.velocity *= Mathf.Exp(-0.6f * dt);
            v.center += p.velocity * dt;
            float r = v.radius;
            if (v.center.y - r < wallTop && Mathf.Abs(v.center.x) < half + r)
            {
                if (Mathf.Abs(v.center.x) > half - r)
                {
                    v.center.x = Mathf.Sign(v.center.x) * (half - r);
                    p.velocity.x = -0.5f * p.velocity.x;
                }
                if (v.center.y < floor + r)
                {
                    v.center.y = floor + r;
                    if (p.velocity.y < 0f) p.velocity.y = -0.4f * p.velocity.y;
                }
            }
            v.scale = p.age < 0.35f ? ElasticOut(p.age / 0.35f) * 0.4f + 0.6f : 1f;
            if (v.center.y - r > top) { _pieces.RemoveAt(i); continue; }
        }
        _pieces.AddRange(_born);
        for (int i = 0; i < _pieces.Count; i++) skin.DrawVirtual(_pieces[i].v, dt);
    }

    // Deux slimes du niveau d'en dessous, côte à côte sur un axe presque horizontal, qui s'écartent et montent.
    void Split(Piece p)
    {
        var v = p.v;
        int level = v.level - 1;
        float r1 = Radius(level);
        float a = Rand(-0.5f, 0.5f);
        Vector2 axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        for (int s = -1; s <= 1; s += 2)
        {
            var child = skin.NewVirtual(level, r1);
            child.center = v.center + axis * (s * 0.6f * r1);
            child.scale = 0.6f;
            _born.Add(new Piece
            {
                v = child,
                velocity = 0.6f * p.velocity + axis * (s * splitSpeed * Rand(0.8f, 1.2f)) + Vector2.up * (launchSpeed * Rand(0.7f, 1.1f)),
                timer = splitInterval * Rand(0.85f, 1.15f),
                generation = p.generation + 1
            });
        }
        if (fx != null) fx.Split(v.center, v.level, v.radius);
        if (sound != null) sound.Split(v.level, p.generation, v.center.x);
    }

    static float ElasticOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * 10f * Mathf.PI / 3f);
    }
}
