using UnityEngine;

/// <summary>
/// Pantalla de opciones de partida: selección de tamaño de mapa,
/// dificultad y botones de inicio (Hospital y Túneles).
/// </summary>
public class MenuScreenPlayOptions : MonoBehaviour
{
    private MainMenuManager ctx;

    private readonly string[] mapSizes   = { "CHICO (15x15)", "MEDIANO (20x20)", "GRANDE (25x25)" };
    private readonly string[] difficulties = { "FÁCIL", "NORMAL", "DIFÍCIL" };

    private int selectedMapSizeIndex    = 0;
    private int selectedDifficultyIndex = 1;
    private int selectedCharacterIndex  = 0;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        string character = PlayerPrefs.GetString("SelectedCharacter", "Male");
        selectedCharacterIndex = (character == "Female") ? 1 : 0;
    }

    public void Draw(MenuStyles s)
    {
        // ─── Textos localizados ───────────────────────────────────────────────
        string title     = "AJUSTES DE LA PARTIDA";
        string sizeLabel = "Tamaño de Hospital:";
        string diffLabel = "Dificultad de Supervivencia:";
        string startBtn  = "  [ EMPEZAR JUEGO ]";
        string tunnelBtn = "  [ IR A LOS TÚNELES (NIVEL 2) ]";
        string backBtn   = "  VOLVER AL MENÚ";

        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.GetIdiomaActual();
            if (lang == LocalizationManager.Idioma.ENGLISH)
            {
                title     = "GAME PARAMETERS";
                sizeLabel = "Hospital Size:";
                diffLabel = "Survival Difficulty:";
                startBtn  = "  [ START GAME ]";
                tunnelBtn = "  [ GO TO TUNNELS (LEVEL 2) ]";
                backBtn   = "  BACK TO MENU";
            }
            else if (lang == LocalizationManager.Idioma.PORTUGUES)
            {
                title     = "AJUSTES DA PARTIDA";
                sizeLabel = "Tamanho do Hospital:";
                diffLabel = "Dificuldade de Sobrevivência:";
                startBtn  = "  [ INICIAR JOGO ]";
                tunnelBtn = "  [ IR PARA OS TÚNEIS (NÍVEL 2) ]";
                backBtn   = "  VOLTAR AO MENU";
            }
        }

        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(30);

        GUILayout.BeginHorizontal();

        // ─── COLUMNA IZQUIERDA: Configuraciones ───
        GUILayout.BeginVertical(GUILayout.Width(500));
        
        // Tamaño del mapa
        GUILayout.Label(sizeLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, GetLocalizedSizes(), ref selectedMapSizeIndex);

        string descSize = $"Hospital seleccionado: {mapSizes[selectedMapSizeIndex]}";
        GUILayout.Label(descSize, s.SubTitle);
        GUILayout.Space(25);

        // Dificultad
        GUILayout.Label(diffLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, GetLocalizedDiffs(), ref selectedDifficultyIndex);
        GUILayout.Label(GetDiffDescription(), s.SubTitle);
        GUILayout.Space(25);

        // Selección de personaje
        string charLabel = "Seleccionar Personaje:";
        string[] charNames = { "ETHAN", "NORA" };
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH)
        {
            charLabel = "Select Character:";
        }
        else if (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES)
        {
            charLabel = "Selecionar Personagem:";
        }

        GUILayout.Label(charLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, charNames, ref selectedCharacterIndex);

        string charDesc = selectedCharacterIndex == 0 ? 
            (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Male Character (Ethan)" : "Personaje Masculino (Ethan)") : 
            (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Female Character (Nora)" : "Personaje Femenino (Nora)");

        GUILayout.Label(charDesc, s.SubTitle);

        GUILayout.EndVertical();

        GUILayout.Space(40); // Espacio entre columnas

        // ─── COLUMNA DERECHA: Botones de Acción ───
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        var redBtn = RedButton(s);
        if (GUILayout.Button(startBtn, redBtn, GUILayout.Height(60)))
        {
            ctx.PlayClickSound();
            SaveAndLoad("Test_ModularHospital", HospitalWidth());
        }
        GUILayout.Space(20);

        if (ctx == null || ctx.enableTunnelsLevel)
        {
            var goldBtn = new GUIStyle(s.Button);
            goldBtn.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
            goldBtn.hover.textColor  = Color.white;

            if (GUILayout.Button(tunnelBtn, goldBtn, GUILayout.Height(50)))
            {
                ctx.PlayClickSound();
                SaveAndLoad("TunnelsMap", TunnelWidth());
            }
            GUILayout.Space(20);
        }

        if (GUILayout.Button(backBtn, s.Button, GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.Main);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    void DrawSelector(MenuStyles s, string[] labels, ref int index)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < labels.Length; i++)
        {
            var st = new GUIStyle(s.OptionSelect);
            st.normal.textColor = (index == i) ? Color.red : Color.gray;
            if (GUILayout.Button(labels[i], st, GUILayout.Height(40)))
            {
                ctx.PlayClickSound();
                index = i;
            }
        }
        GUILayout.EndHorizontal();
    }

    string[] GetLocalizedSizes()
    {
        if (LocalizationManager.Instance == null) return new[] { "CHICO", "MEDIANO", "GRANDE" };
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => new[] { "SMALL",   "MEDIUM", "LARGE" },
            LocalizationManager.Idioma.PORTUGUES => new[] { "PEQUENO", "MÉDIO",  "GRANDE" },
            _                                    => new[] { "CHICO",   "MEDIANO","GRANDE" }
        };
    }

    string[] GetLocalizedDiffs()
    {
        if (LocalizationManager.Instance == null) return new[] { "FÁCIL", "NORMAL", "DIFÍCIL" };
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => new[] { "EASY", "NORMAL", "HARD" },
            LocalizationManager.Idioma.PORTUGUES => new[] { "FÁCIL","NORMAL", "DIFÍCIL" },
            _                                    => new[] { "FÁCIL","NORMAL", "DIFÍCIL" }
        };
    }

    string GetDiffDescription()
    {
        bool isEN = LocalizationManager.Instance != null &&
                    LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH;
        bool isPT = LocalizationManager.Instance != null &&
                    LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES;

        return selectedDifficultyIndex switch
        {
            0 => isEN ? "The monster is slower with reduced sight. Flashlight batteries last longer."
               : isPT ? "O monstro é mais lento com visão reduzida. As baterias duram mais."
               :         "El monstruo es lento y tiene menor rango visual. Las baterías duran más tiempo.",
            2 => isEN ? "The monster is extremely fast and hears noise from far away. Flashlight drains quickly."
               : isPT ? "O monstro é extremamente rápido e ouve ruídos de longe. A lanterna acaba rápido."
               :         "El monstruo es extremadamente rápido y detecta el ruido lejano. La linterna se agota rápido.",
            _ => isEN ? "Aggressive monster. Speed, battery, and sanity calibrated for standard play."
               : isPT ? "Monstro agressivo. Velocidade, bateria e sanidade calibradas para a experiência padrão."
               :         "Monstruo agresivo. Velocidad, batería y cordura calibradas para la experiencia estándar."
        };
    }

    int HospitalWidth() => selectedMapSizeIndex switch { 1 => 20, 2 => 25, _ => 15 };
    int TunnelWidth()   => selectedMapSizeIndex switch { 1 => 25, 2 => 35, _ => 15 };

    string DiffString() => selectedDifficultyIndex switch { 0 => "FACIL", 2 => "DIFICIL", _ => "NORMAL" };

    void SaveAndLoad(string scene, int width)
    {
        PlayerPrefs.SetInt("SelectedMapSize",       width);
        PlayerPrefs.SetString("SelectedDifficulty", DiffString());
        PlayerPrefs.SetString("SelectedCharacter",  selectedCharacterIndex == 0 ? "Male" : "Female");
        PlayerPrefs.SetFloat("MouseSensitivity",    ctx.mouseSensitivity);
        PlayerPrefs.SetFloat("MasterVolume",        ctx.masterVolume);
        PlayerPrefs.SetFloat("CamcorderAccumulatedTime", 0f);
        PlayerPrefs.Save();
        MainMenuManager.startedFromMenu = true;
        SceneLoader.LoadScene(scene);
    }

    static GUIStyle RedButton(MenuStyles s)
    {
        var st = new GUIStyle(s.Button);
        st.normal.textColor = Color.red;
        st.hover.textColor  = Color.white;
        return st;
    }
}
