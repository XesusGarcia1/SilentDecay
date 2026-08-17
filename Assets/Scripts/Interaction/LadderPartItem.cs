using UnityEngine;
using StarterAssets;

public class LadderPartItem : MonoBehaviour
{
    [Header("Ajustes")]
    public float interactDistance = 3.2f;
    
    [Tooltip("ID único de esta pieza de escalera (ej: LadderComponent_1). Si lo dejas vacío, usará el nombre del GameObject.")]
    public string partID = "";
    
    private Transform playerTransform;
    private bool playerNear = false;
    
    // Inventario global de piezas de escalera recogidas
    public static System.Collections.Generic.HashSet<string> collectedParts = new System.Collections.Generic.HashSet<string>();
    
    // ¿El jugador ya está cargando una pieza pesada?
    public static bool isCarryingPart = false;

    // Reiniciar variables estáticas cada vez que se inicia el juego
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        collectedParts = new System.Collections.Generic.HashSet<string>();
        isCarryingPart = false;
    }

    void Start()
    {
        if (string.IsNullOrEmpty(partID))
        {
            partID = gameObject.name.Replace("(Clone)", "").Trim();
        }
        FindPlayer();

        // Crear collider de interacción
        Collider[] oldColliders = GetComponents<Collider>();
        foreach (Collider c in oldColliders)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.center = Vector3.zero;
        
        float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        if (maxScale > 0)
            sphere.radius = 0.5f / maxScale;
        else
            sphere.radius = 0.5f;
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null) { playerTransform = cc.transform; return; }
        
        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null) { playerTransform = pObj.transform; return; }
        
        GameObject pTag = GameObject.FindGameObjectWithTag("Player");
        if (pTag != null) { playerTransform = pTag.transform; return; }
        
        if (Camera.main != null) playerTransform = Camera.main.transform;
    }

    void Update()
    {
        var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.isMenuMode) return;

        if (playerTransform == null) FindPlayer();
        playerNear = InteractionFocusManager.IsFocused(gameObject, interactDistance);
    }

    void LateUpdate()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (isTarget && (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            MobileInput.ePressedDown = false;
            
            // No permitir tomar si ya carga una pieza pesada
            if (isCarryingPart)
            {
                PlayerMonologueManager.ShowDialogue("No puedo cargar otra pieza, esta cosa pesa demasiado.", 3.5f);
                return;
            }
            
            CollectPart();
        }
    }

    void CollectPart()
    {
        if (!string.IsNullOrEmpty(partID))
        {
            collectedParts.Add(partID);
        }

        // Marcar que el jugador carga una pieza pesada
        isCarryingPart = true;

        // Activar carga pesada en el jugador (reduce velocidad)
        FirstPersonController player = FindObjectOfType<FirstPersonController>();
        if (player != null)
        {
            player.isCarryingHeavy = true;
        }

        // Mostrar monólogo del jugador
        PlayerMonologueManager.ShowDialogue("Esta pieza pesa bastante... debo llevarla a la escalera rota.", 4.5f);

        // Sonido de recoger (Interruptor del hospital)
        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
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

        string message;
        Color textColor;

        if (isCarryingPart)
        {
            // Ya carga una pieza, mostrar en rojo
            GUI.color = new Color(0.2f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            message = "Ya cargas una pieza pesada";
            textColor = new Color(1f, 0.4f, 0.3f);
        }
        else
        {
            // Puede recoger
            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            message = LocalizationManager.Instance.Get("interact_ladder_part");
            textColor = new Color(0.6f, 0.85f, 1f);
        }

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), message, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, message, style);
    }
}
