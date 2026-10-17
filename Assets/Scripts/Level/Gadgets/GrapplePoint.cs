using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;

// Point d'accroche du grappin (GrappleGadget) : rond rouge sans corps physique, fixe. Le blob ne le touche pas ;
// il s'y accroche s'il est à portée et en vue.
[ExecuteAlways]
[DisallowMultipleComponent]
public class GrapplePoint : MonoBehaviour, ILevelPhysics
{
    [Header("Forme")]
    [Tooltip("Rayon du rond, en unités Unity (absolu). Visuel seulement : aucune collision, la corde s'accroche au centre.")]
    [Min(0.02f)] public float radius = 0.2f;

    [Header("Debug")]
    [Tooltip("Couleur du rond.")]
    public Color color = new Color(1f, 0.15f, 0.15f, 1f);

    // Points actifs, dans l'ordre de création (ordre de la hiérarchie via LevelRestart).
    static readonly List<GrapplePoint> _active = new List<GrapplePoint>();
    public static IReadOnlyList<GrapplePoint> Active => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearActive() => _active.Clear();

    // Position du point pendant l'essai (lue au départ : déplacer l'objet en Play ne compte qu'au redémarrage suivant).
    public Vector2 Position { get; private set; }

#if UNITY_EDITOR
    GizmoPickArea _pickArea;
#endif

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        Position = transform.position;
        _active.Add(this);
    }

    void OnDisable()
    {
        _active.Remove(this);
#if UNITY_EDITOR
        _pickArea?.Dispose();
        _pickArea = null;
#endif
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        var world = PhysicsWorld.defaultWorld;
        world.DrawCircle(Position, radius, color, 0f, PhysicsWorld.DrawFillOptions.All);
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Vector3 c = transform.position;
        Gizmos.color = color;
        const int N = 32;
        for (int k = 0; k < 2; k++)
        {
            float r = k == 0 ? radius : 0.5f * radius;
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                Vector3 cur = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }

#if UNITY_EDITOR
        _pickArea ??= new GizmoPickArea();
        _pickArea.Clear();
        _pickArea.AddCircle(Vector2.zero, radius);
        _pickArea.Draw(Matrix4x4.Translate(c));
#endif
    }
}
