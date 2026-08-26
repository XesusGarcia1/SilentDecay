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

                if (controller.PlayerTransform != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(controller.PlayerTransform.position);
                }
            }

            controller.PlayChaseAudio();
            controller.TriggerHorrorLightFlickerSequence();
            losePlayerTimer = 0f;
            chaseTimer = 0f;
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform == null) return;

            chaseTimer += Time.deltaTime;

            // Mantener forzada la animación de carrera durante toda la persecución
            if (anim != null) anim.SetRunning(true);

            // Comprobar y golpear/abrir puertas en el camino (tocar-la-puerta)
            controller.CheckAndOpenDoorsInPath();

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTransform.position);

            // 1. Garantizar movimiento y persecución activa
            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                if (agent.isStopped) agent.isStopped = false;

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
