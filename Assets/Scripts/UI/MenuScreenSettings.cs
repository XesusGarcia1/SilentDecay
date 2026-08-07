using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pantalla de configuración: pestañas Audio/Controles y Gráficos/Rendimiento.
/// </summary>
public class MenuScreenSettings : MonoBehaviour
{
    private MainMenuManager ctx;
    private int activeTab = 0; // 0 = Audio & Controles, 1 = Gráficos
    private int selectedQualityIndex = 2;

    // --- VARIABLES DE CALIBRACIÓN DE BRILLO ---
    private bool isCalibratingGamma = false;
    private float tempGamma = 1.0f;

    public bool IsCalibrating => isCalibratingGamma;

#if !UNITY_ANDROID && !UNITY_IOS
    private List<Resolution> pcResolutions = new List<Resolution>();
    private int selectedResIndex = 0;
#endif

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        selectedQualityIndex = PlayerPrefs.GetInt("QualityLevel", 2);

#if !UNITY_ANDROID && !UNITY_IOS
        pcResolutions.Clear();
        foreach (var r in Screen.resolutions)
            if (!pcResolutions.Exists(x => x.width == r.width && x.height == r.height))
                pcResolutions.Add(r);

        selectedResIndex = pcResolutions.Count - 1;
        for (int i = 0; i < pcResolutions.Count; i++)
            if (pcResolutions[i].width == Screen.width && pcResolutions[i].height == Screen.height)
            { selectedResIndex = i; break; }
#endif
    }

    public void Draw(MenuStyles s)
    {
        if (isCalibratingGamma)
        {
            DrawGammaCalibrationScreen(s);
            return;
        }

        // ─── Título ───────────────────────────────────────────────────────────
        string title = GetLocalized("CONFIGURACIÓN DE HARDWARE", "HARDWARE SETTINGS", "CONFIGURAÇÃO DE HARDWARE");
        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(20);

        // ─── Pestañas ─────────────────────────────────────────────────────────
        GUIStyle tabStyle = new GUIStyle(s.OptionSelect);
        tabStyle.fontSize  = 18;
        tabStyle.fontStyle = FontStyle.Bold;
        tabStyle.hover.textColor = Color.red;

        GUILayout.BeginHorizontal();

        tabStyle.normal.textColor = activeTab == 0 ? Color.red : Color.gray;
        string tab0 = GetLocalized("AUDIO Y CONTROLES", "AUDIO & CONTROLS", "ÁUDIO E CONTROLES");
        if (GUILayout.Button(tab0, tabStyle, GUILayout.Height(40))) { ctx.PlayClickSound(); activeTab = 0; }

        tabStyle.normal.textColor = activeTab == 1 ? Color.red : Color.gray;
        string tab1 = GetLocalized("GRÁFICOS Y RENDIMIENTO", "GRAPHICS & RUNTIME", "GRÁFICOS E VIDEO");
        if (GUILayout.Button(tab1, tabStyle, GUILayout.Height(40))) { ctx.PlayClickSound(); activeTab = 1; }

        GUILayout.EndHorizontal();
        GUILayout.Space(30);

        if (activeTab == 0) DrawAudioTab(s);
        else                DrawGraphicsTab(s);

        GUILayout.Space(45);

        // ─── Guardar y Volver ─────────────────────────────────────────────────
        string saveBtn = GetLocalized("  GUARDAR Y VOLVER", "  SAVE & BACK", "  SALVAR E VOLTAR");
        if (GUILayout.Button(saveBtn, s.Button, GUILayout.Height(55)))
        {
            ctx.PlayClickSound();
            PlayerPrefs.SetFloat("MouseSensitivity", ctx.mouseSensitivity);
            PlayerPrefs.SetFloat("MasterVolume",     ctx.masterVolume);
            PlayerPrefs.SetInt("QualityLevel",       selectedQualityIndex);
            PlayerPrefs.Save();
            ctx.GoTo(MainMenuManager.MenuState.Main);
        }
    }

    // ─── Tab 0: Audio & Controles ─────────────────────────────────────────────

    void DrawAudioTab(MenuStyles s)
    {
        // Volumen
        string volLabel = Loc("menu_volumen", "Volumen General");
        GUILayout.Label($"{volLabel}: {Mathf.RoundToInt(ctx.masterVolume * 100)}%", s.Label);
        ctx.masterVolume     = GUILayout.HorizontalSlider(ctx.masterVolume, 0f, 1f);
        AudioListener.volume = ctx.masterVolume;
        if (ctx.menuAudioSource != null) ctx.menuAudioSource.volume = ctx.masterVolume * 0.6f;
        if (ctx.sfxAudioSource  != null) ctx.sfxAudioSource.volume  = ctx.masterVolume * 0.85f;
        GUILayout.Space(25);

        // Sensibilidad
        string sensLabel = Loc("menu_sensibilidad", "Sensibilidad de Cámara");
        GUILayout.Label($"{sensLabel}: {ctx.mouseSensitivity:F1}", s.Label);
        ctx.mouseSensitivity = GUILayout.HorizontalSlider(ctx.mouseSensitivity, 0.5f, 6.0f);
        GUILayout.Space(25);

        // Idioma
        string langLabel = Loc("menu_idioma", "Idioma");
        GUILayout.Label($"{langLabel}:", s.Label);
        GUILayout.Space(5);
        DrawLanguageSelector(s);
        GUILayout.Space(25);

        // Pantalla completa
        string fsLabel = GetLocalized("Pantalla Completa", "Full Screen", "Tela Cheia");
        GUILayout.BeginHorizontal();
        GUILayout.Label(fsLabel, s.Label, GUILayout.Width(200));
        ctx.isFullscreen = GUILayout.Toggle(ctx.isFullscreen, "");
        if (Screen.fullScreen != ctx.isFullscreen) Screen.fullScreen = ctx.isFullscreen;
        GUILayout.EndHorizontal();
    }

    void DrawLanguageSelector(MenuStyles s)
    {
        if (LocalizationManager.Instance == null) return;
        var cur = LocalizationManager.Instance.GetIdiomaActual();

        GUILayout.BeginHorizontal();
        DrawLangBtn(s, "ESPAÑOL",  LocalizationManager.Idioma.ESPAÑOL,  cur);
        DrawLangBtn(s, "ENGLISH",  LocalizationManager.Idioma.ENGLISH,  cur);
        DrawLangBtn(s, "PORTUGUÊS",LocalizationManager.Idioma.PORTUGUES, cur);
        GUILayout.EndHorizontal();
    }

    void DrawLangBtn(MenuStyles s, string label, LocalizationManager.Idioma idioma, LocalizationManager.Idioma cur)
    {
        var st = new GUIStyle(s.OptionSelect);
        st.normal.textColor = (cur == idioma) ? Color.red : Color.gray;
        if (GUILayout.Button(label, st, GUILayout.Height(35)))
        {
            ctx.PlayClickSound();
            LocalizationManager.Instance.CambiarIdioma(idioma);
        }
    }

    // ─── Tab 1: Gráficos ──────────────────────────────────────────────────────

    void DrawGraphicsTab(MenuStyles s)
    {
        // Calidad
        string qualLabel = Loc("menu_graficos", "Calidad de Gráficos:");
        GUILayout.Label(qualLabel, s.Label);
        GUILayout.Space(5);
        string[] qualNames = GetLocalizedQuality();
        GUILayout.BeginHorizontal();
        for (int i = 0; i < qualNames.Length; i++)
        {
            var st = new GUIStyle(s.OptionSelect);
            st.normal.textColor = (selectedQualityIndex == i) ? Color.red : Color.gray;
            if (GUILayout.Button(qualNames[i], st, GUILayout.Height(40)))
            {
                ctx.PlayClickSound();
                selectedQualityIndex = i;
                QualitySettings.SetQualityLevel(i, true);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(35);

        // Resolución
        string resLabel = GetLocalized("Resolución de Pantalla:", "Screen Resolution:", "Resolução de Tela:");
        GUILayout.Label(resLabel, s.Label);
        GUILayout.Space(5);

#if UNITY_ANDROID || UNITY_IOS
        var grayed = new GUIStyle(s.Label);
        grayed.normal.textColor = Color.gray;
        string nativeText = GetLocalized("Nativa del Dispositivo", "Device Native", "Nativa do Dispositivo");
        GUILayout.Label($"{Screen.currentResolution.width}x{Screen.currentResolution.height} ({nativeText})", grayed, GUILayout.Height(40));
#else
        DrawResolutionSelector(s);
#endif

        GUILayout.Space(25);

        // ─── Brillo / Gamma (Calibración dedicada) ───────────────────────────
        float curGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
        string calibTitle = GetLocalized("AJUSTAR BRILLO / GAMMA...", "ADJUST BRIGHTNESS / GAMMA...", "AJUSTAR BRILHO / GAMMA...");
        
        GUIStyle calibBtnStyle = new GUIStyle(s.Button);
        calibBtnStyle.normal.textColor = Color.white;
        calibBtnStyle.hover.textColor = Color.red;

        if (GUILayout.Button($"🔧 {calibTitle} (Actual: {curGamma:F1}x)", calibBtnStyle, GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            tempGamma = curGamma; // Guardar valor inicial por si cancela
            isCalibratingGamma = true;
        }
    }

#if !UNITY_ANDROID && !UNITY_IOS
    void DrawResolutionSelector(MenuStyles s)
    {
        if (pcResolutions == null || pcResolutions.Count == 0)
        {
            GUILayout.Label($"{Screen.width}x{Screen.height}", s.Label);
            return;
        }
        GUIStyle cycleBtn = new GUIStyle(GUI.skin.button);
        cycleBtn.fontSize = 20; cycleBtn.fontStyle = FontStyle.Bold;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", cycleBtn, GUILayout.Width(50), GUILayout.Height(40)))
        {
            ctx.PlayClickSound();
            selectedResIndex = (selectedResIndex - 1 + pcResolutions.Count) % pcResolutions.Count;
            var r = pcResolutions[selectedResIndex];
            Screen.SetResolution(r.width, r.height, ctx.isFullscreen);
        }

        var resLabelStyle = new GUIStyle(s.Label);
        resLabelStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label($"{pcResolutions[selectedResIndex].width} x {pcResolutions[selectedResIndex].height}", resLabelStyle, GUILayout.Height(40));

        if (GUILayout.Button(">", cycleBtn, GUILayout.Width(50), GUILayout.Height(40)))
        {
            ctx.PlayClickSound();
            selectedResIndex = (selectedResIndex + 1) % pcResolutions.Count;
            var r = pcResolutions[selectedResIndex];
            Screen.SetResolution(r.width, r.height, ctx.isFullscreen);
        }
        GUILayout.EndHorizontal();
    }
#endif

    // ─── Helpers ──────────────────────────────────────────────────────────────

    string[] GetLocalizedQuality()
    {
        if (LocalizationManager.Instance == null) return new[] { "BAJO", "MEDIO", "ALTO" };
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => new[] { "LOW",   "MEDIUM", "HIGH" },
            LocalizationManager.Idioma.PORTUGUES => new[] { "BAIXO", "MÉDIO",  "ALTO" },
            _                                    => new[] { "BAJO",  "MEDIO",  "ALTO" }
        };
    }

    string GetLocalized(string es, string en, string pt)
    {
        if (LocalizationManager.Instance == null) return es;
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => en,
            LocalizationManager.Idioma.PORTUGUES => pt,
            _                                    => es
        };
    }

    static string Loc(string key, string fallback)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : fallback;

    // ─── PANTALLA GIGANTE DE CALIBRACIÓN DE BRILLO / GAMMA ────────────────────
    private void DrawGammaCalibrationScreen(MenuStyles s)
    {
        // 1. Título e Instrucciones
        string title = GetLocalized("CALIBRACIÓN DE BRILLO / GAMMA", "BRIGHTNESS / GAMMA CALIBRATION", "CALIBRAÇÃO DE BRILHO / GAMMA");
        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(15);

        string instructions = GetLocalized(
            "Ajusta el brillo hasta que el icono del engranaje de la derecha sea apenas visible sobre el fondo oscuro.",
            "Adjust the slider until the gear icon on the right is barely visible against the dark background.",
            "Ajuste o controle até que o ícone da engrenagem à direita seja quase invisível sobre o fundo escuro."
        );

        GUIStyle instStyle = new GUIStyle(s.Label);
        instStyle.fontSize = 15;
        instStyle.fontStyle = FontStyle.Normal;
        instStyle.wordWrap = true;
        instStyle.alignment = TextAnchor.MiddleCenter;
        instStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        GUILayout.Label(instructions, instStyle, GUILayout.Width(700));
        GUILayout.Space(20);

        // 2. Panel central: Controles del slider a la izquierda, y caja visual de calibración a la derecha
        GUILayout.BeginHorizontal(GUILayout.Width(700));

        // --- SUBPANEL IZQUIERDO: SLIDER GIGANTE Y BOTONES FINOS (Ancho Fijo 400) ---
        GUILayout.BeginVertical(GUILayout.Width(400));
        GUILayout.Space(25);

        string labelVal = GetLocalized("Brillo del Juego", "Game Brightness", "Brilho do Jogo");
        GUILayout.Label($"{labelVal}: {tempGamma:F2}x", s.Label);
        GUILayout.Space(5);

        // Slider Horizontal Principal
        float newGamma = GUILayout.HorizontalSlider(tempGamma, 0.5f, 2.0f, GUILayout.Height(30), GUILayout.Width(380));
        if (Mathf.Abs(newGamma - tempGamma) > 0.005f)
        {
            tempGamma = newGamma;
            GammaManager.AplicarGamma(tempGamma); // Aplicar cambios a la pantalla de Unity al instante
        }
        GUILayout.Space(10);

        // Botones de ajuste fino [-] y [+]
        GUILayout.BeginHorizontal(GUILayout.Width(380));
        if (GUILayout.Button(" - 0.1 ", GUILayout.Width(100), GUILayout.Height(40)))
        {
            ctx.PlayClickSound();
            tempGamma = Mathf.Clamp(tempGamma - 0.1f, 0.5f, 2.0f);
            GammaManager.AplicarGamma(tempGamma);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(" + 0.1 ", GUILayout.Width(100), GUILayout.Height(40)))
        {
            ctx.PlayClickSound();
            tempGamma = Mathf.Clamp(tempGamma + 0.1f, 0.5f, 2.0f);
            GammaManager.AplicarGamma(tempGamma);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(25);

        // --- SUBPANEL DERECHO: CAJA DE PRUEBA DE CONTRASTE (Ancho Fijo 220) ---
        GUIStyle darkBoxStyle = new GUIStyle(GUI.skin.box);
        darkBoxStyle.normal.background = Texture2D.whiteTexture; // Textura plana
        
        // El color del engranaje cambiará drásticamente en contraste según tempGamma
        // Multiplicador agresivo para que el jugador note el contraste del icono de calibración de inmediato
        float colorFactor = Mathf.Clamp01((tempGamma - 0.5f) / 1.5f); // 0 a 1
        float iconColorValue = Mathf.Lerp(0.02f, 0.40f, colorFactor); // De casi negro a gris medio visible
        Color dynamicGearColor = new Color(iconColorValue, iconColorValue, iconColorValue, 1f);

        GUIStyle gearIconStyle = new GUIStyle(s.Label);
        gearIconStyle.fontSize = 110; // Icono gigante
        gearIconStyle.alignment = TextAnchor.MiddleCenter;
        gearIconStyle.normal.textColor = dynamicGearColor;

        // Dibujar el recuadro negro-gris de calibración
        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.015f, 0.015f, 0.015f, 1f); // Fondo extremadamente oscuro
        GUILayout.BeginVertical(darkBoxStyle, GUILayout.Width(220), GUILayout.Height(220));
        GUI.backgroundColor = prevColor;

        GUILayout.FlexibleSpace();
        GUILayout.Label("⚙", gearIconStyle);
        GUILayout.FlexibleSpace();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(35);

        // 3. Botones inferiores de Guardar y Cancelar (Ancho total 700)
        GUILayout.BeginHorizontal(GUILayout.Width(700));

        string btnConfirm = GetLocalized("  CONFIRMAR Y GUARDAR", "  CONFIRM & SAVE", "  CONFIRMAR E SALVAR");
        if (GUILayout.Button(btnConfirm, s.Button, GUILayout.Width(340), GUILayout.Height(55)))
        {
            ctx.PlayClickSound();
            PlayerPrefs.SetFloat("GammaLevel", tempGamma);
            PlayerPrefs.Save();
            GammaManager.AplicarGamma(tempGamma);
            isCalibratingGamma = false;
        }

        GUILayout.Space(20);

        string btnCancel = GetLocalized("  CANCELAR", "  CANCEL", "  CANCELAR");
        if (GUILayout.Button(btnCancel, s.Button, GUILayout.Width(340), GUILayout.Height(55)))
        {
            ctx.PlayClickSound();
            float originalGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
            GammaManager.AplicarGamma(originalGamma);
            isCalibratingGamma = false;
        }

        GUILayout.EndHorizontal();
    }
}
