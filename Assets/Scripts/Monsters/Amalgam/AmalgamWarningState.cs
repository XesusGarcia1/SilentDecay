using UnityEngine;
using UnityEngine.AI;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Estado de Advertencia (Crujido de huesos).
    /// Maneja intervalos aleatorios de crujidos según la distancia al jugador.
    /// EXIGE LÍNEA DE VISIÓN DIRECTA (LINE OF SIGHT) PARA INICIAR LA PERSECUCIÓN.
    /// Si el jugador está resguardado tras paredes o puertas cerradas, NO arranca en chase.
    /// </summary>
    public class AmalgamWarningState : IEnemyState
    {
        private readonly AmalgamAIController controller;
        private readonly NavMeshAgent agent;
        private readonly AmalgamAnimation anim;

        private float nextBoneCrackTimer = 0f;
        private bool postureTriggered = false;
        private float panicTimer = 0f;

        public AmalgamWarningState(AmalgamAIController controller, NavMeshAgent agent, AmalgamAnimation anim)
        {
            this.controller = controller;
            this.agent = agent;
            this.anim = anim;
        }

        public void EnterState()
        {
            Debug.Log("[The Amalgam] Entrando en estado: Warning (Crujido de Huesos)");
            if (controller != null) controller.StopChaseAudio();
            if (anim != null) anim.SetWarning(true);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            postureTriggered = false;
            panicTimer = 0f;
            ScheduleNextCrack(3.5f, 5.5f);
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTransform.position);

            // Escalamiento de latidos de pánico según cercanía
            controller.UpdateHeartbeatPanic(distanceToPlayer, panicTimer / 2.2f);

            // Orientarse hacia la dirección del jugador
            Vector3 lookDirection = (controller.PlayerTransform.position - controller.transform.position).normalized;
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 4.5f);
            }

            // 1. Si se aleja > warningDistance -> Regresar a Idle (Llanto tenue)
            if (distanceToPlayer > controller.warningDistance + 1.0f)
            {
                controller.StopHeartbeatAudio();
                controller.ChangeState(new AmalgamIdleCryingState(controller, agent, anim));
                return;
            }

            // 2. Acumular pánico al quedarse cerca (<= 7.0m): Si el jugador se queda parado frente al monstruo, alcanza el Punto de Pánico y ataca
            if (distanceToPlayer <= 7.0f)
            {
                panicTimer += Time.deltaTime;
                if (panicTimer >= 2.0f)
                {
                    Debug.Log("[The Amalgam] 💓 ¡PUNTO MÁXIMO DE PÁNICO ALCANZADO! Ataque inmediato...");
                    controller.StopHeartbeatAudio();
                    controller.ChangeState(new AmalgamChaseState(controller, agent, anim));
                    return;
                }
            }
            else
            {
                panicTimer = Mathf.Max(0f, panicTimer - Time.deltaTime);
            }

            // 3. Transición a Persecución (¡Inmediata a quemarropa <= 4.5m o por línea de visión!)
            if (distanceToPlayer <= 4.5f)
            {
                controller.StopHeartbeatAudio();
                controller.ChangeState(new AmalgamChaseState(controller, agent, anim));
                return;
            }

            if (distanceToPlayer <= controller.chaseDistance)
            {
                bool hasLineOfSight = controller.HasLineOfSightToPlayer();

                if (hasLineOfSight)
                {
                    if (controller.chaseCooldownTimer <= 0f || distanceToPlayer <= 4.5f)
                    {
                        controller.StopHeartbeatAudio();
                        controller.ChangeState(new AmalgamChaseState(controller, agent, anim));
                        return;
                    }
                }
            }

            // 4. Manejo de crujidos de huesos e intensidad según rango
            bool isCloseRange = distanceToPlayer <= controller.mediumWarningDistance;

            if (isCloseRange && !postureTriggered)
            {
                postureTriggered = true;
                if (anim != null) anim.TriggerThreatPosture();
            }

            nextBoneCrackTimer -= Time.deltaTime;
            if (nextBoneCrackTimer <= 0f)
            {
                controller.PlayBoneCrackAudio();

                if (isCloseRange)
                {
                    ScheduleNextCrack(1.2f, 2.2f);
                }
                else
                {
                    ScheduleNextCrack(3.5f, 5.5f);
                }
            }
        }

        public void ExitState()
        {
            if (anim != null) anim.SetWarning(false);
            if (controller != null) controller.StopHeartbeatAudio();
        }

        private void ScheduleNextCrack(float minTime, float maxTime)
        {
            nextBoneCrackTimer = Random.Range(minTime, maxTime);
        }
    }
}
