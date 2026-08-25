using UnityEngine;

public class BookHeadAnimation : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            // Evita que el monstruo flote/se deslice sin animacin cuando est lejos del jugador
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    // Apaga el resto de booleanos de animacin para evitar conflictos en las transiciones
    private void ClearAllStatesExcept(string activeParam)
    {
        if (animator == null) return;
        string[] parameters = { "Walking", "Running", "Still", "Eating", "Attacking" };
        foreach (string param in parameters)
        {
            if (param != activeParam)
            {
                animator.SetBool(param, false);
            }
        }
    }

    public void SetWalking(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool("Walking", isWalking);
            if (isWalking) ClearAllStatesExcept("Walking");
        }
    }

    public void SetAttacking(bool isAttacking)
    {
        if (animator != null)
        {
            animator.SetBool("Attacking", isAttacking);
            if (isAttacking) ClearAllStatesExcept("Attacking");
        }
    }

    public void SetRunning(bool isRunning)
    {
        if (animator != null)
        {
            animator.SetBool("Running", isRunning);
            if (isRunning) ClearAllStatesExcept("Running");
        }
    }

    public void SetIdle(bool isIdle)
    {
        if (animator != null)
        {
            animator.SetBool("Still", isIdle); // Cambiado de "Idle" a "Still" para coincidir con el Animator Controller
            if (isIdle) ClearAllStatesExcept("Still");
        }
    }

    public void SetEating(bool eating)
    {
        if (animator != null)
        {
            animator.SetBool("Eating", eating); // Cambiado de "IsEating" a "Eating" para coincidir con el Animator Controller
            if (eating) ClearAllStatesExcept("Eating");
        }
    }
}
