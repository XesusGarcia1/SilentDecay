using UnityEngine;
using System.Collections;

public class ProceduralDoorInteract : MonoBehaviour
{
    public enum DoorType { Rotating, Sliding }
    public DoorType doorType = DoorType.Rotating;
    
    [Header("Rotating Door")]
    public float openAngle = 90f;
    [Tooltip("Eje sobre el que gira. Si la puerta se abre chueca (como rampa), cámbialo a X (1,0,0) o Z (0,0,1).")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("Activa esto si la puerta gira desde el centro como puerta giratoria.")]
    public bool autoFixCenterPivot = true; 
    
    public enum PivotAxis { X, Y, Z }
    [Tooltip("Si la bisagra automática se pone en el medio (gira como puerta giratoria), cambia esto a Y o Z.")]
    public PivotAxis pivotWidthAxis = PivotAxis.X;
    
    public bool hingeOnRightSide = false;
    
    [Header("Sliding Door")]
    public Vector3 slideOffset = new Vector3(1.5f, 0, 0);
    
    [Header("Velocidad de Movimiento")]
    public float openSpeed = 4f;
    public float closeSpeed = 4f;

    [Header("Bloqueo de Puertas")]
    [Tooltip("¿La puerta necesita una llave para abrirse?")]
    public bool isLocked = false;
    
    [Tooltip("ID de la llave requerida (ej: Access_keys_mannequin). Si lo dejas vacío, cualquier llave metálica la abrirá.")]
    public string requiredKeyID = "";
    public float interactDistance = 3.2f;

    [Header("Puerta Pesada (Multi-Stage)")]
    [Tooltip("¿Requiere múltiples clicks para abrirse por completo? (Ideal para puertas atascadas o pesadas)")]
    public bool isHeavyDoor = false;
    [Tooltip("Cantidad de clicks necesarios para abrirla al 100%")]
    public int totalStages = 3;
    private int currentStage = 0;

    private Transform player;
    public bool playerNear = false;
    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion targetRot;
    private Vector3 closedPos;
    private Vector3 targetPos;
    private Transform pivotTransform;
    
    [Header("Sonidos")]
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;
    public AudioClip doorLockedSound;

    private AudioSource audioSource;

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

        // Auto-Fix INTELIGENTE para puertas con cualquier rotación de importación
        if (doorType == DoorType.Rotating && autoFixCenterPivot)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = GetComponentInChildren<MeshFilter>();
            if (mf != null)
            {
                Bounds b = mf.mesh.bounds;
                
                // === AUTO-DETECCIÓN DE EJES ===
                // Transformar los 3 ejes locales del modelo al espacio del mundo usando el transform del mesh
                Vector3 worldAxisX = mf.transform.TransformDirection(Vector3.right).normalized;
                Vector3 worldAxisY = mf.transform.TransformDirection(Vector3.up).normalized;
                Vector3 worldAxisZ = mf.transform.TransformDirection(Vector3.forward).normalized;
                
                // ¿Cuál eje local apunta más hacia ARRIBA en el mundo? Ese es el eje de rotación
                float dotXUp = Mathf.Abs(Vector3.Dot(worldAxisX, Vector3.up));
                float dotYUp = Mathf.Abs(Vector3.Dot(worldAxisY, Vector3.up));
                float dotZUp = Mathf.Abs(Vector3.Dot(worldAxisZ, Vector3.up));
                
                if (dotXUp >= dotYUp && dotXUp >= dotZUp)
                    rotationAxis = Vector3.right;
                else if (dotYUp >= dotXUp && dotYUp >= dotZUp)
                    rotationAxis = Vector3.up;
                else
                    rotationAxis = Vector3.forward;
                
                // ¿Cuál eje local es el ANCHO de la puerta?
                float horizX = (1f - dotXUp) * b.size.x;
                float horizY = (1f - dotYUp) * b.size.y;
                float horizZ = (1f - dotZUp) * b.size.z;
                
                Vector3 hingePosLocal = b.center;
                
                if (horizX >= horizY && horizX >= horizZ)
                    hingePosLocal.x = hingeOnRightSide ? b.max.x : b.min.x;
                else if (horizY >= horizX && horizY >= horizZ)
                    hingePosLocal.y = hingeOnRightSide ? b.max.y : b.min.y;
                else
                    hingePosLocal.z = hingeOnRightSide ? b.max.z : b.min.z;
                
                GameObject hinge = new GameObject(gameObject.name + "_AutoHinge");
                hinge.transform.SetParent(transform.parent);
                
                // Poner la bisagra en el borde calculado de la malla
                hinge.transform.position = mf.transform.TransformPoint(hingePosLocal);
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
            if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/OpenDoorMetalic");
            if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/CloseDoorMetalic");
            if (doorLockedSound == null) doorLockedSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/metal-gate-door-knocking");
        }
        else
        {
            if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Hospital/doorOpenSound2");
            if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Hospital/doorCloseSound2");
            if (doorLockedSound == null) doorLockedSound = Resources.Load<AudioClip>("Audio/Hospital/errorSound");
        }

        if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (doorLockedSound == null) doorLockedSound = Resources.Load<AudioClip>("Audio/Hospital/errorSound");
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
                float maxDist = 3.5f;
                float closeDist = 2.0f;

                // Calcular distancia al colisionador de la puerta o al centro geométrico en lugar del pivote offset
                BoxCollider doorCollider = GetComponent<BoxCollider>();
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                if (doorCollider != null)
                {
                    dist = Vector3.Distance(doorCollider.bounds.center, cam.transform.position);
                }
                
                // Buscar si la cámara o el jugador apuntan/miran a la puerta
                if (dist <= maxDist)
                {
                    bool isFocused = InteractionFocusManager.IsFocused(gameObject, maxDist);
                    
                    // Si no detectó por el padre, probar con todos los hijos (mallas de la puerta)
                    if (!isFocused)
                    {
                        foreach (Transform child in transform)
                        {
                            if (InteractionFocusManager.IsFocused(child.gameObject, maxDist))
                            {
                                isFocused = true;
                                break;
                            }
                        }
                    }

                    // Fallback de proximidad directa si está a menos de closeDist mirando hacia la puerta
                    if (!isFocused && dist <= closeDist)
                    {
                        Vector3 targetCenter = doorCollider != null ? doorCollider.bounds.center : transform.position;
                        Vector3 dirToDoor = (targetCenter - cam.transform.position).normalized;
                        if (Vector3.Dot(cam.transform.forward, dirToDoor) > 0.35f)
                        {
                            isFocused = true;
                        }
                    }

                    if (isFocused)
                    {
                        playerNear = true;
                    }
                    Debug.Log($"[DoorDebug] {gameObject.name} dist={dist:F2}/{maxDist:F2}, isFocused={isFocused}, playerNear={playerNear}");
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

        float currentSpeed = isOpen ? openSpeed : closeSpeed;

        // Interpolar rotación o posición según el tipo de puerta
        if (doorType == DoorType.Rotating)
        {
            pivotTransform.localRotation = Quaternion.RotateTowards(pivotTransform.localRotation, targetRot, Time.deltaTime * currentSpeed * 35f);
        }
        else
        {
            Transform parentT = pivotTransform.parent;
            if (parentT != null)
            {
                Vector3 worldCurrent = parentT.TransformPoint(pivotTransform.localPosition);
                Vector3 worldTarget = parentT.TransformPoint(targetPos);
                Vector3 newWorldPos = Vector3.MoveTowards(worldCurrent, worldTarget, Time.deltaTime * currentSpeed);
                pivotTransform.localPosition = parentT.InverseTransformPoint(newWorldPos);
            }
            else
            {
                pivotTransform.localPosition = Vector3.MoveTowards(pivotTransform.localPosition, targetPos, Time.deltaTime * currentSpeed);
            }
        }
    }

    public void ToggleDoor()
    {
        if (Time.unscaledTime < lastToggleTime + toggleCooldown && lastToggleTime > 0f) return;
        lastToggleTime = Time.unscaledTime;

        if (isLocked)
        {
            bool hasRequiredKey = false;

            if (string.IsNullOrEmpty(requiredKeyID))
            {
                // Si no hay ID específico, cualquier llave de metal sirve
                hasRequiredKey = MetalKeyItem.hasMetalKey || MetalKeyItem.collectedKeys.Count > 0;
            }
            else
            {
                // Si hay un ID específico, verificar en el inventario de llaves
                hasRequiredKey = MetalKeyItem.collectedKeys.Contains(requiredKeyID);
            }

            if (hasRequiredKey)
            {
                // Desbloquear puerta con la llave
                isLocked = false;
                
                // Forzar que se reproduzca el sonido de abrir en lugar del sonido bloqueado
                if (audioSource != null && doorOpenSound != null)
                {
                    audioSource.Stop();
                    audioSource.PlayOneShot(doorOpenSound, 1.0f);
                }
            }
            else
            {
                if (audioSource != null)
                {
                    audioSource.Stop();
                    AudioClip lockSound = doorLockedSound != null ? doorLockedSound : Resources.Load<AudioClip>("Audio/Hospital/errorSound");
                    if (lockSound != null) audioSource.PlayOneShot(lockSound, 1.0f);
                }

                PlayerMonologueManager.ShowDialogue("Esta puerta está bloqueada. Necesito el código de seguridad para abrirla.", 3.5f);
                PowerBox pBox = FindObjectOfType<PowerBox>();
                if (pBox != null) pBox.ShowMessage("PUERTA BLOQUEADA: Requiere Código de Seguridad", Color.red, 3.0f);

                return;
            }
        }

        // --- SISTEMA DE PUERTA PESADA (Múltiples tirones) ---
        if (isHeavyDoor && !isOpen)
        {
            currentStage++;
            if (currentStage < totalStages)
            {
                // Calcular qué porcentaje se abre en este click
                float progress = (float)currentStage / totalStages;

                if (doorType == DoorType.Rotating)
                {
                    float partialAngle = (hingeOnRightSide ? -openAngle : openAngle) * progress;
                    targetRot = closedRot * Quaternion.AngleAxis(partialAngle, rotationAxis);
                }
                else
                {
                    Vector3 worldOffset = pivotTransform.right * slideOffset.x + pivotTransform.up * slideOffset.y + pivotTransform.forward * slideOffset.z;
                    Vector3 localSlideOffset = pivotTransform.parent != null ? pivotTransform.parent.InverseTransformVector(worldOffset) : worldOffset;
                    targetPos = closedPos + (localSlideOffset * progress);
                }

                if (audioSource != null)
                {
                    audioSource.Stop();
                    // Usar un sonido para indicar que se abrió a medias (como si se atorara)
                    AudioClip strainSound = doorLockedSound != null ? doorLockedSound : doorCloseSound;
                    if (strainSound != null) audioSource.PlayOneShot(strainSound, 1.0f);
                }
                return; // Cortar la ejecución para que no se marque como completamente abierta
            }
        }

        isOpen = !isOpen;
        if (!isOpen)
        {
            currentStage = 0; // Reiniciar contador si se cierra la puerta
        }

        float actualAngle = hingeOnRightSide ? -openAngle : openAngle;
        targetRot = isOpen ? closedRot * Quaternion.AngleAxis(actualAngle, rotationAxis) : closedRot;
        
        // Calcular el offset exacto en metros del mundo usando los ejes locales de la puerta
        Vector3 finalWorldOffset = pivotTransform.right * slideOffset.x + 
                                   pivotTransform.up * slideOffset.y + 
                                   pivotTransform.forward * slideOffset.z;
                              
        Vector3 finalLocalSlideOffset = pivotTransform.parent != null 
            ? pivotTransform.parent.InverseTransformVector(finalWorldOffset) 
            : finalWorldOffset;
            
        targetPos = isOpen ? closedPos + finalLocalSlideOffset : closedPos;
        
        if (audioSource != null)
        {
            audioSource.Stop();
            AudioClip clipToPlay = isOpen ? doorOpenSound : doorCloseSound;
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
        }

        // Si la puerta se abrió completamente, verificar si es la puerta final de salida
        if (isOpen)
        {
            if (requiredKeyID == "EXITKEY_01" || gameObject.name.Contains("EmergencyExitDoor"))
            {
                Debug.Log("[ProceduralDoorInteract]: Puerta de salida de emergencia abierta. Lanzando cinemática de final de juego.");
                GameEndingManager.TriggerEnding(pivotTransform != null ? pivotTransform : transform);
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

            bool hasRequiredKey = false;
            if (string.IsNullOrEmpty(requiredKeyID))
            {
                hasRequiredKey = MetalKeyItem.hasMetalKey || MetalKeyItem.collectedKeys.Count > 0;
            }
            else
            {
                hasRequiredKey = MetalKeyItem.collectedKeys.Contains(requiredKeyID);
            }

            string action = isLocked ? (hasRequiredKey ? LocalizationManager.Instance.Get("interact_use_key") : LocalizationManager.Instance.Get("interact_door_locked")) : (isOpen ? LocalizationManager.Instance.Get("interact_door_close") : LocalizationManager.Instance.Get("interact_door_open"));
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
