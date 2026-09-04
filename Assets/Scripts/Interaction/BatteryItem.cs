using UnityEngine;

public class BatteryItem : MonoBehaviour
{
    [Header("Ajustes")]
    [Tooltip("Distancia maxima para poder recoger la pila.")]
    public float interactDistance = 6.0f;
    [Tooltip("Porcentaje de bateria que recarga (0 a 100).")]
    public float rechargeAmount = 40f;

    private Transform playerTransform;
    private bool playerNear = false;
    private float lookScore = -1f;

    void Start()
    {
        rechargeAmount = 60f; // Cada pila recarga un 60% de energía
        interactDistance = 2.5f; // Distancia natural y cercana (2.5m)
        FindPlayer();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }
        box.size = new Vector3(0.4f, 0.4f, 0.4f);
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null)
        {
            playerTransform = cc.transform;
            return;
        }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null)
        {
            playerTransform = pObj.transform;
            return;
        }

        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null)
        {
            playerTransform = playerTagObj.transform;
            return;
        }

        if (Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        bool isMenu = modGen != null && modGen.isMenuMode;
        if (isMenu)
        {
            playerNear = false;
            lookScore = -1f;
            if (KeycardItem.bestTarget == gameObject)
            {
                KeycardItem.RegisterTarget(null, -1f);
            }
            return;
        }

        if (playerTransform == null)
        {
            FindPlayer();
        }

        playerNear = false;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist <= interactDistance)
            {
                Vector3 dir = (transform.position - cam.transform.position).normalized;
                if (Vector3.Dot(cam.transform.forward, dir) > 0.35f)
                {
                    // Verificar que no haya pared entre la cámara y la pila
                    bool blocked = Physics.Raycast(
                        cam.transform.position, dir, dist - 0.05f,
                        ~LayerMask.GetMask("Player", "Ignore Raycast"),
                        QueryTriggerInteraction.Ignore
                    );
                    if (!blocked && InteractionFocusManager.IsFocused(gameObject, interactDistance))
                    {
                        playerNear = true;
                    }
                }
            }
        }
    }

    void LateUpdate()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (isTarget && (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            MobileInput.ePressedDown = false;
            CollectBattery();
        }
    }

    void CollectBattery()
    {
        FlashlightController fc = FindObjectOfType<FlashlightController>();
        if (fc != null)
        {
            fc.Recharge(rechargeAmount);
        }

        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            string msg = LocalizationManager.Instance != null 
                ? LocalizationManager.Instance.GetFormat("msg_battery_picked", rechargeAmount)
                : $"Pila de repuesto recogida! Batería cargada +{rechargeAmount}%";
            pBox.ShowMessage(msg, Color.green, 3.5f);
        }

        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }

        // En lugar de destruirla, la ocultamos para que el BatteriesForTheMap pueda regenerarla.
        gameObject.SetActive(false);
    }

    void OnGUI()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (!isTarget) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_battery"), style);

        style.normal.textColor = new Color(0.1f, 0.85f, 0.1f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_battery"), style);
    }
}

