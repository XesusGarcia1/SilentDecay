using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameEndingManager : MonoBehaviour
{
    public static GameEndingManager Instance { get; private set; }
    public static bool isEndingTriggered = false;

    private float fadeAlpha = 0f;
    private bool isFading = false;
    private string currentEndingMessage = "";
    private string endingTitle = "";
    private bool showFinalTitle = false;

    // -------------------------------------------------------
    // RESET COMPLETO al iniciar cada sesión de juego
    // -------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isEndingTriggered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindObjectOfType<GameEndingManager>() == null)
        {
            GameObject go = new GameObject("[GameEndingManager]");
            go.AddComponent<GameEndingManager>();
            DontDestroyOnLoad(go);
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
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Limpia el estado completo al cargar cualquier escena nueva (incluyendo si el jugador vuelve a jugar)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        isEndingTriggered = false;
        isFading = false;
        fadeAlpha = 0f;
        currentEndingMessage = "";
        endingTitle = "";
        showFinalTitle = false;

        // Garantizar que timeScale y AudioListener estén restaurados según la preferencia del usuario
        Time.timeScale = 1f;
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
    }

    public static void TriggerEnding(Transform doorTransform)
    {
        if (isEndingTriggered) return;
        isEndingTriggered = true;

        if (Instance != null)
        {
            Instance.StartCoroutine(Instance.EndingRoutine(doorTransform));
        }
    }

    private IEnumerator EndingRoutine(Transform doorTransform)
    {
        // --- PASO 0: Restaurar timeScale inmediatamente (por si PauseMenu lo congeló)
        Time.timeScale = 1f;

        // --- PASO 1: Bloquear controles del jugador
        MobileInput.SetCursorState(false);

        // Desactivar el PauseMenuManager para que no interfiera
        var pause = FindObjectOfType<PauseMenuManager>();
        if (pause != null) pause.enabled = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("NestedParent_Unpack");

        Camera cam = Camera.main;
        if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();

        if (player != null)
        {
            var fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
            if (fpc != null) fpc.enabled = false;

            var cc = player.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        // Detener al monstruo
        var replicaAI = FindObjectOfType<ReplicaAIController>();
        if (replicaAI != null) replicaAI.enabled = false;

        // --- PASO 2: Cinemática – cámara avanza saliendo por la puerta
        isFading = true;
        if (cam != null)
        {
            cam.transform.SetParent(null); // Desacoplar cámara del jugador

            Vector3 startPos = cam.transform.position;
            Vector3 exitDir = (doorTransform != null)
                ? -doorTransform.forward
                : cam.transform.forward;

            Vector3 targetPos = startPos + exitDir * 2.8f + Vector3.up * 0.05f;
            Quaternion startRot = cam.transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(exitDir);

            float elapsed = 0f;
            float moveDuration = 2.8f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime; // Usa unscaled para que no dependa de timeScale
                float t = Mathf.Clamp01(elapsed / moveDuration);

                cam.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
                cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                fadeAlpha = Mathf.Clamp01(t * 1.2f);

                yield return null;
            }
        }

        fadeAlpha = 1.0f;

        // --- PASO 3: Mensajes finales en pantalla negra (todos con WaitForSecondsRealtime)
        currentEndingMessage = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_msg1") : "Logré salir... La luz del exterior por fin ciega esta pesadilla.";
        yield return new WaitForSecondsRealtime(3.2f);

        currentEndingMessage = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_msg2") : "Pero sé que en la oscuridad de esa fábrica...";
        yield return new WaitForSecondsRealtime(2.5f);

        currentEndingMessage = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_msg3") : "La Réplica seguirá esperando.";
        yield return new WaitForSecondsRealtime(3.0f);

        currentEndingMessage = "";
        endingTitle = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_title") : "¡HAS ESCAPADO DE LA RÉPLICA!";
        showFinalTitle = true;

        yield return new WaitForSecondsRealtime(4.0f);

        // --- PASO 4: Fundir hacia negro total antes de salir al menú
        float exitFade = 0f;
        while (exitFade < 1f)
        {
            exitFade += Time.unscaledDeltaTime * 0.8f;
            fadeAlpha = Mathf.Clamp01(1f + exitFade * 0.2f); // Ya estaba en 1f, esto lo mantiene
            yield return null;
        }

        // --- PASO 5: Volver al Menú Principal limpiamente
        Time.timeScale = 1f;
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        SceneManager.LoadScene("MainMenu");
    }

    private void OnGUI()
    {
        if (!isEndingTriggered && !isFading) return;

        GUI.depth = -200;

        // Fondo negro de fundido
        if (fadeAlpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeAlpha));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Mensajes narrativos (solo cuando la pantalla ya es negra)
        if (fadeAlpha >= 0.9f)
        {
            if (!string.IsNullOrEmpty(currentEndingMessage))
            {
                GUIStyle msgStyle = new GUIStyle(GUI.skin.label);
                msgStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.045f), 24, 70);
                msgStyle.fontStyle = FontStyle.Italic;
                msgStyle.alignment = TextAnchor.MiddleCenter;
                msgStyle.wordWrap = true;
                msgStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f, 0.95f);

                float w = Screen.width * 0.85f;
                float h = 150f;
                GUI.Label(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h),
                    currentEndingMessage, msgStyle);
            }

            if (showFinalTitle)
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.08f), 36, 110);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.normal.textColor = new Color(0.95f, 0.82f, 0.25f);

                GUIStyle subStyle = new GUIStyle(GUI.skin.label);
                subStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.04f), 22, 50);
                subStyle.fontStyle = FontStyle.Italic;
                subStyle.alignment = TextAnchor.MiddleCenter;
                subStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

                float w = Screen.width * 0.95f;
                GUI.Label(new Rect((Screen.width - w) / 2f, Screen.height / 2f - 90f, w, 90f),
                    endingTitle, titleStyle);
                string subtitle = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_subtitle") : "Sobreviviste al Depósito Industrial";
                GUI.Label(new Rect((Screen.width - w) / 2f, Screen.height / 2f + 10f, w, 50f),
                    subtitle, subStyle);

                // Indicación al jugador de que el juego está cargando el menú
                GUIStyle loadStyle = new GUIStyle(GUI.skin.label);
                loadStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.025f), 16, 28);
                loadStyle.alignment = TextAnchor.MiddleCenter;
                loadStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                string loadText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("end_depot_loading") : "Volviendo al Menú Principal...";
                GUI.Label(new Rect((Screen.width - w) / 2f, Screen.height - 100f, w, 40f),
                    loadText, loadStyle);
            }
        }
    }
}
