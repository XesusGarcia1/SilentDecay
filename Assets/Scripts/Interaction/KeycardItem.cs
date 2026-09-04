using UnityEngine;

public class KeycardItem : MonoBehaviour
{
    [Header("Ajustes")]
    public float interactDistance = 4.0f;

    private Transform playerTransform;
    private bool playerNear = false;

    public static int lastFrameChecked = -1;
    public static GameObject bestTarget = null;
    public static float bestScore = -1f;

    public static void RegisterTarget(GameObject obj, float score)
    {
        if (Time.frameCount != lastFrameChecked)
        {
            lastFrameChecked = Time.frameCount;
            bestTarget = obj;
            bestScore = score;
        }
        else if (score > bestScore)
        {
            bestTarget = obj;
            bestScore = score;
        }
    }

    public static GameObject GetBestTarget()
    {
        return Time.frameCount != lastFrameChecked ? null : bestTarget;
    }

    void Start()
    {
        interactDistance = 3.5f;
        FindPlayer();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(0.4f, 0.2f, 0.4f);
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null) { playerTransform = cc.transform; return; }
        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null) { playerTransform = pObj.transform; return; }
        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null) { playerTransform = playerTagObj.transform; return; }
        if (Camera.main != null) { playerTransform = Camera.main.transform; }
    }

    void Update()
    {
        var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.isMenuMode) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Si la tarjeta está dentro de un cajón y el cajón está CERRADO, no permitir interacción
        ModularHospital.DrawerInteract drawer = GetComponentInParent<ModularHospital.DrawerInteract>();
        if (drawer == null) drawer = FindFirstObjectByType<ModularHospital.DrawerInteract>();

        if (drawer != null)
        {
            float distToDrawer = Vector3.Distance(transform.position, drawer.transform.position);
            if (distToDrawer <= 2.2f && !drawer.isOpen)
            {
                playerNear = false;
                return;
            }
        }

        float dist = Vector3.Distance(cam.transform.position, transform.position);
        if (dist <= interactDistance)
        {
            Vector3 dirToCard = (transform.position - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, dirToCard);

            if (dot > 0.35f)
            {
                playerNear = true;

                if (MobileInput.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown)
                {
                    MobileInput.ePressedDown = false;
                    CollectKeycard();
                }
                return;
            }
        }

        playerNear = false;
    }

    void CollectKeycard()
    {
        ElevatorController.hasKeycard = true;

        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            string msg = LocalizationManager.Instance != null 
                ? LocalizationManager.Instance.Get("msg_keycard_picked_elev")
                : "¡Tarjeta del Director recogida! Dirígete al Ascensor de Escape.";
            pBox.ShowMessage(msg, new Color(0.2f, 0.6f, 1f), 4f);
            
            // Si estamos en el tutorial, NO provocar rugidos ni apagones
            if (TutorialMapLogic.Instance == null)
            {
                pBox.ForceKeycardBlackoutAndRoar();
            }
        }

        BookHeadAIController bh = FindFirstObjectByType<BookHeadAIController>();
        if (bh != null && TutorialMapLogic.Instance == null)
        {
            bh.OnKeycardCollected();
        }

        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Interruptor");
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }

        if (TutorialMapLogic.Instance != null)
        {
            TutorialMapLogic.Instance.TriggerTutorialVictory();
        }

        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (!playerNear) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string prompt = LocalizationManager.Instance != null 
            ? LocalizationManager.Instance.Get("interact_keycard") 
            : "[E]  Recoger Tarjeta de Acceso";

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), prompt, style);

        style.normal.textColor = new Color(0.3f, 0.75f, 1f);
        GUI.Label(rect, prompt, style);
    }
}
