using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CrawlerAI : MonoBehaviour
{
    [Header("Ajustes de Acecho y Movimiento")]
    [Tooltip("Velocidad de caminata/arrastre sigiloso (Lenta y aterradora)")]
    public float walkSpeed = 1.35f;
    [Tooltip("Velocidad al perseguir al jugador en la oscuridad")]
    public float chaseSpeed = 2.3f;
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

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private FlashlightController playerFlashlight;
    private PlayerSanity playerSanity;
    private AudioSource spatialAudioSource;
    private AudioSource roarAudioSource;
    private AudioSource heartbeatAudioSource;
    private AudioClip heartbeatClip;

    private List<Vector3> perimeterWaypoints = new List<Vector3>();
    private int currentWaypointIdx = 0;
    private bool isFleeing = false;
    private float fleeTimer = 0f;
    private float stepAudioTimer = 0f;

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

        // Auto-cargar sonidos desde Resources si no están asignados en el inspector
        if (arrastreSound == null) arrastreSound = Resources.Load<AudioClip>("ArrastreRastrero");
        if (rugidoSound == null) rugidoSound = Resources.Load<AudioClip>("RugidoRastrero");
        if (aullidoSound == null) aullidoSound = Resources.Load<AudioClip>("RastreroAullido");
        if (pisadasCercanasSound == null) pisadasCercanasSound = Resources.Load<AudioClip>("PisadasCercasRastrero");
        if (pisadasLejanasSound == null) pisadasLejanasSound = Resources.Load<AudioClip>("PisadasLejosRastrero");

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
            agent.stoppingDistance = 0.5f;
        }

        FindPlayerReferences();
        StartCoroutine(DeferredGeneratePerimeter());
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
        }
    }

    void Update()
    {
        if (playerTransform == null) FindPlayerReferences();

        // 1. Manejo de animación de caminata/arrastre
        if (animator != null && agent != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsMoving", currentSpeed > 0.1f);
        }

        // 2. Detección de luz directa de la linterna (Ángulo y visión libre)
        CheckFlashlightExposure();

        // 3. Daño de Cordura y sonidos de pisadas
        CheckSanityDrain();

        // 4. Comportamiento de IA: Huida -> Persecución en Oscuridad -> Patrullaje
        if (isFleeing)
        {
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

                // Si alcanza al jugador en cuerpo a cuerpo (menos de 1.8m), infligir daño de salud real
                if (distToPlayer <= 1.8f)
                {
                    PlayerHealth hp = playerTransform.GetComponent<PlayerHealth>();
                    if (hp == null) hp = playerTransform.GetComponentInParent<PlayerHealth>();
                    if (hp != null)
                    {
                        hp.TakeDamage(18.0f * Time.deltaTime); // Infligir daño de garrazos/mordiscos
                    }
                }
            }
            else
            {
                agent.speed = walkSpeed;
                if (!agent.pathPending && agent.remainingDistance <= 0.8f)
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

            // Teletransportar a El Rastrero a una posición libre y caminable en el perímetro
            if (perimeterWaypoints.Count > 0 && agent != null)
            {
                agent.enabled = false;
                transform.position = perimeterWaypoints[0];
                agent.enabled = true;
                agent.Warp(perimeterWaypoints[0]);

                SetNextPerimeterDestination();
            }
        }
    }

    void SetNextPerimeterDestination()
    {
        if (perimeterWaypoints.Count == 0 || agent == null || !agent.enabled) return;

        currentWaypointIdx = (currentWaypointIdx + 1) % perimeterWaypoints.Count;
        Vector3 target = perimeterWaypoints[currentWaypointIdx];
        agent.SetDestination(target);
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

    void FleeToShadows()
    {
        if (isFleeing || agent == null || !agent.enabled) return;

        isFleeing = true;
        fleeTimer = 5.0f; // Huir durante 5 segundos hacia las sombras
        agent.speed = fleeSpeed;

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

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // Control del Latido de corazón en audífonos (Audio 2D en la cabeza del jugador)
        if (heartbeatAudioSource == null && playerTransform != null)
        {
            heartbeatClip = Resources.Load<AudioClip>("Latido");
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
            stepAudioTimer = Time.time + (isFleeing ? 0.5f : 0.85f);
            AudioClip stepClip = (dist <= 8.0f && pisadasCercanasSound != null) ? pisadasCercanasSound : pisadasLejanasSound;
            if (stepClip != null && spatialAudioSource != null)
            {
                spatialAudioSource.PlayOneShot(stepClip, Mathf.Clamp01(1.0f - (dist / 22.0f)));
            }
        }
    }
}
