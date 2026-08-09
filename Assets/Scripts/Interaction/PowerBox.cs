using UnityEngine;
using System.Collections;

public class PowerBox : MonoBehaviour
{
    public RoomLightsManager roomLightsManager; // Referencia al sistema de luces del cuarto
    public bool isPowerOut = false; // Estado del fallo elctrico
    private bool lastPowerState; // Guarda el ltimo estado de isPowerOut
    public AudioSource electricitySound; // Sonido del fallo elctrico

    [Header("Sobrecarga de Red")]
    public float maxPowerCapacity = 150f; // Capacidad de energa en segundos (2.5 minutos base)
    public float currentPowerCapacity;
    public float baseDrainRate = 0.5f;     // Consumo base de la red por segundo (incluso con luces apagadas)
    public float perLightDrainRate = 2.5f; // Cunto acelera el drenaje cada bombilla encendida

    [Header("Mecnica de Fusibles (Opcin B)")]
    public int maxFreeRepairs = 2;         // Rearmados gratis antes de fundirse definitivamente
    public int repairsCount = 0;          // Cantidad de rearmados realizados
    public int fusesCount = 0;            // Fusibles de repuesto en posesin del jugador
    private float itemCheckTimer = 0f;    // Temporizador de control periódico de recursos (Fácil, Normal, Difícil)

    [Header("UI & HUD")]
    public Texture2D fuseIcon;            // Textura del icono de fusible para el HUD
    public AudioClip blackoutSoundClip;
    private LightSwitch[] allSwitches; // Cach de todos los interruptores del nivel
    private Light faultLight;          // Luz roja dinmica de indicacin de fallo

    // Variables para la interfaz grfica OnGUI (Feedback directo)
    private float showWarningTimer = 0f;
    private string uiMessage = "";
    private Color uiMessageColor = Color.red;
    private float lastInteractionTime = 0f; // Evita doble interacción móvil
    private bool isInitializing = false;    // Suprime mensajes y sonido en la inicialización inicial

    void Start()
    {
        // Configuración equilibrada e intensa para mapa compacto
        maxPowerCapacity = 90f;         // Capacidad máxima de 90 segundos (1.5 minutos de luz tras rearmar)
        baseDrainRate = 0.5f;           // Consumo base constante
        perLightDrainRate = 0.4f;       // Cada luz encendida acelera la caída
        maxFreeRepairs = 1;

        // Empezar la partida con las luces ENCENDIDAS
        isPowerOut = false;
        lastPowerState = false;
        currentPowerCapacity = maxPowerCapacity;

        if (roomLightsManager == null)
        {
            roomLightsManager = FindObjectOfType<RoomLightsManager>();
            if (roomLightsManager == null)
                Debug.LogWarning("PowerBox: RoomLightsManager no encontrado en la escena. El corte de luz no afectará las luces.");
        }

        lastPowerState = isPowerOut;
        currentPowerCapacity = maxPowerCapacity;

        if (blackoutSoundClip == null)
        {
            blackoutSoundClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
            if (blackoutSoundClip == null) blackoutSoundClip = Resources.Load<AudioClip>("Apagon_Sonido");
        }

        if (electricitySound != null)
        {
            electricitySound.spatialBlend = 1f;
            electricitySound.minDistance = 5f;
            electricitySound.maxDistance = 50f;
            electricitySound.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        // Buscar todos los interruptores en la escena
        allSwitches = FindObjectsOfType<LightSwitch>();
        Debug.Log($"PowerBox: Se detectaron {allSwitches.Length} interruptores de luz en el nivel.");

        // Forzar encendido inicial al iniciar la escena (SIN mensaje ni sonido)
        isInitializing = true;
        TriggerPowerOutage(false);
        isInitializing = false;

        // Iniciar la cuenta regresiva para el apagón automático inicial
        StartCoroutine(InitialPowerTimerCoroutine());
    }

    private System.Collections.IEnumerator InitialPowerTimerCoroutine()
    {
        // El primer apagón ocurre exactamente a los 25 segundos de empezar el juego
        float waitTime = 25f;
        Debug.Log($"PowerBox: Las luces comienzan encendidas. Primer apagón programado en {waitTime} segundos...");
        yield return new WaitForSeconds(waitTime);

        if (!isPowerOut)
        {
            isPowerOut = true;
            lastPowerState = true;
            currentPowerCapacity = 0f;
            TriggerPowerOutage(true);
            Debug.LogWarning("PowerBox: ¡PRIMER APAGÓN DE IMPACTO ACTIVADO A LOS 25 SEGUNDOS!");
        }
    }

    void Update()
    {
        // Permitir activar/desactivar manualmente desde el Inspector
        if (isPowerOut != lastPowerState)
        {
            TriggerPowerOutage(isPowerOut);
            lastPowerState = isPowerOut;
            if (!isPowerOut)
            {
                currentPowerCapacity = maxPowerCapacity;
            }
        }

        // Lógica de interacción mediante mira (InteractionFocusManager) y tecla E / Móvil
        bool isFocused = InteractionFocusManager.IsFocused(gameObject, 3.5f);
        if (isFocused)
        {
            if (MobileInput.GetKeyDown(KeyCode.E))
            {
                OnMouseDown();
            }
        }

        // Lógica de consumo cuando la luz está encendida
        if (!isPowerOut)
        {
            // Contar cuántas luces están encendidas actualmente
            int activeLights = 0;
            if (allSwitches == null) allSwitches = FindObjectsOfType<LightSwitch>();
            if (allSwitches != null)
            {
                foreach (LightSwitch sw in allSwitches)
                {
                    if (sw != null && sw.isOn)
                    {
                        activeLights++;
                    }
                }
            }

            // Calcular velocidad de drenaje (Base + LucesEncendidas * Multiplicador)
            float keycardMultiplier = ElevatorController.hasKeycard ? 2.5f : 1.0f;
            float drainSpeed = (baseDrainRate + (activeLights * perLightDrainRate)) * keycardMultiplier;
            currentPowerCapacity -= Time.deltaTime * drainSpeed;
            currentPowerCapacity = Mathf.Clamp(currentPowerCapacity, 0f, maxPowerCapacity);

            // Si se agota la capacidad de carga, se botan los fusibles (Apagn)
            if (currentPowerCapacity <= 0f)
            {
                isPowerOut = true;
                lastPowerState = true;
                TriggerPowerOutage(true);
                Debug.LogWarning("PowerBox: SOBRECARGA ELCTRICA! Fusibles fundidos.");
            }
        }

        // Lgica de la luz roja de fallo elctrico (Pulsante)
        bool needsFuse = isPowerOut && (repairsCount >= maxFreeRepairs);
        if (needsFuse)
        {
            if (faultLight == null)
            {
                CreateFaultLight();
            }
            else
            {
                faultLight.enabled = true;
                // Hacer que la luz roja pulse de forma ttrica (de 0 a 2 de intensidad)
                faultLight.intensity = Mathf.PingPong(Time.time * 2.5f, 2.0f);
            }
        }
        else
        {
            if (faultLight != null && faultLight.enabled)
            {
                faultLight.enabled = false;
            }
        
        // LEGACY REMOVED: HospitalMazeGenerator anti-softlock block
        // (GetActiveFusesCount, SpawnEmergencyFuse, GetActiveBatteriesCount, SpawnEmergencyBattery
        //  no longer exist on ModularHospital.ModularHospitalGenerator)
        }
    }

    private void CreateFaultLight()
    {
        // Crear un objeto hijo para la luz de advertencia
        GameObject lightObj = new GameObject("PowerBox_FaultLight");
        lightObj.transform.SetParent(transform);
        // Posicionar ligeramente al frente del panel para iluminar la caja
        lightObj.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        lightObj.transform.localRotation = Quaternion.identity;

        faultLight = lightObj.AddComponent<Light>();
        faultLight.type = LightType.Point;
        faultLight.color = Color.red;
        faultLight.range = 2.5f;
        faultLight.intensity = 1.5f;
        faultLight.shadows = LightShadows.None;
        
        Debug.Log("PowerBox: Luz roja de advertencia de fallo creada dinmicamente.");
    }

    public void Interact()
    {
        OnMouseDown();
    }

    // Interacción del jugador al hacer clic directo en la caja de fusibles para rearmarla
    void OnMouseDown()
    {
        // Evitar doble interacción por toques múltiples o simulación de clics en móviles (cooldown de 0.6 segundos)
        if (Time.time - lastInteractionTime < 0.6f)
        {
            return;
        }
        lastInteractionTime = Time.time;

        // Comprobar la distancia PRIMERO — no responder si el jugador está lejos (distancia hasta 7 metros)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float dist = Vector3.Distance(transform.position, mainCam.transform.position);
            if (dist > 7f)
            {
                // No mostrar ningún mensaje — simplemente ignorar el clic
                return;
            }
        }

        // Verificar que todos los subgeneradores estén activos antes de permitir el rearmado
        SubGenerator[] subGens = FindObjectsOfType<SubGenerator>();
        int activeCount = 0;
        foreach (var gen in subGens)
        {
            if (gen != null && gen.isOn) activeCount++;
        }

        if (activeCount < subGens.Length && subGens.Length > 0)
        {
            string msg = LocalizationManager.Instance != null 
                ? string.Format(LocalizationManager.Instance.Get("msg_no_network"), activeCount, subGens.Length)
                : $"SIN RED ELÉCTRICA: Activa todos los Subgeneradores ({activeCount}/{subGens.Length}) en el hospital.";
            ShowMessage(msg, Color.red, 4.5f);
            if (electricitySound != null)
            {
                electricitySound.PlayOneShot(electricitySound.clip);
            }
            return;
        }

        if (isPowerOut)
        {
            if (repairsCount < maxFreeRepairs)
            {
                // Rearmado gratuito de seguridad
                repairsCount++;
                isPowerOut = false;
                lastPowerState = false;
                TriggerPowerOutage(false);
                currentPowerCapacity = Random.Range(maxPowerCapacity * 0.8f, maxPowerCapacity);

                // Sonido de clic/interruptor
                PlayInteractAudio();

                string msg = LocalizationManager.Instance != null 
                    ? string.Format(LocalizationManager.Instance.Get("msg_fuse_repaired"), repairsCount, maxFreeRepairs)
                    : $"Fusibles rearmados. ({repairsCount}/{maxFreeRepairs} reparaciones libres usadas)";
                ShowMessage(msg, Color.green, 4f);
                Debug.Log($"PowerBox: Rearmado gratuito exitoso ({repairsCount}/{maxFreeRepairs}).");

                // Disparar monólogo/pensamiento del jugador
                PlayerMonologueManager.ShowDialogue("Bien, el sistema eléctrico principal está restaurado. Ahora la oficina del director debería tener energía.", 5f);
            }
            else
            {
                // Requiere fusible de repuesto obligatoriamente
                if (fusesCount > 0)
                {
                    fusesCount--;
                    isPowerOut = false;
                    lastPowerState = false;
                    TriggerPowerOutage(false);
                    currentPowerCapacity = Random.Range(maxPowerCapacity * 0.8f, maxPowerCapacity);

                    // Sonido de clic/interruptor
                    PlayInteractAudio();

                    string msg = LocalizationManager.Instance != null 
                        ? string.Format(LocalizationManager.Instance.Get("msg_fuse_placed"), fusesCount)
                        : $"Fusible de repuesto colocado! Energía restablecida. (Quedan: {fusesCount})";
                    ShowMessage(msg, Color.green, 4f);
                    Debug.Log($"PowerBox: Fusible consumido. Fusibles restantes: {fusesCount}.");

                    // Disparar monólogo/pensamiento del jugador
                    PlayerMonologueManager.ShowDialogue("Fusible reemplazado. Volvemos a tener corriente eléctrica. Debo darme prisa antes de otra sobrecarga.", 5f);
                }
                else
                {
                    // No hay fusibles ni reparaciones libres - comprobar e instanciar fusible de emergencia inmediatamente
                    CheckAndSpawnEmergencyFuseInstant();

                    string msg = LocalizationManager.Instance != null 
                        ? LocalizationManager.Instance.Get("msg_fuse_burned") 
                        : "FUSIBLE QUEMADO PERMANENTEMENTE!\nEncuentra un fusible de repuesto en las habitaciones.";
                    ShowMessage(msg, Color.red, 5f);
                    
                    // Reproducir un sonido de error si el AudioSource existe
                    if (electricitySound != null)
                    {
                        electricitySound.PlayOneShot(electricitySound.clip); // Chispazo corto de advertencia
                    }
                    Debug.LogWarning("PowerBox: No se pudo rearmar. Se requiere un fusible de repuesto.");
                }
            }
        }
        else
        {
            string msg = LocalizationManager.Instance != null 
                ? string.Format(LocalizationManager.Instance.Get("msg_stable_fuse"), fusesCount)
                : $"Fusibles en estado estable. Inventario: {fusesCount} fusible(s).";
            ShowMessage(msg, Color.white, 3f);
        }
    }

    [Header("Visual del Fusible")]
    public GameObject internalFuseMesh;

    void TriggerPowerOutage(bool state)
    {
        // Control visual del fusible dentro de la caja de fusibles (desactivar al apagón, activar al rearmar)
        if (internalFuseMesh == null)
        {
            Transform[] childs = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in childs)
            {
                if (t != null && t != transform && t.name.ToLower().Contains("fuse") && !t.name.ToLower().Contains("box"))
                {
                    internalFuseMesh = t.gameObject;
                    break;
                }
            }
        }
        if (internalFuseMesh != null)
        {
            internalFuseMesh.SetActive(!state);
        }

        if (roomLightsManager != null)
        {
            roomLightsManager.TriggerPowerOutage(state);
        }
        else
        {
            // Auto-control de todas las luces de lámparas del mapa procedural (excluyendo linterna y luces de generadores)
            Light[] allLights = FindObjectsOfType<Light>(true);
            foreach (Light l in allLights)
            {
                if (l != null && l.type == LightType.Point)
                {
                    string n = l.name.ToLower();
                    if (!n.Contains("generator") && !n.Contains("flashlight") && !n.Contains("linterna") && !n.Contains("player"))
                    {
                        l.enabled = !state;
                    }
                }
            }

            // Auto-control de los materiales y emisión de las lámparas de techo (oscurecer tubos en apagón)
            Renderer[] allRenderers = FindObjectsOfType<Renderer>(true);
            foreach (Renderer r in allRenderers)
            {
                if (r != null && r.gameObject != null)
                {
                    string rName = r.gameObject.name.ToLower();
                    string parentName = (r.transform.parent != null) ? r.transform.parent.name.ToLower() : "";
                    if (rName.Contains("lamp") || rName.Contains("lampara") || rName.Contains("luz") || parentName.Contains("lamp") || parentName.Contains("lampara"))
                    {
                        foreach (Material m in r.materials)
                        {
                            if (m != null)
                            {
                                if (state) // Apagón -> Oscurecer tubo por completo
                                {
                                    m.SetColor("_EmissionColor", Color.black);
                                    m.DisableKeyword("_EMISSION");
                                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.18f, 0.18f, 0.18f));
                                }
                                else // Energía RESTABLECIDA -> Hacer brillar el tubo verde-esmeralda
                                {
                                    m.EnableKeyword("_EMISSION");
                                    m.SetColor("_EmissionColor", new Color(0.35f, 0.92f, 0.70f) * 2.2f);
                                    if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (state)
        {
            // AL OCURRIR UN APAGÓN: EL MONSTRUO APARECE Y CAZA AL JUGADOR EN LA OSCURIDAD
            // Reposicionar al enemigo a distancia MODERADA (10-15m) del jugador para posible encuentro en pasillo
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            Vector3 playerPos = playerObj != null ? playerObj.transform.position : transform.position;

            EnemyAIBookHead bookHead = FindObjectOfType<EnemyAIBookHead>(true);
            if (bookHead != null)
            {
                RelocateEnemyModerateDistance(bookHead.gameObject, playerPos, 10f, 15f);
                bookHead.gameObject.SetActive(true);
                bookHead.detectionRange = 9.0f;   // Ligeramente mayor en la oscuridad
                bookHead.runSpeed = 2.3f;           // Correr amenazante pero equilibrado
                Debug.Log("PowerBox: ¡Monstruo BookHead activado por el apagón a distancia moderada!");
            }

            EnemyAIController enemyController = FindObjectOfType<EnemyAIController>(true);
            if (enemyController != null)
            {
                RelocateEnemyModerateDistance(enemyController.gameObject, playerPos, 10f, 15f);
                enemyController.gameObject.SetActive(true);
                enemyController.detectionRange = 9.0f;
                enemyController.runSpeed = 2.3f;
                Debug.Log("PowerBox: ¡Monstruo EnemyAIController activado por el apagón a distancia moderada!");
            }

            // Reproducir sonido impactante de chispazo y cortocircuito directo en 2D en los oídos del jugador
            AudioClip popClip = blackoutSoundClip != null ? blackoutSoundClip : Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
            if (popClip == null) popClip = Resources.Load<AudioClip>("Apagon_Sonido");
            if (popClip == null) popClip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
            if (popClip == null) popClip = Resources.Load<AudioClip>("Interruptor");

            if (popClip != null)
            {
                Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(popClip, playPos, 1.0f);
            }

            ShowMessage("¡CORTE ELÉCTRICO! Los fusibles han fallado. Activa Subgeneradores A y B para rearmar.", Color.red, 5.0f);
            // 1. Garantizar una penumbra ambiental de emergencia visible y constante (Evita pantalla 100% negra)
            float curGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f * curGamma, 0.16f * curGamma, 0.19f * curGamma);
            RenderSettings.ambientIntensity = Mathf.Max(0.6f, curGamma * 0.8f);

            // 2. Activar chispas dinámicas en lámparas apagadas del pasillo del Hospital
            Renderer[] hospitalRenderers = FindObjectsOfType<Renderer>(true);
            foreach (Renderer r in hospitalRenderers)
            {
                if (r != null && r.gameObject != null)
                {
                    string rName = r.gameObject.name.ToLower();
                    if ((rName.Contains("lamp") || rName.Contains("lampara") || rName.Contains("luz")) && r.gameObject.GetComponent<TunnelElectricSparks>() == null)
                    {
                        r.gameObject.AddComponent<TunnelElectricSparks>();
                    }
                }
            }

            // Si es un apagón, comprobar e instanciar fusible de emergencia inmediatamente
            CheckAndSpawnEmergencyFuseInstant();
        }
        else
        {
            // AL RESTABLECER LA ENERGÍA / LUZ: EL MONSTRUO SE REPLIEGA Y SE DESACTIVA DE LA ESCENA
            EnemyAIBookHead bookHead = FindObjectOfType<EnemyAIBookHead>(true);
            if (bookHead != null)
            {
                bookHead.detectionRange = 7.5f;
                bookHead.gameObject.SetActive(false);
                Debug.Log("PowerBox: Monstruo BookHead desactivado al restablecer las luces.");
            }

            EnemyAIController enemyController = FindObjectOfType<EnemyAIController>(true);
            if (enemyController != null)
            {
                enemyController.detectionRange = 7.5f;
                enemyController.gameObject.SetActive(false);
                Debug.Log("PowerBox: Monstruo EnemyAIController desactivado al restablecer las luces.");
            }

                float curGamma = PlayerPrefs.GetFloat("GammaLevel", 1.0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.35f * curGamma, 0.36f * curGamma, 0.38f * curGamma);
                RenderSettings.ambientIntensity = curGamma;

                if (!isInitializing)
                {
                    AudioClip onClip = Resources.Load<AudioClip>("Interruptor");
                    if (onClip != null)
                    {
                        Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                        AudioSource.PlayClipAtPoint(onClip, playPos, 1.0f);
                    }
                    ShowMessage("¡RED ELÉCTRICA RESTABLECIDA! Energía alimentando el hospital.", Color.green, 5.0f);
                }
                Debug.Log("PowerBox: Energía restablecida.");
            }
        }

    /// <summary>
    /// Verifica al instante si el jugador está bloqueado sin fusibles y genera uno de emergencia.
    /// </summary>
    public void CheckAndSpawnEmergencyFuseInstant()
    {
        // LEGACY REMOVED: HospitalMazeGenerator.GetActiveFusesCount / SpawnEmergencyFuse
        // no longer exist on ModularHospital.ModularHospitalGenerator
    }

    public void ShowMessage(string message, Color color, float duration)
    {
        uiMessage = message;
        uiMessageColor = color;
        showWarningTimer = duration;
    }

    void OnGUI()
    {
        // Ocultar si el juego está pausado (ej. leyendo una nota a pantalla completa)
        if (Time.timeScale == 0f) return;

        // Ocultar si estamos en el cuaderno
        if (NotepadUIManager.IsOpen) return;

        // Ocultar si estamos en modo menú
        ModularHospital.ModularHospitalGenerator generator = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (generator != null && generator.isMenuMode) return;

        // 0. Cartel de interacción [E] cuando la mirilla enfoca directamente la Caja de Fusibles (distancia corta 2.2m)
        if (InteractionFocusManager.IsFocused(gameObject, 2.2f))
        {
            GUIStyle pStyle = new GUIStyle();
            pStyle.fontSize = 22;
            pStyle.alignment = TextAnchor.MiddleCenter;
            pStyle.fontStyle = FontStyle.Bold;

            Rect pRect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

            GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
            GUI.DrawTexture(new Rect(pRect.x - 10, pRect.y - 5, pRect.width + 20, pRect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            pStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(pRect.x + 2, pRect.y + 2, pRect.width, pRect.height), "[E]  Rearmar Caja de Fusibles", pStyle);

            pStyle.normal.textColor = isPowerOut ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.5f);
            GUI.Label(pRect, "[E]  Rearmar Caja de Fusibles", pStyle);
        }
        // 1. Mensaje de advertencia temporal en el centro de la pantalla
        if (showWarningTimer > 0f)
        {
            showWarningTimer -= Time.deltaTime;

            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;

            style.normal.textColor = Color.black;
            Rect shadowRect = new Rect(Screen.width / 2 - 398, Screen.height / 2 - 98, 800, 200);
            GUI.Label(shadowRect, uiMessage, style);

            style.normal.textColor = uiMessageColor;
            Rect rect = new Rect(Screen.width / 2 - 400, Screen.height / 2 - 100, 800, 200);
            GUI.Label(rect, uiMessage, style);
        }

        // 2. HUD de Subgeneradores y Fusibles Dinámico (Escalable según configuración de usuario)
        float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
        Matrix4x4 oldHudMat = GUI.matrix;
        if (hudScale != 1.0f)
        {
            Vector2 pivot = new Vector2(Screen.width - 25, 25);
            GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
        }

        SubGenerator[] subGens = FindObjectsOfType<SubGenerator>();
        if (subGens != null && subGens.Length > 0)
        {
            // Ordenar alfabéticamente para que salgan en orden A, B, C, D...
            System.Array.Sort(subGens, (x, y) => string.Compare(x.generatorName, y.generatorName));

            int numGens = subGens.Length;
            float boxWidth = 20 + numGens * 46;
            
            // Posicionamiento alineado a la derecha debajo del HUD de fusibles (Compacto y elegante)
            Rect genHudRect = new Rect(Screen.width - 25 - boxWidth, 98, boxWidth, 65);
            
            // Dibujar caja de fondo oscura y semitransparente
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(genHudRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle genTitleStyle = new GUIStyle();
            genTitleStyle.fontSize = 11;
            genTitleStyle.fontStyle = FontStyle.Bold;
            genTitleStyle.alignment = TextAnchor.UpperCenter;
            genTitleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(genHudRect.x, genHudRect.y + 6, genHudRect.width, 16), "SUB-GENS", genTitleStyle);

            GUIStyle charStyle = new GUIStyle();
            charStyle.fontSize = 16;
            charStyle.fontStyle = FontStyle.Bold;
            charStyle.alignment = TextAnchor.MiddleCenter;
            charStyle.normal.textColor = Color.white;

            // Dibujar dinámicamente cada subgenerador presente en el mapa
            for (int i = 0; i < numGens; i++)
            {
                var gen = subGens[i];
                if (gen == null) continue;
                
                float xPos = genHudRect.x + 10 + i * 46;
                bool genOn = gen.isOn;
                
                // Color: Verde si está activado, Rojo si está apagado
                GUI.color = genOn ? new Color(0.1f, 0.8f, 0.2f, 0.85f) : new Color(0.8f, 0.2f, 0.2f, 0.85f);
                GUI.DrawTexture(new Rect(xPos, genHudRect.y + 26, 36, 28), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(xPos, genHudRect.y + 26, 36, 28), gen.generatorName, charStyle);
            }
        }

        // --- HUD de Fusibles (Esquina Superior Derecha - Icono + Contador Elegante) ---
        Rect hudRect = new Rect(Screen.width - 135, 25, 110, 65);
        
        // Dibujar caja de fondo oscura y semitransparente
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(hudRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle hudStyle = new GUIStyle();
        hudStyle.fontSize = 20;
        hudStyle.fontStyle = FontStyle.Bold;
        hudStyle.alignment = TextAnchor.MiddleLeft;

        if (fuseIcon == null)
        {
#if UNITY_EDITOR
            fuseIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dnk_Dev/FuseBox/Fuse_Icon.png");
#endif
            if (fuseIcon == null) fuseIcon = Resources.Load<Texture2D>("Fuse_Icon");
            if (fuseIcon == null) fuseIcon = Resources.Load<Texture2D>("UI/Fuse_Icon");
            if (fuseIcon == null)
            {
                fuseIcon = GetProceduralFuseTexture();
            }
        }

        Rect iconRect = new Rect(hudRect.x + 8, hudRect.y + 6, 44, 52);
        if (fuseIcon != null)
        {
            GUI.DrawTexture(iconRect, fuseIcon, ScaleMode.ScaleToFit, true);
        }
        else
        {
            Rect fGraphic = new Rect(hudRect.x + 18, hudRect.y + 16, 20, 32);
            GUI.color = new Color(0.85f, 0.65f, 0.13f);
            GUI.DrawTexture(new Rect(fGraphic.x, fGraphic.y, fGraphic.width, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(fGraphic.x, fGraphic.y + fGraphic.height - 4, fGraphic.width, 4), Texture2D.whiteTexture);
            GUI.color = fusesCount > 0 ? new Color(0.1f, 0.85f, 0.1f) : new Color(0.8f, 0.2f, 0.2f);
            GUI.DrawTexture(new Rect(fGraphic.x, fGraphic.y + 4, fGraphic.width, fGraphic.height - 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        hudStyle.normal.textColor = fusesCount > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        Rect textRect = new Rect(hudRect.x + 60, hudRect.y, 45, hudRect.height);
        GUI.Label(textRect, "x" + fusesCount, hudStyle);

        GUI.matrix = oldHudMat;
    }

    public void ForceKeycardBlackoutAndRoar()
    {
        isPowerOut = true;
        lastPowerState = true;
        currentPowerCapacity = 0f;
        TriggerPowerOutage(true);

        AudioClip roar = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Monstruo_Alerta");
        if (roar == null) roar = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/RugidoRastrero");
        if (roar == null) roar = Resources.Load<AudioClip>("Audio/Compartido/Impacto_1");

        if (roar != null)
        {
            GameObject roarObj = new GameObject("Keycard_Roar_Source");
            AudioSource aSrc = roarObj.AddComponent<AudioSource>();
            aSrc.clip = roar;
            aSrc.spatialBlend = 0f; // 2D en auriculares
            aSrc.volume = 1.0f;     // Volumen máximo
            aSrc.Play();
            Destroy(roarObj, roar.length + 0.5f);
            Debug.Log("PowerBox: Rugido estruendoso del apagón reproducido en 2D.");
        }
    }

    private void RelocateEnemyModerateDistance(GameObject enemyObj, Vector3 playerPos, float minDistance = 10f, float maxDistance = 15f)
    {
        if (enemyObj == null) return;

        GameObject patrolHolder = GameObject.Find("[BookHead_Patrol_Points]");
        Vector3 bestPos = enemyObj.transform.position;
        float bestScore = 999999f;
        float targetDistance = (minDistance + maxDistance) * 0.5f;

        if (patrolHolder != null)
        {
            Transform[] pts = patrolHolder.GetComponentsInChildren<Transform>();
            foreach (Transform pt in pts)
            {
                if (pt != null && pt != patrolHolder.transform)
                {
                    float d = Vector3.Distance(pt.position, playerPos);
                    if (d >= 8f && d <= maxDistance + 5f)
                    {
                        float score = Mathf.Abs(d - targetDistance);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestPos = pt.position;
                        }
                    }
                }
            }
        }

        UnityEngine.AI.NavMeshAgent agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(bestPos);
        }
        else
        {
            enemyObj.transform.position = bestPos;
        }
    }

    private void RelocateEnemyFarFromPlayer(GameObject enemyObj, Vector3 playerPos, float minDistance)
    {
        if (enemyObj == null) return;

        GameObject patrolHolder = GameObject.Find("[BookHead_Patrol_Points]");
        Vector3 bestPos = enemyObj.transform.position;
        float maxDist = -1f;

        if (patrolHolder != null)
        {
            Transform[] pts = patrolHolder.GetComponentsInChildren<Transform>();
            foreach (Transform pt in pts)
            {
                if (pt != null && pt != patrolHolder.transform)
                {
                    float d = Vector3.Distance(pt.position, playerPos);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        bestPos = pt.position;
                    }
                }
            }
        }

        if (maxDist >= minDistance || Vector3.Distance(enemyObj.transform.position, playerPos) < minDistance)
        {
            UnityEngine.AI.NavMeshAgent agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.Warp(bestPos);
            }
            else
            {
                enemyObj.transform.position = bestPos;
            }
            Debug.Log($"PowerBox: Monstruo teletransportado a punto distante ({maxDist:F1}m del jugador).");
        }
    }

    private void PlayInteractAudio()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (clip == null) clip = Resources.Load<AudioClip>("Interruptor");
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/Compartido/Bateria_Pickup");
        if (clip == null) clip = Resources.Load<AudioClip>("Click");

        if (clip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, pos, 1.0f);
        }
    }

    private static Texture2D proceduralFuseTex;
    private static Texture2D GetProceduralFuseTexture()
    {
        if (proceduralFuseTex != null) return proceduralFuseTex;
        int w = 32, h = 48;
        proceduralFuseTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        Color metalColor = new Color(0.85f, 0.85f, 0.88f, 1f); // Tapa metálica
        Color glassColor = new Color(0.95f, 0.70f, 0.15f, 1f); // Cristal dorado
        Color wireColor = new Color(0.2f, 0.2f, 0.2f, 1f);     // Filamento

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x >= 6 && x <= 25 && y >= 4 && y <= 43)
                {
                    if (y >= 34 || y <= 13)
                    {
                        proceduralFuseTex.SetPixel(x, y, metalColor);
                    }
                    else if (x >= 14 && x <= 17)
                    {
                        proceduralFuseTex.SetPixel(x, y, wireColor);
                    }
                    else
                    {
                        proceduralFuseTex.SetPixel(x, y, glassColor);
                    }
                }
                else
                {
                    proceduralFuseTex.SetPixel(x, y, transparent);
                }
            }
        }
        proceduralFuseTex.Apply();
        return proceduralFuseTex;
    }
}
