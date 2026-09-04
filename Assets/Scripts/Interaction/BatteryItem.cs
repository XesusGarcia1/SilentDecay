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

        // 1. Activar forzosamente todos los objetos mallas hijos (para corregir si 'battery' estaba apagado en el Inspector)
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t == null) continue;
            t.gameObject.SetActive(true);

            // Corregir escalas negativas (ej. Y: -4.32) que invierten mallas e invisibilizan superficies 3D
            Vector3 s = t.localScale;
            if (s.x < 0 || s.y < 0 || s.z < 0)
            {
                t.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }
        box.isTrigger = true;
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

        // Comprobar si la pila está dentro de un cajón (únicamente si es hijo del cajón)
        ModularHospital.DrawerInteract inDrawer = GetComponentInParent<ModularHospital.DrawerInteract>();
        if (inDrawer != null)
        {
            // Si el cajón está CERRADO o recién abriéndose (menos de 0.35s), jamás permitir interacción
            if (!inDrawer.isOpen || Time.unscaledTime < inDrawer.lastOpenedTime + 0.35f)
            {
                return;
            }
        }

        playerNear = InteractionFocusManager.IsFocused(gameObject, interactDistance);
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

        ModularHospital.DrawerInteract inDrawer = GetComponentInParent<ModularHospital.DrawerInteract>();

        bool isInsideDrawer = false;
        if (inDrawer != null)
        {
            isInsideDrawer = true;
            if (!inDrawer.isOpen) return; // Si el cajón está CERRADO, jamás mostrar prompt
        }

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        float posY = Screen.height - 120;
        Rect rect = new Rect(Screen.width / 2 - 260, posY, 520, 50);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_battery"), style);

        style.normal.textColor = new Color(0.1f, 0.85f, 0.1f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_battery"), style);
    }
}

