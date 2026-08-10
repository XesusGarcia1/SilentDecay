using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    [Header("Intensity Settings")]
    [SerializeField] private float minIntensity = 0.2f;
    [SerializeField] private float maxIntensity = 3.5f;

    [Header("Flicker Frequency")]
    [SerializeField] private float flickerSpeed = 0.07f;
    [Range(0f, 1f)]
    [SerializeField] private float blackoutChance = 0.15f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;

    private Light targetLight;
    private float timer;

    private void Awake()
    {
        targetLight = GetComponent<Light>();
        if (targetLight != null && maxIntensity <= 0)
        {
            maxIntensity = targetLight.intensity;
        }
    }

    private void Update()
    {
        if (targetLight == null) return;

        timer += Time.deltaTime;

        if (timer >= flickerSpeed)
        {
            timer = 0f;

            if (Random.value < blackoutChance)
            {
                targetLight.intensity = 0f;
                if (audioSource != null && !audioSource.isPlaying)
                {
                    audioSource.pitch = Random.Range(0.85f, 1.15f);
                    audioSource.Play();
                }
            }
            else
            {
                targetLight.intensity = Random.Range(minIntensity, maxIntensity);
            }
        }
    }
}
