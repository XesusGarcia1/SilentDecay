using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlador principal de IA para "THE REBUTTAL / LA RÉPLICA".
/// </summary>
public class ReplicaAIController : MonoBehaviour
{
    public enum ReplicaPhase
    {
        F0_InertMannequin,
        F1_FirstTransformation,
        F2_AdvancedTransformation,
        F3_MonstrousForm
    }

    [Header("Estado y Fase")]
    public ReplicaPhase currentPhase = ReplicaPhase.F0_InertMannequin;
    public bool isBeingObserved = false;
    public bool isStalking = true;

    [Header("Referencias de Mallas Fases (Sub-Objetos Hijos)")]
    public GameObject modelF0_Character;
    public GameObject modelF2_Terror;
    public GameObject modelF3_Monstrous;

    [Header("Configuración de Visión y Alcance")]
    public float maxDetectionDistance = 25.0f;
    public float attackDistance = 2.5f; // Tolerancia de distancia letal (2D)
    public float startAttackAnimationDistance = 3.2f; // Reducido para que no ataque desde tan lejos
    public float relocateCooldown = 5.0f;

    [Header("Audio SFX Espacializado 3D")]
    public AudioSource loopAudioSource;      
    public AudioSource sfxAudioSource;       
    public AudioSource globalAudioSource;    

    [Header("Clips de Sonido")]
    public AudioClip audioInerteVibracion;
    public AudioClip audioAliento;
    public AudioClip audioTic1;
    public AudioClip audioTic2;
    public AudioClip audioCrujidoCuello;
    public AudioClip audioCrujidoHuesos;
    public AudioClip audioArrastrePesado;
    public AudioClip audioGritoBiomecanico;
    public AudioClip audioJumpscare;

    private Transform playerTransform;
    private Camera playerCamera;
    private NavMeshAgent navAgent;
    private float lastRelocateTime = 0f;
    private float nextBreathTime = 0f;
    private MannequinSpot currentOccupiedSpot;

    private Animator animator;
    private float twitchTimer = 0f;
    private bool isTwitching = false;
    private float twitchEndTime = 0f;

    // Métricas de control de fases y ataque
    private int relocationCount = 0;
    private bool isTransitioning = false;
    private bool hasTriggeredAttackAnimation = false;
    private bool isPlayerDead = false;
    [Header("Debug Casería (Solo Lectura)")]
    public bool isHuntingDebug = false;
    public bool playerInZoneDebug = false;
    public float distanceToPlayer = 0f;
    public int relocationsDebug = 0;
    
    [Header("Mecánicas de Supervivencia")]
    public float safeZoneTimer = 0f;
    public float stareTimer = 0f;
    private float zoneCheckTimer = 0f;

    private struct LightState
    {
        public Light light;
        public bool originalEnabled;
        public FlickeringLight flickerScript;
        public bool originalFlickerEnabled;
    }
    private List<LightState> sceneLights = new List<LightState>();

    private void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
            navAgent.speed = 5.8f;
            navAgent.angularSpeed = 180f;
            navAgent.acceleration = 15f;
            navAgent.stoppingDistance = 1.0f;
            navAgent.height = 2.0f;
            navAgent.radius = 0.5f;
        }

        // Autodetectar mallas de las fases en la jerarquía si no están asignadas
        if (modelF0_Character == null) modelF0_Character = FindChildByName(transform, "LaReplica_CharacterF0");
        if (modelF2_Terror == null) modelF2_Terror = FindChildByName(transform, "LaReplica_IdleTerrorF1");
        if (modelF3_Monstrous == null) modelF3_Monstrous = FindChildByName(transform, "LaReplica_IdleTerrorF2");

        // Alinear automáticamente el contenedor intermedio 'LaReplica' al origen (0,0,0) del padre
        Transform laReplicaContainer = transform.Find("LaReplica");
        if (laReplicaContainer != null)
        {
            laReplicaContainer.localPosition = Vector3.zero;
            laReplicaContainer.localRotation = Quaternion.identity;
        }

        // Alinear los modelos locales al origen para evitar desvíos visuales del NavMesh
        if (modelF0_Character != null) { modelF0_Character.transform.localPosition = Vector3.zero; modelF0_Character.transform.localRotation = Quaternion.identity; }
        if (modelF2_Terror != null) { modelF2_Terror.transform.localPosition = Vector3.zero; modelF2_Terror.transform.localRotation = Quaternion.identity; }
        if (modelF3_Monstrous != null) { modelF3_Monstrous.transform.localPosition = Vector3.zero; modelF3_Monstrous.transform.localRotation = Quaternion.identity; }

        SetupAudioSources();
        LoadAudioClips();
        FindPlayerReferences();

        // Alinear posición inicial al suelo
        transform.position = AlignToGround(transform.position);

        // Encontrar y ocupar el spot inicial más cercano si lo hubiera
        MannequinSpot[] spots = FindObjectsByType<MannequinSpot>(FindObjectsSortMode.None);
        foreach (MannequinSpot spot in spots)
        {
            if (spot != null && Vector3.Distance(transform.position, spot.transform.position) <= 1.5f)
            {
                currentOccupiedSpot = spot;
                spot.isOccupied = true;
                break;
            }
        }

        // Forzar agente detenido en fases iniciales (solo se teletransporta)
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
        }

        twitchTimer = Time.time + Random.Range(10f, 25f);
        nextBreathTime = Time.time + Random.Range(10f, 25f);
        UpdatePhaseVisuals();
    }

    private void UpdateAnimatorReference()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private Vector3 AlignToGround(Vector3 originalPos)
    {
        if (Physics.Raycast(originalPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3.0f))
        {
            return hit.point;
        }
        return originalPos;
    }

    private GameObject FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.gameObject.name == childName)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private void SetupAudioSources()
    {
        if (loopAudioSource == null) loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.spatialBlend = 1.0f;
        loopAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        loopAudioSource.minDistance = 1.5f;
        loopAudioSource.maxDistance = 12.0f;
        loopAudioSource.loop = true;

        if (sfxAudioSource == null) sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.spatialBlend = 1.0f;
        sfxAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        sfxAudioSource.minDistance = 2.0f;
        sfxAudioSource.maxDistance = 15.0f;

        if (globalAudioSource == null) globalAudioSource = gameObject.AddComponent<AudioSource>();
        globalAudioSource.spatialBlend = 0.0f;
    }

    private void LoadAudioClips()
    {
        if (audioInerteVibracion == null) audioInerteVibracion = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/Inerte_Vibracion");
        if (audioAliento == null) audioAliento = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/Aliento");
        if (audioTic1 == null) audioTic1 = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/Tic");
        if (audioTic2 == null) audioTic2 = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/Tic2");
        if (audioCrujidoCuello == null) audioCrujidoCuello = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/CrujidoCuello");
        if (audioCrujidoHuesos == null) audioCrujidoHuesos = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/Crujido de huesos");
        if (audioArrastrePesado == null) audioArrastrePesado = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/ArrastrePesado");
        if (audioGritoBiomecanico == null) audioGritoBiomecanico = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/GritoBiomecánico");
        if (audioJumpscare == null) audioJumpscare = Resources.Load<AudioClip>("Audio/Monstruos/TheRebuttal/jumpscareSound");

        if (audioInerteVibracion != null && loopAudioSource != null)
        {
            loopAudioSource.clip = audioInerteVibracion;
            loopAudioSource.volume = 0.35f;
            if (!loopAudioSource.isPlaying) loopAudioSource.Play();
        }
    }

    private void FindPlayerReferences()
    {
        playerCamera = Camera.main;
        if (playerCamera != null) { playerTransform = playerCamera.transform; return; }

        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { playerTransform = cc.transform; return; }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj == null) pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) playerTransform = pObj.transform;
    }

    private void Update()
    {
        if (!isStalking || isTransitioning || isPlayerDead) return;

        if (playerTransform == null || playerCamera == null)
        {
            FindPlayerReferences();
            if (playerTransform == null) return;
        }

        // F3 es la fase final: persigue agresivamente sin congelarse al mirar
        if (currentPhase == ReplicaPhase.F3_MonstrousForm)
        {
            isHuntingDebug = true;
            isBeingObserved = false;
            HandleActiveChaseBehavior();
            return;
        }
        else
        {
            isHuntingDebug = false;
        }

        CheckIfObservedByPlayer();
        HandleSafeZoneMechanic();
        HandleStareDownMechanic();
        HandleStalkingBehavior();
        HandleRandomBreathingSFX();
        HandleAnimatorTwitch();
        HandlePhaseTransitionTriggers();
    }

    private void HandleSafeZoneMechanic()
    {
        // Actualizar el estado de la zona de forma más ligera (1 vez por segundo)
        zoneCheckTimer += Time.deltaTime;
        if (zoneCheckTimer >= 1.0f)
        {
            zoneCheckTimer = 0f;
            UpdatePlayerInZoneStatus();
        }

        // Si estamos lejos de cualquier maniquí por 8 segundos y no está en F0, se resetea
        if (!playerInZoneDebug && currentPhase != ReplicaPhase.F0_InertMannequin)
        {
            safeZoneTimer += Time.deltaTime;
            if (safeZoneTimer >= 8.0f)
            {
                Debug.Log("[Replica] Jugador en Zona Segura por 8s. Reseteando a Fase 0.");
                ResetToF0();
            }
        }
        else if (playerInZoneDebug)
        {
            safeZoneTimer = 0f;
        }
    }

    private void HandleStareDownMechanic()
    {
        // Si lo miramos fijamente en F1 o F2 por 4 segundos, se resetea
        if (isBeingObserved && currentPhase != ReplicaPhase.F0_InertMannequin && currentPhase != ReplicaPhase.F3_MonstrousForm)
        {
            stareTimer += Time.deltaTime;
            if (stareTimer >= 4.0f)
            {
                Debug.Log("[Replica] Concurso de miradas ganado. Reseteando a Fase 0.");
                ResetToF0();
            }
        }
        else
        {
            stareTimer = 0f;
        }
    }

    private void ResetToF0()
    {
        if (currentOccupiedSpot != null)
        {
            currentOccupiedSpot.SetOccupiedByMonster(false);
            currentOccupiedSpot = null;
        }

        currentPhase = ReplicaPhase.F0_InertMannequin;
        relocationCount = 0;
        relocationsDebug = 0;
        isTransitioning = false;
        hasTriggeredAttackAnimation = false;
        isHuntingDebug = false;
        safeZoneTimer = 0f;
        stareTimer = 0f;

        UpdatePhaseVisuals();

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        if (sfxAudioSource != null && audioArrastrePesado != null)
        {
            sfxAudioSource.PlayOneShot(audioArrastrePesado, 0.75f);
        }
    }

    private void UpdatePlayerInZoneStatus()
    {
        MannequinSpot[] spots = FindObjectsByType<MannequinSpot>(FindObjectsSortMode.None);
        bool inZone = false;
        if (spots != null && spots.Length > 0)
        {
            foreach (var s in spots)
            {
                if (s != null && Vector3.Distance(s.transform.position, playerTransform.position) <= 25.0f)
                {
                    inZone = true;
                    break;
                }
            }
        }
        playerInZoneDebug = inZone;
    }

    private void HandleActiveChaseBehavior()
    {
        Vector3 mPos = GetActiveModelPosition();
        Vector3 pPos = playerTransform.position;
        
        // Calcular distancia en 2D (plano XZ) para ignorar la diferencia de altura de la cámara
        mPos.y = 0;
        pPos.y = 0;
        float distToPlayer = Vector3.Distance(mPos, pPos);

        bool hasLineOfSight = true;
        Vector3 sightDir = (playerTransform.position - transform.position).normalized;
        float sightDist = Vector3.Distance(transform.position, playerTransform.position);
        
        // Raycast físico para asegurar que no haya paredes entre el monstruo y el jugador
        if (Physics.Raycast(transform.position + Vector3.up * 1.2f, sightDir, out RaycastHit sightHit, sightDist, -1, QueryTriggerInteraction.Ignore))
        {
            if (sightHit.collider.gameObject != playerTransform.gameObject && 
                !sightHit.collider.transform.IsChildOf(playerTransform) &&
                sightHit.collider.gameObject != gameObject &&
                !sightHit.collider.transform.IsChildOf(transform))
            {
                hasLineOfSight = false; // Hay un obstáculo físico (pared) en medio
            }
        }

        // Movimiento físico real habilitado solo en fase de caza (F3)
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = 6.5f; 
            navAgent.SetDestination(playerTransform.position);
        }

        // Trigger de la animación de correr cuando se desplaza
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && navAgent.velocity.sqrMagnitude > 0.1f)
        {
            if (animator != null && !hasTriggeredAttackAnimation)
            {
                animator.speed = 1.2f; 
                if (animator.HasState(0, Animator.StringToHash("Run")))
                    animator.Play("Run");
                else if (animator.HasState(0, Animator.StringToHash("Corre")))
                    animator.Play("Corre");
            }
        }

        // Si está en radio de ataque (4.5m) Y no hay pared, reproducir animación de ataque
        if (distToPlayer <= startAttackAnimationDistance && hasLineOfSight)
        {
            if (!hasTriggeredAttackAnimation)
            {
                hasTriggeredAttackAnimation = true;
                if (animator != null)
                {
                    animator.speed = 1.0f;
                    if (animator.HasState(0, Animator.StringToHash("Attack")))
                        animator.Play("Attack");
                    else if (animator.HasState(0, Animator.StringToHash("Atacar")))
                        animator.Play("Atacar");
                    else
                        animator.Play("Attack"); 
                }
                
                if (globalAudioSource != null && audioGritoBiomecanico != null)
                {
                    globalAudioSource.PlayOneShot(audioGritoBiomecanico, 0.85f);
                }
            }
        }

        // Si alcanza el radio letal Y no hay pared, desatar screamer y matar
        if (distToPlayer <= attackDistance && hasLineOfSight)
        {
            TriggerJumpscareSequence();
        }
    }

    private void HandleAnimatorTwitch()
    {
        if (animator == null) UpdateAnimatorReference();
        if (animator == null) return;

        // Si es observada por el jugador, congelar la pose al instante (no se le ve mover)
        if (isBeingObserved)
        {
            animator.speed = 0f;
            isTwitching = false;
            return;
        }

        // En F0, o en F1/F2 al ser observada (gaslighting), actúa como maniquí estático con twitches ocasionales
        bool actAsMannequin = (currentPhase == ReplicaPhase.F0_InertMannequin) || isBeingObserved;
        if (actAsMannequin)
        {
            if (isTwitching)
            {
                if (Time.time >= twitchEndTime)
                {
                    isTwitching = false;
                    animator.speed = 0f; // Congelar nuevamente
                    twitchTimer = Time.time + Random.Range(15f, 35f);
                }
            }
            else
            {
                if (Time.time >= twitchTimer)
                {
                    isTwitching = true;
                    twitchEndTime = Time.time + Random.Range(1.5f, 3.5f);
                    animator.speed = 0.7f; // Animación lenta tétrica

                    string state = (Random.value > 0.5f) ? "Idle" : "Idle2";
                    animator.Play(state, 0, Random.value);

                    PlayJointClickSFX();
                }
                else
                {
                    animator.speed = 0f; // Inmóvil
                }
            }
        }
        else
        {
            // F1 y F2 cuando NO son observadas tienen animaciones activas (respirando)
            animator.speed = 1.0f;
        }
    }

    public void CheckIfObservedByPlayer()
    {
        if (playerCamera == null) return;

        Vector3 headPoint = transform.position + Vector3.up * 1.5f;
        Vector3 vp = playerCamera.WorldToViewportPoint(headPoint);

        bool insideFrustum = vp.z > 0 && vp.x >= -0.05f && vp.x <= 1.05f && vp.y >= -0.05f && vp.y <= 1.05f;

        if (!insideFrustum)
        {
            isBeingObserved = false;
            return;
        }

        Vector3 dirToHead = (headPoint - playerCamera.transform.position).normalized;
        float distToHead = Vector3.Distance(playerCamera.transform.position, headPoint);

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, dirToHead, out hit, distToHead + 0.5f))
        {
            if (hit.collider != null)
            {
                bool isMe = (hit.collider.gameObject == gameObject) ||
                            hit.collider.transform.IsChildOf(transform) ||
                            transform.IsChildOf(hit.collider.transform);

                if (isMe)
                {
                    isBeingObserved = true;
                    return;
                }
            }
        }

        isBeingObserved = false;
    }

    private void HandleStalkingBehavior()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        if (isBeingObserved) return;

        if (Time.time >= lastRelocateTime + relocateCooldown)
        {
            float distToPlayer = Vector3.Distance(GetActiveModelPosition(), playerTransform.position);
            
            if (distToPlayer > 4.0f)
            {
                RelocateToBestMannequinSpot();
            }
        }
    }

    private void RelocateToBestMannequinSpot()
    {
        lastRelocateTime = Time.time;
        relocationCount++;

        MannequinSpot[] spots = FindObjectsByType<MannequinSpot>(FindObjectsSortMode.None);
        
        if (spots != null && spots.Length > 0)
        {
            // Si el jugador está en los Backrooms (lejos de todos los maniquíes), quédate congelado
            if (!playerInZoneDebug)
            {
                relocationCount--; // Deshacer el contador para no avanzar de fase en la nada
                relocationsDebug = relocationCount;
                return;
            }

            MannequinSpot bestSpot = null;
            float bestDist = 999f;

            foreach (MannequinSpot spot in spots)
            {
                if (spot == null || spot.isOccupied) continue;

                Vector3 vp = playerCamera.WorldToViewportPoint(spot.transform.position + Vector3.up * 1.5f);
                bool spotVisible = vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

                if (spotVisible) continue;

                float distToPlayer = Vector3.Distance(spot.transform.position, playerTransform.position);
                if (distToPlayer < bestDist && distToPlayer >= 1.8f)
                {
                    bestDist = distToPlayer;
                    bestSpot = spot;
                }
            }

            if (bestSpot != null)
            {
                if (currentOccupiedSpot != null)
                {
                    currentOccupiedSpot.SetOccupiedByMonster(false);
                }

                Vector3 groundPos = bestSpot.transform.position;
                if (NavMesh.SamplePosition(groundPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    groundPos = hit.position;
                }
                WarpToPosition(groundPos);

                currentOccupiedSpot = bestSpot;
                bestSpot.SetOccupiedByMonster(true);
                return;
            }

            // Si estamos en la zona de maniquíes pero todos están a la vista, fuerza un teleport sigiloso
            // para que no se quede atascado y siga cazando
            TeleportStealthilyOnNavMesh();
        }
    }
    private void TeleportStealthilyOnNavMesh()
    {
        if (playerTransform == null || playerCamera == null) return;

        if (currentOccupiedSpot != null)
        {
            currentOccupiedSpot.SetOccupiedByMonster(false);
            currentOccupiedSpot = null;
        }

        for (int i = 0; i < 15; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(3.5f, 10.0f);

            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            Vector3 targetPos = playerTransform.position + offset;

            Vector3 vp = playerCamera.WorldToViewportPoint(targetPos + Vector3.up * 1.5f);
            bool isVisible = vp.z > 0 && vp.x >= -0.1f && vp.x <= 1.1f && vp.y >= -0.1f && vp.y <= 1.1f;

            if (!isVisible)
            {
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    Vector3 groundPos = hit.position;
                    
                    Vector3 vpGround = playerCamera.WorldToViewportPoint(groundPos + Vector3.up * 1.5f);
                    bool groundVisible = vpGround.z > 0 && vpGround.x >= -0.05f && vpGround.x <= 1.05f && vpGround.y >= -0.05f && vpGround.y <= 1.05f;
                    
                    if (!groundVisible)
                    {
                        WarpToPosition(groundPos);
                        return;
                    }
                }
            }
        }
    }

    private void WarpToPosition(Vector3 targetPos)
    {
        if (navAgent != null)
        {
            navAgent.enabled = false;
            transform.position = targetPos;
            navAgent.enabled = true;
        }
        else
        {
            transform.position = targetPos;
        }

        Vector3 lookAtPlayer = playerTransform.position - transform.position;
        lookAtPlayer.y = 0f;
        if (lookAtPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookAtPlayer);
        }

        PlayJointClickSFX();

        relocateCooldown = Random.Range(3.5f, 7.5f);
    }

    private void HandlePhaseTransitionTriggers()
    {
        float distToPlayer = Vector3.Distance(GetActiveModelPosition(), playerTransform.position);
        distanceToPlayer = distToPlayer; // Debug
        relocationsDebug = relocationCount; // Debug

        // CLIMAX DE MUERTE REAL (Transición F2 -> F3 a corta distancia delante del jugador)
        if (currentPhase == ReplicaPhase.F2_AdvancedTransformation && distToPlayer <= 9.0f && isBeingObserved)
        {
            StartCoroutine(PerformPhaseTransition(ReplicaPhase.F3_MonstrousForm));
            return;
        }

        // Las otras transiciones ocurren cuando NO es observado
        if (isBeingObserved) return;

        // F0 -> F1: Ocurre tras 2 teletransportes cuando está a menos de 15 metros
        if (currentPhase == ReplicaPhase.F0_InertMannequin && relocationCount >= 2 && distToPlayer <= 15.0f)
        {
            StartCoroutine(PerformPhaseTransition(ReplicaPhase.F1_FirstTransformation));
        }
        // F1 -> F2: Ocurre tras 4 teletransportes cuando está a menos de 10 metros
        else if (currentPhase == ReplicaPhase.F1_FirstTransformation && relocationCount >= 4 && distToPlayer <= 10.0f)
        {
            StartCoroutine(PerformPhaseTransition(ReplicaPhase.F2_AdvancedTransformation));
        }
    }

    private IEnumerator PerformPhaseTransition(ReplicaPhase nextPhase)
    {
        isTransitioning = true;
        
        // Encontrar y preparar las luces
        List<Light> targetLights = new List<Light>();
        List<FlickeringLight> flickerScripts = new List<FlickeringLight>();
        
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l != null)
            {
                if (l.type == LightType.Directional || Vector3.Distance(transform.position, l.transform.position) <= 30.0f)
                {
                    targetLights.Add(l);
                    FlickeringLight fl = l.GetComponent<FlickeringLight>();
                    if (fl != null) flickerScripts.Add(fl);
                }
            }
        }

        foreach (FlickeringLight fl in flickerScripts)
        {
            if (fl != null) fl.enabled = false;
        }

        // TRANSICIÓN AGRESIVA E INSTANTE PARA F3 (Clímax de Muerte de frente al jugador)
        if (nextPhase == ReplicaPhase.F3_MonstrousForm)
        {
            foreach (Light l in targetLights) if (l != null) l.enabled = false;
            
            if (sfxAudioSource != null && audioCrujidoHuesos != null) sfxAudioSource.PlayOneShot(audioCrujidoHuesos, 1.0f);
            if (sfxAudioSource != null && audioCrujidoCuello != null) sfxAudioSource.PlayOneShot(audioCrujidoCuello, 1.0f);
            
            yield return new WaitForSeconds(0.4f); 

            currentPhase = nextPhase;
            UpdatePhaseVisuals();
            if (animator != null) animator.speed = 1.0f;
            yield return new WaitForSeconds(0.1f);

            foreach (Light l in targetLights) if (l != null) l.enabled = true;
            foreach (FlickeringLight fl in flickerScripts) if (fl != null) fl.enabled = true;

            if (globalAudioSource != null && audioGritoBiomecanico != null)
            {
                globalAudioSource.PlayOneShot(audioGritoBiomecanico, 1.0f);
            }

            isTransitioning = false;
            yield break; 
        }

        // --- TRANSICIONES LENTAS Y CALMADAS DE SPRINT (F0 -> F1 -> F2) ---
        for (int i = 0; i < 4; i++)
        {
            foreach (Light l in targetLights) if (l != null) l.enabled = false;
            if (sfxAudioSource != null && audioTic1 != null) sfxAudioSource.PlayOneShot(audioTic1, 0.45f);
            yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));
            
            foreach (Light l in targetLights) if (l != null) l.enabled = true;
            yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
        }

        foreach (Light l in targetLights) if (l != null) l.enabled = false;
        yield return new WaitForSeconds(0.8f);

        if (sfxAudioSource != null && audioCrujidoHuesos != null)
        {
            sfxAudioSource.pitch = Random.Range(0.8f, 1.0f);
            sfxAudioSource.PlayOneShot(audioCrujidoHuesos, 0.85f);
        }
        yield return new WaitForSeconds(2.0f); 

        if (sfxAudioSource != null && audioAliento != null)
        {
            sfxAudioSource.PlayOneShot(audioAliento, 0.5f);
        }
        yield return new WaitForSeconds(1.5f);

        if (sfxAudioSource != null && audioCrujidoCuello != null)
        {
            sfxAudioSource.pitch = Random.Range(0.75f, 0.95f);
            sfxAudioSource.PlayOneShot(audioCrujidoCuello, 0.9f);
        }
        yield return new WaitForSeconds(1.8f);

        currentPhase = nextPhase;
        UpdatePhaseVisuals();

        yield return new WaitForSeconds(0.6f);

        for (int i = 0; i < 2; i++)
        {
            foreach (Light l in targetLights) if (l != null) l.enabled = true;
            yield return new WaitForSeconds(0.15f);
            foreach (Light l in targetLights) if (l != null) l.enabled = false;
            yield return new WaitForSeconds(0.25f);
        }

        foreach (Light l in targetLights) if (l != null) l.enabled = true;
        foreach (FlickeringLight fl in flickerScripts)
        {
            if (fl != null) fl.enabled = true;
        }

        isTransitioning = false;
    }

    private void TriggerJumpscareSequence()
    {
        if (isPlayerDead) return;
        isPlayerDead = true;
        isStalking = false;
        
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Resetear y congelar la animación para evitar que el esqueleto se vaya lejos por Root Motion visual
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.speed = 0f;
        }

        if (globalAudioSource != null)
        {
            globalAudioSource.spatialBlend = 0.0f;
            if (audioJumpscare != null) globalAudioSource.PlayOneShot(audioJumpscare, 1.0f);
            if (audioGritoBiomecanico != null) globalAudioSource.PlayOneShot(audioGritoBiomecanico, 1.0f);
        }

        PlayerHealth health = playerTransform.GetComponentInParent<PlayerHealth>();
        if (health == null) health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(99999);
            Debug.Log("[ReplicaAIController]: Muerte del jugador disparada hacia PlayerHealth.");
        }
        else
        {
            Debug.LogError("[ReplicaAIController]: No se encontro PlayerHealth para aplicar el daño.");
        }
    }

    // --- AGREGADO: DETECCION FISICA POR COLISIÓN / TRIGGER DE SEGURIDAD ---
    private void OnTriggerEnter(Collider other)
    {
        if (isPlayerDead || currentPhase != ReplicaPhase.F3_MonstrousForm) return;

        // Si colisiona con el jugador, activar screamer al instante sin importar la coordenada de desfase
        if (other.CompareTag("Player") || other.gameObject.name.Contains("Player") || other.GetComponent<PlayerHealth>() != null || other.GetComponentInParent<PlayerHealth>() != null)
        {
            Debug.Log("[ReplicaAIController]: Colisión física detectada por TriggerEnter. Activando jumpscare...");
            TriggerJumpscareSequence();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isPlayerDead || currentPhase != ReplicaPhase.F3_MonstrousForm) return;

        if (collision.gameObject.CompareTag("Player") || collision.gameObject.name.Contains("Player") || collision.gameObject.GetComponent<PlayerHealth>() != null || collision.gameObject.GetComponentInParent<PlayerHealth>() != null)
        {
            Debug.Log("[ReplicaAIController]: Colisión física detectada por CollisionEnter. Activando jumpscare...");
            TriggerJumpscareSequence();
        }
    }

    private void PlayJointClickSFX()
    {
        if (sfxAudioSource == null) return;
        AudioClip clipToPlay = (Random.value > 0.5f) ? audioTic1 : audioTic2;
        if (clipToPlay != null)
        {
            sfxAudioSource.pitch = Random.Range(0.85f, 1.15f);
            sfxAudioSource.PlayOneShot(clipToPlay, Random.Range(0.4f, 0.7f));
        }
    }

    private void HandleRandomBreathingSFX()
    {
        if (Time.time >= nextBreathTime)
        {
            nextBreathTime = Time.time + Random.Range(15f, 30f);
            if (audioAliento != null && sfxAudioSource != null)
            {
                sfxAudioSource.PlayOneShot(audioAliento, 0.4f);
            }
        }
    }

    public void UpdatePhaseVisuals()
    {
        bool showF0 = (currentPhase == ReplicaPhase.F0_InertMannequin || currentPhase == ReplicaPhase.F1_FirstTransformation);
        
        if (isBeingObserved && (currentPhase == ReplicaPhase.F1_FirstTransformation || currentPhase == ReplicaPhase.F2_AdvancedTransformation))
        {
            showF0 = true;
        }

        if (modelF0_Character != null) modelF0_Character.SetActive(showF0);
        if (modelF2_Terror != null) modelF2_Terror.SetActive(!showF0 && currentPhase == ReplicaPhase.F2_AdvancedTransformation);
        if (modelF3_Monstrous != null) modelF3_Monstrous.SetActive(!showF0 && currentPhase == ReplicaPhase.F3_MonstrousForm);

        UpdateAnimatorReference();
    }

    private Vector3 GetActiveModelPosition()
    {
        if (currentPhase == ReplicaPhase.F3_MonstrousForm && modelF3_Monstrous != null && modelF3_Monstrous.activeInHierarchy)
        {
            return modelF3_Monstrous.transform.position;
        }
        if (currentPhase == ReplicaPhase.F2_AdvancedTransformation && modelF2_Terror != null && modelF2_Terror.activeInHierarchy)
        {
            return modelF2_Terror.transform.position;
        }
        if (modelF0_Character != null && modelF0_Character.activeInHierarchy)
        {
            return modelF0_Character.transform.position;
        }
        return transform.position;
    }

    public void ResetToInitialState()
    {
        isPlayerDead = false;
        isStalking = true;
        currentPhase = ReplicaPhase.F0_InertMannequin;
        isTransitioning = false;
        hasTriggeredAttackAnimation = false;
        relocationCount = 0;
        
        UpdatePhaseVisuals();
        
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        RelocateToBestMannequinSpot();
    }
}

