using UnityEngine;
using System.Collections;

/// <summary>
/// Controlador principal de la lógica del mapa Depósito Industrial.
/// </summary>
public class IndustrialDepotGameLogic : MonoBehaviour
{
    [Header("Referencias del Mapa")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar y cuando muera.")]
    public Transform pointStartRespawn;

    private void Start()
    {
        // Nota: PauseMenuManager viaja desde el MainMenu como Singleton persistente

        // Instanciar managers globales para el depósito
        if (FindFirstObjectByType<GlobalPipeDripManager>() == null)
        {
            GameObject dripManager = new GameObject("[GlobalPipeDripManager]");
            dripManager.AddComponent<GlobalPipeDripManager>();
        }

        if (FindFirstObjectByType<GlobalRedLightEvent>() == null)
        {
            GameObject lightEvent = new GameObject("[GlobalRedLightEvent]");
            lightEvent.AddComponent<GlobalRedLightEvent>();
        }

        SetupPlayerSpawn();
        SetupGuideMapItemAtStart();

        // 2. Disparar monólogo inicial del jugador con pequeño delay (escena completamente cargada)
        StartCoroutine(TriggerStartMonologueDelayed());
    }

    private IEnumerator TriggerStartMonologueDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        LevelIntroData.TriggerStartMonologue("industrial");
    }


    private void SetupGuideMapItemAtStart()
    {
        // Si el jugador no ha recogido la guía y no hay un ítem GuieMapItem en escena, crearlo asentado en el suelo al inicio
        if (!GuideMapUI.hasGuideMap && FindFirstObjectByType<GuieMapItem>() == null)
        {
            Vector3 origin = Vector3.zero;
            Vector3 forward = Vector3.forward;

            if (pointStartRespawn != null)
            {
                origin = pointStartRespawn.position;
                forward = pointStartRespawn.forward;
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    origin = player.transform.position;
                    forward = player.transform.forward;
                }
            }

            if (origin != Vector3.zero)
            {
                // Raycast hacia abajo para encontrar la superficie exacta del suelo o mesa
                Vector3 rayStart = origin + (forward * 1.2f) + (Vector3.up * 1.5f);
                Vector3 spawnPos;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4.0f))
                {
                    spawnPos = hit.point + Vector3.up * 0.015f; // Asentado plano 1.5cm sobre el suelo
                }
                else
                {
                    spawnPos = origin + (forward * 1.2f) + (Vector3.up * 0.02f);
                }

                // Rotación acostada en el suelo
                Quaternion spawnRot = Quaternion.Euler(90f, Quaternion.LookRotation(forward).eulerAngles.y + 15f, 0f);

                GameObject mapObj = null;

                // Intentar cargar prefab NoteItem si existe
                GameObject notePrefab = Resources.Load<GameObject>("Prefabs/NoteItem");
                if (notePrefab != null)
                {
                    mapObj = Instantiate(notePrefab, spawnPos, spawnRot);
                }
                else
                {
                    mapObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    mapObj.transform.position = spawnPos;
                    mapObj.transform.rotation = spawnRot;
                    mapObj.transform.localScale = new Vector3(0.45f, 0.32f, 1.0f);
                }

                mapObj.name = "[GuieMapItem_Start]";
                if (mapObj.GetComponent<GuieMapItem>() == null)
                {
                    mapObj.AddComponent<GuieMapItem>();
                }
                Debug.Log("[IndustrialDepotGameLogic]: GuieMapItem asentado en el suelo con éxito al inicio.");
            }
        }
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
