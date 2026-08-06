using System.Collections.Generic;
using UnityEngine;

namespace ModularHospital
{
    public class ModularHospitalGenerator : MonoBehaviour
    {
        [Header("Referencias del Sistema")]
        public ModuleDatabase database;
        public ModuleValidator validator;

        [Header("Configuración de Generación")]
        public int targetModuleCount = 45;
        public int targetCorridorCount = 25;
        public int targetRoomCount = 5;
        public int maxAttemptsPerConnector = 10;
        public bool generateOnStart = true;
        public bool isMenuMode = false;

        [Header("Ascensor de Escape")]
        public GameObject elevatorPrefab;

        [Header("Caja de Fusibles y Fusibles")]
        public GameObject fuseBoxPrefab;
        public GameObject fusePrefab;
        public bool spawnFuseBoxAndFuse = true;

        [Header("Generadores Eléctricos (Subgeneradores A y B)")]
        public GameObject generatorPrefab;
        public bool spawnGenerator = true;

        [Header("Ítems y Notas del Puzzle (Keypad, Baterías y Notas)")]
        public GameObject batteryPrefab;
        public GameObject notePrefab;
        public string correctKeypadCode = "";

        [Header("Opciones de Pruebas / Dev (Elevador y Tarjeta)")]
        [Tooltip("Activar para iniciar la partida directamente con la tarjeta de acceso del director en la mano")]
        public bool startWithKeycard = false;
        [Tooltip("Activar para ignorar el requisito de la tarjeta de acceso al llamar al elevador")]
        public bool bypassKeycard = false;

        [Header("Configuración de Lámparas y Luces")]
        public GameObject lampPrefab;
        public bool spawnCeilingLamps = true;
        [Range(0.5f, 5.0f)]
        public float lampLightIntensity = 2.6f;
        public Color lampLightColor = new Color(0.95f, 0.95f, 0.85f);

        [Header("Padre del Mapa Generado")]
        public Transform mapParentContainer;

        [Header("Materiales Unificados para Piso, Techo y Paredes")]
        public Material customFloorMaterial;
        public Material customCeilingMaterial;
        public Material customWallMaterial;

        [Header("Configuración del Edificio del Hospital")]
        public Vector2Int smallMapGridSize = new Vector2Int(10, 10);
        private Bounds currentBuildingBounds;

        public List<HospitalModule> placedModules = new List<HospitalModule>();
        private List<ModuleConnector> openConnectors = new List<ModuleConnector>();
        public int[,] gridMatrix; // 0=Empty/Wall, 1=Corridor, 2=DirectorOffice, 3=SmallRoom
        private Vector3 lastElevatorPos = new Vector3(-999f, -999f, -999f);
        private Vector3 lastFuseBoxPos = new Vector3(-999f, -999f, -999f);
        private Vector3 lastSubGenAPos = new Vector3(-999f, -999f, -999f);
        private Vector3 lastSubGenBPos = new Vector3(-999f, -999f, -999f);
        private List<Vector3> spawnedItemPositions = new List<Vector3>();

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void Start()
        {
            AutoAssignReferences();

            if (isMenuMode)
            {
                if (generateOnStart) GenerateHospitalMap();
                return;
            }

            // Asegurar que PauseMenuManager esté siempre presente en la escena para pausar con ESC
            if (FindObjectOfType<PauseMenuManager>() == null)
            {
                GameObject pMenuObj = new GameObject("[PauseMenuManager]");
                pMenuObj.AddComponent<PauseMenuManager>();
            }

            // Asegurar que ElevatorController esté siempre presente en la escena para la Libreta de Notas [TAB]
            if (FindObjectOfType<ElevatorController>() == null)
            {
                GameObject eCtrlObj = new GameObject("[ElevatorController_Manager]");
                eCtrlObj.AddComponent<ElevatorController>();
            }

            if (generateOnStart)
            {
                GenerateHospitalMap();
            }
        }

        private void Update()
        {
            // Forzar en cada frame cero luz ambiental de fondo para que solo iluminen las lámparas del hospital
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientSkyColor = Color.black;
            RenderSettings.ambientEquatorColor = Color.black;
            RenderSettings.ambientGroundColor = Color.black;
            RenderSettings.ambientIntensity = 0.0f;
            RenderSettings.reflectionIntensity = 0.0f;

            // Destrucción continua de cualquier luz solar (Directional Light) en la escena
            Light[] lights = FindObjectsOfType<Light>(true);
            foreach (Light l in lights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    l.enabled = false;
                    l.intensity = 0.0f;
                    if (Application.isPlaying) Destroy(l.gameObject);
                    else DestroyImmediate(l.gameObject);
                }
            }
        }

        public void AutoAssignReferences()
        {
            if (database == null) database = GetComponent<ModuleDatabase>();
            if (database == null) database = gameObject.AddComponent<ModuleDatabase>();

            if (validator == null) validator = GetComponent<ModuleValidator>();
            if (validator == null) validator = gameObject.AddComponent<ModuleValidator>();

            if (database != null) database.AutoAssignPrefabsIfEmpty();
        }

        [ContextMenu("Generar Mapa Modular")]
        public void GenerateHospitalMap()
        {
            ClearExistingMap();
            AutoAssignReferences();
            spawnedItemPositions.Clear();
            PlayerPrefs.DeleteKey("CamcorderAccumulatedTime"); // Reiniciar el reloj de la cámara a 00:00:00 en cada nueva partida del hospital

            if (database == null)
            {
                Debug.LogError("ModularHospitalGenerator: ModuleDatabase no está asignado.");
                return;
            }

            // Leer tamaño de mapa de PlayerPrefs (Chico = 11x11, Mediano = 16x16, Grande = 22x22)
            int mapDim = PlayerPrefs.GetInt("SelectedMapSize", 11);
            if (mapDim < 8) mapDim = 11;
            else mapDim = Mathf.Clamp(mapDim, 11, 25);

            smallMapGridSize = new Vector2Int(mapDim, mapDim);

            // Leer dificultad seleccionada (Default "NORMAL" al ejecutar directamente desde la escena de prueba)
            string diffStr = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
            int baseRooms = 4;
            if (diffStr == "FACIL") baseRooms = 3;
            else if (diffStr == "DIFICIL") baseRooms = 5;

            // Escalar habitaciones según el tamaño del mapa
            if (mapDim >= 22) targetRoomCount = baseRooms + 3; // 6 - 8 habitaciones
            else if (mapDim >= 16) targetRoomCount = baseRooms + 2; // 5 - 7 habitaciones
            else targetRoomCount = baseRooms; // 3 - 5 habitaciones

            int sizeX = Mathf.Clamp(smallMapGridSize.x, 8, 30);
            int sizeZ = Mathf.Clamp(smallMapGridSize.y, 8, 30);

            float width = sizeX * 4.0f;
            float depth = sizeZ * 4.0f;
            currentBuildingBounds = new Bounds(transform.position, new Vector3(width, 4.0f, depth));

            gridMatrix = new int[sizeX, sizeZ];
            float halfW = width / 2.0f;
            float halfD = depth / 2.0f;

            Transform parent = mapParentContainer != null ? mapParentContainer : transform;

            // 1. GENERACIÓN DE LABERINTO ORGANICO TIPO BACKROOMS DE HOSPITAL (PASILLOS ESTRECHOS Y PAREDES DENSAS)
            // Empezar con la matriz totalmente llena de paredes (0) y carvar canales de pasillo (1)
            int startX = sizeX / 2;
            int startZ = sizeZ / 2;
            gridMatrix[startX, startZ] = 1;

            List<Vector2Int> frontier = new List<Vector2Int>();
            AddFrontierNeighbors(startX, startZ, sizeX, sizeZ, frontier);

            while (frontier.Count > 0)
            {
                int rIdx = Random.Range(0, frontier.Count);
                Vector2Int current = frontier[rIdx];
                frontier.RemoveAt(rIdx);

                if (gridMatrix[current.x, current.y] != 0) continue;

                // Contar pasillos adyacentes
                List<Vector2Int> corridorNeighbors = GetCorridorNeighbors(current.x, current.y, sizeX, sizeZ);
                if (corridorNeighbors.Count == 1 || (corridorNeighbors.Count == 2 && Random.value < 0.25f))
                {
                    gridMatrix[current.x, current.y] = 1;
                    AddFrontierNeighbors(current.x, current.y, sizeX, sizeZ, frontier);
                }
            }

            // Permitir tallado orgánico y cerrado de bordes
            for (int x = 0; x < sizeX; x++)
            {
                gridMatrix[x, 0] = 0;
                gridMatrix[x, sizeZ - 1] = 0;
            }
            for (int z = 0; z < sizeZ; z++)
            {
                gridMatrix[0, z] = 0;
                gridMatrix[sizeX - 1, z] = 0;
            }

            // 2. ASIGNAR HABITACIONES EN CELDAS LIBRES CONECTADAS
            List<Vector2Int> availableCells = new List<Vector2Int>();
            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    if (gridMatrix[x, z] == 0 && HasAdjacentCorridor(x, z, sizeX, sizeZ)) availableCells.Add(new Vector2Int(x, z));
                }
            }

            for (int i = 0; i < availableCells.Count; i++)
            {
                int r = Random.Range(i, availableCells.Count);
                Vector2Int temp = availableCells[i];
                availableCells[i] = availableCells[r];
                availableCells[r] = temp;
            }

            List<Vector2Int> roomCells = new List<Vector2Int>();

            // ── PRE-RESERVAR CELDA DEL ASCENSOR ANTES DE COLOCAR HABITACIONES ──────────
            Vector2Int preElevCell = new Vector2Int(1, 1);
            float preElevMaxScore = -1f;
            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    if (gridMatrix[x, z] != 1) continue;
                    int corr = 0;
                    if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) corr++;
                    if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) corr++;
                    if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) corr++;
                    if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) corr++;
                    if (corr != 1) continue;
                    float dist = Vector2.Distance(new Vector2(1, 1), new Vector2(x, z));
                    if (dist > preElevMaxScore) { preElevMaxScore = dist; preElevCell = new Vector2Int(x, z); }
                }
            }
            gridMatrix[preElevCell.x, preElevCell.y] = 4;
            for (int rx = Mathf.Max(0, preElevCell.x - 3); rx <= Mathf.Min(sizeX - 1, preElevCell.x + 3); rx++)
                for (int rz = Mathf.Max(0, preElevCell.y - 3); rz <= Mathf.Min(sizeZ - 1, preElevCell.y + 3); rz++)
                    if (gridMatrix[rx, rz] == 0) gridMatrix[rx, rz] = 4;
            // ─────────────────────────────────────────────────────────────────────────────

            // Asignar Oficina del Director
            foreach (Vector2Int cell in availableCells)
            {
                if (gridMatrix[cell.x, cell.y] == 0 && HasAdjacentCorridor(cell.x, cell.y, sizeX, sizeZ))
                {
                    gridMatrix[cell.x, cell.y] = 2; // DirectorOffice
                    roomCells.Add(cell);
                    EnsureDoorwayCorridor(cell.x, cell.y, sizeX, sizeZ);
                    break;
                }
            }

            // Asignar Habitaciones Normales (Pase 1: Separación estricta de 3 celdas)
            int roomsPlaced = 0;
            foreach (Vector2Int cell in availableCells)
            {
                if (roomsPlaced >= targetRoomCount) break;
                if (gridMatrix[cell.x, cell.y] != 0) continue;

                int minSeparation = 3;
                bool isTooClose = false;
                foreach (Vector2Int rCell in roomCells)
                {
                    int dx = Mathf.Abs(rCell.x - cell.x);
                    int dy = Mathf.Abs(rCell.y - cell.y);
                    if (dx < minSeparation && dy < minSeparation)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                if (!isTooClose && HasAdjacentCorridor(cell.x, cell.y, sizeX, sizeZ))
                {
                    gridMatrix[cell.x, cell.y] = 3;
                    roomCells.Add(cell);
                    roomsPlaced++;

                    EnsureDoorwayCorridor(cell.x, cell.y, sizeX, sizeZ);
                }
            }

            // Pase 2 (Fallback para mapas pequeños): Usar separación de 2 celdas con regla estricta anti-pegadas (dx+dy >= 3)
            if (roomsPlaced < targetRoomCount)
            {
                foreach (Vector2Int cell in availableCells)
                {
                    if (roomsPlaced >= targetRoomCount) break;
                    if (gridMatrix[cell.x, cell.y] != 0) continue;

                    bool isTooClose = false;
                    foreach (Vector2Int rCell in roomCells)
                    {
                        int dx = Mathf.Abs(rCell.x - cell.x);
                        int dy = Mathf.Abs(rCell.y - cell.y);
                        // Jamás permitir habitaciones pegadas directa o diagonalmente (dx+dy < 3 o dx<2 && dy<2)
                        if (dx + dy < 3 || (dx < 2 && dy < 2))
                        {
                            isTooClose = true;
                            break;
                        }
                    }

                    if (!isTooClose && HasAdjacentCorridor(cell.x, cell.y, sizeX, sizeZ))
                    {
                        gridMatrix[cell.x, cell.y] = 3;
                        roomCells.Add(cell);
                        roomsPlaced++;

                        EnsureDoorwayCorridor(cell.x, cell.y, sizeX, sizeZ);
                    }
                }
            }

            // Restaurar celdas marcadas con 4 (zona ascensor) a su estado correcto para la instanciación
            for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < sizeZ; z++)
                    if (gridMatrix[x, z] == 4) gridMatrix[x, z] = (x == preElevCell.x && z == preElevCell.y) ? 1 : 0;

            // 3. INSTANCIAR MÓDULOS DE ACUERDO A LA MATRIZ
            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    int type = gridMatrix[x, z];
                    if (type == 0) continue;

                    float worldX = (x * 4.0f) - halfW + 2.0f;
                    float worldZ = (z * 4.0f) - halfD + 2.0f;
                    Vector3 cellPos = new Vector3(worldX, transform.position.y, worldZ);

                    HospitalModule prefabToInstantiate = null;
                    Quaternion rot = Quaternion.identity;

                    if (type == 2)
                    {
                        prefabToInstantiate = database.directorOfficePrefab;
                        rot = GetBestRoomRotation(x, z, sizeX, sizeZ);
                    }
                    else if (type == 3)
                    {
                        prefabToInstantiate = GetRandomRoomPrefab();
                        rot = GetBestRoomRotation(x, z, sizeX, sizeZ);
                    }
                    else if (type == 1)
                    {
                        prefabToInstantiate = SelectCorridorPrefab(x, z, sizeX, sizeZ, out rot);
                    }

                    if (prefabToInstantiate != null)
                    {
                        HospitalModule instance = Instantiate(prefabToInstantiate, cellPos, rot, parent);
                        instance.FindConnectorsInChildren();
                        placedModules.Add(instance);
                    }
                }
            }

            // 3.5 Ocultar mallas individuales de piso y techo en prefabs modulares (Usamos Unified Floor and Ceiling Slabs)
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                Transform[] subChildren = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in subChildren)
                {
                    if (child == null) continue;
                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("floor") || nameLower.Contains("piso") || nameLower.Contains("ceiling") || nameLower.Contains("techo") || nameLower.Contains("slab"))
                    {
                        Renderer r = child.GetComponent<Renderer>();
                        if (r != null) r.enabled = false;
                    }
                }
            }

            // 3.6 AUTO-CONFIGURAR PIVOTE DE BISAGRA Y PUERTAS EN HABITACIONES
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                
                // Buscar únicamente el objeto de hoja de puerta (evitando marcos duplicados)
                Transform doorObj = null;
                Transform[] allTransforms = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t == null) continue;
                    string n = t.name.ToLower();
                    if ((n.Contains("p_door_01_") || n.Contains("puerta") || n.Contains("door")) && 
                        !n.Contains("base") && !n.Contains("frame") && !n.Contains("marco") && !n.Contains("hinge"))
                    {
                        doorObj = t;
                        break;
                    }
                }

                if (doorObj == null) continue;

                // Desactivar Animator en la puerta y sus hijos para evitar bloqueos
                Animator[] anims = mod.GetComponentsInChildren<Animator>(true);
                foreach (Animator a in anims)
                {
                    if (a != null) a.enabled = false;
                }

                // Evitar duplicar Hinge si ya existe
                if (doorObj.parent != null && doorObj.parent.name.Contains("Hinge")) continue;

                // Crear objeto bisagra (Hinge) exactamente en el borde izquierdo de la puerta (-0.62m)
                GameObject hingeObj = new GameObject("ModuleRoomDoor_Hinge");
                hingeObj.transform.SetParent(mod.transform, false);
                hingeObj.transform.position = doorObj.position - doorObj.right * 0.62f;
                hingeObj.transform.rotation = doorObj.rotation;

                // Emparentar la puerta a la bisagra manteniendo su posición global intacta
                doorObj.SetParent(hingeObj.transform, true);

                // Asignar el script interactivo a la bisagra con distancia de alcance cercano (1.8m)
                ProceduralDoorInteract doorInteract = hingeObj.AddComponent<ProceduralDoorInteract>();
                doorInteract.interactDistance = 1.8f;

                if (mod.moduleType == ModuleType.DirectorOffice)
                {
                    doorInteract.isLocked = true;
                }
            }

            // Auto-configurar items pre-colocados en la Oficina del Director (Baterías, Fusible, Tarjeta)
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null || mod.moduleType != ModuleType.DirectorOffice) continue;

                Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allT)
                {
                    if (t == null) continue;
                    string n = t.name.ToLower();

                    if ((n.Contains("battery") || n.Contains("batery") || n.Contains("pila")) && !n.Contains("canvas") && !n.Contains("ui"))
                    {
                        t.gameObject.SetActive(true);
                        BatteryItem bComp = t.gameObject.GetComponent<BatteryItem>();
                        if (bComp == null) bComp = t.gameObject.AddComponent<BatteryItem>();
                        bComp.interactDistance = 3.2f;
                        bComp.rechargeAmount = 60f;
                    }
                    else if ((n.Contains("fuse") || n.Contains("fusible")) && !n.Contains("box") && !n.Contains("caja"))
                    {
                        t.gameObject.SetActive(true);
                        FuseItem fComp = t.gameObject.GetComponent<FuseItem>();
                        if (fComp == null) fComp = t.gameObject.AddComponent<FuseItem>();
                        fComp.interactDistance = 3.2f;
                    }
                }
            }

            // 0. REINICIAR LA LIBRETA DE NOTAS, EL MAPA Y LAS 3 VIDAS AL GENERAR UN NUEVO HOSPITAL
            // IMPORTANTE: Solo inicializar vidas si es una partida NUEVA (vidasActuales == maxVidas o primera vez).
            // Si el jugador ya tiene menos vidas (respawn mid-game), NO resetear — respetar el conteo actual.
            NotepadUIManager.ResetNotepadData();
            if (GameManager.Instance != null)
            {
                // Detectar si esta es la primera vez que se genera (venimos del menú con vidas llenas)
                // vs. una regeneración por respawn (el jugador ya tiene menos de maxVidas).
                bool esPrimerGeneracion = (GameManager.Instance.vidasActuales >= GameManager.Instance.maxVidas);
                if (esPrimerGeneracion)
                {
                    GameManager.Instance.InicializarVidasParaMapa(3);
                    Debug.Log("ModularHospitalGenerator: Primera generación — vidas inicializadas a 3.");
                }
                else
                {
                    Debug.Log($"ModularHospitalGenerator: Regeneración post-muerte — manteniendo {GameManager.Instance.vidasActuales} vidas actuales.");
                }
            }
            // 4. MANTENER PAREDES INTEGRAS DE PISO A TECHO (Sin borrado de paneles ni huecos flotantes)
            // ClearBlockingWallAtConnector se desactiva para evitar cortar mallas o crear vacíos verticales

            // 4.5 SELLAR ESQUINAS DE INTERSECCIÓN CON PILARES Y SOPORTES ESTRUCTURALES
            SealGridCornerGaps(parent, sizeX, sizeZ, halfW, halfD);

            // 5. TECHO, PISO Y PAREDES PERIMETRALES UNIFICADAS
            BuildUnifiedFloorAndCeilingSlabs(parent);

            // 6. LÁMPARAS ESPACIADAS
            BuildCeilingLamps(parent);

            // 7. INSTANCIAR EL ASCENSOR DE ESCAPE AL FINAL DEL PASILLO
            BuildElevator(parent, sizeX, sizeZ, halfW, halfD);

            // En modo menú solo queremos la geometría del mapa y las luces para el fondo tétrico
            if (isMenuMode)
            {
                Debug.Log("ModularHospitalGenerator: Mapa modular de fondo generado para el menú principal.");
                return;
            }

            // 8. INSTANCIAR LOS SUBGENERADORES A Y B
            BuildGenerator(parent, sizeX, sizeZ, halfW, halfD);

            // 9. INSTANCIAR LA CAJA DE FUSIBLES Y EL FUSIBLE RECOLECTABLE (asegurando separación de SubGeneradores)
            BuildFuseBoxAndFuse(parent, sizeX, sizeZ, halfW, halfD);

            // 10. GENERAR CLAVE DE 7 DÍGITOS, ASIGNAR AL KEYPAD Y REPARTIR NOTAS COLECCIONABLES
            BuildKeypadAndNotes(parent, sizeX, sizeZ, halfW, halfD);

            // 11. REPARTIR BATERÍAS DE LINTERNA RECOLECTABLES
            BuildBatteries(parent, sizeX, sizeZ, halfW, halfD);

            // 10.5 ELIMINAR CUALQUIER PARED O MURO QUE SE HAYA COLADO DENTRO DEL INTERIOR DE LAS HABITACIONES
            CleanInternalRoomBlockingWalls(parent);

            // 10.6 DESOBSTRUIR 100% LA ENTRADA Y EL KEYPAD DE LA OFICINA DEL DIRECTOR
            UnblockDirectorOfficeEntrance();

            // 11.2 CONFIGURAR CAMAS PARA PERMITIR AL JUGADOR ESCONDERSE DEBAJO DE ELLAS
            SetupHideBeds();

            // 11.3 SANITIZAR MESHCOLLIDERS CÓNCAVOS PARA EVITAR ERRORES DE TRIGGER EN PHYSX
            SanitizeMeshColliders(parent);

            // 11.4 HORNEAR EL NAVMESH EN TIEMPO DE EJECUCIÓN SOBRE TODO EL HOSPITAL PROCEDURAL
            Unity.AI.Navigation.NavMeshSurface navSurface = parent.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            if (navSurface == null) navSurface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            navSurface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
            navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            navSurface.defaultArea = 0; // Walkable
            navSurface.BuildNavMesh();
            Debug.Log("ModularHospitalGenerator: NavMeshSurface horneado con éxito en tiempo de ejecución para el monstruo BookHead.");

            // 11.5 INSTANCIAR Y CONFIGURAR PUNTOS DE PATRULLAJE DEL ENEMIGO BOOKHEAD
            BuildEnemyBookHead(parent, sizeX, sizeZ, halfW, halfD);

            // 12. POSICIONAR AL JUGADOR EN EL SPAWN DE ENTRADA (1, 1)
            PositionPlayerAtSpawn(sizeX, sizeZ, halfW, halfD);

            // 13. AMBIENTE DE TERROR OSCURO REALISTA: ELIMINAR LA LUZ DEL SOL Y REFLEJOS DE DÍA
            Light[] allSceneLights = FindObjectsOfType<Light>(true);
            foreach (Light l in allSceneLights)
            {
                if (l == null) continue;
                if (l.type == LightType.Directional)
                {
                    l.enabled = false;
                    l.intensity = 0.0f;
                    if (Application.isPlaying) Destroy(l.gameObject);
                    else DestroyImmediate(l.gameObject);
                }
            }

            // Desactivar Reflection Probes para evitar reflejos de sol/cielo en paredes y suelo
            UnityEngine.ReflectionProbe[] probes = FindObjectsOfType<UnityEngine.ReflectionProbe>(true);
            foreach (UnityEngine.ReflectionProbe rp in probes)
            {
                if (rp != null) rp.enabled = false;
            }

            // Anular toda la iluminación ambiental del Skybox y reflejos de Unity
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.001f, 0.003f, 0.002f);
            RenderSettings.ambientSkyColor = Color.black;
            RenderSettings.ambientEquatorColor = Color.black;
            RenderSettings.ambientGroundColor = Color.black;
            RenderSettings.ambientIntensity = 0.0f;
            RenderSettings.reflectionIntensity = 0.0f;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;

            // Configurar la cámara para que el fondo lejano coincida con la niebla verde/oscura
            Color greenFog = new Color(0.005f, 0.025f, 0.018f);
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = greenFog;
            }

            // NIEBLA TÉTRICA DE HOSPITAL ABANDONADO (FOTO 2)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = greenFog;
            RenderSettings.fogStartDistance = 1.5f;
            RenderSettings.fogEndDistance = 14.0f;

            // MÚSICA AMBIENTAL DEL MAPA DE HOSPITAL (Music Hospital Level)
            AudioClip hospitalMusic = Resources.Load<AudioClip>("Audio/Hospital/Music Hospital Level");
            if (hospitalMusic == null) hospitalMusic = Resources.Load<AudioClip>("Music Hospital Level");
            if (hospitalMusic != null)
            {
                AudioSource bgAudio = GetComponent<AudioSource>();
                if (bgAudio == null) bgAudio = gameObject.AddComponent<AudioSource>();
                bgAudio.clip = hospitalMusic;
                bgAudio.loop = true;
                bgAudio.volume = 0.25f; // Volumen moderado, ambiental y aterrador (25%)
                bgAudio.spatialBlend = 0f; // Sonido ambiente 2D global
                bgAudio.playOnAwake = true;
                if (!bgAudio.isPlaying) bgAudio.Play();
                Debug.Log("ModularHospitalGenerator: Música ambiental 'Music Hospital Level' iniciada con volumen calibrado (25%).");
            }

            Debug.Log($"ModularHospitalGenerator: Mapa de 10x10 generado con éxito. Módulos colocados: {placedModules.Count}. Clave del Keypad: {correctKeypadCode}");

            // 13.5 SINCRONIZAR GRIDMATRIX DE FORMA 100% FIEDELIGNA CON LOS MÓDULOS 3D REALMENTE INSTANCIADOS
            SyncGridMatrixWithPlacedModules(sizeX, sizeZ, halfW, halfD);

            // 14. EJECUTAR EL TEST DEFINITIVO DE VALIDACIÓN DE MAPA (Quality Assurance)
            HospitalMapValidator.ValidateCurrentMap(this);
        }

        private void SyncGridMatrixWithPlacedModules(int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (gridMatrix == null) gridMatrix = new int[sizeX, sizeZ];

            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                Vector3 localPos = mod.transform.position - transform.position;
                int gx = Mathf.Clamp(Mathf.RoundToInt((localPos.x + halfW - 2.0f) / 4.0f), 0, sizeX - 1);
                int gz = Mathf.Clamp(Mathf.RoundToInt((localPos.z + halfD - 2.0f) / 4.0f), 0, sizeZ - 1);

                if (mod.moduleType == ModuleType.DirectorOffice)
                {
                    gridMatrix[gx, gz] = 2; // Oficina del Director
                }
                else if (mod.moduleType == ModuleType.SmallRoom || mod.moduleType == ModuleType.LargeRoom || mod.moduleType == ModuleType.OfficeRoom)
                {
                    gridMatrix[gx, gz] = 3; // Habitaciones
                }
                else
                {
                    if (gridMatrix[gx, gz] == 0) gridMatrix[gx, gz] = 1; // Pasillos (preservando pasillos del laberinto)
                }
            }
        }

        private void CleanAllBlockingRootColliders()
        {
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;

                // Apagar recuadros de visualización gizmo en la vista de escena
                mod.showBoundsGizmo = false;

                // Eliminar colisionadores de volumen en la raíz del módulo
                Collider rootCol = mod.GetComponent<Collider>();
                if (rootCol != null)
                {
                    DestroyImmediate(rootCol);
                }

                // Desactivar gizmos en conectores y eliminar colisionadores invisibles de módulo, marcos de puerta o bounds
                Collider[] childCols = mod.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in childCols)
                {
                    if (c == null) continue;

                    ModuleConnector mc = c.GetComponent<ModuleConnector>();
                    if (mc != null)
                    {
                        mc.showGizmos = false;
                        if (!c.isTrigger) DestroyImmediate(c);
                        continue;
                    }

                    string n = c.gameObject.name.ToLower();

                    // Eliminar colisionadores de marcos de puerta (marco wall) que tapan el umbral de las puertas
                    if (n.Contains("marco") || n.Contains("frame"))
                    {
                        c.enabled = false;
                        DestroyImmediate(c);
                        continue;
                    }

                    if (n.Contains("bound") || n.Contains("volume") || n.Contains("module") || n.Contains("root") || n.Contains("block"))
                    {
                        if (!n.Contains("wall") && !n.Contains("floor") && !n.Contains("suelo") && !n.Contains("piso") && !n.Contains("door"))
                        {
                            c.enabled = false;
                            DestroyImmediate(c);
                        }
                    }
                }
            }
        }

        private HospitalModule GetRandomRoomPrefab()
        {
            if (database.smallRoomPrefabs != null && database.smallRoomPrefabs.Count > 0)
            {
                return database.smallRoomPrefabs[Random.Range(0, database.smallRoomPrefabs.Count)];
            }
            return database.GetRandomStandardModule();
        }

        private bool HasAdjacentCorridor(int x, int z, int sizeX, int sizeZ)
        {
            return (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) ||
                   (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) ||
                   (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) ||
                   (z - 1 >= 0 && gridMatrix[x, z - 1] == 1);
        }

        private void AddFrontierNeighbors(int x, int z, int sizeX, int sizeZ, List<Vector2Int> frontier)
        {
            Vector2Int[] dirs = new Vector2Int[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            foreach (Vector2Int d in dirs)
            {
                int nx = x + d.x;
                int nz = z + d.y;
                if (nx >= 2 && nx < sizeX - 2 && nz >= 2 && nz < sizeZ - 2 && gridMatrix[nx, nz] == 0)
                {
                    frontier.Add(new Vector2Int(nx, nz));
                }
            }
        }

        private List<Vector2Int> GetCorridorNeighbors(int x, int z, int sizeX, int sizeZ)
        {
            List<Vector2Int> list = new List<Vector2Int>();
            if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) list.Add(new Vector2Int(x + 1, z));
            if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) list.Add(new Vector2Int(x - 1, z));
            if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) list.Add(new Vector2Int(x, z + 1));
            if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) list.Add(new Vector2Int(x, z - 1));
            return list;
        }

        private void EnsureDoorwayCorridor(int x, int z, int sizeX, int sizeZ)
        {
            if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) return;
            if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) return;
            if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) return;
            if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) return;

            if (z - 1 >= 0 && gridMatrix[x, z - 1] == 0) gridMatrix[x, z - 1] = 1;
            else if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 0) gridMatrix[x, z + 1] = 1;
            else if (x + 1 < sizeX && gridMatrix[x + 1, z] == 0) gridMatrix[x + 1, z] = 1;
            else if (x - 1 >= 0 && gridMatrix[x - 1, z] == 0) gridMatrix[x - 1, z] = 1;
        }

        private Quaternion GetBestRoomRotation(int x, int z, int sizeX, int sizeZ)
        {
            // El conector de la puerta en las habitaciones está en la cara SUR (local Z -1).
            // Evaluar cuál vecino de pasillo (gridMatrix == 1) está más libre para apuntar la puerta hacia él
            if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) return Quaternion.Euler(0, 180, 0); // Pasillo al NORTE (+Z)
            if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) return Quaternion.Euler(0, 0, 0);      // Pasillo al SUR (-Z)
            if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) return Quaternion.Euler(0, 270, 0); // Pasillo al ESTE (+X)
            if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) return Quaternion.Euler(0, 90, 0);    // Pasillo al OESTE (-X)
            return Quaternion.identity;
        }

        private HospitalModule SelectCorridorPrefab(int x, int z, int sizeX, int sizeZ, out Quaternion rot)
        {
            rot = Quaternion.identity;

            bool n_isRoom = (z + 1 < sizeZ && (gridMatrix[x, z + 1] == 2 || gridMatrix[x, z + 1] == 3));
            bool s_isRoom = (z - 1 >= 0 && (gridMatrix[x, z - 1] == 2 || gridMatrix[x, z - 1] == 3));
            bool e_isRoom = (x + 1 < sizeX && (gridMatrix[x + 1, z] == 2 || gridMatrix[x + 1, z] == 3));
            bool w_isRoom = (x - 1 >= 0 && (gridMatrix[x - 1, z] == 2 || gridMatrix[x - 1, z] == 3));

            bool n = (z + 1 < sizeZ && gridMatrix[x, z + 1] != 0);
            bool s = (z - 1 >= 0 && gridMatrix[x, z - 1] != 0);
            bool e = (x + 1 < sizeX && gridMatrix[x + 1, z] != 0);
            bool w = (x - 1 >= 0 && gridMatrix[x - 1, z] != 0);

            bool hasAdjacentRoom = n_isRoom || s_isRoom || e_isRoom || w_isRoom;
            int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

            if (count == 4)
            {
                rot = Quaternion.identity;
                return GetPrefab(database.cross4WayPrefabs);
            }

            if (count == 3)
            {
                if (!n) rot = Quaternion.Euler(0, 180, 0);
                else if (!s) rot = Quaternion.Euler(0, 0, 0);
                else if (!e) rot = Quaternion.Euler(0, 270, 0);
                else if (!w) rot = Quaternion.Euler(0, 90, 0);
                return GetPrefab(database.tJunctionPrefabs);
            }

            if (count == 2)
            {
                if (n && s) { rot = Quaternion.Euler(0, 0, 0); return GetPrefab(database.straightCorridorPrefabs); }
                if (e && w) { rot = Quaternion.Euler(0, 90, 0); return GetPrefab(database.straightCorridorPrefabs); }

                if (n && e) rot = Quaternion.Euler(0, 0, 0);
                else if (e && s) rot = Quaternion.Euler(0, 90, 0);
                else if (s && w) rot = Quaternion.Euler(0, 180, 0);
                else if (w && n) rot = Quaternion.Euler(0, 270, 0);
                return GetPrefab(database.curve90Prefabs);
            }

            if (count == 1)
            {
                if (n) rot = Quaternion.Euler(0, 0, 0);
                else if (s) rot = Quaternion.Euler(0, 180, 0);
                else if (e) rot = Quaternion.Euler(0, 90, 0);
                else if (w) rot = Quaternion.Euler(0, 270, 0);
                return GetPrefab(database.straightCorridorPrefabs);
            }

            return GetPrefab(database.cross4WayPrefabs);
        }

        private HospitalModule GetPrefab(List<HospitalModule> list)
        {
            if (list != null && list.Count > 0) return list[Random.Range(0, list.Count)];
            return database.GetRandomStandardModule();
        }

        private void ClearBlockingWallAtConnector(ModuleConnector connector)
        {
            if (connector == null) return;
            Vector3 connPos = connector.GetWorldPosition();

            // 1. Limpieza por componentes de módulo
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;

                // NUNCA MODIFICAR NI DESACTIVAR PAREDES DE LAS HABITACIONES
                if (mod.moduleType == ModuleType.SmallRoom || mod.moduleType == ModuleType.DirectorOffice || mod.moduleType == ModuleType.LargeRoom || mod.moduleType == ModuleType.OfficeRoom) continue;

                Transform[] children = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    if (t == null || t == mod.transform || t.GetComponent<ModuleConnector>() != null) continue;

                    string n = t.name.ToLower();
                    if (n.Contains("outer") || n.Contains("marco") || n.Contains("door") || n.Contains("puerta") || n.Contains("frame")) continue;

                    Collider col = t.GetComponent<Collider>();
                    Renderer rend = t.GetComponent<Renderer>();

                    Vector3 objPos = t.position;
                    if (col != null) objPos = col.bounds.center;
                    else if (rend != null) objPos = rend.bounds.center;

                    float dist = Vector3.Distance(objPos, connPos);
                    if (dist <= 1.3f)
                    {
                        // IMPORTANTE: Si esta pared pertenece a un módulo de Habitación (SmallRoom/DirectorOffice/LargeRoom), NUNCA apagarla salvo que sea la puerta
                        HospitalModule wallModule = t.GetComponentInParent<HospitalModule>();
                        if (wallModule != null && (wallModule.moduleType == ModuleType.SmallRoom || wallModule.moduleType == ModuleType.DirectorOffice || wallModule.moduleType == ModuleType.LargeRoom || wallModule.moduleType == ModuleType.OfficeRoom))
                        {
                            // Preservar pared de la habitación
                            continue;
                        }

                        if (col != null) col.enabled = false;
                        t.gameObject.SetActive(false);
                    }
                }
            }

            // 2. Limpieza de seguridad por OverlapSphere de cualquier Collider invisible remanente
            Collider[] hits = Physics.OverlapSphere(connPos, 1.2f);
            foreach (Collider c in hits)
            {
                if (c == null) continue;
                Transform t = c.transform;

                // Ignorar suelo, techo, jugador, linterna, ascensor y puertas
                string nameLower = t.name.ToLower();
                if (nameLower.Contains("unified") || nameLower.Contains("player") || nameLower.Contains("door") || 
                    nameLower.Contains("hinge") || nameLower.Contains("elevator") || nameLower.Contains("floor") || 
                    nameLower.Contains("suelo") || nameLower.Contains("piso")) continue;

                HospitalModule parentMod = t.GetComponentInParent<HospitalModule>();
                if (parentMod != null)
                {
                    if (parentMod.moduleType == ModuleType.SmallRoom || parentMod.moduleType == ModuleType.DirectorOffice || 
                        parentMod.moduleType == ModuleType.LargeRoom || parentMod.moduleType == ModuleType.OfficeRoom) continue;
                }

                c.enabled = false;
                t.gameObject.SetActive(false);
            }
        }

        private void UnblockDirectorOfficeEntrance()
        {
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null || mod.moduleType != ModuleType.DirectorOffice) continue;

                // 1. Localizar la bisagra de la puerta, la puerta o el teclado Keypad de la Oficina del Director
                Vector3 frontCheckPos = mod.transform.position;

                Transform keypadT = null;
                Transform doorT = null;
                Transform[] children = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    if (t == null) continue;
                    string n = t.name.ToLower();
                    if (keypadT == null && (n.Contains("keypad") || n.Contains("teclado") || n.Contains("panel")))
                        keypadT = t;
                    if (doorT == null && (n.Contains("hinge") || n.Contains("puerta") || n.Contains("door")) && !n.Contains("frame") && !n.Contains("marco"))
                        doorT = t;
                }

                if (keypadT != null)
                {
                    frontCheckPos = keypadT.position + keypadT.forward * 0.8f;
                }
                else if (doorT != null)
                {
                    frontCheckPos = doorT.position + doorT.forward * 0.8f;
                }
                else
                {
                    frontCheckPos = mod.transform.position + mod.transform.forward * 1.8f;
                }

                // 2. Limpiar CUALQUIER pared, pilar o bloque macizo de pasillo que esté tapando la entrada o Keypad de la Oficina del Director
                Collider[] nearCols = Physics.OverlapSphere(frontCheckPos, 2.2f);
                foreach (Collider col in nearCols)
                {
                    if (col == null || col.gameObject == mod.gameObject || col.transform.IsChildOf(mod.transform)) continue;

                    string cName = col.gameObject.name.ToLower();
                    string rName = col.transform.root.name.ToLower();

                    // NUNCA deshabilitar elementos esenciales protegidos (Oficina del Director, Ascensor, Jugador, Suelo, Techo, Lámparas, Keypad, Puertas de la habitación)
                    bool isProtected = rName.Contains("director") || rName.Contains("elevator") || rName.Contains("ascensor") ||
                                       cName.Contains("unified_floor") || cName.Contains("unified_ceiling") ||
                                       cName.Contains("player") || cName.Contains("lamp") || cName.Contains("keypad") || cName.Contains("door") || cName.Contains("hinge");

                    if (!isProtected)
                    {
                        // Si es un objeto de pared, bloque macizo o pilar que se interpone frente a la Oficina del Director, desactivarlo
                        if (cName.Contains("wall") || cName.Contains("pared") || cName.Contains("solid") || cName.Contains("pillar") || cName.Contains("cube") || cName.Contains("block"))
                        {
                            col.enabled = false;
                            col.gameObject.SetActive(false);
                            Debug.Log($"ModularHospitalGenerator: Pared/Bloque tapante '{col.gameObject.name}' desactivado con éxito frente a la Oficina del Director.");
                        }
                    }
                }
            }
        }

        private void SanitizeMeshColliders(Transform parent)
        {
            if (parent == null) return;
            MeshCollider[] mcs = parent.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in mcs)
            {
                if (mc == null) continue;
                if (mc.isTrigger && !mc.convex)
                {
                    mc.convex = true;
                }
            }
        }

        private void BuildSolidWallBlock(Transform parent, Vector3 cellPos)
        {
            Material wallMat = customWallMaterial;
            if (wallMat == null)
            {
#if UNITY_EDITOR
                wallMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Dnk_Dev/HospitalHorrorPack/Materials/T_HospitalWall_New.mat");
#endif
            }

            GameObject wallBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallBlock.name = $"[Solid_Wall_Block_{cellPos.x}_{cellPos.z}]";
            wallBlock.transform.SetParent(parent);
            wallBlock.transform.position = new Vector3(cellPos.x, 2.0f, cellPos.z);
            wallBlock.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);

            BoxCollider col = wallBlock.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = false;
                col.size = Vector3.one;
            }

            if (wallMat != null)
            {
                MeshRenderer mr = wallBlock.GetComponent<MeshRenderer>();
                Material instMat = new Material(wallMat);
                instMat.mainTextureScale = new Vector2(1.0f, 1.0f);
                if (instMat.HasProperty("_BaseMap")) instMat.SetTextureScale("_BaseMap", new Vector2(1.0f, 1.0f));
                mr.sharedMaterial = instMat;
            }
        }

        private void BuildUnifiedFloorAndCeilingSlabs(Transform parent)
        {
            Material floorMat = customFloorMaterial;
            Material ceilingMat = customCeilingMaterial;

            if (floorMat == null && database != null && database.straightCorridorPrefabs != null && database.straightCorridorPrefabs.Count > 0)
            {
                MeshRenderer mr = database.straightCorridorPrefabs[0].GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) floorMat = mr.sharedMaterial;
            }

            Vector3 center = currentBuildingBounds.center;
            float width = currentBuildingBounds.size.x;
            float depth = currentBuildingBounds.size.z;

            float minX = center.x - (width / 2.0f);
            float maxX = center.x + (width / 2.0f);
            float minZ = center.z - (depth / 2.0f);
            float maxZ = center.z + (depth / 2.0f);

            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "[Unified_Floor_Slab]";
            floorObj.transform.SetParent(parent);
            floorObj.transform.position = new Vector3(center.x, -0.1f, center.z);
            floorObj.transform.localScale = new Vector3(width, 0.2f, depth);

            if (floorMat != null)
            {
                MeshRenderer floorRenderer = floorObj.GetComponent<MeshRenderer>();
                floorRenderer.material = new Material(floorMat);
                floorRenderer.material.mainTextureScale = new Vector2(width / 4.0f, depth / 4.0f);
            }

            GameObject ceilingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceilingObj.name = "[Unified_Ceiling_Slab]";
            ceilingObj.transform.SetParent(parent);
            ceilingObj.transform.position = new Vector3(center.x, 3.9f, center.z);
            ceilingObj.transform.localScale = new Vector3(width, 0.2f, depth);

            if (ceilingMat != null)
            {
                MeshRenderer ceilingRenderer = ceilingObj.GetComponent<MeshRenderer>();
                ceilingRenderer.material = new Material(ceilingMat);
                ceilingRenderer.material.mainTextureScale = new Vector2(width / 4.0f, depth / 4.0f);
            }

            Material wallMat = customWallMaterial;
            if (wallMat == null)
            {
#if UNITY_EDITOR
                wallMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Dnk_Dev/HospitalHorrorPack/Materials/T_HospitalWall_New.mat");
#endif
            }

            CreatePerimeterWall(parent, "[Outer_Wall_North]", new Vector3(center.x, 2.0f, maxZ), new Vector3(width + 0.8f, 4.0f, 0.4f), wallMat);
            CreatePerimeterWall(parent, "[Outer_Wall_South]", new Vector3(center.x, 2.0f, minZ), new Vector3(width + 0.8f, 4.0f, 0.4f), wallMat);
            CreatePerimeterWall(parent, "[Outer_Wall_East]", new Vector3(maxX, 2.0f, center.z), new Vector3(0.4f, 4.0f, depth + 0.8f), wallMat);
            CreatePerimeterWall(parent, "[Outer_Wall_West]", new Vector3(minX, 2.0f, center.z), new Vector3(0.4f, 4.0f, depth + 0.8f), wallMat);
        }

        private Vector2Int PositionToGridCell(Vector3 worldPos)
        {
            float halfW = (smallMapGridSize.x * 4.0f) / 2.0f;
            float halfD = (smallMapGridSize.y * 4.0f) / 2.0f;
            int gx = Mathf.FloorToInt((worldPos.x + halfW) / 4.0f);
            int gz = Mathf.FloorToInt((worldPos.z + halfD) / 4.0f);
            return new Vector2Int(gx, gz);
        }

        private void CreatePerimeterWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;

            if (mat != null)
            {
                MeshRenderer mr = wall.GetComponent<MeshRenderer>();
                Material instMat = new Material(mat);

                float wallLength = Mathf.Max(scale.x, scale.z);
                float tilingX = wallLength / 4.0f;
                float tilingY = scale.y / 4.0f;

                Vector2 tiling = new Vector2(tilingX, tilingY);
                instMat.mainTextureScale = tiling;
                if (instMat.HasProperty("_BaseMap")) instMat.SetTextureScale("_BaseMap", tiling);

                mr.material = instMat;
            }
        }

        private void SealGridCornerGaps(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            Material wallMat = customWallMaterial;
            if (wallMat == null)
            {
#if UNITY_EDITOR
                wallMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Dnk_Dev/HospitalHorrorPack/Materials/T_HospitalWall_New.mat");
#endif
            }

            // Pilar estructural de sellado únicamente en esquinas de borde de muro (NO en medio de pasillos ni cruces abiertos)
            for (int x = 0; x <= sizeX; x++)
            {
                for (int z = 0; z <= sizeZ; z++)
                {
                    int wallCount = 0;
                    int openCount = 0;

                    for (int dx = -1; dx <= 0; dx++)
                    {
                        for (int dz = -1; dz <= 0; dz++)
                        {
                            int cx = x + dx;
                            int cz = z + dz;

                            if (cx < 0 || cx >= sizeX || cz < 0 || cz >= sizeZ)
                            {
                                wallCount++; // Borde exterior del mapa
                            }
                            else if (gridMatrix[cx, cz] == 0)
                            {
                                wallCount++; // Pared
                            }
                            else
                            {
                                openCount++; // Pasillo o habitación
                            }
                        }
                    }

                    // Colocar pilares estructurales en intersecciones y esquinas amplias para dar soporte y ambiente hospitalario
                    if (wallCount >= 1 && openCount >= 1 && openCount <= 3)
                    {
                        float cornerX = (x * 4.0f) - halfW;
                        float cornerZ = (z * 4.0f) - halfD;
                        Vector3 pillarPos = new Vector3(cornerX, 2.0f, cornerZ);

                        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        pillar.name = $"[Grid_Corner_Pillar_{x}_{z}]";
                        pillar.transform.SetParent(parent);
                        pillar.transform.position = pillarPos;
                        pillar.transform.localScale = new Vector3(0.55f, 4.0f, 0.55f); // Grosor de 55cm para sellar 100% cualquier ranura de junta de pared

                        // BoxCollider físico sólido para que los pilares NO se puedan traspasar
                        BoxCollider boxCol = pillar.GetComponent<BoxCollider>();
                        if (boxCol != null)
                        {
                            boxCol.isTrigger = false;
                            boxCol.size = Vector3.one;
                        }

                        if (wallMat != null)
                        {
                            MeshRenderer mr = pillar.GetComponent<MeshRenderer>();
                            Material instMat = new Material(wallMat);
                            Vector2 tiling = new Vector2(0.25f, 1.0f);
                            instMat.mainTextureScale = tiling;
                            if (instMat.HasProperty("_BaseMap")) instMat.SetTextureScale("_BaseMap", tiling);
                            mr.sharedMaterial = instMat;
                        }
                    }
                }
            }
        }

        private void BuildCeilingLamps(Transform parent)
        {
            if (!spawnCeilingLamps || placedModules == null || placedModules.Count == 0) return;

            if (lampPrefab == null)
            {
#if UNITY_EDITOR
                lampPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Lamp.prefab");
#endif
            }

            HashSet<Vector2Int> corridorLampCells = new HashSet<Vector2Int>();

            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;

                bool isRoom = mod.moduleType == ModuleType.SmallRoom || mod.moduleType == ModuleType.DirectorOffice || mod.moduleType == ModuleType.LargeRoom || mod.moduleType == ModuleType.OfficeRoom;
                Vector3 pos = mod.transform.position;
                Vector2Int cellPos = PositionToGridCell(pos);

                // En pasillos: Lámparas cada 2 celdas para iluminación óptima y uniforme en todo el laberinto (20% quemadas)
                if (!isRoom)
                {
                    bool tooClose = false;
                    foreach (Vector2Int existing in corridorLampCells)
                    {
                        if (Mathf.Abs(existing.x - cellPos.x) < 2 && Mathf.Abs(existing.y - cellPos.y) < 2)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    if (Random.value < 0.20f) continue; // 20% luz rota para mantener buena iluminación en pasillos

                    corridorLampCells.Add(cellPos);
                }
                else
                {
                    if (Random.value < 0.25f) continue; // 25% fundidas en habitaciones
                }

                Vector3 lampPos = new Vector3(pos.x, 3.82f, pos.z);

                GameObject lampObj = null;
                if (lampPrefab != null)
                {
                    lampObj = Instantiate(lampPrefab, lampPos, Quaternion.identity, parent);
                }
                else
                {
                    lampObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lampObj.name = "Ceiling_Lamp_Model";
                    lampObj.transform.SetParent(parent);
                    lampObj.transform.position = lampPos;
                    lampObj.transform.localScale = new Vector3(1.2f, 0.08f, 0.3f);
                }

                GameObject lightChild = new GameObject("Lamp_Light");
                lightChild.transform.SetParent(lampObj.transform);
                lightChild.transform.localPosition = new Vector3(0f, -0.6f, 0f);

                Light ptLight = lightChild.AddComponent<Light>();
                ptLight.type = LightType.Point;
                ptLight.color = new Color(0.40f, 0.95f, 0.75f); // Luz cian-verde esmeralda brillante
                ptLight.intensity = 3.5f; // Potente haz de luz de techo a suelo
                ptLight.range = 8.5f; // Alcance de 8.5m para iluminar completamente pasillos y cruces
                ptLight.shadows = LightShadows.None;

                FlickerLamp flicker = lightChild.AddComponent<FlickerLamp>();
                flicker.baseIntensity = 3.5f;
            }
        }

        private void BuildElevator(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (elevatorPrefab == null)
            {
#if UNITY_EDITOR
                elevatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Elevator/Elevator.prefab");
                if (elevatorPrefab == null) elevatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Elevator.prefab");
#endif
            }

            if (elevatorPrefab == null) return;

            // 1. Buscar ÚNICAMENTE celdas de CALLEJÓN CIEGO / ALCOBA (1 único pasillo de entrada y paredes sólidas a los lados)
            Vector2Int bestCell = new Vector2Int(1, 1);
            float maxDistance = -1f;

            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    if (gridMatrix[x, z] == 1) // Únicamente Pasillo
                    {
                        // EXCLUIR CUALQUIER CELDA QUE ESTÉ A MENOS DE 2 CASILLAS DE DISTANCIA DE CUALQUIER HABITACIÓN
                        bool isNearRoom = false;
                        for (int rx = Mathf.Max(0, x - 2); rx <= Mathf.Min(sizeX - 1, x + 2); rx++)
                        {
                            for (int rz = Mathf.Max(0, z - 2); rz <= Mathf.Min(sizeZ - 1, z + 2); rz++)
                            {
                                if (gridMatrix[rx, rz] == 2 || gridMatrix[rx, rz] == 3)
                                {
                                    isNearRoom = true;
                                    break;
                                }
                            }
                            if (isNearRoom) break;
                        }

                        if (isNearRoom) continue; // SOLO UBICAR EN UN PASILLO AISLADO LEJOS DE HABITACIONES

                        int corridorNeighbors = 0;
                        if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) corridorNeighbors++;
                        if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) corridorNeighbors++;
                        if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) corridorNeighbors++;
                        if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) corridorNeighbors++;

                        int wallNeighbors = 0;
                        if (z + 1 >= sizeZ || gridMatrix[x, z + 1] == 0) wallNeighbors++;
                        if (z - 1 < 0 || gridMatrix[x, z - 1] == 0) wallNeighbors++;
                        if (x + 1 >= sizeX || gridMatrix[x + 1, z] == 0) wallNeighbors++;
                        if (x - 1 < 0 || gridMatrix[x - 1, z] == 0) wallNeighbors++;

                        // PRIORIDAD MÁXIMA: Callejón ciego encajonado entre paredes sólidas (1 pasillo de entrada y paredes a los lados)
                        if (corridorNeighbors == 1)
                        {
                            float dist = Vector2.Distance(new Vector2(1, 1), new Vector2(x, z));
                            float score = dist + (wallNeighbors >= 2 ? 100f : 20f);
                            if (score > maxDistance)
                            {
                                maxDistance = score;
                                bestCell = new Vector2Int(x, z);
                            }
                        }
                    }
                }
            }

            int ex = bestCell.x;
            int ez = bestCell.y;

            float worldX = (ex * 4.0f) - halfW + 2.0f;
            float worldZ = (ez * 4.0f) - halfD + 2.0f;
            Vector3 baseCellPos = new Vector3(worldX, transform.position.y, worldZ);

            // 2. Desactivar el módulo de pasillo base en esa celda para reemplazarlo 1:1 por el nuevo Prefab Módulo Ascensor de 4x4m
            List<HospitalModule> targetMods = new List<HospitalModule>();
            foreach (HospitalModule mod in placedModules)
            {
                if (mod != null && Vector3.Distance(mod.transform.position, baseCellPos) < 2.5f)
                {
                    targetMods.Add(mod);
                }
            }
            foreach (var mod in targetMods)
            {
                if (mod != null) mod.gameObject.SetActive(false);
            }

            // TAMBIÉN desactivar objetos de pared de módulos vecinos que dan al frente del elevador
            // para evitar que sus paredes internas bloqueen la entrada
            int[] dxs = new int[] { 0, 0, 1, -1 };
            int[] dzs = new int[] { 1, -1, 0, 0 };
            for (int d = 0; d < 4; d++)
            {
                int nx = ex + dxs[d];
                int nz = ez + dzs[d];
                if (nx < 0 || nx >= sizeX || nz < 0 || nz >= sizeZ) continue;
                if (gridMatrix[nx, nz] != 1) continue; // Solo pasillos vecinos

                float nwx = (nx * 4.0f) - halfW + 2.0f;
                float nwz = (nz * 4.0f) - halfD + 2.0f;
                Vector3 neighborPos = new Vector3(nwx, transform.position.y, nwz);

                foreach (HospitalModule nMod in placedModules)
                {
                    if (nMod == null || Vector3.Distance(nMod.transform.position, neighborPos) >= 2.5f) continue;

                    // Desactivar solo los hijos que sean paredes apuntando hacia la celda del elevador
                    Transform[] children = nMod.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in children)
                    {
                        if (child == null || child == nMod.transform) continue;
                        string cn = child.name.ToLower();
                        if (!(cn.Contains("wall") || cn.Contains("pared") || cn.Contains("marco") || cn.Contains("frame"))) continue;

                        // Solo desactivar si la pared está entre el vecino y el elevador
                        Vector3 toElev = (baseCellPos - neighborPos).normalized;
                        Vector3 toChild = (child.position - neighborPos).normalized;
                        if (Vector3.Dot(toChild, toElev) > 0.5f)
                        {
                            child.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // 3. Evaluar cuál de las celdas vecinas conduce al interior del mapa para girar la puerta de frente al pasillo libre
            Quaternion rot = Quaternion.identity;
            Vector2 center = new Vector2(sizeX / 2.0f, sizeZ / 2.0f);
            float bestNeighborDist = 999f;

            if (ez + 1 < sizeZ && gridMatrix[ex, ez + 1] == 1)
            {
                float d = Vector2.Distance(new Vector2(ex, ez + 1), center);
                if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 180, 0); } // Pasillo al NORTE
            }
            if (ez - 1 >= 0 && gridMatrix[ex, ez - 1] == 1)
            {
                float d = Vector2.Distance(new Vector2(ex, ez - 1), center);
                if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 0, 0); } // Pasillo al SUR
            }
            if (ex + 1 < sizeX && gridMatrix[ex + 1, ez] == 1)
            {
                float d = Vector2.Distance(new Vector2(ex + 1, ez), center);
                if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 90, 0); } // Pasillo al ESTE
            }
            if (ex - 1 >= 0 && gridMatrix[ex - 1, ez] == 1)
            {
                float d = Vector2.Distance(new Vector2(ex - 1, ez), center);
                if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 270, 0); } // Pasillo al OESTE
            }

            // Desfasar ligeramente hacia adelante para encuadrar 100% al ras con los pilares del pasillo
            Vector3 alignedPos = baseCellPos + rot * Vector3.forward * 0.35f;
            GameObject elevObj = Instantiate(elevatorPrefab, alignedPos, rot, parent);
            elevObj.name = "[Hospital_Escape_Elevator]";
            lastElevatorPos = alignedPos;

            // AJUSTE PRECISO DE ALTURA: Asentar la base del ascensor 100% sobre el piso
            MeshRenderer[] allRends = elevObj.GetComponentsInChildren<MeshRenderer>(true);
            if (allRends != null && allRends.Length > 0)
            {
                float minY = float.MaxValue;
                foreach (MeshRenderer mr in allRends)
                {
                    if (mr != null && mr.enabled)
                    {
                        if (mr.bounds.min.y < minY) minY = mr.bounds.min.y;
                    }
                }
                if (minY < 900f)
                {
                    float yOffset = transform.position.y - minY;
                    if (Mathf.Abs(yOffset) > 0.001f)
                    {
                        elevObj.transform.position += new Vector3(0, yOffset, 0);
                    }
                }
            }

            ElevatorController ctrl = elevObj.GetComponent<ElevatorController>();
            if (ctrl == null) ctrl = elevObj.AddComponent<ElevatorController>();

            ctrl.startWithKeycard = startWithKeycard;
            ctrl.bypassKeycard = bypassKeycard;
            ctrl.nextSceneName = "TunnelsMap";
            ctrl.doorSpeed = 0.22f;
            if (startWithKeycard) ElevatorController.hasKeycard = true;

            // ELIMINAR CUALQUIER PARED Y BLOQUE MACIZO QUE BLOQUEE LA ENTRADA DEL ELEVADOR
            Vector3 doorFacing = rot * Vector3.forward;
            Vector3 doorFront = alignedPos + doorFacing * 2.0f; // Punto 2.0m frente a la puerta del ascensor

            Collider[] nearColliders = Physics.OverlapSphere(doorFront, 2.5f);
            foreach (Collider col in nearColliders)
            {
                if (col == null || col.gameObject == elevObj || col.transform.IsChildOf(elevObj.transform)) continue;

                string cName = col.gameObject.name.ToLower();
                string rName = col.transform.root.name.ToLower();

                // NUNCA borrar el ascensor, jugador, suelo unificado, techo unificado o lámparas
                bool isProtected = rName.Contains("elevator") || rName.Contains("ascensor") ||
                                   cName.Contains("unified_floor") || cName.Contains("unified_ceiling") ||
                                   cName.Contains("player") || cName.Contains("lamp");

                if (!isProtected)
                {
                    col.enabled = false;
                    col.gameObject.SetActive(false);
                }
            }

            Debug.Log($"[Elevator] Entrada desbloqueada — paredes frente al ascensor eliminadas en {doorFront}.");
        }

        private void BuildFuseBoxAndFuse(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (!spawnFuseBoxAndFuse) return;

            if (fuseBoxPrefab == null)
            {
#if UNITY_EDITOR
                fuseBoxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/FuseBox.prefab");
                if (fuseBoxPrefab == null) fuseBoxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Fuse_Box.prefab");
#endif
            }

            if (fusePrefab == null)
            {
#if UNITY_EDITOR
                fusePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Fuse.prefab");
#endif
            }

            // 1. Spawning FuseBox preferentemente en SpawnPoints asignados o adosada a pared de pasillo recto
            if (fuseBoxPrefab != null)
            {
                Vector3 finalFbPos = transform.position + Vector3.up * 1.25f;
                Quaternion finalFbRot = Quaternion.identity;
                bool foundSpawnPoint = false;

                // Buscar prioritariamente un Transform asignado para FuseBox en los módulos colocados
                foreach (HospitalModule mod in placedModules)
                {
                    if (mod == null) continue;
                    Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allT)
                    {
                        if (t == null) continue;
                        string tName = t.name.ToLower();
                        if (tName.Contains("fuseboxspawn") || tName.Contains("fuse_box_spawn") || tName.Contains("spawnpoint_fusebox") || tName.Contains("spawn_fusebox"))
                        {
                            finalFbPos = t.position;
                            finalFbRot = t.rotation;
                            foundSpawnPoint = true;
                            Debug.Log($"[FuseBox] Encontrado SpawnPoint designado '{t.name}' en módulo {mod.name}.");
                            break;
                        }
                    }
                    if (foundSpawnPoint) break;
                }

                if (!foundSpawnPoint)
                {
                    // Buscar exclusivamente módulos de PASILLO RECTO (Module_StraightCorridor)
                    HospitalModule targetModule = null;
                    List<HospitalModule> straightCorridors = new List<HospitalModule>();
                    foreach (HospitalModule mod in placedModules)
                    {
                        if (mod != null && mod.moduleType == ModuleType.StraightCorridor)
                        {
                            // Excluir si está demasiado cerca del elevador o generadores (> 8.0m de distancia mínima)
                            if (lastElevatorPos.x > -900f && Vector3.Distance(mod.transform.position, lastElevatorPos) < 6.0f) continue;

                            bool nearGen = false;
                            SubGenerator[] existingGens = FindObjectsOfType<SubGenerator>();
                            foreach (var gen in existingGens)
                            {
                                if (gen != null && Vector3.Distance(mod.transform.position, gen.transform.position) < 8.0f)
                                {
                                    nearGen = true;
                                    break;
                                }
                            }
                            if (nearGen) continue;

                            straightCorridors.Add(mod);
                        }
                    }

                    if (straightCorridors.Count > 0)
                    {
                        targetModule = straightCorridors[Random.Range(0, straightCorridors.Count)];
                    }

                    if (targetModule != null)
                    {
                        float sideX = Random.value > 0.5f ? -1.85f : 1.85f;
                        Vector3 localWallPos = new Vector3(sideX, 1.25f, 0f);
                        finalFbPos = targetModule.transform.TransformPoint(localWallPos);

                        Vector3 moduleCenter = targetModule.transform.position + Vector3.up * 1.25f;
                        Vector3 rayDir = (finalFbPos - moduleCenter).normalized;
                        RaycastHit wallHit;
                        // Usar raycast ignorando triggers/props
                        if (Physics.Raycast(moduleCenter, rayDir, out wallHit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            finalFbPos = wallHit.point + wallHit.normal * 0.55f; // Desplazar 0.55m hacia afuera para sacar la caja metálica totalmente de la pared
                            finalFbRot = Quaternion.LookRotation(wallHit.normal, Vector3.up); // Mirar hacia el pasillo
                        }
                        else
                        {
                            finalFbRot = targetModule.transform.rotation * Quaternion.Euler(0f, sideX < 0f ? 90f : 270f, 0f);
                        }
                    }
                }

                // Instanciar la Caja de Fusibles
                GameObject fbObj = Instantiate(fuseBoxPrefab, finalFbPos, finalFbRot, parent);
                fbObj.name = "[Hospital_FuseBox]";

                // Si se usó pasillo y no spawn point, ajustar altura suavemente si es necesario
                if (!foundSpawnPoint)
                {
                    Transform cellingPt = null;
                    foreach (Transform child in fbObj.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null) continue;
                        string cName = child.name.ToLower();
                        if (cName.Contains("celling") || cName.Contains("ceiling"))
                        {
                            cellingPt = child;
                            break;
                        }
                    }

                    if (cellingPt != null)
                    {
                        float yDiff = transform.position.y - cellingPt.position.y;
                        fbObj.transform.position += new Vector3(0, yDiff, 0);
                    }
                }


                PowerBox pBox = fbObj.GetComponent<PowerBox>();
                if (pBox == null) pBox = fbObj.AddComponent<PowerBox>();

                BoxCollider bCol = fbObj.GetComponent<BoxCollider>();
                if (bCol == null) bCol = fbObj.AddComponent<BoxCollider>();
                bCol.isTrigger = false;
                bCol.size = new Vector3(0.8f, 1.4f, 0.8f);
                bCol.center = new Vector3(0f, 0.7f, 0f);

                // LIMPIAR CUALQUIER PROP O COLUMNA SECUNDARIA QUE TAPE LA CARA FRONTAL (JAMÁS DESTRUIR PAREDES O ESTRUCTURAS)
                Collider[] nearCols = Physics.OverlapSphere(fbObj.transform.position + Vector3.up * 0.8f, 0.75f);
                foreach (Collider c in nearCols)
                {
                    if (c == null || c.gameObject == fbObj || c.transform.IsChildOf(fbObj.transform)) continue;
                    string cn = c.gameObject.name.ToLower();
                    // Únicamente desactivar columnas de decoración, marcos secundarios o props — JAMÁS paredes principales ni habitaciones
                    if (cn.Contains("pillar") || cn.Contains("columna") || cn.Contains("marco") || cn.Contains("prop") || cn.Contains("decor"))
                    {
                        c.gameObject.SetActive(false);
                    }
                }
            }

            // 2. Spawning dinámico de Fusibles (NUNCA EN OFICINA DEL DIRECTOR, MÁXIMO 1 FUSIBLE POR HABITACIÓN)
            if (fusePrefab != null)
            {
                List<HospitalModule> smallRoomsForFuse = new List<HospitalModule>();
                foreach (HospitalModule mod in placedModules)
                {
                    if (mod != null && mod.moduleType == ModuleType.SmallRoom)
                    {
                        smallRoomsForFuse.Add(mod);
                    }
                }

                string diff = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
                int fusesNeeded = 3;
                if (diff == "FACIL") fusesNeeded = 4;
                else if (diff == "DIFICIL") fusesNeeded = 2;

                // Buscar pasillos de distribución para colocar fusibles sobre el suelo lejos de puertas
                List<Vector3> corridorFloorPositions = new List<Vector3>();
                for (int x = 1; x < sizeX - 1; x++)
                {
                    for (int z = 1; z < sizeZ - 1; z++)
                    {
                        if (gridMatrix[x, z] == 1) // Es pasillo
                        {
                            Vector3 cPos = new Vector3((x * 4.0f) - halfW + 2.0f, transform.position.y + 0.05f, (z * 4.0f) - halfD + 2.0f);
                            if (lastElevatorPos.x > -900f && Vector3.Distance(cPos, lastElevatorPos) < 7.0f) continue;
                            corridorFloorPositions.Add(cPos);
                        }
                    }
                }

                for (int i = 0; i < fusesNeeded; i++)
                {
                    Vector3 spawnPos = Vector3.zero;
                    bool validPosFound = false;

                    for (int attempt = 0; attempt < corridorFloorPositions.Count; attempt++)
                    {
                        int cIdx = Random.Range(0, corridorFloorPositions.Count);
                        Vector3 testPos = corridorFloorPositions[cIdx];

                        // Verificar que no esté cerca de otro ítem ya spawneado (mínimo 4.5 metros)
                        bool tooCloseToOtherItem = false;
                        foreach (Vector3 existingItemPos in spawnedItemPositions)
                        {
                            if (Vector3.Distance(testPos, existingItemPos) < 4.5f)
                            {
                                tooCloseToOtherItem = true;
                                break;
                            }
                        }

                        if (!tooCloseToOtherItem)
                        {
                            spawnPos = testPos;
                            corridorFloorPositions.RemoveAt(cIdx);
                            validPosFound = true;
                            break;
                        }
                    }

                    if (!validPosFound && corridorFloorPositions.Count > 0)
                    {
                        int cIdx = Random.Range(0, corridorFloorPositions.Count);
                        spawnPos = corridorFloorPositions[cIdx];
                        corridorFloorPositions.RemoveAt(cIdx);
                    }
                    else if (!validPosFound)
                    {
                        spawnPos = new Vector3(i * 3.0f, transform.position.y + 0.05f, 0f);
                    }

                    // REGLA ESTRICTA: NUNCA DENTRO NI CERCA DEL ELEVADOR (< 6.0m)
                    if (lastElevatorPos.x > -900f && Vector3.Distance(spawnPos, lastElevatorPos) < 6.0f) continue;

                    spawnedItemPositions.Add(spawnPos); // Registrar posición

                    GameObject fObj = Instantiate(fusePrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360), 0), parent);
                    fObj.name = $"[Hospital_Collectible_Fuse_{i + 1}]";

                    // Habilitar visuales y ajustar base al ras del suelo
                    ActivateItemFully(fObj);

                    RaycastHit fuseHit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out fuseHit, 3.0f))
                    {
                        fObj.transform.position = fuseHit.point;
                        MeshRenderer mrF = fObj.GetComponentInChildren<MeshRenderer>();
                        if (mrF != null)
                        {
                            float bottomY = mrF.bounds.min.y;
                            float diffY = fuseHit.point.y - bottomY;
                            fObj.transform.position += new Vector3(0, diffY, 0);
                        }
                    }
                    else
                    {
                        MeshRenderer mrFuse = fObj.GetComponentInChildren<MeshRenderer>();
                        if (mrFuse != null)
                        {
                            float meshMinY = mrFuse.bounds.min.y;
                            float surfaceY = spawnPos.y;
                            float correction = surfaceY - meshMinY;
                            fObj.transform.position += new Vector3(0, correction + 0.002f, 0);
                        }
                    }

                    FuseItem fuseComp = fObj.GetComponent<FuseItem>();
                    if (fuseComp == null) fuseComp = fObj.AddComponent<FuseItem>();
                    fuseComp.interactDistance = 2.5f;
                }
            }
        }

        private void BuildGenerator(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (!spawnGenerator) return;

            if (generatorPrefab == null)
            {
#if UNITY_EDITOR
                generatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Generator.prefab");
                if (generatorPrefab == null) generatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Gen.prefab");
                if (generatorPrefab == null) generatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/GeneratorRoom.prefab");
#endif
            }

            if (generatorPrefab == null) return;

            // 1. Ubicar SubGenerador A y B en las 2 esquinas opuestas extremas del mapa (Top-Left y Bottom-Right)
            Vector2Int cornerA = new Vector2Int(1, sizeZ - 2);          // Esquina Superior Izquierda (Top-Left)
            Vector2Int cornerB = new Vector2Int(sizeX - 2, 1);          // Esquina Inferior Derecha (Bottom-Right)

            Vector2Int cellA = GetClosestCorridorToCorner(cornerA, sizeX, sizeZ, halfW, halfD);
            Vector2Int cellB = GetClosestCorridorToCorner(cornerB, sizeX, sizeZ, halfW, halfD);

            Vector2Int[] selectedCells = new Vector2Int[] { cellA, cellB };
            string[] genNames = new string[] { "A", "B" };

            for (int i = 0; i < 2; i++)
            {
                int gx = selectedCells[i].x;
                int gz = selectedCells[i].y;

                float worldX = (gx * 4.0f) - halfW + 2.0f;
                float worldZ = (gz * 4.0f) - halfD + 2.0f;
                Vector3 baseCellPos = new Vector3(worldX, transform.position.y, worldZ);

                // Rotación hacia el pasillo libre y desfasar hacia la pared para NO bloquear el paso
                Quaternion rot = Quaternion.identity;
                Vector3 wallOffsetDir = Vector3.back;
                Vector2 center = new Vector2(sizeX / 2.0f, sizeZ / 2.0f);
                float bestNeighborDist = 999f;

                if (gz + 1 < sizeZ && gridMatrix[gx, gz + 1] == 1)
                {
                    float d = Vector2.Distance(new Vector2(gx, gz + 1), center);
                    if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 180, 0); wallOffsetDir = Vector3.forward; }
                }
                if (gz - 1 >= 0 && gridMatrix[gx, gz - 1] == 1)
                {
                    float d = Vector2.Distance(new Vector2(gx, gz - 1), center);
                    if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 0, 0); wallOffsetDir = Vector3.back; }
                }
                if (gx + 1 < sizeX && gridMatrix[gx + 1, gz] == 1)
                {
                    float d = Vector2.Distance(new Vector2(gx + 1, gz), center);
                    if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 90, 0); wallOffsetDir = Vector3.right; }
                }
                if (gx - 1 >= 0 && gridMatrix[gx - 1, gz] == 1)
                {
                    float d = Vector2.Distance(new Vector2(gx - 1, gz), center);
                    if (d < bestNeighborDist) { bestNeighborDist = d; rot = Quaternion.Euler(0, 270, 0); wallOffsetDir = Vector3.left; }
                }

                // Posicionar exactamente en el centro de la celda de pasillo (100% libre de invasión a paredes)
                Vector3 genPos = baseCellPos;

                // ELIMINAR CUALQUIER PARED Y BLOQUE MACIZO QUE BLOQUEE EL GENERADOR
                Collider[] nearGenCols = Physics.OverlapSphere(genPos, 3.0f);
                foreach (Collider col in nearGenCols)
                {
                    if (col == null) continue;
                    string cName = col.gameObject.name.ToLower();
                    string rName = col.transform.root.name.ToLower();

                    bool isProtected = rName.Contains("generator") || rName.Contains("subgenerator") ||
                                       cName.Contains("unified_floor") || cName.Contains("unified_ceiling") ||
                                       cName.Contains("player") || cName.Contains("lamp");

                    if (!isProtected && (cName.Contains("solid_wall") || cName.Contains("pillar") || cName.Contains("wall")))
                    {
                        col.enabled = false;
                        col.gameObject.SetActive(false);
                    }
                }

                GameObject genObj = Instantiate(generatorPrefab, genPos, rot, parent);
                genObj.name = $"[Hospital_SubGenerator_{genNames[i]}]";

                // AJUSTE PRECISO DE ALTURA: Asentar patas 100% sobre el piso
                MeshRenderer mr = genObj.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    float minY = mr.bounds.min.y;
                    float yOffset = baseCellPos.y - minY;
                    if (Mathf.Abs(yOffset) > 0.01f)
                    {
                        genObj.transform.position += new Vector3(0, yOffset, 0);
                    }
                }

                SubGenerator subGen = genObj.GetComponent<SubGenerator>();
                if (subGen == null) subGen = genObj.AddComponent<SubGenerator>();
                subGen.generatorName = genNames[i];
                subGen.isOn = false;

                if (i == 0) lastSubGenAPos = genObj.transform.position;
                else if (i == 1) lastSubGenBPos = genObj.transform.position;
            }
        }

        private Vector2Int GetClosestCorridorToCorner(Vector2Int mapCorner, int sizeX, int sizeZ, float halfW, float halfD)
        {
            Vector2Int bestCell = new Vector2Int(-1, -1);
            float minDistance = 999f;

            // Pasada 1: Buscar pasillos puros que no colinden directamente con habitaciones
            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    if (gridMatrix[x, z] == 1) // EXCLUSIVAMENTE PASILLOS (NUNCA HABITACIONES)
                    {
                        Vector3 worldPos = new Vector3((x * 4.0f) - halfW + 2.0f, transform.position.y, (z * 4.0f) - halfD + 2.0f);
                        if (lastElevatorPos.x > -900f && Vector3.Distance(worldPos, lastElevatorPos) < 8.0f) continue;

                        // EXCLUSIÓN ESTRICTA: No colocar en celdas que estén a menos de 2 celdas de cualquier habitación
                        bool bordersRoom = false;
                        for (int rx = Mathf.Max(0, x - 2); rx <= Mathf.Min(sizeX - 1, x + 2) && !bordersRoom; rx++)
                            for (int rz = Mathf.Max(0, z - 2); rz <= Mathf.Min(sizeZ - 1, z + 2) && !bordersRoom; rz++)
                                if (gridMatrix[rx, rz] == 2 || gridMatrix[rx, rz] == 3) bordersRoom = true;

                        if (bordersRoom) continue;

                        // También requiere al menos 1 vecino corredor (no estar completamente encerrado)
                        int corridorNeighbors = 0;
                        if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) corridorNeighbors++;
                        if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) corridorNeighbors++;
                        if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) corridorNeighbors++;
                        if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) corridorNeighbors++;
                        if (corridorNeighbors < 1) continue;

                        float dist = Vector2Int.Distance(mapCorner, new Vector2Int(x, z));
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestCell = new Vector2Int(x, z);
                        }
                    }
                }
            }

            // Pasada 2 (Fallback 100% Garantizado): Si no se encontró pasillo aislado, seleccionar CUALQUIER celda de pasillo (gridMatrix == 1)
            if (bestCell.x == -1)
            {
                minDistance = 999f;
                for (int x = 1; x < sizeX - 1; x++)
                {
                    for (int z = 1; z < sizeZ - 1; z++)
                    {
                        if (gridMatrix[x, z] == 1) // EXCLUSIVAMENTE PASILLOS
                        {
                            Vector3 worldPos = new Vector3((x * 4.0f) - halfW + 2.0f, transform.position.y, (z * 4.0f) - halfD + 2.0f);
                            if (lastElevatorPos.x > -900f && Vector3.Distance(worldPos, lastElevatorPos) < 6.0f) continue;

                            float dist = Vector2Int.Distance(mapCorner, new Vector2Int(x, z));
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestCell = new Vector2Int(x, z);
                            }
                        }
                    }
                }
            }

            // Pasada 3 (Garantía Absoluta): Retornar cualquier celda de pasillo libre existente
            if (bestCell.x == -1)
            {
                for (int x = 1; x < sizeX - 1; x++)
                {
                    for (int z = 1; z < sizeZ - 1; z++)
                    {
                        if (gridMatrix[x, z] == 1) return new Vector2Int(x, z);
                    }
                }
            }

            return bestCell;
        }

        private void BuildEnemyBookHead(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            // 1. Buscar si ya existe un BookHead en la escena para no duplicar ni incrustar en paredes
            GameObject enemyObj = null;
            EnemyAIBookHead existingAI = FindObjectOfType<EnemyAIBookHead>(true);
            if (existingAI != null)
            {
                enemyObj = existingAI.gameObject;
            }
            else
            {
                GameObject bookHeadPrefab = null;
#if UNITY_EDITOR
                bookHeadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monsters/BookHeadMonster/URP/Animations/BookHeadMonster.prefab");
                if (bookHeadPrefab == null) bookHeadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monsters/BookHeadMonster/URP/Animations/BookHeadMonster_withBlood.prefab");
                if (bookHeadPrefab == null) bookHeadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Monsters/BookHeadMonster.prefab");
#endif
                if (bookHeadPrefab != null)
                {
                    // Instanciar DESACTIVADO para evitar que Start() se ejecute antes de InitializePatrol
                    enemyObj = Instantiate(bookHeadPrefab, new Vector3(0f, -100f, 0f), Quaternion.identity, parent);
                    enemyObj.name = "[Enemy_BookHead]";
                    enemyObj.SetActive(false);
                }
            }

            if (enemyObj == null) return;

            // 2. Recopilar posiciones de pasillo puro para patrullaje
            List<Vector3> patrolPositions = new List<Vector3>();
            Vector3 playerSpawnPos = new Vector3((1 * 4.0f) - halfW + 2.0f, transform.position.y, (1 * 4.0f) - halfD + 2.0f);

            for (int x = 1; x < sizeX - 1; x++)
            {
                for (int z = 1; z < sizeZ - 1; z++)
                {
                    if (gridMatrix[x, z] == 1)
                    {
                        bool nearRoom = (z + 1 < sizeZ && (gridMatrix[x, z + 1] == 2 || gridMatrix[x, z + 1] == 3)) ||
                                        (z - 1 >= 0 && (gridMatrix[x, z - 1] == 2 || gridMatrix[x, z - 1] == 3)) ||
                                        (x + 1 < sizeX && (gridMatrix[x + 1, z] == 2 || gridMatrix[x + 1, z] == 3)) ||
                                        (x - 1 >= 0 && (gridMatrix[x - 1, z] == 2 || gridMatrix[x - 1, z] == 3));
                        if (nearRoom) continue;

                        float worldX = (x * 4.0f) - halfW + 2.0f;
                        float worldZ = (z * 4.0f) - halfD + 2.0f;
                        Vector3 cellCenter = new Vector3(worldX, transform.position.y, worldZ);

                        float distToPlayer = Vector3.Distance(cellCenter, playerSpawnPos);
                        if (distToPlayer >= 6.0f && distToPlayer <= 20.0f)
                            patrolPositions.Add(cellCenter);
                    }
                }
            }

            // Fallback: cualquier celda de pasillo
            if (patrolPositions.Count == 0)
            {
                for (int x = 1; x < sizeX - 1; x++)
                    for (int z = 1; z < sizeZ - 1; z++)
                        if (gridMatrix[x, z] == 1)
                            patrolPositions.Add(new Vector3((x * 4.0f) - halfW + 2.0f, transform.position.y, (z * 4.0f) - halfD + 2.0f));
            }

            if (patrolPositions.Count == 0) return;

            // 3. Crear puntos de patrullaje en el mundo (posiciones raw, se ajustan al NavMesh en la corrutina)
            GameObject patrolHolder = new GameObject("[BookHead_Patrol_Points]");
            patrolHolder.transform.SetParent(parent);

            List<Transform> patrolPointTransforms = new List<Transform>();
            int targetPoints = Mathf.Min(8, patrolPositions.Count);
            for (int i = 0; i < targetPoints; i++)
            {
                int randomIndex = Random.Range(0, patrolPositions.Count);
                Vector3 ptPos = patrolPositions[randomIndex];
                patrolPositions.RemoveAt(randomIndex);

                GameObject ptObj = new GameObject($"PatrolPoint_{i + 1}");
                ptObj.transform.position = ptPos;
                ptObj.transform.SetParent(patrolHolder.transform);
                patrolPointTransforms.Add(ptObj.transform);
            }

            // 4. Lanzar corrutina diferida: activa al monstruo 3 frames después del NavMesh bake
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            Transform playerTransform = playerObj != null ? playerObj.transform : null;
            StartCoroutine(InitEnemyAfterNavMesh(enemyObj, patrolPointTransforms.ToArray(), playerTransform));

            Debug.Log($"ModularHospitalGenerator: BookHead inicialización diferida lanzada con {patrolPointTransforms.Count} puntos de patrullaje.");
        }

        private System.Collections.IEnumerator InitEnemyAfterNavMesh(GameObject enemyObj, Transform[] patrolPoints, Transform playerTransform)
        {
            // Esperar 3 frames para que NavMeshSurface.BuildNavMesh() quede compilado internamente
            yield return null;
            yield return null;
            yield return null;

            if (enemyObj == null) yield break;

            // NO activar aquí — el monstruo debe aparecer solo cuando la caja de fusibles se funda (PowerBox.TriggerPowerOutage)
            // Solo configuramos posición, NavMesh y patrullaje mientras está inactivo

            // Para configurar el NavMeshAgent necesitamos activarlo brevemente, luego desactivarlo
            enemyObj.SetActive(true);
            yield return null; // 1 frame para que Start() del agente se ejecute

            UnityEngine.AI.NavMeshAgent agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            EnemyAIBookHead ai = enemyObj.GetComponent<EnemyAIBookHead>();
            EnemyAIController aiController = enemyObj.GetComponent<EnemyAIController>();
            if (agent == null || (ai == null && aiController == null)) { enemyObj.SetActive(false); yield break; }

            // Configurar parámetros del agente correctamente (agentTypeID = 0 = Humanoid)
            agent.agentTypeID = 0;
            agent.height = 2.0f;
            agent.radius = 0.40f;
            agent.baseOffset = 0f;

            // Ajustar puntos de patrullaje al NavMesh real
            UnityEngine.AI.NavMeshHit navHit;
            Vector3 spawnPos = patrolPoints.Length > 0 ? patrolPoints[0].position : transform.position;

            foreach (Transform pt in patrolPoints)
            {
                if (pt == null) continue;
                if (UnityEngine.AI.NavMesh.SamplePosition(pt.position, out navHit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
                    pt.position = navHit.position;
            }

            // Buscar posición de spawn válida en el NavMesh
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out navHit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
                spawnPos = navHit.position;

            // Colocar y warpear el monstruo sobre el NavMesh
            agent.enabled = false;
            enemyObj.transform.position = spawnPos;
            enemyObj.transform.rotation = Quaternion.identity;
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(spawnPos);

            // Pre-cargar los puntos de patrullaje sin iniciar movimiento en cualquiera de los dos scripts
            if (ai != null) ai.PreloadPatrol(patrolPoints, playerTransform);
            if (aiController != null) aiController.SetPatrolPoints(patrolPoints);

            // DESACTIVAR: el monstruo se activará desde PowerBox cuando la caja de fusibles se funda
            enemyObj.SetActive(false);
            Debug.Log($"[BookHead] Listo en NavMesh: {spawnPos} | Dormido hasta el primer apagón. {patrolPoints.Length} puntos cargados.");
        }

        private Vector3 GetItemSpawnPosition(Vector3 roomWorldCenter)
        {
            // Raycast desde arriba hacia abajo para encontrar la superficie exacta de la mesa/mueble o del suelo
            RaycastHit hit;
            Vector3 rayStart = roomWorldCenter + Vector3.up * 2.5f;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 4.0f))
            {
                return hit.point + Vector3.up * 0.05f; // Elevar 5 cm sobre la superficie para evitar que el piso la tape
            }
            return new Vector3(roomWorldCenter.x, transform.position.y + 0.05f, roomWorldCenter.z);
        }

        private void BuildKeypadAndNotes(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            // 1. Generar la clave aleatoria de 7 dígitos
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < 7; i++)
            {
                sb.Append(Random.Range(0, 10));
            }
            correctKeypadCode = sb.ToString();

            // 2. Buscar la Oficina del Director y configurar su KeypadController con BoxCollider
            ProceduralDoorInteract directorDoor = null;
            Transform keypadObj = null;

            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                if (mod.moduleType == ModuleType.DirectorOffice)
                {
                    directorDoor = mod.GetComponentInChildren<ProceduralDoorInteract>(true);
                    
                    Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allT)
                    {
                        if (t != null && t.name.ToLower().Contains("keypad"))
                        {
                            keypadObj = t;
                            break;
                        }
                    }
                }
            }

            if (keypadObj == null)
            {
                KeypadController existingKp = FindObjectOfType<KeypadController>();
                if (existingKp != null) keypadObj = existingKp.transform;
            }

            if (keypadObj != null)
            {
                KeypadController keypad = keypadObj.GetComponent<KeypadController>();
                if (keypad == null) keypad = keypadObj.gameObject.AddComponent<KeypadController>();

                // Asegurar BoxCollider centrado y en modo Trigger (Trigger = cero bloqueo físico al jugador)
                BoxCollider box = keypadObj.GetComponent<BoxCollider>();
                if (box == null) box = keypadObj.gameObject.AddComponent<BoxCollider>();

                box.center = Vector3.zero;
                box.size = new Vector3(0.5f, 0.6f, 0.25f);
                box.isTrigger = true; // TRIGGER = 100% IMPOSIBLE DE BLOQUEAR EL PASO FÍSICO DEL JUGADOR

                keypad.correctCode = correctKeypadCode;
                keypad.interactDistance = 2.5f;
                keypad.targetProceduralDoor = directorDoor;

                Debug.Log($"ModularHospitalGenerator: Keypad configurado con BoxCollider Trigger centrado, clave '{correctKeypadCode}' y vinculado a la puerta del Director.");
            }

            // 2.5 Configurar el cajón interactivo del escritorio del Director y ocultar la tarjeta de acceso dentro
            SetupDirectorOfficeDesk();

            // 3. Spawning y Activación de las EXACTAS 7 Notas del Código (Habitaciones y Paredes del Pasillo)
            if (notePrefab == null)
            {
#if UNITY_EDITOR
                notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Note.prefab");
                if (notePrefab == null) notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Papel.prefab");
                if (notePrefab == null) notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Note.prefab");
#endif
            }

            // 3. Spawning aleatorio de las 7 Notas del Código
            // Destruir cualquier componente NoteItem residual pre-existente en los prefabs antes de la asignación
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                NoteItem[] oldNotes = mod.GetComponentsInChildren<NoteItem>(true);
                foreach (NoteItem oldN in oldNotes)
                {
                    if (oldN != null) DestroyImmediate(oldN);
                }

                if (mod.moduleType == ModuleType.DirectorOffice) continue;
                Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allT)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if ((cName.StartsWith("papel") || cName.StartsWith("p_note") || cName.StartsWith("note")) && !cName.Contains("canvas") && !cName.Contains("ui"))
                    {
                        if (t.parent == null || !t.parent.name.ToLower().Contains("papel"))
                        {
                            t.gameObject.SetActive(false);
                        }
                    }
                }
            }

            int assignedNotes = 0;
            List<HospitalModule> smallRoomsForNotes = new List<HospitalModule>();
            foreach (HospitalModule mod in placedModules)
            {
                if (mod != null && mod.moduleType == ModuleType.SmallRoom && mod.moduleType != ModuleType.DirectorOffice)
                {
                    // Verificar que la entrada de la habitación esté 100% abierta a un pasillo (no bloqueada por otra habitación)
                    bool hasBlockingWall = HasBlockingGeometryAtRoomEntrance(mod);
                    if (!hasBlockingWall)
                    {
                        smallRoomsForNotes.Add(mod);
                    }
                }
            }

            // 3.1 Asignar 1 Nota por habitación abierta en hasta 4 habitaciones distintas
            while (smallRoomsForNotes.Count > 0 && assignedNotes < 4 && assignedNotes < 7)
            {
                int rIdx = Random.Range(0, smallRoomsForNotes.Count);
                HospitalModule chosenRoom = smallRoomsForNotes[rIdx];
                smallRoomsForNotes.RemoveAt(rIdx);

                GameObject targetPaper = null;
                Transform[] roomTransforms = chosenRoom.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in roomTransforms)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if ((cName.StartsWith("papel") || cName.StartsWith("p_note") || cName.StartsWith("note")) && !cName.Contains("canvas") && !cName.Contains("ui"))
                    {
                        if (t.parent == null || !t.parent.name.ToLower().Contains("papel"))
                        {
                            targetPaper = t.gameObject;
                            break;
                        }
                    }
                }

                if (targetPaper != null)
                {
                    ActivateItemFully(targetPaper);
                    targetPaper.name = $"[Hospital_Note_Digit_{assignedNotes + 1}]";

                    // Encontrar el objeto hijo real de la nota que tiene la malla (MeshRenderer)
                    GameObject meshChildObj = targetPaper;
                    MeshRenderer mr = targetPaper.GetComponentInChildren<MeshRenderer>(true);
                    if (mr != null) meshChildObj = mr.gameObject;

                    BoxCollider bc = meshChildObj.GetComponent<BoxCollider>();
                    if (bc == null) bc = meshChildObj.AddComponent<BoxCollider>();
                    Vector3 lossy = meshChildObj.transform.lossyScale;
                    bc.center = Vector3.zero;
                    bc.size = new Vector3(
                        lossy.x > 0.001f ? 0.35f / lossy.x : 0.35f,
                        lossy.y > 0.001f ? 0.25f / lossy.y : 0.25f,
                        lossy.z > 0.001f ? 0.35f / lossy.z : 0.35f
                    );

                    NoteItem oldN = meshChildObj.GetComponent<NoteItem>();
                    if (oldN != null) DestroyImmediate(oldN);

                    NoteItem nComp = meshChildObj.AddComponent<NoteItem>();
                    nComp.digitPosition = assignedNotes + 1;
                    nComp.digitValue = int.Parse(correctKeypadCode[assignedNotes].ToString());
                    nComp.interactDistance = 2.5f;

                    Debug.Log($"[Note] Asignada en habitación en objeto malla '{meshChildObj.name}': Posición {nComp.digitPosition} = Valor '{nComp.digitValue}'");
                }
                else if (notePrefab != null)
                {
                    Vector3 spawnPos = GetItemSpawnPosition(chosenRoom.transform.position);
                    GameObject noteObj = Instantiate(notePrefab, spawnPos, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), parent);
                    noteObj.name = $"[Hospital_Note_Digit_{assignedNotes + 1}]";

                    // Alinear a ras de suelo o superficie usando un raycast descendente seguro
                    RaycastHit noteHit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 1.0f, Vector3.down, out noteHit, 3.0f))
                    {
                        noteObj.transform.position = noteHit.point + Vector3.up * 0.012f; // Z-fighting safe offset
                    }
                    else
                    {
                        noteObj.transform.position = new Vector3(spawnPos.x, transform.position.y + 0.015f, spawnPos.z);
                    }

                    NoteItem oldN = noteObj.GetComponent<NoteItem>();
                    if (oldN != null) DestroyImmediate(oldN);
                    NoteItem nComp = noteObj.AddComponent<NoteItem>();
                    nComp.digitPosition = assignedNotes + 1;
                    nComp.digitValue = int.Parse(correctKeypadCode[assignedNotes].ToString());
                    nComp.interactDistance = 3.2f;
                }

                assignedNotes++;
            }

            // 3. Montar las notas restantes (hasta completar las 7) EN LAS PAREDES DE LOS PASILLOS a la altura de los ojos (Y = 1.3m)
            if (assignedNotes < 7 && notePrefab != null)
            {
                List<Vector3> corridorCenters = new List<Vector3>();
                for (int x = 1; x < sizeX - 1; x++)
                {
                    for (int z = 1; z < sizeZ - 1; z++)
                    {
                        if (gridMatrix[x, z] == 1) // Pasillo
                        {
                            float cx = (x * 4.0f) - halfW + 2.0f;
                            float cz = (z * 4.0f) - halfD + 2.0f;
                            Vector3 cPos = new Vector3(cx, transform.position.y, cz);

                            if (lastElevatorPos.x > -900f && Vector3.Distance(cPos, lastElevatorPos) < 6.0f) continue;

                            corridorCenters.Add(new Vector3(cx, transform.position.y + 1.3f, cz));
                        }
                    }
                }

                Vector3[] dirs = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

                while (assignedNotes < 7)
                {
                    int idx = (corridorCenters.Count > 0) ? Random.Range(0, corridorCenters.Count) : 0;
                    Vector3 baseCenter = (corridorCenters.Count > 0) ? corridorCenters[idx] : new Vector3(assignedNotes * 2.0f, transform.position.y + 1.3f, 0f);
                    if (corridorCenters.Count > 0) corridorCenters.RemoveAt(idx);

                    bool foundWall = false;
                    Vector3 wallPos = baseCenter;
                    Quaternion wallRot = Quaternion.identity;

                    float minDistance = 999f;
                    foreach (Vector3 d in dirs)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(baseCenter, d, out hit, 3.5f))
                        {
                            if (hit.collider != null)
                            {
                                string n = hit.collider.name.ToLower();
                                string parentName = hit.collider.transform.parent != null ? hit.collider.transform.parent.name.ToLower() : "";
                                string rootName = hit.collider.transform.root != null ? hit.collider.transform.root.name.ToLower() : "";

                                bool isNonWallProp = n.Contains("fuse") || n.Contains("box") || n.Contains("caja") || n.Contains("elevator") || n.Contains("ascensor") ||
                                                     n.Contains("generator") || n.Contains("gen") || n.Contains("subgenerator") || n.Contains("lamp") || n.Contains("luz") || n.Contains("door") ||
                                                     n.Contains("puerta") || n.Contains("marco") || n.Contains("frame") || n.Contains("window") || n.Contains("glass") ||
                                                     n.Contains("hinge") || n.Contains("connector") || n.Contains("player") || n.Contains("pillar") || n.Contains("columna") || n.Contains("machinery") || n.Contains("motor") || n.Contains("pipe") || n.Contains("tubo") ||
                                                     parentName.Contains("fuse") || parentName.Contains("elevator") || parentName.Contains("gen") || parentName.Contains("door") || parentName.Contains("box") || parentName.Contains("generator") || parentName.Contains("subgenerator") ||
                                                     rootName.Contains("generator") || rootName.Contains("subgenerator") || rootName.Contains("fusebox") || rootName.Contains("elevator") || rootName.Contains("ascensor") || rootName.Contains("lamp");

                                if (isNonWallProp) continue;

                                if (hit.distance < minDistance)
                                {
                                    minDistance = hit.distance;
                                    wallPos = hit.point + hit.normal * 0.035f;
                                    wallRot = Quaternion.LookRotation(hit.normal, Vector3.up);
                                    foundWall = true;
                                }
                            }
                        }
                    }

                    if (!foundWall)
                    {
                        Vector3 floorPos = GetItemSpawnPosition(baseCenter);
                        bool tooClose = false;
                        foreach (Vector3 existingP in spawnedItemPositions)
                        {
                            if (Vector3.Distance(floorPos, existingP) < 4.5f)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;

                        wallPos = floorPos;
                        wallRot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                    }

                    spawnedItemPositions.Add(wallPos);

                    GameObject noteObj = Instantiate(notePrefab, wallPos, wallRot, parent);
                    noteObj.name = $"[Hospital_Note_Digit_{assignedNotes + 1}]";

                    if (!foundWall)
                    {
                        // Alineación perfecta sobre el suelo del pasillo
                        RaycastHit noteHit;
                        if (Physics.Raycast(wallPos + Vector3.up * 1.0f, Vector3.down, out noteHit, 4.0f))
                        {
                            noteObj.transform.position = noteHit.point + Vector3.up * 0.015f; // Un pequeño offset vertical para evitar Z-fighting
                            noteObj.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f); // Acostada plana en el suelo
                        }
                        else
                        {
                            // Fallback seguro: colocar a ras de suelo del hospital
                            noteObj.transform.position = new Vector3(wallPos.x, transform.position.y + 0.015f, wallPos.z);
                            noteObj.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                        }
                    }
                    else
                    {
                        // Pegar plana contra la pared vertical a altura fija de los ojos del jugador (evita que flote a Y = 4.1)
                        noteObj.transform.position = wallPos;
                        noteObj.transform.rotation = wallRot; // Mirando hacia el pasillo perpendicularmente
                    }

                    NoteItem oldN2 = noteObj.GetComponent<NoteItem>();
                    if (oldN2 != null) DestroyImmediate(oldN2);
                    NoteItem nComp = noteObj.AddComponent<NoteItem>();

                    nComp.digitPosition = assignedNotes + 1;
                    nComp.digitValue = int.Parse(correctKeypadCode[assignedNotes].ToString());
                    nComp.interactDistance = 3.2f;

                    assignedNotes++;
                }
            }

            // 4. Montar exactamente 3 Notas de Historia (Lore) en el Hospital
            if (notePrefab != null)
            {
                // Recopilar todas las habitaciones del hospital (excepto DirectorOffice y Elevator)
                List<HospitalModule> allRooms = new List<HospitalModule>();
                foreach (HospitalModule mod in placedModules)
                {
                    if (mod != null && (mod.moduleType == ModuleType.SmallRoom || mod.moduleType == ModuleType.LargeRoom || mod.moduleType == ModuleType.OfficeRoom))
                    {
                        allRooms.Add(mod);
                    }
                }

                // Mezclar la lista de habitaciones para aleatorizar
                for (int j = allRooms.Count - 1; j > 0; j--)
                {
                    int r = Random.Range(0, j + 1);
                    HospitalModule tmp = allRooms[j];
                    allRooms[j] = allRooms[r];
                    allRooms[r] = tmp;
                }

                // Textos de lore
                string[] loreTitles = new string[]
                {
                    "Diario del Bibliotecario (BookHead)",
                    "Informe de Psiquiatría (TheCreep)",
                    "Memorándum de Evacuación"
                };
                string[] loreBodies = new string[]
                {
                    "<b>REGISTRO DEL DIARIO - 18 DE OCTUBRE:</b>\n\n" +
                    "Ese maldito monstruo... la criatura con cabeza de libro que merodea la biblioteca principal.\n" +
                    "Confirmado: <i>NO TIENE OJOS</i>. Es completamente ciego.\n" +
                    "Sin embargo, su oído es increíblemente agudo.\n" +
                    "Si caminas despacio, te ignorará por completo. Pero si entras en pánico y corres <b>(sprint)</b>,\n" +
                    "sabrá exactamente dónde estás al instante y te perseguirá.\n" +
                    "Guarda silencio si quieres conservar la cabeza.",

                    "<b>EXPEDIENTE ANÓMALO #09-B:</b>\n\n" +
                    "Los pacientes del Pabellón Este reportan avistamientos de un ser deforme en el suelo.\n" +
                    "Se arrastra como un insecto y lo llaman 'TheCreep' (El Rastrero).\n" +
                    "El personal reporta que prefiere quedarse en las esquinas más oscuras del hospital.\n" +
                    "Es extremadamente agresivo. Si te encuentra, intentará acorralarte y atacarte.\n" +
                    "Para escapar de él, debes correr hacia el spawn o buscar zonas iluminadas.\n" +
                    "Nunca te quedes quieto en los callejones oscuros.",

                    "<b>ORDEN DE EVACUACIÓN INTERNA:</b>\n\n" +
                    "A todo el personal administrativo:\n" +
                    "La fuga biológica ha alcanzado los niveles subterráneos del ala oeste.\n" +
                    "El ascensor de escape principal de la oficina del director ha sido bloqueado por el protocolo de cuarentena.\n" +
                    "Se requiere una contraseña cifrada de 7 dígitos para restablecerlo.\n" +
                    "Las hojas de códigos de seguridad se han esparcido por las habitaciones para evitar que los sujetos de prueba las encuentren.\n" +
                    "Busca los 7 dígitos y evacua inmediatamente."
                };

                int loreSpawned = 0;
                int roomIndex = 0;

                while (loreSpawned < 3 && roomIndex < allRooms.Count)
                {
                    HospitalModule chosenRoom = allRooms[roomIndex];
                    roomIndex++;

                    // Calcular posición dentro de la habitación con un offset aleatorio para no caer en el centro exacto
                    Vector3 roomCenter = chosenRoom.transform.position;
                    Vector3 offset = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                    Vector3 spawnPos = roomCenter + offset;

                    // Verificar que no esté demasiado cerca de otra nota ya colocada
                    bool tooClose = false;
                    foreach (Vector3 existingP in spawnedItemPositions)
                    {
                        if (Vector3.Distance(spawnPos, existingP) < 3.5f)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    // Raycast para encontrar el suelo exacto
                    Vector3 finalPos;
                    RaycastHit hit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 2.0f, Vector3.down, out hit, 5.0f))
                    {
                        finalPos = hit.point + Vector3.up * 0.015f;
                    }
                    else
                    {
                        finalPos = new Vector3(spawnPos.x, transform.position.y + 0.015f, spawnPos.z);
                    }

                    GameObject loreObj = Instantiate(notePrefab, finalPos, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), parent);
                    loreObj.name = $"[Hospital_Lore_Note_{loreSpawned + 1}]";

                    spawnedItemPositions.Add(finalPos);

                    // Remover NoteItem del prefab original y reemplazar con LoreNoteItem
                    NoteItem oldNoteComp = loreObj.GetComponent<NoteItem>();
                    if (oldNoteComp != null) DestroyImmediate(oldNoteComp);

                    // Corregir el BoxCollider: isTrigger = false para que el raycast del jugador lo detecte
                    BoxCollider box = loreObj.GetComponent<BoxCollider>();
                    if (box != null) box.isTrigger = false;

                    LoreNoteItem loreComp = loreObj.AddComponent<LoreNoteItem>();
                    loreComp.loreId = loreSpawned + 1;
                    loreComp.noteTitle = loreTitles[loreSpawned];
                    loreComp.noteBody = loreBodies[loreSpawned];

                    loreSpawned++;
                }
            }
        }

        private void SetupDirectorOfficeDesk()
        {
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null || mod.moduleType != ModuleType.DirectorOffice) continue;

                Transform deskTrans = null;
                Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allT)
                {
                    if (t != null && (t.name.ToLower().Contains("desk") || t.name.ToLower().Contains("escritorio")))
                    {
                        deskTrans = t;
                        break;
                    }
                }

                if (deskTrans == null)
                {
                    Debug.LogWarning("SetupDirectorOfficeDesk: No se encontró escritorio en la Oficina del Director.");
                    continue;
                }

                // Buscar el hijo del cajón (ej. Desk01.003)
                Transform drawerTrans = null;
                Transform[] deskChildren = deskTrans.GetComponentsInChildren<Transform>(true);
                foreach (Transform dt in deskChildren)
                {
                    if (dt != deskTrans && dt.name.ToLower().Contains("desk01"))
                    {
                        drawerTrans = dt;
                        break;
                    }
                }

                if (drawerTrans == null) drawerTrans = deskTrans;

                // Agregar o recuperar el componente DrawerInteract
                DrawerInteract drawerScript = drawerTrans.GetComponent<DrawerInteract>();
                if (drawerScript == null) drawerScript = drawerTrans.gameObject.AddComponent<DrawerInteract>();

                // Asegurar un BoxCollider Trigger de detección
                BoxCollider dBox = drawerTrans.GetComponent<BoxCollider>();
                if (dBox == null) dBox = drawerTrans.gameObject.AddComponent<BoxCollider>();
                dBox.isTrigger = true;
                dBox.size = new Vector3(0.9f, 0.45f, 0.6f);
                dBox.center = new Vector3(0f, 0f, 0.1f);

                drawerScript.interactDistance = 2.0f;
                drawerScript.slideDistance = 0.24f;

                // ─── USAR EL ACCESSCARD ORIGINAL DEL PREFAB ────────────────────────────
                // 1. Buscar el AccessCard que ya existe como hijo del módulo (tiene mesh y material correcto)
                GameObject cardObj = null;
                bool isOriginalPrefabCard = false;
                Transform[] allModT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allModT)
                {
                    string n = t.name.ToLower();
                    if (n.Contains("accesscard") || n.Contains("access_card") || n.Contains("keycard") || n.Contains("tarjeta"))
                    {
                        cardObj = t.gameObject;
                        isOriginalPrefabCard = true;
                        break;
                    }
                }

                // 2. Si no encontramos ninguno, crear un cubo primitivo de fallback
                if (cardObj == null)
                {
                    cardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cardObj.name = "AccessCard_Director";
                    Material keycardMat = Resources.Load<Material>("Mat_ElevatorKeycard_Horror");
                    if (keycardMat == null) keycardMat = Resources.Load<Material>("Mat_Keycard");
                    Renderer r = cardObj.GetComponent<Renderer>();
                    if (r != null && keycardMat != null) r.material = keycardMat;
                    Collider primCol = cardObj.GetComponent<Collider>();
                    if (primCol != null) { if (Application.isPlaying) Destroy(primCol); else DestroyImmediate(primCol); }
                    // Solo para el fallback definimos tamaño de tarjeta estándar
                    cardObj.transform.localScale = new Vector3(0.22f, 0.02f, 0.32f);
                }

                // 3. Reparentar al cajón y posicionar dentro (respetar escala y rotación original del prefab)
                cardObj.transform.SetParent(drawerTrans);
                cardObj.transform.localPosition = new Vector3(0f, -0.03f, -0.15f);
                // Si es el prefab original: respetar su rotación (ya tiene X=90 para quedar plano)
                // Si es el fallback cubo: rotación neutra
                if (!isOriginalPrefabCard)
                    cardObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

                // 4. Eliminar colisionadores viejos y crear uno trigger limpio basado en el mesh real
                Collider[] oldCols = cardObj.GetComponents<Collider>();
                foreach (Collider c in oldCols) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
                BoxCollider cardBox = cardObj.AddComponent<BoxCollider>();
                cardBox.isTrigger = true;
                cardBox.center = Vector3.zero;
                // Ajustar el collider al tamaño real del mesh: si es prefab original usamos sus bounds normalizados
                if (isOriginalPrefabCard)
                {
                    Renderer cardRend = cardObj.GetComponentInChildren<Renderer>();
                    if (cardRend != null)
                    {
                        // Tamaño del collider en espacio local = bounds.size / localScale
                        Vector3 s = cardObj.transform.localScale;
                        Vector3 bSize = cardRend.bounds.size;
                        cardBox.size = new Vector3(
                            s.x > 0 ? bSize.x / s.x : 1f,
                            s.y > 0 ? bSize.y / s.y : 1f,
                            s.z > 0 ? bSize.z / s.z : 1f
                        );
                    }
                    else
                    {
                        cardBox.size = Vector3.one;
                    }
                }
                else
                {
                    cardBox.size = Vector3.one; // Para el cubo primitivo, localScale ya lo hace del tamaño correcto
                }

                // 5. Agregar KeycardItem si no tiene
                KeycardItem cardComp = cardObj.GetComponent<KeycardItem>();
                if (cardComp == null) cardComp = cardObj.AddComponent<KeycardItem>();
                cardComp.interactDistance = 4.0f;

                // 6. Asignar al cajón y ocultar hasta que se abra
                drawerScript.keycardInside = cardObj;
                cardObj.SetActive(false);

                Debug.Log($"ModularHospitalGenerator: AccessCard '{cardObj.name}' configurada en cajón. EsPrefabOriginal={isOriginalPrefabCard}");
            }
        }

        private void BuildBatteries(Transform parent, int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (batteryPrefab == null)
            {
#if UNITY_EDITOR
                batteryPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Battery.prefab");
                if (batteryPrefab == null) batteryPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Battery.prefab");
                if (batteryPrefab == null) batteryPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Batery.prefab");
#endif
            }

            string diff = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
            int batteriesNeeded = 12; // 12 baterías en Normal por defecto
            if (diff == "FACIL") batteriesNeeded = 16;
            else if (diff == "DIFICIL") batteriesNeeded = 8;

            // 1. Spawning de Baterías abundantes utilizando objetos de habitación e instanciación procedural
            List<GameObject> prePlacedBatteries = new List<GameObject>();
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null || mod.moduleType == ModuleType.DirectorOffice) continue;
                Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allT)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if ((cName.Contains("battery") || cName.Contains("batery") || cName.Contains("pila")) && !cName.Contains("canvas") && !cName.Contains("ui"))
                    {
                        t.gameObject.SetActive(false); // Apagar por defecto
                        if (!prePlacedBatteries.Contains(t.gameObject))
                        {
                            prePlacedBatteries.Add(t.gameObject);
                        }
                    }
                }
            }

            int activatedBats = 0;
            List<HospitalModule> usedRoomsForBat = new List<HospitalModule>();

            while (prePlacedBatteries.Count > 0 && activatedBats < batteriesNeeded)
            {
                int rIdx = Random.Range(0, prePlacedBatteries.Count);
                GameObject chosenBat = prePlacedBatteries[rIdx];
                prePlacedBatteries.RemoveAt(rIdx);

                HospitalModule parentMod = chosenBat.GetComponentInParent<HospitalModule>();
                if (parentMod != null && (usedRoomsForBat.Contains(parentMod) || parentMod.moduleType == ModuleType.Elevator))
                {
                    continue; // Máximo 1 batería por habitación y cero en elevador
                }

                // VERIFICACIÓN ESTRICTA ANTI-PARED: Si la batería está atrapada dentro de un bloque macizo (gridMatrix == 0) o a menos de 0.8m de un colisionador sólido, descartar
                Vector2Int bCell = PositionToGridCell(chosenBat.transform.position);
                if (bCell.x >= 0 && bCell.x < sizeX && bCell.y >= 0 && bCell.y < sizeZ)
                {
                    if (gridMatrix[bCell.x, bCell.y] == 0) continue; // Atrapada en pared maciza
                }

                // Verificar colisión directa por OverlapSphere con bloques macizos de pared o pilares
                Collider[] hits = Physics.OverlapSphere(chosenBat.transform.position, 0.45f);
                bool trappedInWall = false;
                foreach (Collider c in hits)
                {
                    if (c == null) continue;
                    string cn = c.name.ToLower();
                    if (cn.Contains("solid_wall") || cn.Contains("pillar") || cn.Contains("outer_wall"))
                    {
                        trappedInWall = true;
                        break;
                    }
                }
                if (trappedInWall) continue;

                if (lastElevatorPos.x > -900f && Vector3.Distance(chosenBat.transform.position, lastElevatorPos) < 6.0f) continue;

                if (parentMod != null) usedRoomsForBat.Add(parentMod);

                chosenBat.SetActive(true); // Activar batería tal como está en el prefab
                chosenBat.name = $"[Hospital_Battery_{activatedBats + 1}]";

                // Activar la batería y asegurar que todos sus renderers e hijos sean visibles
                ActivateItemFully(chosenBat);

                RaycastHit batPreHit;
                if (Physics.Raycast(chosenBat.transform.position + Vector3.up * 1.0f, Vector3.down, out batPreHit, 2.5f))
                {
                    chosenBat.transform.position = batPreHit.point;
                    MeshRenderer mrPreB = chosenBat.GetComponentInChildren<MeshRenderer>();
                    if (mrPreB != null)
                    {
                        float bottomY = mrPreB.bounds.min.y;
                        float diffY = batPreHit.point.y - bottomY;
                        chosenBat.transform.position += new Vector3(0, diffY, 0);
                    }
                }

                // Asegurar BoxCollider trigger para que sea interactable
                BoxCollider bcBat = chosenBat.GetComponent<BoxCollider>();
                if (bcBat == null) bcBat = chosenBat.AddComponent<BoxCollider>();
                bcBat.isTrigger = true;
                BatteryItem bComp = chosenBat.GetComponent<BatteryItem>();
                if (bComp == null) bComp = chosenBat.AddComponent<BatteryItem>();

                bComp.interactDistance = 3.2f;
                bComp.rechargeAmount = 80f;
                activatedBats++;
            }

            // Si faltan baterías para completar la cantidad necesaria (6 a 8), instanciar dinámicamente en el mapa
            if (activatedBats < batteriesNeeded && batteryPrefab != null)
            {
                List<HospitalModule> availableRoomsForBat = new List<HospitalModule>();
                foreach (HospitalModule mod in placedModules)
                {
                    if (mod != null && mod.moduleType == ModuleType.SmallRoom && !usedRoomsForBat.Contains(mod))
                    {
                        availableRoomsForBat.Add(mod);
                    }
                }

                for (int i = activatedBats; i < batteriesNeeded; i++)
                {
                    Vector3 spawnPos;
                    if (availableRoomsForBat.Count > 0 && Random.value < 0.5f)
                    {
                        int rIdx = Random.Range(0, availableRoomsForBat.Count);
                        HospitalModule chosenRoom = availableRoomsForBat[rIdx];
                        availableRoomsForBat.RemoveAt(rIdx);
                        spawnPos = GetItemSpawnPosition(chosenRoom.transform.position);
                    }
                    else
                    {
                        // Spawning directo sobre el suelo de pasillos principales
                        List<Vector3> corridorSpawns = new List<Vector3>();
                        for (int cx = 2; cx < sizeX - 2; cx++)
                        {
                            for (int cz = 2; cz < sizeZ - 2; cz++)
                            {
                                if (gridMatrix[cx, cz] == 1) // Pasillo
                                {
                                    float worldX = (cx * 4.0f) - halfW + 2.0f;
                                    float worldZ = (cz * 4.0f) - halfD + 2.0f;
                                    corridorSpawns.Add(new Vector3(worldX, transform.position.y, worldZ));
                                }
                            }
                        }
                        if (corridorSpawns.Count > 0)
                        {
                            Vector3 baseC = corridorSpawns[Random.Range(0, corridorSpawns.Count)];
                            spawnPos = baseC + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                        }
                        else
                        {
                            spawnPos = GetItemSpawnPosition(transform.position + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f)));
                        }
                    }

                    if (lastElevatorPos.x > -900f && Vector3.Distance(spawnPos, lastElevatorPos) < 6.0f) continue;

                    GameObject bObj = Instantiate(batteryPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360), 0), parent);
                    bObj.name = $"[Hospital_Battery_{i + 1}]";

                    RaycastHit batHit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out batHit, 3.0f))
                    {
                        bObj.transform.position = batHit.point;
                        MeshRenderer mrB = bObj.GetComponentInChildren<MeshRenderer>();
                        if (mrB != null)
                        {
                            float bottomY = mrB.bounds.min.y;
                            float diffY = batHit.point.y - bottomY;
                            bObj.transform.position += new Vector3(0, diffY, 0);
                        }
                    }
                    else
                    {
                        MeshRenderer mrBat = bObj.GetComponentInChildren<MeshRenderer>();
                        if (mrBat != null)
                        {
                            float meshMinY = mrBat.bounds.min.y;
                            float surfaceY = spawnPos.y;
                            float correction = surfaceY - meshMinY;
                            bObj.transform.position += new Vector3(0, correction + 0.002f, 0);
                        }
                    }

                    BatteryItem bComp = bObj.GetComponent<BatteryItem>();
                    if (bComp == null) bComp = bObj.AddComponent<BatteryItem>();
                    bComp.interactDistance = 3.2f;
                    bComp.rechargeAmount = 80f;
                }
            }
        }

        private bool TryGetRoomDoorwayData(HospitalModule mod, out Vector3 threshold, out Vector3 outwardDir)
        {
            threshold = mod != null ? mod.transform.position + Vector3.up * 1.25f : Vector3.zero;
            outwardDir = mod != null ? -mod.transform.forward : Vector3.forward;

            if (mod == null) return false;

            ModuleConnector bestConnector = null;
            foreach (ModuleConnector connector in mod.connectors)
            {
                if (connector == null) continue;
                if (connector.direction == ConnectorDirection.South)
                {
                    bestConnector = connector;
                    break;
                }
                if (bestConnector == null) bestConnector = connector;
            }

            if (bestConnector == null)
            {
                bestConnector = mod.GetComponentInChildren<ModuleConnector>(true);
            }

            if (bestConnector != null)
            {
                Vector3 connectorPos = bestConnector.transform.position;
                Vector3 rawDir = connectorPos - mod.transform.position;
                rawDir.y = 0f;
                if (rawDir.sqrMagnitude > 0.01f)
                {
                    outwardDir = rawDir.normalized;
                    threshold = connectorPos + Vector3.up * 1.25f;
                    return true;
                }
            }

            outwardDir = (-mod.transform.forward).normalized;
            threshold = mod.transform.position + Vector3.up * 1.25f + outwardDir * 1.8f;
            return false;
        }

        private bool IsBlockingRoomGeometry(Collider col)
        {
            if (col == null) return false;

            string cName = col.gameObject.name.ToLower();
            string rName = col.transform.root.name.ToLower();

            bool isProtected = rName.Contains("player") || rName.Contains("lamp") ||
                               cName.Contains("unified_floor") || cName.Contains("unified_ceiling") ||
                               cName.Contains("door") || cName.Contains("puerta") ||
                               cName.Contains("hinge") || cName.Contains("connector") ||
                               cName.Contains("bed") || cName.Contains("cama") ||
                               cName.Contains("desk") || cName.Contains("mueble") ||
                               cName.Contains("table") || cName.Contains("chair") ||
                               cName.Contains("keypad") || cName.Contains("button") ||
                               cName.Contains("screen") || cName.Contains("light") ||
                               cName.Contains("marco") || cName.Contains("frame");

            if (isProtected) return false;

            return cName.Contains("wall") || cName.Contains("pared") ||
                   cName.Contains("solid") || cName.Contains("pillar") ||
                   cName.Contains("bloque") || cName.Contains("column") ||
                   cName.Contains("cube") || cName.Contains("blocking");
        }

        private bool HasBlockingGeometryAtRoomEntrance(HospitalModule mod)
        {
            if (mod == null) return false;

            Vector3 threshold;
            Vector3 outwardDir;
            TryGetRoomDoorwayData(mod, out threshold, out outwardDir);

            Vector3[] probePoints = new Vector3[]
            {
                threshold - outwardDir * 0.55f,
                threshold,
                threshold + outwardDir * 0.85f
            };

            foreach (Vector3 point in probePoints)
            {
                Collider[] cols = Physics.OverlapSphere(point, 0.7f);
                foreach (Collider col in cols)
                {
                    if (IsBlockingRoomGeometry(col)) return true;
                }
            }

            return false;
        }

        private int ClearBlockingGeometryAtRoomEntrance(HospitalModule mod)
        {
            if (mod == null) return 0;

            Vector3 threshold;
            Vector3 outwardDir;
            TryGetRoomDoorwayData(mod, out threshold, out outwardDir);
            Vector3 inwardDir = -outwardDir;

            Vector3[] probePoints = new Vector3[]
            {
                threshold + inwardDir * 0.55f,
                threshold,
                threshold + outwardDir * 0.9f
            };

            int removed = 0;
            foreach (Vector3 point in probePoints)
            {
                Collider[] cols = Physics.OverlapSphere(point, 0.9f);
                foreach (Collider col in cols)
                {
                    if (!IsBlockingRoomGeometry(col)) continue;

                    col.enabled = false;
                    if (col.gameObject.activeSelf)
                    {
                        col.gameObject.SetActive(false);
                        removed++;
                    }
                }
            }

            return removed;
        }

        private void CleanInternalRoomBlockingWalls(Transform parent)
        {
            int removedCount = 0;
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                bool isRoom = mod.moduleType == ModuleType.SmallRoom || mod.moduleType == ModuleType.DirectorOffice || mod.moduleType == ModuleType.LargeRoom || mod.moduleType == ModuleType.OfficeRoom;
                if (!isRoom) continue;

                List<Vector3> scanPoints = new List<Vector3>();
                Vector3 roomCenter = mod.transform.position + Vector3.up * 1.25f;
                scanPoints.Add(roomCenter);

                // Agregar cuadrantes interiores completos de escaneo (3.0m x 3.0m de la habitación)
                scanPoints.Add(roomCenter + mod.transform.forward * 1.2f);
                scanPoints.Add(roomCenter - mod.transform.forward * 1.2f);
                scanPoints.Add(roomCenter + mod.transform.right * 1.2f);
                scanPoints.Add(roomCenter - mod.transform.right * 1.2f);
                scanPoints.Add(roomCenter + (mod.transform.forward + mod.transform.right).normalized * 1.3f);
                scanPoints.Add(roomCenter + (mod.transform.forward - mod.transform.right).normalized * 1.3f);
                scanPoints.Add(roomCenter + (-mod.transform.forward + mod.transform.right).normalized * 1.3f);
                scanPoints.Add(roomCenter + (-mod.transform.forward - mod.transform.right).normalized * 1.3f);

                foreach (Vector3 p in scanPoints)
                {
                    float radius = (mod.moduleType == ModuleType.DirectorOffice || mod.moduleType == ModuleType.LargeRoom) ? 1.95f : 1.65f;
                    Collider[] innerCols = Physics.OverlapSphere(p, radius);
                    foreach (Collider col in innerCols)
                    {
                        if (col == null) continue;
                        if (col.transform.IsChildOf(mod.transform)) continue; // Conservar paredes propias de la habitación
                        if (!IsBlockingRoomGeometry(col)) continue;

                        // PROTECCIÓN ABSOLUTA: Jamás desactivar o borrar paredes que pertenezcan a OTRA habitación
                        bool belongsToOtherRoom = false;
                        foreach (HospitalModule otherMod in placedModules)
                        {
                            if (otherMod != null && otherMod != mod)
                            {
                                bool isOtherRoom = otherMod.moduleType == ModuleType.SmallRoom || 
                                                   otherMod.moduleType == ModuleType.DirectorOffice || 
                                                   otherMod.moduleType == ModuleType.LargeRoom || 
                                                   otherMod.moduleType == ModuleType.OfficeRoom;
                                if (isOtherRoom && col.transform.IsChildOf(otherMod.transform))
                                {
                                    belongsToOtherRoom = true;
                                    break;
                                }
                            }
                        }
                        if (belongsToOtherRoom) continue;

                        col.enabled = false;
                        if (col.gameObject.activeSelf)
                        {
                            col.gameObject.SetActive(false);
                            removedCount++;
                            Debug.Log($"CleanInternalRoomBlockingWalls: Pared invasora ajena '{col.gameObject.name}' desactivada del interior de {mod.name}");
                        }
                    }
                }

                removedCount += ClearBlockingGeometryAtRoomEntrance(mod);
            }
            if (removedCount > 0)
            {
                Debug.Log($"ModularHospitalGenerator: {removedCount} muros/mallas intrusivas eliminadas del interior y umbrales de habitaciones.");
            }
        }

        private void SetupHideBeds()
        {
            int bedsConfigured = 0;
            foreach (HospitalModule mod in placedModules)
            {
                if (mod == null) continue;
                Transform[] allT = mod.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allT)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if ((cName.Contains("bed") || cName.Contains("cama")) && !cName.Contains("sheet") && !cName.Contains("pillow"))
                    {
                        // Asegurar el componente Bed
                        Bed bedComp = t.GetComponent<Bed>();
                        if (bedComp == null) bedComp = t.gameObject.AddComponent<Bed>();

                        // Asegurar el punto hijo de posición de escondite (hidePosition)
                        Transform hidePos = t.Find("HidePosition");
                        if (hidePos == null)
                        {
                            GameObject hObj = new GameObject("HidePosition");
                            hObj.transform.SetParent(t);
                            hObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                            hObj.transform.localRotation = Quaternion.identity;
                            hidePos = hObj.transform;
                        }
                        bedComp.hidePosition = hidePos;

                        // Asegurar colisionador trigger de interacción
                        BoxCollider box = t.GetComponent<BoxCollider>();
                        if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
                        box.isTrigger = true;
                        box.center = Vector3.zero;
                        box.size = new Vector3(2.2f, 1.2f, 2.2f);

                        bedsConfigured++;
                    }
                }
            }
            Debug.Log($"ModularHospitalGenerator: {bedsConfigured} camas configuradas con componente 'Bed' para esconderse.");
        }

        private void PositionPlayerAtSpawn(int sizeX, int sizeZ, float halfW, float halfD)
        {
            if (isMenuMode) return; // En modo menú NO queremos instanciar al jugador ni HUDs de partida

            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");

            if (playerObj == null)
            {
#if UNITY_EDITOR
                GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/FirstPersonController/Prefabs/NestedParent_Unpack.prefab");
                if (playerPrefab == null) playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/FirstPersonController/Prefabs/PlayerCapsule.prefab");
                if (playerPrefab != null)
                {
                    playerObj = Instantiate(playerPrefab);
                    playerObj.name = "NestedParent_Unpack";
                    playerObj.tag = "Player";
                    Debug.Log("ModularHospitalGenerator: Jugador auto-instanciado desde el prefab NestedParent_Unpack.");
                }
#endif
            }

            if (playerObj != null)
            {
                // Buscar una celda de pasillo abierta limpia (gridMatrix == 1) lejos de paredes macizas
                Vector2Int spawnCell = new Vector2Int(-1, -1);
                for (int x = 2; x < sizeX - 2; x++)
                {
                    for (int z = 2; z < sizeZ - 2; z++)
                    {
                        if (gridMatrix[x, z] == 1) // Es pasillo puro
                        {
                            // EVITAR A TODA COSTA aparecer dentro del modelo 3D de una habitación grande (como Director's Office)
                            bool nearRoom = (z + 1 < sizeZ && (gridMatrix[x, z + 1] == 2 || gridMatrix[x, z + 1] == 3)) ||
                                            (z - 1 >= 0 && (gridMatrix[x, z - 1] == 2 || gridMatrix[x, z - 1] == 3)) ||
                                            (x + 1 < sizeX && (gridMatrix[x + 1, z] == 2 || gridMatrix[x + 1, z] == 3)) ||
                                            (x - 1 >= 0 && (gridMatrix[x - 1, z] == 2 || gridMatrix[x - 1, z] == 3));

                            if (nearRoom) continue;

                            int openNeighbors = 0;
                            if (z + 1 < sizeZ && gridMatrix[x, z + 1] == 1) openNeighbors++;
                            if (z - 1 >= 0 && gridMatrix[x, z - 1] == 1) openNeighbors++;
                            if (x + 1 < sizeX && gridMatrix[x + 1, z] == 1) openNeighbors++;
                            if (x - 1 >= 0 && gridMatrix[x - 1, z] == 1) openNeighbors++;

                            if (openNeighbors >= 2)
                            {
                                spawnCell = new Vector2Int(x, z);
                                break;
                            }
                        }
                    }
                    if (spawnCell.x != -1) break;
                }

                // Fallback: cualquier celda gridMatrix == 1
                if (spawnCell.x == -1)
                {
                    for (int x = 1; x < sizeX - 1; x++)
                    {
                        for (int z = 1; z < sizeZ - 1; z++)
                        {
                            if (gridMatrix[x, z] == 1)
                            {
                                bool nearRoom = (z + 1 < sizeZ && (gridMatrix[x, z + 1] == 2 || gridMatrix[x, z + 1] == 3)) ||
                                                (z - 1 >= 0 && (gridMatrix[x, z - 1] == 2 || gridMatrix[x, z - 1] == 3)) ||
                                                (x + 1 < sizeX && (gridMatrix[x + 1, z] == 2 || gridMatrix[x + 1, z] == 3)) ||
                                                (x - 1 >= 0 && (gridMatrix[x - 1, z] == 2 || gridMatrix[x - 1, z] == 3));

                                if (nearRoom) continue;

                                spawnCell = new Vector2Int(x, z);
                                break;
                            }
                        }
                        if (spawnCell.x != -1) break;
                    }
                }

                if (spawnCell.x == -1) spawnCell = new Vector2Int(2, 2);

                float worldX = (spawnCell.x * 4.0f) - halfW + 2.0f;
                float worldZ = (spawnCell.y * 4.0f) - halfD + 2.0f;
                Vector3 spawnPos = new Vector3(worldX, transform.position.y + 0.5f, worldZ);

                CharacterController cc = playerObj.GetComponentInChildren<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerObj.transform.position = spawnPos;

                if (cc != null) cc.enabled = true;

                // CRÍTICO: Registrar el punto de spawn en GameManager para que RespawnSequence
                // pueda teletransportar al jugador aquí cuando muera y tenga vidas restantes.
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegistrarSpawnJugador(spawnPos, playerObj.transform.rotation);
                }

                // Ajustar velocidad ágil y cómoda para el jugador
                StarterAssets.FirstPersonController fpc = playerObj.GetComponentInChildren<StarterAssets.FirstPersonController>();
                if (fpc != null)
                {
                    fpc.MoveSpeed = 3.2f;
                    fpc.SprintSpeed = 5.0f;
                }

                // Garantizar que el script HideUnderBed esté presente en el jugador
                HideUnderBed hideScript = playerObj.GetComponent<HideUnderBed>();
                if (hideScript == null) hideScript = playerObj.AddComponent<HideUnderBed>();
                hideScript.interactDistance = 3.8f;

                Debug.Log($"ModularHospitalGenerator: Jugador posicionado con éxito en el pasillo principal del búnker: {spawnPos}");
                
                // Disparar monólogo inicial narrativo adaptado al personaje seleccionado (Ethan o Nora)
                LevelIntroData.TriggerStartMonologue("hospital");
            }

            // Ubicar al enemigo BookHead en el extremo opuesto del mapa lejos del jugador y reducir su velocidad
            GameObject enemyObj = GameObject.Find("BookHead");
            if (enemyObj == null) enemyObj = GameObject.Find("BookHeadMonster");
            if (enemyObj == null)
            {
                try { enemyObj = GameObject.FindGameObjectWithTag("Enemy"); } catch { /* Tag no definido en TagManager, ignorar */ }
            }

            if (enemyObj != null)
            {
                float enemyWorldX = ((sizeX - 2) * 4.0f) - halfW + 2.0f;
                float enemyWorldZ = ((sizeZ - 2) * 4.0f) - halfD + 2.0f;
                Vector3 enemySpawnPos = new Vector3(enemyWorldX, transform.position.y, enemyWorldZ);

                UnityEngine.AI.NavMeshAgent agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    agent.speed = 2.0f; // Velocidad pausada y sigilosa de acecho de horror
                }

                enemyObj.transform.position = enemySpawnPos;

                if (agent != null) agent.enabled = true;

                Debug.Log($"ModularHospitalGenerator: Enemigo BookHead posicionado con velocidad ajustada a {2.0f}m/s: {enemySpawnPos}");
            }
        }

        /// <summary>
        /// Activa un item pre-colocado del prefab completamente:
        /// habilita el GameObject raíz, todos sus hijos y todos sus Renderers.
        /// Funciona para cualquier item: nota, batería, fusible, etc.
        /// </summary>
        private void ActivateItemFully(GameObject item)
        {
            if (item == null) return;

            // Activar el objeto raíz
            item.SetActive(true);

            // Activar todos los GameObjects hijos (pueden estar desactivados en el prefab)
            Transform[] allChildren = item.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child != null && child.gameObject != item)
                    child.gameObject.SetActive(true);
            }

            // Habilitar todos los Renderers (MeshRenderer, SkinnedMeshRenderer, SpriteRenderer)
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = true;
            }
        }

        public void ClearExistingMap()
        {
            foreach (HospitalModule mod in placedModules)
            {
                if (mod != null)
                {
                    if (Application.isPlaying) Destroy(mod.gameObject);
                    else DestroyImmediate(mod.gameObject);
                }
            }
            placedModules.Clear();
            openConnectors.Clear();

            if (mapParentContainer != null)
            {
                List<GameObject> children = new List<GameObject>();
                foreach (Transform child in mapParentContainer) children.Add(child.gameObject);
                foreach (GameObject child in children)
                {
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }
        }
    }

    public class FlickerLamp : MonoBehaviour
    {
        public float baseIntensity = 0.45f;
        private Light targetLight;
        private AudioSource audioSource;
        private AudioClip flickerClip;
        private ParticleSystem sparkParticles;
        private Color originalColor;
        private float originalRange;
        private float nextFlickerTime;

        // --- CACHE DE RENDIMIENTO HOSPITAL ---
        private PowerBox cachedPowerBox;
        private Transform cachedPlayerCamera;

        private void Start()
        {
            cachedPowerBox = FindObjectOfType<PowerBox>();
        }

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            if (targetLight != null)
            {
                originalColor = targetLight.color;
                originalRange = targetLight.range;
            }
            
            // Cargar el clip de audio de zumbido/chispazo de lámpara
            flickerClip = Resources.Load<AudioClip>("Audio/Hospital/ErrorLightSound");
            if (flickerClip == null) flickerClip = Resources.Load<AudioClip>("ErrorLightSound");
            if (flickerClip == null) flickerClip = Resources.Load<AudioClip>("Audio/Compartido/Chispa");

            if (flickerClip != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = flickerClip;
                audioSource.spatialBlend = 1.0f; // Sonido 3D realista
                audioSource.minDistance = 1.5f;
                audioSource.maxDistance = 12.0f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.playOnAwake = false;
                audioSource.volume = 0.45f;
            }

            // Crear emisor de partículas 3D de chispas cayendo para el Hospital
            GameObject pObj = new GameObject("HospitalSparkParticles");
            pObj.transform.SetParent(transform, false);
            pObj.transform.localPosition = new Vector3(0f, -0.1f, 0f);

            sparkParticles = pObj.AddComponent<ParticleSystem>();
            var main = sparkParticles.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 3.2f;
            main.startSize = 0.06f;
            main.startColor = new Color(1.0f, 0.85f, 0.25f); // Ámbar/Amarillo eléctrico de hospital
            main.gravityModifier = 1.6f; // Las chispas caen despedidas al suelo por gravedad
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = sparkParticles.emission;
            emission.enabled = false;

            var shape = sparkParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.25f;
        }

        private void Update()
        {
            if (targetLight == null) return;

            // No parpadear si el juego está pausado
            if (Time.timeScale <= 0f) return;

            // Obtener la cámara del jugador para calcular distancia y aplicar LOD
            if (cachedPlayerCamera == null && Camera.main != null)
            {
                cachedPlayerCamera = Camera.main.transform;
            }

            bool playerIsNear = false;
            if (cachedPlayerCamera != null)
            {
                playerIsNear = Vector3.Distance(transform.position, cachedPlayerCamera.position) <= 12f;
            }

            // Usar PowerBox cacheado para evitar FindObjectOfType pesado cada frame
            if (cachedPowerBox == null) cachedPowerBox = FindObjectOfType<PowerBox>();
            bool isBlackout = (cachedPowerBox != null && cachedPowerBox.isPowerOut);

            if (isBlackout)
            {
                targetLight.enabled = true; // Luz activada en penumbra
                targetLight.color = new Color(0.9f, 0.7f, 0.35f); // Tinte ámbar tenue
                targetLight.range = 6.0f;
            }
            else
            {
                targetLight.color = originalColor != Color.clear ? originalColor : new Color(0.95f, 0.95f, 0.85f);
                targetLight.range = originalRange > 0f ? originalRange : 8.0f;
            }

            // OPTIMIZACIÓN LOD: Si el jugador está lejos, no parpadeamos, no hacemos chispas ni ejecutamos cálculos complejos
            if (!playerIsNear)
            {
                targetLight.intensity = isBlackout ? 0.35f : baseIntensity;
                return;
            }

            if (Time.time >= nextFlickerTime)
            {
                if (Random.value < (isBlackout ? 0.45f : 0.20f))
                {
                    // Chispazo y parpadeo (Solo si el jugador está cerca)
                    targetLight.intensity = isBlackout ? Random.Range(0.6f, 1.8f) : baseIntensity * Random.Range(0.08f, 0.35f);
                    nextFlickerTime = Time.time + Random.Range(0.05f, 0.2f);

                    // Disparar de 3 a 7 chispas 3D volando hacia el suelo
                    if (sparkParticles != null)
                    {
                        sparkParticles.Emit(Random.Range(3, 8));
                    }

                    // Reproducir el sonido 3D de chispazo
                    if (audioSource != null && flickerClip != null && !audioSource.isPlaying)
                    {
                        audioSource.pitch = Random.Range(0.85f, 1.15f);
                        audioSource.PlayOneShot(flickerClip, Random.Range(0.35f, 0.6f));
                    }
                }
                else
                {
                    targetLight.intensity = isBlackout ? 0.35f : baseIntensity * Random.Range(0.85f, 1.1f);
                    nextFlickerTime = Time.time + Random.Range(0.3f, 1.8f);
                }
            }
        }
    }
}
