using UnityEngine;

public class CamcorderOverlay : MonoBehaviour
{
    [Header("Ajustes del HUD de Camara")]
    public Color hudColor = new Color(1f, 1f, 1f, 0.7f);
    public Color recColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    
    [Header("Estilo de Texto")]
    public int fontSize = 20;

    private FlashlightController flashlight;
    private Texture2D whiteTex;
    private float totalAccumulatedTime = 0f;

    void Start()
    {
        // Cargar el tiempo de grabación acumulado de niveles anteriores
        totalAccumulatedTime = PlayerPrefs.GetFloat("CamcorderAccumulatedTime", 0f);

        // Buscar la linterna en el jugador para leer su bateria si la usa
        flashlight = FindObjectOfType<FlashlightController>();

        // Crear una textura blanca de 1x1 para dibujar las lineas de los brackets
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    void OnGUI()
    {
        // Ocultar si estamos en modo menú
        ModularHospital.ModularHospitalGenerator generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (generator != null && generator.isMenuMode) return;

        if (whiteTex == null) return;

        // 1. DIBUJAR LOS CORNER BRACKETS (Angulos de la camara)
        DrawCornerBrackets();

        // 2. DIBUJAR EL INDICADOR "REC" (Parpadeante en la esquina superior izquierda)
        DrawRecIndicator();

        // 3. DIBUJAR EL CONTADOR DE TIEMPO Y LA FECHA (Esquina inferior izquierda)
        DrawTimerAndDate();

        // 4. DIBUJAR EL ESTADO DE BATERIA (Esquina inferior derecha)
        DrawBatteryIndicator();
    }

    private void DrawCornerBrackets()
    {
        float offset = 35f;
        float length = 30f;
        float thick = 3f;

        GUI.color = hudColor;

        // Top-Left
        GUI.DrawTexture(new Rect(offset, offset, length, thick), whiteTex);
        GUI.DrawTexture(new Rect(offset, offset, thick, length), whiteTex);

        // Top-Right
        GUI.DrawTexture(new Rect(Screen.width - offset - length, offset, length, thick), whiteTex);
        GUI.DrawTexture(new Rect(Screen.width - offset - thick, offset, thick, length), whiteTex);

        // Bottom-Left
        GUI.DrawTexture(new Rect(offset, Screen.height - offset - thick, length, thick), whiteTex);
        GUI.DrawTexture(new Rect(offset, Screen.height - offset - length, thick, length), whiteTex);

        // Bottom-Right
        GUI.DrawTexture(new Rect(Screen.width - offset - length, Screen.height - offset - thick, length, thick), whiteTex);
        GUI.DrawTexture(new Rect(Screen.width - offset - thick, Screen.height - offset - length, thick, length), whiteTex);

        GUI.color = Color.white;
    }

    private void DrawRecIndicator()
    {
        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = fontSize;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.white;

        // Parpadeo cada 1 segundo
        bool showDot = (int)(Time.time * 2f) % 2 == 0;

        float startX = 55f;
        float startY = 55f;

        if (showDot)
        {
            GUIStyle dotStyle = new GUIStyle(textStyle);
            dotStyle.normal.textColor = recColor;
            GUI.Label(new Rect(startX, startY, 30, 30), "●", dotStyle); // Circulo unicode
        }

        // Texto REC
        GUI.Label(new Rect(startX + 22, startY, 100, 30), "REC", textStyle);
    }

    private void DrawTimerAndDate()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        // Formatear tiempo transcurrido total acumulado (HH:MM:SS)
        int t = (int)(totalAccumulatedTime + Time.timeSinceLevelLoad);
        string timeStr = string.Format("{0:00}:{1:00}:{2:00}", t / 3600, (t / 60) % 60, t % 60);
        
        // Usar una fecha fija retro clasica
        string dateStr = "OCT.24 1997";

        float startX = 55f;
        float startY = Screen.height - 110f;

        // Dibujar texto con sombra negra
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(startX + 1, startY + 1, 300, 100), "PLAY\n" + timeStr + "\n" + dateStr, style);

        style.normal.textColor = Color.white;
        GUI.Label(new Rect(startX, startY, 300, 100), "PLAY\n" + timeStr + "\n" + dateStr, style);
    }

    void OnDestroy()
    {
        // Guardar el tiempo transcurrido acumulado al destruir la cámara (cambiar de nivel/escena)
        PlayerPrefs.SetFloat("CamcorderAccumulatedTime", totalAccumulatedTime + Time.timeSinceLevelLoad);
        PlayerPrefs.Save();
    }

    private void DrawBatteryIndicator()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize - 4;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleRight;

        // Posicionado con margen perfecto entre el botón táctil de sprint y el marco L de la cámara
        float startX = Screen.width - 152f;
        float startY = Screen.height - 52f;

        // Obtener porcentaje de bateria
        float batPct = 1f;
        if (flashlight != null && flashlight.useBattery)
        {
            batPct = flashlight.currentBattery / flashlight.maxBattery;
        }
        else
        {
            // Bateria decorativa de la camara que disminuye muy lentamente (1% cada 2 minutos)
            float dec = (Time.timeSinceLevelLoad / 120f) * 0.01f;
            batPct = Mathf.Clamp(0.92f - dec, 0.05f, 1.0f);
        }

        int pctInt = Mathf.RoundToInt(batPct * 100f);

        // Texto de porcentaje
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(startX + 1, startY + 1, 45, 20), pctInt + "%", style);
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(startX, startY, 45, 20), pctInt + "%", style);

        // Contenedor de la bateria alineado con margen limpio antes de la esquina blanca del visor
        Rect batBox = new Rect(Screen.width - 102f, startY + 2f, 28f, 14f);
        Rect batTip = new Rect(batBox.x + batBox.width, batBox.y + 3f, 3f, 8f);

        // Dibujar borde de bateria y tip
        GUI.color = hudColor;
        // Top line
        GUI.DrawTexture(new Rect(batBox.x, batBox.y, batBox.width, 2f), whiteTex);
        // Bottom line
        GUI.DrawTexture(new Rect(batBox.x, batBox.y + batBox.height - 2f, batBox.width, 2f), whiteTex);
        // Left line
        GUI.DrawTexture(new Rect(batBox.x, batBox.y, 2f, batBox.height), whiteTex);
        // Right line
        GUI.DrawTexture(new Rect(batBox.x + batBox.width - 2f, batBox.y, 2f, batBox.height), whiteTex);
        // Battery Tip
        GUI.DrawTexture(batTip, whiteTex);

        // Dibujar las barras de carga internas (hasta 3 barras)
        int bars = Mathf.CeilToInt(batPct * 3f);
        GUI.color = batPct <= 0.2f ? Color.red : new Color(0.2f, 1f, 0.2f, 0.8f); // Rojo si es baja, verde si esta cargada
        
        for (int i = 0; i < bars; i++)
        {
            Rect barRect = new Rect(batBox.x + 3f + (i * 7f), batBox.y + 3f, 5f, 8f);
            GUI.DrawTexture(barRect, whiteTex);
        }

        GUI.color = Color.white;
    }
}
