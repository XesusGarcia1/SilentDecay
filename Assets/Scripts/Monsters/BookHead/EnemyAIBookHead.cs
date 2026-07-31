using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemyAIBookHead : MonoBehaviour
{
    public Transform player;
    public float detectionRange = 7f;    // Rango de detección reducido para mapa pequeño
    public float attackRange = 1.8f;
    public float runSpeed = 2.2f;         // Correr lento — más tenso, menos imposible
    public float walkSpeed = 1.2f;        // Caminar pausado y amenazante
    public Transform[] patrolPoints;

    private NavMeshAgent agent;
    private Animator anim;
    private bool isAttacking = false;
    private bool isEating = false;
    private bool isPatrolling = false;
    private int currentPatrolIndex = 0; // Mantener el índice del punto actual
    private List<Transform> remainingPatrolPoints;

    private bool initialized = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.agentTypeID = 0; // Humanoid por defecto
            agent.height = 2.0f;   // Altura humana realista
            agent.radius = 0.40f;
            agent.stoppingDistance = 1.6f;
            agent.baseOffset = 0f;
        }

        anim = GetComponent<Animator>();

        // Hacer Rigidbody y Colliders de tipo Trigger/Kinematic para evitar colisión física con el jugador
        Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in childRbs)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        foreach (Collider c in childCols)
        {
            if (c != null) c.isTrigger = true;
        }

        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj == null) pObj = GameObject.Find("NestedParent_Unpack");
            if (pObj != null) player = pObj.transform;
        }

        if (player != null)
        {
            Collider[] pCols = player.GetComponentsInChildren<Collider>(true);
            foreach (Collider mC in childCols)
            {
                if (mC == null) continue;
                foreach (Collider pC in pCols)
                {
                    if (pC != null) Physics.IgnoreCollision(mC, pC, true);
                }
            }
        }

        // NO lanzar PatrolRoutine aquí. El generador llamará InitializePatrol() cuando el NavMesh esté listo.
        // Si ya fue inicializado externamente (ej. prefab existente en escena), iniciar normal.
        if (initialized) return;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            initialized = true;
            agent.speed = walkSpeed;
            if (anim != null) { anim.SetBool("Walking", true); anim.SetBool("Still", false); }
            remainingPatrolPoints = new List<Transform>(patrolPoints);
            StartCoroutine(PatrolRoutine());
        }
    }

    void Update()
    {
        if (player == null || isAttacking) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            if (isEating)
            {
                StopCoroutine(EatCycle());
                isEating = false;
                anim.SetBool("Eating", false);
            }

            if (distanceToPlayer <= attackRange)
                AttackPlayer();
            else
                ChasePlayer();
        }
        else
        {
            // Si no está persiguiendo o atacando, sigue patrullando
            if (!isPatrolling && !isAttacking && !isEating && initialized)
            {
                isPatrolling = true;
                StartCoroutine(PatrolRoutine());
            }
        }
    }

    /// <summary>
    /// Pre-carga los puntos de patrullaje y al jugador sin iniciar movimiento.
    /// Llamado por el generador cuando el monstruo está inactivo (antes del primer apagón).
    /// </summary>
    public void PreloadPatrol(Transform[] points, Transform playerTarget)
    {
        if (points != null && points.Length > 0)
        {
            patrolPoints = points;
            remainingPatrolPoints = new List<Transform>(patrolPoints);
        }
        if (playerTarget != null) player = playerTarget;
        // NO iniciar PatrolRoutine aqui — se iniciara en OnEnable cuando PowerBox active al monstruo
    }

    public void InitializePatrol(Transform[] points, Transform playerTarget)
    {
        if (points != null && points.Length > 0)
        {
            patrolPoints = points;
            remainingPatrolPoints = new List<Transform>(patrolPoints);
        }

        if (playerTarget != null)
        {
            player = playerTarget;
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (anim == null) anim = GetComponent<Animator>();

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.speed = walkSpeed;
        }

        if (anim != null) { anim.SetBool("Walking", true); anim.SetBool("Still", false); }

        initialized = true;
        isPatrolling = true;
        isAttacking = false;
        isEating = false;

        StopAllCoroutines();
        StartCoroutine(PatrolRoutine());
    }

    // Al activarse con SetActive(true) desde PowerBox: reanudar patrullaje si ya fue pre-cargado
    void OnEnable()
    {
        if (!initialized && patrolPoints != null && patrolPoints.Length > 0)
        {
            // Pre-cargado por generador: iniciar ahora que estamos activos
            InitializePatrol(patrolPoints, player);
        }
    }

    IEnumerator PatrolRoutine()
    {
        while (true)
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            if (isAttacking || isEating)
            {
                yield return null;
                continue;
            }

            if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;
            Transform nextPoint = patrolPoints[currentPatrolIndex];

            if (nextPoint != null && agent != null && agent.enabled)
            {
                // Si no está sobre el NavMesh, intentar reposicionarse y esperar 1 frame
                if (!agent.isOnNavMesh)
                {
                    UnityEngine.AI.NavMeshHit hit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                    yield return null; // Esperar 1 frame para que isOnNavMesh se actualice
                }

                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;

                    // Verificar que el destino sea alcanzable en el NavMesh
                    UnityEngine.AI.NavMeshHit destHit;
                    Vector3 dest = nextPoint.position;
                    if (UnityEngine.AI.NavMesh.SamplePosition(dest, out destHit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                        dest = destHit.position;

                    agent.SetDestination(dest);

                    if (anim != null)
                    {
                        anim.SetBool("Walking", true);
                        anim.SetBool("Still", false);
                    }

                    // Esperar hasta llegar al punto (timeout 15s para mapas grandes)
                    float timeout = 0f;
                    while (agent != null && agent.enabled && agent.isOnNavMesh
                           && Vector3.Distance(transform.position, dest) > 1.2f
                           && timeout < 15f)
                    {
                        if (isAttacking || isEating) break;
                        timeout += Time.deltaTime;
                        yield return null;
                    }

                    if (anim != null)
                    {
                        anim.SetBool("Walking", false);
                        anim.SetBool("Still", true);
                    }
                    if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                }
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            yield return new WaitForSeconds(Random.Range(2.0f, 4.0f)); // Pausa entre puntos
        }
    }

    void ChasePlayer()
    {
        isPatrolling = false;
        agent.SetDestination(player.position);
        agent.isStopped = false;
        agent.speed = runSpeed;
        anim.SetBool("Running", true);
        anim.SetBool("Walking", false);
        anim.SetBool("Still", false);
    }

    void AttackPlayer()
    {
        isAttacking = true;
        agent.isStopped = true;
        anim.SetBool("Attacking", true);
        anim.SetBool("Running", false);
        anim.SetBool("Walking", false);
        anim.SetBool("Still", false);
        StartCoroutine(AttackCooldown());
    }

    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(1f);
        anim.SetBool("Attacking", false);
        isAttacking = false;
        agent.isStopped = false;

        if (Vector3.Distance(transform.position, player.position) > detectionRange)
        {
            isPatrolling = true;
            StartCoroutine(PatrolRoutine());
        }
    }

    IEnumerator EatCycle()
    {
        isEating = true;
        anim.SetBool("Eating", true);
        anim.SetBool("Still", false);
        yield return new WaitForSeconds(4f);
        anim.SetBool("Eating", false);
        isEating = false;

        if (Vector3.Distance(transform.position, player.position) > detectionRange)
        {
            isPatrolling = true;
            StartCoroutine(PatrolRoutine());
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
        Debug.Log("BookHeadAI: Jugador escondido debajo de cama. Retirándose rápido a punto lejano: " + farthestPos);
    }
}
