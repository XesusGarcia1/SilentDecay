using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BookHeadCrouchInspectState : IEnemyState
{
    private BookHeadAIController enemy;
    private NavMeshAgent agent;
    private BookHeadAnimation anim;
    private Bed targetBed;

    private float timer = 0f;
    private int phase = 0; // -1 = Caminando a la cama, 0 = Rotando, 1 = Inclinándose, 2 = Espera/Inspección, 3 = Enderezándose

    private Quaternion targetLookRotation;
    private Transform modelTransform;
    
    private float totalInclineTime = 1.0f;
    private float holdTime = 1.8f;
    private float recoverTime = 1.0f;

    private float currentXIncline = 0f;
    private Quaternion originalModelLocalRotation;
    private Vector3 targetWalkPosition;

    public BookHeadCrouchInspectState(BookHeadAIController enemy, NavMeshAgent agent, BookHeadAnimation anim, Bed bed)
    {
        this.enemy = enemy;
        this.agent = agent;
        this.anim = anim;
        this.targetBed = bed;
    }

    public void EnterState()
    {
        Debug.Log("[InspectState] Iniciando aproximación e inspección de cama.");
        
        // El modelo del monstruo para inclinarlo
        modelTransform = enemy.transform.Find("Visual") ?? enemy.transform.Find("Model") ?? enemy.transform;
        if (modelTransform == null) modelTransform = enemy.transform;
        originalModelLocalRotation = modelTransform.localRotation;

        // Calcular la posición a la cual debe caminar el monstruo al lado de la cama
        // Intentar usar hidePosition o estar muy cerca del lateral
        if (targetBed.hidePosition != null)
        {
            targetWalkPosition = targetBed.hidePosition.position;
        }
        else
        {
            targetWalkPosition = targetBed.transform.position;
        }

        // Snap al NavMesh más cercano
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetWalkPosition, out hit, 4f, NavMesh.AllAreas))
        {
            targetWalkPosition = hit.position;
        }

        agent.isStopped = false;
        agent.speed = enemy.walkSpeed;
        agent.SetDestination(targetWalkPosition);
        anim?.SetWalking(true);
        anim?.SetRunning(false);
        anim?.SetIdle(false);

        timer = 0f;
        phase = -1; // Iniciar en fase de caminar
    }

    public void UpdateState()
    {
        timer += Time.deltaTime;

        if (phase == -1) // Fase -1: Caminar al lateral de la cama
        {
            // Raycast dinámico para abrir puertas en su camino a la cama
            if (agent.velocity.magnitude > 0.1f && !agent.isStopped)
            {
                Vector3 rayOrigin = enemy.transform.position + Vector3.up * 1.2f;
                Vector3 rayDir = enemy.transform.forward;
                RaycastHit doorHit;
                if (Physics.Raycast(rayOrigin, rayDir, out doorHit, 2.0f))
                {
                    ProceduralDoorInteract procDoor = doorHit.collider.GetComponentInParent<ProceduralDoorInteract>();
                    if (procDoor == null) procDoor = doorHit.collider.GetComponent<ProceduralDoorInteract>();
                    if (procDoor != null)
                    {
                        if (procDoor.isLocked) procDoor.isLocked = false;
                        float angleDiff = Quaternion.Angle(procDoor.transform.localRotation, procDoor.transform.parent != null ? Quaternion.identity : enemy.transform.rotation);
                        if (angleDiff < 10f || doorHit.collider.gameObject.name.Contains("Puerta_Panel"))
                        {
                            procDoor.ToggleDoor();
                            Debug.Log("[InspectState] El monstruo abrió una puerta para entrar a inspeccionar.");
                        }
                    }

                    OpenDoor animDoor = doorHit.collider.GetComponentInParent<OpenDoor>();
                    if (animDoor == null) animDoor = doorHit.collider.GetComponent<OpenDoor>();
                    if (animDoor != null)
                    {
                        if (animDoor.isLocked) animDoor.isLocked = false;
                        if (animDoor.doorAnimator != null && !animDoor.doorAnimator.GetBool("isOpen"))
                        {
                            animDoor.doorAnimator.SetBool("isOpen", true);
                            if (animDoor.audioSource && animDoor.doorOpenSound)
                            {
                                animDoor.audioSource.PlayOneShot(animDoor.doorOpenSound, 1.0f);
                            }
                            Debug.Log("[InspectState] El monstruo abrió una puerta animada para entrar a inspeccionar.");
                        }
                    }
                }
            }

            // Si llegamos cerca de la posición
            if (!agent.pathPending && (agent.remainingDistance <= 1.2f || timer > 6f))
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                anim?.SetWalking(false);
                anim?.SetIdle(true);

                // Calcular rotación de mirada de frente a la cama
                Vector3 dirToBed = (targetBed.transform.position - enemy.transform.position);
                dirToBed.y = 0;
                if (dirToBed != Vector3.zero)
                {
                    targetLookRotation = Quaternion.LookRotation(dirToBed.normalized);
                }
                else
                {
                    targetLookRotation = enemy.transform.rotation;
                }

                phase = 0;
                timer = 0f;
            }
        }
        else if (phase == 0) // Fase 0: Rotar suavemente hacia la cama
        {
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, targetLookRotation, Time.deltaTime * 6f);
            if (Quaternion.Angle(enemy.transform.rotation, targetLookRotation) < 5f || timer > 1f)
            {
                phase = 1;
                timer = 0f;
            }
        }
        else if (phase == 1) // Fase 1: Inclinarse hacia adelante (X)
        {
            float t = Mathf.Clamp01(timer / totalInclineTime);
            float tSmooth = Mathf.Sin(t * Mathf.PI * 0.5f); 
            currentXIncline = Mathf.Lerp(0f, 32f, tSmooth); // Inclinación de 32 grados

            modelTransform.localRotation = originalModelLocalRotation * Quaternion.Euler(currentXIncline, 0f, 0f);

            if (t >= 1f)
            {
                phase = 2;
                timer = 0f;
            }
        }
        else if (phase == 2) // Fase 2: Inspección / Espera abajo de la colcha
        {
            if (timer >= holdTime)
            {
                // Chequear descubrimiento si el jugador está en esta cama
                HideUnderBed hideScript = Object.FindAnyObjectByType<HideUnderBed>();
                if (hideScript != null && hideScript.isHiding && hideScript.targetBed == targetBed)
                {
                    // Chequeo de probabilidad basado en dificultad
                    string diff = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
                    float discoveryChance = 0.40f; // 40% Normal
                    if (diff == "FACIL") discoveryChance = 0.15f;
                    else if (diff == "DIFICIL") discoveryChance = 0.75f;

                    if (Random.value <= discoveryChance)
                    {
                        Debug.LogWarning("[InspectState] ¡El monstruo te descubrió bajo la cama!");
                        hideScript.ToggleHide(targetBed);
                        
                        enemy.ChangeState(new BookHeadAttackState(enemy, agent, anim, enemy.player));
                        return;
                    }
                    else
                    {
                        Debug.Log("[InspectState] El jugador se salvó de ser descubierto.");
                    }
                }

                phase = 3;
                timer = 0f;
            }
        }
        else if (phase == 3) // Fase 3: Enderezarse suavemente
        {
            float t = Mathf.Clamp01(timer / recoverTime);
            float tSmooth = Mathf.Sin(t * Mathf.PI * 0.5f);
            currentXIncline = Mathf.Lerp(32f, 0f, tSmooth);

            modelTransform.localRotation = originalModelLocalRotation * Quaternion.Euler(currentXIncline, 0f, 0f);

            if (t >= 1f)
            {
                enemy.ChangeState(new BookHeadPatrolState(enemy, agent, anim, enemy.patrolPoints));
            }
        }
    }

    public void ExitState()
    {
        if (modelTransform != null)
        {
            modelTransform.localRotation = originalModelLocalRotation;
        }
        agent.isStopped = false;
    }
}
