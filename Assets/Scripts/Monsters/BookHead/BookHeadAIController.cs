using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class BookHeadAIController : MonoBehaviour
{
    public Transform GetTransform()
    {
        return transform;
    }

    public void ForceRelocateFarAway(Vector3 safePlayerPos)
    {
        if (agent == null) return;
        
        Vector3 farPos = safePlayerPos - (Vector3.forward * 40f);
        
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(farPos, out hit, 60f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log("[BookHeadAIController] Relocalizado lejos del jugador en el respawn a: " + hit.position);
        }
        else
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
        
        agent.ResetPath();
        ChangeState(new BookHeadPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
    }

    public Transform player;
    public float attackRange = 2f;
    public float detectionRange = 9.5f;
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.6f;
    public Transform[] patrolPoints;

    [Header("Reposicionamiento Silencioso Invisible")]
    [Tooltip("Segundos sin ver al enemigo antes de que aparezca fuera de visión")]
    public float silentRepositionTime = 45f;
    [Tooltip("Radio maximo al que aparece el enemigo desde el jugador")]
    public float repositionMaxRadius = 26f;
    [Tooltip("Radio minimo de aparicion")]
    public float repositionMinRadius = 18f;

    [HideInInspector] public string currentDifficulty = "NORMAL";

    public void ApplyDifficultySettings()
    {
        currentDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL").ToUpper();

        if (currentDifficulty == "FACIL" || currentDifficulty == "EASY")
        {
            walkSpeed = 3.0f;
            runSpeed = 5.2f;
            detectionRange = 7.0f;
            silentRepositionTime = 40f;
            repositionMinRadius = 16f;
            repositionMaxRadius = 24f;
        }
        else if (currentDifficulty == "DIFICIL" || currentDifficulty == "HARD")
        {
            walkSpeed = 4.3f;
            runSpeed = 7.5f; // MÁS RÁPIDO QUE EL JUGADOR CORRIENDO (~6.5m/s)
            detectionRange = 12.0f;
            silentRepositionTime = 12f; // Acecha muy frecuentemente
            repositionMinRadius = 8f;
            repositionMaxRadius = 14f;
        }
        else // NORMAL
        {
            walkSpeed = 3.5f;
            runSpeed = 6.4f; // CASI IGUALA AL JUGADOR
            detectionRange = 9.5f;
            silentRepositionTime = 22f;
            repositionMinRadius = 12f;
            repositionMaxRadius = 18f;
        }

        originalWalkSpeed = walkSpeed;
        originalRunSpeed = runSpeed;
        originalDetectionRange = detectionRange;
        originalSilentRepositionTime = silentRepositionTime;

        Debug.Log($"[BookHead] Dificultad configurada: {currentDifficulty} | Vel. Caminar: {walkSpeed} | Vel. Correr: {runSpeed} | Reposición: {silentRepositionTime}s");
    }

    public void AlertNoiseAtPosition(Vector3 noisePos)
    {
        // Únicamente reacciona a ruidos/notas SI el monstruo está activo en la escena (durante un apagón)
        if (!gameObject.activeInHierarchy) return;

        float dist = Vector3.Distance(transform.position, noisePos);
        if (dist < 60f)
        {
            Debug.Log($"[BookHead] Alerta de sonido recibida en {noisePos} (distancia: {dist:F1}m). Investigando...");
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(noisePos);
                if (!(currentState is BookHeadChaseState))
                {
                    ChangeState(new BookHeadStalkState(this, agent, anim, player));
                }
            }
        }
    }

    public void OnKeycardCollected()
    {
        Debug.Log("[BookHead] ¡Tarjeta del Director obtenida! Iniciando fase clímax de escape al ascensor.");
        difficultyBoostApplied = true;

        // FASE CLÍMAX EQUILIBRADA DE ESCAPE AL ASCENSOR:
        // Reposición de acecho más frecuente (15s), ligero aumento de velocidad (+0.8m/s) y mayor audición
        silentRepositionTime = Mathf.Min(silentRepositionTime, 15f);
        runSpeed = originalRunSpeed + 0.8f;
        detectionRange = originalDetectionRange + 4f;

        if (gameObject.activeInHierarchy)
        {
            // Reubicarse sigilosamente fuera de cámara en pasillos rumbo al ascensor
            TrySilentReposition();
        }
    }

    public AudioSource audioSource;
    public AudioClip monsterSoundClip;
    public AudioSource footstepAudioSource;
    public AudioClip footstepSoundClip;

    [Header("Sonidos de Terror Dinámicos de BookHead")]
    [Tooltip("Audio de persecución (Chase.mp3)")]
    public AudioClip chaseSoundClip;
    [Tooltip("Grito aterrador a corta distancia / ataque (GritoBookHead.mp3)")]
    public AudioClip screechSoundClip;
    [Tooltip("Impacto de alerta cuando te detecta e inicia persecución (Terrifying_horror_Impact.mp3)")]
    public AudioClip impactSoundClip;

    private NavMeshAgent agent;
    private BookHeadAnimation anim;
    private IEnemyState currentState;
    private FieldOfView fov;
    private float timeSincePlayerSeen = 0f;
    private HideUnderBed hideScript;
    private RoomLightsManager roomLightsManager;
    private PlayerSanity playerSanity;
    private float timeSinceLastSeenGhost = 0f;
    [HideInInspector]
    public SprintDetector playerSprintDetector;

    private AudioClip doorKnockClip;
    private bool isKnockingDoor = false;

    private bool difficultyBoostApplied = false;
    private float originalWalkSpeed;
    private float originalRunSpeed;
    private float originalDetectionRange;
    private float originalSilentRepositionTime;

    private float accumulatedRunTime = 0f;
    private float noiseAlertCooldownTimer = 0f;
    private float screechCooldownTimer = 0f;

    void Start()
    {
        Debug.Log("Start del enemigo iniciado");
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
        anim = GetComponent<BookHeadAnimation>();
        if (anim == null) anim = GetComponentInChildren<BookHeadAnimation>();
        fov = GetComponent<FieldOfView>();
        if (fov == null) fov = GetComponentInChildren<FieldOfView>();
        if (fov != null)
        {
            fov.player = player;
        }

        if (agent != null)
        {
            agent.agentTypeID = 0; // Humanoid por defecto
            agent.height = 2.1f;   // Corregir altura de 9.73m a 2.1m
            agent.radius = 0.50f;
            agent.stoppingDistance = 1.6f;
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }

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

        GameObject scenePlayer = GameObject.FindGameObjectWithTag("Player");
        if (scenePlayer != null)
        {
            player = scenePlayer.transform;
        }
        else
        {
            if (player == null || player.gameObject.scene.name == null)
            {
                GameObject foundPlayer = GameObject.Find("PlayerCapsule");
                if (foundPlayer == null) foundPlayer = GameObject.Find("Player");
                if (foundPlayer == null)
                {
                    var fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
                    if (fpc != null) foundPlayer = fpc.gameObject;
                }
                
                if (foundPlayer != null) player = foundPlayer.transform;
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

        originalWalkSpeed = walkSpeed;
        originalRunSpeed = runSpeed;
        originalDetectionRange = detectionRange;
        originalSilentRepositionTime = silentRepositionTime;

        ApplyDifficultySettings();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupAudio();

        if (player != null)
        {
            playerSprintDetector = player.GetComponent<SprintDetector>();
            if (playerSprintDetector == null) playerSprintDetector = player.GetComponentInChildren<SprintDetector>();
            if (playerSprintDetector == null)
            {
                playerSprintDetector = player.gameObject.AddComponent<SprintDetector>();
            }
        }

        hideScript = FindFirstObjectByType<HideUnderBed>();
        roomLightsManager = FindFirstObjectByType<RoomLightsManager>();
        playerSanity = FindFirstObjectByType<PlayerSanity>();

        if (patrolPoints == null) patrolPoints = new Transform[0];

        ChangeState(new BookHeadPatrolState(this, agent, anim, patrolPoints));

        StartCoroutine(SilentRepositionRoutine());
        StartCoroutine(PsychologicalDoorKnockRoutine());
    }

    void OnEnable()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
        if (agent != null)
        {
            agent.agentTypeID = 0;
            agent.height = 2.1f;
            agent.radius = 0.5f;
            agent.stoppingDistance = 1.6f;
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            StartCoroutine(RestartPatrolDelayed());
        }
    }

    private System.Collections.IEnumerator RestartPatrolDelayed()
    {
        yield return null;
        yield return null;
        yield return null;

        if (agent == null || anim == null) yield break;
        if (!agent.isOnNavMesh)
        {
            float floorY = transform.position.y;
            Vector3 testOrigin = new Vector3(transform.position.x, floorY, transform.position.z);

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testOrigin, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                yield return null;
            }
            else
            {
                yield break;
            }
        }

        currentState?.ExitState();
        currentState = null;
        currentState = new BookHeadPatrolState(this, agent, anim, patrolPoints);
        currentState.EnterState();
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;

        currentState?.UpdateState();
        HandleStateTransitions();
        HandleFootsteps();
        HandleHallucinations();

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

        float timeFactor = Time.timeSinceLevelLoad / 300f;
        timeFactor = Mathf.Clamp01(timeFactor);

        float extraSpeed = (activeGens * 0.25f) + (repairsUsed * 0.2f) + (timeFactor * 0.4f);

        if (ElevatorController.hasKeycard)
        {
            extraSpeed += 0.8f;
            
            if (!difficultyBoostApplied)
            {
                difficultyBoostApplied = true;
                silentRepositionTime = 25f;
            }
        }

        walkSpeed = Mathf.Min(originalWalkSpeed + (extraSpeed * 0.35f), originalWalkSpeed + 1.2f);
        runSpeed = Mathf.Min(originalRunSpeed + (extraSpeed * 0.8f), originalRunSpeed + 1.8f);

        detectionRange = Mathf.Min(originalDetectionRange + (activeGens * 1.5f) + (repairsUsed * 1f) + (timeFactor * 2f), originalDetectionRange + 6f);
        if (ElevatorController.hasKeycard)
        {
            detectionRange += 3f;
            detectionRange = Mathf.Min(detectionRange, originalDetectionRange + 30f);
        }

        bool isDark = roomLightsManager != null && roomLightsManager.powerOutage;

        if (currentState is BookHeadAttackState || currentState is BookHeadStalkState)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.speed = 0f;
        }
        else if (isDark)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.speed = (currentState is BookHeadChaseState) ? (runSpeed + 0.4f) : (walkSpeed + 0.3f);
            if (fov != null) fov.viewRadius = difficultyBoostApplied ? 15f : 12f;
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.speed = (currentState is BookHeadChaseState) ? runSpeed : walkSpeed;
            if (fov != null) fov.viewRadius = difficultyBoostApplied ? 12f : 9f;
        }

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
        }

        if (noiseAlertCooldownTimer > 0f)
        {
            noiseAlertCooldownTimer -= Time.deltaTime;
        }

        if (screechCooldownTimer > 0f)
        {
            screechCooldownTimer -= Time.deltaTime;
        }

        if (playerSprintDetector != null && playerSprintDetector.IsRunning)
        {
            accumulatedRunTime += Time.deltaTime;
        }
        else
        {
            accumulatedRunTime = 0f;
        }

        if (fov != null)
        {
            if (accumulatedRunTime >= 0.5f && noiseAlertCooldownTimer <= 0f)
            {
                fov.hearingRadius = difficultyBoostApplied ? 65f : 50f;
                noiseAlertCooldownTimer = 10f;
            }
            else if (accumulatedRunTime < 0.5f)
            {
                fov.hearingRadius = difficultyBoostApplied ? 6f : 4f;
            }
        }

        // ─── CONTROL DE AUDIO DINÁMICO DE TERROR Y PERSECUCIÓN ────────────────
        if (player != null && audioSource != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            if (currentState is BookHeadChaseState)
            {
                // 1. MÚSICA DE PERSECUCIÓN DE FONDO EN LOOP (Chase.mp3)
                if (chaseSoundClip != null)
                {
                    if (audioSource.clip != chaseSoundClip)
                    {
                        audioSource.clip = chaseSoundClip;
                        audioSource.loop = true;
                        audioSource.spatialBlend = 1f;
                        audioSource.minDistance = 3f;
                        audioSource.maxDistance = 30f;
                        audioSource.pitch = 1.0f; // Pitch normal para la música de persecución
                        audioSource.Play();
                    }

                    float chaseVol = (1f - (dist / 28f)) * 0.85f;
                    audioSource.volume = Mathf.MoveTowards(audioSource.volume, Mathf.Clamp01(chaseVol), Time.deltaTime * 3.0f);
                }

                // 2. RÁFAGAS ESPORÁDICAS DE GRITO DEMONÍACO GRAVE (<15.0m) (GritoBookHead.mp3)
                if (screechSoundClip != null && dist <= 15.0f && screechCooldownTimer <= 0f)
                {
                    screechCooldownTimer = Random.Range(4.5f, 7.5f); // Cooldown entre gritos (evita repetición constante)

                    GameObject screechObj = new GameObject("BookHead_ScreechBurst");
                    screechObj.transform.position = transform.position;
                    AudioSource sSource = screechObj.AddComponent<AudioSource>();
                    sSource.clip = screechSoundClip;
                    sSource.spatialBlend = 1f;
                    sSource.minDistance = 3.5f;
                    sSource.maxDistance = 28f;
                    sSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    sSource.pitch = Random.Range(0.85f, 0.90f); // PITCH GRAVE DEMONÍACO (Elimina tono agudo)
                    sSource.volume = 0.95f;
                    sSource.Play();
                    Destroy(screechObj, screechSoundClip.length + 0.5f);

                    Debug.Log($"[BookHead] ¡Grito demoníaco lanzado a {dist:F1}m del jugador! Próximo grito en {screechCooldownTimer:F1}s.");
                }
            }
            else if (currentState is BookHeadAttackState)
            {
                if (screechSoundClip != null && screechCooldownTimer <= 0f)
                {
                    screechCooldownTimer = 3.0f;
                    GameObject screechObj = new GameObject("BookHead_AttackScreech");
                    screechObj.transform.position = transform.position;
                    AudioSource sSource = screechObj.AddComponent<AudioSource>();
                    sSource.clip = screechSoundClip;
                    sSource.spatialBlend = 1f;
                    sSource.minDistance = 3f;
                    sSource.maxDistance = 20f;
                    sSource.pitch = 0.85f;
                    sSource.volume = 1.0f;
                    sSource.Play();
                    Destroy(screechObj, screechSoundClip.length + 0.5f);
                }
            }
            else
            {
                if (audioSource.clip == chaseSoundClip)
                {
                    audioSource.volume = Mathf.MoveTowards(audioSource.volume, 0f, Time.deltaTime * 2.0f);
                    if (audioSource.volume <= 0.01f)
                    {
                        audioSource.pitch = 1.0f;
                        if (monsterSoundClip != null)
                        {
                            audioSource.clip = monsterSoundClip;
                            audioSource.loop = true;
                            audioSource.spatialBlend = 1f;
                            audioSource.minDistance = 3f;
                            audioSource.maxDistance = 22f;
                            audioSource.volume = 0.50f;
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

        if (currentState is BookHeadChaseState)
        {
            CheckAndOpenObstacles();
        }
    }

    public void PlaySpottingImpact()
    {
        if (impactSoundClip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(impactSoundClip, pos, 0.95f);
        }
    }

    public static bool IsDirectorOfficeDoor(GameObject doorObj)
    {
        if (doorObj == null) return false;
        string n = doorObj.name.ToLower();
        if (n.Contains("director") || n.Contains("keypad") || n.Contains("puertadirector") || n.Contains("oficinadirector")) return true;

        Transform parent = doorObj.transform.parent;
        while (parent != null)
        {
            string pName = parent.name.ToLower();
            if (pName.Contains("director") || pName.Contains("keypad") || pName.Contains("directoroffice")) return true;
            parent = parent.parent;
        }
        return false;
    }

    private void CheckAndOpenObstacles()
    {
        if (isKnockingDoor) return;

        // Detección de esfera precisa en 2.8m para detectar cualquier puerta en el camino sin fallar
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position + Vector3.up * 1.0f, 2.8f);
        foreach (Collider col in nearbyCols)
        {
            if (col == null) continue;

            ProceduralDoorInteract procDoor = col.GetComponentInParent<ProceduralDoorInteract>();
            if (procDoor == null) procDoor = col.GetComponent<ProceduralDoorInteract>();

            OpenDoor animDoor = col.GetComponentInParent<OpenDoor>();
            if (animDoor == null) animDoor = col.GetComponent<OpenDoor>();

            if (procDoor != null && !procDoor.isOpen)
            {
                if (procDoor.isLocked || IsDirectorOfficeDoor(procDoor.gameObject)) continue;

                StartCoroutine(KnockAndOpenDoorRoutine(procDoor, null, procDoor.gameObject));
                break;
            }
            else if (animDoor != null)
            {
                if (animDoor.isLocked || IsDirectorOfficeDoor(animDoor.gameObject)) continue;

                StartCoroutine(KnockAndOpenDoorRoutine(null, animDoor, animDoor.gameObject));
                break;
            }
        }
    }

    private System.Collections.IEnumerator PsychologicalDoorKnockRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(14f, 24f));

            if (isKnockingDoor || player == null) continue;

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            // Si BookHead está en el área cercana al jugador (10 - 25m)
            if (distToPlayer <= 25f && (currentState is BookHeadPatrolState || currentState is BookHeadStalkState))
            {
                // Buscar puertas cerca del jugador para dar golpes terroríficos aleatorios
                Collider[] nearbyDoors = Physics.OverlapSphere(player.position, 12f);
                List<GameObject> validDoors = new List<GameObject>();

                foreach (var col in nearbyDoors)
                {
                    if (col == null) continue;
                    ProceduralDoorInteract pDoor = col.GetComponentInParent<ProceduralDoorInteract>();
                    if (pDoor != null && !pDoor.isLocked && !IsDirectorOfficeDoor(pDoor.gameObject) && !validDoors.Contains(pDoor.gameObject))
                    {
                        validDoors.Add(pDoor.gameObject);
                    }
                }

                if (validDoors.Count > 0)
                {
                    GameObject chosenDoor = validDoors[UnityEngine.Random.Range(0, validDoors.Count)];
                    if (doorKnockClip == null)
                    {
                        doorKnockClip = Resources.Load<AudioClip>("Audio/Compartido/tocar-la-puerta");
                        if (doorKnockClip == null) doorKnockClip = Resources.Load<AudioClip>("tocar-la-puerta");
                    }

                    if (doorKnockClip != null)
                    {
                        AudioSource.PlayClipAtPoint(doorKnockClip, chosenDoor.transform.position, 0.95f);
                        Debug.Log($"[BookHead] Evento de Terror Psicológico: Golpes en la puerta cerca del jugador en '{chosenDoor.name}'.");
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator KnockAndOpenDoorRoutine(ProceduralDoorInteract procDoor, OpenDoor animDoor, GameObject doorObj)
    {
        isKnockingDoor = true;

        if (doorKnockClip == null)
        {
            doorKnockClip = Resources.Load<AudioClip>("Audio/Compartido/tocar-la-puerta");
            if (doorKnockClip == null) doorKnockClip = Resources.Load<AudioClip>("tocar-la-puerta");
        }

        if (audioSource != null && doorKnockClip != null)
        {
            audioSource.PlayOneShot(doorKnockClip, 0.95f);
        }

        yield return new WaitForSeconds(0.40f);

        if (procDoor != null)
        {
            if (procDoor.isLocked || IsDirectorOfficeDoor(procDoor.gameObject))
            {
                isKnockingDoor = false;
                yield break;
            }

            float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : transform.rotation);
            if (angleDiff < 10f || doorObj.name.Contains("Puerta_Panel"))
            {
                procDoor.ToggleDoor();
                Debug.Log("[BookHead] Tocó la puerta y la empujó con impacto.");
            }
        }

        if (animDoor != null)
        {
            if (animDoor.isLocked || IsDirectorOfficeDoor(animDoor.gameObject))
            {
                isKnockingDoor = false;
                yield break;
            }

            if (animDoor.doorAnimator != null && !animDoor.doorAnimator.GetBool("isOpen"))
            {
                animDoor.doorAnimator.SetBool("isOpen", true);
                if (animDoor.audioSource && animDoor.doorOpenSound)
                {
                    animDoor.audioSource.PlayOneShot(animDoor.doorOpenSound, 1.0f);
                }
            }
        }

        yield return new WaitForSeconds(0.60f);
        isKnockingDoor = false;
    }

    private void SetupAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (chaseSoundClip == null)
        {
            chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Chase");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Chase");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Persecusion");
        }

        if (screechSoundClip == null)
        {
            screechSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/GritoBookHead");
            if (screechSoundClip == null) screechSoundClip = Resources.Load<AudioClip>("GritoBookHead");
        }

        if (impactSoundClip == null)
        {
            impactSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Terrifying_horror_Impact");
            if (impactSoundClip == null) impactSoundClip = Resources.Load<AudioClip>("Terrifying_horror_Impact");
        }

        if (doorKnockClip == null)
        {
            doorKnockClip = Resources.Load<AudioClip>("Audio/Compartido/tocar-la-puerta");
            if (doorKnockClip == null) doorKnockClip = Resources.Load<AudioClip>("tocar-la-puerta");
        }

        if (audioSource != null && monsterSoundClip != null)
        {
            audioSource.clip = monsterSoundClip;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 22f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.volume = 0.55f;
            audioSource.Play();
        }

        if (footstepAudioSource == null)
        {
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
        }

        if (footstepAudioSource != null)
        {
            footstepAudioSource.clip = footstepSoundClip;
            footstepAudioSource.spatialBlend = 1f;
            footstepAudioSource.minDistance = 2.5f;
            footstepAudioSource.maxDistance = 18f;
            footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            footstepAudioSource.loop = false;
            footstepAudioSource.volume = 0.85f;
        }
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrolPoints = points;
        if (agent != null && anim != null)
        {
            currentState?.ExitState();
            currentState = null;
            currentState = new BookHeadPatrolState(this, agent, anim, patrolPoints);
            currentState.EnterState();
        }
    }

    public void ChangeState(IEnemyState newState)
    {
        if (currentState != null && currentState.GetType() == newState.GetType())
            return;

        // Disparar impacto sonoro de susto instantáneo al iniciar persecución
        if (newState is BookHeadChaseState && !(currentState is BookHeadChaseState) && !(currentState is BookHeadAttackState))
        {
            PlaySpottingImpact();
        }

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
            float currentMapScale = 4f;

            bool visuallySawHide = fov != null && fov.CanSeePlayer();
            if (currentState is BookHeadChaseState && distanceToPlayer <= (5f * currentMapScale) && visuallySawHide)
            {
                ChangeState(new BookHeadAttackState(this, agent, anim, player));
            }
            else if (!(currentState is BookHeadPatrolState))
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                ChangeState(new BookHeadPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
            }
            return;
        }

        if (canSeePlayer)
        {
            if (distanceToPlayer <= attackRange)
            {
                ChangeState(new BookHeadAttackState(this, agent, anim, player));
            }
            else if (distanceToPlayer <= 7.5f)
            {
                if (!(currentState is BookHeadChaseState))
                {
                    ChangeState(new BookHeadChaseState(this, agent, anim, player));
                }
            }
            else if (distanceToPlayer > 8.0f && distanceToPlayer <= 25.0f)
            {
                if (!(currentState is BookHeadChaseState) && !(currentState is BookHeadStalkState) && !(currentState is BookHeadAttackState))
                {
                    ChangeState(new BookHeadStalkState(this, agent, anim, player));
                }
            }
        }
        else
        {
            if (!(currentState is BookHeadChaseState) && !(currentState is BookHeadPatrolState) && !(currentState is BookHeadStalkState) && !(currentState is BookHeadAttackState))
            {
                ChangeState(new BookHeadPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
            }
        }
    }

    private void HandleFootsteps()
    {
        if (footstepAudioSource == null || footstepSoundClip == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

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

    private System.Collections.IEnumerator SilentRepositionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            if (!(currentState is BookHeadPatrolState))
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
                float distance = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
                if (distance > 35f)
                {
                    timeSincePlayerSeen += 3.0f;
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

    private bool IsPointInPlayerCameraView(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return (vp.x >= -0.05f && vp.x <= 1.05f && vp.y >= -0.05f && vp.y <= 1.05f && vp.z > 0f);
    }

    public void TrySilentReposition()
    {
        if (player == null) return;
        
        if (hideScript != null && hideScript.isHiding)
        {
            return;
        }

        for (int attempt = 0; attempt < 25; attempt++)
        {
            Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
            float dist = UnityEngine.Random.Range(repositionMinRadius, repositionMaxRadius);
            Vector3 candidate = new Vector3(player.position.x + randomDir.x * dist, player.position.y, player.position.z + randomDir.z * dist);

            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                continue;

            if (Mathf.Abs(hit.position.y - player.position.y) > 0.8f) continue;

            if (IsPointInPlayerCameraView(hit.position)) continue;

            if (agent != null && agent.enabled && agent.Warp(hit.position))
            {
                Debug.Log("[Reposicion Invisible] BookHead apareció fuera de pantalla a " + Vector3.Distance(hit.position, player.position).ToString("F1") + "m. Entrando en STALK.");
                ChangeState(new BookHeadStalkState(this, agent, anim, player));
                return;
            }
        }
    }

    public void FleeFarFromPlayer()
    {
        if (player == null) return;

        GameObject patrolHolder = GameObject.Find("[BookHead_Patrol_Points]");
        Vector3 bestPos = transform.position;
        float maxDist = -1f;

        if (patrolHolder != null)
        {
            Transform[] pts = patrolHolder.GetComponentsInChildren<Transform>();
            foreach (Transform pt in pts)
            {
                if (pt != null && pt != patrolHolder.transform)
                {
                    float d = Vector3.Distance(pt.position, player.position);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        bestPos = pt.position;
                    }
                }
            }
        }

        if (agent != null && agent.enabled)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(bestPos, out hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = bestPos;
            }
        }
        else
        {
            transform.position = bestPos;
        }

        ChangeState(new BookHeadPatrolState(this, agent, anim, (patrolPoints != null && patrolPoints.Length > 0) ? patrolPoints : new Transform[0]));
        Debug.Log("[BookHead] Huyendo a pasillo distante (" + maxDist.ToString("F1") + "m del jugador).");
    }

    private void HandleHallucinations()
    {
        if (player == null) return;
        if (playerSanity == null) playerSanity = FindObjectOfType<PlayerSanity>();
        if (playerSanity == null) return;

        if (playerSanity.sanity < 60f)
        {
            if (currentState is BookHeadChaseState || currentState is BookHeadAttackState)
            {
                timeSinceLastSeenGhost = 0f;
                return;
            }

            timeSinceLastSeenGhost += Time.deltaTime;
            if (timeSinceLastSeenGhost >= 35f)
            {
                timeSinceLastSeenGhost = 0f;
            }
        }
    }
}
