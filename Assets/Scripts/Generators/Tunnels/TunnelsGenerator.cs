using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using StarterAssets;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class TunnelsGenerator : MonoBehaviour
{
	public enum EscapeState
	{
		Idle,
		Draining,
		Ready
	}

	[Header("Assets Modulares del Usuario")]
	[Tooltip("El prefab del piso de lámina/plataforma (catwalk)")]
	public GameObject floorCatwalkPrefab;

	[Tooltip("El prefab del soporte de tuberías (arcos de tubos)")]
	public GameObject pipeArchPrefab;

	[Header("Textura de Paredes y Techo")]
	[Tooltip("Textura de concreto húmedo para las paredes y techo")]
	public Texture2D wallConcreteTexture;

	[Tooltip("Textura para los arcos inclinados del techo (ej. metal oxidado)")]
	public Texture2D archTexture;

	[Tooltip("Material base de referencia URP (ej. arrastra cualquier material del Hospital para usar su shader Lit y compatibilidad de sombras)")]
	public Material baseMaterial;

	[Tooltip("Mapa de relieve (Normal Map) de las paredes")]
	public Texture2D wallNormalTexture;

	[Tooltip("Mapa de relieve (Normal Map) de los arcos")]
	public Texture2D archNormalTexture;

	[Header("Texturas del Suelo (Plataforma Catwalk)")]
	[Tooltip("Textura base/color (Albedo) de la pasarela")]
	public Texture2D floorAlbedoTexture;

	[Tooltip("Mapa de relieve (Normal Map) de la pasarela")]
	public Texture2D floorNormalTexture;

	[Header("Texturas de los Tubos (Arco)")]
	[Tooltip("Textura base/color (Albedo) de las tuberías")]
	public Texture2D pipeAlbedoTexture;

	[Tooltip("Mapa de relieve (Normal Map) de las tuberías")]
	public Texture2D pipeNormalTexture;

	[Header("Ajustes de Materiales (Brillo y Reflejo)")]
	[Range(0f, 1f)]
	public float wallSmoothness = 0.3f;

	[Range(0f, 1f)]
	public float wallMetallic = 0.05f;

	[Space(5f)]
	[Range(0f, 1f)]
	public float archSmoothness = 0.3f;

	[Range(0f, 1f)]
	public float archMetallic = 0.1f;

	[Space(5f)]
	[Range(0f, 1f)]
	public float floorSmoothness = 0.35f;

	[Range(0f, 1f)]
	public float floorMetallic;

	[Header("Tuberías de Decoración de Pared (Modular)")]
	[Tooltip("Modelo o Prefab de la tubería modular (Meshy_AI_Modular_industrial_tu_0712001447_texture)")]
	public GameObject wallPipePrefab;

	[Range(0f, 1f)]
	[Tooltip("Probabilidad de que aparezca una tubería en una pared")]
	public float wallPipeSpawnProbability = 0.5f;

	[Tooltip("Distancia desde la pared hacia el centro del pasillo (multiplicada por mapScale)")]
	public float wallPipeOffset = 0.15f;

	[Tooltip("Altura del suelo a la que se colocará la tubería (multiplicada por mapScale)")]
	public float wallPipeHeight = 1.6f;

	[Tooltip("Escala local de la tubería")]
	public Vector3 wallPipeScale = new Vector3(1f, 1f, 1f);

	[Tooltip("Rotación local adicional para alinear la tubería a la pared")]
	public Vector3 wallPipeRotation = Vector3.zero;

	[Tooltip("Textura base (Albedo) de la tubería procedimental de pared")]
	public Texture2D wallPipeAlbedo;

	[Tooltip("Mapa de normales (Normal Map) de la tubería procedimental de pared")]
	public Texture2D wallPipeNormal;

	[Tooltip("Textura de forma del charco (ej. UI_Circle_Faded)")]
	public Texture2D puddleShapeTexture;

	private Material wallPipeMaterial;

	private Material waterPuddleMaterial;

	[Header("Ajustes de Iluminación de las Lámparas")]
	[Tooltip("Color de la luz de las lámparas (Blanco frío para tipo fluorescente)")]
	public Color lightColor = new Color(0.9f, 0.95f, 1f);

	[Tooltip("Rango de la luz (multiplicado por mapScale)")]
	public float lightRange = 8f;

	[Tooltip("Intensidad de la luz")]
	public float lightIntensity = 6f;

	[Tooltip("Desplazamiento vertical de la lámpara desde el techo (multiplicado por mapScale)")]
	public float lightVerticalOffset;

	[Header("Prefabs de Entidades")]
	public GameObject playerPrefab;

	public GameObject enemyPrefab;

	[Range(0.05f, 1f)]
	[Tooltip("Porcentaje de distancia en el laberinto para spawnear el enemigo (0.05 = muy cerca del jugador, 1.0 = extremo opuesto)")]
	public float enemySpawnDistancePercent = 0.35f;

	public GameObject ceilingLightPrefab;

	[Header("Escala del Mapa (Para ajustar a tu Jugador Gigante)")]
	[Tooltip("Aplica un multiplicador a todo el mapa para que coincida con el tamaño del jugador (Normalmente 3.0 si el jugador mide 3x3x3)")]
	public float mapScale = 3f;

	[Header("Ajustes de Escala de Prefabs")]
	[Tooltip("Multiplicador para ensanchar la pasarela metálica")]
	public float catwalkWidthMultiplier = 2f;

	[Header("Dimensiones Base de cada Celda (Antes de multiplicar por la escala)")]
	[Tooltip("Longitud/Ancho de cada celda del laberinto")]
	public float segmentLength = 10f;

	[Tooltip("Ancho interno del túnel (distancia desde el centro al muro). Ajustado a 1.0f para pasillos de 6m y barandillas pegadas.")]
	public float wallOffset = 1f;

	[Tooltip("Altura del túnel")]
	public float wallHeight = 4f;

	[Tooltip("Espesor de las paredes")]
	public float wallThickness = 0.4f;

	[Header("Ajustes de Iluminación (Valores Base)")]
	[Tooltip("Distancia entre focos de luz cenital")]
	public float lightSpacing = 20f;

	[Range(0f, 1f)]
	[Tooltip("Probabilidad de luz encendida (Zona Segura)")]
	public float safeLightProbability = 0.5f;

	[Tooltip("Máxima distancia consecutiva a oscuras permitida")]
	public float maxDarkSpacing = 40f;

	public int width = 25;

	public int height = 25;

	public bool[,] grid;

	private NavMeshSurface navMeshSurface;

	private List<Vector3> patrolPoints = new List<Vector3>();

	private List<Light> spawnedLights = new List<Light>();

	private GameObject navMeshHolder;

	private Material wallMaterial;

	private Material floorMaterial;

	private Material archMaterial;

	private Material pipeMaterial;

	private bool isPaused;

	private MonoBehaviour fpsController;

	private GameObject playerObjInstance;

	private Texture2D pauseBgTex;

	private Vector3 exitPointPos;

	public static Vector3 worldExitPointPos;

	private bool exitReached;

	public static EscapeState escapeState;

	public float drainageDuration = 45f;

	private float currentDrainageTime;

	private Vector3 consolePos;

	private Vector3 playerSpawnPos;

	private float interactionTimer;

	private Light consoleIndicatorLight;

	private Material consoleLightMaterial;

	private AudioSource pumpAudioSource;

	private Renderer hatchRenderer;

	private GameObject leverArmObj;

	private Texture2D alarmBgTex;

	private Texture2D alarmBorderTex;

	private Texture2D alarmProgressTex;

	private Texture2D progressRemainingTex;

	private Texture2D fadeBlackTex;

	private static TunnelsGenerator instance;

	private float victoryFadeAlpha = 1.0f;

	public float VictoryFadeAlpha
	{
		get { return victoryFadeAlpha; }
		set { victoryFadeAlpha = value; }
	}

	private int victoryStep = 0; // 0 = Inactivo, 1 = Juego Terminado, 2 = Gracias por Jugar, 3 = Redes Sociales
	private float victoryStepAlpha = 0f;

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			UnityEngine.Debug.LogWarning("[TunnelsGenerator] Se detectó una instancia duplicada del generador. Destruyendo de inmediato.");
			Object.DestroyImmediate(base.gameObject);
			return;
		}
		instance = this;
	}

	private void Start()
	{
		if (instance != this) return;

		// Auto-añadir el PauseMenuManager si no existe
		PauseMenuManager pauseManager = gameObject.GetComponent<PauseMenuManager>();
		if (pauseManager == null)
		{
			gameObject.AddComponent<PauseMenuManager>();
			UnityEngine.Debug.Log("[TunnelsGenerator] Componente PauseMenuManager auto-añadido con éxito.");
		}

		// Forzar orientación horizontal (Landscape) en móviles y permitir rotación cómoda de 180 grados
		Screen.orientation = ScreenOrientation.AutoRotation;
		Screen.autorotateToPortrait = false;
		Screen.autorotateToPortraitUpsideDown = false;
		Screen.autorotateToLandscapeLeft = true;
		Screen.autorotateToLandscapeRight = true;

		// Esperar a que la escena esté activa antes de generar el mapa y las entidades para sincronizar la cinemática
		StartCoroutine(WaitUntilActiveAndGenerate());
	}

	private System.Collections.IEnumerator WaitUntilActiveAndGenerate()
	{
		while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != gameObject.scene.name)
		{
			yield return null;
		}

		// Asegurar transparencia en negro sólido para el fundido correcto al iniciar
		victoryFadeAlpha = 1.0f;

		string text = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
		if (text == "FACIL")
		{
			drainageDuration = 30f;
		}
		else if (text == "DIFICIL")
		{
			drainageDuration = 60f;
		}
		else
		{
			drainageDuration = 45f;
		}

		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		DisableDirectionalLight();
		DisableExternalCameras();
		navMeshHolder = new GameObject("NavMeshSurfaceHolder");
		navMeshHolder.transform.SetParent(base.transform);
		navMeshHolder.transform.localPosition = Vector3.zero;
		navMeshHolder.transform.localRotation = Quaternion.identity;
		navMeshSurface = navMeshHolder.AddComponent<NavMeshSurface>();
		if (!MainMenuManager.startedFromMenu)
		{
			PlayerPrefs.SetInt("SelectedMapSize", 25);
		}
		height = (width = PlayerPrefs.GetInt("SelectedMapSize", 25));
		UnityEngine.Debug.Log(string.Format("[TunnelsGenerator] Generando laberinto {0}x{1}. Catwalk: {2}. Pipes: {3}", width, height, (floorCatwalkPrefab != null) ? "SI" : "NO", (pipeArchPrefab != null) ? "SI" : "NO"));
		CreateRuntimeMaterials();
		stopwatch.Stop();
		UnityEngine.Debug.Log($"[Performance] Paso 1: Inicialización y materiales: {stopwatch.ElapsedMilliseconds} ms");
		stopwatch.Restart();
		GenerateMazeTunnelsMap();
		stopwatch.Stop();
		UnityEngine.Debug.Log($"[Performance] Paso 2: Generación física del laberinto: {stopwatch.ElapsedMilliseconds} ms");
		stopwatch.Restart();
		if (navMeshSurface != null)
		{
			navMeshSurface.collectObjects = CollectObjects.Children;
			navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			navMeshSurface.overrideVoxelSize = true;
			navMeshSurface.voxelSize = 0.5f;
			navMeshSurface.overrideTileSize = true;
			navMeshSurface.tileSize = 256;
			navMeshSurface.BuildNavMesh();
		}
		stopwatch.Stop();
		UnityEngine.Debug.Log($"[Performance] Paso 3: Horneado del NavMesh: {stopwatch.ElapsedMilliseconds} ms");
		stopwatch.Restart();
		SpawnEntities();
		AudioClip audioClip = Resources.Load<AudioClip>("Audio/Tuneles/AmbienteTunel");
		if (audioClip == null) audioClip = Resources.Load<AudioClip>("AmbienteTunel");
		if (audioClip != null)
		{
			AudioSource audioSource = base.gameObject.AddComponent<AudioSource>();
			audioSource.clip = audioClip;
			audioSource.loop = true;
			audioSource.spatialBlend = 0f;
			audioSource.volume = 0.45f;
			audioSource.playOnAwake = true;
			audioSource.Play();
			UnityEngine.Debug.Log("[TunnelsGenerator] Loop de música ambiental 'AmbienteTunel' iniciado.");
		}
		else
		{
			UnityEngine.Debug.LogWarning("[TunnelsGenerator] No se encontró el sonido de ambiente 'AmbienteTunel' en Resources.");
		}
		base.gameObject.AddComponent<TunnelsPowerOutageManager>();
		stopwatch.Stop();
		UnityEngine.Debug.Log($"[Performance] Paso 4: Spawn y configuración de entidades: {stopwatch.ElapsedMilliseconds} ms");
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			if (FindAnyObjectByType<PauseMenuManager>() == null)
			{
				if (isPaused)
				{
					ResumeGame();
				}
				else
				{
					PauseGame();
				}
			}
		}
		if (leverArmObj != null)
		{
			Quaternion b = ((escapeState == EscapeState.Idle) ? Quaternion.Euler(-45f, 0f, 0f) : Quaternion.Euler(45f, 0f, 0f));
			leverArmObj.transform.localRotation = Quaternion.Slerp(leverArmObj.transform.localRotation, b, Time.deltaTime * 6f);
		}
		if (escapeState == EscapeState.Idle)
		{
			Vector3 worldConsolePos = consolePos;
			GameObject pObj = (playerObjInstance != null) ? playerObjInstance : GameObject.FindGameObjectWithTag("Player");
			float dist = 9999f;
			if (pObj != null)
			{
				Vector3 pPos = pObj.transform.position;
				dist = Vector3.Distance(new Vector3(pPos.x, 0f, pPos.z), new Vector3(worldConsolePos.x, 0f, worldConsolePos.z));
			}

			if (dist < 4.2f)
			{
				bool isHolding = MobileInput.GetKey(KeyCode.E) || Input.GetKey(KeyCode.E) || MobileInput.ePressed;
				if (isHolding)
				{
					interactionTimer = Mathf.MoveTowards(interactionTimer, 2f, Time.deltaTime);
				}
				else
				{
					interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
				}
				if (interactionTimer >= 2f)
				{
					escapeState = EscapeState.Draining;
					currentDrainageTime = drainageDuration;
					interactionTimer = 0f;
					if (consoleIndicatorLight != null)
					{
						consoleIndicatorLight.color = Color.red;
					}
					AudioClip audioClip = Resources.Load<AudioClip>("Audio/Tuneles/Apagon_Sonido");
					if (audioClip == null) audioClip = Resources.Load<AudioClip>("Apagon_Sonido");
					if (audioClip != null)
					{
						AudioSource.PlayClipAtPoint(audioClip, worldConsolePos, 1f);
					}
					AudioClip audioClip2 = Resources.Load<AudioClip>("Audio/Tuneles/FloodSiren");
					if (audioClip2 == null) audioClip2 = Resources.Load<AudioClip>("FloodSiren");
					if (pumpAudioSource != null && audioClip2 != null)
					{
						pumpAudioSource.clip = audioClip2;
						pumpAudioSource.Play();
					}
					UnityEngine.Debug.Log("[TunnelsGenerator] Bomba de drenaje iniciada. Alarma sonando y caza infinita activada.");
				}
			}
			else
			{
				interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
			}
		}
		else if (escapeState == EscapeState.Draining)
		{
			currentDrainageTime -= Time.deltaTime;
			bool flag = Time.time % 0.6f < 0.3f;
			if (consoleIndicatorLight != null)
			{
				consoleIndicatorLight.enabled = flag;
			}
			if (consoleLightMaterial != null)
			{
				if (flag)
				{
					consoleLightMaterial.EnableKeyword("_EMISSION");
					consoleLightMaterial.SetColor("_EmissionColor", Color.red * 3f);
				}
				else
				{
					consoleLightMaterial.DisableKeyword("_EMISSION");
					consoleLightMaterial.SetColor("_EmissionColor", Color.black);
				}
			}
			if (hatchRenderer != null)
			{
				if (flag)
				{
					hatchRenderer.material.color = Color.red;
					hatchRenderer.material.EnableKeyword("_EMISSION");
					hatchRenderer.material.SetColor("_EmissionColor", Color.red * 2.5f);
				}
				else
				{
					hatchRenderer.material.color = Color.gray;
					hatchRenderer.material.DisableKeyword("_EMISSION");
					hatchRenderer.material.SetColor("_EmissionColor", Color.black);
				}
			}
			if (currentDrainageTime % 4f < 0.05f)
			{
				AudioClip audioClip3 = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Error");
				if (audioClip3 == null) audioClip3 = Resources.Load<AudioClip>("Ascensor_Error");
				GameObject pObj = (playerObjInstance != null) ? playerObjInstance : GameObject.FindGameObjectWithTag("Player");
				if (audioClip3 != null && pObj != null)
				{
					AudioSource.PlayClipAtPoint(audioClip3, pObj.transform.position, 0.45f);
				}
			}
			if (currentDrainageTime <= 0f)
			{
				escapeState = EscapeState.Ready;
				AudioClip audioClip4 = Resources.Load<AudioClip>("Audio/Hospital/successSound");
				if (audioClip4 == null) audioClip4 = Resources.Load<AudioClip>("successSound");
				if (audioClip4 != null)
				{
					AudioSource.PlayClipAtPoint(audioClip4, consolePos, 1f);
				}
				if (consoleIndicatorLight != null)
				{
					consoleIndicatorLight.enabled = true;
					consoleIndicatorLight.color = Color.green;
					consoleIndicatorLight.intensity = 3.5f;
				}
				if (consoleLightMaterial != null)
				{
					consoleLightMaterial.color = Color.green;
					consoleLightMaterial.EnableKeyword("_EMISSION");
					consoleLightMaterial.SetColor("_EmissionColor", Color.green * 3f);
				}
				if (hatchRenderer != null)
				{
					hatchRenderer.material.color = Color.gray;
					hatchRenderer.material.DisableKeyword("_EMISSION");
					hatchRenderer.material.SetColor("_EmissionColor", Color.black);
				}
				UnityEngine.Debug.Log("[TunnelsGenerator] Drenaje completado. Escotilla abierta.");
			}
		}
		else
		{
			if (escapeState != EscapeState.Ready || exitReached)
			{
				return;
			}
			Vector3 worldExitPos = exitPointPos;
			GameObject pObjExit = (playerObjInstance != null) ? playerObjInstance : GameObject.FindGameObjectWithTag("Player");
			float distExit = 9999f;
			if (pObjExit != null)
			{
				Vector3 pPosE = pObjExit.transform.position;
				distExit = Vector3.Distance(new Vector3(pPosE.x, 0f, pPosE.z), new Vector3(worldExitPos.x, 0f, worldExitPos.z));
			}

			if (distExit < 4.2f)
			{
				bool isHoldingExit = MobileInput.GetKey(KeyCode.E) || Input.GetKey(KeyCode.E) || MobileInput.ePressed;
				if (isHoldingExit)
				{
					interactionTimer = Mathf.MoveTowards(interactionTimer, 2f, Time.deltaTime);
				}
				else
				{
					interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
				}
				if (interactionTimer >= 2f)
				{
					exitReached = true;
					interactionTimer = 0f;
					StartCoroutine(HandleVictory());
				}
			}
			else
			{
				interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime * 2.5f);
			}
		}
	}

	public void PauseGame()
	{
		isPaused = true;
		Time.timeScale = 0f;
		MobileInput.SetCursorState(false);
		if (playerObjInstance != null)
		{
			fpsController = playerObjInstance.GetComponentInChildren<FirstPersonController>();
			if (fpsController != null)
			{
				fpsController.enabled = false;
			}
		}
	}

	public void ResumeGame()
	{
		isPaused = false;
		Time.timeScale = 1f;
		MobileInput.SetCursorState(true);
		if (fpsController != null)
		{
			fpsController.enabled = true;
		}
	}

	private void OnGUI()
	{
		if (exitReached)
		{
			if (fadeBlackTex == null)
			{
				fadeBlackTex = MakeTex(2, 2, Color.black);
			}

			// Fondo negro completo
			GUI.color = new Color(1f, 1f, 1f, victoryFadeAlpha);
			GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeBlackTex);
			GUI.color = Color.white;

			if (victoryStep > 0 && victoryStepAlpha > 0f)
			{
				GUI.color = new Color(1f, 1f, 1f, victoryStepAlpha);

				// Obtener idioma actual
				LocalizationManager.Idioma lang = LocalizationManager.Idioma.ESPAÑOL;
				if (LocalizationManager.Instance != null)
				{
					lang = LocalizationManager.Instance.GetIdiomaActual();
				}

				if (victoryStep == 1)
				{
					// PASO 1: JUEGO TERMINADO
					GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
					titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.07f);
					titleStyle.fontStyle = FontStyle.Bold;
					titleStyle.normal.textColor = Color.white;
					titleStyle.alignment = TextAnchor.MiddleCenter;

					string winMsg = "JUEGO TERMINADO";
					if (lang == LocalizationManager.Idioma.ENGLISH) winMsg = "GAME COMPLETED";
					else if (lang == LocalizationManager.Idioma.PORTUGUES) winMsg = "JOGO CONCLUÍDO";

					GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), winMsg, titleStyle);
				}
				else if (victoryStep == 2)
				{
					// PASO 2: ¡GRACIAS POR JUGAR!
					GUIStyle thanksStyle = new GUIStyle(GUI.skin.label);
					thanksStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.065f);
					thanksStyle.fontStyle = FontStyle.Bold;
					thanksStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
					thanksStyle.alignment = TextAnchor.MiddleCenter;

					string thanksMsg = "¡GRACIAS POR JUGAR!";
					if (lang == LocalizationManager.Idioma.ENGLISH) thanksMsg = "THANK YOU FOR PLAYING!";
					else if (lang == LocalizationManager.Idioma.PORTUGUES) thanksMsg = "OBRIGADO POR JOGAR!";

					GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), thanksMsg, thanksStyle);
				}
				else if (victoryStep == 3)
				{
					// PASO 3: REDES SOCIALES
					float sWidth = Screen.width;
					float sHeight = Screen.height;

					GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
					headerStyle.fontSize = Mathf.RoundToInt(sHeight * 0.045f);
					headerStyle.fontStyle = FontStyle.Bold;
					headerStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
					headerStyle.alignment = TextAnchor.MiddleCenter;

					string devTitle = "SIGUE EL DESARROLLO Y NOVEDADES EN:";
					if (lang == LocalizationManager.Idioma.ENGLISH) devTitle = "FOLLOW DEVELOPMENT & UPDATES AT:";
					else if (lang == LocalizationManager.Idioma.PORTUGUES) devTitle = "SIGA O DESENVOLVIMENTO EM:";

					GUI.Label(new Rect(0f, sHeight * 0.18f, sWidth, sHeight * 0.1f), devTitle, headerStyle);

					// Tarjeta de redes sociales
					GUIStyle cardStyle = new GUIStyle(GUI.skin.label);
					cardStyle.fontSize = Mathf.RoundToInt(sHeight * 0.035f);
					cardStyle.fontStyle = FontStyle.Bold;
					cardStyle.normal.textColor = Color.white;
					cardStyle.alignment = TextAnchor.MiddleCenter;

					string socialText = "📷 Instagram: @lxesusgarcial\n\n" +
					                    "📘 Facebook: lXesusGarcial\n\n" +
					                    "▶️ YouTube: @Xesus_Garcia";

					GUI.Label(new Rect(sWidth * 0.1f, sHeight * 0.35f, sWidth * 0.8f, sHeight * 0.45f), socialText, cardStyle);
				}

				GUI.color = Color.white;
			}
		}
		else if (!isPaused)
		{
			if (escapeState == EscapeState.Draining || escapeState == EscapeState.Ready)
			{
				if (alarmBgTex == null)
				{
					alarmBgTex = MakeTex(2, 2, new Color(0.08f, 0.01f, 0.01f, 0.85f));
				}
				if (alarmBorderTex == null)
				{
					alarmBorderTex = MakeTex(2, 2, new Color(1f, 0.2f, 0.2f, 0.9f));
				}
				if (alarmProgressTex == null)
				{
					alarmProgressTex = MakeTex(2, 2, new Color(0.9f, 0.1f, 0.1f, 1f));
				}
				if (progressRemainingTex == null)
				{
					progressRemainingTex = MakeTex(2, 2, new Color(0.2f, 0.05f, 0.05f, 0.6f));
				}

				float boxWidth = 330f;
				float boxHeight = 135f;
				// Colocar el cuadro dejando suficiente espacio a la derecha para que no solape el botón del bloc de notas (btnSize 50 + margen)
				float boxX = (float)Screen.width - boxWidth - 80f;
				float boxY = 80f;

				GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), alarmBgTex);
				GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, 3f), alarmBorderTex);
				GUI.DrawTexture(new Rect(boxX, boxY + boxHeight - 3f, boxWidth, 3f), alarmBorderTex);
				GUI.DrawTexture(new Rect(boxX, boxY, 3f, boxHeight), alarmBorderTex);
				GUI.DrawTexture(new Rect(boxX + boxWidth - 3f, boxY, 3f, boxHeight), alarmBorderTex);

				if (escapeState == EscapeState.Draining)
				{
					// LÍNEA 1: TÍTULO Y BOMBA (Separados en rectángulos independientes sin solaparse)
					GUI.Label(new Rect(boxX + 12f, boxY + 10f, 165f, 22f), ((Time.time % 0.8f < 0.4f) ? "⚠️" : "  ") + " ALARMA DE SISTEMA", new GUIStyle(GUI.skin.label)
					{
						fontSize = 13,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.red },
						alignment = TextAnchor.MiddleLeft
					});

					GUI.Label(new Rect(boxX + boxWidth - 150f, boxY + 10f, 140f, 22f), "BOMBA HIDRÁULICA ACTIVA", new GUIStyle(GUI.skin.label)
					{
						fontSize = 10,
						fontStyle = FontStyle.Bold,
						normal = { textColor = new Color(1f, 0.6f, 0.6f, 0.8f) },
						alignment = TextAnchor.MiddleRight
					});

					// LÍNEA 2: BARRA DE PROGRESO
					float barWidth = boxWidth - 30f;
					float barHeight = 14f;
					float barX = boxX + 15f;
					float barY = boxY + 38f;
					GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), progressRemainingTex);
					float fillRatio = Mathf.Clamp01(currentDrainageTime / drainageDuration);
					GUI.DrawTexture(new Rect(barX, barY, barWidth * fillRatio, barHeight), alarmProgressTex);
					GUI.DrawTexture(new Rect(barX, barY, barWidth, 1f), alarmBorderTex);
					GUI.DrawTexture(new Rect(barX, barY + barHeight - 1f, barWidth, 1f), alarmBorderTex);
					GUI.DrawTexture(new Rect(barX, barY, 1f, barHeight), alarmBorderTex);
					GUI.DrawTexture(new Rect(barX + barWidth - 1f, barY, 1f, barHeight), alarmBorderTex);

					// LÍNEA 3: EVACUANDO AGUA Y TIEMPO RESTANTE
					GUI.Label(new Rect(boxX + 15f, boxY + 58f, boxWidth - 30f, 22f), "EVACUANDO AGUA" + ((Time.time % 1.2f < 0.4f) ? "." : ((Time.time % 1.2f < 0.8f) ? ".." : "...")), new GUIStyle(GUI.skin.label)
					{
						fontSize = 12,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.white },
						alignment = TextAnchor.MiddleLeft
					});

					GUI.Label(new Rect(boxX + 15f, boxY + 58f, boxWidth - 30f, 22f), $"{Mathf.CeilToInt(currentDrainageTime)}s RESTANTES", new GUIStyle(GUI.skin.label)
					{
						fontSize = 12,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.red },
						alignment = TextAnchor.MiddleRight
					});

					// LÍNEA 4: ADVERTENCIA INFESTACIÓN
					GUI.Label(new Rect(boxX + 15f, boxY + 95f, boxWidth - 30f, 25f), "⚠️ ACTIVIDAD PARANORMAL DETECTADA: INFESTACIÓN ⚠️", new GUIStyle(GUI.skin.label)
					{
						fontSize = 10,
						fontStyle = FontStyle.Bold,
						normal = { textColor = new Color(1f, 0.3f, 0.3f, 0.9f) },
						alignment = TextAnchor.MiddleCenter
					});
				}
				else if (escapeState == EscapeState.Ready)
				{
					// ESTADO COMPLETADO: ALERTA DE EVACUACIÓN / BUSCAR SALIDA
					bool blink = Time.time % 0.8f < 0.4f;

					// LÍNEA 1: SISTEMA DRENADO (Izquierda) Y ESCOTILLA ABIERTA (Derecha) bien separados
					GUI.Label(new Rect(boxX + 12f, boxY + 12f, 180f, 25f), (blink ? "⚠️" : "  ") + " SISTEMA DRENADO", new GUIStyle(GUI.skin.label)
					{
						fontSize = 13,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.green },
						alignment = TextAnchor.MiddleLeft
					});

					GUI.Label(new Rect(boxX + boxWidth - 145f, boxY + 12f, 135f, 25f), "ESCOTILLA ABIERTA", new GUIStyle(GUI.skin.label)
					{
						fontSize = 11,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.yellow },
						alignment = TextAnchor.MiddleRight
					});

					// MENSAJE PARPANDEANTE DE INSTRUCCIÓN DE SALIDA
					GUI.Label(new Rect(boxX + 15f, boxY + 50f, boxWidth - 30f, 35f), "¡AGUA EVACUADA!\nBUSCA LA ESCOTILLA DE SALIDA", new GUIStyle(GUI.skin.label)
					{
						fontSize = 13,
						fontStyle = FontStyle.Bold,
						normal = { textColor = Color.white },
						alignment = TextAnchor.MiddleCenter
					});

					GUI.Label(new Rect(boxX + 15f, boxY + 95f, boxWidth - 30f, 25f), (blink ? "⚠️ ¡EVACÚA INMEDIATAMENTE! ⚠️" : "  ¡EVACÚA INMEDIATAMENTE!  "), new GUIStyle(GUI.skin.label)
					{
						fontSize = 11,
						fontStyle = FontStyle.Bold,
						normal = { textColor = new Color(1f, 0.8f, 0.2f, 1f) },
						alignment = TextAnchor.MiddleCenter
					});
				}
			}
			if (escapeState == EscapeState.Idle)
			{
				GameObject pObjGui = (playerObjInstance != null) ? playerObjInstance : GameObject.FindGameObjectWithTag("Player");
				if (pObjGui == null) return;
				Vector3 pPosG = pObjGui.transform.position;
				if (Vector3.Distance(new Vector3(pPosG.x, 0f, pPosG.z), new Vector3(consolePos.x, 0f, consolePos.z)) >= 4.2f)
				{
					return;
				}
				float num10 = 70f;
				float num11 = 70f;
				float num12 = (float)Screen.width / 2f - num10 / 2f;
				float num13 = (float)Screen.height * 0.62f;
				if (alarmBgTex == null)
				{
					alarmBgTex = MakeTex(2, 2, new Color(0.08f, 0.01f, 0.01f, 0.75f));
				}
				if (alarmBorderTex == null)
				{
					alarmBorderTex = MakeTex(2, 2, new Color(1f, 0.2f, 0.2f, 0.9f));
				}
				if (alarmProgressTex == null)
				{
					alarmProgressTex = MakeTex(2, 2, new Color(0.9f, 0.1f, 0.1f, 1f));
				}
				if (progressRemainingTex == null)
				{
					progressRemainingTex = MakeTex(2, 2, new Color(0.2f, 0.05f, 0.05f, 0.6f));
				}
				GUI.DrawTexture(new Rect(num12, num13, num10, num11), alarmBgTex);
				GUI.DrawTexture(new Rect(num12, num13, num10, 2f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num12, num13 + num11 - 2f, num10, 2f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num12, num13, 2f, num11), alarmBorderTex);
				GUI.DrawTexture(new Rect(num12 + num10 - 2f, num13, 2f, num11), alarmBorderTex);
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 32,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = Color.white
					},
					alignment = TextAnchor.MiddleCenter
				}, position: new Rect(num12, num13, num10, num11), text: "E");
				float num14 = 140f;
				float num15 = 10f;
				float num16 = (float)Screen.width / 2f - num14 / 2f;
				float num17 = num13 + num11 + 12f;
				GUI.DrawTexture(new Rect(num16, num17, num14, num15), progressRemainingTex);
				float num18 = interactionTimer / 2f;
				GUI.DrawTexture(new Rect(num16, num17, num14 * num18, num15), alarmProgressTex);
				GUI.DrawTexture(new Rect(num16, num17, num14, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num16, num17 + num15 - 1f, num14, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num16, num17, 1f, num15), alarmBorderTex);
				GUI.DrawTexture(new Rect(num16 + num14 - 1f, num17, 1f, num15), alarmBorderTex);
				GUIStyle gUIStyle3 = new GUIStyle(GUI.skin.label);
				gUIStyle3.fontSize = 15;
				gUIStyle3.fontStyle = FontStyle.Bold;
				gUIStyle3.normal.textColor = Color.yellow;
				gUIStyle3.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(0f, num17 + num15 + 8f, Screen.width, 30f), "MANTÉN PRESIONADO 'E' PARA REINICIAR LA BOMBA", gUIStyle3);
			}
			else if (escapeState == EscapeState.Ready)
			{
				GameObject pObjExitGui = (playerObjInstance != null) ? playerObjInstance : GameObject.FindGameObjectWithTag("Player");
				if (pObjExitGui == null) return;
				Vector3 pPosEG = pObjExitGui.transform.position;
				if (Vector3.Distance(new Vector3(pPosEG.x, 0f, pPosEG.z), new Vector3(exitPointPos.x, 0f, exitPointPos.z)) >= 4.2f)
				{
					return;
				}
				float num19 = 70f;
				float num20 = 70f;
				float num21 = (float)Screen.width / 2f - num19 / 2f;
				float num22 = (float)Screen.height * 0.62f;
				if (alarmBgTex == null)
				{
					alarmBgTex = MakeTex(2, 2, new Color(0.08f, 0.01f, 0.01f, 0.75f));
				}
				if (alarmBorderTex == null)
				{
					alarmBorderTex = MakeTex(2, 2, new Color(1f, 0.2f, 0.2f, 0.9f));
				}
				if (alarmProgressTex == null)
				{
					alarmProgressTex = MakeTex(2, 2, new Color(0.9f, 0.1f, 0.1f, 1f));
				}
				if (progressRemainingTex == null)
				{
					progressRemainingTex = MakeTex(2, 2, new Color(0.2f, 0.05f, 0.05f, 0.6f));
				}
				GUI.DrawTexture(new Rect(num21, num22, num19, num20), alarmBgTex);
				GUI.DrawTexture(new Rect(num21, num22, num19, 2f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num21, num22 + num20 - 2f, num19, 2f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num21, num22, 2f, num20), alarmBorderTex);
				GUI.DrawTexture(new Rect(num21 + num19 - 2f, num22, 2f, num20), alarmBorderTex);
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 32,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = Color.white
					},
					alignment = TextAnchor.MiddleCenter
				}, position: new Rect(num21, num22, num19, num20), text: "E");
				float num23 = 140f;
				float num24 = 10f;
				float num25 = (float)Screen.width / 2f - num23 / 2f;
				float num26 = num22 + num20 + 12f;
				GUI.DrawTexture(new Rect(num25, num26, num23, num24), progressRemainingTex);
				float num27 = interactionTimer / 2f;
				GUI.DrawTexture(new Rect(num25, num26, num23 * num27, num24), alarmProgressTex);
				GUI.DrawTexture(new Rect(num25, num26, num23, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num25, num26 + num24 - 1f, num23, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num25, num26, 1f, num24), alarmBorderTex);
				GUI.DrawTexture(new Rect(num25 + num23 - 1f, num27, 1f, num24), alarmBorderTex);
				GUIStyle gUIStyle4 = new GUIStyle(GUI.skin.label);
				gUIStyle4.fontSize = 15;
				gUIStyle4.fontStyle = FontStyle.Bold;
				gUIStyle4.normal.textColor = Color.green;
				gUIStyle4.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(0f, num26 + num24 + 8f, Screen.width, 30f), "MANTÉN PRESIONADO 'E' PARA ESCAPAR POR LA ESCOTILLA", gUIStyle4);
			}
		}
	}

	private Texture2D MakeTex(int width, int height, Color col)
	{
		Color[] array = new Color[width * height];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = col;
		}
		Texture2D texture2D = new Texture2D(width, height);
		texture2D.SetPixels(array);
		texture2D.Apply();
		return texture2D;
	}
}
