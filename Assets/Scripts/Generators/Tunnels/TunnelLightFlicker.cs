using UnityEngine;
using System.Collections;

public class TunnelLightFlicker : MonoBehaviour
{
    private Light targetLight;
    private Renderer bulbRenderer;
    private AudioSource audioSource;
    private AudioClip flickerSound;

    private float maxIntensity;
    private Color maxColor;
    private Color originalEmissionColor;
    private Material bulbMaterial;

    private float minFlickerInterval = 12f;
    private float maxFlickerInterval = 28f;
    private float minFlickerDuration = 3f;
    private float maxFlickerDuration = 7f;
    [HideInInspector] public bool isForcedOff = false;
    [HideInInspector] public bool isPanicFlickering = false;

    private bool lastForcedOff = false;
    private bool lastPanicFlicker = false;
    private Light emergencyRedLight;

    void Start()
    {
        // 1. Obtener la luz principal
        targetLight = GetComponentInChildren<Light>();
        if (targetLight == null) targetLight = GetComponent<Light>();

        if (targetLight != null)
        {
            maxIntensity = targetLight.intensity;
            maxColor = targetLight.color;
        }

        // Crear la luz roja de emergencia secundaria en la lámpara para el apagón global
        GameObject redObj = new GameObject("EmergencyRedBeacon");
        redObj.transform.SetParent(transform);
        redObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        emergencyRedLight = redObj.AddComponent<Light>();
        emergencyRedLight.type = LightType.Point;
        emergencyRedLight.color = new Color(1.0f, 0.08f, 0.04f);
        emergencyRedLight.range = 14f;
        emergencyRedLight.intensity = 1.2f;
        emergencyRedLight.shadows = LightShadows.Soft;
        emergencyRedLight.enabled = false;

        // 2. Obtener el Renderer de la bombilla para el brillo
        bulbRenderer = GetComponentInChildren<Renderer>();
        if (bulbRenderer != null)
        {
            bulbMaterial = bulbRenderer.material;
            if (bulbMaterial != null && bulbMaterial.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = bulbMaterial.GetColor("_EmissionColor");
            }
            else
            {
                originalEmissionColor = new Color(1f, 0.75f, 0.4f) * 2f;
            }
        }

        // 3. Crear AudioSource 3D para el sonido de fallo de luz
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // Sonido 3D
        audioSource.minDistance = 1.0f;
        audioSource.maxDistance = 6.0f;
        audioSource.volume = 0.25f;

        // Cargar el sonido desde Resources
        flickerSound = Resources.Load<AudioClip>("Audio/Hospital/ErrorLightSound");
        if (flickerSound == null) flickerSound = Resources.Load<AudioClip>("ErrorLightSound");
        audioSource.clip = flickerSound;

        // 4. Iniciar ciclo de parpadeo/apagón
        StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // Espera un intervalo aleatorio entre apagones (12 a 28 segundos)
            float waitTime = Random.Range(minFlickerInterval, maxFlickerInterval);
            yield return new WaitForSeconds(waitTime);

            // Iniciar sonido de corto circuito / fallo
            if (audioSource != null && flickerSound != null)
            {
                audioSource.Play();
            }

            // Duración del parpadeo caótico (3 a 7 segundos)
            float flickerDuration = Random.Range(minFlickerDuration, maxFlickerDuration);
            float elapsed = 0f;

            while (elapsed < flickerDuration)
            {
                // Parpadeo rápido aleatorio
                bool state = Random.value < 0.35f;
                SetLightState(state);

                float step = Random.Range(0.05f, 0.25f);
                yield return new WaitForSeconds(step);
                elapsed += step;
            }

            // Apagón total al final del parpadeo (durante 1 a 2.5 segundos extra)
            SetLightState(false);
            yield return new WaitForSeconds(Random.Range(1.0f, 2.5f));

            // Detener el sonido de fallo
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            SetLightState(true);
        }
    }

    private void SetLightState(bool on)
    {
        bool isOutage = TunnelsPowerOutageManager.isGlobalPowerOutage;
        if (isForcedOff || isOutage) on = false;

        if (targetLight != null)
        {
            targetLight.enabled = on;
            targetLight.intensity = on ? maxIntensity : 0f;
        }

        if (bulbMaterial != null)
        {
            if (isOutage)
            {
                // En apagón global, el foco físico brilla en ROJO DE EMERGENCIA
                bulbMaterial.EnableKeyword("_EMISSION");
                bulbMaterial.SetColor("_EmissionColor", new Color(1.0f, 0.06f, 0.03f) * 2.5f);
                bulbMaterial.color = new Color(0.9f, 0.1f, 0.1f);
            }
            else if (on)
            {
                bulbMaterial.EnableKeyword("_EMISSION");
                bulbMaterial.SetColor("_EmissionColor", originalEmissionColor);
                bulbMaterial.color = maxColor;
            }
            else
            {
                bulbMaterial.DisableKeyword("_EMISSION");
                bulbMaterial.SetColor("_EmissionColor", Color.black);
                bulbMaterial.color = Color.gray;
            }
        }
    }

    void Update()
    {
        bool forceOff = isForcedOff || TunnelsPowerOutageManager.isGlobalPowerOutage;
        
        // Control de la luz roja de emergencia durante el apagón global
        if (emergencyRedLight != null)
        {
            bool showEmergency = TunnelsPowerOutageManager.isGlobalPowerOutage;
            emergencyRedLight.enabled = showEmergency;
            if (showEmergency)
            {
                float pulse = 0.8f + Mathf.PingPong(Time.time * 2.2f, 0.6f);
                emergencyRedLight.intensity = pulse;
            }
        }

        if (forceOff)
        {
            if (!lastForcedOff)
            {
                lastForcedOff = true;
                lastPanicFlicker = false;
                SetLightState(false);
                
                // Solo reproducir el zumbido de fallo local si NO es un apagón global general
                if (!TunnelsPowerOutageManager.isGlobalPowerOutage && audioSource != null && flickerSound != null && !audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
        }
        else if (isPanicFlickering)
        {
            lastForcedOff = false;
            lastPanicFlicker = true;

            // Parpadeo caótico constante e intenso en cada cuadro
            bool lightOn = Random.value > 0.55f;
            SetLightState(lightOn);

            if (audioSource != null && flickerSound != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            // Restaurar estado si cambia de forzado/pánico a normal
            if (lastForcedOff || lastPanicFlicker)
            {
                lastForcedOff = false;
                lastPanicFlicker = false;
                SetLightState(true);
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (audioSource != null) audioSource.Stop();
    }
}
