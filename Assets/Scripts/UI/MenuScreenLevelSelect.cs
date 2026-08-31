using UnityEngine;

/// <summary>
/// Pantalla de selección de mapa estilo Carrusel:
/// - Nivel 1: Hospital Modular (Desbloqueado por defecto)
/// - Nivel 2: Túneles (Requiere pasar Hospital)
/// - Nivel 3: Depósito Industrial (Requiere pasar Túneles)
/// - Nivel 4: Bosque (Próximamente / Bloqueado)
/// </summary>
public class MenuScreenLevelSelect : MonoBehaviour
{
    private MainMenuManager ctx;
    private Texture2D texHospitalThumb;
    private Texture2D texTunnelsThumb;
    private Texture2D texDepotThumb;

    private int currentLevelIndex = 0;
    private const int TOTAL_LEVELS = 4;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        texHospitalThumb = Resources.Load<Texture2D>("Texturas/UI/game1");
        texTunnelsThumb  = Resources.Load<Texture2D>("Texturas/UI/game01");
        if (texTunnelsThumb == null) texTunnelsThumb = Resources.Load<Texture2D>("UI/GuieMapTunnels");
        texDepotThumb    = Resources.Load<Texture2D>("Texturas/UI/game2");
    }

    public void Draw(MenuStyles s)
    {
        // Soporte para flechas de teclado / Gamepad
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.A)
            {
                PrevLevel();
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.RightArrow || Event.current.keyCode == KeyCode.D)
            {
                NextLevel();
                Event.current.Use();
            }
        }

        // ─── Título Superior ──────────────────────────────────────────────────
        string mainTitle = GetLocalized("SELECCIÓN DE CAPÍTULO / MAPA", "CHAPTER / MAP SELECTION", "SELEÇÃO DE CAPÍTULO / MAPA", "ВЫБОР ГЛАВЫ / КАРТЫ");
        GUILayout.Label(mainTitle, s.SectionHeader, GUILayout.Height(35));
        GUILayout.Space(10);

        // ─── Fila Central: Flecha Izq + Tarjeta Grande + Flecha Der ───────────
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Botón Flecha Izquierda [ < ]
        GUIStyle arrowBtn = new GUIStyle(s.Button);
        arrowBtn.fontSize = 34;
        arrowBtn.fontStyle = FontStyle.Bold;
        arrowBtn.normal.textColor = currentLevelIndex > 0 ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.5f);

        GUILayout.BeginVertical(GUILayout.Width(75));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("◄", arrowBtn, GUILayout.Width(70), GUILayout.Height(100)))
        {
            PrevLevel();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.Space(20);

        // ─── Tarjeta Central Grande del Nivel Seleccionado ───
        DrawCurrentLevelCard(s);

        GUILayout.Space(20);

        // Botón Flecha Derecha [ > ]
        arrowBtn.normal.textColor = currentLevelIndex < TOTAL_LEVELS - 1 ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        GUILayout.BeginVertical(GUILayout.Width(75));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("►", arrowBtn, GUILayout.Width(70), GUILayout.Height(100)))
        {
            NextLevel();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(14);

        // ─── Indicador de Paginación (● ○ ○ ○) y Contador de Nivel ───────────
        DrawCarouselIndicators(s);

        GUILayout.Space(14);

        // ─── Botón Volver al Menú Principal ──────────────────────────────────
        string backBtn = GetLocalized("  ATRÁS", "  BACK", "  VOLTAR", "  НАЗАД");
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(backBtn, s.Button, GUILayout.Width(620), GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            ctx.GoTo(MainMenuManager.MenuState.Main);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void PrevLevel()
    {
        ctx.PlayClickSound();
        currentLevelIndex = (currentLevelIndex - 1 + TOTAL_LEVELS) % TOTAL_LEVELS;
    }

    private void NextLevel()
    {
        ctx.PlayClickSound();
        currentLevelIndex = (currentLevelIndex + 1) % TOTAL_LEVELS;
    }

    // ─── Renderizado de la Tarjeta Actual ─────────────────────────────────────
    private void DrawCurrentLevelCard(MenuStyles s)
    {
        switch (currentLevelIndex)
        {
            case 0:
                DrawHospitalCard(s);
                break;
            case 1:
                DrawTunnelsCard(s);
                break;
            case 2:
                DrawDepotCard(s);
                break;
            case 3:
                DrawForestCard(s);
                break;
        }
    }

    // ─── Nivel 1: Hospital Modular ────────────────────────────────────────────
    private void DrawHospitalCard(MenuStyles s)
    {
        string lvlTitle = GetLocalized("NIVEL 1: HOSPITAL MODULAR", "LEVEL 1: MODULAR HOSPITAL", "NÍVEL 1: HOSPITAL MODULAR", "УРОВЕНЬ 1: БОЛЬНИЦА");
        string lvlDesc  = GetLocalized(
            "Un antiguo hospital psiquiátrico abandonado. Restablece la energía de la caja de fusibles, encuentra la tarjeta del director y escapa en el ascensor.",
            "An abandoned mental hospital. Restore power at the breaker box, retrieve the director's keycard and escape through the elevator.",
            "Um antigo hospital psiquiátrico abandonado. Restaure a energia, encontre o cartão do diretor e fuja pelo elevador.",
            "Заброшенная психиатрическая больница. Восстановите питание, найдите ключ-карту директора и спаситесь на лифте."
        );
        string diffBadge = GetLocalized("DIFICULTAD: FÁCIL / NORMAL", "DIFFICULTY: EASY / NORMAL", "DIFICULDADE: FÁCIL / NORMAL", "СЛОЖНОСТЬ: ЛЕГКО / НОРМАЛЬНО");
        string playBtnText = GetLocalized("JUGAR CAPÍTULO 1", "PLAY CHAPTER 1", "JOGAR CAPÍTULO 1", "ИГРАТЬ ГЛАВУ 1");

        RenderActiveCard(s, texHospitalThumb, lvlTitle, lvlDesc, diffBadge, new Color(0.3f, 0.9f, 0.4f), playBtnText, () =>
        {
            ctx.screenPlayOptions.SetTargetLevel("Test_ModularHospital", GetLocalized("NIVEL 1: HOSPITAL", "LEVEL 1: HOSPITAL", "NÍVEL 1: HOSPITAL", "УРОВЕНЬ 1: БОЛЬНИЦА"));
            ctx.GoTo(MainMenuManager.MenuState.PlayOptions);
        });
    }

    // ─── Nivel 2: Túneles Subterráneos ────────────────────────────────────────
    private void DrawTunnelsCard(MenuStyles s)
    {
        bool isUnlocked = (ctx != null && ctx.unlockAllMapsDebug) || PlayerPrefs.GetInt("Campaign_HospitalCompleted", 0) == 1;

        string lvlTitle = GetLocalized("NIVEL 2: TÚNELES INUNDADOS", "LEVEL 2: FLOODED TUNNELS", "NÍVEL 2: TÚNEIS INUNDADOS", "УРОВЕНЬ 2: ЗАТОПЛЕННЫЕ ТОННЕЛИ");
        string lvlDesc  = GetLocalized(
            "Los pasajes inferiores bajo el hospital. Encuentra y activa los 3 subgeneradores de respaldo, purga las bombas de agua y evacúa por la escotilla.",
            "Subterranean tunnels beneath the hospital. Locate and activate the 3 backup subgenerators, purge the water pumps and evacuate via the hatch.",
            "Passagens subterrâneas sob o hospital. Ative os 3 subgeradores, drene as bombas de água e fuja pela escotilha.",
            "Подземные туннели под больницей. Найдите и включите 3 субгенератора, откачайте воду и эвакуируйтесь через люк."
        );
        string diffBadge = GetLocalized("DIFICULTAD: NORMAL / DIFÍCIL", "DIFFICULTY: NORMAL / HARD", "DIFICULDADE: NORMAL / DIFÍCIL", "СЛОЖНОСТЬ: НОРМАЛЬНО / СЛОЖНО");
        string playBtnText = GetLocalized("JUGAR CAPÍTULO 2", "PLAY CHAPTER 2", "JOGAR CAPÍTULO 2", "ИГРАТЬ ГЛАВУ 2");
        string lockReqText = GetLocalized("COMPLETA EL HOSPITAL (NIVEL 1) PARA DESBLOQUEAR", "COMPLETE HOSPITAL (LEVEL 1) TO UNLOCK", "COMPLETE O HOSPITAL (NÍVEL 1) PARA DESBLOQUEAR", "ПРОЙДИТЕ БОЛЬНИЦУ (УРОВЕНЬ 1) ДЛЯ РАЗБЛОКИРОВКИ");

        if (isUnlocked)
        {
            RenderActiveCard(s, texTunnelsThumb, lvlTitle, lvlDesc, diffBadge, new Color(0.95f, 0.7f, 0.2f), playBtnText, () =>
            {
                ctx.screenPlayOptions.SetTargetLevel("TunnelsMap", GetLocalized("NIVEL 2: TÚNELES", "LEVEL 2: TUNNELS", "NÍVEL 2: TÚNEIS", "УРОВЕНЬ 2: ТОННЕЛИ"));
                ctx.GoTo(MainMenuManager.MenuState.PlayOptions);
            });
        }
        else
        {
            RenderLockedCard(s, texTunnelsThumb, lvlTitle, lvlDesc, lockReqText);
        }
    }

    // ─── Nivel 3: Depósito Industrial ─────────────────────────────────────────
    private void DrawDepotCard(MenuStyles s)
    {
        bool isUnlocked = (ctx != null && ctx.unlockAllMapsDebug) || PlayerPrefs.GetInt("Campaign_TunnelsCompleted", 0) == 1;

        string lvlTitle = GetLocalized("NIVEL 3: DEPÓSITO INDUSTRIAL", "LEVEL 3: INDUSTRIAL DEPOT", "NÍVEL 3: DEPÓSITO INDUSTRIAL", "УРОВЕНЬ 3: ПРОМЫШЛЕННЫЙ СКЛАД");
        string lvlDesc  = GetLocalized(
            "Almacén de carga pesada y maquinaria oxidada. Un laberinto de contenedores acechado por entidades hostiles en la oscuridad absoluta.",
            "Heavy cargo depot and rusted machinery. A container maze stalked by hostile entities in pitch black darkness.",
            "Depósito de carga pesada e máquinas enferrujadas. Um labirinto de contêineres cercado por entidades na escuridão.",
            "Склад тяжелых грузов и ржавых машин. Лабиринт контейнеров, где во тьме рыщут враждебные сущности."
        );
        string diffBadge = GetLocalized("DIFICULTAD: ⚠ DIFÍCIL / EXPERTO", "DIFFICULTY: ⚠ HARD / EXPERT", "DIFICULDADE: ⚠ DIFÍCIL / EXPERT", "СЛОЖНОСТЬ: ⚠ СЛОЖНО / ЭКСПЕРТ");
        string playBtnText = GetLocalized("JUGAR CAPÍTULO 3", "PLAY CHAPTER 3", "JOGAR CAPÍTULO 3", "ИГРАТЬ ГЛАВУ 3");
        string lockReqText = GetLocalized("COMPLETA LOS TÚNELES (NIVEL 2) PARA DESBLOQUEAR", "COMPLETE TUNNELS (LEVEL 2) TO UNLOCK", "COMPLETE OS TÚNEIS (NÍVEL 2) PARA DESBLOQUEAR", "ПРОЙДИТЕ ТОННЕЛИ (УРОВЕНЬ 2) ДЛЯ РАЗБЛОКИРОВКИ");

        if (isUnlocked)
        {
            RenderActiveCard(s, texDepotThumb, lvlTitle, lvlDesc, diffBadge, new Color(0.95f, 0.35f, 0.2f), playBtnText, () =>
            {
                ctx.GoTo(MainMenuManager.MenuState.DepotOptions);
            });
        }
        else
        {
            RenderLockedCard(s, texDepotThumb, lvlTitle, lvlDesc, lockReqText);
        }
    }

    // ─── Nivel 4: Bosque (Próximamente) ───────────────────────────────────────
    private void DrawForestCard(MenuStyles s)
    {
        string lvlTitle = GetLocalized("NIVEL 4: BOSQUE", "LEVEL 4: FOREST", "NÍVEL 4: FLORESTA", "УРОВЕНЬ 4: ЛЕС");
        string lvlDesc  = GetLocalized(
            "Espesos bosques nocturnos rodeados por niebla impenetrable. Nuevos peligros y enigmas esperan en la próxima actualización.",
            "Dense nighttime woodlands surrounded by impenetrable fog. New threats and mysteries await in the next update.",
            "Florestas noturnas densas cercadas por névoa impenetrável. Novos perigos aguardam na próxima atualização.",
            "Густые ночные леса, окутанные непроглядным туманом. Новые опасности ждут в следующем обновлении."
        );
        string lockReqText = GetLocalized("🔒 PRÓXIMAMENTE EN PRÓXIMA ACTUALIZACIÓN", "🔒 COMING SOON IN FUTURE UPDATE", "🔒 EM BREVE NA PRÓXIMA ATUALIZAÇÃO", "🔒 СКОРО В СЛЕДУЮЩЕМ ОБНОВЛЕНИИ");

        RenderLockedCard(s, null, lvlTitle, lvlDesc, lockReqText);
    }

    // ─── Plantilla de Tarjeta Activa (Desbloqueada) ───────────────────────────
    private void RenderActiveCard(MenuStyles s, Texture2D thumbnail, string title, string description, string diffBadge, Color badgeColor, string playBtnText, System.Action onPlay)
    {
        GUIStyle titleStyle = new GUIStyle(s.Label);
        titleStyle.fontSize = 23;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;

        GUIStyle descStyle = new GUIStyle(s.Label);
        descStyle.fontSize = 14;
        descStyle.alignment = TextAnchor.MiddleCenter;
        descStyle.wordWrap = true;
        descStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        GUIStyle badgeStyle = new GUIStyle(s.Label);
        badgeStyle.fontSize = 13;
        badgeStyle.fontStyle = FontStyle.Bold;
        badgeStyle.alignment = TextAnchor.MiddleCenter;
        badgeStyle.normal.textColor = badgeColor;

        GUIStyle playBtn = new GUIStyle(s.Button);
        playBtn.fontSize = 20;
        playBtn.normal.textColor = s.BrandRed;
        playBtn.hover.textColor  = Color.white;

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(600), GUILayout.Height(450));
        GUILayout.Space(10);

        // Miniatura
        Rect thumbRect = GUILayoutUtility.GetRect(576f, 235f);
        GUI.DrawTexture(thumbRect, thumbnail != null ? thumbnail : Texture2D.blackTexture, ScaleMode.StretchToFill);

        GUILayout.Space(8);
        GUILayout.Label(title, titleStyle, GUILayout.Height(28));
        GUILayout.Label(diffBadge, badgeStyle, GUILayout.Height(20));
        GUILayout.Space(4);
        GUILayout.Label(description, descStyle, GUILayout.Height(45));
        GUILayout.Space(10);

        if (GUILayout.Button(playBtnText, playBtn, GUILayout.Height(50)))
        {
            ctx.PlayClickSound();
            onPlay?.Invoke();
        }

        GUILayout.EndVertical();
    }

    // ─── Plantilla de Tarjeta Bloqueada ───────────────────────────────────────
    private void RenderLockedCard(MenuStyles s, Texture2D thumbnail, string title, string description, string lockReason)
    {
        GUIStyle titleStyle = new GUIStyle(s.Label);
        titleStyle.fontSize = 23;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        GUIStyle descStyle = new GUIStyle(s.Label);
        descStyle.fontSize = 14;
        descStyle.alignment = TextAnchor.MiddleCenter;
        descStyle.wordWrap = true;
        descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);

        GUIStyle lockBanner = new GUIStyle(GUI.skin.box);
        lockBanner.alignment = TextAnchor.MiddleCenter;

        GUIStyle lockText = new GUIStyle(s.Label);
        lockText.fontSize = 13;
        lockText.fontStyle = FontStyle.Bold;
        lockText.alignment = TextAnchor.MiddleCenter;
        lockText.normal.textColor = new Color(0.95f, 0.45f, 0.45f);

        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(600), GUILayout.Height(450));
        GUILayout.Space(10);

        // Miniatura oscurecida con candado
        Rect thumbRect = GUILayoutUtility.GetRect(576f, 235f);
        Color oldC = GUI.color;
        GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        GUI.DrawTexture(thumbRect, thumbnail != null ? thumbnail : Texture2D.blackTexture, ScaleMode.StretchToFill);
        GUI.color = oldC;

        // Ícono de candado centrado sobre la imagen
        GUIStyle bigLock = new GUIStyle(s.Label);
        bigLock.fontSize = 65;
        bigLock.alignment = TextAnchor.MiddleCenter;
        bigLock.normal.textColor = new Color(0.95f, 0.35f, 0.35f, 0.9f);
        GUI.Label(thumbRect, "🔒", bigLock);

        GUILayout.Space(8);
        GUILayout.Label(title, titleStyle, GUILayout.Height(28));
        GUILayout.Label(description, descStyle, GUILayout.Height(45));
        GUILayout.Space(10);

        // Barra de requerimiento de desbloqueo
        GUILayout.BeginHorizontal(lockBanner, GUILayout.Height(50));
        GUILayout.Label(lockReason, lockText);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ─── Indicadores de Paginación del Carrusel (● ○ ○ ○) ─────────────────────
    private void DrawCarouselIndicators(MenuStyles s)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle dotStyle = new GUIStyle(s.Label);
        dotStyle.fontSize = 18;
        dotStyle.alignment = TextAnchor.MiddleCenter;

        string dots = "";
        for (int i = 0; i < TOTAL_LEVELS; i++)
        {
            if (i == currentLevelIndex)
            {
                dots += " <color=#E53935>●</color> ";
            }
            else
            {
                dots += " <color=#777777>○</color> ";
            }
        }

        string levelCounter = GetLocalized(
            $"CAPÍTULO {currentLevelIndex + 1} DE {TOTAL_LEVELS}",
            $"CHAPTER {currentLevelIndex + 1} OF {TOTAL_LEVELS}",
            $"CAPÍTULO {currentLevelIndex + 1} DE {TOTAL_LEVELS}",
            $"ГЛАВА {currentLevelIndex + 1} ИЗ {TOTAL_LEVELS}"
        );

        GUIStyle counterStyle = new GUIStyle(s.Label);
        counterStyle.fontSize = 14;
        counterStyle.fontStyle = FontStyle.Bold;
        counterStyle.alignment = TextAnchor.MiddleCenter;
        counterStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        GUILayout.BeginVertical();
        GUILayout.Label(dots, dotStyle, GUILayout.Height(22));
        GUILayout.Label(levelCounter, counterStyle, GUILayout.Height(18));
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
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
}
