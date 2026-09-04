using UnityEngine;

namespace ModularHospital
{
    public class DrawerInteract : MonoBehaviour
    {
    [Header("Ajustes del Cajón")]
    public float slideDistance = 0.28f; // Desplazamiento sutil y estético para no abrirse exageradamente
    public float openSpeed = 2.5f;    
    public float interactDistance = 4.5f;
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Estado")]
    public bool isOpen = false;
    private Vector3 closedLocalPos;
    private Vector3 openLocalPos;
    private Vector3 targetLocalPos;
    private float lastInteractTime;
    private AudioSource audioSource;

    [Header("Tarjeta de Acceso")]
    public GameObject keycardInside;

    void Start()
    {
        slideDistance = 0.28f;
        closedLocalPos = transform.localPosition;
        // La dirección hacia afuera del cajón hacia el frente del mueble es +Z local
        openLocalPos = closedLocalPos + new Vector3(0f, 0f, slideDistance);
        targetLocalPos = closedLocalPos;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.maxDistance = 10.0f;
        }

        if (openSound == null) openSound = Resources.Load<AudioClip>("Audio/Hospital/OpenDrawer");
        if (closeSound == null) closeSound = Resources.Load<AudioClip>("Audio/Hospital/CloseDrawer");

        // Configurar BoxCollider de interacción como TRIGGER para jamás bloquear el paso del jugador
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }
        box.isTrigger = true;
        box.size = new Vector3(0.8f, 0.6f, 0.8f);
        box.center = new Vector3(0f, 0f, 0.1f);
    }

    // Busca automáticamente la tarjeta en hijos, padres o cualquier objeto de tarjeta en la escena
    void TryAutoFindKeycard()
    {
        if (keycardInside != null && keycardInside.activeInHierarchy) return;

        // 1. Buscar componente KeycardItem en hijos del cajón o del mueble escritorio
        KeycardItem found = GetComponentInChildren<KeycardItem>(true);
        if (found == null && transform.parent != null)
        {
            found = transform.parent.GetComponentInChildren<KeycardItem>(true);
        }

        if (found != null)
        {
            keycardInside = found.gameObject;
            return;
        }

        // 2. Buscar cualquier objeto 3D en la escena cuyo nombre coincida con la tarjeta (card, tarjeta, elevator, keycard)
        Transform[] allTrans = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in allTrans)
        {
            if (t == null) continue;
            string tName = t.name.ToLower();
            if (tName.Contains("card") || tName.Contains("tarjeta") || tName.Contains("keycard") || tName.Contains("elevator"))
            {
                if (!tName.Contains("canvas") && !tName.Contains("ui") && !tName.Contains("controller") && !tName.Contains("manager") && !tName.Contains("door"))
                {
                    KeycardItem ki = t.GetComponent<KeycardItem>();
                    if (ki == null) ki = t.gameObject.AddComponent<KeycardItem>();

                    keycardInside = t.gameObject;
                    break;
                }
            }
        }
    }

    void Update()
    {
        // Animar desplazamiento suave del cajón
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPos, Time.deltaTime * openSpeed * 0.5f);

        // Auto-recuperar referencia de la tarjeta si se perdió
        if (isOpen) TryAutoFindKeycard();

        Camera cam = Camera.main;
        bool isFocused = InteractionFocusManager.IsFocused(gameObject);
        if (!isFocused && cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist <= interactDistance)
            {
                Vector3 dir = (transform.position - cam.transform.position).normalized;
                if (Vector3.Dot(cam.transform.forward, dir) > 0.2f)
                {
                    // Verificar que no haya pared entre la cámara y el cajón
                    bool blocked = Physics.Raycast(
                        cam.transform.position, dir, dist - 0.05f,
                        ~LayerMask.GetMask("Player", "Ignore Raycast"),
                        QueryTriggerInteraction.Ignore
                    );
                    if (!blocked) isFocused = true;
                }
            }
        }

        if (isFocused && (MobileInput.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            if (Time.unscaledTime < lastInteractTime + 0.35f) return;
            lastInteractTime = Time.unscaledTime;
            MobileInput.ePressedDown = false; // Consumir tap para evitar doble activación

            if (isOpen && keycardInside == null) TryAutoFindKeycard();

            // Si el cajón está abierto y la tarjeta está presente y activa → recoger tarjeta
            if (isOpen && keycardInside != null && keycardInside.activeInHierarchy)
            {
                ElevatorController.hasKeycard = true;
                PowerBox pBox = FindObjectOfType<PowerBox>();
                if (pBox != null)
                {
                    string msg = LocalizationManager.Instance != null 
                        ? LocalizationManager.Instance.Get("msg_keycard_picked") 
                        : "¡Tarjeta de Acceso del Director recogida!";
                    pBox.ShowMessage(msg, new Color(0.2f, 0.6f, 1f), 4f);

                    // Si estamos en el tutorial, NO provocar rugidos ni apagones
                    if (TutorialMapLogic.Instance == null)
                    {
                        pBox.ForceKeycardBlackoutAndRoar();
                    }
                }

                AudioClip pickupSound = Resources.Load<AudioClip>("Interruptor");
                if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
                if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, keycardInside.transform.position, 1.0f);

                keycardInside.SetActive(false);
                keycardInside = null;

                // Si estamos en el tutorial, disparar la victoria cinemática limpia
                if (TutorialMapLogic.Instance != null)
                {
                    TutorialMapLogic.Instance.TriggerTutorialVictory();
                }

                return;
            }

            ToggleDrawer();
        }
    }

    public void ToggleDrawer()
    {
        isOpen = !isOpen;
        targetLocalPos = isOpen ? openLocalPos : closedLocalPos;

        AudioClip clipToPlay = isOpen ? openSound : closeSound;
        if (clipToPlay != null && audioSource != null)
        {
            audioSource.pitch = isOpen ? 0.9f : 1.1f;
            audioSource.PlayOneShot(clipToPlay, 0.8f);
        }

        // Auto-buscar tarjeta en hijos si la referencia está vacía
        if (isOpen && keycardInside == null) TryAutoFindKeycard();
        
        // Revelar y posicionar la tarjeta de acceso al abrir
        if (keycardInside != null)
        {
            keycardInside.SetActive(isOpen);
            if (isOpen)
            {
                keycardInside.transform.localPosition = new Vector3(0f, 0.02f, -0.12f);
            }
        }
    }

    void OnGUI()
    {
        Camera cam = Camera.main;
        bool focused = InteractionFocusManager.IsFocused(gameObject);
        if (!focused && cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist <= interactDistance)
            {
                Vector3 dir = (transform.position - cam.transform.position).normalized;
                if (Vector3.Dot(cam.transform.forward, dir) > 0.2f)
                {
                    // Verificar que no haya pared entre la cámara y el cajón
                    bool blocked = Physics.Raycast(
                        cam.transform.position, dir, dist - 0.05f,
                        ~LayerMask.GetMask("Player", "Ignore Raycast"),
                        QueryTriggerInteraction.Ignore
                    );
                    if (!blocked) focused = true;
                }
            }
        }
        if (!focused) return;

        // Auto-buscar tarjeta si no está asignada
        if (isOpen && (keycardInside == null || !keycardInside.activeInHierarchy)) TryAutoFindKeycard();

        // PRIORIDAD MÁXIMA: Si el cajón está abierto y la tarjeta está dentro y activa → mostrar opción de recoger
        bool hasCard = isOpen && keycardInside != null && keycardInside.activeInHierarchy;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        string prompt = "";
        if (hasCard)
        {
            prompt = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("interact_keycard") : "[E]  Recoger Tarjeta de Acceso";
        }
        else if (isOpen)
        {
            prompt = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("interact_drawer_close") : "[E]  Cerrar Cajón";
        }
        else
        {
            prompt = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("interact_drawer_open") : "[E]  Abrir Cajón";
        }

        Color textColor = hasCard ? new Color(0.3f, 0.75f, 1f) : new Color(0.9f, 0.8f, 0.2f);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, style.fontSize + 20), prompt, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, prompt, style);
    }
}
}
