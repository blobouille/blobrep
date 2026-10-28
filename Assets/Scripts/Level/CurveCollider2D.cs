using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CurvePath2D))]
public class CurveCollider2D : MonoBehaviour, ILevelPhysics, IPathRider
{
    [Header("Matériau")]
    [Tooltip("Friction de la surface (0 = glisse parfaitement).")]
    [Min(0)] public float friction = 0.4f;
    [Tooltip("Rebond de la surface : 0 = aucun rebond, 1 = rebond parfait.")]
    [Min(0)] public float bounciness = 0f;

    [Header("Paroi")]
    [Tooltip("Coché : paroi PLEINE sur toute son épaisseur (capsules le long de la courbe, rayon = ½ épaisseur), au lieu de\n" +
             "chaînes. Une chaîne ne retient que de sa face : une capsule passée derrière traverse librement, et une paroi mince\n" +
             "se laisse traverser par un gumori tiré de l'autre côté (fusion à travers un goulot). Ignore le mode de collision.")]
    public bool solidWall;

    [Header("Casse")]
    [Tooltip("Coché : paroi PLEINE découpée en morceaux qui se fêlent puis cèdent sous les explosions (GumoriBombs) ; un\n" +
             "morceau cédé laisse un trou par où les gumori s'échappent. Paroi fixe seulement (pas sur une trajectoire).")]
    public bool breakable;
    [Tooltip("Longueur visée d'un morceau (m), le long de la courbe (arrondie pour un nombre entier de morceaux).")]
    [Min(0.2f)] public float pieceLength = 1f;
    [Tooltip("Explosions (dégâts) pour faire céder un morceau.")]
    [Range(1, 6)] public int pieceHits = 2;

    PhysicsBody _body;
    const float MinGap = 0.01f;

    // Parois FIXES en jeu et leur ligne médiane en coordonnées monde (une fusion ne fait pas naître un gumori à travers :
    // GumoriFusion). Ordre de la liste sans importance (seulement des tests oui / non).
    public static readonly List<CurveCollider2D> Fixed = new List<CurveCollider2D>();
    public Vector2[] WorldLine { get; private set; }

    public struct Piece
    {
        public float from, to;   // le long de WorldLine (m)
        public int first, count; // points de son tracé dans PiecePoints
        public int hitsLeft;
        public bool broken;
    }

    // Parois cassables en jeu (ordre de création des parois : les explosions les parcourent dans cet ordre).
    public static readonly List<CurveCollider2D> Breakables = new List<CurveCollider2D>();
    readonly List<Piece> _pieces = new List<Piece>();
    readonly List<Vector2> _piecePoints = new List<Vector2>();
    readonly List<PhysicsBody> _pieceBodies = new List<PhysicsBody>();
    public IReadOnlyList<Piece> Pieces => _pieces;
    public IReadOnlyList<Vector2> PiecePoints => _piecePoints;
    public int BrokenCount { get; private set; }
    // Morceau fêlé (indice, coups restants ; 0 = cédé), pendant le pas, aussi pendant le rejeu accéléré.
    public event System.Action<int, int> PieceHit;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        var path = GetComponent<CurvePath2D>();
        path.Rebuild();
        bool pieces = breakable && !OnPath;
        if (!pieces && solidWall) BuildSolid(path);
        else if (!pieces) Build(path.BuildCollisionChains());
        if (!OnPath)
        {
            var line = new List<Vector2>(path.center.Count + 1);
            foreach (var p in path.center) line.Add(transform.TransformPoint(p));
            if (path.closed && line.Count > 2) line.Add(line[0]);
            WorldLine = line.ToArray();
            Fixed.Add(this);
        }
        if (pieces) BuildPieces(path);
    }

    // Capsules sur la ligne médiane (points à moins de 'MinGap' sautés), bouts arrondis qui se recouvrent : surface lisse.
    void BuildSolid(CurvePath2D path)
    {
        Matrix4x4 toBody = CreateBody();
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        shapeDef.surfaceMaterial = material;
        float radius = 0.5f * path.thickness * Mathf.Abs(transform.lossyScale.x);
        var points = new List<Vector2>(path.center.Count + 1);
        foreach (var p in path.center)
        {
            Vector2 w = toBody.MultiplyPoint3x4(transform.TransformPoint(p));
            if (points.Count == 0 || (w - points[points.Count - 1]).sqrMagnitude >= MinGap * MinGap) points.Add(w);
        }
        if (path.closed && points.Count > 2) points.Add(points[0]);
        for (int i = 1; i < points.Count; i++)
            _body.CreateShape(new CapsuleGeometry { center1 = points[i - 1], center2 = points[i], radius = radius }, shapeDef);
    }

    // Paroi cassable : WorldLine découpée en morceaux de longueur égale, chacun son corps statique de capsules (comme une
    // paroi pleine) : un morceau qui cède est détruit seul.
    void BuildPieces(CurvePath2D path)
    {
        var line = WorldLine;
        float total = 0f;
        for (int i = 1; i < line.Length; i++) total += (line[i] - line[i - 1]).magnitude;
        if (total <= 0f) return;
        int count = Mathf.Max(1, Mathf.RoundToInt(total / pieceLength));
        float step = total / count;
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = PhysicsBody.BodyType.Static;
        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        shapeDef.surfaceMaterial = material;
        float radius = 0.5f * path.thickness * Mathf.Abs(transform.lossyScale.x);
        int seg = 1;
        float segStart = 0f;
        for (int k = 0; k < count; k++)
        {
            float from = k * step, to = k == count - 1 ? total : (k + 1) * step;
            var piece = new Piece { from = from, to = to, first = _piecePoints.Count, hitsLeft = pieceHits };
            _piecePoints.Add(PointAt(from));
            while (seg < line.Length)
            {
                float segEnd = segStart + (line[seg] - line[seg - 1]).magnitude;
                if (segEnd >= to) break;
                if ((line[seg] - _piecePoints[_piecePoints.Count - 1]).sqrMagnitude >= MinGap * MinGap) _piecePoints.Add(line[seg]);
                segStart = segEnd;
                seg++;
            }
            Vector2 end = PointAt(to);
            if ((end - _piecePoints[_piecePoints.Count - 1]).sqrMagnitude >= MinGap * MinGap || _piecePoints.Count - piece.first < 2) _piecePoints.Add(end);
            piece.count = _piecePoints.Count - piece.first;
            var body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
            for (int i = 1; i < piece.count; i++)
                body.CreateShape(new CapsuleGeometry { center1 = _piecePoints[piece.first + i - 1], center2 = _piecePoints[piece.first + i], radius = radius }, shapeDef);
            _pieces.Add(piece);
            _pieceBodies.Add(body);
        }
        BrokenCount = 0;
        Breakables.Add(this);
    }

    // Point de WorldLine à la distance 'along' (m) de son début.
    public Vector2 PointAt(float along)
    {
        var line = WorldLine;
        for (int i = 1; i < line.Length; i++)
        {
            float len = (line[i] - line[i - 1]).magnitude;
            if (along <= len || i == line.Length - 1) return Vector2.Lerp(line[i - 1], line[i], len > 0f ? Mathf.Clamp01(along / len) : 0f);
            along -= len;
        }
        return line.Length > 0 ? line[0] : (Vector2)transform.position;
    }

    // Morceau cédé à cet endroit de WorldLine (dessin du verre).
    public bool BrokenAt(float along)
    {
        for (int i = 0; i < _pieces.Count; i++)
            if (_pieces[i].broken && along >= _pieces[i].from && along <= _pieces[i].to) return true;
        return false;
    }

    // Distance (m) du point au tracé du morceau (ligne médiane).
    public float PieceDistance(int index, Vector2 p)
    {
        var piece = _pieces[index];
        float best = float.MaxValue;
        for (int i = 1; i < piece.count; i++)
        {
            Vector2 a = _piecePoints[piece.first + i - 1], ab = _piecePoints[piece.first + i] - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, (p - a - ab * t).magnitude);
        }
        return best;
    }

    // Explosion (pendant le pas) : chaque morceau entier à moins de 'radius' du point perd 'damage' coups ; à 0 il cède
    // (corps détruit). Rend le nombre de morceaux touchés.
    public int Damage(Vector2 at, float radius, int damage)
    {
        int hit = 0;
        for (int i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            if (piece.broken || PieceDistance(i, at) > radius) continue;
            piece.hitsLeft = Mathf.Max(0, piece.hitsLeft - damage);
            if (piece.hitsLeft == 0)
            {
                piece.broken = true;
                BrokenCount++;
                if (_pieceBodies[i].isValid) _pieceBodies[i].Destroy();
            }
            _pieces[i] = piece;
            hit++;
            PieceHit?.Invoke(i, piece.hitsLeft);
        }
        return hit;
    }

    void OnDisable()
    {
        Fixed.Remove(this);
        Breakables.Remove(this);
        for (int i = 0; i < _pieceBodies.Count; i++)
            if (_pieceBodies[i].isValid) _pieceBodies[i].Destroy();
        _pieceBodies.Clear();
        _pieces.Clear();
        _piecePoints.Clear();
        Cleanup();
    }

    public PhysicsBody Body => _body;

    // Enfant d'une trajectoire (MovingPath) : corps kinematic posé sur le Transform et piloté par elle ; sinon statique à
    // l'origine du monde, points en coordonnées monde (inchangé).
    bool OnPath => transform.parent != null && transform.parent.TryGetComponent(out MovingPath path) && path.enabled;

    public void PathTick(PathPose now, PathPose next, bool rotate, bool teleport)
    {
        if (!_body.isValid) return;
        if (teleport)
            _body.transform = new PhysicsTransform(now.position, rotate ? PhysicsRotate.FromDegrees(now.angle) : _body.rotation);
        float dt = Time.fixedDeltaTime;
        _body.linearVelocity = (next.position - _body.position) / dt;
        if (rotate) _body.angularVelocity = Mathf.DeltaAngle(_body.rotation.degrees, next.angle) / dt;
    }

    // Corps statique à l'origine (points en coordonnées monde) ou kinematic posé sur le Transform (trajectoire) ; rend la
    // matrice monde → corps.
    Matrix4x4 CreateBody()
    {
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bool onPath = OnPath;
        bodyDef.type = onPath ? PhysicsBody.BodyType.Kinematic : PhysicsBody.BodyType.Static;
        Matrix4x4 toBody = Matrix4x4.identity;
        if (onPath)
        {
            float angle = transform.eulerAngles.z;
            bodyDef.position = (Vector2)transform.position;
            bodyDef.rotation = PhysicsRotate.FromDegrees(angle);
            toBody = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, angle), Vector3.one).inverse;
        }
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        if (onPath) _body.sleepingAllowed = false;
        return toBody;
    }

    void Build(List<CollisionChain> chains)
    {
        bool onPath = OnPath;
        Matrix4x4 toBody = CreateBody();
        var chainDef = PhysicsChainDefinition.defaultDefinition;
        var material = chainDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = bounciness;
        chainDef.surfaceMaterial = material;

        foreach (var c in chains)
        {
            var points = new List<Vector2>(c.points.Length + 2);
            // Box2D refuse une chaîne dont deux points se touchent presque (< linearSlop) : points trop proches retirés.
            foreach (var p in c.points)
            {
                Vector2 w = onPath ? toBody.MultiplyPoint3x4(transform.TransformPoint(p)) : transform.TransformPoint(p);
                if (points.Count == 0 || (w - points[points.Count - 1]).sqrMagnitude >= MinGap * MinGap) points.Add(w);
            }
            if (c.loop && points.Count > 3 && (points[0] - points[points.Count - 1]).sqrMagnitude < MinGap * MinGap) points.RemoveAt(points.Count - 1);

            if (c.loop)
            {
                chainDef.isLoop = true;
            }
            else
            {
                // Chaîne ouverte Box2D : le premier et le dernier point sont des "ghosts" sans collision.
                int n = points.Count;
                points.Insert(0, points[0] + (points[0] - points[1]));
                points.Add(points[n] + (points[n] - points[n - 1]));
                chainDef.isLoop = false;
            }

            _body.CreateChain(points.ToArray(), chainDef);
        }
    }

    void Cleanup()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
    }
}
