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
    public float lastOpenedTime { get; private set; } = -999f;
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

    // Busca automáticamente la tarjeta ÚNICAMENTE en los hijos directos del cajón
    void TryAutoFindKeycard()
    {
        if (keycardInside != null) return;

        // Buscar componente KeycardItem únicamente en los hijos directos de este cajón
        KeycardItem found = GetComponentInChildren<KeycardItem>(true);
        if (found != null && found.transform.IsChildOf(transform))
        {
            keycardInside = found.gameObject;
        }
    }

    private bool IsItemFocusedInsideDrawer()
    {
        if (!isOpen) return false;

        GameObject curr = InteractionFocusManager.CurrentFocus;
        if (curr == null) return false;

        if (curr.GetComponent<BatteryItem>() != null || curr.GetComponentInParent<BatteryItem>() != null)
        {
            return true;
        }

        return false;
    }

    void Update()
    {
        // Animar desplazamiento suave del cajón
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPos, Time.deltaTime * openSpeed * 0.5f);

        // Auto-recuperar referencia de la tarjeta si se perdió
        if (isOpen) TryAutoFindKeycard();

        bool isFocused = InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (isFocused && IsItemFocusedInsideDrawer())
        {
            isFocused = false;
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
        lastOpenedTime = Time.unscaledTime;
        targetLocalPos = isOpen ? openLocalPos : closedLocalPos;

        AudioClip clipToPlay = isOpen ? openSound : closeSound;
        if (clipToPlay != null && audioSource != null)
        {
            audioSource.pitch = isOpen ? 0.9f : 1.1f;
            audioSource.PlayOneShot(clipToPlay, 0.8f);
        }

        // Auto-buscar tarjeta en hijos si la referencia está vacía
        if (isOpen && keycardInside == null) TryAutoFindKeycard();
        
        // Revelar la tarjeta de acceso al abrir
        if (keycardInside != null)
        {
            keycardInside.SetActive(isOpen);
        }
    }

    void OnGUI()
    {
        bool focused = InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (focused && IsItemFocusedInsideDrawer())
        {
            focused = false;
        }
        if (!focused) return;

        // Auto-buscar tarjeta en hijos directos si el cajón se abrió y la referencia estaba vacía
        if (isOpen && keycardInside == null) TryAutoFindKeycard();

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
