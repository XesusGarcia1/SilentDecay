using UnityEngine;

public class SubGenerator : MonoBehaviour
{
    [Header("Ajustes del Subgenerador")]
    public string generatorName = "A";
    public string subgeneratorLetter = "A";
    public bool isOn = false;
    public bool isTurnedOn
    {
        get => isOn;
        set => isOn = value;
    }
    public float interactDistance = 2.5f;

    [Header("Sonidos (Personalizables)")]
    public AudioClip activeLoopSound;
    public AudioClip activateSound;

    [Header("Referencias de Luz")]
    public Light statusLight;
    public AudioSource audioSource;

    private Transform player;
    private bool playerNear = false;

    void Start()
    {
        // Asegurar que exista un Collider para interactuar y recibir raycasts
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.8f, 2.4f, 1.8f);
            bc.center = new Vector3(0f, 1.0f, 0f);
        }

        FindPlayerRef();

        // Buscar o crear la Luz de Punto (Point Light) colocada al frente del tablero del generador
        if (statusLight == null) statusLight = GetComponentInChildren<Light>();

        if (statusLight == null)
        {
            GameObject lightObj = new GameObject("Generator_PointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.6f, 0.65f);

            statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.range = 6.0f;
            statusLight.shadows = LightShadows.None;
            statusLight.renderMode = LightRenderMode.ForcePixel;
        }
        else
        {
            statusLight.type = LightType.Point;
            statusLight.range = 6.0f;
            statusLight.renderMode = LightRenderMode.ForcePixel;
            statusLight.transform.localPosition = new Vector3(0f, 0.6f, 0.65f);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

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
            audioSource.clip = Resources.Load<AudioClip>("Audio/Hospital/activeLoopSound");
            if (audioSource.clip == null) audioSource.clip = Resources.Load<AudioClip>("activeLoopSound");
            if (audioSource.clip == null)
            {
                audioSource.clip = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Viaje");
            }
        }
        
        audioSource.volume = 0.4f;

        if (isOn && audioSource.clip != null)
        {
            audioSource.Play();
        }

        UpdateVisuals();
    }

    private void FindPlayerRef()
    {
        if (player != null) return;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("PlayerMale");
        if (playerObj == null) playerObj = GameObject.Find("PlayerFemale");
        if (playerObj == null)
        {
            CharacterController cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) playerObj = cc.gameObject;
        }
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        FindPlayerRef();
        playerNear = false;

        Camera cam = Camera.main;
        if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            Vector3 genCenter = transform.position + Vector3.up * 1.0f;
            Renderer mr = GetComponentInChildren<Renderer>();
            if (mr != null) genCenter = mr.bounds.center;
            else
            {
                Collider c = GetComponentInChildren<Collider>();
                if (c != null) genCenter = c.bounds.center;
            }

            float dist = Vector3.Distance(cam.transform.position, genCenter);
            if (dist <= 6.0f)
            {
                Vector3 dirToGen = (genCenter - cam.transform.position).normalized;
                float lookDot = Vector3.Dot(cam.transform.forward, dirToGen);
                
                // Activar si está enfocado o a menos de 4.8m en el campo visual del jugador
                if (dist <= 4.8f && (lookDot > 0.05f || InteractionFocusManager.IsFocused(gameObject, 5.0f)))
                {
                    playerNear = true;
                }
            }
        }

        if (playerNear && !isOn && (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            MobileInput.ePressedDown = false;
            ActivateGenerator();
        }

        UpdateVisuals();
    }

    private void OnGUI()
    {
        if (Time.timeScale == 0f || NotepadUIManager.IsOpen) return;

        if (playerNear && !isOn)
        {
            GUIStyle pStyle = new GUIStyle();
            pStyle.fontSize = 22;
            pStyle.alignment = TextAnchor.MiddleCenter;
            pStyle.fontStyle = FontStyle.Bold;

            Rect pRect = new Rect(Screen.width / 2 - 240, Screen.height - 120, 480, 50);

            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(pRect.x - 10, pRect.y - 5, pRect.width + 20, pRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string promptText = $"[E]  Encender Subgenerador {generatorName}";

            pStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(pRect.x + 2, pRect.y + 2, pRect.width, pRect.height), promptText, pStyle);

            pStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUI.Label(pRect, promptText, pStyle);
        }
    }

    void ActivateGenerator()
    {
        isOn = true;
        
        Transform lever = transform.Find("PanelControl/Palanca_Hinge");
        if (lever != null)
        {
            lever.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        }
        
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }

        AudioClip clickSound = activateSound != null ? activateSound : Resources.Load<AudioClip>("Interruptor");
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, 1.0f);
        }

        BookHeadAIController bh = FindFirstObjectByType<BookHeadAIController>();
        if (bh != null)
        {
            bh.AlertNoiseAtPosition(transform.position);
        }

        Monsters.Amalgam.AmalgamAIController amalgam = FindFirstObjectByType<Monsters.Amalgam.AmalgamAIController>();
        if (amalgam != null)
        {
            amalgam.NotifyGeneratorActivated(transform.position);
        }

        UpdateVisuals();

        if (TunnelsFixedMapLogic.Instance != null)
        {
            TunnelsFixedMapLogic.Instance.OnSubGeneratorTurnedOn(this);
        }
        if (TunnelsGenerator.Instance != null)
        {
            TunnelsGenerator.Instance.OnSubGeneratorTurnedOn(this);
        }

        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            string msg = LocalizationManager.Instance != null 
                ? LocalizationManager.Instance.GetFormat("msg_subgen_active", generatorName) 
                : $"SUBGENERADOR {generatorName} ACTIVADO!";
            pBox.ShowMessage(msg, Color.green, 4.0f);
        }
        Debug.Log($"SubGenerator: Subgenerador {generatorName} activado.");
    }

    private void SetupStatusLight()
    {
        if (statusLight == null)
        {
            Transform existingLight = transform.Find("Generator_PointLight");
            if (existingLight != null) statusLight = existingLight.GetComponent<Light>();
        }

        if (statusLight == null)
        {
            GameObject lightObj = new GameObject("Generator_PointLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.7f, 0.7f);

            statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.range = 8.0f;
            statusLight.shadows = LightShadows.None;
            statusLight.renderMode = LightRenderMode.ForcePixel;
        }
        else
        {
            statusLight.type = LightType.Point;
            statusLight.range = 8.0f;
            statusLight.renderMode = LightRenderMode.ForcePixel;
            statusLight.transform.localPosition = new Vector3(0f, 0.7f, 0.7f);
        }
    }

    public void UpdateVisuals()
    {
        if (statusLight == null) SetupStatusLight();

        if (statusLight != null)
        {
            statusLight.enabled = true;
            statusLight.color = isOn ? new Color(0.1f, 1.0f, 0.2f) : new Color(1.0f, 0.05f, 0.05f);
            statusLight.intensity = isOn ? 7.5f : 4.5f;
            statusLight.range = 8.0f;
        }
    }
}
