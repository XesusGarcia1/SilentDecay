using UnityEngine;

public class GammaManager : MonoBehaviour
{
    private static GammaManager instance;
    public static GammaManager Instance => instance;

    private float currentGamma = 1.0f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            AplicarGamma(PlayerPrefs.GetFloat("GammaLevel", 1.0f));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void AplicarGamma(float gammaValue)
    {
        float clamped = Mathf.Clamp(gammaValue, 0.5f, 2.0f);
        PlayerPrefs.SetFloat("GammaLevel", clamped);

        // 1. Modificar la intensidad ambiental global de RenderSettings
        RenderSettings.ambientIntensity = clamped * 1.2f;

        // 2. Modificar la iluminación de fondo y niebla de la cámara principal en tiempo real
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != null)
            {
                // Ajustar el color de fondo o niebla si está activa
                if (RenderSettings.fog)
                {
                    RenderSettings.fogDensity = 0.01f / clamped;
                }
            }
        }

        // 3. Ajustar la exposición o el color ambiental plano si el modo ambiental es Flat
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            RenderSettings.ambientLight = new Color(0.08f * clamped, 0.09f * clamped, 0.11f * clamped);
        }
    }
}
