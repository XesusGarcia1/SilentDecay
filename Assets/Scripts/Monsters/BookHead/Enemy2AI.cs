using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Enemy2AI : MonoBehaviour
{
    public Transform target; // El objetivo que el enemigo debe seguir
    public Transform[] patrolPoints; // Puntos dentro de la zona marcada
    public float movementSpeed = 3.0f; // Velocidad de movimiento del enemigo
    private int currentPatrolIndex = 0;

    private NavMeshAgent navMeshAgent;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.speed = movementSpeed;

        if (patrolPoints.Length > 0)
        {
            SetDestinationToNextPatrolPoint();
        }
        else
        {
            Debug.LogWarning("No se han asignado puntos de patrulla.");
        }
    }

    void Update()
    {
        // Si hay un objetivo asignado, sigue al objetivo
        if (target != null)
        {
            navMeshAgent.SetDestination(target.position);
        }
        else
        {
            // Si no hay un objetivo, vaga por los puntos asginados
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
            {
                SetDestinationToNextPatrolPoint();
            }
        }
    }

    void SetDestinationToNextPatrolPoint()
    {
        if (patrolPoints.Length == 0)
            return;

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        navMeshAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
    }
}
