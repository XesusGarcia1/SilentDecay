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
        interactDistance = 4.5f; // Rango amplio y cómodo (4.5 metros)
        FindPlayer();

        // Eliminar cualquier BoxCollider gigante hijo en Mesh_node
        Transform meshChild = transform.Find("Mesh_node");
        if (meshChild != null)
        {
            BoxCollider childBox = meshChild.GetComponent<BoxCollider>();
            if (childBox != null)
            {
                if (Application.isPlaying) Destroy(childBox);
                else DestroyImmediate(childBox);
            }
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        Vector3 lossy = transform.lossyScale;
        float sx = lossy.x > 0.001f ? 0.45f / lossy.x : 0.45f;
        float sy = lossy.y > 0.001f ? 0.35f / lossy.y : 0.35f;
        float sz = lossy.z > 0.001f ? 0.45f / lossy.z : 0.45f;

        box.center = Vector3.zero;
        box.size = new Vector3(sx, sy, sz); // 45 cm constantes en espacio de mundo

        if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Interruptor");
        if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Audio/Compartido/Bateria_Pickup");
        if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("Click");
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

        if (playerTransform == null)
        {
            playerNear = false;
            return;
        }

        Camera cam = Camera.main;
        Vector3 eyePos = cam != null ? cam.transform.position : playerTransform.position + Vector3.up * 1.5f;
        Vector3 fuseTargetPos = transform.position + Vector3.up * 0.1f;

        float dist = Vector3.Distance(fuseTargetPos, eyePos);
        if (dist > interactDistance)
        {
            playerNear = false;
            return;
        }

        // Permitir recogida si la mirilla (FocusManager) apunta al fusible, si el jugador lo mira o si está cerca
        bool isFocused = InteractionFocusManager.IsFocused(gameObject, interactDistance);
        bool isLooking = false;

        if (cam != null)
        {
            Vector3 dirToFuse = (transform.position - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, dirToFuse);
            if (dot > 0.50f) // El jugador está mirando hacia el fusible en el suelo
            {
                // Verificar que no haya paredes macizas directamente intermedias entre la cámara y el fusible
                RaycastHit hit;
                if (Physics.Raycast(eyePos, dirToFuse, out hit, dist))
                {
                    string hName = hit.collider.name.ToLower();
                    if (hit.transform != transform && !hit.transform.IsChildOf(transform) && (hName.Contains("wall") || hName.Contains("solid_wall") || hName.Contains("pillar")))
                    {
                        playerNear = false;
                        return;
                    }
                }
                isLooking = true;
            }
        }

        playerNear = isFocused || isLooking || (dist <= 3.8f);
    }

    void LateUpdate()
    {
        if (playerNear && MobileInput.GetKeyDown(KeyCode.E))
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
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_fuse"), style);

        style.normal.textColor = new Color(1f, 0.9f, 0.2f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_fuse"), style);
    }
}

