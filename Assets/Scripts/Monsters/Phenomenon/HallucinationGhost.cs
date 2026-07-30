using UnityEngine;

public class HallucinationGhost : MonoBehaviour
{
    private Transform player;
    private FlashlightController fl;
    private AudioSource audioSource;
    private AudioClip whispersSound;
    private Animator animator;

    private bool isDisappearing = false;
    private float fadeTimer = 0f;
    private Renderer[] renderers;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        fl = FindObjectOfType<FlashlightController>();
        
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Ajustar altura para hundirlo un poco en el suelo (evitar flotación)
            animator.transform.localPosition = new Vector3(0f, -0.38f, 0f);

            // Variar la velocidad de respiración/animación ligeramente para que se vea más orgánico (0.8x a 1.2x)
            animator.speed = Random.Range(0.8f, 1.2f);

            // Elegir aleatoriamente una de las poses de descanso reales en el Animator
            string[] idleStates = { "Idle", "Idle2", "Idle3" };
            string chosenIdle = idleStates[Random.Range(0, idleStates.Length)];

            // Reproducir mediante CrossFade
            animator.CrossFade(chosenIdle, 0.1f);
        }

        // Crear AudioSource 3D
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D
        audioSource.minDistance = 3.0f;
        audioSource.maxDistance = 15.0f;
        audioSource.volume = 0.8f;

        // Auto-cargar audio si no está asignado
        if (whispersSound == null) whispersSound = Resources.Load<AudioClip>("Audio/Compartido/Susurros");
        audioSource.clip = whispersSound;

        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = true; // Asegurar visibilidad inicial
        }

        // Destrucción automática de seguridad tras 25 segundos
        Destroy(gameObject, 25f);
    }

    void Update()
    {
        if (isDisappearing)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (fadeTimer / 0.4f));
            
            if (alpha <= 0.05f)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        // 1. Desaparece si el jugador se acerca demasiado (menos de 6.5 metros)
        // 2. Desaparece si es apuntado directamente por la linterna
        if (dist <= 6.5f || IsShinedByPlayer())
        {
            Disappear();
        }
    }

    private bool IsShinedByPlayer()
    {
        if (fl == null || fl.flashlightLight == null || !fl.flashlightLight.enabled) return false;

        Camera mainCam = Camera.main;
        if (mainCam == null) return false;

        float dist = Vector3.Distance(mainCam.transform.position, transform.position);
        if (dist > fl.flashlightLight.range) return false;

        // Foco de linterna (spotAngle = 70, semi-ángulo = 35)
        Vector3 dirToGhost = (transform.position + Vector3.up * 1f - mainCam.transform.position).normalized;
        float angle = Vector3.Angle(mainCam.transform.forward, dirToGhost);
        if (angle > (fl.flashlightLight.spotAngle / 2f)) return false;

        // Línea de visión directa sin paredes
        RaycastHit hit;
        Vector3 start = mainCam.transform.position;
        Vector3 end = transform.position + Vector3.up * 1.2f;
        if (Physics.Linecast(start, end, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != transform.root)
            {
                return false;
            }
        }

        return true;
    }

    private void Disappear()
    {
        isDisappearing = true;
        
        if (audioSource != null && whispersSound != null)
        {
            audioSource.Play();
        }

        // Apagar todos los renderes de golpe
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }
    }
}
