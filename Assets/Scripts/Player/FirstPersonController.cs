using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 3.2f;
        public float SprintSpeed = 5.0f;
        public float RotationSpeed = 1.0f;
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        public float JumpHeight = 0.0f; // Salto desactivado para juego de horror
        public float Gravity = -15.0f;

        [Space(10)]
        [Header("Climbing")]
        public bool isClimbing = false;
        public float climbSpeed = 3.0f;

        [Header("Carga Pesada")]
        [Tooltip("Se activa automáticamente cuando el jugador carga un objeto pesado (ej: pieza de escalera)")]
        public bool isCarryingHeavy = false;
        [Tooltip("Multiplicador de velocidad al cargar algo pesado (0.7 = 70% de la velocidad normal)")]
        public float heavySpeedMultiplier = 0.7f;

        [Header("Parálisis por Miedo")]
        [Tooltip("Multiplicador de velocidad durante eventos de terror (ej. 0.35 = 35% de velocidad)")]
        public float fearParalysisMultiplier = 1.0f;

        [Space(10)]
        public float JumpTimeout = 0.1f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        // cinemachine
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        [Header("Anti-Mareo (Anti-Motion Sickness)")]
        [Tooltip("Suavizado de la rotación de cámara. Valores más bajos son más suaves.")]
        public float CameraRotationSmoothing = 15f; 
        private float _smoothedPitchVelocity;
        private float _smoothedYawVelocity;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // Audio variables
        private AudioSource _audioSource;
        public AudioClip walkSound;  // Sonido al caminar
        public AudioClip runSound;   // Sonido al correr
        private float _nextStepTime = 0.0f;
        public float stepInterval = 0.5f; // Intervalo entre pasos

        [Header("Stamina System")]
        public float maxStamina = 100f;
        public float staminaDrainRate = 12f;
        public float staminaRegenRate = 8f;
        private float _currentStamina;
        private bool _isExhausted = false;

        private AudioSource _breathAudioSource;
        private AudioClip _breathMaleClip;
        private AudioClip _breathFemaleClip;
        private UnityEngine.UI.Image _mobileSprintButtonImage;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private string _currentAnimName = "Idle_Player"; // Almacena el nombre de la animación actual
        private bool _isFemaleAnimationSet = false; // Indica si se usan las animaciones con sufijo 'Female'
        private Animator _animator; // Referencia al animador del modelo de personaje

        private const float _threshold = 0.01f;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            // Lógica robusta de selección de personaje (incluso si uno está desactivado en el Inspector)
            string selected = PlayerPrefs.GetString("SelectedCharacter", "Male");
            string rootName = transform.root.gameObject.name;

            // Buscar en todos los objetos raíz de la escena para asegurar que encendemos al que corresponde
            GameObject male = null;
            GameObject female = null;
            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name.Contains("PlayerMale")) male = go;
                if (go.name.Contains("PlayerFemale")) female = go;
            }

            if (selected == "Male" && rootName.Contains("PlayerFemale"))
            {
                if (male != null) male.SetActive(true);
                transform.root.gameObject.SetActive(false);
                return; // Detener la ejecución del Awake en el personaje desactivado
            }
            else if (selected == "Female" && rootName.Contains("PlayerMale"))
            {
                if (female != null) female.SetActive(true);
                transform.root.gameObject.SetActive(false);
                return; // Detener la ejecución del Awake en el personaje desactivado
            }

            // Sanitizar etiquetas (tags) para evitar ambigüedades en FindGameObjectWithTag.
            // Si el objeto raíz está etiquetado como "Player", lo cambiamos a "Untagged"
            // y nos aseguramos de que solo este objeto (la cápsula de control activa) tenga la etiqueta "Player".
            if (transform.root.gameObject.tag == "Player")
            {
                transform.root.gameObject.tag = "Untagged";
            }
            gameObject.tag = "Player";

            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }

            // get the audio source component
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

            // Aplicar sensibilidad de mouse guardada en la configuración
            RotationSpeed = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);

            // Optimización de Z-Buffer para eliminar Z-Fighting / parpadeo de texturas en los mapas
            if (_mainCamera == null) _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            if (_mainCamera != null)
            {
                Camera cam = _mainCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.nearClipPlane = 0.08f;
                }
            }
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput != null)
            {
                // Forzar refresco del PlayerInput para evitar pérdida de dispositivos
                // al activarse dinámicamente desde otro script en Awake.
                _playerInput.enabled = false;
                _playerInput.enabled = true;
            }
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            // Log scale hierarchy for debugging
            Debug.LogFormat("[PlayerScaleDebug] GameObject: {0}, localScale: {1}, lossyScale: {2}", name, transform.localScale, transform.lossyScale);
            Transform parentTrans = transform.parent;
            while (parentTrans != null)
            {
                Debug.LogFormat("[PlayerScaleDebug] Parent: {0}, localScale: {1}, lossyScale: {2}", parentTrans.name, parentTrans.localScale, parentTrans.lossyScale);
                parentTrans = parentTrans.parent;
            }
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Ethan") || child.name.Contains("char1"))
                {
                    Debug.LogFormat("[PlayerScaleDebug] Child: {0}, localScale: {1}, lossyScale: {2}", child.name, child.localScale, child.lossyScale);
                }
            }

            // Buscar animador en los hijos (modelo 3D)
            _animator = GetComponentInChildren<Animator>();
            // Desactivar Root Motion para que las animaciones no muevan la posición del modelo
            // (el movimiento lo controla el CharacterController, no las animaciones)
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                
                // Detectar si el animator tiene los estados femeninos
                _isFemaleAnimationSet = _animator.HasState(0, Animator.StringToHash("Idle_PlayerFemale"));
                if (_isFemaleAnimationSet)
                {
                    _currentAnimName = "Idle_PlayerFemale";
                }
            }


            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            // Inicializar Stamina
            _currentStamina = maxStamina;
            _breathAudioSource = gameObject.AddComponent<AudioSource>();
            _breathAudioSource.loop = true;
            _breathAudioSource.volume = 0f;
            _breathAudioSource.playOnAwake = true;
            _breathAudioSource.spatialBlend = 0f; // 2D sound for breathing
            
            _breathMaleClip = Resources.Load<AudioClip>("Audio/Players/RespiracionMale");
            _breathFemaleClip = Resources.Load<AudioClip>("Audio/Players/RespiracionFemale");

            string rootNameStr = transform.root.gameObject.name;
            if (rootNameStr.Contains("PlayerFemale"))
                _breathAudioSource.clip = _breathFemaleClip;
            else
                _breathAudioSource.clip = _breathMaleClip;

            if (_breathAudioSource.clip != null)
                _breathAudioSource.Play();

            // Configurar dinámicamente el canvas de inputs en móvil
            SetupMobileUI();

            if (GetComponent<FirstPersonStaminaHelper>() == null)
            {
                gameObject.AddComponent<FirstPersonStaminaHelper>();
            }
        }

        private void Update()
        {
            if (_controller == null || !_controller.enabled || !_controller.gameObject.activeInHierarchy) return;

            if (isClimbing)
            {
                HandleClimbing();
            }
            else
            {
                JumpAndGravity();
                GroundedCheck();
                Move();
            }

            // Lógica de Audio de Respiración
            if (_breathAudioSource != null)
            {
                // El volumen aumenta cuando la stamina baja. Cuando la stamina está llena (maxStamina), volumen = 0
                float exhaustionLevel = 1f - (_currentStamina / maxStamina);
                
                // Si la stamina está arriba del 80%, forzamos silencio
                if (exhaustionLevel < 0.2f) exhaustionLevel = 0f;
                
                _breathAudioSource.volume = Mathf.Lerp(_breathAudioSource.volume, exhaustionLevel, Time.deltaTime * 2f);
            }
        }

        private void HandleClimbing()
        {
            // Detener la acumulación de gravedad
            _verticalVelocity = 0f;
            _fallTimeoutDelta = FallTimeout;
            Grounded = true; // Para evitar animaciones de caída

            // Movimiento libre en 3D basado en a dónde mira la cámara (Estilo Half-Life)
            // Esto permite que el jugador mire hacia la plataforma y presione W para salir de la escalera.
            Transform camTransform = Camera.main != null ? Camera.main.transform : transform;
            Vector3 moveDir = (camTransform.forward * _input.move.y + camTransform.right * _input.move.x).normalized;

            _controller.Move(moveDir * (climbSpeed * Time.deltaTime));

            // Reiniciar stamina al escalar
            _currentStamina = maxStamina;
            _isExhausted = false;

            // Sonido de escalada (pasos)
            if (moveDir.magnitude > 0.1f && Time.time >= _nextStepTime)
            {
                AudioClip climbSound = Resources.Load<AudioClip>("Audio/MannequinCourtyardMap/EscaleraMetálica");
                if (climbSound != null)
                {
                    _audioSource.PlayOneShot(climbSound);
                }
                _nextStepTime = Time.time + stepInterval;
            }
        }

        private void LateUpdate()
        {
            CameraRotation();

        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void CameraRotation()
        {
            // Si es móvil o mouse, el input es un delta directo de arrastre de dedo. No multiplicar por Time.deltaTime.
            bool isDirectDelta = IsCurrentDeviceMouse || Application.isMobilePlatform;
            float deltaTimeMultiplier = isDirectDelta ? 1.0f : Time.deltaTime;

            float targetPitchVelocity = 0f;
            float targetYawVelocity = 0f;

            if (_input.look.sqrMagnitude >= _threshold)
            {
                targetPitchVelocity = _input.look.y * RotationSpeed * deltaTimeMultiplier;
                targetYawVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;
            }

            // Aplicar suavizado lineal (Lerp) para evitar giros instantáneos secos que provocan cinetosis (mareos)
            _smoothedPitchVelocity = Mathf.Lerp(_smoothedPitchVelocity, targetPitchVelocity, Time.deltaTime * CameraRotationSmoothing);
            _smoothedYawVelocity = Mathf.Lerp(_smoothedYawVelocity, targetYawVelocity, Time.deltaTime * CameraRotationSmoothing);

            _cinemachineTargetPitch += _smoothedPitchVelocity;
            _rotationVelocity = _smoothedYawVelocity;

            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
            transform.Rotate(Vector3.up * _rotationVelocity);
        }

        public void ResetCameraRotation(float targetYaw)
        {
            _cinemachineTargetPitch = 0f;
            _smoothedPitchVelocity = 0f;
            _smoothedYawVelocity = 0f;

            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.localRotation = Quaternion.identity;
            }

            if (Camera.main != null)
            {
                Camera.main.transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }

        private void Move()
        {
            // Lógica de Stamina
            bool isMoving = _input.move != Vector2.zero;
            if (_input.sprint && isMoving && !_isExhausted)
            {
                _currentStamina -= staminaDrainRate * Time.deltaTime;
                if (_currentStamina <= 0f)
                {
                    _currentStamina = 0f;
                    _isExhausted = true;
                    _input.sprint = false;
                    
                    // Asegurar que el botón virtual se actualice
                    if (_mobileSprintButtonImage != null)
                    {
                        var virtualButton = _mobileSprintButtonImage.GetComponent<UIVirtualButton>();
                        // Simulamos que dejó de presionarlo si es posible, sino simplemente evitamos que corra
                    }
                }
            }
            else
            {
                RegenerateStamina(Time.deltaTime);
            }

            // Actualizar interfaz visual
            if (_mobileSprintButtonImage != null)
            {
                _mobileSprintButtonImage.fillAmount = _currentStamina / maxStamina;
            }

            // Velocidad objetivo normal
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // Reducir velocidad si carga algo pesado
            if (isCarryingHeavy)
            {
                targetSpeed *= heavySpeedMultiplier;
            }

            // Reducir velocidad durante evento de parálisis por miedo (Slenderman Staredown)
            targetSpeed *= fearParalysisMultiplier;
            
            // Si está corriendo pero con poca energía (menos del 30%), pierde velocidad gradualmente
            if (_input.sprint && isMoving && _currentStamina < maxStamina * 0.3f)
            {
                float factor = _currentStamina / (maxStamina * 0.3f);
                targetSpeed = Mathf.Lerp(MoveSpeed, SprintSpeed, factor);
            }

            if (!isMoving) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
            if (_input.move != Vector2.zero)
            {
                inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
            }

            _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            if (_animator != null)
            {
                // Determinar el nombre de la animación según la velocidad, el input y el género del personaje
                string suffix = _isFemaleAnimationSet ? "Female" : "";
                string animName = "Idle_Player" + suffix;
                
                // Si el jugador realmente se está moviendo con el input
                if (_input.move != Vector2.zero && _speed > 0.1f)
                {
                    // Si corre, usar running_Player/running_PlayerFemale; si camina, usar walking_Player/walking_PlayerFemale
                    animName = (_input.sprint ? "running_Player" : "walking_Player") + suffix;
                }

                // Solo iniciar la transición si cambiamos de animación
                if (animName != _currentAnimName)
                {
                    _currentAnimName = animName;
                    _animator.CrossFade(animName, 0.15f);
                }
            }

            PlayFootstepsSound();
        }

        private void PlayFootstepsSound()
        {
            // Check if the player is grounded and moving
            if (Grounded && _controller.velocity.magnitude > 0.1f && Time.time >= _nextStepTime)
            {
                // Detectar si el jugador está tocando/sobrevolando el Trigger de la mancha de charco
                bool isSteppingOnPuddle = false;
                Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.2f, 1.4f, Physics.AllLayers, QueryTriggerInteraction.Collide);
                foreach (Collider c in hits)
                {
                    if (c != null && c.name.Contains("Rastrero_Corrosion"))
                    {
                        isSteppingOnPuddle = true;
                        break;
                    }
                }

                AudioClip puddleSound = Resources.Load<AudioClip>("Audio/Compartido/PisarAgua");
                AudioClip clipToPlay = (isSteppingOnPuddle && puddleSound != null) ? puddleSound : (_input.sprint ? runSound : walkSound);

                _audioSource.PlayOneShot(clipToPlay);
                _nextStepTime = Time.time + stepInterval; // Set next step time
            }
            else if (_controller.velocity.magnitude <= 0.1f && _audioSource.isPlaying) // Stop sound when player stops
            {
                _audioSource.Stop();
            }
        }

        private void JumpAndGravity()
        {
            _input.jump = false; // Función de salto desactivada por completo

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        private void SetupMobileUI()
        {
            // Buscamos el control virtual de mirada que viene en la plantilla
            GameObject lookObj = GameObject.Find("UI_Virtual_Joystick_Look");
            if (lookObj == null)
            {
                lookObj = GameObject.Find("UI_Virtual_TouchZone");
            }

            if (lookObj != null)
            {
                Debug.Log($"[MobileSetup] Configurando zona de mirada táctil en: {lookObj.name}");

                // 1. Encontrar el componente CanvasInputs para redireccionar los eventos de mirada
                var canvasInput = FindObjectOfType<UICanvasControllerInput>();
                if (canvasInput == null)
                {
                    Debug.LogWarning("[MobileSetup] No se encontró UICanvasControllerInput en la escena.");
                    return;
                }

                // 2. Destruir el joystick virtual antiguo si existe en este objeto para que no interfiera
                var oldJoystick = lookObj.GetComponent<UIVirtualJoystick>();
                if (oldJoystick != null)
                {
                    Destroy(oldJoystick);
                }

                // 3. Añadir e inicializar el componente UIVirtualTouchZone (Trackpad)
                var touchZone = lookObj.GetComponent<UIVirtualTouchZone>();
                if (touchZone == null)
                {
                    touchZone = lookObj.AddComponent<UIVirtualTouchZone>();
                }

                // Enlazar evento táctil con la entrada de mirada del jugador
                touchZone.touchZoneOutputEvent = new UIVirtualTouchZone.Event();
                touchZone.touchZoneOutputEvent.AddListener(canvasInput.VirtualLookInput);
                touchZone.magnitudeMultiplier = 1f;
                touchZone.invertYOutputValue = true; // Invertir Y para que arrastrar arriba mire arriba

                // 4. Destruir físicamente todos los hijos visuales del joystick de mirada para evitar residuos
                for (int i = lookObj.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(lookObj.transform.GetChild(i).gameObject);
                }

                // Asegurar que el área de toque del fondo capture clics/arrastres
                var mainImage = lookObj.GetComponent<UnityEngine.UI.Image>();
                if (mainImage == null)
                {
                    mainImage = lookObj.AddComponent<UnityEngine.UI.Image>();
                }
                mainImage.color = new Color(0f, 0f, 0f, 0f);
                mainImage.raycastTarget = true;

                // 5. Expandir la zona de toque para que cubra toda la mitad DERECHA de la pantalla
                var rect = lookObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // Mitad derecha: de X=0.5 a X=1.0, y de Y=0.0 a Y=1.0
                    rect.anchorMin = new Vector2(0.5f, 0.0f);
                    rect.anchorMax = new Vector2(1.0f, 1.0f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;

                    // Enviarlo al fondo de la jerarquía del Canvas para que los botones de salto/correr
                    // se rendericen por delante y capturen los clics de forma prioritaria
                    rect.SetAsFirstSibling();
                }

                Debug.Log("[MobileSetup] ¡El trackpad de mirada táctil ya cubre la mitad derecha de la pantalla y el ojo ha sido removido!");
            }

            // Buscar el botón de correr (Sprint) en la jerarquía del Canvas Móvil
            GameObject sprintBtnObj = GameObject.Find("UI_Virtual_Button_Sprint");
            if (sprintBtnObj != null)
            {
                _mobileSprintButtonImage = sprintBtnObj.GetComponent<UnityEngine.UI.Image>();
                if (_mobileSprintButtonImage != null)
                {
                    _mobileSprintButtonImage.type = UnityEngine.UI.Image.Type.Filled;
                    _mobileSprintButtonImage.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical; // O Radial360 dependiendo del diseño
                    _mobileSprintButtonImage.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
                    _mobileSprintButtonImage.fillAmount = 1f;
                }
            }
        }

        public void RegenerateStamina(float deltaTime)
        {
            if (_currentStamina < maxStamina)
            {
                _currentStamina += staminaRegenRate * deltaTime;
                if (_currentStamina > maxStamina) _currentStamina = maxStamina;
            }
            
            // Permitir correr de nuevo si la stamina recuperó al menos 25%
            if (_isExhausted && _currentStamina > maxStamina * 0.25f)
            {
                _isExhausted = false;
            }

            // Si está exhausto, forzamos no correr
            if (_isExhausted && _input != null)
            {
                _input.sprint = false;
            }

            if (_mobileSprintButtonImage != null)
            {
                _mobileSprintButtonImage.fillAmount = _currentStamina / maxStamina;
            }

            if (_breathAudioSource != null)
            {
                float exhaustionLevel = 1f - (_currentStamina / maxStamina);
                if (exhaustionLevel < 0.2f) exhaustionLevel = 0f;
                _breathAudioSource.volume = Mathf.Lerp(_breathAudioSource.volume, exhaustionLevel, deltaTime * 2f);
            }
        }
    }

    public class FirstPersonStaminaHelper : MonoBehaviour
    {
        private FirstPersonController fpc;

        void Start()
        {
            fpc = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            if (fpc == null) fpc = GetComponent<FirstPersonController>();
            if (fpc == null) return;

            // Si el FirstPersonController está desactivado por abrir la libreta, mapa u otra interfaz UI,
            // pero el juego NO está pausado (Time.timeScale > 0f), continuar regenerando stamina.
            if (!fpc.enabled && fpc.gameObject.activeInHierarchy && Time.timeScale > 0f)
            {
                bool isPaused = PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsGamePaused;
                if (!isPaused)
                {
                    fpc.RegenerateStamina(Time.deltaTime);
                }
            }
        }
    }
}
