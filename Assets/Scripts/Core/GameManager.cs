using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    Debug.LogWarning("GameManager no fue encontrado en la escena. Se ha creado uno automáticamente para evitar errores de vidas.");
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("Control de Vidas")]
    public int maxVidas = 3;
    public int vidasActuales = 3;

    // Guardar el punto de spawn por nivel para reaparecer al jugador allí
    private Vector3 playerSpawnPosition;
    private Quaternion playerSpawnRotation;
    private bool hasSpawnPoint = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Escuchar eventos de carga de escenas para limpiar/inicializar variables de nivel
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        hasSpawnPoint = false;
        ArrivalElevatorController.HasElevatorSpawn = false;

        // SIEMPRE garantizar que el tiempo esté corriendo al cargar cualquier escena
        Time.timeScale = 1f;
        AudioListener.volume = 1f;
        
        #if UNITY_ANDROID || UNITY_IOS
        FixMobileCanvasScaling();
        #endif

        // Solo reiniciar vidas al volver al menú principal.
        // IMPORTANTE: LoadingScene NO debe reiniciar vidas, porque es una pantalla intermedia
        // que se usa tanto para el primer acceso como para los reintentos mid-game.
        if (scene.name == "MainMenu")
        {
            vidasActuales = maxVidas;
            Debug.Log("GameManager: Vidas de sesión reiniciadas a " + maxVidas + " (vuelta al Menú).");
        }
        else if (scene.name != "LoadingScene")
        {
            // Al entrar a cualquier mapa de juego, si las vidas son 0 (corrupción) → dejar en 1 como fallback
            if (vidasActuales <= 0)
            {
                vidasActuales = 1;
                Debug.LogWarning("GameManager: vidasActuales era 0 al entrar al mapa. Forzando a 1.");
            }
        }
    }

    public void FixMobileCanvasScaling()
    {
        UnityEngine.UI.CanvasScaler[] scalers = FindObjectsOfType<UnityEngine.UI.CanvasScaler>(true);
        foreach (var scaler in scalers)
        {
            // Forzar a 1600x900 en celulares (tamaño intermedio ideal para no ser ni muy pequeño ni muy gigante)
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    /// <summary>
    /// Establece el número de vidas máximas y actuales para el mapa que se acaba de generar.
    /// </summary>
    public void InicializarVidasParaMapa(int vidas)
    {
        maxVidas = vidas;
        vidasActuales = vidas;
        Debug.Log($"GameManager: Inicializadas {vidasActuales}/{maxVidas} vidas para el mapa actual.");
    }

    /// <summary>
    /// Guarda el punto inicial donde aparece el jugador al generar el mapa
    /// </summary>
    public void RegistrarSpawnJugador(Vector3 position, Quaternion rotation)
    {
        playerSpawnPosition = position;
        playerSpawnRotation = rotation;
        hasSpawnPoint = true;
        
        // Invalidar cualquier spawn estático de ascensor residual de un mapa anterior
        ArrivalElevatorController.HasElevatorSpawn = false;
        
        Debug.Log($"GameManager: Registrado punto de spawn del jugador en {position}");
    }

    /// <summary>
    /// Resta una vida. Devuelve true si el jugador sigue vivo, false si es Game Over definitivo.
    /// </summary>
    public bool RestarVida()
    {
        vidasActuales--;
        Debug.Log($"GameManager: Jugador perdió una vida. Restantes: {vidasActuales}");
        return vidasActuales > 0;
    }

    /// <summary>
    /// Teletransporta al jugador de vuelta a su spawn de inicio del mapa.
    /// </summary>
    public void ReaparecerJugador(GameObject player)
    {
        if (player == null) return;

        // 1. Buscar el CharacterController (o FirstPersonController) para identificar el objeto real del jugador
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.GetComponentInChildren<CharacterController>(true);
        if (cc == null) cc = player.GetComponentInParent<CharacterController>(true);

        Transform targetTransform = (cc != null) ? cc.transform : player.transform;

        // 2. Desactivar CharacterController temporalmente antes de la teletransportación
        if (cc != null) cc.enabled = false;

        // 3. Determinar posición y rotación de destino
        Vector3 targetPos;
        Quaternion targetRot;

        if (ArrivalElevatorController.HasElevatorSpawn)
        {
            targetPos = ArrivalElevatorController.InitialElevatorSpawnPosition;
            targetRot = ArrivalElevatorController.InitialElevatorSpawnRotation;
            Debug.Log($"[GameManager] Reapareciendo usando spawn exacto del ascensor: pos={targetPos}");
        }
        else if (hasSpawnPoint)
        {
            targetPos = playerSpawnPosition;
            targetRot = playerSpawnRotation;
            Debug.Log($"[GameManager] Reapareciendo usando spawn registrado: pos={targetPos}");
        }
        else
        {
            targetPos = targetTransform.position.y < -5f ? Vector3.up * 0.5f : targetTransform.position;
            targetRot = Quaternion.identity;

            RaycastHit floorHit;
            if (Physics.Raycast(targetPos + Vector3.up * 0.5f, Vector3.down, out floorHit, 5.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                targetPos.y = floorHit.point.y;
            }
            Debug.Log($"[GameManager] Reapareciendo con fallback Raycast: pos={targetPos}");
        }

        // 4. Mover directamente el objeto exacto que posee el CharacterController
        targetTransform.position = targetPos;
        targetTransform.rotation = targetRot;

        // 5. Forzar actualización inmediata en el motor de físicas
        Physics.SyncTransforms();

        // 6. Resetear Rigidbody si existe
        Rigidbody rb = targetTransform.GetComponent<Rigidbody>();
        if (rb == null) rb = targetTransform.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.position = targetPos;
            rb.rotation = targetRot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 7. Reactivar CharacterController
        if (cc != null)
        {
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        // 8. Resetear rotación de cámara
        var fpc = targetTransform.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc == null) fpc = targetTransform.GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc == null) fpc = targetTransform.GetComponentInParent<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            fpc.ResetCameraRotation(targetRot.eulerAngles.y);
        }

        Debug.Log($"[GameManager] Jugador reaparecido exitosamente en {targetPos}. ObjetoObjetivo='{targetTransform.name}', CC={cc != null}, HasElevatorSpawn={ArrivalElevatorController.HasElevatorSpawn}");
    }
}
