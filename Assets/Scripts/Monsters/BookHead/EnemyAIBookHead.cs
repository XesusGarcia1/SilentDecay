using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemyAIBookHead : MonoBehaviour
{
    public Transform player;
    public float detectionRange = 9.0f;    // Rango de detección
    public float attackRange = 1.8f;
    public float runSpeed = 4.2f;         // Correr rápido e intenso (ajustado a la nueva escala)
    public float walkSpeed = 2.4f;        // Caminar veloz (ajustado a la nueva escala)
    public Transform[] patrolPoints;

    private NavMeshAgent agent;
    private Animator anim;
    private bool isAttacking = false;
    private bool isEating = false;
    private bool isPatrolling = false;
    private int currentPatrolIndex = 0; // Mantener el índice del punto actual
    private List<Transform> remainingPatrolPoints;

    private bool initialized = false;

    void OnEnable()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.agentTypeID = 0; // Humanoid
            agent.height = 2.1f;   // Corregir altura de 9.73m a 2.1m para no chocar con techos ni marcos de puertas
            agent.radius = 0.5f;
            agent.stoppingDistance = 1.6f;
            agent.baseOffset = 0f;
            agent.speed = walkSpeed;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 4.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        if (patrolPoints != null && patrolPoints.Length > 0 && !isPatrolling)
        {
            initialized = true;
            isPatrolling = true;
            if (anim != null) { anim.SetBool("Walking", true); anim.SetBool("Still", false); }
            remainingPatrolPoints = new List<Transform>(patrolPoints);
            StartCoroutine(PatrolRoutine());
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.agentTypeID = 0; // Humanoid por defecto
            agent.height = 2.1f;   // Altura humana realista
            agent.radius = 0.50f;
            agent.stoppingDistance = 1.6f;
            agent.baseOffset = 0f;
            agent.speed = walkSpeed;
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
        if (player == null) return;

        // Comprobar si el jugador está escondido
        HideUnderBed hideScript = FindObjectOfType<HideUnderBed>();
        bool isPlayerHidden = hideScript != null && hideScript.isHiding;

        if (isPlayerHidden)
        {
            if (isAttacking)
            {
                StopAllCoroutines();
                isAttacking = false;
                if (anim != null) anim.SetBool("Attacking", false);
            }
            if (isEating)
            {
                StopAllCoroutines();
                isEating = false;
                if (anim != null) anim.SetBool("Eating", false);
            }

            if (!isPatrolling && initialized)
            {
                isPatrolling = true;
                if (anim != null)
                {
                    anim.SetBool("Running", false);
                    anim.SetBool("Walking", true);
                    anim.SetBool("Still", false);
                }
                StartCoroutine(PatrolRoutine());
            }
            return;
        }

        if (isAttacking) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            if (isEating)
            {
                StopCoroutine(EatCycle());
                isEating = false;
                if (anim != null) anim.SetBool("Eating", false);
            }

            if (distanceToPlayer <= attackRange)
                AttackPlayer();
            else
                ChasePlayer();
        }
        else
        {
            if (!isPatrolling && !isAttacking && !isEating && initialized)
            {
                isPatrolling = true;
                StartCoroutine(PatrolRoutine());
            }
        }
    }

    public void PreloadPatrol(Transform[] points, Transform playerTarget)
    {
        if (points != null && points.Length > 0)
        {
            patrolPoints = points;
            remainingPatrolPoints = new List<Transform>(patrolPoints);
        }
        if (playerTarget != null) player = playerTarget;
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

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
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
                if (!agent.isOnNavMesh)
                {
                    UnityEngine.AI.NavMeshHit hit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                    yield return null;
                }

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;

                    UnityEngine.AI.NavMeshHit destHit;
                    Vector3 dest = nextPoint.position;
                    if (UnityEngine.AI.NavMesh.SamplePosition(dest, out destHit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                        dest = destHit.position;

                    agent.SetDestination(dest);

                    float timeout = 0f;
                    float stuckTimer = 0f;

                    while (agent != null && agent.enabled && agent.isOnNavMesh
                           && Vector3.Distance(transform.position, dest) > 1.3f
                           && timeout < 12f)
                    {
                        if (isAttacking || isEating) break;
                        timeout += Time.deltaTime;

                        bool isMoving = agent.velocity.magnitude > 0.15f;
                        if (anim != null)
                        {
                            anim.SetBool("Walking", isMoving);
                            anim.SetBool("Still", !isMoving);
                        }

                        if (!isMoving)
                        {
                            stuckTimer += Time.deltaTime;
                            if (stuckTimer >= 2.0f)
                            {
                                Debug.LogWarning("BookHeadAI: Estancado en pasillo. Saltando al siguiente punto de patrulla.");
                                break;
                            }
                        }
                        else
                        {
                            stuckTimer = 0f;
                        }

                        yield return null;
                    }

                    if (anim != null)
                    {
                        anim.SetBool("Walking", false);
                        anim.SetBool("Still", true);
                    }
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                }
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            yield return new WaitForSeconds(Random.Range(2.0f, 4.0f));
        }
    }

    void ChasePlayer()
    {
        isPatrolling = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh && player != null)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
        }
        if (anim != null)
        {
            anim.SetBool("Running", true);
            anim.SetBool("Walking", false);
            anim.SetBool("Still", false);
        }
    }

    void AttackPlayer()
    {
        isAttacking = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        if (anim != null)
        {
            anim.SetBool("Attacking", true);
            anim.SetBool("Running", false);
            anim.SetBool("Walking", false);
            anim.SetBool("Still", false);
        }
        StartCoroutine(AttackCooldown());
    }

    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(1f);
        if (anim != null) anim.SetBool("Attacking", false);
        isAttacking = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        if (player != null && Vector3.Distance(transform.position, player.position) > detectionRange)
        {
            isPatrolling = true;
            StartCoroutine(PatrolRoutine());
        }
    }

    IEnumerator EatCycle()
    {
        isEating = true;
        if (anim != null)
        {
            anim.SetBool("Eating", true);
            anim.SetBool("Still", false);
        }
        yield return new WaitForSeconds(4f);
        if (anim != null) anim.SetBool("Eating", false);
        isEating = false;

        if (player != null && Vector3.Distance(transform.position, player.position) > detectionRange)
        {
            isPatrolling = true;
            StartCoroutine(PatrolRoutine());
        }
    }

    public void FleeFarFromPlayer()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

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
    }
}
