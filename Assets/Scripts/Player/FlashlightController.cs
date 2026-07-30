using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("Configuracin de Luz")]
    public Light flashlightLight;      // Foco de luz de la linterna
    public KeyCode toggleKey = KeyCode.F; // Tecla para encender/apagar

    [Header("Sonido")]
    public AudioClip clickSound;        // Sonido de clic
    private AudioSource audioSource;

    [Header("Batera (Opcional)")]
    public bool useBattery = true; // Cambiado por defecto a true para supervivencia      // Consume batera?
    public float maxBattery = 100f;
    public float currentBattery;
    public float drainRate = 0.035f;     // Consumo super optimizado (~45 minutos reales por bateria)

    [HideInInspector] public bool isGlitchedByMonster = false;
    private PlayerSanity playerSanity;   // Referencia a la cordura del jugador
    private float baseIntensity;         // Brillo base de la linterna
    private Light fillLight;             // Foco secundario (Point Light) para rellenar de luz las paredes cercanas al jugador

    private void Start()
    {
        // Obtener o aadir el componente AudioSource para los clics
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Si no se asignó sonido o la referencia está rota, cargamos desde Resources
        if (clickSound == null || !clickSound)
        {
            clickSound = Resources.Load<AudioClip>("Audio/Compartido/Linterna_Click");
        }

        // Si no se asign luz en el Inspector, la creamos dinmicamente
        if (flashlightLight == null)
        {
            CreateDynamicFlashlight();
        }
        else
        {
            // Calibrar la luz existente del Inspector para que actǧe como el foco LED de una cǭmara de video
            flashlightLight.type = LightType.Spot;
            flashlightLight.range = 55f;
            flashlightLight.spotAngle = 70f;
            flashlightLight.intensity = 13.5f;
            flashlightLight.shadows = LightShadows.Soft;
            flashlightLight.color = new Color(0.92f, 0.97f, 1f); // Luz fra LED digital

            // Crear el fillLight como hijo de la luz existente para iluminar las paredes laterales
            if (transform.Find("Player_Flashlight_Fill") == null)
            {
                GameObject fillObj = new GameObject("Player_Flashlight_Fill");
                fillObj.transform.SetParent(flashlightLight.transform, false);
                fillLight = fillObj.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 8f;
                fillLight.intensity = 1.8f;
                fillLight.color = new Color(0.92f, 0.97f, 1f);
                fillLight.shadows = LightShadows.None;
                fillLight.enabled = flashlightLight.enabled;
            }
        }

        if (flashlightLight != null)
        {
            baseIntensity = flashlightLight.intensity;
        }

        // Evitar que el cuerpo o cǭpsula del jugador proyecte sombras que bloqueen la linterna
        foreach (var r in transform.root.GetComponentsInChildren<Renderer>())
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        currentBattery = maxBattery;
        playerSanity = GetComponent<PlayerSanity>();
        if (playerSanity == null) playerSanity = GetComponentInParent<PlayerSanity>();

        // LUZ DE ADAPTACIÓN VISUAL NOCTURNA (OJOS DEL JUGADOR):
        // Permite ver el contorno de paredes, puertas y pasillos en un rango de 9 metros sin arruinar la atmósfera de terror.
        if (transform.Find("Player_Eyes_Ambient") == null)
        {
            GameObject ambientObj = new GameObject("Player_Eyes_Ambient");
            ambientObj.transform.SetParent(transform, false);
            ambientObj.transform.localPosition = Vector3.zero;
            
            Light ambientLight = ambientObj.AddComponent<Light>();
            ambientLight.type = LightType.Point;
            ambientLight.range = 9.0f;
            ambientLight.intensity = 0.85f;
            ambientLight.color = new Color(0.20f, 0.24f, 0.32f); // Azul marino suave / visión nocturna natural
            ambientLight.shadows = LightShadows.None;
            ambientLight.enabled = true;
        }
    }

    private void Update()
    {
        // SISTEMA ANTIBUG DE ESCENAS: Si cambiamos de escena y la luz se rompió o perdió la cámara activa
        if (flashlightLight == null || (Camera.main != null && flashlightLight.transform.parent != Camera.main.transform))
        {
            ReacoplarLinternaACamaraActual();
        }

        // Encender o apagar la linterna al presionar la tecla F
        if (MobileInput.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }

        // Control de consumo de batera
        if (useBattery && flashlightLight != null && flashlightLight.enabled)
        {
            float activeDrainRate = drainRate;
            bool isTunnelsLevel = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TunnelsMap";

            if (isTunnelsLevel)
            {
                // En los túneles el consumo es 4 veces más lento (dura 500 segundos)
                activeDrainRate = drainRate * 0.25f;
            }

            float batteryPercent = (currentBattery / maxBattery) * 100f;

            // MODO AHORRO DE ENERGÍA (< 20% de Batería): La batería se descarga 60% más lento y la intensidad baja a tenue
            if (batteryPercent < 20f && batteryPercent > 0f)
            {
                activeDrainRate *= 0.40f; // Descarga 60% más lenta
                float energySaverIntensity = baseIntensity * (0.40f + (batteryPercent / 20f) * 0.30f); // 40% a 70% de brillo
                if (flashlightLight.intensity > energySaverIntensity && !isGlitchedByMonster)
                {
                    flashlightLight.intensity = Mathf.Lerp(flashlightLight.intensity, energySaverIntensity, Time.deltaTime * 3f);
                }
            }

            currentBattery -= activeDrainRate * Time.deltaTime;
            currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);

            if (currentBattery <= 0f)
            {
                if (isTunnelsLevel)
                {
                    // En los túneles la linterna NO se apaga, se queda fija en modo tenue de emergencia (30% de brillo)
                    float dimIntensity = baseIntensity * 0.3f;
                    if (flashlightLight.intensity > dimIntensity)
                    {
                        flashlightLight.intensity = dimIntensity;
                    }
                    if (fillLight != null && fillLight.intensity > 0.5f)
                    {
                        fillLight.intensity = 0.5f;
                    }
                }
                else
                {
                    // En otros niveles se apaga de forma normal al llegar a cero
                    flashlightLight.enabled = false;
                    if (fillLight != null) fillLight.enabled = false;
                    PlayClickSound();
                    Debug.Log("Flashlight: Linterna agotada sin batera.");
                }
            }
        }

        // Simular parpadeos e inestabilidad si la cordura es baja (< 45%) o por la cercanía del monstruo
        if (flashlightLight != null && flashlightLight.enabled)
        {
            if (isGlitchedByMonster)
            {
                // Parpadeo rápido caótico por interferencia del monstruo
                if (Random.value < 0.35f)
                {
                    flashlightLight.intensity = Random.Range(0.0f, baseIntensity * 0.15f);
                    if (fillLight != null) fillLight.intensity = Random.Range(0.0f, 0.2f);
                }
                else
                {
                    flashlightLight.intensity = baseIntensity;
                    if (fillLight != null) fillLight.intensity = 1.8f;
                }
            }
            else if (playerSanity != null && playerSanity.sanity <= 45f)
            {
                // La gravedad de la falla escala con el nivel de miedo
                float severity = Mathf.InverseLerp(45f, 0f, playerSanity.sanity); // 0 a 1
                
                if (Random.value < severity * 0.25f) // Probabilidad basada en el pnico
                {
                    // Apagar momentneamente o atenuar la linterna
                    flashlightLight.intensity = Random.Range(0.05f, baseIntensity * 0.3f);
                    if (fillLight != null) fillLight.intensity = Random.Range(0.02f, 0.5f);
                }
                else
                {
                    flashlightLight.intensity = baseIntensity;
                    if (fillLight != null) fillLight.intensity = 1.8f;
                }
            }
            else if (flashlightLight.intensity != baseIntensity)
            {
                // Restaurar intensidad si la cordura es normal
                flashlightLight.intensity = baseIntensity;
                if (fillLight != null) fillLight.intensity = 1.8f;
            }
        }
    }

    public void ToggleFlashlight()
    {
        if (flashlightLight == null) return;

        // No encender si no hay batera
        if (useBattery && currentBattery <= 0f && !flashlightLight.enabled)
        {
            Debug.Log("Flashlight: No hay suficiente batera.");
            return;
        }

        flashlightLight.enabled = !flashlightLight.enabled;
        if (fillLight != null) fillLight.enabled = flashlightLight.enabled;
        PlayClickSound();
    }

    private void PlayClickSound()
    {
        if (clickSound != null)
        {
            // Obtener la posición de la cámara principal para que el sonido se reproduzca directamente en los oídos del jugador
            Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clickSound, playPos, 1.0f);
        }
        else
        {
            Debug.LogWarning("[FlashlightDebug] PlayClickSound falló. clickSound es null.");
        }
    }

    private void CreateDynamicFlashlight()
    {
        // Crear un nuevo GameObject para la linterna fsica
        GameObject flashlightObj = new GameObject("Player_Flashlight");

        // Buscar la cmara principal para hacerla hija de ella
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            flashlightObj.transform.SetParent(mainCam.transform);
            // Centrarla con respecto a la cmara
            flashlightObj.transform.localPosition = new Vector3(0f, 0f, -0.05f); // Co-axial con la lente de la cámara para evitar sombras laterales // Ligeramente a la derecha y abajo
            flashlightObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            flashlightObj.transform.SetParent(transform);
            flashlightObj.transform.localPosition = new Vector3(0f, 1f, 0f);
            flashlightObj.transform.localRotation = Quaternion.identity;
            Debug.LogWarning("Flashlight: No se encontr la cmara principal. Linterna acoplada al cuerpo.");
        }

        // Agregar el componente Light y configurarlo como foco de cámara de video
        flashlightLight = flashlightObj.AddComponent<Light>();
        flashlightLight.type = LightType.Spot;
        flashlightLight.range = 55f;           // Alcance ideal para interiores oscuros
        flashlightLight.spotAngle = 70f;       // Ángulo muy amplio (70°) que cubre todo el encuadre de la pantalla
        flashlightLight.intensity = 13.5f;     // Brillo difuso equilibrado
        flashlightLight.shadows = LightShadows.Soft; // Sombras suaves
        flashlightLight.color = new Color(0.92f, 0.97f, 1f); // Blanco frío LED de cámara digital
        flashlightLight.enabled = false;       // Empieza apagada

        // Foco secundario de Point (luz de relleno ambiental para iluminar paredes cercanas)
        GameObject fillObj = new GameObject("Player_Flashlight_Fill");
        fillObj.transform.SetParent(flashlightObj.transform, false);
        fillLight = fillObj.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 8f; // Rango corto de 8 metros
        fillLight.intensity = 1.8f; // Intensidad suave
        fillLight.color = new Color(0.92f, 0.97f, 1f); // Mismo tono frío LED
        fillLight.shadows = LightShadows.None; // Sin sombras para no impactar el rendimiento
        fillLight.enabled = false;
        
        Debug.Log("Flashlight: Foco de luz dinmico creado y configurado correctamente.");
    }

    // Metodo para recargar la bateria de la linterna (llamado al recoger pilas)
    public void Recharge(float amount)
    {
        currentBattery += amount;
        currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);
        Debug.Log("Flashlight: Linterna recargada. Bateria actual: " + currentBattery + "%");
    }

    private void ReacoplarLinternaACamaraActual()
    {
        Debug.Log("FlashlightController: Reacoplando luz física a la nueva cámara de la escena...");
        
        // Destruir el objeto huérfano anterior si existía
        if (flashlightLight != null)
        {
            Destroy(flashlightLight.gameObject);
            flashlightLight = null;
        }

        // Buscar y destruir cualquier objeto residual del fillLight o ambient
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Transform oldFlashlight = mainCam.transform.Find("Player_Flashlight");
            if (oldFlashlight != null) Destroy(oldFlashlight.gameObject);
        }

        // Crear la linterna de nuevo
        CreateDynamicFlashlight();
    }
}

