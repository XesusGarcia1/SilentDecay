using UnityEngine;

/// <summary>
/// Pantalla de selección de mapa: tarjeta Hospital activa + Depósito Industrial activo + Bosque bloqueado.
/// </summary>
public class MenuScreenLevelSelect : MonoBehaviour
{
    private MainMenuManager ctx;
    private Texture2D texHospitalThumb;
    private Texture2D texDepotThumb;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        texHospitalThumb = Resources.Load<Texture2D>("Texturas/UI/game1");
        texDepotThumb    = Resources.Load<Texture2D>("Texturas/UI/game2");
    }

    public void Draw(MenuStyles s)
    {
        // ─── Textos localizados ───────────────────────────────────────────────
        string title         = "SELECCIONA EL ESCENARIO";
        string hospitalLabel = "HOSPITAL Y TÚNELES";
        string depotLabel    = "DEPÓSITO INDUSTRIAL";
        string lockedLabel   = "PRÓXIMAMENTE";
        string backBtn       = "  ATRÁS";

        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.GetIdiomaActual();
            if (lang == LocalizationManager.Idioma.ENGLISH)
            {
                title         = "SELECT MAP";
                hospitalLabel = "HOSPITAL & TUNNELS";
                depotLabel    = "INDUSTRIAL DEPOT";
                lockedLabel   = "COMING SOON";
                backBtn       = "  BACK";
            }
            else if (lang == LocalizationManager.Idioma.PORTUGUES)
            {
                title         = "SELECIONE O MAPA";
                hospitalLabel = "HOSPITAL E TÚNEIS";
                depotLabel    = "DEPÓSITO INDUSTRIAL";
                lockedLabel   = "EM BREVE";
                backBtn       = "  VOLTAR";
            }
            else if (lang == LocalizationManager.Idioma.РУССКИЙ)
            {
                title         = "ВЫБЕРИТЕ КАРТУ";
                hospitalLabel = "БОЛЬНИЦА И ТУННЕЛИ";
                depotLabel    = "ПРОМЫШЛЕННЫЙ СКЛАД";
                lockedLabel   = "СКОРО";
                backBtn       = "  НАЗАД";
            }
        }

        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(25);
        GUILayout.BeginHorizontal();

        DrawHospitalCard(s, hospitalLabel);
        GUILayout.Space(30);
        DrawLockedCard(s, GetLockedTitle("BOSQUE", "FOREST", "FLORESTA", "ЛЕС"), lockedLabel);
        GUILayout.Space(30);
        DrawDepotCard(s, depotLabel);

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

        string playText = GetLocalized("JUGAR", "PLAY", "JOGAR", "ИГРАТЬ");

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

    void DrawDepotCard(MenuStyles s, string label)
    {
        bool isUnlocked = (ctx != null && ctx.unlockAllMapsDebug) || PlayerPrefs.GetInt("Campaign_HospitalTunnelsCompleted", 0) == 1;

        GUIStyle cardLabel = CardLabelStyle(s);

        if (isUnlocked)
        {
            GUIStyle playBtn = new GUIStyle(s.Button);
            playBtn.normal.textColor = Color.red;
            playBtn.hover.textColor  = Color.white;

            string playText = GetLocalized("JUGAR", "PLAY", "JOGAR", "ИГРАТЬ");

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
            GUILayout.Space(10);

            Rect thumb = GUILayoutUtility.GetRect(356f, 200f);
            GUI.DrawTexture(thumb,
                texDepotThumb != null ? texDepotThumb : Texture2D.blackTexture,
                ScaleMode.StretchToFill);

            GUILayout.Space(12);
            GUILayout.Label(label, cardLabel, GUILayout.Height(50));
            GUILayout.Space(16);

            if (GUILayout.Button(playText, playBtn, GUILayout.Height(40)))
            {
                ctx.PlayClickSound();
                ctx.GoTo(MainMenuManager.MenuState.DepotOptions);
            }
            GUILayout.EndVertical();
        }
        else
        {
            // DEPÓSITO BLOQUEADO HASTA COMPLETAR HOSPITAL Y TÚNELES
            GUIStyle lockStyle = new GUIStyle(GUI.skin.label);
            lockStyle.fontSize = 20;
            lockStyle.fontStyle = FontStyle.Bold;
            lockStyle.normal.textColor = new Color(0.95f, 0.45f, 0.45f);
            lockStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle reqStyle = new GUIStyle(GUI.skin.label);
            reqStyle.fontSize = 12;
            reqStyle.fontStyle = FontStyle.Bold;
            reqStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            reqStyle.alignment = TextAnchor.MiddleCenter;

            string lockedStatus = GetLocalized("🔒 BLOQUEADO", "🔒 LOCKED", "🔒 BLOQUEADO", "🔒 ЗАБЛОКИРОВАНО");
            string reqText = GetLocalized("REQUIERE COMPLETAR\nHOSPITAL Y TÚNELES", "REQUIRES COMPLETING\nHOSPITAL & TUNNELS", "REQUER COMPLETAR\nHOSPITAL E TÚNEIS", "ТРЕБУЕТСЯ ПРОЙТИ\nБОЛЬНИЦУ И ТУННЕЛИ");

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(380));
            GUILayout.Space(10);

            Rect thumb = GUILayoutUtility.GetRect(356f, 200f);
            // Dibujar miniatura oscurecida
            Color prevColor = GUI.color;
            GUI.color = new Color(0.35f, 0.35f, 0.35f, 0.9f);
            GUI.DrawTexture(thumb,
                texDepotThumb != null ? texDepotThumb : Texture2D.blackTexture,
                ScaleMode.StretchToFill);
            GUI.color = prevColor;

            GUILayout.Space(12);
            GUILayout.Label(label, cardLabel, GUILayout.Height(40));
            GUILayout.Space(4);
            GUILayout.Label(lockedStatus, lockStyle, GUILayout.Height(25));
            GUILayout.Label(reqText, reqStyle, GUILayout.Height(35));
            GUILayout.EndVertical();
        }
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

    string GetLocalized(string es, string en, string pt, string ru)
    {
        if (LocalizationManager.Instance == null) return es;
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => en,
            LocalizationManager.Idioma.PORTUGUES => pt,
            LocalizationManager.Idioma.РУССКИЙ   => ru,
            _                                    => es
        };
    }

    string GetLockedTitle(string es, string en, string pt, string ru)
    {
        if (LocalizationManager.Instance == null) return $"{es}\n(BLOQUEADO)";
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => $"{en}\n(LOCKED)",
            LocalizationManager.Idioma.PORTUGUES => $"{pt}\n(BLOQUEADO)",
            LocalizationManager.Idioma.РУССКИЙ   => $"{ru}\n(ЗАБЛОКИРОВАНО)",
            _                                    => $"{es}\n(BLOQUEADO)"
        };
    }
}
