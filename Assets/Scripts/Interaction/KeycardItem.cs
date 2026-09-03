using UnityEngine;

public class KeycardItem : MonoBehaviour
{
    [Header("Ajustes")]
    [Tooltip("Distancia maxima para poder recoger la tarjeta.")]
    public float interactDistance = 6.0f;

    private Transform playerTransform;
    private bool playerNear = false;
    private float lookScore = -1f;

    // Sistema global de priorización de interacción por mirada
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
        else
        {
            if (score > bestScore)
            {
                bestTarget = obj;
                bestScore = score;
            }
        }
    }

    public static GameObject GetBestTarget()
    {
        if (Time.frameCount != lastFrameChecked)
        {
            return null;
        }
        return bestTarget;
    }

    void Start()
    {
        interactDistance = 4.0f;
        FindPlayer();

        // Destruir todos los BoxCollider viejos o gigantes del prefab original
        BoxCollider[] oldBoxes = GetComponents<BoxCollider>();
        foreach (BoxCollider b in oldBoxes)
        {
            if (Application.isPlaying) Destroy(b);
            else DestroyImmediate(b);
        }

        // Crear un único BoxCollider limpio, pequeño y perfecto
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(0.4f, 0.2f, 0.4f);
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
        if (isMenu) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Proximidad limpia: Si el jugador está a menos de 3.5 metros y mirando en dirección a la tarjeta
        float dist = Vector3.Distance(cam.transform.position, transform.position);
        if (dist <= 3.8f)
        {
            Vector3 dirToCard = (transform.position - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, dirToCard);

            // Si el jugador está mirando hacia el área del escritorio/tarjeta
            if (dot > 0.35f)
            {
                playerNear = true;

                if (MobileInput.GetKeyDown(KeyCode.E))
                {
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
            pBox.ForceKeycardBlackoutAndRoar();
            Debug.Log("KeycardItem: Apagón y rugido forzado dinámicamente al recoger la tarjeta.");
        }

        BookHeadAIController bh = FindFirstObjectByType<BookHeadAIController>();
        if (bh != null)
        {
            bh.OnKeycardCollected();
        }

        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }

        Debug.Log("KeycardItem: Tarjeta magnética recogida por el jugador.");
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

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_keycard"), style);

        style.normal.textColor = new Color(0.3f, 0.75f, 1f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_keycard"), style);
    }
}

