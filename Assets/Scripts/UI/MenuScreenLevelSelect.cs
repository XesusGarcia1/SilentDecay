using UnityEngine;

/// <summary>
/// Pantalla de selección de mapa: tarjeta Hospital activa + tarjetas bloqueadas.
/// </summary>
public class MenuScreenLevelSelect : MonoBehaviour
{
    private MainMenuManager ctx;
    private Texture2D texHospitalThumb;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        texHospitalThumb = Resources.Load<Texture2D>("game1");
    }

    public void Draw(MenuStyles s)
    {
        // ─── Textos localizados ───────────────────────────────────────────────
        string title         = "SELECCIONA EL ESCENARIO";
        string hospitalLabel = "HOSPITAL Y TÚNELES";
        string lockedLabel   = "PRÓXIMAMENTE";
        string backBtn       = "  ATRÁS";

        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.GetIdiomaActual();
            if (lang == LocalizationManager.Idioma.ENGLISH)
            {
                title         = "SELECT MAP";
                hospitalLabel = "HOSPITAL & TUNNELS";
                lockedLabel   = "COMING SOON";
                backBtn       = "  BACK";
            }
            else if (lang == LocalizationManager.Idioma.PORTUGUES)
            {
                title         = "SELECIONE O MAPA";
                hospitalLabel = "HOSPITAL E TÚNEIS";
                lockedLabel   = "EM BREVE";
                backBtn       = "  VOLTAR";
            }
        }

        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(25);
        GUILayout.BeginHorizontal();

        DrawHospitalCard(s, hospitalLabel);
        GUILayout.Space(30);
        DrawLockedCard(s, GetLockedTitle("BOSQUE", "FOREST", "FLORESTA"), lockedLabel);
        GUILayout.Space(30);
        DrawLockedCard(s, GetLockedTitle("PRISIÓN", "PRISON", "PRISÃO"),  lockedLabel);

        GUILayout.EndHorizontal();
        GUILayout.Space(25);

        if (GUILayout.Button(backBtn, s.Button, GUILayout.Height(45)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.Main);
        }
    }

    // ─── Tarjetas ─────────────────────────────────────────────────────────────

    void DrawHospitalCard(MenuStyles s, string label)
    {
        GUIStyle playBtn = new GUIStyle(s.Button);
        playBtn.normal.textColor = Color.red;
        playBtn.hover.textColor  = Color.white;

        GUIStyle check = new GUIStyle(GUI.skin.label);
        check.fontSize = 32;
        check.normal.textColor = Color.green;
        check.alignment = TextAnchor.MiddleCenter;

        GUIStyle cardLabel = CardLabelStyle(s);

        string playText = GetLocalizedPlay();

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
        GUILayout.Space(10);

        Rect thumb = GUILayoutUtility.GetRect(356f, 200f);
        GUI.DrawTexture(thumb,
            texHospitalThumb != null ? texHospitalThumb : Texture2D.blackTexture,
            ScaleMode.StretchToFill);

        GUILayout.Space(12);
        GUILayout.Label(label, cardLabel, GUILayout.Height(50));
        GUILayout.Space(10);
        GUILayout.Label("✓", check, GUILayout.Height(35));
        GUILayout.Space(10);

        if (GUILayout.Button(playText, playBtn, GUILayout.Height(40)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.PlayOptions);
        }
        GUILayout.EndVertical();
    }

    void DrawLockedCard(MenuStyles s, string title, string lockedLabel)
    {
        GUIStyle cardLabel  = CardLabelStyle(s);
        GUIStyle lockStyle  = new GUIStyle(GUI.skin.label);
        lockStyle.fontSize  = 24;
        lockStyle.normal.textColor = Color.gray;
        lockStyle.alignment = TextAnchor.MiddleCenter;

        // Fondo negro
        Texture2D blackTex = new Texture2D(2, 2);
        Color bc = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        blackTex.SetPixel(0,0,bc); blackTex.SetPixel(0,1,bc);
        blackTex.SetPixel(1,0,bc); blackTex.SetPixel(1,1,bc);
        blackTex.Apply();
        GUIStyle blackBox = new GUIStyle(GUI.skin.box);
        blackBox.normal.background = blackTex;

        GUIStyle qStyle = new GUIStyle(s.Label);
        qStyle.fontSize = 72;
        qStyle.fontStyle = FontStyle.Bold;
        qStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
        qStyle.alignment = TextAnchor.MiddleCenter;

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
        GUILayout.Space(10);
        GUILayout.BeginVertical(blackBox, GUILayout.Width(356), GUILayout.Height(200));
        GUILayout.Label("?", qStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.EndVertical();
        GUILayout.Space(12);
        GUILayout.Label(title, cardLabel, GUILayout.Height(50));
        GUILayout.Space(10);
        GUILayout.Label("🔒 " + lockedLabel, lockStyle, GUILayout.Height(35));
        GUILayout.EndVertical();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    GUIStyle CardLabelStyle(MenuStyles s)
    {
        var st = new GUIStyle(s.Label);
        st.fontSize = 20;
        st.fontStyle = FontStyle.Bold;
        st.alignment = TextAnchor.MiddleCenter;
        return st;
    }

    string GetLocalizedPlay()
    {
        if (LocalizationManager.Instance == null) return "JUGAR";
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => "PLAY",
            LocalizationManager.Idioma.PORTUGUES => "JOGAR",
            _                                    => "JUGAR"
        };
    }

    string GetLockedTitle(string es, string en, string pt)
    {
        if (LocalizationManager.Instance == null) return $"{es}\n(BLOQUEADO)";
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => $"{en}\n(LOCKED)",
            LocalizationManager.Idioma.PORTUGUES => $"{pt}\n(BLOQUEADO)",
            _                                    => $"{es}\n(BLOQUEADO)"
        };
    }
}
