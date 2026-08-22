using UnityEngine;
using UnityEngine.AI;

public class EnemyStalkState : IEnemyState
{
    private EnemyAIController controller;
    private NavMeshAgent agent;
    private EnemyAnimation anim;
    private Transform player;

    private float flashlightTimer = 0f;
    private float stalkDuration = 0f;
    private Vector3 initialPosition;

    public EnemyStalkState(EnemyAIController controller, NavMeshAgent agent, EnemyAnimation anim, Transform player)
    {
        this.controller = controller;
        this.agent = agent;
        this.anim = anim;
        this.player = player;
    }

    public void EnterState()
    {
        initialPosition = controller.transform.position;
        stalkDuration = 0f;
        flashlightTimer = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.speed = 0f;
        }

        if (anim != null)
        {
            anim.SetIdle(true);
        }

        Debug.Log("[BookHead] Entrando en estado STALK (Acecho e inmovilidad a distancia).");
    }

    public void UpdateState()
    {
        if (player == null) return;

        stalkDuration += Time.deltaTime;
        float distToPlayer = Vector3.Distance(controller.transform.position, player.position);

        // Girar sutilmente el cuerpo hacia la posición del jugador sin moverse
        Vector3 dirToPlayer = (player.position - controller.transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
            controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 3.5f);
        }

        // 1. Si el jugador se acerca a menos de 7.5 metros -> Iniciar persecución directa
        if (distToPlayer <= 7.5f)
        {
            Debug.Log("[BookHead] Jugador demasiado cerca durante STALK. ¡Iniciando CHASE!");
            controller.ChangeState(new EnemyChaseState(controller, agent, anim, player));
            return;
        }

        // 2. Si el jugador está corriendo continuamente -> Iniciar persecución directa
        if (controller.playerSprintDetector != null && controller.playerSprintDetector.IsRunning)
        {
            Debug.Log("[BookHead] Escuchó carrera continua durante STALK. ¡Iniciando CHASE!");
            controller.ChangeState(new EnemyChaseState(controller, agent, anim, player));
            return;
        }

        // 3. Comprobar si la linterna lo alumbra directamente
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = Object.FindFirstObjectByType<Camera>();

        if (mainCam != null)
        {
            FlashlightController flashlight = player.GetComponentInChildren<FlashlightController>();
            if (flashlight != null && flashlight.flashlightLight != null && flashlight.flashlightLight.enabled)
            {
                Vector3 toMonster = (controller.transform.position + Vector3.up * 1.2f - mainCam.transform.position).normalized;
                float dot = Vector3.Dot(mainCam.transform.forward, toMonster);

                if (dot > 0.85f && distToPlayer < 18f)
                {
                    flashlightTimer += Time.deltaTime;
                    if (flashlightTimer >= 1.2f)
                    {
                        Debug.Log("[BookHead] Alumbrado directamente por linterna durante 1.2s. ¡Iniciando CHASE!");
                        controller.ChangeState(new EnemyChaseState(controller, agent, anim, player));
                        return;
                    }
                }
                else
                {
                    flashlightTimer = Mathf.Max(0f, flashlightTimer - Time.deltaTime);
                }
            }
        }

        // 4. Si han pasado más de 12 segundos o si el jugador se alejó a más de 26 metros -> Retirarse a patrulla silenciosa
        if (stalkDuration > 12f || distToPlayer > 26f)
        {
            Debug.Log("[BookHead] Fin de tiempo STALK o jugador alejado. Volviendo a patrulla.");
            controller.ChangeState(new EnemyPatrolState(controller, agent, anim, (controller.patrolPoints != null) ? controller.patrolPoints : new Transform[0]));
        }
    }

    public void ExitState()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
}
