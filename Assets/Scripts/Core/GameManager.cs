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

    private void FixMobileCanvasScaling()
    {
        UnityEngine.UI.CanvasScaler[] scalers = FindObjectsOfType<UnityEngine.UI.CanvasScaler>(true);
        foreach (var scaler in scalers)
        {
            if (scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
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

        // 1. Encontrar el CharacterController en la jerarquía del jugador
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.GetComponentInParent<CharacterController>();
        if (cc == null) cc = player.GetComponentInChildren<CharacterController>();

        // 2. Desactivar temporalmente el CharacterController para evitar conflictos al teletransportar
        if (cc != null)
        {
            cc.enabled = false;
        }

        // 3. Determinar posición y rotación seguras de destino
        Vector3 targetPos = hasSpawnPoint ? playerSpawnPosition : (player.transform.position.y < -5f ? Vector3.up * 0.5f : player.transform.position);
        Quaternion targetRot = hasSpawnPoint ? playerSpawnRotation : Quaternion.identity;

        // 4. Encontrar la raíz del personaje (sin subir a la escena o generadores)
        Transform current = player.transform;
        Transform playerRoot = current;
        while (current.parent != null)
        {
            string pName = current.parent.name.ToLower();
            if (pName.Contains("generator") || pName.Contains("map") || pName.Contains("tunnels") || pName.Contains("hospital") || pName.Contains("scene") || pName.Contains("manager"))
            {
                break;
            }
            current = current.parent;
            playerRoot = current;
        }

        // 5. Mover directamente el personaje y su raíz a la posición exacta de spawn
        player.transform.position = targetPos;
        player.transform.rotation = targetRot;

        if (playerRoot != player.transform)
        {
            playerRoot.position = targetPos;
            playerRoot.rotation = targetRot;
            player.transform.localPosition = Vector3.zero;
        }

        // 6. Detener físicas/fuerzas residuales de Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = player.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 7. Resetear pitch vertical del FirstPersonController
        var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            var pitchField = fpc.GetType().GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pitchField != null)
            {
                pitchField.SetValue(fpc, 0f);
            }
        }

        // 8. Sincronizar transformadas y reactivar CharacterController
        Physics.SyncTransforms();
        if (cc != null)
        {
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        Debug.Log($"[GameManager] Jugador reaparecido con éxito en {targetPos}. (Raíz: {playerRoot.name})");
    }
}
