using UnityEngine;

/// <summary>
/// Clase estática auxiliar que provee los textos de introducción VHS y los monólogos iniciales
/// traduciendo todo dinámicamente mediante LocalizationManager según el personaje seleccionado (Male o Female).
/// </summary>
public static class LevelIntroData
{
    public static string GetSelectedCharacter()
    {
        // "Male" es el personaje por defecto (Ethan) si no está definido en PlayerPrefs
        return PlayerPrefs.GetString("SelectedCharacter", "Male");
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
            return LocalizationManager.Instance.Get(character == "Female" ? "intro_hospital_nora" : "intro_hospital_ethan");
        }
        else if (lower.Contains("tunnel") || lower.Contains("túnel"))
        {
            return LocalizationManager.Instance.Get(character == "Female" ? "intro_tunnels_nora" : "intro_tunnels_ethan");
        }
        else if (lower.Contains("industrial") || lower.Contains("depot"))
        {
            return LocalizationManager.Instance.Get(character == "Female" ? "intro_depot_nora" : "intro_depot_ethan");
        }
        else if (lower.Contains("tut") || lower.Contains("tutorial"))
        {
            return LocalizationManager.Instance.Get("intro_tutorial");
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
            string key = (character == "Female") ? "monologue_hospital_nora" : "monologue_hospital_ethan";
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get(key), 6.0f);
        }
        else if (mapType.ToLower() == "tunnels")
        {
            string key = (character == "Female") ? "monologue_tunnels_nora" : "monologue_tunnels_ethan";
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get(key), 6.0f);
        }
        else if (mapType.ToLower() == "industrial" || mapType.ToLower() == "depot")
        {
            string key = (character == "Female") ? "monologue_depot_nora" : "monologue_depot_ethan";
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get(key), 6.0f);
        }
    }

    private static string GetHospitalIntroTextFallback(string character)
    {
        return "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n" +
               "====================================================\n" +
               "CINTA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\n" +
               "SUJETO: " + (character == "Female" ? "NORA HAYES (INVESTIGADORA AMBIENTAL)" : "ETHAN CROSS (INSPECTOR DE INFRAESTRUCTURA)") + "\n" +
               "====================================================";
    }
}

