using UnityEngine;

/// <summary>
/// Zona Segura: El jugador es seguro SOLO si está a menos de safeRadius metros
/// del centro del objeto Safe. Usa distancia en lugar de trigger events porque
/// el BoxCollider del Safe puede ser mucho más grande que el área visual segura.
/// </summary>
public class SafeZoneTrigger : MonoBehaviour
{
    [Header("Radio real de la Zona Segura (metros desde el centro del Safe)")]
    public float safeRadius = 20f;

    public static bool isPlayerSafe = false;
    public static SafeZoneTrigger instance;

    private Transform playerTransform;

    void Awake()
    {
        instance = this;
        isPlayerSafe = false;

        // Mantener el collider como trigger para compatibilidad
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("PlayerCapsule");
        if (p == null) p = GameObject.Find("PlayerMale");
        if (p == null) p = GameObject.Find("Player");
        if (p != null) playerTransform = p.transform;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerSafe = (dist <= safeRadius);
    }

    public static bool IsPositionInSafeZone(Vector3 pos)
    {
        if (instance == null) return false;
        return Vector3.Distance(instance.transform.position, pos) <= instance.safeRadius;
    }

    public static void ResetSafety()
    {
        isPlayerSafe = false;
    }
}
