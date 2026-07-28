using UnityEngine;
using System.Collections;

public class FlickeringLight : MonoBehaviour
{
    public Light lightSource; // La fuente de luz que queremos modificar
    public AudioSource flickerSound; // El AudioSource para el sonido de parpadeo
    public float minIntensity = 0.3f; // Intensidad mínima (luz apagada)
    public float maxIntensity = 20f; // Intensidad máxima (luz encendida)
    public float flickerSpeed = 0.1f; // Velocidad de parpadeo
    public float timeToTurnOff = 1f; // Tiempo máximo para que la luz se apague
    public float colorChangeChance = 0.2f; // Probabilidad de cambio de color a rojo

    public float maxSoundDistance = 10f; // Distancia máxima para escuchar el sonido (ajustable)
    private bool isFlickering = false; // Si la luz está parpadeando
    private float timeUntilNextFlicker; // El tiempo aleatorio hasta que inicie el parpadeo
    private RoomLightsManager roomManager; // Referencia al RoomLightsManager
    private PowerBox powerBox;             // Referencia al PowerBox

    void Start()
    {
        if (lightSource == null)
        {
            lightSource = GetComponent<Light>();
        }

        // Inicializa con la luz encendida y empieza el parpadeo
        if (lightSource != null)
        {
            lightSource.intensity = maxIntensity;
        }
        
        Debug.Log("Flickering light initialized.");

        // Obtener la referencia al RoomLightsManager y PowerBox
        roomManager = FindObjectOfType<RoomLightsManager>();
        powerBox = FindObjectOfType<PowerBox>();

        StartCoroutine(WaitForFlicker()); // Inicia el ciclo del parpadeo
        CacheMaterials(); // Pre-cachear materiales al inicio
    }

    void Update()
    {
        if (lightSource == null) return;

        // Auto-búsqueda auto-reparadora de PowerBox si por orden de ejecución inicial fue null
        if (powerBox == null)
        {
            powerBox = FindObjectOfType<PowerBox>();
        }

        // Determinar si hay un corte de energía (local por manager o global por PowerBox)
        bool hasPowerOutage = (roomManager != null && roomManager.powerOutage) || (powerBox != null && powerBox.isPowerOut);

        // 1. Verificar si hay un corte de energía y apagar la luz si es true
        if (hasPowerOutage)
        {
            lightSource.enabled = false; // Apagar la luz durante el corte de energía
            SetEmission(false);           // Apagar el brillo del material de la lámpara
            if (flickerSound != null && flickerSound.isPlaying) flickerSound.Stop(); // Detener el sonido
            isFlickering = false; // Detener el parpadeo
            return;
        }
        else
        {
            // Si el apagón ha terminado, activar la luz si estaba apagada
            if (!lightSource.enabled)
            {
                lightSource.enabled = true;
                SetEmission(true);        // Restaurar el brillo del material de la lámpara
                lightSource.intensity = maxIntensity; // Asegurarse de que la luz esté encendida con la intensidad máxima
                lightSource.color = Color.white; // Restaurar el color blanco
                Debug.Log("FlickeringLight: Luz activada.");
            }
        }

        // 2. Alerta Crítica (Sobrecarga de Red al 20% o menos)
        bool isCritical = powerBox != null && (powerBox.currentPowerCapacity / powerBox.maxPowerCapacity) <= 0.2f;

        if (isCritical)
        {
            // Forzar parpadeos rápidos e inestables debido a la sobrecarga inminente
            if (Random.value < 0.25f)
            {
                lightSource.intensity = (Random.value < 0.5f) ? minIntensity : maxIntensity * 0.3f;
                lightSource.color = Color.white; // Mantener blanca en este estado crítico
                
                // Sonido de chispas intermitentes
                if (flickerSound != null && !flickerSound.isPlaying && Vector3.Distance(transform.position, Camera.main.transform.position) <= maxSoundDistance)
                {
                    flickerSound.Play();
                }
            }
            return; // Saltarse el parpadeo aleatorio normal si estamos en estado crítico
        }

        // 3. Parpadeo aleatorio estándar (Mecánica de ambiente)
        if (isFlickering)
        {
            // Aleatorio cambio de intensidad de la luz
            if (Random.value < flickerSpeed * Time.deltaTime)
            {
                // Alterna entre la intensidad mínima y máxima
                lightSource.intensity = (lightSource.intensity == minIntensity) ? maxIntensity : minIntensity;
                Debug.Log("Light intensity changed: " + lightSource.intensity);

                // Cambiar color aleatorio
                if (Random.value < colorChangeChance)
                {
                    lightSource.color = Color.red;
                    Debug.Log("Color changed to red.");
                }
                else
                {
                    lightSource.color = Color.white; // Vuelve al color blanco
                    Debug.Log("Color reverted to white.");
                }

                // Reproducir sonido cuando la luz está parpadeando y el jugador está cerca
                if (flickerSound != null && !flickerSound.isPlaying && Vector3.Distance(transform.position, Camera.main.transform.position) <= maxSoundDistance)
                {
                    flickerSound.Play();
                    Debug.Log("Flicker sound started.");
                }
            }
        }
        else
        {
            // Detener el sonido y restaurar el estado original cuando la luz deje de parpadear
            if (lightSource.intensity != maxIntensity)
            {
                lightSource.intensity = maxIntensity;
                lightSource.color = Color.white;
            }

            if (flickerSound != null && flickerSound.isPlaying)
            {
                flickerSound.Stop();
                Debug.Log("Flicker sound stopped.");
            }
        }
    }

    // Este método maneja el ciclo de parpadeo con un intervalo aleatorio de ambiente
    IEnumerator WaitForFlicker()
    {
        while (true)
        {
            // Espera un tiempo aleatorio de entre 2 a 7 minutos
            timeUntilNextFlicker = Random.Range(2f * 60f, 7f * 60f);
            yield return new WaitForSeconds(timeUntilNextFlicker);

            // Comienza el parpadeo
            isFlickering = true;
            Debug.Log("Flickering started.");

            // Espera un tiempo aleatorio para la duración del parpadeo (5 a 20 segundos)
            float flickerDuration = Random.Range(5f, 20f);
            yield return new WaitForSeconds(flickerDuration);

            // Detiene el parpadeo y restaura el estado original de la luz
            isFlickering = false;
            if (lightSource != null)
            {
                lightSource.intensity = maxIntensity; // Restaurar intensidad máxima
                lightSource.color = Color.white; // Restaurar color blanco
            }
            Debug.Log("Flickering stopped, light restored.");
        }
    }

    private System.Collections.Generic.Dictionary<Material, Color> originalEmissionColors = new System.Collections.Generic.Dictionary<Material, Color>();
    private bool cachedMaterials = false;
    private bool lastGlowState = true;

    void CacheMaterials()
    {
        if (cachedMaterials) return;
        
        Transform current = transform;
        while (current != null && !current.name.StartsWith("Ceiling-light") && !current.name.StartsWith("Ceiling-Light"))
        {
            current = current.parent;
        }
        Transform lampRoot = (current != null) ? current : transform.parent;

        if (lampRoot != null)
        {
            UnityEngine.Debug.Log("FlickerDebug: Encontrado lampRoot=" + lampRoot.name);
            MeshRenderer[] renderers = lampRoot.GetComponentsInChildren<MeshRenderer>(true);
            UnityEngine.Debug.Log("FlickerDebug: Encontrados " + renderers.Length + " MeshRenderers en " + lampRoot.name);
            foreach (MeshRenderer r in renderers)
            {
                UnityEngine.Debug.Log("FlickerDebug: Analizando MeshRenderer=" + r.name + " con " + r.materials.Length + " materiales.");
                foreach (Material m in r.materials)
                {
                    if (m != null)
                    {
                        bool hasEmissionColor = m.HasProperty("_EmissionColor");
                        UnityEngine.Debug.Log("FlickerDebug: Material=" + m.name + " | Has_EmissionColor=" + hasEmissionColor);
                        if (hasEmissionColor)
                        {
                            if (!originalEmissionColors.ContainsKey(m))
                            {
                                originalEmissionColors[m] = m.GetColor("_EmissionColor");
                                UnityEngine.Debug.Log("FlickerDebug: Guardado color original de " + m.name + " = " + originalEmissionColors[m]);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("FlickerDebug: lampRoot es nulo!");
        }
        cachedMaterials = true;
    }

    void SetEmission(bool state)
    {
        CacheMaterials();
        if (state == lastGlowState) return;
        
        UnityEngine.Debug.Log("FlickerDebug: Cambiando estado de emision a: " + state + " en " + originalEmissionColors.Count + " materiales.");
        foreach (var kvp in originalEmissionColors)
        {
            if (kvp.Key != null)
            {
                if (state)
                {
                    kvp.Key.SetColor("_EmissionColor", kvp.Value);
                    kvp.Key.EnableKeyword("_EMISSION");
                    UnityEngine.Debug.Log("FlickerDebug: Restaurando emision color en " + kvp.Key.name + " a " + kvp.Value);
                }
                else
                {
                    kvp.Key.SetColor("_EmissionColor", Color.black);
                    kvp.Key.DisableKeyword("_EMISSION");
                    UnityEngine.Debug.Log("FlickerDebug: Apagando emision (Color.black) en " + kvp.Key.name);
                }
            }
        }
        lastGlowState = state;
    }

}
