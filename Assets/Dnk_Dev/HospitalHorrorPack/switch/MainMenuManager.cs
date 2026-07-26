using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    public static bool startedFromMenu = false; // NUEVO: Para saber si iniciamos desde el menú
    public HospitalMazeGenerator generator;
    public ModularHospital.ModularHospitalGenerator modularGenerator;

    [Header("Ajustes del Menú")]
    private string[] difficulties = { "FÁCIL", "NORMAL", "DIFÍCIL" };
    private int selectedDifficultyIndex = 1; // Normal por defecto

    private string[] mapSizes = { "CHICO (10x10)", "MEDIANO (12x12)", "GRANDE (14x14)" };
    private int selectedMapSizeIndex = 0; // Chico por defecto

    private enum MenuState { Main, LevelSelect, PlayOptions, Settings }
    private MenuState currentState = MenuState.Main;

    // Configuración ajustable
    [Header("Título Personalizado")]
    public string gameTitle = "SILENT DECAY";

    [Header("Sonidos de Menú")]
    public AudioClip menuMusic; // Permite arrastrar cualquier canción en el Inspector
    private AudioSource menuAudioSource;
    private AudioSource sfxAudioSource; // AudioSource dedicado exclusivamente a clics para latencia cero
    private AudioClip buttonClickSound;

    private float mouseSensitivity = 2.0f;
    private float masterVolume = 1.0f;
    private bool isFullscreen = true;

    private float startYaw = -999f;
    private Texture2D sidebarTex;
    private Texture2D texHospitalThumb;

    [Header("Redes Sociales (Specimen Style)")]
    public string instagramURL = "https://www.instagram.com/lxesusgarcial";
    public string facebookURL = "https://www.facebook.com/lXesusGarcial";
    public string youtubeURL = "https://www.youtube.com/@Xesus_Garcia";

    private Texture2D texInstagram;
    private Texture2D texFacebook;
    private Texture2D texYoutube;

    // Ajustes de gráficos y pestañas en menú de configuración
    private int activeSettingsTab = 0; // 0 = Audio y Sensibilidad, 1 = Gráficos y Rendimiento
    private int selectedQualityIndex = 2; // 0 = Bajo, 1 = Medio, 2 = Alto
    #if !UNITY_ANDROID && !UNITY_IOS
    private System.Collections.Generic.List<Resolution> pcResolutions = new System.Collections.Generic.List<Resolution>();
    private int selectedResIndex = 0;
    #endif

    void Start()
    {
        // Configurar atmósfera tétrica idéntica al mapa real (oscuridad total salvo las lámparas)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientSkyColor = Color.black;
        RenderSettings.ambientEquatorColor = Color.black;
        RenderSettings.ambientGroundColor = Color.black;
        RenderSettings.ambientIntensity = 0.0f;
        RenderSettings.reflectionIntensity = 0.0f;

        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.08f;

        // Eliminar Directional Light
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                l.enabled = false;
                l.intensity = 0f;
            }
        }

        // Forzar orientación horizontal (Landscape) en móviles
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        if (modularGenerator == null)
        {
            modularGenerator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>(true);
        }

        if (modularGenerator != null)
        {
            modularGenerator.isMenuMode = true;
            modularGenerator.generateOnStart = true;
            modularGenerator.smallMapGridSize = new Vector2Int(8, 8);
        }

        if (generator == null)
        {
            generator = FindObjectOfType<HospitalMazeGenerator>();
        }

        // Crear una textura negra sutil y muy transparente para toda la pantalla
        sidebarTex = new Texture2D(2, 2);
        Color panelColor = new Color(0.0f, 0.0f, 0.0f, 0.45f);
        sidebarTex.SetPixel(0, 0, panelColor);
        sidebarTex.SetPixel(0, 1, panelColor);
        sidebarTex.SetPixel(1, 0, panelColor);
        sidebarTex.SetPixel(1, 1, panelColor);
        sidebarTex.Apply();

        // Cargar iconos de redes sociales desde Resources
        texInstagram = Resources.Load<Texture2D>("social_instagram");
        texFacebook = Resources.Load<Texture2D>("social_facebook");
        texYoutube = Resources.Load<Texture2D>("social_youtube");
        texHospitalThumb = Resources.Load<Texture2D>("game1");

        // Forzar cursor libre y visible al arrancar el menú
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Cargar sensibilidades y volumen guardados
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        AudioListener.volume = masterVolume;
        isFullscreen = Screen.fullScreen;

        selectedQualityIndex = PlayerPrefs.GetInt("QualityLevel", 2);
        QualitySettings.SetQualityLevel(selectedQualityIndex, true);

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

        // Crear reproductores de audio
        GameObject audioObj = new GameObject("MenuAudioSource");
        audioObj.transform.SetParent(transform);
        menuAudioSource = audioObj.AddComponent<AudioSource>();
        menuAudioSource.loop = true;
        menuAudioSource.spatialBlend = 0f; // Sonido 2D (Auriculares)
        menuAudioSource.volume = masterVolume * 0.6f;

        if (menuMusic != null)
        {
            menuAudioSource.clip = menuMusic;
            menuAudioSource.Play();
        }
        else
        {
            AudioClip defaultMusic = Resources.Load<AudioClip>("Song");
            if (defaultMusic != null)
            {
                menuAudioSource.clip = defaultMusic;
                menuAudioSource.Play();
            }
        }

        GameObject sfxObj = new GameObject("MenuSFXAudioSource");
        sfxObj.transform.SetParent(transform);
        sfxAudioSource = sfxObj.AddComponent<AudioSource>();
        sfxAudioSource.spatialBlend = 0f;
        sfxAudioSource.volume = masterVolume * 0.85f;

        buttonClickSound = Resources.Load<AudioClip>("Interruptor");
    }

    private void PlayClickSound()
    {
        if (sfxAudioSource != null && buttonClickSound != null)
        {
            sfxAudioSource.PlayOneShot(buttonClickSound);
        }
    }

    private Light menuFlashlight;

    void Update()
    {
        var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.isMenuMode)
        {
            if (startYaw == -999f) startYaw = 90f;
            float swayAngle = Mathf.Sin(Time.time * 0.25f) * 12f;
            float slowWalk = Mathf.Sin(Time.time * 0.12f) * 1.5f;
            if (Camera.main != null)
            {
                // Cámara viva: avanza y retrocede suavemente por el pasillo del hospital modular
                Camera.main.transform.position = modGen.transform.position + new Vector3(2.0f + slowWalk, 1.35f, 2.0f);
                Camera.main.transform.rotation = Quaternion.Euler(1.5f, startYaw + swayAngle, 0f);

                // Linterna potente en la cámara del menú con parpadeo atmosférico realista
                if (menuFlashlight == null)
                {
                    GameObject flashObj = new GameObject("[Menu_Flashlight]");
                    flashObj.transform.SetParent(Camera.main.transform);
                    flashObj.transform.localPosition = Vector3.zero;
                    flashObj.transform.localRotation = Quaternion.identity;

                    menuFlashlight = flashObj.AddComponent<Light>();
                    menuFlashlight.type = LightType.Spot;
                    menuFlashlight.range = 38f;
                    menuFlashlight.spotAngle = 75f; // Ángulo amplio para iluminar bien las paredes
                    menuFlashlight.color = new Color(0.98f, 0.96f, 0.90f); // Blanco linterna brillante

                    // Luz ambiental tenue de relleno
                    GameObject ambientObj = new GameObject("[Menu_FillLight]");
                    ambientObj.transform.SetParent(Camera.main.transform);
                    ambientObj.transform.localPosition = Vector3.zero;
                    Light fillLight = ambientObj.AddComponent<Light>();
                    fillLight.type = LightType.Point;
                    fillLight.range = 15f;
                    fillLight.intensity = 1.2f;
                    fillLight.color = new Color(0.25f, 0.35f, 0.30f); // Tinte verde hospitalario sutil
                }

                if (menuFlashlight != null)
                {
                    // Efecto de linterna potente (intensidad 3.5 a 6.0) con parpadeos sutiles
                    float noise = Mathf.PerlinNoise(Time.time * 7.0f, 0f);
                    float baseIntensity = Mathf.Lerp(3.2f, 5.8f, noise);

                    // Micro-chispazos ocasionales
                    if (Random.value < 0.025f)
                    {
                        baseIntensity *= Random.Range(0.45f, 0.70f); // Leve caída de voltaje sin oscurecer
                    }

                    menuFlashlight.intensity = baseIntensity;
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (generator != null && generator.isMenuMode && generator.grid != null)
        {
            float tileSize = generator.tileSize;
            Vector3 menuCamPos = generator.transform.position + new Vector3(generator.playerSpawnCell.x * tileSize, 1.25f, generator.playerSpawnCell.y * tileSize);
            if (Camera.main != null) Camera.main.transform.position = menuCamPos;

            if (startYaw == -999f)
            {
                Vector2Int cell = generator.playerSpawnCell;
                int width = generator.width;
                int height = generator.height;
                
                if (cell.y + 1 < height && generator.grid[cell.x, cell.y + 1]) startYaw = 0f;
                else if (cell.x + 1 < width && generator.grid[cell.x + 1, cell.y]) startYaw = 90f;
                else if (cell.y - 1 >= 0 && generator.grid[cell.x, cell.y - 1]) startYaw = 180f;
                else if (cell.x - 1 >= 0 && generator.grid[cell.x - 1, cell.y]) startYaw = 270f;
                else startYaw = 90f;
            }

            float angle = Mathf.Sin(Time.time * 0.16f) * 25f;
            if (Camera.main != null) Camera.main.transform.rotation = Quaternion.Euler(4f, startYaw + angle, 0f);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnGUI()
    {
        // En la escena del menú, la interfaz (botones JUGAR, AJUSTES, SALIR) siempre se debe dibujar
        // independientemente del generador activo.

        // Escalado dinámico de matriz GUI para móviles y alta densidad (Resolución de Referencia: 1920x1080)
        Vector2 scaleRef = new Vector2(1920f, 1080f);
        float scaleX = Screen.width / scaleRef.x;
        float scaleY = Screen.height / scaleRef.y;
        
        Matrix4x4 svMat = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

        // Dibujar el tinte oscuro sutil transparente cubriendo la pantalla virtual 1920x1080
        GUI.DrawTexture(new Rect(0, 0, 1920f, 1080f), sidebarTex);

        // Estilos del menú
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 62;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.85f, 0.05f, 0.05f); // Rojo sangre
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle subTitleStyle = new GUIStyle(GUI.skin.label);
        subTitleStyle.fontSize = 15;
        subTitleStyle.fontStyle = FontStyle.Italic;
        subTitleStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        subTitleStyle.alignment = TextAnchor.MiddleCenter;

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

        GUIStyle optionSelectStyle = new GUIStyle(GUI.skin.button);
        optionSelectStyle.fontSize = 16;
        optionSelectStyle.fontStyle = FontStyle.Bold;
        optionSelectStyle.alignment = TextAnchor.MiddleCenter;

        // Título del juego en el centro superior de la pantalla virtual 1920x1080
        GUILayout.BeginArea(new Rect(0, 60, 1920f, 150));
        GUILayout.Label(gameTitle, titleStyle, GUILayout.Height(65));
        GUILayout.Label("• REC  00:00:01  |  VHS  |  OCT.24 1997", subTitleStyle, GUILayout.Height(22));
        GUILayout.EndArea();

        // Área central para los botones del menú (referenciada a 1920x1080)
        int menuWidth = (currentState == MenuState.LevelSelect) ? 1280 : 480;
        int menuHeight = (currentState == MenuState.LevelSelect) ? 640 : 580;
        float menuY = (currentState == MenuState.LevelSelect) ? (1080f / 2f - 240f) : (1080f / 2f - 200f);
        GUILayout.BeginArea(new Rect(1920f / 2f - menuWidth / 2f, menuY, menuWidth, menuHeight));
        GUILayout.Space(10);

        if (currentState == MenuState.Main)
        {
            // BOTÓN JUGAR (Abre menú de opciones)
            string playBtn = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_jugar") : "INICIAR PARTIDA";
            if (GUILayout.Button($"  {playBtn}", buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                currentState = MenuState.LevelSelect;
            }
            GUILayout.Space(25);

            // BOTÓN CONFIGURACIÓN
            string settingsBtn = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_ajustes") : "CONFIGURACIÓN";
            if (GUILayout.Button($"  {settingsBtn}", buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                currentState = MenuState.Settings;
            }
            GUILayout.Space(25);

            // BOTÓN SALIR
            string exitBtn = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_salir") : "SALIR DEL JUEGO";
            if (GUILayout.Button($"  {exitBtn}", buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
        }
        else if (currentState == MenuState.LevelSelect)
        {
            string selectTitle = "SELECCIONA EL ESCENARIO";
            string hospitalLabel = "HOSPITAL Y TÚNELES";
            string lockedLabel = "PRÓXIMAMENTE";
            string backBtnText = "  ATRÁS";

            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                if (curLang == LocalizationManager.Idioma.ENGLISH)
                {
                    selectTitle = "SELECT MAP";
                    hospitalLabel = "HOSPITAL & TUNNELS";
                    lockedLabel = "COMING SOON";
                    backBtnText = "  BACK";
                }
                else if (curLang == LocalizationManager.Idioma.PORTUGUES)
                {
                    selectTitle = "SELECIONE O MAPA";
                    hospitalLabel = "HOSPITAL E TÚNEIS";
                    lockedLabel = "EM BREVE";
                    backBtnText = "  VOLTAR";
                }
            }

            GUILayout.Label(selectTitle, sectionHeaderStyle, GUILayout.Height(30));
            GUILayout.Space(25);

            GUILayout.BeginHorizontal();

            // --- TARJETA 1: HOSPITAL (ACTIVO) ---
            GUIStyle cardStyle = new GUIStyle(GUI.skin.button);
            cardStyle.normal.background = null;
            cardStyle.hover.background = null;
            cardStyle.active.background = null;
            cardStyle.padding = new RectOffset(10, 10, 10, 10);
            
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
            GUILayout.Space(10);
            
            // Imagen miniatura estirada al 100% de la caja de tamaño 356x200
            Rect thumbRect = GUILayoutUtility.GetRect(356f, 200f);
            if (texHospitalThumb != null)
            {
                GUI.DrawTexture(thumbRect, texHospitalThumb, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.DrawTexture(thumbRect, Texture2D.blackTexture, ScaleMode.StretchToFill);
            }

            GUILayout.Space(12);
            GUIStyle cardLabelStyle = new GUIStyle(labelStyle);
            cardLabelStyle.fontSize = 20;
            cardLabelStyle.fontStyle = FontStyle.Bold;
            cardLabelStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(hospitalLabel, cardLabelStyle, GUILayout.Height(50));

            GUILayout.Space(10);
            
            GUIStyle checkStyle = new GUIStyle(GUI.skin.label);
            checkStyle.fontSize = 32;
            checkStyle.normal.textColor = Color.green;
            checkStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("✓", checkStyle, GUILayout.Height(35));

            // Si el jugador hace clic en cualquier parte de esta tarjeta vertical, ir a opciones
            Rect cardRect = GUILayoutUtility.GetLastRect();
            // Truco: Hacer toda la caja del vertical clickable
            if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().y > 0)
            {
                // Manejado de forma más limpia usando un botón invisible en la tarjeta o detectando clic
            }
            
            GUILayout.Space(10);
            buttonStyle.normal.textColor = Color.red;
            buttonStyle.hover.textColor = Color.white;
            if (GUILayout.Button(LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "PLAY" : (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES ? "JOGAR" : "JUGAR"), buttonStyle, GUILayout.Height(40)))
            {
                PlayClickSound();
                currentState = MenuState.PlayOptions;
            }
            buttonStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            buttonStyle.hover.textColor = Color.red;

            GUILayout.EndVertical();

            GUILayout.Space(30);

            // --- TARJETA 2: BOSQUE (BLOQUEADO) ---
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
            GUILayout.Space(10);
            
            // Fondo negro con signo de interrogación
            GUIStyle blackBoxStyle = new GUIStyle(GUI.skin.box);
            // Hacer fondo negro opaco
            Texture2D blackTex = new Texture2D(2, 2);
            Color bCol = new Color(0.05f, 0.05f, 0.05f, 0.9f);
            blackTex.SetPixel(0, 0, bCol); blackTex.SetPixel(0, 1, bCol); blackTex.SetPixel(1, 0, bCol); blackTex.SetPixel(1, 1, bCol);
            blackTex.Apply();
            blackBoxStyle.normal.background = blackTex;
            
            GUILayout.BeginVertical(blackBoxStyle, GUILayout.Width(356), GUILayout.Height(200));
            GUIStyle questionStyle = new GUIStyle(labelStyle);
            questionStyle.fontSize = 72;
            questionStyle.fontStyle = FontStyle.Bold;
            questionStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            questionStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("?", questionStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();

            GUILayout.Space(12);
            string lockedTitle1 = "BOSQUE\n(BLOQUEADO)";
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH) lockedTitle1 = "FOREST\n(LOCKED)";
            else if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES) lockedTitle1 = "FLORESTA\n(BLOQUEADO)";

            GUILayout.Label(lockedTitle1, cardLabelStyle, GUILayout.Height(50));
            GUILayout.Space(10);
            
            GUIStyle lockStyle = new GUIStyle(GUI.skin.label);
            lockStyle.fontSize = 24;
            lockStyle.normal.textColor = Color.gray;
            lockStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("🔒 " + lockedLabel, lockStyle, GUILayout.Height(35));

            GUILayout.EndVertical();

            GUILayout.Space(30);

            // --- TARJETA 3: PRISIÓN (BLOQUEADO) ---
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
            GUILayout.Space(10);
            
            GUILayout.BeginVertical(blackBoxStyle, GUILayout.Width(356), GUILayout.Height(200));
            GUILayout.Label("?", questionStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();

            GUILayout.Space(12);
            string lockedTitle2 = "PRISIÓN\n(BLOQUEADO)";
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH) lockedTitle2 = "PRISON\n(LOCKED)";
            else if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES) lockedTitle2 = "PRISÃO\n(BLOQUEADO)";

            GUILayout.Label(lockedTitle2, cardLabelStyle, GUILayout.Height(50));
            GUILayout.Space(10);
            GUILayout.Label("🔒 " + lockedLabel, lockStyle, GUILayout.Height(35));

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(25);

            // Botón Atrás
            if (GUILayout.Button(backBtnText, buttonStyle, GUILayout.Height(45)))
            {
                PlayClickSound();
                currentState = MenuState.Main;
            }
        }
        else if (currentState == MenuState.PlayOptions)
        {
            string playPanelTitle = "AJUSTES DE LA PARTIDA";
            string sizeLabel = "Tamaño de Hospital:";
            string diffLabel = "Dificultad de Supervivencia:";
            string startBtnText = "  [ EMPEZAR JUEGO ]";
            string backBtnText = "  VOLVER AL MENÚ";

            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                if (curLang == LocalizationManager.Idioma.ENGLISH)
                {
                    playPanelTitle = "GAME PARAMETERS";
                    sizeLabel = "Hospital Size:";
                    diffLabel = "Survival Difficulty:";
                    startBtnText = "  [ START GAME ]";
                    backBtnText = "  BACK TO MENU";
                }
                else if (curLang == LocalizationManager.Idioma.PORTUGUES)
                {
                    playPanelTitle = "AJUSTES DA PARTIDA";
                    sizeLabel = "Tamanho do Hospital:";
                    diffLabel = "Dificuldade de Sobrevivência:";
                    startBtnText = "  [ INICIAR JOGO ]";
                    backBtnText = "  VOLTAR AO MENU";
                }
            }

            GUILayout.Label(playPanelTitle, sectionHeaderStyle, GUILayout.Height(30));
            GUILayout.Space(30);

            // Selección de tamaño de mapa
            GUILayout.Label(sizeLabel, labelStyle);
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            
            string[] localizedSizes = { "CHICO", "MEDIANO", "GRANDE" };
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                if (curLang == LocalizationManager.Idioma.ENGLISH) localizedSizes = new string[] { "SMALL", "MEDIUM", "LARGE" };
                else if (curLang == LocalizationManager.Idioma.PORTUGUES) localizedSizes = new string[] { "PEQUENO", "MÉDIO", "GRANDE" };
            }

            for (int i = 0; i < mapSizes.Length; i++)
            {
                bool isSelected = selectedMapSizeIndex == i;
                optionSelectStyle.normal.textColor = isSelected ? Color.red : Color.gray;
                if (GUILayout.Button(localizedSizes[i], optionSelectStyle, GUILayout.Height(40)))
                {
                    PlayClickSound();
                    selectedMapSizeIndex = i;
                }
            }
            GUILayout.EndHorizontal();
            
            string descSize = $"Hospital seleccionado: {mapSizes[selectedMapSizeIndex]}";
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH)
                descSize = $"Selected hospital: {mapSizes[selectedMapSizeIndex].Replace("CHICO", "SMALL").Replace("MEDIANO", "MEDIUM").Replace("GRANDE", "LARGE")}";
            else if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES)
                descSize = $"Hospital selecionado: {mapSizes[selectedMapSizeIndex].Replace("CHICO", "PEQUENO").Replace("MEDIANO", "MÉDIO").Replace("GRANDE", "GRANDE")}";

            GUILayout.Label(descSize, subTitleStyle);
            GUILayout.Space(25);

            // Selección de dificultad
            GUILayout.Label(diffLabel, labelStyle);
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            
            string[] localizedDiffs = { "FÁCIL", "NORMAL", "DIFÍCIL" };
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                if (curLang == LocalizationManager.Idioma.ENGLISH) localizedDiffs = new string[] { "EASY", "NORMAL", "HARD" };
                else if (curLang == LocalizationManager.Idioma.PORTUGUES) localizedDiffs = new string[] { "FÁCIL", "NORMAL", "DIFÍCIL" };
            }

            for (int i = 0; i < difficulties.Length; i++)
            {
                bool isSelected = selectedDifficultyIndex == i;
                optionSelectStyle.normal.textColor = isSelected ? Color.red : Color.gray;
                if (GUILayout.Button(localizedDiffs[i], optionSelectStyle, GUILayout.Height(40)))
                {
                    PlayClickSound();
                    selectedDifficultyIndex = i;
                }
            }
            GUILayout.EndHorizontal();

            string descDiff = "Monstruo agresivo. Velocidad, batería y cordura calibradas.";
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                if (selectedDifficultyIndex == 0) // FÁCIL
                {
                    descDiff = curLang == LocalizationManager.Idioma.ENGLISH 
                        ? "The monster is slower with reduced sight. Flashlight batteries last longer." 
                        : (curLang == LocalizationManager.Idioma.PORTUGUES ? "O monstro é mais lento com visão reduzida. As baterias duram mais." : "El monstruo es lento y tiene menor rango visual. Las baterías duran más tiempo.");
                }
                else if (selectedDifficultyIndex == 1) // NORMAL
                {
                    descDiff = curLang == LocalizationManager.Idioma.ENGLISH 
                        ? "Aggressive monster. Speed, battery, and sanity calibrated for standard play." 
                        : (curLang == LocalizationManager.Idioma.PORTUGUES ? "Monstro agressivo. Velocidade, bateria e sanidade calibradas para a experiência padrão." : "Monstruo agresivo. Velocidad, batería y cordura calibradas para la experiencia estándar.");
                }
                else if (selectedDifficultyIndex == 2) // DIFÍCIL
                {
                    descDiff = curLang == LocalizationManager.Idioma.ENGLISH 
                        ? "The monster is extremely fast and hears noise from far away. Flashlight drains quickly." 
                        : (curLang == LocalizationManager.Idioma.PORTUGUES ? "O monstro é extremamente rápido e ouve ruídos de longe. A lanterna acaba rápido." : "El monstruo es extremadamente rápido y detecta el ruido lejano. La linterna se agota rápido.");
                }
            }
            else
            {
                if (selectedDifficultyIndex == 0) descDiff = "El monstruo es lento y tiene menor rango visual. Las baterías duran más tiempo.";
                else if (selectedDifficultyIndex == 1) descDiff = "Monstruo agresivo. Velocidad, batería y cordura calibradas.";
                else if (selectedDifficultyIndex == 2) descDiff = "El monstruo es extremadamente rápido y detecta el ruido lejano. La linterna se agota rápido.";
            }

            GUILayout.Label(descDiff, subTitleStyle);
            GUILayout.Space(30);

            // Botones de acción final
            buttonStyle.normal.textColor = Color.red;
            buttonStyle.hover.textColor = Color.white;
            if (GUILayout.Button(startBtnText, buttonStyle, GUILayout.Height(60)))
            {
                PlayClickSound();
                int finalWidth = 10;
                if (selectedMapSizeIndex == 1) finalWidth = 12;
                else if (selectedMapSizeIndex == 2) finalWidth = 14;

                string diffStr = "NORMAL";
                if (selectedDifficultyIndex == 0) diffStr = "FACIL";
                else if (selectedDifficultyIndex == 2) diffStr = "DIFICIL";

                PlayerPrefs.SetInt("SelectedMapSize", finalWidth);
                PlayerPrefs.SetString("SelectedDifficulty", diffStr);
                PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.Save();

                MainMenuManager.startedFromMenu = true;
                PlayerPrefs.SetFloat("CamcorderAccumulatedTime", 0f);
                PlayerPrefs.Save();

                SceneLoader.LoadScene("Test_ModularHospital");
            }
            
            GUILayout.Space(12);

            // BOTÓN DE PRUEBAS DE TÚNELES (Dorado/Naranja)
            buttonStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
            buttonStyle.hover.textColor = Color.white;
            string tunnelsBtnText = "  [ IR A LOS TÚNELES (NIVEL 2) ]";
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH)
                tunnelsBtnText = "  [ GO TO TUNNELS (LEVEL 2) ]";
            else if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES)
                tunnelsBtnText = "  [ IR PARA OS TÚNEIS (NÍVEL 2) ]";

            if (GUILayout.Button(tunnelsBtnText, buttonStyle, GUILayout.Height(50)))
            {
                PlayClickSound();
                int finalWidth = 15;
                if (selectedMapSizeIndex == 1) finalWidth = 25;
                else if (selectedMapSizeIndex == 2) finalWidth = 35;

                string diffStr = "NORMAL";
                if (selectedDifficultyIndex == 0) diffStr = "FACIL";
                else if (selectedDifficultyIndex == 2) diffStr = "DIFICIL";

                PlayerPrefs.SetInt("SelectedMapSize", finalWidth);
                PlayerPrefs.SetString("SelectedDifficulty", diffStr);
                PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.Save();

                MainMenuManager.startedFromMenu = true;
                PlayerPrefs.SetFloat("CamcorderAccumulatedTime", 0f);
                PlayerPrefs.Save();

                SceneLoader.LoadScene("TunnelsMap");
            }
            buttonStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            buttonStyle.hover.textColor = Color.red;

            GUILayout.Space(25);

            if (GUILayout.Button(backBtnText, buttonStyle, GUILayout.Height(50)))
            {
                PlayClickSound();
                currentState = MenuState.Main;
            }
        }
        else if (currentState == MenuState.Settings)
        {
            string settingsTitle = "CONFIGURACIÓN DE HARDWARE";
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                settingsTitle = curLang == LocalizationManager.Idioma.ENGLISH ? "HARDWARE SETTINGS" 
                    : (curLang == LocalizationManager.Idioma.PORTUGUES ? "CONFIGURAÇÃO DE HARDWARE" : "CONFIGURACIÓN DE HARDWARE");
            }
            GUILayout.Label(settingsTitle, sectionHeaderStyle, GUILayout.Height(30));
            GUILayout.Space(20);

            // Estilos para pestañas
            GUIStyle tabButtonStyle = new GUIStyle(GUI.skin.button);
            tabButtonStyle.fontSize = 16;
            tabButtonStyle.fontStyle = FontStyle.Bold;
            tabButtonStyle.normal.textColor = Color.gray;
            tabButtonStyle.hover.textColor = Color.red;

            GUILayout.BeginHorizontal();
            
            // Pestaña Audio & Sensibilidad
            tabButtonStyle.normal.textColor = activeSettingsTab == 0 ? Color.red : Color.gray;
            string tabAudioText = "AUDIO Y CONTROLES";
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                tabAudioText = curLang == LocalizationManager.Idioma.ENGLISH ? "AUDIO & CONTROLS" 
                    : (curLang == LocalizationManager.Idioma.PORTUGUES ? "ÁUDIO E CONTROLES" : "AUDIO Y CONTROLES");
            }
            if (GUILayout.Button(tabAudioText, tabButtonStyle, GUILayout.Height(40)))
            {
                PlayClickSound();
                activeSettingsTab = 0;
            }

            // Pestaña Gráficos y Rendimiento
            tabButtonStyle.normal.textColor = activeSettingsTab == 1 ? Color.red : Color.gray;
            string tabGraphicsText = "GRÁFICOS Y RENDIMIENTO";
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                tabGraphicsText = curLang == LocalizationManager.Idioma.ENGLISH ? "GRAPHICS & RUNTIME" 
                    : (curLang == LocalizationManager.Idioma.PORTUGUES ? "GRÁFICOS E VIDEO" : "GRÁFICOS Y RENDIMIENTO");
            }
            if (GUILayout.Button(tabGraphicsText, tabButtonStyle, GUILayout.Height(40)))
            {
                PlayClickSound();
                activeSettingsTab = 1;
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(30);

            if (activeSettingsTab == 0)
            {
                // Control de volumen
                string volLabel = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_volumen") : "Volumen General";
                GUILayout.Label($"{volLabel}: {Mathf.RoundToInt(masterVolume * 100)}%", labelStyle);
                masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);
                AudioListener.volume = masterVolume;
                if (menuAudioSource != null) menuAudioSource.volume = masterVolume * 0.6f;
                if (sfxAudioSource != null) sfxAudioSource.volume = masterVolume * 0.85f;
                GUILayout.Space(25);

                // Control de sensibilidad
                string sensLabel = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_sensibilidad") : "Sensibilidad de Cámara";
                GUILayout.Label($"{sensLabel}: {mouseSensitivity:F1}", labelStyle);
                mouseSensitivity = GUILayout.HorizontalSlider(mouseSensitivity, 0.5f, 6.0f);
                GUILayout.Space(25);

                // Selector de Idioma
                string langLabel = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_idioma") : "Idioma";
                GUILayout.Label($"{langLabel}:", labelStyle);
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                if (LocalizationManager.Instance != null)
                {
                    var currentLang = LocalizationManager.Instance.GetIdiomaActual();
                    
                    optionSelectStyle.normal.textColor = currentLang == LocalizationManager.Idioma.ESPAÑOL ? Color.red : Color.gray;
                    if (GUILayout.Button("ESPAÑOL", optionSelectStyle, GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        LocalizationManager.Instance.CambiarIdioma(LocalizationManager.Idioma.ESPAÑOL);
                    }

                    optionSelectStyle.normal.textColor = currentLang == LocalizationManager.Idioma.ENGLISH ? Color.red : Color.gray;
                    if (GUILayout.Button("ENGLISH", optionSelectStyle, GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        LocalizationManager.Instance.CambiarIdioma(LocalizationManager.Idioma.ENGLISH);
                    }

                    optionSelectStyle.normal.textColor = currentLang == LocalizationManager.Idioma.PORTUGUES ? Color.red : Color.gray;
                    if (GUILayout.Button("PORTUGUÊS", optionSelectStyle, GUILayout.Height(35)))
                    {
                        PlayClickSound();
                        LocalizationManager.Instance.CambiarIdioma(LocalizationManager.Idioma.PORTUGUES);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(25);

                // Pantalla completa
                GUILayout.BeginHorizontal();
                string fsLabel = "Pantalla Completa";
                if (LocalizationManager.Instance != null)
                {
                    fsLabel = LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Full Screen" 
                        : (LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES ? "Tela Cheia" : "Pantalla Completa");
                }
                GUILayout.Label(fsLabel, labelStyle, GUILayout.Width(200));
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
                string qualLabelText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("menu_graficos") : "Calidad de Gráficos:";
                GUILayout.Label(qualLabelText, labelStyle);
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                
                string[] qualityLevels = { "BAJO", "MEDIO", "ALTO" };
                if (LocalizationManager.Instance != null)
                {
                    var curLang = LocalizationManager.Instance.GetIdiomaActual();
                    if (curLang == LocalizationManager.Idioma.ENGLISH) qualityLevels = new string[] { "LOW", "MEDIUM", "HIGH" };
                    else if (curLang == LocalizationManager.Idioma.PORTUGUES) qualityLevels = new string[] { "BAIXO", "MÉDIO", "ALTO" };
                }

                for (int i = 0; i < qualityLevels.Length; i++)
                {
                    bool isSelected = selectedQualityIndex == i;
                    optionSelectStyle.normal.textColor = isSelected ? Color.red : Color.gray;
                    if (GUILayout.Button(qualityLevels[i], optionSelectStyle, GUILayout.Height(40)))
                    {
                        PlayClickSound();
                        selectedQualityIndex = i;
                        QualitySettings.SetQualityLevel(i, true);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(35);

                // RESOLUCIÓN (PC/Móvil)
                string resLabelText = "Resolución de Pantalla:";
                if (LocalizationManager.Instance != null)
                {
                    var curLang = LocalizationManager.Instance.GetIdiomaActual();
                    resLabelText = curLang == LocalizationManager.Idioma.ENGLISH ? "Screen Resolution:" 
                        : (curLang == LocalizationManager.Idioma.PORTUGUES ? "Resolução de Tela:" : "Resolución de Pantalla:");
                }
                GUILayout.Label(resLabelText, labelStyle);
                GUILayout.Space(5);

                #if UNITY_ANDROID || UNITY_IOS
                GUIStyle centeredLabelStyle = new GUIStyle(labelStyle);
                centeredLabelStyle.normal.textColor = Color.gray;
                string nativeText = LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Device Native" : "Nativa del Dispositivo";
                GUILayout.Label($"{Screen.currentResolution.width}x{Screen.currentResolution.height} ({nativeText})", centeredLabelStyle, GUILayout.Height(40));
                #else
                if (pcResolutions != null && pcResolutions.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUIStyle cycleButtonStyle = new GUIStyle(GUI.skin.button);
                    cycleButtonStyle.fontSize = 20;
                    cycleButtonStyle.fontStyle = FontStyle.Bold;

                    if (GUILayout.Button("<", cycleButtonStyle, GUILayout.Width(50), GUILayout.Height(40)))
                    {
                        PlayClickSound();
                        selectedResIndex = (selectedResIndex - 1 + pcResolutions.Count) % pcResolutions.Count;
                        Resolution targetRes = pcResolutions[selectedResIndex];
                        Screen.SetResolution(targetRes.width, targetRes.height, isFullscreen);
                    }

                    GUIStyle resLabelStyle = new GUIStyle(labelStyle);
                    resLabelStyle.alignment = TextAnchor.MiddleCenter;
                    GUILayout.Label($"{pcResolutions[selectedResIndex].width} x {pcResolutions[selectedResIndex].height}", resLabelStyle, GUILayout.Height(40));

                    if (GUILayout.Button(">", cycleButtonStyle, GUILayout.Width(50), GUILayout.Height(40)))
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
            }

            GUILayout.Space(45);

            // Guardar y Volver
            string saveBtnText = "  GUARDAR Y VOLVER";
            if (LocalizationManager.Instance != null)
            {
                var curLang = LocalizationManager.Instance.GetIdiomaActual();
                saveBtnText = curLang == LocalizationManager.Idioma.ENGLISH ? "  SAVE & BACK" 
                    : (curLang == LocalizationManager.Idioma.PORTUGUES ? "  SALVAR E VOLTAR" : "  GUARDAR Y VOLVER");
            }

            if (GUILayout.Button(saveBtnText, buttonStyle, GUILayout.Height(55)))
            {
                PlayClickSound();
                PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.SetInt("QualityLevel", selectedQualityIndex);
                PlayerPrefs.Save();
                currentState = MenuState.Main;
            }
        }

        GUILayout.EndArea();

        // DIBUJAR BOTONES DE REDES SOCIALES EN EL LATERAL DERECHO (Estilo Specimen Zero)
        if (currentState == MenuState.Main || currentState == MenuState.Settings || currentState == MenuState.PlayOptions)
        {
            float socialX = 1810f; // Extremo derecho de la pantalla virtual 1920x1080
            float startY = 400f;   // Altura inicial
            float btnSize = 80f;   // Tamaño
            float spacing = 20f;   // Espacio

            // Instagram
            if (texInstagram != null)
            {
                Rect rectInsta = new Rect(socialX, startY, btnSize, btnSize);
                if (GUI.Button(rectInsta, texInstagram, GUIStyle.none))
                {
                    PlayClickSound();
                    Application.OpenURL(instagramURL);
                }
            }

            // Facebook
            if (texFacebook != null)
            {
                Rect rectFB = new Rect(socialX, startY + btnSize + spacing, btnSize, btnSize);
                if (GUI.Button(rectFB, texFacebook, GUIStyle.none))
                {
                    PlayClickSound();
                    Application.OpenURL(facebookURL);
                }
            }

            // YouTube
            if (texYoutube != null)
            {
                Rect rectYT = new Rect(socialX, startY + (btnSize + spacing) * 2f, btnSize, btnSize);
                if (GUI.Button(rectYT, texYoutube, GUIStyle.none))
                {
                    PlayClickSound();
                    Application.OpenURL(youtubeURL);
                }
            }
        }

        // Restaurar la matriz original del GUI
        GUI.matrix = svMat;
    }
}
