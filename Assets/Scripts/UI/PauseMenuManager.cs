using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }
    public bool IsGamePaused => currentState != PauseState.None;

    private enum PauseState { None, Paused, Settings }
    private PauseState currentState = PauseState.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu" || sceneName == "LoadingScene") return;

        if (Instance == null && FindObjectOfType<PauseMenuManager>() == null)
        {
            GameObject go = new GameObject("[PauseMenuManager]");
            go.AddComponent<PauseMenuManager>();
            DontDestroyOnLoad(go);
            Debug.Log("[PauseMenuManager] 🎮 Auto-inicializado para control de pausa con tecla ESC.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Al cargar cualquier escena que NO sea el menú ni la carga, aseguramos estado limpio
        bool isMenuOrLoader = scene.name == "MainMenu" || scene.name == "LoadingScene";
        if (!isMenuOrLoader)
        {
            currentState = PauseState.None;
            isCalibratingGamma = false;
            Time.timeScale = 1f;
            MobileInput.SetCursorState(true);
            playerObj = null; // Forzar re-búsqueda del jugador en el nuevo mapa
        }
    }

    private float mouseSensitivity = 2.0f;
    private float masterVolume = 1.0f;
    private bool isFullscreen = true;

    // Ajustes de gráficos y pestañas en el menú de pausa
    private int activeSettingsTab = 0; // 0 = Audio y Sensibilidad, 1 = Gráficos y Rendimiento
    private int selectedQualityIndex = 2; // 0 = Bajo, 1 = Medio, 2 = Alto

    [Header("Estilos de Botones")]
    public Texture2D btnNormalTexture;
    public Texture2D btnHoverTexture;
    private MenuStyles cachedStyles;
    
    // --- VARIABLES DE CALIBRACIÓN DE BRILLO ---
    private bool isCalibratingGamma = false;
    private float tempGamma = 1.0f;

    #if !UNITY_ANDROID && !UNITY_IOS
    private System.Collections.Generic.List<Resolution> pcResolutions = new System.Collections.Generic.List<Resolution>();
    private int selectedResIndex = 0;
    #endif

    private Texture2D pauseBgTex;
    private AudioClip buttonClickSound;
    private AudioSource sfxSource;

    private GameObject playerObj;
    private MonoBehaviour fpsController;

    void Start()
    {
        // Crear textura de fondo oscura semi-transparente para la pausa (tinte negro sutil, 50% opacidad)
        pauseBgTex = new Texture2D(2, 2);
        Color tintColor = new Color(0f, 0f, 0f, 0.65f);
        pauseBgTex.SetPixel(0, 0, tintColor);
        pauseBgTex.SetPixel(0, 1, tintColor);
        pauseBgTex.SetPixel(1, 0, tintColor);
        pauseBgTex.SetPixel(1, 1, tintColor);
        pauseBgTex.Apply();

        // Cargar sonidos
        buttonClickSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0.0f; // SFX 2D inmediato
        sfxSource.bypassEffects = true;
        sfxSource.bypassListenerEffects = true;
        sfxSource.bypassReverbZones = true;

        // Cargar preferencias iniciales
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        isFullscreen = Screen.fullScreen;

        // Cargar nivel de calidad de gráficos guardado (Default: Alto / 2)
        selectedQualityIndex = PlayerPrefs.GetInt("QualityLevel", 2);
        QualitySettings.SetQualityLevel(selectedQualityIndex, true);

        // Aplicar nivel de Gamma / Brillo ambiental en la partida en tiempo real
        float savedGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
        GammaManager.AplicarGamma(savedGamma);

        // Inicializar resoluciones únicas en PC
        #if !UNITY_ANDROID && !UNITY_IOS
        pcResolutions.Clear();
        foreach (var r in Screen.resolutions)
        {
            if (!pcResolutions.Exists(x => x.width == r.width && x.height == r.height))
            {
                pcResolutions.Add(r);
            }
        }

        // Buscar el índice de la resolución de pantalla actual
        selectedResIndex = pcResolutions.Count - 1;
        for (int i = 0; i < pcResolutions.Count; i++)
        {
            if (pcResolutions[i].width == Screen.width && pcResolutions[i].height == Screen.height)
            {
                selectedResIndex = i;
                break;
            }
        }
        #endif

        // Buscar jugador y su controlador en la escena
        playerObj = GameObject.Find("NestedParent_Unpack");
        if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
    }

    void Update()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu" || sceneName == "LoadingScene")
            return;

        if (GameEndingManager.isEndingTriggered)
            return;

        // Asegurar referencia del jugador si no se encontró al arrancar
        if (playerObj == null)
        {
            playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
        }

        // Detectar pulsación de la tecla Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GuideMapUI.Instance != null && GuideMapUI.isOpen)
            {
                GuideMapUI.Instance.CloseMap();
                return;
            }

            if (currentState == PauseState.None)
            {
                PauseGame();
            }
            else if (currentState == PauseState.Paused)
            {
                ResumeGame();
            }
            else if (currentState == PauseState.Settings)
            {
                PlayClickSound();
                currentState = PauseState.Paused;
            }
        }
    }

    public void PauseGame()
    {
        // Si hay una nota de lore abierta, cerrarla silenciosamente al pausar
        var activeNotes = FindObjectsOfType<LoreNoteItem>();
        foreach (var note in activeNotes)
        {
            if (note != null && note.IsReading)
            {
                note.CloseReadingSilently();
            }
        }

        currentState = PauseState.Paused;
        Time.timeScale = 0f; // Congelar físicas y tiempo

        // Liberar cursor de forma segura
        MobileInput.SetCursorState(false);

        // Desactivar controles de movimiento del jugador
        if (playerObj != null)
        {
            fpsController = playerObj.GetComponentInChildren<StarterAssets.FirstPersonController>() as MonoBehaviour;
            if (fpsController != null) fpsController.enabled = false;
        }

        PlayClickSound();
    }

    public void ResumeGame()
    {
        currentState = PauseState.None;
        Time.timeScale = 1f; // Reanudar tiempo

        // Bloquear cursor nuevamente de forma segura
        MobileInput.SetCursorState(true);

        // Reactivar controles de movimiento del jugador
        if (fpsController != null) fpsController.enabled = true;

        PlayClickSound();
    }

    private void PlayClickSound()
    {
        if (sfxSource != null && buttonClickSound != null)
        {
            sfxSource.volume = masterVolume * 0.85f;
            sfxSource.PlayOneShot(buttonClickSound, 0.45f);
        }
    }

    void OnGUI()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu" || sceneName == "LoadingScene")
            return;

        if (GameEndingManager.isEndingTriggered)
            return;

        // Evitar dibujar el menú de pausa o la tuerca si el jugador está muerto
        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
            return;

        // Evitar dibujar el menú de pausa o la tuerca si se ha ganado la partida
        if (TunnelsFixedMapLogic.Instance != null && TunnelsFixedMapLogic.Instance.IsVictoryActive)
            return;
        if (TunnelsGenerator.Instance != null && TunnelsGenerator.Instance.IsVictoryActive)
            return;

        GUI.depth = -100; // Garantizar que el menú de pausa se dibuje SIEMPRE por encima de cualquier otro elemento GUI

        // 1. DIBUJAR BOTÓN DE CONFIGURACIÓN (TUERCA) EN LA ESQUINA SUPERIOR DERECHA (Para móviles y ratón libre)
        // Solo visible cuando no está pausado y el mouse está libre, o siempre como fallback táctil
        if (currentState == PauseState.None)
        {
            float uiScale = 1f;
            #if UNITY_ANDROID || UNITY_IOS
            uiScale = 1.8f; // Ajustado para móviles
            #endif
            
            GUIStyle gearButtonStyle = new GUIStyle(GUI.skin.button);
            gearButtonStyle.fontSize = (int)(20 * uiScale);
            gearButtonStyle.alignment = TextAnchor.MiddleCenter;
            gearButtonStyle.normal.textColor = Color.white;
            gearButtonStyle.hover.textColor = Color.red;

            // Posición en el lado izquierdo, debajo del indicador REC para evitar amontonarse
            float btnSize = 38 * uiScale;
            Rect gearRect = new Rect(30, 115, btnSize, btnSize);
            if (GUI.Button(gearRect, GUIContent.none, gearButtonStyle))
            {
                PauseGame();
            }
            Texture2D gTex = GetGearTexture();
            Rect iconPadding = new Rect(gearRect.x + 3 * uiScale, gearRect.y + 3 * uiScale, gearRect.width - 6 * uiScale, gearRect.height - 6 * uiScale);
            if (gTex != null) GUI.DrawTexture(iconPadding, gTex, ScaleMode.ScaleToFit, true);

            // --- FILTRO DE BRILLO / GAMMA UI EN TIEMPO REAL IN-GAME ---
            float currentGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
            if (currentGamma < 1.00f)
            {
                float opacity = Mathf.Lerp(0f, 0.85f, (1.00f - currentGamma) / 0.50f);
                Color prevCol = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, opacity);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prevCol;
            }

            return;
        }

        // --- INTERFAZ DE MENÚ DE PAUSA ---
        // Pintar fondo oscuro semi-transparente sobre toda la pantalla
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), pauseBgTex);

        if (btnNormalTexture == null)
            btnNormalTexture = Resources.Load<Texture2D>("Texturas/UI/Btn_Normal");
        
        if (btnHoverTexture == null)
            btnHoverTexture = Resources.Load<Texture2D>("Texturas/UI/Btn_Hover");

        if (cachedStyles == null)
        {
            cachedStyles = new MenuStyles(btnNormalTexture, btnHoverTexture);
        }
        var s = cachedStyles;

        // --- RENDER DE LA PANTALLA DE CALIBRACIÓN DE BRILLO EN PARTIDA ---
        if (isCalibratingGamma)
        {
            int calibW = 820;
            int calibH = 580;
            GUILayout.BeginArea(new Rect(Screen.width / 2 - calibW / 2, Screen.height / 2 - calibH / 2, calibW, calibH));
            DrawGammaCalibrationArea(s.Label, s.Button, s.SectionHeader, s);
            GUILayout.EndArea();
            return;
        }

        // Título del menú de pausa
        GUILayout.BeginArea(new Rect(0, 80, Screen.width, 150));
        
        GUIStyle titleStyle = new GUIStyle(s.Title);
        titleStyle.fontSize = 50;
        
        GUILayout.Label(LocalizationManager.Instance.Get("pause_title"), titleStyle, GUILayout.Height(55));
        GUILayout.Label(LocalizationManager.Instance.Get("pause_subtitle"), s.SubTitle, GUILayout.Height(20));
        GUILayout.EndArea();

        // Contenedor central de botones
        // El título ocupa y=80 a y=230 (height=150). El contenido siempre arranca debajo.
        float titleAreaBottom = 245f; // 80 (top) + 150 (height) + 15 margen extra
        float availableH = Screen.height - titleAreaBottom - 20f;

        int menuWidth  = (currentState == PauseState.Settings) ? 580 : 450;
        int menuHeight = (currentState == PauseState.Settings)
            ? (int)Mathf.Min(700f, availableH)
            : 450;
        float menuY = (currentState == PauseState.Settings)
            ? titleAreaBottom
            : Screen.height / 2f - 80f;
        GUILayout.BeginArea(new Rect(Screen.width / 2 - menuWidth / 2, menuY, menuWidth, menuHeight));

        if (currentState == PauseState.Paused)
        {
            GUILayout.Space(20);
            
            // BOTÓN REANUDAR
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_resume"), s.Button, GUILayout.Height(60)))
            {
                ResumeGame();
            }
            GUILayout.Space(25);

            // BOTÓN OPCIONES
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_options"), s.Button, GUILayout.Height(60)))
            {
                PlayClickSound();
                currentState = PauseState.Settings;
            }
            GUILayout.Space(15);

            // BOTÓN SALIR AL MENÚ
            var quitBtnStyle = new GUIStyle(s.Button);
            quitBtnStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_quit"), quitBtnStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                Time.timeScale = 1f; // Reestablecer escala de tiempo antes de cambiar de escena
                
                if (SilentDecay.Core.AdManager.Instance != null)
                {
                    SilentDecay.Core.AdManager.Instance.ShowInterstitialTransition(() =>
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                    });
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                }
            }
        }
        else if (currentState == PauseState.Settings)
        {
            GUILayout.Label(LocalizationManager.Instance.Get("pause_settings_title"), s.SectionHeader, GUILayout.Height(50));
            GUILayout.Space(15);

            // Estilos para pestañas
            GUIStyle tabButtonStyle = new GUIStyle(s.TabButton);

            GUILayout.BeginHorizontal();
            
            // Pestaña Audio & Sensibilidad
            tabButtonStyle.normal.textColor = activeSettingsTab == 0 ? s.BrandRed : Color.gray;
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_tab_audio"), tabButtonStyle, GUILayout.Height(35)))
            {
                PlayClickSound();
                activeSettingsTab = 0;
            }

            // Pestaña Gráficos y Rendimiento
            tabButtonStyle.normal.textColor = activeSettingsTab == 1 ? s.BrandRed : Color.gray;
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_tab_graphics"), tabButtonStyle, GUILayout.Height(35)))
            {
                PlayClickSound();
                activeSettingsTab = 1;
            }

            // Pestaña Controles
            tabButtonStyle.normal.textColor = activeSettingsTab == 2 ? s.BrandRed : Color.gray;
            string controlsTabTitle = GetLocalized("CONTROLES", "CONTROLS", "CONTROLES", "УПРАВЛЕНИЕ");
            if (GUILayout.Button(controlsTabTitle, tabButtonStyle, GUILayout.Height(35)))
            {
                PlayClickSound();
                activeSettingsTab = 2;
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            if (activeSettingsTab == 0)
            {
                // 1. Control de volumen
                GUILayout.Label(LocalizationManager.Instance.GetFormat("pause_volume", Mathf.RoundToInt(masterVolume * 100)), s.Label);
                masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f, s.SliderTrack, s.SliderThumb, GUILayout.Height(32f));
                AudioListener.volume = masterVolume;
                GUILayout.Space(8);

                // 2. Control de sensibilidad
                GUILayout.Label(LocalizationManager.Instance.GetFormat("pause_sensitivity", mouseSensitivity), s.Label);
                mouseSensitivity = GUILayout.HorizontalSlider(mouseSensitivity, 0.5f, 6.0f, s.SliderTrack, s.SliderThumb, GUILayout.Height(32f));
                if (playerObj != null)
                {
                    var controller = playerObj.GetComponentInChildren<StarterAssets.FirstPersonController>();
                    if (controller != null)
                    {
                        controller.RotationSpeed = mouseSensitivity;
                    }
                }
                GUILayout.Space(8);

                // 3. Control de Escala de Interfaz / HUD (Móvil y PC)
                float currentHudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
                GUILayout.Label(LocalizationManager.Instance.GetFormat("pause_hud", currentHudScale), s.Label);
                float newHudScale = GUILayout.HorizontalSlider(currentHudScale, 0.85f, 1.75f, s.SliderTrack, s.SliderThumb, GUILayout.Height(32f));
                if (Mathf.Abs(newHudScale - currentHudScale) > 0.01f)
                {
                    PlayerPrefs.SetFloat("HUDScale", newHudScale);
                    PlayerPrefs.Save();
                }
                GUILayout.Space(8);

                // Pantalla completa
                GUILayout.BeginHorizontal();
                GUILayout.Label(LocalizationManager.Instance.Get("pause_fullscreen"), s.Label, GUILayout.Width(200));
                isFullscreen = GUILayout.Toggle(isFullscreen, "", s.Toggle);
                if (Screen.fullScreen != isFullscreen)
                {
                    Screen.fullScreen = isFullscreen;
                }
                GUILayout.EndHorizontal();
            }
            else if (activeSettingsTab == 1)
            {
                // CALIDAD DE GRÁFICOS
                GUILayout.Label(LocalizationManager.Instance.Get("pause_graphics_quality"), s.Label);
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                
                GUIStyle optionSelectStyle = new GUIStyle(s.OptionSelect);

                string[] qualityLevels = { "BAJO", "MEDIO", "ALTO" };
                for (int i = 0; i < qualityLevels.Length; i++)
                {
                    bool isSelected = selectedQualityIndex == i;
                    optionSelectStyle.normal.textColor = isSelected ? s.BrandRed : Color.gray;
                    if (GUILayout.Button(qualityLevels[i], optionSelectStyle, GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        selectedQualityIndex = i;
                        QualitySettings.SetQualityLevel(i, true);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(25);

                // RESOLUCIÓN (PC vs Móvil)
                GUILayout.Label(LocalizationManager.Instance.Get("pause_resolution"), s.Label);
                GUILayout.Space(5);

                #if UNITY_ANDROID || UNITY_IOS
                GUIStyle centeredLabelStyle = new GUIStyle(s.Label);
                centeredLabelStyle.normal.textColor = Color.gray;
                GUILayout.Label(LocalizationManager.Instance.GetFormat("pause_native_res", Screen.currentResolution.width, Screen.currentResolution.height), centeredLabelStyle, GUILayout.Height(35));
                #else
                if (pcResolutions != null && pcResolutions.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    
                    GUIStyle cycleButtonStyle = new GUIStyle(s.SmallButton);

                    if (GUILayout.Button("<", cycleButtonStyle, GUILayout.Width(45), GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        selectedResIndex = (selectedResIndex - 1 + pcResolutions.Count) % pcResolutions.Count;
                        Resolution targetRes = pcResolutions[selectedResIndex];
                        Screen.SetResolution(targetRes.width, targetRes.height, isFullscreen);
                    }

                    GUIStyle resLabelStyle = new GUIStyle(s.Label);
                    resLabelStyle.alignment = TextAnchor.MiddleCenter;
                    GUILayout.Label($"{pcResolutions[selectedResIndex].width} x {pcResolutions[selectedResIndex].height}", resLabelStyle, GUILayout.Height(35));

                    if (GUILayout.Button(">", cycleButtonStyle, GUILayout.Width(45), GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        selectedResIndex = (selectedResIndex + 1) % pcResolutions.Count;
                        Resolution targetRes = pcResolutions[selectedResIndex];
                        Screen.SetResolution(targetRes.width, targetRes.height, isFullscreen);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label($"{Screen.width}x{Screen.height}", s.Label);
                }
                #endif

                GUILayout.Space(25);

                // Botón para calibración dedicada
                float savedGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
                string gammaLabel = LocalizationManager.Instance != null &&
                    LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH
                    ? $"ADJUST BRIGHTNESS  ({savedGamma:F1}x)"
                    : $"AJUSTAR BRILLO  ({savedGamma:F1}x)";
                if (GUILayout.Button(gammaLabel, s.Button, GUILayout.Height(45)))
                {
                    PlayClickSound();
                    tempGamma = savedGamma;
                    isCalibratingGamma = true;
                }
            }
            else
            {
                // CONTROLES IN-GAME
                DrawPauseControlsTab(s);
            }

            GUILayout.Space(20);

            // Guardar y Volver
            if (GUILayout.Button(LocalizationManager.Instance.Get("pause_save_back"), s.Button, GUILayout.Height(65)))
            {
                PlayClickSound();
                PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.SetInt("QualityLevel", selectedQualityIndex);
                PlayerPrefs.Save();
                currentState = PauseState.Paused;
            }
        }

        GUILayout.EndArea();
    }

    // ─── PANTALLA GIGANTE DE CALIBRACIÓN DE BRILLO IN-GAME ────────────────────
    private void DrawGammaCalibrationArea(GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle headerStyle, MenuStyles s)
    {
        // 1. Título e Instrucciones
        GUILayout.Label(LocalizationManager.Instance.Get("pause_gamma_title"), headerStyle, GUILayout.Height(30));
        GUILayout.Space(10);

        string instructions = LocalizationManager.Instance.Get("pause_gamma_inst");
        GUIStyle instStyle = new GUIStyle(labelStyle);
        instStyle.fontSize = 15;
        instStyle.fontStyle = FontStyle.Normal;
        instStyle.wordWrap = true;
        instStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        
        GUILayout.Label(instructions, instStyle, GUILayout.Width(680));
        GUILayout.Space(15);

        // 2. Controles horizontales (Slider + Caja de calibración)
        GUILayout.BeginHorizontal(GUILayout.Width(680));

        // Subpanel Izquierdo: Controles finos y Slider (Ancho 380)
        GUILayout.BeginVertical(GUILayout.Width(380));
        GUILayout.Space(25);

        GUILayout.Label(LocalizationManager.Instance.GetFormat("pause_gamma_level", tempGamma), s.Label);
        GUILayout.Space(5);

        // Slider de Brillo
        float newGamma = GUILayout.HorizontalSlider(tempGamma, 0.5f, 2.0f, s.SliderTrack, s.SliderThumb, GUILayout.Height(36f), GUILayout.Width(360));
        if (Mathf.Abs(newGamma - tempGamma) > 0.005f)
        {
            tempGamma = newGamma;
            GammaManager.AplicarGamma(tempGamma); // Aplicar cambios a la luz 3D de inmediato
        }
        GUILayout.Space(10);

        // Botones finos [-] y [+]
        GUILayout.BeginHorizontal(GUILayout.Width(360));
        if (GUILayout.Button(" - 0.1 ", s.SmallButton, GUILayout.Width(90), GUILayout.Height(40)))
        {
            PlayClickSound();
            tempGamma = Mathf.Clamp(tempGamma - 0.1f, 0.5f, 2.0f);
            GammaManager.AplicarGamma(tempGamma);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(" + 0.1 ", s.SmallButton, GUILayout.Width(90), GUILayout.Height(40)))
        {
            PlayClickSound();
            tempGamma = Mathf.Clamp(tempGamma + 0.1f, 0.5f, 2.0f);
            GammaManager.AplicarGamma(tempGamma);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(40);

        // Subpanel Derecho: Caja de calibración con engranaje (Ancho 220)
        GUIStyle darkBoxStyle = new GUIStyle(GUI.skin.box);
        darkBoxStyle.normal.background = Texture2D.whiteTexture;

        float colorFactor = Mathf.Clamp01((tempGamma - 0.5f) / 1.5f); // 0 a 1
        float iconColorValue = Mathf.Lerp(0.02f, 0.40f, colorFactor); // De casi negro a gris medio
        Color dynamicGearColor = new Color(iconColorValue, iconColorValue, iconColorValue, 1f);

        GUIStyle gearIconStyle = new GUIStyle(labelStyle);
        gearIconStyle.fontSize = 110;
        gearIconStyle.alignment = TextAnchor.MiddleCenter;
        gearIconStyle.normal.textColor = dynamicGearColor;

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.015f, 0.015f, 0.015f, 1f); // Fondo muy oscuro
        GUILayout.BeginVertical(darkBoxStyle, GUILayout.Width(220), GUILayout.Height(220));
        GUI.backgroundColor = prevColor;

        Texture2D gearTex = Resources.Load<Texture2D>("UI/HUD_Gear_Icon");
        if (gearTex != null)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.color = dynamicGearColor;
            Rect gearRect = GUILayoutUtility.GetRect(130, 130, GUILayout.Width(130), GUILayout.Height(130));
            GUI.DrawTexture(gearRect, gearTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("⚙", gearIconStyle);
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(30);

        // 3. Botones inferiores: Guardar y Cancelar
        GUILayout.BeginHorizontal(GUILayout.Width(800));

        if (GUILayout.Button(LocalizationManager.Instance.Get("pause_gamma_confirm"), buttonStyle, GUILayout.Width(380), GUILayout.Height(65)))
        {
            PlayClickSound();
            PlayerPrefs.SetFloat("GammaLevel", tempGamma);
            PlayerPrefs.Save();
            GammaManager.AplicarGamma(tempGamma);
            isCalibratingGamma = false;
        }

        GUILayout.Space(20);

        if (GUILayout.Button(LocalizationManager.Instance.Get("pause_gamma_cancel"), buttonStyle, GUILayout.Width(380), GUILayout.Height(65)))
        {
            PlayClickSound();
            // Revertir al valor original antes de abrir el menú de calibración
            float originalGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
            GammaManager.AplicarGamma(originalGamma);
            isCalibratingGamma = false;
        }

        GUILayout.EndHorizontal();
    }

    private static Texture2D gearTex;
    private static Texture2D GetGearTexture()
    {
        if (gearTex == null) gearTex = Resources.Load<Texture2D>("UI/HUD_Gear_Icon");
        return gearTex;
    }

    // ─── Pestaña de Controles In-Game ─────────────────────────────────────────
    private Vector2 pauseControlsScroll = Vector2.zero;

    private string GetLocalized(string es, string en, string pt, string ru)
    {
        if (LocalizationManager.Instance == null) return es;
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ESPAÑOL => es,
            LocalizationManager.Idioma.ENGLISH => en,
            LocalizationManager.Idioma.PORTUGUES => pt,
            LocalizationManager.Idioma.РУССКИЙ => ru,
            _ => es
        };
    }

    private void DrawPauseControlsTab(MenuStyles s)
    {
        pauseControlsScroll = GUILayout.BeginScrollView(pauseControlsScroll, GUILayout.Height(330));

        GUIStyle headerStyle = new GUIStyle(s.SectionHeader);
        headerStyle.fontSize = 20;
        headerStyle.alignment = TextAnchor.MiddleLeft;
        headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.70f);

        // --- TECLADO Y RATÓN ---
        string pcHeader = GetLocalized("⌨️ TECLADO Y RATÓN (PC)", "⌨️ KEYBOARD & MOUSE (PC)", "⌨️ TECLADO E MOUSE (PC)", "⌨️ КЛАВИАТУРА И МЫШЬ (ПК)");
        GUILayout.Label(pcHeader, headerStyle);
        GUILayout.Space(6);

        DrawPauseControlRow(s, "W / A / S / D", GetLocalized("Moverse / Caminar", "Move / Walk", "Mover-se / Andar", "Движение / Ходьба"));
        DrawPauseControlRow(s, GetLocalized("Shift (Mantener)", "Shift (Hold)", "Shift (Segurar)", "Shift (Удерживать)"), GetLocalized("Correr / Sprint", "Sprint / Run", "Correr / Sprint", "Бег / Спринт"));
        DrawPauseControlRow(s, GetLocalized("E / Clic Izq.", "E / Left Click", "E / Clique Esq.", "E / ЛКМ"), GetLocalized("Interactuar / Recoger notas / Usar máquinas", "Interact / Pick Up / Use machines", "Interagir / Pegar notas / Usar máquinas", "Взаимодействие / Взять / Использовать"));
        DrawPauseControlRow(s, "F", GetLocalized("Encender / Apagar Linterna", "Flashlight (Toggle)", "Ligar / Desligar Lanterna", "Фонарик (Вкл/Выкл)"));
        DrawPauseControlRow(s, "M", GetLocalized("Abrir Mapa directamente", "Open Map directly", "Abrir Mapa diretamente", "Открыть карту"));
        DrawPauseControlRow(s, "Tab / N", GetLocalized("Abrir Libreta (Claves, Mapa, Registros)", "Open Notepad (Notes, Map, Lore)", "Abrir Caderno (Notas, Mapa, Lore)", "Блокнот (Коды, Карта, Заметки)"));
        DrawPauseControlRow(s, "ESC", GetLocalized("Pausar / Cerrar menús", "Pause Menu / Close UI", "Pausa / Fechar UI", "Меню паузы / Закрыть меню"));

        GUILayout.Space(14);

        // --- CONTROLES TÁCTILES ---
        string touchHeader = GetLocalized("📱 CONTROLES TÁCTILES (MÓVIL)", "📱 TOUCH CONTROLS (MOBILE)", "📱 CONTROLES DE TOQUE (MOBILE)", "📱 СЕНСОРНОЕ УПРАВЛЕНИЕ (ТЕЛЕФОН)");
        GUILayout.Label(touchHeader, headerStyle);
        GUILayout.Space(6);

        DrawPauseControlRow(s, GetLocalized("Joystick Izquierdo", "Left Joystick", "Joystick Esquerdo", "Левый джойстик"), GetLocalized("Mover al personaje", "Move character", "Mover o personagem", "Передвижение персонажа"));
        DrawPauseControlRow(s, GetLocalized("Deslizar Pantalla", "Touch & Drag", "Arrastar na Tela", "Проведение по экрану"), GetLocalized("Rotar cámara / Mirar", "Look around / Aim", "Olhar ao redor / Mirar", "Обзор камеры / Прицел"));
        DrawPauseControlRow(s, GetLocalized("Botón 'Uso'", "'Use' Button", "Botão 'Uso'", "Кнопка 'Использование'"), GetLocalized("Interactuar con puertas, objetos y generadores", "Interact with doors, items & generators", "Interagir com portas, itens e geradores", "Взаимодействие с дверьми и предметами"));
        DrawPauseControlRow(s, GetLocalized("Botón 'Luz'", "'Light' Button", "Botão 'Luz'", "Кнопка 'Свет'"), GetLocalized("Alternar Linterna", "Toggle Flashlight", "Alternar Lanterna", "Включить / Выключить фонарик"));
        DrawPauseControlRow(s, GetLocalized("Botón 'Correr'", "'Sprint' Button", "Botão 'Correr'", "Кнопка 'Бег'"), GetLocalized("Activar sprint / Correr", "Toggle Sprint / Run", "Ativar sprint / Correr", "Бег / Ускорение"));

        GUILayout.EndScrollView();
    }

    private void DrawPauseControlRow(MenuStyles s, string key, string desc)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);

        GUIStyle keyStyle = new GUIStyle(s.Label);
        keyStyle.fontSize = 18;
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.normal.textColor = new Color(0.95f, 0.45f, 0.45f);
        keyStyle.alignment = TextAnchor.MiddleLeft;
        GUILayout.Label(key, keyStyle, GUILayout.Width(220));

        GUIStyle descStyle = new GUIStyle(s.Label);
        descStyle.fontSize = 17;
        descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        descStyle.alignment = TextAnchor.MiddleLeft;
        GUILayout.Label(desc, descStyle);

        GUILayout.EndHorizontal();
        GUILayout.Space(2);
    }
}
