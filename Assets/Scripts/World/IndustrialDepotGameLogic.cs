using UnityEngine;

/// <summary>
/// Controlador principal de la lógica del mapa Depósito Industrial.
/// Puedes agregar aquí más funciones en el futuro (ej. recoger llaves, activar generadores, etc.)
/// </summary>
public class IndustrialDepotGameLogic : MonoBehaviour
{
    [Header("Referencias del Mapa")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar y cuando muera.")]
    public Transform pointStartRespawn;

    private void Start()
    {
        SetupPlayerSpawn();
    }

    private void SetupPlayerSpawn()
    {
        // 1. Encontrar el punto de spawn si no lo arrastraste manualmente en el Inspector
        if (pointStartRespawn == null)
        {
            GameObject spawnObj = GameObject.Find("PointStarRespawn");
            if (spawnObj != null)
            {
                pointStartRespawn = spawnObj.transform;
            }
        }

        if (pointStartRespawn != null)
        {
            // 2. Encontrar al jugador
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("NestedParent_Unpack"); // Fallback a la raíz

            if (player != null)
            {
                // 3. Registrar el punto en el GameManager y mover al jugador allí
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegistrarSpawnJugador(pointStartRespawn.position, pointStartRespawn.rotation);
                    GameManager.Instance.ReaparecerJugador(player);
                }
                else
                {
                    // Fallback de emergencia si no hay GameManager activo
                    CharacterController cc = player.GetComponentInChildren<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    
                    player.transform.position = pointStartRespawn.position;
                    player.transform.rotation = pointStartRespawn.rotation;
                    
                    if (cc != null) cc.enabled = true;
                }
                Debug.Log("[IndustrialDepotGameLogic]: Jugador ubicado correctamente en PointStarRespawn.");
            }
        }
        else
        {
            Debug.LogWarning("[IndustrialDepotGameLogic]: No se encontró ningún objeto llamado 'PointStarRespawn' en la escena.");
        }
    }
}
