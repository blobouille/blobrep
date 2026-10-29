using Unity.U2D.Physics;
using UnityEngine;

// Moulin (Space, niveaux) : des pales qui tournent lentement autour d'un moyeu fixe et brassent la pile. Corps dynamique sur une
// charnière motorisée (couple limité) : bloqué par une pile trop lourde, il cale au lieu d'écraser les gumori contre les parois.
// Sens inversé toutes les 'reverseEvery' s, compté en pas : déterministe. Rien ne dépasse ≈ 2 cm par pas au bout des pales
// (contact vu à 0,02 m) : garder longueur × vitesse modérées.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gumori/Mécaniques/GumoriMill")]
public class GumoriMill : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Forme")]
    [Tooltip("Nombre de pales, régulièrement réparties.")]
    [Range(1, 6)] public int blades = 2;
    [Tooltip("Longueur d'une pale, du centre du moyeu au bout (m).")]
    [Min(0.1f)] public float length = 2f;
    [Tooltip("Demi-épaisseur des pales (m) : rayon de leurs bouts arrondis.")]
    [Min(0.02f)] public float thickness = 0.2f;
    [Tooltip("Rayon du moyeu (m).")]
    [Min(0.02f)] public float hubRadius = 0.35f;
    [Tooltip("Angle de départ (°).")]
    public float startAngle = 0f;

    [Header("Rotation")]
    [Tooltip("Vitesse de rotation (°/s), positive = sens trigonométrique. Bout des pales : longueur × vitesse en rad/s (2 m à\n" +
             "30 °/s ≈ 3,8 km/h).")]
    public float speed = 30f;
    [Tooltip("Couple max du moteur (N·m) : au-delà, le moulin cale sous la pile.")]
    [Min(0f)] public float torque = 150f;
    [Tooltip("Inverser le sens toutes les … s (0 = jamais).")]
    [Min(0f)] public float reverseEvery = 0f;
    [Tooltip("Densité des pales (kg/m²) : plus lourd = moins bousculé par les gumori.")]
    [Min(0.01f)] public float density = 3f;
    [Tooltip("Friction des pales (0 = glisse parfaitement).")]
    [Min(0f)] public float friction = 0.3f;

    [Header("Affichage")]
    public Color color = new Color(1f, 0.55f, 0.65f);

    PhysicsBody _anchor, _rotor;
    PhysicsHingeJoint _joint;
    int _steps;

    // Angle actuel du rotor (rad) ; hors Play : l'angle de départ.
    public float Angle => _rotor.isValid ? _rotor.rotation.radians : startAngle * Mathf.Deg2Rad;
    public Vector2 Hub => transform.position;

    void OnEnable()
    {
        _steps = 0;
        if (!Application.isPlaying) return;
        var world = PhysicsWorld.defaultWorld;
        var anchorDef = PhysicsBodyDefinition.defaultDefinition;
        anchorDef.type = PhysicsBody.BodyType.Static;
        anchorDef.position = Hub;
        _anchor = world.CreateBody(anchorDef);

        var rotorDef = PhysicsBodyDefinition.defaultDefinition;
        rotorDef.type = PhysicsBody.BodyType.Dynamic;
        rotorDef.position = Hub;
        rotorDef.rotation = PhysicsRotate.FromRadians(startAngle * Mathf.Deg2Rad);
        _rotor = world.CreateBody(rotorDef);
        _rotor.sleepingAllowed = false;

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.density = density;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = 0f;
        material.customColor = color;
        shapeDef.surfaceMaterial = material;
        _rotor.CreateShape(new CircleGeometry { center = Vector2.zero, radius = hubRadius }, shapeDef);
        // Deux pales opposées = une seule barre (pas de jointure au centre).
        bool bar = blades == 2;
        for (int i = 0; i < (bar ? 1 : blades); i++)
        {
            Vector2 dir = BladeDirection(i, 0f) * (length - thickness);
            _rotor.CreateShape(new CapsuleGeometry { center1 = bar ? -dir : Vector2.zero, center2 = dir, radius = thickness }, shapeDef);
        }

        var jointDef = PhysicsHingeJointDefinition.defaultDefinition;
        jointDef.bodyA = _anchor;
        jointDef.bodyB = _rotor;
        jointDef.enableMotor = true;
        jointDef.motorSpeed = speed;
        jointDef.maxMotorTorque = torque;
        _joint = world.CreateJoint(jointDef);
    }

    void OnDisable()
    {
        if (_joint.isValid) _joint.Destroy();
        if (_rotor.isValid) _rotor.Destroy();
        if (_anchor.isValid) _anchor.Destroy();
        _joint = default;
        _rotor = default;
        _anchor = default;
    }

    public void LevelTick()
    {
        _steps++;
        if (reverseEvery <= 0f || !_joint.isValid) return;
        int period = Mathf.Max(1, Mathf.RoundToInt(reverseEvery / Time.fixedDeltaTime));
        if (_steps % period == 0) _joint.motorSpeed = -_joint.motorSpeed;
    }

    // Direction de la pale i (repère du rotor tourné de 'angle' rad).
    public Vector2 BladeDirection(int i, float angle)
    {
        float a = angle + i * 2f * Mathf.PI / blades;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = color;
        float a = startAngle * Mathf.Deg2Rad;
        for (int i = 0; i < blades; i++)
        {
            Vector2 dir = BladeDirection(i, a);
            Vector2 n = new Vector2(-dir.y, dir.x) * thickness;
            Vector2 tip = Hub + dir * (length - thickness);
            Gizmos.DrawLine(Hub + n, tip + n);
            Gizmos.DrawLine(Hub - n, tip - n);
            Gizmos.DrawWireSphere(tip, thickness);
        }
        Gizmos.DrawWireSphere(Hub, hubRadius);
        Gizmos.color = new Color(color.r, color.g, color.b, 0.25f);
        Gizmos.DrawWireSphere(Hub, length);
    }
}
