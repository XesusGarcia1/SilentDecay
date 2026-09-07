using UnityEngine;

public class InteractionFocusManager : MonoBehaviour
{
    public static GameObject CurrentFocus { get; private set; }
    public static float CurrentDist { get; private set; }

    // Ítem de alta prioridad directamente bajo la mirilla (BatteryItem, KeycardItem, etc.)
    public static GameObject HighPriorityFocus { get; private set; }

    private static InteractionFocusManager instance;

    // Orden de prioridad: cuanto más BAJO el número, más prioridad tiene.
    // Los ítems coleccionables (pila, tarjeta) tienen mayor prioridad que el cajón.
    private static int GetPriority(GameObject go)
    {
        if (go == null) return 99;
        if (go.GetComponent<BatteryItem>() != null || go.GetComponentInParent<BatteryItem>() != null) return 0;
        if (go.GetComponent<KeycardItem>() != null || go.GetComponentInParent<KeycardItem>() != null) return 1;
        if (go.GetComponent<ModularHospital.DrawerInteract>() != null) return 10;
        return 5;
    }

    private static bool IsBlockedByWall(Vector3 camPos, GameObject obj, Vector3 targetPoint, int layerMask)
    {
        if (obj == null) return true;

        // 1. Probar raycast hacia el punto de impacto exacto en la superficie del objeto
        Vector3 dirToHit = (targetPoint - camPos).normalized;
        float distToHit = Vector3.Distance(camPos, targetPoint);
        if (distToHit > 0.05f)
        {
            RaycastHit wallHit;
            if (Physics.Raycast(camPos, dirToHit, out wallHit, distToHit - 0.05f, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.collider != null)
                {
                    GameObject obstacle = wallHit.collider.gameObject;
                    if (obstacle != obj && !obstacle.transform.IsChildOf(obj.transform) && !obj.transform.IsChildOf(obstacle.transform))
                    {
                        if (obj.GetComponent<ProceduralDoorInteract>() == null && obj.GetComponentInParent<ProceduralDoorInteract>() == null)
                        {
                            return true; // Pared o colisionador sólido intermedio
                        }
                    }
                }
            }
        }

        // 2. Probar raycast hacia el centro físico real del objeto (transform.position o bounds.center)
        Vector3 objCenter = obj.transform.position;
        Collider objCol = obj.GetComponent<Collider>();
        if (objCol != null) objCenter = objCol.bounds.center;

        Vector3 dirToCenter = (objCenter - camPos).normalized;
        float distToCenter = Vector3.Distance(camPos, objCenter);
        if (distToCenter > 0.1f)
        {
            RaycastHit wallHitCenter;
            if (Physics.Raycast(camPos, dirToCenter, out wallHitCenter, distToCenter - 0.05f, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (wallHitCenter.collider != null)
                {
                    GameObject obstacle = wallHitCenter.collider.gameObject;
                    if (obstacle != obj && !obstacle.transform.IsChildOf(obj.transform) && !obj.transform.IsChildOf(obstacle.transform))
                    {
                        if (obj.GetComponent<ProceduralDoorInteract>() == null && obj.GetComponentInParent<ProceduralDoorInteract>() == null)
                        {
                            return true; // Pared interpuesta bloqueando el cuerpo del objeto
                        }
                    }
                }
            }
        }

        return false;
    }

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
        HighPriorityFocus = null;
        CurrentDist = 999f;

        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~LayerMask.GetMask("Player");

        RaycastHit[] hits = Physics.RaycastAll(ray, 5f, layerMask, QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Elegir el objeto de mayor prioridad dentro del rayo que NO esté bloqueado por una pared
        int bestPriority = 99;
        GameObject bestObj = null;
        float bestDist = 999f;

        Vector3 camPos = cam.transform.position;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            GameObject go = hit.collider.gameObject;

            if (IsBlockedByWall(camPos, go, hit.point, layerMask))
            {
                continue; // Ignorar objetos bloqueados por muros
            }

            int p = GetPriority(go);
            if (p < bestPriority)
            {
                bestPriority = p;
                bestObj = go;
                bestDist = hit.distance;
            }
        }

        CurrentFocus = bestObj;
        CurrentDist = bestDist;

        if (bestPriority <= 1)
            HighPriorityFocus = bestObj;
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

        // Descarte rápido por distancia física
        float distToPlayer = Vector3.Distance(cam.transform.position, obj.transform.position);
        if (distToPlayer > maxDist * 3.0f) return false;

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~LayerMask.GetMask("Player");

        RaycastHit[] hits = Physics.RaycastAll(centerRay, maxDist, layerMask, QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 camPos = cam.transform.position;

        // Determinar la prioridad más alta (número más bajo) entre todos los objetos sin pared interpuesta
        int highestPriorityInPath = 99;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            GameObject hitGo = hit.collider.gameObject;

            if (IsBlockedByWall(camPos, hitGo, hit.point, layerMask))
            {
                continue; // Ignorar objetos tapados por paredes
            }

            int p = GetPriority(hitGo);
            if (p < highestPriorityInPath) highestPriorityInPath = p;
        }

        // Buscar el mejor hit que corresponda al objeto solicitado
        RaycastHit bestMatchHit = default;
        bool found = false;
        int objBestPriority = 99;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            GameObject hitGo = hit.collider.gameObject;

            bool matches = (hitGo == obj) ||
                           hitGo.transform.IsChildOf(obj.transform) ||
                           obj.transform.IsChildOf(hitGo.transform);

            // Caso especial: KeycardItem dentro del cajón
            if (!matches)
            {
                KeycardItem kInObj = obj.GetComponentInChildren<KeycardItem>(true);
                KeycardItem kInHit = hitGo.GetComponentInChildren<KeycardItem>(true);
                if (kInObj != null && kInHit != null && kInObj == kInHit) matches = true;
            }

            if (matches)
            {
                // Verificar obstáculo sólido para el objeto objetivo
                if (IsBlockedByWall(camPos, obj, hit.point, layerMask))
                {
                    continue; // Bloqueado por pared o colisionador físico
                }

                int p = GetPriority(hitGo);
                if (p < objBestPriority)
                {
                    objBestPriority = p;
                    bestMatchHit = hit;
                    found = true;
                }
            }
        }

        if (!found) return false;

        // Si hay un objeto de mayor prioridad en el rayo que NO pertenece a obj y no está tapado, denegar.
        if (objBestPriority > highestPriorityInPath) return false;

        return true;
    }
}
