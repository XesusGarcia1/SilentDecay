using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Control de Vidas")]
    public int maxVidas = 3;
    public int vidasActuales = 3;

    // Guardar el punto de spawn por nivel para reaparecer al jugador allí
    private Vector3 playerSpawnPosition;
    private Quaternion playerSpawnRotation;
    private bool hasSpawnPoint = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (!hasSpawnPoint)
        {
            // Fallback si no se registró spawn: intentar usar la posición actual inicial
            Debug.LogWarning("GameManager: No se registró punto de spawn. Reapareciendo en el origen.");
            player.transform.position = Vector3.up * 1f;
            return;
        }

        // 1. Encontrar el CharacterController en la jerarquía del jugador
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.GetComponentInParent<CharacterController>();
        if (cc == null) cc = player.GetComponentInChildren<CharacterController>();

        // 2. Encontrar el objeto raíz del jugador de forma segura (sin subir a carpetas del mapa/escena)
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

        // 3. Desactivar temporalmente el CharacterController para evitar conflictos de físicas al teletransportar
        if (cc != null)
        {
            cc.enabled = false;
        }

        // 4. Calcular el offset de la cápsula respecto a la raíz del jugador
        Vector3 offset = player.transform.position - playerRoot.position;

        // 5. Mover la raíz de manera que la cápsula quede exactamente en playerSpawnPosition
        playerRoot.position = playerSpawnPosition - offset;
        playerRoot.rotation = playerSpawnRotation;

        // 6. Resetear rotaciones locales del FirstPersonController si existe
        var fpc = playerRoot.GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();

        if (fpc != null)
        {
            var pitchField = fpc.GetType().GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pitchField != null)
            {
                pitchField.SetValue(fpc, 0f);
            }
        }

        // 7. Reactivar el CharacterController
        if (cc != null)
        {
            Physics.SyncTransforms();
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        Debug.Log($"GameManager: Jugador teletransportado al spawn ({playerSpawnPosition}). Raíz movida: {playerRoot.name}, Cápsula movida: {player.name}");
    }
}
