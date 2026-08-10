using UnityEngine;

public class WaterPuddle : MonoBehaviour
{
    private AudioClip dripSound;
    private AudioClip wetStepSound;
    private float nextStepTime = 0f;
    private Texture2D rippleTexture;

    void Start()
    {
        // Cargar los clips de sonido desde Resources
        dripSound = Resources.Load<AudioClip>("Audio/Compartido/Gotera");
        wetStepSound = Resources.Load<AudioClip>("Audio/Compartido/PisarAgua");

        // Fallbacks de seguridad en caso de usar nombres en inglés
        if (dripSound == null) dripSound = Resources.Load<AudioClip>("Drip");
        if (wetStepSound == null) wetStepSound = Resources.Load<AudioClip>("WetStep");

        // Guardar referencia de la textura del charco para usarla en los ripples
        Renderer r = GetComponent<Renderer>();
        if (r != null && r.material != null)
        {
            rippleTexture = r.material.mainTexture as Texture2D;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Si el jugador aún está dentro del ascensor de llegada, no reproducir pasos de agua
        if (ArrivalElevatorController.IsPlayerInElevator) return;

        // 1. Si entra una gota de agua procedimental
        if (other.name.Contains("WaterDroplet"))
        {
            // Generar efecto visual de onda/ripple en la superficie del charco
            SpawnRippleEffect(other.transform.position);

            Destroy(other.gameObject); // Destruir la gota de forma silenciosa (el audio ya suena en bucle en el tubo emisor)
        }

        // 2. Si entra el jugador (solo si el mapa ya inició y el jugador se está moviendo)
        if (Time.time > 0.8f && (other.CompareTag("Player") || other.name.Contains("Player") || other.GetComponent<CharacterController>() != null))
        {
            CharacterController cc = other.GetComponent<CharacterController>();
            Rigidbody rb = other.GetComponent<Rigidbody>();
            float speed = 0f;
            if (cc != null) speed = cc.velocity.magnitude;
            else if (rb != null) speed = rb.linearVelocity.magnitude;

            if (speed > 0.5f)
            {
                PlayWetStepSound(other.transform.position);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        // Si el jugador aún está dentro del ascensor de llegada, no reproducir pasos de agua
        if (ArrivalElevatorController.IsPlayerInElevator) return;

        // Si el jugador camina dentro del charco, reproducir el sonido a intervalos según su velocidad
        if (other.CompareTag("Player") || other.name.Contains("Player") || other.GetComponent<CharacterController>() != null)
        {
            CharacterController cc = other.GetComponent<CharacterController>();
            Rigidbody rb = other.GetComponent<Rigidbody>();
            float speed = 0f;

            if (cc != null) speed = cc.velocity.magnitude;
            else if (rb != null) speed = rb.linearVelocity.magnitude;

            if (speed > 0.5f && Time.time >= nextStepTime)
            {
                PlayWetStepSound(other.transform.position);
                // Intervalo de pasos dinámico según la velocidad del jugador
                float interval = speed > 4f ? 0.35f : 0.6f;
                nextStepTime = Time.time + interval;
            }
        }
    }

    private void PlayWetStepSound(Vector3 pos)
    {
        if (wetStepSound != null)
        {
            float pitch = Random.Range(0.85f, 1.15f);
            float volume = Random.Range(0.55f, 0.75f);

            GameObject audioObj = new GameObject("WetStepAudioTemp");
            audioObj.transform.position = pos;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = wetStepSound;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1.0f; // Sonido 3D
            source.Play();
            Destroy(audioObj, wetStepSound.length + 0.1f);
        }
    }

    private void SpawnRippleEffect(Vector3 hitPos)
    {
        // Crear un plano pequeño para la onda de agua
        GameObject ripple = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ripple.name = "WaterRippleProcedural";
        
        // Colocar plano horizontal ligeramente arriba del charco actual (evitar Z-fighting)
        ripple.transform.position = new Vector3(hitPos.x, transform.position.y + 0.002f, hitPos.z);
        ripple.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Acostado
        ripple.transform.localScale = new Vector3(0.08f, 0.08f, 1f); // Pequeño al inicio

        // Eliminar colisionador
        Collider col = ripple.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Crear material simple compatible con transparencia
        Renderer r = ripple.GetComponent<Renderer>();
        if (r != null)
        {
            Material rippleMat = new Material(Shader.Find("Sprites/Default"));
            rippleMat.color = new Color(0.5f, 0.55f, 0.6f, 0.6f);
            if (rippleTexture != null)
            {
                rippleMat.mainTexture = rippleTexture;
            }
            r.material = rippleMat;
        }

        // Agregar script de animación
        ripple.AddComponent<WaterRipple>();
    }
}

// --- CLASE AUXILIAR PARA ANIMAR EL RIPPLE (Onda de Agua) ---
public class WaterRipple : MonoBehaviour
{
    public float duration = 0.5f;
    private Material materialInstance;
    private float elapsed = 0f;
    private Vector3 startScale;
    private Vector3 targetScale;

    void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null) materialInstance = r.material;
        
        startScale = transform.localScale;
        // Crece unas 6 veces su tamaño inicial para simular la expansión de la onda
        targetScale = startScale * 6f; 
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        
        if (t >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        // Expandir tamaño
        transform.localScale = Vector3.Lerp(startScale, targetScale, t);

        // Desvanecer alpha progresivamente
        if (materialInstance != null)
        {
            Color c = materialInstance.color;
            c.a = Mathf.Lerp(0.6f, 0f, t);
            materialInstance.color = c;
        }
    }
}
