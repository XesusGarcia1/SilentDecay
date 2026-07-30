using UnityEngine;

/// <summary>
/// Estilos GUI reutilizables compartidos entre todas las pantallas del menú.
/// Se instancia una vez por frame en OnGUI para evitar allocations.
/// </summary>
public class MenuStyles
{
    public readonly GUIStyle Title;
    public readonly GUIStyle SubTitle;
    public readonly GUIStyle SectionHeader;
    public readonly GUIStyle Label;
    public readonly GUIStyle Button;
    public readonly GUIStyle OptionSelect;

    public MenuStyles()
    {
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
            fontSize  = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        Button.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        Button.hover.textColor  = Color.red;
        Button.active.textColor = Color.red;

        OptionSelect = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }
}
