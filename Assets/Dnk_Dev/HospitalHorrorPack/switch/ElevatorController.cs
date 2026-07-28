using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;

public class ElevatorController : MonoBehaviour
{
    [Header("Ajustes del Elevador")]
    public float callTime = 30f;
    public float interactDistance = 6.5f;
    public string nextSceneName = "TunnelsMap";
    [Tooltip("Tiempo en segundos a omitir al reproducir el sonido de llamada (salta el silencio inicial)")]
    public float callSoundOffset = 1.5f;
    [Tooltip("Tiempo en segundos a omitir al reproducir el sonido de llegada (salta el silencio inicial)")]
    public float arriveSoundOffset = 3.0f;
    [Tooltip("Burlar la tarjeta de acceso para pruebas de desarrollo")]
    public bool bypassKeycard = false;
    [Tooltip("Iniciar la partida con la tarjeta de acceso en el inventario para pruebas")]
    public bool startWithKeycard = false;
    [Tooltip("Burlar la necesidad de energía eléctrica para probar el ascensor de inmediato")]
    public bool bypassPower = false;
    [Tooltip("Tiempo de espera (en segundos) antes de abrir las puertas al llegar (para dar tiempo al timbre)")]
    public float doorOpenDelay = 1.0f;

    [Header("Referencias a Puertas")]
    public Transform leftDoor;
    public Transform rightDoor;
    public float doorSlideDistance = 0.45f; // Ancho exacto del marco para que NO sobresalga del elevador
    public float doorSpeed = 0.25f; // Velocidad súper pausada coincidiendo con el audio (~4s)

    [Header("Sonidos (Opcional)")]
    public AudioClip callSound;      // Al presionar el boton exterior
    public AudioClip arriveSound;    // Al llegar (Ding!)
    public AudioClip errorSound;     // Cuando no hay energia o no hay tarjeta
    public AudioClip travelSound;    // Durante el viaje

    // Estado global de la tarjeta de acceso (seteado por KeycardItem)
    public static bool hasKeycard = false;

    // Estados internos
    private float currentTimer = 0f;
    private bool isCalling = false;
    private bool isArrived = false;
    private bool isEscaping = false;
    private bool doorsOpen = false;
    private bool keycardUsed = false;
    private float doorOpenDelayTimer = 0f;
    private float currentDoorProgress = 0f; // Progreso de deslizamiento suave (0 = cerrada, 1 = abierta completa)

    private Transform playerTransform;
    private RoomLightsManager roomLightsManager;
    private PowerBox powerBox;
    private AudioSource audioSource;

    private Transform extButtonTrans;
    private Transform intButtonTrans;
    private TextMesh extTM;
    private TextMesh intTM;
    private Light cabinLight;
    
    // Referencias a los renderers para controlar la emisión (encendido/apagado visual)
    private Renderer cabinLightRenderer;
    private Renderer extScreenRenderer;
    private Renderer intScreenRenderer;
    private bool isAudioPaused = false;

    // Posiciones y escalas iniciales congeladas de las puertas
    private Vector3 originalLeftScale;
    private Vector3 originalRightScale;
    private Vector3 originalLeftPos;
    private Vector3 originalRightPos;
    private Vector3 slideAxis = Vector3.right;
    private bool isScaleOnX = true;
    private float doorWidth = 0.45f;

    private bool isGameEnded = false; // Estado temporal para el fin del juego
    public static int[] foundNotes = new int[] { -1, -1, -1, -1, -1, -1, -1 }; // Notas encontradas (posiciones 1-7)
    private bool isNotepadOpen = false; // Estado de la libreta de notas HUD

    // Pantalla de carga y fundido cinemático
    private float escapeFadeAlpha = 0f;
    private Texture2D fadeBlackTex;
    private bool isAsyncLoading = false;
    private float asyncProgress = 0f;

    void Start()
    {
        // Encontrar referencias en la escena
        FindPlayer();

        roomLightsManager = FindObjectOfType<RoomLightsManager>();
        powerBox = FindObjectOfType<PowerBox>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        // Forzar audioSource a 2D (spatialBlend = 0f) y volumen al máximo (1.0f) para que el sonido de error/acceso denegado y viaje se escuchen recios y claros
        if (audioSource != null)
        {
            audioSource.volume = 1.0f;
            audioSource.spatialBlend = 0.0f; // Sonido 2D (Estéreo directo en audífonos)
            audioSource.playOnAwake = false;
        }

        if (startWithKeycard || bypassKeycard)
        {
            hasKeycard = true;
            keycardUsed = true;
            isArrived = true;
            doorsOpen = true;
            currentDoorProgress = 1.0f;
        }

        // Resolver referencias de puertas si no estan asignadas (busqueda profunda de jerarquia)
        if (leftDoor == null || rightDoor == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child == transform) continue;
                string n = child.name.ToLower();
                if (leftDoor == null && (n.Contains("puerta") || n.Contains("door")) && (n.Contains("izq") || n.Contains("left") || n.Contains("_l") || n.EndsWith("l") || n.Contains("1")))
                {
                    leftDoor = child;
                }
                else if (rightDoor == null && (n.Contains("puerta") || n.Contains("door")) && (n.Contains("der") || n.Contains("right") || n.Contains("_r") || n.EndsWith("r") || n.Contains("2")))
                {
                    rightDoor = child;
                }
            }
        }

        // Resolver referencias de botoneras y pantallas
        extButtonTrans = transform.Find("BotoneraExterior");
        intButtonTrans = transform.Find("BotoneraInterior");

        // Resolver referencias de los textos de los pisos directamente (como hijos del ascensor)
        Transform extText = transform.Find("TextoPisoExterior");
        if (extText == null) extText = transform.Find("PantallaPisoExterior/TextoPisoExterior");
        if (extText != null)
        {
            extTM = extText.GetComponent<TextMesh>();
            if (extTM != null)
            {
                extTM.anchor = TextAnchor.MiddleCenter;
                extTM.alignment = TextAlignment.Center;
            }
        }

        Transform intText = transform.Find("TextoPisoInterior");
        if (intText == null) intText = transform.Find("BotoneraInterior/PantallaPisoInterior/TextoPisoInterior");
        if (intText != null)
        {
            intTM = intText.GetComponent<TextMesh>();
            if (intTM != null)
            {
                intTM.anchor = TextAnchor.MiddleCenter;
                intTM.alignment = TextAlignment.Center;
            }
        }

        // Buscar la luz interior de la cabina (que es hija del panel del techo) y su panel físico
        Transform lightTrans = transform.Find("PanelLuzTecho/LuzAscensor");
        if (lightTrans != null) cabinLight = lightTrans.GetComponent<Light>();
        
        Transform panelTrans = transform.Find("PanelLuzTecho");
        if (panelTrans != null) cabinLightRenderer = panelTrans.GetComponent<Renderer>();

        // Buscar pantallas indicadoras de piso para apagar su emisión verde al irse la luz
        Transform extScreenTrans = transform.Find("PantallaPisoExterior");
        if (extScreenTrans != null) extScreenRenderer = extScreenTrans.GetComponent<Renderer>();

        Transform intScreenTrans = transform.Find("BotoneraInterior/PantallaPisoInterior");
        if (intScreenTrans != null) intScreenRenderer = intScreenTrans.GetComponent<Renderer>();

        // Cargar sonidos automáticamente desde la carpeta Assets/Resources al iniciar
        if (callSound == null) callSound = Resources.Load<AudioClip>("Ascensor_Llamar");
        if (arriveSound == null) arriveSound = Resources.Load<AudioClip>("Ascensor_Llegar");
        if (errorSound == null) errorSound = Resources.Load<AudioClip>("Ascensor_Error");
        if (travelSound == null) travelSound = Resources.Load<AudioClip>("Ascensor_Viaje");

        // Configurar escalas y posiciones iniciales congeladas para contraccion anclada al pivote lateral exterior
        if (leftDoor != null)
        {
            originalLeftScale = leftDoor.localScale;
            originalLeftPos = leftDoor.localPosition;

            if (rightDoor != null && Mathf.Abs(rightDoor.localPosition.z - leftDoor.localPosition.z) > Mathf.Abs(rightDoor.localPosition.x - leftDoor.localPosition.x))
            {
                isScaleOnX = false;
                slideAxis = Vector3.forward;
            }

            MeshFilter mf = leftDoor.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float meshW = isScaleOnX ? mf.sharedMesh.bounds.size.x * leftDoor.localScale.x : mf.sharedMesh.bounds.size.z * leftDoor.localScale.z;
                if (meshW > 0.1f && meshW < 1.5f) doorWidth = meshW;
            }
        }

        if (rightDoor != null)
        {
            originalRightScale = rightDoor.localScale;
            originalRightPos = rightDoor.localPosition;
        }

        currentTimer = callTime;
        for (int i = 0; i < 7; i++)
        {
            foundNotes[i] = -1;
        }
    }

    void Update()
    {
        if (isGameEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            return;
        }

        // Manejar libreta de notas abierta
        if (isNotepadOpen)
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleNotepad();
            }
            return; // Pausar logica de actualizacion
        }

        // Escuchar TAB para abrir libreta si el teclado de la oficina no esta activo
        if (Input.GetKeyDown(KeyCode.Tab) && !isEscaping)
        {
            KeypadController activeKeypad = FindObjectOfType<KeypadController>();
            bool isKeypadActive = activeKeypad != null && activeKeypad.isOpened;
            if (!isKeypadActive)
            {
                ToggleNotepad();
            }
        }

        if (playerTransform == null)
        {
            FindPlayer();
        }
        if (playerTransform == null) return;

        // Comprobar estado de la energia
        bool hasPower = true;
        if (powerBox != null)
        {
            hasPower = !powerBox.isPowerOut;
        }
        else if (roomLightsManager != null)
        {
            hasPower = !roomLightsManager.powerOutage;
        }

        // 1. Logica del temporizador de llamada
        if (isCalling && !isArrived)
        {
            if (hasPower)
            {
                currentTimer -= Time.deltaTime;

                // Conteo de pisos ascendentes desde -5 hasta 1 como si el elevador subiera de los sotanos
                float progress = (callTime - currentTimer) / callTime;
                int currentFloor = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(-5f, 1f, progress)), -5, 1);
                string floorStr = currentFloor.ToString();
                
                if (extTM != null) extTM.text = floorStr;
                if (intTM != null) intTM.text = floorStr;

                if (currentTimer <= 0f)
                {
                    Arrive();
                }
            }
        }

        // Control de encendido/apagado de la luz interior fisica y su brillo (emision) segun energia
        if (cabinLight != null)
        {
            cabinLight.enabled = hasPower;
        }
        
        if (cabinLightRenderer != null)
        {
            cabinLightRenderer.material.SetColor("_EmissionColor", hasPower ? Color.white * 3f : Color.black);
        }

        // Control de emision de las pantallas verdes de piso segun energia
        if (extScreenRenderer != null)
        {
            extScreenRenderer.material.SetColor("_EmissionColor", hasPower ? new Color(0f, 0.6f, 0.4f) * 2f : Color.black);
        }
        if (intScreenRenderer != null)
        {
            intScreenRenderer.material.SetColor("_EmissionColor", hasPower ? new Color(0f, 0.6f, 0.4f) * 2f : Color.black);
        }

        // Control de reproduccion de audio loops
        if (!hasPower)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
                isAudioPaused = true;
            }
        }
        else
        {
            if (isAudioPaused)
            {
                if (audioSource != null) audioSource.UnPause();
                isAudioPaused = false;
            }
        }

        // 2. Control de puertas deslizantes cinematográficas
        bool effectivePower = hasPower || bypassPower || bypassKeycard || startWithKeycard;
        if (effectivePower)
        {
            float targetProgress = 0f;
            if (isArrived && !isEscaping)
            {
                if (doorOpenDelayTimer > 0f)
                {
                    doorOpenDelayTimer -= Time.deltaTime;
                    targetProgress = 0f;
                }
                else
                {
                    targetProgress = 1f;
                    doorsOpen = true;
                }
            }
            else if (isEscaping)
            {
                targetProgress = 0f;
                doorsOpen = false;
            }

            // Animación deslizante suave y realista de elevador mecánico (desplazamiento puro sobre el eje X local)
            currentDoorProgress = Mathf.MoveTowards(currentDoorProgress, targetProgress, doorSpeed * Time.deltaTime);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, currentDoorProgress);

            float slideDistance = 0.015f;
            bool fullyOpen = currentDoorProgress >= 0.95f;

            if (leftDoor != null)
            {
                leftDoor.localScale = originalLeftScale;
                float targetX = originalLeftPos.x - slideDistance;
                leftDoor.localPosition = new Vector3(Mathf.Lerp(originalLeftPos.x, targetX, smoothProgress), originalLeftPos.y, originalLeftPos.z);

                foreach (Renderer r in leftDoor.GetComponentsInChildren<Renderer>(true)) r.enabled = !fullyOpen;
                foreach (Collider c in leftDoor.GetComponentsInChildren<Collider>(true)) c.enabled = !fullyOpen;
            }

            if (rightDoor != null)
            {
                rightDoor.localScale = originalRightScale;
                float targetX = originalRightPos.x + slideDistance;
                rightDoor.localPosition = new Vector3(Mathf.Lerp(originalRightPos.x, targetX, smoothProgress), originalRightPos.y, originalRightPos.z);

                foreach (Renderer r in rightDoor.GetComponentsInChildren<Renderer>(true)) r.enabled = !fullyOpen;
                foreach (Collider c in rightDoor.GetComponentsInChildren<Collider>(true)) c.enabled = !fullyOpen;
            }
        }

        // 3. Procesar interacciones: Comprobación directa de distancia global a la cabina (radio de 3.0m)
        float worldDistToElevator = Vector3.Distance(transform.position, playerTransform.position);
        // isInside es verdadero solo si las puertas estan ABIERTAS y el jugador esta dentro.
        // Con puertas cerradas, el jugador nunca esta "dentro" aunque este a 3m.
        bool isInside = doorsOpen && worldDistToElevator <= 3.0f;

        float distToButton = interactDistance + 1f;
        if (isInside && intButtonTrans != null)
        {
            distToButton = Vector3.Distance(intButtonTrans.position, playerTransform.position);
        }
        else if (!isInside && extButtonTrans != null)
        {
            distToButton = Vector3.Distance(extButtonTrans.position, playerTransform.position);
        }
        else
        {
            distToButton = Vector3.Distance(transform.position, playerTransform.position);
        }

        if (distToButton <= interactDistance && (MobileInput.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.E)))
        {
            // Evitar que el manager ficticio intercepte la llamada si no es el elevador real
            if (transform.name.Contains("Manager") && extButtonTrans == null && intButtonTrans == null) return;

            if (isInside)
            {
                HandleInteraction(hasPower);
            }
            else
            {
                // Verificar que el jugador esté mirando hacia el elevador usando raycast
                // (misma lógica que OnGUI para que si el prompt es visible, E siempre funcione)
                Camera cam = Camera.main;
                bool canInteract = false;

                if (cam != null)
                {
                    // Opción A: Raycast impacta directamente en una parte del elevador
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, interactDistance + 1.5f))
                    {
                        string n = hit.transform.name.ToLower();
                        bool isElevatorPart = hit.transform == transform || hit.transform.IsChildOf(transform) ||
                                              hit.transform == extButtonTrans || hit.transform == intButtonTrans ||
                                              n.Contains("elevator") || n.Contains("ascensor") || n.Contains("puerta");
                        if (isElevatorPart) canInteract = true;
                    }

                    // Opción B: Si está muy cerca (≤2.5m) y mirando vagamente hacia el elevador, permitir igualmente
                    // Esto evita que el sonido no suene cuando se está pegado a las puertas
                    if (!canInteract && worldDistToElevator <= 2.5f)
                    {
                        Transform targetFocus = extButtonTrans != null ? extButtonTrans : transform;
                        Vector3 dirToElevator = (targetFocus.position - cam.transform.position).normalized;
                        if (Vector3.Dot(cam.transform.forward, dirToElevator) >= -0.1f) // Muy permisivo al estar cerca
                            canInteract = true;
                    }
                }
                else
                {
                    // Sin cámara: permitir si está en rango de distancia
                    canInteract = true;
                }

                if (canInteract)
                {
                    HandleInteraction(hasPower);
                }
            }
        }

        // 4. Control de pantallas
        if (!hasPower)
        {
            if (extTM != null) extTM.text = "";
            if (intTM != null) intTM.text = "";
        }
        else if (!isCalling && !isEscaping)
        {
            if (extTM != null && extTM.text == "") extTM.text = "1";
            if (intTM != null && intTM.text == "") intTM.text = "1";
        }
    }

    void HandleInteraction(bool hasPower)
    {
        // En modo pruebas o dev (startWithKeycard/bypassKeycard/bypassPower), habilitar la energía del elevador de inmediato
        bool effectivePower = hasPower || bypassPower || bypassKeycard || startWithKeycard;

        // Determinar si el jugador esta dentro o fuera de la cabina
        bool isInside = Vector3.Distance(transform.position, playerTransform.position) <= 3.0f;

        if (isInside)
        {
            // Panel Interior: Solo permite descender si el ascensor ya fue llamado, llegó y abrió sus puertas
            if (!isArrived || !doorsOpen)
            {
                PlaySound(errorSound);
                ShowScreenMsg("LLAME EL ELEVADOR DESDE LA BOTONERA EXTERIOR", Color.yellow);
                return;
            }

            if (!isEscaping)
            {
                if (effectivePower)
                {
                    StartCoroutine(EscapeRoutine());
                }
                else
                {
                    PlaySound(errorSound);
                    ShowScreenMsg("PANEL DE CONTROL SIN ENERGIA", Color.red);
                }
            }
        }
        else
        {
            // Panel Exterior: Intentar llamar
            if (!keycardUsed && !bypassKeycard)
            {
                if (hasKeycard)
                {
                    keycardUsed = true;
                    PlaySound(callSound);
                    
                    // Iniciar llamada automáticamente al insertar la tarjeta
                    if (effectivePower)
                    {
                        isCalling = true;
                        currentTimer = (startWithKeycard || bypassKeycard) ? 5.0f : callTime;
                        ShowScreenMsg("TARJETA ACEPTADA. LLAMANDO ELEVADOR...", Color.green);
                        Debug.Log("Elevator: Llamada iniciada automáticamente por tarjeta. Temporizador: " + currentTimer + "s");
                    }
                    else
                    {
                        ShowScreenMsg("TARJETA ACEPTADA. BOTONERA SIN ENERGIA.", Color.yellow);
                    }
                }
                else
                {
                    PlaySound(errorSound);
                    ShowScreenMsg("REQUIERE TARJETA DE ACCESO DEL DIRECTOR", Color.yellow);
                }
            }
            else if (!isCalling && !isArrived)
            {
                if (effectivePower)
                {
                    isCalling = true;
                    currentTimer = (startWithKeycard || bypassKeycard) ? 5.0f : callTime;
                    PlaySound(callSound);
                    ShowScreenMsg("LLAMANDO ELEVADOR...", Color.cyan);
                    Debug.Log("Elevator: Llamada iniciada. Temporizador: " + currentTimer + "s");
                }
                else
                {
                    PlaySound(errorSound);
                    ShowScreenMsg("BOTONERA SIN ENERGIA. REPARE CAJA DE PODER.", Color.red);
                }
            }
        }
    }

    void Arrive()
    {
        isArrived = true;
        isCalling = false; // Detener el estado de llamada al llegar
        
        // Forzar a mostrar el número 1 inmediatamente al llegar
        if (extTM != null) extTM.text = "1";
        if (intTM != null) intTM.text = "1";

        // Iniciar el temporizador de retraso de apertura de puertas
        doorOpenDelayTimer = doorOpenDelay;

        PlaySound(arriveSound);
        ShowScreenMsg("ELEVADOR EN PLANTA. PUERTAS ABIERTAS.", Color.green);
        Debug.Log("Elevator: Llegado a planta.");
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.clip = clip;
            if (clip == callSound)
            {
                // Saltar el silencio inicial configurado
                audioSource.time = Mathf.Clamp(callSoundOffset, 0f, clip.length - 0.1f);
            }
            else if (clip == arriveSound)
            {
                // Saltar el silencio inicial configurado en el sonido de llegada
                audioSource.time = Mathf.Clamp(arriveSoundOffset, 0f, clip.length - 0.1f);
            }
            else
            {
                audioSource.time = 0f;
            }
            audioSource.Play();
        }
    }

    void ShowScreenMsg(string msg, Color col)
    {
        if (powerBox != null)
        {
            powerBox.ShowMessage(msg, col, 3f);
        }
    }

    private IEnumerator EscapeRoutine()
    {
        isEscaping = true;
        Debug.Log("Elevator: Escape iniciado. Cerrando puertas...");
        ShowScreenMsg("DESCENDIENDO AL SOTANO...", Color.cyan);

        // Cambiar la pantalla indicadora a una flecha de descenso ("v")
        if (extTM != null) extTM.text = "v";
        if (intTM != null) intTM.text = "v";

        // 1. Esperar a que se cierren las puertas (1.5 segundos)
        yield return new WaitForSeconds(1.5f);

        // Reproducir sonido de viaje/motor
        if (travelSound != null)
        {
            audioSource.clip = travelSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 2. Desactivar controles de movimiento del jugador
        var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
        if (controller != null) controller.enabled = false;

        // 3. Efecto de sacudida y caída cinemática (4 segundos)
        float elapsed = 0f;
        float duration = 4.0f;
        Vector3 originalCameraLocalPos = Camera.main.transform.localPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Simular conteo regresivo de pisos (1 a -5) durante el descenso
            float pct = elapsed / duration;
            int currentFloor = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(1f, -5f, pct)), -5, 1);
            string floorStr = currentFloor == -5 ? "S" : currentFloor.ToString();
            
            if (extTM != null) extTM.text = floorStr;
            if (intTM != null) intTM.text = floorStr;

            // Simular temblor
            float xOffset = Random.Range(-0.04f, 0.04f);
            float yOffset = Random.Range(-0.04f, 0.04f);
            Camera.main.transform.localPosition = originalCameraLocalPos + new Vector3(xOffset, yOffset, 0f);

            // Fundir a negro progresivamente durante la segunda mitad del viaje (de 2.0s a 4.0s)
            if (elapsed > 2.0f)
            {
                escapeFadeAlpha = Mathf.Clamp01((elapsed - 2.0f) / 2.0f);
            }

            yield return null;
        }

        Camera.main.transform.localPosition = originalCameraLocalPos;
        escapeFadeAlpha = 1f;

        // 4. Iniciar la carga de la escena a través de la pantalla de carga unificada (con consejos)
        SceneLoader.LoadScene(nextSceneName);
    }

    public static void RegisterNote(int pos, int val)
    {
        if (pos >= 1 && pos <= 7)
        {
            foundNotes[pos - 1] = val;
            Debug.Log($"ElevatorController: Registrada nota. Posicion {pos} = {val}");
        }
    }


    void FindPlayer()
    {
        // 1. Buscar por componente CharacterController (objeto del jugador real en movimiento)
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null)
        {
            playerTransform = cc.transform;
            return;
        }

        // 2. Buscar por nombre real
        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null)
        {
            playerTransform = pObj.transform;
            return;
        }

        // 3. Fallback a tag Player
        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null)
        {
            playerTransform = playerTagObj.transform;
            return;
        }

        // 4. Fallback a camara principal
        if (Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void ToggleNotepad()
    {
        isNotepadOpen = !isNotepadOpen;
        
        var controller = playerTransform.GetComponent<StarterAssets.FirstPersonController>();
        if (controller != null) controller.enabled = !isNotepadOpen;

        if (isNotepadOpen)
        {
            MobileInput.SetCursorState(false);
        }
        else
        {
            MobileInput.SetCursorState(true);
        }
    }

    void OnGUI()
    {
        // Dibujar el fundido a negro y la pantalla de carga si el elevador está escapando
        if (isEscaping)
        {
            if (fadeBlackTex == null)
            {
                fadeBlackTex = new Texture2D(2, 2);
                Color c = Color.black;
                fadeBlackTex.SetPixel(0, 0, c);
                fadeBlackTex.SetPixel(0, 1, c);
                fadeBlackTex.SetPixel(1, 0, c);
                fadeBlackTex.SetPixel(1, 1, c);
                fadeBlackTex.Apply();
            }

            GUI.color = new Color(1f, 1f, 1f, escapeFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeBlackTex);
            GUI.color = Color.white;

            if (isAsyncLoading && escapeFadeAlpha >= 0.95f)
            {
                GUIStyle loadStyle = new GUIStyle();
                loadStyle.fontSize = 26;
                loadStyle.alignment = TextAnchor.MiddleCenter;
                loadStyle.fontStyle = FontStyle.Bold;
                loadStyle.normal.textColor = new Color(0.9f, 0.1f, 0.1f); // Rojo sangre

                GUIStyle subLoadStyle = new GUIStyle();
                subLoadStyle.fontSize = 16;
                subLoadStyle.alignment = TextAnchor.MiddleCenter;
                subLoadStyle.normal.textColor = Color.gray;

                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 40), "NIVEL 2: LOS TÚNELES", loadStyle);
                
                string progressText = $"CARGANDO ACCESO DE VENTILACIÓN... {Mathf.RoundToInt(asyncProgress * 100)}%";
                GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 30), progressText, subLoadStyle);
            }
            return;
        }

        // Ocultar si estamos en modo menú
        HospitalMazeGenerator generator = FindObjectOfType<HospitalMazeGenerator>();
        if (generator != null && generator.isMenuMode) return;
        if (isGameEnded)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle();
            titleStyle.fontSize = 32;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(0.9f, 0.1f, 0.1f);

            GUIStyle subStyle = new GUIStyle();
            subStyle.fontSize = 20;
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.normal.textColor = Color.gray;

            GUIStyle promptStyle = new GUIStyle();
            promptStyle.fontSize = 18;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.fontStyle = FontStyle.Italic;
            promptStyle.normal.textColor = new Color(0.3f, 0.75f, 1f);

            GUI.Label(new Rect(0, Screen.height / 2 - 80, Screen.width, 50), "FIN DE LA TRANSMISION", titleStyle);
            GUI.Label(new Rect(0, Screen.height / 2 - 10, Screen.width, 40), "Lograste escapar del hospital en el elevador... por ahora.", subStyle);
            GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 30), "Presiona [R] para reiniciar o [ESC] para salir", promptStyle);
            return;
        }

        // MENU DE LIBRETA DE NOTAS
        if (isNotepadOpen)
        {
            Rect padRect = new Rect(Screen.width / 2 - 180, Screen.height / 2 - 200, 360, 380);
            
            GUI.color = new Color(0.96f, 0.94f, 0.82f, 0.98f);
            GUI.DrawTexture(padRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle();
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.UpperCenter;
            titleStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f);
            GUI.Box(padRect, "LIBRETA DE NOTAS", titleStyle);

            GUIStyle subStyle = new GUIStyle();
            subStyle.fontSize = 14;
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(padRect.x, padRect.y + 45, padRect.width, 30), "Codigo de la Oficina del Director:", subStyle);

            float startX = padRect.x + 22f;
            float startY = padRect.y + 85f;
            float slotW = 38f;
            float slotH = 45f;
            float spacingX = 7f;

            GUIStyle slotStyle = new GUIStyle();
            slotStyle.fontSize = 22;
            slotStyle.fontStyle = FontStyle.Bold;
            slotStyle.alignment = TextAnchor.MiddleCenter;
            slotStyle.normal.textColor = new Color(0.05f, 0.5f, 0.1f);

            for (int i = 0; i < 7; i++)
            {
                Rect slotRect = new Rect(startX + i * (slotW + spacingX), startY, slotW, slotH);
                
                GUI.color = Color.white;
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                
                GUI.color = Color.black;
                GUI.Box(slotRect, "");
                GUI.color = Color.white;

                string slotVal = foundNotes[i] != -1 ? foundNotes[i].ToString() : "_";
                GUI.Label(slotRect, slotVal, slotStyle);
            }

            GUIStyle hintStyle = new GUIStyle();
            hintStyle.fontSize = 13;
            hintStyle.alignment = TextAnchor.UpperLeft;
            hintStyle.wordWrap = true;
            hintStyle.normal.textColor = Color.black;

            string hintText = "Pistas encontradas en el laberinto:\n\n";
            int notesCount = 0;
            for (int i = 0; i < 7; i++)
            {
                if (foundNotes[i] != -1)
                {
                    notesCount++;
                    hintText += $"• Digito {i + 1} del codigo: {foundNotes[i]}\n";
                }
            }

            if (notesCount == 0)
            {
                hintText += "(Aun no has encontrado ninguna nota. Busca papeles blancos con numeros en las consultas y oficinas del hospital).";
            }
            else if (notesCount == 7)
            {
                hintText += "¡Codigo completo descubierto! Ve a la puerta de la Oficina del Director e ingresa los 7 numeros.";
            }
            else
            {
                hintText += $"\n({notesCount} de 7 notas encontradas. Sigue explorando para rellenar los casilleros vacios).";
            }

            GUI.Label(new Rect(padRect.x + 25, padRect.y + 145, padRect.width - 50, 180), hintText, hintStyle);

            Rect closeBtn = new Rect(padRect.x + padRect.width / 2 - 50, padRect.y + padRect.height - 40, 100, 30);
            if (GUI.Button(closeBtn, "Cerrar"))
            {
                ToggleNotepad();
            }
            return;
        }

        // ICONO DE PAPEL DE LIBRETA (Siempre visible en el HUD superior derecho)
        if (playerTransform != null && !isEscaping)
        {
            Rect iconRect = new Rect(Screen.width - 330, 25, 180, 45);
            
            GUIStyle iconStyle = new GUIStyle();
            iconStyle.fontSize = 16;
            iconStyle.alignment = TextAnchor.MiddleCenter;
            iconStyle.fontStyle = FontStyle.Bold;
            
            GUI.color = new Color(0f, 0.1f, 0.2f, 0.7f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            iconStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);
            if (GUI.Button(iconRect, "📝 LIBRETA [TAB]", iconStyle))
            {
                ToggleNotepad();
            }
        }

        if (playerTransform == null || isEscaping) return;

        // isInside solo es verdadero si las puertas estan ABIERTAS y el jugador esta dentro de la cabina.
        // Si las puertas están cerradas, aunque esté a 3m, NO está "dentro".
        bool isInside = doorsOpen && Vector3.Distance(transform.position, playerTransform.position) <= 3.0f;

        float dist = interactDistance + 1f;
        if (isInside && intButtonTrans != null)
        {
            dist = Vector3.Distance(intButtonTrans.position, playerTransform.position);
        }
        else if (!isInside && extButtonTrans != null)
        {
            dist = Vector3.Distance(extButtonTrans.position, playerTransform.position);
        }
        else
        {
            dist = Vector3.Distance(transform.position, playerTransform.position);
        }

        if (dist > interactDistance) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (!isInside)
        {
            // 1. Verificar que el jugador esté mirando DE FRENTE hacia las puertas o botón del elevador si está afuera
            Transform targetFocus = extButtonTrans != null ? extButtonTrans : transform;
            Vector3 dirToElevator = (targetFocus.position - cam.transform.position).normalized;
            float faceDot = Vector3.Dot(cam.transform.forward, dirToElevator);

            if (faceDot < 0.35f) return; // NUNCA MOSTRAR SI EL JUGADOR ESTÁ DE ESPALDAS AFUERA

            // 2. Verificar que la mirilla central impacte directamente sobre el botón, puertas o cabina (NUNCA EN PAREDES)
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;
            bool lookingAtElevator = false;

            if (Physics.Raycast(ray, out hit, interactDistance + 1.2f))
            {
                string n = hit.transform.name.ToLower();
                bool isElevatorPart = hit.transform == transform || hit.transform.IsChildOf(transform) ||
                                      hit.transform == extButtonTrans || hit.transform == intButtonTrans ||
                                      n.Contains("elevator") || n.Contains("ascensor");

                if (isElevatorPart)
                {
                    lookingAtElevator = true;
                }
            }

            if (!lookingAtElevator) return;
        }

        string promptText = "";
        Color textColor = Color.white;

        bool hasPower = roomLightsManager == null || !roomLightsManager.powerOutage;

        if (isInside)
        {
            if (!isEscaping)
            {
                promptText = hasPower ? "[E]  Iniciar Descenso al Sotano" : "Elevador sin Energia (Repare Fusibles)";
                textColor = hasPower ? Color.green : Color.red;
            }
        }
        else
        {
            if (!keycardUsed && !bypassKeycard)
            {
                promptText = hasKeycard ? "[E]  Insertar Tarjeta del Director" : "Panel Cerrado (Requiere Tarjeta de Acceso)";
                textColor = hasKeycard ? new Color(0.3f, 0.75f, 1f) : Color.yellow;
            }
            else if (!isCalling && !isArrived)
            {
                promptText = hasPower ? "[E]  Llamar al Elevador" : "Botonera sin Energia (Repare Fusibles)";
                textColor = hasPower ? Color.cyan : Color.red;
            }
            else if (isCalling && !isArrived)
            {
                int remaining = Mathf.CeilToInt(currentTimer);
                promptText = hasPower ? "Elevador descendiendo... (" + remaining + "s)" : "Llamada Suspendida (Sin Energia)";
                textColor = hasPower ? Color.cyan : Color.red;
            }
        }

        if (string.IsNullOrEmpty(promptText)) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 150, 520, 50);

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), promptText, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, promptText, style);
    }
}

