using UnityEngine;

public class OpenDoor : MonoBehaviour
{
    public Animator doorAnimator;  // Referencia al Animator de la puerta
    public AudioSource audioSource; // Referencia al AudioSource
    public AudioClip doorOpenSound; // Sonido de apertura
    public AudioClip doorCloseSound; // Sonido de cierre

    public bool isLocked = false; // Si está bloqueada, no responde a la interacción del jugador
    private bool isOpen = false;
    private bool playerNearby = false; // Solo la puerta cercana reacciona

    private float lastInteractTime = 0f;

    void Update()
    {
        // Verifica si el jugador está cerca y presiona 'E' para abrir o cerrar la puerta
        if (playerNearby && MobileInput.GetKeyDown(KeyCode.E))
        {
            if (Time.unscaledTime < lastInteractTime + 0.35f) return;
            lastInteractTime = Time.unscaledTime;
            MobileInput.ePressedDown = false;

            isOpen = !isOpen;
            Debug.Log("isOpen: " + isOpen);

            // Usa el parmetro 'isOpen' para controlar la animacin de la puerta
            doorAnimator.SetBool("isOpen", isOpen);

            // Reproducir sonido correspondiente
            if (audioSource)
            {
                audioSource.PlayOneShot(isOpen ? doorOpenSound : doorCloseSound);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifica si el objeto que entra al trigger tiene la etiqueta "Player"
        if (other.CompareTag("Player"))
        {
            Debug.Log("Jugador detectado en el trigger");
            playerNearby = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Verifica si el objeto que sale del trigger tiene la etiqueta "Player"
        if (other.CompareTag("Player"))
        {
            Debug.Log("Jugador sali del trigger");
            playerNearby = false;
        }
    }
}
