using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CrawlerAI : MonoBehaviour
{
    [Header("Ajustes de Acecho y Movimiento")]
    [Tooltip("Velocidad de caminata/arrastre sigiloso")]
    public float walkSpeed = 1.8f;
    [Tooltip("Velocidad al huir hacia las sombras")]
    public float fleeSpeed = 3.2f;
    [Tooltip("Distancia mínima al jugador para afectar su cordura")]
    public float sanityEffectRadius = 6.0f;
    [Tooltip("Pérdida de cordura por segundo al estar cerca del Rastrero")]
    public float sanityDrainRate = 5.0f;

    [Header("Detección de Luz")]
    [Tooltip("Si la linterna del jugador lo alumbra directamente a esta distancia, huye hacia la sombra")]
    public float flashlightFleeDistance = 7.0f;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private FlashlightController playerFlashlight;
    private PlayerSanity playerSanity;

    private List<Vector3> perimeterWaypoints = new List<Vector3>();
    private int currentWaypointIdx = 0;
    private bool isFleeing = false;
    private float fleeTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (agent != null)
        {
            agent.speed = walkSpeed;
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

        // 2. Detección de luz directa de la linterna
        CheckFlashlightExposure();

        // 3. Daño de Cordura al jugador si está cerca en la oscuridad
        CheckSanityDrain();

        // 4. Patrullaje perimetral constante si no está huyendo
        if (!isFleeing && agent != null && agent.enabled)
        {
            if (!agent.pathPending && agent.remainingDistance <= 0.8f)
            {
                SetNextPerimeterDestination();
            }
        }
        else if (isFleeing)
        {
            fleeTimer -= Time.deltaTime;
            if (fleeTimer <= 0f)
            {
                isFleeing = false;
                if (agent != null) agent.speed = walkSpeed;
                SetNextPerimeterDestination();
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
                            if (NavMesh.SamplePosition(worldPos, out hit, 3.5f, NavMesh.AllAreas))
                            {
                                if (!perimeterWaypoints.Contains(hit.position))
                                    perimeterWaypoints.Add(hit.position);
                            }
                        }
                    }
                }
                if (perimeterWaypoints.Count >= 6) break;
            }

            // Ordenar los puntos perimetrales en sentido horario alrededor del centro del mapa
            Vector3 center = modGen.transform.position;
            perimeterWaypoints.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.z - center.z, a.x - center.x);
                float angleB = Mathf.Atan2(b.z - center.z, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            // Teletransportar a El Rastrero a la primera esquina del anillo exterior al iniciar
            if (perimeterWaypoints.Count > 0 && agent != null)
            {
                agent.enabled = false;
                transform.position = perimeterWaypoints[0];
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(perimeterWaypoints[0]);

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
        if (playerTransform == null || playerFlashlight == null) return;

        // Si la linterna está encendida y apunta hacia El Rastrero
        Light flashLightComp = playerFlashlight.GetComponent<Light>();
        if (flashLightComp != null && flashLightComp.enabled)
        {
            Vector3 dirToRastrero = (transform.position - playerTransform.position);
            float dist = dirToRastrero.magnitude;

            if (dist <= flashlightFleeDistance)
            {
                float angle = Vector3.Angle(playerTransform.forward, dirToRastrero.normalized);
                if (angle < flashLightComp.spotAngle * 0.5f)
                {
                    // Alumbrado directo: Huir hacia la esquina perimetral opuesta
                    FleeToShadows();
                }
            }
        }
    }

    void FleeToShadows()
    {
        if (isFleeing || agent == null || !agent.enabled) return;

        isFleeing = true;
        fleeTimer = 4.0f; // Huir durante 4 segundos
        agent.speed = fleeSpeed;

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
        if (playerTransform == null || playerSanity == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= sanityEffectRadius)
        {
            // Bajar la cordura del jugador gradualmente
            playerSanity.TakeSanityDamage(sanityDrainRate * Time.deltaTime);
        }
    }
}
