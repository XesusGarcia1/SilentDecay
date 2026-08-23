using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic : MonoBehaviour
{
    [Header("Referencias Generales")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar y cuando muera.")]
    public Transform pointStartRespawn;

    [Tooltip("El prefab de la nota de Lore (PapelLore) que se instanciará en el suelo.")]
    public GameObject loreNotePrefab;

    [Header("Opciones de Pruebas / Dev (Elevador y Tarjeta)")]
    [Tooltip("Iniciar la partida con la tarjeta de acceso del director en el inventario para pruebas de desarrollo.")]
    public bool startWithKeycard = false;
    [Tooltip("Burlar la tarjeta de acceso para probar el ascensor sin buscar la tarjeta.")]
    public bool bypassKeycard = false;
    [Tooltip("Burlar la necesidad de energía eléctrica para probar el ascensor sin activar fusibles.")]
    public bool bypassPower = false;

    private string correctKeypadCode = "";
    private Transform playerTransform;
    private List<GameObject> backupFusePool = new List<GameObject>();
    private bool isFuseRespawnTimerRunning = false;
    private List<GameObject> backupBatteryPool = new List<GameObject>();
    private bool isBatteryRespawnTimerRunning = false;

    private void Start()
    {
        // Asegurar que el menú de pausa (PauseMenuManager) esté presente
        if (FindFirstObjectByType<PauseMenuManager>() == null)
        {
            GameObject pMenu = new GameObject("[PauseMenuManager]");
            pMenu.AddComponent<PauseMenuManager>();
        }

        // Ejecutar spawn del jugador inmediatamente para evitar caídas
        SetupPlayerSpawn();
        
        // Retrasar el resto para asegurar que ModularHospitalGenerator (si existe) haya terminado de instanciar todas las habitaciones y notas
        StartCoroutine(DelayedSetupRoutine());
    }

    private IEnumerator DelayedSetupRoutine()
    {
        // Esperar unos frames para garantizar que el generador procedural y NavMesh terminen
        yield return new WaitForSeconds(0.2f);

        SetupDoors();
        ProcessGurneyItems();  // 1. Filtrar camillas (ESTRICTAMENTE 1 ítem por camilla para evitar papeles duplicados)
        SetupRandomElements(); // 2. Configurar clave del Director y activar EXACTAMENTE las 7 Notas del Código
        SetupHideBeds();
        SetupLoreNotes();      // 3. Activar EXACTAMENTE las 3 Notas de Lore en los papeles restantes
        SetupItems();
        SetupElevators();
        SetupBookHeadMonster();

        // Monitorear e iniciar respawn automático de fusibles y baterías si el jugador los necesita
        StartCoroutine(CheckAndRespawnFusesRoutine());
        StartCoroutine(CheckAndRespawnBatteriesRoutine());

        // Disparar monólogo inicial del jugador con pequeño delay
        StartCoroutine(TriggerStartMonologueDelayed());

        // BARRIDO NUCLEAR FINAL: Asegurar que CUALQUIER objeto 'Nota' que esté activo en la escena
        // pero que no haya sido seleccionado (no tiene NoteItem) sea DESACTIVADO FORZOSAMENTE.
        Transform[] finalSweep = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Transform t in finalSweep)
        {
            if (t == null) continue;
            string tName = t.name.Trim();

            if (tName.StartsWith("NotaCode"))
            {
                // Solo revisamos el objeto principal (padre) para ver si fue elegido
                if (t.parent != null && t.parent.name.StartsWith("NotaCode")) continue;

                bool hasValidNote = false;
                foreach (var comp in t.GetComponents<NoteItem>())
                {
                    if (comp != null) hasValidNote = true;
                }
                
                if (!hasValidNote)
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
    }

    private void SetupBookHeadMonster()
    {
        EnemyAIBookHead bookHeadA = FindFirstObjectByType<EnemyAIBookHead>(FindObjectsInactive.Include);
        EnemyAIController bookHeadB = FindFirstObjectByType<EnemyAIController>(FindObjectsInactive.Include);

        if (bookHeadA == null && bookHeadB == null)
        {
            GameObject bhObj = GameObject.Find("BookHead");
            if (bhObj != null)
            {
                bookHeadA = bhObj.GetComponent<EnemyAIBookHead>();
                bookHeadB = bhObj.GetComponent<EnemyAIController>();
            }
        }

        if (bookHeadA == null && bookHeadB == null) return;

        // Recolectar ÚNICAMENTE pasillos/corredores del mapa (EXCLUYENDO habitaciones y la Oficina del Director)
        List<Vector3> corridorPositions = new List<Vector3>();

        var modGen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.gridMatrix != null)
        {
            int sX = modGen.gridMatrix.GetLength(0);
            int sZ = modGen.gridMatrix.GetLength(1);
            float halfW = (sX * 4.0f) / 2.0f;
            float halfD = (sZ * 4.0f) / 2.0f;

            for (int x = 1; x < sX - 1; x++)
            {
                for (int z = 1; z < sZ - 1; z++)
                {
                    // gridMatrix[x, z] == 1 indica pasillo abierto del hospital
                    if (modGen.gridMatrix[x, z] == 1)
                    {
                        float wX = (x * 4.0f) - halfW + 2.0f;
                        float wZ = (z * 4.0f) - halfD + 2.0f;
                        Vector3 worldPos = modGen.transform.position + new Vector3(wX, 0f, wZ);

                        UnityEngine.AI.NavMeshHit hit;
                        if (UnityEngine.AI.NavMesh.SamplePosition(worldPos, out hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            if (!corridorPositions.Contains(hit.position))
                            {
                                corridorPositions.Add(hit.position);
                            }
                        }
                    }
                }
            }
        }

        // Si no se encontraron por gridMatrix, buscar objetos con 'corridor' o 'pasillo' en el mapa sin tocar rooms/director
        if (corridorPositions.Count == 0)
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string tName = t.name.ToLower();

                if (tName.Contains("director") || tName.Contains("room") || tName.Contains("apothecary") || tName.Contains("medical")) continue;

                if (tName.Contains("corridor") || tName.Contains("pasillo") || tName.Contains("hallway"))
                {
                    if (t.position.sqrMagnitude > 0.01f && !corridorPositions.Contains(t.position))
                    {
                        corridorPositions.Add(t.position);
                    }
                }
            }
        }

        // Crear o actualizar la carpeta exclusiva de patrulla en pasillos [BookHead_Patrol_Points]
        GameObject patrolHolder = GameObject.Find("[BookHead_Patrol_Points]");
        if (patrolHolder != null) Destroy(patrolHolder);
        patrolHolder = new GameObject("[BookHead_Patrol_Points]");

        List<Transform> validPts = new List<Transform>();
        for (int i = 0; i < corridorPositions.Count; i++)
        {
            GameObject pPoint = new GameObject($"[Corridor_Patrol_Point_{i + 1}]");
            pPoint.transform.position = corridorPositions[i];
            pPoint.transform.parent = patrolHolder.transform;
            validPts.Add(pPoint.transform);
        }

        Transform playerTarget = playerTransform;
        if (playerTarget == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) playerTarget = pObj.transform;
        }

        // Pre-cargar patrulla para EnemyAIBookHead y EnemyAIController
        if (bookHeadA != null)
        {
            bookHeadA.PreloadPatrol(validPts.ToArray(), playerTarget);
            bookHeadA.gameObject.SetActive(false);
        }
        if (bookHeadB != null)
        {
            bookHeadB.patrolPoints = validPts.ToArray();
            bookHeadB.player = playerTarget;
            bookHeadB.gameObject.SetActive(false);
        }

        Debug.Log($"[FixedHospital] BookHead configurado con {validPts.Count} puntos de patrulla en pasillos. Esperando apagón...");
    }

    private void SetupPlayerSpawn()
    {
        // 1. Buscar automáticamente el objeto 'StartGame' en la escena si no está asignado en el Inspector
        if (pointStartRespawn == null)
        {
            GameObject sgObj = GameObject.Find("StartGame");
            if (sgObj != null)
            {
                pointStartRespawn = sgObj.transform;
            }
        }

        if (pointStartRespawn == null)
        {
            Time.timeScale = 1.0f;
            return;
        }

        // 2. Encontrar el objeto de personaje activo (PlayerMale o PlayerFemale)
        GameObject playerObj = null;
        GameObject male = GameObject.Find("PlayerMale");
        GameObject female = GameObject.Find("PlayerFemale");

        if (male != null && male.activeInHierarchy) playerObj = male;
        else if (female != null && female.activeInHierarchy) playerObj = female;
        else if (male != null) playerObj = male;
        else if (female != null) playerObj = female;
        else playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            CharacterController cc = playerObj.GetComponentInChildren<CharacterController>(true);
            
            // Método oficial de Unity para teletransportar un CharacterController sin romper físicas ni controles
            if (cc != null) cc.enabled = false;

            playerObj.transform.position = pointStartRespawn.position;
            playerObj.transform.rotation = pointStartRespawn.rotation;

            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;

            // Asegurar que el componente HideUnderBed está presente y configurado en el jugador activo
            HideUnderBed hub = playerObj.GetComponent<HideUnderBed>();
            if (hub == null) hub = playerObj.AddComponent<HideUnderBed>();
            hub.player = playerObj;
            hub.playerCapsule = playerObj;
            hub.mainCamera = playerObj.GetComponentInChildren<Camera>(true);
            hub.enabled = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegistrarSpawnJugador(pointStartRespawn.position, pointStartRespawn.rotation);
            }
        }

        Time.timeScale = 1.0f;
    }

    private bool IsTopLevelElement(Transform t, string[] keywords, string[] excludeKeywords)
    {
        if (t == null) return false;

        // Jamás procesar objetos pertenecientes al jugador ni a su jerarquía
        Transform root = t.root;
        if (root != null)
        {
            string rootName = root.name.ToLower();
            if (rootName.Contains("player") || rootName.Contains("nestedparent") || root.CompareTag("Player"))
            {
                return false;
            }
        }

        string n = t.name.ToLower();
        if (n.Contains("player") || n.Contains("capsule") || n.Contains("camera")) return false;

        // Si está en el origen exacto (0,0,0), ignorar por ser plantilla deshabilitada fuera del mapa
        if (t.position.sqrMagnitude < 0.001f) return false;

        bool matches = false;
        foreach (var kw in keywords)
        {
            if (n.Contains(kw)) { matches = true; break; }
        }
        if (!matches) return false;

        foreach (var ex in excludeKeywords)
        {
            if (n.Contains(ex)) return false;
        }

        // Si un ancestro directo ya coincide con la misma palabra clave (ej: sub-mallas dentro del objeto principal), ignorar el hijo
        Transform p = t.parent;
        while (p != null)
        {
            string pName = p.name.ToLower();
            if (pName.Contains("props") || pName.Contains("rooms") || pName.Contains("hospitalgame") || pName.Contains("modular") || pName.Contains("prefabs")) 
            {
                break;
            }

            foreach (var kw in keywords)
            {
                if (pName.Contains(kw))
                {
                    return false;
                }
            }
            p = p.parent;
        }

        return true;
    }

    private void ConfigureBoxColliderFromRenderers(GameObject obj, BoxCollider box, bool isTrigger, float sizePaddingMultiplier = 1.0f)
    {
        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>(true);
        if (mfs.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
            return;
        }

        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            
            Bounds meshBounds = mf.sharedMesh.bounds;
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.min.z);
            corners[1] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.max.z);
            corners[2] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.min.z);
            corners[3] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.max.z);
            corners[4] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.min.z);
            corners[5] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.max.z);
            corners[6] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.min.z);
            corners[7] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.max.z);

            for (int i = 0; i < 8; i++)
            {
                Vector3 worldCorner = mf.transform.TransformPoint(corners[i]);
                Vector3 localCorner = obj.transform.InverseTransformPoint(worldCorner);
                
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (hasBounds)
        {
            box.isTrigger = isTrigger;
            box.center = localBounds.center;
            box.size = localBounds.size * sizePaddingMultiplier;
        }
        else
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
        }
    }

    private void ActivateItemWithAllChildren(GameObject itemObj)
    {
        if (itemObj == null) return;

        // Activar el objeto y todos sus padres hacia arriba en la jerarquía
        Transform curr = itemObj.transform;
        while (curr != null)
        {
            curr.gameObject.SetActive(true);
            curr = curr.parent;
        }

        Transform[] allChildren = itemObj.GetComponentsInChildren<Transform>(true);
        foreach (Transform c in allChildren)
        {
            if (c != null)
            {
                c.gameObject.SetActive(true);
            }
        }

        Renderer[] renderers = itemObj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }
    }

    private IEnumerator TriggerStartMonologueDelayed()
    {
        yield return new WaitForSeconds(1.2f);
        PlayerMonologueManager.ShowDialogue("¿Dónde estoy?... El hospital parece totalmente abandonado. Debo encontrar la tarjeta de acceso del director para usar el ascensor de evacuación...", 6.0f);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
