using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerMonologueManager : MonoBehaviour
{
    private static PlayerMonologueManager instance;
    public static PlayerMonologueManager Instance => instance;

    private string activeText = "";
    private float displayTimer = 0f;
    private GUIStyle subtitleStyle;
    private Texture2D backgroundBarTex;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Limpiar diálogos activos al volver al menú o entrar a la pantalla de carga
        if (scene.name == "MainMenu" || scene.name == "LoadingScene")
        {
            activeText = "";
            displayTimer = 0f;
        }
    }

    private void Start()
    {
        // Crear fondo translúcido negro para los subtítulos
        backgroundBarTex = new Texture2D(2, 2);
        Color c = new Color(0f, 0f, 0f, 0.65f);
        backgroundBarTex.SetPixel(0, 0, c); backgroundBarTex.SetPixel(0, 1, c);
        backgroundBarTex.SetPixel(1, 0, c); backgroundBarTex.SetPixel(1, 1, c);
        backgroundBarTex.Apply();
    }

    public static void ShowDialogue(string text, float duration = 4.0f)
    {
        if (Instance == null)
        {
            // Auto-instanciar si no existe en escena
            GameObject go = new GameObject("[PlayerMonologueManager]");
            go.AddComponent<PlayerMonologueManager>();
        }

        if (Instance != null)
        {
            Instance.activeText = text;
            Instance.displayTimer = duration;
        }
    }

    public static void HideDialogue()
    {
        if (Instance != null)
        {
            Instance.activeText = "";
            Instance.displayTimer = 0f;
        }
    }

    private void Update()
    {
        if (displayTimer > 0f)
        {
            displayTimer -= Time.deltaTime;
            if (displayTimer <= 0f)
            {
                activeText = "";
            }
        }
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(activeText) || Time.timeScale == 0f) return;

        // Estilos
        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle();
            subtitleStyle.fontStyle = FontStyle.Italic;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Blanco crudo desgastado
            subtitleStyle.wordWrap = true;
        }

        // Auto escalar en base a resolución para asegurar legibilidad en PC y Móvil
        subtitleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.045f), 24, 60);

        // Posicionamiento de la barra de subtítulo
        float width = Mathf.Min(1200f, Screen.width * 0.9f);
        float height = subtitleStyle.fontSize * 3.5f;
        float x = Screen.width / 2f - width / 2f;
        float y = Screen.height - 210f; // Posicionado limpiamente por encima de las alertas de interacción

        Rect barRect = new Rect(x, y, width, height);

        // Dibujar fondo oscuro
        GUI.color = Color.white;
        GUI.DrawTexture(barRect, backgroundBarTex);

        // Dibujar texto con sombra negra sutil para legibilidad
        GUIStyle shadowStyle = new GUIStyle(subtitleStyle);
        shadowStyle.normal.textColor = Color.black;

        // Sombra
        GUI.Label(new Rect(barRect.x + 2, barRect.y + 2, barRect.width, barRect.height), activeText, shadowStyle);
        
        // Frente
        GUI.Label(barRect, activeText, subtitleStyle);
    }
}
