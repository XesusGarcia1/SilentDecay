using UnityEngine;
using UnityEngine.AI;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Estado de Persecución (Chase_Amalgam).
    /// Reproduce el audio Chase_Amalgam en loop, activa la animación de carrera,
    /// dispara el efecto terrorífico de fallo/oscuridad de luces, comprueba puertas en el camino
    /// y persigue al jugador físicamente sin frenarse hasta que termine la ráfaga de tiempo o alcance al jugador.
    /// </summary>
    public class AmalgamChaseState : IEnemyState
    {
        private readonly AmalgamAIController controller;
        private readonly NavMeshAgent agent;
        private readonly AmalgamAnimation anim;

        private float losePlayerTimer = 0f;
        private float chaseTimer = 0f;
        private float targetChaseDuration = 12.0f;
        private Vector2 lastPosition2D = Vector2.zero;
        private float stuckTimer = 0f;

        public AmalgamChaseState(AmalgamAIController controller, NavMeshAgent agent, AmalgamAnimation anim)
        {
            this.controller = controller;
            this.agent = agent;
            this.anim = anim;
        }

        public void EnterState()
        {
            targetChaseDuration = Random.Range(10.0f, 15.0f);
            Debug.Log($"[The Amalgam] Entrando en estado: PERSECUCIÓN DIRECTA (Duración aleatoria: {targetChaseDuration:F1}s)");
            
            if (anim != null) anim.SetRunning(true);

            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                agent.isStopped = false;
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.speed = controller.runSpeed;
                agent.angularSpeed = 720f; // Giro ultra veloz (evita rodeos amplios en esquinas)
                agent.acceleration = 40f;   // Aceleración inmediata
                agent.autoBraking = false;  // Sin frenos en esquinas
                agent.stoppingDistance = 0.5f;

                if (controller.PlayerTransform != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(controller.PlayerTransform.position);
                }
            }

            if (controller.PlayerTransform != null)
            {
                lastPosition2D = new Vector2(controller.transform.position.x, controller.transform.position.z);
            }

            controller.PlayChaseAudio();
            controller.TriggerHorrorLightFlickerSequence();
            losePlayerTimer = 0f;
            chaseTimer = 0f;
            stuckTimer = 0f;
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform == null) return;

            chaseTimer += Time.deltaTime;

            // Mantener forzada la animación de carrera durante toda la persecución
            if (anim != null) anim.SetRunning(true);

            // Comprobar y golpear/abrir puertas en el camino (tocar-la-puerta)
            controller.CheckAndOpenDoorsInPath();
            controller.ForcePhaseThroughDoorwayIfNeeded();

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTransform.position);

            // 1. Garantizar movimiento y persecución activa
            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                if (agent.isStopped) agent.isStopped = false;

                agent.angularSpeed = 720f;
                agent.acceleration = 40f;
                agent.autoBraking = false;

                // Reenganchar al NavMesh si por teletransporte se desfasó levemente
                if (!agent.isOnNavMesh)
                {
                    NavMeshHit navHit;
                    if (NavMesh.SamplePosition(controller.transform.position, out navHit, 4.0f, NavMesh.AllAreas))
                    {
                        agent.Warp(navHit.position);
                    }
                }

                if (agent.isOnNavMesh)
                {
                    agent.speed = controller.runSpeed;
                    agent.SetDestination(controller.PlayerTransform.position);

                    // Orientar la rotación directamente hacia la dirección del camino o del jugador si hay desvío de ángulo
                    if (agent.velocity.sqrMagnitude > 0.1f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
                        controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 15.0f);
                    }
                }
                else
                {
                    // Fallback físico: Avance directo hacia el jugador si el punto NavMesh está desalineado
                    Vector3 moveDir = (controller.PlayerTransform.position - controller.transform.position).normalized;
                    moveDir.y = 0f;
                    controller.transform.position += moveDir * controller.runSpeed * Time.deltaTime;
                    if (moveDir != Vector3.zero)
                    {
                        controller.transform.rotation = Quaternion.LookRotation(moveDir);
                    }
                }
            }

            // Detección y corrección de atascamiento (Anti-Stuck / Correr en el mismo lugar)
            Vector2 currentPos2D = new Vector2(controller.transform.position.x, controller.transform.position.z);
            float distMoved = Vector2.Distance(currentPos2D, lastPosition2D);
            lastPosition2D = currentPos2D;

            if (distMoved < 0.04f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 0.30f)
                {
                    // Nudge/Empuje físico hacia la dirección del jugador para salir del atolladero de la pared/marco
                    Vector3 pushDir = (controller.PlayerTransform.position - controller.transform.position).normalized;
                    pushDir.y = 0f;
                    controller.transform.position += pushDir * (controller.runSpeed * 0.8f) * Time.deltaTime;
                    if (pushDir != Vector3.zero)
                    {
                        controller.transform.rotation = Quaternion.LookRotation(pushDir);
                    }
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            // 2. Comprobar si alcanza al jugador para atacar (<= attackRange)
            if (distanceToPlayer <= controller.attackRange)
            {
                controller.ChangeState(new AmalgamAttackState(controller, agent, anim));
                return;
            }

            // 3. Expiración de la ráfaga de persecución (aleatoria 10s a 15s):
            if (chaseTimer >= targetChaseDuration)
            {
                Debug.Log($"[The Amalgam] Ráfaga de persecución ({targetChaseDuration:F1}s) concluida. Monstruo se desvanece/relocaliza...");
                controller.chaseCooldownTimer = Random.Range(10.0f, 15.0f); // Respiro de calma tras persecución
                controller.StopChaseAudio();
                controller.TrySilentRelocate();
                controller.ChangeState(new AmalgamIdleCryingState(controller, agent, anim));
                return;
            }

            // 4. Comprobar si el jugador logró escapar (> 28m)
            if (distanceToPlayer > 28.0f)
            {
                losePlayerTimer += Time.deltaTime;
                if (losePlayerTimer >= 3.0f)
                {
                    Debug.Log("[The Amalgam] El jugador logró escapar a gran distancia. Relocalizando...");
                    controller.chaseCooldownTimer = Random.Range(12.0f, 18.0f);
                    controller.StopChaseAudio();
                    controller.TrySilentRelocate();
                    controller.ChangeState(new AmalgamIdleCryingState(controller, agent, anim));
                }
            }
            else
            {
                losePlayerTimer = 0f;
            }
        }

        public void ExitState()
        {
            if (anim != null) anim.SetRunning(false);
            controller.StopChaseAudio();
        }
    }
}
