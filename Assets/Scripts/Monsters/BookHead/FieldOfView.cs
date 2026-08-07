using UnityEngine;
using UnityEngine.AI;

public class FieldOfView : MonoBehaviour
{
    public float viewRadius = 25f;
    [Range(0, 360)]
    public float viewAngle = 120f;
    public float eyeHeight = 1.8f;

    [Header("Deteccion Trasera (Escucha)")]
    [Tooltip("Si el jugador esta a esta distancia detras del enemigo, lo detecta igual. 0 = desactivado.")]
    public float hearingRadius = 4f;

    [Header("Referencias")]
    public Transform player; // Publica para permitir asignacion directa desde el controlador

    private void Start()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }
        if (player == null)
        {
            Debug.LogWarning("FieldOfView: No se asigno ni se encontro al jugador con el tag 'Player'.");
        }
    }

    // Deteccion SOLO visual (cono frontal)
    public bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 startPoint = transform.position + Vector3.up * eyeHeight;
        Vector3 endPoint   = player.position + Vector3.up * 1f;
        float dist = Vector3.Distance(startPoint, endPoint);

        if (dist > viewRadius) return false;

        Vector3 dirToPlayer = (endPoint - startPoint).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle / 2f) return false;

        RaycastHit hit;
        if (Physics.Linecast(startPoint, endPoint, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Si el raycast golpea algo, verificamos si es el jugador.
            // Con los nuevos personajes separados (PlayerMale / PlayerFemale / PlayerCapsule), 
            // comprobamos si el objeto golpeado pertenece a alguno de ellos por su nombre de raíz o tag.
            bool hitPlayer = hit.transform.CompareTag("Player") || 
                             hit.transform.root == player.root ||
                             hit.transform.root.name.Contains("Player") ||
                             hit.transform.GetComponentInParent<StarterAssets.FirstPersonController>() != null;

            // Si golpeó una pared u obstáculo (no es el jugador), no lo puede ver.
            if (!hitPlayer)
            {
                return false;
            }
        }

        return true;
    }

    // Deteccion COMPLETA: vision frontal + radio de escucha trasero
    // Usar este metodo en la IA para deteccion total
    public bool CanDetectPlayer()
    {
        if (player == null) return false;

        // Radio de escucha: detecta al jugador en plano 2D (ignorando diferencias de altura de pivote)
        if (hearingRadius > 0f)
        {
            Vector3 enemyPos2D = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 playerPos2D = new Vector3(player.position.x, 0f, player.position.z);
            float proximityDist = Vector3.Distance(enemyPos2D, playerPos2D);
            if (proximityDist <= hearingRadius)
                return true;
        }

        // Si no esta dentro del radio de escucha, usar el cono de vision frontal
        return CanSeePlayer();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

        // Cono de vision frontal (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle( viewAngle / 2, false);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(eyePos, eyePos + viewAngleA * viewRadius);
        Gizmos.DrawLine(eyePos, eyePos + viewAngleB * viewRadius);

        // Radio de escucha trasero (rojo)
        if (hearingRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, hearingRadius);
        }
    }

    private Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
            angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
