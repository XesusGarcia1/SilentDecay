using UnityEngine;

public class TunnelElectricSparks : MonoBehaviour
{
    public float minSparkInterval = 4f;
    public float maxSparkInterval = 12f;

    private Light sparkLight;
    private ParticleSystem sparkParticles;
    private AudioSource audioSource;
    private AudioClip sparkAudioClip;

    private void Start()
    {
        bool isHospital = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower().Contains("hospital");

        // 1. Crear luz de chispa eléctrica efímera
        GameObject lightObj = new GameObject("SparkLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, -0.3f, 0f);

        sparkLight = lightObj.AddComponent<Light>();
        sparkLight.type = LightType.Point;
        sparkLight.color = isHospital ? new Color(1.0f, 0.75f, 0.3f) : new Color(0.4f, 0.75f, 1f);
        sparkLight.range = isHospital ? 7f : 9f;
        sparkLight.intensity = 0.8f; // Resplandor tenue continuo de emergencia para ilumiar la zona
        sparkLight.enabled = true;

        // 2. Crear emisor de partículas de chispas en 3D
        GameObject particleObj = new GameObject("SparkParticles");
        particleObj.transform.SetParent(transform, false);
        particleObj.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        sparkParticles = particleObj.AddComponent<ParticleSystem>();
        var main = sparkParticles.main;
        main.startLifetime = 0.4f;
        main.startSpeed = 3.5f;
        main.startSize = 0.05f;
        main.startColor = isHospital ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.85f, 1f);
        main.gravityModifier = 1.5f; // Las chispas caen hacia el suelo por gravedad
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = sparkParticles.emission;
        emission.enabled = false;

        var shape = sparkParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.2f;

        // 3. AudioSource para el chisporroteo 3D
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 15f;
        audioSource.volume = 0.45f;
        audioSource.playOnAwake = false;

        sparkAudioClip = Resources.Load<AudioClip>("Audio/Compartido/Chispa");

        // Iniciar el ciclo de chispas esporádicas
        StartCoroutine(SparkRoutine());
    }

    private Transform cachedPlayerCamera;

    private System.Collections.IEnumerator SparkRoutine()
    {
        while (true)
        {
            // Si el juego está en pausa, no procesamos chispas
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            float waitTime = Random.Range(minSparkInterval, maxSparkInterval);
            yield return new WaitForSeconds(waitTime);

            // Obtener la cámara del jugador para calcular distancias
            if (cachedPlayerCamera == null && Camera.main != null)
            {
                cachedPlayerCamera = Camera.main.transform;
            }

            bool playerIsNear = false;
            if (cachedPlayerCamera != null)
            {
                playerIsNear = Vector3.Distance(transform.position, cachedPlayerCamera.position) <= 15f;
            }

            // Si el jugador está lejos, mantener apagada la luz para ahorrar drawcalls de luces dinámicas
            if (!playerIsNear)
            {
                if (sparkLight != null && sparkLight.enabled)
                {
                    sparkLight.enabled = false;
                }
                continue; // Saltar el ciclo de chispas visibles
            }

            // Ráfaga rápida de 2 a 4 chispazos (Solo si el jugador está cerca)
            int bursts = Random.Range(2, 5);
            for (int i = 0; i < bursts; i++)
            {
                if (sparkLight != null)
                {
                    sparkLight.enabled = true;
                    sparkLight.intensity = Random.Range(4f, 8f);
                    sparkLight.color = (Random.value > 0.3f) ? new Color(1.0f, 0.8f, 0.3f) : new Color(1f, 0.95f, 0.6f);
                }

                if (sparkParticles != null)
                {
                    sparkParticles.Emit(Random.Range(4, 9)); // Disparar de 4 a 8 chispitas cayendo al suelo
                }

                if (audioSource != null && sparkAudioClip != null)
                {
                    audioSource.PlayOneShot(sparkAudioClip, Random.Range(0.2f, 0.45f));
                }

                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));

                if (sparkLight != null)
                {
                    sparkLight.intensity = 0.8f; // Regresar al resplandor tenue continuo de la zona
                    sparkLight.enabled = true;
                }

                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            }
        }
    }
}
