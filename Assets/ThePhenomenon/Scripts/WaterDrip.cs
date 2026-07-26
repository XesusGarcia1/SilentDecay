using UnityEngine;

public class WaterDrip : MonoBehaviour
{
    public float dripInterval = 0.4f;
    public Material waterMaterial;
    
    private float timer;

    void Start()
    {
        // Cargar el clip de gotera
        AudioClip clip = Resources.Load<AudioClip>("Gotera");
        if (clip == null) clip = Resources.Load<AudioClip>("Drip");

        if (clip != null)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 1.0f; // Sonido 3D
            source.minDistance = 1.5f;
            source.maxDistance = 10f;
            source.playOnAwake = true;

            // Determinar un pitch aleatorio consistente basado en la posición en el mundo
            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            int seed = (int)(transform.position.x * 7919f + transform.position.z * 104729f);
            UnityEngine.Random.InitState(seed);
            float randPitch = UnityEngine.Random.Range(0.6f, 1.3f);
            UnityEngine.Random.state = oldState;

            source.pitch = randPitch;
            source.volume = 0.5f; // Volumen equilibrado
            source.Play();

            // Sincronizar el goteo visual del cilindro al tempo del audio
            dripInterval = 0.45f / randPitch;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= dripInterval)
        {
            timer = 0f;
            SpawnDroplet();
        }
    }

    private void SpawnDroplet()
    {
        // Crear pequeña gota de agua procedimental
        GameObject droplet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        droplet.name = "WaterDroplet";
        
        // Posicionar justo debajo del emisor
        droplet.transform.position = transform.position + Vector3.down * 0.1f;
        droplet.transform.localScale = new Vector3(0.025f, 0.035f, 0.025f); // Forma ligeramente estirada

        // Aplicar material de agua
        if (waterMaterial != null)
        {
            droplet.GetComponent<Renderer>().material = waterMaterial;
        }

        // Agregar gravedad para que caiga de forma realista
        Rigidbody rb = droplet.AddComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = 0.01f;
            rb.linearDamping = 0.1f;
            rb.useGravity = true;
        }

        // Activar colisionador como Trigger para que active la detección del charco sin empujar objetos
        Collider col = droplet.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = true;
        }

        // Destruir automáticamente después de que caiga al suelo (1.2 segundos es suficiente)
        Destroy(droplet, 1.2f);
    }
}
