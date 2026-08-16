using UnityEngine;

/// <summary>
/// Auto-configura el sonido ambiental del mapa (MannequinCourtyardMapSound.mp3),
/// las goteras 3D de las tuberías y el parpadeo con sonido en las lámparas.
/// </summary>
public class IndustrialDepotAudioSetup : MonoBehaviour
{
    private void Awake()
    {
        SetupAmbientAudio();
        SetupPipesAudio();
        SetupLampsAudio();
    }

    private void SetupAmbientAudio()
    {
        StartCoroutine(DelayedAmbientStart());
    }

    private System.Collections.IEnumerator DelayedAmbientStart()
    {
        // Esperar un poco a que termine la transición de carga del menú
        yield return new WaitForSecondsRealtime(0.5f);

        AudioSource ambient = GetComponent<AudioSource>();
        if (ambient == null) ambient = gameObject.AddComponent<AudioSource>();

        // Cargar el sonido de ambiente específico colocado en Assets/Resources/Audio/MannequinCourtyardMap/
        AudioClip clip = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/MannequinCourtyardMapSound");
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/Hospital/activeLoopSound");
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/Tuneles/AmbienteTunel");

        if (clip != null)
        {
            ambient.clip = clip;
            ambient.loop = true;
            ambient.volume = 0.65f;
            ambient.spatialBlend = 0f; // Sonido ambiente 2D global
            ambient.playOnAwake = true;
            if (!ambient.isPlaying) ambient.Play();
            Debug.Log($"IndustrialDepotAudioSetup: Reproduciendo ambiente loop '{clip.name}'.");
        }
    }

    private void SetupPipesAudio()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (GameObject go in allObjects)
        {
            string n = go.name.ToLower();
            if (n.Contains("pipe") || n.Contains("tubo") || n.Contains("tuber") || n.Contains("ventilation"))
            {
                if (go.GetComponent<Renderer>() != null || go.GetComponent<Collider>() != null)
                {
                    if (go.GetComponent<PipeDripAudio>() == null)
                    {
                        go.AddComponent<PipeDripAudio>();
                        count++;
                    }
                }
            }
        }
    }

    private void SetupLampsAudio()
    {
        Light[] lights = FindObjectsOfType<Light>();
        foreach (Light l in lights)
        {
            if (l.type == LightType.Point)
            {
                bool isLamp = l.gameObject.name.ToLower().Contains("lampara") || 
                              l.gameObject.name.ToLower().Contains("lamp") || 
                              (l.transform.parent != null && l.transform.parent.name.ToLower().Contains("lamp"));
                if (isLamp)
                {
                    if (l.GetComponent<FlickeringLight>() == null)
                    {
                        l.gameObject.AddComponent<FlickeringLight>();
                    }
                }
            }
        }
    }
}
