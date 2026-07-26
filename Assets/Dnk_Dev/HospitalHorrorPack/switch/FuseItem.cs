using UnityEngine;

public class FuseItem : MonoBehaviour
{
    [Header("Sonido")]
    public AudioClip pickupSound;

    [Header("Interaccion")]
    [Tooltip("Distancia maxima de interaccion (desde el jugador al borde del colisionador).")]
    public float interactDistance = 6.0f;

    private Transform playerTransform;
    private bool playerNear = false;
    private float lookScore = -1f;

    void Start()
    {
        if (interactDistance < 3.2f) interactDistance = 3.2f;
        FindPlayer();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }

        box.center = Vector3.zero;
        box.size = new Vector3(0.8f, 0.8f, 0.8f);

        // Si el prefab tiene una malla hija (Mesh_node), asegurarle también su collider para que el Raycast de la mirilla impacte 100% directo
        Transform meshChild = transform.Find("Mesh_node");
        if (meshChild != null)
        {
            BoxCollider childBox = meshChild.GetComponent<BoxCollider>();
            if (childBox == null) childBox = meshChild.gameObject.AddComponent<BoxCollider>();
            childBox.isTrigger = true;
            childBox.center = Vector3.zero;
            childBox.size = new Vector3(0.8f, 0.8f, 0.8f);
        }

        if (pickupSound == null)
            pickupSound = Resources.Load<AudioClip>("Interruptor");
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
        if (playerTransform == null)
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
            CollectFuse();
        }
    }

    void CollectFuse()
    {
        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            pBox.fusesCount++;
            pBox.ShowMessage($"Fusible de repuesto recogido! (Total: {pBox.fusesCount})", Color.green, 4f);
        }

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
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
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), "[E]  Recoger Fusible de Repuesto", style);

        style.normal.textColor = new Color(1f, 0.9f, 0.2f);
        GUI.Label(rect, "[E]  Recoger Fusible de Repuesto", style);
    }
}

