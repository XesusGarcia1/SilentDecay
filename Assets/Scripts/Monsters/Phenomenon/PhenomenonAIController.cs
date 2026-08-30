using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using StarterAssets;

public class PhenomenonAIController : MonoBehaviour
{
    public enum PhenomenonState
    {
        Patrol,
        Alert,
        Investigate,
        Chase,
        ObservingLight,
        Attack
    }

    [Header("Referencias del Jugador")]
    public Transform player;
    public float attackRange = 2.2f;
    public float detectionRange = 18f;

    [Header("Velocidades de NavMesh")]
    public float patrolSpeed = 3.0f;
    public float investigateSpeed = 2.4f;
    public float chaseSpeed = 5.0f;
    [Tooltip("Ajuste vertical del monstruo sobre el NavMesh. Si flota, pon un valor negativo (ej. -0.1 o -0.15).")]
    public float navMeshBaseOffset = -1.30f;

    [Header("Parámetros de Patrulla")]
    public Transform[] patrolPoints;
    public float minIdleTime = 2f;
    public float maxIdleTime = 6f;

    [Header("Tiempos de Alerta y Observación")]
    [Tooltip("Tiempo que se queda quieto en alerta al escuchar un ruido antes de ir a investigar")]
    public float alertStareTime = 1.5f;
    [Tooltip("Tiempo máximo que se queda observando al jugador en la luz antes de retirarse")]
    public float maxObserveTime = 6f;
    [Tooltip("Duración de la búsqueda al llegar a la última posición conocida")]
    public float searchDuration = 3f;
 
    [Header("Sonido de la Criatura")]
    [Tooltip("Sonido de arrastre de garras/dedos en la oscuridad (CERCANO)")]
    public AudioClip dragFingersSound;
    [Tooltip("Sonido de arrastre de garras/dedos en la oscuridad (DISTANTE)")]
    public AudioClip dragFingersSoundShort;
 
    private AudioSource dragAudioSource;
    private AudioSource dragShortAudioSource;
    private AudioSource heartbeatAudio;

    [Header("Efectos de Silbido de la Muerte")]
    private AudioClip whistleClip;
    private AudioSource whistleAudioSource;
    private float nextWhistleTime = 0f;

    private NavMeshAgent agent;
    private PhenomenonAnimation anim;
    private FieldOfView fov;
    private PlayerSanity playerSanity;
    private PlayerHealth playerHealth;
    private SprintDetector playerSprintDetector;
    private RoomLightsManager roomLightsManager;
    private HideUnderBed hideScript;
    private Transform playerCamera;

    [Header("Estado Actual de la IA")]
    public PhenomenonState currentState = PhenomenonState.Patrol;

    private int currentPatrolIndex = -1;
    private bool isWaitingInPatrol = false;
    private Vector3 lastKnownPlayerPosition;
    private float stateTimer = 0f;
    private float observeTimer = 0f;
    private float timeSinceLastSeen = 0f;
    private bool isCurrentlyVisible = false;

    [Header("Eventos de Tensión (Caza de Pánico)")]
    [Tooltip("Tiempo de calma en segundos entre fases de pánico")]
    public float calmDuration = 180f;
    [Tooltip("Duración en segundos de la fase de pánico")]
    public float panicDuration = 120f; // Duración de cacería: 2 minutos (120 segundos)
    [Tooltip("¿Está el evento de pánico activo?")]
    public bool isPanicEventActive = false;

    private float panicTimer = 0f;
    private float panicWarpTimer = 0f;
    private float chaseLostTimer = 0f;
    private float timeSinceFarAway = 0f;
    private float pathCheckTimer = 0f;
    private float lightObserveCooldownTimer = 0f;
    private float shadowWarpCooldownTimer = 0f;
    private float phantomAudioTimer = 0f;
    private float nextPhantomAudioDelay = 18f; // Primer sonido a los 18 segundos
    private float scratchScareTimer = 0f;
    private float nextScratchScareDelay = 45f; // Primer evento a los 45 segundos
    private bool wasPlayerLookingLastFrame = false;
    private float antiStuckTimer = 0f;
    private float difficultySpeedMultiplier = 1.0f;
    private float activeWarpChance = 0.40f;
    private float activeLookDamageRate = 8.0f;
    private float grabAttackDamage = 50f;
    private float darknessInstantKillDamage = 50f;
    private float timeSinceLastVisualContact = 20f; // Inicializado en 20s para que la primera vez funcione
    private float spectralTimer = 0f;
    private bool isSpectrallyInvisible = false;

    [Header("Sonidos de Jumpscare (Sting)")]
    private AudioClip jumpscareStingBassClip;
    private AudioClip jumpscareStingNormalClip;
    private AudioClip jumpscareStingNormal2Clip;
    private AudioClip jumpscareStingStrongClip;
    private int lastJumpscareVariationIndex = -1;
    private float lastJumpscareStingTime = 0f;
    private Coroutine cameraGlanceCoroutine;
    private Vector3 initialChildLocalPosition;
    private Quaternion initialChildLocalRotation;
    private bool hasStoredInitialTransforms = false;

    // --- VARIABLES DE RENDIMIENTO CACHED ---
    private CharacterController cachedPlayerCC;
    private FlashlightController cachedPlayerFlashlight;
    private Renderer[] cachedRenderers;
    private Light[] cachedLights;
    private Animator cachedChildAnimator;
    private TunnelLightFlicker[] cachedTunnelLights;
    private float tunnelLightsCacheTimer = 0f;

    void Start()
    {
        // Conservar la escala del transform y las posiciones locales diseñadas en el prefab del Inspector.
        Animator childAnim = GetComponentInChildren<Animator>();
        if (childAnim != null)
        {
            cachedChildAnimator = childAnim;
            childAnim.applyRootMotion = false; // Desactivar Root Motion para evitar desvíos
            if (childAnim.transform != transform)
            {
                initialChildLocalPosition = childAnim.transform.localPosition;
                initialChildLocalRotation = childAnim.transform.localRotation;
                hasStoredInitialTransforms = true;
                Debug.Log($"[PhenomenonAIController] Posición inicial del hijo guardada: {initialChildLocalPosition}");
            }
        }

        // Cachar renderizadores y luces para optimización del Update
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedLights = GetComponentsInChildren<Light>(true);

        // Dejar que el SkinnedMeshRenderer y huesos mantengan su alineación por defecto del Prefab
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            smr.transform.localRotation = Quaternion.identity;
            Debug.Log("[PhenomenonAIController] Configuración inicial del prefab respetada.");
        }

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
        anim = GetComponent<PhenomenonAnimation>();
        if (anim == null) anim = GetComponentInChildren<PhenomenonAnimation>();
        fov = GetComponent<FieldOfView>();
        if (fov == null) fov = GetComponentInChildren<FieldOfView>();
        if (fov == null)
        {
            fov = gameObject.AddComponent<FieldOfView>();
            fov.viewRadius = 25f;
            fov.viewAngle = 120f;
            fov.eyeHeight = 1.8f;
            fov.hearingRadius = 4f;
            Debug.Log("[PhenomenonAIController] Componente FieldOfView no encontrado. Añadido y configurado automáticamente.");
        }

        // Resolver referencias del jugador
        ResolvePlayerReferences();

        // Buscar componentes del entorno
        roomLightsManager = FindObjectOfType<RoomLightsManager>();
        hideScript = FindObjectOfType<HideUnderBed>();

        if (agent != null)
        {
            agent.radius = 0.85f; // Radio aumentado para evitar colisión visual y traspaso de paredes
            agent.height = 2.6f;  // Altura real del monstruo para evitar que entre bajo techos bajos o inclinados
            // Respetar baseOffset del Inspector original del prefab
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.speed = patrolSpeed;
        }

        // Rigidbodies kinematic para evitar conflictos con NavMesh
        Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in childRbs)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Inicializar AudioSource 2D para el latido de corazón
        heartbeatAudio = gameObject.AddComponent<AudioSource>();
        heartbeatAudio.spatialBlend = 0.0f; // 2D (Stereo)
        heartbeatAudio.loop = true;
        heartbeatAudio.volume = 0f;
        heartbeatAudio.playOnAwake = false;
        heartbeatAudio.clip = Resources.Load<AudioClip>("Audio/Compartido/Latido");
 
        // Inicializar AudioSource 3D para el arrastre de garras (CERCANO)
        dragAudioSource = gameObject.AddComponent<AudioSource>();
        if (dragFingersSound == null)
        {
            dragFingersSound = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/DragFingersSound");
        }
        dragAudioSource.clip = dragFingersSound;
        dragAudioSource.loop = true;
        dragAudioSource.spatialBlend = 1.0f; // Sonido 3D
        dragAudioSource.minDistance = 2.0f;
        dragAudioSource.maxDistance = 12.0f; // Se apaga de lejos
        dragAudioSource.volume = 0f;
        dragAudioSource.playOnAwake = false;
 
        // Inicializar AudioSource 3D para el arrastre de garras (DISTANTE)
        dragShortAudioSource = gameObject.AddComponent<AudioSource>();
        if (dragFingersSoundShort == null)
        {
            dragFingersSoundShort = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/DragFingersSoundShort");
        }
        dragShortAudioSource.clip = dragFingersSoundShort;
        dragShortAudioSource.loop = true;
        dragShortAudioSource.spatialBlend = 1.0f; // Sonido 3D
        dragShortAudioSource.minDistance = 8.0f;
        dragShortAudioSource.maxDistance = 22.0f; // Rango más amplio
        dragShortAudioSource.volume = 0f;
        dragShortAudioSource.playOnAwake = false;
 
        // Crear un brillo rojo sutil en los ojos/cabeza del monstruo para que sea visible en la distancia oscura
        GameObject eyeGlow = new GameObject("Phenomenon_EyeGlow");
        eyeGlow.transform.SetParent(transform);
        eyeGlow.transform.localPosition = new Vector3(0f, 1.75f, 0.2f);
        Light glowLight = eyeGlow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.9f, 0.1f, 0.1f); // Rojo sangre siniestro
        glowLight.range = 2.5f;
        glowLight.intensity = 1.8f;
        glowLight.shadows = LightShadows.None;

        // Inicializar Silbido de la Muerte
        whistleClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/Silbido");
        if (whistleClip == null) whistleClip = Resources.Load<AudioClip>("Whistle");
        
        if (whistleClip != null)
        {
            whistleAudioSource = gameObject.AddComponent<AudioSource>();
            whistleAudioSource.spatialBlend = 1.0f; // Sonido 3D
            whistleAudioSource.minDistance = 3f;
            whistleAudioSource.maxDistance = 18f;
            whistleAudioSource.playOnAwake = false;
        }

        // Cargar sonidos de Jumpscare Sting (Bass + Variaciones)
        jumpscareStingBassClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/jumpscareStingBass");
        if (jumpscareStingBassClip == null) jumpscareStingBassClip = Resources.Load<AudioClip>("jumpscareStingBass");

        jumpscareStingNormalClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/jumpscareStingNormal");
        if (jumpscareStingNormalClip == null) jumpscareStingNormalClip = Resources.Load<AudioClip>("jumpscareStingNormal");

        jumpscareStingNormal2Clip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/jumpscareStingNormal2");
        if (jumpscareStingNormal2Clip == null) jumpscareStingNormal2Clip = Resources.Load<AudioClip>("jumpscareStingNormal2");

        jumpscareStingStrongClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/jumpscareStingStrong");
        if (jumpscareStingStrongClip == null) jumpscareStingStrongClip = Resources.Load<AudioClip>("jumpscareStingStrong");

        // Inicializar dificultad de PlayerPrefs
        string savedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
        if (savedDifficulty == "FACIL")
        {
            patrolSpeed = 2.2f;
            investigateSpeed = 1.8f;
            chaseSpeed = 4.0f;
            difficultySpeedMultiplier = 0.75f;
            activeWarpChance = 0.15f;
            activeLookDamageRate = 4.0f;
            nextScratchScareDelay = Random.Range(55f, 90f);
            grabAttackDamage = 50f;
            darknessInstantKillDamage = 50f;
        }
        else if (savedDifficulty == "DIFICIL")
        {
            patrolSpeed = 3.8f;
            investigateSpeed = 3.0f;
            chaseSpeed = 6.5f;
            difficultySpeedMultiplier = 1.25f;
            activeWarpChance = 0.55f;
            activeLookDamageRate = 12.0f;
            nextScratchScareDelay = Random.Range(20f, 40f);
            grabAttackDamage = 50f;
            darknessInstantKillDamage = 50f;
        }
        else // NORMAL
        {
            patrolSpeed = 3.0f;
            investigateSpeed = 2.4f;
            chaseSpeed = 5.0f;
            difficultySpeedMultiplier = 1.0f;
            activeWarpChance = 0.30f;
            activeLookDamageRate = 8.0f;
            nextScratchScareDelay = Random.Range(35f, 65f);
            grabAttackDamage = 50f;
            darknessInstantKillDamage = 50f;
        }

        // Iniciar patrulla y configurar el período de gracia inicial físicamente
        ChangeState(PhenomenonState.Patrol);

        if (graceActive)
        {
            transform.position = new Vector3(0f, -500f, 0f);
            if (agent != null) agent.enabled = false;
            SetMonsterVisible(false);
        }
    }

    private void OnEnable()
    {
        // Limpiar estados de invisibilidad residual
        isSpectrallyInvisible = false;
        isCurrentlyVisible = true;

        if (graceActive)
        {
            // Si el período de gracia está activo, mantener al monstruo oculto y el agente apagado
            SetMonsterVisible(false);
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            return;
        }

        // CRÍTICO: Resetear posición y re-vincular el Animator para resincronizar los huesos del modelo con la raíz
        Animator childAnim = cachedChildAnimator != null ? cachedChildAnimator : GetComponentInChildren<Animator>();
        if (childAnim != null)
        {
            childAnim.applyRootMotion = false;
            
            if (childAnim.transform != transform && hasStoredInitialTransforms)
            {
                childAnim.transform.localPosition = initialChildLocalPosition;
                childAnim.transform.localRotation = initialChildLocalRotation;
            }
            
            childAnim.Rebind();
            childAnim.Update(0f);
        }

        // Forzar renderers a encenderse al reactivarse tras el respawn (Usando cache)
        Renderer[] allRends = cachedRenderers != null ? cachedRenderers : GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in allRends)
        {
            if (r != null && r.gameObject != gameObject && !r.gameObject.name.Contains("Light"))
            {
                r.enabled = true;
            }
        }

        Light[] allLights = GetComponentsInChildren<Light>(true);
        foreach (Light l in allLights)
        {
            if (l != null) l.enabled = true;
        }

        // Asegurar que el NavMeshAgent esté listo
        NavMeshAgent avAgent = GetComponent<NavMeshAgent>();
        if (avAgent == null) avAgent = GetComponentInChildren<NavMeshAgent>();
        if (avAgent != null && avAgent.enabled)
        {
            if (avAgent.isOnNavMesh)
            {
                avAgent.speed = patrolSpeed;
                avAgent.ResetPath();
            }
        }

        Debug.Log("[PhenomenonAIController] OnEnable: Hijo visual alineado en Y = " + navMeshBaseOffset + ", Animator reseteado.");
    }

    /// <summary>
    /// Resetea la posición local del hijo visual (Animator/mesh) para que esté alineado
    /// con el NavMeshAgent raíz. Llamar después de cada Warp() para evitar desincronización.
    /// </summary>
    public void ResetVisualChildTransform()
    {
        Animator childAnim = GetComponentInChildren<Animator>();
        if (childAnim != null)
        {
            if (childAnim.transform != transform && hasStoredInitialTransforms)
            {
                childAnim.transform.localPosition = initialChildLocalPosition;
                childAnim.transform.localRotation = initialChildLocalRotation;
            }
            childAnim.applyRootMotion = false;
        }

        // --- ROTAR INSTANTÁNEAMENTE AL MONSTRUO HACIA EL JUGADOR TRAS EL WARP ---
        // Esto previene que tras teletransportarse aparezca dándole la espalda al jugador.
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0f;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    public bool IsPlayerInSafeZone()
    {
        // Confiar solo en el trigger event (OnTriggerEnter/Exit) de SafeZoneTrigger
        return SafeZoneTrigger.isPlayerSafe;
    }

    // --- PERÍODO DE GRACIA INICIAL ---
    // El monstruo está completamente inactivo y oculto durante este tiempo al inicio de partida.
    // Esto reemplaza el sistema de Zona Segura que causaba bugs.
    [Header("Período de Gracia Inicial")]
    public float gracePeriodDuration = 25f; // Segundos de calma al inicio (ajustable en Inspector)
    private float graceTimer = 0f;
    private bool graceActive = true;

    // Chase agresivo: timer para teletransportes frecuentes durante la cacería
    private float chaseWarpTimer = 0f;
    private float nextChaseWarpInterval = 3.5f; // Warp cada ~3.5s si no es visible

    private float lightCheckTimer = 0f;
    void Update()
    {
        if (player == null || agent == null) return;

        // ═══════════════════════════════════════════════
        // PERÍODO DE GRACIA INICIAL
        // El monstruo está oculto e inactivo al inicio.
        // Reemplaza el sistema de Zona Segura que daba bugs.
        // ═══════════════════════════════════════════════
        if (graceActive)
        {
            graceTimer += Time.deltaTime;

            // Ocultar el monstruo completamente durante la gracia
            SetMonsterVisible(false);
            if (agent != null) agent.enabled = false;

            if (graceTimer >= gracePeriodDuration)
            {
                graceActive = false;
                if (agent != null) agent.enabled = true;
                SetMonsterVisible(true);
                detectionRange = 40f;
                isPanicEventActive = true;
                ChangeState(PhenomenonState.Chase);
                ForceWarpNearPlayer(12f);
                Debug.Log("[PhenomenonAIController] ⚠️ ¡Período de gracia terminado! El Phenomenon ha despertado.");
            }
            return; // No ejecutar nada más mientras está en gracia
        }

        // ═══════════════════════════════════════════════
        // CONTROL DE DETECCIÓN Y CACERÍA DINÁMICA
        // ═══════════════════════════════════════════════
        // En calma el rango es de 20m para poder evadirlo; en cacería sube a 40m.
        detectionRange = isPanicEventActive ? 40f : 20f;

        // Distancia al jugador — necesaria aquí y más abajo
        float distToMonster = (player != null) ? Vector3.Distance(transform.position, player.position) : 999f;

        // Teletransportes agresivos (Slenderman) SOLO activos durante la fase de pánico (cacería/apagón)
        if (isPanicEventActive && currentState == PhenomenonState.Chase)
        {
            chaseWarpTimer += Time.deltaTime;
            bool playerCanSeeMonster = CheckIfPlayerIsLookingAtMonster() && distToMonster <= 30f;

            // 1. Warp inmediato Slenderman: El jugador estaba mirando y apartó la mirada.
            // Cooldown de 7.0s para evitar spam, y a distancia de 12.0m para dar aire.
            if (wasPlayerLookingLastFrame && !playerCanSeeMonster && chaseWarpTimer >= 7.0f)
            {
                chaseWarpTimer = 0f;
                nextChaseWarpInterval = Random.Range(9.0f, 14.0f);
                ForceWarpNearPlayer(12.0f); 
                Debug.Log("[PhenomenonAIController] 👁️ ¡Jugador apartó la mirada! Warp instantáneo al frente (12m).");
            }
            // 2. Warp periódico si el jugador sigue sin mirar al monstruo
            else if (!playerCanSeeMonster && chaseWarpTimer >= nextChaseWarpInterval)
            {
                chaseWarpTimer = 0f;
                nextChaseWarpInterval = Random.Range(9.0f, 14.0f);
                ForceWarpNearPlayer(12.0f);
                Debug.Log("[PhenomenonAIController] 👁️ Warp periódico en Chase (12m).");
            }
        }
        else
        {
            chaseWarpTimer = 0f;
        }

        // Anti-stuck para NavMesh
        antiStuckTimer += Time.deltaTime;
        if (antiStuckTimer >= 1.5f)
        {
            antiStuckTimer = 0f;
            if (agent != null && player != null)
            {
                bool isStuck = !agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid || (agent.pathStatus == NavMeshPathStatus.PathPartial && Vector3.Distance(transform.position, player.position) > 15f);
                if (isStuck)
                {
                    Debug.LogWarning("[PhenomenonAIController] 🚨 Monstruo atascado. Recuperando.");
                    RecoverToPlayerCorridor();
                }
            }
        }

        // Seguro anti-drift: Asegurar que el root motion del hijo visual NUNCA se active (Usando cache)
        if (cachedChildAnimator == null) cachedChildAnimator = GetComponentInChildren<Animator>();
        if (cachedChildAnimator != null && cachedChildAnimator.applyRootMotion)
        {
            cachedChildAnimator.applyRootMotion = false;
        }

        // Actualizar el temporizador y comportamiento del evento de pánico/tensión
        UpdatePanicEventSystem();

        // --- AMBIENTE DE CACERÍA DINÁMICO (TERROR ATMOSFÉRICO) ---
        if (!TunnelsPowerOutageManager.isGlobalPowerOutage)
        {
            Color targetAmbient = isPanicEventActive ? new Color(0.012f, 0.002f, 0.002f) : new Color(0.04f, 0.06f, 0.07f);
            Color targetFog = isPanicEventActive ? new Color(0.008f, 0.001f, 0.001f) : new Color(0.03f, 0.05f, 0.06f);
            float targetFogDensity = isPanicEventActive ? 0.032f : 0.016f;

            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, Time.deltaTime * 2.2f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 2.2f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 2.2f);
        }

        // Decrementar el cooldown de observación de luz
        if (lightObserveCooldownTimer > 0f)
        {
            lightObserveCooldownTimer -= Time.deltaTime;
        }

        // Decrementar el cooldown del salto de sombras
        if (shadowWarpCooldownTimer > 0f)
        {
            shadowWarpCooldownTimer -= Time.deltaTime;
        }

        // Actualizar temporizador de sonidos fantasma para asustar al jugador
        phantomAudioTimer += Time.deltaTime;
        if (phantomAudioTimer >= nextPhantomAudioDelay)
        {
            phantomAudioTimer = 0f;
            nextPhantomAudioDelay = Random.Range(16f, 28f);
            PlayPhantomSoundNearPlayer();
        }

        // Temporizador para el evento del sonido de rasguños acercándose en carrera
        scratchScareTimer += Time.deltaTime;
        if (scratchScareTimer >= nextScratchScareDelay)
        {
            scratchScareTimer = 0f;
            nextScratchScareDelay = Random.Range(35f, 65f);
            if (currentState != PhenomenonState.Attack && Time.timeScale > 0f)
            {
                StartCoroutine(RunScratchScareEventCoroutine());
            }
        }

        // Comprobar si el jugador está mirando cara a cara al monstruo (dentro de 40m)
        bool isPlayerLookingNow = (distToMonster <= 40f) && CheckIfPlayerIsLookingAtMonster();

        if (isPlayerLookingNow)
        {
            // Susto si el monstruo está a una distancia media/cercana (≤13m).
            // Esto incluye las distancias de teletransporte (que son a ~8.5m - 12m).
            if (distToMonster <= 13.0f && !wasPlayerLookingLastFrame)
            {
                TriggerVisualImpactSound();
            }
            timeSinceLastVisualContact = 0f;
        }
        else
        {
            timeSinceLastVisualContact += Time.deltaTime;
        }
        wasPlayerLookingLastFrame = isPlayerLookingNow;

        // Daño progresivo por pánico al mirar al Fenómeno muy de cerca (distancia de 2.2m a 6.0m)
        if (player != null && distToMonster <= 6.0f && isPlayerLookingNow && currentState != PhenomenonState.Attack)
        {
            if (playerHealth != null)
            {
                // Pierde vida por segundo según dificultad
                playerHealth.TakeDamage(activeLookDamageRate * Time.deltaTime);
            }
        }
 
        // Comprobar si el jugador está escondido
        bool isPlayerHidden = hideScript != null && hideScript.isHiding;
        if (isPlayerHidden && currentState != PhenomenonState.Patrol)
        {
            ChangeState(PhenomenonState.Patrol);
            return;
        }
 
        // Control periódico de luces cercanas (cada 0.15 segundos para optimizar rendimiento)
        lightCheckTimer += Time.deltaTime;
        if (lightCheckTimer >= 0.15f)
        {
            lightCheckTimer = 0f;
            ManageNearbyLights();
        }
 
        // Control dinámico de visibilidad, congelamiento por luz y sonido
        UpdateVisibilityAndMovement();
 
        // Ejecutar comportamiento de estado
        UpdateStateBehavior();
 
        // Control de alucinaciones espectrales
        if (isCurrentlyVisible)
        {
            timeSinceLastSeen = 0f;
        }
        else
        {
            timeSinceLastSeen += Time.deltaTime;
            if (timeSinceLastSeen >= 45f)
            {
                timeSinceLastSeen = 0f;
                TrySpawnSpectralHallucination();
            }
        }

        // Director de teletransportación por lejanía/inactividad
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            // Durante el apagón, el monstruo se mantendrá siempre cerca del jugador (Warp si se aleja más de 30m por 4 segundos)
            float maxAllowedDist = TunnelsPowerOutageManager.isGlobalPowerOutage ? 30f : 90f;
            float maxFarTime = TunnelsPowerOutageManager.isGlobalPowerOutage ? 4.0f : 25f;

            if (dist > maxAllowedDist)
            {
                timeSinceFarAway += Time.deltaTime;
                if (timeSinceFarAway >= maxFarTime)
                {
                    timeSinceFarAway = 0f;
                    TryTeleportNearPlayer();
                }
            }
            else
            {
                timeSinceFarAway = 0f;
            }
        }

        // Chequeo periódico (cada 0.5s) de rodeos largos para teletransporte sigiloso
        if (player != null && currentState != PhenomenonState.Attack)
        {
            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.5f)
            {
                pathCheckTimer = 0f;
                CheckLabyrinthRerouteTeleport();
            }
        }

        // Comprobar silbido de tensión cuando el monstruo está cerca
        if (whistleClip != null && whistleAudioSource != null && player != null)
        {
            float currentDist = Vector3.Distance(transform.position, player.position);
            if (currentDist <= 22f && Time.time >= nextWhistleTime && !whistleAudioSource.isPlaying && currentState != PhenomenonState.Attack)
            {
                whistleAudioSource.clip = whistleClip;
                whistleAudioSource.volume = 1.0f; // Volumen máximo silbante aterrador
                whistleAudioSource.Play();
                
                // Cooldown aleatorio entre 55 y 90 segundos después de que termine la canción silbada
                nextWhistleTime = Time.time + whistleClip.length + Random.Range(55f, 90f);
            }
        }
    }

    private void ResolvePlayerReferences()
    {
        GameObject scenePlayer = GameObject.FindGameObjectWithTag("Player");
        if (scenePlayer == null) scenePlayer = GameObject.Find("PlayerCapsule");
        if (scenePlayer == null) scenePlayer = GameObject.Find("Player");
        if (scenePlayer == null)
        {
            var fpc = FindObjectOfType<FirstPersonController>();
            if (fpc != null) scenePlayer = fpc.gameObject;
        }

        if (scenePlayer != null)
        {
            player = scenePlayer.transform;
            playerHealth = scenePlayer.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = scenePlayer.GetComponentInParent<PlayerHealth>();
            playerSanity = scenePlayer.GetComponent<PlayerSanity>();
            if (playerSanity == null) playerSanity = scenePlayer.GetComponentInParent<PlayerSanity>();
            playerSprintDetector = scenePlayer.GetComponent<SprintDetector>();
            if (playerSprintDetector == null) playerSprintDetector = scenePlayer.GetComponentInParent<SprintDetector>();
            
            // Cachar componentes pesados del jugador para optimizar FPS
            cachedPlayerCC = scenePlayer.GetComponent<CharacterController>();
            if (cachedPlayerCC == null) cachedPlayerCC = scenePlayer.GetComponentInParent<CharacterController>();
            cachedPlayerFlashlight = FindObjectOfType<FlashlightController>();

            // Resolver cámara del jugador
            playerCamera = Camera.main != null ? Camera.main.transform : player;

            if (fov != null) fov.player = player;
        }
    }

    private void ChangeState(PhenomenonState newState)
    {
        currentState = newState;
        stateTimer = 0f;
        observeTimer = 0f;

        if (agent == null || anim == null) return;

        switch (currentState)
        {
            case PhenomenonState.Patrol:
                SetAgentStopped(true);
                agent.speed = 0f;
                isWaitingInPatrol = false;
                anim.SetWalking(false);
                anim.SetAlert(false);
                anim.SetAttacking(false);
                if (player != null && Vector3.Distance(transform.position, player.position) < 20f)
                {
                    TeleportToDistantPatrolPoint();
                }
                break;

            case PhenomenonState.Alert:
                ResetAgentPath();
                SetAgentStopped(true);
                agent.speed = 0f;
                anim.SetWalking(false);
                anim.SetAlert(true);
                break;

            case PhenomenonState.Investigate:
                SetAgentStopped(true);
                agent.speed = 0f;
                anim.SetWalking(false);
                anim.SetAlert(true);
                break;

            case PhenomenonState.Chase:
                SetAgentStopped(true);
                agent.speed = 0f;
                anim.SetWalking(false);
                anim.SetAlert(true);
                break;

            case PhenomenonState.ObservingLight:
                ResetAgentPath();
                SetAgentStopped(true);
                agent.speed = 0f;
                anim.SetWalking(false);
                anim.SetAlert(true);
                break;

            case PhenomenonState.Attack:
                ResetAgentPath();
                SetAgentStopped(true);
                agent.speed = 0f;
                anim.SetWalking(false);
                anim.SetAttacking(true);
                StartCoroutine(PerformGrabAttack());
                break;
        }
    }

    private void UpdateStateBehavior()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSee = fov != null && fov.CanSeePlayer();
        bool isPlayerInLight = IsPlayerInLight();

        // 1. Escuchar sonidos: corriendo se escucha de lejos (55m), caminando normal se escucha a media distancia (20m) (Usando cache)
        bool playerHeard = false;
        if (cachedPlayerCC == null && player != null) cachedPlayerCC = player.GetComponent<CharacterController>();
        float playerSpeed = cachedPlayerCC != null ? cachedPlayerCC.velocity.magnitude : 0f;
        bool isPlayerRunning = playerSprintDetector != null && playerSprintDetector.IsRunning;

        if (isPlayerRunning && distanceToPlayer <= 55f)
        {
            playerHeard = true;
            lastKnownPlayerPosition = player.position;
        }
        else if (playerSpeed > 1.8f && distanceToPlayer <= 20f)
        {
            playerHeard = true;
            lastKnownPlayerPosition = player.position;
        }

        switch (currentState)
        {
            case PhenomenonState.Patrol:
                // Detección del jugador
                if (canSee)
                {
                    if (isPlayerInLight && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                    {
                        ChangeState(PhenomenonState.ObservingLight);
                    }
                    else
                    {
                        ChangeState(PhenomenonState.Chase);
                    }
                    return;
                }
                else if (playerHeard)
                {
                    ChangeState(PhenomenonState.Alert);
                    return;
                }

                // Lógica de movimiento en patrulla
                if (!isWaitingInPatrol)
                {
                    if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.8f)
                    {
                        StartCoroutine(PatrolWaitRoutine());
                    }
                }
                break;

            case PhenomenonState.Alert:
                // Mirar hacia la fuente del sonido lentamente
                FaceTarget(lastKnownPlayerPosition);

                stateTimer += Time.deltaTime;
                if (stateTimer >= alertStareTime)
                {
                    ChangeState(PhenomenonState.Investigate);
                }
                break;

            case PhenomenonState.Investigate:
                // Si ve al jugador mientras investiga
                if (canSee)
                {
                    if (isPlayerInLight && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                    {
                        ChangeState(PhenomenonState.ObservingLight);
                    }
                    else
                    {
                        ChangeState(PhenomenonState.Chase);
                    }
                    return;
                }

                // Si escucha un nuevo ruido, actualizar destino
                if (playerHeard)
                {
                    SetAgentDestination(lastKnownPlayerPosition);
                }

                // Al llegar a la posición del ruido
                if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 1f)
                {
                    SetAgentStopped(true);
                    anim.SetWalking(false);

                    stateTimer += Time.deltaTime;
                    if (stateTimer >= searchDuration)
                    {
                        ChangeState(PhenomenonState.Patrol);
                    }
                }
                break;

            case PhenomenonState.Chase:
                // Si el jugador entra a una zona iluminada (y no estamos en cooldown ni hay apagón)
                if (isPlayerInLight && lightObserveCooldownTimer <= 0f && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    ChangeState(PhenomenonState.ObservingLight);
                    return;
                }

                // Si no puede verlo ni escucharlo, aplicar tiempo de gracia antes de rendirse
                if (!canSee && !playerHeard)
                {
                    chaseLostTimer += Time.deltaTime;
                    if (chaseLostTimer >= 14.0f) // Aumentado de 4.0f a 14.0f para persistencia real de persecución
                    {
                        chaseLostTimer = 0f;
                        lastKnownPlayerPosition = player.position;
                        ChangeState(PhenomenonState.Investigate);
                        return;
                    }
                }
                else
                {
                    chaseLostTimer = 0f;
                }

                // Perseguir
                SetAgentDestination(player.position);

                // 1. Velocidad dinámica según si el jugador lo ve (Reverse Weeping Angel)
                // Usamos CheckIfPlayerIsLookingAtMonster() que implementa el Linecast correcto saltándose el cuerpo del jugador.
                bool isPlayerLooking = CheckIfPlayerIsLookingAtMonster();

                // 2. Chequear salto de sombras (Shadow-Warp) si el jugador corre de espaldas
                if (!isPlayerLooking && shadowWarpCooldownTimer <= 0f)
                {
                    TunnelLightFlicker nearLight = GetClosestLightToPosition(transform.position);
                    if (nearLight != null && !nearLight.isForcedOff && Vector3.Distance(transform.position, nearLight.transform.position) < 14f)
                    {
                        TryShadowWarpSkipLight();
                    }
                }

                // 3. Ataque por cercanía prioritaria
                float activeAttackRange = attackRange;
                if (TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    activeAttackRange = attackRange * 1.5f;
                }

                if (distanceToPlayer <= activeAttackRange)
                {
                    ChangeState(PhenomenonState.Attack);
                    return;
                }

                // 4. Mecánica de la Estatua: Se congela completamente si el jugador lo mira directamente (a más del rango de ataque)
                // Eliminamos la necesidad de la linterna (IsShinedByFlashlight()) para que funcione consistentemente tipo Slenderman
                if (isPlayerLooking && distanceToPlayer > activeAttackRange)
                {
                    SetAgentStopped(true);
                    agent.velocity = Vector3.zero;
                    agent.speed = 0f;
                    anim.SetWalking(false);
                    return;
                }
                else
                {
                    SetAgentStopped(false);
                }

                float baseSpeed = chaseSpeed;
                float walkAnimSpeed = 1.4f;

                if (isPanicEventActive)
                {
                    // Cacería: Velocidad de acecho (10.5 m/s)
                    baseSpeed = 10.5f;
                    walkAnimSpeed = 3.2f;
                    if (dragAudioSource != null) dragAudioSource.volume = 1.0f;
                }
                else
                {
                    // Modo normal: Carrera rápida (5.5 m/s)
                    baseSpeed = 5.5f;
                    walkAnimSpeed = 2.0f;
                    if (dragAudioSource != null) dragAudioSource.volume = 0.9f;
                }

                // Mecánica de "Repulsión por Luz": Ralentiza su velocidad cuando se acerca a un foco de luz encendido (solo fuera de cacería)
                float speedMultiplier = 1.0f;
                if (!isPanicEventActive)
                {
                    TunnelLightFlicker closestLightToMonster = GetClosestLightToPosition(transform.position);
                    if (closestLightToMonster != null && !closestLightToMonster.isForcedOff)
                    {
                        float distToLight = Vector3.Distance(transform.position, closestLightToMonster.transform.position);
                        if (distToLight < 16f)
                        {
                            speedMultiplier = Mathf.Lerp(0.5f, 1.0f, distToLight / 16f);
                        }
                    }
                }

                agent.speed = baseSpeed * speedMultiplier * difficultySpeedMultiplier;
                anim.SetWalkSpeed(walkAnimSpeed * speedMultiplier);
                break;

            case PhenomenonState.ObservingLight:
                // Mirar al jugador fijamente
                FaceTarget(player.position);

                // Ataque por cercanía extrema (incluso si está observado bajo la luz)
                if (distanceToPlayer <= attackRange)
                {
                    ChangeState(PhenomenonState.Attack);
                    return;
                }

                // Si el jugador SALE de la luz (o apaga la linterna/mira a otro lado), reanudar persecución
                if (!isPlayerInLight)
                {
                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist <= detectionRange * 1.5f)
                    {
                        lightObserveCooldownTimer = 5f; // Cooldown corto para evitar parpadeos rápidos de estado
                        ChangeState(PhenomenonState.Chase);
                        return;
                    }
                }

                // Si el jugador permanece en la luz, se retira lentamente
                observeTimer += Time.deltaTime;
                
                // 1. A los 2.2 segundos, forzar parpadeo de pánico (falla de luz)
                if (observeTimer >= 2.2f && observeTimer < 4.5f)
                {
                    TunnelLightFlicker closestLight = GetClosestLightToPlayer();
                    if (closestLight != null)
                    {
                        closestLight.isPanicFlickering = true;
                    }
                }
                // 2. A los 4.5 segundos, apagar la luz del todo y perseguir
                else if (observeTimer >= 4.5f)
                {
                    TunnelLightFlicker closestLight = GetClosestLightToPlayer();
                    if (closestLight != null)
                    {
                        closestLight.isForcedOff = true;
                    }
                    lightObserveCooldownTimer = 20f; // Cooldown largo para evitar bucles
                    ChangeState(PhenomenonState.Chase);
                    return;
                }
                break;

            case PhenomenonState.Attack:
                // Esperar a que la corrutina termine de matar al jugador
                break;
        }
    }

    /// <summary>
    /// Muestra u oculta completamente el monstruo (renderers + luces).
    /// Usado durante el período de gracia inicial.
    /// </summary>
    private void SetMonsterVisible(bool visible)
    {
        Renderer[] rends = cachedRenderers != null ? cachedRenderers : GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in rends)
        {
            if (r != null) r.enabled = visible;
        }
        Light[] lights = cachedLights != null ? cachedLights : GetComponentsInChildren<Light>(true);
        foreach (Light l in lights)
        {
            if (l != null) l.enabled = visible;
        }
        // También silenciar audios mientras está en gracia
        if (!visible)
        {
            if (heartbeatAudio != null && heartbeatAudio.isPlaying) heartbeatAudio.Stop();
            if (dragAudioSource != null && dragAudioSource.isPlaying) dragAudioSource.Stop();
            if (dragShortAudioSource != null && dragShortAudioSource.isPlaying) dragShortAudioSource.Stop();
        }
    }

    private bool IsPositionInNarrowSpace(Vector3 pos)
    {
        float maxMeasure = 5.0f;
        float limitWidth = 2.0f; // Ancho mínimo del pasillo para permitir spawn
        int layerMask = Physics.DefaultRaycastLayers;

        float distN = GetDistanceToWall(pos + Vector3.up * 1f, Vector3.forward, maxMeasure, layerMask);
        float distS = GetDistanceToWall(pos + Vector3.up * 1f, Vector3.back, maxMeasure, layerMask);
        float distE = GetDistanceToWall(pos + Vector3.up * 1f, Vector3.right, maxMeasure, layerMask);
        float distW = GetDistanceToWall(pos + Vector3.up * 1f, Vector3.left, maxMeasure, layerMask);

        float widthNS = distN + distS;
        float widthEW = distE + distW;

        if (widthNS < limitWidth || widthEW < limitWidth)
        {
            return true;
        }
        return false;
    }

    private float GetDistanceToWall(Vector3 origin, Vector3 direction, float maxDistance, int layerMask)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (player != null && hit.transform.root == player.transform.root)
            {
                return maxDistance;
            }
            if (hit.transform.root == transform.root)
            {
                return maxDistance;
            }
            return hit.distance;
        }
        return maxDistance;
    }

    private bool IsPlayerInLight()
    {
        // Nunca protege si hay apagón global
        if (TunnelsPowerOutageManager.isGlobalPowerOutage) return false;

        // Protege solo si el jugador está a 5m o menos de una lámpara de pasillo activa (TunnelLightFlicker)
        // Las luces de ambiente, generadores y subgeneradores NO cuentan.
        TunnelLightFlicker closest = GetClosestLightToPlayer();
        if (closest != null && !closest.isForcedOff)
        {
            float dist = Vector3.Distance(player.position, closest.transform.position);
            if (dist <= 5.0f)
            {
                return true;
            }
        }
        return false;
    }

    private void MoveToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (!agent.isOnNavMesh) return;

        if (patrolPoints.Length == 1)
        {
            SetAgentDestination(patrolPoints[0].position);
            return;
        }

        // Con una probabilidad del 65%, patrullar en las cercanías del jugador para acecharlo constantemente
        System.Collections.Generic.List<int> stalkIndices = new System.Collections.Generic.List<int>();
        if (player != null && Random.value < 0.65f)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                if (SafeZoneTrigger.IsPositionInSafeZone(patrolPoints[i].position)) continue;

                float distToPlayer = Vector3.Distance(player.position, patrolPoints[i].position);
                float distToMonster = Vector3.Distance(transform.position, patrolPoints[i].position);
                
                // Buscar puntos de patrulla cerca del jugador (30m a 75m)
                // y no demasiado lejos del monstruo (menos de 85m) para poder llegar caminando
                if (distToPlayer >= 30f && distToPlayer <= 75f && distToMonster <= 85f)
                {
                    stalkIndices.Add(i);
                }
            }
        }

        int nextIndex = -1;
        if (stalkIndices.Count > 0)
        {
            nextIndex = stalkIndices[Random.Range(0, stalkIndices.Count)];
        }
        else
        {
            // Filtrar puntos de patrulla cercanos (ej. dentro de 65 metros para mantener la ronda localizada)
            System.Collections.Generic.List<int> nearIndices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                if (SafeZoneTrigger.IsPositionInSafeZone(patrolPoints[i].position)) continue;

                float dist = Vector3.Distance(transform.position, patrolPoints[i].position);
                // Evitar elegir el mismo punto en el que ya está parado
                if (dist > 3f && dist <= 65f)
                {
                    nearIndices.Add(i);
                }
            }

            // Si no hay puntos cercanos, buscar en un radio más amplio (ej. 130 metros)
            if (nearIndices.Count == 0)
            {
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    if (patrolPoints[i] == null) continue;
                    if (SafeZoneTrigger.IsPositionInSafeZone(patrolPoints[i].position)) continue;

                    float dist = Vector3.Distance(transform.position, patrolPoints[i].position);
                    if (dist > 3f && dist <= 130f)
                    {
                        nearIndices.Add(i);
                    }
                }
            }

            if (nearIndices.Count > 0)
            {
                nextIndex = nearIndices[Random.Range(0, nearIndices.Count)];
            }
            else
            {
                // Fallback total: elegir cualquier punto aleatorio diferente del actual
                int safety = 0;
                nextIndex = currentPatrolIndex;
                while (nextIndex == currentPatrolIndex && safety < 20)
                {
                    nextIndex = Random.Range(0, patrolPoints.Length);
                    safety++;
                }
            }
        }

        currentPatrolIndex = nextIndex;
        
        // Ajustar destino en NavMesh
        NavMeshHit hit;
        Vector3 dest = patrolPoints[currentPatrolIndex].position;
        Vector3 finalDest = NavMesh.SamplePosition(dest, out hit, 8f, NavMesh.AllAreas) ? hit.position : dest;
        SetAgentDestination(finalDest);
    }

    private IEnumerator PatrolWaitRoutine()
    {
        isWaitingInPatrol = true;
        SetAgentStopped(true);
        anim.SetWalking(false);

        float waitTime = Random.Range(minIdleTime, maxIdleTime);
        yield return new WaitForSeconds(waitTime);

        if (currentState == PhenomenonState.Patrol)
        {
            isWaitingInPatrol = false;
            SetAgentStopped(false);
            anim.SetWalking(true);
            MoveToNextPatrolPoint();
        }
    }

    private IEnumerator PerformGrabAttack()
    {
        Debug.Log("El Fenómeno ha atrapado al jugador.");

        // Forzar visibilidad al 100% de la malla y luces al atacar (evitar invisibilidad residual por respawn/apagón)
        isSpectrallyInvisible = false;
        isCurrentlyVisible = true;
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in rends)
        {
            if (r.gameObject != gameObject && !r.gameObject.name.Contains("Light")) r.enabled = true;
        }
        Light[] lghts = GetComponentsInChildren<Light>(true);
        foreach (Light l in lghts) l.enabled = true;

        // Jumpscare: Rotar la cámara del jugador para forzarlo a mirar al monstruo de frente
        if (player != null)
        {
            // Detener el movimiento del FirstPersonController si es posible
            var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
            if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
            if (fpc != null)
            {
                // Desactivar temporalmente el control para que no pueda mirar a otro lado
                fpc.enabled = false;
            }

            // Rotar cuerpo del jugador hacia el monstruo
            Vector3 dirToMonster = (transform.position - player.position).normalized;
            dirToMonster.y = 0f;
            if (dirToMonster != Vector3.zero)
            {
                player.rotation = Quaternion.LookRotation(dirToMonster);
            }

            // Rotar cámara del jugador hacia el rostro del monstruo de forma precisa y auto-calibrada
            if (playerCamera != null)
            {
                // Buscar hueso de la cabeza/cuello del monstruo para enfocar su cara
                Transform headBone = null;
                Transform[] allBones = GetComponentsInChildren<Transform>();
                foreach (Transform bone in allBones)
                {
                    if (bone != null)
                    {
                        string nameLower = bone.name.ToLower();
                        if (nameLower.Contains("head") || nameLower.Contains("neck") || nameLower.Contains("cabeza"))
                        {
                            headBone = bone;
                            break;
                        }
                    }
                }

                // Forzar que la altura apunte siempre a la cabeza (Y = ~2.3m sobre el suelo del monstruo)
                Vector3 targetFacePos = transform.position + Vector3.up * 2.3f;
                
                // Si el hueso de la cabeza es válido y está a una altura lógica superior, usarlo
                if (headBone != null && headBone.position.y > transform.position.y + 1.5f)
                {
                    targetFacePos = headBone.position;
                }

                Vector3 dirToFace = (targetFacePos - playerCamera.position).normalized;
                if (dirToFace != Vector3.zero)
                {
                    playerCamera.rotation = Quaternion.LookRotation(dirToFace);
                }
            }

            // Si la linterna estaba apagada, forzar encendido parpadeante para revelar al monstruo
            FlashlightController fl = FindObjectOfType<FlashlightController>();
            if (fl != null && fl.flashlightLight != null && !fl.flashlightLight.enabled)
            {
                fl.flashlightLight.enabled = true;
                fl.isGlitchedByMonster = true;
            }

            // Sonido de susto/ataque (Bass constante + variación de Jumpscare Sting)
            PlayJumpscareSting(1.0f);
            TriggerJumpscareCameraGlance();

            AudioClip sClip = Resources.Load<AudioClip>("Audio/Compartido/Susurros");
            if (sClip != null) AudioSource.PlayClipAtPoint(sClip, player.position, 1.0f);
            
            AudioClip aClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
            if (aClip == null) aClip = Resources.Load<AudioClip>("Apagon");
            if (aClip != null) AudioSource.PlayClipAtPoint(aClip, player.position, 1.0f);
        }

        yield return new WaitForSeconds(0.6f);
 
        if (playerHealth != null)
        {
            // Comprobar si el jugador está en la oscuridad completa (linterna apagada y sin luz de techo)
            FlashlightController fl = FindObjectOfType<FlashlightController>();
            bool isFlashlightOn = fl != null && fl.flashlightLight != null && fl.flashlightLight.enabled;
            bool isPlayerInLitArea = playerSanity != null && playerSanity.IsInLight();
 
            // Reactivar el FirstPersonController si sobrevivió (para que pueda moverse tras recibir daño)
            var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
            if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
            if (fpc != null)
            {
                fpc.enabled = true;
            }

            if (!isFlashlightOn && !isPlayerInLitArea)
            {
                // Daño en la oscuridad
                playerHealth.TakeDamage(darknessInstantKillDamage);
                Debug.Log($"El Fenómeno ha atacado al jugador en la oscuridad. Daño: {darknessInstantKillDamage}");
            }
            else
            {
                // Daño si el jugador lo repele con luz
                playerHealth.TakeDamage(grabAttackDamage);
                Debug.Log($"El Fenómeno ha herido al jugador en la luz. Daño: {grabAttackDamage}");
 
                // Retroceso: Empujar al jugador un poco hacia atrás (solo si el CC está activo, es decir el jugador está vivo)
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.enabled)
                {
                    Vector3 pushDir = (player.position - transform.position).normalized;
                    pushDir.y = 0f;
                    cc.Move(pushDir * 3f);
                }
 
                // Si sobrevivió y la linterna fue forzada a encenderse, devolver el control del glitch
                if (fl != null)
                {
                    fl.isGlitchedByMonster = false;
                }

                // Retirada temporal a patrulla
                ChangeState(PhenomenonState.Patrol);
            }
        }
    }
 
    private void UpdateVisibilityAndMovement()
    {
        bool isMonsterInCeilingLight = IsMonsterInLight();
        bool isShinedDirectly = IsShinedByFlashlight();
        bool isIlluminated = isMonsterInCeilingLight || isShinedDirectly;

        // Comprobar si hay alguna luz activa en la escena para no estar en oscuridad total (Usando cache)
        if (cachedPlayerFlashlight == null) cachedPlayerFlashlight = FindObjectOfType<FlashlightController>();
        bool isFlashlightOn = cachedPlayerFlashlight != null && cachedPlayerFlashlight.flashlightLight != null && cachedPlayerFlashlight.flashlightLight.enabled;
        bool isCompletelyDark = !isFlashlightOn && !isMonsterInCeilingLight;

        // Visibilidad progresiva (original): se hace visible si está iluminado directamente,
        // o si está cerca (dentro de 12 metros) y no hay oscuridad total (ej. la linterna rebota o hay luz ambiental).
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool shouldBeVisible = isIlluminated || (distToPlayer <= 12f && !isCompletelyDark);

        // --- EFECTO ESPECTRAL INVISIBLE ---
        if (isPanicEventActive)
        {
            spectralTimer += Time.deltaTime;
            if (isSpectrallyInvisible)
            {
                // Si está invisible, reaparece después de 3.5 segundos o si se acerca demasiado (menos de 6.5 metros)
                if (spectralTimer >= 3.5f || distToPlayer < 6.5f)
                {
                    isSpectrallyInvisible = false;
                    spectralTimer = 0f;
                    // Producir un sonido de glitch/aparición si está disponible
                    AudioSource audio = GetComponent<AudioSource>();
                    if (audio != null)
                    {
                        AudioClip glitchSound = Resources.Load<AudioClip>("Audio/Compartido/Linterna_Click");
                        if (glitchSound != null)
                        {
                            audio.PlayOneShot(glitchSound, 0.4f);
                        }
                    }
                }
            }
            else
            {
                // Si está visible, se hace invisible después de 5.5 segundos (pero solo si el jugador no está extremadamente cerca)
                if (spectralTimer >= 5.5f && distToPlayer >= 8.0f)
                {
                    isSpectrallyInvisible = true;
                    spectralTimer = 0f;
                }
            }
        }
        else
        {
            isSpectrallyInvisible = false;
            spectralTimer = 0f;
        }

        if (isSpectrallyInvisible)
        {
            shouldBeVisible = false;
        }
        // ----------------------------------

        // EXCEPCIÓN DE GRACIA: Durante el período de gracia inicial o del respawn (detectionRange == 0),
        // mantenemos los renderers encendidos al 100% para evitar bugs de oclusión visual en el frame de reinicio.
        if (detectionRange <= 0.1f)
        {
            shouldBeVisible = true;
        }

        isCurrentlyVisible = shouldBeVisible;

        // 1. Visibilidad dinámica: desactivada en oscuridad total, activada al iluminarse (Usando cache)
        Renderer[] renderers = cachedRenderers != null ? cachedRenderers : GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null && r.gameObject != gameObject && !r.gameObject.name.Contains("Light"))
            {
                r.enabled = shouldBeVisible;
            }
        }

        // Desactivar/activar fuentes de luz (como la luz roja de la cabeza) según la visibilidad para evitar halos fantasma (Usando cache)
        Light[] childLights = cachedLights != null ? cachedLights : GetComponentsInChildren<Light>(true);
        foreach (Light l in childLights)
        {
            if (l != null)
            {
                l.enabled = shouldBeVisible;
            }
        }

        // --- CONTROL DE LATIDO DE CORAZÓN DINÁMICO (TERROR) ---
        if (heartbeatAudio != null && heartbeatAudio.clip != null)
        {
            if (isPanicEventActive || distToPlayer <= 25f)
            {
                if (!heartbeatAudio.isPlaying) heartbeatAudio.Play();

                // Calcular ratio (0 en 25m, 1 en 3m)
                float ratio = 0f;
                if (distToPlayer <= 25f)
                {
                    ratio = Mathf.Clamp01((25f - distToPlayer) / (25f - 3f));
                }
                
                // Si el evento de pánico está activo, el volumen base mínimo es 0.35f
                float minVolume = isPanicEventActive ? 0.35f : 0f;
                heartbeatAudio.volume = Mathf.Max(minVolume, ratio * 0.85f);
                heartbeatAudio.pitch = 1.0f + ratio * 0.75f;
            }
            else
            {
                // Desvanecer el sonido si se aleja y no hay pánico activo
                if (heartbeatAudio.isPlaying)
                {
                    heartbeatAudio.volume = Mathf.MoveTowards(heartbeatAudio.volume, 0f, Time.deltaTime * 0.5f);
                    if (heartbeatAudio.volume <= 0.01f) heartbeatAudio.Stop();
                }
            }
        }

        // --- CONTROL DE INTERFERENCIA EN LA LINTERNA DEL JUGADOR ---
        if (cachedPlayerFlashlight == null) cachedPlayerFlashlight = FindObjectOfType<FlashlightController>();
        if (cachedPlayerFlashlight != null)
        {
            cachedPlayerFlashlight.isGlitchedByMonster = (distToPlayer <= 12f);
        }
 
        // 2. Control de sonido de arrastre de garras en la oscuridad con mezcla dinámica (Crossfade)
        bool isMoving = agent != null && agent.isOnNavMesh && !agent.isStopped && agent.velocity.magnitude > 0.1f;
        if (isMoving && !isIlluminated)
        {
            if (dragAudioSource != null && !dragAudioSource.isPlaying) dragAudioSource.Play();
            if (dragShortAudioSource != null && !dragShortAudioSource.isPlaying) dragShortAudioSource.Play();
 
            // Calcular distancias y ponderar volúmenes
            float dist = Vector3.Distance(transform.position, player.position);
 
            // Sonido corto/lejano: fuerte entre 8 y 20m, decae al acercarse mucho (deja espacio al de cerca)
            float shortVolume = Mathf.Clamp01((dist - 4f) / 10f); // 0 a 4m, sube a 1 a partir de 14m
            // Sonido largo/cercano: fuerte por debajo de 8m, decae por encima de 14m
            float closeVolume = Mathf.Clamp01((14f - dist) / 8f); // 0 a 14m o más, sube a 1 por debajo de 6m
 
            if (dragAudioSource != null) dragAudioSource.volume = closeVolume * 0.9f;
            if (dragShortAudioSource != null) dragShortAudioSource.volume = shortVolume * 0.8f;
        }
        else
        {
            if (dragAudioSource != null && dragAudioSource.isPlaying) dragAudioSource.Stop();
            if (dragShortAudioSource != null && dragShortAudioSource.isPlaying) dragShortAudioSource.Stop();
        }
 
        // 3. Forzar detención física inmediata si es iluminado (Boceto - Solo fuera de cacería/pánico)
        if (isIlluminated)
        {
            if (!isPanicEventActive)
            {
                SetAgentStopped(true);
                if (currentState != PhenomenonState.ObservingLight && currentState != PhenomenonState.Attack)
                {
                    ChangeState(PhenomenonState.ObservingLight);
                }
            }
            else
            {
                // Durante la cacería no se congela por luz (la velocidad se calcula dinámicamente en Chase)
            }
        }
        else
        {
            // Reanudar si vuelve a la oscuridad
            if (currentState == PhenomenonState.ObservingLight)
            {
                if (distToPlayer <= detectionRange * 1.5f)
                {
                    ChangeState(PhenomenonState.Chase);
                }
                else
                {
                    ChangeState(PhenomenonState.Patrol);
                }
            }
        }
    }
 
    private bool IsMonsterInLight()
    {
        if (roomLightsManager != null && roomLightsManager.powerOutage) return false;
 
        Light[] lights = FindObjectsOfType<Light>();
        foreach (Light l in lights)
        {
            if (l != null && l.enabled && l.type != LightType.Directional && 
                l.gameObject.name != "Player_Flashlight" && !l.gameObject.name.Contains("Fill"))
            {
                float dist = Vector3.Distance(transform.position, l.transform.position);
                if (dist <= l.range) return true;
            }
        }
        return false;
    }
 
    private bool IsShinedByFlashlight()
    {
        FlashlightController fl = FindObjectOfType<FlashlightController>();
        if (fl == null || fl.flashlightLight == null || !fl.flashlightLight.enabled) return false;
        
        // Si la linterna está parpadeando con muy baja intensidad (glitch), no congela al monstruo
        if (fl.flashlightLight.intensity < 2.0f) return false;

        Camera mainCam = Camera.main;
        if (mainCam == null) return false;

        float dist = Vector3.Distance(mainCam.transform.position, transform.position);
        if (dist > fl.flashlightLight.range) return false;

        // Foco de linterna (spotAngle = 70, semi-ángulo = 35)
        Vector3 dirToMonster = (transform.position + Vector3.up * 1f - mainCam.transform.position).normalized;
        float angle = Vector3.Angle(mainCam.transform.forward, dirToMonster);
        if (angle > (fl.flashlightLight.spotAngle / 2f)) return false;

        // Línea de visión directa
        RaycastHit hit;
        // Empezar el rayo 0.5m por delante de la cámara para evitar colisionar con la propia cápsula del jugador
        Vector3 start = mainCam.transform.position + mainCam.transform.forward * 0.5f;
        Vector3 end = transform.position + Vector3.up * 1.2f;
        if (Physics.Linecast(start, end, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != transform.root)
            {
                // Solo bloquear si impacta contra un obstáculo estructural real (muro, suelo, techo, columnas)
                // Esto ignora barandillas delgadas, tuberías decorativas u otros objetos que no bloquean la luz físicamente
                string hitName = hit.transform.name.ToLower();
                bool isSolidObstacle = hitName.Contains("wall") || 
                                       hitName.Contains("floor") || 
                                       hitName.Contains("ceiling") || 
                                       hitName.Contains("column") || 
                                       hitName.Contains("pillar") || 
                                       hitName.Contains("solid");
                if (isSolidObstacle)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
 
    private void SetAgentStopped(bool stop)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = stop;
        }
    }
 
    private void ResetAgentPath()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }
 
    private void SetAgentDestination(Vector3 destination)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(destination);
        }
    }
 
    private void ManageNearbyLights()
    {
        if (player == null) return;
 
        TunnelLightFlicker[] lights = FindObjectsOfType<TunnelLightFlicker>();
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
 
        foreach (TunnelLightFlicker l in lights)
        {
            if (l == null) continue;
 
            float distToMonster = Vector3.Distance(transform.position, l.transform.position);
            float distToPlayer = Vector3.Distance(player.position, l.transform.position);
 
            // 1. Aura de Oscuridad del Monstruo: Apaga las luces por donde camina (radio de 22 metros)
            // Esto cubre el tamaño de una celda de túnel (30m) para evitar que se congele a la mitad
            if (distToMonster <= 22f)
            {
                l.isForcedOff = true;
                l.isPanicFlickering = false;
            }
            else
            {
                l.isForcedOff = false;
 
                // 2. Parpadeo de Pánico: Durante el evento de pánico (si está cerca del jugador a menos de 30m)
                // o de forma estándar si el monstruo está cerca del jugador (32m) y la luz cerca del jugador (22m)
                if (isPanicEventActive && distToPlayer <= 30f)
                {
                    l.isPanicFlickering = true;
                }
                else if (distanceToPlayer <= 32f && distToPlayer <= 22f)
                {
                    l.isPanicFlickering = true;
                }
                else
                {
                    l.isPanicFlickering = false;
                }
            }
        }
    }
 
    private void TrySpawnSpectralHallucination()
    {
        if (player == null) return;
 
        // Buscar focos de luz cenital en el laberinto
        TunnelLightFlicker[] lights = FindObjectsOfType<TunnelLightFlicker>();
        System.Collections.Generic.List<TunnelLightFlicker> candidateLights = new System.Collections.Generic.List<TunnelLightFlicker>();
 
        foreach (TunnelLightFlicker l in lights)
        {
            if (l == null) continue;
            float dist = Vector3.Distance(player.position, l.transform.position);
            // Queremos que esté a una distancia media (entre 12 y 24 metros),
            // lo suficientemente cerca para verlo por el rabillo del ojo, pero no en su misma celda.
            if (dist >= 12f && dist <= 24f)
            {
                candidateLights.Add(l);
            }
        }
 
        if (candidateLights.Count == 0) return;
 
        // Seleccionar una luz aleatoria
        TunnelLightFlicker targetLight = candidateLights[Random.Range(0, candidateLights.Count)];
 
        // Posición de la alucinación: justo al borde del cono de luz, a oscuras
        // Hacemos un pequeño desplazamiento aleatorio en X/Z (ej. 4m)
        Vector3 spawnPos = targetLight.transform.position;
        Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 4.0f;
        spawnPos += offset;
        spawnPos.y = transform.position.y; // Mantener la misma altura que el monstruo real
 
        // Instanciar duplicado a partir de nuestro propio GameObject
        GameObject ghostObj = Instantiate(gameObject, spawnPos, Quaternion.identity);
        ghostObj.name = "ThePhenomenon_Hallucination";
        ghostObj.transform.localScale = transform.localScale;
 
        // Limpiar componentes de IA y movimiento para que quede estático
        PhenomenonAIController oldController = ghostObj.GetComponent<PhenomenonAIController>();
        if (oldController != null) DestroyImmediate(oldController);
 
        NavMeshAgent oldAgent = ghostObj.GetComponent<NavMeshAgent>();
        if (oldAgent != null) DestroyImmediate(oldAgent);
 
        // Añadir el script de comportamiento de alucinación
        ghostObj.AddComponent<HallucinationGhost>();
 
        // Rotar para mirar hacia el jugador
        Vector3 dirToPlayer = (player.position - spawnPos).normalized;
        dirToPlayer.y = 0f;
        if (dirToPlayer != Vector3.zero)
        {
            ghostObj.transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }
 
        Debug.Log("[PhenomenonAIController] Aparición espectral (alucinación) creada cerca del jugador.");
    }

    private TunnelLightFlicker GetClosestLightToPlayer()
    {
        if (player == null) return null;
        return GetClosestLightToPosition(player.position);
    }

    private TunnelLightFlicker GetClosestLightToPosition(Vector3 pos)
    {
        TunnelLightFlicker closestLight = null;
        float minD = float.MaxValue;
        foreach (var l in FindObjectsOfType<TunnelLightFlicker>())
        {
            if (l == null) continue;
            float d = Vector3.Distance(pos, l.transform.position);
            if (d < minD)
            {
                minD = d;
                closestLight = l;
            }
        }
        return closestLight;
    }

    private void TryTeleportNearPlayer()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        // Buscar celdas de patrulla cerca del jugador (distancia normal o reducida si hay apagón)
        System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
        
        float minDist = TunnelsPowerOutageManager.isGlobalPowerOutage ? 10f : 13f;
        float maxDist = TunnelsPowerOutageManager.isGlobalPowerOutage ? 16f : 25f;

        // Override de seguridad si el jugador está en la luz
        bool inLight = IsPlayerInLight();
        if (inLight)
        {
            minDist = 22f; // Forzar lejanía para respetar la zona segura de luz
            maxDist = 35f;
        }

        // Override de seguridad si el jugador está en la zona de la escotilla de escape
        bool isNearHatch = (TunnelsGenerator.escapeState == TunnelsGenerator.EscapeState.Ready) 
            || (Vector3.Distance(player.position, TunnelsGenerator.worldExitPointPos) < 25f);
        if (isNearHatch)
        {
            minDist = Mathf.Max(minDist, 18f); // Forzar lejanía para no aparecer encima del jugador en la escotilla
            maxDist = Mathf.Max(maxDist, 32f);
        }

        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            float distToPlayer = Vector3.Distance(player.position, p.position);
            
            // Si está en la escotilla, asegurarse de que el spawn no esté cerca de la trampilla física tampoco
            if (isNearHatch)
            {
                float distToHatch = Vector3.Distance(TunnelsGenerator.worldExitPointPos, p.position);
                if (distToHatch < 15f) continue; // Descartar celdas adyacentes a la escotilla
            }

            if (distToPlayer >= minDist && distToPlayer <= maxDist)
            {
                    if (IsPositionInNarrowSpace(p.position))
                {
                    continue; // Evitar pasillos muy reducidos
                }
                // Asegurarse de que el spawn point en sí mismo NO esté iluminado
                TunnelLightFlicker lightAtPoint = GetClosestLightToPosition(p.position);
                if (lightAtPoint != null && !lightAtPoint.isForcedOff && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    if (Vector3.Distance(p.position, lightAtPoint.transform.position) <= 6.0f)
                    {
                        continue; // Descartar punto iluminado
                    }
                }

                if (IsPositionHiddenFromPlayer(p.position))
                {
                    candidates.Add(p.position);
                }
            }
        }

        Vector3 targetPos = Vector3.zero;
        if (candidates.Count > 0)
        {
            targetPos = candidates[Random.Range(0, candidates.Count)];
        }
        else
        {
            float fallbackMaxDist = TunnelsPowerOutageManager.isGlobalPowerOutage ? 20f : 28f;
            if (isNearHatch) fallbackMaxDist = 38f;

            foreach (var p in patrolPoints)
            {
                if (p == null) continue;
                float distToPlayer = Vector3.Distance(player.position, p.position);

                if (isNearHatch)
                {
                    float distToHatch = Vector3.Distance(TunnelsGenerator.worldExitPointPos, p.position);
                    if (distToHatch < 15f) continue;
                }

                if (distToPlayer >= minDist && distToPlayer <= fallbackMaxDist)
                {
                    if (IsPositionInNarrowSpace(p.position))
                    {
                        continue; // Evitar pasillos muy reducidos
                    }
                    TunnelLightFlicker lightAtPoint = GetClosestLightToPosition(p.position);
                    if (lightAtPoint != null && !lightAtPoint.isForcedOff && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                    {
                        if (Vector3.Distance(p.position, lightAtPoint.transform.position) <= 6.0f)
                        {
                            continue;
                        }
                    }

                    targetPos = p.position;
                    break;
                }
            }
        }

        if (targetPos != Vector3.zero)
        {
            // Ajustar posición en NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 4f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            // Realizar Warp
            if (agent.Warp(targetPos))
            {
                ResetVisualChildTransform();
                ResetAgentPath();
                
                // Forzar que empiece a patrullar/investigar la zona del jugador (o cacería si hay apagón)
                lastKnownPlayerPosition = player.position;
                chaseLostTimer = 0f;
                
                if (TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    ChangeState(PhenomenonState.Chase);
                }
                else
                {
                    ChangeState(PhenomenonState.Investigate);
                }

                // Efecto de parpadeo de linterna por interferencia electromagnética repentina
                FlashlightController fl = FindObjectOfType<FlashlightController>();
                if (fl != null)
                {
                    StartCoroutine(GlitchFlashlightCoroutine(fl));
                }

                // Play warp sound (Apagon) y Jumpscare si aparece cerca
                if (Vector3.Distance(targetPos, player.position) <= 12f)
                {
                    PlayJumpscareSting(0.85f);
                    TriggerJumpscareCameraGlance();
                }

                AudioClip warpSound = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
                if (warpSound == null) warpSound = Resources.Load<AudioClip>("Apagon");
                if (warpSound != null)
                {
                    AudioSource.PlayClipAtPoint(warpSound, player.position, 0.9f);
                }

                Debug.Log("[PhenomenonAIController] Teletransporte Director: Monstruo reubicado cerca del jugador.");
            }
        }
    }

    private IEnumerator RunScratchScareEventCoroutine()
    {
        if (player == null) yield break;

        // 1. Elegir una dirección aleatoria y buscar una celda de NavMesh a 15-22 metros del jugador
        Vector3 randomDir = Random.insideUnitSphere;
        randomDir.y = 0f;
        randomDir = randomDir.normalized;
        Vector3 startPos = player.position + randomDir * Random.Range(15f, 22f);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(startPos, out hit, 10f, NavMesh.AllAreas))
        {
            startPos = hit.position;
        }

        // 2. Crear un objeto temporal para reproducir las garras deslizándose por el NavMesh
        GameObject scareSoundObj = new GameObject("ScareClawSoundSource");
        scareSoundObj.transform.position = startPos;

        AudioSource snd = scareSoundObj.AddComponent<AudioSource>();
        // Cargar el arrastre de garras
        AudioClip clawClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/DragFingersSound");
        if (clawClip == null) clawClip = dragFingersSound;
        
        snd.clip = clawClip;
        snd.spatialBlend = 1.0f; // 3D
        snd.minDistance = 3.0f;
        snd.maxDistance = 25.0f;
        snd.volume = 1.0f;
        snd.loop = true;
        snd.Play();

        // 3. Mover el sonido en carrera rápida hacia el jugador durante 2.8 segundos (de menos a más)
        float elapsed = 0f;
        float duration = 2.8f;
        
        // Punto objetivo cerca del jugador (con un leve offset lateral)
        Vector3 targetOffset = Random.insideUnitCircle.normalized * 1.5f;
        Vector3 finalTarget = player.position + new Vector3(targetOffset.x, 0f, targetOffset.y);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Interpolar posición del sonido hacia el jugador para que se escuche acercándose rápidamente
            scareSoundObj.transform.position = Vector3.Lerp(startPos, finalTarget, t);
            yield return null;
        }

        // 4. Detener y limpiar el emisor de sonido
        snd.Stop();
        Destroy(scareSoundObj);

        // 5. Determinar probabilidad de Teletransportación Real del Fenómeno
        // - No teletransportar si el jugador ya está cerca de la escotilla de escape para no romper el juego
        bool isNearHatch = (TunnelsGenerator.escapeState == TunnelsGenerator.EscapeState.Ready) 
            || (Vector3.Distance(player.position, TunnelsGenerator.worldExitPointPos) < 25f);
            
        if (!isNearHatch)
        {
            // Probabilidad de aparecer realmente según la dificultad
            if (Random.value <= activeWarpChance && agent != null && agent.isOnNavMesh)
            {
                // Teletransportarse detrás/cerca del jugador con distancia dependiente de la dificultad
                float minDist = 10f;
                float maxDist = 16f;
                string currentDiff = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
                if (currentDiff == "FACIL")
                {
                    minDist = 15f;
                    maxDist = 22f;
                }
                else if (currentDiff == "DIFICIL")
                {
                    minDist = 5f;
                    maxDist = 9f;
                }
                else // NORMAL
                {
                    minDist = 10f;
                    maxDist = 16f;
                }

                Vector3 warpTarget = player.position + player.forward * Random.Range(minDist, maxDist);
                NavMeshHit warpHit;
                if (NavMesh.SamplePosition(warpTarget, out warpHit, 6f, NavMesh.AllAreas))
                {
                    warpTarget = warpHit.position;
                }
                else
                {
                    warpTarget = finalTarget;
                }

                if (agent.Warp(warpTarget))
                {
                    ResetVisualChildTransform();
                    ResetAgentPath();
                    lastKnownPlayerPosition = player.position;
                    chaseLostTimer = 0f;
                    ChangeState(PhenomenonState.Chase);

                    // Glitch de linterna y sonido de apagón para marcar la aparición real
                    FlashlightController fl = FindObjectOfType<FlashlightController>();
                    if (fl != null)
                    {
                        StartCoroutine(GlitchFlashlightCoroutine(fl));
                    }

                    AudioClip warpSound = Resources.Load<AudioClip>("Apagon");
                    if (warpSound != null)
                    {
                        AudioSource.PlayClipAtPoint(warpSound, player.position, 1.0f);
                    }
                    
                    Debug.Log("[PhenomenonAIController] ¡Rasguños Reales! El monstruo se ha teletransportado al jugador.");
                }
            }
            else
            {
                Debug.Log("[PhenomenonAIController] ¡Falsa Alarma! Los rasguños eran una alucinación sonora.");
            }
        }
    }

    private bool IsPositionHiddenFromPlayer(Vector3 pos)
    {
        if (playerCamera == null) return true;

        Vector3 dirToPos = (pos + Vector3.up * 1f - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, dirToPos);
        
        // Si está a espaldas del jugador (fuera del cono de visión de 80 grados), es seguro
        if (angle > 80f) return true;

        // Si está al frente, comprobar si hay una pared o techo bloqueando la vista
        RaycastHit hit;
        if (Physics.Linecast(playerCamera.position, pos + Vector3.up * 1.2f, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != playerCamera.root && hit.transform.root != transform.root)
            {
                return true; // Hay una pared interponiéndose
            }
        }

        return false;
    }

    private IEnumerator GlitchFlashlightCoroutine(FlashlightController fl)
    {
        fl.isGlitchedByMonster = true;
        yield return new WaitForSeconds(1.0f);
        fl.isGlitchedByMonster = false;
    }

    private void CheckLabyrinthRerouteTeleport()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Si físicamente está cerca (menos de 22 metros) pero hay un obstáculo/rodeo largo
        if (distToPlayer <= 22f)
        {
            NavMeshPath path = new NavMeshPath();
            if (agent.CalculatePath(player.position, path))
            {
                float pathLength = 0f;
                if (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial)
                {
                    Vector3[] corners = path.corners;
                    for (int i = 0; i < corners.Length - 1; i++)
                    {
                        pathLength += Vector3.Distance(corners[i], corners[i + 1]);
                    }
                }

                // Si el camino es largo (>45m) o está cortado (Partial), hacer teletransporte sigiloso
                if (pathLength > 45f || path.status == NavMeshPathStatus.PathPartial)
                {
                    TrySilentTeleportNearPlayer();
                }
            }
        }
    }

    private void TrySilentTeleportNearPlayer()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        // Buscar celdas de patrulla cerca del jugador (distancia entre 15 y 28 metros)
        System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
        
        float minDist = 15f;
        float maxDist = 28f;
        if (IsPlayerInLight())
        {
            minDist = 22f;
            maxDist = 35f;
        }

        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            float distToPlayer = Vector3.Distance(player.position, p.position);
            
            if (distToPlayer >= minDist && distToPlayer <= maxDist)
            {
                    if (IsPositionInNarrowSpace(p.position))
                {
                    continue; // Evitar pasillos muy reducidos
                }
                // Asegurarse de que el spawn point en sí mismo NO esté iluminado
                TunnelLightFlicker lightAtPoint = GetClosestLightToPosition(p.position);
                if (lightAtPoint != null && !lightAtPoint.isForcedOff && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    if (Vector3.Distance(p.position, lightAtPoint.transform.position) <= 6.0f)
                    {
                        continue; // Descartar punto iluminado
                    }
                }

                if (IsPositionHiddenFromPlayer(p.position))
                {
                    candidates.Add(p.position);
                }
            }
        }

        Vector3 targetPos = Vector3.zero;
        if (candidates.Count > 0)
        {
            targetPos = candidates[Random.Range(0, candidates.Count)];
        }

        if (targetPos != Vector3.zero)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 4f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            if (agent.Warp(targetPos))
            {
                ResetVisualChildTransform();
                ResetAgentPath();
                lastKnownPlayerPosition = player.position;
                chaseLostTimer = 0f;
                
                // Conservar el estado de persecución si ya estábamos persiguiendo, sino ir a Investigar
                if (currentState != PhenomenonState.Chase)
                {
                    ChangeState(PhenomenonState.Investigate);
                }
                
                Debug.Log($"[PhenomenonAIController] Teletransporte Sigiloso: Monstruo deslizado cerca del jugador por rodeo. Estado: {currentState}");
            }
        }
    }

    private void TryShadowWarpSkipLight()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        float currentDistToPlayer = Vector3.Distance(transform.position, player.position);

        // Solo saltar si el monstruo está a una distancia media/larga (más de 22 metros)
        if (currentDistToPlayer > 22f)
        {
            // Buscar un punto de patrulla que esté más adelante de la luz (más cerca del jugador)
            System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
            foreach (var p in patrolPoints)
            {
                if (p == null) continue;
                float dToPlayer = Vector3.Distance(player.position, p.position);

                // El punto debe estar más cerca del jugador que el monstruo actual
                // pero a una distancia segura para no matarlo instantáneamente (entre 12 y 22 metros)
                if (dToPlayer >= 12f && dToPlayer < currentDistToPlayer - 6f && dToPlayer <= 22f)
                {
                    if (IsPositionInNarrowSpace(p.position))
                    {
                        continue; // Evitar pasillos muy reducidos
                    }
                    TunnelLightFlicker lightAtPoint = GetClosestLightToPosition(p.position);
                    if (lightAtPoint != null && !lightAtPoint.isForcedOff && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                    {
                        if (Vector3.Distance(p.position, lightAtPoint.transform.position) <= 6.0f)
                        {
                            continue;
                        }
                    }

                    if (IsPositionBehindPlayer(p.position))
                    {
                        candidates.Add(p.position);
                    }
                }
            }

            Vector3 targetPos = Vector3.zero;
            if (candidates.Count > 0)
            {
                targetPos = candidates[Random.Range(0, candidates.Count)];
            }

            if (targetPos != Vector3.zero)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, 4f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                if (agent.Warp(targetPos))
                {
                    ResetVisualChildTransform();
                    PlayShadowWarpEvent();
                }
            }
        }
    }

    private void PlayShadowWarpEvent()
    {
        if (player != null)
        {
            ResetAgentPath();
            shadowWarpCooldownTimer = 4.0f; // Cooldown de 4 segundos para evitar spam

            // Jumpscare Sting + Mirada rápida de cámara al reaparecer tras el salto de sombras
            PlayJumpscareSting(0.85f);
            TriggerJumpscareCameraGlance();

            // Sonido 3D de desaparición/reaparición espectral
            AudioClip warpSound = Resources.Load<AudioClip>("Audio/Compartido/Susurros");
            if (warpSound != null)
            {
                AudioSource.PlayClipAtPoint(warpSound, player.position, 0.75f);
            }

            // Pequeña interferencia en la linterna
            FlashlightController fl = FindObjectOfType<FlashlightController>();
            if (fl != null)
            {
                StartCoroutine(GlitchFlashlightCoroutine(fl));
            }

            Debug.Log("[PhenomenonAIController] Salto de Sombras: Monstruo atravesó la luz y reapareció detrás del jugador.");
        }
    }

    private bool IsPositionBehindPlayer(Vector3 pos)
    {
        if (playerCamera == null) return true;

        Vector3 dirToPos = (pos - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, dirToPos);
        
        // Si el ángulo con el frente de la cámara es mayor a 85 grados, está detrás o a los lados fuera de vista
        return angle > 85f;
    }

    private void PlayPhantomSoundNearPlayer()
    {
        if (player == null) return;

        // Buscar puntos de patrulla a una distancia confusa (entre 12 y 26 metros del jugador)
        System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            float dist = Vector3.Distance(player.position, p.position);
            if (dist >= 12f && dist <= 26f)
            {
                candidates.Add(p.position);
            }
        }

        if (candidates.Count > 0)
        {
            Vector3 soundPos = candidates[Random.Range(0, candidates.Count)];
            
            // Elegir un sonido aleatorio de rasguño o arrastre
            AudioClip soundClip = null;
            float vol = Random.Range(0.45f, 0.75f);
            
            int randType = Random.Range(0, 3);
            if (randType == 0)
            {
                soundClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/DragFingersSoundShort");
            }
            else if (randType == 1)
            {
                soundClip = Resources.Load<AudioClip>("Audio/Monstruos/Phenomenon/DragFingersSound");
                vol *= 0.7f; // El largo es un poco más ruidoso
            }
            else
            {
                soundClip = Resources.Load<AudioClip>("Audio/Compartido/Susurros");
                vol *= 0.6f;
            }

            if (soundClip != null)
            {
                AudioSource.PlayClipAtPoint(soundClip, soundPos, vol);
                Debug.Log($"[PhenomenonAIController] Sonido Fantasma ({soundClip.name}) reproducido en {soundPos} para confundir al jugador.");
            }
        }
    }

    private bool CheckIfPlayerIsLookingAtMonster()
    {
        if (player == null || playerCamera == null) return false;

        // Comprobar ángulo de visión de la cámara del jugador hacia el monstruo (dentro de 60 grados para estar centrado)
        Vector3 dirToMonster = (transform.position + Vector3.up * 1f - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, dirToMonster);
        
        if (angle <= 60f)
        {
            RaycastHit hit;
            // IMPORTANTE: Empezar el Linecast 0.5 metros en dirección de la cámara para saltar el colisionador del propio jugador
            Vector3 rayStart = playerCamera.position + playerCamera.forward * 0.5f;
            Vector3 rayEnd = transform.position + Vector3.up * 1.3f; // Apuntar a la altura del pecho/cabeza del monstruo
            
            if (!Physics.Linecast(rayStart, rayEnd, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            else if (hit.transform.root == transform.root)
            {
                return true;
            }
        }
        return false;
    }

    private void TriggerVisualImpactSound()
    {
        if (player == null) return;

        // Ejecutar Jumpscare Sting (Bass siempre presente + variación rotativa)
        PlayJumpscareSting(1.0f);
        TriggerJumpscareCameraGlance();

        // Elegir aleatoriamente uno de los dos sonidos para evitar repetición constante
        int randSound = Random.Range(1, 3); // Retorna 1 o 2
        AudioClip impactClip = Resources.Load<AudioClip>($"Audio/Compartido/Impacto_{randSound}");
        
        // Búsqueda en cascada / Fallback de seguridad
        if (impactClip == null)
        {
            impactClip = Resources.Load<AudioClip>("Audio/Compartido/Impacto_1");
        }
        if (impactClip == null)
        {
            impactClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
        }
        if (impactClip == null)
        {
            impactClip = Resources.Load<AudioClip>("Impacto");
        }
        if (impactClip == null)
        {
            impactClip = Resources.Load<AudioClip>("Apagon");
        }

        if (impactClip != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            // Curva de volumen amplificada: fuerte hasta 20m, decayendo lentamente
            float volume = Mathf.Lerp(1.0f, 0.4f, Mathf.InverseLerp(10f, 35f, dist));
            volume = Mathf.Clamp(volume, 0.4f, 1.0f);

            AudioSource.PlayClipAtPoint(impactClip, player.position, volume);
            
            Debug.Log($"[PhenomenonAIController] Susto de impacto ({impactClip.name}) reproducido a volumen {volume} (Distancia: {dist}m).");
        }
    }

    private void PlayJumpscareSting(float volume = 1.0f)
    {
        if (player == null) return;

        // Cooldown de 1.2s para evitar superposición acelerada
        if (Time.time - lastJumpscareStingTime < 1.2f) return;

        // El susto solo suena si el monstruo está relativamente cerca del jugador (≤14m).
        // Como Slenderman: si aparece lejos no asusta con sonido, solo cuando está cerca o se teletransporta.
        float currentDist = Vector3.Distance(transform.position, player.position);
        if (currentDist > 14.0f) return;

        lastJumpscareStingTime = Time.time;

        // FORZAR VOLUMEN MÁXIMO DE SUSTO EN TODO MOMENTO (1.0f)
        float activeVolume = 1.0f;

        // 1. Sonido de Bass: Siempre se ejecuta en todos los sustos/apariciones
        if (jumpscareStingBassClip != null)
        {
            AudioSource.PlayClipAtPoint(jumpscareStingBassClip, player.position, activeVolume);
        }

        // 2. Variaciones: Intercambiar aleatoriamente entre Normal, Normal2 y Strong sin repetir la última
        System.Collections.Generic.List<AudioClip> variations = new System.Collections.Generic.List<AudioClip>();
        if (jumpscareStingNormalClip != null) variations.Add(jumpscareStingNormalClip);
        if (jumpscareStingNormal2Clip != null) variations.Add(jumpscareStingNormal2Clip);
        if (jumpscareStingStrongClip != null) variations.Add(jumpscareStingStrongClip);

        if (variations.Count > 0)
        {
            int randomIndex = Random.Range(0, variations.Count);
            if (variations.Count > 1 && randomIndex == lastJumpscareVariationIndex)
            {
                randomIndex = (randomIndex + 1) % variations.Count;
            }
            lastJumpscareVariationIndex = randomIndex;

            AudioClip selectedVariation = variations[randomIndex];
            AudioSource.PlayClipAtPoint(selectedVariation, player.position, activeVolume);
            Debug.Log($"[PhenomenonAIController] Jumpscare Sting: Bass + {selectedVariation.name} (Vol: {activeVolume:F2})");
        }
    }

    private void TriggerJumpscareCameraGlance()
    {
        if (player == null) return;
        if (cameraGlanceCoroutine != null)
        {
            StopCoroutine(cameraGlanceCoroutine);
        }
        cameraGlanceCoroutine = StartCoroutine(JumpscareCameraGlanceRoutine());
    }

    private IEnumerator JumpscareCameraGlanceRoutine()
    {
        if (player == null) yield break;

        // Obtener FirstPersonController para manipular _cinemachineTargetPitch y la rotación del personaje
        var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc == null) fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc == null && player.parent != null) fpc = player.parent.GetComponentInChildren<StarterAssets.FirstPersonController>();

        Transform playerTransform = fpc != null ? fpc.transform : player;
        Transform camTransform = playerCamera != null ? playerCamera : ((Camera.main != null) ? Camera.main.transform : null);
        if (camTransform == null && fpc != null && fpc.CinemachineCameraTarget != null)
        {
            camTransform = fpc.CinemachineCameraTarget.transform;
        }
        if (camTransform == null) yield break;

        // Apuntar al rostro/cabeza del monstruo (altura aprox 1.7m)
        Vector3 monsterTargetPos = transform.position + Vector3.up * 1.7f;
        Vector3 dirToMonster = (monsterTargetPos - camTransform.position).normalized;
        if (dirToMonster == Vector3.zero) yield break;

        // 1. Dirección horizontal (Yaw) directa hacia el monstruo
        Vector3 flatDir = Vector3.ProjectOnPlane(dirToMonster, Vector3.up).normalized;
        if (flatDir == Vector3.zero) yield break;
        Quaternion targetBodyRot = Quaternion.LookRotation(flatDir, Vector3.up);
        Quaternion startPlayerRot = playerTransform.rotation;

        // 2. Ángulo vertical (Pitch) directo hacia el rostro del monstruo
        float targetPitch = -Mathf.Asin(Mathf.Clamp(dirToMonster.y, -0.99f, 0.99f)) * Mathf.Rad2Deg;
        if (fpc != null)
        {
            targetPitch = Mathf.Clamp(targetPitch, fpc.BottomClamp, fpc.TopClamp);
        }

        System.Reflection.FieldInfo pitchField = null;
        float startPitch = 0f;
        if (fpc != null)
        {
            pitchField = fpc.GetType().GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pitchField != null)
            {
                startPitch = (float)pitchField.GetValue(fpc);
            }
        }

        float duration = 0.16f; // Sacudida rápida, impactante e inmediata (0.16 segundos)
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float t = progress * (2f - progress);

            // Girar el cuerpo del jugador directamente hacia el Phenomenon (95% directo a su rostro)
            playerTransform.rotation = Quaternion.Slerp(startPlayerRot, targetBodyRot, t * 0.95f);

            // Ajustar el pitch de la cámara del FirstPersonController
            if (fpc != null && pitchField != null)
            {
                float currentPitch = Mathf.Lerp(startPitch, targetPitch, t * 0.95f);
                pitchField.SetValue(fpc, currentPitch);
            }

            yield return null;
        }

        // Asegurar posición final precisa apuntando al monstruo
        playerTransform.rotation = Quaternion.Slerp(startPlayerRot, targetBodyRot, 0.95f);
        if (fpc != null && pitchField != null)
        {
            pitchField.SetValue(fpc, Mathf.Lerp(startPitch, targetPitch, 0.95f));
        }
    }

    private void UpdatePanicEventSystem()
    {
        // Forzar cacería infinita si la bomba de drenaje en el mapa de túneles está activa o si hay un apagón global
        bool isForcedHunt = (TunnelsGenerator.escapeState == TunnelsGenerator.EscapeState.Draining) || TunnelsPowerOutageManager.isGlobalPowerOutage;

        if (isForcedHunt)
        {
            if (!isPanicEventActive)
            {
                isPanicEventActive = true;
                panicTimer = 0f;
                panicWarpTimer = 0f;
                if (currentState != PhenomenonState.Attack && currentState != PhenomenonState.Chase)
                {
                    ChangeState(PhenomenonState.Chase);
                }
                ManageNearbyLights();
            }
            // Mientras esté drenando o haya apagón, mantener el temporizador a cero para que no acabe
            panicTimer = 0f;
        }
        else
        {
            panicTimer += Time.deltaTime;
        }

        if (!isPanicEventActive)
        {
            // Fase de Calma
            if (panicTimer >= calmDuration)
            {
                isPanicEventActive = true;
                panicTimer = 0f;
                panicWarpTimer = 0f;

                // Forzar persecución si no está atacando ni congelado observando la luz
                if (currentState != PhenomenonState.Attack && currentState != PhenomenonState.ObservingLight && currentState != PhenomenonState.Chase)
                {
                    ChangeState(PhenomenonState.Chase);
                }

                // Forzar actualización inmediata de luces
                ManageNearbyLights();

                Debug.Log("[PhenomenonAIController] EVENTO DE PÁNICO ACTIVADO. Caza implacable iniciada.");
            }
        }
        else
        {
            // Fase de Pánico
            if (panicTimer >= panicDuration)
            {
                isPanicEventActive = false;
                panicTimer = 0f;

                // Si no nos ve y no está en pánico, regresar a patrullar
                if (currentState == PhenomenonState.Chase)
                {
                    ChangeState(PhenomenonState.Patrol);
                }

                // Forzar actualización inmediata de luces para restaurarlas
                ManageNearbyLights();

                Debug.Log("[PhenomenonAIController] Evento de pánico finalizado. Regresando a fase de calma.");
            }
            else
            {
                // Durante el pánico:
                // 1. Forzar persecución si no está atacando ni congelado observando la luz
                if (currentState != PhenomenonState.Attack && currentState != PhenomenonState.ObservingLight && currentState != PhenomenonState.Chase)
                {
                    ChangeState(PhenomenonState.Chase);
                }

                // Durante la cacería, asegurar que se mueva a velocidad normal de persecución si no está bajo la luz
                if (currentState == PhenomenonState.Chase && !IsShinedByFlashlight() && !IsMonsterInLight() && agent != null && agent.isOnNavMesh)
                {
                    agent.speed = chaseSpeed;
                }

                // 2. Teletransportes balanceados cada 12.0 segundos si no lo vemos directamente durante la cacería
                panicWarpTimer += Time.deltaTime;
                if (panicWarpTimer >= 12.0f)
                {
                    panicWarpTimer = 0f;
                    if (!isCurrentlyVisible)
                    {
                        TryAggressivePanicWarpNearPlayer(true); // true para modo de cacería extrema
                    }
                }
            }
        }
    }

    private void TryAggressivePanicWarpNearPlayer(bool isHunting)
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        float minDistanceToWarp = isHunting ? 12f : 20f; // Teletransportar si está a más de 12m durante cacería
        
        if (distToPlayer > minDistanceToWarp)
        {
            System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector3> backupCandidates = new System.Collections.Generic.List<Vector3>();
            
            float minRange = isHunting ? 10f : 15f;
            float maxRange = isHunting ? 16f : 25f;

            foreach (var p in patrolPoints)
            {
                if (p == null) continue;

                // Evitar aparecer cerca de la escotilla de escape
                if (TunnelsFixedMapLogic.worldExitPointPos != Vector3.zero && Vector3.Distance(p.position, TunnelsFixedMapLogic.worldExitPointPos) < 18f)
                {
                    continue;
                }
                if (TunnelsGenerator.worldExitPointPos != Vector3.zero && Vector3.Distance(p.position, TunnelsGenerator.worldExitPointPos) < 18f)
                {
                    continue;
                }

                if (IsPositionInNarrowSpace(p.position))
                {
                    continue; // Evitar pasillos muy reducidos
                }
                // Evitar aparecer directamente en una zona iluminada
                TunnelLightFlicker lightAtPoint = GetClosestLightToPosition(p.position);
                if (lightAtPoint != null && !lightAtPoint.isForcedOff && !TunnelsPowerOutageManager.isGlobalPowerOutage)
                {
                    if (Vector3.Distance(p.position, lightAtPoint.transform.position) <= 6.0f)
                    {
                        continue;
                    }
                }

                float d = Vector3.Distance(player.position, p.position);
                if (d >= minRange && d <= maxRange)
                {
                    backupCandidates.Add(p.position);
                    if (IsPositionHiddenFromPlayer(p.position))
                    {
                        candidates.Add(p.position);
                    }
                }
            }

            // Usar candidatos escondidos preferencialmente, o backup si no hay ninguno escondido (asegura teletransporte constante)
            System.Collections.Generic.List<Vector3> finalCandidates = candidates.Count > 0 ? candidates : backupCandidates;

            Vector3 targetPos = Vector3.zero;
            if (finalCandidates.Count > 0)
            {
                targetPos = finalCandidates[Random.Range(0, finalCandidates.Count)];
            }

            if (targetPos != Vector3.zero)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, 4f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                if (agent.Warp(targetPos))
                {
                    ResetVisualChildTransform();
                    ResetAgentPath();
                    lastKnownPlayerPosition = player.position;
                    chaseLostTimer = 0f;
                    SetAgentDestination(player.position);

                    if (Vector3.Distance(targetPos, player.position) <= 10f)
                    {
                        PlayJumpscareSting(0.9f);
                        TriggerJumpscareCameraGlance();
                    }

                    Debug.Log("[PhenomenonAIController] Cacería/Pánico: Monstruo teletransportado súper cerca del jugador (" + Vector3.Distance(targetPos, player.position).ToString("F1") + "m)");
                }
            }
        }
    }

    /// <summary>
    /// Activa de manera 100% interna el período de gracia y reposiciona de forma segura al monstruo
    /// lejos del jugador tras su reaparición, previniendo bugs de renderizado.
    /// </summary>
    public void TriggerRespawnGracePeriod(float duration)
    {
        graceActive = true;
        graceTimer = 0f;
        gracePeriodDuration = duration;

        // Reposicionar al monstruo físicamente en el subsuelo inmediatamente
        transform.position = new Vector3(0f, -500f, 0f);
        if (agent != null)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }

        SetMonsterVisible(false);
        ChangeState(PhenomenonState.Patrol);
        detectionRange = 0f;
        if (fov != null) fov.enabled = false;

        Debug.Log($"[PhenomenonAIController] TriggerRespawnGracePeriod: Monstruo desactivado y enviado al subsuelo. Gracia activa por {duration} segundos.");
    }

    public bool IsValidWalkablePositionForPlayer(Vector3 testPos, out Vector3 validPos)
    {
        validPos = testPos;
        if (player == null) return false;

        // Evitar aparecer cerca de la escotilla de escape
        if (TunnelsFixedMapLogic.worldExitPointPos != Vector3.zero && Vector3.Distance(testPos, TunnelsFixedMapLogic.worldExitPointPos) < 18f)
        {
            return false;
        }
        if (TunnelsGenerator.worldExitPointPos != Vector3.zero && Vector3.Distance(testPos, TunnelsGenerator.worldExitPointPos) < 18f)
        {
            return false;
        }

        if (SafeZoneTrigger.IsPositionInSafeZone(testPos)) return false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(testPos, out hit, 3.5f, NavMesh.AllAreas))
        {
            if (SafeZoneTrigger.IsPositionInSafeZone(hit.position)) return false;

            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(hit.position, player.position, NavMesh.AllAreas, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    validPos = hit.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void RecoverToPlayerCorridor()
    {
        if (player == null || agent == null) return;

        Vector3 target = player.position + player.forward * 6.0f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 6.0f, NavMesh.AllAreas))
        {
            if (agent.isOnNavMesh)
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = hit.position;
                agent.enabled = false;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            ResetVisualChildTransform();
            ResetAgentPath();
            SetAgentDestination(player.position);
            Debug.Log("[PhenomenonAIController] 🚑 Monstruo rescatado al pasillo AL FRENTE del jugador.");
        }
        else
        {
            ForceWarpNearPlayer(8f);
        }
    }

    private void SetRenderersState(bool state)
    {
        Renderer[] rends = cachedRenderers != null ? cachedRenderers : GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in rends)
        {
            if (r != null && r.gameObject != gameObject && !r.gameObject.name.Contains("Light"))
            {
                r.enabled = state;
            }
        }
        Light[] lights = cachedLights != null ? cachedLights : GetComponentsInChildren<Light>(true);
        foreach (Light l in lights)
        {
            if (l != null) l.enabled = state;
        }
    }

    public void ForceRelocateFarAway()
    {
        if (player == null) return;

        Vector3 targetPos = Vector3.zero;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform bestPoint = null;
            float maxDist = -1f;
            foreach (Transform pt in patrolPoints)
            {
                if (pt == null) continue;

                Vector3 validP;
                if (IsValidWalkablePositionForPlayer(pt.position, out validP))
                {
                    float d = Vector3.Distance(player.position, validP);
                    if (d >= 20f && d > maxDist)
                    {
                        maxDist = d;
                        bestPoint = pt;
                        targetPos = validP;
                    }
                }
            }
        }

        if (targetPos == Vector3.zero)
        {
            Vector3[] dirs = new Vector3[] { -player.forward, player.right, -player.right, player.forward };
            foreach (Vector3 d in dirs)
            {
                for (float dist = 55f; dist >= 25f; dist -= 10f)
                {
                    Vector3 testP = player.position + d * dist;
                    Vector3 validP;
                    if (IsValidWalkablePositionForPlayer(testP, out validP))
                    {
                        targetPos = validP;
                        break;
                    }
                }
                if (targetPos != Vector3.zero) break;
            }
        }

        // Si NO hay ninguna posición caminable lejos de la Zona Segura, ocultar al monstruo fuera de la escena
        if (targetPos == Vector3.zero || SafeZoneTrigger.IsPositionInSafeZone(targetPos))
        {
            Vector3 offscreenPos = player.position - player.forward * 80f;
            offscreenPos.y = -500f;

            if (agent != null)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }
            transform.position = offscreenPos;
            SetRenderersState(false);
            Debug.Log("[PhenomenonAIController] 🛡️ No hay espacio lejano en NavMesh. Monstruo ocultado bajo el mapa mientras el jugador está a salvo.");
            return;
        }

        SetRenderersState(true);
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(targetPos);
                ResetVisualChildTransform();
                ResetAgentPath();
            }
            else
            {
                transform.position = targetPos;
            }
        }
    }

    public void ForceWarpNearPlayer(float dist = 11f)
    {
        if (player == null) return;

        SetRenderersState(true);
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }

        // Priorizar teletransporte AL FRENTE (en el pasillo que mira el jugador) o a los lados, NUNCA a las espaldas
        Vector3[] offsets = new Vector3[]
        {
            player.forward * dist,
            player.forward * (dist * 0.75f),
            player.right * (dist * 0.85f),
            -player.right * (dist * 0.85f),
            player.forward * (dist * 1.3f)
        };

        Vector3 chosenWarp = Vector3.zero;
        foreach (Vector3 off in offsets)
        {
            Vector3 testPos = player.position + off;
            Vector3 validPos;
            if (IsValidWalkablePositionForPlayer(testPos, out validPos))
            {
                chosenWarp = validPos;
                break;
            }
        }

        if (chosenWarp == Vector3.zero)
        {
            Vector3 fallback = player.position + player.forward * 6.0f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(fallback, out hit, 6.0f, NavMesh.AllAreas))
            {
                chosenWarp = hit.position;
            }
        }

        if (chosenWarp != Vector3.zero && agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.Warp(chosenWarp);
            }
            else
            {
                transform.position = chosenWarp;
                agent.enabled = true;
                agent.Warp(chosenWarp);
            }
            ResetVisualChildTransform();
            ResetAgentPath();
            SetAgentDestination(player.position);
            PlayJumpscareSting(1.0f);
            Debug.Log($"[PhenomenonAIController] 👁️ Monster spawned IN FRONT OF PLAYER at {chosenWarp}");
        }
    }

    private void TeleportToDistantPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0 || player == null) return;

        // Buscar puntos de patrulla lejanos al jugador (más de 30 metros de distancia)
        System.Collections.Generic.List<Vector3> farPoints = new System.Collections.Generic.List<Vector3>();
        foreach (Transform pt in patrolPoints)
        {
            if (pt == null) continue;
            if (SafeZoneTrigger.IsPositionInSafeZone(pt.position)) continue;

            float dist = Vector3.Distance(player.position, pt.position);
            if (dist > 30f)
            {
                farPoints.Add(pt.position);
            }
        }

        Vector3 targetWarp = Vector3.zero;
        if (farPoints.Count > 0)
        {
            targetWarp = farPoints[Random.Range(0, farPoints.Count)];
        }
        else
        {
            // Fallback: elegir el punto de patrulla más lejano
            float maxDist = -1f;
            foreach (Transform pt in patrolPoints)
            {
                if (pt == null) continue;
                float dist = Vector3.Distance(player.position, pt.position);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    targetWarp = pt.position;
                }
            }
        }

        if (targetWarp != Vector3.zero)
        {
            if (agent != null)
            {
                if (agent.isOnNavMesh)
                {
                    agent.Warp(targetWarp);
                }
                else
                {
                    transform.position = targetWarp;
                    agent.enabled = true;
                    agent.Warp(targetWarp);
                }
            }
            else
            {
                transform.position = targetWarp;
            }

            ResetVisualChildTransform();
            ResetAgentPath();
            MoveToNextPatrolPoint(); // Asignar nuevo destino inmediatamente
            Debug.Log($"[PhenomenonAIController] 🌌 Teletransportado a patrulla lejana ({Vector3.Distance(player.position, targetWarp):F1}m) para evitar campeo.");
        }
    }
}
