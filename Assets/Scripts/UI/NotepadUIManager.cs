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

    private int activeTab = 0; // 0 = Notas, 1 = Mapa del Hospital
    private Transform playerTransform;
    private static HashSet<Vector2Int> discoveredRooms = new HashSet<Vector2Int>();

    public static void ResetNotepadData()
    {
        discoveredRooms.Clear();
        for (int i = 0; i < foundNotes.Length; i++)
        {
            foundNotes[i] = -1;
        }
        Debug.Log("NotepadUIManager: Datos de libreta y mapa reseteados.");
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
        var gen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
        if (gen != null && gen.gridMatrix != null)
        {
            if (playerTransform == null) FindPlayer();
            if (playerTransform == null) return;

            int sX = gen.gridMatrix.GetLength(0);
            int sZ = gen.gridMatrix.GetLength(1);

            float halfW = (sX * 4.0f) / 2.0f;
            float halfD = (sZ * 4.0f) / 2.0f;
            Vector3 pLocal = playerTransform.position - gen.transform.position;
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

            float yPos = 98f;
            if (numGens > 0)
            {
                yPos = 98f + 65f + 8f; // Abajo de la caja de subgeneradores
            }
            else
            {
                yPos = 98f; // Si no hay subgeneradores, va justo abajo de la caja de fusibles (que termina en Y=90)
            }

            float rightEdge = Screen.width - 25f;
            float btnSize = 50f;
            Rect iconRect = new Rect(rightEdge - btnSize, yPos, btnSize, btnSize);

            // Fondo semitransparente oscuro unificado (como fusibles y subgeneradores, no azul)
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle iconStyle = new GUIStyle(GUI.skin.button);
            iconStyle.fontSize = 22;
            iconStyle.alignment = TextAnchor.MiddleCenter;
            iconStyle.fontStyle = FontStyle.Bold;
            iconStyle.normal.background = null;
            iconStyle.hover.background = null;
            iconStyle.active.background = null;
            iconStyle.normal.textColor = Color.white;
            iconStyle.hover.textColor = new Color(0.9f, 0.9f, 0.9f);

            if (GUI.Button(iconRect, "📝", iconStyle))
            {
                OpenNotepad();
            }
            return;
        }

        // Evitar dibujar si el mapa de hospital de la escena actual no está disponible
        ModularHospital.ModularHospitalGenerator gen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (gen != null && gen.isMenuMode) return;

        Rect padRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 220, 400, 440);
        
        GUI.color = new Color(0.96f, 0.94f, 0.82f, 0.98f);
        GUI.DrawTexture(padRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // PESTAÑAS SUPERIORES
        Rect tab1Rect = new Rect(padRect.x + 15, padRect.y + 12, 180, 34);
        Rect tab2Rect = new Rect(padRect.x + 205, padRect.y + 12, 180, 34);

        // Buscar generadores en la escena (Hospital o Túneles)
        var hospitalGen = FindFirstObjectByType<ModularHospital.ModularHospitalGenerator>();
        var tunnelsGen = FindFirstObjectByType<TunnelsGenerator>();

        GUIStyle activeTabStyle = new GUIStyle();
        activeTabStyle.fontSize = 14;
        activeTabStyle.fontStyle = FontStyle.Bold;
        activeTabStyle.alignment = TextAnchor.MiddleCenter;
        activeTabStyle.normal.textColor = Color.white;

        GUIStyle inactiveTabStyle = new GUIStyle();
        inactiveTabStyle.fontSize = 13;
        inactiveTabStyle.fontStyle = FontStyle.Normal;
        inactiveTabStyle.alignment = TextAnchor.MiddleCenter;
        inactiveTabStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);

        // Pestaña 1: NOTAS
        bool isTunnelsMode = tunnelsGen != null && tunnelsGen.grid != null;

        GUI.color = isTunnelsMode ? new Color(0.6f, 0.6f, 0.6f, 0.6f) : ((activeTab == 0) ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.85f, 0.82f, 0.70f, 0.9f));
        GUI.DrawTexture(tab1Rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        string tab1Title = isTunnelsMode ? "<s>📝 NOTAS (N/A)</s>" : "📝 NOTAS (CLAVE)";
        if (GUI.Button(tab1Rect, tab1Title, activeTab == 0 ? activeTabStyle : inactiveTabStyle))
        {
            activeTab = 0;
        }

        // Si estamos en túneles y el usuario entra por defecto a notas, cambiar automáticamente a mapa
        if (isTunnelsMode && activeTab == 0)
        {
            // O si hace clic en la pestaña, mostramos la versión rayada de la nota
        }

        // Pestaña 2: PLANO DEL MAPA
        GUI.color = (activeTab == 1) ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.85f, 0.82f, 0.70f, 0.9f);
        GUI.DrawTexture(tab2Rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(tab2Rect, "🗺️ PLANO DEL MAPA", activeTab == 1 ? activeTabStyle : inactiveTabStyle))
        {
            activeTab = 1;
        }

        if (activeTab == 0)
        {
            RenderNotesTab(padRect, isTunnelsMode);
        }
        else
        {
            RenderMapTab(padRect, hospitalGen, tunnelsGen);
        }

        // BOTÓN CERRAR
        if (GUI.Button(new Rect(padRect.x + (padRect.width - 120) / 2.0f, padRect.y + padRect.height - 45, 120, 30), "Cerrar"))
        {
            CloseNotepad();
        }
    }

    private void RenderNotesTab(Rect padRect, bool isTunnelsMode = false)
    {
        GUIStyle subStyle = new GUIStyle();
        subStyle.fontSize = 14;
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(padRect.x, padRect.y + 55, padRect.width, 30), "Codigo de la Oficina del Director:", subStyle);

        float startX = padRect.x + 22f;
        float startY = padRect.y + 90f;
        float slotW = 42f;
        float slotH = 48f;
        float spacingX = 7f;

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
            hintText = "Pistas del Hospital:\n\n" +
                       "(Esta sección correspondía al Hospital. En los túneles no se requieren notas clave para avanzar).\n\n" +
                       "⚠️ Tu objetivo actual en el sector de túneles es localizar la consola de drenaje, accionar la palanca de bombeo y evacuar por la escotilla principal.";
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

        GUI.Label(new Rect(padRect.x + 25, padRect.y + 155, padRect.width - 50, 180), hintText, hintStyle);

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

        string mapTitle = isTunnels ? "PLANO DE LOS TÚNELES" : "MAPA DEL HOSPITAL";
        GUI.Label(new Rect(padRect.x, padRect.y + 54, padRect.width - 45, 22), mapTitle, mapTitleStyle);

        if (isTunnels)
        {
            RenderTunnelsMapTab(padRect, tunnelsGen);
            return;
        }

        if (gen != null && gen.gridMatrix != null)
        {
            int sX = gen.gridMatrix.GetLength(0);
            int sZ = gen.gridMatrix.GetLength(1);

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
                float halfW = (sX * 4.0f) / 2.0f;
                float halfD = (sZ * 4.0f) / 2.0f;
                Vector3 pLocal = playerTransform.position - gen.transform.position;
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
                    int type = gen.gridMatrix[x, z];
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
                    int type = gen.gridMatrix[x, z];
                    if (type == 0) continue;

                    float rx = startMapX + x * cellW;
                    float ry = startMapY + (sZ - 1 - z) * cellH;

                    GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

                    if (z + 1 >= sZ || gen.gridMatrix[x, z + 1] == 0)
                        GUI.DrawTexture(new Rect(rx, ry, cellW, 2.5f), Texture2D.whiteTexture);

                    if (z - 1 < 0 || gen.gridMatrix[x, z - 1] == 0)
                        GUI.DrawTexture(new Rect(rx, ry + cellH - 2.5f, cellW, 2.5f), Texture2D.whiteTexture);

                    if (x - 1 < 0 || gen.gridMatrix[x - 1, z] == 0)
                        GUI.DrawTexture(new Rect(rx, ry, 2.5f, cellH), Texture2D.whiteTexture);

                    if (x + 1 >= sX || gen.gridMatrix[x + 1, z] == 0)
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
}
