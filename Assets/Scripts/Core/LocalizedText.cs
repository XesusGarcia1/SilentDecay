using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    [Header("Clave de Localización")]
    [Tooltip("Clave única definida en el LocalizationManager")]
    public string key;

    // Componentes de texto soportados
    private TextMeshProUGUI tmpGuiText;
    private TextMeshPro tmpText3D;
    private UnityEngine.UI.Text legacyUiText;
    private TextMesh legacyText3D;

    private void Awake()
    {
        // Encontrar cualquier componente de texto asignado a este objeto
        tmpGuiText = GetComponent<TextMeshProUGUI>();
        tmpText3D = GetComponent<TextMeshPro>();
        legacyUiText = GetComponent<UnityEngine.UI.Text>();
        legacyText3D = GetComponent<TextMesh>();
    }

    private void OnEnable()
    {
        // Escuchar el evento de cambio de idioma
        LocalizationManager.OnLanguageChanged += ActualizarTexto;
        ActualizarTexto();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= ActualizarTexto;
    }

    public void ActualizarTexto()
    {
        if (LocalizationManager.Instance == null) return;
        if (string.IsNullOrEmpty(key)) return;

        string translatedText = LocalizationManager.Instance.Get(key);

        // Asignar el texto traducido al componente correspondiente
        if (tmpGuiText != null) tmpGuiText.text = translatedText;
        else if (tmpText3D != null) tmpText3D.text = translatedText;
        else if (legacyUiText != null) legacyUiText.text = translatedText;
        else if (legacyText3D != null) legacyText3D.text = translatedText;
    }
}
