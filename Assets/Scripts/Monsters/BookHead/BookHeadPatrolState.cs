using UnityEngine.AI;
using UnityEngine;
using System.Collections;

public class BookHeadPatrolState : IEnemyState
{
    private BookHeadAIController enemy;
    private NavMeshAgent agent;
    private BookHeadAnimation anim;
    private Transform[] patrolPoints;
    private int currentPatrolIndex = 0;

    private bool isIdle = false;
    private float idleTime = 3f;
    private float eatTime = 2f;
    private float idleTimer = 0f;
    private float eatTimer = 0f;

    public BookHeadPatrolState(BookHeadAIController enemy, NavMeshAgent agent, BookHeadAnimation anim, Transform[] patrolPoints)
    {
        this.enemy = enemy;
        this.agent = agent;
        this.anim = anim;
        this.patrolPoints = patrolPoints;
    }

    public void EnterState()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

        // NO llamar isStopped = false antes de estar en el NavMesh
        agent.speed = enemy.walkSpeed;
        anim?.SetWalking(true);

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            MoveToNextPatrolPoint();
        }
        // Si no esta en NavMesh, UpdateState() lo manejara
    }

    public void UpdateState()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

        // Si no esta en el NavMesh, intentar anclar con un barrido vertical
        if (!agent.isOnNavMesh)
        {
            TryWarpToNavMesh();
            return;
        }

        // Asegurar que el agente no este pausado
        if (agent.isStopped)
            agent.isStopped = false;

        // ─── DETECCION DE CAMINO INVALIDO ───────────────────────────────
        // Si el path calculado es inválido o parcial, cambiar de punto YA
        if (agent.hasPath && !agent.pathPending &&
            (agent.pathStatus == NavMeshPathStatus.PathInvalid ||
             agent.pathStatus == NavMeshPathStatus.PathPartial))
        {
            Debug.LogWarning("[Patrol] Path inválido/parcial al punto " + currentPatrolIndex + " → cambiando destino.");
            MoveToNextPatrolPoint();
            return;
        }

        // Si no tiene destino, pedir uno
        if (!agent.hasPath && !agent.pathPending && !isIdle)
        {
            MoveToNextPatrolPoint();
            return;
        }

        if (isIdle)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleTime)
            {
                idleTimer = 0f;
                if (Random.Range(0f, 1f) <= 0.8f)
                {
                    anim?.SetIdle(true);
                    anim?.SetEating(true);
                    eatTimer = 0f;
                }
                else
                {
                    MoveToNextPatrolPoint();
                    isIdle = false;
                    anim?.SetWalking(true);
                }
            }
            else
            {
                if (eatTimer >= eatTime)
                {
                    MoveToNextPatrolPoint();
                    isIdle = false;
                    anim?.SetWalking(true);
                    anim?.SetIdle(false);
                    anim?.SetEating(false);
                }
                else
                {
                    eatTimer += Time.deltaTime;
                }
            }
        }
        else
        {
            // Lanzar un raycast corto hacia adelante durante la patrulla para abrir puertas cerradas en su camino
            if (agent.velocity.magnitude > 0.1f && !agent.isStopped)
            {
                Vector3 rayOrigin = enemy.transform.position + Vector3.up * 1.2f;
                Vector3 rayDir = enemy.transform.forward;
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, rayDir, out hit, 2.0f))
                {
                    ProceduralDoorInteract procDoor = hit.collider.GetComponentInParent<ProceduralDoorInteract>();
                    if (procDoor == null) procDoor = hit.collider.GetComponent<ProceduralDoorInteract>();
                    if (procDoor != null)
                    {
                        // La oficina del director requiere obligatoriamente descifrar el Keypad,
                        // el monstruo NUNCA debe abrir ni forzar esta puerta.
                        if (procDoor.gameObject.name.Contains("PuertaDirector"))
                        {
                            // Ignorar por completo
                        }
                        else
                        {
                            // Si está bloqueada por llaves, desbloquearla
                            if (procDoor.isLocked) procDoor.isLocked = false;
                            
                            // Si está cerrada (ángulo cercano a cero), abrirla suavemente
                            float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : enemy.transform.rotation);
                            if (angleDiff < 10f || hit.collider.gameObject.name.Contains("Puerta_Panel"))
                            {
                                procDoor.ToggleDoor();
                                Debug.Log("BookHeadPatrolState: El monstruo abrió una puerta cerrada en su camino de patrulla.");
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
                            Debug.Log("BookHeadPatrolState: El monstruo abrió una puerta animada en su camino de patrulla.");
                        }
                    }
                }
            }

            // ─── TIMEOUT ANTI-ESTANCAMIENTO ────────────────────────────────
            // Si lleva más de 6s con velocidad casi cero y tiene un destino, fuerza cambio de punto
            if (agent.hasPath && !agent.pathPending)
            {
                stuckTimer += Time.deltaTime;
                if (agent.velocity.magnitude > 0.15f)
                    stuckTimer = 0f; // Se está moviendo, reiniciar contador

                if (stuckTimer >= stuckTimeout)
                {
                    stuckTimer = 0f;
                    Debug.LogWarning("[Patrol] BookHead atascado (" + stuckTimeout + "s sin moverse) → cambiando punto de patrulla.");
                    MoveToNextPatrolPoint();
                    return;
                }
            }

            if (!agent.pathPending && agent.remainingDistance < 1.2f)
            {
                if (Random.Range(0f, 1f) <= 0.2f)
                {
                    anim?.SetWalking(false);
                    anim?.SetIdle(true);
                    isIdle = true;
                    idleTimer = 0f;
                }
                else
                {
                    MoveToNextPatrolPoint();
                }
            }
            else
            {
                anim?.SetWalking(agent.velocity.magnitude > 0.1f);
            }
        }
    }

    public void ExitState()
    {
        anim?.SetWalking(false);
        anim?.SetIdle(false);
        anim?.SetEating(false);
    }

    // ─── TIMER ANTI-ESTANCAMIENTO ───────────────────────────────────────────
    private float stuckTimer = 0f;
    private const float stuckTimeout = 6f; // Segundos sin moverse antes de cambiar de punto

    // Barrido vertical para encontrar el NavMesh: prueba Y exacto, +/-0.5, +/-1, +/-2, +/-4
    private void TryWarpToNavMesh()
    {
        Vector3 origin = enemy.transform.position;
        float[] yOffsets = { 0f, -0.5f, 0.5f, -1f, 1f, -2f, 2f, -4f, 4f };

        foreach (float dy in yOffsets)
        {
            Vector3 testPos = new Vector3(origin.x, origin.y + dy, origin.z);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPos, out hit, 2f, NavMesh.AllAreas))
            {
                // Intentar Warp al punto exacto del NavMesh
                if (agent.Warp(hit.position))
                {
                    agent.isStopped = false;
                    agent.speed = enemy.walkSpeed;
                    Debug.Log("[Enemy] Warp exitoso al NavMesh en " + hit.position + " (offset Y=" + dy + ")");
                    MoveToNextPatrolPoint();
                    return;
                }
            }
        }
        // Si ninguno funciono, loggear solo cada 2 segundos para no saturar la consola
        if (Time.frameCount % 120 == 0)
            Debug.LogWarning("[Enemy] No se encontro NavMesh valido en ningun offset Y. Verificar que BuildNavMesh() cubre esta zona.");
    }

    private void MoveToNextPatrolPoint()
    {
        if (!agent.isOnNavMesh) return;

        stuckTimer = 0f; // Reiniciar contador al pedir nuevo punto

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            // SI NO HAY PUNTOS MANUALES: Generar un punto de patrulla aleatorio automático en los pasillos del mapa
            TrySetRandomNavMeshDestination();
            return;
        }

        if (patrolPoints.Length == 1)
        {
            if (!SetDestinationSafe(patrolPoints[0].position))
                TrySetRandomNavMeshDestination();
            return;
        }

        // Intentar hasta patrolPoints.Length puntos diferentes para encontrar uno alcanzable
        int attempts = 0;
        int maxAttempts = patrolPoints.Length;
        int randomIndex;

        do
        {
            randomIndex = Random.Range(0, patrolPoints.Length);
            int safety = 0;
            while (randomIndex == currentPatrolIndex && safety < 20)
            {
                randomIndex = Random.Range(0, patrolPoints.Length);
                safety++;
            }
            currentPatrolIndex = randomIndex;
            attempts++;
        }
        while (!SetDestinationSafe(patrolPoints[currentPatrolIndex].position) && attempts < maxAttempts);

        // Si ningún punto es alcanzable, ir a una posición aleatoria del NavMesh
        if (attempts >= maxAttempts && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete))
        {
            Debug.LogWarning("[Patrol] Ningún punto de patrulla es alcanzable. Usando destino aleatorio del NavMesh.");
            TrySetRandomNavMeshDestination();
        }
    }

    /// <summary>
    /// Mueve el agente a un punto aleatorio válido del NavMesh dentro de un radio de búsqueda.
    /// </summary>
    private void TrySetRandomNavMeshDestination()
    {
        Vector3 searchCenter = enemy.transform.position;

        // Si el jugador está presente, el 65% de las veces patrullar en pasillos cercanos al área del jugador
        if (enemy != null && enemy.player != null && Random.value < 0.65f)
        {
            searchCenter = enemy.player.position;
        }

        for (int i = 0; i < 15; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * 18f;
            randomDir += searchCenter;
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
        Debug.LogWarning("[Patrol] No se encontró ningún destino aleatorio alcanzable en el NavMesh.");
    }

    /// <summary>
    /// Valida que el destino sea alcanzable (PathComplete) antes de asignarlo.
    /// Devuelve true si el path fue asignado con éxito, false si el punto es inalcanzable.
    /// </summary>
    private bool SetDestinationSafe(Vector3 dest)
    {
        // Snap al NavMesh mas cercano al destino
        NavMeshHit hit;
        Vector3 finalDest = NavMesh.SamplePosition(dest, out hit, 8f, NavMesh.AllAreas) ? hit.position : dest;

        // Verificar que el camino sea completo (alcanzable) antes de asignarlo
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(finalDest, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning("[Patrol] Punto de patrulla inalcanzable (PathStatus=" + path.status + "). Buscando otro...");
            return false;
        }

        agent.SetPath(path);
        return true;
    }
}
