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
        // Escalar los tiempos de apagón según la dificultad elegida
        string savedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
        if (savedDifficulty == "FACIL")
        {
            minTimeBetweenOutages = 60f;
            maxTimeBetweenOutages = 100f;
            minOutageDuration = 8f;
            maxOutageDuration = 15f;
        }
        else if (savedDifficulty == "DIFICIL")
        {
            minTimeBetweenOutages = 25f;
            maxTimeBetweenOutages = 45f;
            minOutageDuration = 18f;
            maxOutageDuration = 32f;
        }
        else // NORMAL
        {
            minTimeBetweenOutages = 40f;
            maxTimeBetweenOutages = 75f;
            minOutageDuration = 12f;
            maxOutageDuration = 22f;
        }

        // Crear un AudioSource 2D para reproducir los sonidos en la cabeza del jugador
        globalAudioSource = gameObject.AddComponent<AudioSource>();
        globalAudioSource.spatialBlend = 0.0f; // 2D (Estéreo directo)
        globalAudioSource.volume = 0.85f;
        globalAudioSource.playOnAwake = false;

        // Cargar sonidos
        outageStartClip = Resources.Load<AudioClip>("Apagon_Sonido");
        outageEndClip = Resources.Load<AudioClip>("Interruptor");

        // Iniciar la corrutina del ciclo de energía
        StartCoroutine(PowerCycleRoutine());
    }

    private IEnumerator PowerCycleRoutine()
    {
        // Espera inicial antes del primer apagón
        yield return new WaitForSeconds(Random.Range(30f, 50f));

        while (true)
        {
            // --- INICIAR APAGÓN ---
            isGlobalPowerOutage = true;
            Debug.Log("[TunnelsPowerOutageManager] ¡Corte de energía global! Luces apagadas.");

            // Reproducir sonido de apagón
            if (globalAudioSource != null && outageStartClip != null)
            {
                globalAudioSource.PlayOneShot(outageStartClip);
            }

            // Duración del apagón
            float duration = Random.Range(minOutageDuration, maxOutageDuration);
            yield return new WaitForSeconds(duration);

            // --- RESTAURAR ENERGÍA ---
            isGlobalPowerOutage = false;
            Debug.Log("[TunnelsPowerOutageManager] ¡Energía restaurada! Luces encendidas.");

            // Reproducir sonido de interruptor
            if (globalAudioSource != null && outageEndClip != null)
            {
                globalAudioSource.PlayOneShot(outageEndClip);
            }

            // Intervalo de energía estable antes del próximo apagón
            float cooldown = Random.Range(minTimeBetweenOutages, maxTimeBetweenOutages);
            yield return new WaitForSeconds(cooldown);
        }
    }

    private void OnDestroy()
    {
        isGlobalPowerOutage = false;
    }
}
