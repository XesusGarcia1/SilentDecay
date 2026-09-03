using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador principal del Mapa Manual de Túneles.
/// Selecciona aleatoriamente por partida:
/// - 3 Subgeneradores (y desactiva el resto)
/// - 1 Consola de Bombeo de Agua (y desactiva el resto)
/// - 1 Escotilla de Escape (y desactiva el resto)
/// Reproduce la música de ambiente de los túneles y configura los sonidos de fallo en las luces.
/// </summary>
public partial class TunnelsGenerator : MonoBehaviour
{
    public static TunnelsGenerator Instance { get; private set; }

    public enum EscapeState
    {
        Idle,
        Draining,
        Ready
    }

    public static EscapeState escapeState = EscapeState.Idle;
    public static Vector3 worldExitPointPos = Vector3.zero;

    private Texture2D alarmBgTex;
    private Texture2D alarmBorderTex;
    private Texture2D alarmProgressTex;
    private Texture2D progressRemainingTex;
    private Texture2D fadeBlackTex;

    [Header("Pruebas / Debug")]
    [Tooltip("Activar automáticamente los 3 subgeneradores al iniciar la partida para pruebas")]
    public bool autoActivateAllGenerators = false;

    [Header("Configuración del Mapa Manual de Túneles")]
    [Tooltip("Duración del evento de drenaje de agua en segundos")]
    public float drainageDuration = 45.0f;

    [Tooltip("Escala del mapa (por defecto 1.0)")]
    public float mapScale = 1.0f;

    [Header("Referencias Opcionales")]
    [Tooltip("Punto de spawn inicial del jugador")]
    public Transform pointStartRespawn;

    [Tooltip("Plano de agua global del mapa")]
    public GameObject waterPlaneObj;

    // ─── CAMPOS DE COMPATIBILIDAD CON SCRIPTS DE ESCENA ───
    [HideInInspector] public int width = 15;
    [HideInInspector] public int height = 15;
    [HideInInspector] public float segmentLength = 2.8f;
    [HideInInspector] public bool[,] grid = new bool[1, 1];
    [HideInInspector] public float playerSpawnCellSize = 2.8f;
    [HideInInspector] public int playerSpawnGridX = 0;
    [HideInInspector] public int playerSpawnGridZ = 0;

    private List<SubGenerator> activeSubGenerators = new List<SubGenerator>();
    private int activatedSubGenCount = 0;
    private GameObject activeConsoleObj;
    private GameObject activeHatchObj;
    private AudioSource pumpAudioSource;
    private AudioSource ambientMusicSource;
    private float currentDrainageTime = 0f;
    private float waterFullY = 0.38f;
    private float waterDrainedY = -0.05f;
    private float interactionTimer = 0f;

    private float victoryFadeAlpha = 0f;
    private float victoryStepAlpha = 0f;
    private int victoryStep = 0;
    private bool exitReached = false;

    public bool IsVictoryActive => exitReached || victoryStep > 0;

    public float VictoryFadeAlpha
    {
        get => victoryFadeAlpha;
        set => victoryFadeAlpha = value;
    }

    private void Awake()
    {
        Instance = this;
        escapeState = EscapeState.Idle;
        worldExitPointPos = Vector3.zero;
        grid = new bool[1, 1];
        exitReached = false;
    }

    private void Start()
    {
        InitManualTunnelsMap();
    }

    public void InitManualTunnelsMap()
    {
        Debug.Log("[TunnelsGenerator] 🛠️ Inicializando controlador de mapa manual de túneles...");
        escapeState = EscapeState.Idle;

        // 1. Configurar spawn del jugador
        SetupPlayerSpawn();

        // 2. Seleccionar 3 Subgeneradores aleatorios (y desactivar el resto)
        SetupRandomSubgenerators();

        // 3. Seleccionar 1 Consola de Bombeo aleatoria (y desactivar el resto)
        SetupRandomWaterPumpConsole();

        // 4. Seleccionar 1 Escotilla de Salida aleatoria (y desactivar el resto)
        SetupRandomEscapeHatch();

        // 5. Configurar Plano de Agua e Inundación
        SetupWaterPlane();

        // 6. Configurar Música de Ambiente y Sonidos de Luces Fallando
        SetupAmbientMusicAndFlickerLights();

        // 7. Configurar Notas de Lore y Pilas
        SetupLoreNotesAndBatteries();

        // 8. Configurar Monstruo (The Phenomenon)
        SetupPhenomenonMonster();
        
        // 9. Configurar Sonidos Ambientales Optimizados (Chispas y Goteras)
        gameObject.AddComponent<TunnelsAmbientAudioManager>();

        // 10. Disparar monólogo inicial
        LevelIntroData.TriggerStartMonologue("tunnels");
    }

    private void SetupPlayerSpawn()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerMale");
        if (player == null) player = GameObject.Find("PlayerFemale");

        GameObject elevatorCabin = GameObject.Find("ArrivalElevatorCabin");
        if (elevatorCabin == null) elevatorCabin = GameObject.Find("ArrivalElevatorPrefab");
        if (elevatorCabin == null) elevatorCabin = GameObject.Find("Ascensor");
        if (elevatorCabin == null) elevatorCabin = GameObject.Find("Elevator");

        if (elevatorCabin != null)
        {
            var ctrl = elevatorCabin.GetComponent<ArrivalElevatorController>();
            if (ctrl == null) ctrl = elevatorCabin.AddComponent<ArrivalElevatorController>();
        }

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (pointStartRespawn != null)
        {
            spawnPos = pointStartRespawn.position;
            spawnRot = pointStartRespawn.rotation;
        }
        else if (player != null)
        {
            spawnPos = player.transform.position;
            spawnRot = player.transform.rotation;
        }

        if (player != null)
        {
            // Raycast hacia abajo para colocar al jugador al ras del suelo
            RaycastHit floorHit;
            if (Physics.Raycast(spawnPos + Vector3.up * 2.0f, Vector3.down, out floorHit, 10.0f))
            {
                spawnPos.y = floorHit.point.y;
            }

            CharacterController cc = player.GetComponentInChildren<CharacterController>(true);
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = spawnPos;
                player.transform.rotation = spawnRot;
                Physics.SyncTransforms();
                cc.enabled = true;
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.InicializarVidasParaMapa(3);
            GameManager.Instance.RegistrarSpawnJugador(spawnPos, spawnRot);
        }
    }

    /// <summary>
    /// Busca recursivamente objetos candidatos por palabra clave excluyendo carpetas contenedoras vacías.
    /// </summary>
    private List<GameObject> FindCandidateObjects(params string[] keywords)
    {
        List<GameObject> candidates = new List<GameObject>();
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var obj in allObjects)
        {
            if (obj == null) continue;

            string nLower = obj.name.ToLower();

            // Excluir carpetas padre contenedoras principales que tengan más de un hijo
            if ((nLower == "generatorts" || nLower == "generators" || nLower == "pump" || nLower == "escape_hatch") && obj.transform.childCount > 0)
            {
                continue;
            }

            foreach (string kw in keywords)
            {
                if (nLower.Contains(kw.ToLower()))
                {
                    if (!candidates.Contains(obj))
                    {
                        candidates.Add(obj);
                    }
                    break;
                }
            }
        }
        return candidates;
    }

    private void SetupRandomSubgenerators()
    {
        activeSubGenerators.Clear();
        activatedSubGenCount = 0;

        if (DevTestSettings.testModeEnableAll || DevTestSettings.autoActivateGenerators)
        {
            autoActivateAllGenerators = true;
        }

        List<GameObject> candidates = FindCandidateObjects("Subgenerator", "SubGenerator");
        Debug.Log($"[TunnelsGenerator] ⚡ Se encontraron {candidates.Count} objetos candidatos de Subgenerador.");

        if (candidates.Count == 0) return;

        // Mezclar aleatoriamente la lista de subgeneradores
        for (int i = 0; i < candidates.Count; i++)
        {
            int rnd = Random.Range(i, candidates.Count);
            var temp = candidates[i];
            candidates[i] = candidates[rnd];
            candidates[rnd] = temp;
        }

        // Elegir exactamente 3 activos por partida
        int activeCount = Mathf.Min(3, candidates.Count);
        string[] letters = new string[] { "A", "B", "C" };

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject sObj = candidates[i];
            if (i < activeCount)
            {
                sObj.SetActive(true);
                SubGenerator subComp = sObj.GetComponent<SubGenerator>();
                if (subComp == null) subComp = sObj.AddComponent<SubGenerator>();
                subComp.generatorName = letters[i];
                subComp.subgeneratorLetter = letters[i];
                subComp.isOn = autoActivateAllGenerators;
                activeSubGenerators.Add(subComp);
                Debug.Log($"[TunnelsGenerator] ⚡ Subgenerador '{letters[i]}' ACTIVADO (Encendido: {autoActivateAllGenerators}) en la partida pos={sObj.transform.position}");
            }
            else
            {
                sObj.SetActive(false);
            }
        }

        if (autoActivateAllGenerators)
        {
            activatedSubGenCount = activeCount;
            Debug.Log("[TunnelsGenerator] ⚡ PRUEBAS: Todos los subgeneradores se activaron automáticamente al iniciar.");
        }
    }

    public void OnSubGeneratorTurnedOn(SubGenerator subGen)
    {
        activatedSubGenCount++;
        Debug.Log($"[TunnelsGenerator] ⚡ Subgenerador {subGen.generatorName} activado. ({activatedSubGenCount}/3)");

        if (activatedSubGenCount >= 3)
        {
            Debug.Log("[TunnelsGenerator] 💧 ¡Los 3 subgeneradores están activos! Consola de bombeo energizada.");
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("monologue_subgens_complete") : "¡Energía reactivada! La consola de bombeo ya puede presurizarse.", 5.0f);
        }
    }

    private void SetupRandomWaterPumpConsole()
    {
        List<GameObject> candidates = FindCandidateObjects("Water_Pump_Console", "WaterPumpConsole", "PumpConsole");
        Debug.Log($"[TunnelsGenerator] 🎛️ Se encontraron {candidates.Count} Consolas de Bombeo candidatos en el mapa.");

        if (candidates.Count == 0) return;

        int chosenIndex = Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (i == chosenIndex)
            {
                activeConsoleObj = candidates[i];
                activeConsoleObj.SetActive(true);
                Debug.Log($"[TunnelsGenerator] 🎛️ Consola de Bombeo ACTIVA elegida en {activeConsoleObj.transform.position}");
            }
            else
            {
                candidates[i].SetActive(false);
            }
        }
    }

    private void SetupRandomEscapeHatch()
    {
        // 1. Asegurar que el objeto o carpeta contenedora ("Escape_Hatch" / "EscapeTrampilla") esté SIEMPRE activo
        GameObject containerObj = GameObject.Find("Escape_Hatch");
        if (containerObj == null) containerObj = GameObject.Find("EscapeTrampilla");
        if (containerObj != null) containerObj.SetActive(true);

        // 2. Buscar únicamente los objetos escotilla individuales (hijos)
        List<GameObject> candidates = new List<GameObject>();
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            
            // Ignorar carpetas contenedoras con más de 0 hijos
            if (obj.transform.childCount > 0 && (obj.name.ToLower() == "escape_hatch" || obj.name.ToLower() == "escapetrampilla"))
            {
                obj.SetActive(true);
                continue;
            }

            string nLower = obj.name.ToLower();
            if (nLower.Contains("escape_hatch_hole") || nLower.Contains("trampilla") || nLower.Contains("escotilla"))
            {
                // Ignorar objetos hijos de otros candidatos para no apagar partes internas como la tapa (EscapeTrampilla)
                if (obj.transform.parent != null && 
                    (obj.transform.parent.name.ToLower().Contains("escape_hatch_hole") || 
                     obj.transform.parent.name.ToLower().Contains("trampilla") || 
                     obj.transform.parent.name.ToLower().Contains("escotilla")))
                {
                    continue;
                }

                if (!candidates.Contains(obj))
                {
                    candidates.Add(obj);
                }
            }
        }

        Debug.Log($"[TunnelsGenerator] 🚪 Se encontraron {candidates.Count} Escotillas de Escape candidatas en el mapa.");

        if (candidates.Count == 0) return;

        // Mezclar y elegir exactamente 1 escotilla activa por partida
        int chosenIndex = Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (i == chosenIndex)
            {
                activeHatchObj = candidates[i];
                activeHatchObj.SetActive(true);

                // Asegurar que toda su cadena de padres esté activa
                Transform curr = activeHatchObj.transform.parent;
                while (curr != null)
                {
                    curr.gameObject.SetActive(true);
                    curr = curr.parent;
                }

                worldExitPointPos = activeHatchObj.transform.position;
                Debug.Log($"[TunnelsGenerator] 🚪 Escotilla de Salida ACTIVA en {worldExitPointPos}");
            }
            else
            {
                candidates[i].SetActive(false);
            }
        }
    }

    private void SetupWaterPlane()
    {
        if (waterPlaneObj == null)
            waterPlaneObj = GameObject.Find("[Global_Tunnel_Water_Plane]");
        if (waterPlaneObj == null)
            waterPlaneObj = GameObject.Find("WaterPlane");

        if (waterPlaneObj == null)
        {
            waterPlaneObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterPlaneObj.name = "[Global_Tunnel_Water_Plane]";
            waterPlaneObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            MeshCollider mc = waterPlaneObj.GetComponent<MeshCollider>();
            if (mc != null) DestroyImmediate(mc);

            BoxCollider bc = waterPlaneObj.GetComponent<BoxCollider>();
            if (bc == null) bc = waterPlaneObj.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1f, 1f, 0.2f);
        }

        // Aumentar la escala a 800m x 800m para cubrir completamente cualquier mapa manual gigante
        waterPlaneObj.transform.localScale = new Vector3(800f, 800f, 1f);

        float floorY = 0f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            RaycastHit floorHit;
            if (Physics.Raycast(player.transform.position + Vector3.up * 1.5f, Vector3.down, out floorHit, 10.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                floorY = floorHit.point.y;
            }
            else
            {
                floorY = player.transform.position.y;
            }
            waterPlaneObj.transform.position = new Vector3(player.transform.position.x, floorY + 0.38f, player.transform.position.z);
        }
        else
        {
            waterPlaneObj.transform.position = new Vector3(0f, 0.38f, 0f);
        }

        waterFullY = floorY + 0.38f;
        waterDrainedY = floorY - 0.05f;

        Material waterMat = Resources.Load<Material>("Texturas/Mundo/Mat_Agua_Tuneles");
        if (waterMat == null) waterMat = Resources.Load<Material>("Mat_Agua_Tuneles");

        Renderer rend = waterPlaneObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = true; // Asegurar que esté encendido al iniciar Setup
            if (waterMat != null)
            {
                Material matInstance = new Material(waterMat);
                matInstance.SetFloat("_Cull", 0f);
                matInstance.color = new Color(0.06f, 0.18f, 0.22f, 0.65f);
                rend.material = matInstance;
            }
        }

        BoxCollider boxCol = waterPlaneObj.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            boxCol.enabled = true; // Asegurar que esté encendido al iniciar Setup
        }

        if (waterPlaneObj.GetComponent<WaterAnimator>() == null)
            waterPlaneObj.AddComponent<WaterAnimator>();
        if (waterPlaneObj.GetComponent<WaterPuddle>() == null)
            waterPlaneObj.AddComponent<WaterPuddle>();
    }

    private void SetupAmbientMusicAndFlickerLights()
    {
        // 1. Música de Ambiente de los Túneles (1 solo AudioSource 2D global)
        if (ambientMusicSource == null)
        {
            ambientMusicSource = gameObject.AddComponent<AudioSource>();
            ambientMusicSource.loop = true;
            ambientMusicSource.spatialBlend = 0f; // 2D global
            ambientMusicSource.volume = 0.40f;

            AudioClip musicClip = Resources.Load<AudioClip>("Audio/Tuneles/AmbienteTunel");
            if (musicClip == null) musicClip = Resources.Load<AudioClip>("AmbienteTunel");
            if (musicClip != null)
            {
                ambientMusicSource.clip = musicClip;
                ambientMusicSource.Play();
                Debug.Log("[TunnelsGenerator] 🎵 Música de ambiente 'AmbienteTunel' iniciada.");
            }
        }

        // 2. Seleccionar SOLO 3 a 4 lámparas aleatorias para parpadear y sonar (evita caída de FPS y saturación de sonido)
        List<GameObject> allLampObjs = new List<GameObject>();
        GameObject[] allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var obj in allObjs)
        {
            if (obj == null) continue;
            string nLower = obj.name.ToLower();

            if (nLower.Contains("fluorescent_light") || nLower.Contains("lampara") || nLower.Contains("tunnel_light"))
            {
                if (!allLampObjs.Contains(obj)) allLampObjs.Add(obj);
            }
        }

        // Mezclar lámparas aleatoriamente
        for (int i = 0; i < allLampObjs.Count; i++)
        {
            int rnd = Random.Range(i, allLampObjs.Count);
            var temp = allLampObjs[i];
            allLampObjs[i] = allLampObjs[rnd];
            allLampObjs[rnd] = temp;
        }

        int flickerLimit = Mathf.Min(4, allLampObjs.Count);
        for (int i = 0; i < flickerLimit; i++)
        {
            if (allLampObjs[i].GetComponent<TunnelLightFlicker>() == null)
            {
                allLampObjs[i].AddComponent<TunnelLightFlicker>();
            }
        }

        Debug.Log($"[TunnelsGenerator] 💡 Rendimiento optimizado: {flickerLimit} de {allLampObjs.Count} lámparas configuradas con fallos de parpadeo y audio 3D.");

        // 3. Configurar Triggers de Zona Segura ("Safe")
        SafeZoneTrigger.ResetSafety();
        int safeTriggersConfigured = 0;

        foreach (var obj in allObjs)
        {
            if (obj == null) continue;
            string nLower = obj.name.ToLower();

            if (nLower == "safe" || nLower == "safezone" || nLower == "safe_zone" || nLower == "safezonetrigger")
            {
                Collider col = obj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                if (obj.GetComponent<SafeZoneTrigger>() == null)
                {
                    obj.AddComponent<SafeZoneTrigger>();
                    safeTriggersConfigured++;
                }
            }
        }

        Debug.Log($"[TunnelsGenerator] 🛡️ Configurados {safeTriggersConfigured} Triggers de Zona Segura ('Safe').");
    }

    private void SetupLoreNotesAndBatteries()
    {
        LoreNoteItem[] notes = FindObjectsByType<LoreNoteItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i] != null)
            {
                notes[i].loreId = (i % 3) + 1;
            }
        }

        GameObject[] sceneObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in sceneObjs)
        {
            if (obj != null && (obj.name.ToLower().Contains("battery") || obj.name.ToLower().Contains("pila")))
            {
                if (obj.GetComponent<BatteryItem>() == null)
                    obj.AddComponent<BatteryItem>();
            }
        }
    }

    private void SetupPhenomenonMonster()
    {
        PhenomenonAIController phenomenon = FindFirstObjectByType<PhenomenonAIController>();
        if (phenomenon != null)
        {
            phenomenon.detectionRange = 0f;
            StartCoroutine(ActivatePhenomenonGraceDelay(phenomenon, 18.0f));
        }
    }

    private IEnumerator ActivatePhenomenonGraceDelay(PhenomenonAIController phenomenon, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (phenomenon != null)
        {
            phenomenon.detectionRange = 15.0f;
            Debug.Log("[TunnelsGenerator] 👻 ¡The Phenomenon activado tras período de gracia!");
        }
    }

    private void Update()
    {
        CheckConsoleInteraction();

        // Centrar continuamente la posición X,Z del agua en el jugador para 100% cobertura del mapa
        if (waterPlaneObj != null && escapeState != EscapeState.Draining)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                waterPlaneObj.transform.position = new Vector3(p.transform.position.x, waterPlaneObj.transform.position.y, p.transform.position.z);
            }
        }

        if (escapeState == EscapeState.Draining)
        {
            currentDrainageTime -= Time.deltaTime;

            float fillRatio = Mathf.Clamp01(currentDrainageTime / drainageDuration);
            float currentWaterY = Mathf.Lerp(waterDrainedY, waterFullY, fillRatio);

            if (waterPlaneObj != null)
            {
                waterPlaneObj.transform.position = new Vector3(waterPlaneObj.transform.position.x, currentWaterY, waterPlaneObj.transform.position.z);
                Renderer r = waterPlaneObj.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    Color waterCol = new Color(0.06f, 0.18f, 0.22f, Mathf.Lerp(0f, 0.65f, fillRatio));
                    if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", waterCol);
                    r.material.color = waterCol;
                }
            }

            if (currentDrainageTime <= 0f)
            {
                FinishDrainage();
            }
        }

        CheckHatchInteraction();
    }

    private void CheckConsoleInteraction()
    {
        if (escapeState != EscapeState.Idle || activeConsoleObj == null) return;

        float dist = Vector3.Distance(activeConsoleObj.transform.position, Camera.main.transform.position);
        if (dist <= 3.5f && InteractionFocusManager.IsFocused(activeConsoleObj, 3.5f))
        {
            if (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown)
            {
                MobileInput.ePressedDown = false;
                if (activatedSubGenCount >= 3)
                {
                    StartWaterDrainage();
                }
                else
                {
                    PlayerMonologueManager.ShowDialogue($"La consola de bombeo está despresurizada... Faltan subgeneradores ({activatedSubGenCount}/3).", 4.0f);
                }
            }
        }
    }

    public void StartWaterDrainage()
    {
        if (escapeState != EscapeState.Idle) return;

        escapeState = EscapeState.Draining;
        currentDrainageTime = drainageDuration;

        Debug.Log("[TunnelsGenerator] 💧 Evacuación de agua iniciada (45s)...");

        // 1. Sonido de impacto inicial
        AudioClip impactClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
        if (impactClip == null) impactClip = Resources.Load<AudioClip>("Apagon_Sonido");
        if (impactClip != null && activeConsoleObj != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, activeConsoleObj.transform.position, 1.0f);
        }

        // 2. Sirena de evacuación/bomba
        AudioClip sirenClip = Resources.Load<AudioClip>("Audio/Tuneles/FloodSiren");
        if (sirenClip == null) sirenClip = Resources.Load<AudioClip>("FloodSiren");
        if (sirenClip != null && activeConsoleObj != null)
        {
            pumpAudioSource = activeConsoleObj.GetComponent<AudioSource>();
            if (pumpAudioSource == null) pumpAudioSource = activeConsoleObj.AddComponent<AudioSource>();
            pumpAudioSource.clip = sirenClip;
            pumpAudioSource.loop = false; // Sonar solo una vez como en el código viejo
            pumpAudioSource.volume = 0.35f;
            pumpAudioSource.Play();
        }
    }

    private void FinishDrainage()
    {
        escapeState = EscapeState.Ready;
        Debug.Log("[TunnelsGenerator] 🟢 Drenaje completado. Escotilla abierta.");

        if (pumpAudioSource != null) pumpAudioSource.Stop();

        // 1. Sonido de éxito
        AudioClip successClip = Resources.Load<AudioClip>("Audio/Hospital/successSound");
        if (successClip == null) successClip = Resources.Load<AudioClip>("successSound");
        if (successClip != null && activeConsoleObj != null)
        {
            AudioSource.PlayClipAtPoint(successClip, activeConsoleObj.transform.position, 1.0f);
        }

        if (waterPlaneObj != null)
        {
            var mr = waterPlaneObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var bc = waterPlaneObj.GetComponent<BoxCollider>();
            if (bc != null) bc.enabled = false;
            waterPlaneObj.transform.position = new Vector3(waterPlaneObj.transform.position.x, -500f, waterPlaneObj.transform.position.z);
        }

        if (activeHatchObj != null)
        {
            Renderer rend = activeHatchObj.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                rend.material.color = Color.green;
                if (rend.material.HasProperty("_EmissionColor"))
                    rend.material.SetColor("_EmissionColor", Color.green * 3f);
            }
        }

        PlayerMonologueManager.ShowDialogue("¡El agua ha sido evacuada! La escotilla de salida está abierta.", 5.0f);
    }

    private void CheckHatchInteraction()
    {
        if (escapeState != EscapeState.Ready || activeHatchObj == null || exitReached) return;

        // Medir distancia horizontal plana para que la altura de la cámara no afecte
        Vector3 playerPos = Camera.main.transform.position;
        Vector3 exitPos = activeHatchObj.transform.position;
        float distExit = Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(exitPos.x, 0f, exitPos.z));

        if (distExit < 4.2f)
        {
            bool isHoldingExit = MobileInput.GetKey(KeyCode.E) || Input.GetKey(KeyCode.E) || MobileInput.ePressed;
            if (isHoldingExit)
            {
                interactionTimer = Mathf.MoveTowards(interactionTimer, 2f, Time.deltaTime);
            }
            else
            {
                interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
            }

            if (interactionTimer >= 2f)
            {
                exitReached = true;
                interactionTimer = 0f;

                // Desactivar controles del jugador inmediatamente al ganar para evitar que siga moviéndose o interactuando
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    var fp = playerObj.GetComponent<StarterAssets.FirstPersonController>();
                    if (fp == null) fp = playerObj.GetComponentInChildren<StarterAssets.FirstPersonController>();
                    if (fp != null) fp.enabled = false;

                    var cc = playerObj.GetComponent<CharacterController>();
                    if (cc == null) cc = playerObj.GetComponentInParent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                }

                // Desactivar al Fenómeno si está en escena para que no ataque durante la victoria
                var monsters = FindObjectsOfType<PhenomenonAIController>();
                foreach (var m in monsters)
                {
                    m.enabled = false;
                }

                StartCoroutine(HandleVictorySequence());
            }
        }
        else
        {
            interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
        }
    }

    private IEnumerator HandleVictorySequence()
    {
        float elapsed = 0f;
        float duration = 1.5f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            victoryFadeAlpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        victoryFadeAlpha = 1f;

        Time.timeScale = 0f;
        MobileInput.SetCursorState(false);

        AudioClip exitClip = Resources.Load<AudioClip>("Audio/Tuneles/SonidoEscape");
        if (exitClip != null) AudioSource.PlayClipAtPoint(exitClip, Camera.main.transform.position, 1.0f);

        victoryStep = 1;
        // Guardar progreso: Nivel 2 completado, desbloquea Nivel 3 (Depósito Industrial)
        PlayerPrefs.SetInt("Campaign_TunnelsCompleted", 1);
        PlayerPrefs.SetInt("Campaign_HospitalTunnelsCompleted", 1); // compatibilidad
        PlayerPrefs.Save();
        Debug.Log("[TunnelsGenerator] 🏆 ¡Nivel 2 (Túneles) completado! Nivel 3 (Depósito Industrial) desbloqueado.");

        yield return StartCoroutine(FadeVictoryStepText(4.2f));
        victoryStep = 2;
        yield return StartCoroutine(FadeVictoryStepText(3.2f));
        victoryStep = 3;
        yield return StartCoroutine(FadeVictoryStepText(4.5f));

        Time.timeScale = 1f;
        if (SilentDecay.Core.AdManager.Instance != null)
        {
            SilentDecay.Core.AdManager.Instance.ShowInterstitialTransition(() =>
            {
                SceneManager.LoadScene("MainMenu");
            });
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    private IEnumerator FadeVictoryStepText(float displayTime)
    {
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            victoryStepAlpha = Mathf.Clamp01(t / 0.6f);
            yield return null;
        }
        victoryStepAlpha = 1f;
        yield return new WaitForSecondsRealtime(displayTime);
        t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            victoryStepAlpha = Mathf.Clamp01(1f - (t / 0.6f));
            yield return null;
        }
        victoryStepAlpha = 0f;
    }

    private void OnGUI()
    {
        // ─── PANTALLA DE VICTORIA Y DESBLOQUEO (NIVEL 2: TÚNELES) ───────────────
        if (victoryStep > 0)
        {
            if (fadeBlackTex == null)
            {
                fadeBlackTex = MakeTex(2, 2, Color.black);
            }

            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeBlackTex);

            GUI.color = new Color(1f, 1f, 1f, victoryStepAlpha);

            LocalizationManager.Idioma lang = LocalizationManager.Idioma.ESPAÑOL;
            if (LocalizationManager.Instance != null)
            {
                lang = LocalizationManager.Instance.GetIdiomaActual();
            }

            float sWidth = Screen.width;
            float sHeight = Screen.height;

            if (victoryStep == 1)
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = Mathf.RoundToInt(sHeight * 0.055f);
                titleStyle.normal.textColor = Color.white;

                GUIStyle subStyle = new GUIStyle(GUI.skin.label);
                subStyle.alignment = TextAnchor.MiddleCenter;
                subStyle.fontStyle = FontStyle.Bold;
                subStyle.fontSize = Mathf.RoundToInt(sHeight * 0.038f);
                subStyle.normal.textColor = new Color(0.3f, 0.95f, 0.4f);

                string winTitle = "¡NIVEL 2: TÚNELES COMPLETADO!";
                string unlockSub = "¡NIVEL 3: DEPÓSITO INDUSTRIAL DESBLOQUEADO!";

                if (lang == LocalizationManager.Idioma.ENGLISH)
                {
                    winTitle = "LEVEL 2: FLOODED TUNNELS COMPLETED!";
                    unlockSub = "LEVEL 3: INDUSTRIAL DEPOT UNLOCKED!";
                }
                else if (lang == LocalizationManager.Idioma.PORTUGUES)
                {
                    winTitle = "NÍVEL 2: TÚNEIS INUNDADOS CONCLUÍDO!";
                    unlockSub = "NÍVEL 3: DEPÓSITO INDUSTRIAL DESBLOQUEADO!";
                }
                else if (lang == LocalizationManager.Idioma.РУССКИЙ)
                {
                    winTitle = "УРОВЕНЬ 2: ТОННЕЛИ ПРОЙДЕНЫ!";
                    unlockSub = "УРОВЕНЬ 3: ПРОМЫШЛЕННЫЙ СКЛАД РАЗБЛОКИРОВАН!";
                }

                GUI.Label(new Rect(0f, sHeight * 0.38f, sWidth, sHeight * 0.10f), winTitle, titleStyle);
                GUI.Label(new Rect(0f, sHeight * 0.50f, sWidth, sHeight * 0.08f), unlockSub, subStyle);
            }
            else if (victoryStep == 2)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontStyle = FontStyle.Bold;
                style.fontSize = Mathf.RoundToInt(sHeight * 0.065f);
                style.normal.textColor = new Color(0.95f, 0.85f, 0.4f);

                string thanksMsg = "¡GRACIAS POR JUGAR!";
                if (lang == LocalizationManager.Idioma.ENGLISH) thanksMsg = "THANK YOU FOR PLAYING!";
                else if (lang == LocalizationManager.Idioma.PORTUGUES) thanksMsg = "OBRIGADO POR JOGAR!";
                else if (lang == LocalizationManager.Idioma.РУССКИЙ) thanksMsg = "СПАСИБО ЗА ИГРУ!";

                GUI.Label(new Rect(0f, 0f, sWidth, sHeight), thanksMsg, style);
            }
            else if (victoryStep == 3)
            {
                GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.fontSize = Mathf.RoundToInt(sHeight * 0.045f);
                headerStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);

                string devTitle = "SIGUE EL DESARROLLO Y NOVEDADES EN:";
                if (lang == LocalizationManager.Idioma.ENGLISH) devTitle = "FOLLOW DEVELOPMENT & UPDATES AT:";
                else if (lang == LocalizationManager.Idioma.PORTUGUES) devTitle = "SIGA O DESENVOLVIMENTO EM:";
                else if (lang == LocalizationManager.Idioma.РУССКИЙ) devTitle = "СЛЕДИТЕ ЗА РАЗРАБОТКОЙ И НОВОСТЯМИ:";

                GUI.Label(new Rect(0f, sHeight * 0.18f, sWidth, sHeight * 0.1f), devTitle, headerStyle);

                // Tarjeta de redes sociales
                GUIStyle cardStyle = new GUIStyle(GUI.skin.label);
                cardStyle.alignment = TextAnchor.MiddleCenter;
                cardStyle.fontStyle = FontStyle.Bold;
                cardStyle.fontSize = Mathf.RoundToInt(sHeight * 0.035f);
                cardStyle.normal.textColor = Color.white;

                string socialText = "INSTAGRAM: @lxesusgarcial\n\n" +
                                    "FACEBOOK: lXesusGarcial\n\n" +
                                    "YOUTUBE: @Xesus_Garcia";

                GUI.Label(new Rect(sWidth * 0.1f, sHeight * 0.35f, sWidth * 0.8f, sHeight * 0.45f), socialText, cardStyle);
            }

            GUI.color = Color.white;
            return;
        }

        if (Time.timeScale == 0f) return;

        DrawSubgeneratorsHUD();

        if (escapeState == EscapeState.Idle && activeConsoleObj != null)
        {
            float dist = Vector3.Distance(activeConsoleObj.transform.position, Camera.main.transform.position);
            if (dist <= 3.5f && InteractionFocusManager.IsFocused(activeConsoleObj, 3.5f))
            {
                DrawPrompt("[E]  Presurizar Consola de Bombeo");
            }
        }
        else if (escapeState == EscapeState.Ready && activeHatchObj != null)
        {
            Vector3 playerPos = Camera.main.transform.position;
            Vector3 exitPos = activeHatchObj.transform.position;
            float distExit = Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(exitPos.x, 0f, exitPos.z));
            if (distExit < 4.2f)
            {
                DrawPrompt("MANTÉN [E] PARA ESCAPAR", interactionTimer / 2f);
            }
        }

        // --- CÓDIGO VIEJO DE LA EVACUACIÓN DE AGUA (ALARMA DE SISTEMA) ---
        if (escapeState == EscapeState.Draining || escapeState == EscapeState.Ready)
        {
            // Ocultar la alarma si el jugador está leyendo una nota de lore o tiene la libreta abierta
            bool isReadingLore = false;
            foreach (var note in Object.FindObjectsOfType<LoreNoteItem>())
            {
                if (note != null && note.IsReading)
                {
                    isReadingLore = true;
                    break;
                }
            }
            bool notepadOpen = NotepadUIManager.IsOpen;

            if (!isReadingLore && !notepadOpen)
            {
                if (alarmBgTex == null) alarmBgTex = MakeTex(2, 2, new Color(0.08f, 0.01f, 0.01f, 0.85f));
                if (alarmBorderTex == null) alarmBorderTex = MakeTex(2, 2, new Color(1f, 0.2f, 0.2f, 0.9f));
                if (alarmProgressTex == null) alarmProgressTex = MakeTex(2, 2, new Color(0.9f, 0.1f, 0.1f, 1f));
                if (progressRemainingTex == null) progressRemainingTex = MakeTex(2, 2, new Color(0.2f, 0.05f, 0.05f, 0.6f));

                // Aplicar el mismo escalado de matriz HUDScale anclado en la esquina superior derecha
                float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
                Matrix4x4 oldMat = GUI.matrix;
                if (hudScale != 1.0f)
                {
                    Vector2 pivot = new Vector2(Screen.width - 25f, 25f);
                    GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
                }

                float boxWidth = 320f;
                float boxHeight = 130f;
                float boxX = (float)Screen.width - boxWidth - 110f;
                float boxY = 25f;

                GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), alarmBgTex);
                GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, 3f), alarmBorderTex);
                GUI.DrawTexture(new Rect(boxX, boxY + boxHeight - 3f, boxWidth, 3f), alarmBorderTex);
                GUI.DrawTexture(new Rect(boxX, boxY, 3f, boxHeight), alarmBorderTex);
                GUI.DrawTexture(new Rect(boxX + boxWidth - 3f, boxY, 3f, boxHeight), alarmBorderTex);

                if (escapeState == EscapeState.Draining)
                {
                    string txtAlarm = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_alarm") : "[!] ALARMA DE SISTEMA";
                    string txtPump = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_pump_active") : "BOMBA HIDRÁULICA ACTIVA";
                    string txtEvac = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_evacuating") : "EVACUANDO AGUA";
                    string txtRemain = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetFormat("tunnels_time_remaining", Mathf.CeilToInt(currentDrainageTime)) : $"{Mathf.CeilToInt(currentDrainageTime)}s RESTANTES";
                    string txtInfest = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_infestation") : "[!] ACTIVIDAD PARANORMAL DETECTADA: INFESTACIÓN [!]";

                    // LÍNEA 1: TÍTULO Y BOMBA
                    GUI.Label(new Rect(boxX + 12f, boxY + 10f, 165f, 22f), ((Time.time % 0.8f < 0.4f) ? "[!]" : "   ") + " " + txtAlarm.Replace("[!]", "").Trim(), new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.red },
                        alignment = TextAnchor.MiddleLeft
                    });

                    GUI.Label(new Rect(boxX + boxWidth - 150f, boxY + 10f, 140f, 22f), txtPump, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.6f, 0.6f, 0.8f) },
                        alignment = TextAnchor.MiddleRight
                    });

                    // LÍNEA 2: BARRA DE PROGRESO
                    float barWidth = boxWidth - 30f;
                    float barHeight = 14f;
                    float barX = boxX + 15f;
                    float barY = boxY + 38f;
                    GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), progressRemainingTex);
                    float fillRatio = Mathf.Clamp01(currentDrainageTime / drainageDuration);
                    GUI.DrawTexture(new Rect(barX, barY, barWidth * (1f - fillRatio), barHeight), alarmProgressTex);
                    GUI.DrawTexture(new Rect(barX, barY, barWidth, 1f), alarmBorderTex);
                    GUI.DrawTexture(new Rect(barX, barY + barHeight - 1f, barWidth, 1f), alarmBorderTex);
                    GUI.DrawTexture(new Rect(barX, barY, 1f, barHeight), alarmBorderTex);
                    GUI.DrawTexture(new Rect(barX + barWidth - 1f, barY, 1f, barHeight), alarmBorderTex);

                    // LÍNEA 3: EVACUANDO AGUA Y TIEMPO RESTANTE
                    GUI.Label(new Rect(boxX + 15f, boxY + 58f, boxWidth - 30f, 22f), txtEvac + ((Time.time % 1.2f < 0.4f) ? "." : ((Time.time % 1.2f < 0.8f) ? ".." : "...")), new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleLeft
                    });

                    GUI.Label(new Rect(boxX + 15f, boxY + 58f, boxWidth - 30f, 22f), txtRemain, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.red },
                        alignment = TextAnchor.MiddleRight
                    });

                    // LÍNEA 4: ADVERTENCIA INFESTACIÓN
                    GUI.Label(new Rect(boxX + 15f, boxY + 92f, boxWidth - 30f, 25f), txtInfest, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.3f, 0.3f, 0.9f) },
                        alignment = TextAnchor.MiddleCenter
                    });
                }
                else if (escapeState == EscapeState.Ready)
                {
                    // ESTADO COMPLETADO: ALERTA DE EVACUACIÓN / BUSCAR SALIDA
                    bool blink = Time.time % 0.8f < 0.4f;

                    string txtDrained = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_drained") : "[!] SISTEMA DRENADO";
                    string txtHatch = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_hatch_open") : "ESCOTILLA ABIERTA";
                    string txtDrainedMsg = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_drained_msg") : "¡AGUA EVACUADA!\nBUSCA LA ESCOTILLA DE SALIDA";
                    string txtEvacNow = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("tunnels_evacuate_now") : "[!] ¡EVACÚA INMEDIATAMENTE! [!]";

                    // LÍNEA 1: SISTEMA DRENADO Y ESCOTILLA ABIERTA
                    GUI.Label(new Rect(boxX + 12f, boxY + 12f, 180f, 25f), (blink ? "[!]" : "   ") + " " + txtDrained.Replace("[!]", "").Trim(), new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.green },
                        alignment = TextAnchor.MiddleLeft
                    });

                    GUI.Label(new Rect(boxX + boxWidth - 145f, boxY + 12f, 135f, 25f), txtHatch, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.yellow },
                        alignment = TextAnchor.MiddleRight
                    });

                    // MENSAJE PARPADEANTE DE INSTRUCCIÓN DE SALIDA
                    GUI.Label(new Rect(boxX + 15f, boxY + 48f, boxWidth - 30f, 35f), txtDrainedMsg, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleCenter
                    });

                    GUI.Label(new Rect(boxX + 15f, boxY + 92f, boxWidth - 30f, 25f), (blink ? txtEvacNow : txtEvacNow.Replace("[!]", "   ")), new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.8f, 0.2f, 1f) },
                        alignment = TextAnchor.MiddleCenter
                    });
                }

                if (hudScale != 1.0f)
                {
                    GUI.matrix = oldMat;
                }
            }
        }

    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] array = new Color[width * height];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = col;
        }
        Texture2D texture2D = new Texture2D(width, height);
        texture2D.SetPixels(array);
        texture2D.Apply();
        return texture2D;
    }

    private void DrawPrompt(string text, float progress = -1f)
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);

        style.normal.textColor = new Color(0.3f, 0.85f, 1f);
        GUI.Label(rect, text, style);

        // Si hay progreso de interacción, dibujar una pequeña barra debajo del prompt
        if (progress >= 0f)
        {
            float barW = 300f;
            float barH = 10f;
            float barX = Screen.width / 2 - barW / 2;
            float barY = rect.y + rect.height + 8f;

            // Fondo de la barra
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            // Progreso relleno (Dorado/Amarillo)
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            GUI.DrawTexture(new Rect(barX, barY, barW * progress, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }

    private void DrawSubgeneratorsHUD()
    {
        if (activeSubGenerators == null || activeSubGenerators.Count == 0) return;

        GUIStyle genStyle = new GUIStyle(GUI.skin.label);
        genStyle.fontSize = 14;
        genStyle.fontStyle = FontStyle.Bold;
        genStyle.alignment = TextAnchor.MiddleRight;

        int width = 120;
        int height = (activeSubGenerators.Count * 24) + 6;
        float xPos = Screen.width - width - 25;
        float yPos = 120f; // Ubicado a la derecha, debajo del icono del bloc de notas

        Rect hudRect = new Rect(xPos, yPos, width, height);

        GUI.color = new Color(0.0f, 0.05f, 0.1f, 0.65f);
        GUI.DrawTexture(hudRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float offset = yPos + 3;
        foreach (var sub in activeSubGenerators)
        {
            if (sub == null) continue;

            genStyle.normal.textColor = sub.isOn ? new Color(0.2f, 0.95f, 0.3f) : new Color(1.0f, 0.25f, 0.25f);
            GUI.Label(new Rect(xPos + 5, offset, width - 15, 22), $"Gen {sub.subgeneratorLetter}  ●", genStyle);
            offset += 24;
        }
    }
}
