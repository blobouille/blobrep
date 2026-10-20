using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

// Saut du blob, deux façons au choix :
// - Impulse : saut de jeu vidéo. Vitesse donnée d'un coup au départ, puis poussée qui s'éteint tant que la touche est tenue.
// - Stretch : saut physique. Tant que la touche est tenue, le blob s'allonge à vitesse constante (capsules des côtés allongées) ;
//   contre le sol, ça le pousse de l'autre côté ; en l'air, ça ne donne rien. Relâché : il arrête et revient rond tout seul.
// La forme (écrasé / étiré) passe par l'angle de repos des joints entre capsules : le blob prend vraiment cette forme.
[DisallowMultipleComponent]
[RequireComponent(typeof(BLOB))]
public class BlobJump : MonoBehaviour, ILevelPhysics, ILevelTick
{
    public enum JumpMode { Impulse, Stretch }

    [Header("Saut")]
    [Tooltip("Impulse : saut de jeu vidéo (vitesse au départ + poussée qui diminue tant qu'on tient).\n" +
             "Stretch : saut physique (tenu = le blob s'allonge et pousse contre ce qu'il touche ; relâché = il revient rond).")]
    public JumpMode mode = JumpMode.Impulse;
    [Tooltip("Touche du saut (enregistrée par LevelInput).")]
    public FlipperKey key = FlipperKey.UpArrow;
    [Tooltip("Angle max (degrés) entre la normale d'un contact et la verticale (opposé de la gravité) pour compter comme un appui.\n" +
             "180 = toute surface touchée compte : sol, mur, plafond (tête en bas dans un looping : il saute vers le bas).\n" +
             "90 = sols et murs seulement ; 80 = sols seulement.")]
    [Range(0f, 180f)] public float maxGroundAngle = 180f;
    [Tooltip("Direction du saut : 0 = perpendiculaire au sol sous le blob (dans une courbe, il saute « hors » de la courbe),\n" +
             "1 = toujours vers le haut (opposé de la gravité). Entre les deux : mélange.\n" +
             "Mode Impulse seulement : en Stretch, le sol ne peut pousser que perpendiculairement à lui-même.")]
    [Range(0f, 1f)] public float upBlend = 0.3f;
    [Tooltip("Délai (s) pendant lequel on peut encore sauter après avoir quitté le sol (bord d'une plateforme, bosse).\n" +
             "0 = coupé (et le sol n'est plus lu à chaque pas : un peu moins coûteux).")]
    [Min(0f)] public float coyoteTime = 0.08f;
    [Tooltip("Appui fait en l'air juste avant de toucher le sol : le saut part à l'atterrissage\n" +
             "s'il arrive dans ce délai (s). 0 = coupé.")]
    [Min(0f)] public float bufferTime = 0.1f;

    [Header("Impulse : saut de jeu vidéo")]
    [Tooltip("Vitesse donnée au départ, en km/h, dans la direction du saut (appui bref = saut minimal).\n" +
             "La vitesse qui allait dans le sol est d'abord annulée : même saut que le blob roule ou non.\n" +
             "Mesuré au sol plat, gravité −15, défauts : 0,50 m (tape) → 2,62 m (tenu ≥ 0,25 s).")]
    [SpeedKmh] [Min(0f)] public float impulseSpeed = 3.9f;
    [Tooltip("Poussée tant que la touche est tenue, en m/s², au début (elle diminue jusqu'à 0 à la fin de 'holdTime').\n" +
             "Plus haut = écart plus grand entre un appui bref et un appui long.")]
    [Min(0f)] public float holdAcceleration = 45f;
    [Tooltip("Durée max de la poussée (s) : au-delà, tenir plus longtemps ne fait plus monter plus haut.")]
    [Min(0f)] public float holdTime = 0.25f;
    [Tooltip("Forme de la décroissance : poussée × (1 − t / holdTime)^holdCurve. 1 = linéaire ; plus grand = elle s'éteint plus\n" +
             "vite (l'essentiel au début) ; plus petit = elle tient jusqu'au bout.")]
    [Min(0.1f)] public float holdCurve = 1f;
    [Tooltip("Au relâchement pendant la montée : part de la vitesse de montée gardée. 1 = gardée (seule la poussée s'arrête) ;\n" +
             "0,5 = coupée de moitié (saut court plus net, façon Mario).")]
    [Range(0f, 1f)] public float releaseKeep = 1f;
    [Tooltip("Étirement visuel au départ (0 = aucun, 0,9 = très allongé), puis lâché (voir 'relaxShape').\n" +
             "Il commence un pas après le départ : il ne change pas la hauteur du saut.")]
    [Range(0f, 0.95f)] public float impulseStretch = 0.4f;

    [Header("Stretch : saut physique")]
    [Tooltip("Vitesse d'étirement, en km/h : vitesse à laquelle les deux bouts du blob s'écartent tant que la touche est tenue.\n" +
             "Côté sol, le bout ne peut pas s'enfoncer : c'est le blob qui part de l'autre côté. En l'air, il s'étire sans rien gagner.\n" +
             "Relâché : il arrête net de s'étirer et ses capsules le ramènent rond toutes seules.")]
    [SpeedKmh] [Min(0.1f)] public float stretchSpeed = 8f;
    [Tooltip("Allongement max, en m (absolu) : au-delà, tenir ne l'étire plus. Atteint en maxStretch / vitesse d'étirement secondes.\n" +
             "Les capsules des côtés s'allongent (masse inchangée), les bouts restent ronds.")]
    [Min(0f)] public float maxStretch = 2f;

    [Header("Forme (les deux modes)")]
    [Tooltip("Décoché : une fois étiré, le blob est lâché dès qu'il ne touche plus rien et ses joints le ramènent rond tout seuls\n" +
             "(lâché contre le sol, sa détente le pousserait : saut plus haut que prévu). Coché : retour piloté, progressif, en 'relaxTime'.")]
    public bool relaxShape = false;
    [Tooltip("Coché ('relaxShape') : temps (s) pour que le blob redevienne rond. Décoché : délai max avant de le lâcher s'il touche encore.")]
    [Min(0.001f)] public float relaxTime = 0.3f;

    enum Phase { Idle, Buffered, Thrust, Stretching }

    BLOB _blob;
    int _generation;
    Phase _phase;
    bool _wasDown;
    float _timer;      // Thrust : temps écoulé ; Buffered : temps restant
    Vector2 _dir;      // direction du saut (Stretch : axe de l'étirement)
    float _stretch;    // Stretch : allongement actuel (m)
    bool _still;       // Stretch : blob immobile à l'appui (saut sur place)
    float _startLength; // Stretch : longueur le long de l'axe à l'appui
    int _thrustStep;
    int _noJumpUntil;
    int _trackStep;
    bool _trackOk;

    // Forme : amplitude actuelle (négatif = écrasé, positif = étiré) le long de _shapeAxis, et où elle va.
    float _amp, _ampGoal, _ampRate, _writtenAmp, _releaseWait;
    bool _ampReturn;   // l'objectif atteint, revenir à 0 en 'relaxTime'
    Vector2 _shapeAxis = Vector2.up;

    // Sol : lu au plus une fois par pas.
    int _groundStep = -1;
    bool _groundOk;
    Vector2 _groundNormal;
    int _lastGroundStep = int.MinValue / 2;
    Vector2 _lastGroundNormal = Vector2.up;

    void OnEnable()
    {
        _blob = GetComponent<BLOB>();
        ResetState();
    }

    void ResetState()
    {
        _generation = _blob != null ? _blob.Generation : 0;
        _phase = Phase.Idle;
        _wasDown = false;
        _timer = _stretch = _startLength = 0f;
        _amp = _ampGoal = _ampRate = _writtenAmp = 0f;
        _ampReturn = false;
        _shapeAxis = _dir = Up();
        _groundStep = -1;
        _noJumpUntil = 0;
        _trackStep = int.MinValue / 2;
        _trackOk = false;
        _lastGroundStep = int.MinValue / 2;
        _lastGroundNormal = Up();
    }

    public void LevelTick()
    {
        if (_blob == null || _blob.CapsuleCount == 0) return;
        if (_blob.Generation != _generation) ResetState();
        if (_blob.IsBroken)
        {
            if (_stretch > 0f) ReleaseStretch();
            _phase = Phase.Idle;
            return;
        }

        float dt = Time.fixedDeltaTime;
        int step = LevelSimulation.Step;
        bool down = LevelInput.IsDown(key);
        bool pressed = down && !_wasDown;
        _wasDown = down;

        if (coyoteTime > 0f) TrackGround(step);

        if (mode == JumpMode.Impulse) TickImpulse(step, dt, down, pressed);
        else TickStretch(step, dt, down, pressed);

        if (_phase != Phase.Stretching) UpdateShape(dt); // Stretch écrit sa forme lui-même
    }

    // ---------- Impulse ----------

    void TickImpulse(int step, float dt, bool down, bool pressed)
    {
        if (_phase == Phase.Stretching) ReleaseStretch(); // mode changé en Play

        if (pressed)
        {
            if (CanJump(step, out var n)) TakeOff(n);
            else if (bufferTime > 0f) { _phase = Phase.Buffered; _timer = bufferTime; }
        }
        else if (_phase == Phase.Buffered)
        {
            _timer -= dt;
            if (step >= _noJumpUntil && Ground(step, out var n, out _))
            {
                TakeOff(n);
                if (!down) _phase = Phase.Idle; // relâché avant d'atterrir : saut minimal
            }
            else if (_timer <= 0f) _phase = Phase.Idle;
        }
        else if (_phase == Phase.Thrust)
        {
            _thrustStep++;
            if (_thrustStep == 1 && impulseStretch > 0f) StartPulse(_dir, impulseStretch, impulseStretch / 0.05f);

            if (!down)
            {
                if (releaseKeep < 1f) CutRise();
                _phase = Phase.Idle;
            }
            else if (_timer >= holdTime) _phase = Phase.Idle;
            else
            {
                float a = holdAcceleration * math.pow(1f - _timer / holdTime, holdCurve);
                AddVelocity(_dir * (a * dt));
                _timer += dt;
            }
        }
    }

    void TakeOff(Vector2 n)
    {
        _dir = JumpDirection(n);
        Vector2 v = AverageVelocity();
        float into = math.min(0f, Vector2.Dot(v, n));
        AddVelocity(_dir * impulseSpeed - n * into);
        _phase = holdTime > 0f && holdAcceleration > 0f ? Phase.Thrust : Phase.Idle;
        _timer = 0f;
        _thrustStep = 0;
        _lastGroundStep = int.MinValue / 2;
        LockJump();
    }

    // Juste après un départ, le blob touche encore le sol quelques pas : pas de second saut (double appui, coyote).
    void LockJump() => _noJumpUntil = LevelSimulation.Step + Mathf.CeilToInt(0.1f / Time.fixedDeltaTime);

    void CutRise()
    {
        Vector2 v = AverageVelocity();
        float rise = Vector2.Dot(v, _dir);
        if (rise > 0f) AddVelocity(-_dir * (rise * (1f - releaseKeep)));
    }

    // ---------- Stretch ----------

    // Tenu : le blob s'allonge le long de l'axe à 'stretchSpeed' jusqu'à 'maxStretch'. Le « muscle » impose la vitesse
    // d'écartement (somme nulle : le haut part d'un côté, le bas de l'autre) ; contre le sol, le bout du bas ne peut pas reculer
    // et le blob part de l'autre côté ; en l'air, ça ne donne rien. Les joints prennent la forme qu'il a (capsules des côtés
    // allongées, bouts arrondis) : ils l'accompagnent sans la tirer. Relâché : il arrête net et ses joints le ramènent rond.
    // Ni vitesse visée ni poussée ajoutée : le saut est celui que donne la physique (sauf HoldInPlace, sur place).
    // Capsules lues une fois par pas, vitesses écrites une fois, forme écrite une fois (BLOB.SetShape).
    void TickStretch(int step, float dt, bool down, bool pressed)
    {
        if (_phase == Phase.Thrust || _phase == Phase.Buffered) _phase = Phase.Idle; // mode changé en Play

        if (pressed)
        {
            bool grounded = Ground(step, out var n0, out _);
            _dir = grounded ? n0 : _lastGroundNormal;
            ReadBodies();
            _phase = Phase.Stretching;
            _stretch = _holdTime = 0f;
            _still = grounded && _v.sqrMagnitude < StillSpeed * StillSpeed;
            _startLength = AxisLength();
        }
        if (_phase != Phase.Stretching) return;
        if (!down)
        {
            ReadBodies();
            DriveExtension(0f, AxisLength()); // il arrête net de s'étirer…
            if (_still) HoldInPlace();
            WriteVelocities();
            ReleaseStretch();                 // …et ses joints le ramènent rond
            return;
        }
        // Perpendiculaire à la direction de déplacement : au sol, le blob se déplace le long du sol, c'est donc la normale du sol
        // (dans une courbe, elle tourne avec lui). En l'air, l'axe reste celui du dernier appui.
        if (Ground(step, out var n, out _)) _dir = n;
        _holdTime += dt;
        ReadBodies();
        float length = AxisLength();
        // Mise en route douce ('RampTime') et vitesse imposée sans courir après une longueur : d'un coup, ou asservi en longueur,
        // il rattrapait ses retards par à-coups et décollait un instant de lui-même (hauteur qui plafonnait au lieu de suivre
        // l'appui). Le sol et les joints mangent un peu d'écartement : ≈ 85 % de 'stretchSpeed' mesuré.
        float rate = length - _startLength >= maxStretch ? 0f : stretchSpeed * math.min(1f, _holdTime / RampTime);
        DriveExtension(rate, length);
        if (_still) HoldInPlace();
        WriteVelocities();
        // Forme de la fin du pas (avec celle du début du pas, les joints freinaient l'étirement : 5 m/s au lieu de 8).
        _stretch = math.max(0f, length + rate * dt - _startLength);
        StretchShape(_stretch);
        FillBend(MaxBend * math.min(1f, _stretch / BendStretch), _dir);
        _blob.SetShape(_extra, _bend);
    }

    const float RampTime = 0.06f;    // s pour atteindre la vitesse d'étirement
    const float MinDriveLength = 0.1f;
    const float StillSpeed = 0.5f;   // m/s : en dessous, le blob est « sur place » à l'appui
    const float MaxBend = 0.8f;      // côtés presque droits
    const float BendStretch = 0.4f;  // allongement (m) sur lequel l'anneau passe de rond à gélule
    float _holdTime;

    // Capsules du pas : positions, vitesses, centre et vitesse moyenne ; _dv = changements de vitesse à écrire.
    Vector2[] _p, _vel, _dv;
    float[] _extra, _bend;
    Vector2 _c, _v;

    void EnsureArrays(int n)
    {
        if (_p != null && _p.Length == n) return;
        _p = new Vector2[n];
        _vel = new Vector2[n];
        _dv = new Vector2[n];
        _extra = new float[n];
        _bend = new float[n];
    }

    void ReadBodies()
    {
        int n = _blob.CapsuleCount;
        EnsureArrays(n);
        _c = _v = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            var body = _blob.Capsule(i);
            _p[i] = body.position;
            _vel[i] = body.linearVelocity;
            _dv[i] = Vector2.zero;
            _c += _p[i];
            _v += _vel[i];
        }
        _c /= n;
        _v /= n;
    }

    void WriteVelocities()
    {
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++)
        {
            if (_dv[i] == Vector2.zero) continue;
            var body = _blob.Capsule(i);
            body.linearVelocity = _vel[i] + _dv[i];
        }
    }

    // Étendue du blob le long de l'axe (centres des capsules), en m.
    float AxisLength()
    {
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < _p.Length; i++)
        {
            float s = Vector2.Dot(_p[i] - _c, _dir);
            lo = math.min(lo, s);
            hi = math.max(hi, s);
        }
        return hi - lo;
    }

    // Le « muscle » : vitesse d'écartement 'rate' (m/s) entre les deux bouts le long de l'axe (0 = arrêt net). Seule la part
    // « étirement » du mouvement relatif est corrigée (vitesse relative proportionnelle à la position le long de l'axe) :
    // somme nulle, élan inchangé, rotation laissée.
    void DriveExtension(float rate, float length)
    {
        float ss = 0f, sa = 0f;
        for (int i = 0; i < _p.Length; i++)
        {
            float s = Vector2.Dot(_p[i] - _c, _dir);
            ss += s * s;
            sa += s * Vector2.Dot(_vel[i] - _v, _dir);
        }
        if (ss < 1e-8f) return;
        float dk = rate / math.max(length, MinDriveLength) - sa / ss; // voulu − actuel (vitesse relative = k × s)
        for (int i = 0; i < _p.Length; i++) _dv[i] += _dir * (dk * Vector2.Dot(_p[i] - _c, _dir));
    }

    // Saut sur place (blob immobile à l'appui) : les contacts de l'anneau contre le sol ne sont jamais parfaitement symétriques
    // et le feraient partir de côté en tournant (mesuré : 4 cm, 30 °/s). Pendant l'étirement, vitesse de côté et rotation
    // d'ensemble restent nulles : il retombe là où il était. Seul écart à la physique du mode, et seulement sur place.
    void HoldInPlace()
    {
        int n = _p.Length;
        Vector2 v = Vector2.zero;
        for (int i = 0; i < n; i++) v += _vel[i] + _dv[i];
        v /= n;
        Vector2 side = v - _dir * Vector2.Dot(v, _dir);
        float num = 0f, den = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 r = _p[i] - _c;
            Vector2 rel = _vel[i] + _dv[i] - v;
            num += r.x * rel.y - r.y * rel.x;
            den += r.sqrMagnitude;
        }
        float spin = den > 1e-8f ? num / den : 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 r = _p[i] - _c;
            _dv[i] += -side - new Vector2(-r.y, r.x) * spin;
        }
    }

    // Allongement 'length' le long de l'axe : chaque côté de l'anneau gagne 'length', partagé entre ses capsules selon sin² de
    // leur angle à l'axe (celles des bouts ne s'allongent pas ; la largeur ne change pas).
    void StretchShape(float length)
    {
        int n = _p.Length;
        float wSum = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 r = _p[i] - _c;
            float len2 = r.sqrMagnitude;
            float cos = Vector2.Dot(r, _dir);
            float w = len2 < 1e-10f ? 0f : 1f - cos * cos / len2;
            _extra[i] = w;
            wSum += w;
        }
        float k = wSum > 1e-6f ? 2f * length / wSum : 0f;
        for (int i = 0; i < n; i++) _extra[i] *= k;
    }

    void ReleaseStretch()
    {
        _phase = Phase.Idle;
        _stretch = 0f;
        EnsureArrays(_blob.CapsuleCount);
        System.Array.Clear(_extra, 0, _extra.Length);
        System.Array.Clear(_bend, 0, _bend.Length);
        _blob.SetShape(_extra, _bend);
        _amp = _ampGoal = _writtenAmp = 0f;
        _ampReturn = false;
    }

    void ReturnShape()
    {
        _ampGoal = 0f;
        _ampRate = relaxShape ? math.max(math.abs(_amp), 1e-4f) / relaxTime : 1e9f; // décoché : lâché d'un coup
        _ampReturn = false;
    }


    // ---------- Sol ----------

    // Pour le coyote time : dernier pas où le blob était au sol. La normale (≈ 3 µs) n'est relue qu'un pas sur 4 tant qu'il
    // touche quelque chose ; entre deux lectures, il est compté au sol s'il touche encore et que la dernière lecture l'était.
    void TrackGround(int step)
    {
        if (_blob.TouchingCapsules == 0) return;
        if (step - _trackStep >= GroundTrackInterval)
        {
            _trackStep = step;
            _trackOk = Ground(step, out _, out _);
        }
        else if (_trackOk) _lastGroundStep = step;
    }

    const int GroundTrackInterval = 4;
    // Distance max (m) d'un contact pour compter comme appui. Box2D signale les contacts dès 2 cm et plus (spéculatifs).
    const float GroundTolerance = 0.01f;

    bool CanJump(int step, out Vector2 n)
    {
        n = _lastGroundNormal;
        if (step < _noJumpUntil) return false;
        if (Ground(step, out n, out _)) return true;
        n = _lastGroundNormal;
        return coyoteTime > 0f && (step - _lastGroundStep) * Time.fixedDeltaTime <= coyoteTime;
    }

    Vector2 JumpDirection(Vector2 n)
    {
        Vector2 d = Vector2.Lerp(n, Up(), upBlend);
        return d.sqrMagnitude > 1e-8f ? d.normalized : n;
    }

    // Normale moyenne des appuis (contacts dont la normale est à moins de 'maxGroundAngle' de la verticale). Pour chaque
    // capsule, son meilleur appui (le plus vertical, égalités départagées par la normale) : indépendant de l'ordre de Box2D.
    bool Ground(int step, out Vector2 n, out int supports)
    {
        if (_groundStep == step)
        {
            n = _groundNormal;
            supports = _groundOk ? 1 : 0;
            return _groundOk;
        }
        _groundStep = step;
        _groundOk = false;
        n = _groundNormal = Vector2.zero;
        supports = 0;
        if (_blob.TouchingCapsules == 0) return false;

        Vector2 up = Up();
        float minDot = math.cos(math.radians(maxGroundAngle));
        Vector2 sum = Vector2.zero;
        int count = _blob.CapsuleCount;
        for (int i = 0; i < count; i++)
        {
            if (!_blob.IsTouching(i)) continue;
            var body = _blob.Capsule(i);
            bool found = false;
            Vector2 best = Vector2.zero;
            var contacts = body.GetContacts(Allocator.Temp);
            for (int k = 0; k < contacts.Length; k++)
            {
                var manifold = contacts[k].manifold;
                if (manifold.pointCount == 0) continue;
                float sep = manifold[0].separation;
                for (int p = 1; p < manifold.pointCount; p++) sep = math.min(sep, manifold[p].separation);
                if (sep > GroundTolerance) continue; // contact encore à distance (pente voisine) : il fausserait la normale
                Vector2 normal = contacts[k].shapeA.body == body ? -manifold.normal : manifold.normal;
                if (Vector2.Dot(normal, up) < minDot) continue;
                if (!found || Better(normal, best, up)) best = normal;
                found = true;
            }
            contacts.Dispose();
            if (!found) continue;
            sum += best;
            supports++;
        }
        if (supports == 0 || sum.sqrMagnitude < 1e-12f) return false;
        _groundNormal = n = sum.normalized;
        _groundOk = true;
        _lastGroundStep = step;
        _lastGroundNormal = n;
        return true;
    }

    static bool Better(Vector2 a, Vector2 b, Vector2 up)
    {
        float da = Vector2.Dot(a, up), db = Vector2.Dot(b, up);
        if (da != db) return da > db;
        if (a.x != b.x) return a.x < b.x;
        return a.y < b.y;
    }

    static Vector2 Up()
    {
        Vector2 g = PhysicsWorld.defaultWorld.gravity;
        return g.sqrMagnitude > 1e-8f ? -g.normalized : Vector2.up;
    }

    // ---------- Corps ----------

    Vector2 AverageVelocity()
    {
        Vector2 v = Vector2.zero;
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++) v += _blob.Capsule(i).linearVelocity;
        return v / n;
    }

    void AddVelocity(Vector2 dv)
    {
        int n = _blob.CapsuleCount;
        for (int i = 0; i < n; i++)
        {
            var body = _blob.Capsule(i);
            body.linearVelocity += dv;
        }
    }

    // ---------- Forme ----------

    void StartPulse(Vector2 axis, float amplitude, float rate)
    {
        _shapeAxis = axis;
        _ampGoal = amplitude;
        _ampRate = rate;
        _ampReturn = true;
        _releaseWait = 0f;
    }

    void UpdateShape(float dt)
    {
        if (_amp != _ampGoal)
        {
            float d = _ampRate * dt;
            _amp = _amp < _ampGoal ? math.min(_ampGoal, _amp + d) : math.max(_ampGoal, _amp - d);
        }
        else if (_ampReturn && _amp != 0f)
        {
            // Lâché d'un coup, l'anneau (joints rigides) se détend sec : contre le sol, cette détente le pousserait (saut plus
            // haut, dérive). Il n'est donc lâché qu'en l'air, ou au plus tard après 'relaxTime'.
            _releaseWait += dt;
            if (relaxShape || _blob.TouchingCapsules == 0 || _releaseWait >= relaxTime) ReturnShape();
        }
        if (_amp == 0f && _writtenAmp == 0f) return;
        WriteShape(_amp, _shapeAxis);
    }

    // Impulse : étirement visuel seul (aucun allongement de capsule).
    void WriteShape(float amp, Vector2 axis)
    {
        EnsureArrays(_blob.CapsuleCount);
        System.Array.Clear(_extra, 0, _extra.Length);
        FillBend(amp, axis);
        _blob.SetShape(_extra, _bend);
        _writtenAmp = amp;
    }

    // Angle de repos de chaque joint : (2π / N) × amp × cos(2ψ), ψ = position du joint sur l'anneau par rapport à l'axe.
    // Somme nulle sur l'anneau (il reste fermé) ; amp > 0 : bouts de l'axe plus courbés = blob allongé le long de l'axe.
    // Orientation de l'anneau lue sur 4 capsules (capsule i posée à l'angle i × 2π / N, tournée d'autant).
    void FillBend(float amp, Vector2 axis)
    {
        int n = _bend.Length;
        if (amp == 0f)
        {
            System.Array.Clear(_bend, 0, n);
            return;
        }
        float step = 2f * math.PI / n;
        float cs = 0f, sn = 0f;
        for (int q = 0; q < 4; q++)
        {
            int i = q * n / 4;
            float r = _blob.Capsule(i).rotation.radians - i * step;
            cs += math.cos(r);
            sn += math.sin(r);
        }
        float rho = math.atan2(sn, cs);
        float axisAngle = math.atan2(axis.y, axis.x);
        for (int j = 0; j < n; j++) _bend[j] = amp * math.cos(2f * (rho + (j - 0.5f) * step - axisAngle)) * step;
    }
}
