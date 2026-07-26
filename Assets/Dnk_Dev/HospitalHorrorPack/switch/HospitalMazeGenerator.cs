using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation; // Requerido en Unity 6 para NavMeshSurface
public class HospitalMazeGenerator : MonoBehaviour
{
[Header("Prefabs de Mapeado")]
public GameObject floorPrefab;          // Prefab de tu loseta de suelo
public GameObject wallPrefab;           // Prefab de tu pared
public GameObject roomPrefab;           // Prefab de tu habitacion completa (Habitacion_Modulo)
public GameObject ceilingPrefab;        // Prefab del techo ciego/normal (Ceiling)
public GameObject ceilingLightPrefab;   // Prefab del techo con luz (Ceiling-light)
public float ceilingHeight = 4f;        // Altura base del techo en metros
public float ceilingScaleMultiplier = 1.15f; // Multiplicador extra para el techo (para cerrar huecos)
[Header("Escala y Grosor de Paredes")]
public float mapScale = 3f;             // Factor por el cual agrandar toda la estructura
public float wallThicknessMultiplier = 2.5f; // Grosor de la pared (Ya no se usa en Z local, pero se conserva por compatibilidad)
[Header("Dimensiones del Laberinto")]
public int width = 15;                  // Ancho en celdas (usar numeros impares)
public int height = 15;                 // Alto en celdas (usar numeros impares)
public float baseTileSize = 4f;         // Tamano base de cada loseta
public float tileSize;                 // Tamano real escalado (baseTileSize * mapScale)
public bool autoDetectTileSize = false; // Desactivar para poder ajustar la medida manualmente en el Inspector
[Header("Ajustes de Juego (Estilo Backrooms)")]
public int numberOfSpecialRooms = 3;    // Cuantas habitaciones con puerta generar
public int numberOfLobbies = 3;         // Numero de salas grandes/vestibulos abiertos
public int lobbySize = 3;               // Tamano del vestibulo (ej. 3x3 celdas)
[Range(0f, 0.6f)]
public float loopPercentage = 0.35f;    // Porcentaje de paredes a remover para crear bucles (Braid Maze)
[Range(0f, 1f)]
public float lightProbability = 0.35f;  // Densidad de lamparas en los pasillos (0.35 = 35% de los techos tendran luz)
[Header("Referencias a Objetos de la Escena")]
public GameObject playerObj;            // El Player del escena
public GameObject enemyObj;             // El Monstruo del escena
public GameObject powerBoxObj;          // La Caja de Fusibles del escena
public GameObject fusePrefab;           // Prefab del cilindro Fusible
[Header("Baterias de Repuesto")]
public GameObject batteryPrefab;        // Prefab de la bateria
public int batteriesToSpawn = 0;         // Cantidad (0 = auto-escala)
[Header("Ascensor y Llave (Escape)")]
public GameObject elevatorPrefab;       // Prefab del Ascensor (si es null se crea procedural)
public GameObject keycardPrefab;        // Prefab de la Tarjeta del Director (si es null se crea procedural)
[Header("Camas de Hospital (Escondites)")]
public GameObject bedPrefab;            // Prefab de la cama de hospital (P_BedBedding)
private System.Collections.Generic.List<Vector2Int> bedCells = new System.Collections.Generic.List<Vector2Int>();
[Header("Mobiliario de Oficinas (House Props)")]
public GameObject officeDeskPrefab;      // Escritorio
public GameObject officeChairPrefab;     // Silla
public GameObject officeCabinetPrefab;   // Archivador/Drawer
[Header("Mobiliario de Banos (House Props)")]
public GameObject bathroomToiletPrefab;  // Inodoro
public GameObject bathroomSinkPrefab;    // Lavamanos
public GameObject bathroomMirrorPrefab;  // Espejo
[Header("Mobiliario Medico")]
public GameObject medCabinetPrefab;      // P_Med_box_01
[Header("Alucinacion del Monstruo")]
public GameObject hallucinationEnemyPrefab; // Prefab del enemigo alucinacion
public enum RoomType { PatientRoom, Office, Bathroom }
private System.Collections.Generic.Dictionary<Vector2Int, RoomType> roomTypes = new System.Collections.Generic.Dictionary<Vector2Int, RoomType>();
[Header("Ajustes de Control del Ascensor")]
[Tooltip("Tiempo que tarda el ascensor en llegar tras llamarlo (en segundos)")]
public float elevatorCallTime = 30f;
[Tooltip("Velocidad de deslizamiento de las puertas del ascensor")]
public float elevatorDoorSpeed = 2f;
[Tooltip("Silencio inicial a omitir (en segundos) en el sonido de llamada del ascensor")]
public float elevatorCallSoundOffset = 1.5f;
[Tooltip("Silencio inicial a omitir (en segundos) en el sonido de llegada del ascensor")]
public float elevatorArriveSoundOffset = 3.0f;
[Tooltip("Tiempo de espera (en segundos) desde que suena el timbre hasta que las puertas empiezan a abrirse")]
public float elevatorDoorOpenDelay = 1.0f;
[Tooltip("SI SE ACTIVA: El ascensor no requerira tarjeta de acceso para ser llamado (ideal para pruebas rapidas)")]
public bool elevatorBypassKeycard = false;
[Header("Enemigo y Patrullaje")]
[Tooltip("Separacion minima entre puntos de patrulla en celdas")]
public int minPatrolSpacing = 4;         // Distancia minima entre puntos de patrulla (celdas)
[Tooltip("Distancia minima al jugador al spawnear el enemigo (celdas)")]
public int enemySpawnMinDist = 8;        // Distancia minima del enemigo al jugador al inicio
[Tooltip("Distancia maxima al jugador al spawnear el enemigo (celdas)")]
public int enemySpawnMaxDist = 15;       // Distancia maxima del enemigo al jugador al inicio
[Tooltip("Factor de escala para ajustar el enemigo al pasillo (0.6-0.8 recomendado)")]
public float enemyScaleMultiplier = 1.0f;  // Escala fija del enemigo (1.0 = tamano original del modelo, ajustar si es muy grande/pequeno)
[Tooltip("Radio del NavMeshAgent (aumentar para evitar que brazos/pies traspasen paredes). Recomendado: 1.0-2.0")]
public float enemyNavMeshRadius = 1.5f;   // Cuanto se aleja el centro del enemigo de las paredes
[Tooltip("Cantidad de fusibles a generar en el mapa. 0 = escala automaticamente con el tamano.")]
public int fusesToSpawn = 0;             // Cantidad a generar (0 = auto)
private List<Transform> generatorPoints = new List<Transform>();
public Vector2Int playerSpawnCell = new Vector2Int(1, 1); // Cache del spawn del jugador
public bool[,] grid;                   // true = Camino, false = Pared/Bloqueo
private List<Vector2Int> corridors = new List<Vector2Int>();
private List<Vector2Int> roomPositions = new List<Vector2Int>();
private Material doorMaterial; // Material de la puerta original copiado al iniciar
#if UNITY_EDITOR
private GameObject editorDoorPrefab;
private GameObject editorDoorFramePrefab;
void LoadEditorPrefabs()
{
editorDoorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Door_01_.prefab");
editorDoorFramePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Door_01_Base.prefab");
if (bedPrefab == null)
{
bedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_BedBedding.prefab");
}
if (editorDoorPrefab != null) Debug.Log("MazeGenerator: Prefab de puerta original cargado con éxito.");
if (editorDoorFramePrefab != null) Debug.Log("MazeGenerator: Prefab de marco de puerta original cargado con éxito.");
}
#endif
private List<Vector2Int> roomPivots = new List<Vector2Int>();
private List<Vector2Int> spawnedRooms = new List<Vector2Int>();
private HashSet<Vector3> spawnedWalls = new HashSet<Vector3>();
private System.Collections.Generic.Dictionary<Vector2Int, float> roomRotations = new System.Collections.Generic.Dictionary<Vector2Int, float>();
private System.Collections.Generic.Dictionary<Vector2Int, Vector2Int> roomDoors = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();
private NavMeshSurface navMeshSurface;
[Header("Puzzle del Teclado (7 digitos)")]
public string correctKeypadCode = ""; // Clave generada para el Keypad
private System.Collections.Generic.List<GameObject> activeFuses = new System.Collections.Generic.List<GameObject>();
private System.Collections.Generic.List<GameObject> activeBatteries = new System.Collections.Generic.List<GameObject>();
private Vector2Int generatorACell;
private Vector2Int generatorBCell;
private Vector2Int elevatorCell;
private Vector2Int elevatorFrontCell;
[Header("Menu y Estado de Juego")]
public bool isMenuMode = true;
// Variables para la Pantalla de Carga VHS
private bool isGeneratingMap = false;
private string loadingProgressText = "";
private int loadingStep = 0;
private System.Collections.Generic.List<Canvas> disabledHUDCanvases = new System.Collections.Generic.List<Canvas>();
private GameObject cachedCanvasInputs;
[Header("Ajustes de Iluminacion de Juego")]
public Color gameAmbientColor = new Color(0.08f, 0.09f, 0.11f); // Gris azulado mas claro (estilo tuneles) para que no sea tan oscuro y las notas sean visibles
public System.Collections.Generic.List<Vector2Int> generatorCells = new System.Collections.Generic.List<Vector2Int>();
public enum SpawnPointType { Floor, Desk, Wall, ToiletTank }
public class ItemSpawnPoint
{
public Vector3 position;
public Quaternion rotation;
public SpawnPointType type;
public bool isDirector = false;
}
    private System.Collections.Generic.List<ItemSpawnPoint> availableSpawnPoints = new System.Collections.Generic.List<ItemSpawnPoint>();

    void OnValidate()
    {
        baseTileSize = 4.0f;
        mapScale = 1.0f;
        tileSize = 4.0f;
        if (ceilingHeight <= 0.1f) ceilingHeight = 2.9f;
    }

    void Start()
{
if (bedPrefab == null)
{
bedPrefab = Resources.Load<GameObject>("P_BedBedding");
}
// Auto-deteccion de la pila en escena si se deja vacia en el Inspector
if (batteryPrefab == null)
{
GameObject sceneBattery = GameObject.Find("PilaProcedural");
if (sceneBattery == null) sceneBattery = GameObject.Find("Pila");
if (sceneBattery != null)
{
batteryPrefab = sceneBattery;
// Desactivar el objeto original en la jerarquia para que no aparezca flotando en medio del aire
sceneBattery.SetActive(false); 
Debug.Log("[MazeGenerator] Auto-detectada la pila 'PilaProcedural' de la escena como plantilla para la geneón.");
}
else
{
Debug.LogWarning("[MazeGenerator] batteryPrefab es nulo y no se encontro 'PilaProcedural' en la jerarquia de la escena.");
}
}
// 1. Desanidar y encontrar al jugador real en la jerarquia (si existe)
GameObject nestedParent = GameObject.Find("NestedParent_Unpack");
if (nestedParent != null)
{
if (nestedParent != null)
{
GameObject parentObj = nestedParent.gameObject;
if (parentObj.CompareTag("Player"))
{
parentObj.tag = "Untagged"; // Quitar tag al contenedor vacio estatico
}
parentObj.transform.SetParent(null);
}
nestedParent.tag = "Player"; // Etiquetar al jugador real que se mueve
playerObj = nestedParent;
Debug.Log("MazeGenerator: Player Obj re-asignado al jugador real 'NestedParent_Unpack' y etiquetado como 'Player'.");
}
// 2. Detectar automaticamente si es el menu o el juego real (tolerante a mayusculas/minusculas y escenas de backup)
string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
Debug.Log($"[MazeGenerator] Start() ejecutado en la escena: '{activeSceneName}'");
        string activeSceneLower = activeSceneName.ToLower();
        if (activeSceneLower.Contains("sample") || activeSceneLower.Contains("hospital") || activeSceneLower.Contains("game") || activeSceneLower.Contains("tunnels"))
        {
            isMenuMode = false;
            Debug.Log("[MazeGenerator] Escena de juego real detectada. Modo juego forzado.");
        }
        else if (activeSceneLower.Contains("menu"))
        {
            isMenuMode = true;
            Debug.Log("[MazeGenerator] Escena de menu principal detectada. Modo menu forzado.");
        }

        // 3. Aplicar configuraciones de dimensiones y objetos segun el modo detectado
        if (isMenuMode)
        {
            width = 12;
            height = 12;
            Debug.Log("[MazeGenerator] Configurado en MODO MENU (12x12).");
        }
        else
        {
            // Si no se inicio desde el menu (por ejemplo, dar Play directo en la escena del hospital)
            if (!MainMenuManager.startedFromMenu)
            {
                PlayerPrefs.SetInt("SelectedMapSize", 25);
            }

            // Cargar tamano de mapa seleccionado en el menu principal
            int savedSize = PlayerPrefs.GetInt("SelectedMapSize", 25);
            if (savedSize == 15) savedSize = 17; // Aumentar de 15 a 17 para dar espacio adecuado a cuartos y decoracion
            width = savedSize;
            height = savedSize;

            // Cargar dificultad y aplicar nerf/buff al spawn inicial de recursos
            string savedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
            float itemMultiplier = 1.0f;
            if (savedDifficulty == "FACIL") itemMultiplier = 1.3f;
            else if (savedDifficulty == "DIFICIL") itemMultiplier = 0.65f;

            // Escalar cantidad de fusibles y baterías segun el tamano de mapa y dificultad
            if (width <= 25)
            {
                fusesToSpawn = Mathf.Max(3, Mathf.RoundToInt(5 * itemMultiplier));
                batteriesToSpawn = Mathf.Max(3, Mathf.RoundToInt(5 * itemMultiplier));
            }
            else if (width <= 35)
            {
                fusesToSpawn = Mathf.Max(4, Mathf.RoundToInt(8 * itemMultiplier));
                batteriesToSpawn = Mathf.Max(4, Mathf.RoundToInt(8 * itemMultiplier));
            }
            else
            {
                fusesToSpawn = Mathf.Max(5, Mathf.RoundToInt(12 * itemMultiplier));
                batteriesToSpawn = Mathf.Max(5, Mathf.RoundToInt(12 * itemMultiplier));
            }
            Debug.Log($"[MazeGenerator] MODO JUEGO ACTIVO. Dificultad: {savedDifficulty}. Mapa: {width}x{height}, Fusibles: {fusesToSpawn}, Baterias: {batteriesToSpawn}");
        }

        // 4. Desactivar todos los objetos manuales estaticos del editor (Hospital, Wall 2.0, Luces, FocosRoom, Prefabs, etc.)
        string[] manualNames = new string[] { 
            "Hospital", "Wall 2.0", "Door", "Wall", "Ceiling", "Celling", 
            "Luces", "FocosRoom", "Prefabs", "Music", "AscensorProcedural", "TarjetaAccesoDirector" 
        };
        
        foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObj == gameObject || rootObj.name.StartsWith("Generated_") || rootObj.name.Contains("Player") || rootObj.name.Contains("Main Camera")) continue;

            string rNameLower = rootObj.name.ToLower();
            foreach (string mName in manualNames)
            {
                if (rNameLower == mName.ToLower() || rNameLower.StartsWith(mName.ToLower()))
                {
                    rootObj.SetActive(false);
                    Debug.Log($"MazeGenerator: Objeto manual estatico '{rootObj.name}' desactivado en runtime.");
                    break;
                }
            }
        }

        // 5. Detector de duplicados
        HospitalMazeGenerator[] generators = FindObjectsByType<HospitalMazeGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (generators.Length > 1 && generators[0] != this)
        {
            Debug.LogWarning("MazeGenerator: Detectado script duplicado en la escena. Destruyendo este componente.");
            Destroy(this);
            return;
        }

        navMeshSurface = GetComponent<NavMeshSurface>();
        if (navMeshSurface == null)
        {
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }

        baseTileSize = 4.0f;
        mapScale = 1.0f;
        tileSize = 4.0f;

#if UNITY_EDITOR
        LoadEditorPrefabs();
#endif

        // Copiar material de la puerta
        GameObject existingDoor = GameObject.Find("Door");
        if (existingDoor != null)
        {
            Renderer r = existingDoor.GetComponent<Renderer>();
            if (r != null)
            {
                doorMaterial = r.sharedMaterial;
            }
        }
        if (doorMaterial == null)
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r.gameObject.name.Contains("Door") || r.gameObject.name.Contains("Puerta"))
                {
                    doorMaterial = r.sharedMaterial;
                    break;
                }
            }
        }

        // 6. Generar el laberinto (con Pantalla de Carga en modo juego real)
        if (!isMenuMode)
        {
            GenerateMaze();
        }
        else
        {
            GenerateMaze();
        }

        // 7. Si es modo menu, configurar la desactivacion del jugador, iluminacion lugubre y auto-anadir el MainMenuManager
        if (isMenuMode)
        {
            ConfigurePlayerForMenu();

            // Desactivar de forma infalible TODAS las luces direccionales en la escena del menu
            Light[] allSceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light l in allSceneLights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    l.gameObject.SetActive(false);
                    Debug.Log($"[MazeGenerator] Luz direccional desactivada en menu: '{l.gameObject.name}'");
                }
            }

            // Configurar la camara para limpiar el fondo a negro sólido (evita el gris/cielo de URP)
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
                Debug.Log("[MazeGenerator] Camara de menu configurada con fondo negro sólido.");
            }

            // Ajustar la iluminacion de Unity en el menu para que use el mismo tono que el juego real
// NavMeshAgentMode
            RenderSettings.ambientLight = gameAmbientColor; // Mismo ambiente que la partida
            RenderSettings.skybox = null; // Quitar el cielo gris claro por defecto
            Debug.Log($"[MazeGenerator] Iluminacion de menu establecida al mismo ambiente: {gameAmbientColor}");

            MainMenuManager menuManager = gameObject.GetComponent<MainMenuManager>();
            if (menuManager == null)
            {
                menuManager = gameObject.AddComponent<MainMenuManager>();
                menuManager.generator = this;
                Debug.Log("[MazeGenerator] Componente MainMenuManager auto-anadido con éxito.");
            }
        }
        else
        {
            // Cargar y aplicar la dificultad guardada, sensibilidad y volumen
            string savedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
            ApplyDifficultySettings(savedDifficulty);

            float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
            var controller = playerObj != null ? playerObj.GetComponent<CharacterController>() : null;
            // if (controller != null)
            {
// RotationSpeed
            }

            float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            AudioListener.volume = savedVolume;
            Debug.Log($"[MazeGenerator] Partida real cargada. Dificultad: {savedDifficulty}, Sens: {savedSensitivity}");

            // Ajustar iluminacion ambiental minima de la partida para evitar el negro absoluto
// NavMeshAgentMode
            RenderSettings.ambientLight = gameAmbientColor;
            Debug.Log($"[MazeGenerator] Iluminacion ambiental minima aplicada: {gameAmbientColor}");

            // Auto-anadir gestor de menu de pausa en partida real
            // PauseMenuManager pauseManager = gameObject.GetComponent<MainMenuManager>();
//             if (pauseManager == null)
//             {
//                 gameObject.AddComponent<PauseMenuManager>();
//                 Debug.Log("[MazeGenerator] Componente PauseMenuManager auto-anadido con éxito.");
//             }
        }
    }

    private void ConfigurePlayerForMenu()
    {
        if (playerObj == null) return;

        // Desactivar movimiento y fisicas del jugador (buscando en hijos como PlayerCapsule)
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Hacer el Rigidbody kinematic para que no caiga al vacio por gravedad al apagar el CharacterController
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log("MazeGenerator: Rigidbody del jugador configurado como kinematic para el menu.");
        }

        // Desactivar todos los Canvas de la escena para evitar superposiciones de HUD (libreta, joysticks, etc.)
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            // No apagar el Canvas si es parte de la UI del menu o del propio generador
            if (canvas.gameObject.name.Contains("Menu") || canvas.GetComponent<MainMenuManager>() != null)
            {
                continue;
            }
            canvas.gameObject.SetActive(false);
            Debug.Log($"[MazeGenerator] Desactivado Canvas de HUD: '{canvas.gameObject.name}'");
        }

        // Desactivar EventSystem del jugador si existe
// GetComponeontSystem
        // if (ev != null) ev.gameObject.SetActive(false);

        // Desactivar Joysticks y controles de pantalla
        GameObject canvasInputs = GameObject.Find("UICanvas_StarterAssetsInputs_Required");
        if (canvasInputs == null) canvasInputs = GameObject.Find("UICanvas_StarterAssetsInputs");
        if (canvasInputs != null) canvasInputs.SetActive(false);

        // foreach (EnemyController monster in allMonsters)
        {
            // monster
//             Debug.Log($"[MazeGenerator] Desactivado monstruo en menu: '{monster.gameObject.name}'");
        }
        if (enemyObj != null)
        {
            enemyObj.SetActive(false);
        }

        MonoBehaviour[] scripts = playerObj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in scripts)
        {
            if (script == null || script == this || script.GetType().Name == "HospitalMazeGenerator" || script.GetType().Name == "MainMenuManager") continue;
            string scriptName = script.GetType().Name;
            if (scriptName.Contains("FirstPersonController") || 
                scriptName.Contains("StarterAssetsInputs") ||
                scriptName.Contains("FlashlightController") ||
                scriptName.Contains("PlayerHealth") ||
                scriptName.Contains("PlayerSanity"))
            {
                script.enabled = false;
            }
        }

        // Apagar la linterna del jugador
        Light[] lights = playerObj.GetComponentsInChildren<Light>(true);
        foreach (Light l in lights)
        {
            if (l.gameObject.name.Contains("Flashlight") || l.gameObject.name.Contains("Light"))
            {
            // Debug.Log
            }
        }

        // Desactivar a TODOS los monstruos y su IA en la escena para que no hagan ruido ni vean al jugador en el menu
        // EnemyController[] allMonsters
        // foreach (EnemyController monster in allMonsters)
        {
            // monster
            // Debug.Log
        }
        if (enemyObj != null)
        {
            enemyObj.SetActive(false);
        }

        // Desactivar CinemachineBrain temporalmente para poder animar la camara del menu
        // CinemachineBrain brain = ...
        // if (brain != null) // brain.enabled = false;
    }

    public void StartRealGame(int selectedWidth, int selectedHeight, string difficulty)
    {
        StopAllCoroutines(); // Detener la geneón de fondo del menu principal si aun esta corriendo
        isGeneratingMap = false; // Resetear el estado
        
        isMenuMode = false;
        
        // 1. Destruir mapa procedural previo
        Transform mapRoot = transform.Find("Generated_Hospital_Map");
        if (mapRoot != null)
        {
            foreach (Transform child in mapRoot)
            {
                Destroy(child.gameObject);
            }
        }

        // Limpiar colecciones
        corridors.Clear();
        roomPositions.Clear();
        roomPivots.Clear();
        bedCells.Clear();
        generatorCells.Clear();
        activeFuses.Clear();
        activeBatteries.Clear();
        
        // 2. Establecer el tamano de mapa seleccionado
        if (selectedWidth == 15) selectedWidth = 17;
        if (selectedHeight == 15) selectedHeight = 17;
        width = selectedWidth;
        height = selectedHeight;

        // Escalar cantidad de fusibles, baterías y habitaciones segun el tamano de mapa
        if (width <= 20) // Chico
        {
            fusesToSpawn = 3;
            batteriesToSpawn = 4;
            numberOfSpecialRooms = 3;
        }
        else if (width <= 28) // Mediano
        {
            fusesToSpawn = 5;
            batteriesToSpawn = 7;
            numberOfSpecialRooms = 6;
        }
        else // Grande
        {
            fusesToSpawn = 8;
            batteriesToSpawn = 10;
            numberOfSpecialRooms = 9;
        }

        // 3. Generar el laberinto real de juego
        GenerateMaze();

        // 4. Configurar dificultad en el jugador y enemigo
        ApplyDifficultySettings(difficulty);

        // 5. Posicionar al jugador en la celda de inicio y habilitar controles
        if (playerObj != null)
        {
            Vector3 startPos = transform.position + new Vector3(playerSpawnCell.x * tileSize, 1.0f, playerSpawnCell.y * tileSize);
            playerObj.transform.position = startPos;

            // Reactivar CinemachineBrain
            // CinemachineBrain brain = ...
            // if (brain != null) // brain.enabled = true;

            // Reactivar componentes (buscando en hijos como PlayerCapsule)
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            // Restaurar fisicas normales del Rigidbody
            Rigidbody rb = playerObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                Debug.Log("MazeGenerator: Rigidbody del jugador restaurado a fisicas normales.");
            }

            MonoBehaviour[] scripts = playerObj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in scripts)
            {
                if (script == null || script == this || script.GetType().Name == "HospitalMazeGenerator" || script.GetType().Name == "MainMenuManager") continue;
                string scriptName = script.GetType().Name;
                if (scriptName.Contains("FirstPersonController") || 
                    scriptName.Contains("StarterAssetsInputs") ||
                    scriptName.Contains("FlashlightController") ||
                    scriptName.Contains("PlayerHealth") ||
                    scriptName.Contains("PlayerSanity"))
                {
                    script.enabled = true;
                }
            }

            // Cargar linterna a tope
            // FlashlightController flashlight = playerObj.GetComponent<CharacterController>();
            // if (flashlight != null)
            {
                // flashlight
            }
        }

        // 6. Activar y posicionar al enemigo
        if (enemyObj != null)
        {
            enemyObj.SetActive(true);
            SpawnEnemy(playerSpawnCell);
        }

        // Bloquear cursor de forma segura
        MobileInput.SetCursorState(true);

        // Auto-anadir Brújula HUD Minimalista
        if (gameObject.GetComponent<BackroomsCompassHUD>() == null)
        {
            gameObject.AddComponent<BackroomsCompassHUD>();
        }

        Debug.Log($"MazeGenerator: Partida real iniciada con éxito! Mapa: {width}x{height}, Dificultad: {difficulty}");
    }

    private void ApplyDifficultySettings(string difficulty)
    {
        // EnemyController enemy = enemyObj != null ? enemyObj.GetComponent<EnemyController>() : null;
        PlayerSanity sanity = playerObj != null ? playerObj.GetComponent<PlayerSanity>() : null;
        if (sanity == null && playerObj != null) sanity = playerObj.GetComponent<PlayerSanity>();
        // FlashlightController flashlight = playerObj != null ? playerObj.GetComponent<CharacterController>() : null;

        if (difficulty == "FACIL")
        {
            // if (enemy != null)
            {
                // enemy
                // enemy
                // enemy
            }
            if (sanity != null)
            {
                sanity.darkDrainRate = 0.5f;
            }
            // if (flashlight != null)
            {
                // flashlight
            }
        }
        else if (difficulty == "DIFICIL")
        {
            // if (enemy != null)
            {
                // enemy
                // enemy
                // enemy
            }
            if (sanity != null)
            {
                sanity.darkDrainRate = 1.1f;
            }
            // if (flashlight != null)
            {
                // flashlight
            }
        }
        else
        {
            // if (enemy != null)
            {
                // enemy
                // enemy
                // enemy
            }
            if (sanity != null)
            {
                sanity.darkDrainRate = 0.8f;
            }
            // if (flashlight != null)
            {
                // flashlight
            }
        }
    }


void GenerateMaze()
    {
        spawnedRooms.Clear();
        availableSpawnPoints.Clear();
        corridors.Clear();
        roomPositions.Clear();
        roomPivots.Clear();
        bedCells.Clear();
        generatorCells.Clear();
        activeFuses.Clear();
        activeBatteries.Clear();
        roomTypes.Clear();
        roomRotations.Clear();
        roomDoors.Clear();
        spawnedWalls.Clear();

        // Inicializar código del keypad al principio
        correctKeypadCode = "";
        for (int i = 0; i < 7; i++)
        {
            correctKeypadCode += UnityEngine.Random.Range(0, 10).ToString();
        }
        Debug.Log("HospitalMazeGenerator: Clave generada anticipadamente para el Keypad = " + correctKeypadCode);

        // 1. Inicializar la cuadricula (toda llena de paredes)
        grid = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = false;
            }
        }

        // 2. Generar laberinto usando algoritmo DFS (Depth-First Search)
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int startCell = new Vector2Int(1, 1);
        grid[startCell.x, startCell.y] = true;
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                Vector2Int chosen = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
                
                // Romper la pared entre celdas
                int wallX = current.x + (chosen.x - current.x) / 2;
                int wallY = current.y + (chosen.y - current.y) / 2;
                grid[wallX, wallY] = true;
                grid[chosen.x, chosen.y] = true;

                stack.Push(chosen);
            }
            else
            {
                stack.Pop();
            }
        }

        // 3. Carvear salas grandes y salones abiertos (Estilo Backrooms)
        CarveLobbies();

        // 4. Crear bucles/caminos alternativos y ensanchar pasillos
        CreateLoops();
        CarveWiderCorridors();

        // 5. Reservar ubicaciones para Habitaciones Especiales (Habitacion_Modulo)
        PlaceRooms();

        // 6. Instanciar los objetos fisicos en la escena de Unity
        BuildPhysicalMap();

        // 7. Regenerar el NavMesh dinamente para que el enemigo sepa caminar por el laberinto
        if (navMeshSurface != null)
        {
            // Sincronizar agentTypeID del NavMeshSurface con el del enemigo
            // para evitar el error "agent not placed on NavMesh"
            if (enemyObj != null)
            {
                UnityEngine.AI.NavMeshAgent agentComp = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agentComp != null)
                {
                    // // // // navMeshSurface.agentTypeID = agentTypeID;
                    // Debug.Log("MazeGenerator: NavMeshSurface agentTypeID sincronizado con enemigo: " + agentTypeID);
                }
            }

            // Desactivar temporalmente los colliders de todos los paneles de puertas
            // para que no dejen un agujero/bloqueo ciego en el NavMesh al hornearse
            ProceduralDoorInteract[] allDoors = FindObjectsOfType<ProceduralDoorInteract>();
            System.Collections.Generic.List<Collider> disabledColliders = new System.Collections.Generic.List<Collider>();
            foreach (var door in allDoors)
            {
                if (door != null)
                {
                    Collider[] cols = door.GetComponents<Collider>();
                    foreach (var c in cols)
                    {
                        if (c != null && c.enabled)
                        {
                            c.enabled = false;
                            disabledColliders.Add(c);
                        }
                    }
                }
            }

            // Desactivar tambien temporalmente los colliders del techo y lamparas colgantes para evitar que el horneado
            // piense que el espacio es verticalmente intransitable en zonas de pasillo ancho o vestibulo (Lobby)
            Collider[] allSceneColliders = FindObjectsOfType<Collider>();
            foreach (var c in allSceneColliders)
            {
                if (c != null && c.enabled)
                {
                    string nameLower = c.gameObject.name.ToLower();
                    if (nameLower.Contains("ceiling") || nameLower.Contains("techo") || nameLower.Contains("lamp") || nameLower.Contains("luz") || nameLower.Contains("light") || nameLower.Contains("keypad") || nameLower.Contains("botonera"))
                    {
                        c.enabled = false;
                        disabledColliders.Add(c);
                    }
                }
            }

            // Asegurar que el NavMeshSurface recoja todos los GameObjects de la escena
            navMeshSurface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.BuildNavMesh();

            // Re-activar los colliders de las puertas y techos de forma inmediata
            foreach (var c in disabledColliders)
            {
                if (c != null) c.enabled = true;
            }

            Debug.Log("MazeGenerator: NavMesh generado dinamicamente con éxito (puertas y techos ignorados durante el bake).");
        }

        // 8. Determinar la ubicacion de la celda del ascensor primero (para excluirla de los generadores)
        DetermineElevatorCell();

        // 9. Determinar y spawnear subgeneradores
        SpawnSubGenerators();
        
        // 10. Spawnear el ascensor en la celda previamente calculada
        SpawnElevator();

        // 11. Ubicar al jugador, enemigo e items (que filtraran distancia contra los subgeneradores y ascensor)
        SpawnEntitiesAndItems();
    
        // 11. Spawnear notas y otros sistemas
        SpawnNotes();
        // // StartCoroutine(GenerateMazeRoutine()); // Removido por redundancia, ahora se controla en PowerBox.Update()
    }

    List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        Vector2Int[] directions = {
            new Vector2Int(0, 2),  // Norte
            new Vector2Int(0, -2), // Sur
            new Vector2Int(2, 0),  // Este
            new Vector2Int(-2, 0)  // Oeste
        };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighbor = cell + dir;
            if (neighbor.x > 0 && neighbor.x < width - 1 && neighbor.y > 0 && neighbor.y < height - 1)
            {
                if (!grid[neighbor.x, neighbor.y])
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    void CarveLobbies()
    {
        for (int i = 0; i < numberOfLobbies; i++)
        {
            int centerX = Random.Range(2, width - 3);
            int centerY = Random.Range(2, height - 3);

            int halfLobby = lobbySize / 2;
            for (int x = centerX - halfLobby; x <= centerX + halfLobby; x++)
            {
                for (int y = centerY - halfLobby; y <= centerY + halfLobby; y++)
                {
                    if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
                    {
                        grid[x, y] = true; // Abrir espacio
                    }
                }
            }

            if (lobbySize >= 3)
            {
                grid[centerX, centerY] = false; // Pilar sólido
            }
        }
    }

    void CreateLoops()
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (!grid[x, y]) // Es una pared
                {
                    bool horizontalPath = grid[x - 1, y] && grid[x + 1, y];
                    bool verticalPath = grid[x, y - 1] && grid[x, y + 1];

                    if ((horizontalPath || verticalPath) && Random.value < loopPercentage)
                    {
                        grid[x, y] = true;
                    }
                }
            }
        }
    }

    void CarveWiderCorridors()
    {
        for (int x = 2; x < width - 2; x++)
        {
            for (int y = 2; y < height - 2; y++)
            {
                if (!grid[x, y])
                {
                    int adjacentPaths = 0;
                    if (grid[x - 1, y]) adjacentPaths++;
                    if (grid[x + 1, y]) adjacentPaths++;
                    if (grid[x, y - 1]) adjacentPaths++;
                    if (grid[x, y + 1]) adjacentPaths++;

                    if (adjacentPaths >= 3 && Random.value < 0.5f)
                    {
                        grid[x, y] = true;
                    }
                }
            }
        }
    }

        struct RoomSlotCandidate
    {
        public Vector2Int roomCell;
        public Vector2Int backCell;
        public Vector2Int doorCell;
        public float rotation;
    }

    void PlaceRooms()
    {
        int placedRooms = 0;
        roomPivots.Clear();
        roomPositions.Clear();
        roomRotations.Clear();
        roomDoors.Clear();
        roomTypes.Clear();

        List<RoomSlotCandidate> candidates = new List<RoomSlotCandidate>();

        // 1. Orilla Oeste (Izquierda): La celda trasera queda en x = 1 (borde), puerta hacia el Este (x = 3)
        for (int y = 2; y < height - 2; y += 2)
        {
            candidates.Add(new RoomSlotCandidate {
                roomCell = new Vector2Int(2, y),
                backCell = new Vector2Int(1, y),
                doorCell = new Vector2Int(3, y),
                rotation = -90f // Mirar al Este (hacia adentro)
            });
        }

        // 2. Orilla Este (Derecha): La celda trasera queda en x = width - 2 (borde), puerta hacia el Oeste (x = width - 4)
        for (int y = 2; y < height - 2; y += 2)
        {
            candidates.Add(new RoomSlotCandidate {
                roomCell = new Vector2Int(width - 3, y),
                backCell = new Vector2Int(width - 2, y),
                doorCell = new Vector2Int(width - 4, y),
                rotation = 90f // Mirar al Oeste (hacia adentro)
            });
        }

        // 3. Orilla Sur (Abajo): La celda trasera queda en y = 1 (borde), puerta hacia el Norte (y = 3)
        for (int x = 2; x < width - 2; x += 2)
        {
            candidates.Add(new RoomSlotCandidate {
                roomCell = new Vector2Int(x, 2),
                backCell = new Vector2Int(x, 1),
                doorCell = new Vector2Int(x, 3),
                rotation = 180f // Mirar al Norte (hacia adentro)
            });
        }

        // 4. Orilla Norte (Arriba): La celda trasera queda en y = height - 2 (borde), puerta hacia el Sur (y = height - 4)
        for (int x = 2; x < width - 2; x += 2)
        {
            candidates.Add(new RoomSlotCandidate {
                roomCell = new Vector2Int(x, height - 3),
                backCell = new Vector2Int(x, height - 2),
                doorCell = new Vector2Int(x, height - 4),
                rotation = 0f // Mirar al Sur (hacia adentro)
            });
        }

        // Barajar la lista de candidatos aleatoriamente (Fisher-Yates)
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            RoomSlotCandidate temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        // Intentar posicionar las habitaciones
        foreach (var slot in candidates)
        {
            if (placedRooms >= numberOfSpecialRooms) break;

            // Evitar colocar habitaciones en las cercanas del punto de spawn del jugador (1, 1) para que no quede atrapado ni aislado
            if (slot.roomCell.x <= 3 && slot.roomCell.y <= 3) continue;
            if (slot.backCell.x <= 3 && slot.backCell.y <= 3) continue;

            // Evitar solapamientos
            if (roomPositions.Contains(slot.roomCell) || roomPositions.Contains(slot.backCell)) continue;

            // Mantener una distancia saludable de 3 celdas de otras habitaciones especiales
            if (IsRoomTooClose(slot.roomCell, 3) || IsRoomTooClose(slot.backCell, 3)) continue;

            // Forzar que el pasillo de la puerta y el interior del cuarto sean transitables en la rejilla
            grid[slot.doorCell.x, slot.doorCell.y] = true;
            grid[slot.roomCell.x, slot.roomCell.y] = true;
            grid[slot.backCell.x, slot.backCell.y] = true;

            // Registrar datos
            roomPivots.Add(slot.roomCell);
            roomPositions.Add(slot.roomCell);
            roomPositions.Add(slot.backCell);
            roomRotations[slot.roomCell] = slot.rotation;
            roomDoors[slot.roomCell] = slot.doorCell;

            // Determinar tipo de habitacion
            RoomType type = RoomType.PatientRoom;
            if (placedRooms % 3 == 0) type = RoomType.PatientRoom;
            else if (placedRooms % 3 == 1) type = RoomType.Office;
            else type = RoomType.Bathroom;
            roomTypes[slot.roomCell] = type;

            placedRooms++;
        }

        // Forzar que la oficina del director (el ultimo cuarto) sea siempre de tipo Office
        if (roomPivots.Count > 0)
        {
            roomTypes[roomPivots[roomPivots.Count - 1]] = RoomType.Office;
        }

        Debug.Log("MazeGenerator: Se posicionaron " + placedRooms + " habitaciones especiales en las orillas del mapa.");
    }

    private Light EnsureLightComponent(GameObject lampObj, Color? lightColor = null, float intensity = 3.0f, float range = 8.0f)
    {
        if (lampObj == null) return null;

        Light l = lampObj.GetComponentInChildren<Light>();
        if (l == null)
        {
            GameObject lightChild = new GameObject("LampPointLight");
            lightChild.transform.SetParent(lampObj.transform, false);
            lightChild.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            l = lightChild.AddComponent<Light>();
        }

        l.type = LightType.Point;
        l.color = lightColor ?? new Color(0.95f, 0.95f, 1.0f);
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.Soft;
        l.enabled = true;

        // Activar emision en las mallas de la lampara
        Renderer[] renderers = lampObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r != null && r.material != null)
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", l.color * 2.5f);
            }
        }

        return l;
    }

    void BuildPhysicalMap()
    {
        List<GameObject> oldGenerated = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Generated_"))
            {
                oldGenerated.Add(child.gameObject);
            }
        }
        foreach (GameObject oldObj in oldGenerated)
        {
            if (Application.isPlaying) Destroy(oldObj);
            else DestroyImmediate(oldObj);
        }

        GameObject mapParent = new GameObject("Generated_Hospital_Map");
        mapParent.transform.SetParent(transform, false);
        mapParent.transform.localPosition = Vector3.zero;
        mapParent.transform.localRotation = Quaternion.identity;

        spawnedRooms.Clear();
        availableSpawnPoints.Clear();
        spawnedRooms.Clear();
        corridors.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 localCenterPos = new Vector3(x * tileSize, 0f, y * tileSize);
                Vector3 worldCenterPos = transform.position + localCenterPos;

                if (roomPositions.Contains(new Vector2Int(x, y)))
                {
                    // Instanciar suelo debajo de la habitacion especial (manteniendo la escala Y original del prefab para que no se eleve)
                    if (floorPrefab != null)
                    {
                        GameObject floor = Instantiate(floorPrefab, worldCenterPos, Quaternion.identity, mapParent.transform);
                        floor.transform.localScale = new Vector3(tileSize * 1.02f, floor.transform.localScale.y, tileSize * 1.02f);
                    }

                    // Instanciar SIEMPRE el techo plano estandar del laberinto sobre ambas celdas del cuarto para evitar agujeros
                    if (ceilingPrefab != null)
                    {
                        Vector3 localCeilingPos = localCenterPos + new Vector3(0f, ceilingHeight, 0f);
                        Vector3 worldCeilingPos = transform.position + localCeilingPos;
                        GameObject ceiling = Instantiate(ceilingPrefab, worldCeilingPos + Vector3.up * 0.02f, Quaternion.identity, mapParent.transform);
                        ceiling.transform.localScale = new Vector3(tileSize * 1.02f * ceilingScaleMultiplier, ceiling.transform.localScale.y, tileSize * 1.02f * ceilingScaleMultiplier);
                    }

                    // Instanciacion de componentes de habitacion procedimental (Luz de Techo, Interruptor en Pared, Puerta)
                    if (roomPivots.Contains(new Vector2Int(x, y)))
                    {
                        // 1. Encontrar la celda trasera (backCell)
                        Vector2Int doorCell = roomDoors[new Vector2Int(x, y)];
                        Vector2Int backCell = new Vector2Int(x, y) + (new Vector2Int(x, y) - doorCell);

                        // 2. Colocar foco/luz de techo en el area intermedia de los 2 techos, pero desplazada 4m hacia adentro para no iluminar la pared superior de la puerta
                        Vector3 backCellCenter = transform.position + new Vector3(backCell.x * tileSize, 0f, backCell.y * tileSize);
                        Vector3 roomBackDir = (backCellCenter - worldCenterPos).normalized;
                        Vector3 roomMidpoint = (worldCenterPos + backCellCenter) * 0.5f;
                        Vector3 lightPos = roomMidpoint + roomBackDir * 1.0f + new Vector3(0f, ceilingHeight, 0f);
                        GameObject lightObj = null;
                        Light roomLight = null;

                        if (ceilingLightPrefab != null)
                        {
                            lightObj = Instantiate(ceilingLightPrefab, lightPos, Quaternion.identity, mapParent.transform);
                            lightObj.transform.localScale = Vector3.one * mapScale;
                            
                            // Centrar descendientes
                            foreach (Transform t in lightObj.GetComponentsInChildren<Transform>())
                            {
                                if (t != lightObj.transform)
                                {
                                    t.localPosition = new Vector3(0f, t.localPosition.y, 0f);
                                }
                            }

                            roomLight = EnsureLightComponent(lightObj, new Color(1.0f, 0.96f, 0.9f), 3.5f, tileSize * 1.8f);
                        }

                        if (roomLight == null)
                        {
                            // Luz de respaldo procedural si no hay prefab
                            GameObject dLight = new GameObject("ProceduralRoomLight");
                            dLight.transform.position = lightPos;
                            dLight.transform.SetParent(mapParent.transform);
                            roomLight = dLight.AddComponent<Light>();
                            roomLight.type = LightType.Point;
                            roomLight.color = new Color(1f, 0.95f, 0.85f);
                            roomLight.intensity = 3.0f;
                            roomLight.range = tileSize * 1.8f;
                            roomLight.shadows = LightShadows.Soft;
                        }

                        // 3. Crear la pared divisoria procedimental con el marco de la puerta
                        SpawnProceduralWallWithDoorway(new Vector2Int(x, y), doorCell, mapParent.transform);

                        // 4. Crear interruptor de luz en la pared junto al marco de la puerta (lado derecho, visto desde el pasillo)
                        Vector3 doorCellCenter = new Vector3(doorCell.x * tileSize, 0f, doorCell.y * tileSize);
                        Vector3 boundaryCenter = (worldCenterPos + transform.position + doorCellCenter) * 0.5f;
                        Vector3 boundaryWorldPos = boundaryCenter;

                        Vector3 forwardDir = (transform.position + doorCellCenter - worldCenterPos).normalized;
                        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

                        // Interruptor al lado de la puerta de 2m de ancho
                        

                        // Calcular posicion del interruptor pegada a la pared exterior del cuarto (eje Z ajustado por mapScale)
                        float wallDepthOffset = 0.05f * mapScale; // Alineado con el grosor de la pared
                        Vector3 switchPos = boundaryWorldPos + rightDir * 3.4f + Vector3.up * (1.15f * mapScale) - forwardDir * (wallDepthOffset + 0.01f);

                        // Crear el objeto raiz del Interruptor Procedural Premium a escala humana real
                        GameObject switchObj = new GameObject("LightSwitch_Procedural");
                        switchObj.transform.position = switchPos;
                        switchObj.transform.rotation = Quaternion.LookRotation(-forwardDir, Vector3.up);
                        switchObj.transform.SetParent(mapParent.transform, true);

                        // Placa trasera (Backplate) - Plastico blanco marfil limpio
                        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        plate.name = "Backplate";
                        plate.transform.SetParent(switchObj.transform, false);
                        plate.transform.localPosition = Vector3.zero;
                        plate.transform.localScale = new Vector3(0.06f, 0.08f, 0.015f) * mapScale;
                        
                        Material plateMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        plateMat.color = new Color(0.92f, 0.92f, 0.90f); // Blanco marfil
                        plateMat.SetFloat("_Smoothness", 0.2f);
                        plate.GetComponent<Renderer>().sharedMaterial = plateMat;
                        Destroy(plate.GetComponent<Collider>()); // Evitar colisiones individuales

                        // Boton de encendido (Toggle Button) - Plastico gris claro
                        GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        button.name = "Toggle_Button";
                        button.transform.SetParent(switchObj.transform, false);
                        button.transform.localPosition = new Vector3(0f, 0f, 0.01f * mapScale); // Z positivo para que este al frente
                        button.transform.localScale = new Vector3(0.015f, 0.03f, 0.012f) * mapScale;

                        Material buttonMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        buttonMat.color = new Color(0.75f, 0.75f, 0.75f); // Gris claro
                        buttonMat.SetFloat("_Smoothness", 0.1f);
                        button.GetComponent<Renderer>().sharedMaterial = buttonMat;
                        Destroy(button.GetComponent<Collider>()); // Evitar colisiones individuales

                        // Anadir BoxCollider general
                        BoxCollider col = switchObj.AddComponent<BoxCollider>();
                        col.size = new Vector3(0.06f, 0.08f, 0.025f) * mapScale;

                        LightSwitch lightSwitch = switchObj.AddComponent<LightSwitch>();
                        lightSwitch.lightToToggle = roomLight;
                        lightSwitch.isOn = false;
                        lightSwitch.interactionDistance = 7.0f; // Distancia amplia para que se alcance facil
                        lightSwitch.switchSound = Resources.Load<AudioClip>("Interruptor");

                        // 5. Si NO es la oficina del director, spawnear puerta interactiva normal de tamano humano.
                        // Si ES la oficina del director, instanciar su puerta cerrada con keypad especial.
                        bool isDirector = (new Vector2Int(x, y) == roomPivots[roomPivots.Count - 1]);
                        if (!isDirector)
                        {
                            SpawnRoomDoor(new Vector2Int(x, y), doorCell, false);
                        }
                        else
                        {
                            // Spawnear la puerta bloqueada procedimental del director
                            GameObject doorHinge = SpawnRoomDoor(new Vector2Int(x, y), doorCell, true);
                            doorHinge.name = "PuertaDirector_Hinge";
                            var targetDoor = doorHinge.GetComponent<ProceduralDoorInteract>();

                            // Calcular posicion del keypad al lado izquierdo de la puerta (visto desde el pasillo)
                            Vector3 cellCenter = worldCenterPos;
                            Vector3 kpDoorCellCenter = transform.position + new Vector3(doorCell.x * tileSize, 0f, doorCell.y * tileSize);
                            Vector3 kpBoundaryWorldPos = (cellCenter + kpDoorCellCenter) * 0.5f;

                            Vector3 kpForwardDir = (kpDoorCellCenter - cellCenter).normalized;
                            Vector3 kpRightDir = Vector3.Cross(Vector3.up, kpForwardDir).normalized;

                            // Altura del keypad ajustada exactamente al pomo de la puerta escalado (1.15f * mapScale) y pegado a la pared
                            float keypadWallOffset = 0.05f * mapScale;
                            Vector3 keypadPos = kpBoundaryWorldPos - kpRightDir * 3.4f + Vector3.up * (1.15f * mapScale) + kpForwardDir * (keypadWallOffset + 0.01f);

                            // Crear el objeto raiz del Keypad Premium Procedural
                            GameObject keypadRoot = new GameObject("KeypadOficina");
                            keypadRoot.transform.position = keypadPos;
                            keypadRoot.transform.rotation = Quaternion.LookRotation(kpForwardDir, Vector3.up);
                            keypadRoot.transform.SetParent(mapParent.transform, true);

                            // Carcasa trasera (Housing)
                            GameObject backplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            backplate.name = "Housing";
                            backplate.transform.SetParent(keypadRoot.transform, false);
                            backplate.transform.localPosition = Vector3.zero;
                            backplate.transform.localScale = new Vector3(0.08f, 0.15f, 0.04f) * mapScale;
                            
                            Material housingMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            housingMat.color = new Color(0.12f, 0.12f, 0.13f); // Gris metalico oscuro
                            housingMat.SetFloat("_Metallic", 0.8f);
                            housingMat.SetFloat("_Smoothness", 0.6f);
                            backplate.GetComponent<Renderer>().sharedMaterial = housingMat;
                            Destroy(backplate.GetComponent<Collider>());

                            // Bisel exterior de la pantalla (Screen Bezel) - Negro piano
                            GameObject bezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            bezel.name = "Screen_Bezel";
                            bezel.transform.SetParent(keypadRoot.transform, false);
                            bezel.transform.localPosition = new Vector3(0f, 0.04f * mapScale, 0.0205f * mapScale);
                            bezel.transform.localScale = new Vector3(0.066f, 0.036f, 0.003f) * mapScale;
                            
                            Material bezelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            bezelMat.color = new Color(0.05f, 0.05f, 0.05f); // Negro
                            bezelMat.SetFloat("_Smoothness", 0.9f);
                            bezel.GetComponent<Renderer>().sharedMaterial = bezelMat;
                            Destroy(bezel.GetComponent<Collider>());

                            // Pantalla (Display)
                            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            screen.name = "Display";
                            screen.transform.SetParent(keypadRoot.transform, false);
                            screen.transform.localPosition = new Vector3(0f, 0.04f * mapScale, 0.021f * mapScale);
                            screen.transform.localScale = new Vector3(0.06f, 0.03f, 0.003f) * mapScale;

                            Material screenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            screenMat.color = new Color(0f, 0.6f, 0.8f); // Cyan brillante
                            screenMat.EnableKeyword("_EMISSION");
                            screenMat.SetColor("_EmissionColor", new Color(0f, 0.35f, 0.45f)); // Brillo
                            screen.GetComponent<Renderer>().sharedMaterial = screenMat;
                            Destroy(screen.GetComponent<Collider>());

                            // LED Rojo (Bloqueado)
                            GameObject ledRed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            ledRed.name = "StatusLED_Red";
                            ledRed.transform.SetParent(keypadRoot.transform, false);
                            ledRed.transform.localPosition = new Vector3(0.025f * mapScale, 0.04f * mapScale, 0.021f * mapScale);
                            ledRed.transform.localScale = Vector3.one * 0.012f * mapScale;

                            Material ledRedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            ledRedMat.color = Color.red;
                            ledRedMat.EnableKeyword("_EMISSION");
                            ledRedMat.SetColor("_EmissionColor", Color.red * 0.8f);
                            ledRed.GetComponent<Renderer>().sharedMaterial = ledRedMat;
                            Destroy(ledRed.GetComponent<Collider>());

                            // LED Verde (Desbloqueado)
                            GameObject ledGreen = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            ledGreen.name = "StatusLED_Green";
                            ledGreen.transform.SetParent(keypadRoot.transform, false);
                            ledGreen.transform.localPosition = new Vector3(0.025f * mapScale, 0.025f * mapScale, 0.021f * mapScale);
                            ledGreen.transform.localScale = Vector3.one * 0.012f * mapScale;

                            Material ledGreenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            ledGreenMat.color = new Color(0.1f, 0.1f, 0.1f); // Apagado por defecto
                            ledGreen.GetComponent<Renderer>().sharedMaterial = ledGreenMat;
                            Destroy(ledGreen.GetComponent<Collider>());

                            // Teclas Numericas (Rejilla de 3x4 con diseno mejorado)
                            float startX = -0.022f * mapScale;
                            float stepX = 0.022f * mapScale;
                            float startY = 0.01f * mapScale;
                            float stepY = -0.024f * mapScale;
                            int btnIndex = 1;
                            
                            for (int r = 0; r < 4; r++)
                            {
                                for (int c = 0; c < 3; c++)
                                {
                                    GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                    btn.name = "Button_" + btnIndex++;
                                    btn.transform.SetParent(keypadRoot.transform, false);
                                    btn.transform.localPosition = new Vector3(startX + c * stepX, startY + r * stepY, 0.021f * mapScale);
                                    btn.transform.localScale = new Vector3(0.013f, 0.016f, 0.005f) * mapScale;

                                    Material btnMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                    
                                    // Colorear teclas especiales (* en rojo, # en azul)
                                    if (btnIndex - 1 == 10) // Fila 4 Col 1 (*)
                                    {
                                        btnMat.color = new Color(0.85f, 0.2f, 0.2f); // Rojo
                                    }
                                    else if (btnIndex - 1 == 12) // Fila 4 Col 3 (#)
                                    {
                                        btnMat.color = new Color(0.2f, 0.5f, 0.85f); // Azul
                                    }
                                    else
                                    {
                                        btnMat.color = new Color(0.85f, 0.85f, 0.85f); // Botones blancos/grisaceos estandar
                                    }
                                    
                                    btnMat.SetFloat("_Smoothness", 0.1f);
                                    btn.GetComponent<Renderer>().sharedMaterial = btnMat;
                                    Destroy(btn.GetComponent<Collider>());
                                }
                            }

                            // Anadir BoxCollider general al Keypad raiz para interaccion limpia
                            BoxCollider keypadCol = keypadRoot.AddComponent<BoxCollider>();
                            keypadCol.size = new Vector3(0.08f, 0.15f, 0.04f) * mapScale;

                            // Crear TextMesh en la pantalla 3D
                            GameObject textObj = new GameObject("DisplayText");
                            textObj.transform.SetParent(keypadRoot.transform, false);
                            textObj.transform.localPosition = new Vector3(0f, 0.04f * mapScale, 0.023f * mapScale);
                            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Rotar 180 en Y para que sea legible al frente
                            textObj.transform.localScale = new Vector3(0.0022f, 0.0028f, 0.003f) * mapScale; // Ajustar escala para que quepa

                            TextMesh tm = textObj.AddComponent<TextMesh>();
                            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                            tm.font = font;
                            if (font != null) tm.GetComponent<Renderer>().sharedMaterial = font.material;
                            tm.fontSize = 36;
                            tm.alignment = TextAlignment.Center;
                            tm.anchor = TextAnchor.MiddleCenter;
                            tm.color = new Color(0f, 1f, 0.8f);
                            tm.text = "LOCKED";

                            // Vincular el controlador original con rango correcto
                            var kp = keypadRoot.AddComponent<KeypadController>();
                            kp.correctCode = correctKeypadCode;
                            kp.targetProceduralDoor = targetDoor;
                            kp.interactDistance = 7.0f; // Rango escalado y comodo
                            kp.screenText = tm;
                            kp.ledRedRenderer = ledRed.GetComponent<Renderer>();
                            kp.ledGreenRenderer = ledGreen.GetComponent<Renderer>();

                            Debug.Log("MazeGenerator: Keypad Oficina Creado anticipadamente en BuildPhysicalMap en altura " + keypadPos.y);
                        }
                    }

                    // Levantar paredes normales del laberinto alrededor de la habitacion (excepto en la puerta y en medio de ella)
                    CheckAndSpawnWalls(x, y, mapParent.transform);
                }
                else
                {
                    // Suelo en pasillos y paredes (2% de solape extra para cerrar costuras)
                    if (floorPrefab != null)
                    {
                        GameObject floor = Instantiate(floorPrefab, worldCenterPos, Quaternion.identity, mapParent.transform);
                        floor.transform.localScale = new Vector3(tileSize * 1.02f, floor.transform.localScale.y, tileSize * 1.02f);
                    }

                    // Instanciar techo continuo (evita huecos sobre pasillos y paredes)
                    if (ceilingPrefab != null)
                    {
                        Vector3 localCeilingPos = localCenterPos + new Vector3(0f, ceilingHeight, 0f);
                        Vector3 worldCeilingPos = transform.position + localCeilingPos;

                        // Instanciar SIEMPRE el techo estandar para evitar huecos en la estructura del techo
                        GameObject ceiling = Instantiate(ceilingPrefab, worldCeilingPos, Quaternion.identity, mapParent.transform);
                        ceiling.transform.localScale = new Vector3(tileSize * 1.02f * ceilingScaleMultiplier, ceiling.transform.localScale.y, tileSize * 1.02f * ceilingScaleMultiplier);

                        // Si hay prefab de lámpara asignado (P_Lamp), instanciar la lámpara colgando del techo como objeto independiente
                        if (grid[x, y] && !IsAdjacentToRoom(x, y) && ceilingLightPrefab != null && Random.value < lightProbability)
                        {
                            Quaternion testRot = Quaternion.identity;
                            bool hasHorizontal = (x - 1 >= 0 && grid[x - 1, y]) || (x + 1 < width && grid[x + 1, y]);
                            bool hasVertical = (y - 1 >= 0 && grid[x, y - 1]) || (y + 1 < height && grid[x, y + 1]);
                            if (hasHorizontal && !hasVertical)
                            {
                                testRot = Quaternion.Euler(0, 90, 0);
                            }

                            if (!WouldLightClip(x, y, testRot) && !IsAnyLightTooClose(x, y, 3))
                            {
                                GameObject lampObj = Instantiate(ceilingLightPrefab, worldCeilingPos, testRot, mapParent.transform);
                                lampObj.transform.localScale = Vector3.one * mapScale;

                                Transform[] lightChildren = lampObj.GetComponentsInChildren<Transform>(true);
                                foreach (Transform t in lightChildren)
                                {
                                    if (t != lampObj.transform)
                                    {
                                        if (Mathf.Abs(t.localPosition.x) > 0.5f || Mathf.Abs(t.localPosition.z) > 0.5f)
                                        {
                                            t.localPosition = new Vector3(0f, t.localPosition.y, 0f);
                                        }
                                    }
                                }

                                EnsureLightComponent(lampObj, new Color(0.95f, 0.95f, 1.0f), 3.0f, tileSize * 1.5f);
                            }
                        }
                    }

                    if (grid[x, y])
                    {
                        corridors.Add(new Vector2Int(x, y));

                        // Levantar paredes en los bordes internos
                        CheckAndSpawnWalls(x, y, mapParent.transform);
                    }
                }
            }
        }

        // Levantar el permetro exterior de seguridad alrededor de todo el mapa (Desactivado para evitar la doble muralla)
        // SpawnMapPerimeterWalls(mapParent.transform);
        SpawnBeds();
    }

    void AlignRoomRotation(GameObject room, Vector2Int cell)
    {
        // Correccion de mapeo de angulos para prefabricados con puerta en el eje Sur
        if (cell.y - 1 >= 0 && grid[cell.x, cell.y - 1]) // Sur -> Mirar al Sur (Rotacion 0)
        {
            room.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (cell.x + 1 < width && grid[cell.x + 1, cell.y]) // Este -> Mirar al Este (Rotacion -90)
        {
            room.transform.rotation = Quaternion.Euler(0, -90, 0);
        }
        else if (cell.y + 1 < height && grid[cell.x, cell.y + 1]) // Norte -> Mirar al Norte (Rotacion 180)
        {
            room.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        else if (cell.x - 1 >= 0 && grid[cell.x - 1, cell.y]) // Oeste -> Mirar al Oeste (Rotacion 90)
        {
            room.transform.rotation = Quaternion.Euler(0, 90, 0);
        }
    }

    bool IsWallOrRoom(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return true;
        return !grid[x, y] || roomPositions.Contains(new Vector2Int(x, y));
    }

    bool WouldLightClip(int x, int y, Quaternion rotation)
    {
        // Si la lmpara corre de Este a Oeste (rotacin de 90 grados)
        if (Mathf.Approximately(rotation.eulerAngles.y, 90f))
        {
            // Hay pared inmediatamente al Este o al Oeste de este pasillo
            if (IsWallOrRoom(x + 1, y) || IsWallOrRoom(x - 1, y))
                return true;
        }
        else // Si la lmpara corre de Norte a Sur (rotacin de 0 o 180 grados)
        {
            // Hay pared inmediatamente al Norte o al Sur de este pasillo
            if (IsWallOrRoom(x, y + 1) || IsWallOrRoom(x, y - 1))
                return true;
        }
        return false;
    }

    void SpawnMapPerimeterWalls(Transform parent)
    {
        if (wallPrefab == null) return;

        float halfTile = tileSize / 2f;

        // Paredes del permetro Norte y Sur (cierran el mapa completo por arriba y por abajo)
        for (int x = 0; x < width; x++)
        {
            // Borde Norte (arriba) de la ltima fila (height - 1)
            Vector3 wallPosN = transform.position + new Vector3(x * tileSize, 0, (height - 1) * tileSize + halfTile);
            SpawnWallAt(wallPosN, Quaternion.Euler(0, 0, 0), parent);

            // Borde Sur (abajo) de la primera fila (0)
            Vector3 wallPosS = transform.position + new Vector3(x * tileSize, 0, 0 * tileSize - halfTile);
            SpawnWallAt(wallPosS, Quaternion.Euler(0, 180, 0), parent);
        }

        // Paredes del permetro Este y Oeste (cierran el mapa completo por la derecha y la izquierda)
        for (int y = 0; y < height; y++)
        {
            // Borde Este (derecha) de la ltima columna (width - 1)
            Vector3 wallPosE = transform.position + new Vector3((width - 1) * tileSize + halfTile, 0, y * tileSize);
            SpawnWallAt(wallPosE, Quaternion.Euler(0, 90, 0), parent);

            // Borde Oeste (izquierda) de la primera columna (0)
            Vector3 wallPosW = transform.position + new Vector3(0 * tileSize - halfTile, 0, y * tileSize);
            SpawnWallAt(wallPosW, Quaternion.Euler(0, -90, 0), parent);
        }
    }

    Vector2Int GetRoomPivotForBackCell(Vector2Int backCell)
    {
        foreach (var pivot in roomPivots)
        {
            Vector2Int door = GetRoomDoorCell(pivot);
            Vector2Int dir = pivot - door;
            if (pivot + dir == backCell) return pivot;
        }
        return backCell;
    }

    bool IsRoomTooClose(Vector2Int cell, int minDistance = 2)
    {
        foreach (var pos in roomPositions)
        {
            if (Mathf.Abs(pos.x - cell.x) <= minDistance && Mathf.Abs(pos.y - cell.y) <= minDistance)
                return true;
        }
        return false;
    }

    bool IsAnyLightTooClose(int x, int y, int minDistance)
    {
        foreach (var pos in spawnedRooms)
        {
            if (Mathf.Abs(pos.x - x) + Mathf.Abs(pos.y - y) < minDistance)
                return true;
        }
        return false;
    }

    bool IsAdjacentToRoom(int x, int y)
    {
        Vector2Int[] dirs = {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };
        foreach (var dir in dirs)
        {
            if (roomPositions.Contains(new Vector2Int(x + dir.x, y + dir.y)))
                return true;
        }
        return false;
    }

    bool ShouldSpawnWallBetween(int cx, int cy, int nx, int ny)
    {
        // Si esta fuera del mapa, debe haber pared
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return true;

        Vector2Int cell = new Vector2Int(cx, cy);
        Vector2Int neighbor = new Vector2Int(nx, ny);

        // Evitar ABSOLUTAMENTE colocar una pared en frente de las puertas del ascensor
        if ((cx == elevatorCell.x && cy == elevatorCell.y && nx == elevatorFrontCell.x && ny == elevatorFrontCell.y) ||
            (nx == elevatorCell.x && ny == elevatorCell.y && cx == elevatorFrontCell.x && cy == elevatorFrontCell.y))
        {
            return false;
        }

        // Si ambos son celdas de habitacion especial, comprobar si perteneón al mismo modulo
        if (roomPositions.Contains(cell) && roomPositions.Contains(neighbor))
        {
            // No colocar pared divisoria entre la celda principal y trasera del mismo modulo de cuarto
            Vector2Int pivotC = roomPivots.Contains(cell) ? cell : GetRoomPivotForBackCell(cell);
            Vector2Int pivotN = roomPivots.Contains(neighbor) ? neighbor : GetRoomPivotForBackCell(neighbor);
            if (pivotC == pivotN) return false;
            return true; // Si son de habitaciones distintas, debe haber pared divisoria
        }

        // Si uno es habitacin y el otro es pasillo, colocar pared en las caras traseras y laterales (no en la puerta)
        if (roomPositions.Contains(neighbor))
        {
            return !IsDoorCellForRoom(cell, neighbor);
        }
        if (roomPositions.Contains(cell))
        {
            return !IsDoorCellForRoom(neighbor, cell);
        }

        // Si el vecino es una pared solida (y no es habitacion)
        if (!grid[nx, ny]) return true;

        return false;
    }

    bool IsDoorCellForRoom(Vector2Int corridorCell, Vector2Int roomCell)
    {
        // La celda trasera de la habitacin no tiene puerta, solo el pivote (entrada) tiene
        if (!roomPivots.Contains(roomCell)) return false;

        return GetRoomDoorCell(roomCell) == corridorCell;
    }

    Vector2Int GetRoomDoorCell(Vector2Int cell)
    {
        if (roomDoors.ContainsKey(cell))
        {
            return roomDoors[cell];
        }
        if (cell.y - 1 >= 0 && grid[cell.x, cell.y - 1]) return new Vector2Int(cell.x, cell.y - 1); // Sur
        if (cell.x + 1 < width && grid[cell.x + 1, cell.y]) return new Vector2Int(cell.x + 1, cell.y); // Este
        if (cell.y + 1 < height && grid[cell.x, cell.y + 1]) return new Vector2Int(cell.x, cell.y + 1); // Norte
        if (cell.x - 1 >= 0 && grid[cell.x - 1, cell.y]) return new Vector2Int(cell.x - 1, cell.y); // Oeste
        return cell;
    }

    void CheckAndSpawnWalls(int x, int y, Transform parent)
    {
        if (wallPrefab == null) return;

        float halfTile = tileSize / 2f;
        Vector3 cellCenter = new Vector3(x * tileSize, 0, y * tileSize);

        // Norte (y + 1)
        if (ShouldSpawnWallBetween(x, y, x, y + 1))
        {
            Vector3 localPos = cellCenter + new Vector3(0, 0, halfTile);
            Vector3 worldPos = transform.position + localPos;
            SpawnWallAt(worldPos, Quaternion.Euler(0, 0, 0), parent);
        }
        // Sur (y - 1)
        if (ShouldSpawnWallBetween(x, y, x, y - 1))
        {
            Vector3 localPos = cellCenter + new Vector3(0, 0, -halfTile);
            Vector3 worldPos = transform.position + localPos;
            SpawnWallAt(worldPos, Quaternion.Euler(0, 180, 0), parent);
        }
        // Este (x + 1)
        if (ShouldSpawnWallBetween(x, y, x + 1, y))
        {
            Vector3 localPos = cellCenter + new Vector3(halfTile, 0, 0);
            Vector3 worldPos = transform.position + localPos;
            SpawnWallAt(worldPos, Quaternion.Euler(0, 90, 0), parent);
        }
        // Oeste (x - 1)
        if (ShouldSpawnWallBetween(x, y, x - 1, y))
        {
            Vector3 localPos = cellCenter + new Vector3(-halfTile, 0, 0);
            Vector3 worldPos = transform.position + localPos;
            SpawnWallAt(worldPos, Quaternion.Euler(0, -90, 0), parent);
        }
    }

    void SpawnWallAt(Vector3 position, Quaternion rotation, Transform parent)
    {
        Vector3 key = RoundVector(position);
        if (spawnedWalls.Contains(key)) return; // Evitar duplicar paredes

        // Direccion hacia adelante local de la pared para separarlas minimamente y evitar Z-fighting (pixeles buggeados)
        Vector3 forwardDir = rotation * Vector3.forward;
        float shift = 0.01f * mapScale; // Separacin imperceptible para el jugador pero efectiva para la tarjeta grfica

        float wallH = ceilingHeight > 0.1f ? ceilingHeight : 2.9f;

        // Instanciar pared frontal
        GameObject wall1 = Instantiate(wallPrefab, position + forwardDir * shift, rotation, parent);
        wall1.transform.localScale = new Vector3(tileSize * 1.02f, wallH, mapScale);

        // Instanciar pared trasera (girada 180 grados)
        Quaternion backRotation = rotation * Quaternion.Euler(0, 180, 0);
        GameObject wall2 = Instantiate(wallPrefab, position - forwardDir * shift, backRotation, parent);
        wall2.transform.localScale = new Vector3(tileSize * 1.02f, wallH, mapScale);

        spawnedWalls.Add(key);

        // --- SISTEMA DE GRAFFITI / PINTADAS DE SUPERVIVIENTES PROCEDIMENTALES ---
        // Spawnear aleatoriamente en algunas paredes frontales (tasa del 12% para no saturar visualmente)
        if (UnityEngine.Random.value < 0.12f && !isMenuMode)
        {
            string[] graffitiTexts = {
                "DON'T LOOK BACK",
                "HELP ME",
                "EXIT ¢Ãƒâ€¦¾ÃƒÂ¢Ã¢â€šÂ¬",
                "IT IS CLOSE",
                "SOMETHING IS HERE",
                "DON'T STOP",
                "TURN HERE",
                "THE ELEVATOR IS THE EXIT",
                "N",
                "S",
                "E",
                "W",
                "DEATH HOSPITAL",
                "DANGER"
            };

            string chosenText = graffitiTexts[UnityEngine.Random.Range(0, graffitiTexts.Length)];

            // Crear un objeto 3D TextMesh pegado a la superficie de la pared
            GameObject graffitiObj = new GameObject("Procedural_Graffiti");
            graffitiObj.transform.SetParent(wall1.transform, false);
            
            // Posicionar al frente de la pared frontal (Z positivo con un offset minimo)
            graffitiObj.transform.localPosition = new Vector3(0f, UnityEngine.Random.Range(-0.3f, 0.4f), 0.01f);
            
            // Girar 180 grados en Y para que mire al pasillo y no al interior sólido de la pared
            graffitiObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            TextMesh tm = graffitiObj.AddComponent<TextMesh>();
            tm.text = chosenText;
            tm.fontSize = 32;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            
            // Configurar el color rojo sangre o negro carbon
            Color textColor = UnityEngine.Random.value < 0.7f ? new Color(0.55f, 0.05f, 0.05f) : new Color(0.1f, 0.1f, 0.1f);
            tm.color = textColor;

            // --- CORRECCION DE TRANSPARENCIA A TRAVES DE PAREDES (OCLUSION REAL) ---
            // Por defecto, el TextMesh de Unity usa un shader Overlay 3D que ignora la profundidad del Z-Buffer (se ve a traves de las paredes).
            // Le asignamos un shader Lit de URP estandar o cargamos la tipografia con un material opaco que respete el Z-Write.
            var mr = graffitiObj.GetComponent<Renderer>();
            if (mr != null)
            {
                // Cargar un material estandar de tipografia que si respete la oclusion de profundidad (Z-Test / Z-Write standard)
                Material occludedTextMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                occludedTextMat.color = textColor;
                
                // Habilitar transparencia de tipo Cutout (Alpha Clip) para no ver el cuadrado sólido de fondo
                occludedTextMat.SetFloat("_AlphaClip", 1.0f);
                occludedTextMat.SetFloat("_Cutoff", 0.1f);
                occludedTextMat.EnableKeyword("_ALPHATEST_ON");
                
                // Si la tipografia por defecto tiene una textura de mapa de caracteres, la enlazamos para que las letras se dibujen
                if (tm.font != null && tm.font.material != null && tm.font.material.mainTexture != null)
                {
                    occludedTextMat.SetTexture("_BaseMap", tm.font.material.mainTexture);
                }
                
                mr.sharedMaterial = occludedTextMat;
            }
        }
    }

    Vector3 RoundVector(Vector3 v)
    {
        return new Vector3(Mathf.Round(v.x * 10f) / 10f, Mathf.Round(v.y * 10f) / 10f, Mathf.Round(v.z * 10f) / 10f);
    }

                    void SpawnEntitiesAndItems()
    {
        if (corridors.Count < 3) return;

        // --- Agregar puntos de spawn en pasillos para dispersar items (pilas, fusibles) ---
        foreach (Vector2Int cell in corridors)
        {
            if (cell == generatorACell || cell == generatorBCell || cell == elevatorCell || roomPivots.Contains(cell) || bedCells.Contains(cell)) continue;
            
            // 20% de probabilidad de tener un punto de spawn en este pasillo (suficiente para que haya muchos en el mapa)
            if (UnityEngine.Random.value < 0.20f)
            {
                availableSpawnPoints.Add(new ItemSpawnPoint {
                    position = transform.position + new Vector3(cell.x * tileSize, 0.02f, cell.y * tileSize),
                    rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0),
                    type = SpawnPointType.Floor
                });
            }
        }

        // 1. Ubicar al Jugador en la celda de pasillo con mayor conectividad, garantizando distancia del ascensor y subgeneradores
        Vector2Int spawnCell = new Vector2Int(1, 1);
        int maxReachable = -1;
        float minRequiredDist = 7.0f; // Al menos 7 celdas de distancia (112 metros)
        
        // Intentar encontrar candidato que cumpla con el filtro de distancia
        bool foundSafeSpawn = false;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            float limit = attempt == 0 ? minRequiredDist : 4.0f; // Fallback si es un mapa muy pequeno o denso
            
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (grid[x, y] && !roomPositions.Contains(candidate))
                    {
                        // Evitar spawnear cerca de los generadores A, B o del ascensor
                        float distA = Vector2Int.Distance(candidate, generatorACell);
                        float distB = Vector2Int.Distance(candidate, generatorBCell);
                        float distElev = Vector2Int.Distance(candidate, elevatorCell);
                        
                        if (distA >= limit && distB >= limit && distElev >= limit)
                        {
                            int reachable = GetReachableCorridorCount(candidate);
                            if (reachable > maxReachable)
                            {
                                maxReachable = reachable;
                                spawnCell = candidate;
                                foundSafeSpawn = true;
                            }
                        }
                    }
                }
            }
            if (foundSafeSpawn) break;
        }
        Debug.Log("HospitalMazeGenerator: Jugador posicionado en celda " + spawnCell + " con conectividad de " + maxReachable + " celdas.");

        // Posicionamiento relativo seguro en Y = 0.5f, pero activado mediante Coroutine con retraso para evitar cada al vaco
        playerSpawnCell = spawnCell; // Guardar para usarlo en SpawnEnemyNearPlayer
        Vector3 playerPos = transform.position + new Vector3(spawnCell.x * tileSize, 1.2f, spawnCell.y * tileSize);
        if (playerObj != null)
        {
            if (isMenuMode)
            {
                // En modo menu, posicionar silenciosamente pero NO habilitar el controlador del jugador
                playerObj.transform.position = playerPos;
                if (Camera.main != null)
                {
                    Camera.main.transform.position = playerPos;
                }
            }
            else
            {
                if (GameManager.Instance == null)
                {
                    GameObject gmObj = new GameObject("GameManager");
                    gmObj.AddComponent<GameManager>();
                }
                GameManager.Instance.InicializarVidasParaMapa(3); // El hospital siempre tiene 3 vidas
                GameManager.Instance.RegistrarSpawnJugador(playerPos, playerObj.transform.rotation);

                StartCoroutine(EnablePlayerControllerDelayed(playerObj, playerPos));
                Debug.Log("MazeGenerator: Iniciado spawn seguro del jugador en " + playerPos);
            }
        }

        // 2. Generar puntos de patrullaje dinamicos distribuidos por el laberinto
        GeneratePatrolPoints();

        // 3. Ubicar al Monstruo cerca del jugador (SOLO si no estamos en modo menu para preservar la sorpresa)
        if (!isMenuMode)
        {
            SpawnEnemy(playerSpawnCell);
        }

        // 4. Colocar la Caja de Fusibles lejana al jugador y pegada a una pared real
        PlacePowerBox();

        // 5. Instanciar los Fusibles de repuesto en las habitaciones
        SpawnFuses();

        // 6. Instanciar las Pilas de repuesto en las habitaciones
        SpawnBatteries();

        // 7. Instanciar la Tarjeta de Acceso del Director
        SpawnKeycard();
    }

    // -----------------------------------------------------------------------
    // Genera N puntos de patrullaje distribuidos por el laberinto
    // La cantidad escala con el tamano del mapa: (width * height) / 30
    // -----------------------------------------------------------------------
    void GeneratePatrolPoints()
    {
        generatorCells.Clear();

        // Destruir puntos anteriores si los hay (si se regenera el mapa)
        GameObject oldContainer = GameObject.Find("PatrolPoints_Container");
        if (oldContainer != null) Destroy(oldContainer);

        GameObject container = new GameObject("PatrolPoints_Container");
        container.transform.SetParent(transform, false);

        // Cantidad dinamica: escala con el tamano del mapa, minimo 6, maximo 24
        int targetCount = Mathf.Clamp((width * height) / 30, 6, 24);

        // Barajar los corredores para seleccion aleatoria eficiente
        List<Vector2Int> shuffled = new List<Vector2Int>(corridors);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }

        List<Vector2Int> chosen = new List<Vector2Int>();
        foreach (Vector2Int cell in shuffled)
        {
            // Ignorar celdas de habitaciones especiales
            if (roomPositions.Contains(cell)) continue;

            // Verificar separacion minima con puntos ya elegidos
            bool tooClose = false;
            foreach (Vector2Int c in chosen)
            {
                if (Mathf.Abs(cell.x - c.x) + Mathf.Abs(cell.y - c.y) < minPatrolSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            chosen.Add(cell);
            if (chosen.Count >= targetCount) break;
        }

        // Crear un GameObject vacio por cada punto elegido
        for (int i = 0; i < chosen.Count; i++)
        {
            Vector3 worldPos = transform.position + new Vector3(chosen[i].x * tileSize, 0.1f, chosen[i].y * tileSize);
            GameObject pt = new GameObject("PatrolPoint_" + i);
            pt.transform.SetParent(container.transform, false);
            pt.transform.position = worldPos;
            // generatorCells.Add(pt.transform);
        }

        Debug.Log("MazeGenerator: Generados " + generatorCells.Count + " puntos de patrullaje (mapa " + width + "x" + height + " -> target=" + targetCount + ")");
    }

    // -----------------------------------------------------------------------
    // Coloca el enemigo en una celda valida a distancia media del jugador
    // -----------------------------------------------------------------------
    void SpawnEnemy(Vector2Int playerCell)
    {
        if (enemyObj == null) return;

        // Buscar celdas de corredor dentro del rango de distancia deseado
        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (Vector2Int cell in corridors)
        {
            if (roomPositions.Contains(cell)) continue;
            int dist = Mathf.Abs(cell.x - playerCell.x) + Mathf.Abs(cell.y - playerCell.y);
            // Enforzar un minimo de 6 celdas de distancia (96 metros) sin importar si en el Inspector se configuro un numero menor por accidente
            int safeMinDist = 6;
            if (dist >= safeMinDist)
            {
                candidates.Add(cell);
            }
        }

        // Fallback: si no hay candidatos en el rango, usar la celda mas alejada disponible
        Vector2Int spawnCell;
        if (candidates.Count > 0)
        {
            spawnCell = candidates[Random.Range(0, candidates.Count)];
        }
        else
        {
            spawnCell = new Vector2Int(width - 2, height - 2);
            Debug.LogWarning("MazeGenerator: No se encontro celda en rango para el enemigo. Usando esquina opuesta.");
        }

        Vector3 enemyPos = transform.position + new Vector3(spawnCell.x * tileSize, 0.05f, spawnCell.y * tileSize);
        enemyObj.transform.position = enemyPos;

        // Usar agent.Warp() para que el NavMeshAgent se posicione correctamente sobre el NavMesh
        UnityEngine.AI.NavMeshAgent agentWarp = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agentWarp != null && agentWarp.isActiveAndEnabled)
        {
            agentWarp.Warp(enemyPos);
        }

        // Escalar el enemigo con escala fija (la altura del corredor es fija, no depende de mapScale)
        // enemyScaleMultiplier = 1.0 -> tamano original del modelo
        float finalScale = enemyScaleMultiplier;  // NO multiplicar por mapScale
        enemyObj.transform.localScale = new Vector3(finalScale, finalScale, finalScale);

        // Ajustar el radio del NavMeshAgent segun la escala del enemigo
        // Radio mayor = el centro del enemigo se aleja mas de las paredes -> menos clipping de brazos/pies
        UnityEngine.AI.NavMeshAgent agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            // Limitar el radio a un maximo de 0.6f para que pase de forma fluida por puertas de 1.44m de ancho.
            agent.radius = Mathf.Min(0.6f, enemyNavMeshRadius);   
            agent.height = 2.0f * finalScale;
            agent.baseOffset = 0f;
            // agent.obstacleAvoidanceType = ...;
        }

        // Inyectar los puntos de patrullaje al controlador de IA
        MonoBehaviour controller = enemyObj.GetComponent<MonoBehaviour>();
        if (controller != null && generatorCells.Count > 0)
        {
            // controller.SetPatrolPoints(generatorCells.ToArray());
            Debug.Log("MazeGenerator: " + generatorCells.Count + " patrol points inyectados al enemigo.");
        }
        else if (controller == null)
        {
            Debug.LogWarning("MazeGenerator: EnemyController no encontrado en enemyObj.");
        }

        Physics.SyncTransforms();
        Debug.Log("MazeGenerator: Enemigo spawneado en " + enemyPos + " (dist=" + (Mathf.Abs(spawnCell.x - playerCell.x) + Mathf.Abs(spawnCell.y - playerCell.y)) + " celdas del jugador, escala=" + finalScale + ")");
    }

    void PlacePowerBox()
    {
        if (powerBoxObj == null) return;

        // Buscar celdas de pasillo lejanas al jugador y preferiblemente en los bordes para colocar la caja
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int i = 0; i < corridors.Count; i++)
        {
            Vector2Int cell = corridors[i];
            // Estar en la mitad lejana del mapa en al menos un eje y no en la celda del jugador (1,1)
            if (cell.x > width / 2 || cell.y > height / 2)
            {
                // EVITAR ABSOLUTAMENTE colocar la caja de fusibles en la celda del ascensor o de los subgeneradores
                if (cell != elevatorCell && (generatorCells == null || !generatorCells.Contains(cell)))
                {
                    if (HasAnyWall(cell.x, cell.y))
                    {
                        candidates.Add(cell);
                    }
                }
            }
        }

        Vector2Int targetCell = corridors[corridors.Count - 1]; // Fallback
        if (candidates.Count > 0)
        {
            targetCell = candidates[Random.Range(0, candidates.Count)];
        }

        // Posicionar contra la primera pared real encontrada
        Vector3 boxPos = transform.position + new Vector3(targetCell.x * tileSize, 1.5f * mapScale, targetCell.y * tileSize);
        Quaternion boxRot = Quaternion.identity;
        float halfTile = tileSize / 2f;
        float offset = 0.05f * mapScale; // Ajustado para que quede al ras de la pared sin flotar

        if (targetCell.y - 1 < 0 || (!grid[targetCell.x, targetCell.y - 1] && !roomPositions.Contains(new Vector2Int(targetCell.x, targetCell.y - 1))))
        {
            // Pared Sur -> Mirar al Norte
            boxPos.z -= (halfTile - offset);
            boxRot = Quaternion.Euler(0, 0, 0);
        }
        else if (targetCell.y + 1 >= height || (!grid[targetCell.x, targetCell.y + 1] && !roomPositions.Contains(new Vector2Int(targetCell.x, targetCell.y + 1))))
        {
            // Pared Norte -> Mirar al Sur
            boxPos.z += (halfTile - offset);
            boxRot = Quaternion.Euler(0, 180, 0);
        }
        else if (targetCell.x - 1 < 0 || (!grid[targetCell.x - 1, targetCell.y] && !roomPositions.Contains(new Vector2Int(targetCell.x - 1, targetCell.y))))
        {
            // Pared Oeste -> Mirar al Este
            boxPos.x -= (halfTile - offset);
            boxRot = Quaternion.Euler(0, 90, 0);
        }
        else if (targetCell.x + 1 >= width || (!grid[targetCell.x + 1, targetCell.y] && !roomPositions.Contains(new Vector2Int(targetCell.x + 1, targetCell.y))))
        {
            // Pared Este -> Mirar al Oeste
            boxPos.x += (halfTile - offset);
            boxRot = Quaternion.Euler(0, -90, 0);
        }

        powerBoxObj.transform.position = boxPos;
        powerBoxObj.transform.rotation = boxRot;
        powerBoxObj.transform.localScale = Vector3.one * mapScale;
        Debug.Log("MazeGenerator: Caja de Fusibles colocada en pared de la celda " + targetCell + " en " + boxPos);
    }

    bool HasAnyWall(int x, int y)
    {
        if (y - 1 < 0 || (!grid[x, y - 1] && !roomPositions.Contains(new Vector2Int(x, y - 1)))) return true;
        if (y + 1 >= height || (!grid[x, y + 1] && !roomPositions.Contains(new Vector2Int(x, y + 1)))) return true;
        if (x - 1 < 0 || (!grid[x - 1, y] && !roomPositions.Contains(new Vector2Int(x - 1, y)))) return true;
        if (x + 1 >= width || (!grid[x + 1, y] && !roomPositions.Contains(new Vector2Int(x + 1, y)))) return true;
        return false;
    }

    private System.Collections.IEnumerator EnablePlayerControllerDelayed(GameObject player, Vector3 targetPos)
    {
        HideUnderBed autoHide = player.GetComponentInChildren<HideUnderBed>();
        if (autoHide == null)
        {
            player.AddComponent<HideUnderBed>();
        }

        CharacterController cc = player.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        MonoBehaviour fpc = player.GetComponent("FirstPersonController") as MonoBehaviour;
        if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc != null) fpc.enabled = false;

        Transform playerCapsule = player.transform.Find("PlayerCapsule");
        if (playerCapsule == null)
        {
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PlayerCapsule") { playerCapsule = t; break; }
            }
        }
        if (playerCapsule != null)
        {
            playerCapsule.localPosition = Vector3.zero;
        }

        player.transform.position = targetPos;
        Physics.SyncTransforms();

        for (int i = 0; i < 15; i++)
        {
            yield return null;
        }

        if (cc != null)
        {
            cc.enabled = true;
            Physics.SyncTransforms();
        }
        if (fpc != null)
        {
            fpc.enabled = true;
        }
        Debug.Log("MazeGenerator: Player re-habilitado con seguridad sobre el suelo firme en " + targetPos);
    }


    // Llamado por EnemyActivator cada vez que el enemigo se reactiva tras un apagon
    // Reposiciona al enemigo en una celda aleatoria cercana al jugador
    public void RespawnEnemyNearPlayer()
    {
        if (corridors.Count == 0) return; // El mapa aun no esta generado
        SpawnEnemy(playerSpawnCell);
        Debug.Log("MazeGenerator: Enemigo reposicionado cerca del jugador por apagon.");
    }

    void SpawnFuses()
    {
        activeFuses.Clear();
        
        int count = fusesToSpawn;
        if (count <= 0)
        {
            // Escalar dinamicamente: 1 fusible por cada 80 celdas
            // 15x15 = ~3 fusibles
            // 25x25 = ~7 fusibles
            count = Mathf.Max(2, (width * height) / 80);
        }

        Debug.Log("MazeGenerator: Generando " + count + " fusibles de repuesto en el mapa.");
        for (int i = 0; i < count; i++)
        {
            SpawnFuseRandom();
        }
    }

    public void SpawnFuseRandom()
    {
        if (fusePrefab == null) return;
        activeFuses.RemoveAll(item => item == null);

        // Buscar puntos de tipo Floor o Desk
        System.Collections.Generic.List<ItemSpawnPoint> validPoints = new System.Collections.Generic.List<ItemSpawnPoint>();
        foreach (var pt in availableSpawnPoints)
        {
            if (pt.type == SpawnPointType.Floor || pt.type == SpawnPointType.Desk || pt.type == SpawnPointType.ToiletTank)
            {
                validPoints.Add(pt);
            }
        }

        if (validPoints.Count > 0)
        {
            ItemSpawnPoint chosen = validPoints[UnityEngine.Random.Range(0, validPoints.Count)];
            availableSpawnPoints.Remove(chosen);
            // Evitar aglomeracion: remover todos los puntos en un radio de 3.5 metros (max 1 o 2 items por cuarto pequeno)
            availableSpawnPoints.RemoveAll(pt => Vector3.Distance(pt.position, chosen.position) < 3.5f);

            GameObject fuse = Instantiate(fusePrefab, chosen.position, chosen.rotation);
            fuse.name = "FusibleItem_" + activeFuses.Count;
            fuse.transform.SetParent(GetItemsParent("Fuses"));
            if (fuse.GetComponent<FuseItem>() == null)
            {
                fuse.AddComponent<FuseItem>();
            }
            activeFuses.Add(fuse);
        }
    }


    // -----------------------------------------------------------------------
    // METODOS PARA SPAWNEAR PILAS/BATERÍAS
    // -----------------------------------------------------------------------
    void SpawnBatteries()
    {
        activeBatteries.Clear();
        
        int count = batteriesToSpawn;
        if (count <= 0)
        {
            // Escalar dinamicamente: 1 pila por cada 90 celdas
            // 15x15 = ~2 pilas
            // 25x25 = ~6 pilas
            count = Mathf.Max(2, (width * height) / 90);
        }

        Debug.Log("MazeGenerator: Geneóndo " + count + " pilas de repuesto en el mapa.");
        for (int i = 0; i < count; i++)
        {
            SpawnBatteryRandom();
        }
    }

    public void SpawnBatteryRandom()
    {
        activeBatteries.RemoveAll(item => item == null);

        // Buscar puntos de tipo Floor o Desk
        System.Collections.Generic.List<ItemSpawnPoint> validPoints = new System.Collections.Generic.List<ItemSpawnPoint>();
        foreach (var pt in availableSpawnPoints)
        {
            if (pt.type == SpawnPointType.Floor || pt.type == SpawnPointType.Desk || pt.type == SpawnPointType.ToiletTank)
            {
                validPoints.Add(pt);
            }
        }

        if (validPoints.Count > 0)
        {
            ItemSpawnPoint chosen = validPoints[UnityEngine.Random.Range(0, validPoints.Count)];
            availableSpawnPoints.Remove(chosen);
            // Evitar aglomeracion: remover todos los puntos en un radio de 3.5 metros (max 1 o 2 items por cuarto pequeno)
            availableSpawnPoints.RemoveAll(pt => Vector3.Distance(pt.position, chosen.position) < 3.5f);

            GameObject battery;
            if (batteryPrefab != null)
            {
                battery = Instantiate(batteryPrefab, chosen.position, chosen.rotation);
            }
            else
            {
                // 1. Crear el cuerpo de la pila (Cilindro principal negro)
                battery = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                battery.name = "PilaProcedural";
                battery.transform.position = chosen.position;
                battery.transform.rotation = chosen.rotation;
                
                battery.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);

                Collider col = battery.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                CapsuleCollider triggerCol = battery.AddComponent<CapsuleCollider>();
                triggerCol.isTrigger = true;

                Renderer bodyRend = battery.GetComponent<Renderer>();
                if (bodyRend != null)
                {
                    Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    bodyMat.color = new Color(0.12f, 0.12f, 0.12f);
                    bodyMat.SetFloat("_Metallic", 0.8f);
                    bodyMat.SetFloat("_Smoothness", 0.6f);
                    bodyRend.sharedMaterial = bodyMat;
                }

                // 2. Polo positivo
                GameObject positivePole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                positivePole.name = "PoloPositivo";
                positivePole.transform.SetParent(battery.transform);
                positivePole.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                positivePole.transform.localRotation = Quaternion.identity;
                positivePole.transform.localScale = new Vector3(0.35f, 0.1f, 0.35f);
                
                Collider childCol1 = positivePole.GetComponent<Collider>();
                if (childCol1 != null) DestroyImmediate(childCol1);

                Renderer poleRend = positivePole.GetComponent<Renderer>();
                if (poleRend != null)
                {
                    Material goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    goldMat.color = new Color(0.85f, 0.5f, 0.15f);
                    goldMat.SetFloat("_Metallic", 0.95f);
                    goldMat.SetFloat("_Smoothness", 0.85f);
                    poleRend.sharedMaterial = goldMat;
                }

                // 3. Polo negativo
                GameObject negativePole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                negativePole.name = "PoloNegativo";
                negativePole.transform.SetParent(battery.transform, false);
                negativePole.transform.localPosition = new Vector3(0f, -1.02f, 0f);
                negativePole.transform.localRotation = Quaternion.identity;
                negativePole.transform.localScale = new Vector3(0.98f, 0.04f, 0.98f);

                // // // Collider childCol2 = child2.GetComponent<Collider>();
                // if (childCol2 != null) DestroyImmediate(childCol2);

                Renderer silverRend = negativePole.GetComponent<Renderer>();
                if (silverRend != null)
                {
                    Material silverMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    silverMat.color = new Color(0.75f, 0.75f, 0.75f);
                    silverMat.SetFloat("_Metallic", 0.95f);
                    silverMat.SetFloat("_Smoothness", 0.8f);
                    silverRend.sharedMaterial = silverMat;
                }
            }

            if (battery.GetComponent<BatteryItem>() == null)
            {
                battery.AddComponent<BatteryItem>();
            }

            activeBatteries.Add(battery);
            battery.transform.SetParent(GetItemsParent("Batteries"));
            Debug.Log($"MazeGenerator: Pila dinamica generada sobre {chosen.type} en {chosen.position}");
        }
    }

    void SpawnProceduralWallWithDoorway(Vector2Int roomCell, Vector2Int doorCell, Transform parent)
    {
        Vector3 cellCenter = transform.position + new Vector3(roomCell.x * tileSize, 0f, roomCell.y * tileSize);
        Vector3 doorCellCenter = transform.position + new Vector3(doorCell.x * tileSize, 0f, doorCell.y * tileSize);
        Vector3 boundaryWorldPos = (cellCenter + doorCellCenter) * 0.5f;

        Vector3 forwardDir = (doorCellCenter - cellCenter).normalized;
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        // Restauramos el multiplicador mapScale para que la puerta sea proporcional al tamano gigante de la grilla (16m) y el jugador no se atore fisica o visualmente
        float doorW = 1.4490f * mapScale; 
        float doorH = 2.6548f * mapScale;
        float wallW = tileSize; // Ancho total de la celda (16m)
        float sideWallW = (wallW - doorW) / 2f; // Ancho de cada lateral (7m)
        float thickness = 0.15f * mapScale;

        // Separacion imperceptible para el renderizado doble cara
        float shift = 0.01f * mapScale;

        // 1. Pared lateral izquierda
        Vector3 leftPos = boundaryWorldPos - rightDir * (doorW / 2f + sideWallW / 2f);
        SpawnHalfWall(leftPos, sideWallW, ceilingHeight * mapScale, thickness, forwardDir, shift, parent);

        // 2. Pared lateral derecha
        Vector3 rightPos = boundaryWorldPos + rightDir * (doorW / 2f + sideWallW / 2f);
        SpawnHalfWall(rightPos, sideWallW, ceilingHeight * mapScale, thickness, forwardDir, shift, parent);

        // 3. Pared superior (sobre la puerta) usando dos cubos planos y delgados con la textura de yeso/plaster para no mostrar las baldosas en el techo
        float topWallH = (ceilingHeight * mapScale - doorH) + 0.3f * mapScale;
        // Colocacion exacta considerando que el pivote del Cubo de Unity esta en el centro
        Vector3 topPos = boundaryWorldPos + Vector3.up * (doorH + (ceilingHeight * mapScale - doorH) / 2f + 0.15f * mapScale);
        
        Quaternion rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        Quaternion backRotation = rotation * Quaternion.Euler(0, 180, 0);
        
        // Buscar dinamicamente el material de yeso/plaster de forma ultra-robusta revisando todos los renderizadores del prefab
        Material plasterMat = null;
        if (wallPrefab != null)
        {
            Renderer[] allRens = wallPrefab.GetComponentsInChildren<Renderer>(true);
            bool foundPlaster = false;
            foreach (Renderer r in allRens)
            {
                if (foundPlaster) break;
                if (r != null && r.sharedMaterials != null)
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m != null)
                        {
                            // Guardar el primer material valido como fallback de seguridad para que NUNCA sea null
                            if (plasterMat == null) plasterMat = m;

                            // Si encontramos el de yeso/muro, lo preferimos y salimos del bucle
                            if (m.name.ToLower().Contains("plaster") || m.name.ToLower().Contains("paint") || 
                                m.name.ToLower().Contains("muro") || m.name.ToLower().Contains("pared") || 
                                m.name.ToLower().Contains("wall") || m.name.ToLower().Contains("brick"))
                            {
                                plasterMat = m;
                                foundPlaster = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("WALL MATERIAL DETECTED: " + (plasterMat != null ? plasterMat.name : "NULL"));
        
        float thinThickness = 0.02f * mapScale; // Grosor muy delgado para quedar enrasado con las paredes

        // Cubo frente (hacia el pasillo oscuro) - Usamos el material de yeso pero forzamos a negro mate absoluto
        GameObject w1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w1.name = "TopWall_Front";
        w1.transform.position = topPos + forwardDir * shift;
        w1.transform.rotation = rotation;
        w1.transform.localScale = new Vector3(doorW + 0.8f * mapScale, topWallH, thinThickness);
        w1.transform.SetParent(parent, true);
        Destroy(w1.GetComponent<Collider>());
        if (plasterMat != null)
        {
            Material frontMat = Instantiate(plasterMat);
            frontMat.name = "Mat_TopWall_Front_Shadow";
            
            Color blackColor = new Color(0.015f, 0.015f, 0.015f, 1f); // Negro casi absoluto (1.5% de brillo)
            if (frontMat.HasProperty("_BaseColor")) frontMat.SetColor("_BaseColor", blackColor);
            if (frontMat.HasProperty("_Color")) frontMat.SetColor("_Color", blackColor);
            
            // Remover texturas para forzar el uso del color mate oscuro puro
            if (frontMat.HasProperty("_BaseMap")) frontMat.SetTexture("_BaseMap", null);
            if (frontMat.HasProperty("_MainTex")) frontMat.SetTexture("_MainTex", null);
            if (frontMat.HasProperty("_BumpMap")) frontMat.SetTexture("_BumpMap", null);
            if (frontMat.HasProperty("_MetallicGlossMap")) frontMat.SetTexture("_MetallicGlossMap", null);
            if (frontMat.HasProperty("_OcclusionMap")) frontMat.SetTexture("_OcclusionMap", null);

            // Apagar todo brillo
            if (frontMat.HasProperty("_Smoothness")) frontMat.SetFloat("_Smoothness", 0f);
            if (frontMat.HasProperty("_Glossiness")) frontMat.SetFloat("_Glossiness", 0f);
            if (frontMat.HasProperty("_Metallic")) frontMat.SetFloat("_Metallic", 0f);
            if (frontMat.HasProperty("_SpecColor")) frontMat.SetColor("_SpecColor", Color.black);
            
            // Apagar emisiones
            if (frontMat.HasProperty("_EmissionColor")) frontMat.SetColor("_EmissionColor", Color.clear);
            frontMat.DisableKeyword("_EMISSION");
            
            w1.GetComponent<Renderer>().sharedMaterial = frontMat;
        }

        // Cubo atras (hacia el cuarto) - Usamos un receptor dinamico de luz para cambiar de color segun el estado del foco
        GameObject w2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w2.name = "TopWall_Back";
        w2.transform.position = topPos - forwardDir * shift;
        w2.transform.rotation = backRotation;
        w2.transform.localScale = new Vector3(doorW + 0.8f * mapScale, topWallH, thinThickness);
        w2.transform.SetParent(parent, true);
        Destroy(w2.GetComponent<Collider>());
        if (plasterMat != null)
        {
            var receiver = w2.AddComponent<DynamicWallLightReceiver>();
            receiver.baseMaterial = plasterMat;
        }
    }



    void SpawnHalfWall(Vector3 pos, float width, float height, float thickness, Vector3 forwardDir, float shift, Transform parent)
    {
        if (wallPrefab == null) return;

        Quaternion rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        Quaternion backRotation = rotation * Quaternion.Euler(0, 180, 0);

        // Escalado relativo a baseTileSize (4m)
        Vector3 wallScale = new Vector3((width / baseTileSize) * 1.02f, height / baseTileSize, mapScale);

        // Pared frente
        GameObject w1 = Instantiate(wallPrefab, pos + forwardDir * shift, rotation, parent);
        w1.transform.localScale = wallScale;

        // Pared atras (girada 180)
        GameObject w2 = Instantiate(wallPrefab, pos - forwardDir * shift, backRotation, parent);
        w2.transform.localScale = wallScale;
    }

    GameObject SpawnRoomDoor(Vector2Int roomCell, Vector2Int doorCell, bool isLocked)
    {
        Vector3 cellCenter = transform.position + new Vector3(roomCell.x * tileSize, 0f, roomCell.y * tileSize);
        Vector3 doorCellCenter = transform.position + new Vector3(doorCell.x * tileSize, 0f, doorCell.y * tileSize);
        Vector3 boundaryWorldPos = (cellCenter + doorCellCenter) * 0.5f;

        // Restauramos el multiplicador mapScale para que las dimensiones fisicas de la puerta y bisagra escalen correctamente con el marco
        float doorWidth = 1.2372f * mapScale;
        float doorHeight = 2.5489f * mapScale;
        float thickness = 0.08f * mapScale;

        GameObject mapParent = GameObject.Find("Generated_Hospital_Map");

        // Intentar instanciar usando los prefabs originales del Hospital Horror Pack
        GameObject targetDoorPrefab = null;
        GameObject targetFramePrefab = null;
#if UNITY_EDITOR
        targetDoorPrefab = editorDoorPrefab;
        targetFramePrefab = editorDoorFramePrefab;
#else
        targetDoorPrefab = Resources.Load<GameObject>("P_Door_01_");
        targetFramePrefab = Resources.Load<GameObject>("P_Door_01_Base");
#endif

        Vector3 forwardDir = (doorCellCenter - cellCenter).normalized;
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
        Quaternion doorRot = Quaternion.LookRotation(forwardDir, Vector3.up);

        if (targetDoorPrefab != null && targetFramePrefab != null)
        {
            // 1. Instanciar marco original centrado
            GameObject frameObj = Instantiate(targetFramePrefab, boundaryWorldPos, doorRot, mapParent != null ? mapParent.transform : null);
            frameObj.name = "MarcoPuerta_Original";
            frameObj.transform.localScale = Vector3.one * mapScale;

            // 2. Crear objeto bisagra (Hinge) en el borde izquierdo de la puerta (visto desde adentro de la habitacion o del pasillo)
            GameObject hingeObj = new GameObject("ProceduralRoomDoor_Hinge");
            hingeObj.transform.SetParent(mapParent != null ? mapParent.transform : null, true);
            
            // Bisagra colocada exactamente en el borde izquierdo de la apertura
            Vector3 hingePos = boundaryWorldPos - rightDir * (doorWidth / 2f);
            hingeObj.transform.position = hingePos;
            hingeObj.transform.rotation = doorRot;

            // 3. Instanciar panel de la puerta exactamente CENTRADO en la apertura (boundaryWorldPos)
            GameObject doorObj = Instantiate(targetDoorPrefab, boundaryWorldPos, doorRot, null);
            doorObj.name = "Puerta_Panel";
            doorObj.transform.localScale = Vector3.one * mapScale;

            // 4. Emparentar a la bisagra manteniendo su posicion y rotacion del mundo intactas
            // Unity calculara automaticamente el offset local correcto para que gire perfecto
            doorObj.transform.SetParent(hingeObj.transform, true);

            // El prefab original ya cuenta con un MeshCollider en sus mallas hijas, 
            // no debemos agregar un BoxCollider en la raiz porque se distorsiona con la escala de Unity.

            // Desactivar Animator en el panel de la puerta para evitar que bloquee la rotacion de la bisagra
            Animator anim = doorObj.GetComponent<Animator>();
            if (anim == null) anim = doorObj.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            var interact = hingeObj.AddComponent<ProceduralDoorInteract>();
            interact.isLocked = isLocked;
            interact.interactDistance = 7.0f;

            return hingeObj;
        }
        else
        {
            // --- Fallback Procedural de Respaldo si no hay prefabs ---
            GameObject hingeObj = new GameObject("ProceduralRoomDoor_Hinge");
            if (mapParent != null) hingeObj.transform.SetParent(mapParent.transform, true);
            
            Vector3 hingePos;
            Vector3 panelLocalPos;
            Vector3 panelLocalScale;
            Quaternion doorBaseRot = Quaternion.identity;

            if (roomCell.y != doorCell.y) // Puerta Norte/Sur
            {
                hingePos = boundaryWorldPos + new Vector3(-doorWidth / 2f, 0f, 0f);
                panelLocalPos = new Vector3(doorWidth / 2f, doorHeight / 2f, 0f);
                panelLocalScale = new Vector3(doorWidth, doorHeight, thickness);
            }
            else // Puerta Este/Oeste
            {
                hingePos = boundaryWorldPos + new Vector3(0f, 0f, -doorWidth / 2f);
                panelLocalPos = new Vector3(0f, doorHeight / 2f, doorWidth / 2f);
                panelLocalScale = new Vector3(thickness, doorHeight, doorWidth);
                doorBaseRot = Quaternion.Euler(0f, 90f, 0f);
            }

            hingeObj.transform.position = hingePos;
            hingeObj.transform.rotation = doorBaseRot;

            GameObject doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorPanel.name = "Puerta_Panel";
            doorPanel.transform.SetParent(hingeObj.transform, false);
            doorPanel.transform.localPosition = panelLocalPos;
            doorPanel.transform.localScale = panelLocalScale;
            
            Material doorMat = doorMaterial;
            if (doorMat == null)
            {
                doorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                doorMat.color = new Color(0.25f, 0.16f, 0.1f);
                doorMat.SetFloat("_Smoothness", 0.08f);
            }
            doorPanel.GetComponent<Renderer>().sharedMaterial = doorMat;

            if (doorPanel.GetComponent<BoxCollider>() == null)
            {
                doorPanel.AddComponent<BoxCollider>();
            }

            var interact = hingeObj.AddComponent<ProceduralDoorInteract>();
            interact.isLocked = isLocked;
            interact.interactDistance = 7.0f;

            return hingeObj;
        }
    }

    // -----------------------------------------------------------------------
    // METODOS PARA SPAWNEAR TARJETA Y ASCENSOR DE ESCAPE
    // -----------------------------------------------------------------------
    void SpawnKeycard()
    {
        Vector2Int directorRoomCell = new Vector2Int(width / 2, height / 2);

        // 1. Intentar colocar en la ultima habitacion si existen
        if (roomPivots != null && roomPivots.Count > 0)
        {
            directorRoomCell = roomPivots[roomPivots.Count - 1];
        }
        else
        {
            // Fallback: Buscar un pasillo alejado del jugador
            System.Collections.Generic.List<Vector2Int> candidates = new System.Collections.Generic.List<Vector2Int>();
            foreach (Vector2Int cell in corridors)
            {
                if (Vector2Int.Distance(cell, new Vector2Int(1, 1)) > 6f)
                {
                    candidates.Add(cell);
                }
            }

            if (candidates.Count > 0)
            {
                directorRoomCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }
        }

        // Colocar la tarjeta del director sobre una mesita o suelo dentro de la habitacion
        float heightOffset = 0.05f * mapScale;
        Vector3 spawnPos = transform.position + new Vector3(directorRoomCell.x * tileSize, heightOffset, directorRoomCell.y * tileSize);

        GameObject keycard;
        if (keycardPrefab != null)
        {
            keycard = Instantiate(keycardPrefab, spawnPos, Quaternion.identity);
            keycard.transform.localScale = keycard.transform.localScale * mapScale;
            keycard.transform.SetParent(GetItemsParent("Keycards"));
            keycard.name = "TarjetaAccesoDirector";
        }
        else
        {
            // Tarjeta magnetica procedural
            keycard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keycard.name = "TarjetaAccesoDirector";
            keycard.transform.position = spawnPos;
            keycard.transform.localScale = new Vector3(0.18f, 0.01f, 0.32f) * mapScale;
            
            Material keyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            keyMat.color = new Color(1f, 0.45f, 0.1f); // Naranja
            keyMat.SetFloat("_Smoothness", 0.5f);
            keycard.GetComponent<Renderer>().sharedMaterial = keyMat;
        }

        if (keycard.GetComponent<KeycardItem>() == null)
        {
            keycard.AddComponent<KeycardItem>();
        }
    }

    void DetermineElevatorCell()
    {
        // 1. Encontrar todos los callejones sin salida (Dead Ends) de los pasillos
        System.Collections.Generic.List<Vector2Int> deadEnds = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (grid[x, y] && !roomPositions.Contains(new Vector2Int(x, y)))
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    int openNeighbors = 0;
                    if (grid[x - 1, y]) openNeighbors++;
                    if (grid[x + 1, y]) openNeighbors++;
                    if (grid[x, y - 1]) openNeighbors++;
                    if (grid[x, y + 1]) openNeighbors++;

                    if (openNeighbors == 1)
                    {
                        // Determinar cual es el unico vecino abierto
                        Vector2Int openNeighbor = cell;
                        if (grid[x - 1, y]) openNeighbor = new Vector2Int(x - 1, y);
                        else if (grid[x + 1, y]) openNeighbor = new Vector2Int(x + 1, y);
                        else if (grid[x, y - 1]) openNeighbor = new Vector2Int(x, y - 1);
                        else if (grid[x, y + 1]) openNeighbor = new Vector2Int(x, y + 1);

                        // El callejon sin salida del ascensor DEBE abrir hacia un pasillo abierto.
                        // Excluimos vecinos que sean habitaciones para evitar que las puertas se bloqueen con paredes.
                        if (!roomPositions.Contains(openNeighbor))
                        {
                            deadEnds.Add(cell);
                        }
                    }
                }
            }
        }

        // 2. Seleccionar el callejon sin salida mas alejado del inicio (1, 1) donde inicia el jugador
        Vector2Int targetCell = new Vector2Int(1, 1);
        float maxDist = -1f;
        
        if (deadEnds.Count > 0)
        {
            foreach (Vector2Int cell in deadEnds)
            {
                float dist = Vector2Int.Distance(cell, new Vector2Int(1, 1));
                if (dist > maxDist)
                {
                    maxDist = dist;
                    targetCell = cell;
                }
            }
        }
        else
        {
            // Fallback dinamico: Buscar cualquier celda de pasillo abierta que no sea habitacion, lo mas lejos posible
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (grid[x, y] && !roomPositions.Contains(new Vector2Int(x, y)))
                    {
                        // Asegurar que tenga al menos un vecino pasillo abierto que no sea habitacion
                        bool hasValidCorridorNeighbor = false;
                        if (x - 1 >= 0 && grid[x - 1, y] && !roomPositions.Contains(new Vector2Int(x - 1, y))) hasValidCorridorNeighbor = true;
                        else if (x + 1 < width && grid[x + 1, y] && !roomPositions.Contains(new Vector2Int(x + 1, y))) hasValidCorridorNeighbor = true;
                        else if (y - 1 >= 0 && grid[x, y - 1] && !roomPositions.Contains(new Vector2Int(x, y - 1))) hasValidCorridorNeighbor = true;
                        else if (y + 1 < height && grid[x, y + 1] && !roomPositions.Contains(new Vector2Int(x, y + 1))) hasValidCorridorNeighbor = true;

                        if (hasValidCorridorNeighbor)
                        {
                            float dist = Vector2Int.Distance(new Vector2Int(x, y), new Vector2Int(1, 1));
                            if (dist > maxDist)
                            {
                                maxDist = dist;
                                targetCell = new Vector2Int(x, y);
                            }
                        }
                    }
                }
            }
        }

        elevatorCell = targetCell;

        // Determinar que celda queda en frente de las puertas del ascensor para evitar spawnear una pared alli
        Vector2Int frontCell = elevatorCell;
        Vector2Int north = new Vector2Int(elevatorCell.x, elevatorCell.y + 1);
        Vector2Int south = new Vector2Int(elevatorCell.x, elevatorCell.y - 1);
        Vector2Int east = new Vector2Int(elevatorCell.x + 1, elevatorCell.y);
        Vector2Int west = new Vector2Int(elevatorCell.x - 1, elevatorCell.y);

        if (north.y < height && grid[north.x, north.y] && !roomPositions.Contains(north)) 
            frontCell = north;
        else if (south.y >= 0 && grid[south.x, south.y] && !roomPositions.Contains(south)) 
            frontCell = south;
        else if (east.x < width && grid[east.x, east.y] && !roomPositions.Contains(east)) 
            frontCell = east;
        else if (west.x >= 0 && grid[west.x, west.y] && !roomPositions.Contains(west)) 
            frontCell = west;
        else
        {
            if (grid[elevatorCell.x, elevatorCell.y + 1]) frontCell = north;
            else if (grid[elevatorCell.x, elevatorCell.y - 1]) frontCell = south;
            else if (grid[elevatorCell.x + 1, elevatorCell.y]) frontCell = east;
            else if (grid[elevatorCell.x - 1, elevatorCell.y]) frontCell = west;
        }
        elevatorFrontCell = frontCell;

        Debug.Log($"[MazeGenerator] Ubicacion del ascensor establecida en: {elevatorCell}, abre hacia: {elevatorFrontCell} (Distancia al origen: {maxDist})");
    }

    void SpawnElevator()
    {
        Vector2Int targetCell = elevatorCell;

        // 3. Determinar rotacion para que mire al pasillo abierto (evitando siempre orientarse hacia una habitacion bloqueada)
        Quaternion elevRot = Quaternion.identity;
        Vector2Int north = new Vector2Int(targetCell.x, targetCell.y + 1);
        Vector2Int south = new Vector2Int(targetCell.x, targetCell.y - 1);
        Vector2Int east = new Vector2Int(targetCell.x + 1, targetCell.y);
        Vector2Int west = new Vector2Int(targetCell.x - 1, targetCell.y);

        if (north.y < height && grid[north.x, north.y] && !roomPositions.Contains(north)) 
            elevRot = Quaternion.Euler(0, 0, 0); // Abre al Norte (Pasillo)
        else if (south.y >= 0 && grid[south.x, south.y] && !roomPositions.Contains(south)) 
            elevRot = Quaternion.Euler(0, 180, 0); // Abre al Sur (Pasillo)
        else if (east.x < width && grid[east.x, east.y] && !roomPositions.Contains(east)) 
            elevRot = Quaternion.Euler(0, 90, 0); // Abre al Este (Pasillo)
        else if (west.x >= 0 && grid[west.x, west.y] && !roomPositions.Contains(west)) 
            elevRot = Quaternion.Euler(0, 270, 0); // Abre al Oeste (Pasillo)
        else
        {
            // Fallback original por si todos los caminos abiertos fuesen catalogados como habitaciones (caso extremo/improbable)
            if (grid[targetCell.x, targetCell.y + 1]) elevRot = Quaternion.Euler(0, 0, 0);
            else if (grid[targetCell.x, targetCell.y - 1]) elevRot = Quaternion.Euler(0, 180, 0);
            else if (grid[targetCell.x + 1, targetCell.y]) elevRot = Quaternion.Euler(0, 90, 0);
            else if (grid[targetCell.x - 1, targetCell.y]) elevRot = Quaternion.Euler(0, 270, 0);
        }

        Vector3 spawnPos = transform.position + new Vector3(targetCell.x * tileSize, 0.0f, targetCell.y * tileSize);

        GameObject elevator;
        if (elevatorPrefab != null)
        {
            elevator = Instantiate(elevatorPrefab, spawnPos, elevRot);
            elevator.transform.localScale = elevator.transform.localScale * mapScale;
        }
        else
        {
            // 4. Crear cabina de ascensor procedural DETALLADA (Estilo "QA Elevator" de las imagenes)
            elevator = new GameObject("AscensorProcedural");
            elevator.transform.position = spawnPos;
            elevator.transform.rotation = elevRot;

            float innerHeight = 2.2f * mapScale;
            float thickness = 0.08f * mapScale;

            // Material del metal de la cabina (Premium - Gris/Azulado industrial con textura)
            Material cabinaMat = Resources.Load<Material>("Mat_Bed_Metal_01");
            if (cabinaMat != null)
            {
                cabinaMat = Instantiate(cabinaMat);
                cabinaMat.color = new Color(0.5f, 0.52f, 0.55f);
            }
            else
            {
                cabinaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                cabinaMat.color = new Color(0.2f, 0.23f, 0.25f);
                cabinaMat.SetFloat("_Metallic", 0.85f);
                cabinaMat.SetFloat("_Smoothness", 0.5f);
            }

            // Material del parachoques y carcasa de botones (Negro mate)
            Material bumperMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bumperMat.color = new Color(0.08f, 0.08f, 0.08f);
            bumperMat.SetFloat("_Metallic", 0.4f);
            bumperMat.SetFloat("_Smoothness", 0.15f);

            // Material de las puertas (Gris acero brillante pulido con textura)
            Material puertaMat = Resources.Load<Material>("Mat_Bed_Metal_01");
            if (puertaMat != null)
            {
                puertaMat = Instantiate(puertaMat);
                puertaMat.color = new Color(0.7f, 0.72f, 0.75f);
            }
            else
            {
                puertaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                puertaMat.color = new Color(0.35f, 0.38f, 0.4f);
                puertaMat.SetFloat("_Metallic", 0.95f);
                puertaMat.SetFloat("_Smoothness", 0.65f);
            }

            // Material de luz de techo (Blanco frio con Emision potente)
            Material lightEmissiveMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lightEmissiveMat.color = Color.white;
            lightEmissiveMat.EnableKeyword("_EMISSION");
            lightEmissiveMat.SetColor("_EmissionColor", new Color(1.3f, 1.45f, 1.6f) * 1.8f);

            // Material de pantalla indicadora de piso (Verde brillante emisivo)
            Material greenScreenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            greenScreenMat.color = Color.black;
            greenScreenMat.EnableKeyword("_EMISSION");
            greenScreenMat.SetColor("_EmissionColor", new Color(0f, 1.6f, 0.25f) * 2.5f);

            // A. Suelo de la cabina
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Suelo";
            floor.transform.SetParent(elevator.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.02f * mapScale, 0f);
            floor.transform.localScale = new Vector3(tileSize * 0.98f, thickness, tileSize * 0.98f);
            floor.GetComponent<Renderer>().sharedMaterial = cabinaMat;

            // B. Techo de la cabina
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Techo";
            ceiling.transform.SetParent(elevator.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, innerHeight, 0f);
            ceiling.transform.localScale = new Vector3(tileSize * 0.98f, thickness, tileSize * 0.98f);
            ceiling.GetComponent<Renderer>().sharedMaterial = cabinaMat;

            // C. Paneles de la Pared Izquierda (Secciones verticales individuales para simular uniones reales)
            int panelCount = 4;
            float panelWidth = tileSize / panelCount;
            for (int i = 0; i < panelCount; i++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "PanelIzq_" + i;
                panel.transform.SetParent(elevator.transform, false);
                float offset = -0.5f * tileSize + (i * panelWidth) + (panelWidth / 2f);
                panel.transform.localPosition = new Vector3(-0.48f * tileSize, innerHeight / 2f, offset);
                panel.transform.localScale = new Vector3(thickness * 0.8f, innerHeight, panelWidth * 1.02f);
                panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
            }

            // D. Paneles de la Pared Derecha (Secciones verticales)
            for (int i = 0; i < panelCount; i++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "PanelDer_" + i;
                panel.transform.SetParent(elevator.transform, false);
                float offset = -0.5f * tileSize + (i * panelWidth) + (panelWidth / 2f);
                panel.transform.localPosition = new Vector3(0.48f * tileSize, innerHeight / 2f, offset);
                panel.transform.localScale = new Vector3(thickness * 0.8f, innerHeight, panelWidth * 1.02f);
                panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
            }

            // E. Paneles de la Pared Trasera (Secciones verticales)
            float panelWidthBack = tileSize / panelCount;
            for (int i = 0; i < panelCount; i++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "PanelTrasero_" + i;
                panel.transform.SetParent(elevator.transform, false);
                float offset = -0.5f * tileSize + (i * panelWidthBack) + (panelWidthBack / 2f);
                panel.transform.localPosition = new Vector3(offset, innerHeight / 2f, -0.48f * tileSize);
                panel.transform.localScale = new Vector3(panelWidthBack * 1.02f, innerHeight, thickness * 0.8f);
                panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
            }

            // F. Parachoques / Pasamanos protectores (Horizontal bumper guards)
            float bumperHeight = 0.9f * mapScale;
            float bumperSize = 0.05f * mapScale;

            GameObject leftBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBumper.name = "BumperIzquierdo";
            leftBumper.transform.SetParent(elevator.transform, false);
            leftBumper.transform.localPosition = new Vector3(-0.46f * tileSize, bumperHeight, 0f);
            leftBumper.transform.localScale = new Vector3(bumperSize, bumperSize * 1.5f, tileSize * 0.92f);
            leftBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

            GameObject rightBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBumper.name = "BumperDerecho";
            rightBumper.transform.SetParent(elevator.transform, false);
            rightBumper.transform.localPosition = new Vector3(0.46f * tileSize, bumperHeight, 0f);
            rightBumper.transform.localScale = new Vector3(bumperSize, bumperSize * 1.5f, tileSize * 0.92f);
            rightBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

            GameObject backBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backBumper.name = "BumperTrasero";
            backBumper.transform.SetParent(elevator.transform, false);
            backBumper.transform.localPosition = new Vector3(0f, bumperHeight, -0.46f * tileSize);
            backBumper.transform.localScale = new Vector3(tileSize * 0.92f, bumperSize, bumperSize * 1.5f);
            backBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

            // G. Panel de Luz Fluorescente en el techo e Iluminacion en tiempo real
            GameObject panelLuz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelLuz.name = "PanelLuzTecho";
            panelLuz.transform.SetParent(elevator.transform, false);
            float ceilingBottomY = innerHeight - (thickness / 2f);
            float lightHeight = 0.02f * mapScale;
            panelLuz.transform.localPosition = new Vector3(0f, ceilingBottomY - (lightHeight / 2f) - 0.005f, 0f);
            panelLuz.transform.localScale = new Vector3(tileSize * 0.45f, lightHeight, tileSize * 0.45f);
            panelLuz.GetComponent<Renderer>().sharedMaterial = lightEmissiveMat;
            Collider lpCol = panelLuz.GetComponent<Collider>();
            if (lpCol != null) DestroyImmediate(lpCol);

            // Luz PointLight en tiempo real
            GameObject pointLightObj = new GameObject("LuzAscensor");
            pointLightObj.transform.SetParent(panelLuz.transform);
            pointLightObj.transform.localPosition = new Vector3(0f, -0.15f * mapScale, 0f);
            Light pLight = pointLightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(0.85f, 0.95f, 1f);
            pLight.intensity = 3.5f;
            pLight.range = tileSize * 0.6f;
            pLight.shadows = LightShadows.Soft;

            // H. Pantalla indicadora de piso exterior
            // La pared frontal tiene su CENTRO en Z=0.48*tileSize y grosor=thickness.
            // Cara exterior = 0.48*tileSize + thickness/2 ¢ÃƒÂ¢Ã¢â€šÂ¬ ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ pantalla va en esa cara exterior.
            float frontWallOuterZ = 0.48f * tileSize + (thickness / 2f) + 0.008f * mapScale;

            GameObject extScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extScreen.name = "PantallaPisoExterior";
            extScreen.transform.SetParent(elevator.transform, false);
            extScreen.transform.localPosition = new Vector3(0f, innerHeight + 0.15f * mapScale, frontWallOuterZ);
            extScreen.transform.localScale = new Vector3(0.35f * mapScale, 0.15f * mapScale, 0.02f * mapScale);
            extScreen.GetComponent<Renderer>().sharedMaterial = greenScreenMat;

            // Luz de la pantalla exterior
            GameObject extScreenLightObj = new GameObject("LuzPantallaExterior");
            extScreenLightObj.transform.SetParent(extScreen.transform, false);
            extScreenLightObj.transform.localPosition = new Vector3(0f, 0f, 1f);
            Light extScreenLight = extScreenLightObj.AddComponent<Light>();
            extScreenLight.type = LightType.Point;
            extScreenLight.color = new Color(0.2f, 1f, 0.3f);
            extScreenLight.intensity = 1.5f;
            extScreenLight.range = 2f * mapScale;
            extScreenLight.shadows = LightShadows.None;

            // Texto indicador exterior ¢ÃƒÂ¢Ã¢â‚¬Å¡¬ÃƒÂ¢Ã¢â€šÂ¬ valores calibrados: K_x=0.45, K_y=0.48
            GameObject extTextObj = new GameObject("TextoPisoExterior");
            extTextObj.transform.SetParent(elevator.transform, false);
            extTextObj.transform.localPosition = new Vector3(0f, innerHeight + 0.15f * mapScale, frontWallOuterZ + 0.01f * mapScale);
            extTextObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            extTextObj.transform.localScale = new Vector3(mapScale * 0.45f, mapScale * 0.48f, 1f);
            
            TextMesh extTM = extTextObj.AddComponent<TextMesh>();
            extTM.text = "S";
            extTM.fontSize = 64;
            extTM.characterSize = 0.05f;
            
            Renderer extRend = extTextObj.GetComponent<Renderer>();
            Material extTextMat = new Material(Shader.Find("Sprites/Default"));
            extTextMat.mainTexture = extTM.font.material.mainTexture;
            extTextMat.color = new Color(0.02f, 0.1f, 0.02f);
            extRend.sharedMaterial = extTextMat;
            extTM.color = new Color(0.02f, 0.1f, 0.02f);
            extTM.alignment = TextAlignment.Center;
            extTM.anchor = TextAnchor.MiddleCenter;
            extTM.fontStyle = FontStyle.Bold;

            // I. Puertas deslizantes
            GameObject lDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lDoor.name = "PuertaIzquierda";
            lDoor.transform.SetParent(elevator.transform, false);
            lDoor.transform.localPosition = new Vector3(-0.25f * tileSize, innerHeight / 2f, 0.478f * tileSize);
            lDoor.transform.localScale = new Vector3(0.5f * tileSize, innerHeight, thickness);
            lDoor.GetComponent<Renderer>().sharedMaterial = puertaMat;

            GameObject rDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rDoor.name = "PuertaDerecha";
            rDoor.transform.SetParent(elevator.transform, false);
            rDoor.transform.localPosition = new Vector3(0.25f * tileSize, innerHeight / 2f, 0.478f * tileSize);
            rDoor.transform.localScale = new Vector3(0.5f * tileSize, innerHeight, thickness);
            rDoor.GetComponent<Renderer>().sharedMaterial = puertaMat;

            // Umbral metalico (Placa de piso que tapa el hueco entre el ascensor y el pasillo)
            GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
            threshold.name = "UmbralAscensor";
            threshold.transform.SetParent(elevator.transform, false);
            threshold.transform.localPosition = new Vector3(0f, 0.02f * mapScale, 0.495f * tileSize);
            threshold.transform.localScale = new Vector3(0.5f * tileSize, thickness * 0.5f, 0.05f * tileSize);
            threshold.GetComponent<Renderer>().sharedMaterial = puertaMat;

            // J. Botonera exterior
            GameObject extButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extButton.name = "BotoneraExterior";
            extButton.transform.SetParent(elevator.transform, false);
            // Elevar a la altura de los ojos del jugador (1.15f * mapScale) y escalar de acuerdo al mapa
            extButton.transform.localPosition = new Vector3(0.35f * tileSize, 1.15f * mapScale, 0.48f * tileSize + (thickness / 2f) + 0.06f * mapScale);
            extButton.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f) * mapScale;
            Renderer btnRend = extButton.GetComponent<Renderer>();
            if (btnRend != null) btnRend.sharedMaterial = bumperMat;

            GameObject btnRed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            btnRed.name = "BotonRojo";
            btnRed.transform.SetParent(extButton.transform);
            btnRed.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            btnRed.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            btnRed.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            Collider btnCol = btnRed.GetComponent<Collider>();
            if (btnCol != null) DestroyImmediate(btnCol);
            Renderer redRend = btnRed.GetComponent<Renderer>();
            if (redRend != null) redRend.sharedMaterial = lightEmissiveMat;

            // K. Botonera interior ¢ÃƒÂ¢Ã¢â‚¬Å¡¬ÃƒÂ¢Ã¢â€šÂ¬ mismos valores calibrados que Tunneónerator
            float intPanelX = 0.43f * tileSize; // panel interior a salvo de la pared

            GameObject intButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intButton.name = "Botoneónterior";
            intButton.transform.SetParent(elevator.transform, false);
            intButton.transform.localPosition = new Vector3(intPanelX, 1.45f * mapScale, 0f);
            intButton.transform.localScale = new Vector3(0.022f * mapScale, 0.22f * mapScale, 0.18f * mapScale);
            intButton.GetComponent<Renderer>().sharedMaterial = puertaMat;

            // Pantalla verde en la cara interior izquierda del panel
            GameObject intScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intScreen.name = "PantallaPisoInterior";
            intScreen.transform.SetParent(elevator.transform, false);
            intScreen.transform.localPosition = new Vector3(intPanelX - 0.015f * mapScale, 1.50f * mapScale, 0f);
            intScreen.transform.localScale = new Vector3(0.007f * mapScale, 0.09f * mapScale, 0.09f * mapScale);
            intScreen.GetComponent<Renderer>().sharedMaterial = greenScreenMat;
            Collider isCol = intScreen.GetComponent<Collider>();
            if (isCol != null) DestroyImmediate(isCol);

            // Texto indicador interior ¢ÃƒÂ¢Ã¢â‚¬Å¡¬ÃƒÂ¢Ã¢â€šÂ¬ Y=+90Ã¢â‚¬Å¡° para que se vea correctamente (no al reves)
            GameObject intTextObj = new GameObject("TextoPisoInterior");
            intTextObj.transform.SetParent(elevator.transform, false);
            intTextObj.transform.localPosition = new Vector3(intPanelX - 0.028f * mapScale, 1.50f * mapScale, 0f);
            intTextObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            intTextObj.transform.localScale = new Vector3(mapScale * 0.48f, mapScale * 0.48f, 1f);
            
            TextMesh intTM = intTextObj.AddComponent<TextMesh>();
            intTM.text = "S";
            intTM.fontSize = 64;
            intTM.characterSize = 0.014f;
            
            Renderer intRend = intTextObj.GetComponent<Renderer>();
            Material intTextMat = new Material(Shader.Find("Sprites/Default"));
            intTextMat.mainTexture = intTM.font.material.mainTexture;
            intTextMat.color = new Color(0.2f, 1f, 0.3f);
            intRend.sharedMaterial = intTextMat;
            intTM.color = new Color(0.2f, 1f, 0.3f);
            intTM.alignment = TextAlignment.Center;
            intTM.anchor = TextAnchor.MiddleCenter;
            intTM.fontStyle = FontStyle.Bold;

            // L. Pared de sellado frontal
            float ceilingWorldHeight = ceilingHeight * mapScale;
            float gapHeight = ceilingWorldHeight - innerHeight;

            GameObject frontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontLeft.name = "ParedFrontalIzquierda";
            frontLeft.transform.SetParent(elevator.transform, false);
            frontLeft.transform.localPosition = new Vector3(-0.375f * tileSize, ceilingWorldHeight / 2f, 0.48f * tileSize);
            frontLeft.transform.localScale = new Vector3(0.25f * tileSize, ceilingWorldHeight, thickness);
            frontLeft.GetComponent<Renderer>().sharedMaterial = cabinaMat;

            GameObject frontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontRight.name = "ParedFrontalDerecha";
            frontRight.transform.SetParent(elevator.transform, false);
            frontRight.transform.localPosition = new Vector3(0.375f * tileSize, ceilingWorldHeight / 2f, 0.48f * tileSize);
            frontRight.transform.localScale = new Vector3(0.25f * tileSize, ceilingWorldHeight, thickness);
            frontRight.GetComponent<Renderer>().sharedMaterial = cabinaMat;

            if (gapHeight > 0.05f)
            {
                GameObject sealWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sealWall.name = "ParedSelladoTecho";
                sealWall.transform.SetParent(elevator.transform, false);
                sealWall.transform.localPosition = new Vector3(0f, innerHeight + gapHeight / 2f, 0.48f * tileSize);
                sealWall.transform.localScale = new Vector3(0.5f * tileSize, gapHeight, thickness);
                sealWall.GetComponent<Renderer>().sharedMaterial = cabinaMat;
            }
        }

        if (elevator.GetComponent<CharacterController>() == null)
        {
            var controller = elevator.AddComponent<ElevatorController>();
            controller.doorSlideDistance = 0.44f * tileSize; 
            controller.callTime = elevatorCallTime;
            controller.doorSpeed = elevatorDoorSpeed;
            controller.callSoundOffset = elevatorCallSoundOffset;
            controller.arriveSoundOffset = elevatorArriveSoundOffset;
            controller.doorOpenDelay = elevatorDoorOpenDelay;
            controller.bypassKeycard = elevatorBypassKeycard;
        }

    }

    void SpawnNotes()
    {
        // Limpiar notas anteriores si existen
        ElevatorController.foundNotes = new int[7];
        for (int idx = 0; idx < 7; idx++) ElevatorController.foundNotes[idx] = -1;

        Vector2Int directorPivot = (roomPivots != null && roomPivots.Count > 0) ? roomPivots[roomPivots.Count - 1] : new Vector2Int(-999, -999);

        // Puntos pre-registrados en habitaciones (Desk o Wall)
        System.Collections.Generic.List<ItemSpawnPoint> validRoomPoints = new System.Collections.Generic.List<ItemSpawnPoint>();
        foreach (var pt in availableSpawnPoints)
        {
            Vector2Int ptCell = new Vector2Int(Mathf.RoundToInt(pt.position.x / tileSize), Mathf.RoundToInt(pt.position.z / tileSize));
            if ((pt.type == SpawnPointType.Desk || pt.type == SpawnPointType.Wall || pt.type == SpawnPointType.ToiletTank) && !pt.isDirector && Vector2Int.Distance(ptCell, directorPivot) > 3.0f)
            {
                validRoomPoints.Add(pt);
            }
        }

        // Pasillos disponibles para el respawn anterior
        System.Collections.Generic.List<Vector2Int> corridorList = new System.Collections.Generic.List<Vector2Int>(corridors);
        
        // Textura base
        Texture2D paperTex = Resources.Load<Texture2D>("note_paper_blood");

        for (int i = 0; i < 7; i++)
        {
            Vector3 notePos = Vector3.zero;
            Quaternion noteRot = Quaternion.identity;
            bool placed = false;

            // 40% probabilidad de usar punto de habitación (si hay disponibles) o 100% si no hay pasillos
            if (validRoomPoints.Count > 0 && (UnityEngine.Random.value < 0.4f || corridorList.Count == 0))
            {
                int randIdx = UnityEngine.Random.Range(0, validRoomPoints.Count);
                ItemSpawnPoint spawnPoint = validRoomPoints[randIdx];
                notePos = spawnPoint.position;
                noteRot = spawnPoint.rotation;
                
                availableSpawnPoints.Remove(spawnPoint);
                validRoomPoints.RemoveAt(randIdx);
                // Evitar aglomeración: remover puntos cercanos (5.0 metros)
                availableSpawnPoints.RemoveAll(pt => Vector3.Distance(pt.position, notePos) < 5.0f);
                validRoomPoints.RemoveAll(pt => Vector3.Distance(pt.position, notePos) < 5.0f);
                placed = true;
            }
            else if (corridorList.Count > 0)
            {
                // Respawn anterior en paredes de pasillo
                // Intentar hasta 150 veces encontrar una pared válida en pasillos aleatorios
                for (int attempts = 0; attempts < 150; attempts++)
                {
                    int randCorr = UnityEngine.Random.Range(0, corridorList.Count);
                    Vector2Int cell = corridorList[randCorr];
                    
                    // Excluir celdas con camas, subgeneradores, habitaciones especiales o habitación del director
                    if (bedCells.Contains(cell) || generatorCells.Contains(cell) || roomPositions.Contains(cell) || Vector2Int.Distance(cell, directorPivot) < 3.5f) continue;
                    
                    Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left };
                    for (int d = 0; d < 4; d++)
                    {
                        int r = UnityEngine.Random.Range(d, 4);
                        Vector2Int temp = dirs[d];
                        dirs[d] = dirs[r];
                        dirs[r] = temp;
                    }

                    foreach (Vector2Int dir in dirs)
                    {
                        Vector2Int neighbor = cell + dir;
                        // Excluir si el vecino tiene camas o subgeneradores
                        if (bedCells.Contains(neighbor) || generatorCells.Contains(neighbor)) continue;
                        
                        // Si es pared fisica real (fuera del mapa o celda no transitable del laberinto)
                        bool isSolidWall = (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height) || !grid[neighbor.x, neighbor.y];
                        if (isSolidWall)
                        {
                            float offsetDist = (tileSize / 2f) - 0.15f;
                            float wallHeight = 2.2f; // Altura ideal a la vista del jugador

                            notePos = transform.position + new Vector3(
                                cell.x * tileSize + dir.x * offsetDist,
                                wallHeight,
                                cell.y * tileSize + dir.y * offsetDist
                            );

                            // Pequeno desplazamiento aleatorio lateral para variedad
                            if (dir.x == 0) notePos.x += UnityEngine.Random.Range(-0.6f, 0.6f);
                            else            notePos.z += UnityEngine.Random.Range(-0.6f, 0.6f);

                            Vector3 faceDir = new Vector3(-dir.x, 0f, -dir.y);
                            noteRot = Quaternion.LookRotation(faceDir, Vector3.up);
                            
                            placed = true;
                            // Remover para no saturar el mismo pasillo
                            corridorList.RemoveAll(c => Vector2Int.Distance(c, cell) < 3);
                            break;
                        }
                    }
                    if (placed) break;
                }
            }

            // Fallback muy raro si no encontro lugar (casi imposible)
            if (!placed)
            {
                // Aparecer de forma segura en la habitacion inicial del jugador (evita generar debajo del mapa)
                notePos = transform.position + new Vector3(playerSpawnCell.x * tileSize, 1.4f, playerSpawnCell.y * tileSize);
                noteRot = Quaternion.identity;
            }

            // --- Creacion Visual ---
            GameObject parentObj = new GameObject("NotaCodigo_" + (i + 1) + "_Padre");
            parentObj.transform.position = notePos;
            parentObj.transform.rotation = noteRot;
            parentObj.transform.SetParent(GetItemsParent("Notes"));

            Material noteMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            noteMat.color = Color.white;
            if (paperTex != null)
                noteMat.SetTexture("_BaseMap", paperTex);
            noteMat.SetFloat("_Smoothness", 0.05f);

            GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualObj.name = "Visual";
            visualObj.transform.SetParent(parentObj.transform);
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localRotation = Quaternion.identity; 
            visualObj.transform.localScale = new Vector3(0.45f, 0.6f, 0.005f); // Papel delgado para evitar traspaso
            Destroy(visualObj.GetComponent<Collider>());
            visualObj.GetComponent<Renderer>().sharedMaterial = noteMat;

            // Script de interaccion
            var noteItem = parentObj.AddComponent<NoteItem>();
            noteItem.digitPosition = i + 1;
            noteItem.digitValue = int.Parse(correctKeypadCode[i].ToString());
            noteItem.interactDistance = 4.8f;

            // BoxCollider trigger
            BoxCollider bc = parentObj.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(2.0f, 2.0f, 2.0f);
            // Si esta acostado (Desk/Toilet), su rotacion en X es cercana a 90 grados. Si es asi, no desplazamos el collider.
            bool isLyingDown = Mathf.Abs(noteRot.eulerAngles.x - 90f) < 5f || Mathf.Abs(noteRot.eulerAngles.x - 270f) < 5f;
            
            if (!isLyingDown)
            {
                bc.center = new Vector3(0f, 0f, 0.5f); 
            }
            else
            {
                bc.center = Vector3.zero;
            }

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parentObj.transform);
            textObj.transform.localPosition = new Vector3(0f, 0f, 0.005f); // Delante del papel
            textObj.transform.localRotation = Quaternion.Euler(0, 180, 0); 
            textObj.transform.localScale = Vector3.one * 0.01f; // Tamano de texto estandar

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = $"? {noteItem.digitPosition} ?\n\n{noteItem.digitValue}";
            tm.characterSize = 1f;
            tm.fontSize = 72;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.1f, 0.1f, 0.1f); 

            // Asignar material con Z-Buffer Depth Test para que el texto NO se vea a traves de las paredes desde atras
            Renderer textRend = textObj.GetComponent<Renderer>();
            if (textRend != null && tm.font != null && tm.font.material != null)
            {
                Material occludedTextMat = new Material(Shader.Find("Sprites/Default"));
                occludedTextMat.mainTexture = tm.font.material.mainTexture;
                occludedTextMat.color = new Color(0.1f, 0.1f, 0.1f);
                textRend.sharedMaterial = occludedTextMat;
            } 
            
            Debug.Log($"MazeGenerator: Nota [{noteItem.digitPosition}] spawneada en {notePos}");
        }
    }


    /// <summary>
    /// Devuelve una lista de todas las celdas del laberinto que son accesibles a pie desde la celda de inicio.
    /// </summary>
    System.Collections.Generic.List<Vector2Int> GetAccessibleCells(Vector2Int startCell)
    {
        System.Collections.Generic.List<Vector2Int> accessible = new System.Collections.Generic.List<Vector2Int>();
        System.Collections.Generic.Queue<Vector2Int> queue = new System.Collections.Generic.Queue<Vector2Int>();

        queue.Enqueue(startCell);
        accessible.Add(startCell);

        Vector2Int[] directions = {
            new Vector2Int(0, 1),  // Norte
            new Vector2Int(0, -1), // Sur
            new Vector2Int(1, 0),  // Este
            new Vector2Int(-1, 0)  // Oeste
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                // Comprobar limites
                if (neighbor.x >= 0 && neighbor.x < width && neighbor.y >= 0 && neighbor.y < height)
                {
                    // Si es caminable y no ha sido visitada
                    if (grid[neighbor.x, neighbor.y] && !accessible.Contains(neighbor))
                    {
                        // Comprobar si hay una pared fisica bloqueando el paso entre las celdas
                        if (ShouldSpawnWallBetween(current.x, current.y, neighbor.x, neighbor.y))
                        {
                            continue; // Hay pared
                        }

                        accessible.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return accessible;
    }


    void SpawnSubGenerators()
    {
        System.Collections.Generic.List<Vector2Int> candidates = new System.Collections.Generic.List<Vector2Int>();
        
        // Obtener el conjunto de celdas que son realmente accesibles para el jugador a pie
        System.Collections.Generic.List<Vector2Int> accessibleCells = GetAccessibleCells(playerSpawnCell);

        // Los generadores deben ir unicamente en los pasillos principales, NUNCA dentro de las habitaciones
        foreach (Vector2Int cell in corridors)
        {
            if (!roomPositions.Contains(cell) && accessibleCells.Contains(cell))
            {
                // EVITAR ABSOLUTAMENTE spawnear dentro o cerca del ascensor (minimo 3 celdas de distancia)
                if (cell == elevatorCell || Vector2Int.Distance(cell, elevatorCell) < 3f)
                {
                    continue;
                }
                candidates.Add(cell);
            }
        }

        // Determinar cantidad de generadores segun el tamano de mapa (2 para Chico, 3 para Mediano, 4 para Grande)
        int numGenerators = (width <= 25) ? 2 : ((width <= 35) ? 3 : 4);
        
        if (candidates.Count < numGenerators)
        {
            Debug.LogError($"MazeGenerator: No hay suficientes celdas para colocar {numGenerators} subgeneradores.");
            return;
        }

        // BARAJAR candidatos para introducir aleatoriedad antes del algoritmo de dispersion
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2Int temp = candidates[i];
            int randIdx = UnityEngine.Random.Range(i, candidates.Count);
            candidates[i] = candidates[randIdx];
            candidates[randIdx] = temp;
        }

        // Distribucion inteligente maximizando la distancia minima entre ellos y el ascensor (Farthest-Point Selection)
        generatorCells.Clear();
        
        // El primer subgenerador se coloca en la celda candidata mas alejada del ascensor
        Vector2Int firstGenCell = candidates[0];
        float maxDistFromElev = -1f;
        foreach (Vector2Int cand in candidates)
        {
            float d = Vector2Int.Distance(cand, elevatorCell);
            if (d > maxDistFromElev)
            {
                maxDistFromElev = d;
                firstGenCell = cand;
            }
        }
        SpawnEnemy(playerSpawnCell);

        while (generatorCells.Count < numGenerators)
        {
            Vector2Int bestCandidate = candidates[0];
            float maxMinDist = -1f;
            bool foundSufficientlyDistant = false;

            foreach (Vector2Int cand in candidates)
            {
                if (generatorCells.Contains(cand)) continue;

                // Encontrar la distancia minima al ascensor y a los generadores ya colocados
                float minDist = Vector2Int.Distance(cand, elevatorCell);
                float distToOtherGens = float.MaxValue;
                foreach (Vector2Int gen in generatorCells)
                {
                    float d = Vector2Int.Distance(cand, gen);
                    if (d < minDist) minDist = d;
                    if (d < distToOtherGens) distToOtherGens = d;
                }

                // Intentar enforzar que esten a al menos 6 celdas de distancia entre si
                if (distToOtherGens >= 6.0f)
                {
                    if (!foundSufficientlyDistant || minDist > maxMinDist)
                    {
                        maxMinDist = minDist;
                        bestCandidate = cand;
                        foundSufficientlyDistant = true;
                    }
                }
                else if (!foundSufficientlyDistant)
                {
                    // Fallback si no hay candidatos lejanos: buscar el que maximice la separacion
                    if (minDist > maxMinDist)
                    {
                        maxMinDist = minDist;
                        bestCandidate = cand;
                    }
                }
            }
            generatorCells.Add(bestCandidate);
        }

        // Sincronizar generatorACell y generatorBCell para mantener compatibilidad hacia atras
        generatorACell = generatorCells[0];
        generatorBCell = generatorCells.Count > 1 ? generatorCells[1] : generatorACell;

        // Cargar materiales industriales desde Resources
        Material bodyMat = Resources.Load<Material>("Mat_Bed_Metal_01");
        Material panelMat = Resources.Load<Material>("Material_CajaFusibles");
        
        if (bodyMat == null)
        {
            bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.color = new Color(0.2f, 0.22f, 0.24f);
            bodyMat.SetFloat("_Metallic", 0.8f);
        }
        if (panelMat == null)
        {
            panelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            panelMat.color = new Color(0.1f, 0.1f, 0.1f);
        }

        Material blackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        blackMat.color = new Color(0.05f, 0.05f, 0.05f);
        blackMat.SetFloat("_Smoothness", 0.15f);

        string[] names = { "A", "B", "C", "D" };
        for (int i = 0; i < numGenerators; i++)
        {
            Vector2Int cell = generatorCells[i];
            
            // ESCALA AUMENTADA: Para que no parezcan cajas diminutas en pasillos gigantes de escala 4.
            Vector3 worldPos = transform.position + new Vector3(cell.x * tileSize, 0.325f * mapScale, cell.y * tileSize);

            GameObject genRoot = new GameObject("SubGenerador_" + names[i]);
            genRoot.transform.position = worldPos;
            genRoot.transform.rotation = Quaternion.identity;
            genRoot.transform.SetParent(transform.Find("Generated_Hospital_Map")); // Emparentar para limpieza al destruir mapa

            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Chasis";
            chassis.transform.SetParent(genRoot.transform, false);
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localScale = new Vector3(0.9f, 0.65f, 0.9f) * mapScale;
            chassis.GetComponent<Renderer>().sharedMaterial = bodyMat;

            GameObject panelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelObj.name = "PanelControl";
            panelObj.transform.SetParent(transform, false);
            panelObj.transform.localPosition = new Vector3(0f, 0.08f * mapScale, -0.452f * mapScale);
            panelObj.transform.localScale = new Vector3(0.65f, 0.4f, 0.04f) * mapScale;
            panelObj.GetComponent<Renderer>().sharedMaterial = panelMat;

            GameObject leftVent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftVent.name = "VentilacionIzq";
            leftVent.transform.SetParent(genRoot.transform, false);
            leftVent.transform.localPosition = new Vector3(-0.452f * mapScale, 0f, 0f);
            leftVent.transform.localScale = new Vector3(0.02f, 0.4f, 0.65f) * mapScale;
            leftVent.GetComponent<Renderer>().sharedMaterial = blackMat;

            GameObject rightVent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightVent.name = "VentilacionDer";
            rightVent.transform.SetParent(genRoot.transform, false);
            rightVent.transform.localPosition = new Vector3(0.452f * mapScale, 0f, 0f);
            rightVent.transform.localScale = new Vector3(0.02f, 0.4f, 0.65f) * mapScale;
            rightVent.GetComponent<Renderer>().sharedMaterial = blackMat;

            // Anadir script SubGenerator y asignar su etiqueta
            SubGenerator subGenScript = genRoot.AddComponent<SubGenerator>();
            subGenScript.generatorName = names[i];
            subGenScript.interactDistance = 5.0f; // Escala corregida de 2.5 a 5.0 para evitar bloqueo por el gran colisionador

            // CREAR LA BOMBILLA Y LA LUZ DE ESTADO (Para que brille en rojo y cambie a verde)
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bombilla";
            bulb.transform.SetParent(genRoot.transform, false);
            // Colocada al frente del panel de control
            bulb.transform.localPosition = new Vector3(0f, 0.28f * mapScale, -0.48f * mapScale);
            bulb.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f) * mapScale;

            // Crear material emissive independiente para esta bombilla
            Material bulbMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bulbMat.color = Color.red;
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetColor("_EmissionColor", Color.red * 1.5f);
            bulb.GetComponent<Renderer>().sharedMaterial = bulbMat;
            subGenScript.lightRenderer = bulb.GetComponent<Renderer>();

            // Crear el punto de luz dinamico
            GameObject lightObj = new GameObject("StatusLight");
            lightObj.transform.SetParent(genRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.28f * mapScale, -0.6f * mapScale);
            Light statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.color = Color.red;
            statusLight.range = 2.5f * mapScale;
            statusLight.intensity = 2.0f;
            subGenScript.statusLight = statusLight;

            // CREAR LA PALANCA FSICA MECNICA (Para que gire al accionarlo)
            GameObject leverHinge = new GameObject("Palanca_Hinge");
            leverHinge.transform.SetParent(panelObj.transform, false);
            leverHinge.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            leverHinge.transform.localRotation = Quaternion.Euler(35f, 0f, 0f); // Inclinado arriba por defecto

            GameObject leverBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leverBar.name = "Palanca_Fisica";
        leverBar.transform.SetParent(leverHinge.transform, false);
            leverBar.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            leverBar.GetComponent<Renderer>().sharedMaterial = blackMat;

            Debug.Log($"MazeGenerator: SubGenerador {names[i]} instanciado en celda {cell}");
        }
    }

    void SpawnBeds()
    {
        bedCells.Clear();

        if (bedPrefab == null)
        {
            Debug.LogError("MazeGenerator: bedPrefab no asignado. No se generaron camas.");
            return;
        }

        Transform mapParent = transform.Find("Generated_Hospital_Map");

        // 1. Amueblar y decorar cada habitacion especial segun su tipo
        for (int rIdx = 0; rIdx < roomPivots.Count; rIdx++)
        {
            Vector2Int pivot = roomPivots[rIdx];
            Vector2Int doorCell = roomDoors[pivot];
            Vector2Int backCell = pivot + (pivot - doorCell);

            // Registrar celda trasera como zona de cama/escondite
            bedCells.Add(backCell);

            Vector3 backCellCenter = transform.position + new Vector3(backCell.x * tileSize, 0.05f, backCell.y * tileSize);
            Vector3 roomForward = new Vector3(pivot.x - doorCell.x, 0f, pivot.y - doorCell.y).normalized;
            Vector3 roomRight = Vector3.Cross(Vector3.up, roomForward).normalized;

            RoomType currentType = RoomType.PatientRoom;
            if (roomTypes.ContainsKey(pivot))
            {
                currentType = roomTypes[pivot];
            }

            // FALLBACK
            if (currentType == RoomType.Office && officeDeskPrefab == null)
            {
                currentType = RoomType.PatientRoom;
            }
            else if (currentType == RoomType.Bathroom && bathroomToiletPrefab == null)
            {
                currentType = RoomType.PatientRoom;
            }

            int startIndex = availableSpawnPoints.Count;

            if (currentType == RoomType.PatientRoom)
            {
                // -- HABITACION DE PACIENTES (Escala humana 1:1) --
                Quaternion bedRot = Quaternion.LookRotation(-roomForward, Vector3.up);
                GameObject bed = Instantiate(bedPrefab, backCellCenter, bedRot, mapParent);
                bed.name = "PatientBed_" + rIdx;
                bed.transform.localScale = Vector3.one;
                SetupBedComponent(bed, 1.0f);

                // Cama apoyada al fondo
                bed.transform.position = backCellCenter + roomForward * 0.3f + roomRight * 0.6f;

                if (medCabinetPrefab != null)
                {
                    // Mesita/Armario al lado de la cama
                    Vector3 medPos = backCellCenter + roomForward * 0.3f - roomRight * 0.8f; 
                    GameObject med = Instantiate(medCabinetPrefab, medPos, bedRot, mapParent);
                    med.transform.localScale = Vector3.one; 
                    med.name = "MedCabinet_" + rIdx;

                    availableSpawnPoints.Add(new ItemSpawnPoint {
                        position = medPos + Vector3.up * 0.85f,
                        rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0),
                        type = SpawnPointType.Desk
                    });
                }
            }
            else if (currentType == RoomType.Office)
            {
                // -- OFICINA MEDICA (Escala humana 1:1) --
                float deskScale = 0.35f;

                GameObject desk = null;
                if (officeDeskPrefab != null)
                {
                    Vector3 deskPos = backCellCenter + roomForward * 0.5f; 
                    Quaternion deskRot = Quaternion.LookRotation(-roomForward, Vector3.up) * Quaternion.Euler(0, -90, 0);
                    desk = Instantiate(officeDeskPrefab, deskPos, deskRot, mapParent);
                    desk.transform.localScale = Vector3.one * deskScale;
                    desk.name = "OfficeDesk_" + rIdx;

                    availableSpawnPoints.Add(new ItemSpawnPoint {
                        position = deskPos + Vector3.up * 0.75f - roomForward * 0.2f,
                        rotation = Quaternion.LookRotation(roomForward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f),
                        type = SpawnPointType.Desk
                    });
                }

                if (officeChairPrefab != null)
                {
                    Vector3 chairPos = backCellCenter - roomForward * 0.2f; 
                    Quaternion chairRot = Quaternion.LookRotation(roomForward, Vector3.up) * Quaternion.Euler(0, -90, 0); 
                    GameObject chair = Instantiate(officeChairPrefab, chairPos, chairRot, mapParent);
                    chair.transform.localScale = Vector3.one * deskScale;
                    chair.name = "OfficeChair_" + rIdx;
                }

                if (officeCabinetPrefab != null)
                {
                    Vector3 cabinetPos = backCellCenter - roomRight * 0.9f + roomForward * 0.3f; 
                    Quaternion cabinetRot = Quaternion.LookRotation(roomRight, Vector3.up); 
                    GameObject cabinet = Instantiate(officeCabinetPrefab, cabinetPos, cabinetRot, mapParent);
                    cabinet.transform.localScale = Vector3.one * deskScale;
                    cabinet.name = "OfficeCabinet_" + rIdx;

                    availableSpawnPoints.Add(new ItemSpawnPoint {
                        position = backCellCenter + roomRight * 0.5f + roomForward * 0.2f + Vector3.up * 0.02f,
                        rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0),
                        type = SpawnPointType.Floor
                    });
                }
            }
            else if (currentType == RoomType.Bathroom)
            {
                // -- BAÑO DEL HOSPITAL (Escala humana 1:1) --
                if (bathroomToiletPrefab != null)
                {
                    Vector3 toiletPos = backCellCenter - roomRight * 0.8f + roomForward * 0.4f;
                    Quaternion toiletRot = Quaternion.LookRotation(roomRight, Vector3.up) * Quaternion.Euler(0, -90, 0);
                    GameObject toilet = Instantiate(bathroomToiletPrefab, toiletPos, toiletRot, mapParent);
                    toilet.transform.localScale = Vector3.one;
                    toilet.name = "BathroomToilet_" + rIdx;
                }

                if (bathroomSinkPrefab != null)
                {
                    Vector3 sinkPos = backCellCenter + roomRight * 0.8f + roomForward * 0.4f;
                    Quaternion sinkRot = Quaternion.LookRotation(-roomRight, Vector3.up) * Quaternion.Euler(0, -90, 0);
                    GameObject sink = Instantiate(bathroomSinkPrefab, sinkPos, sinkRot, mapParent);
                    sink.transform.localScale = Vector3.one;
                    sink.name = "BathroomSink_" + rIdx;
                }

                if (bathroomMirrorPrefab != null)
                {
                    Vector3 mirrorPos = backCellCenter + roomRight * 0.88f + roomForward * 0.4f + Vector3.up * 1.5f;
                    Quaternion mirrorRot = Quaternion.LookRotation(-roomRight, Vector3.up) * Quaternion.Euler(0, -90, 0);
                    GameObject mirror = Instantiate(bathroomMirrorPrefab, mirrorPos, mirrorRot, mapParent);
                    mirror.transform.localScale = Vector3.one;
                    mirror.name = "BathroomMirror_" + rIdx;

                    availableSpawnPoints.Add(new ItemSpawnPoint {
                        position = new Vector3(mirrorPos.x, 1.4f, mirrorPos.z) - roomRight * 0.2f,
                        rotation = Quaternion.LookRotation(-roomRight, Vector3.up),
                        type = SpawnPointType.Wall
                    });
                }
            }
            
            bool isDir = (rIdx == roomPivots.Count - 1);
            if (isDir)
            {
                for (int i = startIndex; i < availableSpawnPoints.Count; i++)
                {
                    availableSpawnPoints[i].isDirector = true;
                }
            }
        }

        // 2. Colocar camas en pasillos amplios (intersecciones / zonas anchas con al menos 3 celdas conectadas)
        int spawnedCorridorBeds = 0;
        
        System.Collections.Generic.List<Vector2Int> corridorList = new System.Collections.Generic.List<Vector2Int>(corridors);
        for (int i = 0; i < corridorList.Count; i++)
        {
            Vector2Int temp = corridorList[i];
            int randIdx = UnityEngine.Random.Range(i, corridorList.Count);
            corridorList[i] = corridorList[randIdx];
            corridorList[randIdx] = temp;
        }

        foreach (Vector2Int cell in corridorList)
        {
            if (spawnedCorridorBeds >= 2) break;

            if (cell.x <= 3 && cell.y <= 3) continue; 
            if (roomPositions.Contains(cell)) continue;
            if (generatorCells.Contains(cell) || cell == elevatorCell) continue; // Excluir infaliblemente TODOS los subgeneradores (A, B, C, D...) y el ascensor
            // Excluir tambien las celdas vecinas al ascensor (no poner camas FRENTE a la puerta del ascensor)
            if (Mathf.Abs(cell.x - elevatorCell.x) + Mathf.Abs(cell.y - elevatorCell.y) <= 1) continue;

            // Determinar conectividad: zona amplia = interseccion de 3 o 4 caminos
            int openNeighbors = 0;
            System.Collections.Generic.List<Vector3> wallDirections = new System.Collections.Generic.List<Vector3>();

            if (cell.x + 1 < width && grid[cell.x + 1, cell.y]) openNeighbors++; else wallDirections.Add(Vector3.right);
            if (cell.x - 1 >= 0 && grid[cell.x - 1, cell.y]) openNeighbors++; else wallDirections.Add(-Vector3.right);
            if (cell.y + 1 < height && grid[cell.x, cell.y + 1]) openNeighbors++; else wallDirections.Add(Vector3.forward);
            if (cell.y - 1 >= 0 && grid[cell.x, cell.y - 1]) openNeighbors++; else wallDirections.Add(-Vector3.forward);

            if (openNeighbors < 4) continue; // Solo cruces de 4 caminos para evitar obstruccion total del pasillo
            if (wallDirections.Count == 0) continue; // Necesitamos al menos una pared para apoyar la cama

            // Elegir una pared al azar para apoyar la cama
            Vector3 pushDir = wallDirections[UnityEngine.Random.Range(0, wallDirections.Count)];
            float yaw = 0f;

            // Decidir al azar si la cama va paralela (acostada) o perpendicular (mirando hacia enfrente de lado)
            bool isParallel = UnityEngine.Random.value < 0.5f;

            if (pushDir == Vector3.right) // Pared Este
            {
                yaw = isParallel ? 0f : -90f;
            }
            else if (pushDir == -Vector3.right) // Pared Oeste
            {
                yaw = isParallel ? 180f : 90f;
            }
            else if (pushDir == Vector3.forward) // Pared Norte
            {
                yaw = isParallel ? 90f : 180f;
            }
            else if (pushDir == -Vector3.forward) // Pared Sur
            {
                yaw = isParallel ? 270f : 0f;
            }

            bedCells.Add(cell);

            Vector3 cellCenter = transform.position + new Vector3(cell.x * tileSize, 0.05f, cell.y * tileSize);
            Quaternion bedRot = Quaternion.Euler(0f, yaw, 0f);

            // Instanciar en el centro para calcular el BoxCollider
            GameObject corridorBed = Instantiate(bedPrefab, cellCenter, bedRot, transform.Find("Generated_Hospital_Map"));
            corridorBed.name = "CamaPasillo_" + spawnedCorridorBeds;
            corridorBed.transform.localScale = Vector3.one;
            SetupBedComponent(corridorBed, 1.0f);

            // Obtener el BoxCollider calculado para saber el tamano real de la cama
            BoxCollider box = corridorBed.GetComponent<BoxCollider>();
            float halfLength = 1.0f;
            float halfWidth = 0.5f;
            if (box != null)
            {
                halfLength = box.size.z / 2f;
                halfWidth = box.size.x / 2f;
            }

            // Calcular el desplazamiento dinamico exacto segun la orientacion (dejando 20cm de margen de la pared)
            float offsetAmount = isParallel ? ((0.5f * tileSize) - halfWidth - 0.20f) : ((0.5f * tileSize) - halfLength - 0.20f);
            corridorBed.transform.position = cellCenter + pushDir * offsetAmount;

            spawnedCorridorBeds++;
            Debug.Log($"MazeGenerator: Cama de pasillo colocada dinamicamente en zona amplia {cell} (Pared: {pushDir}, Paralela: {isParallel})");
        }
    }

        void SetupBedComponent(GameObject bedObj, float scale)
    {
        // 1. Asignar etiquetas BedArea recursivamente
        bedObj.tag = "BedArea";
        foreach (Transform child in bedObj.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.tag = "BedArea";
        }

        // 2. Calcular limites en el espacio local del modelo (inmune a rotaciones del mundo)
        MeshFilter[] mfs = bedObj.GetComponentsInChildren<MeshFilter>(true);
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasMesh = false;
        
        foreach (MeshFilter mf in mfs)
        {
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds subBounds = mf.sharedMesh.bounds;
                // Transformar el centro y el tamano del sub-mesh al espacio local del bedObj principal
                Vector3 subCenterInParent = bedObj.transform.InverseTransformPoint(mf.transform.TransformPoint(subBounds.center));
                Vector3 subSizeInParent = bedObj.transform.InverseTransformVector(mf.transform.TransformVector(subBounds.size));
                
                // Asegurar valores absolutos para el tamano
                subSizeInParent = new Vector3(Mathf.Abs(subSizeInParent.x), Mathf.Abs(subSizeInParent.y), Mathf.Abs(subSizeInParent.z));
                Bounds subBoundsInParent = new Bounds(subCenterInParent, subSizeInParent);
                
                if (!hasMesh)
                {
                    localBounds = subBoundsInParent;
                    hasMesh = true;
                }
                else
                {
                    localBounds.Encapsulate(subBoundsInParent);
                }
            }
        }

        // 3. Centrar visualmente el modelo corrigiendo cualquier desfase de pivote del archivo FBX original
        if (hasMesh)
        {
            Vector3 offset = localBounds.center;
            // Si el pivote esta desfasado, mover los hijos visuales para centrar el modelo fisicamente en (0, Y, 0)
            if (Mathf.Abs(offset.x) > 0.01f || Mathf.Abs(offset.z) > 0.01f)
            {
                foreach (Transform child in bedObj.transform)
                {
                    if (child.name != "HidePosition")
                    {
                        child.localPosition -= new Vector3(offset.x, 0f, offset.z);
                    }
                }
                // Ajustar el centro local calculado a cero tras la correccion
                localBounds.center = new Vector3(0f, localBounds.center.y, 0f);
            }
        }

        // 4. Generar un BoxCollider dinamico basado en las mallas centradas
        BoxCollider box = bedObj.GetComponent<BoxCollider>();
        if (box == null) box = bedObj.AddComponent<BoxCollider>();

        if (hasMesh)
        {
            box.center = localBounds.center;
            box.size = localBounds.size;
            box.isTrigger = false;
        }
        else
        {
            box.center = new Vector3(0f, 0.45f, 0f);
            box.size = new Vector3(1.2f, 0.9f, 2.2f);
            box.isTrigger = false;
        }

        // 5. Agregar componente Bed si no existe
        Bed bedScript = bedObj.GetComponent<Bed>();
        if (bedScript == null)
        {
            bedScript = bedObj.AddComponent<Bed>();
        }

        // 6. Crear el punto de ocultamiento (HidePosition) debajo de la cama
        Transform hideTrans = bedObj.transform.Find("HidePosition");
        if (hideTrans == null)
        {
            GameObject hideObj = new GameObject("HidePosition");
            hideObj.transform.SetParent(bedObj.transform, false);
            // Posicionar al jugador bien metido bajo el centro de la cama (15cm del piso)
            hideObj.transform.localPosition = new Vector3(0f, 0.15f, 0f); 
            hideObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            hideTrans = hideObj.transform;
        }

        bedScript.hidePosition = hideTrans;
    }

    // Evitar softlocks: Retorna cantidad de fusibles activos en el suelo
        public int GetActiveFusesCount()
    {
        activeFuses.RemoveAll(item => item == null);
        return activeFuses.Count;
    }

    // Spawnea un fusible de repuesto de emergencia alejado del jugador

    // Metodo BFS para contar celdas de pasillo alcanzables desde una posicion (evita spawnear al jugador en islas aisladas)
        private int GetReachableCorridorCount(Vector2Int start)
    {
        System.Collections.Generic.List<Vector2Int> visited = new System.Collections.Generic.List<Vector2Int>();
        System.Collections.Generic.Queue<Vector2Int> queue = new System.Collections.Generic.Queue<Vector2Int>();
        
        queue.Enqueue(start);
        visited.Add(start);
        
        int count = 0;
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            count++;
            
            Vector2Int[] neighbors = {
                new Vector2Int(current.x, current.y + 1),
                new Vector2Int(current.x, current.y - 1),
                new Vector2Int(current.x + 1, current.y),
                new Vector2Int(current.x - 1, current.y)
            };
            
            foreach (var n in neighbors)
            {
                if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height)
                {
                    if (grid[n.x, n.y] && !roomPositions.Contains(n) && !visited.Contains(n))
                    {
                        visited.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }
        }
        return count;
    }
        public void SpawnEmergencyFuse()
    {
        if (fusePrefab == null || corridors.Count == 0) return;

        // Encontrar la celda actual en tiempo real del jugador
        Vector2Int playerCell = playerSpawnCell;
        CharacterController playerCC = FindObjectOfType<CharacterController>();
        if (playerCC != null)
        {
            Vector3 localPos = playerCC.transform.position - transform.position;
            playerCell = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(localPos.x / tileSize), 0, width - 1),
                Mathf.Clamp(Mathf.RoundToInt(localPos.z / tileSize), 0, height - 1)
            );
        }

        // Buscar celdas de pasillo alejadas del jugador (minimo 4 celdas)
        System.Collections.Generic.List<Vector2Int> candidates = new System.Collections.Generic.List<Vector2Int>();
        foreach (Vector2Int cell in corridors)
        {
            if (roomPositions.Contains(cell) || bedCells.Contains(cell)) continue;
            if (cell == generatorACell || cell == generatorBCell || cell == elevatorCell) continue;

            float dist = Vector2Int.Distance(cell, playerCell);
            if (dist >= 4f) 
            {
                candidates.Add(cell);
            }
        }

        if (candidates.Count > 0)
        {
            Vector2Int spawnCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            float heightOffset = 0.05f * mapScale;
            Vector3 spawnPos = transform.position + new Vector3(spawnCell.x * tileSize, heightOffset, spawnCell.y * tileSize);
            
            Quaternion randomRot = Quaternion.Euler(
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-5f, 5f)
            );

            GameObject fuse = Instantiate(fusePrefab, spawnPos, randomRot);
            fuse.name = "FusibleItem_Emergencia";
            fuse.transform.localScale = fuse.transform.localScale * mapScale;
            
            if (fuse.GetComponent<FuseItem>() == null)
            {
                fuse.AddComponent<FuseItem>();
            }

            activeFuses.Add(fuse);
            Debug.LogWarning($"HospitalMazeGenerator: FUSIBLE DE EMERGENCIA SPAWNEADO en celda {spawnCell} (dist={Vector2Int.Distance(spawnCell, playerCell)} celdas) para evitar softlock.");
        }
    }

        public int GetActiveBatteriesCount()
    {
        activeBatteries.RemoveAll(item => item == null);
        return activeBatteries.Count;
    }

        public void SpawnEmergencyBattery()
    {
        if (batteryPrefab == null || corridors.Count == 0) return;

        // Encontrar la celda actual en tiempo real del jugador
        Vector2Int playerCell = playerSpawnCell;
        CharacterController playerCC = FindObjectOfType<CharacterController>();
        if (playerCC != null)
        {
            Vector3 localPos = playerCC.transform.position - transform.position;
            playerCell = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(localPos.x / tileSize), 0, width - 1),
                Mathf.Clamp(Mathf.RoundToInt(localPos.z / tileSize), 0, height - 1)
            );
        }

        // Buscar celdas de pasillo alejadas del jugador (minimo 4 celdas)
        System.Collections.Generic.List<Vector2Int> candidates = new System.Collections.Generic.List<Vector2Int>();
        foreach (Vector2Int cell in corridors)
        {
            if (roomPositions.Contains(cell) || bedCells.Contains(cell)) continue;
            if (cell == generatorACell || cell == generatorBCell || cell == elevatorCell) continue;

            float dist = Vector2Int.Distance(cell, playerCell);
            if (dist >= 4f) 
            {
                candidates.Add(cell);
            }
        }

        if (candidates.Count > 0)
        {
            Vector2Int spawnCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            float heightOffset = 0.05f * mapScale;
            Vector3 spawnPos = transform.position + new Vector3(spawnCell.x * tileSize, heightOffset, spawnCell.y * tileSize);
            
            Quaternion randomRot = Quaternion.Euler(
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-5f, 5f)
            );

            GameObject battery = Instantiate(batteryPrefab, spawnPos, randomRot);
            battery.name = "BateriaItem_Emergencia";
            battery.transform.SetParent(GetItemsParent("Batteries"));
            battery.transform.localScale = battery.transform.localScale * mapScale;
            
            if (battery.GetComponent<BatteryItem>() == null)
            {
                battery.AddComponent<BatteryItem>();
            }

            activeBatteries.Add(battery);
            Debug.LogWarning($"HospitalMazeGenerator: BATERÍA DE EMERGENCIA SPAWNEADA en celda {spawnCell} (dist={Vector2Int.Distance(spawnCell, playerCell)} celdas) para evitar softlock.");
        }
    }

    // --- PANTALLA DE CARGA PROCEDURAL VHS ---
    System.Collections.IEnumerator Geneóne()
    {
        isGeneratingMap = true;
        loadingStep = 0;
        loadingProgressText = "Inicializando base de datos del hospital...";
        yield return null;

        // Desactivar temporalmente los scripts de IA del enemigo durante la carga para evitar errores de NavMesh no inicializado
        System.Collections.Generic.List<MonoBehaviour> disabledEnemyScripts = new System.Collections.Generic.List<MonoBehaviour>();
        if (enemyObj != null)
        {
            MonoBehaviour[] enemyScripts = enemyObj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in enemyScripts)
            {
                if (script != null && (script.GetType().Name == "EnemyController" || script.GetType().Name == "EnemyAIBookHead" || script.GetType().Name == "Enemy2AI"))
                {
                    script.enabled = false;
                    disabledEnemyScripts.Add(script);
                }
            }
        }

        // Desactivar controles e interfaces del jugador temporalmente
        GameObject player = playerObj;
        if (player == null)
        {
            playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            player = playerObj;
        }

        MonoBehaviour controllerScript = null;
        if (player != null)
        {
            
            if (controllerScript != null) controllerScript.enabled = false;
        }

        disabledHUDCanvases.Clear();
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.activeSelf && !canvas.gameObject.name.Contains("Menu"))
            {
                canvas.gameObject.SetActive(false);
                disabledHUDCanvases.Add(canvas);
            }
        }

        cachedCanvasInputs = GameObject.Find("UICanvas_StarterAssetsInputs_Required");
        if (cachedCanvasInputs == null) cachedCanvasInputs = GameObject.Find("UICanvas_StarterAssetsInputs");
        if (cachedCanvasInputs != null && cachedCanvasInputs.activeSelf)
        {
            cachedCanvasInputs.SetActive(false);
        }

        yield return new WaitForSeconds(0.4f);

        // Paso 1: Geneón DFS
        loadingStep = 1;
        loadingProgressText = "Trazando estructura del laberinto (Depth-First Search)...";
        yield return null;

        // Inicializar código del keypad al principio
        correctKeypadCode = "";
        for (int i = 0; i < 7; i++)
        {
            correctKeypadCode += UnityEngine.Random.Range(0, 10).ToString();
        }
        Debug.Log("HospitalMazeGenerator: Clave generada anticipadamente en Corrutina = " + correctKeypadCode);

        spawnedRooms.Clear();
        availableSpawnPoints.Clear();
        grid = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = false;
            }
        }

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int startCell = new Vector2Int(1, 1);
        grid[startCell.x, startCell.y] = true;
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                Vector2Int chosen = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
                int wallX = current.x + (chosen.x - current.x) / 2;
                int wallY = current.y + (chosen.y - current.y) / 2;
                grid[wallX, wallY] = true;
                grid[chosen.x, chosen.y] = true;
                stack.Push(chosen);
            }
            else
            {
                stack.Pop();
            }
        }
        yield return null;

        // Paso 2: Pasillos y Lobbies
        loadingStep = 2;
        loadingProgressText = "Carveando salas de espera y ensanchando pasillos principales...";
        yield return null;

        CarveLobbies();
        CreateLoops();
        CarveWiderCorridors();
        PlaceRooms();
        yield return null;

        // Paso 3: Instanciacion fisica del mapa
        loadingStep = 3;
        loadingProgressText = "Instanciando paredes, suelos y bombillas industriales...";
        yield return null;

        DetermineElevatorCell();
        BuildPhysicalMap();
        yield return null;

        // Paso 4: Geneón de NavMesh
        loadingStep = 4;
        loadingProgressText = "Calculando NavMesh dinámico para el monstruo...";
        yield return null;

        if (navMeshSurface != null)
        {
            if (enemyObj != null)
            {
                UnityEngine.AI.NavMeshAgent agentComp = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agentComp != null)
                {
                    // // // // navMeshSurface.agentTypeID = agentTypeID;
                }
            }

            // Desactivar temporalmente los colliders de las puertas
            ProceduralDoorInteract[] allDoors = FindObjectsOfType<ProceduralDoorInteract>();
            System.Collections.Generic.List<Collider> disabledColliders = new System.Collections.Generic.List<Collider>();
            foreach (var door in allDoors)
            {
                if (door != null)
                {
                    Collider[] cols = door.GetComponents<Collider>();
                    foreach (var c in cols)
                    {
                        if (c != null && c.enabled)
                        {
                            c.enabled = false;
                            disabledColliders.Add(c);
                        }
                    }
                }
            }

            // Desactivar tambien temporalmente los colliders del techo y lamparas colgantes para evitar que el horneado
            // piense que el espacio es verticalmente intransitable en zonas de pasillo ancho o vestibulo (Lobby)
            Collider[] allSceneColliders = FindObjectsOfType<Collider>();
            foreach (var c in allSceneColliders)
            {
                if (c != null && c.enabled)
                {
                    string nameLower = c.gameObject.name.ToLower();
                    if (nameLower.Contains("ceiling") || nameLower.Contains("techo") || nameLower.Contains("lamp") || nameLower.Contains("luz") || nameLower.Contains("light") || nameLower.Contains("keypad") || nameLower.Contains("botonera"))
                    {
                        c.enabled = false;
                        disabledColliders.Add(c);
                    }
                }
            }

            navMeshSurface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.BuildNavMesh();

            // Re-activar
            foreach (var c in disabledColliders)
            {
                if (c != null) c.enabled = true;
            }
        }
        yield return null;

        // Paso 5: Distribucion de subgeneradores y ascensor
        loadingStep = 5;
        loadingProgressText = "Posicionando subgeneradores y calibrando escape de ascensor...";
        yield return null;

        SpawnSubGenerators();
        SpawnElevator();
        yield return null;

        // Paso 6: Items y entidades
        loadingStep = 6;
        loadingProgressText = "Distribuyendo fusibles, baterías y notas de registro...";
        yield return null;

        SpawnEntitiesAndItems();
        SpawnNotes();
        yield return new WaitForSeconds(0.4f);

        // Paso 7: Carga completa
        loadingStep = 7;
        loadingProgressText = "Grabación de videocámara inicializada con éxito.";
        yield return new WaitForSeconds(0.6f);

        // Reactivar controles e interfaces
        if (controllerScript != null) controllerScript.enabled = true;
        foreach (var script in disabledEnemyScripts)
        {
            if (script != null) script.enabled = true;
        }
        foreach (Canvas canvas in disabledHUDCanvases)
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
        }
        if (cachedCanvasInputs != null) cachedCanvasInputs.SetActive(true);

        // Auto-anadir Brújula HUD Minimalista al finalizar la geneón
        if (gameObject.GetComponent<BackroomsCompassHUD>() == null)
        {
            gameObject.AddComponent<BackroomsCompassHUD>();
        }

        isGeneratingMap = false;
    }

    void OnGUI()
    {
        if (isGeneratingMap)
        {
            DrawLoadingScreen();
        }
    }

    void DrawLoadingScreen()
    {
        // Fondo negro sólido para toda la pantalla
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Estilos de texto
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.85f, 0.05f, 0.05f); // Rojo sangre VHS

        GUIStyle progressStyle = new GUIStyle();
        progressStyle.fontSize = 16;
        progressStyle.alignment = TextAnchor.MiddleCenter;
        progressStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        GUIStyle barStyle = new GUIStyle();
        barStyle.fontSize = 18;
        barStyle.fontStyle = FontStyle.Bold;
        barStyle.alignment = TextAnchor.MiddleCenter;
        barStyle.normal.textColor = new Color(0.1f, 0.85f, 0.2f); // Verde neón VHS

        // Dibujar en el centro
        GUILayout.BeginArea(new Rect(0, Screen.height / 2 - 120, Screen.width, 240));
        
        GUILayout.Label("CARGANDO REGISTRO VHS...", titleStyle);
        GUILayout.Space(20);
        
        // Puntero de consola parpadeante
        string blinkCursor = (Time.time % 0.8f < 0.4f) ? "_" : " ";
        GUILayout.Label($"> {loadingProgressText}{blinkCursor}", progressStyle);
        
        GUILayout.Space(30);
        
        // Barra de progreso procedural
        string progressBar = GetLoadingProgressBar();
        GUILayout.Label(progressBar, barStyle);

        GUILayout.EndArea();
    }

    string GetLoadingProgressBar()
    {
        int totalSteps = 7;
        float percent = (float)loadingStep / totalSteps * 100f;
        string bar = " [";
        for (int i = 0; i < totalSteps; i++)
        {
            if (i <= loadingStep)
            {
                bar += "■ ";
            }
            else
            {
                bar += "■ ";
            }
        }
        bar += $"]  {Mathf.RoundToInt(percent)}%";
        return bar;
    }

    public Transform GetItemsParent(string subfolder = "")
    {
        Transform mapParent = transform.Find("Generated_Hospital_Map");
        Transform baseParent = mapParent != null ? mapParent : transform;

        Transform itemsParent = baseParent.Find("Generated_Items");
        if (itemsParent == null)
        {
            GameObject itemsObj = new GameObject("Generated_Items");
            itemsObj.transform.SetParent(baseParent, false);
            itemsParent = itemsObj.transform;
        }

        if (!string.IsNullOrEmpty(subfolder))
        {
            Transform sub = itemsParent.Find(subfolder);
            if (sub == null)
            {
                GameObject subObj = new GameObject(subfolder);
                subObj.transform.SetParent(itemsParent, false);
                sub = subObj.transform;
            }
            return sub;
        }

        return itemsParent;
    }
}

public class DynamicWallLightReceiver : MonoBehaviour
{
    public Material baseMaterial;
    private Light targetLight;
    private Material instancedMat;
    private bool wasLit = true;

    void Start()
    {
        // Crear una instancia unica del material para este objeto
        if (baseMaterial != null)
        {
            instancedMat = Instantiate(baseMaterial);
            GetComponent<Renderer>().sharedMaterial = instancedMat;
        }

        // Buscar la luz de techo de la habitacion mas cercana (dentro de 10 metros)
        float minDist = 10f;
        foreach (Light l in FindObjectsOfType<Light>())
        {
            // Omitir la linterna del jugador
            if (l.gameObject.name.Contains("Flashlight") || l.gameObject.name.Contains("Linterna") || l.CompareTag("Player"))
                continue;

            float dist = Vector3.Distance(transform.position, l.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                targetLight = l;
            }
        }
        
        // Forzar actualizacion inicial
        UpdateLighting(true);
    }

    void Update()
    {
        UpdateLighting(false);
    }

    void UpdateLighting(bool force)
    {
        if (instancedMat == null) return;

        // Determinar si la luz esta encendida y activa
        bool isLit = targetLight != null && targetLight.gameObject.activeInHierarchy && targetLight.enabled && targetLight.intensity > 0.05f;

        if (isLit != wasLit || force)
        {
            wasLit = isLit;
            if (isLit)
            {
                // Restaurar el material a su estado original texturizado y claro
                if (instancedMat.HasProperty("_BaseColor")) instancedMat.SetColor("_BaseColor", Color.white);
                if (instancedMat.HasProperty("_Color")) instancedMat.SetColor("_Color", Color.white);
                
                // Restaurar texturas originales
                if (baseMaterial.HasProperty("_BaseMap")) instancedMat.SetTexture("_BaseMap", baseMaterial.GetTexture("_BaseMap"));
                if (baseMaterial.HasProperty("_MainTex")) instancedMat.SetTexture("_MainTex", baseMaterial.GetTexture("_MainTex"));
                if (baseMaterial.HasProperty("_BumpMap")) instancedMat.SetTexture("_BumpMap", baseMaterial.GetTexture("_BumpMap"));
                if (baseMaterial.HasProperty("_MetallicGlossMap")) instancedMat.SetTexture("_MetallicGlossMap", baseMaterial.GetTexture("_MetallicGlossMap"));
                if (baseMaterial.HasProperty("_OcclusionMap")) instancedMat.SetTexture("_OcclusionMap", baseMaterial.GetTexture("_OcclusionMap"));

                // Restaurar brillo y metalicidad originales
                if (instancedMat.HasProperty("_Smoothness")) instancedMat.SetFloat("_Smoothness", baseMaterial.GetFloat("_Smoothness"));
                if (instancedMat.HasProperty("_Glossiness")) instancedMat.SetFloat("_Glossiness", baseMaterial.HasProperty("_Glossiness") ? baseMaterial.GetFloat("_Glossiness") : 0.5f);
                if (instancedMat.HasProperty("_Metallic")) instancedMat.SetFloat("_Metallic", baseMaterial.HasProperty("_Metallic") ? baseMaterial.GetFloat("_Metallic") : 0.0f);
            }
            else
            {
                // Apagar por completo el material (negro mate absoluto) para mimetizarse con la oscuridad total del cuarto apagado
                Color blackColor = new Color(0.015f, 0.015f, 0.015f, 1f);
                if (instancedMat.HasProperty("_BaseColor")) instancedMat.SetColor("_BaseColor", blackColor);
                if (instancedMat.HasProperty("_Color")) instancedMat.SetColor("_Color", blackColor);
                
                // Quitar texturas
                if (instancedMat.HasProperty("_BaseMap")) instancedMat.SetTexture("_BaseMap", null);
                if (instancedMat.HasProperty("_MainTex")) instancedMat.SetTexture("_MainTex", null);
                if (instancedMat.HasProperty("_BumpMap")) instancedMat.SetTexture("_BumpMap", null);
                if (instancedMat.HasProperty("_MetallicGlossMap")) instancedMat.SetTexture("_MetallicGlossMap", null);
                if (instancedMat.HasProperty("_OcclusionMap")) instancedMat.SetTexture("_OcclusionMap", null);

                // Apagar brillo y metalicidad
                if (instancedMat.HasProperty("_Smoothness")) instancedMat.SetFloat("_Smoothness", 0f);
                if (instancedMat.HasProperty("_Glossiness")) instancedMat.SetFloat("_Glossiness", 0f);
                if (instancedMat.HasProperty("_Metallic")) instancedMat.SetFloat("_Metallic", 0f);
                if (instancedMat.HasProperty("_SpecColor")) instancedMat.SetColor("_SpecColor", Color.black);
            }
        }
    }
}

// ===========================================================================
// BRUJULA HUD MINIMALISTA PARA MODO DE JUEGO (ANTI-MAREO/ANTI-DESORIENTACIÓN)
// ===========================================================================
public class BackroomsCompassHUD : MonoBehaviour
{
    private Transform playerCam;
    private GUIStyle compassStyle;
    private GUIStyle markerStyle;

    void Start()
    {
        // Encontrar la camara del jugador
        if (Camera.main != null)
        {
            playerCam = Camera.main.transform;
        }
        else
        {
            var cc = FindObjectOfType<CharacterController>();
            if (cc != null) playerCam = cc.transform;
        }

        // Disenar estilos minimalistas y limpios de interfaz
        compassStyle = new GUIStyle();
        compassStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.8f);
        compassStyle.fontSize = 20;
        compassStyle.alignment = TextAnchor.MiddleCenter;
        compassStyle.fontStyle = FontStyle.Bold;

        markerStyle = new GUIStyle();
        markerStyle.normal.textColor = new Color(0.7f, 0.1f, 0.1f, 0.9f); // Rojo oxido/sangre
        markerStyle.fontSize = 22;
        markerStyle.alignment = TextAnchor.MiddleCenter;
        markerStyle.fontStyle = FontStyle.Bold;
    }

    void OnGUI()
    {
        if (playerCam == null)
        {
            if (Camera.main != null) playerCam = Camera.main.transform;
            return;
        }

        // Si el juego esta pausado o en menu principal, no mostrar HUD
        if (Time.timeScale == 0f) return;

        // Centrado en el borde superior de la pantalla
        float centerX = Screen.width / 2f;
        float centerY = 45f;

        // Dibujar el marcador de rumbo central "¢ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“¼"
        GUI.Label(new Rect(centerX - 50, centerY - 25, 100, 20), "▼", markerStyle);

        // Obtener el angulo yaw de la camara (de 0 a 360 grados)
        float yaw = playerCam.eulerAngles.y;

        // Dibujar los 4 puntos cardinales con un desplazamiento segun hacia donde mira el jugador
        DrawCardinalPoint("N", yaw, 0f, centerX, centerY);
        DrawCardinalPoint("E", yaw, 90f, centerX, centerY);
        DrawCardinalPoint("S", yaw, 180f, centerX, centerY);
        DrawCardinalPoint("O", yaw, 270f, centerX, centerY);

        // Opcional: Dibujar una linea sutil de fondo
        Texture2D lineTex = new Texture2D(1, 1);
        lineTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        lineTex.Apply();
        GUI.DrawTexture(new Rect(centerX - 150, centerY + 2, 300, 1), lineTex);
    }

    void DrawCardinalPoint(string label, float currentYaw, float targetAngle, float cx, float cy)
    {
        // Calcular la distancia angular entre hacia donde mira la camara y el punto cardinal
        float diff = targetAngle - currentYaw;
        while (diff < -180f) diff += 360f;
        while (diff > 180f) diff -= 360f;

        // Ocultar si esta a mas de 90 grados de distancia (fuera de la vision frontal)
        if (Mathf.Abs(diff) > 90f) return;

        // Mapear los grados a pixeles horizontales (ej. +/- 90 grados = +/- 120 pixeles de desplazamiento)
        float xOffset = (diff / 90f) * 120f;

        // Opacidad degradada segun se acerca a los extremos laterales
        float alpha = 1f - (Mathf.Abs(diff) / 90f);
        compassStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, alpha * 0.75f);
        
        // Puntos cardinales principales destacados en rojo sutil si quedan al centro
        if (Mathf.Abs(diff) < 5f)
        {
            compassStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f, alpha * 0.9f);
        }

        GUI.Label(new Rect(cx + xOffset - 50, cy - 10, 100, 20), label, compassStyle);
    }


}
