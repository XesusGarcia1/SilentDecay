using UnityEngine;

public class LoreNoteItem : MonoBehaviour
{
    [Header("Ajustes de Lore")]
    [Tooltip("ID único de la nota de historia")]
    public int loreId = 1;
    [Tooltip("Título del documento")]
    public string noteTitle = "Diario Envejecido";
    [TextArea(5, 12)]
    public string noteBody = "Texto de historia...";

    public float interactDistance = 4.5f;

    private Transform player;
    private bool playerNear = false;
    private bool isReading = false;

    private Texture2D paperReadingTex;
    private GUIStyle contentStyle;
    private GUIStyle titleStyle;
    private GUIStyle closeStyle;
    private Light glowLight;

    void Start()
    {
        FindPlayer();

        // 1. APLICAR TINTE DE MATERIAL ENVEJECIDO (OPCIÓN C)
        ApplyEnvejecidoMaterial();

        // Configurar colisionador
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }

        box.center = Vector3.zero;
        box.size = new Vector3(0.45f, 0.3f, 0.45f); // Tamaño local proporcional al escalado visual de la nota
        interactDistance = 6.0f; // Aumentar distancia para interactuar cómodamente en mapas de cualquier tamaño/escala

        // Textura para la lectura
        paperReadingTex = new Texture2D(2, 2);
        Color paperColor = new Color(0.92f, 0.88f, 0.72f, 0.98f); // Fondo beige pergamino
        paperReadingTex.SetPixel(0, 0, paperColor); paperReadingTex.SetPixel(0, 1, paperColor);
        paperReadingTex.SetPixel(1, 0, paperColor); paperReadingTex.SetPixel(1, 1, paperColor);
        paperReadingTex.Apply();

        // 2. CREAR LUZ DE GUÍA CÁLIDA PULSANTE PARA LA OSCURIDAD
        GameObject lightObj = new GameObject("LoreNote_GlowLight");
        lightObj.transform.SetParent(this.transform);
        lightObj.transform.localPosition = new Vector3(0f, 0.12f, 0f); // Ligeramente levantada sobre el papel

        glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.9f, 0.65f, 0.35f); // Tinte ámbar/cálido de papel viejo
        glowLight.range = 2.5f * transform.lossyScale.x; // Escalar rango con la nota
        glowLight.intensity = 0.6f;
        glowLight.shadows = LightShadows.None; // Optimizado: sin sombras dinámicas
    }

    private void ApplyEnvejecidoMaterial()
    {
        // Obtener el renderizador del objeto o de sus hijos
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null && rend.material != null)
        {
            // Tinte amarillento/pergamino envejecido para diferenciarlo visualmente en 3D
            rend.material.color = new Color(0.82f, 0.68f, 0.44f, 1.0f);
        }
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null) { player = cc.transform; return; }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null) { player = pObj.transform; return; }

        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null) { player = playerTagObj.transform; return; }

        if (Camera.main != null) player = Camera.main.transform;
    }

    void Update()
    {
        // Hacer parpadear/pulsar la luz de guía de forma natural
        if (glowLight != null)
        {
            glowLight.intensity = (0.2f + Mathf.PingPong(Time.unscaledTime * 0.7f, 0.5f)) * Mathf.Min(2f, transform.lossyScale.x);
        }

        if (isReading)
        {
            // Cerrar con Escape, E, Tab o Clic
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab))
            {
                CloseReading();
            }
            return;
        }

        if (player == null) FindPlayer();

        float dist = player != null ? Vector3.Distance(transform.position, player.position) : 999f;
        if (dist > interactDistance)
        {
            playerNear = false;
            return;
        }

        // Raycast de mirilla
        bool isHitDirectly = false;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactDistance))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                {
                    isHitDirectly = true;
                }
            }
        }

        playerNear = isHitDirectly;
    }

    void LateUpdate()
    {
        if (playerNear && !isReading && MobileInput.GetKeyDown(KeyCode.E))
        {
            CollectAndReadLore();
        }
    }

    private void CollectAndReadLore()
    {
        isReading = true;
        Time.timeScale = 0f; // Pausar partida para lectura

        // Apagar la luz de guía durante la lectura a pantalla completa
        if (glowLight != null) glowLight.enabled = false;

        // Liberar cursor
        MobileInput.SetCursorState(false);

        // Desactivar controles de movimiento del jugador
        SetPlayerControlsActive(false);

        // Reproducir sonido de papel
        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Hospital/Nota_Grab");
        if (pickupSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, Camera.main.transform.position, 1.0f);
        }

        // Registrar la nota recogida en NotepadUIManager con textos traducidos
        string finalTitle = noteTitle;
        string finalBody = noteBody;
        if (LocalizationManager.Instance != null)
        {
            string keyPrefix = loreId <= 3 ? "lore_hosp" : "lore_tunn";
            int keyNum = loreId <= 3 ? loreId : loreId - 3;
            finalTitle = LocalizationManager.Instance.Get($"{keyPrefix}_title_{keyNum}");
            finalBody = LocalizationManager.Instance.Get($"{keyPrefix}_body_{keyNum}");
        }

        NotepadUIManager.RegisterLoreNote(loreId, finalTitle, finalBody);

        // Gatillo narrativo: Primera nota de lore
        if (loreId == 1 && LocalizationManager.Instance != null)
        {
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get("monologue_lore_1"), 6.0f);
        }
        else if (loreId == 2 && LocalizationManager.Instance != null)
        {
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get("monologue_lore_2"), 6.0f);
        }
        else if (loreId == 4 && LocalizationManager.Instance != null)
        {
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get("monologue_lore_4"), 6.5f);
        }
        else if (loreId == 5 && LocalizationManager.Instance != null)
        {
            PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get("monologue_lore_5"), 6.0f);
        }
    }

    private void CloseReading()
    {
        isReading = false;
        Time.timeScale = 1f; // Reanudar partida

        // Re-bloquear cursor
        MobileInput.SetCursorState(true);
        SetPlayerControlsActive(true);

        Destroy(gameObject); // Desaparece del suelo
    }

    private void SetPlayerControlsActive(bool active)
    {
        GameObject p = GameObject.Find("NestedParent_Unpack");
        if (p == null) p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            var controller = p.GetComponentInChildren<StarterAssets.FirstPersonController>();
            if (controller != null) controller.enabled = active;
        }
    }

    private void OnGUI()
    {
        if (isReading)
        {
            DrawFullscreenReading();
            return;
        }

        // DIBUJAR MIRILLA / HUD DE INTERACCIÓN (OPCIÓN C)
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (!isTarget) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        // Fondo oscuro
        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string interactionMsg = "[E]  Examinar Registro / Diario";
        if (LocalizationManager.Instance != null)
        {
            interactionMsg = LocalizationManager.Instance.Get("interact_lore_note");
        }

        // Texto con sombra negra
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), interactionMsg, style);

        // Texto naranja distintivo para Notas de Lore
        style.normal.textColor = new Color(1f, 0.5f, 0.1f); // Naranja/Rojo cálido
        GUI.Label(rect, interactionMsg, style);
    }

    private void DrawFullscreenReading()
    {
        // 1. Dibujar fondo oscuro traslúcido completo
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. Rectángulo de papel pergamino centrado
        int w = Mathf.Min(600, Screen.width - 40);
        int h = Mathf.Min(560, Screen.height - 60);
        Rect paperRect = new Rect(Screen.width / 2 - w / 2, Screen.height / 2 - h / 2, w, h);

        GUI.DrawTexture(paperRect, paperReadingTex);

        // Estilos de texto para la nota
        if (contentStyle == null)
        {
            contentStyle = new GUIStyle();
            contentStyle.fontSize = 17;
            contentStyle.wordWrap = true;
            contentStyle.normal.textColor = new Color(0.12f, 0.12f, 0.12f, 1f); // Gris oscuro/Carboncillo legible
            contentStyle.alignment = TextAnchor.UpperLeft;
            contentStyle.richText = true;

            titleStyle = new GUIStyle(contentStyle);
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.fontSize = 18;
            closeStyle.fontStyle = FontStyle.Bold;
        }

        // Recuperar títulos y cuerpos traducidos para mostrar en la pantalla
        string finalTitle = noteTitle;
        string finalBody = noteBody;
        if (LocalizationManager.Instance != null)
        {
            string keyPrefix = loreId <= 3 ? "lore_hosp" : "lore_tunn";
            int keyNum = loreId <= 3 ? loreId : loreId - 3;
            finalTitle = LocalizationManager.Instance.Get($"{keyPrefix}_title_{keyNum}");
            finalBody = LocalizationManager.Instance.Get($"{keyPrefix}_body_{keyNum}");
        }

        // Margen y dibujo de texto
        GUILayout.BeginArea(new Rect(paperRect.x + 35, paperRect.y + 35, paperRect.width - 70, paperRect.height - 110));
        
        GUILayout.Label(finalTitle, titleStyle);
        GUILayout.Space(20);
        GUILayout.Label(finalBody, contentStyle);

        GUILayout.EndArea();

        // Botón inferior para cerrar la lectura
        float btnW = 200f;
        float btnH = 45f;
        Rect closeBtnRect = new Rect(paperRect.x + paperRect.width / 2f - btnW / 2f, paperRect.y + paperRect.height - 70f, btnW, btnH);
        
        if (GUI.Button(closeBtnRect, "Cerrar [E]", closeStyle))
        {
            CloseReading();
        }
    }
}
