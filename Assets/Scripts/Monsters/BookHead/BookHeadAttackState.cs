using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BookHeadAttackState : IEnemyState
{
    private BookHeadAIController enemy;
    private NavMeshAgent agent;
    private BookHeadAnimation anim;
    private Transform player;
    private float attackRange = 2f;  // Rango del ataque

    private bool isAttacking = false;
    private Coroutine attackCoroutine;

    public BookHeadAttackState(BookHeadAIController enemy, NavMeshAgent agent, BookHeadAnimation anim, Transform player)
    {
        this.enemy = enemy;
        this.agent = agent;
        this.anim = anim;
        this.player = player;
        this.attackRange = enemy.attackRange; // Sincronizar con el controlador
    }

    public void EnterState()
    {
        Debug.Log("Enemigo entra en estado de ataque (Jumpscare Instantáneo).");
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
        
        attackCoroutine = enemy.StartCoroutine(PerformAttackJumpscare());
    }

    public void UpdateState()
    {
        if (player != null && isAttacking)
        {
            Vector3 direction = (player.position - enemy.transform.position).normalized;
            direction.y = 0f;
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, lookRot, Time.deltaTime * 5f);
            }
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
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private IEnumerator PerformAttackJumpscare()
    {
        isAttacking = true;

        // Ya no hacemos animación de ataque (puñetazo).
        // En su lugar, si te toca, mueres al instante como si te hubiera atrapado.
        
        float distance = Vector3.Distance(enemy.transform.position, player.position);
        if (distance <= attackRange + 0.5f)
        {
            Collider[] hitColliders = Physics.OverlapSphere(enemy.transform.position, attackRange + 0.5f);
            bool playerHit = false;

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player") && !playerHit)
                {
                    playerHit = true;
                    PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        Debug.Log("Jugador atrapado por BookHead. Initiating Jumpscare Death.");
                        playerHealth.TakeDamage(9999f); // Muerte instantánea que dispara el Jumpscare en PlayerHealth
                    }
                }
            }
        }

        // Si por alguna razón el collider no lo detectó (muy raro), vuelve a perseguir
        yield return new WaitForSeconds(0.2f);
        
        isAttacking = false;
        enemy.ChangeState(new BookHeadChaseState(enemy, agent, anim, player));
    }
}
