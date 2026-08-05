using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CrawlerAI : MonoBehaviour
{
    [Header("Ajustes de Acecho y Movimiento")]
    [Tooltip("Velocidad de caminata/arrastre sigiloso (Lenta y aterradora)")]
    public float walkSpeed = 1.35f;
    [Tooltip("Velocidad al perseguir al jugador en la oscuridad (Rápida e intensa)")]
    public float chaseSpeed = 3.25f;
    [Tooltip("Velocidad al huir hacia las sombras")]
    public float fleeSpeed = 3.0f;
    [Tooltip("Distancia a la que empieza a perseguir al jugador en la oscuridad")]
    public float chaseDistance = 10.0f;
    [Tooltip("Distancia mínima al jugador para afectar su cordura")]
    public float sanityEffectRadius = 10.0f;
    [Tooltip("Pérdida de cordura por segundo al estar cerca del Rastrero")]
    public float sanityDrainRate = 12.0f;

    [Header("Detección de Luz")]
    [Tooltip("Si la linterna del jugador lo alumbra directamente a esta distancia, huye hacia la sombra")]
    public float flashlightFleeDistance = 12.0f;

    [Header("Efectos de Audio de Terror (Auto-cargados desde Resources)")]
    public AudioClip arrastreSound;
    public AudioClip rugidoSound;
    public AudioClip aullidoSound;
    public AudioClip pisadasCercanasSound;
    public AudioClip pisadasLejanasSound;
    public AudioClip atackSound;

    [Header("Ajustes de Ataque / Mordisco")]
    [Tooltip("Tiempo de enfriamiento en segundos entre cada mordisco de ataque")]
    public float attackBiteCooldown = 1.0f;
    [Tooltip("Daño infligido a la salud del jugador por cada mordisco")]
    public float biteDamage = 25.0f;
    private float attackBiteTimer = 0f;

    [Header("Sonido de Persecución/Tensión")]
    [Tooltip("AudioClip asignado para el momento de persecución (ej. Persecusion)")]
    public AudioClip chaseSoundClip;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private FlashlightController playerFlashlight;
    private PlayerSanity playerSanity;
    private AudioSource spatialAudioSource;
    private AudioSource roarAudioSource;
    private AudioSource chaseAudioSource;
    private AudioSource heartbeatAudioSource;
    private AudioClip heartbeatClip;

    private List<Vector3> perimeterWaypoints = new List<Vector3>();
    private int currentWaypointIdx = 0;
    private bool isFleeing = false;
    private float fleeTimer = 0f;
    private float stepAudioTimer = 0f;
    private float chaseRoarTimer = 0f;

    [Header("Sistema Anti-Estancamiento (Anti-Stuck)")]
    private float stuckTimer = 0f;
    private float stuckTimeout = 4.5f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Asegurar que el MeshCollider asignado a la malla tenga el Mesh 'char1' asignado
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        MeshCollider mc = GetComponentInChildren<MeshCollider>();
        if (mc != null && smr != null && mc.sharedMesh == null)
        {
            mc.sharedMesh = smr.sharedMesh;
        }

        // Configurar AudioSource 3D espacializado para el Arrastre de El Rastrero
        spatialAudioSource = GetComponent<AudioSource>();
        if (spatialAudioSource == null) spatialAudioSource = gameObject.AddComponent<AudioSource>();
        spatialAudioSource.spatialBlend = 1.0f; // 100% 3D Audio
        spatialAudioSource.minDistance = 1.0f;
        spatialAudioSource.maxDistance = 10.0f; // Solo se escucha si está verdaderamente cerca
        spatialAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        // Crear un AudioSource secundario exclusivo para Rugidos y Aullidos
        roarAudioSource = gameObject.AddComponent<AudioSource>();
        roarAudioSource.spatialBlend = 1.0f;
        roarAudioSource.minDistance = 2.0f;
        roarAudioSource.maxDistance = 25.0f;

        // Crear AudioSource exclusivo para sonido de persecución
        chaseAudioSource = gameObject.AddComponent<AudioSource>();
        chaseAudioSource.spatialBlend = 1.0f; // 3D
        chaseAudioSource.minDistance = 3.0f;
        chaseAudioSource.maxDistance = 25.0f;
        chaseAudioSource.loop = true;

        // Auto-cargar sonidos desde Resources si no están asignados en el inspector
        if (arrastreSound == null) arrastreSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/ArrastreRastrero");
        if (arrastreSound == null) arrastreSound = Resources.Load<AudioClip>("ArrastreRastrero");

        if (rugidoSound == null) rugidoSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/RugidoRastrero");
        if (rugidoSound == null) rugidoSound = Resources.Load<AudioClip>("RugidoRastrero");

        if (aullidoSound == null) aullidoSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/RastreroAullido");
        if (aullidoSound == null) aullidoSound = Resources.Load<AudioClip>("RastreroAullido");

        if (pisadasCercanasSound == null) pisadasCercanasSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/PisadasCercasRastrero");
        if (pisadasCercanasSound == null) pisadasCercanasSound = Resources.Load<AudioClip>("PisadasCercasRastrero");

        if (pisadasLejanasSound == null) pisadasLejanasSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/PisadasLejosRastrero");
        if (pisadasLejanasSound == null) pisadasLejanasSound = Resources.Load<AudioClip>("PisadasLejosRastrero");

        if (atackSound == null) atackSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/Atack");
        if (atackSound == null) atackSound = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/atack");
        if (atackSound == null) atackSound = Resources.Load<AudioClip>("Atack");
        if (atackSound == null) atackSound = Resources.Load<AudioClip>("atack");
        if (chaseSoundClip == null)
        {
            chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/TheCreep/Persecucion");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Persecusion");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Persecucion");
            if (chaseSoundClip == null) chaseSoundClip = Resources.Load<AudioClip>("Persecusion");
        }
        if (chaseSoundClip != null)
        {
            chaseAudioSource.clip = chaseSoundClip;
        }

        // Iniciar sonido de arrastre continuo en segundo plano (volumen moderado)
        if (arrastreSound != null)
        {
            spatialAudioSource.clip = arrastreSound;
            spatialAudioSource.loop = true;
            spatialAudioSource.volume = 0.5f;
            spatialAudioSource.Play();
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.angularSpeed = 180f; // Giro fluido y natural (~180 deg/s)
            agent.acceleration = 12f;   // Aceleración orgánica sin tirones
            agent.stoppingDistance = 1.6f; // Evitar que el agente se empotre dentro de la cápsula del jugador
        }

        // Configurar colisiones para que los colliders del cuerpo sean Triggers y no sirvan de rampa física al jugador
        SetupCollisions();

        FindPlayerReferences();
        StartCoroutine(DeferredGeneratePerimeter());
    }

    void SetupCollisions()
    {
        // 1. Convertir todos los Rigidbodies del monstruo en Kinematic
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rbs)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        // 2. Hacer Triggers todos los Colliders del cuerpo para que no actúen como rampas o escalones
        Collider[] myCols = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in myCols)
        {
            if (col != null)
            {
                col.isTrigger = true;
            }
        }
    }

    void IgnorePlayerCollisions()
    {
        if (playerTransform == null) return;

        Collider[] playerCols = playerTransform.GetComponentsInChildren<Collider>(true);
        Collider[] myCols = GetComponentsInChildren<Collider>(true);

        foreach (Collider myCol in myCols)
        {
            if (myCol == null) continue;
            foreach (Collider pCol in playerCols)
            {
                if (pCol != null)
                {
                    Physics.IgnoreCollision(myCol, pCol, true);
                }
            }
        }
    }

    void FindPlayerReferences()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) playerObj = FindObjectOfType<StarterAssets.FirstPersonController>()?.gameObject;

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerFlashlight = playerObj.GetComponentInChildren<FlashlightController>();
            playerSanity = playerObj.GetComponent<PlayerSanity>();

            IgnorePlayerCollisions();
        }
    }

    void TryWarpToNavMesh()
    {
        if (agent == null) return;
        Vector3 origin = transform.position;
        float[] yOffsets = { 0f, -0.5f, 0.5f, -1f, 1f, -2f, 2f, -4f, 4f };

        foreach (float dy in yOffsets)
        {
            Vector3 testPos = new Vector3(origin.x, origin.y + dy, origin.z);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPos, out hit, 3f, NavMesh.AllAreas))
            {
                if (agent.Warp(hit.position))
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                    Debug.Log("[TheCreep] Warp exitoso al NavMesh en " + hit.position);
                    SetNextPerimeterDestination();
                    return;
                }
            }
        }
    }

    void CheckAndOpenObstacleDoors()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || agent.velocity.magnitude < 0.1f) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;
        Vector3 rayDir = transform.forward;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, rayDir, out hit, 2.2f))
        {
            ProceduralDoorInteract procDoor = hit.collider.GetComponentInParent<ProceduralDoorInteract>();
            if (procDoor == null) procDoor = hit.collider.GetComponent<ProceduralDoorInteract>();
            if (procDoor != null)
            {
                if (!procDoor.gameObject.name.Contains("PuertaDirector"))
                {
                    if (procDoor.isLocked) procDoor.isLocked = false;
                    float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : transform.rotation);
                    if (angleDiff < 10f || hit.collider.gameObject.name.Contains("Puerta_Panel"))
                    {
                        procDoor.ToggleDoor();
                        Debug.Log("[TheCreep] Abrío una puerta obstáculo en su camino.");
                    }
                }
            }

            OpenDoor animDoor = hit.collider.GetComponentInParent<OpenDoor>();
            if (animDoor == null) animDoor = hit.collider.GetComponent<OpenDoor>();
            if (animDoor != null)
            {
                if (animDoor.isLocked) animDoor.isLocked = false;
                if (animDoor.doorAnimator != null && !animDoor.doorAnimator.GetBool("isOpen"))
                {
                    animDoor.doorAnimator.SetBool("isOpen", true);
                    if (animDoor.audioSource && animDoor.doorOpenSound)
                    {
                        animDoor.audioSource.PlayOneShot(animDoor.doorOpenSound, 1.0f);
                    }
                }
            }
        }
    }

    void Update()
    {
        if (playerTransform == null) FindPlayerReferences();

        // 0. Si se sale del NavMesh, re-anclar inmediatamente
        if (agent != null && !agent.isOnNavMesh)
        {
            TryWarpToNavMesh();
            return;
        }

        // 1. Abrir puertas en su trayectoria
        CheckAndOpenObstacleDoors();

        // 2. Manejo de animación de caminata/arrastre
        if (animator != null && agent != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsMoving", currentSpeed > 0.1f);
        }

        // 3. Detección de luz directa de la linterna (Ángulo y visión libre)
        CheckFlashlightExposure();

        // 4. Daño de Cordura y sonidos de pisadas
        CheckSanityDrain();

        // --- SISTEMA ANTI-ESTANCAMIENTO (TIMEOUT Y PATH INVALIDO/PARCIAL) ---
        if (agent != null && agent.enabled && agent.hasPath && !agent.pathPending && !isFleeing)
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                stuckTimer = 0f;
                Debug.LogWarning("[TheCreep] Path inválido/parcial → buscando nuevo destino de patrulla.");
                SetNextPerimeterDestination();
            }
            else if (agent.velocity.magnitude < 0.15f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckTimeout)
                {
                    stuckTimer = 0f;
                    Debug.LogWarning($"[TheCreep] Atascado ({stuckTimeout}s sin avanzar) → Cambiando punto de patrulla.");
                    SetNextPerimeterDestination();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        // 5. Comportamiento de IA: Huida -> Persecución en Oscuridad -> Patrullaje
        if (isFleeing)
        {
            if (chaseAudioSource != null && chaseAudioSource.isPlaying) chaseAudioSource.Stop();
            fleeTimer -= Time.deltaTime;
            if (fleeTimer <= 0f)
            {
                isFleeing = false;
                if (agent != null) agent.speed = walkSpeed;
                SetNextPerimeterDestination();
            }
        }
        else if (playerTransform != null && agent != null && agent.enabled)
        {
            // Comprobar si el jugador está escondido bajo la cama
            HideUnderBed hideScript = FindObjectOfType<HideUnderBed>();
            bool isPlayerHidden = hideScript != null && hideScript.isHiding;

            if (isPlayerHidden)
            {
                if (chaseAudioSource != null && chaseAudioSource.isPlaying)
                {
                    chaseAudioSource.Stop();
                }
                agent.speed = walkSpeed;
                if (animator != null) animator.speed = 1.0f;
                
                // Si el jugador está escondido, El Rastrero patrulla el perímetro exterior en vez de perseguir
                if (!agent.pathPending && agent.remainingDistance <= 1.1f)
                {
                    SetNextPerimeterDestination();
                }
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // SISTEMA ANTI-DOBLE PERSECUCIÓN: Si BookHead ya está cerca (menos de 8m), El Rastrero se retira al perímetro
            bool isBookHeadChasing = false;
            EnemyAIController b1 = FindObjectOfType<EnemyAIController>();
            if (b1 != null && b1.gameObject.activeInHierarchy)
            {
                float distToBook = Vector3.Distance(transform.position, b1.transform.position);
                float bookDistToPlayer = Vector3.Distance(b1.transform.position, playerTransform.position);
                if (distToBook < 8.0f || (bookDistToPlayer < 6.0f && distToPlayer < 10.0f)) isBookHeadChasing = true;
            }
            EnemyAIBookHead b2 = FindObjectOfType<EnemyAIBookHead>();
            if (b2 != null && b2.gameObject.activeInHierarchy)
            {
                float distToBook = Vector3.Distance(transform.position, b2.transform.position);
                float bookDistToPlayer = Vector3.Distance(b2.transform.position, playerTransform.position);
                if (distToBook < 8.0f || (bookDistToPlayer < 6.0f && distToPlayer < 10.0f)) isBookHeadChasing = true;
            }

            if (isBookHeadChasing)
            {
                // Cederle el paso a BookHead y huir momentáneamente hacia el perímetro exterior
                FleeToShadows();
                return;
            }

            // Si el jugador está en su rango de caza (10.0m), El Rastrero LO PERSEGUE furiosamente
            if (distToPlayer <= chaseDistance)
            {
                agent.speed = chaseSpeed;
                agent.SetDestination(playerTransform.position);

                // Aumentar velocidad de animación para simular caminata/arrastre rápido y frenético
                if (animator != null) animator.speed = 1.55f;

                // Reproducir audio ambiental de persecución
                if (chaseAudioSource != null && chaseSoundClip != null && !chaseAudioSource.isPlaying)
                {
                    chaseAudioSource.clip = chaseSoundClip;
                    chaseAudioSource.Play();
                }

                // Reproducir sonidos raros/espeluznantes (rugidos, aullidos) periódicamente durante la persecución
                chaseRoarTimer -= Time.deltaTime;
                if (chaseRoarTimer <= 0f)
                {
                    chaseRoarTimer = Random.Range(3.5f, 6.0f);
                    if (roarAudioSource != null)
                    {
                        AudioClip weirdSound = null;
                        float r = Random.value;
                        if (r < 0.45f && rugidoSound != null) weirdSound = rugidoSound;
                        else if (r < 0.8f && aullidoSound != null) weirdSound = aullidoSound;
                        else if (arrastreSound != null) weirdSound = arrastreSound;

                        if (weirdSound != null)
                        {
                            roarAudioSource.pitch = Random.Range(0.85f, 1.15f);
                            roarAudioSource.PlayOneShot(weirdSound, 1.0f);
                        }
                    }
                }

                // Si alcanza al jugador en cuerpo a cuerpo (menos de 1.8m), infligir daño por mordisco periódico con el sonido atack
                if (distToPlayer <= 1.8f)
                {
                    attackBiteTimer -= Time.deltaTime;
                    if (attackBiteTimer <= 0f)
                    {
                        attackBiteTimer = attackBiteCooldown;

                        PlayerHealth hp = playerTransform.GetComponent<PlayerHealth>();
                        if (hp == null) hp = playerTransform.GetComponentInParent<PlayerHealth>();
                        if (hp != null)
                        {
                            hp.TakeDamage(biteDamage);
                        }

                        if (roarAudioSource != null && atackSound != null)
                        {
                            roarAudioSource.pitch = Random.Range(0.95f, 1.05f);
                            roarAudioSource.PlayOneShot(atackSound, 1.0f);
                        }
                    }
                }
                else
                {
                    attackBiteTimer = 0f;
                }
            }
            else
            {
                if (animator != null) animator.speed = 1.0f; // Restaurar velocidad normal de animación

                if (chaseAudioSource != null && chaseAudioSource.isPlaying)
                {
                    chaseAudioSource.Stop();
                }
                agent.speed = walkSpeed;
                if (!agent.pathPending && agent.remainingDistance <= 1.1f)
                {
                    SetNextPerimeterDestination();
                }
            }
        }
    }

    IEnumerator DeferredGeneratePerimeter()
    {
        // Esperar a que el mapa procedural se genere completamente
        yield return new WaitForSeconds(0.2f);

        perimeterWaypoints.Clear();
        var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
        if (modGen != null && modGen.gridMatrix != null)
        {
            int sX = modGen.gridMatrix.GetLength(0);
            int sZ = modGen.gridMatrix.GetLength(1);
            float halfW = (sX * 4.0f) / 2.0f;
            float halfD = (sZ * 4.0f) / 2.0f;

            // Recolectar celdas de pasillo abiertas lo más externas posible (Perímetro exterior del edificio)
            for (int ring = 1; ring <= 3; ring++)
            {
                for (int x = ring; x < sX - ring; x++)
                {
                    for (int z = ring; z < sZ - ring; z++)
                    {
                        bool isEdge = (x == ring || x == sX - 1 - ring || z == ring || z == sZ - 1 - ring);
                        if (isEdge && modGen.gridMatrix[x, z] == 1)
                        {
                            float wX = (x * 4.0f) - halfW + 2.0f;
                            float wZ = (z * 4.0f) - halfD + 2.0f;
                            Vector3 worldPos = modGen.transform.position + new Vector3(wX, transform.position.y, wZ);

                            NavMeshHit hit;
                            if (NavMesh.SamplePosition(worldPos, out hit, 2.5f, NavMesh.AllAreas))
                            {
                                // Verificar que el punto tenga un camino NavMesh válido hasta el centro/jugador (No atrapado tras paredes)
                                NavMeshPath testPath = new NavMeshPath();
                                if (agent != null && agent.CalculatePath(hit.position, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
                                {
                                    if (!perimeterWaypoints.Contains(hit.position))
                                        perimeterWaypoints.Add(hit.position);
                                }
                                else if (agent == null && !perimeterWaypoints.Contains(hit.position))
                                {
                                    perimeterWaypoints.Add(hit.position);
                                }
                            }
                        }
                    }
                }
                if (perimeterWaypoints.Count >= 8) break;
            }

            // Ordenar los puntos perimetrales en sentido horario alrededor del centro del mapa
            Vector3 center = modGen.transform.position;
            perimeterWaypoints.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.z - center.z, a.x - center.x);
                float angleB = Mathf.Atan2(b.z - center.z, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            // Teletransportar a El Rastrero a la esquina más alejada del jugador en el perímetro
            if (perimeterWaypoints.Count > 0 && agent != null)
            {
                Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
                Vector3 farthestCorner = perimeterWaypoints[0];
                float maxDist = -1f;

                foreach (Vector3 pt in perimeterWaypoints)
                {
                    float d = Vector3.Distance(pt, playerPos);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        farthestCorner = pt;
                    }
                }

                agent.enabled = false;
                transform.position = farthestCorner;
                agent.enabled = true;
                agent.Warp(farthestCorner);

                SetNextPerimeterDestination();
            }
        }
    }

    void SetNextPerimeterDestination()
    {
        stuckTimer = 0f;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (perimeterWaypoints.Count == 0)
        {
            TrySetRandomNavMeshDestination();
            return;
        }

        int attempts = 0;
        int maxAttempts = perimeterWaypoints.Count;

        do
        {
            currentWaypointIdx = (currentWaypointIdx + 1) % perimeterWaypoints.Count;
            Vector3 target = perimeterWaypoints[currentWaypointIdx];
            attempts++;

            NavMeshHit hit;
            Vector3 finalTarget = NavMesh.SamplePosition(target, out hit, 4f, NavMesh.AllAreas) ? hit.position : target;

            NavMeshPath path = new NavMeshPath();
            if (agent.CalculatePath(finalTarget, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetPath(path);
                return;
            }
        }
        while (attempts < maxAttempts);

        // Fallback si ningún waypoint perimetral es directamente completable
        TrySetRandomNavMeshDestination();
    }

    private void TrySetRandomNavMeshDestination()
    {
        stuckTimer = 0f;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * 18f;
            randomDir += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 18f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetPath(path);
                    return;
                }
            }
        }
    }

    void CheckFlashlightExposure()
    {
        if (playerTransform == null) return;

        if (playerFlashlight == null) playerFlashlight = playerTransform.GetComponentInChildren<FlashlightController>();
        Light flashLightComp = (playerFlashlight != null) ? playerFlashlight.GetComponent<Light>() : playerTransform.GetComponentInChildren<Light>();

        bool isLightOn = (flashLightComp != null && flashLightComp.enabled);

        if (isLightOn)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Huir inmediatamente si la linterna encendida alumbra en un radio de 12.0m
            if (dist <= flashlightFleeDistance)
            {
                FleeToShadows();
            }
        }
    }

    public void FleeToShadows()
    {
        if (agent == null || !agent.enabled) return;

        isFleeing = true;
        fleeTimer = 8.0f; // Huir durante 8 segundos hacia el perímetro lejano
        agent.speed = 3.8f; // Velocidad rápida de retirada hacia las sombras

        // Reproducir Aullido o Rugido de furia/dolor al ser alumbrado
        if (roarAudioSource != null)
        {
            AudioClip reactClip = (Random.value < 0.5f && aullidoSound != null) ? aullidoSound : rugidoSound;
            if (reactClip != null) roarAudioSource.PlayOneShot(reactClip, 1.0f);
        }

        // Seleccionar la celda perimetral más lejana del jugador
        Vector3 farthestPt = transform.position;
        float maxDist = -1f;

        foreach (Vector3 pt in perimeterWaypoints)
        {
            float d = Vector3.Distance(pt, playerTransform.position);
            if (d > maxDist)
            {
                maxDist = d;
                farthestPt = pt;
            }
        }

        agent.SetDestination(farthestPt);
    }

    void CheckSanityDrain()
    {
        if (playerTransform == null) return;

        HideUnderBed hideScript = FindObjectOfType<HideUnderBed>();
        if (hideScript != null && hideScript.isHiding)
        {
            if (heartbeatAudioSource != null)
            {
                heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, 0f, Time.deltaTime * 0.5f);
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // Control del Latido de corazón en audífonos (Audio 2D en la cabeza del jugador)
        if (heartbeatAudioSource == null && playerTransform != null)
        {
            heartbeatClip = Resources.Load<AudioClip>("Audio/Compartido/Latido");
            if (heartbeatClip != null)
            {
                heartbeatAudioSource = playerTransform.gameObject.AddComponent<AudioSource>();
                heartbeatAudioSource.clip = heartbeatClip;
                heartbeatAudioSource.loop = true;
                heartbeatAudioSource.spatialBlend = 0f; // 2D Headphone sound
                heartbeatAudioSource.volume = 0f;
                heartbeatAudioSource.Play();
            }
        }

        if (heartbeatAudioSource != null)
        {
            if (dist <= sanityEffectRadius)
            {
                float targetVol = Mathf.Lerp(0.85f, 0.1f, dist / sanityEffectRadius);
                heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, targetVol, Time.deltaTime * 0.8f);
            }
            else
            {
                heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, 0f, Time.deltaTime * 0.5f);
            }
        }

        if (playerSanity != null && dist <= sanityEffectRadius)
        {
            playerSanity.TakeSanityDamage(sanityDrainRate * Time.deltaTime);
        }

        // Reproducir pasos 3D (Cercanos vs Lejanos) según la distancia del jugador
        if (agent != null && agent.velocity.magnitude > 0.1f && Time.time >= stepAudioTimer)
        {
            bool isChasingPlayer = (dist <= chaseDistance && !isFleeing);
            float stepInterval = isChasingPlayer ? 0.32f : (isFleeing ? 0.45f : 0.85f);
            stepAudioTimer = Time.time + stepInterval;

            AudioClip stepClip = (dist <= 8.0f && pisadasCercanasSound != null) ? pisadasCercanasSound : pisadasLejanasSound;
            if (stepClip == null) stepClip = arrastreSound;

            if (stepClip != null && spatialAudioSource != null)
            {
                spatialAudioSource.pitch = isChasingPlayer ? Random.Range(1.15f, 1.35f) : Random.Range(0.95f, 1.05f);
                spatialAudioSource.PlayOneShot(stepClip, Mathf.Clamp01(1.0f - (dist / 22.0f)));
            }
        }
    }

    // --- SISTEMA DE GRACIA Y RECOLOCACIÓN POST-RESPAWN ---
    public void TriggerRespawnGracePeriod(float duration)
    {
        // Detener sonidos de persecución y tensión
        if (chaseAudioSource != null && chaseAudioSource.isPlaying) chaseAudioSource.Stop();
        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying) heartbeatAudioSource.Stop();

        // Recolocar en la esquina más alejada
        TeleportToFarthestCorner();

        // Iniciar gracia de inmunidad/invisibilidad
        StartCoroutine(RespawnGraceRoutine(duration));
    }

    private void TeleportToFarthestCorner()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        // Si no hay waypoints cargados aún, cargarlos manualmente
        if (perimeterWaypoints.Count == 0)
        {
            var modGen = FindObjectOfType<ModularHospital.ModularHospitalGenerator>();
            if (modGen != null && modGen.gridMatrix != null)
            {
                int sX = modGen.gridMatrix.GetLength(0);
                int sZ = modGen.gridMatrix.GetLength(1);
                float halfW = (sX * 4.0f) / 2.0f;
                float halfD = (sZ * 4.0f) / 2.0f;

                for (int ring = 1; ring <= 3; ring++)
                {
                    for (int x = ring; x < sX - ring; x++)
                    {
                        for (int z = ring; z < sZ - ring; z++)
                        {
                            if ((x == ring || x == sX - 1 - ring || z == ring || z == sZ - 1 - ring) && modGen.gridMatrix[x, z] == 1)
                            {
                                float wX = (x * 4.0f) - halfW + 2.0f;
                                float wZ = (z * 4.0f) - halfD + 2.0f;
                                Vector3 worldPos = modGen.transform.position + new Vector3(wX, transform.position.y, wZ);

                                NavMeshHit hit;
                                if (NavMesh.SamplePosition(worldPos, out hit, 3f, NavMesh.AllAreas))
                                {
                                    if (!perimeterWaypoints.Contains(hit.position))
                                        perimeterWaypoints.Add(hit.position);
                                }
                            }
                        }
                    }
                }
            }
        }

        Vector3 spawnTarget = transform.position;
        if (perimeterWaypoints.Count > 0)
        {
            Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
            Vector3 farthest = perimeterWaypoints[0];
            float maxDist = -1f;

            foreach (Vector3 pt in perimeterWaypoints)
            {
                float d = Vector3.Distance(pt, playerPos);
                if (d > maxDist)
                {
                    maxDist = d;
                    farthest = pt;
                }
            }
            spawnTarget = farthest;
        }

        // Teletransportación forzada desactivando NavMeshAgent para evitar el bug "Failed to create agent because it is not close enough"
        if (agent != null) agent.enabled = false;
        transform.position = spawnTarget;
        
        // Tratar de anclar al NavMesh de forma segura
        NavMeshHit hitSafe;
        if (NavMesh.SamplePosition(spawnTarget, out hitSafe, 4f, NavMesh.AllAreas))
        {
            transform.position = hitSafe.position;
        }

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.isStopped = false;
            agent.speed = walkSpeed;
        }

        Debug.Log($"[TheCreep] Recolocado con éxito al revivir en la posición perimetral: {transform.position}");
    }

    private IEnumerator RespawnGraceRoutine(float duration)
    {
        // 1. Ocultar renderers para que el Rastrero sea invisible
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null && r.gameObject != gameObject) r.enabled = false;
        }

        // Desactivar temporalmente los sonidos y la cacería de cordura
        isFleeing = true; // Forzar estado de huida para que no persiga

        // 2. Esperar período de gracia
        yield return new WaitForSeconds(duration);

        // 3. Reactivar visibilidad y restablecer comportamiento
        isFleeing = false;
        foreach (Renderer r in renderers)
        {
            if (r != null && r.gameObject != gameObject) r.enabled = true;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            SetNextPerimeterDestination();
        }

        Debug.Log("[TheCreep] Período de gracia post-respawn finalizado. Rastrero de nuevo activo en cacería.");
    }
}
