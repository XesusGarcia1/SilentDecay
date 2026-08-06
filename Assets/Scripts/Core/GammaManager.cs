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

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Aplicar la configuración de Gamma guardada tan pronto como se cargue cualquier escena
        AplicarGamma(PlayerPrefs.GetFloat("GammaLevel", 1.0f));
    }

    public static void AplicarGamma(float gammaValue)
    {
        float clamped = Mathf.Clamp(gammaValue, 0.5f, 2.0f);
        PlayerPrefs.SetFloat("GammaLevel", clamped);

        // 1. Modificar la intensidad ambiental de RenderSettings de forma balanceada
        RenderSettings.ambientIntensity = clamped;

        // 2. Ajustar el color de luz ambiental global si el modo ambiental es Flat (Tinte de penumbra limpio)
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            // Usamos un gris neutro oscuro para iluminar pasillos sin blanquear ni pintar de lechoso el juego
            RenderSettings.ambientLight = new Color(0.14f * clamped, 0.14f * clamped, 0.16f * clamped);
        }
    }
}
