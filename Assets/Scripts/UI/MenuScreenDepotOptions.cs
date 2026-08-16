using UnityEngine;

/// <summary>
/// Pantalla de opciones para el Depósito Industrial.
/// El mapa es de tamaño fijo (no procedural), por lo que solo
/// muestra selección de Dificultad y Personaje.
/// Dificultad base recomendada: DIFÍCIL (mapa más avanzado que el Hospital).
/// </summary>
public class MenuScreenDepotOptions : MonoBehaviour
{
    private MainMenuManager ctx;

    // El Depósito es más difícil que el Hospital (Normal), así que arranca en Difícil (índice 2)
    private int selectedDifficultyIndex = 2;
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
        bool isEN = LocalizationManager.Instance != null &&
                    LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.ENGLISH;
        bool isPT = LocalizationManager.Instance != null &&
                    LocalizationManager.Instance.GetIdiomaActual() == LocalizationManager.Idioma.PORTUGUES;

        string title    = isEN ? "INDUSTRIAL DEPOT — GAME SETTINGS" :
                          isPT ? "DEPÓSITO INDUSTRIAL — AJUSTES"    :
                                 "DEPÓSITO INDUSTRIAL — AJUSTES DE PARTIDA";
        string diffLabel = isEN ? "Survival Difficulty:"         :
                           isPT ? "Dificuldade de Sobrevivência:" :
                                  "Dificultad de Supervivencia:";
        string charLabel = isEN ? "Select Character:"    :
                           isPT ? "Selecionar Personagem:" :
                                  "Seleccionar Personaje:";
        string startBtn  = isEN ? "  [ START GAME ]"        :
                           isPT ? "  [ INICIAR JOGO ]"       :
                                  "  [ EMPEZAR JUEGO ]";
        string backBtn   = isEN ? "  BACK"          :
                           isPT ? "  VOLTAR"         :
                                  "  VOLVER AL MENÚ";

        GUILayout.Label(title, s.SectionHeader, GUILayout.Height(30));
        GUILayout.Space(10);

        // Aviso de dificultad del mapa
        GUIStyle warningStyle = new GUIStyle(s.SubTitle);
        warningStyle.normal.textColor = new Color(1f, 0.45f, 0.1f);
        warningStyle.fontStyle = FontStyle.Bold;
        string warnMsg = isEN ? "⚠  This map is harder than the Hospital. Recommended difficulty: HARD."  :
                         isPT ? "⚠  Este mapa é mais difícil que o Hospital. Dificuldade recomendada: DIFÍCIL." :
                                "⚠  Este mapa es más difícil que el Hospital. Dificultad recomendada: DIFÍCIL.";
        GUILayout.Label(warnMsg, warningStyle, GUILayout.Height(26));
        GUILayout.Space(18);

        GUILayout.BeginHorizontal();

        // ─── COLUMNA IZQUIERDA: Selectores ─────────────────────────────────
        GUILayout.BeginVertical(GUILayout.Width(520));

        // Dificultad
        GUILayout.Label(diffLabel, s.Label);
        GUILayout.Space(5);
        string[] diffs = isEN ? new[] { "EASY", "NORMAL", "HARD" } :
                         new[] { "FÁCIL", "NORMAL", "DIFÍCIL" };
        DrawSelector(s, diffs, ref selectedDifficultyIndex);
        GUILayout.Label(GetDiffDescription(isEN, isPT), s.SubTitle);
        GUILayout.Space(25);

        // Personaje
        GUILayout.Label(charLabel, s.Label);
        GUILayout.Space(5);
        DrawSelector(s, new[] { "ETHAN", "NORA" }, ref selectedCharacterIndex);
        string charDesc = selectedCharacterIndex == 0
            ? (isEN ? "Male Character (Ethan)" : "Personaje Masculino (Ethan)")
            : (isEN ? "Female Character (Nora)" : "Personaje Femenino (Nora)");
        GUILayout.Label(charDesc, s.SubTitle);

        GUILayout.EndVertical();

        GUILayout.Space(40);

        // ─── COLUMNA DERECHA: Botones de Acción ─────────────────────────────
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        var redBtn = new GUIStyle(s.Button);
        redBtn.normal.textColor = Color.red;
        redBtn.hover.textColor  = Color.white;

        if (GUILayout.Button(startBtn, redBtn, GUILayout.Height(60)))
        {
            ctx.PlayClickSound();
            SaveAndLoadDepot();
        }
        GUILayout.Space(20);

        if (GUILayout.Button(backBtn, s.Button, GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.LevelSelect);
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

    string GetDiffDescription(bool isEN, bool isPT)
    {
        return selectedDifficultyIndex switch
        {
            0 => isEN ? "The Replica is slower with reduced detection range. Easier escape routes."
               : isPT ? "A Réplica é mais lenta com menor alcance. Rotas de fuga mais fáceis."
               :         "La Réplica es más lenta y con menor rango de detección. Escapes más accesibles.",
            2 => isEN ? "The Replica is fast and relentless. Minimal mistakes allowed. High tension."
               : isPT ? "A Réplica é rápida e implacável. Erros mínimos permitidos. Alta tensão."
               :         "La Réplica es rápida y despiadada. Se permiten mínimos errores. Máxima tensión.",
            _ => isEN ? "Aggressive Replica. Speed and detection calibrated for a standard tense experience."
               : isPT ? "Réplica agressiva. Velocidade e detecção para experiência padrão tensa."
               :         "Réplica agresiva. Velocidad y detección calibradas para una experiencia de tensión estándar."
        };
    }

    string DiffString() => selectedDifficultyIndex switch { 0 => "FACIL", 2 => "DIFICIL", _ => "NORMAL" };

    void SaveAndLoadDepot()
    {
        PlayerPrefs.SetInt("SelectedMapSize",              0); // Mapa fijo, no aplica
        PlayerPrefs.SetString("SelectedDifficulty",        DiffString());
        PlayerPrefs.SetString("SelectedCharacter",         selectedCharacterIndex == 0 ? "Male" : "Female");
        PlayerPrefs.SetFloat("MouseSensitivity",           ctx.mouseSensitivity);
        PlayerPrefs.SetFloat("MasterVolume",               ctx.masterVolume);
        PlayerPrefs.SetFloat("CamcorderAccumulatedTime",   0f);
        PlayerPrefs.Save();
        MainMenuManager.startedFromMenu = true;
        SceneLoader.LoadScene("IndustrialDepotMap");
    }
}
