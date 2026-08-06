using UnityEngine;
using UnityEngine.AI;

public class EnemyAIController : MonoBehaviour
{
    public Transform player;
    public float attackRange = 2f;
    public float detectionRange = 5.0f;
    public float walkSpeed = 1.0f;
    public float runSpeed = 1.8f;
    public Transform[] patrolPoints;

    [Header("Reposicionamiento Silencioso")]
    [Tooltip("Segundos sin ver al enemigo antes de que aparezca cerca del jugador")]
    public float silentRepositionTime = 45f;
    [Tooltip("Radio maximo al que aparece el enemigo desde el jugador (unidades)")]
    public float repositionMaxRadius = 10f;
    [Tooltip("Radio minimo de aparicion (evita que aparezca encima del jugador)")]
    public float repositionMinRadius = 4f;

    public AudioSource audioSource; // AudioSource para el monstruo
    public AudioClip monsterSoundClip;
    public AudioSource footstepAudioSource; // Reutilizamos el mismo AudioSource para pasos
    public AudioClip footstepSoundClip; // AudioClip para los pasos

    private NavMeshAgent agent;
    private EnemyAnimation anim;
    private IEnemyState currentState;
    private FieldOfView fov;
    private float timeSincePlayerSeen = 0f; // Para reposicionamiento silencioso
    private HideUnderBed hideScript;
    private RoomLightsManager roomLightsManager;
    private PlayerSanity playerSanity;
    private float timeSinceLastSeenGhost = 0f;
    [HideInInspector]
    public SprintDetector playerSprintDetector;

    [Header("Sonido de Persecución/Tensión")]
    [Tooltip("Sonido terrorífico que suena al perseguir o estar muy cerca (se carga de Resources/Monstruo_Alerta si está vacío)")]
    public AudioClip chaseSoundClip;
    
    private bool difficultyBoostApplied = false;
    private float originalWalkSpeed;
    private float originalRunSpeed;
    private float originalDetectionRange;
    private float originalSilentRepositionTime;

    // --- SISTEMA DE COOLDOWN Y DETECCIÓN DE SIGILO ---
    private float accumulatedRunTime = 0f;
    private float noiseAlertCooldownTimer = 0f;
    private Vector3 lastKnownPlayerPosition;

    void Start()
    {
        Debug.Log("Start del enemigo iniciado");
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
        anim = GetComponent<EnemyAnimation>();
        if (anim == null) anim = GetComponentInChildren<EnemyAnimation>();
        fov = GetComponent<FieldOfView>();
        if (fov == null) fov = GetComponentInChildren<FieldOfView>();
        if (fov != null)
        {
            fov.player = player; // Asignacion incondicional para evitar falsos positivos con tags
        }

        if (agent != null)
        {
            agent.radius = 0.45f;
            agent.stoppingDistance = 1.6f;
            // Asegurar que el agente controla el transform (no el motor de fisica)
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        // CRITICO: Rigidbodies y BoxColliders sólidos en hijos pelean contra el NavMeshAgent.
        // Hacerlos kinematic y triggers para que el NavMesh tenga control total del movimiento sin trabarse.
        Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in childRbs)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        BoxCollider[] boxCols = GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider bc in boxCols)
        {
            bc.isTrigger = true;
        }

        if (fov == null)
        {
            Debug.LogError("El componente FieldOfView no est asignado al enemigo.");
        }

        // RESOLUCION BLINDADA DEL JUGADOR (Bypassea tags perdidos, referencias a prefabs del Project y nombres)
        GameObject scenePlayer = GameObject.FindGameObjectWithTag("Player");
        if (scenePlayer != null)
        {
            player = scenePlayer.transform;
            Debug.Log("EnemyAIController: Jugador encontrado en escena por Tag 'Player'.");
        }
        else
        {
            // Si fallan los tags, verificar si la referencia del Inspector es un prefab del Project en vez de la escena
            if (player == null || player.gameObject.scene.name == null)
            {
                // Buscar en la escena activa por los nombres comunes
                GameObject foundPlayer = GameObject.Find("PlayerCapsule");
                if (foundPlayer == null) foundPlayer = GameObject.Find("Player");
                if (foundPlayer == null)
                {
                    // Buscar mediante el componente FirstPersonController
                    var fpc = FindObjectOfType<StarterAssets.FirstPersonController>();
                    if (fpc != null) foundPlayer = fpc.gameObject;
                }
                
                if (foundPlayer != null)
                {
                    player = foundPlayer.transform;
                    Debug.Log("EnemyAIController: Jugador de la escena resuelto por busqueda de nombre/componente: " + foundPlayer.name);
                }
                else
                {
                    Debug.LogError("EnemyAIController: CRITICO - No se pudo encontrar al jugador en la escena!");
                }
            }
            else
            {
                Debug.Log("EnemyAIController: Usando referencia del jugador asignada directamente en el Inspector: " + player.name);
            }
        }

        if (player != null)
        {
            Collider[] pCols = player.GetComponentsInChildren<Collider>(true);
            Collider[] myCols = GetComponentsInChildren<Collider>(true);
            foreach (Collider mC in myCols)
            {
                if (mC == null) continue;
                foreach (Collider pC in pCols)
                {
                    if (pC != null) Physics.IgnoreCollision(mC, pC, true);
                }
            }
        }

        // Buscar referencias automticamente para evitar requerir configuracin manual
        // Guardar valores base del inspector para escalar dificultad
        originalWalkSpeed = walkSpeed;
        originalRunSpeed = runSpeed;
        originalDetectionRange = detectionRange;
        originalSilentRepositionTime = silentRepositionTime;

        // Asegurar que el AudioSource local no sea nulo (búsqueda y autoguardado dinámico)
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Auto-cargar sonido de persecucion si esta vacio
        if (chaseSoundClip == null)
        {
            chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Persecusion");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Monstruo_Alerta");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Persecusion");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Monstruo_Alerta");
            if (chaseSoundClip != null)
            {
                Debug.Log("EnemyAIController: Foco de persecucion cargado exitosamente: " + chaseSoundClip.name);
            }
            else
            {
                Debug.LogError("EnemyAIController: ¡CRÍTICO! No se encontró sonido de persecución en Assets/Resources.");
            }
        }

        if (player != null)
        {
            playerSprintDetector = player.GetComponent<SprintDetector>();
            if (playerSprintDetector == null) playerSprintDetector = player.GetComponentInChildren<SprintDetector>();
            if (playerSprintDetector == null)
            {
                playerSprintDetector = player.gameObject.AddComponent<SprintDetector>();
                Debug.Log("EnemyAIController: SprintDetector no estaba en el jugador. Se ha agregado dinamicamente.");
            }
        }

        hideScript = FindObjectOfType<HideUnderBed>();
        roomLightsManager = FindObjectOfType<RoomLightsManager>();
        playerSanity = FindObjectOfType<PlayerSanity>();

        ChangeState(new EnemyPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));

        SetupAudio();
        StartCoroutine(SilentRepositionRoutine());
    }

    // Se ejecuta CADA VEZ que el enemigo se activa con SetActive(true)
    void OnEnable()
    {
        // Solo reiniciar si ya tenemos patrol points (no en el primer Start())
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            StartCoroutine(RestartPatrolDelayed());
        }
    }

    private System.Collections.IEnumerator RestartPatrolDelayed()
    {
        // Esperar 3 frames para que NavMeshAgent este completamente habilitado y en el NavMesh
        yield return null;
        yield return null;
        yield return null;

        if (agent == null || anim == null) yield break;
        if (!agent.isOnNavMesh)
        {
            // Si no está en el NavMesh, usar Warp para anclar al punto del suelo más cercano (restringiendo a Y del nivel)
            // LEGACY REMOVED: HospitalMazeGenerator.transform.position.y
            float floorY = transform.position.y;
            Vector3 testOrigin = new Vector3(transform.position.x, floorY, transform.position.z);

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testOrigin, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log("EnemyAIController: Warp al NavMesh del piso en " + hit.position);
                yield return null; // Un frame extra tras el Warp
            }
            else
            {
                Debug.LogWarning("EnemyAIController: No se encontro punto en el NavMesh cerca del suelo.");
                yield break;
            }
        }

        // Reiniciar el estado de patrulla limpiamente con los puntos ya cargados
        currentState?.ExitState();
        currentState = null;
        currentState = new EnemyPatrolState(this, agent, anim, patrolPoints);
        currentState.EnterState();
        Debug.Log("EnemyAIController: Patrulla reiniciada. isOnNavMesh=" + agent.isOnNavMesh + " Puntos=" + patrolPoints.Length);
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;

        currentState?.UpdateState();
        HandleStateTransitions();
        HandleFootsteps();
        HandleHallucinations();

        // ---------------------------------------------------------------------
        // SISTEMA DE DIFICULTAD PROGRESIVA DINÁMICA
        // ---------------------------------------------------------------------
        int activeGens = 0;
        SubGenerator[] subGens = FindObjectsOfType<SubGenerator>();
        foreach (var gen in subGens)
        {
            if (gen != null && gen.isOn) activeGens++;
        }

        int repairsUsed = 0;
        PowerBox pBox = FindObjectOfType<PowerBox>();
        if (pBox != null)
        {
            repairsUsed = pBox.repairsCount;
        }

        // Factor de tiempo: escala lineal de 0 a 1 en 5 minutos (300 segundos) de juego transcurridos
        float timeFactor = Time.timeSinceLevelLoad / 300f;
        timeFactor = Mathf.Clamp01(timeFactor);

        // Aumentar progresivamente la velocidad del enemigo de forma sutil según generadores, reparaciones eléctricas y tiempo
        float extraSpeed = (activeGens * 0.25f) + (repairsUsed * 0.2f) + (timeFactor * 0.4f);

        // Si el jugador consigue la tarjeta del director, la dificultad escala moderadamente (+0.8f velocidad)
        if (ElevatorController.hasKeycard)
        {
            extraSpeed += 0.8f;
            
            if (!difficultyBoostApplied)
            {
                difficultyBoostApplied = true;
                silentRepositionTime = 25f; // Monstruo se reposiciona más seguido si no ve al jugador
                Debug.LogWarning("EnemyAI: Dificultad Extrema (End-Game) activada por recogida de tarjeta del director.");
            }
        }

        // Aplicar velocidades dinámicas pausadas (con topes máximos ajustados al mapa chico)
        walkSpeed = Mathf.Min(originalWalkSpeed + (extraSpeed * 0.35f), originalWalkSpeed + 1.2f);
        runSpeed = Mathf.Min(originalRunSpeed + (extraSpeed * 0.8f), originalRunSpeed + 1.8f);

        // Aumentar el rango de detección visual del monstruo de forma moderada
        detectionRange = Mathf.Min(originalDetectionRange + (activeGens * 1.5f) + (repairsUsed * 1f) + (timeFactor * 2f), originalDetectionRange + 6f);
        if (ElevatorController.hasKeycard)
        {
            detectionRange += 3f;
            detectionRange = Mathf.Min(detectionRange, originalDetectionRange + 30f);
        }

        bool isDark = roomLightsManager != null && roomLightsManager.powerOutage;

        if (currentState is EnemyAttackState)
        {
            agent.speed = 0f;
        }
        else if (isDark)
        {
            agent.speed = (currentState is EnemyChaseState) ? (runSpeed + 0.4f) : (walkSpeed + 0.3f);
            if (fov != null) fov.viewRadius = difficultyBoostApplied ? 15f : 12f;
        }
        else
        {
            agent.speed = (currentState is EnemyChaseState) ? runSpeed : walkSpeed;
            if (fov != null) fov.viewRadius = difficultyBoostApplied ? 12f : 9f;
        }

        // REGLA ABSOLUTA DE SEGURIDAD: EL MONSTRUO JAMÁS PUEDE ESTAR ELEVADO SOBRE EL TECHO
        // LEGACY REMOVED: HospitalMazeGenerator.transform.position.y
        float targetFloorY = 0f;
        if (transform.position.y > targetFloorY + 0.8f)
        {
            Vector3 currentP = transform.position;
            Vector3 groundedP = new Vector3(currentP.x, targetFloorY, currentP.z);
            if (agent != null && agent.enabled)
            {
                UnityEngine.AI.NavMeshHit groundHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(groundedP, out groundHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(groundHit.position);
                }
                else
                {
                    transform.position = groundedP;
                }
            }
            else
            {
                transform.position = groundedP;
            }
            Debug.LogWarning("EnemyAIController: ¡Corrección de Emergencia! Monstruo bajado inmediatamente del techo al suelo.");
        }

        // Actualizar el cooldown del temporizador de alerta por ruido
        if (noiseAlertCooldownTimer > 0f)
        {
            noiseAlertCooldownTimer -= Time.deltaTime;
        }

        // Acumular tiempo de carrera continua para evitar alertas por micro-toques de Shift
        if (playerSprintDetector != null && playerSprintDetector.IsRunning)
        {
            accumulatedRunTime += Time.deltaTime;
        }
        else
        {
            accumulatedRunTime = 0f; // Reiniciar si camina o se detiene
        }

        // Ajustar radio de escucha (ruido de pisadas) segun si el jugador esta corriendo y no estamos en cooldown
        if (fov != null)
        {
            // Solo escuchar si el jugador corre continuamente durante > 0.5 segundos Y no hay cooldown activo
            if (accumulatedRunTime >= 0.5f && noiseAlertCooldownTimer <= 0f)
            {
                // Rango aumentado drásticamente (50m base / 65m boosted) para que el monstruo escuche correr
                fov.hearingRadius = difficultyBoostApplied ? 65f : 50f;
                
                // Activar el cooldown de 10 segundos para que no cambie de foco o sea "trolleado" infinitamente
                noiseAlertCooldownTimer = 10f;
                Debug.Log("[BookHead] Escuchó ruido de carrera. Alerta activada. Cooldown de audición de 10 segundos iniciado.");
            }
            else if (accumulatedRunTime < 0.5f)
            {
                // Si el jugador no corre continuamente, el oído vuelve a su rango normal de pisadas cortas
                fov.hearingRadius = difficultyBoostApplied ? 6f : 4f;
            }
        }

        // ─── CONTROL DE AUDIO DE TENSIÓN Y PERSECUCIÓN ──────────────────────────
        if (player != null && audioSource != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            bool isChasing = (currentState is EnemyChaseState || currentState is EnemyAttackState);

            if (chaseSoundClip != null && isChasing)
            {
                if (audioSource.clip != chaseSoundClip)
                {
                    audioSource.clip = chaseSoundClip;
                    audioSource.loop = true;
                    audioSource.spatialBlend = 1f;
                    audioSource.minDistance = 3f;
                    audioSource.maxDistance = 25f;  // Rango extendido: se escucha de más lejos
                    audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    audioSource.Play();
                }

                // Persiguiendo: volumen adaptativo por distancia (0.35 a 0.75)
                float chaseVol = Mathf.Clamp01(1f - (dist - 2f) / 20f) * 0.75f;
                audioSource.volume = Mathf.MoveTowards(audioSource.volume, Mathf.Max(0.35f, chaseVol), Time.deltaTime * 2f);
            }
            else
            {
                if (audioSource.clip == chaseSoundClip)
                {
                    audioSource.volume = Mathf.MoveTowards(audioSource.volume, 0f, Time.deltaTime * 1.5f);
                    if (audioSource.volume <= 0.01f)
                    {
                        if (monsterSoundClip != null)
                        {
                            audioSource.clip = monsterSoundClip;
                            audioSource.loop = true;
                            audioSource.spatialBlend = 1f;
                            audioSource.minDistance = 3f;
                            audioSource.maxDistance = 22f;
                            audioSource.volume = 0.55f; // Rugido/gruñido ambiental audible
                            audioSource.Play();
                        }
                        else
                        {
                            audioSource.Stop();
                        }
                    }
                }
            }
        }

        // Si el monstruo está persiguiendo, romper/abrir obstáculos en su camino
        if (currentState is EnemyChaseState)
        {
            CheckAndOpenObstacles();
        }
    }

    private void CheckAndOpenObstacles()
    {
        // Lanzar un rayo o barrido corto desde el pecho del monstruo hacia adelante
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 direction = transform.forward;
        float checkDistance = 1.8f;

        RaycastHit hit;
        // Capa de colisión por defecto que incluya las puertas
        if (Physics.Raycast(origin, direction, out hit, checkDistance))
        {
            // 1. Detectar puertas procedimentales (ProceduralDoorInteract)
            ProceduralDoorInteract procDoor = hit.collider.GetComponentInParent<ProceduralDoorInteract>();
            if (procDoor == null) procDoor = hit.collider.GetComponent<ProceduralDoorInteract>();
            if (procDoor != null)
            {
                // Si la puerta está bloqueada, forzar el desbloqueo porque el monstruo no se detiene ante llaves
                if (procDoor.isLocked) procDoor.isLocked = false;
                
                // Si está cerrada, abrirla de inmediato con un golpe de impacto
                // Obtenemos el estado usando reflexión para leer 'isOpen' o forzándolo mediante Toggle
                // Como 'isOpen' es privado en ProceduralDoorInteract, forzamos un ToggleDoor() si detectamos que no está rotada
                float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : transform.rotation);
                // Si está cerca del ángulo de rotación cerrada, asumimos que está cerrada
                if (angleDiff < 10f || hit.collider.gameObject.name.Contains("Puerta_Panel"))
                {
                    // Forzar apertura ruidosa
                    procDoor.ToggleDoor();
                    Debug.Log("EnemyAIController: Monstruo empujó/abrió puerta procedimental cerrada.");
                }
            }

            // 2. Detectar puertas con animación tradicional (OpenDoor)
            OpenDoor animDoor = hit.collider.GetComponentInParent<OpenDoor>();
            if (animDoor == null) animDoor = hit.collider.GetComponent<OpenDoor>();
            if (animDoor != null)
            {
                if (animDoor.isLocked) animDoor.isLocked = false;
                
                // Si la variable privada es inaccesible, forzamos el seteo por Animator
                if (animDoor.doorAnimator != null && !animDoor.doorAnimator.GetBool("isOpen"))
                {
                    animDoor.doorAnimator.SetBool("isOpen", true);
                    if (animDoor.audioSource && animDoor.doorOpenSound)
                    {
                        animDoor.audioSource.PlayOneShot(animDoor.doorOpenSound, 1.0f);
                    }
                    Debug.Log("EnemyAIController: Monstruo forzó/abrió puerta animada.");
                }
            }
        }
    }

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource != null && monsterSoundClip != null)
        {
            audioSource.clip = monsterSoundClip;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 3f;   // Se empieza a escuchar desde más lejos
            audioSource.maxDistance = 22f;  // Rango más amplio para sonido ambiental del monstruo
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.volume = 0.55f;     // Volumen base del rugido/gruñido ambiental
            audioSource.Play();
        }

        if (footstepAudioSource == null)
        {
            // Si no hay AudioSource secundario para pasos, crearlo dinámicamente
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 1) footstepAudioSource = sources[1];
            else
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (footstepSoundClip == null)
        {
            footstepSoundClip = Resources.Load<AudioClip>("Pasos_Monstruo");
            if (footstepSoundClip == null) footstepSoundClip = Resources.Load<AudioClip>("Pasos_Pisadas");
            if (footstepSoundClip == null) footstepSoundClip = Resources.Load<AudioClip>("Interruptor");
        }

        if (footstepAudioSource != null)
        {
            footstepAudioSource.clip = footstepSoundClip;
            footstepAudioSource.spatialBlend = 1f;
            footstepAudioSource.minDistance = 2.5f;  // Sonido fuerte en los primeros 2.5m
            footstepAudioSource.maxDistance = 18f;   // Se escucha tenuemente hasta 18m
            footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            footstepAudioSource.loop = false;
            footstepAudioSource.volume = 0.85f;      // Pisadas claramente audibles
        }
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrolPoints = points;
        if (agent != null && anim != null)
        {
            currentState?.ExitState();
            currentState = null;
            currentState = new EnemyPatrolState(this, agent, anim, patrolPoints);
            currentState.EnterState();
            Debug.Log("EnemyAIController: Patrol points actualizados. Total: " + patrolPoints.Length);
        }
    }

    public void ChangeState(IEnemyState newState)
    {
        if (currentState != null && currentState.GetType() == newState.GetType())
            return; // No reinicia el mismo estado
        currentState?.ExitState();
        currentState = newState;
        currentState.EnterState();
    }

    private void HandleStateTransitions()
    {
        if (player == null) return;

        bool isPlayerHidden = hideScript != null && hideScript.isHiding;
        bool canSeePlayer = fov != null && fov.CanDetectPlayer();
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (isPlayerHidden)
        {
            // LEGACY REMOVED: HospitalMazeGenerator.mapScale
            float currentMapScale = 4f;

            bool visuallySawHide = fov != null && fov.CanSeePlayer();
            if (currentState is EnemyChaseState && distanceToPlayer <= (5f * currentMapScale) && visuallySawHide)
            {
                Debug.Log("El enemigo te vio esconderte!");
                ChangeState(new EnemyAttackState(this, agent, anim, player));
            }
            else if (!(currentState is EnemyPatrolState))
            {
                Debug.Log("Jugador escondido. Volviendo a patrulla.");
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                ChangeState(new EnemyPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
            }
            return;
        }

        if (canSeePlayer)
        {
            if (distanceToPlayer <= attackRange)
            {
                ChangeState(new EnemyAttackState(this, agent, anim, player));
            }
            else if (currentState is EnemyAttackState)
            {
                // Si el jugador escapa durante el ataque, esperar a que el estado de ataque termine (o se aleje a más de 1.8x el attackRange)
                if (distanceToPlayer > attackRange * 1.8f)
                {
                    ChangeState(new EnemyChaseState(this, agent, anim, player));
                }
            }
            else if (!(currentState is EnemyChaseState))
            {
                ChangeState(new EnemyChaseState(this, agent, anim, player));
            }
        }
        else
        {
            // Si no detectamos al jugador y no estamos en persecución ni patrulla, forzar patrulla.
            // La transición de salida de persecución (ChaseState -> PatrolState) la maneja internamente ChaseState cuando expira el temporizador de búsqueda.
            if (!(currentState is EnemyChaseState) && !(currentState is EnemyPatrolState) && !(currentState is EnemyCrouchInspectState) && !(currentState is EnemyAttackState))
            {
                ChangeState(new EnemyPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
            }
        }
    }

    private void HandleFootsteps()
    {
        if (footstepAudioSource == null || footstepSoundClip == null) return;

        if (agent.velocity.magnitude > 0.1f && !agent.isStopped)
        {
            if (agent.speed > walkSpeed)
            {
                if (!footstepAudioSource.isPlaying)
                {
                    footstepAudioSource.pitch = 1.5f;
                    footstepAudioSource.Play();
                }
            }
            else
            {
                if (!footstepAudioSource.isPlaying)
                {
                    footstepAudioSource.pitch = 1f;
                    footstepAudioSource.Play();
                }
            }
        }
        else if (footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Stop();
        }
    }

    // =========================================================
    // REPOSICIONAMIENTO SILENCIOSO
    // =========================================================
    private System.Collections.IEnumerator SilentRepositionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // El timer solo corre cuando el enemigo esta patrullando (no persiguiendo)
            if (!(currentState is EnemyPatrolState))
            {
                timeSincePlayerSeen = 0f;
                continue;
            }

            bool playerVisible = fov != null && fov.CanDetectPlayer();
            if (playerVisible)
            {
                timeSincePlayerSeen = 0f;
            }
            else
            {
                // SISTEMA DINÁMICO EN MAPAS GRANDES: 
                // Si el monstruo está a más de 35 metros, acumular el temporizador a 3x velocidad
                float distance = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
                if (distance > 35f)
                {
                    timeSincePlayerSeen += 3.0f; // Teletransporte acelerado (en unos 15 segundos reales se reposiciona)
                }
                else
                {
                    timeSincePlayerSeen += 1.0f;
                }

                if (timeSincePlayerSeen >= silentRepositionTime)
                {
                    TrySilentReposition();
                    timeSincePlayerSeen = 0f;
                }
            }
        }
    }

    private void TrySilentReposition()
    {
        if (player == null) return;
        
        // Evitar teletransportarse/reposicionarse si el jugador está escondido bajo la cama,
        // ya que podría aparecer mágicamente dentro de la habitación/cama cerrada rompiendo la inmersión.
        if (hideScript != null && hideScript.isHiding)
        {
            Debug.Log("[Reposicion] Jugador escondido. Cancelando teletransporte silencioso.");
            return;
        }

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
            float dist = UnityEngine.Random.Range(repositionMinRadius, repositionMaxRadius);
            // Restringir altura Y a la misma altura del piso del jugador
            Vector3 candidate = new Vector3(player.position.x + randomDir.x * dist, player.position.y, player.position.z + randomDir.z * dist);

            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                continue;

            // Verificar que el punto del NavMesh no esté elevado en techos o estructuras superiores (máximo 0.8m de diferencia vertical)
            if (Mathf.Abs(hit.position.y - player.position.y) > 0.8f) continue;

            Vector3 toCandidate = (hit.position - player.position).normalized;
            float dotWithPlayerForward = Vector3.Dot(toCandidate, player.forward);
            if (dotWithPlayerForward > 0.6f) continue; // Evitar que aparezca enfrente de la cara directamente

            if (agent.Warp(hit.position))
            {
                Debug.Log("[Reposicion] Enemigo apareció silenciosamente en " + hit.position + " (en piso a espaldas del jugador)");
                currentState?.ExitState();
                currentState = new EnemyPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]);
                currentState.EnterState();
                return;
            }
        }
        Debug.LogWarning("[Reposicion] No se encontro posicion valida.");
    }

    private void HandleHallucinations()
    {
        if (player == null) return;
        if (playerSanity == null) playerSanity = FindObjectOfType<PlayerSanity>();
        if (playerSanity == null) return;

        // Comprobamos si la cordura está por debajo del 60%
        if (playerSanity.sanity < 60f)
        {
            // Solo si el jugador NO está en combate directo / persecución inmediata
            if (currentState is EnemyChaseState || currentState is EnemyAttackState)
            {
                timeSinceLastSeenGhost = 0f;
                return;
            }

            timeSinceLastSeenGhost += Time.deltaTime;
            // Spawnea una alucinación cada 35 segundos si sigue con cordura baja
            if (timeSinceLastSeenGhost >= 35f)
            {
                timeSinceLastSeenGhost = 0f;
                TrySpawnSpectralHallucination();
            }
        }
        else
        {
            timeSinceLastSeenGhost = 0f;
        }
    }

    private void TrySpawnSpectralHallucination()
    {
        if (player == null) return;

        // Encontrar una posición NavMesh aleatoria en un rango medio (entre 12 y 25 metros)
        Vector3 randomDir = Random.insideUnitSphere * 20f;
        randomDir += player.position;
        UnityEngine.AI.NavMeshHit hit;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(randomDir, out hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
        {
            float dist = Vector3.Distance(player.position, hit.position);
            if (dist >= 10f && dist <= 28f)
            {
                Vector3 spawnPos = hit.position;
                spawnPos.y = transform.position.y; // Mantener la misma altura que el monstruo

                // Instanciar duplicado a partir de nuestro propio GameObject
                GameObject ghostObj = Instantiate(gameObject, spawnPos, Quaternion.identity);
                ghostObj.name = "BookHeadMonster_Hallucination";
                ghostObj.transform.localScale = transform.localScale;

                // Limpiar componentes de IA y movimiento para que quede estático
                EnemyAIController oldController = ghostObj.GetComponent<EnemyAIController>();
                if (oldController != null) DestroyImmediate(oldController);

                UnityEngine.AI.NavMeshAgent oldAgent = ghostObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
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

                Debug.Log("[EnemyAIController] Alucinación espectral de BookHead creada cerca del jugador.");
            }
        }
    }

    public void FleeFarFromPlayer()
    {
        if (agent == null || !agent.enabled) return;

        GameObject patrolHolder = GameObject.Find("[BookHead_Patrol_Points]");
        Vector3 farthestPos = transform.position;
        float maxDist = -1f;
        Vector3 pPos = player != null ? player.position : transform.position;

        if (patrolHolder != null)
        {
            Transform[] pts = patrolHolder.GetComponentsInChildren<Transform>();
            foreach (Transform pt in pts)
            {
                if (pt != null && pt != patrolHolder.transform)
                {
                    float d = Vector3.Distance(pt.position, pPos);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        farthestPos = pt.position;
                    }
                }
            }
        }

        agent.speed = runSpeed * 1.35f;
        agent.SetDestination(farthestPos);
        Debug.Log("BookHead: Jugador escondido debajo de cama. Retirándose rápido a punto lejano: " + farthestPos);
    }
}
