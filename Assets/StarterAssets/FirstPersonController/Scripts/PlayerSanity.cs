using UnityEngine;

public class PlayerSanity : MonoBehaviour
{
    [Header("Ajustes de Cordura")]
    public float sanity = 100f;
    public float maxSanity = 100f;
    public float darkDrainRate = 0.8f;       // Consumo en la oscuridad por segundo (bajado de 1.5 a 0.8)
    public float monsterDrainRate = 8f;      // Consumo al mirar al monstruo por segundo
    public float lightRestoreRate = 2.5f;    // Recuperación bajo la luz por segundo

    [Header("Sonidos de Locura")]
    public AudioClip whispersSound;
    private AudioSource whispersAudioSource;
    public float maxWhispersVolume = 0.7f;

    private RoomLightsManager roomLightsManager;
    private Transform monsterTransform;
    private Texture2D darkVignetteTex;

    void Start()
    {
        roomLightsManager = FindObjectOfType<RoomLightsManager>();

        // Intentar encontrar al enemigo en la escena
        EnemyAIController enemy = FindObjectOfType<EnemyAIController>();
        if (enemy != null)
        {
            monsterTransform = enemy.transform;
        }

        // Generar la textura del vignette de cordura oscura
        CreateProceduralDarkVignette();

        // Cargar el sonido de susurros copiado en Resources
        if (whispersSound == null)
        {
            whispersSound = Resources.Load<AudioClip>("Susurros");
        }

        // Inicializar AudioSource para los susurros (sonido 2D en la cabeza del jugador)
        whispersAudioSource = gameObject.AddComponent<AudioSource>();
        whispersAudioSource.clip = whispersSound;
        whispersAudioSource.loop = true;
        whispersAudioSource.volume = 0f;
        whispersAudioSource.spatialBlend = 0f; // Sonido 2D (Estéreo)
        whispersAudioSource.Play();
    }

    void Update()
    {
        // Encontrar al monstruo si no se asignó en Start (por estar desactivado)
        if (monsterTransform == null)
        {
            EnemyAIController enemy = FindObjectOfType<EnemyAIController>(true); // Buscar incluso desactivados
            if (enemy != null) monsterTransform = enemy.transform;
        }

        // 1. Calcular el estado de la cordura
        float distToMonster = 25f;
        if (monsterTransform != null)
        {
            distToMonster = Vector3.Distance(transform.position, monsterTransform.position);
        }

        if (IsLookingAtMonster())
        {
            // Drenado dinamico basado en la distancia (de lejos drena poco, de cerca muchisimo)
            // A 25m o mas: 2.0 de drenado por segundo
            // A 3m o menos: monsterDrainRate * 2.5 (ej: 20 de drenado por segundo)
            float distanceFactor = Mathf.Clamp01((25f - distToMonster) / 22f); // 0 (lejos) a 1 (cerca)
            float dynamicDrain = Mathf.Lerp(2.0f, monsterDrainRate * 2.5f, distanceFactor);
            sanity -= dynamicDrain * Time.deltaTime;
        }
        else if (IsInLight())
        {
            // Recupera cordura bajo luces encendidas
            sanity += lightRestoreRate * Time.deltaTime;
        }
        else
        {
            // Drenado lento en la oscuridad total
            sanity -= darkDrainRate * Time.deltaTime;
        }

        sanity = Mathf.Clamp(sanity, 0f, maxSanity);

        // 2. Control del audio de los susurros
        if (sanity <= 40f && whispersAudioSource != null && whispersSound != null)
        {
            // El volumen sube linealmente conforme la cordura baja de 40% a 0%
            float targetVolume = Mathf.InverseLerp(40f, 0f, sanity) * maxWhispersVolume;
            whispersAudioSource.volume = targetVolume;
        }
        else if (whispersAudioSource != null)
        {
            // Desvanecer volumen de susurros si la cordura está por encima del 40%
            whispersAudioSource.volume = Mathf.MoveTowards(whispersAudioSource.volume, 0f, Time.deltaTime * 0.5f);
        }

        // 3. Mecanica: Perder salud por locura extrema (daño por infarto/panico cuando sanity es 0)
        if (sanity <= 0f)
        {
            PlayerHealth healthComp = GetComponent<PlayerHealth>();
            if (healthComp == null) healthComp = GetComponentInParent<PlayerHealth>();
            if (healthComp != null)
            {
                // Drenar 3 de vida por segundo cuando la cordura esta en 0
                healthComp.TakeDamage(1.2f * Time.deltaTime); // Bajado de 3f a 1.2f para dar mas tiempo
            }
        }
    }

    public void TakeSanityDamage(float amount)
    {
        sanity = Mathf.Clamp(sanity - amount, 0f, maxSanity);
    }

    // Camara inestable / Mareo por locura en LateUpdate (para ejecutarse despues del movimiento de camara principal)
    void LateUpdate()
    {
        if (sanity <= 35f)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Intensidad del mareo escala segun el nivel de miedo/locura (0 a 1)
                float severity = Mathf.InverseLerp(35f, 0f, sanity);

                // Movimiento oscilatorio lento en Roll (inclinacion lateral Z) y Pitch (cabeceo X)
                float roll = Mathf.Sin(Time.time * 2.2f) * 4.5f * severity; // Giro de cabeza
                float pitch = Mathf.Cos(Time.time * 2.6f) * 2.0f * severity; // Cabeceo

                // Inyectar el desvio a la rotacion actual de la camara
                mainCam.transform.localRotation *= Quaternion.Euler(pitch, 0f, roll);
            }
        }
    }

    // Comprobar si el jugador está bajo alguna luz encendida (del techo, excluyendo la linterna)
    public bool IsInLight()
    {
        if (roomLightsManager != null && roomLightsManager.powerOutage) return false;

        // Buscar todas las luces de la escena
        Light[] lights = FindObjectsOfType<Light>();
        foreach (Light l in lights)
        {
            if (l != null && l.enabled && l.type != LightType.Directional && l.gameObject.name != "Player_Flashlight")
            {
                float dist = Vector3.Distance(transform.position, l.transform.position);
                // Si está en el rango de iluminación física de la bombilla (ej. 8 metros)
                if (dist <= l.range)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Comprobar si la cámara del jugador está enfocando directamente al monstruo
        private void CreateProceduralDarkVignette()
    {
        darkVignetteTex = new Texture2D(32, 32);
        darkVignetteTex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // El color negro se hace mas denso en los bordes para tunelizar la vision
                float alpha = Mathf.Clamp01((dist - 0.3f) / 0.7f);
                darkVignetteTex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        darkVignetteTex.Apply();
    }

    void OnGUI()
    {
        if (sanity >= 100f || darkVignetteTex == null) return;

        // Calcular opacidad en base a la cordura perdida (maxima opacidad de 0.65f para no cegar del todo)
        float sanityPercent = sanity / maxSanity;
        float baseAlpha = Mathf.Lerp(0.65f, 0f, sanityPercent);

        // Anadir una leve pulsacion dinamica para simular panico
        float pulse = 1f;
        if (sanity < 30f)
        {
            pulse = 1.0f + Mathf.PingPong(Time.time * 1.5f, 0.1f);
        }

        float finalAlpha = Mathf.Clamp01(baseAlpha * pulse);

        if (finalAlpha > 0.01f)
        {
            GUI.color = new Color(1f, 1f, 1f, finalAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), darkVignetteTex);
            GUI.color = Color.white;
        }
    }

    private bool IsLookingAtMonster()
    {
        if (monsterTransform == null || !monsterTransform.gameObject.activeInHierarchy) return false;

        Camera mainCam = Camera.main;
        if (mainCam == null) return false;

        // Convertir posición del monstruo al plano visual de la pantalla
        Vector3 screenPoint = mainCam.WorldToViewportPoint(monsterTransform.position + Vector3.up * 1f);
        bool onScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;

        if (onScreen)
        {
            // Comprobar si hay una pared o cobertura tapando la vista
            RaycastHit hit;
            Vector3 start = mainCam.transform.position;
            Vector3 end = monsterTransform.position + Vector3.up * 1f;

            if (Physics.Linecast(start, end, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.root == monsterTransform.root)
                {
                    return true; // No hay obstáculos y vemos al monstruo
                }
            }
        }
        return false;
    }
}
