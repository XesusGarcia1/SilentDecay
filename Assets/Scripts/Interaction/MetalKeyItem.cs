using UnityEngine;

public class MetalKeyItem : MonoBehaviour
{
    [Header("Ajustes")]
    public float interactDistance = 3.2f;
    
    [Tooltip("El nombre único de esta llave (ej: Access_keys_mannequin). Si lo dejas vacío, usará el nombre del GameObject.")]
    public string keyID = "";
    
    private Transform playerTransform;
    private bool playerNear = false;
    
    // Inventario global para llaves específicas
    public static System.Collections.Generic.HashSet<string> collectedKeys = new System.Collections.Generic.HashSet<string>();
    
    // Compatibilidad hacia atrás (por si acaso)
    public static bool hasMetalKey = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasMetalKey = false;
        if (collectedKeys != null)
        {
            collectedKeys.Clear();
        }
        else
        {
            collectedKeys = new System.Collections.Generic.HashSet<string>();
        }
    }

    void Start()
    {
        // Auto-asignar ID si está vacío
        if (string.IsNullOrEmpty(keyID))
        {
            keyID = gameObject.name.Replace("(Clone)", "").Trim();
        }
        FindPlayer();

        // Destruir colliders defectuosos
        Collider[] oldColliders = GetComponents<Collider>();
        foreach (Collider c in oldColliders)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        // Crear una esfera de interacción perfecta de 0.4 metros en el mundo real, ignorando las escalas locas de los modelos FBX
        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.center = Vector3.zero;
        
        float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        if (maxScale > 0)
        {
            sphere.radius = 0.4f / maxScale;
        }
        else
        {
            sphere.radius = 0.4f;
        }
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
            CollectKey();
        }
    }

    void CollectKey()
    {
        hasMetalKey = true;
        if (!string.IsNullOrEmpty(keyID))
        {
            collectedKeys.Add(keyID);
        }

        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            pBox.ShowMessage("Llave obtenida. Ahora puedes abrir puertas bloqueadas.", new Color(0.9f, 0.8f, 0.1f), 4.0f);
        }

        AudioClip pickupSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/SoundKeys");
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }
        else
        {
            pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
            if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
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
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_metal_key"), style);

        style.normal.textColor = new Color(0.9f, 0.8f, 0.1f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_metal_key"), style);
    }
}
