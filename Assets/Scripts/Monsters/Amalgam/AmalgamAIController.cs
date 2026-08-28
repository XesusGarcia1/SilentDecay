using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

namespace Monsters.Amalgam
{
    /// <summary>
    /// Controlador principal de Inteligencia Artificial y Audio para The Amalgam.
    /// Incluye escalamiento dinámico de intensidad, desahogo de marcos de puerta/paredes,
    /// espejismos/ilusiones psicológicas en pasillos ("Paranoia de Pasillo") y
    /// ráfagas de persecución a quemarropa.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AmalgamAIController : MonoBehaviour
    {
        [Header("Referencias del Entorno")]
        [Tooltip("Transform del jugador (si está vacío, se busca por Tag 'Player' automáticamente)")]
        public Transform playerTransform;

        [Tooltip("Puntos de aparición fijados en el mapa (opcional). Si está vacío, usará posiciones dinámicas en NavMesh.")]
        public Transform[] spawnPoints;

        [Header("Configuración de Movimiento")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6.4f;
        public float attackRange = 2.5f;

        [Header("Distancias de Alerta y Comportamiento Sonoro")]
        [Tooltip("Distancia a la que el jugador comienza a escuchar crujidos ocasionales (Ej: 18m)")]
        public float warningDistance = 18.0f;
        [Tooltip("Distancia a la que los crujidos son más frecuentes y adopta postura amenazante (Ej: 12m)")]
        public float mediumWarningDistance = 12.0f;
        [Tooltip("Distancia a la que The Amalgam arranca a correr y perseguir (Ej: 8m)")]
        public float chaseDistance = 8.0f;

        [Header("Escalado de Intensidad y Agresividad")]
        [Tooltip("Intensidad actual del monstruo (aumenta dinámicamente con cada apagón y tiempo)")]
        public float intensityLevel = 1.0f;
        [Tooltip("Duración máxima de una ráfaga de persecución antes de relocalizarse (segundos)")]
        public float maxChaseDuration = 10.0f;

        [Header("Clips de Audio del Monstruo")]
        [Tooltip("Sonido ambiental principal en 3D (HOMBRELLORANDO)")]
        public AudioClip hombreLlorandoClip;

        [Tooltip("Efectos de crujidos de huesos (Crujido de huesos)")]
        public AudioClip[] crujidoHuesosClips;

        [Tooltip("Audio en loop de persecución (Chase_Amalgam)")]
        public AudioClip chaseAmalgamClip;

        [Tooltip("Fenómeno de apagón ambiental (LamentosNiñosFantasmas)")]
        public AudioClip lamentosNinosClip;

        [Tooltip("Grito aterrador exclusivo para la pantalla de muerte/jumpscare (TerrifyScreamAmalgam)")]
        public AudioClip terrifyScreamClip;

        [Tooltip("Sonido de pasos del monstruo (footsteps)")]
        public AudioClip footstepSoundClip;

        [Tooltip("Efecto de golpes a puertas (tocar-la-puerta)")]
        public AudioClip doorKnockClip;

        [Header("Componentes de Audio")]
        public AudioSource ambientAudioSource;
        public AudioSource boneCrackAudioSource;
        public AudioSource chaseAudioSource;
        public AudioSource screamAudioSource;
        public AudioSource footstepAudioSource;

        [Header("Ajuste de Altura de Malla 3D")]
        [Tooltip("Desplazamiento Y de la malla 3D (ajustar si el modelo flota sobre la cápsula del NavMesh)")]
        public float modelYOffset = 0f;

        private NavMeshAgent agent;
        private AmalgamAnimation anim;
        private IEnemyState currentState;
        private float footstepTimer = 0f;
        private bool isKnockingDoor = false;
        private List<AmalgamIllusion> activeIllusions = new List<AmalgamIllusion>();

        public Transform PlayerTransform => playerTransform;
        public IEnemyState CurrentState => currentState;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
            anim = GetComponent<AmalgamAnimation>();
            if (anim == null) anim = GetComponentInChildren<AmalgamAnimation>();

            // Desactivar Root Motion en el Animator para evitar que las animaciones mece el modelo
            Animator unityAnim = GetComponent<Animator>();
            if (unityAnim == null) unityAnim = GetComponentInChildren<Animator>();
            if (unityAnim != null)
            {
                unityAnim.applyRootMotion = false;
                unityAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // Corregir desplazamiento Y en objetos hijos (malla 3D) si tienen offset local
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    Vector3 localP = child.localPosition;
                    localP.y = modelYOffset;
                    child.localPosition = localP;
                }
            }

            // Asegurar que los rígidos hijos no causen flotación Y
            Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in childRbs)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            SetupAudioSources();
        }

        [HideInInspector] public string currentDifficulty = "NORMAL";
        [HideInInspector] public float silentRepositionInterval = 16.0f;
        [HideInInspector] public float illusionSpawnChance = 0.45f;

        private void Start()
        {
            FindPlayerReference();
            ApplyDifficultySettings();
            SetupNavMeshAgent();
            EnsureGrounded();

            StartCoroutine(PsychologicalDoorKnockRoutine());
            StartCoroutine(SilentRepositionRoutine());

            // Estado inicial: Llanto ambiental en 3D
            ChangeState(new AmalgamIdleCryingState(this, agent, anim));
        }

        private void OnEnable()
        {
            ApplyDifficultySettings();
            SetupNavMeshAgent();
            EnsureGrounded();
            StopChaseAudio();
            StopCryingAudio();
            if (screamAudioSource != null) screamAudioSource.Stop();
            if (chaseAudioSource != null) chaseAudioSource.Stop();
        }

        public void ApplyDifficultySettings()
        {
            currentDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL").ToUpper();

            if (currentDifficulty == "FACIL" || currentDifficulty == "EASY")
            {
                walkSpeed = 3.8f;
                runSpeed = 7.0f; // El jugador corre a 5.0m/s -> El monstruo le saca clara ventaja
                chaseDistance = 6.0f;
                warningDistance = 11.0f;
                mediumWarningDistance = 8.0f;
                intensityLevel = 0.7f;
                silentRepositionInterval = 24.0f;
                illusionSpawnChance = 0.20f;
            }
            else if (currentDifficulty == "DIFICIL" || currentDifficulty == "HARD")
            {
                walkSpeed = 5.2f;
                runSpeed = 9.8f; // Ráfaga feroz e implacable
                chaseDistance = 9.0f;
                warningDistance = 15.0f;
                mediumWarningDistance = 12.0f;
                intensityLevel = 1.5f;
                silentRepositionInterval = 10.0f;
                illusionSpawnChance = 0.70f;
            }
            else // NORMAL
            {
                walkSpeed = 4.4f;
                runSpeed = 8.4f; // Velocidad rápida que acorta distancia en 2 segundos
                chaseDistance = 7.5f;
                warningDistance = 13.0f;
                mediumWarningDistance = 10.0f;
                intensityLevel = 1.0f;
                silentRepositionInterval = 16.0f;
                illusionSpawnChance = 0.45f;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = (currentState is AmalgamChaseState) ? runSpeed : walkSpeed;
            }

            Debug.Log($"[The Amalgam] 💀 Dificultad configurada: {currentDifficulty} | Vel. Caminar: {walkSpeed} | Vel. Carrera: {runSpeed} | Dist. Chase: {chaseDistance}m | Reposición: {silentRepositionInterval}s");
        }

        [HideInInspector] public float chaseCooldownTimer = 0f;

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            if (playerTransform == null)
            {
                FindPlayerReference();
            }

            EnsureGrounded();
            HandleFootsteps();
            
            // Decrementar tiempo de respiro/cooldown tras persecuciones
            if (chaseCooldownTimer > 0f)
            {
                chaseCooldownTimer -= Time.deltaTime;
            }

            // Incrementar intensidad gradualmente con el tiempo de juego (ritmo acelerado)
            intensityLevel += Time.deltaTime * 0.038f;

            currentState?.UpdateState();
        }

        #region Mecánica de Escondite y Visión Directa

        public void OnPlayerHid(Bed bed)
        {
            float distToPlayer = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 999f;
            bool playerInLOS = HasLineOfSightToPlayer();

            // ¿Estaba persiguiéndolo a corta distancia (<=11m) y con visión directa cuando el jugador intentó esconderse?
            if ((currentState is AmalgamChaseState) && distToPlayer <= 11.0f && playerInLOS)
            {
                Debug.Log("[The Amalgam] 👁️ ¡VIÓ AL JUGADOR ESCONDERSE! Asalto directo a la cama...");
                StartCoroutine(ExecuteHidingSpotAssaultRoutine(bed));
            }
            else
            {
                Debug.Log("[The Amalgam] 🛌 El jugador se escondió sin ser visto. Dando tiempo de respiro y calma.");
                StopChaseAudio();
                chaseCooldownTimer = Random.Range(12.0f, 18.0f); // Calma y tiempo de respiro
                TrySilentRelocate();
                ChangeState(new AmalgamIdleCryingState(this, agent, anim));
            }
        }

        public bool HasLineOfSightToPlayer()
        {
            if (playerTransform == null) return false;

            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 target = playerTransform.position + Vector3.up * 1.0f;
            Vector3 dir = (target - origin);
            float dist = dir.magnitude;

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        private IEnumerator ExecuteHidingSpotAssaultRoutine(Bed bed)
        {
            ChangeState(new AmalgamChaseState(this, agent, anim));

            Vector3 bedPos = bed != null ? bed.transform.position : (playerTransform != null ? playerTransform.position : transform.position);

            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                agent.isStopped = false;
                agent.speed = runSpeed;
                agent.SetDestination(bedPos);
            }

            float timer = 0f;
            while (Vector3.Distance(transform.position, bedPos) > 1.8f && timer < 7.0f)
            {
                timer += Time.deltaTime;
                CheckAndOpenDoorsInPath();
                yield return null;
            }

            if (agent != null) agent.isStopped = true;

            Vector3 lookDir = (bedPos - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);

            PlayTerrifyScreamAudio();
            if (anim != null) anim.TriggerAttack();

            yield return new WaitForSeconds(0.4f);

            HideUnderBed hbed = FindFirstObjectByType<HideUnderBed>();
            if (hbed != null && hbed.isHiding)
            {
                hbed.ToggleHide(null);
            }

            var health = FindFirstObjectByType<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(9999f);
            }
        }

        #endregion

        public void ChangeState(IEnemyState newState)
        {
            currentState?.ExitState();
            currentState = newState;
            currentState?.EnterState();
        }

        private void FindPlayerReference()
        {
            if (playerTransform != null) return;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("PlayerCapsule");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj == null)
            {
                var fpc = FindFirstObjectByType<StarterAssets.FirstPersonController>();
                if (fpc != null) playerObj = fpc.gameObject;
            }

            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        private void SetupNavMeshAgent()
        {
            if (agent != null)
            {
                agent.speed = (currentState is AmalgamChaseState) ? runSpeed : walkSpeed;
                agent.stoppingDistance = 0.5f;
                agent.height = 1.85f; // Reducido para pasar fácilmente por marcos de puertas
                agent.radius = 0.35f; // Reducido para evitar atascarse en paredes estrechas
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.baseOffset = 0f; // Garantiza pivote a ras de suelo
            }
        }

        public void EnsureGrounded()
        {
            if (agent != null)
            {
                agent.baseOffset = 0f;
            }

            Animator unityAnim = GetComponent<Animator>();
            if (unityAnim == null) unityAnim = GetComponentInChildren<Animator>();
            if (unityAnim != null && unityAnim.applyRootMotion)
            {
                unityAnim.applyRootMotion = false;
            }

            // CORRECCIÓN ANTI-TECHO / ROOF: Si por algún desfase el monstruo queda flotando en el techo (Y > 2m respecto al jugador), forzar bajada al suelo del mapa.
            if (playerTransform != null && Mathf.Abs(transform.position.y - playerTransform.position.y) > 1.8f)
            {
                Vector3 groundCheckPos = transform.position;
                groundCheckPos.y = playerTransform.position.y;

                NavMeshHit groundHit;
                if (NavMesh.SamplePosition(groundCheckPos, out groundHit, 4.0f, NavMesh.AllAreas))
                {
                    if (agent != null && agent.enabled)
                    {
                        agent.Warp(groundHit.position);
                    }
                    else
                    {
                        transform.position = groundHit.position;
                    }
                    Debug.Log($"[The Amalgam] 🧱 Corregida posición flotante en el techo. Bajado al suelo: {groundHit.position}");
                }
            }

            if (modelYOffset != 0f)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child != null && child.localPosition.y != modelYOffset)
                    {
                        Vector3 lp = child.localPosition;
                        lp.y = modelYOffset;
                        child.localPosition = lp;
                    }
                }
            }
        }

        public bool IsPositionClearOfWalls(Vector3 pos)
        {
            Collider[] cols = Physics.OverlapSphere(pos + Vector3.up * 0.9f, 0.55f);
            foreach (var c in cols)
            {
                if (c == null || c.isTrigger) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
                if (playerTransform != null && (c.transform == playerTransform || c.transform.IsChildOf(playerTransform))) continue;

                string n = c.name.ToLower();
                if (n.Contains("wall") || n.Contains("pared") || n.Contains("door") || n.Contains("puerta") || c.gameObject.layer == LayerMask.NameToLayer("Default"))
                {
                    return false;
                }
            }
            return true;
        }

        private void SetupAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            int idx = 0;

            if (ambientAudioSource == null)
                ambientAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

            if (boneCrackAudioSource == null)
                boneCrackAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

            if (chaseAudioSource == null)
                chaseAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

            if (screamAudioSource == null)
                screamAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

            if (footstepAudioSource == null)
                footstepAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

            // Cargar clips por defecto desde Resources si no están asignados
            if (footstepSoundClip == null)
            {
                footstepSoundClip = Resources.Load<AudioClip>("Audio/Compartido/footsteps");
                if (footstepSoundClip == null) footstepSoundClip = Resources.Load<AudioClip>("footsteps");
            }

            if (doorKnockClip == null)
            {
                doorKnockClip = Resources.Load<AudioClip>("Audio/Compartido/tocar-la-puerta");
                if (doorKnockClip == null) doorKnockClip = Resources.Load<AudioClip>("tocar-la-puerta");
            }

            if (footstepAudioSource != null)
            {
                footstepAudioSource.spatialBlend = 0.35f; // Mezcla 2D/3D para resonar fuerte y presente en audífonos
                footstepAudioSource.minDistance = 6.0f;  // Volumen máximo constante hasta los 6 metros
                footstepAudioSource.maxDistance = 35.0f; // Audible en todos los pasillos
                footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }

            if (ambientAudioSource != null)
            {
                ambientAudioSource.spatialBlend = 0.65f;
                ambientAudioSource.minDistance = 3.0f;
                ambientAudioSource.maxDistance = 28.0f;
                ambientAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                ambientAudioSource.loop = true;
            }

            if (boneCrackAudioSource != null)
            {
                boneCrackAudioSource.spatialBlend = 0.75f;
                boneCrackAudioSource.minDistance = 2.5f;
                boneCrackAudioSource.maxDistance = 22.0f;
            }

            if (chaseAudioSource != null)
            {
                chaseAudioSource.spatialBlend = 0.5f;
                chaseAudioSource.minDistance = 3.5f;
                chaseAudioSource.maxDistance = 32.0f;
                chaseAudioSource.loop = true;
            }

            if (screamAudioSource != null)
            {
                screamAudioSource.spatialBlend = 0.2f;
                screamAudioSource.volume = 1.0f;
            }
        }

        #region Métodos de Pasos y Puertas

        private void HandleFootsteps()
        {
            if (footstepAudioSource == null || footstepSoundClip == null || agent == null) return;

            if (agent.enabled && agent.velocity.magnitude > 0.4f)
            {
                footstepTimer += Time.deltaTime;
                bool isRunning = (currentState is AmalgamChaseState);
                float stepInterval = isRunning ? 0.32f : 0.55f;

                if (footstepTimer >= stepInterval)
                {
                    footstepTimer = 0f;
                    footstepAudioSource.pitch = Random.Range(0.85f, 1.02f); // Tono ligeramente más grave e imponente
                    float stepVol = isRunning ? 1.0f : 0.85f;
                    footstepAudioSource.PlayOneShot(footstepSoundClip, stepVol);
                }
            }
            else
            {
                footstepTimer = 0f;
            }
        }

        private IEnumerator PsychologicalDoorKnockRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(10f, 18f));

                if (isKnockingDoor || playerTransform == null) continue;

                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

                if (distToPlayer <= 22f && (currentState is AmalgamIdleCryingState || currentState is AmalgamWarningState))
                {
                    Collider[] nearbyDoors = Physics.OverlapSphere(playerTransform.position, 12f);
                    List<GameObject> validDoors = new List<GameObject>();

                    foreach (var col in nearbyDoors)
                    {
                        if (col == null) continue;
                        ProceduralDoorInteract pDoor = col.GetComponentInParent<ProceduralDoorInteract>();
                        if (pDoor != null && !validDoors.Contains(pDoor.gameObject))
                        {
                            validDoors.Add(pDoor.gameObject);
                        }
                    }

                    if (validDoors.Count > 0)
                    {
                        GameObject chosenDoor = validDoors[Random.Range(0, validDoors.Count)];
                        PlayDoorKnockAtPosition(chosenDoor.transform.position);
                        Debug.Log($"[The Amalgam] 🚪 Golpeó la puerta cerca del jugador en '{chosenDoor.name}' (tocar-la-puerta).");
                    }
                }
            }
        }

        public void PlayDoorKnockAtPosition(Vector3 pos)
        {
            if (doorKnockClip == null)
            {
                doorKnockClip = Resources.Load<AudioClip>("Audio/Compartido/tocar-la-puerta");
                if (doorKnockClip == null) doorKnockClip = Resources.Load<AudioClip>("tocar-la-puerta");
            }

            if (doorKnockClip != null)
            {
                AudioSource.PlayClipAtPoint(doorKnockClip, pos, 1.0f);
            }
        }

        public void CheckAndOpenDoorsInPath()
        {
            if (isKnockingDoor) return;

            Collider[] nearbyCols = Physics.OverlapSphere(transform.position + Vector3.up * 1.0f, 3.0f);
            foreach (Collider col in nearbyCols)
            {
                if (col == null) continue;

                ProceduralDoorInteract procDoor = col.GetComponentInParent<ProceduralDoorInteract>();
                if (procDoor == null) procDoor = col.GetComponent<ProceduralDoorInteract>();

                OpenDoor animDoor = col.GetComponentInParent<OpenDoor>();
                if (animDoor == null) animDoor = col.GetComponent<OpenDoor>();

                if (procDoor != null && !procDoor.isOpen)
                {
                    if (procDoor.isLocked) continue;
                    StartCoroutine(KnockAndOpenDoorRoutine(procDoor, null));
                    break;
                }
                else if (animDoor != null)
                {
                    if (animDoor.isLocked) continue;
                    StartCoroutine(KnockAndOpenDoorRoutine(null, animDoor));
                    break;
                }
            }
        }

        private IEnumerator KnockAndOpenDoorRoutine(ProceduralDoorInteract procDoor, OpenDoor animDoor)
        {
            isKnockingDoor = true;

            PlayDoorKnockAtPosition(transform.position);

            yield return new WaitForSeconds(0.15f);

            if (procDoor != null)
            {
                procDoor.ToggleDoor();
                Debug.Log("[The Amalgam] Puerta derribada con impacto tocar-la-puerta durante la persecución.");
            }

            if (animDoor != null && animDoor.doorAnimator != null && !animDoor.doorAnimator.GetBool("isOpen"))
            {
                animDoor.doorAnimator.SetBool("isOpen", true);
            }

            yield return new WaitForSeconds(0.35f);
            isKnockingDoor = false;
        }

        #endregion

        #region Métodos de Control de Audio

        public void PlayCryingAudio()
        {
            if (ambientAudioSource == null || hombreLlorandoClip == null) return;

            if (ambientAudioSource.clip != hombreLlorandoClip || !ambientAudioSource.isPlaying)
            {
                ambientAudioSource.clip = hombreLlorandoClip;
                ambientAudioSource.volume = 0.85f;
                ambientAudioSource.Play();
            }
        }

        public void StopCryingAudio()
        {
            if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Stop();
            }
        }

        public void PlayBoneCrackAudio()
        {
            if (boneCrackAudioSource == null || crujidoHuesosClips == null || crujidoHuesosClips.Length == 0) return;

            AudioClip chosenClip = crujidoHuesosClips[Random.Range(0, crujidoHuesosClips.Length)];
            if (chosenClip != null)
            {
                boneCrackAudioSource.pitch = Random.Range(0.9f, 1.1f);
                boneCrackAudioSource.PlayOneShot(chosenClip, Random.Range(0.85f, 1.0f));
                Debug.Log("[The Amalgam] 🦴 CRACK... Crujido de huesos reproducido.");
            }
        }

        public void PlayChaseAudio()
        {
            StopCryingAudio();

            if (chaseAudioSource == null || chaseAmalgamClip == null) return;

            if (chaseAudioSource.clip != chaseAmalgamClip || !chaseAudioSource.isPlaying)
            {
                chaseAudioSource.clip = chaseAmalgamClip;
                chaseAudioSource.volume = 1.0f;
                chaseAudioSource.Play();
                Debug.Log("[The Amalgam] 🏃 Inició audio de persecución Chase_Amalgam.");
            }
        }

        public void StopChaseAudio()
        {
            if (chaseAudioSource != null && chaseAudioSource.isPlaying)
            {
                chaseAudioSource.Stop();
            }
        }

        public void PlayTerrifyScreamAudio()
        {
            StopChaseAudio();
            StopCryingAudio();

            if (terrifyScreamClip != null)
            {
                if (screamAudioSource != null)
                {
                    screamAudioSource.PlayOneShot(terrifyScreamClip, 1.0f);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(terrifyScreamClip, transform.position, 1.0f);
                }
                Debug.Log("[The Amalgam] ☠️ TerrifyScreamAmalgam ejecutado en el ataque final.");
            }
        }

        #endregion

        #region Eventos y Escalado de Intensidad

        /// <summary>
        /// Corrutina de reposicionamiento continuo en los pasillos frente a la marcha del jugador.
        /// </summary>
        private IEnumerator SilentRepositionRoutine()
        {
            while (true)
            {
                float baseInterval = silentRepositionInterval;
                float waitTime = Random.Range(baseInterval * 0.8f, baseInterval * 1.3f) / Mathf.Max(0.7f, intensityLevel * 0.65f);
                yield return new WaitForSeconds(waitTime);

                if (currentState is AmalgamIdleCryingState || currentState is AmalgamWarningState)
                {
                    if (playerTransform != null)
                    {
                        // ¿Generar espejismo/ilusión psicológica en el pasillo alternativo según dificultad?
                        if (intensityLevel >= 1.6f && Random.value < illusionSpawnChance)
                        {
                            SpawnCorridorIllusion();
                        }

                        TrySilentRelocateClose(9f, 15f);
                        PlayBoneCrackAudio();
                    }
                }
            }
        }

        public void SpawnCorridorIllusion()
        {
            if (playerTransform == null) return;

            // Limpiar ilusiones nulas o destruidas
            activeIllusions.RemoveAll(item => item == null);

            // MÁXIMO 2 ILUSIONES ACTIVAS SIMULTÁNEAMENTE (evita acumulación de clones)
            if (activeIllusions.Count >= 2) return;

            float sideAngle = Random.value < 0.5f ? 55f : -55f;
            Vector3 illusionDir = Quaternion.Euler(0, sideAngle, 0) * playerTransform.forward;
            illusionDir.y = 0f;

            Vector3 targetPos = playerTransform.position + illusionDir.normalized * Random.Range(10f, 16f);
            targetPos.y = playerTransform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 6f, NavMesh.AllAreas))
            {
                if (Mathf.Abs(hit.position.y - playerTransform.position.y) <= 1.8f && IsPositionClearOfWalls(hit.position))
                {
                    GameObject illusionObj = Instantiate(gameObject, hit.position, Quaternion.identity);

                    // 1. ELIMINAR TODOS LOS AUDIOSOURCES DEL CLON (SILENCIO 100% ABSOLUTO)
                    AudioSource[] cloneAudios = illusionObj.GetComponentsInChildren<AudioSource>();
                    foreach (var a in cloneAudios)
                    {
                        if (a != null) DestroyImmediate(a);
                    }

                    // 2. ELIMINAR CONTROLADOR IA PRIMERO Y LUEGO NAVEGACIÓN (evita error de dependencia)
                    AmalgamAIController cloneCtrl = illusionObj.GetComponent<AmalgamAIController>();
                    if (cloneCtrl != null) DestroyImmediate(cloneCtrl);

                    NavMeshAgent cloneAgent = illusionObj.GetComponent<NavMeshAgent>();
                    if (cloneAgent != null) DestroyImmediate(cloneAgent);

                    // 3. ASIGNAR COMPONENTE DE ILUSIÓN PURAMENTE VISUAL
                    AmalgamIllusion illusionComp = illusionObj.AddComponent<AmalgamIllusion>();
                    illusionComp.Initialize(this, playerTransform);
                    activeIllusions.Add(illusionComp);

                    Debug.Log($"[The Amalgam] 👻 Ilusión puramente visual creada en silencio: {hit.position}");
                }
            }
        }

        public void OnPlayerApproachedIllusion(Vector3 illusionPos)
        {
            Debug.Log("[The Amalgam] 😱 El jugador se acercó al espejismo. El monstruo REAL se materializa en su lugar.");

            ClearAllIllusions();

            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                agent.Warp(illusionPos);
                agent.isStopped = false;
                EnsureGrounded();
            }

            PlayBoneCrackAudio();
            ChangeState(new AmalgamChaseState(this, agent, anim));
        }

        public void ClearAllIllusions()
        {
            foreach (var ill in activeIllusions)
            {
                if (ill != null) ill.DestroyIllusion();
            }
            activeIllusions.Clear();
        }

        /// <summary>
        /// Incrementa la intensidad cuando el jugador recoge una nota de lore.
        /// </summary>
        public void NotifyNoteCollected()
        {
            intensityLevel += 0.75f;
            runSpeed = Mathf.Min(7.2f + (intensityLevel * 0.55f), 10.2f);
            chaseDistance = Mathf.Min(8.0f + (intensityLevel * 1.8f), 22.0f);
            Debug.Log($"[The Amalgam] 📜 Nota recogida. Intensidad aumentada a: {intensityLevel:F2} | Vel. Carrera: {runSpeed:F1}m/s");

            PlayBoneCrackAudio();
            if (Random.value < 0.60f)
            {
                TrySilentRelocateClose(8f, 13f);
            }
        }

        /// <summary>
        /// Incrementa drásticamente la intensidad cuando el jugador enciende un subgenerador.
        /// </summary>
        public void NotifyGeneratorActivated(Vector3 genPos)
        {
            intensityLevel += 1.30f;
            runSpeed = Mathf.Min(7.5f + (intensityLevel * 0.60f), 10.8f);
            chaseDistance = Mathf.Min(8.0f + (intensityLevel * 1.8f), 22.0f);
            Debug.Log($"[The Amalgam] ⚡ Generador activado. Intensidad escalada a: {intensityLevel:F2} | Vel. Carrera: {runSpeed:F1}m/s");

            PlayBoneCrackAudio();
            TriggerBlackoutEvent();
        }

        /// <summary>
        /// Activa el fenómeno paranormal durante un apagón.
        /// Incrementa la intensidad y velocidad del monstruo.
        /// Reproduce los lamentos con un crescendo de volumen aterrador y teletransporta a The Amalgam A QUEMARROPA (6m-9m).
        /// </summary>
        public void TriggerBlackoutEvent()
        {
            intensityLevel += 1.50f;
            runSpeed = Mathf.Min(7.8f + (intensityLevel * 0.65f), 10.8f);
            chaseDistance = Mathf.Min(8.0f + (intensityLevel * 2.0f), 24.0f);

            Debug.Log($"[The Amalgam] 💡 APAGÓN Y CRESCENDO TERRORÍFICO. Intensidad: {intensityLevel:F1} | Vel: {runSpeed:F1}m/s");

            StartCoroutine(TerrorCrescendoRoutine());
        }

        private IEnumerator TerrorCrescendoRoutine()
        {
            AudioClip clipToPlay = lamentosNinosClip != null ? lamentosNinosClip : hombreLlorandoClip;
            if (clipToPlay == null)
            {
                clipToPlay = Resources.Load<AudioClip>("Audio/Monstruos/The_Amalgam/LamentosNiñosFantasmas");
                if (clipToPlay == null) clipToPlay = Resources.Load<AudioClip>("Audio/Hospital/LamentosNiñosFantasmas");
                if (clipToPlay == null) clipToPlay = Resources.Load<AudioClip>("LamentosNiñosFantasmas");
                if (clipToPlay == null) clipToPlay = Resources.Load<AudioClip>("Audio/Monstruos/The_Amalgam/HOMBRELLORANDO");
                if (clipToPlay == null) clipToPlay = Resources.Load<AudioClip>("HOMBRELLORANDO");
            }

            Debug.Log($"[The Amalgam] 😱 CRESCENDO TERRORÍFICO INICIADO con clip: '{(clipToPlay != null ? clipToPlay.name : "null")}'");

            if (clipToPlay != null && screamAudioSource != null)
            {
                screamAudioSource.spatialBlend = 0.08f;
                screamAudioSource.clip = clipToPlay;
                screamAudioSource.loop = false;
                screamAudioSource.volume = 0.40f;
                screamAudioSource.pitch = 0.95f;
                screamAudioSource.Play();

                float elapsed = 0f;
                while (elapsed < 1.6f)
                {
                    elapsed += Time.deltaTime;
                    screamAudioSource.volume = Mathf.Lerp(0.40f, 1.0f, elapsed / 1.6f);
                    screamAudioSource.pitch = Random.Range(0.92f, 1.08f);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(1.2f);
            }

            PlayBoneCrackAudio();

            HideUnderBed hBed = FindFirstObjectByType<HideUnderBed>();
            bool isPlayerHiding = hBed != null && hBed.isHiding;
            chaseCooldownTimer = 15.0f; // Asignar tiempo de respiro durante el apagón para evitar carreras inmediatas

            if (isPlayerHiding)
            {
                Debug.Log("[The Amalgam] 💡 Apagón activado mientras el jugador está escondido. Reposicionamiento lejanía en estado Idle.");
                TrySilentRelocate();
                ChangeState(new AmalgamIdleCryingState(this, agent, anim));
            }
            else
            {
                bool hasLOS = HasLineOfSightToPlayer();

                if (hasLOS)
                {
                    Debug.Log("[The Amalgam] ⚡ Apagón en visión directa: ¡Emboscada y carrera inmediata!");
                    TrySilentRelocateClose(6f, 9f);
                    ChangeState(new AmalgamChaseState(this, agent, anim));
                }
                else
                {
                    Debug.Log("[The Amalgam] 💡 Apagón sin visión directa (jugador resguardado AFK en habitación). Acecho exterior en pasillo en estado de advertencia.");
                    TrySilentRelocateClose(10f, 16f);
                    ChangeState(new AmalgamWarningState(this, agent, anim));
                }
            }
        }

        public void TrySilentRelocateClose(float minDist, float maxDist)
        {
            if (agent == null || playerTransform == null) return;

            // Mantener distancia mínima de 11.5m para NUNCA teletransportarse dentro de la habitación del jugador
            float scaledMin = Mathf.Max(11.5f, minDist);
            float scaledMax = Mathf.Max(17.5f, maxDist);

            for (int attempts = 0; attempts < 12; attempts++)
            {
                Vector3 randomDir = Random.insideUnitSphere;
                randomDir.y = 0f;
                if (randomDir == Vector3.zero) randomDir = -playerTransform.forward;

                Vector3 targetPos = playerTransform.position + randomDir.normalized * Random.Range(scaledMin, scaledMax);
                targetPos.y = playerTransform.position.y;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, 8f, NavMesh.AllAreas))
                {
                    Vector3 finalPos = hit.position;

                    // COMPROBACIÓN ANTI-TECHO: Descartar si la posición cae en el tejado o cubierta (Y desfasado > 1.8m)
                    if (Mathf.Abs(finalPos.y - playerTransform.position.y) > 1.8f)
                    {
                        continue;
                    }

                    // COMPROBACIÓN ANTI-HABITACIÓN:
                    // Si un Raycast desde la posición destino hacia el jugador NO choca con ningún muro o puerta,
                    // significa que la posición elegida está DENTRO de la misma habitación. ¡Rechazar e intentar otro ángulo en el pasillo!
                    Vector3 dirToPlayer = (playerTransform.position + Vector3.up * 1.0f) - (finalPos + Vector3.up * 1.0f);
                    float distToPlayer = dirToPlayer.magnitude;

                    if (Physics.Raycast(finalPos + Vector3.up * 1.0f, dirToPlayer.normalized, out RaycastHit wallHit, distToPlayer, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.transform == playerTransform || wallHit.transform.IsChildOf(playerTransform))
                        {
                            // Está dentro de la misma habitación sin muros -> RECHAZAR Y REINTENTAR
                            continue;
                        }
                    }

                    if (!IsPositionClearOfWalls(finalPos))
                    {
                        finalPos += (playerTransform.position - finalPos).normalized * 0.65f;
                    }

                    if (!agent.enabled) agent.enabled = true;
                    agent.Warp(finalPos);
                    agent.isStopped = false;
                    EnsureGrounded();
                    Debug.Log($"[The Amalgam] 🚪 Reposicionado de forma segura afuera en el pasillo: {finalPos}");
                    return;
                }
            }

            // Fallback seguro en spawn point distante si todos los intentos fallaron
            TrySilentRelocate();
        }

        public void TrySilentRelocate()
        {
            if (agent == null) return;

            ClearAllIllusions();

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                List<Transform> validPoints = new List<Transform>();
                foreach (var sp in spawnPoints)
                {
                    if (sp == null) continue;
                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(sp.position, playerTransform.position);
                        if (dist >= 10f) validPoints.Add(sp);
                    }
                    else
                    {
                        validPoints.Add(sp);
                    }
                }

                if (validPoints.Count > 0)
                {
                    Transform chosenPoint = validPoints[Random.Range(0, validPoints.Count)];
                    agent.Warp(chosenPoint.position);
                    EnsureGrounded();
                    Debug.Log($"[The Amalgam] Reposicionado en SpawnPoint: {chosenPoint.name}");
                    return;
                }
            }

            if (playerTransform != null)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = 0f;
                Vector3 targetPosition = playerTransform.position + randomDirection.normalized * Random.Range(12f, 18f);
                targetPosition.y = playerTransform.position.y;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPosition, out hit, 6f, NavMesh.AllAreas))
                {
                    Vector3 finalPos = hit.position;
                    if (!IsPositionClearOfWalls(finalPos))
                    {
                        finalPos += (playerTransform.position - finalPos).normalized * 0.65f;
                    }

                    if (Mathf.Abs(finalPos.y - playerTransform.position.y) <= 1.8f)
                    {
                        agent.Warp(finalPos);
                        EnsureGrounded();
                        Debug.Log($"[The Amalgam] Reposicionado silenciosamente en NavMesh: {finalPos}");
                    }
                }
            }
            EnsureGrounded();
        }


        public void TriggerRespawnGracePeriod(float duration = 28.0f)
        {
            chaseCooldownTimer = duration;
            StopChaseAudio();
            StopCryingAudio();
            if (screamAudioSource != null) screamAudioSource.Stop();

            if (anim != null)
            {
                anim.ResetAllStateBools();
                anim.SetRunning(false);
                anim.SetCrying(true);
            }

            Vector3 refPos = playerTransform != null ? playerTransform.position : transform.position;
            ForceRelocateFarAway(refPos);
            ChangeState(new AmalgamIdleCryingState(this, agent, anim));
            Debug.Log($"[The Amalgam] 🕊️ Período de calma tras reaparición activado por {duration} segundos.");
        }

        public void ForceRelocateFarAway(Vector3 playerPos)
        {
            ClearAllIllusions();
            if (agent == null) return;

            for (int attempts = 0; attempts < 35; attempts++)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = 0f;
                Vector3 targetPosition = playerPos + randomDirection.normalized * Random.Range(28f, 48f);
                targetPosition.y = playerPos.y;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPosition, out hit, 10f, NavMesh.AllAreas))
                {
                    if (Mathf.Abs(hit.position.y - playerPos.y) <= 1.8f)
                    {
                        if (!agent.enabled) agent.enabled = true;
                        agent.Warp(hit.position);
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        EnsureGrounded();
                        Debug.Log($"[The Amalgam] 🕊️ Relocalizado con éxito lejos del jugador en {hit.position}");
                        return;
                    }
                }
            }

            // Fallback a SpawnPoints distantes si los intentos aleatorios fallaron
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform furthest = null;
                float bestDist = 0f;
                foreach (var sp in spawnPoints)
                {
                    if (sp == null) continue;
                    float d = Vector3.Distance(sp.position, playerPos);
                    if (d > bestDist)
                    {
                        bestDist = d;
                        furthest = sp;
                    }
                }

                if (furthest != null)
                {
                    if (!agent.enabled) agent.enabled = true;
                    agent.Warp(furthest.position);
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    EnsureGrounded();
                    Debug.Log($"[The Amalgam] 🕊️ Relocalizado en SpawnPoint distante '{furthest.name}'");
                    return;
                }
            }

            EnsureGrounded();
        }

        #endregion

        #region Evento de Luz / Parpadeo Terrorífico

        private Coroutine lightFlickerCoroutine;

        public void TriggerHorrorLightFlickerSequence()
        {
            if (lightFlickerCoroutine != null) StopCoroutine(lightFlickerCoroutine);
            lightFlickerCoroutine = StartCoroutine(HorrorLightFlickerRoutine());
        }

        private IEnumerator HorrorLightFlickerRoutine()
        {
            Vector3 center = playerTransform != null ? playerTransform.position : transform.position;

            Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<Light> nearbyLights = new List<Light>();

            foreach (var l in allLights)
            {
                if (l != null && l.type != LightType.Directional)
                {
                    string n = l.name.ToLower();
                    if (!n.Contains("flashlight") && !n.Contains("linterna") && !n.Contains("player"))
                    {
                        if (Vector3.Distance(l.transform.position, center) <= 35f)
                        {
                            nearbyLights.Add(l);
                        }
                    }
                }
            }

            AudioClip sparkClip = Resources.Load<AudioClip>("Audio/Hospital/ErrorLightSound");
            if (sparkClip == null) sparkClip = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
            if (sparkClip == null) sparkClip = Resources.Load<AudioClip>("Interruptor");

            if (sparkClip != null && boneCrackAudioSource != null)
            {
                boneCrackAudioSource.PlayOneShot(sparkClip, 0.95f);
            }

            Dictionary<Light, float> origIntensities = new Dictionary<Light, float>();
            foreach (var l in nearbyLights)
            {
                if (l != null && !origIntensities.ContainsKey(l))
                    origIntensities[l] = l.intensity > 0 ? l.intensity : 1.5f;
            }

            Debug.Log($"[The Amalgam] ⚡ Iniciando secuencia terrorífica de luz en {nearbyLights.Count} bombillas cercanas...");

            float elapsed = 0f;
            while (elapsed < 0.7f)
            {
                elapsed += Time.deltaTime;
                foreach (var l in nearbyLights)
                {
                    if (l == null) continue;
                    l.enabled = Random.value > 0.45f;
                    l.intensity = Random.value < 0.4f ? 0f : origIntensities[l] * Random.Range(0.2f, 1.5f);
                }
                yield return new WaitForSeconds(0.04f);
            }

            Color origAmbientLight = RenderSettings.ambientLight;
            float origAmbientIntensity = RenderSettings.ambientIntensity;
            UnityEngine.Rendering.AmbientMode origAmbientMode = RenderSettings.ambientMode;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;

            foreach (var l in nearbyLights)
            {
                if (l != null)
                {
                    l.enabled = false;
                    l.intensity = 0f;
                }
            }

            yield return new WaitForSeconds(1.2f);

            RenderSettings.ambientMode = origAmbientMode;
            RenderSettings.ambientLight = origAmbientLight;
            RenderSettings.ambientIntensity = origAmbientIntensity;

            float chaseFlickerTimer = 0f;
            while (chaseFlickerTimer < 10.0f && currentState is AmalgamChaseState)
            {
                chaseFlickerTimer += Time.deltaTime;
                foreach (var l in nearbyLights)
                {
                    if (l == null) continue;
                    l.enabled = Random.value > 0.25f;
                    l.intensity = origIntensities[l] * Random.Range(0.25f, 1.2f);
                }
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }

            RenderSettings.ambientMode = origAmbientMode;
            RenderSettings.ambientLight = origAmbientLight;
            RenderSettings.ambientIntensity = origAmbientIntensity;

            foreach (var kvp in origIntensities)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = true;
                    kvp.Key.intensity = kvp.Value;
                }
            }
        }

        #endregion
    }
}
