using UnityEngine;

public class PhenomenonAnimation : MonoBehaviour
{
    private Animator animator;
    private string currentState = "";
    private string currentIdleName = "Idle";

    private string[] idleStates = { "Idle", "Idle2", "Idle3" };

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Elegir una pose idle aleatoria de entrada
            currentIdleName = idleStates[Random.Range(0, idleStates.Length)];
            PlayState(currentIdleName, 0.1f);
        }
        else
        {
            Debug.LogError("PhenomenonAnimation: ¡No se encontró un componente Animator!");
        }
    }

    private void PlayState(string stateName, float transition = 0.25f)
    {
        if (animator != null && currentState != stateName)
        {
            // Como el Animator no tiene parámetros, reproducimos el estado directamente por nombre con fundido
            animator.CrossFade(stateName, transition);
            currentState = stateName;
        }
    }

    public void SetWalking(bool isWalking)
    {
        if (animator == null) return;

        if (isWalking)
        {
            PlayState("WalkPrimary");
        }
        else
        {
            // Al detenerse, elegir una pose de descanso diferente para variar
            if (currentState == "WalkPrimary" || currentState == "")
            {
                currentIdleName = idleStates[Random.Range(0, idleStates.Length)];
            }
            PlayState(currentIdleName);
        }
    }

    public void SetAlert(bool isAlert)
    {
        // El comportamiento se maneja a través de los estados directos del Animator
    }

    public void SetWalkSpeed(float speedMultiplier)
    {
        if (animator != null)
        {
            // Adaptar la velocidad de la animación al movimiento del agente
            animator.speed = Mathf.Clamp(speedMultiplier, 0.6f, 2.8f);
        }
    }

    public void SetAttacking(bool isAttacking)
    {
        // Opcional: Se puede reproducir una pose estática al atrapar al jugador
        if (animator == null) return;
        if (isAttacking)
        {
            PlayState("Idle2", 0.15f);
        }
    }
}
