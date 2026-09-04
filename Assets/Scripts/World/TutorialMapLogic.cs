using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TutorialMapLogic : MonoBehaviour
{
    public static TutorialMapLogic Instance { get; private set; }

    [Header("Referencias Generales")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar el tutorial.")]
    public Transform pointStartRespawn;

    [Header("Progreso del Tutorial")]
    public int currentStep = 0; // 0=Inicio, 1=Apagón/Subgeneradores, 2=FuseBox, 3=Tarjeta/Teclado, 4=Completado
    public bool isVictoryActive = false;

    private List<SubGenerator> subgens = new List<SubGenerator>();
    private PowerBox powerBox;
    private Transform playerTransform;

    private void Awake()
    {
        Instance = this;
        // Forzar siempre personaje Male para la escena del tutorial
        PlayerPrefs.SetString("SelectedCharacter", "Male");
    }

    private void Start()
    {
        // Asegurar que el menú de pausa (PauseMenuManager) esté presente
        if (FindFirstObjectByType<PauseMenuManager>() == null)
        {
            GameObject pMenu = new GameObject("[PauseMenuManager]");
            pMenu.AddComponent<PauseMenuManager>();
        }

        SetupPlayerSpawn();
        FindTutorialElements();
        SetupTutorialNotepadAndKeypad();

        // Aplicar configuraciones guardadas de audio, sensibilidad y gráficos
        GameManager.AplicarConfiguracionesGuardadas();

        // Iniciar secuencia del tutorial guiado
        StartCoroutine(TutorialSequenceRoutine());
    }

    private void OnDestroy()
    {
        // Limpiar datos de libreta estáticos al salir del tutorial
        NotepadUIManager.ResetNotepadData();
    }

    private void SetupTutorialNotepadAndKeypad()
    {
        // Resetear e inicializar 6 de los 7 dígitos en la Libreta [TAB]
        NotepadUIManager.ResetNotepadData();

        string tutorialCode = "7482195";

        for (int i = 0; i < 6; i++)
        {
            NotepadUIManager.foundNotes[i] = int.Parse(tutorialCode[i].ToString());
        }
        NotepadUIManager.foundNotes[6] = -1; // 7mo dígito libre para recoger la nota en el tutorial

        // Configurar Teclado Numérico con el código 7482195
        KeypadController keypad = FindFirstObjectByType<KeypadController>();
        if (keypad != null)
        {
            keypad.correctCode = tutorialCode;
        }

        // Configurar las notas de papel en el tutorial para que otorguen el 7mo dígito faltante (Dígito #7 es 5)
        NoteItem[] notes = FindObjectsByType<NoteItem>(FindObjectsSortMode.None);
        foreach (var note in notes)
        {
            if (note != null)
            {
                note.digitPosition = 7;
                note.digitValue = 5;
            }
        }

        // Bloquear la puerta que requiere el Teclado Numérico para abrirse
        if (keypad != null && keypad.targetProceduralDoor != null)
        {
            keypad.targetProceduralDoor.isLocked = true;
        }
        else
        {
            ProceduralDoorInteract[] doors = FindObjectsByType<ProceduralDoorInteract>(FindObjectsSortMode.None);
            foreach (var door in doors)
            {
                if (door != null && keypad != null)
                {
                    float distToKeypad = Vector3.Distance(door.transform.position, keypad.transform.position);
                    if (distToKeypad < 4.0f)
                    {
                        door.isLocked = true;
                        keypad.targetProceduralDoor = door;
                    }
                }
            }
        }
    }

    private void SetupPlayerSpawn()
    {
        if (pointStartRespawn == null)
        {
            GameObject pStart = GameObject.Find("StartGame");
            if (pStart != null) pointStartRespawn = pStart.transform;
        }

        // Buscar NestedParent_Unpack (el verdadero prefab del jugador) o PlayerMale
        GameObject playerObj = GameObject.Find("NestedParent_Unpack");
        if (playerObj == null) playerObj = GameObject.Find("PlayerMale");
        if (playerObj == null) playerObj = GameObject.FindWithTag("Player");

        var fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
        if (fpc != null) playerObj = fpc.gameObject;

        if (playerObj != null)
        {
            playerObj.SetActive(true);

            // Asegurar que todos los objetos padres (como PlayerMale) estén activos
            Transform pCurr = playerObj.transform.parent;
            while (pCurr != null)
            {
                pCurr.gameObject.SetActive(true);
                pCurr = pCurr.parent;
            }

            // Forzar activación de todas las cámaras pertenecientes al jugador
            Camera[] allPlayerCams = playerObj.GetComponentsInChildren<Camera>(true);
            if (allPlayerCams.Length == 0 && playerObj.transform.parent != null)
            {
                allPlayerCams = playerObj.transform.parent.GetComponentsInChildren<Camera>(true);
            }

            foreach (Camera cam in allPlayerCams)
            {
                if (cam != null)
                {
                    cam.gameObject.SetActive(true);
                    cam.enabled = true;
                    cam.tag = "MainCamera";
                }
            }

            // Si aún no hay cámara principal detectada por Unity, activar el objeto 'MainCamera' dentro de NestedParent_Unpack
            if (Camera.main == null)
            {
                Transform mainCamTrans = playerObj.transform.Find("MainCamera");
                if (mainCamTrans == null && playerObj.transform.parent != null)
                    mainCamTrans = playerObj.transform.parent.Find("MainCamera");

                if (mainCamTrans != null)
                {
                    mainCamTrans.gameObject.SetActive(true);
                    Camera c = mainCamTrans.GetComponent<Camera>();
                    if (c == null) c = mainCamTrans.gameObject.AddComponent<Camera>();
                    c.enabled = true;
                    c.tag = "MainCamera";
                }
            }

            if (pointStartRespawn != null)
            {
                playerTransform = playerObj.transform;

                CharacterController cc = playerObj.GetComponent<CharacterController>();
                if (cc == null) cc = playerObj.GetComponentInParent<CharacterController>();
                if (cc == null) cc = playerObj.GetComponentInChildren<CharacterController>();

                if (cc != null) cc.enabled = false;

                playerObj.transform.position = pointStartRespawn.position;
                playerObj.transform.rotation = pointStartRespawn.rotation;

                if (cc != null) cc.enabled = true;

                if (fpc != null) fpc.ResetCameraRotation(pointStartRespawn.eulerAngles.y);
            }
        }
    }

    private void FindTutorialElements()
    {
        subgens.Clear();

        // 1. Buscar o auto-configurar los Subgeneradores en la escena (Gen (9), Gen (10), etc.)
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<GameObject> genObjs = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name.ToLower();
            if (n.Contains("gen (") || n.Contains("subgenerator") || n.Equals("gen") || n.StartsWith("gen_"))
            {
                if (!genObjs.Contains(t.gameObject) && !n.Contains("canvas") && !n.Contains("manager") && !n.Contains("powerbox"))
                {
                    genObjs.Add(t.gameObject);
                }
            }
        }

        string[] genLetters = new string[] { "A", "B", "C", "D" };
        for (int i = 0; i < genObjs.Count; i++)
        {
            GameObject gObj = genObjs[i];
            SubGenerator subGen = gObj.GetComponent<SubGenerator>();
            if (subGen == null) subGen = gObj.GetComponentInParent<SubGenerator>();
            if (subGen == null)
            {
                subGen = gObj.AddComponent<SubGenerator>();
            }

            subGen.generatorName = (i < genLetters.Length) ? genLetters[i] : (i + 1).ToString();
            subGen.subgeneratorLetter = subGen.generatorName;
            subGen.isOn = false;

            // Asegurar que el subgenerador tenga un BoxCollider para raycast e interacción sin bloquear el paso
            Collider col = gObj.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider bc = gObj.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(2.2f, 2.4f, 2.2f);
                bc.center = new Vector3(0f, 1.0f, 0f);
            }
            else
            {
                col.isTrigger = true;
            }

            if (!subgens.Contains(subGen))
            {
                subgens.Add(subGen);
            }
        }

        // Si no encontró por nombre, buscar por tipo directo
        if (subgens.Count == 0)
        {
            SubGenerator[] foundSubgens = FindObjectsByType<SubGenerator>(FindObjectsSortMode.None);
            subgens.AddRange(foundSubgens);
        }

        powerBox = FindFirstObjectByType<PowerBox>();
        SetupDoors();
        SetupDrawersKeycardsAndBatteries();
    }

    private void SetupDrawersKeycardsAndBatteries()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);

        // 1. Configurar Baterías (BatteryItem) en la escena
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name.ToLower();
            if (n.Contains("battery") || n.Contains("bateria") || n.Contains("pilas") || n.Contains("pila"))
            {
                if (!n.Contains("canvas") && !n.Contains("ui") && !n.Contains("controller"))
                {
                    BatteryItem bat = t.GetComponent<BatteryItem>();
                    if (bat == null) bat = t.gameObject.AddComponent<BatteryItem>();
                    bat.interactDistance = 2.5f;

                    BoxCollider col = t.GetComponent<BoxCollider>();
                    if (col == null) col = t.gameObject.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(0.4f, 0.4f, 0.4f);
                }
            }
        }

        // 2. Configurar Tarjeta de Acceso (KeycardItem)
        KeycardItem keycard = FindFirstObjectByType<KeycardItem>();
        GameObject keycardObj = keycard != null ? keycard.gameObject : null;

        if (keycardObj == null)
        {
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.name.ToLower();
                if (n.Contains("keycard") || n.Contains("tarjeta") || n.Contains("access") || n.Contains("card") || n.Contains("elevator"))
                {
                    if (!n.Contains("canvas") && !n.Contains("ui") && !n.Contains("controller") && !n.Contains("manager") && !n.Contains("door"))
                    {
                        keycardObj = t.gameObject;
                        if (t.GetComponent<KeycardItem>() == null) t.gameObject.AddComponent<KeycardItem>();
                        break;
                    }
                }
            }
        }

        // 3. Configurar Cajones de Escritorio (DrawerInteract)
        ModularHospital.DrawerInteract drawer = FindFirstObjectByType<ModularHospital.DrawerInteract>();
        if (drawer == null)
        {
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.name.ToLower();
                if (n.Contains("cajon") || n.Contains("drawer") || n.Contains("desk") || n.Contains("mesa"))
                {
                    if (!n.Contains("canvas") && !n.Contains("ui") && !n.Contains("room") && !n.Contains("wall") && !n.Contains("door"))
                    {
                        drawer = t.gameObject.AddComponent<ModularHospital.DrawerInteract>();
                        break;
                    }
                }
            }
        }

        if (drawer != null)
        {
            drawer.interactDistance = 4.5f;
            if (keycardObj != null)
            {
                drawer.keycardInside = keycardObj;
            }

            BoxCollider drawerCol = drawer.GetComponent<BoxCollider>();
            if (drawerCol == null) drawerCol = drawer.gameObject.AddComponent<BoxCollider>();
            drawerCol.isTrigger = true;
            drawerCol.size = new Vector3(0.8f, 0.6f, 0.8f);
            drawerCol.center = new Vector3(0f, 0f, 0.1f);
        }
    }

    private void SetupDoors()
    {
        string[] doorKeywords = new string[] { "p_door_01_", "puerta", "door" };
        string[] doorExcludes = new string[] { "base", "frame", "marco", "hinge", "autohinge", "player", "canvas", "ui" };

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);

        AudioClip hospOpenSound = Resources.Load<AudioClip>("Audio/Hospital/hospital-opening-door");
        if (hospOpenSound == null) hospOpenSound = Resources.Load<AudioClip>("hospital-opening-door");

        AudioClip hospCloseSound = Resources.Load<AudioClip>("Audio/Hospital/hospital-closing-door");
        if (hospCloseSound == null) hospCloseSound = Resources.Load<AudioClip>("hospital-closing-door");

        foreach (Transform t in allTransforms)
        {
            if (t == null || t.name.Contains("_AutoHinge")) continue;

            Transform root = t.root;
            if (root != null)
            {
                string rName = root.name.ToLower();
                if (rName.Contains("player") || rName.Contains("nestedparent") || root.CompareTag("Player")) continue;
            }

            string n = t.name.ToLower();
            bool matches = false;
            foreach (var kw in doorKeywords)
            {
                if (n.Contains(kw)) { matches = true; break; }
            }
            if (!matches) continue;

            bool excluded = false;
            foreach (var ex in doorExcludes)
            {
                if (n.Contains(ex)) { excluded = true; break; }
            }
            if (excluded) continue;

            ProceduralDoorInteract doorInteract = t.GetComponent<ProceduralDoorInteract>();
            if (doorInteract == null) doorInteract = t.GetComponentInParent<ProceduralDoorInteract>();

            if (doorInteract == null)
            {
                doorInteract = t.gameObject.AddComponent<ProceduralDoorInteract>();
            }

            doorInteract.autoFixCenterPivot = true;
            doorInteract.hingeOnRightSide = true;
            doorInteract.openAngle = -90f;
            doorInteract.interactDistance = 3.2f;
            doorInteract.isLocked = false;
            if (hospOpenSound != null) doorInteract.doorOpenSound = hospOpenSound;
            if (hospCloseSound != null) doorInteract.doorCloseSound = hospCloseSound;
        }
    }

    private IEnumerator TutorialSequenceRoutine()
    {
        // Paso 0: Bienvenida y Movimiento (5 segundos)
        currentStep = 0;
        yield return new WaitForSeconds(5f);

        // Paso 1: Disparar Apagón de Prueba a los 6.5 segundos
        currentStep = 1;
        if (powerBox != null)
        {
            powerBox.TriggerPowerOutage(true);
        }

        // Esperar a que el jugador active todos los subgeneradores
        while (CountActiveSubgens() < subgens.Count)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Paso 2: Otorgar 1 fusible de regalo para el tutorial y guiar hacia la FuseBox
        currentStep = 2;
        if (powerBox != null)
        {
            powerBox.fusesCount = 1; // Regalamos 1 fusible al jugador para el tutorial
        }

        // Esperar a que la caja de fusibles esté armada y la energía restaurada
        while (powerBox != null && powerBox.isPowerOut)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Paso 3: Explicación Animada Automática del Cuaderno [TAB]
        currentStep = 3;
        yield return new WaitForSeconds(1.0f);

        // Ocultar monólogos previos para evitar amontonamiento de texto
        if (PlayerMonologueManager.Instance != null)
        {
            PlayerMonologueManager.HideDialogue();
        }

        // Abrir libreta automáticamente e iniciar recorrido guiado animado
        if (NotepadUIManager.Instance != null && !NotepadUIManager.IsOpen)
        {
            NotepadUIManager.Instance.ToggleNotepad();
        }

        isNotebookTutorialActive = true;
        notebookTutorialStep = 0; // Explicar pestaña Clave

        yield return new WaitForSeconds(6.5f);
        notebookTutorialStep = 1; // Explicar pestaña Mapa

        yield return new WaitForSeconds(6.5f);
        notebookTutorialStep = 2; // Explicar pestaña Registros/Lore

        yield return new WaitForSeconds(6.5f);
        notebookTutorialStep = 3; // Finalización del recorrido guiado

        // El jugador ahora puede cerrar libremente el cuaderno
        while (NotepadUIManager.IsOpen)
        {
            yield return new WaitForSeconds(0.3f);
        }
        isNotebookTutorialActive = false;

        // Paso 4: Evacuar / Teclado Numérico
        currentStep = 4;
    }

    public void TriggerTutorialVictory()
    {
        if (isVictoryActive) return;
        isVictoryActive = true;
        currentStep = 5;
        StartCoroutine(VictorySequenceRoutine());
    }

    private float victoryFadeAlpha = 0f;

    private IEnumerator VictorySequenceRoutine()
    {
        Time.timeScale = 1f;

        // Ocultar monólogos o banners secundarios
        if (PlayerMonologueManager.Instance != null)
        {
            PlayerMonologueManager.HideDialogue();
        }

        // 1. Transición suave de oscurecimiento (fade out) de 2.5 segundos
        float elapsed = 0f;
        float fadeDuration = 2.2f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            victoryFadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        victoryFadeAlpha = 1.0f;
        MobileInput.SetCursorState(false);
    }

    private int CountActiveSubgens()
    {
        int count = 0;
        foreach (var s in subgens)
        {
            if (s != null && s.isOn) count++;
        }
        return count;
    }

    public bool isNotebookTutorialActive = false;
    public int notebookTutorialStep = 0;

    private void OnGUI()
    {
        if (isVictoryActive)
        {
            DrawVictoryScreen();
            return;
        }

        Draw3DTargetMarkers();
        DrawGuidedBanner();

        if (isNotebookTutorialActive)
        {
            DrawNotebookTutorialOverlay();
        }
    }

    private void Draw3DTargetMarkers()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        GUIStyle markerStyle = new GUIStyle(GUI.skin.box);
        markerStyle.alignment = TextAnchor.MiddleCenter;
        markerStyle.fontSize = 13;
        markerStyle.fontStyle = FontStyle.Bold;
        markerStyle.normal.textColor = Color.yellow;

        string objTitle = Loc("tut_obj_title", "✦ OBJETIVO ✦");

        // Marcadores de Subgeneradores en Paso 1 (Apagón)
        if (currentStep == 1)
        {
            string subgenLabel = Loc("tut_obj_subgen", "[SUBGENERADOR]");
            foreach (var s in subgens)
            {
                if (s != null && !s.isOn)
                {
                    Vector3 worldPos = s.transform.position + Vector3.up * 1.6f;
                    Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                    if (screenPos.z > 0f)
                    {
                        float GUIy = Screen.height - screenPos.y;
                        Rect rect = new Rect(screenPos.x - 90, GUIy - 18, 180, 36);
                        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
                        GUI.Box(rect, GUIContent.none);
                        GUI.color = Color.yellow;
                        GUI.Label(rect, $"{objTitle}\n{subgenLabel}", markerStyle);
                        GUI.color = Color.white;
                    }
                }
            }
        }
        // Marcador de Caja de Fusibles en Paso 2
        else if (currentStep == 2 && powerBox != null && powerBox.isPowerOut)
        {
            string fuseboxLabel = Loc("tut_obj_fusebox", "[CAJA DE FUSIBLES]");
            Vector3 worldPos = powerBox.transform.position + Vector3.up * 1.3f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z > 0f)
            {
                float GUIy = Screen.height - screenPos.y;
                Rect rect = new Rect(screenPos.x - 95, GUIy - 18, 190, 36);
                GUI.color = new Color(0.05f, 0.2f, 0.05f, 0.85f);
                GUI.Box(rect, GUIContent.none);
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUI.Label(rect, $"{objTitle}\n{fuseboxLabel}", markerStyle);
                GUI.color = Color.white;
            }
        }
    }

    private void DrawNotebookTutorialOverlay()
    {
        float sWidth = Screen.width;
        float sHeight = Screen.height;

        GUIStyle guideStyle = new GUIStyle(GUI.skin.box);
        guideStyle.alignment = TextAnchor.MiddleCenter;
        guideStyle.fontSize = Mathf.RoundToInt(sHeight * 0.027f);
        guideStyle.fontStyle = FontStyle.Bold;
        guideStyle.wordWrap = true;
        guideStyle.normal.textColor = new Color(0.25f, 0.95f, 1f);

        string text = "";
        switch (notebookTutorialStep)
        {
            case 0:
                text = Loc("tut_nb_0", "[1] PESTAÑA 1: CLAVE DEL DIRECTOR\nAquí se registran los 7 dígitos encontrados en las notas del hospital para abrir la Oficina del Director.");
                break;
            case 1:
                text = Loc("tut_nb_1", "[2] PESTAÑA 2: PLANO DEL SECTOR\nMuestra la imagen de referencia y el esquema general del mapa de la zona.");
                break;
            case 2:
                text = Loc("tut_nb_2", "[3] PESTAÑA 3: REGISTROS Y LORE\nAlmacena expedientes médicos y diarios de historia encontrados en el complejo.");
                break;
            case 3:
                text = Loc("tut_nb_3", "[*] ¡EXPLICACIÓN COMPLETADA!\nPresiona el botón 'Cerrar' para continuar tu misión.");
                break;
        }

        // Posicionar el recuadro informativo arriba en la barra superior para jamás tapar la libreta ni las notas
        Rect rect = new Rect(sWidth * 0.08f, 15f, sWidth * 0.84f, sHeight * 0.13f);
        GUI.color = new Color(0.02f, 0.08f, 0.18f, 0.95f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(rect, text, guideStyle);
    }

    private void DrawGuidedBanner()
    {
        // Si la libreta guiada está activa, ocultar el banner de fondo para no amontonar texto
        if (isNotebookTutorialActive) return;

        float sWidth = Screen.width;
        float sHeight = Screen.height;

        GUIStyle bannerStyle = new GUIStyle(GUI.skin.box);
        bannerStyle.alignment = TextAnchor.MiddleCenter;
        bannerStyle.fontSize = Mathf.RoundToInt(sHeight * 0.026f);
        bannerStyle.fontStyle = FontStyle.Bold;
        bannerStyle.wordWrap = true;
        bannerStyle.normal.textColor = Color.yellow;

        string message = "";
        switch (currentStep)
        {
            case 0:
                message = Loc("tut_step_0", "¡BIENVENIDO AL TUTORIAL!\nUsa el joystick o WASD para moverte y mantén encendida tu linterna. La luz regenera tu cordura.");
                break;
            case 1:
                int activeCount = CountActiveSubgens();
                int totalCount = subgens.Count > 0 ? subgens.Count : 2;
                message = LocFormat("tut_step_1", totalCount, activeCount);
                break;
            case 2:
                message = Loc("tut_step_2", "¡SUBGENERADORES LISTOS!\nTe otorgamos 1 Fusible de repuesto. Ve a la Caja de Fusibles (FuseBox) y presiona [E] para rearmar.");
                break;
            case 3:
                message = Loc("tut_step_3", "¡EXPLICACIÓN DE LIBRETA EN PROGRESO!\nRevisa las pestañas en pantalla.");
                break;
            case 4:
                message = Loc("tut_step_4", "¡ENERGÍA RESTAURADA!\nExplora la habitación, interactúa con la puerta o ingresa la clave en el Teclado para evacuar.");
                break;
            case 5:
                message = Loc("tut_win_title", "¡TUTORIAL COMPLETADO!");
                break;
        }

        if (!string.IsNullOrEmpty(message))
        {
            float boxW = sWidth * 0.85f;
            float boxH = sHeight * 0.12f;
            Rect rect = new Rect((sWidth - boxW) / 2f, 25f, boxW, boxH);

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;

            GUI.Label(rect, message, bannerStyle);
        }
    }

    private void DrawVictoryScreen()
    {
        float sWidth = Screen.width;
        float sHeight = Screen.height;

        // 1. Capa de oscurecimiento total (Fondo cinematográfico)
        GUI.color = new Color(0.02f, 0.03f, 0.05f, victoryFadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, sWidth, sHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (victoryFadeAlpha < 0.95f) return;

        // 2. Título Principal (Verde Esmeralda Centrado)
        GUIStyle titleSt = new GUIStyle(GUI.skin.label);
        titleSt.alignment = TextAnchor.MiddleCenter;
        titleSt.fontStyle = FontStyle.Bold;
        titleSt.fontSize = Mathf.RoundToInt(sHeight * 0.055f);
        titleSt.normal.textColor = new Color(0.25f, 0.95f, 0.45f);

        string winTitle = Loc("tut_win_title", "¡TUTORIAL COMPLETADO!");
        GUI.Label(new Rect(0, sHeight * 0.08f, sWidth, sHeight * 0.10f), winTitle, titleSt);

        // 3. Párrafo 1: Introducción (Blanco Azulado Centrado)
        GUIStyle introSt = new GUIStyle(GUI.skin.label);
        introSt.alignment = TextAnchor.MiddleCenter;
        introSt.fontStyle = FontStyle.Normal;
        introSt.fontSize = Mathf.RoundToInt(sHeight * 0.028f);
        introSt.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
        introSt.wordWrap = true;

        string introText = Loc("tut_win_intro", "Has aprendido las mecánicas fundamentales de energía, fusibles y claves de acceso.");
        GUI.Label(new Rect(sWidth * 0.08f, sHeight * 0.20f, sWidth * 0.84f, sHeight * 0.10f), introText, introSt);

        // 4. Párrafo 2: Niveles (Cyan Claro Centrado en Tarjeta de Presentación)
        Rect cardRect = new Rect(sWidth * 0.06f, sHeight * 0.32f, sWidth * 0.88f, sHeight * 0.36f);
        GUI.color = new Color(0.05f, 0.10f, 0.16f, 0.90f);
        GUI.Box(cardRect, GUIContent.none);
        GUI.color = Color.white;

        GUIStyle levelSt = new GUIStyle(GUI.skin.label);
        levelSt.alignment = TextAnchor.MiddleCenter;
        levelSt.fontSize = Mathf.RoundToInt(sHeight * 0.026f);
        levelSt.fontStyle = FontStyle.Bold;
        levelSt.normal.textColor = new Color(0.35f, 0.92f, 1f);
        levelSt.wordWrap = true;

        string levelText = Loc("tut_win_levels", "Nivel 1 (Hospital): Subgeneradores, Caja de Fusibles, Tarjeta y Ascensor.\n\nNivel 2 (Túneles): Generadores principales, Consola de Bombeo y Trampilla.\n\nNivel 3 y posteriores: Desafíos únicos de exploración y supervivencia.");
        GUI.Label(new Rect(cardRect.x + 20f, cardRect.y + 15f, cardRect.width - 40f, cardRect.height - 30f), levelText, levelSt);

        // 5. Párrafo 3: Cierre (Amarillo suave Centrado)
        GUIStyle outroSt = new GUIStyle(GUI.skin.label);
        outroSt.alignment = TextAnchor.MiddleCenter;
        outroSt.fontStyle = FontStyle.Italic;
        outroSt.fontSize = Mathf.RoundToInt(sHeight * 0.027f);
        outroSt.normal.textColor = new Color(1.0f, 0.88f, 0.35f);
        outroSt.wordWrap = true;

        string outroText = Loc("tut_win_outro", "Cada nivel tiene sus propias mecánicas, ¡pero el objetivo de supervivencia es el mismo!");
        GUI.Label(new Rect(sWidth * 0.08f, sHeight * 0.70f, sWidth * 0.84f, sHeight * 0.08f), outroText, outroSt);

        // 6. Botón Volver al Menú Principal (Centrado abajo)
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = Mathf.RoundToInt(sHeight * 0.034f);
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter;

        string btnText = Loc("tut_btn_menu", "VOLVER AL MENÚ PRINCIPAL");
        float btnW = sWidth * 0.46f;
        float btnH = sHeight * 0.09f;
        if (GUI.Button(new Rect((sWidth - btnW) / 2f, sHeight * 0.80f, btnW, btnH), btnText, btnStyle))
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    private string Loc(string key, string fallback)
    {
        return LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : fallback;
    }

    private string LocFormat(string key, params object[] args)
    {
        if (LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.GetFormat(key, args);
        }
        return string.Format("¡APAGÓN DE PRUEBA! La energía del hospital falló. Encuentra y enciende los {0} Subgeneradores ({1}/{0}).", args);
    }
}
