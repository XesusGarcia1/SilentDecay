using UnityEngine;

namespace ModularHospital
{
    public class DrawerInteract : MonoBehaviour
    {
    [Header("Ajustes del Cajón")]
    public float slideDistance = 0.45f;
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
        closedLocalPos = transform.localPosition;
        // La dirección hacia afuera del cajón en este modelo es +Z local
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

        // Configurar BoxCollider de interacción amplio para el cajón
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }
        box.size = new Vector3(1.2f, 0.6f, 0.8f);
        box.center = new Vector3(0f, 0f, 0.2f);
    }

    // Busca automáticamente la tarjeta en los hijos del cajón si la referencia se perdió
    void TryAutoFindKeycard()
    {
        if (keycardInside != null) return;
        KeycardItem found = GetComponentInChildren<KeycardItem>(true);
        if (found != null)
        {
            keycardInside = found.gameObject;
            Debug.Log("DrawerInteract: Tarjeta encontrada automáticamente en hijos del cajón.");
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

        if (isFocused && MobileInput.GetKeyDown(KeyCode.E))
        {
            if (Time.unscaledTime < lastInteractTime + 0.35f) return;
            lastInteractTime = Time.unscaledTime;
            MobileInput.ePressedDown = false; // Consumir tap para evitar doble activación

            // Si el cajón está abierto y la tarjeta está dentro, presionar E recoge directamente la tarjeta de acceso
            if (isOpen && keycardInside != null)
            {
                ElevatorController.hasKeycard = true;
                PowerBox pBox = FindObjectOfType<PowerBox>();
                if (pBox != null)
                {
                    pBox.ShowMessage("Tarjeta de Acceso del Director recogida!", new Color(0.2f, 0.6f, 1f), 4f);
                    pBox.ForceKeycardBlackoutAndRoar();
                }
                AudioClip pickupSound = Resources.Load<AudioClip>("Interruptor");
                if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, keycardInside.transform.position, 1.0f);

                Destroy(keycardInside);
                keycardInside = null;
                Debug.Log("DrawerInteract: Tarjeta recogida directamente al interactuar con el cajón abierto.");
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

        // Auto-buscar tarjeta en hijos si no está asignada
        if (isOpen && keycardInside == null) TryAutoFindKeycard();

        // PRIORIDAD MÁXIMA: Si el cajón está abierto y la tarjeta está dentro → mostrar opción de recoger
        bool hasCard = isOpen && keycardInside != null;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        string prompt = hasCard ? LocalizationManager.Instance.Get("interact_keycard") : (isOpen ? LocalizationManager.Instance.Get("interact_drawer_close") : LocalizationManager.Instance.Get("interact_drawer_open"));
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
