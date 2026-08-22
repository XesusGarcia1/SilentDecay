using UnityEngine;
using Cinemachine;

public class HideUnderBed : MonoBehaviour
{
    [Header("Ajustes")]
    public GameObject player;         // Objeto del jugador (se auto-detectará si es null)
    public GameObject playerCapsule;  // Objeto con scripts de movimiento (se auto-detectará si es null)
    public Camera mainCamera;         // Cámara del jugador (se auto-detectará si es null)
    public float interactDistance = 2.5f;

    [Header("Estado")]
    public bool isHiding = false;

    private Transform bedHidePosition;
    private CinemachineBrain cinemachineBrain;
    private MonoBehaviour[] movementScripts;
    private Renderer[] playerRenderers;

    // Posición original antes de esconderse (para retornar al salir)
    private Vector3 originalPlayerPosition;
    private Quaternion originalPlayerRotation;

    private float rotationX = 0f;
    private float rotationY = 0f;
    
    public bool nearBed = false;
    public Bed targetBed = null;
    private StarterAssets.StarterAssetsInputs playerInputs;

    void Start()
    {
        InitializeReferences();
    }

    void InitializeReferences()
    {
        if (player == null || !player.activeInHierarchy)
        {
            var fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
            if (fpc != null && fpc.gameObject.activeInHierarchy)
            {
                player = fpc.gameObject;
            }
            else
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null && cc.gameObject.activeInHierarchy) player = cc.gameObject;
                else player = GameObject.Find("NestedParent_Unpack");
            }
        }

        if (playerCapsule == null || !playerCapsule.activeInHierarchy)
        {
            playerCapsule = player;
        }

        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy)
        {
            mainCamera = Camera.main;
            if (mainCamera == null && player != null)
            {
                mainCamera = player.GetComponentInChildren<Camera>();
            }
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (mainCamera != null && cinemachineBrain == null)
        {
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }

        if (playerCapsule != null)
        {
            movementScripts = playerCapsule.GetComponents<MonoBehaviour>();
            playerRenderers = playerCapsule.GetComponentsInChildren<Renderer>(true);
            playerInputs = playerCapsule.GetComponent<StarterAssets.StarterAssetsInputs>();
        }
    }

    void Update()
    {
        // Si el juego está pausado o en el menú de opciones, desactivar interacción de cama
        if (Time.timeScale == 0f || (PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsGamePaused))
        {
            nearBed = false;
            targetBed = null;
            return;
        }

        if (player == null || playerCapsule == null || mainCamera == null)
        {
            InitializeReferences();
            if (player == null || playerCapsule == null || mainCamera == null) return;
        }

        if (isHiding)
        {
            if (bedHidePosition != null)
            {
                mainCamera.transform.position = bedHidePosition.position;
            }

            float mouseX = Input.GetAxis("Mouse X") * 2.5f;
            float mouseY = Input.GetAxis("Mouse Y") * 2.5f;

            if (playerInputs != null && (Mathf.Abs(playerInputs.look.x) > 0.01f || Mathf.Abs(playerInputs.look.y) > 0.01f))
            {
                mouseX = playerInputs.look.x * 0.8f;
                mouseY = playerInputs.look.y * 0.8f;
            }

            rotationY += mouseX;
            rotationX -= mouseY;

            rotationX = Mathf.Clamp(rotationX, -8f, 25f);
            rotationY = Mathf.Clamp(rotationY, -75f, 75f);

            Quaternion baseRot = bedHidePosition != null ? bedHidePosition.rotation : Quaternion.identity;
            mainCamera.transform.rotation = baseRot * Quaternion.Euler(rotationX, rotationY, 0f);
        }
        else
        {
            nearBed = false;
            targetBed = null;

            if (ElevatorController.isNotepadOpen) return;

            // Detección de camas en el mapa
            Bed[] beds = FindObjectsByType<Bed>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Auto-asignación al vuelo de componente Bed en cualquier objeto de cama/camilla cercano
            Collider[] nearbyCols = Physics.OverlapSphere(mainCamera.transform.position, 4.5f);
            foreach (Collider col in nearbyCols)
            {
                if (col == null) continue;
                string n = col.gameObject.name.ToLower();
                if (n.Contains("cama") || n.Contains("bed") || n.Contains("camilla") || n.Contains("gurney") || n.Contains("medical") || n.Contains("bedding"))
                {
                    if (col.GetComponentInParent<ProceduralDoorInteract>() != null) continue;
                    
                    Bed bComp = col.GetComponent<Bed>();
                    if (bComp == null) bComp = col.GetComponentInParent<Bed>();
                    if (bComp == null) bComp = col.gameObject.AddComponent<Bed>();

                    Transform hidePos = bComp.transform.Find("HidePosition");
                    if (hidePos == null)
                    {
                        GameObject hObj = new GameObject("HidePosition");
                        hObj.transform.SetParent(bComp.transform, false);
                        hObj.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                        hObj.transform.localRotation = Quaternion.identity;
                        hidePos = hObj.transform;
                    }
                    bComp.hidePosition = hidePos;

                    BoxCollider bc = bComp.GetComponent<BoxCollider>();
                    if (bc != null) bc.isTrigger = true;
                }
            }

            beds = FindObjectsByType<Bed>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Bed closestBed = null;
            float bestSurfaceDist = float.MaxValue;
            Vector3 camPos = mainCamera.transform.position;

            foreach (Bed bed in beds)
            {
                if (bed == null || !bed.gameObject.activeInHierarchy) continue;

                BoxCollider box = bed.GetComponent<BoxCollider>();
                if (box != null) box.isTrigger = true;

                Vector3 surfacePoint = bed.transform.position;
                if (box != null)
                {
                    surfacePoint = box.ClosestPoint(camPos);
                }

                float surfaceDist = Vector3.Distance(surfacePoint, camPos);
                float maxDistAllowed = Mathf.Max(interactDistance, 3.5f);

                // Si la superficie de la cama está a menos de 4.5 metros de la cámara, es válida inmediatamente
                if (surfaceDist <= maxDistAllowed && surfaceDist < bestSurfaceDist)
                {
                    bestSurfaceDist = surfaceDist;
                    closestBed = bed;
                }
            }

            if (closestBed != null)
            {
                nearBed = true;
                targetBed = closestBed;
            }
        }
    }

    private float lastToggleTime = 0f;

    void LateUpdate()
    {
        // Si el juego está pausado o en menú de opciones, cancelar cualquier interacción
        if (Time.timeScale == 0f || (PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsGamePaused)) return;
        if (ElevatorController.isNotepadOpen) return;

        if (Time.unscaledTime < lastToggleTime + 0.40f) return;

        if (isHiding)
        {
            if (MobileInput.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
            {
                MobileInput.ePressedDown = false;
                ToggleHide(null);
            }
        }
        else
        {
            // Interacción directa: Presionar E o presionar botón táctil cuando el juego NO está pausado
            bool triggerPressed = MobileInput.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.E);
            if (nearBed && targetBed != null && triggerPressed)
            {
                MobileInput.ePressedDown = false;
                ToggleHide(targetBed);
            }
        }
    }

    public void ToggleHide(Bed activeBed)
    {
        if (player == null || playerCapsule == null) return;

        lastToggleTime = Time.unscaledTime;
        MobileInput.ePressedDown = false;

        isHiding = !isHiding;

        if (isHiding && activeBed != null)
        {
            Debug.Log("🛌 Escondiéndose bajo la cama...");

            EvictEnemiesFarFromPlayer();

            bedHidePosition = activeBed.hidePosition;
            originalPlayerPosition = player.transform.position;
            originalPlayerRotation = player.transform.rotation;

            CharacterController cc = playerCapsule.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            AudioSource playerAudio = playerCapsule.GetComponent<AudioSource>();
            if (playerAudio != null)
            {
                playerAudio.Stop();
            }

            if (playerRenderers != null)
            {
                foreach (var r in playerRenderers)
                {
                    if (r != null) r.enabled = false;
                }
            }

            if (movementScripts != null)
            {
                foreach (var script in movementScripts)
                {
                    if (script == null || script == this) continue;
                    
                    string scriptName = script.GetType().Name;
                    if (scriptName.Contains("Flashlight") || 
                        scriptName.Contains("Camcorder") || 
                        scriptName.Contains("PlayerHealth") || 
                        scriptName.Contains("PlayerSanity") ||
                        scriptName.Contains("AudioSource") ||
                        scriptName.Contains("Inputs") ||
                        scriptName.Contains("PlayerInput"))
                    {
                        continue;
                    }
                    script.enabled = false;
                }
            }

            if (bedHidePosition != null)
            {
                mainCamera.transform.position = bedHidePosition.position;
                mainCamera.transform.rotation = bedHidePosition.rotation;
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.enabled = false;
            }

            MobileInput.SetCursorState(true);

            rotationX = 0f;
            rotationY = 0f;
        }
        else
        {
            Debug.Log("🚶 Saliendo del escondite...");

            if (playerRenderers != null)
            {
                foreach (var r in playerRenderers)
                {
                    if (r != null) r.enabled = true;
                }
            }

            if (movementScripts != null)
            {
                foreach (var script in movementScripts)
                {
                    if (script != null) script.enabled = true;
                }
            }

            player.transform.position = originalPlayerPosition;
            player.transform.rotation = originalPlayerRotation;

            CharacterController cc = playerCapsule.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = true;
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.enabled = true;
            }

            MobileInput.SetCursorState(true);
        }
    }

    void OnGUI()
    {
        // Si el juego está pausado o en el menú de opciones, NUNCA mostrar el cartel de la cama
        if (Time.timeScale == 0f || (PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsGamePaused)) return;
        if (ElevatorController.isNotepadOpen) return;

        if (isHiding)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 22;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;

            Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), "[E] o Click  Salir del Escondite", style);

            style.normal.textColor = new Color(0.1f, 0.85f, 0.1f);
            GUI.Label(rect, "[E] o Click  Salir del Escondite", style);
        }
        else
        {
            if (nearBed && targetBed != null)
            {
                BoxCollider box = targetBed.GetComponent<BoxCollider>();
                Vector3 targetPoint = targetBed.transform.position;
                if (box != null)
                {
                    targetPoint = box.ClosestPoint(mainCamera.transform.position);
                }

                float distToBed = Vector3.Distance(targetPoint, mainCamera.transform.position);
                float maxDistAllowed = Mathf.Max(interactDistance, 3.5f);
                if (distToBed <= maxDistAllowed)
                {
                    GUIStyle style = new GUIStyle();
                    style.fontSize = 22;
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontStyle = FontStyle.Bold;

                    Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

                    GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
                    GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    style.normal.textColor = Color.black;
                    GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), "[E]  Esconderse bajo la Cama", style);

                    style.normal.textColor = new Color(0.3f, 0.75f, 1f);
                    GUI.Label(rect, "[E]  Esconderse bajo la Cama", style);
                }
            }
        }
    }

    void EvictEnemiesFarFromPlayer()
    {
        CrawlerAI[] crawlers = FindObjectsOfType<CrawlerAI>(true);
        foreach (var c in crawlers)
        {
            if (c != null && c.gameObject.activeInHierarchy)
            {
                c.FleeToShadows();
            }
        }

        EnemyAIController[] b1s = FindObjectsOfType<EnemyAIController>(true);
        foreach (var b in b1s)
        {
            if (b != null && b.gameObject.activeInHierarchy)
            {
                b.FleeFarFromPlayer();
            }
        }

        EnemyAIBookHead[] b2s = FindObjectsOfType<EnemyAIBookHead>(true);
        foreach (var b in b2s)
        {
            if (b != null && b.gameObject.activeInHierarchy)
            {
                b.FleeFarFromPlayer();
            }
        }
    }
}
