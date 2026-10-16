using System.Collections.Generic;
using UnityEngine;
using Unity.U2D.Physics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class Ball2D : MonoBehaviour, ICameraTarget, ILevelPhysics, ITrainingBody
{
    [Header("Balle")]
    [Tooltip("Diamètre de la balle, en unités Unity (absolu). 1 = standard du projet (même taille qu'un BLOB de diamètre 1).")]
    [Min(0.01f)] public float diameter = 1f;
    [Tooltip("Type de corps : Dynamic = soumise à la gravité et aux chocs, Kinematic = suit sa vitesse sans être poussée, Static = immobile.")]
    public PhysicsBody.BodyType typeCorps = PhysicsBody.BodyType.Kinematic;

    [Header("Mouvement")]
    [Tooltip("Vitesse linéaire initiale, en unités Unity par seconde.")]
    public Vector2 vitesseLineaire = Vector2.zero;
    [Tooltip("Vitesse angulaire initiale, en radians par seconde (positif = anti-horaire).")]
    public float vitesseAngulaire = 0f;

    [Header("Collisions")]
    [Tooltip("Mode 'bullet' de Box2D v3 (fastCollisionsAllowed) : collision continue aussi contre les corps dynamiques.\n" +
             "Même décoché, la balle reste en collision continue contre les formes statiques.")]
    public bool collisionsRapides = false;

    [Header("Matériau")]
    [Tooltip("Densité : masse = densité × aire du disque.")]
    [Min(0.0001f)] public float densite = 1f;
    [Tooltip("Friction de surface (0 = glisse parfaitement).")]
    [Range(0f, 2f)] public float friction = 0.6f;
    [Tooltip("Rebond : 0 = aucun rebond, 1 = rebond parfait.")]
    [Range(0f, 1f)] public float rebond = 0.3f;
    [Tooltip("Résistance au roulement : freine la rotation au contact (0 = aucune).")]
    [Min(0f)] public float resistanceRoulement = 0f;

    [Header("Debug")]
    [Tooltip("Couleur de l'aperçu Scene view.")]
    public Color previewColor = Color.white;

    PhysicsBody _body;
#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    public Vector2 CameraPosition => _body.isValid ? _body.position : (Vector2)transform.position;

    public CapsuleGeometry GhostShape => new CapsuleGeometry { center1 = Vector2.zero, center2 = Vector2.zero, radius = 0.5f * diameter };
    public bool IsBroken => false;

    public void GetBodyStates(List<BodyState> states)
    {
        states.Clear();
        if (!_body.isValid) return;
        states.Add(new BodyState { position = _body.position, angle = _body.rotation.radians, velocity = _body.linearVelocity, angularVelocity = _body.angularVelocity });
    }

    public void RestoreBodyStates(IReadOnlyList<BodyState> states, bool broken)
    {
        if (!_body.isValid || states.Count != 1) return;
        _body.transform = new PhysicsTransform(states[0].position, PhysicsRotate.FromRadians(states[0].angle));
        _body.linearVelocity = states[0].velocity;
        _body.angularVelocity = states[0].angularVelocity;
    }

    void OnEnable()
    {
        if (Application.isPlaying) Spawn();
    }

    void OnDisable()
    {
        Despawn();
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void Spawn()
    {
        var bodyDef = PhysicsBodyDefinition.defaultDefinition;
        bodyDef.type = typeCorps;
        bodyDef.position = (Vector2)transform.position;
        bodyDef.linearVelocity = vitesseLineaire;
        bodyDef.angularVelocity = vitesseAngulaire;
        bodyDef.fastCollisionsAllowed = collisionsRapides;
        _body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var shapeDef = PhysicsShapeDefinition.defaultDefinition;
        shapeDef.density = densite;
        var material = shapeDef.surfaceMaterial;
        material.friction = friction;
        material.bounciness = rebond;
        material.rollingResistance = resistanceRoulement;
        shapeDef.surfaceMaterial = material;

        _body.CreateShape(new CircleGeometry { center = Vector2.zero, radius = 0.5f * diameter }, shapeDef);
    }

    void Despawn()
    {
        if (_body.isValid) _body.Destroy();
        _body = default;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = previewColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f * diameter);
#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddCircle(Vector2.zero, 0.5f * diameter);
        _pickArea.Draw(Matrix4x4.Translate(transform.position));
#endif
    }
}
