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
        texInstagram = Resources.Load<Texture2D>("social_instagram");
        texFacebook  = Resources.Load<Texture2D>("social_facebook");
        texYoutube   = Resources.Load<Texture2D>("social_youtube");
    }

    public void Draw(MenuStyles s)
    {
        string playBtn     = Loc("menu_jugar",    "INICIAR PARTIDA");
        string settingsBtn = Loc("menu_ajustes",  "CONFIGURACIÓN");
        string exitBtn     = Loc("menu_salir",    "SALIR DEL JUEGO");

        if (GUILayout.Button($"  {playBtn}", s.Button, GUILayout.Height(60)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.LevelSelect);
        }
        GUILayout.Space(25);

        if (GUILayout.Button($"  {settingsBtn}", s.Button, GUILayout.Height(60)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.Settings);
        }
        GUILayout.Space(25);

        if (GUILayout.Button($"  {exitBtn}", s.Button, GUILayout.Height(60)))
        {
            ctx.PlayClickSound();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    public void DrawSocialButtons()
    {
        float socialX = 1810f;
        float startY  = 400f;
        float btnSize = 80f;
        float spacing = 20f;

        DrawSocialBtn(texInstagram, socialX, startY,                       ctx.instagramURL);
        DrawSocialBtn(texFacebook,  socialX, startY + btnSize + spacing,   ctx.facebookURL);
        DrawSocialBtn(texYoutube,   socialX, startY + (btnSize+spacing)*2, ctx.youtubeURL);
    }

    void DrawSocialBtn(Texture2D tex, float x, float y, string url)
    {
        if (tex == null) return;
        if (GUI.Button(new Rect(x, y, 80f, 80f), tex, GUIStyle.none))
        {
            ctx.PlayClickSound();
            Application.OpenURL(url);
        }
    }

    // ─── Helper de localización ───────────────────────────────────────────────
    static string Loc(string key, string fallback)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : fallback;
}
