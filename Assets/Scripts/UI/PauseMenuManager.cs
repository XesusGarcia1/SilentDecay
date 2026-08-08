using UnityEngine;

public class PauseMenuManager : MonoBehaviour
{
    private enum PauseState { None, Paused, Settings }
    private PauseState currentState = PauseState.None;

    private float mouseSensitivity = 2.0f;
    private float masterVolume = 1.0f;
    private bool isFullscreen = true;

    // Ajustes de gráficos y pestañas en el menú de pausa
    private int activeSettingsTab = 0; // 0 = Audio y Sensibilidad, 1 = Gráficos y Rendimiento
    private int selectedQualityIndex = 2; // 0 = Bajo, 1 = Medio, 2 = Alto
    
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
        // Asegurar referencia del jugador si no se encontró al arrancar
        if (playerObj == null)
        {
            playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
        }

        // Detectar pulsación de la tecla Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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
            if (GUI.Button(gearRect, "⚙", gearButtonStyle))
            {
                PauseGame();
            }

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

        // Estilos
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 50;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.85f, 0.05f, 0.05f); // Rojo sangre
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = 16;
        subtitleStyle.fontStyle = FontStyle.Italic;
        subtitleStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
        subtitleStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle sectionHeaderStyle = new GUIStyle(GUI.skin.label);
        sectionHeaderStyle.fontSize = 24;
        sectionHeaderStyle.fontStyle = FontStyle.Bold;
        sectionHeaderStyle.normal.textColor = Color.white;
        sectionHeaderStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 22;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        buttonStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        buttonStyle.hover.textColor = Color.red;
        buttonStyle.active.textColor = Color.red;

        // --- RENDER DE LA PANTALLA DE CALIBRACIÓN DE BRILLO EN PARTIDA ---
        if (isCalibratingGamma)
        {
            int calibW = 700;
            int calibH = 580;
            GUILayout.BeginArea(new Rect(Screen.width / 2 - calibW / 2, Screen.height / 2 - calibH / 2, calibW, calibH));
            DrawGammaCalibrationArea(labelStyle, buttonStyle, sectionHeaderStyle);
            GUILayout.EndArea();
            return;
        }

        // Título del menú de pausa
        GUILayout.BeginArea(new Rect(0, 80, Screen.width, 150));
        GUILayout.Label("PARTIDA EN PAUSA", titleStyle, GUILayout.Height(55));
        GUILayout.Label("• TRANSMISION SUSPENDIDA  |  VHS", subtitleStyle, GUILayout.Height(20));
        GUILayout.EndArea();

        // Contenedor central de botones
        int menuWidth = 450;
        int menuHeight = 450;
        GUILayout.BeginArea(new Rect(Screen.width / 2 - menuWidth / 2, Screen.height / 2 - 80, menuWidth, menuHeight));

        if (currentState == PauseState.Paused)
        {
            GUILayout.Space(20);
            
            // BOTÓN REANUDAR
            if (GUILayout.Button("  REANUDAR PARTIDA", buttonStyle, GUILayout.Height(60)))
            {
                ResumeGame();
            }
            GUILayout.Space(25);

            // BOTÓN OPCIONES
            if (GUILayout.Button("  OPCIONES", buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                currentState = PauseState.Settings;
            }
            GUILayout.Space(25);

            // BOTÓN SALIR AL MENÚ
            buttonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            if (GUILayout.Button("  SALIR AL MENÚ", buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                Time.timeScale = 1f; // Reestablecer escala de tiempo antes de cambiar de escena
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
        else if (currentState == PauseState.Settings)
        {
            GUILayout.Label("CONFIGURACIÓN DE AJUSTES", sectionHeaderStyle, GUILayout.Height(30));
            GUILayout.Space(15);

            // Estilos para pestañas
            GUIStyle tabButtonStyle = new GUIStyle(GUI.skin.button);
            tabButtonStyle.fontSize = 15;
            tabButtonStyle.fontStyle = FontStyle.Bold;
            tabButtonStyle.normal.textColor = Color.gray;
            tabButtonStyle.hover.textColor = Color.red;

            GUILayout.BeginHorizontal();
            
            // Pestaña Audio & Sensibilidad
            tabButtonStyle.normal.textColor = activeSettingsTab == 0 ? Color.red : Color.gray;
            if (GUILayout.Button("AUDIO Y CONTROLES", tabButtonStyle, GUILayout.Height(35)))
            {
                PlayClickSound();
                activeSettingsTab = 0;
            }

            // Pestaña Gráficos y Rendimiento
            tabButtonStyle.normal.textColor = activeSettingsTab == 1 ? Color.red : Color.gray;
            if (GUILayout.Button("GRÁFICOS Y RENDIMIENTO", tabButtonStyle, GUILayout.Height(35)))
            {
                PlayClickSound();
                activeSettingsTab = 1;
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            if (activeSettingsTab == 0)
            {
                // Control de volumen
                GUILayout.Label($"Volumen de Audio: {Mathf.RoundToInt(masterVolume * 100)}%", labelStyle);
                masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);
                AudioListener.volume = masterVolume;
                GUILayout.Space(15);

                // Control de sensibilidad
                GUILayout.Label($"Sensibilidad de Cámara: {mouseSensitivity:F1}", labelStyle);
                mouseSensitivity = GUILayout.HorizontalSlider(mouseSensitivity, 0.5f, 6.0f);
                
                // Aplicar sensibilidad al controlador en tiempo real si existe
                if (playerObj != null)
                {
                    var controller = playerObj.GetComponentInChildren<StarterAssets.FirstPersonController>();
                    if (controller != null)
                    {
                        controller.RotationSpeed = mouseSensitivity;
                    }
                }
                GUILayout.Space(15);

                // Pantalla completa
                GUILayout.BeginHorizontal();
                GUILayout.Label("Pantalla Completa", labelStyle, GUILayout.Width(200));
                isFullscreen = GUILayout.Toggle(isFullscreen, "");
                if (Screen.fullScreen != isFullscreen)
                {
                    Screen.fullScreen = isFullscreen;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // CALIDAD DE GRÁFICOS
                GUILayout.Label("Calidad de Gráficos:", labelStyle);
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                
                GUIStyle optionSelectStyle = new GUIStyle(GUI.skin.button);
                optionSelectStyle.fontSize = 15;
                optionSelectStyle.fontStyle = FontStyle.Bold;

                string[] qualityLevels = { "BAJO", "MEDIO", "ALTO" };
                for (int i = 0; i < qualityLevels.Length; i++)
                {
                    bool isSelected = selectedQualityIndex == i;
                    optionSelectStyle.normal.textColor = isSelected ? Color.red : Color.gray;
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
                GUILayout.Label("Resolución de Pantalla:", labelStyle);
                GUILayout.Space(5);

                #if UNITY_ANDROID || UNITY_IOS
                GUIStyle centeredLabelStyle = new GUIStyle(labelStyle);
                centeredLabelStyle.normal.textColor = Color.gray;
                GUILayout.Label($"{Screen.currentResolution.width}x{Screen.currentResolution.height} (Nativa del Dispositivo)", centeredLabelStyle, GUILayout.Height(35));
                #else
                if (pcResolutions != null && pcResolutions.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    
                    GUIStyle cycleButtonStyle = new GUIStyle(GUI.skin.button);
                    cycleButtonStyle.fontSize = 18;
                    cycleButtonStyle.fontStyle = FontStyle.Bold;

                    if (GUILayout.Button("<", cycleButtonStyle, GUILayout.Width(45), GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        selectedResIndex = (selectedResIndex - 1 + pcResolutions.Count) % pcResolutions.Count;
                        Resolution targetRes = pcResolutions[selectedResIndex];
                        Screen.SetResolution(targetRes.width, targetRes.height, isFullscreen);
                    }

                    GUIStyle resLabelStyle = new GUIStyle(labelStyle);
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
                    GUILayout.Label($"{Screen.width}x{Screen.height}", labelStyle);
                }
                #endif

                GUILayout.Space(25);

                // Botón para calibración dedicada
                float savedGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
                if (GUILayout.Button($"🔧 AJUSTAR BRILLO / GAMMA... (Actual: {savedGamma:F1}x)", buttonStyle, GUILayout.Height(45)))
                {
                    PlayClickSound();
                    tempGamma = savedGamma;
                    isCalibratingGamma = true;
                }
            }

            GUILayout.Space(30);

            // Guardar y Volver
            if (GUILayout.Button("  GUARDAR Y VOLVER", buttonStyle, GUILayout.Height(50)))
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
    private void DrawGammaCalibrationArea(GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle headerStyle)
    {
        // 1. Título e Instrucciones
        GUILayout.Label("CALIBRACIÓN DE BRILLO / GAMMA", headerStyle, GUILayout.Height(30));
        GUILayout.Space(10);

        string instructions = "Ajusta el brillo hasta que el icono del engranaje de la derecha sea apenas visible sobre el fondo oscuro.";
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

        GUILayout.Label($"Brillo del Juego: {tempGamma:F2}x", labelStyle);
        GUILayout.Space(5);

        // Slider de Brillo
        float newGamma = GUILayout.HorizontalSlider(tempGamma, 0.5f, 2.0f, GUILayout.Height(30), GUILayout.Width(360));
        if (Mathf.Abs(newGamma - tempGamma) > 0.005f)
        {
            tempGamma = newGamma;
            GammaManager.AplicarGamma(tempGamma); // Aplicar cambios a la luz 3D de inmediato
        }
        GUILayout.Space(10);

        // Botones finos [-] y [+]
        GUILayout.BeginHorizontal(GUILayout.Width(360));
        if (GUILayout.Button(" - 0.1 ", GUILayout.Width(90), GUILayout.Height(40)))
        {
            PlayClickSound();
            tempGamma = Mathf.Clamp(tempGamma - 0.1f, 0.5f, 2.0f);
            GammaManager.AplicarGamma(tempGamma);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(" + 0.1 ", GUILayout.Width(90), GUILayout.Height(40)))
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

        GUILayout.FlexibleSpace();
        GUILayout.Label("⚙", gearIconStyle);
        GUILayout.FlexibleSpace();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(30);

        // 3. Botones inferiores: Guardar y Cancelar
        GUILayout.BeginHorizontal(GUILayout.Width(680));

        if (GUILayout.Button("  CONFIRMAR Y GUARDAR", buttonStyle, GUILayout.Width(330), GUILayout.Height(55)))
        {
            PlayClickSound();
            PlayerPrefs.SetFloat("GammaLevel", tempGamma);
            PlayerPrefs.Save();
            GammaManager.AplicarGamma(tempGamma);
            isCalibratingGamma = false;
        }

        GUILayout.Space(20);

        if (GUILayout.Button("  CANCELAR", buttonStyle, GUILayout.Width(330), GUILayout.Height(55)))
        {
            PlayClickSound();
            // Revertir al valor original antes de abrir el menú de calibración
            float originalGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
            GammaManager.AplicarGamma(originalGamma);
            isCalibratingGamma = false;
        }

        GUILayout.EndHorizontal();
    }
}
