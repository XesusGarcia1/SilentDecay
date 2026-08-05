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
        // ─── Título ───────────────────────────────────────────────────────────
        string title = GetLocalized("CONFIGURACIÓN DE HARDWARE", "HARDWARE SETTINGS", "CONFIGURAÇÃO DE HARDWARE");
        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(20);

        // ─── Pestañas ─────────────────────────────────────────────────────────
        GUIStyle tabStyle = new GUIStyle(GUI.skin.button);
        tabStyle.fontSize  = 16;
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

        // ─── Brillo / Gamma ──────────────────────────────────────────────────
        float curGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
        string gammaTitle = GetLocalized("BRILLO / GAMMA", "BRIGHTNESS / GAMMA", "BRILHO / GAMMA");
        GUILayout.Label($"{gammaTitle}: {curGamma:F1}x", s.Label);
        float newGamma = GUILayout.HorizontalSlider(curGamma, 0.5f, 2.0f);
        if (Mathf.Abs(newGamma - curGamma) > 0.01f)
        {
            GammaManager.AplicarGamma(newGamma);
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
}
