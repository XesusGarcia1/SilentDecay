using UnityEngine;
using System.Collections;

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
        }
        else
        {
            Destroy(gameObject);
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
            subtitleStyle.fontSize = 20;
            subtitleStyle.fontStyle = FontStyle.Italic;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Blanco crudo desgastado
            subtitleStyle.wordWrap = true;
        }

        // Posicionamiento de la barra de subtítulo abajo de la pantalla
        float width = Mathf.Min(800f, Screen.width - 60f);
        float height = 75f;
        float x = Screen.width / 2f - width / 2f;
        float y = Screen.height - 130f; // Justo por encima de los joysticks táctiles del móvil

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
