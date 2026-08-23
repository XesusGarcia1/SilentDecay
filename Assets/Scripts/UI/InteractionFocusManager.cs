using UnityEngine;

public class InteractionFocusManager : MonoBehaviour
{
    public static GameObject CurrentFocus { get; private set; }
    public static float CurrentDist { get; private set; }

    private static InteractionFocusManager instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        CurrentFocus = null;
        CurrentDist = 999f;

        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(ray, out hit, 4.5f, layerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != null)
            {
                CurrentFocus = hit.collider.gameObject;
                CurrentDist = hit.distance;
            }
        }
    }

    public static bool IsFocused(GameObject obj, float maxDist = 3.8f)
    {
        if (ElevatorController.isNotepadOpen) return false;

        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null || obj == null) return false;

        // Auto-crear gestor dinámico en la escena si no existe
        if (instance == null)
        {
            GameObject managerObj = new GameObject("[InteractionFocusManager]");
            instance = managerObj.AddComponent<InteractionFocusManager>();
        }

        // 1. Verificar una distancia física aproximada muy generosa para descartar objetos lejanos rápidamente
        float distToPlayer = Vector3.Distance(cam.transform.position, obj.transform.position);
        if (distToPlayer > maxDist * 3.0f) return false;

        // 2. Raycast frontal desde la mirilla del jugador (centro de la pantalla)
        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(centerRay, out hit, maxDist, layerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != null)
            {
                GameObject hitObj = hit.collider.gameObject;
                
                // Comprobar si el objeto apuntado es obj o su hijo o su padre
                bool isMatch = (hitObj == obj) ||
                               hitObj.transform.IsChildOf(obj.transform) ||
                               obj.transform.IsChildOf(hitObj.transform);

                if (!isMatch)
                {
                    // Si el objeto apuntado contiene el componente obj o es un hijo de obj (ej. KeycardItem dentro del cajón)
                    if (obj.GetComponentInChildren<KeycardItem>(true) != null || hitObj.GetComponentInChildren<KeycardItem>(true) != null)
                    {
                        KeycardItem kInObj = obj.GetComponentInChildren<KeycardItem>(true);
                        KeycardItem kInHit = hitObj.GetComponentInChildren<KeycardItem>(true);
                        if (kInObj != null && kInHit != null && kInObj == kInHit) isMatch = true;
                    }
                }

                if (isMatch)
                {
                    // VERIFICACIÓN ESTRICTA DE OBSTRUCCIÓN DE PAREDES O PILARES FÍSICOS
                    // Lanzar un rayo físico de prueba para ver si colisiona con una pared/muro antes del objeto
                    Vector3 camPos = cam.transform.position;
                    Vector3 targetPos = hit.point;
                    Vector3 dir = (targetPos - camPos).normalized;
                    float distToTarget = Vector3.Distance(camPos, targetPos);

                    RaycastHit wallHit;
                    // Probar si choca con cualquier collider con colisión física (excluyendo triggers)
                    if (Physics.Raycast(camPos, dir, out wallHit, distToTarget - 0.05f, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.collider != null)
                        {
                            GameObject obstacle = wallHit.collider.gameObject;
                            
                            // Si el obstáculo no pertenece al objeto interactuable
                            if (obstacle != obj && !obstacle.transform.IsChildOf(obj.transform) && !obj.transform.IsChildOf(obstacle.transform))
                            {
                                if (obj.GetComponent<ProceduralDoorInteract>() != null || obj.GetComponentInParent<ProceduralDoorInteract>() != null)
                                {
                                    return true;
                                }

                                string oName = obstacle.name.ToLower();
                                if (oName.Contains("wall") || oName.Contains("pared") || oName.Contains("solid") || oName.Contains("pillar") || oName.Contains("column") || oName.Contains("bloque"))
                                {
                                    return false; // Pared interpuesta detectada. Denegar interacción.
                                }
                            }
                        }
                    }

                    return true;
                }
            }
        }

        return false;
    }
}
