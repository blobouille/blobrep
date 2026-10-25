using System.Collections.Generic;
using UnityEngine;

// Gumori « mousse » (Space Endless) : une pression intérieure tient la SURFACE de chaque gumori (aire du polygone de ses
// capsules) au lieu de sa forme ronde. Avec des joints de flexion mous, ils s'aplatissent les uns contre les autres et
// comblent l'espace comme des bulles de savon, au lieu de rester ronds avec des trous entre eux. Freine aussi leur rotation
// d'ensemble (les gumori qui se retournent sans cesse). Même correction de vitesse calculée pour tout l'anneau : rien ne
// propulse un gumori. Dans LevelTick, après le pas des gumori, dans l'ordre de création : déterministe. Ajouté seulement dans
// la scène Space Endless : le réservoir normal n'a pas ce composant.
[DisallowMultipleComponent]
[RequireComponent(typeof(GumoriDropper))]
public class GumoriFoam : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Pression")]
    [Tooltip("Fréquence (Hz) du ressort qui ramène la surface du gumori à sa valeur cible. Plus haut = plus « gonflé », il pousse\n" +
             "plus fort ses voisins et comble mieux les trous ; trop haut = il tremble.")]
    [Min(0f)] public float pressureFrequency = 6f;
    [Tooltip("Amortissement de ce ressort (1 = critique).")]
    [Min(0f)] public float pressureDamping = 0.7f;
    [Tooltip("Surface cible = surface au repos × ce facteur. Au-dessus de 1, les gumori se serrent et remplissent les espaces.")]
    [Range(0.5f, 2f)] public float inflate = 1.1f;
    [Tooltip("Pas de pression pendant une fusion (la cacahuète de la mitose garde sa forme).")]
    public bool skipMerging = true;
    [Tooltip("Après une fusion, durée (s) pendant laquelle la surface cible passe en douceur de la surface du gumori à sa\n" +
             "naissance (cacahuète) à sa vraie valeur, mitose comprise. 0 = d'un coup : un coup d'énergie qui fait tout sauter.")]
    [Min(0f)] public float growTime = 0.8f;
    [Tooltip("Pli (°) : une capsule où l'anneau tourne vers l'intérieur de plus que cet angle est un pli (gumori à moitié\n" +
             "retourné, coincé entre deux gros) ; elle est repoussée vers l'extérieur à 'unfoldSpeed'.")]
    [Range(10f, 180f)] public float foldAngle = 60f;
    [Tooltip("Vitesse (km/h) donnée vers l'extérieur à une capsule pliée, pour déplier l'anneau.")]
    [SpeedKmh] [Min(0f)] public float unfoldSpeed = 4f;

    [Header("Rotation")]
    [Tooltip("Freinage de la rotation d'ensemble de chaque gumori (1/s) : 0 = libre, 5 = s'arrête de tourner en ≈ 0,2 s.")]
    [Min(0f)] public float spinDamping = 3f;

    GumoriDropper _dropper;
    GumoriFusion _fusion;
    readonly Vector2[] _p = new Vector2[64], _v = new Vector2[64], _g = new Vector2[64];
    readonly bool[] _fold = new bool[64];

    // Gumori nés d'une fusion : en mitose (start = -1), puis surface cible en rampe depuis 'from' à partir du pas 'start'.
    struct Growth { public Gumori blob; public int start; public float from; }
    readonly List<Growth> _growth = new List<Growth>();

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _fusion = GetComponent<GumoriFusion>();
        _growth.Clear();
        if (_fusion != null) _fusion.Merged += OnMerged;
    }

    void OnDisable()
    {
        if (_fusion != null) _fusion.Merged -= OnMerged;
        _growth.Clear();
    }

    void OnMerged(Gumori merged, int level, Vector2 at) => _growth.Add(new Growth { blob = merged, start = -1 });

    int GrowthIndex(Gumori blob)
    {
        for (int i = 0; i < _growth.Count; i++) if (_growth[i].blob == blob) return i;
        return -1;
    }

    public void LevelTick()
    {
        float dt = Time.fixedDeltaTime;
        float w = 2f * Mathf.PI * pressureFrequency;
        float spin = Mathf.Min(1f, spinDamping * dt);
        for (int i = _growth.Count - 1; i >= 0; i--)
            if (_growth[i].blob == null || _growth[i].blob.CapsuleCount == 0) _growth.RemoveAt(i);
        var blobs = _dropper.InPlay;
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            int n = blob.CapsuleCount;
            if (n < 3 || n > _p.Length) continue;
            // Né d'une fusion : pression dès sa naissance, cible en rampe (sans pression pendant la mitose, la foule
            // l'écrasait, puis la pression revenue d'un coup le regonflait brutalement : tout sautait).
            int gi = _growth.Count > 0 ? GrowthIndex(blob) : -1;
            bool pressure = w > 0f && (gi >= 0 || !(skipMerging && _fusion != null && _fusion.IsMerging(blob)));

            Vector2 c = Vector2.zero, vc = Vector2.zero;
            for (int k = 0; k < n; k++)
            {
                var b = blob.Capsule(k);
                _p[k] = b.position;
                _v[k] = b.linearVelocity;
                c += _p[k];
                vc += _v[k];
            }
            c /= n;
            vc /= n;

            float dw = 0f;
            if (spin > 0f)
            {
                float l = 0f, inertia = 0f;
                for (int k = 0; k < n; k++)
                {
                    Vector2 r = _p[k] - c, u = _v[k] - vc;
                    l += r.x * u.y - r.y * u.x;
                    inertia += r.sqrMagnitude;
                }
                dw = inertia > 0f ? spin * l / inertia : 0f;
            }

            float scale = 0f;
            if (pressure)
            {
                // Aire (signée) du polygone des capsules et son gradient par capsule ; ressort amorti sur l'aire.
                float area = 0f, g2 = 0f, rate = 0f;
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = _p[k], b = _p[(k + 1) % n];
                    area += a.x * b.y - b.x * a.y;
                    Vector2 prev = _p[(k + n - 1) % n], next = _p[(k + 1) % n];
                    _g[k] = 0.5f * new Vector2(next.y - prev.y, prev.x - next.x);
                    g2 += _g[k].sqrMagnitude;
                    rate += Vector2.Dot(_g[k], _v[k]);
                }
                area *= 0.5f;
                float ring = blob.RestRingRadius;
                // Anneau construit dans le sens trigonométrique : cible toujours positive (un gumori retourné est ramené à
                // l'endroit, au lieu d'être tenu à l'envers).
                float target = inflate * 0.5f * n * ring * ring * Mathf.Sin(2f * Mathf.PI / n);
                if (gi >= 0)
                {
                    var grow = _growth[gi];
                    if (grow.start < 0) { grow.start = LevelSimulation.Step; grow.from = area; _growth[gi] = grow; }
                    float t = growTime > 0f ? (LevelSimulation.Step - grow.start) * dt / growTime : 1f;
                    if (t >= 1f) _growth.RemoveAt(gi);
                    else target = Mathf.Lerp(grow.from, target, t * t * (3f - 2f * t));
                }
                if (g2 > 1e-8f) scale = (w * w * (target - area) - 2f * pressureDamping * w * rate) * dt / g2;
            }

            int folds = 0;
            if (pressure && unfoldSpeed > 0f)
            {
                // Pli : l'anneau (sens trigonométrique) tourne normalement vers la gauche à chaque capsule.
                for (int k = 0; k < n; k++)
                {
                    Vector2 e1 = _p[k] - _p[(k + n - 1) % n], e2 = _p[(k + 1) % n] - _p[k];
                    float cross = e1.x * e2.y - e1.y * e2.x, dot = Vector2.Dot(e1, e2);
                    _fold[k] = cross < 0f && Mathf.Atan2(cross, dot) < -foldAngle * Mathf.Deg2Rad;
                    if (_fold[k]) folds++;
                }
            }

            if (scale == 0f && dw == 0f && folds == 0) continue;
            for (int k = 0; k < n; k++)
            {
                var b = blob.Capsule(k);
                Vector2 r = _p[k] - c;
                Vector2 dv = new Vector2(dw * r.y, -dw * r.x);
                if (scale != 0f) dv += scale * _g[k];
                if (folds > 0 && _fold[k] && _g[k].sqrMagnitude > 1e-8f)
                {
                    Vector2 outward = _g[k].normalized;
                    float now = Vector2.Dot(_v[k] + dv, outward);
                    if (now < unfoldSpeed) dv += (unfoldSpeed - now) * outward;
                }
                b.linearVelocity = _v[k] + dv;
                if (dw != 0f) b.angularVelocity -= dw * Mathf.Rad2Deg;
            }
        }
    }
}
