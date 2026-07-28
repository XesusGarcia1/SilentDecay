using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAttackState : IEnemyState
{
    private EnemyAIController enemy;
    private NavMeshAgent agent;
    private EnemyAnimation anim;
    private Transform player;
    private float attackRange = 2f;  // Rango del ataque
    private float attackDamage = 30f;  // Daño del ataque
    private float attackCooldown = 1.5f;  // Cooldown entre ataques
    private float attackAnimationDuration = 0.8f;  // Duración de la animación de golpe

    private bool isAttacking = false;
    private Coroutine attackCoroutine;

    public EnemyAttackState(EnemyAIController enemy, NavMeshAgent agent, EnemyAnimation anim, Transform player)
    {
        this.enemy = enemy;
        this.agent = agent;
        this.anim = anim;
        this.player = player;
        this.attackRange = enemy.attackRange; // Sincronizar con el controlador
    }

    public void EnterState()
    {
        Debug.Log("Enemigo entra en estado de ataque.");
        agent.ResetPath(); // Detener movimiento durante el ataque
        attackCoroutine = enemy.StartCoroutine(PerformAttackLoop());
    }

    public void UpdateState()
    {
        // El controlador EnemyAIController maneja la salida a Chase o Patrol en HandleStateTransitions,
        // pero mantenemos esto por redundancia y compatibilidad.
        if (Vector3.Distance(enemy.transform.position, player.position) > attackRange)
        {
            enemy.ChangeState(new EnemyChaseState(enemy, agent, anim, player));
        }
    }

    public void ExitState()
    {
        isAttacking = false;
        if (attackCoroutine != null)
        {
            enemy.StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        anim?.SetAttacking(false);
    }

    private IEnumerator PerformAttackLoop()
    {
        isAttacking = true;

        while (isAttacking)
        {
            // 1. Iniciar animación de ataque
            anim?.SetAttacking(true);
            yield return new WaitForSeconds(attackAnimationDuration);

            // 2. Comprobar si el jugador sigue en rango para aplicar daño
            float distance = Vector3.Distance(enemy.transform.position, player.position);
            if (distance <= attackRange)
            {
                Collider[] hitColliders = Physics.OverlapSphere(enemy.transform.position, attackRange);
                bool playerHit = false;

                foreach (Collider hitCollider in hitColliders)
                {
                    if (hitCollider.CompareTag("Player") && !playerHit)
                    {
                        playerHit = true;
                        PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(attackDamage);
                            Debug.Log("Jugador ha recibido daño: " + attackDamage);
                        }
                    }
                }
            }

            // 3. Desactivar flag de animación y esperar cooldown
            anim?.SetAttacking(false);
            yield return new WaitForSeconds(attackCooldown);
        }
    }
}
