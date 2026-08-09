using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static string SceneToLoad = "Test_ModularHospital"; // Escena de destino por defecto
    private string currentTip = "";
    private float loadProgress = 0f;
    private bool canStartTransition = false;
    private float minLoadDuration = 6.0f;

    // === INTRO VHS ===
    private enum LoaderPhase { Intro, Loading }
    private LoaderPhase currentPhase = LoaderPhase.Intro;
    private string introText = "";
    private string currentDisplayedText = "";
    private bool introTextComplete = false;
    private bool hasIntro = false;
    private float vhsNoiseTimer = 0f;
    private GUIStyle terminalStyle;
    private GUIStyle promptStyle;
    private Texture2D blackTex;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Preparar textura negra
        blackTex = new Texture2D(2, 2);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                blackTex.SetPixel(x, y, Color.black);
        blackTex.Apply();

        // Intentar obtener texto de intro para la escena destino
        introText = LevelIntroData.GetIntroText(SceneToLoad);
        hasIntro = !string.IsNullOrEmpty(introText);

        if (hasIntro)
        {
            // Mostrar la intro VHS primero — NO cargar el mapa todavía
            currentPhase = LoaderPhase.Intro;
            currentDisplayedText = "";
            introTextComplete = false;
            StartCoroutine(TypewriterRoutine());
        }
        else
        {
            // Sin intro, ir directo a la carga normal
            StartLoadingPhase();
        }
    }

    private void StartLoadingPhase()
    {
        currentPhase = LoaderPhase.Loading;

        // Seleccionar tip aleatorio localizado
        if (LocalizationManager.Instance != null)
        {
            string tipKey = "tip_" + Random.Range(1, 9);
            currentTip = LocalizationManager.Instance.Get(tipKey);
        }
        else
        {
            currentTip = "Cargando...";
        }

        // Iniciar la carga asíncrona del mapa real
        StartCoroutine(LoadSceneAsyncCoroutine());
    }

    // === TYPEWRITER DE LA INTRO VHS ===
    private IEnumerator TypewriterRoutine()
    {
        // Cargar el clip de sonido de tecla una sola vez
        AudioClip typeClip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");

        float speed = 0.035f;
        for (int i = 0; i < introText.Length; i++)
        {
            currentDisplayedText += introText[i];

            // Sonido mecánico de tecla cada 3 caracteres
            if (i % 3 == 0 && typeClip != null)
            {
                AudioSource.PlayClipAtPoint(typeClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.08f);
            }

            yield return new WaitForSecondsRealtime(speed);
        }
        introTextComplete = true;
    }

    // === CARGA ASÍNCRONA DEL MAPA (FASE 2) ===
    private IEnumerator LoadSceneAsyncCoroutine()
    {
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(SceneToLoad);

        if (operation == null)
        {
            Debug.LogError($"SceneLoader: No se pudo cargar '{SceneToLoad}'. Verifica que esté añadida en File > Build Profiles.");
            yield break;
        }

        operation.allowSceneActivation = false;

        float timer = 0f;

        while (timer < minLoadDuration || operation.progress < 0.9f)
        {
            timer += Time.unscaledDeltaTime;
            
            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(timer / minLoadDuration);
            loadProgress = Mathf.Min(realProgress, timeProgress);

            yield return null;
        }

        loadProgress = 1.0f;
        canStartTransition = true;

        yield return new WaitForSecondsRealtime(0.5f);

        operation.allowSceneActivation = true;
    }

    /// <summary>
    /// Método de utilidad global para llamar la carga de escenas desde cualquier script
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        SceneToLoad = sceneName;
        SceneManager.LoadScene("LoadingScene");
    }

    private void Update()
    {
        if (currentPhase == LoaderPhase.Intro)
        {
            vhsNoiseTimer += Time.unscaledDeltaTime;

            // Detectar pulsación de tecla o toque táctil
            if (Input.anyKeyDown || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                if (introTextComplete)
                {
                    // El texto ya se completó → avanzar a la fase de carga del mapa
                    StopAllCoroutines();
                    StartLoadingPhase();
                }
                else
                {
                    // Todavía escribiendo → autocompletar el texto
                    StopAllCoroutines();
                    currentDisplayedText = introText;
                    introTextComplete = true;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (currentPhase == LoaderPhase.Intro)
        {
            DrawIntroScreen();
        }
        else
        {
            DrawLoadingScreen();
        }
    }

    // ==================== PANTALLA DE INTRO VHS ====================
    private void DrawIntroScreen()
    {
        // Fondo negro completo
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        // Estilos de terminal retro
        if (terminalStyle == null)
        {
            terminalStyle = new GUIStyle();
            terminalStyle.fontSize = 20;
            terminalStyle.normal.textColor = new Color(0.1f, 0.85f, 0.15f, 0.95f); // Verde fósforo
            terminalStyle.wordWrap = true;
            terminalStyle.alignment = TextAnchor.UpperCenter;

            promptStyle = new GUIStyle(terminalStyle);
            promptStyle.fontSize = 17;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        }

        // Contenedor centrado
        int w = Mathf.Min(800, Screen.width - 40);
        int h = 480;
        Rect container = new Rect(Screen.width / 2 - w / 2, Screen.height / 2 - h / 2, w, h);

        GUILayout.BeginArea(container);
        GUILayout.Label(currentDisplayedText, terminalStyle, GUILayout.Height(380));
        GUILayout.Space(20);

        if (introTextComplete)
        {
            float promptAlpha = Mathf.PingPong(Time.unscaledTime * 2.2f, 1.0f);
            promptStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, promptAlpha);
            GUILayout.Label("[ PULSA CUALQUIER BOTÓN PARA REPRODUCIR CINTA ]", promptStyle);
        }
        else
        {
            promptStyle.normal.textColor = Color.gray;
            GUILayout.Label("[ OPRIMIR CUALQUIER BOTÓN PARA OMITIR ]", promptStyle);
        }

        GUILayout.EndArea();

        // Efecto de Scanlines CRT suaves dibujados POR ENCIMA del texto
        // Esto da la sensación de VHS/CRT sin generar destellos de epilepsia.
        GUI.color = new Color(0f, 0.05f, 0f, 0.45f); // Tinte negro-verdoso oscuro
        float offset = (Time.unscaledTime * 40f) % 6f; 
        for (float y = offset; y < Screen.height; y += 6f)
        {
            GUI.DrawTexture(new Rect(0, y, Screen.width, 2f), Texture2D.whiteTexture); 
        }
        GUI.color = Color.white;
    }

    // ==================== PANTALLA DE CARGA NORMAL ====================
    private void DrawLoadingScreen()
    {
        // 1. Fondo negro sólido completo
        GUI.color = new Color(0.04f, 0.04f, 0.05f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. Estilo de los consejos
        GUIStyle tipStyle = new GUIStyle();
        tipStyle.fontSize = 20;
        tipStyle.alignment = TextAnchor.MiddleCenter;
        tipStyle.fontStyle = FontStyle.Italic;
        tipStyle.wordWrap = true;
        tipStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        float tipWidth = Mathf.Min(700, Screen.width - 100);
        Rect tipRect = new Rect(Screen.width / 2 - tipWidth / 2, Screen.height / 2 - 80, tipWidth, 100);
        
        // Sombra estética
        tipStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(tipRect.x + 2, tipRect.y + 2, tipRect.width, tipRect.height), currentTip, tipStyle);
        // Texto principal
        tipStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUI.Label(tipRect, currentTip, tipStyle);

        // 3. Título de consejos
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.7f, 0f, 0f);
        
        string titleText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("load_titulo") : "CONSEJO DE SUPERVIVENCIA";
        GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 120, 300, 25), titleText, titleStyle);

        // 4. Barra de carga
        float barWidth = Mathf.Min(400, Screen.width - 100);
        float barHeight = 6f;
        Rect barBgRect = new Rect(Screen.width / 2 - barWidth / 2, Screen.height - 120, barWidth, barHeight);

        GUI.color = new Color(0.15f, 0.02f, 0.02f, 1f);
        GUI.DrawTexture(barBgRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.85f, 0.1f, 0.1f, 1f);
        Rect barFillRect = new Rect(barBgRect.x, barBgRect.y, barWidth * loadProgress, barHeight);
        GUI.DrawTexture(barFillRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 5. Estado de carga
        GUIStyle loadStyle = new GUIStyle();
        loadStyle.fontSize = 13;
        loadStyle.alignment = TextAnchor.MiddleCenter;
        loadStyle.fontStyle = FontStyle.Bold;
        loadStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        
        string statusText = "";
        if (LocalizationManager.Instance != null)
        {
            statusText = canStartTransition 
                ? LocalizationManager.Instance.Get("load_status_iniciando") 
                : $"{LocalizationManager.Instance.Get("load_status_cargando")} {Mathf.RoundToInt(loadProgress * 100)}%";
        }
        else
        {
            statusText = canStartTransition ? "INICIANDO ENCUENTRO..." : $"CARGANDO MAPA PROCEDURAL... {Mathf.RoundToInt(loadProgress * 100)}%";
        }
        GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height - 150, 400, 20), statusText.ToUpper(), loadStyle);
    }
}
