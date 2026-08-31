using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Coordinador principal del menú. Inicializa sistemas y delega
/// el renderizado de cada pantalla a sus subcomponentes.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ─── Estado compartido (accesible por los subcomponentes) ────────────────
    public static bool startedFromMenu = false;

    public enum MenuState { Main, LevelSelect, PlayOptions, DepotOptions, Settings }
    [HideInInspector] public MenuState currentState = MenuState.Main;

    // ─── Referencias ─────────────────────────────────────────────────────────

    [Header("Título Personalizado")]
    public string gameTitle = "SILENT DECAY";
    public Texture2D titleLogo; // NUEVA VARIABLE PARA EL LOGO
    [Range(20f, 500f)]
    public float logoHeight = 80f; // Controla el tamaño del logo desde el Inspector

    [Header("Configuración de Niveles")]
    [Tooltip("Si es true, desbloquea todos los mapas en el carrusel para pruebas sin necesidad de completar los anteriores")]
    public bool unlockAllMapsDebug = false;
    [Tooltip("Marcar esta casilla en el Inspector borra el progreso guardado de campaña (PlayerPrefs) para probar como un jugador nuevo")]
    public bool resetCampaignProgress = false;

    [Header("Modo de Pruebas Global (Developer Test Mode)")]
    [Tooltip("Activa todas las ayudas globales: Inicia con tarjeta, burla tarjeta, burla energía en Hospital; auto-activa generadores en Túneles; y entrega todas las llaves, repara escaleras y da mapa en Depósito")]
    public bool testModeEnableAll = false;

    [Header("Opciones de Pruebas - Hospital")]
    [Tooltip("Hospital: Iniciar la partida con la tarjeta de acceso")]
    public bool testStartWithKeycard = false;
    [Tooltip("Hospital: Burlar la tarjeta de acceso (abierto directamente)")]
    public bool testBypassKeycard = false;
    [Tooltip("Hospital: Burlar la energía eléctrica del elevador")]
    public bool testBypassPower = false;

    [Header("Opciones de Pruebas - Túneles")]
    [Tooltip("Túneles: Auto-activar todos los subgeneradores al iniciar")]
    public bool testAutoActivateGenerators = false;

    [Header("Opciones de Pruebas - Depósito Industrial")]
    [Tooltip("Depósito: Iniciar con todas las llaves recolectadas")]
    public bool testDepotGiveAllKeys = false;
    [Tooltip("Depósito: Escalera ya armada y reparada desde el inicio")]
    public bool testDepotLadderRepaired = false;
    [Tooltip("Depósito: Iniciar con el mapa de guía en el inventario")]
    public bool testDepotGiveGuideMap = false;

    [Header("Estilos de Botones")]
    public Texture2D btnNormalTexture;
    public Texture2D btnHoverTexture;

    [Header("Redes Sociales")]
    public string instagramURL = "https://www.instagram.com/lxesusgarcial";
    public string facebookURL  = "https://www.facebook.com/lXesusGarcial";
    public string youtubeURL   = "https://www.youtube.com/@Xesus_Garcia";

    [Header("Sonidos de Menú")]
    public AudioClip menuMusic;

    // ─── Estado de audio ─────────────────────────────────────────────────────
    [HideInInspector] public AudioSource menuAudioSource;
    [HideInInspector] public AudioSource sfxAudioSource;
    [HideInInspector] public AudioClip   buttonClickSound;

    // ─── Opciones persistentes ───────────────────────────────────────────────
    [HideInInspector] public float mouseSensitivity = 2.0f;
    [HideInInspector] public float masterVolume     = 1.0f;
    [HideInInspector] public bool  isFullscreen     = true;

    // ─── Textura de overlay ──────────────────────────────────────────────────
    [HideInInspector] public Texture2D sidebarTex;

    // ─── Subcomponentes de pantalla ──────────────────────────────────────────
    [HideInInspector] public MenuCameraController     cameraController;
    [HideInInspector] public MenuScreenMain           screenMain;
    [HideInInspector] public MenuScreenLevelSelect    screenLevelSelect;
    [HideInInspector] public MenuScreenPlayOptions    screenPlayOptions;
    [HideInInspector] public MenuScreenDepotOptions   screenDepotOptions;
    [HideInInspector] public MenuScreenSettings       screenSettings;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        DevTestSettings.SyncFromMainMenu(this);
    }

    void OnValidate()
    {
        if (resetCampaignProgress)
        {
            ResetCampaignProgress();
            resetCampaignProgress = false;
        }
        DevTestSettings.SyncFromMainMenu(this);
    }

    void Start()
    {
        DevTestSettings.SyncFromMainMenu(this);
        InitRenderSettings();
        InitScreenOrientation();
        InitTextures();
        InitPreferences();
        InitAudio();
        InitSubScreens();
    }

    // ─── Inicialización ───────────────────────────────────────────────────────

    void InitRenderSettings()
    {
        RenderSettings.ambientMode           = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight          = Color.black;
        RenderSettings.ambientSkyColor       = Color.black;
        RenderSettings.ambientEquatorColor   = Color.black;
        RenderSettings.ambientGroundColor    = Color.black;
        RenderSettings.ambientIntensity      = 0.0f;
        RenderSettings.reflectionIntensity   = 0.0f;
        RenderSettings.fog                   = true;
        RenderSettings.fogColor              = Color.black;
        RenderSettings.fogMode               = FogMode.ExponentialSquared;
        RenderSettings.fogDensity            = 0.08f;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                l.enabled   = false;
                l.intensity = 0f;
            }
        }
    }

    void InitScreenOrientation()
    {
        Screen.orientation                     = ScreenOrientation.AutoRotation;
        Screen.autorotateToPortrait            = false;
        Screen.autorotateToPortraitUpsideDown  = false;
        Screen.autorotateToLandscapeLeft       = true;
        Screen.autorotateToLandscapeRight      = true;
    }



    void InitTextures()
    {
        sidebarTex = new Texture2D(2, 2);
        Color c = new Color(0f, 0f, 0f, 0.45f);
        sidebarTex.SetPixel(0, 0, c); sidebarTex.SetPixel(0, 1, c);
        sidebarTex.SetPixel(1, 0, c); sidebarTex.SetPixel(1, 1, c);
        sidebarTex.Apply();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void InitPreferences()
    {
        mouseSensitivity     = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
        masterVolume         = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        AudioListener.volume = masterVolume;
        isFullscreen         = Screen.fullScreen;

        int qualityIdx = PlayerPrefs.GetInt("QualityLevel", 2);
        QualitySettings.SetQualityLevel(qualityIdx, true);
    }

    void InitAudio()
    {
        GameObject audioObj = new GameObject("MenuAudioSource");
        audioObj.transform.SetParent(transform);
        menuAudioSource            = audioObj.AddComponent<AudioSource>();
        menuAudioSource.loop       = true;
        menuAudioSource.spatialBlend = 0f;
        menuAudioSource.volume     = masterVolume * 0.6f;

        AudioClip clip = menuMusic != null ? menuMusic : Resources.Load<AudioClip>("Audio/Menu/Song");
        if (clip == null) clip = Resources.Load<AudioClip>("Song");
        if (clip != null)
        {
            menuAudioSource.clip = clip;
            menuAudioSource.Play();
        }

        buttonClickSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        GameObject sfxObj = new GameObject("MenuSFXAudioSource");
        sfxObj.transform.SetParent(transform);
        sfxAudioSource              = sfxObj.AddComponent<AudioSource>();
        sfxAudioSource.spatialBlend = 0f;
        sfxAudioSource.volume       = masterVolume * 0.85f;
    }

    void InitSubScreens()
    {
        cameraController  = new MenuCameraController();
        cameraController.Init(this);

        screenMain        = gameObject.AddComponent<MenuScreenMain>();
        screenMain.Init(this);

        screenLevelSelect = gameObject.AddComponent<MenuScreenLevelSelect>();
        screenLevelSelect.Init(this);

        screenPlayOptions = gameObject.AddComponent<MenuScreenPlayOptions>();
        screenPlayOptions.Init(this);

        screenDepotOptions = gameObject.AddComponent<MenuScreenDepotOptions>();
        screenDepotOptions.Init(this);

        screenSettings    = gameObject.AddComponent<MenuScreenSettings>();
        screenSettings.Init(this);
    }

    // ─── Helpers públicos ─────────────────────────────────────────────────────

    public void PlayClickSound()
    {
        if (buttonClickSound != null && sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(buttonClickSound, 0.45f);
    }

    public void GoTo(MenuState state) => currentState = state;

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        cameraController?.Tick();
    }

    private MenuStyles cachedStyles;

    void OnGUI()
    {
        // Escalado dinámico 1920x1080
        Vector2 scaleRef = new Vector2(1920f, 1080f);
        Matrix4x4 svMat = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(Screen.width / scaleRef.x, Screen.height / scaleRef.y, 1f));

        // Overlay oscuro de la barra lateral
        GUI.DrawTexture(new Rect(0, 0, 1920f, 1080f), sidebarTex);

        // --- FILTRO DE BRILLO / GAMMA UI EN TIEMPO REAL ---
        float currentGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
        if (currentGamma < 1.00f)
        {
            // Dibujar una capa negra semitransparente para oscurecer el fondo
            float opacity = Mathf.Lerp(0f, 0.85f, (1.00f - currentGamma) / 0.50f);
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, opacity);
            GUI.DrawTexture(new Rect(0, 0, 1920f, 1080f), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }

        // Estilos compartidos cacheados (crucial para no crear texturas dinámicas cada frame)
        if (cachedStyles == null)
        {
            cachedStyles = new MenuStyles(btnNormalTexture, btnHoverTexture);
        }
        var styles = cachedStyles;

        // Título
        float areaHeight = (titleLogo != null) ? Mathf.Max(150f, logoHeight + 50f) : 150f;
        GUILayout.BeginArea(new Rect(0, 60, 1920f, areaHeight));
        
        if (titleLogo != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float height = logoHeight;
            float width = height * ((float)titleLogo.width / titleLogo.height);
            GUILayout.Label(titleLogo, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label(gameTitle, styles.Title, GUILayout.Height(65));
        }
        
        GUILayout.Label("• REC  00:00:01  |  VHS  |  BACKROOMS  |  OCT.24 1997", styles.SubTitle, GUILayout.Height(22));
        GUILayout.EndArea();

        // El contenido siempre arranca debajo del logo+VHS, sin importar el estado
        bool isSettingsCalibrating = (currentState == MenuState.Settings && screenSettings != null && screenSettings.IsCalibrating);
        float logoAreaBottom = 60f + Mathf.Max(150f, logoHeight + 50f) + 10f;
        float availableH = 1080f - logoAreaBottom - 20f; // Espacio restante hasta el borde inferior

        int menuW = (currentState == MenuState.LevelSelect) ? 960 :
                    (currentState == MenuState.PlayOptions || currentState == MenuState.DepotOptions) ? 1100 :
                    (isSettingsCalibrating ? 820 : (currentState == MenuState.Settings ? 720 : 640));
        int menuH = isSettingsCalibrating ? 620 : (int)Mathf.Min(700f, availableH);
        float menuY = logoAreaBottom;
        GUILayout.BeginArea(new Rect(1920f / 2f - menuW / 2f, menuY, menuW, menuH));
        GUILayout.Space(10);

        switch (currentState)
        {
            case MenuState.Main:         screenMain?.Draw(styles);         break;
            case MenuState.LevelSelect:  screenLevelSelect?.Draw(styles);  break;
            case MenuState.PlayOptions:  screenPlayOptions?.Draw(styles);  break;
            case MenuState.DepotOptions: screenDepotOptions?.Draw(styles); break;
            case MenuState.Settings:     screenSettings?.Draw(styles);     break;
        }

        GUILayout.EndArea();

        // Redes sociales (en todas las pantallas excepto LevelSelect y cuando calibramos)
        if (currentState != MenuState.LevelSelect && !isSettingsCalibrating)
            screenMain?.DrawSocialButtons();

        GUI.matrix = svMat;
    }

    [ContextMenu("Resetear Progreso de Campaña")]
    public void ResetCampaignProgress()
    {
        PlayerPrefs.DeleteKey("Campaign_HospitalCompleted");
        PlayerPrefs.DeleteKey("Campaign_TunnelsCompleted");
        PlayerPrefs.DeleteKey("Campaign_HospitalUnlocked");
        PlayerPrefs.DeleteKey("Campaign_TunnelsUnlocked");
        PlayerPrefs.DeleteKey("HospitalCompleted");
        PlayerPrefs.DeleteKey("TunnelsCompleted");
        PlayerPrefs.Save();
        Debug.Log("[MainMenuManager] 🧹 Progreso guardado de campaña reseteado exitosamente.");
    }
}

/// <summary>
/// Configuración de pruebas persistente entre escenas durante la sesión de juego.
/// </summary>
public static class DevTestSettings
{
    public static bool testModeEnableAll = false;
    public static bool startWithKeycard = false;
    public static bool bypassKeycard = false;
    public static bool bypassPower = false;
    public static bool autoActivateGenerators = false;
    public static bool testDepotGiveAllKeys = false;
    public static bool testDepotLadderRepaired = false;
    public static bool testDepotGiveGuideMap = false;

    public static void SyncFromMainMenu(MainMenuManager m)
    {
        if (m == null) return;
        testModeEnableAll = m.testModeEnableAll;
        startWithKeycard = m.testModeEnableAll || m.testStartWithKeycard;
        bypassKeycard = m.testModeEnableAll || m.testBypassKeycard;
        bypassPower = m.testModeEnableAll || m.testBypassPower;
        autoActivateGenerators = m.testModeEnableAll || m.testAutoActivateGenerators;
        testDepotGiveAllKeys = m.testModeEnableAll || m.testDepotGiveAllKeys;
        testDepotLadderRepaired = m.testModeEnableAll || m.testDepotLadderRepaired;
        testDepotGiveGuideMap = m.testModeEnableAll || m.testDepotGiveGuideMap;
    }
}
