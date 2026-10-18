using Unity.Mathematics;
using UnityEngine;

// Motif de casse d'une vitre, façon verre réel : fissures radiales ondulées qui se ramifient en s'éloignant de l'impact,
// fissures concentriques courbes et discontinues (en quinconce). Chaque morceau est un polygone quelconque (non convexe),
// découpé en parties convexes d'au plus 8 sommets pour Box2D, puis par le rectangle de la vitre.
// Calcul pur et déterministe (mêmes entrées = mêmes morceaux au bit près). Une seule instance partagée (Shared), tampons
// créés une fois : aucune allocation. Les résultats sont lus juste après Build (la vitre les recopie).
public sealed class GlassFracture
{
    public static readonly GlassFracture Shared = new GlassFracture();

    public const int MaxVerts = 8;
    public const int MaxRays = 32;
    const int SlotsPerRay = 4;                  // radiale principale + 3 ramifications possibles (¼, ½, ¾ du secteur)
    const int MaxSlots = MaxRays * SlotsPerRay;
    const int MaxLevels = 48;
    public const int MaxCells = 700;
    public const int MaxParts = 2400;
    const int RaySub = 2;                       // points intermédiaires d'un segment de radiale (ondulation)
    const int MaxArcSub = 4;
    const int MaxPoly = 200;
    const float WeldDistance = 0.021f;          // Box2D fusionne les sommets à moins de 4 × linearSlop (0,02 m)
    const float MinDepth = 0.012f;              // et retire ceux à moins de 2 × linearSlop d'une arête
    const int Never = int.MaxValue;

    public const byte EdgeCrack = 0, EdgeInternal = 1, EdgeBorder = 2;

    public struct Settings
    {
        public int rays;
        public float firstRing;      // rayon du premier anneau (m)
        public float ringGrowth;     // rapport entre deux écarts d'anneaux successifs
        public float maxRingGap;     // écart maximal entre deux anneaux (m) : au-delà, écart constant et les radiales se ramifient
        public float reach;          // portée des fissures concentriques (m)
        public float ringContinuity; // probabilité qu'un segment d'anneau soit fissuré
        public float angleJitter;
        public float radiusJitter;
        public float waviness;       // ondulation des fissures radiales (0 à 1)
        public float branching;      // probabilité qu'une ramification apparaisse quand le secteur s'élargit
        public float curvature;      // courbure des fissures concentriques (0 = droites)
        public uint seed;
    }

    // Morceaux
    public int CellCount { get; private set; }
    public readonly int[] CellPartStart = new int[MaxCells];
    public readonly int[] CellPartCount = new int[MaxCells];
    public readonly Vector2[] Centroid = new Vector2[MaxCells];
    public readonly float[] Area = new float[MaxCells];
    public readonly float[] InnerRadius = new float[MaxCells];
    public readonly float[] Radius = new float[MaxCells];
    public readonly bool[] Outer = new bool[MaxCells];
    public readonly bool[] TouchesEnd = new bool[MaxCells];

    // Parties convexes (repère de la vitre) ; PartEdges[i] = nature de l'arête qui part du sommet i
    public int PartCount { get; private set; }
    public readonly Vector2[] PartVerts = new Vector2[MaxParts * MaxVerts];
    public readonly byte[] PartEdges = new byte[MaxParts * MaxVerts];
    public readonly int[] PartVertCount = new int[MaxParts];

    readonly float[] _radii = new float[MaxLevels + 1];
    int _levels; // niveaux 0 (impact) .. _levels - 1 (lointain)
    readonly float[] _slotBase = new float[MaxSlots];
    readonly int[] _slotStart = new int[MaxSlots];
    int _slots;
    readonly Vector2[] _rayPt = new Vector2[MaxSlots * MaxLevels];
    readonly float[] _rayRho = new float[MaxSlots * MaxLevels];
    readonly float[] _rayAng = new float[MaxSlots * MaxLevels];
    readonly Vector2[] _raySub = new Vector2[MaxSlots * MaxLevels * RaySub];
    readonly bool[] _arcCracked = new bool[MaxSlots * MaxLevels];
    readonly Vector2[] _arcSub = new Vector2[MaxSlots * MaxLevels * MaxArcSub];
    readonly byte[] _arcSubCount = new byte[MaxSlots * MaxLevels];

    readonly Vector2[] _poly = new Vector2[MaxPoly];
    readonly int[] _idx = new int[MaxPoly];
    readonly int[] _tri = new int[MaxPoly * 3];
    readonly int[] _piece = new int[MaxPoly * MaxVerts];
    readonly int[] _pieceCount = new int[MaxPoly];
    readonly Vector2[] _clipA = new Vector2[24];
    readonly Vector2[] _clipB = new Vector2[32];
    readonly byte[] _flagA = new byte[24];
    readonly byte[] _flagB = new byte[32];
    readonly int[] _merged = new int[MaxVerts + 2];

    int L(int slot, int level) => slot * MaxLevels + level;

    public void Build(Vector2 half, Vector2 origin, in Settings s)
    {
        CellCount = 0;
        PartCount = 0;
        var rng = new Unity.Mathematics.Random(math.max(1u, s.seed));

        float maxDist = 0f;
        for (int c = 0; c < 4; c++)
        {
            var corner = new Vector2((c & 1) == 0 ? -half.x : half.x, (c & 2) == 0 ? -half.y : half.y);
            maxDist = math.max(maxDist, (corner - origin).magnitude);
        }

        // Anneaux : écarts géométriques (firstRing, ringGrowth) plafonnés à maxRingGap, jusqu'à la portée.
        float limit = math.min(s.reach, maxDist);
        bool fullReach = s.reach >= maxDist;
        _radii[0] = 0f;
        _levels = 1;
        float gap = math.max(0.01f, s.firstRing), cap = math.max(gap, s.maxRingGap);
        if ((limit - gap) / cap > MaxLevels - 3) cap = (limit - gap) / (MaxLevels - 3);
        for (float r = gap; r < limit && _levels < MaxLevels - 1; )
        {
            _radii[_levels++] = r;
            gap = math.min(cap, gap * math.max(1f, s.ringGrowth));
            r += gap;
        }
        float far = 2f * maxDist + 1f;
        _radii[_levels++] = far;
        int lastLevel = _levels - 1;

        // Radiales : principales régulières (décalées), ramifications au milieu puis aux quarts de chaque secteur, qui
        // démarrent à l'anneau où le secteur devient plus large que l'écart des anneaux.
        int rays = math.clamp(s.rays, 4, MaxRays);
        float step = 2f * math.PI / rays;
        float start = rng.NextFloat(0f, 2f * math.PI);
        _slots = rays * SlotsPerRay;
        for (int i = 0; i < rays; i++)
            _slotBase[i * SlotsPerRay] = start + step * (i + s.angleJitter * 0.4f * rng.NextFloat(-1f, 1f));
        for (int i = 0; i < rays; i++)
        {
            float a0 = _slotBase[i * SlotsPerRay];
            float a1 = i + 1 < rays ? _slotBase[(i + 1) * SlotsPerRay] : _slotBase[0] + 2f * math.PI;
            float sector = a1 - a0;
            _slotStart[i * SlotsPerRay] = 0;
            int half2 = BranchLevel(sector, 1f, s, ref rng, 1, lastLevel);
            int q1 = half2 == Never ? Never : BranchLevel(sector, 0.5f, s, ref rng, half2 + 1, lastLevel);
            int q3 = half2 == Never ? Never : BranchLevel(sector, 0.5f, s, ref rng, half2 + 1, lastLevel);
            _slotStart[i * SlotsPerRay + 2] = half2;
            _slotStart[i * SlotsPerRay + 1] = q1;
            _slotStart[i * SlotsPerRay + 3] = q3;
            for (int j = 1; j < SlotsPerRay; j++)
                _slotBase[i * SlotsPerRay + j] = a0 + sector * (j + 0.3f * s.angleJitter * rng.NextFloat(-1f, 1f) * 0.5f) / SlotsPerRay;
        }

        // Sommets des radiales : marche aléatoire de l'angle (ondulation à grande échelle) et rayon décalé.
        for (int sl = 0; sl < _slots; sl++)
        {
            int st = _slotStart[sl];
            if (st == Never) continue;
            float wob = 0f;
            for (int k = st; k < _levels; k++)
            {
                float spacing = LocalSpacing(sl, k);
                if (k > st) wob = math.clamp(wob + s.waviness * 0.25f * spacing * rng.NextFloat(-1f, 1f), -0.3f * spacing, 0.3f * spacing);
                float rho;
                if (k == 0) rho = 0f;
                else if (k == lastLevel) rho = far;
                else
                {
                    float g = math.min(_radii[k] - _radii[k - 1], _radii[k + 1] - _radii[k]);
                    rho = _radii[k] + s.radiusJitter * 0.35f * g * rng.NextFloat(-1f, 1f);
                }
                float a = _slotBase[sl] + wob;
                _rayAng[L(sl, k)] = a;
                _rayRho[L(sl, k)] = rho;
                _rayPt[L(sl, k)] = origin + new Vector2(math.cos(a), math.sin(a)) * rho;
            }
            // Ondulation fine entre deux anneaux, bornée par l'écart aux radiales voisines (jamais de croisement).
            for (int k = st; k < lastLevel; k++)
            {
                Vector2 p0 = _rayPt[L(sl, k)], p1 = _rayPt[L(sl, k + 1)];
                Vector2 d = p1 - p0;
                float len = d.magnitude;
                Vector2 n = len > 1e-6f ? new Vector2(-d.y, d.x) / len : Vector2.zero;
                for (int t = 1; t <= RaySub; t++)
                {
                    float f = (float)t / (RaySub + 1);
                    float rhoMid = math.lerp(_rayRho[L(sl, k)], _rayRho[L(sl, k + 1)], f);
                    float room = 0.25f * rhoMid * LocalSpacing(sl, k);
                    float amp = math.min(s.waviness * 0.15f * len, room) * (k == 0 ? 0.5f : 1f);
                    _raySub[L(sl, k) * RaySub + t - 1] = p0 + d * f + n * (amp * rng.NextFloat(-1f, 1f));
                }
            }
        }

        // Arcs entre radiales voisines : fissurés selon ringContinuity (toujours là où une ramification démarre),
        // courbés (bombés vers l'intérieur ou l'extérieur), subdivisés ; l'arc lointain l'est assez pour rester hors de la vitre.
        for (int k = 1; k < _levels; k++)
            for (int a = 0; a < _slots; a++)
            {
                if (!Active(a, k)) continue;
                int b = NextActive(a, k);
                bool beyond = _radii[k] > s.reach;
                _arcCracked[L(a, k)] = k == lastLevel || _slotStart[a] == k || _slotStart[b] == k
                                       || !beyond && rng.NextFloat() < s.ringContinuity;
                float ta = AngleOf(a, k), tb = AngleOf(b, k);
                while (tb <= ta) tb += 2f * math.PI;
                float ra = _rayRho[L(a, k)], rb = _rayRho[L(b, k)];
                float arcLen = 0.5f * (ra + rb) * (tb - ta);
                int n;
                float bow = 0f;
                if (k == lastLevel) n = MaxArcSub;
                else
                {
                    float g = math.min(_radii[k] - _radii[k - 1], _radii[k + 1] - _radii[k]);
                    n = math.clamp((int)math.ceil(arcLen / math.max(0.05f, 1.2f * g)), 1, MaxArcSub);
                    bow = s.curvature * 0.3f * g * rng.NextFloat(-1f, 1f);
                }
                _arcSubCount[L(a, k)] = (byte)n;
                for (int t = 1; t <= n; t++)
                {
                    float f = (float)t / (n + 1);
                    float ang = math.lerp(ta, tb, f);
                    float rho = math.lerp(ra, rb, f) + bow * math.sin(math.PI * f);
                    _arcSub[L(a, k) * MaxArcSub + t - 1] = origin + new Vector2(math.cos(ang), math.sin(ang)) * rho;
                }
            }

        // Morceaux : entre deux radiales voisines, d'un arc fissuré au suivant (ou jusqu'à une ramification).
        for (int k = 0; k < lastLevel; k++)
            for (int a = 0; a < _slots; a++)
            {
                if (!Active(a, k)) continue;
                if (k == 0 ? _slotStart[a] != 0 : !_arcCracked[L(a, k)]) continue;
                int b = NextActive(a, k);
                int m = k + 1;
                while (m < lastLevel && NextActive(a, m) == b && !_arcCracked[L(a, m)]) m++;
                int count = BuildPolygon(a, b, k, m, origin);
                float inner = k == 0 ? 0f : math.min(_rayRho[L(a, k)], _rayRho[L(b, k)]);
                AddCell(count, half, inner, !fullReach && m == lastLevel);
                if (CellCount >= MaxCells) return;
            }
    }

    int BranchLevel(float sector, float fraction, in Settings s, ref Unity.Mathematics.Random rng, int from, int lastLevel)
    {
        bool spawn = rng.NextFloat() < s.branching;
        int jitter = rng.NextInt(0, 2);
        if (!spawn) return Never;
        for (int k = math.max(1, from); k < lastLevel; k++)
        {
            float g = _radii[k + 1] - _radii[k];
            if (k + 1 == lastLevel || _radii[k] > s.reach) return Never;
            if (_radii[k] * sector * fraction * 0.5f > 1.2f * g) return math.min(k + jitter, lastLevel - 1);
        }
        return Never;
    }

    bool Active(int slot, int level) => _slotStart[slot] <= level;

    int NextActive(int slot, int level)
    {
        for (int i = 1; i <= _slots; i++)
        {
            int s = (slot + i) % _slots;
            if (_slotStart[s] <= level) return s;
        }
        return slot;
    }

    float AngleOf(int slot, int level) => _rayAng[L(slot, level)];

    // Écart angulaire aux radiales actives voisines (le plus petit des deux côtés).
    float LocalSpacing(int slot, int level)
    {
        float best = 2f * math.PI;
        for (int i = 1; i < _slots; i++)
        {
            int s = (slot + i) % _slots;
            if (_slotStart[s] > level) continue;
            float d = _slotBase[s] - _slotBase[slot];
            d -= 2f * math.PI * math.floor(d / (2f * math.PI));
            best = math.min(best, d);
            break;
        }
        for (int i = 1; i < _slots; i++)
        {
            int s = (slot - i + _slots) % _slots;
            if (_slotStart[s] > level) continue;
            float d = _slotBase[slot] - _slotBase[s];
            d -= 2f * math.PI * math.floor(d / (2f * math.PI));
            best = math.min(best, d);
            break;
        }
        return best;
    }

    // Contour anti-horaire : radiale a vers l'extérieur, arcs de l'anneau m de a à b, radiale b vers l'intérieur, arc k de b à a.
    int BuildPolygon(int a, int b, int k, int m, Vector2 origin)
    {
        int n = 0;
        void Add(Vector2 p, ref int count, Vector2[] poly)
        {
            if (count < MaxPoly) poly[count++] = p;
        }

        Add(k == 0 ? origin : _rayPt[L(a, k)], ref n, _poly);
        for (int j = k; j < m; j++)
        {
            for (int t = 0; t < RaySub; t++) Add(_raySub[L(a, j) * RaySub + t], ref n, _poly);
            Add(_rayPt[L(a, j + 1)], ref n, _poly);
        }
        int s = a;
        while (true)
        {
            int c = _arcSubCount[L(s, m)];
            for (int t = 0; t < c; t++) Add(_arcSub[L(s, m) * MaxArcSub + t], ref n, _poly);
            int next = NextActive(s, m);
            Add(_rayPt[L(next, m)], ref n, _poly);
            if (next == b || next == s) break;
            s = next;
        }
        for (int j = m - 1; j >= k; j--)
        {
            for (int t = RaySub - 1; t >= 0; t--) Add(_raySub[L(b, j) * RaySub + t], ref n, _poly);
            if (j > k) Add(_rayPt[L(b, j)], ref n, _poly);
            else if (k > 0) Add(_rayPt[L(b, k)], ref n, _poly);
        }
        if (k > 0)
        {
            int c = _arcSubCount[L(a, k)];
            for (int t = c - 1; t >= 0; t--) Add(_arcSub[L(a, k) * MaxArcSub + t], ref n, _poly);
        }
        return n;
    }

    // Polygone quelconque → triangles (oreilles) → parties convexes ≤ 8 sommets (fusion de Hertel-Mehlhorn)
    // → découpe par le rectangle (arêtes marquées : fissure, intérieure, bord de la vitre).
    void AddCell(int n, Vector2 half, float innerRadius, bool outer)
    {
        n = Simplify(n);
        if (n < 3) return;
        if (SignedArea(n) < 0f) System.Array.Reverse(_poly, 0, n);

        int tris = Triangulate(n);
        if (tris == 0) return;
        int pieces = MergeConvex(n, tris);

        int cell = CellCount;
        int firstPart = PartCount;
        float area = 0f;
        Vector2 moment = Vector2.zero;
        bool touchesEnd = false;
        for (int p = 0; p < pieces; p++)
        {
            int count = _pieceCount[p];
            if (count < 3 || PartCount >= MaxParts) continue;
            for (int i = 0; i < count; i++)
            {
                int u = _piece[p * MaxVerts + i], v = _piece[p * MaxVerts + (i + 1) % count];
                _clipA[i] = _poly[u];
                _flagA[i] = v == (u + 1) % n ? EdgeCrack : EdgeInternal;
            }
            count = ClipRect(count, half);
            count = Weld(count);
            if (count < 3) continue;
            if (count <= MaxVerts)
            {
                if (!IsThin(0, count)) StorePart(0, count, half, ref area, ref moment, ref touchesEnd);
                continue;
            }
            // Plus de 8 sommets après la découpe (coin de la vitre) : deux parties convexes par une diagonale.
            int h = count / 2;
            for (int i = 0; i <= h; i++) { _clipB[i] = _clipA[i]; _flagB[i] = _flagA[i]; }
            _flagB[h] = EdgeInternal;
            int second = count - h + 1;
            for (int i = 0; i < second; i++) { _clipB[12 + i] = _clipA[(h + i) % count]; _flagB[12 + i] = _flagA[(h + i) % count]; }
            _flagB[12 + second - 1] = EdgeInternal;
            for (int i = 0; i <= h; i++) { _clipA[i] = _clipB[i]; _flagA[i] = _flagB[i]; }
            if (!IsThin(0, h + 1)) StorePart(0, h + 1, half, ref area, ref moment, ref touchesEnd);
            for (int i = 0; i < second; i++) { _clipA[i] = _clipB[12 + i]; _flagA[i] = _flagB[12 + i]; }
            if (!IsThin(0, second)) StorePart(0, second, half, ref area, ref moment, ref touchesEnd);
        }
        int parts = PartCount - firstPart;
        if (parts == 0 || area < 1e-5f) { PartCount = firstPart; return; }

        Vector2 centroid = moment / area;
        float radius = 0f;
        for (int p = firstPart; p < PartCount; p++)
            for (int i = 0; i < PartVertCount[p]; i++)
                radius = math.max(radius, (PartVerts[p * MaxVerts + i] - centroid).magnitude);

        CellCount++;
        CellPartStart[cell] = firstPart;
        CellPartCount[cell] = parts;
        Centroid[cell] = centroid;
        Area[cell] = area;
        InnerRadius[cell] = innerRadius;
        Radius[cell] = radius;
        Outer[cell] = outer;
        TouchesEnd[cell] = touchesEnd;
    }

    void StorePart(int from, int count, Vector2 half, ref float area, ref Vector2 moment, ref bool touchesEnd)
    {
        if (PartCount >= MaxParts) return;
        int part = PartCount++;
        float pa = 0f;
        Vector2 pc = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = _clipA[from + i], b = _clipA[from + (i + 1) % count];
            float cross = a.x * b.y - a.y * b.x;
            pa += cross;
            pc += (a + b) * cross;
            PartVerts[part * MaxVerts + i] = a;
            PartEdges[part * MaxVerts + i] = _flagA[from + i];
            if (math.abs(math.abs(a.x) - half.x) < 1e-4f) touchesEnd = true;
        }
        PartVertCount[part] = count;
        area += 0.5f * pa;
        moment += pc / 6f;
    }

    // Points presque confondus ou alignés retirés (le découpage en triangles les refuserait).
    int Simplify(int n)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                var p = _poly[i];
                if (m > 0 && (p - _poly[m - 1]).sqrMagnitude < 1.6e-5f) continue;
                _poly[m++] = p;
            }
            if (m > 1 && (_poly[0] - _poly[m - 1]).sqrMagnitude < 1.6e-5f) m--;
            n = m;
            m = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = _poly[(i - 1 + n) % n], b = _poly[i], c = _poly[(i + 1) % n];
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (math.abs(cross) < 1e-7f) continue;
                _idx[m++] = i;
            }
            for (int i = 0; i < m; i++) _poly[i] = _poly[_idx[i]];
            n = m;
        }
        return n;
    }

    float SignedArea(int n)
    {
        float a = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = _poly[i], q = _poly[(i + 1) % n];
            a += p.x * q.y - p.y * q.x;
        }
        return 0.5f * a;
    }

    // Découpage en oreilles (polygone anti-horaire). Si aucune oreille (polygone abîmé), on coupe le sommet le moins rentrant.
    int Triangulate(int n)
    {
        for (int i = 0; i < n; i++) _idx[i] = i;
        int count = n, tris = 0;
        while (count > 3)
        {
            int ear = -1;
            for (int i = 0; i < count && ear < 0; i++)
            {
                int ia = _idx[(i - 1 + count) % count], ib = _idx[i], ic = _idx[(i + 1) % count];
                Vector2 a = _poly[ia], b = _poly[ib], c = _poly[ic];
                if ((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x) <= 0f) continue;
                bool empty = true;
                for (int j = 0; j < count && empty; j++)
                {
                    int ip = _idx[j];
                    if (ip == ia || ip == ib || ip == ic) continue;
                    if (InTriangle(_poly[ip], a, b, c)) empty = false;
                }
                if (empty) ear = i;
            }
            if (ear < 0)
            {
                float best = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = _poly[_idx[(i - 1 + count) % count]], b = _poly[_idx[i]], c = _poly[_idx[(i + 1) % count]];
                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                    if (cross > best) { best = cross; ear = i; }
                }
            }
            int pa = _idx[(ear - 1 + count) % count], pb = _idx[ear], pc = _idx[(ear + 1) % count];
            Vector2 ta = _poly[pa], tb = _poly[pb], tc = _poly[pc];
            if ((tb.x - ta.x) * (tc.y - tb.y) - (tb.y - ta.y) * (tc.x - tb.x) > 0f)
            {
                _tri[tris * 3] = pa; _tri[tris * 3 + 1] = pb; _tri[tris * 3 + 2] = pc;
                tris++;
            }
            for (int j = ear; j < count - 1; j++) _idx[j] = _idx[j + 1];
            count--;
        }
        {
            Vector2 a = _poly[_idx[0]], b = _poly[_idx[1]], c = _poly[_idx[2]];
            if ((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x) > 0f)
            {
                _tri[tris * 3] = _idx[0]; _tri[tris * 3 + 1] = _idx[1]; _tri[tris * 3 + 2] = _idx[2];
                tris++;
            }
        }
        return tris;
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        float d2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
        float d3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
        return d1 >= 0f && d2 >= 0f && d3 >= 0f;
    }

    // Fusion gloutonne de triangles voisins tant que le résultat reste convexe et sous 8 sommets.
    int MergeConvex(int n, int tris)
    {
        for (int t = 0; t < tris; t++)
        {
            _pieceCount[t] = 3;
            for (int i = 0; i < 3; i++) _piece[t * MaxVerts + i] = _tri[t * 3 + i];
        }
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int p = 0; p < tris && !merged; p++)
            {
                int pc = _pieceCount[p];
                if (pc == 0) continue;
                for (int e = 0; e < pc && !merged; e++)
                {
                    int u = _piece[p * MaxVerts + e], v = _piece[p * MaxVerts + (e + 1) % pc];
                    if (v == (u + 1) % n) continue; // bord du morceau, pas une diagonale
                    for (int q = 0; q < tris && !merged; q++)
                    {
                        int qc = _pieceCount[q];
                        if (q == p || qc == 0) continue;
                        int f = -1;
                        for (int i = 0; i < qc; i++)
                            if (_piece[q * MaxVerts + i] == v && _piece[q * MaxVerts + (i + 1) % qc] == u) { f = i; break; }
                        if (f < 0) continue;
                        int total = pc + qc - 2;
                        if (total > MaxVerts) continue;
                        // p de v à u (dans son ordre), puis q strictement entre u et v.
                        int m = 0;
                        for (int i = 0; i < pc; i++) _merged[m++] = _piece[p * MaxVerts + (e + 1 + i) % pc];
                        for (int i = 2; i < qc; i++) _merged[m++] = _piece[q * MaxVerts + (f + i) % qc];
                        if (!ConvexIndices(m)) continue;
                        for (int i = 0; i < m; i++) _piece[p * MaxVerts + i] = _merged[i];
                        _pieceCount[p] = m;
                        _pieceCount[q] = 0;
                        merged = true;
                    }
                }
            }
        }
        int pieces = 0;
        for (int t = 0; t < tris; t++)
        {
            if (_pieceCount[t] == 0) continue;
            if (pieces != t)
            {
                _pieceCount[pieces] = _pieceCount[t];
                for (int i = 0; i < _pieceCount[t]; i++) _piece[pieces * MaxVerts + i] = _piece[t * MaxVerts + i];
            }
            pieces++;
        }
        return pieces;
    }

    bool ConvexIndices(int m)
    {
        for (int i = 0; i < m; i++)
        {
            Vector2 a = _poly[_merged[i]], b = _poly[_merged[(i + 1) % m]], c = _poly[_merged[(i + 2) % m]];
            if ((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x) < -1e-7f) return false;
        }
        return true;
    }

    int ClipRect(int count, Vector2 half)
    {
        count = Clip(count, 0, -1f, -half.x);
        count = Clip(count, 0, 1f, -half.x);
        count = Clip(count, 1, -1f, -half.y);
        count = Clip(count, 1, 1f, -half.y);
        return count;
    }

    // Sutherland-Hodgman sur le demi-plan sign × p[axis] ≥ bound ; l'arête le long de la coupe devient un bord de vitre.
    int Clip(int count, int axis, float sign, float bound)
    {
        int m = 0;
        for (int i = 0; i < count && m < 22; i++)
        {
            Vector2 a = _clipA[i], b = _clipA[(i + 1) % count];
            float da = sign * a[axis] - bound, db = sign * b[axis] - bound;
            if (da >= 0f)
            {
                _clipB[m] = a;
                _flagB[m++] = db >= 0f ? _flagA[i] : _flagA[i];
            }
            if ((da >= 0f) != (db >= 0f))
            {
                _clipB[m] = a + (b - a) * (da / (da - db));
                _flagB[m++] = da >= 0f ? EdgeBorder : _flagA[i];
            }
        }
        for (int i = 0; i < m; i++) { _clipA[i] = _clipB[i]; _flagA[i] = _flagB[i]; }
        return m;
    }

    int Weld(int count)
    {
        int m = 0;
        for (int i = 0; i < count; i++)
        {
            if (m > 0 && (_clipA[i] - _clipB[m - 1]).sqrMagnitude < WeldDistance * WeldDistance)
            {
                if (_flagA[i] == EdgeBorder || _flagB[m - 1] == EdgeInternal) _flagB[m - 1] = _flagA[i];
                continue;
            }
            _clipB[m] = _clipA[i];
            _flagB[m++] = _flagA[i];
        }
        if (m > 1 && (_clipB[0] - _clipB[m - 1]).sqrMagnitude < WeldDistance * WeldDistance) m--;
        for (int i = 0; i < m; i++) { _clipA[i] = _clipB[i]; _flagA[i] = _flagB[i]; }
        return m;
    }

    // Trop fin pour Box2D (enveloppe refusée, avec un avertissement coûteux) : la partie est abandonnée (poussière).
    bool IsThin(int from, int m)
    {
        for (int i = 0; i < m; i++)
        {
            Vector2 a = _clipA[from + i], edge = _clipA[from + (i + 1) % m] - a;
            float len = edge.magnitude, depth = 0f;
            if (len < WeldDistance) return true;
            for (int j = 0; j < m; j++)
                depth = math.max(depth, math.abs(edge.x * (_clipA[from + j].y - a.y) - edge.y * (_clipA[from + j].x - a.x)) / len);
            if (depth < MinDepth) return true;
        }
        return false;
    }
}
