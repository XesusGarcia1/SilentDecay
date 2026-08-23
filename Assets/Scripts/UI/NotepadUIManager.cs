using UnityEngine;
using System.Collections.Generic;

public class NotepadUIManager : MonoBehaviour
{
    public static NotepadUIManager Instance { get; private set; }
    public static bool IsOpen { get; private set; } = false;

    [Header("Configuración de Libreta")]
    [Tooltip("Dígitos de clave descubiertos (1 a 7)")]
    public static int[] foundNotes = new int[] { -1, -1, -1, -1, -1, -1, -1 };
    
    [Tooltip("Mostrar punto verde 'TÚ' del jugador en el mapa (desactivado por requerimiento)")]
    public bool showPlayerPositionOnMap = false;

    // --- ARCHIVO DE HISTORIA ---
    public struct LoreNoteData
    {
        public int id;
        public string title;
        public string body;
    }
    public static Dictionary<int, LoreNoteData> collectedLoreNotes = new Dictionary<int, LoreNoteData>();
    private int selectedLoreNoteId = -1;

    public static bool isReadingFullscreen = false;
    public static string fullscreenTitle = "";
    public static string fullscreenBody = "";
    public static float openTime = 0f;

    private int activeTab = 0; // 0 = Notas, 1 = Mapa, 2 = Archivo Lore
    
    public float currentUIScale
    {
        get
        {
            float baseScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
            #if UNITY_ANDROID || UNITY_IOS
            return baseScale * 1.15f;
            #else
            return baseScale;
            #endif
        }
    }

    private Transform playerTransform;
    private static HashSet<Vector2Int> discoveredRooms = new HashSet<Vector2Int>();

    public static void ResetNotepadData()
    {
        discoveredRooms.Clear();
        collectedLoreNotes.Clear();
        for (int i = 0; i < foundNotes.Length; i++)
        {
            foundNotes[i] = -1;
        }
        Debug.Log("NotepadUIManager: Datos de libreta, notas de lore y mapa reseteados.");
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    [RuntimeInitializeOnLoadMethod]
    private static void AutoInitialize()
    {
        if (Instance == null && FindObjectOfType<NotepadUIManager>() == null)
        {
            GameObject go = new GameObject("[NotepadUIManager]");
            go.AddComponent<NotepadUIManager>();
            DontDestroyOnLoad(go);
        }
    }

    private bool ShouldSuppressNotepadUI()
    {
        // 1. Si el juego está pausado
        if (Time.timeScale == 0f) return true;

        // 2. Si estamos en la escena de carga o menú principal
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "LoadingScene" || currentScene == "MainMenu") return true;

        // 3. Si hay una pantalla de carga (SceneLoader) activa en escena
        if (FindObjectOfType<SceneLoader>() != null) return true;

        // 4. Si el generador está en modo menú
        var generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (generator != null && generator.isMenuMode) return true;

        return false;
    }

    void Update()
    {
        if (ShouldSuppressNotepadUI())
        {
            if (IsOpen) CloseNotepad();
            return;
        }

        // Escuchar únicamente la tecla TAB o botón táctil para la libreta
        if (Input.GetKeyDown(KeyCode.Tab) || MobileInput.GetKeyDown(KeyCode.Tab))
        {
            ToggleNotepad();
        }

        // Descubrir celdas del mapa continuamente en segundo plano mientras se explora el hospital
        TrackMapExploration();
    }

    private void TrackMapExploration()
    {
        int[,] grid = GetFallbackHospitalGridMatrix();
        if (grid != null)
        {
            if (playerTransform == null) FindPlayer();
            if (playerTransform == null) return;

            var gen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
            int sX = grid.GetLength(0);
            int sZ = grid.GetLength(1);

            Vector3 basePos = (gen != null) ? gen.transform.position : Vector3.zero;
            float halfW = (sX * 4.0f) / 2.0f;
            float halfD = (sZ * 4.0f) / 2.0f;
            Vector3 pLocal = playerTransform.position - basePos;
            int pGX = Mathf.Clamp(Mathf.RoundToInt((pLocal.x + halfW - 2.0f) / 4.0f), 0, sX - 1);
            int pGZ = Mathf.Clamp(Mathf.RoundToInt((pLocal.z + halfD - 2.0f) / 4.0f), 0, sZ - 1);

            // Revelar celdas al explorar por proximidad (Radio 1 casilla)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = pGX + dx;
                    int cz = pGZ + dz;
                    if (cx >= 0 && cx < sX && cz >= 0 && cz < sZ)
                    {
                        discoveredRooms.Add(new Vector2Int(cx, cz));
                    }
                }
            }
        }
    }

    public void ToggleNotepad()
    {
        if (IsOpen) CloseNotepad();
        else OpenNotepad();
    }

    public void OpenNotepad()
    {
        FindPlayer();
        IsOpen = true;

        if (playerTransform != null)
        {
            var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
            if (controller != null) controller.enabled = false;

            var playerInputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (playerInputs != null)
            {
                playerInputs.cursorLocked = false;
                playerInputs.cursorInputForLook = false;
                playerInputs.look = Vector2.zero;
            }
        }

        MobileInput.SetCursorState(false);
    }

    public void CloseNotepad()
    {
        IsOpen = false;

        FindPlayer();
        if (playerTransform != null)
        {
            var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
            if (controller != null) controller.enabled = true;

            var playerInputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (playerInputs != null)
            {
                playerInputs.cursorLocked = true;
                playerInputs.cursorInputForLook = true;
            }
        }

        MobileInput.SetCursorState(true);
    }

    public static void RegisterLoreNote(int id, string title, string body)
    {
        if (!collectedLoreNotes.ContainsKey(id))
        {
            LoreNoteData data = new LoreNoteData { id = id, title = title, body = body };
            collectedLoreNotes.Add(id, data);
            Debug.Log($"NotepadUIManager: Documento de lore registrado #{id}: '{title}'");
        }
    }

    public static void RegisterNote(int pos, int val)
    {
        if (pos >= 1 && pos <= 7)
        {
            foundNotes[pos - 1] = val;
            Debug.Log($"NotepadUIManager: Registrada nota pos {pos} = {val}");
        }
    }

    void FindPlayer()
    {
        if (playerTransform != null) return;

        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null)
        {
            playerTransform = cc.transform;
            return;
        }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null)
        {
            playerTransform = pObj.transform;
            return;
        }

        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null)
        {
            playerTransform = playerTagObj.transform;
            return;
        }

        if (Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void OnGUI()
    {
        if (isReadingFullscreen)
        {
            DrawFullscreenReadingFromNotepad();
            return;
        }

        if (ShouldSuppressNotepadUI()) return;

        if (!IsOpen)
        {
            var generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
            if (generator != null && generator.isMenuMode) return;

            // Reubicar botón de Libreta abajo del HUD de subgeneradores en la derecha, compacto y oscuro
            int numGens = 0;
            SubGenerator[] subGens = FindObjectsOfType<SubGenerator>();
            if (subGens != null && subGens.Length > 0)
            {
                numGens = subGens.Length;
            }

            float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
            Matrix4x4 oldHudMat = GUI.matrix;
            if (hudScale != 1.0f)
            {
                Vector2 pivot = new Vector2(Screen.width - 25, 25);
                GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
            }

            float yPos = (numGens > 0) ? (98f + 65f + 8f) : 98f;
            float btnSize = 50f;
            Rect iconRect = new Rect(Screen.width - 25f - btnSize, yPos, btnSize, btnSize);

            // Fondo semitransparente oscuro unificado (como fusibles y subgeneradores)
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle iconStyle = new GUIStyle(GUI.skin.button);
            iconStyle.normal.background = null;
            iconStyle.hover.background = null;
            iconStyle.active.background = null;

            if (GUI.Button(iconRect, GUIContent.none, iconStyle))
            {
                OpenNotepad();
            }
            Texture2D nbTex = GetNotebookTexture();
            Rect nbIconPadding = new Rect(iconRect.x + 4, iconRect.y + 4, iconRect.width - 8, iconRect.height - 8);
            if (nbTex != null) GUI.DrawTexture(nbIconPadding, nbTex, ScaleMode.ScaleToFit, true);

            GUI.matrix = oldHudMat;
            return;
        }

        // Evitar dibujar si el mapa de hospital de la escena actual no está disponible
        ModularHospital.ModularHospitalGenerator gen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (gen != null && gen.isMenuMode) return;
        
        // Aplicar escalado global a toda la libreta para móviles
        Matrix4x4 oldMatrix = GUI.matrix;
        float padScale = this.currentUIScale;
        if (padScale > 1.0f)
        {
            Vector2 pivot = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GUIUtility.ScaleAroundPivot(new Vector2(padScale, padScale), pivot);
        }

        Rect padRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 220, 400, 440);
        
        GUI.DrawTexture(padRect, ProceduralPaperTexture.GetPaperTexture());
        
        // PESTAÑAS SUPERIORES (Redistribuido a 3 pestañas para incluir el archivo de Lore)
        float tabW = 115f;
        float tabH = 34f;
        float startTabX = padRect.x + 18f;
        float spacingTab = 10f;

        Rect tab1Rect = new Rect(startTabX, padRect.y + 12, tabW, tabH);
        Rect tab2Rect = new Rect(startTabX + tabW + spacingTab, padRect.y + 12, tabW, tabH);
        Rect tab3Rect = new Rect(startTabX + (tabW + spacingTab) * 2, padRect.y + 12, tabW, tabH);

        // Buscar generadores en la escena (Hospital o Túneles)
        var hospitalGen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
        var tunnelsGen = FindFirstObjectByType<TunnelsGenerator>();

        GUIStyle activeTabStyle = new GUIStyle();
        activeTabStyle.fontSize = 12;
        activeTabStyle.fontStyle = FontStyle.Bold;
        activeTabStyle.alignment = TextAnchor.MiddleCenter;
        activeTabStyle.padding.left = 22;
        activeTabStyle.normal.textColor = Color.white;

        GUIStyle inactiveTabStyle = new GUIStyle();
        inactiveTabStyle.fontSize = 11;
        inactiveTabStyle.fontStyle = FontStyle.Normal;
        inactiveTabStyle.alignment = TextAnchor.MiddleCenter;
        inactiveTabStyle.padding.left = 22;
        inactiveTabStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);

        // Pestaña 1: NOTAS DE CLAVE
        bool isTunnelsMode = tunnelsGen != null && tunnelsGen.grid != null;

        GUI.color = isTunnelsMode ? new Color(0.6f, 0.6f, 0.6f, 0.6f) : ((activeTab == 0) ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.85f, 0.82f, 0.70f, 0.9f));
        GUI.DrawTexture(tab1Rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        string tab1Title = "";
        if (LocalizationManager.Instance != null)
        {
            string rawCodeTab = LocalizationManager.Instance.Get("notepad_tab_code");
            tab1Title = isTunnelsMode ? $"<s>{rawCodeTab} (N/A)</s>" : rawCodeTab;
        }
        else
        {
            tab1Title = isTunnelsMode ? "<s>CLAVE (N/A)</s>" : "CLAVE";
        }

        if (GUI.Button(tab1Rect, tab1Title, activeTab == 0 ? activeTabStyle : inactiveTabStyle))
        {
            if (!isTunnelsMode) activeTab = 0;
        }
        Texture2D t1Icon = GetTabCodeTexture();
        if (t1Icon != null) GUI.DrawTexture(new Rect(tab1Rect.x + 8, tab1Rect.y + (tab1Rect.height - 22) / 2f, 22, 22), t1Icon, ScaleMode.ScaleToFit, true);

        // Pestaña 2: PLANO DEL MAPA
        GUI.color = (activeTab == 1) ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.85f, 0.82f, 0.70f, 0.9f);
        GUI.DrawTexture(tab2Rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        string tab2Title = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("notepad_tab_map") : "MAPA";
        if (GUI.Button(tab2Rect, tab2Title, activeTab == 1 ? activeTabStyle : inactiveTabStyle))
        {
            activeTab = 1;
        }
        Texture2D t2Icon = GetTabMapTexture();
        if (t2Icon != null) GUI.DrawTexture(new Rect(tab2Rect.x + 8, tab2Rect.y + (tab2Rect.height - 22) / 2f, 22, 22), t2Icon, ScaleMode.ScaleToFit, true);

        // Pestaña 3: ARCHIVOS DE LORE (Coleccionables de historia)
        GUI.color = (activeTab == 2) ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.85f, 0.82f, 0.70f, 0.9f);
        GUI.DrawTexture(tab3Rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string tab3Title = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("notepad_tab_lore") : "REGISTROS";
        if (GUI.Button(tab3Rect, tab3Title, activeTab == 2 ? activeTabStyle : inactiveTabStyle))
        {
            activeTab = 2;
        }
        Texture2D t3Icon = GetTabLoreTexture();
        if (t3Icon != null) GUI.DrawTexture(new Rect(tab3Rect.x + 8, tab3Rect.y + (tab3Rect.height - 22) / 2f, 22, 22), t3Icon, ScaleMode.ScaleToFit, true);

        if (activeTab == 0)
        {
            RenderNotesTab(padRect, isTunnelsMode);
        }
        else if (activeTab == 1)
        {
            RenderMapTab(padRect, hospitalGen, tunnelsGen);
        }
        else if (activeTab == 2)
        {
            RenderLoreTab(padRect);
        }

        // BOTÓN CERRAR
        string closeText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("notepad_close") : "Cerrar";
        if (GUI.Button(new Rect(padRect.x + (padRect.width - 120) / 2.0f, padRect.y + padRect.height - 45, 120, 30), closeText))
        {
            CloseNotepad();
        }
        
        GUI.matrix = oldMatrix;
    }

    private void RenderNotesTab(Rect padRect, bool isTunnelsMode = false)
    {
        GUIStyle subStyle = new GUIStyle();
        subStyle.fontSize = 14;
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.normal.textColor = Color.gray;
        string codeTitle = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("notepad_director_code") : "Codigo de la Oficina del Director:";
        GUI.Label(new Rect(padRect.x, padRect.y + 55, padRect.width, 30), codeTitle, subStyle);

        float startX = padRect.x + 52f;
        float startY = padRect.y + 90f;
        float slotW = 38f;
        float slotH = 48f;
        float spacingX = 5f;

        GUIStyle slotStyle = new GUIStyle();
        slotStyle.fontSize = 24;
        slotStyle.fontStyle = FontStyle.Bold;
        slotStyle.alignment = TextAnchor.MiddleCenter;
        slotStyle.normal.textColor = isTunnelsMode ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.05f, 0.5f, 0.1f);

        for (int i = 0; i < 7; i++)
        {
            Rect slotRect = new Rect(startX + i * (slotW + spacingX), startY, slotW, slotH);
            
            GUI.color = Color.white;
            GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
            
            GUI.color = Color.black;
            GUI.Box(slotRect, "");
            GUI.color = Color.white;

            string slotVal = foundNotes[i] != -1 ? foundNotes[i].ToString() : "_";
            GUI.Label(slotRect, slotVal, slotStyle);
        }

        GUIStyle hintStyle = new GUIStyle();
        hintStyle.fontSize = 13;
        hintStyle.alignment = TextAnchor.UpperLeft;
        hintStyle.wordWrap = true;
        hintStyle.normal.textColor = Color.black;

        string hintText = "";
        if (isTunnelsMode)
        {
            hintText = LocalizationManager.Instance != null 
                ? LocalizationManager.Instance.Get("notepad_hint_tunnels") 
                : "Pistas del Hospital:\n\n(Esta sección correspondía al Hospital. En los túneles no se requieren notas clave para avanzar).\n\n⚠️ Tu objetivo actual en el sector de túneles es localizar la consola de drenaje, accionar la palanca de bombeo y evacuar por la escotilla principal.";
        }
        else
        {
            if (LocalizationManager.Instance != null)
            {
                hintText = LocalizationManager.Instance.Get("notepad_hint_header");
                int notesCount = 0;
                for (int i = 0; i < 7; i++)
                {
                    if (foundNotes[i] != -1)
                    {
                        notesCount++;
                        // Formatear el dígito ej: "• Dígito 1 del código: X"
                        hintText += string.Format(LocalizationManager.Instance.Get("notepad_hint_digit"), i + 1, foundNotes[i]);
                    }
                }

                if (notesCount == 0)
                {
                    hintText += LocalizationManager.Instance.Get("notepad_hint_none");
                }
                else if (notesCount == 7)
                {
                    hintText += LocalizationManager.Instance.Get("notepad_hint_complete");
                }
                else
                {
                    hintText += string.Format(LocalizationManager.Instance.Get("notepad_hint_progress"), notesCount);
                }
            }
            else
            {
                hintText = "Pistas encontradas en el laberinto:\n\n";
                int notesCount = 0;
                for (int i = 0; i < 7; i++)
                {
                    if (foundNotes[i] != -1)
                    {
                        notesCount++;
                        hintText += $"• Digito {i + 1} del codigo: {foundNotes[i]}\n";
                    }
                }

                if (notesCount == 0)
                {
                    hintText += "(Aun no has encontrado ninguna nota. Busca papeles blancos con numeros en las consultas y oficinas del hospital).";
                }
                else if (notesCount == 7)
                {
                    hintText += "¡Codigo completo descubierto! Ve a la puerta de la Oficina del Director e ingresa los 7 numeros.";
                }
                else
                {
                    hintText += $"\n({notesCount} de 7 notas encontradas. Sigue explorando para rellenar los casilleros vacios).";
                }
            }
        }

        // Margen aumentado para no tocar el contorno oscuro
        GUI.Label(new Rect(padRect.x + 45, padRect.y + 155, padRect.width - 90, 180), hintText, hintStyle);

        // Si estamos en túneles, dibujar una gran X o líneas rayadas rojas sobre toda la hoja de notas
        if (isTunnelsMode)
        {
            GUI.color = new Color(0.85f, 0.1f, 0.1f, 0.45f);
            
            // Línea diagonal 1
            DrawLine(new Vector2(padRect.x + 20, padRect.y + 60), new Vector2(padRect.x + padRect.width - 20, padRect.y + 340), 4f);
            // Línea diagonal 2
            DrawLine(new Vector2(padRect.x + padRect.width - 20, padRect.y + 60), new Vector2(padRect.x + 20, padRect.y + 340), 4f);

            GUI.color = Color.white;
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, float width)
    {
        Vector2 d = end - start;
        float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(a, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width / 2f, d.magnitude, width), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-a, start);
    }

    private void RenderMapTab(Rect padRect, ModularHospital.ModularHospitalGenerator gen, TunnelsGenerator tunnelsGen)
    {
        bool isTunnels = tunnelsGen != null && tunnelsGen.grid != null;

        GUIStyle mapTitleStyle = new GUIStyle();
        mapTitleStyle.fontSize = 14;
        mapTitleStyle.fontStyle = FontStyle.Bold;
        mapTitleStyle.alignment = TextAnchor.MiddleCenter;
        mapTitleStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f);

        string mapTitle = "MAPA DEL HOSPITAL";
        if (LocalizationManager.Instance != null)
        {
            mapTitle = LocalizationManager.Instance.Get(isTunnels ? "notepad_tunnels_map" : "notepad_hospital_map");
        }
        else
        {
            mapTitle = isTunnels ? "PLANO DE LOS TÚNELES" : "MAPA DEL HOSPITAL";
        }
        GUI.Label(new Rect(padRect.x, padRect.y + 54, padRect.width - 45, 22), mapTitle, mapTitleStyle);

        if (isTunnels)
        {
            RenderTunnelsMapTab(padRect, tunnelsGen);
            return;
        }

        int[,] currentGrid = (gen != null && gen.gridMatrix != null) ? gen.gridMatrix : GetFallbackHospitalGridMatrix();

        if (currentGrid != null)
        {
            int sX = currentGrid.GetLength(0);
            int sZ = currentGrid.GetLength(1);

            float mapBoxSize = 255f;
            float cellW = mapBoxSize / sX;
            float cellH = mapBoxSize / sZ;
            float startMapX = padRect.x + (padRect.width - mapBoxSize) / 2.0f + 10f;
            float startMapY = padRect.y + 120f;

            if (playerTransform == null) FindPlayer();

            int pGX = -1;
            int pGZ = -1;
            if (playerTransform != null)
            {
                Vector3 basePos = (gen != null) ? gen.transform.position : Vector3.zero;
                float halfW = (sX * 4.0f) / 2.0f;
                float halfD = (sZ * 4.0f) / 2.0f;
                Vector3 pLocal = playerTransform.position - basePos;
                pGX = Mathf.Clamp(Mathf.RoundToInt((pLocal.x + halfW - 2.0f) / 4.0f), 0, sX - 1);
                pGZ = Mathf.Clamp(Mathf.RoundToInt((pLocal.z + halfD - 2.0f) / 4.0f), 0, sZ - 1);

                // Revelar celdas al explorar por proximidad (Radio 1 casilla)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int cx = pGX + dx;
                        int cz = pGZ + dz;
                        if (cx >= 0 && cx < sX && cz >= 0 && cz < sZ)
                        {
                            discoveredRooms.Add(new Vector2Int(cx, cz));
                        }
                    }
                }
            }

            // ROSA DE LOS VIENTOS (UBICADA CON MARGENES LIBRES DE 10PX ARRIBA, ABAJO E IZQUIERDA)
            Rect compassRect = new Rect(padRect.x + padRect.width - 45f, padRect.y + 60f, 32f, 32f);
            GUI.color = new Color(0.94f, 0.93f, 0.85f, 0.95f);
            GUI.DrawTexture(compassRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            GUI.Box(compassRect, "");

            GUIStyle northStyle = new GUIStyle();
            northStyle.fontSize = 9;
            northStyle.fontStyle = FontStyle.Bold;
            northStyle.alignment = TextAnchor.MiddleCenter;
            northStyle.normal.textColor = new Color(0.85f, 0.15f, 0.1f);
            GUI.Label(new Rect(compassRect.x, compassRect.y + 1, compassRect.width, 11), "N", northStyle);

            GUIStyle dirStyle = new GUIStyle();
            dirStyle.fontSize = 7;
            dirStyle.fontStyle = FontStyle.Bold;
            dirStyle.alignment = TextAnchor.MiddleCenter;
            dirStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);

            GUI.Label(new Rect(compassRect.x, compassRect.y + compassRect.height - 11, compassRect.width, 11), "S", dirStyle);
            GUI.Label(new Rect(compassRect.x + compassRect.width - 10, compassRect.y + 10, 10, 11), "E", dirStyle);
            GUI.Label(new Rect(compassRect.x + 1, compassRect.y + 10, 10, 11), "O", dirStyle);
            GUI.color = Color.white;

            // MARCO DEL PLANO
            Rect mapBgRect = new Rect(startMapX - 4, startMapY - 4, mapBoxSize + 8, mapBoxSize + 8);
            GUI.color = new Color(0.93f, 0.92f, 0.85f, 1f);
            GUI.DrawTexture(mapBgRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
            GUI.Box(mapBgRect, "");
            GUI.color = Color.white;

            // PASTILLAS DE COORDENADAS EJE X
            int numPillsX = 9;
            float pillW_X = mapBoxSize / numPillsX;

            GUIStyle coordPillStyle = new GUIStyle();
            coordPillStyle.fontSize = 9;
            coordPillStyle.fontStyle = FontStyle.Bold;
            coordPillStyle.alignment = TextAnchor.MiddleCenter;
            coordPillStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            for (int i = 0; i < numPillsX; i++)
            {
                float cx = startMapX + i * pillW_X;
                Rect pillRect = new Rect(cx + 0.5f, startMapY - 18f, pillW_X - 1f, 15f);
                GUI.color = new Color(0.88f, 0.87f, 0.78f, 0.95f);
                GUI.DrawTexture(pillRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
                GUI.Box(pillRect, "");
                GUI.color = Color.white;

                int num = i + 1;
                string numStr = num < 10 ? $"0{num}" : $"{num}";
                GUI.Label(pillRect, numStr, coordPillStyle);
            }

            // Eje Z Izquierdo
            int numPillsZ = 9;
            float pillH_Z = mapBoxSize / numPillsZ;

            for (int i = 0; i < numPillsZ; i++)
            {
                float ry = startMapY + (numPillsZ - 1 - i) * pillH_Z;
                Rect pillRect = new Rect(startMapX - 22f, ry + 0.5f, 18f, pillH_Z - 1f);
                GUI.color = new Color(0.88f, 0.87f, 0.78f, 0.95f);
                GUI.DrawTexture(pillRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
                GUI.Box(pillRect, "");
                GUI.color = Color.white;

                int num = i + 1;
                string numStr = num < 10 ? $"0{num}" : $"{num}";
                GUI.Label(pillRect, numStr, coordPillStyle);
            }

            // LÍNEAS DE CUADRÍCULA
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.18f);
            for (int x = 1; x < sX; x++)
            {
                float lx = startMapX + x * cellW;
                GUI.DrawTexture(new Rect(lx, startMapY, 1f, mapBoxSize), Texture2D.whiteTexture);
            }
            for (int z = 1; z < sZ; z++)
            {
                float ly = startMapY + z * cellH;
                GUI.DrawTexture(new Rect(startMapX, ly, mapBoxSize, 1f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            // MATRIZ DE MUROS Y PUNTOS DE INTERÉS
            for (int x = 0; x < sX; x++)
            {
                for (int z = 0; z < sZ; z++)
                {
                    int type = currentGrid[x, z];
                    float rx = startMapX + x * cellW;
                    float ry = startMapY + (sZ - 1 - z) * cellH;
                    Rect cellRect = new Rect(rx, ry, cellW, cellH);

                    bool isDiscovered = discoveredRooms.Contains(new Vector2Int(x, z));

                    if (type == 0) // Muro macizo
                    {
                        GUI.color = new Color(0.22f, 0.22f, 0.24f, 0.95f);
                        GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
                        GUI.Box(cellRect, "");
                        GUI.color = Color.white;
                    }
                    else if (type == 2) // Oficina del Director
                    {
                        if (isDiscovered)
                        {
                            GUI.color = new Color(0.88f, 0.28f, 0.22f, 0.95f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                            GUI.color = Color.black;
                            GUI.Box(cellRect, "");

                            GUIStyle dirTagStyle = new GUIStyle();
                            dirTagStyle.fontSize = 9;
                            dirTagStyle.fontStyle = FontStyle.Bold;
                            dirTagStyle.alignment = TextAnchor.MiddleCenter;
                            dirTagStyle.normal.textColor = Color.white;
                            GUI.Label(cellRect, "DIR", dirTagStyle);
                        }
                        else
                        {
                            GUI.color = new Color(0.95f, 0.45f, 0.20f, 0.95f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                            GUI.color = Color.black;
                            GUI.Box(cellRect, "");

                            GUIStyle qStyle = new GUIStyle();
                            qStyle.fontSize = 11;
                            qStyle.fontStyle = FontStyle.Bold;
                            qStyle.alignment = TextAnchor.MiddleCenter;
                            qStyle.normal.textColor = Color.black;
                            GUI.Label(cellRect, "?", qStyle);
                        }
                        GUI.color = Color.white;
                    }
                    else if (type == 1) // Pasillo caminable
                    {
                        if (isDiscovered)
                        {
                            GUI.color = new Color(0.88f, 0.85f, 0.75f, 0.95f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                            GUI.color = new Color(0.4f, 0.4f, 0.35f, 0.3f);
                            GUI.Box(cellRect, "");
                        }
                        else
                        {
                            GUI.color = new Color(0.65f, 0.62f, 0.55f, 0.75f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                        }
                        GUI.color = Color.white;
                    }
                    else if (type == 3) // Habitaciones
                    {
                        if (isDiscovered)
                        {
                            GUI.color = new Color(0.28f, 0.58f, 0.78f, 0.95f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                            GUI.color = new Color(0.1f, 0.3f, 0.5f, 0.8f);
                            GUI.Box(cellRect, "");
                        }
                        else
                        {
                            GUI.color = new Color(0.95f, 0.78f, 0.15f, 0.95f);
                            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                            GUI.Box(cellRect, "");

                            GUIStyle qStyle = new GUIStyle();
                            qStyle.fontSize = 11;
                            qStyle.fontStyle = FontStyle.Bold;
                            qStyle.alignment = TextAnchor.MiddleCenter;
                            qStyle.normal.textColor = Color.black;
                            GUI.Label(cellRect, "?", qStyle);
                        }
                        GUI.color = Color.white;
                    }
                }
            }

            // BORDES DE MUROS EN NEGRO
            for (int x = 0; x < sX; x++)
            {
                for (int z = 0; z < sZ; z++)
                {
                    int type = currentGrid[x, z];
                    if (type == 0) continue;

                    float rx = startMapX + x * cellW;
                    float ry = startMapY + (sZ - 1 - z) * cellH;

                    GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

                    if (z + 1 >= sZ || currentGrid[x, z + 1] == 0)
                        GUI.DrawTexture(new Rect(rx, ry, cellW, 2.5f), Texture2D.whiteTexture);

                    if (z - 1 < 0 || currentGrid[x, z - 1] == 0)
                        GUI.DrawTexture(new Rect(rx, ry + cellH - 2.5f, cellW, 2.5f), Texture2D.whiteTexture);

                    if (x - 1 < 0 || currentGrid[x - 1, z] == 0)
                        GUI.DrawTexture(new Rect(rx, ry, 2.5f, cellH), Texture2D.whiteTexture);

                    if (x + 1 >= sX || currentGrid[x + 1, z] == 0)
                        GUI.DrawTexture(new Rect(rx + cellW - 2.5f, ry, 2.5f, cellH), Texture2D.whiteTexture);

                    GUI.color = Color.white;
                }
            }

            // CAPA JUGADOR
            if (showPlayerPositionOnMap && pGX >= 0 && pGZ >= 0)
            {
                float prx = startMapX + pGX * cellW;
                float pry = startMapY + (sZ - 1 - pGZ) * cellH;
                Rect pRect = new Rect(prx + 1, pry + 1, cellW - 2, cellH - 2);

                float blinkAlpha = 0.85f + Mathf.PingPong(Time.time * 4f, 0.15f);
                GUI.color = new Color(0.05f, 0.95f, 0.25f, blinkAlpha);
                GUI.DrawTexture(pRect, Texture2D.whiteTexture);

                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(pRect.x, pRect.y, pRect.width, 1.5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pRect.x, pRect.y + pRect.height - 1.5f, pRect.width, 1.5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pRect.x, pRect.y, 1.5f, pRect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pRect.x + pRect.width - 1.5f, pRect.y, 1.5f, pRect.height), Texture2D.whiteTexture);

                GUIStyle pTagStyle = new GUIStyle();
                pTagStyle.fontSize = 9;
                pTagStyle.fontStyle = FontStyle.Bold;
                pTagStyle.alignment = TextAnchor.MiddleCenter;
                pTagStyle.normal.textColor = Color.black;
                GUI.Label(pRect, "TÚ", pTagStyle);
                GUI.color = Color.white;
            }
        }
    }

    private void RenderTunnelsMapTab(Rect padRect, TunnelsGenerator tunnelsGen)
    {
        if (tunnelsGen == null || tunnelsGen.grid == null) return;

        int sX = tunnelsGen.width;
        int sZ = tunnelsGen.height;

        float mapBoxSize = 255f;
        float cellW = mapBoxSize / sX;
        float cellH = mapBoxSize / sZ;
        float startMapX = padRect.x + (padRect.width - mapBoxSize) / 2.0f + 10f;
        float startMapY = padRect.y + 120f;

        if (playerTransform == null) FindPlayer();

        int pGX = -1;
        int pGZ = -1;
        if (playerTransform != null)
        {
            float segLen = tunnelsGen.segmentLength * tunnelsGen.mapScale;
            Vector3 pLocal = playerTransform.position - tunnelsGen.transform.position;
            pGX = Mathf.Clamp(Mathf.RoundToInt(pLocal.x / segLen), 0, sX - 1);
            pGZ = Mathf.Clamp(Mathf.RoundToInt(pLocal.z / segLen), 0, sZ - 1);

            // Revelar celdas al explorar por proximidad (Radio 1 casilla)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = pGX + dx;
                    int cz = pGZ + dz;
                    if (cx >= 0 && cx < sX && cz >= 0 && cz < sZ)
                    {
                        discoveredRooms.Add(new Vector2Int(cx, cz));
                    }
                }
            }
        }

        // ROSA DE LOS VIENTOS
        Rect compassRect = new Rect(padRect.x + padRect.width - 45f, padRect.y + 60f, 32f, 32f);
        GUI.color = new Color(0.94f, 0.93f, 0.85f, 0.95f);
        GUI.DrawTexture(compassRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        GUI.Box(compassRect, "");

        GUIStyle northStyle = new GUIStyle();
        northStyle.fontSize = 9;
        northStyle.fontStyle = FontStyle.Bold;
        northStyle.alignment = TextAnchor.MiddleCenter;
        northStyle.normal.textColor = new Color(0.85f, 0.15f, 0.1f);
        GUI.Label(new Rect(compassRect.x, compassRect.y + 1, compassRect.width, 11), "N", northStyle);

        GUIStyle dirStyle = new GUIStyle();
        dirStyle.fontSize = 7;
        dirStyle.fontStyle = FontStyle.Bold;
        dirStyle.alignment = TextAnchor.MiddleCenter;
        dirStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);

        GUI.Label(new Rect(compassRect.x, compassRect.y + compassRect.height - 11, compassRect.width, 11), "S", dirStyle);
        GUI.Label(new Rect(compassRect.x + compassRect.width - 10, compassRect.y + 10, 10, 11), "E", dirStyle);
        GUI.Label(new Rect(compassRect.x + 1, compassRect.y + 10, 10, 11), "O", dirStyle);
        GUI.color = Color.white;

        // MARCO DEL PLANO (ESTILO PERGAMINO HOSPITAL)
        Rect mapBgRect = new Rect(startMapX - 4, startMapY - 4, mapBoxSize + 8, mapBoxSize + 8);
        GUI.color = new Color(0.93f, 0.92f, 0.85f, 1f);
        GUI.DrawTexture(mapBgRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        GUI.Box(mapBgRect, "");
        GUI.color = Color.white;

        // PASTILLAS DE COORDENADAS EJE X (NÚMEROS 01 A 09)
        int numPillsX = 9;
        float pillW_X = mapBoxSize / numPillsX;

        GUIStyle coordPillStyle = new GUIStyle();
        coordPillStyle.fontSize = 9;
        coordPillStyle.fontStyle = FontStyle.Bold;
        coordPillStyle.alignment = TextAnchor.MiddleCenter;
        coordPillStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        for (int i = 0; i < numPillsX; i++)
        {
            float cx = startMapX + i * pillW_X;
            Rect pillRect = new Rect(cx + 0.5f, startMapY - 18f, pillW_X - 1f, 15f);
            GUI.color = new Color(0.88f, 0.87f, 0.78f, 0.95f);
            GUI.DrawTexture(pillRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            GUI.Box(pillRect, "");
            GUI.color = Color.white;

            int num = i + 1;
            string numStr = num < 10 ? $"0{num}" : $"{num}";
            GUI.Label(pillRect, numStr, coordPillStyle);
        }

        // PASTILLAS DE COORDENADAS EJE Z (NÚMEROS 01 A 09 IZQUIERDA)
        int numPillsZ = 9;
        float pillH_Z = mapBoxSize / numPillsZ;

        for (int i = 0; i < numPillsZ; i++)
        {
            float ry = startMapY + (numPillsZ - 1 - i) * pillH_Z;
            Rect pillRect = new Rect(startMapX - 22f, ry + 0.5f, 18f, pillH_Z - 1f);
            GUI.color = new Color(0.88f, 0.87f, 0.78f, 0.95f);
            GUI.DrawTexture(pillRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            GUI.Box(pillRect, "");
            GUI.color = Color.white;

            int num = i + 1;
            string numStr = num < 10 ? $"0{num}" : $"{num}";
            GUI.Label(pillRect, numStr, coordPillStyle);
        }

        // LÍNEAS DE CUADRÍCULA DEL PLANO
        GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.18f);
        for (int x = 1; x < sX; x++)
        {
            float lx = startMapX + x * cellW;
            GUI.DrawTexture(new Rect(lx, startMapY, 1f, mapBoxSize), Texture2D.whiteTexture);
        }
        for (int z = 1; z < sZ; z++)
        {
            float ly = startMapY + z * cellH;
            GUI.DrawTexture(new Rect(startMapX, ly, mapBoxSize, 1f), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // MATRIZ DE MUROS Y PASILLOS (ESTILO LIMPIO MAPA HOSPITAL)
        for (int x = 0; x < sX; x++)
        {
            for (int z = 0; z < sZ; z++)
            {
                bool isPath = tunnelsGen.grid[x, z];
                float rx = startMapX + x * cellW;
                float ry = startMapY + (sZ - 1 - z) * cellH;
                Rect cellRect = new Rect(rx, ry, cellW, cellH);

                bool isDiscovered = discoveredRooms.Contains(new Vector2Int(x, z));

                if (!isPath) // Muro macizo (Bloque oscuro suave sin bisel pesado)
                {
                    GUI.color = new Color(0.24f, 0.25f, 0.27f, 0.95f);
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                }
                else // Pasillo de túnel
                {
                    if (isDiscovered)
                    {
                        // Pasillo descubierto (Papel pergamino claro idéntico al hospital)
                        GUI.color = new Color(0.92f, 0.89f, 0.80f, 0.95f);
                        GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    }
                    else
                    {
                        // Pasillo sin descubrir (Tono de niebla)
                        GUI.color = new Color(0.65f, 0.62f, 0.55f, 0.75f);
                        GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    }
                }
                GUI.color = Color.white;
            }
        }

        // BORDES NEGROS Y PAREDES INTERIORES DE PASILLOS
        for (int x = 0; x < sX; x++)
        {
            for (int z = 0; z < sZ; z++)
            {
                if (!tunnelsGen.grid[x, z]) continue;

                float rx = startMapX + x * cellW;
                float ry = startMapY + (sZ - 1 - z) * cellH;

                GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

                if (z + 1 >= sZ || !tunnelsGen.grid[x, z + 1])
                    GUI.DrawTexture(new Rect(rx, ry, cellW, 1.5f), Texture2D.whiteTexture);

                if (z - 1 < 0 || !tunnelsGen.grid[x, z - 1])
                    GUI.DrawTexture(new Rect(rx, ry + cellH - 1.5f, cellW, 1.5f), Texture2D.whiteTexture);

                if (x - 1 < 0 || !tunnelsGen.grid[x - 1, z])
                    GUI.DrawTexture(new Rect(rx, ry, 1.5f, cellH), Texture2D.whiteTexture);

                if (x + 1 >= sX || !tunnelsGen.grid[x + 1, z])
                    GUI.DrawTexture(new Rect(rx + cellW - 1.5f, ry, 1.5f, cellH), Texture2D.whiteTexture);

                GUI.color = Color.white;
            }
        }

        // MARCADOR DE POSICIÓN DEL JUGADOR ("TÚ")
        if (showPlayerPositionOnMap && pGX >= 0 && pGZ >= 0)
        {
            float prx = startMapX + pGX * cellW;
            float pry = startMapY + (sZ - 1 - pGZ) * cellH;
            Rect pRect = new Rect(prx, pry, cellW, cellH);

            float blinkAlpha = 0.85f + Mathf.PingPong(Time.time * 4f, 0.15f);
            GUI.color = new Color(0.85f, 0.15f, 0.1f, blinkAlpha);
            GUI.DrawTexture(pRect, Texture2D.whiteTexture);

            GUIStyle pTagStyle = new GUIStyle();
            pTagStyle.fontSize = 8;
            pTagStyle.fontStyle = FontStyle.Bold;
            pTagStyle.alignment = TextAnchor.MiddleCenter;
            pTagStyle.normal.textColor = Color.white;
            GUI.Label(pRect, "TÚ", pTagStyle);
            GUI.color = Color.white;
        }
    }

    private void RenderLoreTab(Rect padRect)
    {
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 13; // Ajustado ligeramente para caber mejor en la libreta
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.12f, 0.12f, 0.12f);
        titleStyle.alignment = TextAnchor.UpperCenter;
        titleStyle.wordWrap = true; // Evitar textos cortados/mochados en títulos largos
        titleStyle.richText = true;

        GUIStyle bodyStyle = new GUIStyle();
        bodyStyle.fontSize = 11;
        bodyStyle.wordWrap = true;
        bodyStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);
        bodyStyle.alignment = TextAnchor.UpperLeft;
        bodyStyle.richText = true; // Soporte completo para negrita, cursiva, etc.

        GUIStyle listStyle = new GUIStyle(GUI.skin.button);
        listStyle.fontSize = 11;
        listStyle.alignment = TextAnchor.MiddleLeft;

        if (collectedLoreNotes.Count == 0)
        {
            GUIStyle emptyStyle = new GUIStyle(bodyStyle);
            emptyStyle.alignment = TextAnchor.MiddleCenter;
            emptyStyle.fontSize = 13;
            string noLoreMsg = LocalizationManager.Instance != null 
                ? LocalizationManager.Instance.Get("notepad_no_lore") 
                : "No has recopilado ningún informe ni documento de historia todavía.\n\nBusca papeles envejecidos y quemados en las mesas y consultas del hospital.";
            GUI.Label(new Rect(padRect.x + 45, padRect.y + 120, padRect.width - 90, 150), noLoreMsg, emptyStyle);
            return;
        }

        // Diseño en 2 columnas con márgenes aumentados para no solapar los bordes oscuros del pergamino
        float listW = 100f;
        float viewW = 195f;
        float height = 310f;

        float listX = padRect.x + 45f;
        float viewX = listX + listW + 15f;
        float startY = padRect.y + 55f;

        // --- COLUMNA 1: LISTADO DE NOTAS DE LORE ---
        GUILayout.BeginArea(new Rect(listX, startY, listW, height));
        GUILayout.Space(5);

        int firstKey = -1;
        foreach (var pair in collectedLoreNotes)
        {
            if (firstKey == -1) firstKey = pair.Key;

            bool isSelected = selectedLoreNoteId == pair.Key;
            GUI.color = isSelected ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : Color.white;
            if (GUILayout.Button(pair.Value.title, listStyle, GUILayout.Height(35)))
            {
                selectedLoreNoteId = pair.Key;
            }
            GUI.color = Color.white;
            GUILayout.Space(5);
        }
        GUILayout.EndArea();

        if (selectedLoreNoteId == -1 && firstKey != -1)
        {
            selectedLoreNoteId = firstKey;
        }

        // --- COLUMNA 2: VISOR DE TEXTO SELECCIONADO ---
        if (selectedLoreNoteId != -1 && collectedLoreNotes.ContainsKey(selectedLoreNoteId))
        {
            LoreNoteData selectedData = collectedLoreNotes[selectedLoreNoteId];
            GUILayout.BeginArea(new Rect(viewX, startY, viewW, height));
            
            // Título
            titleStyle.alignment = TextAnchor.MiddleLeft;
            GUILayout.Label(selectedData.title.ToUpper(), titleStyle);
            GUILayout.Space(8);

            // Contenido recortado (Concepto resumido)
            string snippet = selectedData.body.Length > 90 ? selectedData.body.Substring(0, 90) + "..." : selectedData.body;
            GUILayout.Label(snippet, bodyStyle);
            
            GUILayout.Space(15);
            
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 12;
            
            if (GUILayout.Button("Ver más", btnStyle, GUILayout.Width(100), GUILayout.Height(30)))
            {
                // Reproducir sonido de papel en la cámara
                AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Hospital/Nota_Grab");
                if (pickupSound != null && Camera.main != null)
                {
                    AudioSource camAudio = Camera.main.GetComponent<AudioSource>();
                    if (camAudio == null) camAudio = Camera.main.gameObject.AddComponent<AudioSource>();
                    camAudio.ignoreListenerPause = true;
                    camAudio.PlayOneShot(pickupSound);
                }
                
                // Cerrar cuaderno
                Instance.CloseNotepad();
                
                // Configurar lectura a pantalla completa global
                fullscreenTitle = selectedData.title;
                fullscreenBody = selectedData.body;
                isReadingFullscreen = true;
                openTime = Time.unscaledTime; // Registrar tiempo de apertura
                Time.timeScale = 0f; // Pausar juego
                MobileInput.SetCursorState(false); // Mostrar cursor para cerrar
                
                // Desactivar controles del jugador y resetear inputs a cero para evitar giros infinitos
                var playerInput = FindObjectOfType<StarterAssets.StarterAssetsInputs>();
                if (playerInput == null) playerInput = FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
                if (playerInput != null)
                {
                    playerInput.move = Vector2.zero;
                    playerInput.look = Vector2.zero;
                    playerInput.enabled = false;
                }

                // Desactivar también el FirstPersonController para congelar físicamente la rotación de la cámara
                var fpc = FindObjectOfType<StarterAssets.FirstPersonController>();
                if (fpc == null) fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
                if (fpc != null)
                {
                    fpc.enabled = false;
                }
            }

            GUILayout.EndArea();
        }
    }

    private void DrawFullscreenReadingFromNotepad()
    {
        // 1. Dibujar fondo oscuro traslúcido completo
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Aplicar escalado del usuario en tiempo real según configuración del HUD
        float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
        Matrix4x4 oldMat = GUI.matrix;
        if (hudScale != 1.0f)
        {
            Vector2 pivot = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
        }

        // 2. Rectángulo de papel pergamino centrado
        int w = Mathf.Min(600, Screen.width - 40);
        int h = Mathf.Min(560, Screen.height - 60);
        Rect paperRect = new Rect(Screen.width / 2 - w / 2, Screen.height / 2 - h / 2, w, h);

        Texture2D tex = LoreNoteItem.globalPaperTexture;
        if (tex == null) tex = ProceduralPaperTexture.GetPaperTexture();

        GUI.DrawTexture(paperRect, tex);

        // Estilos de texto
        GUIStyle contentStyle = new GUIStyle();
        contentStyle.fontSize = 17;
        contentStyle.wordWrap = true;
        contentStyle.normal.textColor = new Color(0.12f, 0.12f, 0.12f, 1f); // Gris oscuro legible
        contentStyle.alignment = TextAnchor.UpperLeft;
        contentStyle.richText = true;

        GUIStyle titleStyle = new GUIStyle(contentStyle);
        titleStyle.fontSize = 22;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUIStyle closeStyle = new GUIStyle(GUI.skin.button);
        closeStyle.fontSize = 18;
        closeStyle.fontStyle = FontStyle.Bold;

        // Área del contenido
        GUILayout.BeginArea(new Rect(paperRect.x + 75, paperRect.y + 55, paperRect.width - 150, paperRect.height - 130));
        
        GUILayout.Label(fullscreenTitle.ToUpper(), titleStyle);
        GUILayout.Space(20);
        GUILayout.Label(fullscreenBody, contentStyle);

        GUILayout.EndArea();

        // Botón inferior para cerrar la lectura
        float btnW = 200f;
        float btnH = 45f;
        Rect closeBtnRect = new Rect(paperRect.x + paperRect.width / 2f - btnW / 2f, paperRect.y + paperRect.height - 70f, btnW, btnH);
        
        bool canClose = (Time.unscaledTime - openTime) > 0.3f;
        bool closePressed = false;
        
        if (canClose)
        {
            if (GUI.Button(closeBtnRect, "Cerrar [E]", closeStyle) || Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E))
            {
                closePressed = true;
            }
        }
        else
        {
            GUI.Button(closeBtnRect, "Cerrar [E]", closeStyle);
        }

        if (closePressed)
        {
            // Reproducir sonido de papel al cerrar
            AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Hospital/Nota_Grab");
            if (pickupSound != null && Camera.main != null)
            {
                AudioSource camAudio = Camera.main.GetComponent<AudioSource>();
                if (camAudio != null) camAudio.PlayOneShot(pickupSound);
            }

            isReadingFullscreen = false;
            Time.timeScale = 1f;
            
            // Reactivar controles del jugador
            var playerInput = FindObjectOfType<StarterAssets.StarterAssetsInputs>();
            if (playerInput == null) playerInput = FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
            if (playerInput != null)
            {
                playerInput.enabled = true;
                playerInput.cursorInputForLook = true;
                playerInput.cursorLocked = true;
            }

            // Reactivar también el FirstPersonController para restaurar el movimiento de la cámara
            var fpc = FindObjectOfType<StarterAssets.FirstPersonController>();
            if (fpc == null) fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
            if (fpc != null)
            {
                fpc.enabled = true;
            }

            MobileInput.SetCursorState(true);
        }

        GUI.matrix = oldMat;
    }

    private static Texture2D notebookTex;
    private static Texture2D GetNotebookTexture()
    {
        if (notebookTex == null) notebookTex = Resources.Load<Texture2D>("UI/HUD_Notebook_Icon");
        return notebookTex;
    }

    private static Texture2D tabCodeTex;
    private static Texture2D GetTabCodeTexture()
    {
        if (tabCodeTex == null) tabCodeTex = Resources.Load<Texture2D>("UI/Tab_Code_Icon");
        return tabCodeTex;
    }

    private static Texture2D tabMapTex;
    private static Texture2D GetTabMapTexture()
    {
        if (tabMapTex == null) tabMapTex = Resources.Load<Texture2D>("UI/Tab_Map_Icon");
        return tabMapTex;
    }

    private static Texture2D tabLoreTex;
    private static Texture2D GetTabLoreTexture()
    {
        if (tabLoreTex == null) tabLoreTex = Resources.Load<Texture2D>("UI/Tab_Lore_Icon");
        return tabLoreTex;
    }

    private static int[,] cachedRealGrid;
    private static int[,] GetFallbackHospitalGridMatrix()
    {
        var modGen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.gridMatrix != null && modGen.gridMatrix.GetLength(0) >= 5)
        {
            return modGen.gridMatrix;
        }

        if (cachedRealGrid != null) return cachedRealGrid;

        // Escanear dinámicamente todos los objetos y módulos de la escena real en 3D
        int[,] grid = new int[9, 9];

        // Llenar bordes exteriores como paredes (0) y centro como pasillos caminables (1)
        for (int x = 0; x < 9; x++)
        {
            for (int z = 0; z < 9; z++)
            {
                if (x == 0 || x == 8 || z == 0 || z == 8) grid[x, z] = 0;
                else grid[x, z] = 1;
            }
        }

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Vector3 minPos = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 maxPos = new Vector3(float.MinValue, 0, float.MinValue);
        int validCount = 0;

        foreach (var t in allTransforms)
        {
            if (t == null || t.position.sqrMagnitude < 0.1f) continue;
            string nameLower = t.name.ToLower();

            if (nameLower.Contains("gurney") || nameLower.Contains("room") || nameLower.Contains("director") || nameLower.Contains("elevator") || nameLower.Contains("ascensor") || nameLower.Contains("corridor") || nameLower.Contains("pasillo"))
            {
                minPos = Vector3.Min(minPos, t.position);
                maxPos = Vector3.Max(maxPos, t.position);
                validCount++;
            }
        }

        if (validCount < 4)
        {
            minPos = new Vector3(-35f, 0, -35f);
            maxPos = new Vector3(35f, 0, 35f);
        }

        float sizeX = Mathf.Max(maxPos.x - minPos.x, 10f);
        float sizeZ = Mathf.Max(maxPos.z - minPos.z, 10f);

        // Mapear la ubicación 3D real de cada módulo importante dentro de la matriz 9x9 del plano
        foreach (var t in allTransforms)
        {
            if (t == null || t.position.sqrMagnitude < 0.1f) continue;
            string n = t.name.ToLower();

            float normX = Mathf.Clamp01((t.position.x - minPos.x) / sizeX);
            float normZ = Mathf.Clamp01((t.position.z - minPos.z) / sizeZ);

            int gx = Mathf.Clamp(Mathf.FloorToInt(normX * 9f), 0, 8);
            int gz = Mathf.Clamp(Mathf.FloorToInt(normZ * 9f), 0, 8);

            if (n.Contains("director") || n.Contains("puertadirector") || n.Contains("keypad"))
            {
                grid[gx, gz] = 2; // Oficina del Director (Rojo/Dorado)
            }
            else if (n.Contains("elevator") || n.Contains("ascensor"))
            {
                grid[gx, gz] = 4; // Ascensor de Evacuación (Azul)
            }
            else if (n.Contains("room") || n.Contains("camilla") || n.Contains("apothecary") || n.Contains("morgue") || n.Contains("desk"))
            {
                if (grid[gx, gz] != 2 && grid[gx, gz] != 4)
                {
                    grid[gx, gz] = 3; // Habitaciones secundarias de notas (Marrón/Dorado)
                }
            }
        }

        cachedRealGrid = grid;
        return cachedRealGrid;
    }
}
