using UnityEngine;
using UnityEngine.AI;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Estado Inactivo / Llanto en 3D para The Amalgam.
    /// El monstruo permanece en su posición o patrulla tenue emitiendo HOMBRELLORANDO.
    /// Si el jugador se acerca a <= warningDistance (10m), cambia a AmalgamWarningState.
    /// </summary>
    public class AmalgamIdleCryingState : IEnemyState
    {
        private readonly AmalgamAIController controller;
        private readonly NavMeshAgent agent;
        private readonly AmalgamAnimation anim;

        public AmalgamIdleCryingState(AmalgamAIController controller, NavMeshAgent agent, AmalgamAnimation anim)
        {
            this.controller = controller;
            this.agent = agent;
            this.anim = anim;
        }

        public void EnterState()
        {
            Debug.Log("[The Amalgam] Entrando en estado: Idle Crying (HOMBRELLORANDO)");
            if (anim != null) anim.SetCrying(true);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            controller.PlayCryingAudio();
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTransform.position);

            // Transición a Warning si el jugador se acerca a la zona de peligro (<=10m)
            if (distanceToPlayer <= controller.warningDistance)
            {
                controller.ChangeState(new AmalgamWarningState(controller, agent, anim));
            }
        }

        public void ExitState()
        {
            if (anim != null) anim.SetCrying(false);
        }
    }
}
