using UnityEngine;
using UnityEngine.AI;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Estado de Advertencia (Crujido de huesos).
    /// Maneja intervalos aleatorios de crujidos según la distancia al jugador:
    /// - 10m - 6m: Crujidos ocasionales.
    /// - 6m - 3m: Crujidos frecuentes y postura amenazante.
    /// - < 3m: Transición a Persecución.
    /// - > 11m: Regresa a Llanto tenue.
    /// </summary>
    public class AmalgamWarningState : IEnemyState
    {
        private readonly AmalgamAIController controller;
        private readonly NavMeshAgent agent;
        private readonly AmalgamAnimation anim;

        private float nextBoneCrackTimer = 0f;
        private bool postureTriggered = false;

        public AmalgamWarningState(AmalgamAIController controller, NavMeshAgent agent, AmalgamAnimation anim)
        {
            this.controller = controller;
            this.agent = agent;
            this.anim = anim;
        }

        public void EnterState()
        {
            Debug.Log("[The Amalgam] Entrando en estado: Warning (Crujido de Huesos)");
            if (anim != null) anim.SetWarning(true);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            postureTriggered = false;
            ScheduleNextCrack(3.5f, 5.5f);
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTransform.position);

            // Orientarse lentamente hacia el jugador durante el aviso
            Vector3 lookDirection = (controller.PlayerTransform.position - controller.transform.position).normalized;
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 3.5f);
            }

            // 1. Si se aleja > 11m -> Regresar a Idle (Llanto tenue)
            if (distanceToPlayer > controller.warningDistance + 1.0f)
            {
                controller.ChangeState(new AmalgamIdleCryingState(controller, agent, anim));
                return;
            }

            // 2. Si se acerca a < 3m -> Persecución inmediata
            if (distanceToPlayer <= controller.chaseDistance)
            {
                controller.ChangeState(new AmalgamChaseState(controller, agent, anim));
                return;
            }

            // 3. Manejo de crujidos de huesos e intensidad según rango (6m - 3m vs 10m - 6m)
            bool isCloseRange = distanceToPlayer <= controller.mediumWarningDistance; // <= 6m

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
                    ScheduleNextCrack(1.2f, 2.2f); // Crujidos frecuentes
                }
                else
                {
                    ScheduleNextCrack(3.5f, 5.5f); // Crujidos ocasionales
                }
            }
        }

        public void ExitState()
        {
            if (anim != null) anim.SetWarning(false);
        }

        private void ScheduleNextCrack(float minTime, float maxTime)
        {
            nextBoneCrackTimer = Random.Range(minTime, maxTime);
        }
    }
}
