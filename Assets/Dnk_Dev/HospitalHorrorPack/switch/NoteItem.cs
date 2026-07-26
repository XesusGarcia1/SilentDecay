using UnityEngine;

public class NoteItem : MonoBehaviour
{
    [Header("Ajustes del Dígito")]
    [Tooltip("Posición del dígito en la clave (1 a 7)")]
    public int digitPosition = 1; 
    [Tooltip("Valor del dígito (0 a 9)")]
    public int digitValue = 0;
    public float interactDistance = 8.0f; // Aumentado para interactuar cómodamente de lejos

    private Transform player;
    private bool playerNear = false;
    private float lookScore = -1f;

    void Start()
    {
        interactDistance = 3.2f; // Rango de interacción ampliado para tomar cómodamente de lejos
        FindPlayer();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null)
        {
            player = cc.transform;
            return;
        }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null)
        {
            player = pObj.transform;
            return;
        }

        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null)
        {
            player = playerTagObj.transform;
            return;
        }

        if (Camera.main != null)
        {
            player = Camera.main.transform;
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
        }

        playerNear = InteractionFocusManager.IsFocused(gameObject, interactDistance);
    }

    void LateUpdate()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (isTarget && MobileInput.GetKeyDown(KeyCode.E))
        {
            CollectNote();
        }
    }

    void CollectNote()
    {
        ElevatorController.RegisterNote(digitPosition, digitValue);

        // Cargar el sonido realista de tomar/hojear papel
        AudioClip pickupSound = Resources.Load<AudioClip>("Nota_Grab");
        if (pickupSound != null)
        {
            // Reproducir en la cámara principal para que se escuche directamente en los oídos del jugador
            Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(pickupSound, playPos, 1.0f);
        }

        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            pBox.ShowMessage($"Nota de clave recogida: Dígito #{digitPosition} es {digitValue}", Color.yellow, 4.5f);
        }

        Destroy(gameObject);
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
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), "[E]  Leer Nota de Seguridad", style);

        style.normal.textColor = new Color(1f, 0.9f, 0.2f);
        GUI.Label(rect, "[E]  Leer Nota de Seguridad", style);
    }
}

