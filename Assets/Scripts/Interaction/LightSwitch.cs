using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    public Light lightToToggle;  // La luz que se va a controlar
    public AudioClip switchSound; // Sonido del interruptor
    private AudioSource audioSource; // Componente AudioSource para reproducir el sonido
    public bool isOn = false;  // Estado de la luz (encendida o apagada)
    public float interactionDistance = 3f;  // Distancia para interactuar con el interruptor
    public Animator switchAnimator; // Referencia al Animator del interruptor
    
    private RoomLightsManager roomManager; // Referencia al RoomLightsManager
    private PowerBox powerBox;             // Referencia a la caja de fusibles
    private bool lastIsOnState; // Guarda el estado anterior de isOn
    private Transform player;
    private bool playerNear = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (lightToToggle == null)
        {
            lightToToggle = GetComponentInChildren<Light>();
        }

        roomManager = GetComponentInParent<RoomLightsManager>();
        powerBox = FindObjectOfType<PowerBox>();
        lastIsOnState = isOn;

        if (lightToToggle != null)
        {
            lightToToggle.enabled = isOn;
        }

        if (switchAnimator != null)
        {
            switchAnimator.SetBool("isOn", isOn);
        }

        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { player = cc.transform; }
        else {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            playerNear = dist <= interactionDistance;
            
            // Evitar interactuar a través de paredes usando el dot product
            if (playerNear)
            {
                Vector3 toPlayer = player.position - transform.position;
                bool playerInFront = Vector3.Dot(transform.forward, toPlayer.normalized) > 0.0f;
                if (!playerInFront)
                {
                    playerNear = false;
                }
                else
                {
                    // Evitar interacción doble verificando que el jugador mire hacia el interruptor
                    Transform cam = Camera.main != null ? Camera.main.transform : player;
                    Vector3 dirToTarget = (transform.position - cam.position).normalized;
                    float dot = Vector3.Dot(cam.forward, dirToTarget);
                    if (dot < 0.82f) // Mayor enfoque por ser un objeto pequeño
                    {
                        playerNear = false;
                    }
                }
            }
        }
        else
        {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            playerNear = false;
        }

        if (playerNear && MobileInput.GetKeyDown(KeyCode.E) && !isUIActive())
        {
            isOn = !isOn;
        }

        if (powerBox == null)
        {
            powerBox = FindObjectOfType<PowerBox>();
        }

        bool hasPowerOutage = (roomManager != null && roomManager.powerOutage) || (powerBox != null && powerBox.isPowerOut);

        if (hasPowerOutage)
        {
            if (lightToToggle != null && lightToToggle.enabled)
            {
                lightToToggle.enabled = false;
            }
        }
        else
        {
            bool isCritical = powerBox != null && (powerBox.currentPowerCapacity / powerBox.maxPowerCapacity) <= 0.2f;

            if (isCritical && isOn)
            {
                if (lightToToggle != null)
                {
                    if (Random.value < 0.25f)
                    {
                        lightToToggle.enabled = (Random.value < 0.5f);
                    }
                }
            }
            else
            {
                if (lightToToggle != null && lightToToggle.enabled != isOn)
                {
                    lightToToggle.enabled = isOn;
                }
            }
        }

        if (isOn != lastIsOnState)
        {
            if (audioSource != null && switchSound != null)
            {
                audioSource.PlayOneShot(switchSound);
            }
            lastIsOnState = isOn;
        }

        if (switchAnimator != null)
        {
            switchAnimator.SetBool("isOn", isOn);
        }

        // Animación programática del botón procedural (rotación del interruptor)
        Transform btn = transform.Find("Toggle_Button");
        if (btn != null)
        {
            float targetAngle = isOn ? 15f : -15f;
            btn.localRotation = Quaternion.Euler(targetAngle, 0f, 0f);
        }
    }

    void OnGUI()
    {
        if (playerNear && !isUIActive())
        {
            GUIStyle promptStyle = new GUIStyle();
            promptStyle.fontSize = 20;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.fontStyle = FontStyle.Bold;
            promptStyle.normal.textColor = Color.white;

            Rect promptRect = new Rect(Screen.width / 2 - 200, Screen.height - 120, 400, 40);
            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(promptRect.x - 10, promptRect.y - 5, promptRect.width + 20, promptRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(promptRect, "[E] Encender / Apagar Luz", promptStyle);
        }
    }

    private bool isUIActive()
    {
        KeypadController kp = FindObjectOfType<KeypadController>();
        if (kp != null && kp.isOpened) return true;

        PlayerHealth ph = FindObjectOfType<PlayerHealth>();
        if (ph != null && ph.health <= 0f) return true;

        return false;
    }
}

