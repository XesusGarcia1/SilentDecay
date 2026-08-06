using UnityEngine;

/// <summary>
/// Estilos GUI reutilizables compartidos entre todas las pantallas del menú.
/// Debe instanciarse una sola vez por MainMenuManager para no crear texturas en cada frame.
/// </summary>
public class MenuStyles
{
    public readonly GUIStyle Title;
    public readonly GUIStyle SubTitle;
    public readonly GUIStyle SectionHeader;
    public readonly GUIStyle Label;
    public readonly GUIStyle Button;
    public readonly GUIStyle OptionSelect;

    // Texturas generadas en memoria
    private Texture2D btnNormalTex;
    private Texture2D btnHoverTex;
    private Texture2D btnActiveTex;
    
    private Texture2D optNormalTex;
    private Texture2D optHoverTex;

    public MenuStyles()
    {
        // Generar texturas de botones (fondos oscuros semitransparentes elegantes)
        btnNormalTex = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.85f));
        btnHoverTex  = MakeTex(2, 2, new Color(0.18f, 0.05f, 0.05f, 0.95f)); // Rojo oscuro al pasar el ratón
        btnActiveTex = MakeTex(2, 2, new Color(0.3f, 0.05f, 0.05f, 1.0f));

        optNormalTex = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.6f));
        optHoverTex  = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.8f));

        Title = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 62,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        Title.normal.textColor = new Color(0.85f, 0.05f, 0.05f);

        SubTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        SubTitle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        SectionHeader = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        SectionHeader.normal.textColor = Color.white;

        Label = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            alignment = TextAnchor.MiddleCenter
        };
        Label.normal.textColor = Color.white;

        Button = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 24, // Ligeramente más grande
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        
        // Aplicar texturas de fondo en vez del botón gris por defecto
        Button.normal.background = btnNormalTex;
        Button.hover.background  = btnHoverTex;
        Button.active.background = btnActiveTex;
        Button.focused.background = btnNormalTex;
        
        Button.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        Button.hover.textColor  = new Color(1f, 0.2f, 0.2f);
        Button.active.textColor = new Color(1f, 0.4f, 0.4f);
        Button.border = new RectOffset(0,0,0,0); // Eliminar bordes curvos de Unity por defecto

        OptionSelect = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        OptionSelect.normal.background = optNormalTex;
        OptionSelect.hover.background  = optHoverTex;
        OptionSelect.active.background = btnHoverTex;
        OptionSelect.focused.background = optNormalTex;
        OptionSelect.border = new RectOffset(0,0,0,0);
        OptionSelect.normal.textColor = Color.gray;
        OptionSelect.hover.textColor  = Color.white;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for(int i = 0; i < pix.Length; ++i)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
