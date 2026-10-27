using UnityEngine;

// Gumori « mousse » (scènes Space) : une pression intérieure tient la SURFACE de chaque gumori (aire du polygone de ses
// capsules) au lieu de sa forme ronde. Avec des joints de flexion mous, ils s'aplatissent les uns contre les autres et
// comblent l'espace comme des bulles de savon, au lieu de rester ronds avec des trous entre eux. Freine aussi leur rotation
// d'ensemble (les gumori qui se retournent sans cesse). Même correction de vitesse calculée pour tout l'anneau : rien ne
// propulse un gumori. Dans LevelTick, après le pas des gumori, dans l'ordre de création : déterministe. Seulement dans les
// scènes Space : le réservoir terrestre n'a pas ce composant.
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Corps/GumoriFoam")]
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

    [Header("Rotation")]
    [Tooltip("Freinage de la rotation d'ensemble de chaque gumori (1/s) : 0 = libre, 5 = s'arrête de tourner en ≈ 0,2 s.")]
    [Min(0f)] public float spinDamping = 3f;

    GumoriDropper _dropper;
    GumoriFusion _fusion;
    readonly Vector2[] _p = new Vector2[64], _v = new Vector2[64], _g = new Vector2[64];

    void OnEnable()
    {
        _dropper = GumoriDropper.Find<GumoriDropper>(this);
        _fusion = GumoriDropper.Find<GumoriFusion>(this);
    }

    public void LevelTick()
    {
        float dt = Time.fixedDeltaTime;
        float w = 2f * Mathf.PI * pressureFrequency;
        float spin = Mathf.Min(1f, spinDamping * dt);
        var blobs = _dropper.InPlay;
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            int n = blob.CapsuleCount;
            if (n < 3 || n > _p.Length) continue;
            bool pressure = w > 0f && !(skipMerging && _fusion != null && _fusion.IsMerging(blob));

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
                if (g2 > 1e-8f) scale = (w * w * (target - area) - 2f * pressureDamping * w * rate) * dt / g2;
            }

            if (scale == 0f && dw == 0f) continue;
            for (int k = 0; k < n; k++)
            {
                var b = blob.Capsule(k);
                Vector2 r = _p[k] - c;
                Vector2 dv = new Vector2(dw * r.y, -dw * r.x);
                if (scale != 0f) dv += scale * _g[k];
                b.linearVelocity = _v[k] + dv;
                if (dw != 0f) b.angularVelocity -= dw * Mathf.Rad2Deg;
            }
        }
    }
}
