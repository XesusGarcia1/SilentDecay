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
            Collider c = GetComponent<Collider>();
            if (c != null) genCenter = c.bounds.center;

            float dist = Vector3.Distance(cam.transform.position, genCenter);
            float maxRange = 4.2f;

            if (dist <= maxRange)
            {
                // 1. Raycast de mirilla central directo
                if (InteractionFocusManager.IsFocused(gameObject, maxRange))
                {
                    playerNear = true;
                }
                else
                {
                    // 2. Comprobar todos los colliders hijos (a cualquier nivel)
                    Collider[] childCols = GetComponentsInChildren<Collider>();
                    foreach (var childCol in childCols)
                    {
                        if (childCol != null && InteractionFocusManager.IsFocused(childCol.gameObject, maxRange))
                        {
                            playerNear = true;
                            break;
                        }
                    }

                    // 3. Fallback infalible de proximidad y ángulo:
                    // Si el jugador está a menos de 3.2 metros y mirando en dirección general al generador
                    if (!playerNear && dist <= 3.2f)
                    {
                        Vector3 dirToGen = (genCenter - cam.transform.position).normalized;
                        float lookDot = Vector3.Dot(cam.transform.forward, dirToGen);
                        if (lookDot > 0.35f) // Ángulo amplio (~70°)
                        {
                            playerNear = true;
                        }
                    }
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
