using UnityEngine;
using UnityEngine.AI;

public class EnemyChaseState : IEnemyState
{
    private EnemyAIController enemy;
    private NavMeshAgent agent;
    private EnemyAnimation anim;
    private Transform player;

    private float maxChaseDistance = 25f;
    private float searchDuration = 8f; // Tiempo de busqueda total tras perder al jugador
    private float searchTimer = 0f;

    private bool playerLost = false;
    private Vector3 lastKnownPlayerPosition;
    private bool isRunningSoundPlaying = false;

    public EnemyChaseState(EnemyAIController enemy, NavMeshAgent agent, EnemyAnimation anim, Transform player)
    {
        this.enemy = enemy;
        this.agent = agent;
        this.anim = anim;
        this.player = player;
    }

    public void EnterState()
    {
        Debug.Log("Enemigo detecta jugador - inicia persecucion.");

        playerLost = false;
        searchTimer = 0f;
        isRunningSoundPlaying = false;
        lastKnownPlayerPosition = player.position;

        anim?.SetIdle(false);
        anim?.SetWalking(false);
        anim?.SetRunning(true);

        agent.isStopped = false;
        agent.speed = enemy.runSpeed;
        agent.stoppingDistance = 1.5f;
        agent.SetDestination(player.position);
    }

    public void UpdateState()
    {
        if (!agent.isOnNavMesh) return;

        float distanceToPlayer = Vector3.Distance(enemy.transform.position, player.position);
        FieldOfView fov = enemy.GetComponent<FieldOfView>();

        // El enemigo tiene rastro en tiempo real del jugador SOLO si lo ve visualmente 
        // o si lo escucha porque el jugador está corriendo dentro del radio de audición
        bool canSee = fov != null && fov.CanSeePlayer();
        bool canHear = fov != null && enemy.playerSprintDetector != null && enemy.playerSprintDetector.IsRunning && distanceToPlayer <= fov.hearingRadius;

        if (canSee || canHear)
        {
            // Jugador visto o escuchado corriendo - registrar posición en tiempo real
            lastKnownPlayerPosition = player.position;
            playerLost = false;
            searchTimer = 0f;

            if (distanceToPlayer <= agent.stoppingDistance)
            {
                agent.ResetPath();
                anim?.SetRunning(false);
                StopRunningSound();
                return;
            }

            Vector3 direction = (player.position - enemy.transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, lookRotation, Time.deltaTime * 8f);
            }

            float targetSpeed = enemy.runSpeed;
            if (enemy.playerSprintDetector != null && enemy.playerSprintDetector.IsRunning)
            {
                // Si el jugador sigue corriendo en pánico, BookHead entra en frenesí auditivo (mayor velocidad y rango de oído)
                targetSpeed = enemy.runSpeed + 1.2f; // Velocidad frenética de carrera (hasta 6.8 m/s)
                if (fov != null) fov.hearingRadius = 75f;
            }
            else
            {
                float speedFactor = Mathf.InverseLerp(5f, maxChaseDistance, distanceToPlayer);
                targetSpeed = Mathf.Lerp(enemy.walkSpeed, enemy.runSpeed, speedFactor);
            }

            agent.speed = targetSpeed;
            agent.SetDestination(player.position);

            bool isRunning = agent.velocity.magnitude > 0.5f;
            anim?.SetRunning(isRunning);
            if (isRunning && !isRunningSoundPlaying)
            {
                PlayRunningSound();
                isRunningSoundPlaying = true;
            }
            else if (!isRunning && isRunningSoundPlaying)
            {
                StopRunningSound();
                isRunningSoundPlaying = false;
            }
        }
        else
        {
            // Jugador fuera del rango - buscar
            if (!playerLost)
            { 
                playerLost = true;
                searchTimer = 0f;
                anim?.SetRunning(false);
                anim?.SetWalking(true); // Cambiar a caminar en vez de deslizarse rígido
                agent.speed = enemy.walkSpeed * 1.2f; // Trote ligero de alerta
                agent.SetDestination(lastKnownPlayerPosition);
                StopRunningSound();
                isRunningSoundPlaying = false;
            }
            else
            {
                // Timer incondicional para evitar atascarse si la ruta a la ultima posicion es invalida
                searchTimer += Time.deltaTime;

                if (searchTimer < searchDuration)
                { 
                    // Los primeros 3 segundos va a la ultima posicion, luego busca alrededor
                    if (searchTimer > 3f && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
                    {
                        Vector3 randomDir = Random.insideUnitSphere * 4f + lastKnownPlayerPosition;
                        NavMeshHit navHit;
                        if (NavMesh.SamplePosition(randomDir, out navHit, 4f, NavMesh.AllAreas))
                        { 
                            agent.SetDestination(navHit.position);
                        }
                    }

                    // Control de animación dinámico durante la búsqueda:
                    // Si el agente se mueve físicamente, reproducir caminar. De lo contrario, Idle (Still).
                    if (agent.velocity.magnitude > 0.15f && !agent.isStopped)
                    {
                        anim?.SetWalking(true);

                        // Raycast para abrir puertas en su camino de búsqueda
                        Vector3 rayOrigin = enemy.transform.position + Vector3.up * 1.2f;
                        Vector3 rayDir = enemy.transform.forward;
                        RaycastHit hit;
                        if (Physics.Raycast(rayOrigin, rayDir, out hit, 2.0f))
                        {
                            ProceduralDoorInteract procDoor = hit.collider.GetComponentInParent<ProceduralDoorInteract>();
                            if (procDoor == null) procDoor = hit.collider.GetComponent<ProceduralDoorInteract>();
                            if (procDoor != null)
                            {
                                // La oficina del director requiere descifrar el Keypad obligatoriamente.
                                // El monstruo NUNCA debe abrir ni forzar esta puerta especial.
                                if (procDoor.gameObject.name.Contains("PuertaDirector"))
                                {
                                    // Ignorar
                                }
                                else
                                {
                                    if (procDoor.isLocked) procDoor.isLocked = false;
                                    float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : enemy.transform.rotation);
                                    if (angleDiff < 10f || hit.collider.gameObject.name.Contains("Puerta_Panel"))
                                    {
                                        procDoor.ToggleDoor();
                                        Debug.Log("[ChaseState] El monstruo abrió una puerta durante la búsqueda.");
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
                                    Debug.Log("[ChaseState] El monstruo abrió una puerta animada durante la búsqueda.");
                                }
                            }
                        }
                    }
                    else
                    {
                        anim?.SetIdle(true);
                    }
                }
                else
                {
                    // Al final de la búsqueda de alerta, inspeccionar una cama cercana si hay una
                    Bed[] beds = Object.FindObjectsByType<Bed>(FindObjectsSortMode.None);
                    Bed nearBed = null;
                    float closestBedDist = 8f; // Radio máximo de inspección

                    foreach (var bed in beds)
                    {
                        if (bed != null)
                        {
                            float d = Vector3.Distance(enemy.transform.position, bed.transform.position);
                            if (d < closestBedDist)
                            {
                                closestBedDist = d;
                                nearBed = bed;
                            }
                        }
                    }

                    if (nearBed != null)
                    {
                        Debug.Log("[ChaseState] Búsqueda finalizada. Inspeccionando cama cercana a " + closestBedDist + "m.");
                        enemy.ChangeState(new EnemyCrouchInspectState(enemy, agent, anim, nearBed));
                    }
                    else
                    {
                        Debug.Log("[ChaseState] Persecución y búsqueda finalizadas. BookHead desaparece en las sombras (Reposición Silenciosa).");
                        enemy.TrySilentReposition();
                    }
                }
            }
        }
    }

    public void ExitState()
    {
        anim?.SetRunning(false);
        anim?.SetIdle(false);
        StopRunningSound();
        isRunningSoundPlaying = false;
    }

    private void PlayRunningSound()
    {
        if (enemy.footstepAudioSource != null && enemy.footstepSoundClip != null)
        {
            enemy.footstepAudioSource.clip = enemy.footstepSoundClip;
            enemy.footstepAudioSource.loop = true;
            enemy.footstepAudioSource.pitch = 1.5f;
            enemy.footstepAudioSource.spatialBlend = 1f;
            enemy.footstepAudioSource.Play();
        }
    }

    private void StopRunningSound()
    {
        if (enemy.footstepAudioSource != null && enemy.footstepAudioSource.isPlaying)
            enemy.footstepAudioSource.Stop();
    }
}
