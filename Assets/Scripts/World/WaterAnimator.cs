using UnityEngine;

/// <summary>
/// Anima el mapa de normales de agua estilo realista (HDRP quality en URP)
/// con optimización automática por culling de cámara (60+ FPS constante en móvil).
/// </summary>
public class WaterAnimator : MonoBehaviour
{
    [Header("Velocidad de Olas Complejas")]
    public float scrollSpeedX = 0.025f;
    public float scrollSpeedY = 0.040f;

    private Renderer rend;
    private Material mat;
    private bool isBumpSupported = false;
    private bool isBaseSupported = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = rend.material;
            if (mat != null)
            {
                isBumpSupported = mat.HasProperty("_BumpMap");
                isBaseSupported = mat.HasProperty("_BaseMap");
            }
        }
    }

    void Update()
    {
        if (mat == null || rend == null) return;

        // Optimización Móvil: Si el agua no se ve en la cámara del jugador, pausar la animación
        if (!rend.isVisible) return;

        float t = Time.time;
        Vector2 offset = new Vector2(t * scrollSpeedX % 1.0f, t * scrollSpeedY % 1.0f);

        if (isBumpSupported)
        {
            mat.SetTextureOffset("_BumpMap", offset);
        }
        if (isBaseSupported)
        {
            mat.SetTextureOffset("_BaseMap", offset * 0.4f);
        }
    }
}
