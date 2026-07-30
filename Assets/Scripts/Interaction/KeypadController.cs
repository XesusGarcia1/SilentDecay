using UnityEngine;
using System.Collections;

public class KeypadController : MonoBehaviour
{
    [Header("Ajustes del Teclado")]
    public string correctCode = "1234567";
    public float interactDistance = 2.5f;
    public Transform officeDoor;
    public OpenDoor targetOpenDoor; // Script original de apertura de puerta de la Oficina del Director
    public ProceduralDoorInteract targetProceduralDoor; // Puerta procedimental bloqueada para la Oficina del Director

    [Header("Sonidos (Personalizables)")]
    public AudioClip keySound;
    public AudioClip successSound;
    public AudioClip errorSound;

    [Header("Referencias a Malla 3D")]
    public TextMesh screenText;        // Texto de la pantalla 3D física
    public Renderer ledRedRenderer;    // LED rojo de la malla 3D
    public Renderer ledGreenRenderer;  // LED verde de la malla 3D

    private Transform player;
    private bool playerNear = false;
    public bool isOpened = false;
    private bool isUnlocked = false;
    private string currentInput = "";
    private AudioSource audioSource;
    private int openedFrame = -1;

    void Start()
    {
        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { player = cc.transform; }
        else {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // Configurar el AudioSource para sonido 3D espacializado en el keypad físico
        audioSource.spatialBlend = 1f; // 3D
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 10f;

        // Cargar sonidos por defecto de Resources si no están asignados en el Inspector
        if (keySound == null) keySound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor"); 
        
        if (successSound == null) successSound = Resources.Load<AudioClip>("Audio/Hospital/successSound");
        if (successSound == null) successSound = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Llegar"); // Fallback
        
        if (errorSound == null) errorSound = Resources.Load<AudioClip>("Audio/Hospital/errorSound");
        if (errorSound == null) errorSound = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Error"); // Fallback

        if (screenText != null)
        {
            screenText.text = "LOCKED";
            screenText.color = new Color(0.05f, 0.12f, 0.15f); // Negro azulado de alto contraste
        }
    }

    void Update()
    {
        if (isUnlocked) return;

        if (player != null)
        {
            playerNear = false;
            Camera cam = Camera.main;
            if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();

            if (cam != null)
            {
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                if (dist <= 3.2f) // Distancia cómoda de 3.2m
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    RaycastHit hit;

                    // QueryTriggerInteraction.Collide permite detectar el BoxCollider Trigger del Keypad
                    if (Physics.Raycast(ray, out hit, 3.5f, ~0, QueryTriggerInteraction.Collide))
                    {
                        string n = hit.transform.name.ToLower();
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform) || n.Contains("keypad") || n.Contains("teclado"))
                        {
                            playerNear = true;
                        }
                    }

                    if (!playerNear)
                    {
                        Vector3 dirToKeypad = (transform.position - cam.transform.position).normalized;
                        float dot = Vector3.Dot(cam.transform.forward, dirToKeypad);
                        if (dot > 0.70f && dist <= 2.8f)
                        {
                            playerNear = true;
                        }
                    }
                }
            }
        }
        else
        {
            UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
            if (cc != null) { player = cc.transform; }
            else {
                GameObject playerObj = GameObject.Find("NestedParent_Unpack");
                if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
            }
            playerNear = false;
        }

        if (playerNear && MobileInput.GetKeyDown(KeyCode.E) && !isOpened)
        {
            OpenKeypad();
        }

        // Permitir cerrar pulsando Escape o Tab si está abierto, o haciendo click/tap fuera del teclado
        if (isOpened)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
            {
                CloseKeypad();
            }
            else if (Input.GetMouseButtonDown(0) && Time.frameCount > openedFrame)
            {
                Vector3 mousePos = Input.mousePosition;
                float guiMouseX = mousePos.x;
                float guiMouseY = Screen.height - mousePos.y;

                // Definir el área del cuadro del teclado real (320x440 en el centro) para evitar cierres erróneos al pulsar teclas
                Rect boxRect = new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 220f, 320f, 440f);
                if (!boxRect.Contains(new Vector2(guiMouseX, guiMouseY)))
                {
                    CloseKeypad();
                }
            }
        }
    }

    void OpenKeypad()
    {
        isOpened = true;
        openedFrame = Time.frameCount;
        currentInput = "";
        
        if (screenText != null)
        {
            screenText.text = "ENTER";
            screenText.color = new Color(0.05f, 0.12f, 0.15f); // Negro azulado
        }

        // Bloquear movimiento de jugador
        var controller = player.GetComponent<StarterAssets.FirstPersonController>();
        if (controller != null) controller.enabled = false;

        // Liberar cursor de forma segura para plataformas
        MobileInput.SetCursorState(false);
    }

    void CloseKeypad()
    {
        isOpened = false;

        if (screenText != null && !isUnlocked)
        {
            screenText.text = "LOCKED";
            screenText.color = new Color(0.05f, 0.12f, 0.15f); // Negro azulado
        }

        // Desbloquear movimiento del jugador
        var controller = player.GetComponent<StarterAssets.FirstPersonController>();
        if (controller != null) controller.enabled = true;

        // Bloquear cursor de nuevo de forma segura
        MobileInput.SetCursorState(true);
    }

    private Texture2D panelTex;
    private Texture2D displayTex;
    private Texture2D btnNormalTex;
    private Texture2D btnClearTex;
    private Texture2D btnEnterTex;
    private Texture2D btnCloseTex;

    private void EnsureTexturesCreated()
    {
        if (panelTex != null) return;

        panelTex = CreatePanelTexture(340, 460);
        displayTex = CreateDisplayTexture(290, 60);
        btnNormalTex = CreateBeveledTexture(80, 60, new Color(0.20f, 0.23f, 0.26f));
        btnClearTex = CreateBeveledTexture(80, 60, new Color(0.55f, 0.15f, 0.15f));
        btnEnterTex = CreateBeveledTexture(80, 60, new Color(0.15f, 0.50f, 0.20f));
        btnCloseTex = CreateBeveledTexture(130, 32, new Color(0.18f, 0.20f, 0.23f));
    }

    private Texture2D CreatePanelTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color baseDark = new Color(0.09f, 0.11f, 0.13f, 1.0f);
        Color borderLight = new Color(0.42f, 0.46f, 0.50f, 1.0f);
        Color borderDark = new Color(0.03f, 0.04f, 0.05f, 1.0f);
        Color rivetColor = new Color(0.65f, 0.68f, 0.72f, 1.0f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x < 4 || y >= h - 4) tex.SetPixel(x, y, borderLight);
                else if (x >= w - 4 || y < 4) tex.SetPixel(x, y, borderDark);
                else if (x < 8 || x >= w - 8 || y < 8 || y >= h - 8) tex.SetPixel(x, y, new Color(0.16f, 0.18f, 0.20f, 1.0f));
                else tex.SetPixel(x, y, baseDark);

                // Remaches metálicos de seguridad en las 4 esquinas
                bool isRivet = ((x >= 14 && x <= 22) && (y >= 14 && y <= 22)) ||
                               ((x >= w - 22 && x <= w - 14) && (y >= 14 && y <= 22)) ||
                               ((x >= 14 && x <= 22) && (y >= h - 22 && y <= h - 14)) ||
                               ((x >= w - 22 && x <= w - 14) && (y >= h - 22 && y <= h - 14));
                if (isRivet) tex.SetPixel(x, y, rivetColor);
            }
        }
        tex.Apply();
        return tex;
    }

    private Texture2D CreateDisplayTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color border = new Color(0.10f, 0.45f, 0.20f, 1.0f);
        Color background = new Color(0.02f, 0.06f, 0.03f, 1.0f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x < 3 || x >= w - 3 || y < 3 || y >= h - 3) tex.SetPixel(x, y, border);
                else tex.SetPixel(x, y, background);
            }
        }
        tex.Apply();
        return tex;
    }

    private Texture2D CreateBeveledTexture(int w, int h, Color baseCol)
    {
        Texture2D tex = new Texture2D(w, h);
        Color lightBevel = baseCol * 1.5f; lightBevel.a = 1.0f;
        Color darkBevel = baseCol * 0.4f; darkBevel.a = 1.0f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (y >= h - 3 || x < 3) tex.SetPixel(x, y, lightBevel);
                else if (y < 3 || x >= w - 3) tex.SetPixel(x, y, darkBevel);
                else tex.SetPixel(x, y, baseCol);
            }
        }
        tex.Apply();
        return tex;
    }

    void OnGUI()
    {
        if (isUnlocked) return;

        // 1. Mostrar prompt flotante de interacción si está cerca
        if (playerNear && !isOpened)
        {
            GUIStyle promptStyle = new GUIStyle();
            promptStyle.fontSize = 22;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.fontStyle = FontStyle.Bold;
            promptStyle.normal.textColor = new Color(0.3f, 0.75f, 1f);

            Rect promptRect = new Rect(Screen.width / 2 - 250, Screen.height - 120, 500, 40);
            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(promptRect.x - 10, promptRect.y - 5, promptRect.width + 20, promptRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(promptRect, "[E]  Ingresar Código de Seguridad", promptStyle);
        }

        // 2. Dibujar la Interfaz del Teclado Numérico si está abierto
        if (isOpened)
        {
            EnsureTexturesCreated();

            Rect boxRect = new Rect(Screen.width / 2 - 170, Screen.height / 2 - 230, 340, 460);

            // Chasis Metálico Industrial con Remaches
            GUI.DrawTexture(boxRect, panelTex);

            // Título superior estilo chasis de seguridad industrial
            GUIStyle titleStyle = new GUIStyle();
            titleStyle.fontSize = 15;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = new Color(0.70f, 0.75f, 0.80f);
            Rect titleRect = new Rect(boxRect.x, boxRect.y + 12, boxRect.width, 25);
            GUI.Label(titleRect, "•  SECURITY KEYPAD SYSTEM  •", titleStyle);

            // Pantalla Pantalla LCD Verde Neón
            Rect displayRect = new Rect(boxRect.x + 25, boxRect.y + 45, 290, 60);
            GUI.DrawTexture(displayRect, displayTex);

            GUIStyle displayStyle = new GUIStyle();
            displayStyle.fontSize = 30;
            displayStyle.fontStyle = FontStyle.Bold;
            displayStyle.alignment = TextAnchor.MiddleCenter;
            displayStyle.normal.textColor = isUnlocked ? new Color(0.2f, 1.0f, 0.3f) : new Color(0.1f, 1.0f, 0.25f);

            string displayText = "";
            for (int i = 0; i < 7; i++)
            {
                if (i < currentInput.Length) displayText += currentInput[i] + " ";
                else displayText += "_ ";
            }
            GUI.Label(displayRect, displayText.Trim(), displayStyle);

            // Botones numéricos estilo teclas industriales en relieve 3D
            float btnW = 80f;
            float btnH = 60f;
            float spacingX = 15f;
            float spacingY = 15f;
            float startX = boxRect.x + 35f;
            float startY = boxRect.y + 125f;

            string[] buttons = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "E" };

            for (int i = 0; i < buttons.Length; i++)
            {
                int row = i / 3;
                int col = i % 3;

                float bx = startX + col * (btnW + spacingX);
                float by = startY + row * (btnH + spacingY);
                Rect btnRect = new Rect(bx, by, btnW, btnH);

                Texture2D targetTex = btnNormalTex;
                Color textColor = Color.white;
                int txtSize = 24;

                if (buttons[i] == "C")
                {
                    targetTex = btnClearTex;
                    textColor = new Color(1.0f, 0.85f, 0.85f);
                    txtSize = 22;
                }
                else if (buttons[i] == "E")
                {
                    targetTex = btnEnterTex;
                    textColor = new Color(0.85f, 1.0f, 0.85f);
                    txtSize = 22;
                }

                GUI.DrawTexture(btnRect, targetTex);

                GUIStyle bStyle = new GUIStyle();
                bStyle.fontSize = txtSize;
                bStyle.fontStyle = FontStyle.Bold;
                bStyle.alignment = TextAnchor.MiddleCenter;
                bStyle.normal.textColor = textColor;

                if (GUI.Button(btnRect, buttons[i], bStyle))
                {
                    OnButtonPressed(buttons[i]);
                }
            }

            // Botón Salir / Cerrar inferior
            Rect closeRect = new Rect(boxRect.x + (boxRect.width - 130) / 2, boxRect.y + boxRect.height - 45, 130, 32);
            GUI.DrawTexture(closeRect, btnCloseTex);

            GUIStyle cStyle = new GUIStyle();
            cStyle.fontSize = 14;
            cStyle.fontStyle = FontStyle.Bold;
            cStyle.alignment = TextAnchor.MiddleCenter;
            cStyle.normal.textColor = new Color(0.80f, 0.82f, 0.85f);

            if (GUI.Button(closeRect, "CERRAR", cStyle))
            {
                CloseKeypad();
            }
        }
    }

    void OnButtonPressed(string value)
    {
        if (value == "C")
        {
            currentInput = "";
            PlaySound(keySound, 0.7f);
            if (screenText != null)
            {
                screenText.text = "ENTER";
                screenText.color = new Color(0.05f, 0.12f, 0.15f);
            }
        }
        else if (value == "E")
        {
            if (currentInput == correctCode)
            {
                isUnlocked = true;
                PlaySound(successSound, 1.0f);
                
                if (screenText != null)
                {
                    screenText.text = "GRANTED";
                    screenText.color = new Color(0.02f, 0.35f, 0.05f); // Verde oscuro de alto contraste
                }

                if (ledRedRenderer != null)
                {
                    ledRedRenderer.material.color = new Color(0.15f, 0f, 0f);
                    ledRedRenderer.material.SetColor("_EmissionColor", Color.clear);
                }
                if (ledGreenRenderer != null)
                {
                    ledGreenRenderer.material.color = Color.green;
                    ledGreenRenderer.material.SetColor("_EmissionColor", Color.green * 1.5f);
                    ledGreenRenderer.material.EnableKeyword("_EMISSION");
                }

                CloseKeypad();
                
                if (targetProceduralDoor != null)
                {
                    targetProceduralDoor.isLocked = false;
                    targetProceduralDoor.ToggleDoor();
                }
                else if (targetOpenDoor != null)
                {
                    targetOpenDoor.isLocked = false;
                    if (targetOpenDoor.doorAnimator != null)
                    {
                        targetOpenDoor.doorAnimator.SetBool("isOpen", true);
                    }
                    if (targetOpenDoor.audioSource != null && targetOpenDoor.doorOpenSound != null)
                    {
                        targetOpenDoor.audioSource.PlayOneShot(targetOpenDoor.doorOpenSound);
                    }
                }
                else
                {
                    StartCoroutine(OpenDoorSmoothly());
                }
                
                PowerBox pBox = FindObjectOfType<PowerBox>();
                if (pBox != null) pBox.ShowMessage("¡ACCESO CONCEDIDO!\nOficina del Director abierta.", Color.green, 5f);
            }
            else
            {
                currentInput = "";
                PlaySound(errorSound, 1.0f);
                
                StartCoroutine(ShowTemporaryScreenMessage("DENIED", new Color(0.5f, 0.02f, 0.02f), 2.0f)); // Rojo oscuro

                PowerBox pBox = FindObjectOfType<PowerBox>();
                if (pBox != null) pBox.ShowMessage("CÓDIGO DE ACCESO INCORRECTO", Color.red, 3f);
            }
        }
        else
        {
            if (currentInput.Length < 7)
            {
                currentInput += value;
                PlaySound(keySound, 0.7f);
                if (screenText != null)
                {
                    screenText.text = new string('*', currentInput.Length);
                    screenText.color = new Color(0.05f, 0.12f, 0.15f);
                }
            }
        }
    }

    private IEnumerator ShowTemporaryScreenMessage(string msg, Color color, float duration)
    {
        if (screenText != null)
        {
            screenText.text = msg;
            screenText.color = color;
        }
        yield return new WaitForSeconds(duration);
        if (screenText != null && !isUnlocked)
        {
            screenText.text = isOpened ? (currentInput == "" ? "ENTER" : new string('*', currentInput.Length)) : "LOCKED";
            screenText.color = new Color(0.05f, 0.12f, 0.15f);
        }
    }

    void PlaySound(AudioClip clip, float vol)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, vol);
        }
    }

    private IEnumerator OpenDoorSmoothly()
    {
        if (officeDoor == null) yield break;

        float elapsed = 0f;
        float duration = 1.5f;
        Quaternion startRot = officeDoor.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 90f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            officeDoor.localRotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            yield return null;
        }
        
        Debug.Log("KeypadController: Puerta abierta con éxito.");
    }
}

