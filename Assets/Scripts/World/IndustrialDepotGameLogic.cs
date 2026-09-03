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
        ApplyDevTestSettings();

        // 2. Disparar monólogo inicial del jugador con pequeño delay (escena completamente cargada)
        StartCoroutine(TriggerStartMonologueDelayed());
    }

    private void ApplyDevTestSettings()
    {
        if (DevTestSettings.testModeEnableAll || DevTestSettings.testDepotGiveAllKeys)
        {
            MetalKeyItem.hasMetalKey = true;
            MetalKeyItem.collectedKeys.Add("EXITKEY_01");
            MetalKeyItem.collectedKeys.Add("Access_keys_mannequin");
            MetalKeyItem.collectedKeys.Add("MetalKey");
            MetalKeyItem.collectedKeys.Add("Key_01");
            MetalKeyItem.collectedKeys.Add("Key_02");
            MetalKeyItem.collectedKeys.Add("Key_03");

            // Otorgar todos los IDs de llaves presentes en la escena
            foreach (var k in Resources.FindObjectsOfTypeAll<MetalKeyItem>())
            {
                if (k != null && !string.IsNullOrEmpty(k.keyID))
                {
                    MetalKeyItem.collectedKeys.Add(k.keyID);
                }
            }
            Debug.Log("[IndustrialDepotGameLogic] 🔑 MODO DE PRUEBAS: Todas las llaves del depósito otorgadas al inventario.");
        }

        if (DevTestSettings.testModeEnableAll || DevTestSettings.testDepotGiveGuideMap)
        {
            GuideMapUI.hasGuideMap = true;
            Debug.Log("[IndustrialDepotGameLogic] 🗺️ MODO DE PRUEBAS: Mapa de guía entregado al inventario.");
        }

        if (DevTestSettings.testModeEnableAll || DevTestSettings.testDepotLadderRepaired)
        {
            foreach (var ladder in Resources.FindObjectsOfTypeAll<LadderInteract>())
            {
                if (ladder != null)
                {
                    ladder.isBroken = false;
                    if (ladder.ladderComponents != null)
                    {
                        foreach (var c in ladder.ladderComponents)
                        {
                            if (c != null) c.SetActive(true);
                        }
                    }
                }
            }
            Debug.Log("[IndustrialDepotGameLogic] 🪜 MODO DE PRUEBAS: Escaleras del depósito completamente armadas y reparadas.");
        }
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

                // Intentar cargar prefab de NotaLore de las distintas rutas posibles de Resources
                GameObject notePrefab = Resources.Load<GameObject>("Prefabs/NotaLore");
                if (notePrefab == null) notePrefab = Resources.Load<GameObject>("NotaLore");
                if (notePrefab == null) notePrefab = Resources.Load<GameObject>("Prefabs/NoteItem");

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
            if (spawnObj == null) spawnObj = GameObject.Find("StartGame");
            if (spawnObj != null)
            {
                pointStartRespawn = spawnObj.transform;
            }
        }

        if (pointStartRespawn != null)
        {
            // 2. Encontrar al jugador
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("NestedParent_Unpack");
            if (player == null) player = GameObject.Find("PlayerMale");
            if (player == null) player = GameObject.Find("PlayerFemale");

            if (player != null)
            {
                Vector3 spawnPos = pointStartRespawn.position;
                Quaternion spawnRot = pointStartRespawn.rotation;

                // Raycast de seguridad: asentar exactamente sobre el suelo físico (evita caer al vacío en builds compilados)
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out hit, 5.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spawnPos.y = hit.point.y + 0.05f;
                }

                // 3. Registrar el punto en el GameManager y mover al jugador allí
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegistrarSpawnJugador(spawnPos, spawnRot);
                    GameManager.Instance.ReaparecerJugador(player);
                }
                else
                {
                    // Fallback de emergencia si no hay GameManager activo
                    CharacterController cc = player.GetComponentInChildren<CharacterController>();
                    if (cc == null) cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    
                    player.transform.position = spawnPos;
                    player.transform.rotation = spawnRot;
                    Physics.SyncTransforms();
                    
                    if (cc != null) cc.enabled = true;
                }
                Debug.Log($"[IndustrialDepotGameLogic]: Jugador ubicado correctamente en spawn: {spawnPos}.");
            }
        }
        else
        {
            Debug.LogWarning("[IndustrialDepotGameLogic]: No se encontró ningún objeto llamado 'PointStarRespawn' en la escena.");
        }
    }
}
