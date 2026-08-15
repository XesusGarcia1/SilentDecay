using UnityEngine;

/// <summary>
/// Marca una posición de maniquí válida en el mapa para que La Réplica se teletransporte/camufle cuando el jugador no la observe.
/// </summary>
public class MannequinSpot : MonoBehaviour
{
    [Header("Configuración de Nodo")]
    [Tooltip("ID único de este punto de maniquí")]
    public string spotID = "Spot_01";

    [Tooltip("Indica si este nodo está ocupado por un maniquí o por La Réplica")]
    public bool isOccupied = false;

    [Tooltip("Indica si este nodo está actualmente visible en la cámara del jugador")]
    public bool isVisibleByPlayer = false;

    [Tooltip("Los renderers que se ocultarán cuando el monstruo ocupe este spot")]
    public Renderer[] defaultRenderers;
    private Collider[] defaultColliders;

    public void SetOccupiedByMonster(bool state)
    {
        isOccupied = state;
        
        if (defaultRenderers != null)
        {
            foreach (Renderer r in defaultRenderers)
            {
                if (r != null) r.enabled = !state;
            }
        }
        
        if (defaultColliders != null)
        {
            foreach (Collider c in defaultColliders)
            {
                if (c != null) c.enabled = !state;
            }
        }
    }

    private void Awake()
    {
        if (defaultRenderers == null || defaultRenderers.Length == 0)
        {
            defaultRenderers = GetComponentsInChildren<Renderer>();
        }
        defaultColliders = GetComponentsInChildren<Collider>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * 0.5f);
    }
}
