using System.Collections.Generic;
using UnityEngine;

// Gumori enfermés dans un autre : un gumori qui vient de fusionner démarre sur une cacahuète tracée sur des cercles idéaux, plus
// gros que des gumori écrasés dans la pile. Il peut alors chevaucher la membrane d'un voisin, et Box2D le pousse du mauvais côté
// (dedans), ou refermer son propre col sur un petit pris dans le creux. Détection : pendant 'watchTime' après chaque fusion, le
// nouveau gumori et ses voisins sont testés (centre du petit dans le polygone des capsules du gros). Sortie : la membrane du gros
// laisse passer le petit (même groupe de collision), qui glisse dehors à 'escapeSpeed' par le bord le plus PROCHE du gros (pas
// vers le haut : il traversait tout le gros et remontait à la surface de la pile, mesuré jusqu'à 3,5 m), puis la reprend dès
// qu'il en est dégagé. Le petit touche tout le reste normalement. Tout dans LevelTick, dans l'ordre de création.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(GumoriFusion))]
public class GumoriEscape : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Détection")]
    [Tooltip("Durée (s) de surveillance d'un gumori après sa fusion (en plus de la durée de fusion de GumoriFusion).")]
    [Min(0f)] public float watchTime = 1f;
    [Tooltip("Test tous les N pas (120 pas/s). Plus grand = moins coûteux, sortie un peu plus tardive.")]
    [Min(1)] public int checkInterval = 2;

    [Header("Sortie")]
    [Tooltip("Vitesse de sortie (km/h) du gumori enfermé, vers le bord le plus proche du gros.")]
    [SpeedKmh] [Min(0f)] public float escapeSpeed = 3f;
    [Tooltip("Angle max (°) sous l'horizontale de la sortie : un bord plus bas n'est pas choisi (le petit finirait coincé sous " +
             "le gros contre le fond). 0 = jamais vers le bas, 90 = n'importe quel bord.")]
    [Range(0f, 90f)] public float maxDownAngle = 30f;
    [Tooltip("Durée (s) après laquelle la membrane redevient solide dès que le petit ne la chevauche plus, même s'il est encore " +
             "dedans. Encore à cheval : attendu jusqu'à 3 × cette durée (rendue à cheval, Box2D les séparait d'un coup et le " +
             "petit était projeté).")]
    [Min(0.1f)] public float maxEscapeTime = 3f;

    struct Watch
    {
        public Gumori blob;
        public int until;
    }

    struct Escape
    {
        public Gumori blob, ring;
        public int until, last;
    }

    GumoriDropper _dropper;
    GumoriFusion _fusion;
    readonly List<Watch> _watch = new List<Watch>();
    readonly List<Escape> _escapes = new List<Escape>();

    public int Escapes { get; private set; }

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _fusion = GetComponent<GumoriFusion>();
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

    void OnMerged(Gumori merged, int level, Vector2 position)
    {
        int steps = Mathf.RoundToInt((_fusion.MergeDuration + watchTime) / Time.fixedDeltaTime);
        _watch.Add(new Watch { blob = merged, until = LevelSimulation.Step + steps });
    }

    public void LevelTick()
    {
        int step = LevelSimulation.Step;
        for (int i = _escapes.Count - 1; i >= 0; i--)
        {
            var e = _escapes[i];
            bool alive = e.blob != null && e.blob.isActiveAndEnabled;
            if (!alive || e.ring == null || !e.ring.isActiveAndEnabled || step >= e.last || IsClear(e.blob, e.ring) ||
                (step >= e.until && !Overlaps(e.blob, e.ring)))
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

    // Le gumori surveillé W est-il dans un plus gros, ou un plus petit est-il dans W ? Tri grossier par une seule capsule.
    void Check(Gumori w)
    {
        if (w.CapsuleCount == 0) return;
        Vector2 cw = w.Center;
        float rw = 0.5f * w.diameter;
        var blobs = _dropper.InPlay;
        for (int k = 0; k < blobs.Count; k++)
        {
            var x = blobs[k];
            if (x == w || x.CapsuleCount == 0 || x.diameter == w.diameter) continue;
            float rx = 0.5f * x.diameter;
            if (Vector2.Distance(x.Capsule(0).position, cw) > rw + 2f * rx) continue;
            if (x.diameter > w.diameter)
            {
                if (!IsEscaping(w) && x.Contains(cw)) StartEscape(w, x);
            }
            else if (!IsEscaping(x) && w.Contains(x.Center)) StartEscape(x, w);
        }
    }

    void StartEscape(Gumori blob, Gumori ring)
    {
        blob.SetCollisionGroup(ring.OwnGroup);
        int steps = Mathf.RoundToInt(maxEscapeTime / Time.fixedDeltaTime);
        _escapes.Add(new Escape { blob = blob, ring = ring, until = LevelSimulation.Step + steps, last = LevelSimulation.Step + 3 * steps });
        Escapes++;
    }

    bool IsEscaping(Gumori blob)
    {
        for (int i = 0; i < _escapes.Count; i++)
            if (_escapes[i].blob == blob) return true;
        return false;
    }

    // Vitesse vers le bord le plus proche du gros d'au moins 'escapeSpeed' : le reste du mouvement est gardé.
    void Push(Gumori blob, Gumori ring)
    {
        Vector2 dir = ExitDirection(blob.Center, ring);
        for (int i = 0; i < blob.CapsuleCount; i++)
        {
            var body = blob.Capsule(i);
            Vector2 v = body.linearVelocity;
            float along = Vector2.Dot(v, dir);
            if (along < escapeSpeed) body.linearVelocity = v + dir * (escapeSpeed - along);
        }
    }

    // Du centre du petit vers le point le plus proche de la membrane du gros, parmi les directions pas plus basses que
    // 'maxDownAngle'. Centre déjà dehors : droit vers l'extérieur, le plus court chemin pour finir de se dégager.
    Vector2 ExitDirection(Vector2 c, Gumori ring)
    {
        Vector2 rc = ring.Center;
        Vector2 away = c - rc;
        away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
        if (!ring.Contains(c)) return away;
        float minY = -Mathf.Sin(maxDownAngle * Mathf.Deg2Rad) - 1e-4f;
        float best = float.MaxValue;
        Vector2 dir = Vector2.zero;
        int n = ring.CapsuleCount;
        Vector2 a = ring.Capsule(n - 1).position;
        for (int i = 0; i < n; i++)
        {
            Vector2 b = ring.Capsule(i).position, ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            Vector2 to = a + t * ab - c;
            float d2 = to.sqrMagnitude;
            a = b;
            if (d2 < 1e-8f || d2 >= best || to.y < minY * Mathf.Sqrt(d2)) continue;
            best = d2;
            dir = to / Mathf.Sqrt(d2);
        }
        if (best == float.MaxValue) dir = new Vector2(away.x >= 0f ? 1f : -1f, 0f);
        return dir;
    }

    // Une capsule du petit chevauche-t-elle une capsule du gros ?
    static bool Overlaps(Gumori blob, Gumori ring)
    {
        float gap = blob.CapsuleThickness + ring.CapsuleThickness, g2 = gap * gap;
        for (int i = 0; i < blob.CapsuleCount; i++)
        {
            Vector2 p = blob.Capsule(i).position;
            for (int k = 0; k < ring.CapsuleCount; k++)
                if ((ring.Capsule(k).position - p).sqrMagnitude < g2) return true;
        }
        return false;
    }

    // Dégagé : centre hors du gros, et à plus de (son rayon + épaisseur de la membrane) de chaque capsule du gros.
    bool IsClear(Gumori blob, Gumori ring)
    {
        Vector2 c = blob.Center;
        if (ring.Contains(c)) return false;
        float clear = 0.5f * blob.diameter + ring.CapsuleThickness;
        for (int i = 0; i < ring.CapsuleCount; i++)
            if ((ring.Capsule(i).position - c).sqrMagnitude < clear * clear) return false;
        return true;
    }
}
