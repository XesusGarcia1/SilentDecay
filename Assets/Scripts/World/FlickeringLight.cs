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

    [Header("Audio SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip errorLightClip;

    private Light targetLight;
    private float timer;

    private void Awake()
    {
        targetLight = GetComponent<Light>();
        if (targetLight != null && maxIntensity <= 0)
        {
            maxIntensity = targetLight.intensity;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1.0f; // Sonido 3D estéreo
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2.0f;
        audioSource.maxDistance = 12.0f;

        if (errorLightClip == null) errorLightClip = Resources.Load<AudioClip>("Audio/Hospital/ErrorLightSound");
        if (errorLightClip == null) errorLightClip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
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
                if (audioSource != null && errorLightClip != null && !audioSource.isPlaying)
                {
                    audioSource.pitch = Random.Range(0.85f, 1.15f);
                    audioSource.PlayOneShot(errorLightClip, 0.45f);
                }
            }
            else
            {
                targetLight.intensity = Random.Range(minIntensity, maxIntensity);
            }
        }
    }
}
