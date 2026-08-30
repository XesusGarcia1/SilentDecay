using UnityEngine;
using System.Collections;

public class TunnelsPowerOutageManager : MonoBehaviour
{
    public static bool isGlobalPowerOutage = false;

    [Header("Intervalos de Apagón")]
    public float minTimeBetweenOutages = 40f;
    public float maxTimeBetweenOutages = 75f;
    public float minOutageDuration = 12f;
    public float maxOutageDuration = 22f;

    private AudioSource globalAudioSource;
    private AudioClip outageStartClip;
    private AudioClip outageEndClip;

    private void Start()
    {
        // Forzar la activación de niebla volumétrica y luz ambiental de Unity en el mapa de túneles
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        // Escalar los tiempos de apagón según la dificultad elegida
        string savedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
        if (savedDifficulty == "FACIL")
        {
            minTimeBetweenOutages = 50f;
            maxTimeBetweenOutages = 90f;
            minOutageDuration = 10f;
            maxOutageDuration = 18f;
        }
        else if (savedDifficulty == "DIFICIL")
        {
            minTimeBetweenOutages = 20f;
            maxTimeBetweenOutages = 40f;
            minOutageDuration = 20f;
            maxOutageDuration = 35f;
        }
        else // NORMAL
        {
            minTimeBetweenOutages = 30f;
            maxTimeBetweenOutages = 60f;
            minOutageDuration = 14f;
            maxOutageDuration = 25f;
        }

        // Crear un AudioSource 2D para reproducir los sonidos en la cabeza del jugador
        globalAudioSource = gameObject.AddComponent<AudioSource>();
        globalAudioSource.spatialBlend = 0.0f; // 2D (Estéreo directo)
        globalAudioSource.volume = 0.85f;
        globalAudioSource.playOnAwake = false;

        // Cargar sonidos
        outageStartClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
        if (outageStartClip == null) outageStartClip = Resources.Load<AudioClip>("Apagon_Sonido");
        if (outageStartClip == null) Debug.LogError("[TunnelsPowerOutageManager] ¡No se pudo encontrar Apagon_Sonido en Resources!");
        
        outageEndClip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (outageEndClip == null) outageEndClip = Resources.Load<AudioClip>("Interruptor");
        if (outageEndClip == null) Debug.LogError("[TunnelsPowerOutageManager] ¡No se pudo encontrar Interruptor en Resources!");

        // Iniciar la corrutina del ciclo de energía
        StartCoroutine(PowerCycleRoutine());
    }

    private IEnumerator PowerCycleRoutine()
    {
        // Período de gracia inicial: Esperar 2 minutos (120s) de tensión inicial antes del primer apagón
        Debug.Log("[TunnelsPowerOutageManager] Período de gracia inicial iniciado (120s de energía garantizada).");
        yield return new WaitForSeconds(120f);

        while (true)
        {
            // --- INICIAR APAGÓN ---
            isGlobalPowerOutage = true;
            Debug.Log("[TunnelsPowerOutageManager] ¡Corte de energía global! Luces encendidas en rojo de emergencia.");

            // Activar chispas dinámicas en las lámparas apagadas de los túneles
            Renderer[] allRends = FindObjectsOfType<Renderer>(true);
            foreach (Renderer r in allRends)
            {
                if (r != null && r.gameObject != null)
                {
                    string rName = r.gameObject.name.ToLower();
                    if ((rName.Contains("light") || rName.Contains("lamp") || rName.Contains("luz")) && r.gameObject.GetComponent<TunnelElectricSparks>() == null)
                    {
                        r.gameObject.AddComponent<TunnelElectricSparks>();
                    }
                }
            }

            // Reproducir sonido de apagón
            if (outageStartClip != null)
            {
                Vector3 playPos = (Camera.main != null) ? Camera.main.transform.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(outageStartClip, playPos, 1.0f);
            }

            // Duración del apagón
            float duration = Random.Range(minOutageDuration, maxOutageDuration);
            yield return new WaitForSeconds(duration);

            // --- RESTAURAR ENERGÍA ---
            isGlobalPowerOutage = false;
            Debug.Log("[TunnelsPowerOutageManager] ¡Energía restaurada! Luces normales encendidas.");

            // Desactivar chispas al volver la energía
            TunnelElectricSparks[] activeSparks = FindObjectsOfType<TunnelElectricSparks>();
            foreach (var spark in activeSparks)
            {
                if (spark != null) Destroy(spark);
            }

            // Reproducir sonido de interruptor
            if (outageEndClip != null)
            {
                Vector3 playPos = (Camera.main != null) ? Camera.main.transform.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(outageEndClip, playPos, 0.85f);
            }

            // Intervalo de energía estable antes del próximo apagón
            float cooldown = Random.Range(minTimeBetweenOutages, maxTimeBetweenOutages);
            yield return new WaitForSeconds(cooldown);
        }
    }

    private void Update()
    {
        // Control de atmósfera y niebla industrial roja de pánico según el estado del apagón global
        Color targetAmbient = isGlobalPowerOutage ? new Color(0.08f, 0.015f, 0.015f) : new Color(0.05f, 0.07f, 0.08f);
        Color targetFog = isGlobalPowerOutage ? new Color(0.06f, 0.008f, 0.008f) : new Color(0.035f, 0.05f, 0.06f);
        float targetFogDensity = isGlobalPowerOutage ? 0.040f : 0.022f;

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, Time.deltaTime * 2.5f);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 2.5f);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 2.5f);
    }

    private void OnDestroy()
    {
        isGlobalPowerOutage = false;
    }
}
