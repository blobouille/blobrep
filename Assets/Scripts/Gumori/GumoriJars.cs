using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Bocaux (Space, niveaux) : un gumori enfermé dans une bulle de verre fixe. Une fusion qui a lieu contre le verre (une capsule du
// gumori né à moins de 'reach' du verre) le fêle ; au bout de 'hits' fêlures il se brise et libère son gumori, un vrai, qui
// rejoint la partie au pas suivant. Le verre est un cercle statique : les gumori s'y posent et s'y écrasent.
// Tout se passe pendant les pas (fusions de GumoriFusion, libération dans LevelTick) : même partie = mêmes bocaux brisés.
[ExecuteAlways]
[DisallowMultipleComponent]
public class GumoriJars : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [System.Serializable]
    public class Jar
    {
        [Tooltip("Centre du bocal, en m, relatif à cet objet.")]
        public Vector2 position;
        [Tooltip("Niveau du gumori enfermé (taille, design).")]
        [Min(0)] public int level = 3;
        [Tooltip("Nombre de fusions contre le verre pour le briser (1 = la première le brise).")]
        [Range(1, 3)] public int hits = 1;
    }

    [Header("Bocaux")]
    [Tooltip("Lâcher (liste des gumori, fusion). Vide = le premier de la scène.")]
    public GumoriDropper dropper;
    public List<Jar> jars = new List<Jar>();
    [Tooltip("Épaisseur du verre autour du gumori enfermé (m) : rayon du bocal = rayon du gumori + épaisseur.")]
    [Min(0.02f)] public float glass = 0.16f;
    [Tooltip("Distance (m) entre le verre et le gumori né d'une fusion pour que la fusion fêle le bocal.")]
    [Min(0f)] public float reach = 0.45f;
    [Tooltip("Friction du verre (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.4f;
    [Tooltip("Rebond du verre : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0f)] public float bounciness = 0.1f;

    public struct State
    {
        public Vector2 center;
        public float radius;
        public int level, hitsLeft, hits;
        public bool broken;
    }

    // Fêlé (indice du bocal, fêlures restantes) et brisé (indice, gumori libéré) : pendant le pas, aussi pendant le rejeu
    // accéléré (à ignorer côté effets).
    public event System.Action<int, int> Cracked;
    public event System.Action<int, Gumori> Freed;

    readonly List<State> _states = new List<State>();
    readonly List<PhysicsBody> _bodies = new List<PhysicsBody>();
    readonly List<int> _toFree = new List<int>();
    GumoriFusion _fusion;

    public IReadOnlyList<State> States => _states;
    public int Total => _states.Count;
    public int Remaining { get; private set; }

    void Prepare()
    {
        if (dropper == null) dropper = FindAnyObjectByType<GumoriDropper>();
        if (_fusion == null && dropper != null) _fusion = dropper.GetComponent<GumoriFusion>();
    }

    float Radius(Jar jar) => 0.5f * (_fusion != null ? _fusion.LevelDiameter(jar.level) : 1f) + glass;

    void OnEnable()
    {
        Prepare();
        _states.Clear();
        _toFree.Clear();
        for (int i = 0; i < jars.Count; i++)
        {
            var jar = jars[i];
            _states.Add(new State
            {
                center = (Vector2)transform.position + jar.position, radius = Radius(jar),
                level = _fusion != null ? Mathf.Min(jar.level, _fusion.MaxLevel) : jar.level, hits = jar.hits, hitsLeft = jar.hits
            });
        }
        Remaining = _states.Count;
        if (!Application.isPlaying) return;
        Build();
        if (_fusion != null) _fusion.Merged += OnMerged;
    }

    void OnDisable()
    {
        if (_fusion != null) _fusion.Merged -= OnMerged;
        for (int i = 0; i < _bodies.Count; i++)
            if (_bodies[i].isValid) _bodies[i].Destroy();
        _bodies.Clear();
    }

    void Build()
    {
        var world = PhysicsWorld.defaultWorld;
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        shapeDef.surfaceMaterial = material;
        for (int i = 0; i < _states.Count; i++)
        {
            bodyDef.position = _states[i].center;
            var body = world.CreateBody(bodyDef);
            body.CreateShape(new CircleGeometry { center = Vector2.zero, radius = _states[i].radius }, shapeDef);
            _bodies.Add(body);
        }
    }

    // Fusion (pendant le pas de GumoriFusion) : chaque bocal entier assez proche du gumori né prend une fêlure.
    void OnMerged(Gumori merged, int level, Vector2 at)
    {
        int n = merged.CapsuleCount;
        float thickness = merged.CapsuleRadius;
        for (int i = 0; i < _states.Count; i++)
        {
            var s = _states[i];
            if (s.broken) continue;
            float limit = s.radius + thickness + reach;
            if ((at - s.center).sqrMagnitude > (limit + merged.diameter) * (limit + merged.diameter)) continue;
            bool near = false;
            for (int k = 0; k < n && !near; k++) near = (merged.Capsule(k).position - s.center).sqrMagnitude <= limit * limit;
            if (!near) continue;
            s.hitsLeft--;
            if (s.hitsLeft <= 0)
            {
                s.broken = true;
                Remaining--;
                if (_bodies[i].isValid) _bodies[i].Destroy();
                _toFree.Add(i);
            }
            _states[i] = s;
            Cracked?.Invoke(i, s.hitsLeft);
        }
    }

    // Gumori libéré au pas suivant, à la place du bocal (vide) : jamais pendant la boucle de fusion.
    public void LevelTick()
    {
        if (_toFree.Count == 0 || dropper == null) return;
        for (int k = 0; k < _toFree.Count; k++)
        {
            int i = _toFree[k];
            var blob = dropper.SpawnLevel(_states[i].center, _states[i].level);
            Freed?.Invoke(i, blob);
        }
        _toFree.Clear();
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Prepare();
        for (int i = 0; i < jars.Count; i++)
        {
            Vector2 c = (Vector2)transform.position + jars[i].position;
            float r = Radius(jars[i]);
            Gizmos.color = new Color(0.75f, 0.92f, 1f, 0.9f);
            DrawCircle(c, r);
            Gizmos.color = _fusion != null ? _fusion.LevelColor(jars[i].level) : Color.white;
            DrawCircle(c, r - glass);
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
