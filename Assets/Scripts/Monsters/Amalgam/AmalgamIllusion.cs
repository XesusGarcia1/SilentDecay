using UnityEngine;
using System.Collections;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Script para las Ilusiones / Espejismos Psicológicos de The Amalgam.
    /// Crea una silueta o copia del monstruo en pasillos alternativos.
    /// Cuando el jugador se acerca a la ilusión, el monstruo REAL se materializa en su lugar
    /// y comienza a perseguir al jugador de inmediato.
    /// </summary>
    public class AmalgamIllusion : MonoBehaviour
    {
        private AmalgamAIController realController;
        private Transform playerTransform;
        private Animator anim;
        private bool isActivated = false;

        public void Initialize(AmalgamAIController controller, Transform player)
        {
            this.realController = controller;
            this.playerTransform = player;

            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();

            if (anim != null)
            {
                anim.applyRootMotion = false;
                // Reproducir postura de amenaza o llanto estático
                anim.Play("The_Amalgam_IndieHorror");
            }

            // Aplicar tinte/sombra oscura a los materiales para simular un espejismo sombrío
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
                }
            }

            // GARANTIZAR SILENCIO 100% ABSOLUTO EN LAS ILUSIONES
            AudioSource[] sources = GetComponentsInChildren<AudioSource>();
            foreach (var s in sources)
            {
                if (s != null) DestroyImmediate(s);
            }

            Light[] lights = GetComponentsInChildren<Light>();
            foreach (var l in lights)
            {
                if (l != null) DestroyImmediate(l);
            }

            StartCoroutine(DissolveTimerRoutine());
        }

        private void Update()
        {
            if (isActivated || playerTransform == null || realController == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Hacer que la ilusión siempre mire inquietantemente hacia el jugador
            Vector3 lookDir = (playerTransform.position - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Si el jugador se acerca a la ilusión (<= 7m), ¡el monstruo REAL se materializa en esta posición!
            if (dist <= 7.0f)
            {
                isActivated = true;
                realController.OnPlayerApproachedIllusion(transform.position);
                DestroyIllusion();
            }
        }

        private IEnumerator DissolveTimerRoutine()
        {
            // La ilusión se disuelve sola tras 18 segundos si el jugador no se acerca
            yield return new WaitForSeconds(18.0f);
            DestroyIllusion();
        }

        public void DestroyIllusion()
        {
            // Efecto de desvanecimiento
            Destroy(gameObject);
        }
    }
}
