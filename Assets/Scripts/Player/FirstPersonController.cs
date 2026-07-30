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

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

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
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            // Configurar dinámicamente el canvas de inputs en móvil
            SetupMobileUI();
        }

        private void Update()
        {
            JumpAndGravity();
            GroundedCheck();
            Move();
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

        private void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

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

                AudioClip puddleSound = Resources.Load<AudioClip>("PisarAgua");
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
            else
            {
                Debug.Log("[MobileSetup] No se encontró un control táctil de mirada.");
            }
        }
    }
}
