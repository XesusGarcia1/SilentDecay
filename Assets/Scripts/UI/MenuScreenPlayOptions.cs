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

    private string targetScene = "Test_ModularHospital";
    private string targetLevelName = "NIVEL 1: HOSPITAL";

    public void SetTargetLevel(string sceneName, string levelName)
    {
        targetScene = sceneName;
        targetLevelName = levelName;
    }

    public void Draw(MenuStyles s)
    {
        // ─── Textos localizados ───────────────────────────────────────────────
        string title     = GetLocalized("AJUSTES DE LA PARTIDA", "GAME PARAMETERS", "AJUSTES DA PARTIDA", "ПАРАМЕТРЫ ИГРЫ");
        string diffLabel = GetLocalized("Dificultad de Supervivencia:", "Survival Difficulty:", "Dificuldade de Sobrevivência:", "Сложность выживания:");
        string startBtn  = GetLocalized("  [ EMPEZAR JUEGO ]", "  [ START GAME ]", "  [ INICIAR JOGO ]", "  [ НАЧАТЬ ИГРУ ]");
        string backBtn   = GetLocalized("  VOLVER A SELECCIÓN", "  BACK TO SELECTION", "  VOLTAR À SELEÇÃO", "  НАЗАД К ВЫБОРУ");

        GUILayout.Label($"{title}  •  {targetLevelName}", s.SectionHeader, GUILayout.Height(30));
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
        string charLabel = GetLocalized("Seleccionar Personaje:", "Select Character:", "Selecionar Personagem:", "Выбор персонажа:");
        string[] charNames = { "ETHAN", "NORA" };

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
        if (GUILayout.Button(startBtn, redBtn, GUILayout.Height(65)))
        {
            ctx.PlayClickSound();
            int defaultSize = (targetScene == "TunnelsMap") ? 25 : 20;
            SaveAndLoad(targetScene, defaultSize);
        }
        GUILayout.Space(25);

        if (GUILayout.Button(backBtn, s.Button, GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.LevelSelect);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private string GetLocalized(string es, string en, string pt, string ru)
    {
        if (LocalizationManager.Instance == null) return es;
        return LocalizationManager.Instance.GetIdiomaActual() switch
        {
            LocalizationManager.Idioma.ENGLISH   => en,
            LocalizationManager.Idioma.PORTUGUES => pt,
            LocalizationManager.Idioma.РУССКИЙ   => ru,
            _                                    => es
        };
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
