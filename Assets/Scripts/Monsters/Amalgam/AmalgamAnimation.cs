using UnityEngine;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Controlador de animaciones ultra-flexible para The Amalgam.
    /// Resetea booleans y reproduce la animación una sola vez al cambiar de estado
    /// evitando que se congele en el primer frame.
    /// </summary>
    public class AmalgamAnimation : MonoBehaviour
    {
        private Animator anim;
        private string currentPlayingState = "";

        [Header("Nombres de Parámetros del Animator Controller")]
        public string cryingBoolName = "IsCrying";
        public string warningBoolName = "IsWarning";
        public string runningBoolName = "IsRunning";
        public string speedFloatName = "Speed";
        public string threatTriggerName = "ThreatPosture";
        public string attackTriggerName = "Attack";

        [Header("Nombres de Estados Directos (Animation Clips)")]
        public string idleStateName = "The_Amalgam_Indie";
        public string runStateName = "The_Amalgam_Run";
        public string threatStateName = "The_Amalgam_IndieHorror";
        public string attackStateName = "Attack";

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
        }

        public void ResetAllStateBools()
        {
            if (anim == null) return;
            SetBoolIfExists(cryingBoolName, false);
            SetBoolIfExists(warningBoolName, false);
            SetBoolIfExists(runningBoolName, false);
            SetBoolIfExists("IsChasing", false);
            SetBoolIfExists("IsRun", false);
            SetBoolIfExists("IsAttacking", false);
        }

        public void SetCrying(bool isCrying)
        {
            if (anim == null) return;
            if (isCrying)
            {
                if (currentPlayingState != idleStateName)
                {
                    currentPlayingState = idleStateName;
                    ResetAllStateBools();
                    SetBoolIfExists(cryingBoolName, true);
                    SetFloatIfExists(speedFloatName, 0f);
                    PlayStateIfExists(idleStateName);
                }
            }
            else
            {
                if (currentPlayingState == idleStateName) currentPlayingState = "";
                SetBoolIfExists(cryingBoolName, false);
            }
        }

        public void SetWarning(bool isWarning)
        {
            if (anim == null) return;
            if (isWarning)
            {
                if (currentPlayingState != threatStateName)
                {
                    currentPlayingState = threatStateName;
                    ResetAllStateBools();
                    SetBoolIfExists(warningBoolName, true);
                    SetFloatIfExists(speedFloatName, 1.5f);
                    PlayStateIfExists(threatStateName);
                }
            }
            else
            {
                if (currentPlayingState == threatStateName) currentPlayingState = "";
                SetBoolIfExists(warningBoolName, false);
            }
        }

        public void SetRunning(bool isRunning)
        {
            if (anim == null) return;
            if (isRunning)
            {
                SetBoolIfExists(runningBoolName, true);
                SetBoolIfExists("IsChasing", true);
                SetBoolIfExists("IsRun", true);
                SetFloatIfExists(speedFloatName, 6.2f);

                if (currentPlayingState != runStateName)
                {
                    currentPlayingState = runStateName;
                    ResetAllStateBools();
                    SetBoolIfExists(runningBoolName, true);
                    SetBoolIfExists("IsChasing", true);
                    SetBoolIfExists("IsRun", true);
                    SetFloatIfExists(speedFloatName, 6.2f);
                    PlayStateIfExists(runStateName, 0.15f);
                    Debug.Log($"[AmalgamAnimation] 🏃 Animación de carrera '{runStateName}' iniciada de forma fluida.");
                }
            }
            else
            {
                if (currentPlayingState == runStateName)
                {
                    currentPlayingState = "";
                }
                SetBoolIfExists(runningBoolName, false);
                SetBoolIfExists("IsChasing", false);
                SetBoolIfExists("IsRun", false);
                SetFloatIfExists(speedFloatName, 0f);
            }
        }

        public void TriggerThreatPosture()
        {
            if (anim == null) return;
            SetTriggerIfExists(threatTriggerName);
            SetTriggerIfExists("Threat");
            PlayStateIfExists(threatStateName);
        }

        public void TriggerAttack()
        {
            if (anim == null) return;
            currentPlayingState = attackStateName;
            ResetAllStateBools();
            SetBoolIfExists("IsAttacking", true);
            SetTriggerIfExists(attackTriggerName);
            SetTriggerIfExists("Attack1");
            PlayStateIfExists(attackStateName, 0.05f);
            Debug.Log("[AmalgamAnimation] ⚔️ Animación de ataque activada.");
        }

        #region Helper Methods de Animator Seguros y Forzado Directo

        private void PlayStateIfExists(string stateName, float transitionDuration = 0.15f)
        {
            if (string.IsNullOrEmpty(stateName) || anim == null || anim.runtimeAnimatorController == null) return;

            int stateHash = Animator.StringToHash(stateName);
            if (anim.HasState(0, stateHash))
            {
                anim.CrossFadeInFixedTime(stateHash, transitionDuration);
            }
            else
            {
                try
                {
                    anim.CrossFadeInFixedTime(stateName, transitionDuration);
                }
                catch { }
            }
        }

        private void SetBoolIfExists(string paramName, bool value)
        {
            if (string.IsNullOrEmpty(paramName) || anim == null) return;
            if (HasParameter(paramName, AnimatorControllerParameterType.Bool))
            {
                anim.SetBool(paramName, value);
            }
        }

        private void SetFloatIfExists(string paramName, float value)
        {
            if (string.IsNullOrEmpty(paramName) || anim == null) return;
            if (HasParameter(paramName, AnimatorControllerParameterType.Float))
            {
                anim.SetFloat(paramName, value);
            }
        }

        private void SetTriggerIfExists(string paramName)
        {
            if (string.IsNullOrEmpty(paramName) || anim == null) return;
            if (HasParameter(paramName, AnimatorControllerParameterType.Trigger))
            {
                anim.SetTrigger(paramName);
            }
        }

        private bool HasParameter(string paramName, AnimatorControllerParameterType type)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
            {
                if (p.name == paramName && p.type == type) return true;
            }
            return false;
        }

        #endregion
    }
}
