using UnityEngine;
using Cinemachine;

public class HideUnderBed : MonoBehaviour
{
    [Header("Ajustes")]
    public GameObject player;         // Objeto del jugador (se auto-detectará si es null)
    public GameObject playerCapsule;  // Objeto con scripts de movimiento (se auto-detectará si es null)
    public Camera mainCamera;         // Cámara del jugador (se auto-detectará si es null)
    public float interactDistance = 3.5f;

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
    
    private bool nearBed = false;
    public Bed targetBed = null;
    private StarterAssets.StarterAssetsInputs playerInputs;

    void Start()
    {
        InitializeReferences();
    }

    void InitializeReferences()
    {
        if (player == null)
        {
            CharacterController cc = FindObjectOfType<CharacterController>();
            if (cc != null) player = cc.gameObject;
            else player = GameObject.Find("NestedParent_Unpack");
        }

        if (playerCapsule == null)
        {
            playerCapsule = player;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
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

            float mouseX = 0f;
            float mouseY = 0f;

            #if UNITY_ANDROID || UNITY_IOS
            if (playerInputs != null)
            {
                // Leer del trackpad de pantalla táctil del móvil (corregido para no estar invertido y mejor sensibilidad)
                mouseX = playerInputs.look.x * 0.45f;
                mouseY = -playerInputs.look.y * 0.45f; // Invertido para que deslizar arriba mire arriba
            }
            #else
            mouseX = Input.GetAxis("Mouse X") * 2f;
            mouseY = Input.GetAxis("Mouse Y") * 2f;
            #endif

            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -60f, 60f);
            rotationY += mouseX;

            mainCamera.transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
        }
        else
        {
            nearBed = false;
            targetBed = null;

            if (ElevatorController.isNotepadOpen) return;

            // Detección de cama limpia y directa por proximidad y ángulo de mirada
            Bed[] beds = FindObjectsOfType<Bed>();
            Bed closestBed = null;
            float bestDist = float.MaxValue;

            foreach (Bed bed in beds)
            {
                if (bed == null) continue;

                Vector3 bedCenter = bed.transform.position;
                BoxCollider box = bed.GetComponent<BoxCollider>();
                if (box != null)
                {
                    bedCenter = box.bounds.center;
                }

                // Distancia entre el jugador y el centro/cuerpo de la cama
                float dist = Vector3.Distance(bedCenter, player.transform.position);
                
                // Rango justo de 2.8 metros para interactuar estando dentro de la habitación
                if (dist > 2.8f) continue;

                // Dirección hacia la cama
                Vector3 dirToBed = (bedCenter - mainCamera.transform.position).normalized;
                float lookScore = Vector3.Dot(mainCamera.transform.forward, dirToBed);

                // Ángulo de mirada directo hacia la cama
                if (lookScore > 0.4f && dist < bestDist)
                {
                    // VERIFICACIÓN ESTRICTA DE LÍNEA DE VISIÓN: Raycast desde los ojos hacia la cama
                    // Si el raycast choca con un muro o puerta cerrada antes de llegar a la cama, ignorar
                    RaycastHit wallCheck;
                    if (Physics.Raycast(mainCamera.transform.position, dirToBed, out wallCheck, dist + 0.5f))
                    {
                        GameObject hitObj = wallCheck.collider.gameObject;
                        bool hitBed = (hitObj == bed.gameObject) || hitObj.transform.IsChildOf(bed.transform);
                        if (!hitBed)
                        {
                            // Si chocó contra un muro, pared o puerta, NO mostrar el prompt
                            string hitName = hitObj.name.ToLower();
                            if (hitName.Contains("wall") || hitName.Contains("pared") || hitName.Contains("door") || hitName.Contains("puerta") || hitName.Contains("frame"))
                            {
                                continue;
                            }
                        }
                    }

                    bestDist = dist;
                    closestBed = bed;
                }
            }

            if (closestBed != null)
            {
                // PRIORIDAD ABSOLUTA DE BATERÍA/ITEM: Si la mirilla apunta directamente a una Batería cercana, NO activar esconderse
                bool lookingAtBattery = false;
                RaycastHit itemHit;
                if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out itemHit, 3.5f))
                {
                    if (itemHit.collider != null)
                    {
                        BatteryItem batComp = itemHit.collider.GetComponentInParent<BatteryItem>();
                        if (batComp == null) batComp = itemHit.collider.GetComponent<BatteryItem>();
                        if (batComp != null) lookingAtBattery = true;
                    }
                }

                if (!lookingAtBattery)
                {
                    nearBed = true;
                    targetBed = closestBed;
                }
            }
        }
    }

    void LateUpdate()
    {
        if (ElevatorController.isNotepadOpen) return;

        if (isHiding)
        {
            #if UNITY_ANDROID || UNITY_IOS
            if (MobileInput.GetKeyDown(KeyCode.E)) // En móviles sólo salir tocando el botón de interactuar (mano) para no bugearse al girar la cámara
            #else
            if (MobileInput.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
            #endif
            {
                ToggleHide(null);
            }
        }
        else
        {
            // Interacción directa con cama: presionar E cuando nearBed es true
            if (nearBed && targetBed != null && MobileInput.GetKeyDown(KeyCode.E))
            {
                ToggleHide(targetBed);
            }
        }
    }

    public void ToggleHide(Bed activeBed)
    {
        if (player == null || playerCapsule == null) return;

        isHiding = !isHiding;

        if (isHiding && activeBed != null)
        {
            Debug.Log("🛌 Escondiéndose bajo la cama...");

            // Forzar a los monstruos a retirarse rápidamente a un punto lejano del mapa
            EvictEnemiesFarFromPlayer();

            bedHidePosition = activeBed.hidePosition;
            originalPlayerPosition = player.transform.position;
            originalPlayerRotation = player.transform.rotation;

            CharacterController cc = playerCapsule.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            // Detener inmediatamente cualquier sonido de pasos (caminando/corriendo) que se haya quedado reproduciendo
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
                        scriptName.Contains("AudioSource"))
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

            rotationX = mainCamera.transform.localEulerAngles.x;
            rotationY = mainCamera.transform.localEulerAngles.y;
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
            // Mostrar prompt directamente cuando el jugador está cerca de una cama y la mira
            if (nearBed && targetBed != null)
            {
                BoxCollider box = targetBed.GetComponent<BoxCollider>();
                Vector3 targetPoint = targetBed.transform.position;
                if (box != null)
                {
                    targetPoint = box.ClosestPoint(player.transform.position);
                }

                float distToBed = Vector3.Distance(targetPoint, player.transform.position);
                float maxRange = interactDistance * 1.5f;
                if (distToBed <= maxRange)
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
        // 1. Evacuar a El Rastrero / CrawlerAI hacia las sombras alejadas del mapa
        CrawlerAI[] crawlers = FindObjectsOfType<CrawlerAI>(true);
        foreach (var c in crawlers)
        {
            if (c != null && c.gameObject.activeInHierarchy)
            {
                c.FleeToShadows();
            }
        }

        // 2. Evacuar a BookHead / EnemyAIController & EnemyAIBookHead hacia puntos de patrulla distantes
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
