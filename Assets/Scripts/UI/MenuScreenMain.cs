using UnityEngine;

/// <summary>
/// Pantalla principal del menú: botones INICIAR PARTIDA, CONFIGURACIÓN, SALIR
/// y botones de redes sociales en el lateral derecho.
/// </summary>
public class MenuScreenMain : MonoBehaviour
{
    private MainMenuManager ctx;

    // Texturas de redes sociales
    private Texture2D texInstagram;
    private Texture2D texFacebook;
    private Texture2D texYoutube;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        texInstagram = Resources.Load<Texture2D>("Texturas/UI/social_instagram");
        texFacebook  = Resources.Load<Texture2D>("Texturas/UI/social_facebook");
        texYoutube   = Resources.Load<Texture2D>("Texturas/UI/social_youtube");
    }

    public void Draw(MenuStyles s)
    {
        string playBtn     = Loc("menu_jugar",    "INICIAR PARTIDA");
        string settingsBtn = Loc("menu_ajustes",  "CONFIGURACIÓN");
        string exitBtn     = Loc("menu_salir",    "SALIR DEL JUEGO");

        GUILayout.Space(30);

        if (GUILayout.Button($"  {playBtn}", s.Button, GUILayout.Height(75)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.LevelSelect);
        }
        GUILayout.Space(35);

        if (GUILayout.Button($"  {settingsBtn}", s.Button, GUILayout.Height(75)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.Settings);
        }
        GUILayout.Space(35);

        if (GUILayout.Button($"  {exitBtn}", s.Button, GUILayout.Height(75)))
        {
            ctx.PlayClickSound();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        
        GUILayout.FlexibleSpace();
    }

    public void DrawSocialButtons()
    {
        float socialX = 1810f;
        float startY  = 330f;
        float btnSize = 80f;
        float spacing = 22f;

        DrawSocialBtn(texInstagram, socialX, startY,                         ctx.instagramURL);
        DrawSocialBtn(texFacebook,  socialX, startY + btnSize + spacing,     ctx.facebookURL);
        
        string studioUrl = !string.IsNullOrEmpty(ctx.youtubeStudioURL) ? ctx.youtubeStudioURL : "https://www.youtube.com/@Xevora-Studios/videos";
        string artistUrl = !string.IsNullOrEmpty(ctx.youtubeArtistURL) ? ctx.youtubeArtistURL : (!string.IsNullOrEmpty(ctx.youtubeURL) ? ctx.youtubeURL : "https://www.youtube.com/@Xesus_Garcia");

        DrawSocialBtn(texYoutube,   socialX, startY + (btnSize+spacing)*2,   studioUrl, Loc("social_studio", "STUDIO"));
        DrawSocialBtn(texYoutube,   socialX, startY + (btnSize+spacing)*3,   artistUrl, Loc("social_artist", "ARTIST"));
    }

    void DrawSocialBtn(Texture2D tex, float x, float y, string url, string badge = "")
    {
        if (tex == null) return;
        Rect r = new Rect(x, y, 80f, 80f);
        if (GUI.Button(r, tex, GUIStyle.none))
        {
            ctx.PlayClickSound();
            Application.OpenURL(url);
        }

        if (!string.IsNullOrEmpty(badge))
        {
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            badgeStyle.normal.textColor = new Color(1f, 0.9f, 0.45f, 0.95f);

            Rect badgeRect = new Rect(x - 5f, y + 62f, 90f, 16f);

            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(badgeRect, GUIContent.none);
            GUI.color = prevColor;

            GUI.Label(badgeRect, badge, badgeStyle);
        }
    }

    // ─── Helper de localización ───────────────────────────────────────────────
    static string Loc(string key, string fallback)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : fallback;
}
