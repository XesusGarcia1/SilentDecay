using UnityEngine;

/// <summary>
/// Emite el sonido de gotera (Gotera.mp3) de forma 3D cercana y confiable.
/// </summary>
public class PipeDripAudio : MonoBehaviour
{
    [Header("Configuración de Distancia 3D")]
    [SerializeField] private float maxAudibleDistance = 5.0f;

    [Header("Tiempos e Intervalos")]
    [SerializeField] private float minInterval = 2.0f;
    [SerializeField] private float maxInterval = 5.0f;

    private AudioSource audioSource;
    private AudioClip dripClip;
    private float nextDripTime;
    private Transform playerTransform;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // Configuración 3D estricta y de rango corto
        audioSource.spatialBlend = 1.0f; // 100% sonido 3D para posicionamiento perfecto
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 0.8f;
        audioSource.maxDistance = maxAudibleDistance;

        if (dripClip == null) dripClip = Resources.Load<AudioClip>("Audio/Compartido/Gotera");

        FindPlayer();

        // Primera gotera rápida (0.2s a 1.2s) para escuchar la gota al acercarse
        nextDripTime = Time.time + Random.Range(0.2f, 1.2f);
    }

    private void FindPlayer()
    {
        Camera cam = Camera.main;
        if (cam != null) { playerTransform = cam.transform; return; }

        UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
        if (cc != null) { playerTransform = cc.transform; return; }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj == null) pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) playerTransform = pObj.transform;
    }

    private void Update()
    {
        if (dripClip == null) return;

        if (Time.time >= nextDripTime)
        {
            if (playerTransform == null) FindPlayer();

            if (playerTransform != null)
            {
                // Usar posición global real de esta tubería específica
                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                
                if (distToPlayer <= maxAudibleDistance)
                {
                    if (audioSource != null)
                    {
                        audioSource.pitch = Random.Range(0.88f, 1.12f);
                        audioSource.PlayOneShot(dripClip, Random.Range(0.55f, 0.85f));
                    }
                }
            }

            ScheduleNextDrip();
        }
    }

    private void ScheduleNextDrip()
    {
        nextDripTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
