using System.Collections.Generic;
using UnityEngine;

// Slimes enfermés dans un autre : un slime qui vient de fusionner démarre sur une cacahuète tracée sur des cercles idéaux, plus
// gros que des slimes écrasés dans la pile. Il peut alors chevaucher la membrane d'un voisin, et Box2D le pousse du mauvais côté
// (dedans), ou refermer son propre col sur un petit pris dans le creux. Détection : pendant 'watchTime' après chaque fusion, le
// nouveau slime et ses voisins sont testés (centre du petit dans le polygone des capsules du gros). Sortie : la membrane du gros
// laisse passer le petit (même groupe de collision), qui glisse dehors à 'escapeSpeed', vers l'extérieur et vers le haut, puis
// la reprend dès qu'il est dégagé. Le petit touche tout le reste normalement. Tout dans LevelTick, dans l'ordre de création.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobFusion))]
public class SlimeEscape : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Détection")]
    [Tooltip("Durée (s) de surveillance d'un slime après sa fusion (en plus de 'mergeTime' de BlobFusion).")]
    [Min(0f)] public float watchTime = 1f;
    [Tooltip("Test tous les N pas (120 pas/s). Plus grand = moins coûteux, sortie un peu plus tardive.")]
    [Min(1)] public int checkInterval = 2;

    [Header("Sortie")]
    [Tooltip("Vitesse de sortie (km/h) du slime enfermé, vers l'extérieur du gros.")]
    [SpeedKmh] [Min(0f)] public float escapeSpeed = 3f;
    [Tooltip("Penche la sortie vers le haut : 0 = droit vers l'extérieur, 1 = à 45° vers le haut en moyenne.\n" +
             "Vers le haut, il ne finit pas coincé sous le gros contre le fond.")]
    [Min(0f)] public float upBias = 1f;
    [Tooltip("Durée max (s) d'une sortie : au-delà, la membrane redevient solide quoi qu'il arrive.")]
    [Min(0.1f)] public float maxEscapeTime = 3f;

    struct Watch
    {
        public BLOB blob;
        public int until;
    }

    struct Escape
    {
        public BLOB blob, ring;
        public int until;
    }

    BlobDropper _dropper;
    BlobFusion _fusion;
    readonly List<Watch> _watch = new List<Watch>();
    readonly List<Escape> _escapes = new List<Escape>();

    public int Escapes { get; private set; }

    void OnEnable()
    {
        _dropper = GetComponent<BlobDropper>();
        _fusion = GetComponent<BlobFusion>();
        _fusion.Merged += OnMerged;
        _watch.Clear();
        _escapes.Clear();
        Escapes = 0;
    }

    void OnDisable()
    {
        if (_fusion != null) _fusion.Merged -= OnMerged;
        _watch.Clear();
        _escapes.Clear();
    }

    void OnMerged(BLOB merged, int level, Vector2 position)
    {
        int steps = Mathf.RoundToInt((_fusion.mergeTime + watchTime) / Time.fixedDeltaTime);
        _watch.Add(new Watch { blob = merged, until = LevelSimulation.Step + steps });
    }

    public void LevelTick()
    {
        int step = LevelSimulation.Step;
        for (int i = _escapes.Count - 1; i >= 0; i--)
        {
            var e = _escapes[i];
            bool alive = e.blob != null && e.blob.isActiveAndEnabled;
            if (!alive || e.ring == null || !e.ring.isActiveAndEnabled || step >= e.until || IsClear(e.blob, e.ring))
            {
                if (alive) e.blob.SetCollisionGroup(e.blob.OwnGroup);
                _escapes.RemoveAt(i);
                continue;
            }
            Push(e.blob, e.ring);
        }

        for (int i = _watch.Count - 1; i >= 0; i--)
            if (step > _watch[i].until || _watch[i].blob == null || !_watch[i].blob.isActiveAndEnabled) _watch.RemoveAt(i);
        if (step % checkInterval != 0) return;
        for (int i = 0; i < _watch.Count; i++) Check(_watch[i].blob);
    }

    // Le slime surveillé W est-il dans un plus gros, ou un plus petit est-il dans W ? Tri grossier par une seule capsule.
    void Check(BLOB w)
    {
        if (w.CapsuleCount == 0) return;
        Vector2 cw = Center(w);
        float rw = 0.5f * w.diameter;
        var blobs = _dropper.Blobs;
        for (int k = 0; k < blobs.Count; k++)
        {
            var x = blobs[k];
            if (x == w || x.CapsuleCount == 0 || x.diameter == w.diameter) continue;
            float rx = 0.5f * x.diameter;
            if (Vector2.Distance(x.Capsule(0).position, cw) > rw + 2f * rx) continue;
            if (x.diameter > w.diameter)
            {
                if (!IsEscaping(w) && Inside(x, cw)) StartEscape(w, x);
            }
            else if (!IsEscaping(x) && Inside(w, Center(x))) StartEscape(x, w);
        }
    }

    void StartEscape(BLOB blob, BLOB ring)
    {
        blob.SetCollisionGroup(ring.OwnGroup);
        _escapes.Add(new Escape { blob = blob, ring = ring, until = LevelSimulation.Step + Mathf.RoundToInt(maxEscapeTime / Time.fixedDeltaTime) });
        Escapes++;
    }

    bool IsEscaping(BLOB blob)
    {
        for (int i = 0; i < _escapes.Count; i++)
            if (_escapes[i].blob == blob) return true;
        return false;
    }

    // Vitesse vers l'extérieur du gros (penchée vers le haut) d'au moins 'escapeSpeed' : le reste du mouvement est gardé.
    void Push(BLOB blob, BLOB ring)
    {
        Vector2 away = Center(blob) - Center(ring);
        away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
        Vector2 dir = (away + upBias * Vector2.up).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
        for (int i = 0; i < blob.CapsuleCount; i++)
        {
            var body = blob.Capsule(i);
            Vector2 v = body.linearVelocity;
            float along = Vector2.Dot(v, dir);
            if (along < escapeSpeed) body.linearVelocity = v + dir * (escapeSpeed - along);
        }
    }

    // Dégagé : centre hors du gros, et à plus de (son rayon + épaisseur de la membrane) de chaque capsule du gros.
    bool IsClear(BLOB blob, BLOB ring)
    {
        Vector2 c = Center(blob);
        if (Inside(ring, c)) return false;
        float clear = 0.5f * blob.diameter + ring.CapsuleThickness;
        for (int i = 0; i < ring.CapsuleCount; i++)
            if ((ring.Capsule(i).position - c).sqrMagnitude < clear * clear) return false;
        return true;
    }

    static Vector2 Center(BLOB blob)
    {
        Vector2 c = Vector2.zero;
        int n = blob.CapsuleCount;
        for (int i = 0; i < n; i++) c += blob.Capsule(i).position;
        return c / n;
    }

    // Point dans le polygone des centres des capsules (pair-impair).
    static bool Inside(BLOB ring, Vector2 p)
    {
        bool inside = false;
        int n = ring.CapsuleCount;
        Vector2 a = ring.Capsule(n - 1).position;
        for (int i = 0; i < n; i++)
        {
            Vector2 b = ring.Capsule(i).position;
            if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
            a = b;
        }
        return inside;
    }
}
