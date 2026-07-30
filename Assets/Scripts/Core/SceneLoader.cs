using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static string SceneToLoad = "SampleScene"; // Escena de destino por defecto (hospital procedural)
    private string currentTip = "";
    private float loadProgress = 0f;
    private bool canStartTransition = false;
    private float minLoadDuration = 6.0f; // Tiempo de espera mínimo de 6 segundos para leer el consejo

    private void Start()
    {
        // Forzar cursor liberado para la pantalla de carga si fuera necesario
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

        // Iniciar la carga asíncrona en segundo plano
        StartCoroutine(LoadSceneAsyncCoroutine());
    }

    private IEnumerator LoadSceneAsyncCoroutine()
    {
        yield return null;

        // Crear una carga asíncrona de fondo
        AsyncOperation operation = SceneManager.LoadSceneAsync(SceneToLoad);

        // Protección: si la escena no existe en el Build Profile, abortar con mensaje claro
        if (operation == null)
        {
            Debug.LogError($"SceneLoader: No se pudo cargar '{SceneToLoad}'. Verifica que esté añadida en File > Build Profiles.");
            yield break;
        }

        operation.allowSceneActivation = false; // Evitar que la escena se active inmediatamente al cargar

        float timer = 0f;

        while (timer < minLoadDuration || operation.progress < 0.9f)
        {
            timer += Time.unscaledDeltaTime;
            
            // Simular y suavizar el porcentaje de progreso de carga en la UI
            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(timer / minLoadDuration);
            loadProgress = Mathf.Min(realProgress, timeProgress);

            yield return null;
        }

        loadProgress = 1.0f;
        canStartTransition = true;

        // Breve pausa al llegar al 100% para una transición pulida
        yield return new WaitForSecondsRealtime(0.5f);

        // Activar la escena cargada
        operation.allowSceneActivation = true;
    }

    /// <summary>
    /// Método de utilidad global para llamar la carga de escenas desde cualquier script
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        SceneToLoad = sceneName;
        // Cargamos la escena de carga procedural
        SceneManager.LoadScene("LoadingScene");
    }

    private void OnGUI()
    {
        // 1. Dibujar fondo negro sólido completo
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

        // Dibujar caja contenedora del consejo
        float tipWidth = Mathf.Min(700, Screen.width - 100);
        Rect tipRect = new Rect(Screen.width / 2 - tipWidth / 2, Screen.height / 2 - 80, tipWidth, 100);
        
        // Sombra estética
        tipStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(tipRect.x + 2, tipRect.y + 2, tipRect.width, tipRect.height), currentTip, tipStyle);
        // Texto principal
        tipStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUI.Label(tipRect, currentTip, tipStyle);

        // 3. Estilo para el título de consejos
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.7f, 0f, 0f); // Rojo sangre
        
        string titleText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("load_titulo") : "CONSEJO DE SUPERVIVENCIA";
        GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 120, 300, 25), titleText, titleStyle);

        // 4. Dibujar barra de carga dinámica abajo
        float barWidth = Mathf.Min(400, Screen.width - 100);
        float barHeight = 6f;
        Rect barBgRect = new Rect(Screen.width / 2 - barWidth / 2, Screen.height - 120, barWidth, barHeight);

        // Fondo de la barra (Rojo oscuro)
        GUI.color = new Color(0.15f, 0.02f, 0.02f, 1f);
        GUI.DrawTexture(barBgRect, Texture2D.whiteTexture);

        // Relleno de la barra (Rojo brillante / Emisivo)
        GUI.color = new Color(0.85f, 0.1f, 0.1f, 1f);
        Rect barFillRect = new Rect(barBgRect.x, barBgRect.y, barWidth * loadProgress, barHeight);
        GUI.DrawTexture(barFillRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 5. Estado de carga ("CARGANDO...")
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
