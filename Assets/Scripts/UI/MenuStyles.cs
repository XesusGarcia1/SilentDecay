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
    public readonly GUIStyle SmallButton;
    public readonly GUIStyle TabButton;
    public readonly GUIStyle SliderTrack;
    public readonly GUIStyle SliderThumb;
    public readonly GUIStyle Toggle;
    public readonly Color BrandRed = new Color(0.6f, 0.05f, 0.05f); // Rojo sangre oscuro consistente

    // Texturas generadas en memoria
    private Texture2D btnNormalTex;
    private Texture2D btnHoverTex;
    private Texture2D btnActiveTex;
    
    private Texture2D optNormalTex;
    private Texture2D optHoverTex;
    private Texture2D optActiveTex; // Para pequeños controles activos

    public MenuStyles(Texture2D customNormal = null, Texture2D customHover = null)
    {
        // Generar texturas de botones (fondos oscuros semitransparentes elegantes) o usar las personalizadas
        btnNormalTex = customNormal != null ? customNormal : MakeTex(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.85f));
        btnHoverTex  = customHover != null ? customHover : MakeTex(2, 2, new Color(0.18f, 0.05f, 0.05f, 0.95f)); // Rojo oscuro al pasar el ratón
        btnActiveTex = customHover != null ? customHover : MakeTex(2, 2, new Color(0.3f, 0.05f, 0.05f, 1.0f));

        optNormalTex = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.6f));
        optHoverTex  = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        optActiveTex = MakeTex(2, 2, new Color(0.2f, 0.02f, 0.02f, 0.85f)); // Rojo muy sutil para OptionSelect seleccionado

        Title = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        Title.normal.textColor = BrandRed;

        SubTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        SubTitle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        SectionHeader = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        SectionHeader.normal.textColor = Color.white;

        Label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleLeft
        };
        Label.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        Button = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 24, // Ligeramente más grande
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding   = new RectOffset(0, 0, 0, 0)
        };
        
        // Aplicar texturas de fondo en vez del botón gris por defecto
        Button.normal.background = btnNormalTex;
        Button.hover.background  = btnHoverTex;
        Button.active.background = btnActiveTex;
        Button.focused.background = btnNormalTex;
        
        Button.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        Button.hover.textColor  = BrandRed;
        Button.active.textColor = new Color(1f, 0.4f, 0.4f);
        Button.border = new RectOffset(12, 12, 12, 12); // Proteger bordes del VHS
        Button.overflow = new RectOffset(0, 0, 0, 0);

        OptionSelect = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        OptionSelect.normal.background = optNormalTex;
        OptionSelect.hover.background  = optHoverTex;
        OptionSelect.active.background = optActiveTex;
        OptionSelect.focused.background = optNormalTex;
        OptionSelect.border = new RectOffset(0,0,0,0);
        OptionSelect.normal.textColor = Color.gray;
        OptionSelect.hover.textColor  = Color.white;
        
        SmallButton = new GUIStyle(OptionSelect)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        SmallButton.hover.textColor = BrandRed;
        
        TabButton = new GUIStyle(OptionSelect)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        TabButton.hover.textColor = BrandRed;

        Texture2D sliderTrackTex = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 1.0f));
        Texture2D sliderThumbTex = MakeTex(2, 2, new Color(0.5f, 0.5f, 0.5f, 1.0f));
        Texture2D sliderThumbHoverTex = MakeTex(2, 2, BrandRed);

        SliderTrack = new GUIStyle(GUI.skin.horizontalSlider)
        {
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 15, 15)
        };
        SliderTrack.normal.background = sliderTrackTex;
        SliderTrack.hover.background = sliderTrackTex;
        SliderTrack.active.background = sliderTrackTex;
        SliderTrack.focused.background = sliderTrackTex;

        SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth = 20,
            fixedHeight = 32,
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };
        SliderThumb.normal.background = sliderThumbTex;
        SliderThumb.hover.background = sliderThumbHoverTex;
        SliderThumb.active.background = sliderThumbHoverTex;
        SliderThumb.focused.background = sliderThumbHoverTex;

        Texture2D toggleNormalTex = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        Texture2D toggleActiveTex = MakeTex(2, 2, BrandRed);

        Toggle = new GUIStyle(GUI.skin.toggle)
        {
            fixedWidth = 32,
            fixedHeight = 32,
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 2, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        Toggle.normal.textColor = Color.gray;
        Toggle.hover.textColor = Color.white;
        Toggle.onNormal.textColor = BrandRed;
        Toggle.onHover.textColor = BrandRed;
        
        Toggle.normal.background = toggleNormalTex;
        Toggle.hover.background = toggleNormalTex;
        Toggle.onNormal.background = toggleActiveTex;
        Toggle.onHover.background = toggleActiveTex;
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
