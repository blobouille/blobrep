using UnityEngine;
using UnityEngine.InputSystem;
using Unity.U2D.Physics;

public class LowLevelRaycastGrabber : MonoBehaviour
{
    [Header("Réglages")]
    public float grabRadius = 0.5f;
    public float pullSpeed = 25.0f;
    public bool drawLine = true;
    public Color lineColor = Color.cyan;

    private PhysicsBody m_TargetBody;
    private bool m_IsDragging = false;
    private PhysicsBody.BodyType m_OriginalBodyType;
    private Vector2 m_WorldPos;

    void Update()
    {
        var currentMouse = Mouse.current;
        if (currentMouse == null) return;

        m_WorldPos = Camera.main.ScreenToWorldPoint(currentMouse.position.value);
        var world = PhysicsWorld.defaultWorld;

        if (currentMouse.leftButton.wasPressedThisFrame)
        {
            // On utilise la structure de requête de Box2D pour trouver une forme sous la souris
            // (Similaire à ce que fait le dépôt d'exemples pour interagir avec le low-level)
            m_IsDragging = false;

            // On récupère tous les corps du monde par défaut pour tester la distance
            using var bodies = world.GetBodies();
            foreach (var body in bodies)
            {
                // On vérifie si le corps est dynamique et proche de la souris
                if (body.type == PhysicsBody.BodyType.Dynamic)
                {
                    if (Vector2.Distance((Vector2)body.position, m_WorldPos) <= grabRadius)
                    {
                        m_TargetBody = body;
                        m_IsDragging = true;

                        // On le passe en Kinematic pour le contrôler proprement
                        m_OriginalBodyType = m_TargetBody.type;
                        m_TargetBody.type = PhysicsBody.BodyType.Kinematic;
                        m_TargetBody.linearVelocity = Vector2.zero;
                        m_TargetBody.angularVelocity = 0f;
                        break;
                    }
                }
            }
        }
        else if (currentMouse.leftButton.wasReleasedThisFrame)
        {
            if (m_IsDragging)
            {
                m_TargetBody.type = m_OriginalBodyType;
            }
            m_IsDragging = false;
            return;
        }

        if (m_IsDragging)
        {
            Vector2 targetPos = Vector2.Lerp(m_TargetBody.position, m_WorldPos, Time.deltaTime * pullSpeed);
            Vector2 velocity = (targetPos - (Vector2)m_TargetBody.position) / Time.deltaTime;

            m_TargetBody.linearVelocity = velocity;
            m_TargetBody.position = targetPos;

            if (drawLine)
                Debug.DrawLine(m_TargetBody.position, m_WorldPos, lineColor);
        }
    }
}