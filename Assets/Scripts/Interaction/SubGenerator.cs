using UnityEngine;

public class SubGenerator : MonoBehaviour
{
    [Header("Ajustes del Subgenerador")]
    public string generatorName = "A";
    public bool isOn = false;
    public float interactDistance = 2.5f;

    [Header("Sonidos (Personalizables)")]
    public AudioClip activeLoopSound; // Sonido de motor en bucle cuando está encendido
    public AudioClip activateSound;   // Sonido transitorio al encenderse (clic/arranque)

    [Header("Referencias (Autoresueltas)")]
    public Light statusLight;
    public Renderer lightRenderer;
    public AudioSource audioSource;

    private Transform player;
    private bool playerNear = false;
    private Material lightMaterial;

    void Start()
    {
        // Encontrar al jugador en la escena
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Auto-resolver o crear la luz de estado interna (sin esfera flotante)
        if (statusLight == null) statusLight = GetComponentInChildren<Light>();
        if (lightRenderer == null) lightRenderer = GetComponentInChildren<Renderer>();

        if (statusLight == null)
        {
            GameObject lightObj = new GameObject("Generator_PointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.range = 5.0f;
            statusLight.shadows = LightShadows.None;
        }

        if (lightRenderer != null && lightMaterial == null)
        {
            lightMaterial = lightRenderer.material;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // Configurar sonido eléctrico en bucle (se activa al encender)
        audioSource.loop = true;
        audioSource.spatialBlend = 1f; // Sonido 3D
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 15f;
        
        if (activeLoopSound != null)
        {
            audioSource.clip = activeLoopSound;
        }
        else
        {
            audioSource.clip = Resources.Load<AudioClip>("activeLoopSound");
            if (audioSource.clip == null)
            {
                audioSource.clip = Resources.Load<AudioClip>("Ascensor_Viaje"); // Fallback secundario
            }
        }
        
        audioSource.volume = 0.4f;

        if (isOn && audioSource.clip != null)
        {
            audioSource.Play();
        }

        UpdateVisuals();
    }

    void Update()
    {
        if (player != null)
        {
            playerNear = false;
            Camera cam = Camera.main;
            if (cam != null)
            {
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                if (dist <= 3.0f)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 3.2f))
                    {
                        string n = hit.transform.name.ToLower();
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform) || n.Contains("gen"))
                        {
                            playerNear = true;
                        }
                    }
                }
            }
        }
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            playerNear = false;
        }

        if (playerNear && !isOn && MobileInput.GetKeyDown(KeyCode.E))
        {
            ActivateGenerator();
        }
    }

    void ActivateGenerator()
    {
        isOn = true;
        
        // Rotar palanca física hacia abajo para simular accionamiento
        Transform lever = transform.Find("PanelControl/Palanca_Hinge");
        if (lever != null)
        {
            lever.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        }
        isOn = true;
        
        // Reproducir sonido en bucle
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }

        // Tocar sonido de clic/interruptor
        AudioClip clickSound = activateSound != null ? activateSound : Resources.Load<AudioClip>("Interruptor");
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, 1.0f);
        }

        UpdateVisuals();

        // Buscar caja de fusibles central para notificar y comprobar si ya se activaron ambos
        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            pBox.ShowMessage($"Subgenerador {generatorName} Encendido! (Restableciendo entrada de red)", Color.green, 4.0f);
            
            // Verificar si el otro generador también está encendido
            SubGenerator[] allGens = FindObjectsOfType<SubGenerator>();
            int activeCount = 0;
            foreach (var gen in allGens)
            {
                if (gen != null && gen.isOn) activeCount++;
            }

            if (activeCount >= 2)
            {
                pBox.ShowMessage("¡Subgeneradores A y B listos!\nCaja de fusibles central energizada.", new Color(0.2f, 0.8f, 1f), 5f);
            }
        }
        Debug.Log($"SubGenerator: Subgenerador {generatorName} activado.");
    }

    void UpdateVisuals()
    {
        // Únicamente controlar la luz proyectada (brillo ambiental), sin alterar las texturas ni materiales del modelo del generador
        Color lightColor = isOn ? new Color(0.1f, 1.0f, 0.3f) : new Color(1.0f, 0.08f, 0.08f);

        if (statusLight != null)
        {
            statusLight.enabled = true;
            statusLight.color = lightColor;
            statusLight.intensity = isOn ? 3.0f : 2.0f;
            statusLight.range = 5.0f;
        }
    }

    void OnGUI()
    {
        if (playerNear && !isOn)
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

            GUI.Label(promptRect, $"[E] Activar Subgenerador {generatorName}", promptStyle);
        }
    }
}

