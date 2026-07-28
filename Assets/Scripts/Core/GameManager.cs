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
        
        // Al cargar el menú principal, reiniciamos las vidas
        if (scene.name == "MainMenu")
        {
            vidasActuales = maxVidas;
        }

        // Al cargar la pantalla de carga o el hospital de nuevo, reiniciar vidas para que el retry funcione
        if (scene.name == "LoadingScene" || scene.name == "SampleScene")
        {
            // Solo reiniciar si están en 0 (venimos de un Game Over)
            if (vidasActuales <= 0)
            {
                vidasActuales = maxVidas;
                Debug.Log("GameManager: Vidas reiniciadas al detectar Game Over + retry.");
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

        // Desactivar temporalmente el CharacterController para evitar conflictos de físicas al teletransportar
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = playerSpawnPosition;
        player.transform.rotation = playerSpawnRotation;

        if (cc != null) cc.enabled = true;

        Debug.Log("GameManager: Jugador teletransportado de vuelta al punto de spawn del nivel.");
    }
}
