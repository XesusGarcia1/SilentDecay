using UnityEngine;
using System.Collections;

public class ProceduralDoorInteract : MonoBehaviour
{
    public float openAngle = 90f;
    public float speed = 4f;
    public bool isLocked = false;
    public float interactDistance = 3.2f;

    private Transform player;
    public bool playerNear = false;
    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion targetRot;
    
    private AudioSource audioSource;
    private AudioClip doorOpenSound;
    private AudioClip doorCloseSound;

    private float lastToggleTime = 0f;
    public float toggleCooldown = 0.25f;

    void Start()
    {
        // Desactivar Animators conflictivos para evitar giros continuos
        Animator[] anims = GetComponentsInChildren<Animator>(true);
        foreach (Animator a in anims)
        {
            if (a != null) a.enabled = false;
        }

        closedRot = transform.localRotation;
        targetRot = closedRot;

        FindPlayerReference();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        audioSource.spatialBlend = 0.85f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 4.0f;
        audioSource.maxDistance = 15.0f;

        if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Hospital/doorOpenSound2");
        if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Hospital/doorCloseSound2");
#if UNITY_EDITOR
        if (doorOpenSound == null) doorOpenSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dnk_Dev/HospitalHorrorPack/Models/Animation/doorOpenSound2.mp3");
        if (doorCloseSound == null) doorCloseSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dnk_Dev/HospitalHorrorPack/Models/Animation/doorCloseSound2.mp3");
#endif
        if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
    }

    private void FindPlayerReference()
    {
        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { player = cc.transform; }
        else {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        if (isLocked) return;

        if (player == null)
        {
            FindPlayerReference();
        }

        playerNear = false;

        if (player != null)
        {
            Camera cam = Camera.main;
            if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();

            if (cam != null)
            {
                // Calcular distancia a cualquier hijo o centro de la puerta
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                
                // Buscar si la cámara o el jugador apuntan/miran a la puerta
                if (dist <= 3.5f)
                {
                    bool isFocused = InteractionFocusManager.IsFocused(gameObject, 3.5f);
                    
                    // Si no detectó por el padre, probar con todos los hijos (mallas de la puerta)
                    if (!isFocused)
                    {
                        foreach (Transform child in transform)
                        {
                            if (InteractionFocusManager.IsFocused(child.gameObject, 3.5f))
                            {
                                isFocused = true;
                                break;
                            }
                        }
                    }

                    // Fallback de proximidad directa si está a menos de 2.2m mirando hacia la puerta
                    if (!isFocused && dist <= 2.2f)
                    {
                        Vector3 dirToDoor = (transform.position - cam.transform.position).normalized;
                        if (Vector3.Dot(cam.transform.forward, dirToDoor) > 0.35f)
                        {
                            isFocused = true;
                        }
                    }

                    if (isFocused)
                    {
                        playerNear = true;
                    }
                }
            }
        }

        // Detectar entrada de Tecla E (PC) o Boton USO (Móvil)
        bool ePressed = Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown;

        if (playerNear && ePressed && !isUIActive())
        {
            if (Time.unscaledTime < lastToggleTime + toggleCooldown) return;
            MobileInput.ePressedDown = false;
            ToggleDoor();
        }

        // Interpolar rotación
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRot, Time.deltaTime * speed * 35f);
    }

    public void ToggleDoor()
    {
        if (Time.unscaledTime < lastToggleTime + toggleCooldown && lastToggleTime > 0f) return;
        lastToggleTime = Time.unscaledTime;

        isOpen = !isOpen;
        targetRot = isOpen ? closedRot * Quaternion.Euler(0f, openAngle, 0f) : closedRot;
        
        if (audioSource != null)
        {
            audioSource.Stop();
            AudioClip clipToPlay = isOpen ? doorOpenSound : doorCloseSound;
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
        }
    }

    void OnGUI()
    {
        if (playerNear && !isUIActive())
        {
            GUIStyle promptStyle = new GUIStyle();
            promptStyle.fontSize = 20;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.fontStyle = FontStyle.Bold;
            promptStyle.normal.textColor = isLocked ? new Color(1f, 0.3f, 0.3f) : Color.white;

            Rect promptRect = new Rect(Screen.width / 2 - 200, Screen.height - 120, 400, 40);
            GUI.color = isLocked ? new Color(0.3f, 0f, 0f, 0.85f) : new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(promptRect.x - 10, promptRect.y - 5, promptRect.width + 20, promptRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string action = isLocked ? "Puerta Bloqueada" : (isOpen ? "Cerrar Puerta" : "Abrir Puerta");
            GUI.Label(promptRect, "[E] " + action, promptStyle);
        }
    }

    private bool isUIActive()
    {
        KeypadController kp = FindObjectOfType<KeypadController>();
        if (kp != null && kp.isOpened) return true;

        PlayerHealth ph = FindObjectOfType<PlayerHealth>();
        if (ph != null && ph.health <= 0f) return true;

        return false;
    }
}
