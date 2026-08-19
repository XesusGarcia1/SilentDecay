using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using StarterAssets;

public class PlayerHealth : MonoBehaviour
{
    public float health = 100f;

    [Header("Pantalla de Muerte / Game Over")]
    private bool isDead = false;
    private float deathTimer = 0f;
    private float blackFadeAlpha = 0f;
    private float initialAudioListenerVolume = 1f;
    public TextMeshProUGUI healthText;  // Texto de TextMeshPro que ocultaremos

    [Header("Screamer Settings")]
    private bool isPlaying3DScare = false;
    private float screamerTimer = 0f;
    private float screamerDuration = 1.8f;
    private AudioClip screamerSound;
    private AudioClip secondaryScreamerSound;
    private Transform monsterTransform;
    private Vector3 originalCamPos;
    private Vector3 originalPlayerPos;
    private Texture2D customScreamerTex;

    [Header("Animación de Muerte de Cordura")]
    private bool diedBySanity = false;
    private Vector3 startCamLocalPos;
    private Quaternion startCamLocalRot;

    [Header("Sonido de Latidos (Pánico)")]
    [Tooltip("Arrastra aquí el archivo de sonido de latidos (o guárdalo como 'Latido' en Resources).")]
    public AudioClip heartbeatSound;
    private AudioSource heartbeatAudioSource;
    public float maxHeartbeatVolume = 0.8f;

    [Header("Regeneración segmentada (Bajo la Luz)")]
    [Tooltip("Velocidad de regeneración rápida dentro de tu segmento de salud actual.")]
    public float normalRegenRate = 2.5f;
    [Tooltip("Velocidad de regeneración ultra lenta para pasar al siguiente segmento superior.")]
    public float crossingRegenRate = 0.8f;

    private Texture2D vignetteTex;
    private PlayerSanity playerSanity;
    
    // Límite dinámico del segmento alcanzado. Segmentos: [0-35], (35-70], (70-100]
    private float currentRegenLimit = 100f;

    [Header("Sistema de Vidas (Granny Style)")]
    private bool isRespawning = false;
    private string respawnStatusText = "";
    private System.Collections.Generic.List<Canvas> disabledCanvases = new System.Collections.Generic.List<Canvas>();
    private bool respawnCoroutineStarted = false;
    private bool isInvulnerable = false;

    void Start()
    {
        #if UNITY_ANDROID || UNITY_IOS
        if (GameManager.Instance != null)
        {
            GameManager.Instance.FixMobileCanvasScaling();
        }
        #endif

        // Asegurar que el HUD de camara tipo camcorder este presente
        if (GetComponent<CamcorderOverlay>() == null)
        {
            gameObject.AddComponent<CamcorderOverlay>();
        }

        playerSanity = GetComponent<PlayerSanity>();
        if (playerSanity == null) playerSanity = GetComponentInParent<PlayerSanity>();

        // Ocultar el texto de salud de la pantalla
        if (healthText != null)
        {
            healthText.gameObject.SetActive(false);
        }

        // Intentar auto-cargar latido de corazón desde Resources si no está configurado
        if (heartbeatSound == null) heartbeatSound = Resources.Load<AudioClip>("Audio/Compartido/Latido");

        if (heartbeatSound != null)
        {
            heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
            heartbeatAudioSource.clip = heartbeatSound;
            heartbeatAudioSource.loop = true;
            heartbeatAudioSource.spatialBlend = 0f; // Sonido Estéreo
            heartbeatAudioSource.volume = 0f;
            heartbeatAudioSource.Play();
        }

        // Generar una textura de Vignette (borde rojo) procedural al inicio
        CreateProceduralVignette();
        
        // Inicializar el límite según la salud inicial
        UpdateRegenLimit();

        // Asegurar que el audio esté restaurado al reiniciar la partida
        AudioListener.volume = 1f;
        Time.timeScale = 1f;

        // Cargar recursos del screamer según el nivel actual
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "TunnelsMap")
        {
            screamerSound = Resources.Load<AudioClip>("Audio/Compartido/Screamer");
            if (screamerSound == null) screamerSound = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Monstruo_Alerta");
        }
        else
        {
            // Hospital (SampleScene)
            screamerSound = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/Monstruo_Alerta");
        }

        // Cargar clip secundario de Jumpscare (agregado por el usuario)
        secondaryScreamerSound = Resources.Load<AudioClip>("Audio/Monstruos/BookHead/jumpscareStingNormal");
    }

    private void CreateProceduralVignette()
    {
        vignetteTex = new Texture2D(32, 32);
        vignetteTex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01((dist - 0.4f) / 0.6f);
                vignetteTex.SetPixel(x, y, new Color(1f, 0f, 0f, alpha));
            }
        }
        vignetteTex.Apply();
    }

    void Update()
    {
        // Lógica de transición de Game Over si el jugador ha muerto
        if (isDead)
        {
            if (isPlaying3DScare)
            {
                screamerTimer += Time.unscaledDeltaTime;
                
                // Enfocar la cámara al rostro del monstruo con sacudida violenta
                if (Camera.main != null)
                {
                    if (monsterTransform != null)
                    {
                        Vector3 targetFace = monsterTransform.position + Vector3.up * 1.7f;
                        Camera.main.transform.LookAt(targetFace);
                    }
                    
                    // Aplicar vibración/temblor de impacto horror
                    Camera.main.transform.position = originalCamPos + Random.insideUnitSphere * 0.08f;
                }

                if (screamerTimer >= screamerDuration)
                {
                    isPlaying3DScare = false;
                    
                    // Restaurar la posición original de la cámara antes del fundido a negro
                    if (Camera.main != null)
                    {
                        Camera.main.transform.position = originalCamPos;
                    }

                    // Iniciar la secuencia de respawn SIEMPRE (RespawnSequence maneja el caso de GameManager null internamente)
                    if (!respawnCoroutineStarted)
                    {
                        respawnCoroutineStarted = true;
                        StartCoroutine(RespawnSequence());
                    }
                }
                return;
            }

            // Si la secuencia de reaparición está activa, la corrutina maneja el tiempo y el fade
            if (isRespawning || respawnCoroutineStarted)
            {
                return;
            }

            deathTimer += Time.unscaledDeltaTime; // Unscaled para que funcione al pausar
            blackFadeAlpha = Mathf.Min(1f, deathTimer / 1.5f); // Fundido a negro rápido de 1.5 segundos
            
            // Fading progresivo del audio general del juego
            AudioListener.volume = Mathf.Lerp(initialAudioListenerVolume, 0f, blackFadeAlpha);

            if (deathTimer >= 1.5f)
            {
                Time.timeScale = 0f; // Pausar todo lo demás una vez finalizado el fade
                
                // Forzar cursor libre y visible para poder interactuar con los botones
                MobileInput.SetCursorState(false);
            }
            return;
        }

        // 1. Regeneración de salud si el jugador está bajo la luz
        bool inLight = (playerSanity != null) && playerSanity.IsInLight();
        if (inLight && health > 0f && health < 100f)
        {
            RegenerateHealth();
        }

        // 2. Control dinámico del sonido de latidos según el estado del jugador
        if (heartbeatAudioSource != null && heartbeatSound != null)
        {
            float healthPercent = health / 100f;
            float sanityPercent = (playerSanity != null) ? (playerSanity.sanity / 100f) : 1f;

            // Medir la desesperación del jugador (si salud < 40% o cordura < 35%)
            float healthDistress = Mathf.InverseLerp(0.40f, 0f, healthPercent); // 0 a 1
            float sanityDistress = Mathf.InverseLerp(0.35f, 0f, sanityPercent); // 0 a 1
            float distress = Mathf.Max(healthDistress, sanityDistress); // Tomar el peor caso

            if (distress > 0.01f)
            {
                heartbeatAudioSource.volume = distress * maxHeartbeatVolume;
                heartbeatAudioSource.pitch = Mathf.Lerp(1.0f, 1.55f, distress);
            }
            else
            {
                heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, 0f, Time.deltaTime * 0.5f);
                heartbeatAudioSource.pitch = Mathf.MoveTowards(heartbeatAudioSource.pitch, 1.0f, Time.deltaTime * 0.5f);
            }
        }
    }

    private void RegenerateHealth()
    {
        // Si estamos por debajo del límite de nuestro segmento dañado, regeneramos rápido.
        // Si intentamos pasar al siguiente segmento superior, regeneramos a velocidad crossing (ultra lenta).
        if (health < currentRegenLimit)
        {
            health += normalRegenRate * Time.deltaTime;
            health = Mathf.Min(health, currentRegenLimit);
        }
        else
        {
            health += crossingRegenRate * Time.deltaTime;
        }

        health = Mathf.Min(health, 100f);

        // Si nos curamos al máximo, reajustar el límite
        if (health >= 100f)
        {
            currentRegenLimit = 100f;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvulnerable) return; // Ignorar daño temporalmente tras reaparecer

        health -= damage;
        health = Mathf.Max(0f, health);

        // Al recibir daño, recalculamos el límite de regeneración rápida
        UpdateRegenLimit();

        Debug.Log("Salud restante: " + health + " (Límite de regeneración rápida: " + currentRegenLimit + ")");

        if (health <= 0f && !isDead)
        {
            isDead = true;
            TriggerDeathTransition();
        }
    }

    private void TriggerDeathTransition()
    {
        isDead = true;
        isPlaying3DScare = true;
        screamerTimer = 0f;
        deathTimer = 0f;
        blackFadeAlpha = 0f;
        initialAudioListenerVolume = AudioListener.volume;

        // IMPORTANTE: NO establecer isRespawning aquí todavía.
        // Se determinará dentro de RespawnSequence() después de RestarVida().
        // Si lo ponemos aquí, Update() hace return y el screamerTimer nunca avanza → freeze.
        isRespawning = false;

        // Precalcular texto de día para mostrarlo en la pantalla negra
        int currentLives = (GameManager.Instance != null) ? GameManager.Instance.vidasActuales : 3;
        int nextLives = Mathf.Max(1, currentLives - 1);
        if (LocalizationManager.Instance != null)
        {
            respawnStatusText = nextLives == 1 
                ? LocalizationManager.Instance.Get("hud_intentos_ult") 
                : $"{LocalizationManager.Instance.Get("hud_dia_prefix")}{(GameManager.Instance != null ? GameManager.Instance.maxVidas : 3) - nextLives + 1}";
        }
        else
        {
            respawnStatusText = nextLives == 1 ? "Último intento" : $"Día {(GameManager.Instance != null ? GameManager.Instance.maxVidas : 3) - nextLives + 1}";
        }

        // Desactivar Cinemachine Brain para tomar control directo de la cámara
        if (Camera.main != null)
        {
            Cinemachine.CinemachineBrain brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            
            originalCamPos = Camera.main.transform.position;
        }

        // Buscar al monstruo en la escena (funciona en ambos mapas: Hospital, Túneles y Depósito Industrial)
        GameObject monsterObj = GameObject.Find("ThePhenomenon");
        if (monsterObj == null)
        {
            var ai = FindFirstObjectByType<PhenomenonAIController>();
            if (ai != null) monsterObj = ai.gameObject;
        }
        // Hospital: BookHead (EnemyAIController)
        if (monsterObj == null)
        {
            var bookHead = FindFirstObjectByType<EnemyAIController>();
            if (bookHead != null) monsterObj = bookHead.gameObject;
        }
        // TheCreep (CrawlerAI)
        if (monsterObj == null)
        {
            var creep = FindFirstObjectByType<CrawlerAI>();
            if (creep != null) monsterObj = creep.gameObject;
        }
        // La Réplica: TheRebuttal (ReplicaAIController)
        if (monsterObj == null)
        {
            var replica = FindFirstObjectByType<ReplicaAIController>();
            if (replica != null) monsterObj = replica.gameObject;
        }
        
        if (monsterObj == null) monsterObj = GameObject.Find("BookHead");
        if (monsterObj == null) monsterObj = GameObject.Find("BookHeadMonster");
        if (monsterObj == null) monsterObj = GameObject.Find("TheCreep");
        if (monsterObj == null) monsterObj = GameObject.Find("TheRebuttal");
        
        monsterTransform = monsterObj != null ? monsterObj.transform : null;

        bool playDefaultScream = true;
        // Cargar imagen de screamer específica si morimos por La Réplica (TheRebuttal)
        customScreamerTex = null;
        if (monsterObj != null && (monsterObj.name.Contains("TheRebuttal") || monsterObj.GetComponent<ReplicaAIController>() != null))
        {
            playDefaultScream = false;
            Debug.Log("[PlayerHealth]: Cargando screamer para La Réplica...");
            customScreamerTex = Resources.Load<Texture2D>("DepositoIndustrial/La Replica/La Replica/LaReplicaScream");
            if (customScreamerTex == null) customScreamerTex = Resources.Load<Texture2D>("DepositoIndustrial/La Replica/LaReplicaScream");
            if (customScreamerTex == null) customScreamerTex = Resources.Load<Texture2D>("LaReplicaScream");

            // Fallback ultra-robusto: buscar recursivamente cualquier Texture2D con nombre lareplicascream
            if (customScreamerTex == null)
            {
                Debug.Log("[PlayerHealth]: Intentando búsqueda por escaneo en Resources...");
                Texture2D[] allTexs = Resources.LoadAll<Texture2D>("");
                foreach (Texture2D t in allTexs)
                {
                    if (t != null && t.name.ToLower().Contains("lareplicascream"))
                    {
                        customScreamerTex = t;
                        Debug.Log("[PlayerHealth]: Screamer encontrado por escaneo: " + t.name);
                        break;
                    }
                }
            }

            if (customScreamerTex == null)
            {
                Debug.LogError("[PlayerHealth]: No se pudo encontrar la textura 'LaReplicaScream' en Resources mediante ningún método.");
            }
        }


        // Reproducir grito aterrador en 2D al volumen máximo (independiente de la atenuación)
        if (playDefaultScream && (screamerSound != null || secondaryScreamerSound != null))
        {
            GameObject screamObj = new GameObject("ScreamTempAudio");
            
            if (screamerSound != null)
            {
                AudioSource source1 = screamObj.AddComponent<AudioSource>();
                source1.clip = screamerSound;
                source1.volume = 1.0f;
                source1.spatialBlend = 0f;
                source1.ignoreListenerVolume = true;
                source1.Play();
            }

            if (secondaryScreamerSound != null)
            {
                AudioSource source2 = screamObj.AddComponent<AudioSource>();
                source2.clip = secondaryScreamerSound;
                source2.volume = 1.0f;
                source2.spatialBlend = 0f;
                source2.ignoreListenerVolume = true;
                source2.Play();
            }

            float duration = Mathf.Max(
                screamerSound != null ? screamerSound.length : 0f, 
                secondaryScreamerSound != null ? secondaryScreamerSound.length : 0f
            );
            Destroy(screamObj, duration + 0.1f);
        }

        // Desactivar cualquier Canvas en escena (HUD, controles móviles, etc.) para una inmersión limpia
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        disabledCanvases.Clear();
        foreach (Canvas c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy)
            {
                disabledCanvases.Add(c);
                c.gameObject.SetActive(false);
            }
        }

        // Determinar si murió por cordura (locura extrema)
        diedBySanity = (playerSanity != null && playerSanity.sanity <= 1.5f);

        if (diedBySanity && Camera.main != null)
        {
            // Desactivar Cinemachine para poder animar la cámara de forma manual y realista
            Cinemachine.CinemachineBrain brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null)
            {
                brain.enabled = false;
            }
            startCamLocalPos = Camera.main.transform.localPosition;
            startCamLocalRot = Camera.main.transform.localRotation;
            Debug.Log("PlayerHealth: Iniciando animación de cámara caída por locura.");
        }

        // Guardar la posición inicial del jugador para congelarla en la muerte
        originalPlayerPos = transform.position;

        // Desactivar el CharacterController para detener toda física, gravedad o fuerzas de empuje
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) cc = GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // Desactivar o congelar Rigidbody si existe
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        // Desactivar componentes de control de movimiento del jugador
        FirstPersonController fpController = GetComponent<FirstPersonController>();
        if (fpController == null) fpController = GetComponentInParent<FirstPersonController>();
        if (fpController != null)
        {
            fpController.enabled = false;
        }

        StarterAssetsInputs fpInput = GetComponent<StarterAssetsInputs>();
        if (fpInput == null) fpInput = GetComponentInParent<StarterAssetsInputs>();
        if (fpInput != null)
        {
            fpInput.enabled = false;
            fpInput.move = Vector2.zero;
            fpInput.sprint = false;
        }

        // Desbloquear cursor del mouse
        MobileInput.SetCursorState(false);
    }

    private void UpdateRegenLimit()
    {
        if (health <= 35f)
        {
            currentRegenLimit = 35f;
        }
        else if (health <= 70f)
        {
            currentRegenLimit = 70f;
        }
        else
        {
            currentRegenLimit = 100f;
        }
    }

    void OnGUI()
    {
        // 1. Renderizar Vignette roja (solo si está vivo, dañado y no ha completado el juego)
        bool winTriggered = false;
        TunnelsGenerator tunnels = FindObjectOfType<TunnelsGenerator>();
        if (tunnels != null && tunnels.VictoryFadeAlpha > 0f) winTriggered = true;

        if (!isDead && health < 100f && vignetteTex != null && !winTriggered)
        {
            float healthPercent = health / 100f;
            float baseAlpha = Mathf.Lerp(0.85f, 0f, healthPercent);

            float pulse = 1.0f;
            if (health < 35f)
            {
                float speedMultiplier = 2.5f;
                if (heartbeatAudioSource != null && heartbeatAudioSource.volume > 0.1f)
                {
                    speedMultiplier = 2.5f * heartbeatAudioSource.pitch;
                }
                pulse = 1.0f + Mathf.PingPong(Time.time * speedMultiplier, 0.2f);
            }

            float finalAlpha = Mathf.Clamp01(baseAlpha * pulse);

            if (finalAlpha > 0.01f)
            {
                GUI.color = new Color(1f, 1f, 1f, finalAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTex);
                GUI.color = Color.white;
            }
        }

        // 2. Renderizar pantalla de fundido a negro y Game Over
        if (isDead)
        {
            if (isPlaying3DScare)
            {
                if (customScreamerTex != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), customScreamerTex, ScaleMode.ScaleAndCrop);
                }
                else
                {
                    // Fallback visual en caso de que la imagen falle: pintar pantalla roja con fondo oscuro
                    GUI.color = new Color(0.15f, 0.02f, 0.02f, 1f);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                return; // No dibujar fundido a negro durante el jumpscare 3D
            }

            // Fundido negro
            GUI.color = new Color(0f, 0f, 0f, blackFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Mostrar el menú de muerte o la pantalla de reaparición cuando ya está fundido a negro
            if (blackFadeAlpha >= 0.95f)
            {
                if (isRespawning)
                {
                    // Pantalla de reaparición (Día restante / Intento restante) estilo Granny
                    GUIStyle dayStyle = new GUIStyle();
                    dayStyle.fontSize = 54;
                    dayStyle.alignment = TextAnchor.MiddleCenter;
                    dayStyle.fontStyle = FontStyle.Bold;
                    dayStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

                    GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 40, 600, 80), respawnStatusText.ToUpper(), dayStyle);
                }
                else
                {
                    // TÍTULO: GAME OVER en estilo horror retro
                    GUIStyle titleStyle = new GUIStyle();
                    titleStyle.fontSize = 72;
                    titleStyle.alignment = TextAnchor.MiddleCenter;
                    titleStyle.fontStyle = FontStyle.Bold;

                    // Sombra negra terrorífica
                    titleStyle.normal.textColor = Color.black;
                    GUI.Label(new Rect(Screen.width / 2 - 300 + 4, Screen.height / 2 - 160 + 4, 600, 100), "GAME OVER", titleStyle);

                    // Texto rojo sangre
                    titleStyle.normal.textColor = new Color(0.7f, 0f, 0f);
                    GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 160, 600, 100), "GAME OVER", titleStyle);

                    // Subtexto estilizado
                    GUIStyle subStyle = new GUIStyle();
                    subStyle.fontSize = 18;
                    subStyle.alignment = TextAnchor.MiddleCenter;
                    subStyle.fontStyle = FontStyle.Italic;
                    subStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                    
                    string consumedText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("hud_consumido") : "La oscuridad te ha consumido...";
                    GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 80, 600, 40), consumedText, subStyle);

                    // Botones interactivos — ancho amplio para que el texto nunca se corte
                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.fontSize = 20;
                    buttonStyle.fontStyle = FontStyle.Bold;
                    buttonStyle.normal.textColor = Color.white;
                    buttonStyle.hover.textColor = new Color(0.9f, 0.1f, 0.1f);
                    buttonStyle.alignment = TextAnchor.MiddleCenter;

                    float btnW = 260f;
                    float btnH = 52f;
                    float btnX = Screen.width / 2f - btnW / 2f;

                    // --- BOTÓN 1: REVIVIR CON ANUNCIO (Si AdMob está activo y listo) ---
                    if (SilentDecay.Core.AdManager.Instance != null && SilentDecay.Core.AdManager.Instance.enableAds)
                    {
                        Rect adReviveRect = new Rect(btnX, Screen.height / 2f + 10f, btnW, btnH);
                        bool isAdReady = SilentDecay.Core.AdManager.Instance.IsReviveAdReady();

                        GUIStyle adButtonStyle = new GUIStyle(buttonStyle);
                        adButtonStyle.normal.textColor = isAdReady ? new Color(1f, 0.85f, 0.2f) : Color.gray; // Dorado si listo

                        string reviveAdText = isAdReady ? "📺 REVIVIR (ANUNCIO)" : "📺 CARGANDO ANUNCIO...";
                        
                        if (GUI.Button(adReviveRect, reviveAdText, adButtonStyle) && isAdReady)
                        {
                            SilentDecay.Core.AdManager.Instance.ShowRewardedRevive(() =>
                            {
                                ReviveFromAd();
                            });
                        }
                    }

                    // Botón 2: REINTENTAR (reiniciar desde el inicio)
                    float retryY = (SilentDecay.Core.AdManager.Instance != null && SilentDecay.Core.AdManager.Instance.enableAds) 
                        ? Screen.height / 2f + 70f 
                        : Screen.height / 2f + 20f;
                    
                    Rect retryRect = new Rect(btnX, retryY, btnW, btnH);
                    string retryBtnText = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("hud_reintentar_inicio")
                        : "JUGAR DE NUEVO";
                    if (GUI.Button(retryRect, retryBtnText, buttonStyle))
                    {
                        Time.timeScale = 1f;
                        AudioListener.volume = 1f;
                        if (GameManager.Instance != null)
                            GameManager.Instance.InicializarVidasParaMapa(GameManager.Instance.maxVidas);
                        string targetScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        if (string.IsNullOrEmpty(targetScene) || targetScene == "LoadingScene") targetScene = "Test_ModularHospital";
                        SceneLoader.LoadScene(targetScene);
                    }

                    // Botón 3: IR AL MENÚ (Regresa al menú principal con Anuncio Intersticial)
                    float menuY = (SilentDecay.Core.AdManager.Instance != null && SilentDecay.Core.AdManager.Instance.enableAds) 
                        ? Screen.height / 2f + 130f 
                        : Screen.height / 2f + 86f;

                    Rect menuRect = new Rect(btnX, menuY, btnW, btnH);
                    string menuBtnText = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("hud_ir_menu")
                        : "IR AL MENÚ";
                    if (GUI.Button(menuRect, menuBtnText, buttonStyle))
                    {
                        Time.timeScale = 1f;
                        AudioListener.volume = 1f;

                        if (SilentDecay.Core.AdManager.Instance != null)
                        {
                            SilentDecay.Core.AdManager.Instance.ShowInterstitialTransition(() =>
                            {
                                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                            });
                        }
                        else
                        {
                            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                        }
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator RespawnSequence()
    {
        isRespawning = true;

        // Deshabilitar componentes de control del jugador
        CharacterController ccOnDeath = GetComponent<CharacterController>();
        if (ccOnDeath == null) ccOnDeath = GetComponentInParent<CharacterController>();
        if (ccOnDeath != null) ccOnDeath.enabled = false;

        FirstPersonController fpControllerOnDeath = GetComponent<FirstPersonController>();
        if (fpControllerOnDeath == null) fpControllerOnDeath = GetComponentInParent<FirstPersonController>();
        if (fpControllerOnDeath != null) fpControllerOnDeath.enabled = false;

        StarterAssetsInputs inputsOnDeath = GetComponent<StarterAssetsInputs>();
        if (inputsOnDeath == null) inputsOnDeath = GetComponentInParent<StarterAssetsInputs>();
        if (inputsOnDeath != null) inputsOnDeath.enabled = false;

        // 1. Transición limpia a pantalla negra (1 segundo)
        float fadeTimer = 0f;
        float fadeDuration = 1.0f;
        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.unscaledDeltaTime;
            blackFadeAlpha = Mathf.Clamp01(fadeTimer / fadeDuration);
            AudioListener.volume = Mathf.Lerp(initialAudioListenerVolume, 0f, blackFadeAlpha);
            yield return null;
        }
        blackFadeAlpha = 1.0f;

        // 2. Restar vida en GameManager
        bool tieneMasIntentos = false;
        if (GameManager.Instance != null)
        {
            Debug.Log($"[RESPAWN] Antes de RestarVida: vidasActuales={GameManager.Instance.vidasActuales}, maxVidas={GameManager.Instance.maxVidas}");
            tieneMasIntentos = GameManager.Instance.RestarVida();
            Debug.Log($"[RESPAWN] Después de RestarVida: vidasActuales={GameManager.Instance.vidasActuales}, tieneMasIntentos={tieneMasIntentos}");
        }
        else
        {
            Debug.LogWarning("[RESPAWN] GameManager.Instance es NULL — asumiendo que hay vidas (fallback a true).");
            tieneMasIntentos = true;
        }

        if (tieneMasIntentos)
        {
            isRespawning = true;
            isInvulnerable = true;
            int vidasQuedan = (GameManager.Instance != null) ? GameManager.Instance.vidasActuales : 2;
            if (LocalizationManager.Instance != null)
            {
                respawnStatusText = vidasQuedan == 1 
                    ? LocalizationManager.Instance.Get("hud_intentos_ult") 
                    : $"{LocalizationManager.Instance.Get("hud_dia_prefix")}{(GameManager.Instance != null ? GameManager.Instance.maxVidas : 3) - vidasQuedan + 1}";
            }
            else
            {
                respawnStatusText = vidasQuedan == 1 ? "Último intento" : $"Día {(GameManager.Instance != null ? GameManager.Instance.maxVidas : 3) - vidasQuedan + 1}";
            }

            // 3. Buscar y desactivar monstruo temporalmente (funciona en Hospital Y Túneles)
            // Hospital: BookHead (EnemyAIController) | Túneles: Phenomenon (PhenomenonAIController) | TheCreep (CrawlerAI)
            GameObject monsterObj = GameObject.Find("ThePhenomenon");
            if (monsterObj == null)
            {
                var phenomenon = FindFirstObjectByType<PhenomenonAIController>();
                if (phenomenon != null) monsterObj = phenomenon.gameObject;
            }
            if (monsterObj == null)
            {
                var bookHead = FindFirstObjectByType<EnemyAIController>();
                if (bookHead != null) monsterObj = bookHead.gameObject;
            }
            if (monsterObj == null)
            {
                var creep = FindFirstObjectByType<CrawlerAI>();
                if (creep != null) monsterObj = creep.gameObject;
            }
            if (monsterObj == null) monsterObj = GameObject.Find("TheCreep");
            if (monsterObj == null)
            {
                var replica = UnityEngine.Object.FindFirstObjectByType<ReplicaAIController>();
                if (replica != null) monsterObj = replica.gameObject;
            }
            if (monsterObj == null) monsterObj = GameObject.Find("TheRebuttal");
            if (monsterObj != null)
            {
                // Desactivar NavMeshAgent antes de SetActive(false) para evitar errores de Unity
                var agentTemp = monsterObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agentTemp != null) agentTemp.enabled = false;
                monsterObj.SetActive(false);
                Debug.Log("PlayerHealth: Monstruo desactivado temporalmente para respawn: " + monsterObj.name);
            }
            else
            {
                Debug.LogWarning("PlayerHealth: No se encontró monstruo activo para desactivar durante el respawn.");
            }
            
            // Pausar juego y mostrar pantalla del Día (2.5 segundos)
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(2.5f);

            // 4. Reaparecer jugador en su punto de spawn
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReaparecerJugador(gameObject);
            }

            if (monsterObj != null)
            {
                // REACTIVAR AL MONSTRUO Y AL AGENTE ANTES DE MOVERLO
                // Si el agent o el objeto están inactivos, "Warp" o "SetDestination" tiran error
                monsterObj.SetActive(true);
                var agentTemp2 = monsterObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agentTemp2 != null) agentTemp2.enabled = true;

                // Mover al monstruo lejos ahora que está activo
                var enemyAI = monsterObj.GetComponent<EnemyAIController>();
                if (enemyAI != null) enemyAI.ForceRelocateFarAway(transform.position);

                var crawlerAI = monsterObj.GetComponent<CrawlerAI>();
                if (crawlerAI != null) crawlerAI.ForceRelocateFarAway(transform.position);

                // Forzar rebind de animación para evitar que se congele en la pose de ataque anterior
                Animator monsterAnimator = monsterObj.GetComponentInChildren<Animator>();
                if (monsterAnimator != null)
                {
                    monsterAnimator.applyRootMotion = false;
                    monsterAnimator.Rebind();
                    monsterAnimator.Update(0f);
                }

                // El período de gracia y recolocación se delegarán internamente al script de cada monstruo
                var phenomenonCtrl = monsterObj.GetComponent<PhenomenonAIController>();
                if (phenomenonCtrl != null)
                {
                    phenomenonCtrl.TriggerRespawnGracePeriod(90f);
                }

                var bookHeadCtrl2 = monsterObj.GetComponent<EnemyAIController>();
                if (bookHeadCtrl2 != null)
                {
                    bookHeadCtrl2.detectionRange = 0f;
                    bookHeadCtrl2.StartCoroutine(ActivateBookHeadGraceDelay(bookHeadCtrl2, 90f));
                }

                var crawlerCtrl = monsterObj.GetComponent<CrawlerAI>();
                if (crawlerCtrl != null)
                {
                    crawlerCtrl.TriggerRespawnGracePeriod(90f);
                }

                var replicaCtrl = monsterObj.GetComponent<ReplicaAIController>();
                if (replicaCtrl != null)
                {
                    replicaCtrl.ResetToInitialState();
                }
 
                Debug.Log("PlayerHealth: Monstruo reactivado de forma simple. IA toma el control de su reposicionamiento.");
            }

            // 6. Restaurar salud y controles
            health = 100f;
            currentRegenLimit = 100f;
            if (playerSanity != null)
            {
                playerSanity.sanity = 100f;
            }

            CharacterController cc = GetComponent<CharacterController>();
            if (cc == null) cc = GetComponentInParent<CharacterController>();
            if (cc != null) cc.enabled = true;

            if (Camera.main != null)
            {
                Camera.main.transform.SetParent(null); // Cinemachine usa la cámara en la raíz
                Camera.main.transform.position = transform.position + Vector3.up * 1.5f; // Posición aproximada de la cabeza en el nuevo spawn
                Cinemachine.CinemachineBrain brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
                if (brain != null) 
                {
                    brain.enabled = true;
                    brain.ManualUpdate(); // Forzar actualización inmediata para evitar transiciones lentas desde el pasillo
                }
            }

            FirstPersonController fpController = GetComponent<FirstPersonController>();
            if (fpController == null) fpController = GetComponentInParent<FirstPersonController>();
            if (fpController != null) fpController.enabled = true;

            StarterAssetsInputs fpInput = GetComponent<StarterAssetsInputs>();
            if (fpInput == null) fpInput = GetComponentInParent<StarterAssetsInputs>();
            if (fpInput != null) fpInput.enabled = true;

            foreach (Canvas c in disabledCanvases)
            {
                if (c != null) c.gameObject.SetActive(true);
            }
            disabledCanvases.Clear();

            // 7. Transición suave de regreso al juego
            float fadeOutTimer = 0f;
            float fadeOutDuration = 1.2f;
            while (fadeOutTimer < fadeOutDuration)
            {
                fadeOutTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(fadeOutTimer / fadeOutDuration);
                blackFadeAlpha = Mathf.Lerp(1f, 0f, t);
                AudioListener.volume = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            blackFadeAlpha = 0f;

            Time.timeScale = 1f;
            AudioListener.volume = 1f;
            isDead = false;
            isRespawning = false;
            respawnCoroutineStarted = false;
            deathTimer = 0f;
            
            // Usar MobileInput para asegurar que en móviles no se bloquee el cursor
            MobileInput.SetCursorState(true);

            StartCoroutine(DisableInvulnerabilityDelayed(3.0f));
        }
        else
        {
            // Game Over definitivo (pantalla con botones)
            isRespawning = false;
            Time.timeScale = 0f;
            MobileInput.SetCursorState(false);
        }
    }

    private System.Collections.IEnumerator DisableInvulnerabilityDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        isInvulnerable = false;
        Debug.Log("PlayerHealth: Inmunidad de reaparición desactivada.");
    }

    private System.Collections.IEnumerator ActivateBookHeadGraceDelay(EnemyAIController controller, float delay)
    {
        controller.detectionRange = 0f;
        yield return new WaitForSeconds(delay);
        controller.detectionRange = 7.5f;
        Debug.Log("[PlayerHealth] Período de gracia del BookHead finalizado. Detección activada.");
    }

    /// <summary>
    /// Revivir al jugador a través del sistema de recompensa de anuncios (AdMob).
    /// Restaura la salud al 100%, desactiva estados de muerte y relocaliza monstruos lejanos.
    /// </summary>
    public void ReviveFromAd()
    {
        health = 100f;
        isDead = false;
        isRespawning = false;
        deathTimer = 0f;
        blackFadeAlpha = 0f;
        AudioListener.volume = 1f;
        Time.timeScale = 1f;

        if (playerSanity != null)
        {
            playerSanity.sanity = 100f;
        }

        // Relocalizar enemigos lejanos para evitar campeo en el punto de respawn
        var bookheads = FindObjectsOfType<EnemyAIController>();
        foreach (var bh in bookheads)
        {
            bh.ForceRelocateFarAway(transform.position);
        }

        var crawlers = FindObjectsOfType<CrawlerAI>();
        foreach (var cr in crawlers)
        {
            cr.ForceRelocateFarAway(transform.position);
        }

        MobileInput.SetCursorState(true);
        isInvulnerable = true;
        StartCoroutine(DisableInvulnerabilityDelayed(3.0f));

        Debug.Log("[PlayerHealth] Jugador REVIVIDO exitosamente mediante Anuncio Recompensado.");
    }
}
