using System.Collections.Generic;
using UnityEngine;

// Gumori emmêlés (Space Endless) : très mous et gonflés (GumoriFoam), leur anneau s'étire, des trous s'ouvrent entre ses
// capsules et une capsule d'un voisin passe à travers ; elle reste ensuite coincée DEDANS (l'anneau la retient de l'intérieur).
// GumoriEscape ne surveille que les naissances de fusion. Ici, à tout moment : capsule d'un gumori dans le polygone des capsules
// d'un autre → le moins enfoncé des deux prend le groupe de collision de l'autre (ils se traversent), ils sont écartés à
// 'escapeSpeed', et chacun retrouve son groupe dès qu'aucune capsule n'est plus dans l'autre. Dans LevelTick, dans l'ordre de
// création : déterministe. Ajouté seulement dans la scène Space Endless.
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Corps/GumoriUntangle")]
public class GumoriUntangle : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Tooltip("Vitesse (km/h) à laquelle deux gumori emmêlés sont écartés l'un de l'autre pendant qu'ils se traversent.")]
    [SpeedKmh] [Min(0f)] public float escapeSpeed = 5f;
    [Tooltip("Durée max (s) d'un démêlage : passé ce délai, les deux gumori redeviennent solides l'un pour l'autre.")]
    [Min(0.05f)] public float maxTime = 1.5f;
    [Tooltip("Vérification tous les N pas (1 = à chaque pas).")]
    [Min(1)] public int checkInterval = 2;

    struct Untangling { public Gumori a, b; public int until; }

    GumoriDropper _dropper;
    GumoriFusion _fusion;
    readonly List<Untangling> _active = new List<Untangling>();
    Vector2[] _pos = new Vector2[0];
    readonly List<int> _first = new List<int>(), _count = new List<int>();
    readonly List<Vector2> _center = new List<Vector2>();
    readonly List<float> _reach = new List<float>();

    public int Untangled { get; private set; }

    void OnEnable()
    {
        _dropper = GumoriDropper.Find<GumoriDropper>(this);
        _fusion = GumoriDropper.Find<GumoriFusion>(this);
        _active.Clear();
        Untangled = 0;
    }

    void OnDisable() => _active.Clear();

    public void LevelTick()
    {
        int step = LevelSimulation.Step;
        bool check = step % checkInterval == 0;
        if (!check && _active.Count == 0) return;
        Gather();

        // Démêlages en cours : écarter, puis rendre les groupes quand c'est dégagé (ou trop long).
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var u = _active[i];
            int ia = IndexOf(u.a), ib = IndexOf(u.b);
            if (ia < 0 || ib < 0) { if (ia >= 0) u.a.SetCollisionGroup(u.a.OwnGroup); _active.RemoveAt(i); continue; }
            // Rendre les groupes seulement une fois VRAIMENT dégagés (épaisseur des capsules comprise) : encore à cheval,
            // Box2D les séparait d'un coup et ils sautaient (mesuré : jusqu'à 4,5 m/s vers le haut après une fusion).
            if (step >= u.until || (Inside(ia, ib) == 0 && Inside(ib, ia) == 0 && Clear(ia, ib, u.a.CapsuleRadius + u.b.CapsuleRadius)))
            {
                u.a.SetCollisionGroup(u.a.OwnGroup);
                _active.RemoveAt(i);
                continue;
            }
            Push(u.a, ia, ib);
        }
        if (!check) return;

        var blobs = _dropper.InPlay;
        for (int i = 0; i < blobs.Count; i++)
        {
            if (Busy(blobs[i])) continue;
            for (int j = i + 1; j < blobs.Count; j++)
            {
                if (Busy(blobs[j]) || Busy(blobs[i])) continue;
                if ((_center[i] - _center[j]).sqrMagnitude > (_reach[i] + _reach[j]) * (_reach[i] + _reach[j])) continue;
                if (_fusion != null && (_fusion.IsMerging(blobs[i]) || _fusion.IsMerging(blobs[j]))) continue;
                int inJ = Inside(i, j), inI = Inside(j, i);
                if (inJ == 0 && inI == 0) continue;
                // Deux pareils emmêlés (pointe passée par un trou entre deux capsules, sans aucun contact) : ils fusionnent au
                // lieu d'être séparés (sinon : collés l'un dans l'autre, puis écartés sans fusionner).
                if (_fusion != null && _fusion.RequestMerge(blobs[i], blobs[j])) continue;
                // Le moins enfoncé (relativement à sa taille) traverse l'autre.
                bool aIsI = inJ * _count[j] <= inI * _count[i];
                int ia = aIsI ? i : j, ib = aIsI ? j : i;
                var a = blobs[ia];
                a.SetCollisionGroup(blobs[ib].OwnGroup);
                _active.Add(new Untangling { a = a, b = blobs[ib], until = step + Mathf.CeilToInt(maxTime / Time.fixedDeltaTime) });
                Untangled++;
                Push(a, ia, ib);
            }
        }
    }

    // Paires de gumori emmêlés en ce moment (tests et réglages).
    public int TangledPairs()
    {
        Gather();
        int pairs = 0;
        for (int i = 0; i < _count.Count; i++)
            for (int j = i + 1; j < _count.Count; j++)
                if (Inside(i, j) > 0 || Inside(j, i) > 0) pairs++;
        return pairs;
    }

    bool Busy(Gumori blob)
    {
        for (int i = 0; i < _active.Count; i++) if (_active[i].a == blob || _active[i].b == blob) return true;
        return false;
    }

    static int IndexOf(Gumori blob) => GumoriDropper.IsInPlay(blob) ? blob.Index : -1;

    // Positions des capsules de tous les gumori (lues une fois), centre et portée (capsule la plus loin + son rayon).
    void Gather()
    {
        var blobs = _dropper.InPlay;
        int total = 0;
        for (int i = 0; i < blobs.Count; i++) total += blobs[i].CapsuleCount;
        if (_pos.Length < total) _pos = new Vector2[Mathf.Max(total, 2 * _pos.Length)];
        _first.Clear(); _count.Clear(); _center.Clear(); _reach.Clear();
        int at = 0;
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            int n = blob.CapsuleCount;
            Vector2 c = Vector2.zero;
            for (int k = 0; k < n; k++) { _pos[at + k] = blob.Capsule(k).position; c += _pos[at + k]; }
            if (n > 0) c /= n;
            float r = 0f;
            for (int k = 0; k < n; k++) r = Mathf.Max(r, (_pos[at + k] - c).sqrMagnitude);
            _first.Add(at); _count.Add(n); _center.Add(c); _reach.Add(Mathf.Sqrt(r) + blob.CapsuleRadius);
            at += n;
        }
    }

    // Nombre de capsules du gumori 'a' dont le centre est dans le polygone des capsules du gumori 'b'.
    int Inside(int a, int b)
    {
        int count = 0, fb = _first[b], nb = _count[b];
        if (nb < 3) return 0;
        for (int k = 0; k < _count[a]; k++)
        {
            Vector2 p = _pos[_first[a] + k];
            if ((p - _center[b]).sqrMagnitude > _reach[b] * _reach[b]) continue;
            bool inside = false;
            for (int e = 0, f = nb - 1; e < nb; f = e++)
            {
                Vector2 u = _pos[fb + e], v = _pos[fb + f];
                if ((u.y > p.y) != (v.y > p.y) && p.x < (v.x - u.x) * (p.y - u.y) / (v.y - u.y) + u.x) inside = !inside;
            }
            if (inside) count++;
        }
        return count;
    }

    // Aucune capsule de 'a' à moins de 'gap' (m) d'un côté du polygone de 'b'.
    bool Clear(int a, int b, float gap)
    {
        float g2 = gap * gap;
        int fb = _first[b], nb = _count[b];
        for (int k = 0; k < _count[a]; k++)
        {
            Vector2 p = _pos[_first[a] + k];
            if ((p - _center[b]).sqrMagnitude > (_reach[b] + gap) * (_reach[b] + gap)) continue;
            for (int e = 0, f = nb - 1; e < nb; f = e++)
            {
                Vector2 u = _pos[fb + f], v = _pos[fb + e], uv = v - u;
                float t = Mathf.Clamp01(Vector2.Dot(p - u, uv) / Mathf.Max(1e-8f, uv.sqrMagnitude));
                if ((u + t * uv - p).sqrMagnitude < g2) return false;
            }
        }
        return true;
    }

    // Écarte 'a' de 'b' : vitesse relative (le long de la ligne des centres) portée à au moins 'escapeSpeed', partagée moitié
    // chacun, même correction pour toutes les capsules d'un gumori (sa forme n'est pas touchée).
    void Push(Gumori a, int ia, int ib)
    {
        Vector2 dir = _center[ia] - _center[ib];
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
        Vector2 va = a.Velocity, vb = _dropper.InPlay[ib].Velocity;
        float apart = Vector2.Dot(va - vb, dir);
        if (apart >= escapeSpeed) return;
        Vector2 dv = 0.5f * (escapeSpeed - apart) * dir;
        a.AddVelocity(dv);
        _dropper.InPlay[ib].AddVelocity(-dv);
    }
}
