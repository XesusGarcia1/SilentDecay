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
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Buscar o crear la Luz de Punto (Point Light) colocada al frente del tablero del generador
        if (statusLight == null) statusLight = GetComponentInChildren<Light>();

        if (statusLight == null)
        {
            GameObject lightObj = new GameObject("Generator_PointLight");
            lightObj.transform.SetParent(transform, false);
            // Colocar ligeramente al frente y arriba del panel de control para que no quede atrapada dentro del modelo 3D
            lightObj.transform.localPosition = new Vector3(0f, 0.6f, 0.65f);

            statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.range = 6.0f;
            statusLight.shadows = LightShadows.None;
            statusLight.renderMode = LightRenderMode.ForcePixel; // Forzar renderizado por píxel en Unity
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

    void Update()
    {
        if (player != null)
        {
            playerNear = false;
            Camera cam = Camera.main;
            if (cam == null) cam = player.GetComponentInChildren<Camera>();
            if (cam == null) cam = FindAnyObjectByType<Camera>();

            if (cam != null)
            {
                BoxCollider bCol = GetComponent<BoxCollider>();
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                if (bCol != null)
                {
                    dist = Vector3.Distance(bCol.bounds.center, cam.transform.position);
                }

                float maxRange = 3.8f;
                if (dist <= maxRange)
                {
                    if (InteractionFocusManager.IsFocused(gameObject, maxRange))
                    {
                        playerNear = true;
                    }
                    else
                    {
                        foreach (Transform child in transform)
                        {
                            if (InteractionFocusManager.IsFocused(child.gameObject, maxRange))
                            {
                                playerNear = true;
                                break;
                            }
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

        if (playerNear && !isOn && (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            MobileInput.ePressedDown = false;
            ActivateGenerator();
        }

        UpdateVisuals();
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
            pBox.ShowMessage($"Subgenerador {generatorName} Encendido! (Restableciendo entrada de red)", Color.green, 4.0f);
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
