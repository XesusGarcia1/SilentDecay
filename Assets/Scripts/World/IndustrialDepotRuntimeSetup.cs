using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Inicializador en tiempo de ejecución para el mapa de Depósito Industrial.
/// Configura dinámicamente el ambiente, los audios, la cámara y el volumen
/// sin necesidad de guardar o modificar el archivo de la escena física en disco.
/// </summary>
public static class IndustrialDepotRuntimeSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene activeScene, LoadSceneMode mode)
    {
        // Solo actuar en el mapa del depósito industrial
        if (activeScene.name == "IndustrialDepotMap")
        {
            Debug.Log("[IndustrialDepotRuntimeSetup]: Inicializando ambiente dinámico.");

            // 1. Configuración de Audio Setup dinámico
            GameObject audioSetupObj = new GameObject("IndustrialDepotAudioSetup_Dynamic");
            audioSetupObj.AddComponent<IndustrialDepotAudioSetup>();
            Debug.Log("[IndustrialDepotRuntimeSetup]: Componente IndustrialDepotAudioSetup agregado dinámicamente.");

            // 2. Configurar la cámara del personaje (Fondo negro limpio)
            Camera playerCam = Camera.main;
            if (playerCam != null)
            {
                playerCam.clearFlags = CameraClearFlags.SolidColor;
                playerCam.backgroundColor = Color.black;
                Debug.Log("[IndustrialDepotRuntimeSetup]: Cámara del jugador configurada con fondo negro sólido.");
            }
            else
            {
                // Si la cámara principal no se encuentra en el primer frame, buscarla de forma retardada
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Camera cam = player.GetComponentInChildren<Camera>();
                    if (cam != null)
                    {
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = Color.black;
                        Debug.Log("[IndustrialDepotRuntimeSetup]: Cámara hija del jugador configurada con fondo negro sólido.");
                    }
                }
            }

            // 3. Configurar la viñeta oscura del personaje en el Volume Profile
            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (Volume vol in volumes)
            {
                if (vol.profile != null)
                {
                    if (vol.profile.TryGet<Vignette>(out var vignette))
                    {
                        vignette.active = true;
                        vignette.intensity.Override(0.38f);
                        vignette.smoothness.Override(0.45f);
                        vignette.color.Override(Color.black);
                        Debug.Log($"[IndustrialDepotRuntimeSetup]: Viñeta URP del volumen '{vol.gameObject.name}' configurada a 0.38 de intensidad.");
                    }
                }
            }

            // 4. Configurar niebla lineal negra de runtime para ocultar el fondo en la distancia
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.015f, 0.02f, 1.0f); // Carbón muy oscuro (evita ceguera del 100%)
            RenderSettings.fogStartDistance = 8.0f;  // Mayor rango de visibilidad nítida cercana (8 metros)
            RenderSettings.fogEndDistance = 25.0f;   // Fundido a oscuridad profunda a los 25 metros
            Debug.Log("[IndustrialDepotRuntimeSetup]: Niebla oscura lineal configurada de 8m a 25m.");
        }
    }
}
