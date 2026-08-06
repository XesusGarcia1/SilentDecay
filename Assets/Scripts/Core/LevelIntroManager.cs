using UnityEngine;

/// <summary>
/// Clase estática auxiliar que provee los textos de introducción VHS y los monólogos iniciales
/// traduciendo todo dinámicamente mediante LocalizationManager según el personaje seleccionado (Ethan o Nora).
/// </summary>
public static class LevelIntroData
{
    public static string GetSelectedCharacter()
    {
        // Ethan es el personaje por defecto si no está definido en PlayerPrefs
        return PlayerPrefs.GetString("SelectedCharacter", "Ethan");
    }

    public static string GetIntroText(string sceneName)
    {
        string character = GetSelectedCharacter();
        string lower = sceneName.ToLower();

        if (LocalizationManager.Instance == null)
        {
            // Fallback por si no se encuentra LocalizationManager
            return GetHospitalIntroTextFallback(character);
        }

        if (lower.Contains("hospital") || lower.Contains("modular"))
        {
            return LocalizationManager.Instance.Get(character == "Nora" ? "intro_hospital_nora" : "intro_hospital_ethan");
        }
        else if (lower.Contains("tunnel") || lower.Contains("túnel"))
        {
            return LocalizationManager.Instance.Get(character == "Nora" ? "intro_tunnels_nora" : "intro_tunnels_ethan");
        }
        return null;
    }

    /// <summary>
    /// Dispara el monólogo inicial del jugador de acuerdo al personaje seleccionado y al mapa.
    /// </summary>
    public static void TriggerStartMonologue(string mapType)
    {
        string character = GetSelectedCharacter();
        if (LocalizationManager.Instance == null) return;

        if (mapType.ToLower() == "hospital")
        {
            string key = (character == "Nora") ? "monologue_hospital_nora" : "monologue_hospital_ethan";
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get(key), 6.0f);
        }
        else if (mapType.ToLower() == "tunnels")
        {
            string key = (character == "Nora") ? "monologue_tunnels_nora" : "monologue_tunnels_ethan";
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get(key), 6.0f);
        }
    }

    private static string GetHospitalIntroTextFallback(string character)
    {
        return "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n" +
               "====================================================\n" +
               "CINTA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\n" +
               "SUJETO: " + (character == "Nora" ? "NORA HAYES (INVESTIGADORA AMBIENTAL)" : "ETHAN CROSS (INSPECTOR DE INFRAESTRUCTURA)") + "\n" +
               "====================================================";
    }
}
