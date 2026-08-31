using UnityEngine;

/// <summary>
/// Pantalla de opciones de partida: selección de dificultad, personaje y botones de inicio.
/// Los tamaños de mapa ya no se seleccionan (Hospital usa mapa estático y Túneles usa Mediano por defecto).
/// </summary>
public class MenuScreenPlayOptions : MonoBehaviour
{
    private MainMenuManager ctx;

    private readonly string[] difficulties = { "FÁCIL", "NORMAL", "DIFÍCIL" };

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
        string diffLabel = "Dificultad de Supervivencia:";
        string startBtn  = "  [ EMPEZAR JUEGO ]";
        string tunnelBtn = "  [ IR A LOS TÚNELES ]";
        string backBtn   = "  VOLVER AL MENÚ";

        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.GetIdiomaActual();
            if (lang == LocalizationManager.Idioma.ENGLISH)
            {
                title     = "GAME PARAMETERS";
                diffLabel = "Survival Difficulty:";
                startBtn  = "  [ START GAME ]";
                tunnelBtn = "  [ GO TO TUNNELS ]";
                backBtn   = "  BACK TO MENU";
            }
            else if (lang == LocalizationManager.Idioma.PORTUGUES)
            {
                title     = "AJUSTES DA PARTIDA";
                diffLabel = "Dificuldade de Sobrevivência:";
                startBtn  = "  [ INICIAR JOGO ]";
                tunnelBtn = "  [ IR PARA OS TÚNEIS ]";
                backBtn   = "  VOLTAR AO MENU";
            }
            else if (lang == LocalizationManager.Idioma.РУССКИЙ)
            {
                title     = "ПАРАМЕТРЫ ИГРЫ";
                diffLabel = "Сложность выживания:";
                startBtn  = "  [ НАЧАТЬ ИГРУ ]";
                tunnelBtn = "  [ В ТОННЕЛИ ]";
                backBtn   = "  НАЗАД В МЕНЮ";
            }
        }

        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(30);

        GUILayout.BeginHorizontal();

        // ─── COLUMNA IZQUIERDA: Configuraciones ───
        GUILayout.BeginVertical(GUILayout.Width(500));

        // Dificultad
        GUILayout.Label(diffLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, GetLocalizedDiffs(), ref selectedDifficultyIndex);
        GUILayout.Space(8);
        GUILayout.Label(GetDiffDescription(), s.SubTitle);
        GUILayout.Space(30);

        // Selección de personaje
        string charLabel = "Seleccionar Personaje:";
        string[] charNames = { "ETHAN", "NORA" };
        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.GetIdiomaActual();
            if (lang == LocalizationManager.Idioma.ENGLISH) charLabel = "Select Character:";
            else if (lang == LocalizationManager.Idioma.PORTUGUES) charLabel = "Selecionar Personagem:";
            else if (lang == LocalizationManager.Idioma.РУССКИЙ) charLabel = "Выбор персонажа:";
        }

        GUILayout.Label(charLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, charNames, ref selectedCharacterIndex);
        GUILayout.Space(8);

        string charDesc = selectedCharacterIndex == 0 ? 
            (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Male Character (Ethan)" : (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.РУССКИЙ ? "Мужской персонаж (Ethan)" : "Personaje Masculino (Ethan)")) : 
            (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH ? "Female Character (Nora)" : (LocalizationManager.Instance != null && LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.РУССКИЙ ? "Женский персонаж (Nora)" : "Personaje Femenino (Nora)"));

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
            SaveAndLoad("Test_ModularHospital", 20); // Tamaño por defecto para el Hospital estático
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
                SaveAndLoad("TunnelsMap", 25); // Tamaño Mediano por defecto para Túneles
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
            st.normal.textColor = (index == i) ? s.BrandRed : Color.gray;
            if (GUILayout.Button(labels[i], st, GUILayout.Height(40)))
            {
                ctx.PlayClickSound();
                index = i;
            }
        }
        GUILayout.EndHorizontal();
    }

    string[] GetLocalizedDiffs()
    {
        if (LocalizationManager.Instance == null) return new[] { "FÁCIL", "NORMAL", "DIFÍCIL" };
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => new[] { "EASY", "NORMAL", "HARD" },
            LocalizationManager.Idioma.PORTUGUES => new[] { "FÁCIL","NORMAL", "DIFÍCIL" },
            LocalizationManager.Idioma.РУССКИЙ   => new[] { "ЛЕГКО", "НОРМАЛЬНО", "СЛОЖНО" },
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
            0 => isEN ? "Monsters are slower with reduced vision. Flashlight batteries last longer."
               : isPT ? "Os monstros são mais lentos com visão reduzida. As baterias duram mais."
               :         "Los monstruos son más lentos y tienen menor rango visual. Las baterías duran más tiempo.",
            2 => isEN ? "Monsters are extremely fast and aggressive. Flashlight drains faster."
               : isPT ? "Os monstros são extremamente rápidos e agressivos. A lanterna acaba mais rápido."
               :         "Los monstruos son extremadamente rápidos y agresivos. La linterna se agota más rápido.",
            _ => isEN ? "Standard experience. Monster speed, noise detection, and sanity calibrated for standard play."
               : isPT ? "Experiência padrão. Velocidade dos monstros, detecção de ruído e sanidade equilibradas."
               :         "Experiencia estándar. Velocidad de monstruos, detección de ruido y baterías calibradas."
        };
    }

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
        st.normal.textColor = s.BrandRed;
        st.hover.textColor  = Color.white;
        return st;
    }
}
