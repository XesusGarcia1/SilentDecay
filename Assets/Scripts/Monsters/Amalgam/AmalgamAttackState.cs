using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Estado de Ataque Final y Jumpscare (TerrifyScreamAmalgam).
    /// El monstruo se detiene, cambia de postura (sale de Idle), enfoca al jugador, 
    /// ejecuta la animación de ataque, lanza el grito TerrifyScreamAmalgam y aplica la muerte instantánea al jugador.
    /// </summary>
    public class AmalgamAttackState : IEnemyState
    {
        private readonly AmalgamAIController controller;
        private readonly NavMeshAgent agent;
        private readonly AmalgamAnimation anim;

        private Coroutine attackCoroutine;
        private bool isAttacking = false;

        public AmalgamAttackState(AmalgamAIController controller, NavMeshAgent agent, AmalgamAnimation anim)
        {
            this.controller = controller;
            this.agent = agent;
            this.anim = anim;
        }

        public void EnterState()
        {
            Debug.Log("[The Amalgam] ¡ATRAPÓ AL JUGADOR! Entrando en estado: Attack & TerrifyScreamAmalgam");
            
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }

            // Cambiar postura inmediatamente saliendo de Idle
            if (anim != null)
            {
                anim.ResetAllStateBools();
                anim.TriggerAttack();
            }

            attackCoroutine = controller.StartCoroutine(PerformAttackSequence());
        }

        public void UpdateState()
        {
            if (controller.PlayerTransform != null && isAttacking)
            {
                Vector3 lookDirection = (controller.PlayerTransform.position - controller.transform.position).normalized;
                lookDirection.y = 0f;
                if (lookDirection != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                    controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 10.0f);
                }
            }
        }

        public void ExitState()
        {
            isAttacking = false;
            if (attackCoroutine != null)
            {
                controller.StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
        }

        private IEnumerator PerformAttackSequence()
        {
            isAttacking = true;

            // 1. Reproducir grito aterrador exclusivo de muerte (TerrifyScreamAmalgam)
            controller.PlayTerrifyScreamAudio();

            // 2. Disparar animación de ataque y postura
            if (anim != null)
            {
                anim.TriggerAttack();
            }

            yield return new WaitForSeconds(0.15f);

            // 3. Aplicar daño letal / Jumpscare al jugador
            if (controller.PlayerTransform != null)
            {
                PlayerHealth pHealth = controller.PlayerTransform.GetComponent<PlayerHealth>();
                if (pHealth == null) pHealth = controller.PlayerTransform.GetComponentInChildren<PlayerHealth>();

                if (pHealth != null)
                {
                    Debug.Log("[The Amalgam] Aplicando daño letal (9999) al PlayerHealth.");
                    pHealth.TakeDamage(9999f);
                }
                else
                {
                    Debug.LogWarning("[The Amalgam] PlayerHealth no encontrado en el jugador. Verificar componentes.");
                }
            }

            yield return new WaitForSeconds(1.5f);
            isAttacking = false;
        }
    }
}
