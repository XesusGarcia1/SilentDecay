using UnityEngine;

public class GuideMapUI : MonoBehaviour
{
    public static GuideMapUI Instance { get; private set; }

    public static bool hasGuideMap = true;
    public static bool isOpen = false;
    private static float openTime = 0f;

    private Texture2D mapTexture;
    private Texture2D mapIconTexture;
    private AudioClip paperSound;

    private Transform playerTransform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasGuideMap = true;
        isOpen = false;
        openTime = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindObjectOfType<GuideMapUI>() == null)
        {
            GameObject go = new GameObject("[GuideMapUI]");
            go.AddComponent<GuideMapUI>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        LoadMapTexture();
        paperSound = Resources.Load<AudioClip>("Audio/Hospital/Nota_Grab");
    }

    private void LoadMapTexture()
    {
        if (mapTexture != null) return;

        mapTexture = Resources.Load<Texture2D>("UI/GuieMap");
        if (mapTexture == null) mapTexture = Resources.Load<Texture2D>("GuieMap");
        if (mapTexture == null) mapTexture = Resources.Load<Texture2D>("DepositoIndustrial/Texturas/GuieMap");
        if (mapTexture == null) mapTexture = Resources.Load<Texture2D>("Texturas/GuieMap");
    }

    private Texture2D GetMapIconTexture()
    {
        if (mapIconTexture != null) return mapIconTexture;
        mapIconTexture = mapTexture; // Usar la textura de la guía como icono miniatura
        return mapIconTexture;
    }

    private bool ShouldSuppressMapUI()
    {
        // Si el juego está pausado por el PauseMenuManager (TimeScale 0 y en pausa de opciones)
        if (PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsGamePaused && !isOpen) return true;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "LoadingScene" || sceneName == "MainMenu") return true;

        // Suprimir en mapas de Túneles y Hospital (estos mapas usan el plano interactivo de la Libreta)
        if (sceneName.Contains("Tunnel") || sceneName.Contains("Hospital") ||
            FindFirstObjectByType<TunnelsGenerator>() != null ||
            FindFirstObjectByType<TunnelsFixedMapLogic>() != null ||
            FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>() != null ||
            FindFirstObjectByType<HospitalFixedMapLogic>() != null)
        {
            return true;
        }

        var generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (generator != null && generator.isMenuMode) return true;

        return false;
    }

    private void Update()
    {
        if (ShouldSuppressMapUI()) return;

        // Abrir/Cerrar con la tecla M (Map)
        if (hasGuideMap && Input.GetKeyDown(KeyCode.M))
        {
            ToggleMap();
        }

        if (isOpen)
        {
            if (Time.unscaledTime < openTime + 0.3f) return;

            // Cerrar con Escape, E, Tab o M
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || 
                Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M) ||
                MobileInput.GetKeyDown(KeyCode.E))
            {
                MobileInput.ePressedDown = false;
                CloseMap();
            }
        }
    }

    public void ToggleMap()
    {
        if (isOpen) CloseMap();
        else OpenMap();
    }

    public void OpenMap()
    {
        if (!hasGuideMap) return;

        LoadMapTexture();
        isOpen = true;
        openTime = Time.unscaledTime;
        Time.timeScale = 0f; // Congelar juego durante lectura

        FindPlayer();
        if (playerTransform != null)
        {
            var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
            if (controller != null) controller.enabled = false;

            var playerInputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (playerInputs != null)
            {
                playerInputs.cursorLocked = false;
                playerInputs.cursorInputForLook = false;
                playerInputs.look = Vector2.zero;
            }
        }

        MobileInput.SetCursorState(false);

        // Sonido de hojear papel
        if (paperSound != null && Camera.main != null)
        {
            AudioSource camAudio = Camera.main.GetComponent<AudioSource>();
            if (camAudio == null) camAudio = Camera.main.gameObject.AddComponent<AudioSource>();
            camAudio.ignoreListenerPause = true;
            camAudio.PlayOneShot(paperSound, 0.9f);
        }
    }

    public void CloseMap()
    {
        isOpen = false;
        Time.timeScale = 1f;

        FindPlayer();
        if (playerTransform != null)
        {
            var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
            if (controller != null) controller.enabled = true;

            var playerInputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (playerInputs != null)
            {
                playerInputs.cursorLocked = true;
                playerInputs.cursorInputForLook = true;
            }
        }

        MobileInput.SetCursorState(true);

        if (paperSound != null && Camera.main != null)
        {
            AudioSource camAudio = Camera.main.GetComponent<AudioSource>();
            if (camAudio != null)
            {
                camAudio.ignoreListenerPause = true;
                camAudio.PlayOneShot(paperSound, 0.7f);
            }
        }
    }

    private void FindPlayer()
    {
        if (playerTransform != null) return;

        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null) { playerTransform = cc.transform; return; }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null) { playerTransform = pObj.transform; return; }

        GameObject pTag = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null) { playerTransform = playerTagObj.transform; return; }

        if (Camera.main != null) playerTransform = Camera.main.transform;
    }

    private GameObject playerTagObj => GameObject.FindGameObjectWithTag("Player");

    private void OnGUI()
    {
        if (ShouldSuppressMapUI()) return;

        if (isOpen)
        {
            GUI.depth = -90; // Dibujar justo debajo del PauseMenu

            // 1. Fondo semitransparente oscuro de pantalla completa
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 2. Contenedor de la Guía estilo libro/pergamino
            float padding = 30f;
            float maxW = Screen.width - (padding * 2f);
            float maxH = Screen.height - (padding * 2f) - 60f; // Espacio para el título y botón cerrar

            // Mantener la relación de aspecto de la textura GuieMap (aprox 3:2)
            float texAspect = 1.5f;
            if (mapTexture != null && mapTexture.width > 0 && mapTexture.height > 0)
            {
                texAspect = (float)mapTexture.width / (float)mapTexture.height;
            }

            float mapW = maxW;
            float mapH = mapW / texAspect;
            if (mapH > maxH)
            {
                mapH = maxH;
                mapW = mapH * texAspect;
            }

            float mapX = (Screen.width - mapW) / 2f;
            float mapY = ((Screen.height - mapH) / 2f) + 15f;

            // Marco de pergamino arrugado alrededor de la guía
            Rect borderRect = new Rect(mapX - 12f, mapY - 12f, mapW + 24f, mapH + 24f);
            GUI.color = new Color(0.35f, 0.25f, 0.15f, 0.95f);
            GUI.DrawTexture(borderRect, ProceduralPaperTexture.GetPaperTexture());
            GUI.color = Color.white;

            // Renderizar la textura de la Guía de Supervivencia
            if (mapTexture != null)
            {
                GUI.DrawTexture(new Rect(mapX, mapY, mapW, mapH), mapTexture, ScaleMode.StretchToFill);
            }
            else
            {
                // Fallback si la textura no se ha cargado
                GUIStyle fallbackStyle = new GUIStyle(GUI.skin.label);
                fallbackStyle.fontSize = 24;
                fallbackStyle.alignment = TextAnchor.MiddleCenter;
                fallbackStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(mapX, mapY, mapW, mapH), "GUÍA DE SUPERVIVENCIA (Cargando Mapa...)", fallbackStyle);
            }

            // Encabezado superior
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.035f), 18, 32);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.6f);

            GUI.Label(new Rect(0, mapY - 45f, Screen.width, 35f), "• GUÍA DE SUPERVIVENCIA •", headerStyle);

            // Botón inferior para cerrar
            GUIStyle closeBtnStyle = new GUIStyle(GUI.skin.button);
            closeBtnStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.028f), 16, 24);
            closeBtnStyle.fontStyle = FontStyle.Bold;
            closeBtnStyle.alignment = TextAnchor.MiddleCenter;
            closeBtnStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            closeBtnStyle.hover.textColor = Color.red;

            float btnW = Mathf.Clamp(Screen.width * 0.35f, 260f, 450f);
            float btnH = 42f;
            Rect closeRect = new Rect((Screen.width - btnW) / 2f, mapY + mapH + 12f, btnW, btnH);

            if (GUI.Button(closeRect, "[ ESC / E / TAP ]  CERRAR GUÍA", closeBtnStyle))
            {
                CloseMap();
            }

            return;
        }
    }
}
