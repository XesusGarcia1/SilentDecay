using UnityEngine;
using System.Collections;

public class ProceduralDoorInteract : MonoBehaviour
{
    public enum DoorType { Rotating, Sliding }
    public DoorType doorType = DoorType.Rotating;
    
    [Header("Rotating Door")]
    public float openAngle = 90f;
    [Tooltip("Activa esto si la puerta gira desde el centro como puerta giratoria.")]
    public bool autoFixCenterPivot = true; 
    public bool hingeOnRightSide = false;
    
    [Header("Sliding Door")]
    public Vector3 slideOffset = new Vector3(1.5f, 0, 0);

    public float speed = 4f;
    public bool isLocked = false;
    public float interactDistance = 3.2f;

    private Transform player;
    public bool playerNear = false;
    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion targetRot;
    private Vector3 closedPos;
    private Vector3 targetPos;
    private Transform pivotTransform;
    
    private AudioSource audioSource;
    private AudioClip doorOpenSound;
    private AudioClip doorCloseSound;
    private AudioClip doorLockedSound;

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

        pivotTransform = transform;

        // Auto-Fix para puertas cuyo modelo 3D tiene el pivote en el centro
        if (doorType == DoorType.Rotating && autoFixCenterPivot)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null)
            {
                Bounds b = mf.mesh.bounds;
                float edgeX = hingeOnRightSide ? b.max.x : b.min.x;
                
                GameObject hinge = new GameObject(gameObject.name + "_AutoHinge");
                hinge.transform.SetParent(transform.parent);
                
                // Poner la bisagra en el borde lateral de la puerta
                hinge.transform.position = transform.TransformPoint(new Vector3(edgeX, b.center.y, b.center.z));
                hinge.transform.rotation = transform.rotation;
                
                transform.SetParent(hinge.transform);
                pivotTransform = hinge.transform;
            }
        }

        closedRot = pivotTransform.localRotation;
        targetRot = closedRot;
        closedPos = pivotTransform.localPosition;
        targetPos = closedPos;

        FindPlayerReference();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        audioSource.spatialBlend = 0.85f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 4.0f;
        audioSource.maxDistance = 15.0f;

        if (gameObject.tag.Contains("Blocked"))
        {
            isLocked = true;
        }

        if (gameObject.tag.Contains("Metalic") || gameObject.tag.Contains("Metallic"))
        {
            doorOpenSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/OpenDoorMetalic");
            doorCloseSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/CloseDoorMetalic");
            doorLockedSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/metal-gate-door-knocking");
        }
        else
        {
            if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Hospital/doorOpenSound2");
            if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Hospital/doorCloseSound2");
            if (doorLockedSound == null) doorLockedSound = Resources.Load<AudioClip>("Audio/Hospital/doorCloseSound2");
        }

        if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (doorLockedSound == null) doorLockedSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
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

        // Interpolar rotación o posición según el tipo de puerta
        if (doorType == DoorType.Rotating)
        {
            pivotTransform.localRotation = Quaternion.RotateTowards(pivotTransform.localRotation, targetRot, Time.deltaTime * speed * 35f);
        }
        else
        {
            pivotTransform.localPosition = Vector3.MoveTowards(pivotTransform.localPosition, targetPos, Time.deltaTime * speed);
        }
    }

    public void ToggleDoor()
    {
        if (Time.unscaledTime < lastToggleTime + toggleCooldown && lastToggleTime > 0f) return;
        lastToggleTime = Time.unscaledTime;

        if (isLocked)
        {
            if (MetalKeyItem.hasMetalKey)
            {
                // Desbloquear puerta con la llave
                isLocked = false;
                
                // Opcional: Podríamos consumir la llave aquí si quisiéramos que fuera de un solo uso
                // MetalKeyItem.hasMetalKey = false; 
                
                // Forzar que se reproduzca el sonido de abrir en lugar del sonido bloqueado
                if (audioSource != null && doorOpenSound != null)
                {
                    audioSource.Stop();
                    audioSource.PlayOneShot(doorOpenSound, 1.0f);
                }
                
                // Proceder a abrir la puerta inmediatamente
            }
            else
            {
                if (audioSource != null && doorLockedSound != null)
                {
                    audioSource.PlayOneShot(doorLockedSound, 1.0f);
                }
                return;
            }
        }

        isOpen = !isOpen;
        targetRot = isOpen ? closedRot * Quaternion.Euler(0f, openAngle, 0f) : closedRot;
        targetPos = isOpen ? closedPos + slideOffset : closedPos;
        
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

            string action = isLocked ? (MetalKeyItem.hasMetalKey ? "Usar Llave" : "Puerta Bloqueada") : (isOpen ? "Cerrar Puerta" : "Abrir Puerta");
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
