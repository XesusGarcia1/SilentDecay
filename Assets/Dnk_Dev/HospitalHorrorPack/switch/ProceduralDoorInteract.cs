using UnityEngine;
using System.Collections;

public class ProceduralDoorInteract : MonoBehaviour
{
    public float openAngle = 90f;
    public float speed = 4f;
    public bool isLocked = false;
    public float interactDistance = 1.8f;

    private Transform player;
    private bool playerNear = false;
    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion targetRot;
    
    private AudioSource audioSource;
    private AudioClip doorOpenSound;
    private AudioClip doorCloseSound;

    void Start()
    {
        // Desactivar Animator en el objeto y sus hijos para evitar el bucle de rotación continua de 360 grados
        Animator[] anims = GetComponentsInChildren<Animator>(true);
        foreach (Animator a in anims)
        {
            if (a != null) a.enabled = false;
        }

        closedRot = transform.localRotation;
        targetRot = closedRot;

        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { player = cc.transform; }
        else {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configurar el AudioSource para que sea sonido 3D espacial (atenuación por distancia)
        audioSource.spatialBlend = 1.0f; // 100% 3D
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2.0f;  // Volumen máximo hasta 2 metros
        audioSource.maxDistance = 15.0f; // Completamente inaudible después de 15 metros

#if UNITY_EDITOR
        doorOpenSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dnk_Dev/HospitalHorrorPack/Models/Animation/doorOpenSound2.mp3");
        doorCloseSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dnk_Dev/HospitalHorrorPack/Models/Animation/doorCloseSound2.mp3");
#endif
        // Fallback si no se encuentran en editor o es standalone
        if (doorOpenSound == null) doorOpenSound = Resources.Load<AudioClip>("Interruptor");
        if (doorCloseSound == null) doorCloseSound = Resources.Load<AudioClip>("Interruptor");
    }

    void Update()
    {
        if (isLocked) return;

        if (player != null)
        {
            Camera cam = Camera.main;
            if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();

            if (cam != null)
            {
                Transform panel = transform.Find("Puerta_Panel");
                Vector3 targetPos = panel != null ? panel.position : transform.position;

                float distToHinge = Vector3.Distance(transform.position, cam.transform.position);
                float distToPanel = Vector3.Distance(targetPos, cam.transform.position);
                float minDist = Mathf.Min(distToHinge, distToPanel);

                playerNear = false;

                if (minDist <= 3.2f) // Distancia cómoda y accesible de 3.2m para interactuar con la puerta fácilmente
                {
                    bool isFocused = InteractionFocusManager.IsFocused(gameObject, 3.2f);
                    if (!isFocused && panel != null)
                    {
                        isFocused = InteractionFocusManager.IsFocused(panel.gameObject, 3.2f);
                    }

                    if (isFocused)
                    {
                        playerNear = true;
                    }
                }
            }
        }
        else
        {
            GameObject playerObj = GameObject.Find("NestedParent_Unpack");
            if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            playerNear = false;
        }

        if (playerNear && MobileInput.GetKeyDown(KeyCode.E) && !isUIActive())
        {
            ToggleDoor();
        }

        // Interpolar rotación limpia y fluida hacia el ángulo objetivo sin giros de 360 grados
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRot, Time.deltaTime * speed * 35f);
    }

    private float lastToggleTime = 0f;
    public float toggleCooldown = 0.25f;

    public void ToggleDoor()
    {
        if (Time.time - lastToggleTime < toggleCooldown) return;
        lastToggleTime = Time.time;

        isOpen = !isOpen;
        targetRot = isOpen ? closedRot * Quaternion.Euler(0f, openAngle, 0f) : closedRot;
        
        if (audioSource != null)
        {
            audioSource.Stop(); // Detener reproducción previa para evitar superposición y desfasamiento
            AudioClip clipToPlay = isOpen ? doorOpenSound : doorCloseSound;
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, 0.8f);
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
            promptStyle.normal.textColor = Color.white;

            Rect promptRect = new Rect(Screen.width / 2 - 200, Screen.height - 120, 400, 40);
            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(promptRect.x - 10, promptRect.y - 5, promptRect.width + 20, promptRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string action = isOpen ? "Cerrar Puerta" : "Abrir Puerta";
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

