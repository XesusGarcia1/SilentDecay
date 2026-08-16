using UnityEngine;

public class KeyHUDManager : MonoBehaviour
{
    public static KeyHUDManager Instance { get; private set; }

    private Texture2D proceduralKeyTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // No static variables needed to reset
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindObjectOfType<KeyHUDManager>() == null)
        {
            GameObject go = new GameObject("[KeyHUDManager]");
            go.AddComponent<KeyHUDManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private Texture2D GetKeyTexture()
    {
        if (proceduralKeyTex != null) return proceduralKeyTex;
        proceduralKeyTex = CreateProceduralKeyTexture();
        return proceduralKeyTex;
    }

    private Texture2D CreateProceduralKeyTexture()
    {
        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Color gold = new Color(0.98f, 0.84f, 0.25f, 1.0f);
        Color goldDark = new Color(0.7f, 0.55f, 0.12f, 1.0f);

        Vector2 ringCenter = new Vector2(22, 42);
        float outerR = 13f;
        float innerR = 6f;

        Vector2 shaftStart = ringCenter;
        Vector2 shaftEnd = new Vector2(48, 16);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 pt = new Vector2(x, y);
                float distRing = Vector2.Distance(pt, ringCenter);

                // Anillo de la llave
                if (distRing <= outerR && distRing >= innerR)
                {
                    pixels[y * w + x] = (distRing > outerR - 1.8f || distRing < innerR + 1.8f) ? goldDark : gold;
                    continue;
                }

                // Vástago/cuerpo diagonal de la llave
                float distLine = DistanceToLineSegment(pt, shaftStart, shaftEnd);
                if (distLine <= 3.2f && pt.x >= ringCenter.x - 2)
                {
                    pixels[y * w + x] = distLine > 1.8f ? goldDark : gold;
                    continue;
                }

                // Dientes de la llave en la punta
                if (x >= 38 && x <= 43 && y >= 11 && y <= 18)
                {
                    pixels[y * w + x] = gold;
                }
                if (x >= 45 && x <= 50 && y >= 4 && y <= 11)
                {
                    pixels[y * w + x] = gold;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private float DistanceToLineSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
        return Vector2.Distance(p, a + t * ab);
    }

    private bool ShouldSuppressKeyHUD()
    {
        if (Time.timeScale == 0f) return true;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "LoadingScene" || sceneName == "MainMenu") return true;

        var generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (generator != null && generator.isMenuMode) return true;

        return false;
    }

    void OnGUI()
    {
        if (ShouldSuppressKeyHUD()) return;

        // Solo mostrar si el jugador tiene al menos una llave recogida
        bool hasKeys = MetalKeyItem.hasMetalKey || (MetalKeyItem.collectedKeys != null && MetalKeyItem.collectedKeys.Count > 0);
        if (!hasKeys) return;

        int keyCount = MetalKeyItem.collectedKeys != null ? Mathf.Max(1, MetalKeyItem.collectedKeys.Count) : 1;

        // Aplicar escalado de HUD según preferencias del jugador
        float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
        Matrix4x4 oldHudMat = GUI.matrix;
        if (hudScale != 1.0f)
        {
            Vector2 pivot = new Vector2(25, 25);
            GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
        }

        // Posicionar el icono de llaves con espacio holgado debajo del botón de la Guía
        float btnSize = 46f;
        float yPos = GuideMapUI.hasGuideMap ? 245f : 170f;
        Rect iconRect = new Rect(25f, yPos, btnSize, btnSize);

        // 1. Fondo semitransparente oscuro unificado
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. Borde dorado fino si se tienen llaves
        GUI.color = new Color(0.95f, 0.8f, 0.25f, 0.6f);
        GUI.DrawTexture(new Rect(iconRect.x - 1, iconRect.y - 1, iconRect.width + 2, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(iconRect.x - 1, iconRect.y + iconRect.height, iconRect.width + 2, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(iconRect.x - 1, iconRect.y - 1, 1, iconRect.height + 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(iconRect.x + iconRect.width, iconRect.y - 1, 1, iconRect.height + 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 3. Renderizar icono 2D nítido de la Llave
        Texture2D keyTex = GetKeyTexture();
        if (keyTex != null)
        {
            GUI.DrawTexture(new Rect(iconRect.x + 4, iconRect.y + 4, iconRect.width - 8, iconRect.height - 8), keyTex, ScaleMode.ScaleToFit, true);
        }

        // 4. Etiqueta / Contador de Llaves (ej: LLAVES (1))
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 10;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.normal.textColor = new Color(0.95f, 0.85f, 0.3f);
        GUI.Label(new Rect(iconRect.x - 10, iconRect.y + iconRect.height + 1, iconRect.width + 20, 16), $"LLAVES ({keyCount})", labelStyle);

        GUI.matrix = oldHudMat;
    }
}
