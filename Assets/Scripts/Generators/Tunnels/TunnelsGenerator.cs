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

public class TunnelsGenerator : MonoBehaviour
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

	private int width = 25;

	private int height = 25;

	private bool[,] grid;

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

	private bool displayWinText;

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

	private void DisableDirectionalLight()
	{
		Light[] array = Object.FindObjectsOfType<Light>();
		foreach (Light light in array)
		{
			if (light.type == LightType.Directional)
			{
				light.enabled = false;
				UnityEngine.Debug.Log("[TunnelsGenerator] Luz direccional '" + light.gameObject.name + "' desactivada automáticamente.");
			}
		}
		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f);
		RenderSettings.ambientIntensity = 0.5f;
		RenderSettings.reflectionIntensity = 0.25f;
		RenderSettings.skybox = null;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.06f);
		RenderSettings.fogDensity = 0.011f * (3f / mapScale);
		Camera[] array2 = Object.FindObjectsOfType<Camera>();
		foreach (Camera obj in array2)
		{
			obj.clearFlags = CameraClearFlags.Color;
			obj.backgroundColor = Color.black;
		}
		UnityEngine.Debug.Log("[TunnelsGenerator] Iluminación ambiental, niebla negra exponencial y cámaras configuradas para oscuridad total.");
	}

	private void DisableExternalCameras()
	{
		Camera[] array = Object.FindObjectsOfType<Camera>();
		foreach (Camera camera in array)
		{
			if (camera.transform.root.name != "Player" && camera.transform.root.name != "NestedParent_Unpack" && camera.gameObject.name == "Main Camera")
			{
				camera.gameObject.SetActive(value: false);
				UnityEngine.Debug.Log("[TunnelsGenerator] Cámara suelta '" + camera.gameObject.name + "' desactivada.");
			}
		}
	}

	private void CreateRuntimeMaterials()
	{
		Shader shader = null;
		if (baseMaterial != null)
		{
			shader = baseMaterial.shader;
		}
		else
		{
			shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
			{
#if UNITY_EDITOR
				string[] array = UnityEditor.AssetDatabase.FindAssets("t:Material");
				for (int i = 0; i < array.Length; i++)
				{
					string text = UnityEditor.AssetDatabase.GUIDToAssetPath(array[i]);
					Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(text);
					if (material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit")
					{
						shader = material.shader;
						UnityEngine.Debug.Log("[TunnelsGenerator] Shader URP Lit encontrado automáticamente a través del material: " + text);
						break;
					}
				}
#endif
			}
			if (shader == null)
			{
				shader = Shader.Find("Standard");
			}
		}
		wallMaterial = new Material(shader);
		wallMaterial.name = "M_ProceduralConcrete_Wall";
		if (baseMaterial != null)
		{
			wallMaterial.CopyPropertiesFromMaterial(baseMaterial);
		}
		if (wallMaterial.HasProperty("_BaseColor"))
		{
			wallMaterial.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.18f));
		}
		wallMaterial.color = new Color(0.18f, 0.18f, 0.18f);
		wallMaterial.SetFloat("_Smoothness", wallSmoothness);
		if (wallMaterial.HasProperty("_Glossiness"))
		{
			wallMaterial.SetFloat("_Glossiness", wallSmoothness);
		}
		wallMaterial.SetFloat("_Metallic", wallMetallic);
		if (wallConcreteTexture != null)
		{
			wallMaterial.SetTexture("_BaseMap", wallConcreteTexture);
			if (wallMaterial.HasProperty("_MainTex"))
			{
				wallMaterial.SetTexture("_MainTex", wallConcreteTexture);
			}
			if (wallMaterial.HasProperty("_BaseColor"))
			{
				wallMaterial.SetColor("_BaseColor", Color.white);
			}
			wallMaterial.color = Color.white;
			if (wallNormalTexture != null)
			{
				wallMaterial.SetTexture("_BumpMap", wallNormalTexture);
				wallMaterial.EnableKeyword("_NORMALMAP");
			}
			else
			{
				wallMaterial.SetTexture("_BumpMap", null);
				wallMaterial.DisableKeyword("_NORMALMAP");
			}
		}
		else
		{
			wallMaterial.SetTexture("_BumpMap", null);
			wallMaterial.DisableKeyword("_NORMALMAP");
		}
		archMaterial = new Material(shader);
		archMaterial.name = "M_ProceduralConcrete_Arch";
		if (baseMaterial != null)
		{
			archMaterial.CopyPropertiesFromMaterial(baseMaterial);
		}
		if (archMaterial.HasProperty("_BaseColor"))
		{
			archMaterial.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f));
		}
		archMaterial.color = new Color(0.15f, 0.15f, 0.15f);
		archMaterial.SetFloat("_Smoothness", archSmoothness);
		if (archMaterial.HasProperty("_Glossiness"))
		{
			archMaterial.SetFloat("_Glossiness", archSmoothness);
		}
		archMaterial.SetFloat("_Metallic", archMetallic);
		if (archTexture != null)
		{
			archMaterial.SetTexture("_BaseMap", archTexture);
			if (archMaterial.HasProperty("_MainTex"))
			{
				archMaterial.SetTexture("_MainTex", archTexture);
			}
			if (archMaterial.HasProperty("_BaseColor"))
			{
				archMaterial.SetColor("_BaseColor", Color.white);
			}
			archMaterial.color = Color.white;
			if (archNormalTexture != null)
			{
				archMaterial.SetTexture("_BumpMap", archNormalTexture);
				archMaterial.EnableKeyword("_NORMALMAP");
			}
			else
			{
				archMaterial.SetTexture("_BumpMap", null);
				archMaterial.DisableKeyword("_NORMALMAP");
			}
		}
		else
		{
			archMaterial.SetTexture("_BumpMap", null);
			archMaterial.DisableKeyword("_NORMALMAP");
		}
		floorMaterial = new Material(shader);
		floorMaterial.name = "M_ProceduralFloor_Catwalk";
		if (baseMaterial != null)
		{
			floorMaterial.CopyPropertiesFromMaterial(baseMaterial);
		}
		if (floorMaterial.HasProperty("_BaseColor"))
		{
			floorMaterial.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.22f));
		}
		floorMaterial.color = new Color(0.22f, 0.22f, 0.22f);
		floorMaterial.SetFloat("_Smoothness", floorSmoothness);
		if (floorMaterial.HasProperty("_Glossiness"))
		{
			floorMaterial.SetFloat("_Glossiness", floorSmoothness);
		}
		floorMaterial.SetFloat("_Metallic", floorMetallic);
		if (floorAlbedoTexture != null)
		{
			floorMaterial.SetTexture("_BaseMap", floorAlbedoTexture);
			if (floorMaterial.HasProperty("_MainTex"))
			{
				floorMaterial.SetTexture("_MainTex", floorAlbedoTexture);
			}
			if (floorMaterial.HasProperty("_BaseColor"))
			{
				floorMaterial.SetColor("_BaseColor", Color.white);
			}
			floorMaterial.color = Color.white;
			if (floorNormalTexture != null)
			{
				floorMaterial.SetTexture("_BumpMap", floorNormalTexture);
				floorMaterial.EnableKeyword("_NORMALMAP");
			}
			else
			{
				floorMaterial.SetTexture("_BumpMap", null);
				floorMaterial.DisableKeyword("_NORMALMAP");
			}
		}
		else
		{
			floorMaterial.SetTexture("_BumpMap", null);
			floorMaterial.DisableKeyword("_NORMALMAP");
		}
		pipeMaterial = new Material(shader);
		pipeMaterial.name = "M_ProceduralPipe_Arch";
		if (baseMaterial != null)
		{
			pipeMaterial.CopyPropertiesFromMaterial(baseMaterial);
		}
		if (pipeMaterial.HasProperty("_BaseColor"))
		{
			pipeMaterial.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.35f));
		}
		pipeMaterial.color = new Color(0.35f, 0.35f, 0.35f);
		pipeMaterial.SetFloat("_Smoothness", 0.45f);
		if (pipeMaterial.HasProperty("_Glossiness"))
		{
			pipeMaterial.SetFloat("_Glossiness", 0.45f);
		}
		pipeMaterial.SetFloat("_Metallic", 0.7f);
		if (pipeAlbedoTexture != null)
		{
			pipeMaterial.SetTexture("_BaseMap", pipeAlbedoTexture);
			if (pipeMaterial.HasProperty("_MainTex"))
			{
				pipeMaterial.SetTexture("_MainTex", pipeAlbedoTexture);
			}
			if (pipeMaterial.HasProperty("_BaseColor"))
			{
				pipeMaterial.SetColor("_BaseColor", Color.white);
			}
			pipeMaterial.color = Color.white;
			if (pipeNormalTexture != null)
			{
				pipeMaterial.SetTexture("_BumpMap", pipeNormalTexture);
				pipeMaterial.EnableKeyword("_NORMALMAP");
			}
			else
			{
				pipeMaterial.SetTexture("_BumpMap", null);
				pipeMaterial.DisableKeyword("_NORMALMAP");
			}
		}
		else
		{
			pipeMaterial.SetTexture("_BumpMap", null);
			pipeMaterial.DisableKeyword("_NORMALMAP");
		}
		wallPipeMaterial = new Material(shader);
		wallPipeMaterial.name = "M_ProceduralWallPipe";
		if (baseMaterial != null)
		{
			wallPipeMaterial.CopyPropertiesFromMaterial(baseMaterial);
		}
#if UNITY_EDITOR
		if (wallPipeAlbedo == null)
		{
			wallPipeAlbedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TunnelsMap/Meshy_AI_Modular_industrial_tu_0712001447_texture_fbx/Meshy_AI_Modular_industrial_tu_0712001447_texture.png");
		}
		if (wallPipeNormal == null)
		{
			wallPipeNormal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TunnelsMap/Meshy_AI_Modular_industrial_tu_0712001447_texture_fbx/Meshy_AI_Modular_industrial_tu_0712001447_texture_normal.png");
		}
#endif
		if (wallPipeAlbedo != null)
		{
			wallPipeMaterial.SetTexture("_BaseMap", wallPipeAlbedo);
			if (wallPipeMaterial.HasProperty("_MainTex"))
			{
				wallPipeMaterial.SetTexture("_MainTex", wallPipeAlbedo);
			}
			wallPipeMaterial.color = Color.white;
			float num = wallOffset * mapScale * 2f;
			wallPipeMaterial.SetTextureScale("_BaseMap", new Vector2(1f, num * 0.4f));
			if (wallPipeMaterial.HasProperty("_MainTex"))
			{
				wallPipeMaterial.SetTextureScale("_MainTex", new Vector2(1f, num * 0.4f));
			}
		}
		else
		{
			wallPipeMaterial.color = new Color(0.35f, 0.22f, 0.15f);
		}
		if (wallPipeNormal != null)
		{
			wallPipeMaterial.SetTexture("_BumpMap", wallPipeNormal);
			wallPipeMaterial.EnableKeyword("_NORMALMAP");
			wallPipeMaterial.SetTextureScale("_BumpMap", new Vector2(1f, wallOffset * mapScale * 2f * 0.4f));
		}
		else
		{
			wallPipeMaterial.SetTexture("_BumpMap", null);
			wallPipeMaterial.DisableKeyword("_NORMALMAP");
		}
		wallPipeMaterial.SetFloat("_Smoothness", 0.4f);
		if (wallPipeMaterial.HasProperty("_Glossiness"))
		{
			wallPipeMaterial.SetFloat("_Glossiness", 0.4f);
		}
		wallPipeMaterial.SetFloat("_Metallic", 0.75f);
		Shader puddleShader = Shader.Find("Universal Render Pipeline/Unlit");
		if (puddleShader == null) puddleShader = Shader.Find("Sprites/Default");
		waterPuddleMaterial = new Material(puddleShader);
		if (puddleShader.name.Contains("Universal Render Pipeline"))
		{
			waterPuddleMaterial.SetFloat("_Surface", 1f); // 1 = Transparent
			waterPuddleMaterial.SetFloat("_Blend", 0f); // 0 = Alpha
			waterPuddleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			waterPuddleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			waterPuddleMaterial.SetInt("_ZWrite", 0);
			waterPuddleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}
		waterPuddleMaterial.name = "M_WaterPuddle";
		Color color = new Color(0.35f, 0.4f, 0.45f, 0.55f);
		waterPuddleMaterial.color = color;
		if (waterPuddleMaterial.HasProperty("_Color"))
		{
			waterPuddleMaterial.SetColor("_Color", color);
		}
#if UNITY_EDITOR
		if (puddleShapeTexture == null)
		{
			puddleShapeTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StarterAssets/Mobile/UI/UI_Circle_Faded.png");
		}
#endif
		if (puddleShapeTexture != null)
		{
			waterPuddleMaterial.mainTexture = puddleShapeTexture;
			if (waterPuddleMaterial.HasProperty("_BaseMap"))
			{
				waterPuddleMaterial.SetTexture("_BaseMap", puddleShapeTexture);
			}
			if (waterPuddleMaterial.HasProperty("_MainTex"))
			{
				waterPuddleMaterial.SetTexture("_MainTex", puddleShapeTexture);
			}
		}
	}

	private void GenerateMazeTunnelsMap()
	{
		grid = new bool[width, height];
		Stack<Vector2Int> stack = new Stack<Vector2Int>();
		Vector2Int item = new Vector2Int(1, 1);
		grid[item.x, item.y] = true;
		stack.Push(item);
		while (stack.Count > 0)
		{
			Vector2Int vector2Int = stack.Peek();
			List<Vector2Int> list = new List<Vector2Int>();
			Vector2Int[] array = new Vector2Int[4]
			{
				new Vector2Int(0, 2),
				new Vector2Int(0, -2),
				new Vector2Int(2, 0),
				new Vector2Int(-2, 0)
			};
			foreach (Vector2Int vector2Int2 in array)
			{
				Vector2Int item2 = vector2Int + vector2Int2;
				if (item2.x > 0 && item2.x < width - 1 && item2.y > 0 && item2.y < height - 1 && !grid[item2.x, item2.y])
				{
					list.Add(item2);
				}
			}
			if (list.Count > 0)
			{
				Vector2Int item3 = list[Random.Range(0, list.Count)];
				grid[vector2Int.x + (item3.x - vector2Int.x) / 2, vector2Int.y + (item3.y - vector2Int.y) / 2] = true;
				grid[item3.x, item3.y] = true;
				stack.Push(item3);
			}
			else
			{
				stack.Pop();
			}
		}
		for (int j = 2; j < width - 2; j += 2)
		{
			for (int k = 2; k < height - 2; k += 2)
			{
				if (grid[j, k])
				{
					continue;
				}
				if (grid[j - 1, k] && grid[j + 1, k])
				{
					if (Random.value < 0.2f)
					{
						grid[j, k] = true;
					}
				}
				else if (grid[j, k - 1] && grid[j, k + 1] && Random.value < 0.2f)
				{
					grid[j, k] = true;
				}
			}
		}
		float num = segmentLength * mapScale;
		patrolPoints.Clear();
		for (int l = 0; l < width; l++)
		{
			for (int m = 0; m < height; m++)
			{
				if (grid[l, m])
				{
					Vector3 vector = new Vector3((float)l * num, 0f, (float)m * num);
					if ((l + m) % 2 == 0)
					{
						patrolPoints.Add(vector);
					}
				}
			}
		}
		Vector3 vector2 = Vector3.zero;
		Vector3 vector3 = Vector3.zero;
		Vector3 vector4 = Vector3.zero;
		float num2 = num;
		List<Vector3> list2 = new List<Vector3>();
		int num3 = 4;
		foreach (Vector3 patrolPoint in patrolPoints)
		{
			int num4 = Mathf.RoundToInt(patrolPoint.x / num2);
			int num5 = Mathf.RoundToInt(patrolPoint.z / num2);
			int num6 = 0;
			if (num4 - 1 < 0 || !grid[num4 - 1, num5])
			{
				num6++;
			}
			if (num4 + 1 >= width || !grid[num4 + 1, num5])
			{
				num6++;
			}
			if (num5 - 1 < 0 || !grid[num4, num5 - 1])
			{
				num6++;
			}
			if (num5 + 1 >= height || !grid[num4, num5 + 1])
			{
				num6++;
			}
			if (num6 < num3)
			{
				num3 = num6;
			}
		}
		foreach (Vector3 patrolPoint2 in patrolPoints)
		{
			int num7 = Mathf.RoundToInt(patrolPoint2.x / num2);
			int num8 = Mathf.RoundToInt(patrolPoint2.z / num2);
			int num9 = 0;
			if (num7 - 1 < 0 || !grid[num7 - 1, num8])
			{
				num9++;
			}
			if (num7 + 1 >= width || !grid[num7 + 1, num8])
			{
				num9++;
			}
			if (num8 - 1 < 0 || !grid[num7, num8 - 1])
			{
				num9++;
			}
			if (num8 + 1 >= height || !grid[num7, num8 + 1])
			{
				num9++;
			}
			if (num9 == num3)
			{
				list2.Add(patrolPoint2);
			}
		}
		if (list2.Count == 0)
		{
			list2 = patrolPoints;
		}

		// Encontrar celdas candidatas para el spawn del jugador (callejones sin salida / esquinas)
		List<Vector3> playerCandidates = new List<Vector3>();
		int maxWallsPlayer = 0;
		foreach (Vector3 patrolPoint in patrolPoints)
		{
			int num4 = Mathf.RoundToInt(patrolPoint.x / num2);
			int num5 = Mathf.RoundToInt(patrolPoint.z / num2);
			int num6 = 0;
			if (num4 - 1 < 0 || !grid[num4 - 1, num5]) num6++;
			if (num4 + 1 >= width || !grid[num4 + 1, num5]) num6++;
			if (num5 - 1 < 0 || !grid[num4, num5 - 1]) num6++;
			if (num5 + 1 >= height || !grid[num4, num5 + 1]) num6++;
			if (num6 > maxWallsPlayer)
			{
				maxWallsPlayer = num6;
			}
		}
		foreach (Vector3 patrolPoint2 in patrolPoints)
		{
			int num7 = Mathf.RoundToInt(patrolPoint2.x / num2);
			int num8 = Mathf.RoundToInt(patrolPoint2.z / num2);
			int num9 = 0;
			if (num7 - 1 < 0 || !grid[num7 - 1, num8]) num9++;
			if (num7 + 1 >= width || !grid[num7 + 1, num8]) num9++;
			if (num8 - 1 < 0 || !grid[num7, num8 - 1]) num9++;
			if (num8 + 1 >= height || !grid[num7, num8 + 1]) num9++;
			if (num9 == maxWallsPlayer)
			{
				playerCandidates.Add(patrolPoint2);
			}
		}
		if (playerCandidates.Count == 0)
		{
			playerCandidates = patrolPoints;
		}

		if (patrolPoints.Count >= 3)
		{
			float num10 = 0f;
			int num11 = Mathf.Min(150, patrolPoints.Count * 2);
			for (int n = 0; n < num11; n++)
			{
				int index = Random.Range(0, list2.Count);
				Vector3 vector5 = list2[index];
				int index2 = Random.Range(0, patrolPoints.Count);
				int index3 = Random.Range(0, playerCandidates.Count);
				Vector3 candidatePlayerPos = playerCandidates[index3];

				if (!(patrolPoints[index2] == vector5) && !(candidatePlayerPos == vector5) && !(patrolPoints[index2] == candidatePlayerPos))
				{
					float num12 = Vector3.Distance(vector5, patrolPoints[index2]);
					float num13 = Vector3.Distance(patrolPoints[index2], candidatePlayerPos);
					float num14 = Vector3.Distance(candidatePlayerPos, vector5);
					float num15 = num12 + num13 + num14;
					if (num15 > num10)
					{
						num10 = num15;
						vector2 = vector5;
						vector3 = patrolPoints[index2];
						vector4 = candidatePlayerPos;
					}
				}
			}
		}
		else
		{
			float num16 = segmentLength * mapScale;
			vector2 = new Vector3((float)(width - 2) * num16, 0.2f * mapScale, (float)(height - 2) * num16);
			vector3 = new Vector3(2f * num16, 0.2f * mapScale, 2f * num16);
			vector4 = new Vector3(1f * num16, 0.2f * mapScale, 1f * num16);
		}
		exitPointPos = vector2;
		consolePos = vector3;
		playerSpawnPos = vector4;
		float num17 = segmentLength * mapScale;
		int playerCellX = Mathf.RoundToInt(playerSpawnPos.x / num17);
		int playerCellZ = Mathf.RoundToInt(playerSpawnPos.z / num17);

		for (int l = 0; l < width; l++)
		{
			for (int m = 0; m < height; m++)
			{
				if (grid[l, m])
				{
					Vector3 vectorSegmentPos = new Vector3((float)l * num17, 0f, (float)m * num17);
					bool isPlayerCell = (l == playerCellX && m == playerCellZ);
					SpawnMazeSegment(l, m, vectorSegmentPos, isPlayerCell);
				}
			}
		}
		float y = base.transform.position.y;
		float num18 = wallOffset * mapScale;
		int num19 = Mathf.RoundToInt(exitPointPos.x / num17);
		int num20 = Mathf.RoundToInt(exitPointPos.z / num17);
		bool num21 = num19 - 1 < 0 || !grid[num19 - 1, num20];
		bool flag = num19 + 1 >= width || !grid[num19 + 1, num20];
		bool flag2 = num20 - 1 < 0 || !grid[num19, num20 - 1];
		bool flag3 = num20 + 1 >= height || !grid[num19, num20 + 1];
		Vector3 zero = Vector3.zero;
		Quaternion localRotation = Quaternion.identity;
		if (num21)
		{
			zero = new Vector3(0f - num18 + 0.4f * mapScale, 0f, 0f);
			localRotation = Quaternion.Euler(0f, 90f, 0f);
		}
		else if (flag)
		{
			zero = new Vector3(num18 - 0.4f * mapScale, 0f, 0f);
			localRotation = Quaternion.Euler(0f, -90f, 0f);
		}
		else if (flag2)
		{
			zero = new Vector3(0f, 0f, 0f - num18 + 0.4f * mapScale);
			localRotation = Quaternion.Euler(0f, 0f, 0f);
		}
		else if (flag3)
		{
			zero = new Vector3(0f, 0f, num18 - 0.4f * mapScale);
			localRotation = Quaternion.Euler(0f, 180f, 0f);
		}
		else
		{
			zero = new Vector3(0.8f * mapScale, 0f, 0f);
		}
		Vector3 position = exitPointPos + zero;
		int num22 = Mathf.RoundToInt(consolePos.x / num17);
		int num23 = Mathf.RoundToInt(consolePos.z / num17);
		bool flag4 = num22 - 1 < 0 || !grid[num22 - 1, num23];
		bool flag5 = num22 + 1 >= width || !grid[num22 + 1, num23];
		bool flag6 = num23 - 1 < 0 || !grid[num22, num23 - 1];
		bool flag7 = num23 + 1 >= height || !grid[num22, num23 + 1];
		Vector3 vector6 = Vector3.zero;
		Quaternion localRotation2 = Quaternion.identity;
		if (flag4)
		{
			vector6 = new Vector3(0f - num18 + 0.35f * mapScale, 0f, 0f);
			localRotation2 = Quaternion.Euler(0f, 90f, 0f);
		}
		else if (flag5)
		{
			vector6 = new Vector3(num18 - 0.35f * mapScale, 0f, 0f);
			localRotation2 = Quaternion.Euler(0f, -90f, 0f);
		}
		else if (flag6)
		{
			vector6 = new Vector3(0f, 0f, 0f - num18 + 0.25f * mapScale);
			localRotation2 = Quaternion.Euler(0f, 0f, 0f);
		}
		else if (flag7)
		{
			vector6 = new Vector3(0f, 0f, num18 - 0.25f * mapScale);
			localRotation2 = Quaternion.Euler(0f, 180f, 0f);
		}
		Vector3 vector7 = consolePos + vector6;
		Vector3 vector8 = base.transform.TransformPoint(exitPointPos);
		Vector3 vector9 = base.transform.TransformPoint(position);
		Vector3 vector10 = base.transform.TransformPoint(vector7);
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		gameObject.name = "Escape_Hatch_Visual";
		gameObject.transform.SetParent(navMeshHolder.transform);
		gameObject.transform.position = new Vector3(vector8.x, y + 0.01f * mapScale, vector8.z);
		gameObject.transform.localScale = new Vector3(1.8f * mapScale, 0.05f * mapScale, 1.8f * mapScale);
		ApplyProceduralMaterial(gameObject, archMaterial, gameObject.transform.localScale);
		Collider component = gameObject.GetComponent<Collider>();
		if (component != null)
		{
			Object.DestroyImmediate(component);
		}
		hatchRenderer = gameObject.GetComponent<Renderer>();
		if (hatchRenderer != null)
		{
			hatchRenderer.material.color = Color.red;
			hatchRenderer.material.EnableKeyword("_EMISSION");
			hatchRenderer.material.SetColor("_EmissionColor", Color.red * 2f);
		}
		GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject2.name = "Escape_Activator_Console";
		gameObject2.transform.SetParent(navMeshHolder.transform);
		gameObject2.transform.position = new Vector3(vector10.x, y + 0.7f * mapScale, vector10.z);
		gameObject2.transform.localRotation = localRotation2;
		gameObject2.transform.localScale = new Vector3(0.7f * mapScale, 1.4f * mapScale, 0.5f * mapScale);
		ApplyProceduralMaterial(gameObject2, archMaterial, gameObject2.transform.localScale);
		Vector3 vector11 = new Vector3(0f, 0.4f * mapScale, 0.26f * mapScale);
		if (flag6)
		{
			vector11 = new Vector3(0f, 0.4f * mapScale, 0.26f * mapScale);
		}
		else if (flag7)
		{
			vector11 = new Vector3(0f, 0.4f * mapScale, -0.26f * mapScale);
		}
		else if (flag4)
		{
			vector11 = new Vector3(0.26f * mapScale, 0.4f * mapScale, 0f);
		}
		else if (flag5)
		{
			vector11 = new Vector3(-0.26f * mapScale, 0.4f * mapScale, 0f);
		}
		GameObject gameObject3 = new GameObject("Console_Lever_Pivot");
		gameObject3.transform.SetParent(navMeshHolder.transform);
		gameObject3.transform.position = base.transform.TransformPoint(vector7 + vector11);
		if (flag6)
		{
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
		}
		else if (flag7)
		{
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		}
		else if (flag4)
		{
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
		}
		else if (flag5)
		{
			gameObject3.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
		}
		GameObject gameObject4 = new GameObject("Lever_Rotator");
		gameObject4.transform.SetParent(gameObject3.transform);
		gameObject4.transform.localPosition = Vector3.zero;
		gameObject4.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
		leverArmObj = gameObject4;
		GameObject gameObject5 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		gameObject5.name = "Lever_Base";
		gameObject5.transform.SetParent(gameObject4.transform);
		gameObject5.transform.localPosition = Vector3.zero;
		gameObject5.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		gameObject5.transform.localScale = new Vector3(0.18f * mapScale, 0.04f * mapScale, 0.18f * mapScale);
		ApplyProceduralMaterial(gameObject5, wallMaterial, gameObject5.transform.localScale);
		Collider component2 = gameObject5.GetComponent<Collider>();
		if (component2 != null)
		{
			Object.DestroyImmediate(component2);
		}
		GameObject gameObject6 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		gameObject6.name = "Lever_Arm";
		gameObject6.transform.SetParent(gameObject4.transform);
		gameObject6.transform.localPosition = new Vector3(0f, 0.22f * mapScale, 0f);
		gameObject6.transform.localRotation = Quaternion.identity;
		gameObject6.transform.localScale = new Vector3(0.04f * mapScale, 0.22f * mapScale, 0.04f * mapScale);
		ApplyProceduralMaterial(gameObject6, wallMaterial, gameObject6.transform.localScale);
		Collider component3 = gameObject6.GetComponent<Collider>();
		if (component3 != null)
		{
			Object.DestroyImmediate(component3);
		}
		GameObject obj = new GameObject("Console_Indicator_PointLight");
		obj.transform.SetParent(gameObject2.transform);
		obj.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
		Light obj2 = obj.AddComponent<Light>();
		obj2.type = LightType.Point;
		obj2.range = 5f * mapScale;
		obj2.intensity = 1.8f;
		obj2.color = Color.yellow;
		GameObject gameObject7 = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject7.name = "Hatch_Status_Panel";
		gameObject7.transform.SetParent(navMeshHolder.transform);
		gameObject7.transform.position = new Vector3(vector9.x, y + 0.9f * mapScale, vector9.z);
		gameObject7.transform.localRotation = localRotation;
		gameObject7.transform.localScale = new Vector3(0.8f * mapScale, 1.8f * mapScale, 0.8f * mapScale);
		ApplyProceduralMaterial(gameObject7, archMaterial, gameObject7.transform.localScale);
		GameObject gameObject8 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		gameObject8.name = "Hatch_Indicator";
		gameObject8.transform.SetParent(gameObject7.transform);
		gameObject8.transform.localPosition = new Vector3(0f, 0.55f, 0f);
		gameObject8.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
		Collider component4 = gameObject8.GetComponent<Collider>();
		if (component4 != null)
		{
			Object.DestroyImmediate(component4);
		}
		Renderer component5 = gameObject8.GetComponent<Renderer>();
		if (component5 != null)
		{
			consoleLightMaterial = component5.material;
			consoleLightMaterial.color = Color.red;
			consoleLightMaterial.EnableKeyword("_EMISSION");
			consoleLightMaterial.SetColor("_EmissionColor", Color.red * 2.5f);
		}
		GameObject gameObject9 = new GameObject("Hatch_Indicator_PointLight");
		gameObject9.transform.SetParent(gameObject8.transform);
		gameObject9.transform.localPosition = Vector3.zero;
		consoleIndicatorLight = gameObject9.AddComponent<Light>();
		consoleIndicatorLight.type = LightType.Point;
		consoleIndicatorLight.range = 10f * mapScale;
		consoleIndicatorLight.intensity = 3.5f;
		consoleIndicatorLight.color = Color.red;
		pumpAudioSource = gameObject7.AddComponent<AudioSource>();
		pumpAudioSource.spatialBlend = 0.3f;
		pumpAudioSource.minDistance = 8f;
		pumpAudioSource.maxDistance = 180f;
		pumpAudioSource.loop = true;
		pumpAudioSource.volume = 0.85f;
		pumpAudioSource.playOnAwake = false;
		escapeState = EscapeState.Idle;
		consolePos = gameObject2.transform.position;
		exitPointPos = gameObject.transform.position;
		worldExitPointPos = exitPointPos;
		patrolPoints.Add(exitPointPos);
	}

	private void SpawnMazeSegment(int gx, int gz, Vector3 position, bool isPlayerCell = false)
	{
		GameObject gameObject = new GameObject($"Cell_{gx}_{gz}");
		gameObject.transform.SetParent(navMeshHolder.transform);
		gameObject.transform.localPosition = position;
		gameObject.transform.rotation = Quaternion.identity;
		float num = segmentLength * mapScale;
		float num2 = wallOffset * mapScale;
		float num3 = wallHeight * mapScale;
		float num4 = wallThickness * mapScale;
		bool flag = gx - 1 < 0 || !grid[gx - 1, gz];
		bool flag2 = gx + 1 >= width || !grid[gx + 1, gz];
		bool flag3 = gz - 1 < 0 || !grid[gx, gz - 1];
		bool flag4 = gz + 1 >= height || !grid[gx, gz + 1];
		SpawnFloorAndCeiling(gameObject, num, num4, flag, flag2, flag3, flag4, num2);
		float num5 = num3 * 0.6f;
		float num6 = num3 - num5;
		float num7 = 0.35f * num2;
		float y = Mathf.Sqrt(num7 * num7 + num6 * num6);
		float num8 = Mathf.Atan2(num7, num6) * 57.29578f;
		if (flag)
		{
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject2.name = "Wall_West_Vertical";
			gameObject2.transform.SetParent(gameObject.transform);
			gameObject2.transform.localPosition = new Vector3(0f - num2, num5 / 2f, 0f);
			Vector3 vector = new Vector3(num4, num5, num);
			gameObject2.transform.localScale = vector;
			ApplyProceduralMaterial(gameObject2, wallMaterial, vector);
			GameObject gameObject3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject3.name = "Wall_West_Arch";
			gameObject3.transform.SetParent(gameObject.transform);
			gameObject3.transform.localPosition = new Vector3(0f - num2 + num7 / 2f, num5 + num6 / 2f, 0f);
			Vector3 vector2 = new Vector3(num4, y, num);
			gameObject3.transform.localScale = vector2;
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 0f, 0f - num8);
			ApplyProceduralMaterial(gameObject3, archMaterial, vector2);
			SpawnWallPipe(gameObject, new Vector3(0f - num2 + wallPipeOffset * mapScale, wallPipeHeight * mapScale, 0f), Quaternion.Euler(0f, 0f, 0f), "West");
		}
		if (flag2)
		{
			GameObject gameObject4 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject4.name = "Wall_East_Vertical";
			gameObject4.transform.SetParent(gameObject.transform);
			gameObject4.transform.localPosition = new Vector3(num2, num5 / 2f, 0f);
			Vector3 vector3 = new Vector3(num4, num5, num);
			gameObject4.transform.localScale = vector3;
			ApplyProceduralMaterial(gameObject4, wallMaterial, vector3);
			GameObject gameObject5 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject5.name = "Wall_East_Arch";
			gameObject5.transform.SetParent(gameObject.transform);
			gameObject5.transform.localPosition = new Vector3(num2 - num7 / 2f, num5 + num6 / 2f, 0f);
			Vector3 vector4 = new Vector3(num4, y, num);
			gameObject5.transform.localScale = vector4;
			gameObject5.transform.localRotation = Quaternion.Euler(0f, 0f, num8);
			ApplyProceduralMaterial(gameObject5, archMaterial, vector4);
			SpawnWallPipe(gameObject, new Vector3(num2 - wallPipeOffset * mapScale, wallPipeHeight * mapScale, 0f), Quaternion.Euler(0f, 180f, 0f), "East");
		}
		if (flag3)
		{
			GameObject gameObject6 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject6.name = "Wall_South_Vertical";
			gameObject6.transform.SetParent(gameObject.transform);
			gameObject6.transform.localPosition = new Vector3(0f, num5 / 2f, 0f - num2);
			Vector3 vector5 = new Vector3(num, num5, num4);
			gameObject6.transform.localScale = vector5;
			ApplyProceduralMaterial(gameObject6, wallMaterial, vector5);
			GameObject gameObject7 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject7.name = "Wall_South_Arch";
			gameObject7.transform.SetParent(gameObject.transform);
			gameObject7.transform.localPosition = new Vector3(0f, num5 + num6 / 2f, 0f - num2 + num7 / 2f);
			Vector3 vector6 = new Vector3(num, y, num4);
			gameObject7.transform.localScale = vector6;
			gameObject7.transform.localRotation = Quaternion.Euler(num8, 0f, 0f);
			ApplyProceduralMaterial(gameObject7, archMaterial, vector6);
			SpawnWallPipe(gameObject, new Vector3(0f, wallPipeHeight * mapScale, 0f - num2 + wallPipeOffset * mapScale), Quaternion.Euler(0f, 90f, 0f), "South");
		}
		if (flag4)
		{
			GameObject gameObject8 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject8.name = "Wall_North_Vertical";
			gameObject8.transform.SetParent(gameObject.transform);
			gameObject8.transform.localPosition = new Vector3(0f, num5 / 2f, num2);
			Vector3 vector7 = new Vector3(num, num5, num4);
			gameObject8.transform.localScale = vector7;
			ApplyProceduralMaterial(gameObject8, wallMaterial, vector7);
			GameObject gameObject9 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject9.name = "Wall_North_Arch";
			gameObject9.transform.SetParent(gameObject.transform);
			gameObject9.transform.localPosition = new Vector3(0f, num5 + num6 / 2f, num2 - num7 / 2f);
			Vector3 vector8 = new Vector3(num, y, num4);
			gameObject9.transform.localScale = vector8;
			gameObject9.transform.localRotation = Quaternion.Euler(0f - num8, 0f, 0f);
			ApplyProceduralMaterial(gameObject9, archMaterial, vector8);
			SpawnWallPipe(gameObject, new Vector3(0f, wallPipeHeight * mapScale, num2 - wallPipeOffset * mapScale), Quaternion.Euler(0f, -90f, 0f), "North");
		}
		float num9 = num7 * 1.15f;
		Vector3 scale = new Vector3(num9, num6, num9);
		if (flag && flag3)
		{
			SpawnCornerCol(gameObject, new Vector3(0f - num2 + num9 / 2f, num5 + num6 / 2f, 0f - num2 + num9 / 2f), scale);
		}
		if (flag && flag4)
		{
			SpawnCornerCol(gameObject, new Vector3(0f - num2 + num9 / 2f, num5 + num6 / 2f, num2 - num9 / 2f), scale);
		}
		if (flag2 && flag3)
		{
			SpawnCornerCol(gameObject, new Vector3(num2 - num9 / 2f, num5 + num6 / 2f, 0f - num2 + num9 / 2f), scale);
		}
		if (flag2 && flag4)
		{
			SpawnCornerCol(gameObject, new Vector3(num2 - num9 / 2f, num5 + num6 / 2f, num2 - num9 / 2f), scale);
		}
		if (gz - 1 >= 0 && grid[gx, gz - 1])
		{
			if (flag && gx - 1 >= 0 && grid[gx - 1, gz - 1])
			{
				SpawnCornerCol(gameObject, new Vector3(0f - num2 + num9 / 2f, num5 + num6 / 2f, (0f - num) / 2f + num9 / 2f), scale);
			}
			if (flag2 && gx + 1 < width && grid[gx + 1, gz - 1])
			{
				SpawnCornerCol(gameObject, new Vector3(num2 - num9 / 2f, num5 + num6 / 2f, (0f - num) / 2f + num9 / 2f), scale);
			}
		}
		if (gz + 1 < height && grid[gx, gz + 1])
		{
			if (flag && gx - 1 >= 0 && grid[gx - 1, gz + 1])
			{
				SpawnCornerCol(gameObject, new Vector3(0f - num2 + num9 / 2f, num5 + num6 / 2f, num / 2f - num9 / 2f), scale);
			}
			if (flag2 && gx + 1 < width && grid[gx + 1, gz + 1])
			{
				SpawnCornerCol(gameObject, new Vector3(num2 - num9 / 2f, num5 + num6 / 2f, num / 2f - num9 / 2f), scale);
			}
		}
		if (gx - 1 >= 0 && grid[gx - 1, gz])
		{
			if (flag3 && gz - 1 >= 0 && grid[gx - 1, gz - 1])
			{
				SpawnCornerCol(gameObject, new Vector3((0f - num) / 2f + num9 / 2f, num5 + num6 / 2f, 0f - num2 + num9 / 2f), scale);
			}
			if (flag4 && gz + 1 < height && grid[gx - 1, gz + 1])
			{
				SpawnCornerCol(gameObject, new Vector3((0f - num) / 2f + num9 / 2f, num5 + num6 / 2f, num2 - num9 / 2f), scale);
			}
		}
		if (gx + 1 < width && grid[gx + 1, gz])
		{
			if (flag3 && gz - 1 >= 0 && grid[gx + 1, gz - 1])
			{
				SpawnCornerCol(gameObject, new Vector3(num / 2f - num9 / 2f, num5 + num6 / 2f, 0f - num2 + num9 / 2f), scale);
			}
			if (flag4 && gz + 1 < height && grid[gx + 1, gz + 1])
			{
				SpawnCornerCol(gameObject, new Vector3(num / 2f - num9 / 2f, num5 + num6 / 2f, num2 - num9 / 2f), scale);
			}
		}
		float num10 = 2f * num2;
		float num11_fill = (num - num10) / 2f;
		float num12_fill = num10 / 2f + num11_fill / 2f;
		if (!flag)
		{
			SpawnFillerWall(gameObject, new Vector3((0f - num) / 2f, num3 / 2f, 0f - num12_fill), new Vector3(num4, num3, num11_fill), "West_South");
			SpawnFillerWall(gameObject, new Vector3((0f - num) / 2f, num3 / 2f, num12_fill), new Vector3(num4, num3, num11_fill), "West_North");
		}
		if (!flag2)
		{
			SpawnFillerWall(gameObject, new Vector3(num / 2f, num3 / 2f, 0f - num12_fill), new Vector3(num4, num3, num11_fill), "East_South");
			SpawnFillerWall(gameObject, new Vector3(num / 2f, num3 / 2f, num12_fill), new Vector3(num4, num3, num11_fill), "East_North");
		}
		if (!flag3)
		{
			SpawnFillerWall(gameObject, new Vector3(0f - num12_fill, num3 / 2f, (0f - num) / 2f), new Vector3(num11_fill, num3, num4), "South_West");
			SpawnFillerWall(gameObject, new Vector3(num12_fill, num3 / 2f, (0f - num) / 2f), new Vector3(num11_fill, num3, num4), "South_East");
		}
		if (!flag4)
		{
			SpawnFillerWall(gameObject, new Vector3(0f - num12_fill, num3 / 2f, num / 2f), new Vector3(num11_fill, num3, num4), "North_West");
			SpawnFillerWall(gameObject, new Vector3(num12_fill, num3 / 2f, num / 2f), new Vector3(num11_fill, num3, num4), "North_East");
		}
		if (isPlayerCell)
		{
			GameObject flatBridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
			flatBridge.name = "Catwalk_Bridge";
			flatBridge.transform.SetParent(gameObject.transform);
			flatBridge.transform.localPosition = Vector3.zero;
			if ((gx - 1 >= 0 && grid[gx - 1, gz]) || (gx + 1 < width && grid[gx + 1, gz]))
			{
				flatBridge.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			}
			else
			{
				flatBridge.transform.localRotation = Quaternion.identity;
			}
			float bridgeWidth = 2.4f * mapScale;
			float bridgeLength = num;
			flatBridge.transform.localScale = new Vector3(bridgeWidth, 0.1f * mapScale, bridgeLength);
			ApplyProceduralMaterial(flatBridge, floorMaterial, flatBridge.transform.localScale);
		}
		else if (floorCatwalkPrefab != null)
		{
			GameObject gameObject10 = Object.Instantiate(floorCatwalkPrefab, gameObject.transform);
			gameObject10.name = "Catwalk";
			gameObject10.transform.localPosition = Vector3.zero;
			if ((gx - 1 >= 0 && grid[gx - 1, gz]) || (gx + 1 < width && grid[gx + 1, gz]))
			{
				gameObject10.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			}
			else
			{
				gameObject10.transform.localRotation = Quaternion.identity;
			}
			Vector3 localScale = floorCatwalkPrefab.transform.localScale;
			gameObject10.transform.localScale = new Vector3(localScale.x * mapScale * catwalkWidthMultiplier, localScale.y * mapScale, localScale.z * mapScale);
			ApplyMaterialToAllRenderers(gameObject10, floorMaterial);
			Collider[] componentsInChildren = gameObject10.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Object.DestroyImmediate(componentsInChildren[i]);
			}
			BoxCollider boxCollider = gameObject10.AddComponent<BoxCollider>();
			boxCollider.size = new Vector3(num2 * 2f, 1f * mapScale, num);
			boxCollider.center = new Vector3(0f, -0.5f * mapScale, 0f);
		}
		if (!isPlayerCell)
		{
			if (pipeArchPrefab != null && Random.value < 0.7f)
			{
				if (flag)
				{
					SpawnPipesOnWall(gameObject.transform, "West");
				}
				else if (flag2)
				{
					SpawnPipesOnWall(gameObject.transform, "East");
				}
				else if (flag3)
				{
					SpawnPipesOnWall(gameObject.transform, "South");
				}
				else if (flag4)
				{
					SpawnPipesOnWall(gameObject.transform, "North");
				}
			}
			if ((gx == 1 && gz == 1) || Random.value < safeLightProbability)
			{
				SpawnCeilingLight(gameObject.transform, isCurrentlyOn: true);
			}
		}
	}

	private void SpawnFillerWall(GameObject cellRoot, Vector3 localPos, Vector3 scale, string nameTag)
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject.name = "Wall_Filler_" + nameTag;
		gameObject.transform.SetParent(cellRoot.transform);
		gameObject.transform.localPosition = localPos;
		gameObject.transform.localScale = scale;
		ApplyProceduralMaterial(gameObject, wallMaterial, scale);
		DecorateFillerWall(gameObject, scale, nameTag);
	}

	private void SpawnCornerCol(GameObject cellRoot, Vector3 localPos, Vector3 scale)
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject.name = "Corner_Col_Seal";
		gameObject.transform.SetParent(cellRoot.transform);
		gameObject.transform.localPosition = localPos;
		gameObject.transform.localScale = scale;
		ApplyProceduralMaterial(gameObject, wallMaterial, scale);
	}

	private void SpawnFloorAndCeiling(GameObject cellRoot, float S, float scaledWallThickness, bool hasWestWall, bool hasEastWall, bool hasSouthWall, bool hasNorthWall, float scaledWallOffset)
	{
		float num = (hasWestWall ? (0f - scaledWallOffset) : ((0f - S) / 2f));
		float num2 = (hasEastWall ? scaledWallOffset : (S / 2f));
		float num3 = (hasSouthWall ? (0f - scaledWallOffset) : ((0f - S) / 2f));
		float num4 = (hasNorthWall ? scaledWallOffset : (S / 2f));
		if (hasWestWall)
		{
			num -= 0.1f;
		}
		if (hasEastWall)
		{
			num2 += 0.1f;
		}
		if (hasSouthWall)
		{
			num3 -= 0.1f;
		}
		if (hasNorthWall)
		{
			num4 += 0.1f;
		}
		float x = num2 - num;
		float z = num4 - num3;
		Vector3 vector = new Vector3((num + num2) / 2f, 0f, (num3 + num4) / 2f);
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject.name = "Floor_Solid";
		gameObject.transform.SetParent(cellRoot.transform);
		gameObject.transform.localPosition = new Vector3(vector.x, (0f - scaledWallThickness) / 2f, vector.z);
		gameObject.transform.localScale = new Vector3(x, scaledWallThickness, z);
		ApplyProceduralMaterial(gameObject, floorMaterial, gameObject.transform.localScale);
		GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject2.name = "Ceiling_Solid";
		gameObject2.transform.SetParent(cellRoot.transform);
		gameObject2.transform.localPosition = new Vector3(vector.x, wallHeight * mapScale + scaledWallThickness / 2f, vector.z);
		gameObject2.transform.localScale = new Vector3(x, scaledWallThickness, z);
		ApplyProceduralMaterial(gameObject2, wallMaterial, gameObject2.transform.localScale);
		gameObject2.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
	}

	private void SpawnPipesOnWall(Transform cellRoot, string wallSide)
	{
		if (!(pipeArchPrefab == null))
		{
			float num = wallOffset * mapScale;
			GameObject gameObject = Object.Instantiate(pipeArchPrefab, cellRoot);
			gameObject.name = "Pipes_" + wallSide;
			Vector3 localPosition = Vector3.zero;
			Quaternion localRotation = Quaternion.identity;
			Vector3 localScale = pipeArchPrefab.transform.localScale;
			gameObject.transform.localScale = new Vector3(localScale.x * mapScale, localScale.y * mapScale, localScale.z * mapScale);
			switch (wallSide)
			{
			case "West":
				localPosition = new Vector3(0f - num + 0.1f * mapScale, 1.4f * mapScale, 0f);
				localRotation = Quaternion.Euler(0f, 90f, 0f);
				break;
			case "East":
				localPosition = new Vector3(num - 0.1f * mapScale, 1.4f * mapScale, 0f);
				localRotation = Quaternion.Euler(0f, -90f, 0f);
				break;
			case "South":
				localPosition = new Vector3(0f, 1.4f * mapScale, 0f - num + 0.1f * mapScale);
				localRotation = Quaternion.Euler(0f, 0f, 0f);
				break;
			case "North":
				localPosition = new Vector3(0f, 1.4f * mapScale, num - 0.1f * mapScale);
				localRotation = Quaternion.Euler(0f, 180f, 0f);
				break;
			}
			gameObject.transform.localPosition = localPosition;
			gameObject.transform.localRotation = localRotation;
			Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Object.DestroyImmediate(componentsInChildren[i]);
			}
			ApplyMaterialToAllRenderers(gameObject, pipeMaterial);
		}
	}

	private void ApplyProceduralMaterial(GameObject obj, Material mat, Vector3 scale)
	{
		Renderer component = obj.GetComponent<Renderer>();
		if (!(component == null) && !(mat == null))
		{
			component.material = mat;
			float num = 1f;
			float num2 = 1f;
			if (scale.z > scale.x && scale.y > scale.x)
			{
				num = scale.z / 4f;
				num2 = scale.y / 4f;
			}
			else if (scale.x > scale.z && scale.y > scale.z)
			{
				num = scale.x / 4f;
				num2 = scale.y / 4f;
			}
			else if (scale.y >= scale.x && scale.y >= scale.z)
			{
				num = scale.x / 4f;
				num2 = scale.y / 4f;
			}
			else
			{
				num = scale.x / 4f;
				num2 = scale.z / 4f;
			}
			Vector2 value = new Vector2(num, num2);
			if (component.material.HasProperty("_BaseMap"))
			{
				component.material.SetTextureScale("_BaseMap", value);
			}
			if (component.material.HasProperty("_MainTex"))
			{
				component.material.SetTextureScale("_MainTex", value);
			}
			if (component.material.HasProperty("_BumpMap"))
			{
				component.material.SetTextureScale("_BumpMap", value);
			}
		}
	}

	private void SpawnCeilingLight(Transform cellRoot, bool isCurrentlyOn)
	{
		if (ceilingLightPrefab == null)
		{
			return;
		}
		float num = wallHeight * mapScale;
		Vector3 localPosition = new Vector3(0f, num + lightVerticalOffset * mapScale, 0f);
		GameObject gameObject = Object.Instantiate(ceilingLightPrefab, cellRoot);
		gameObject.name = (isCurrentlyOn ? "CeilingLight_ON" : "CeilingLight_OFF");
		gameObject.transform.localPosition = localPosition;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one * mapScale;
		FlickeringLight flickeringLight = gameObject.GetComponent<FlickeringLight>();
		if (flickeringLight == null)
		{
			flickeringLight = gameObject.GetComponentInChildren<FlickeringLight>();
		}
		if (flickeringLight != null)
		{
			Object.DestroyImmediate(flickeringLight);
		}
		gameObject.AddComponent<TunnelLightFlicker>();
		Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform != gameObject.transform && (transform.name.Contains("Ceiling") || transform.name.Contains("Celling")))
			{
				transform.gameObject.SetActive(value: false);
			}
		}
		Light light = gameObject.GetComponentInChildren<Light>();
		if (light == null)
		{
			GameObject obj = new GameObject("PointLight");
			obj.transform.SetParent(gameObject.transform);
			obj.transform.localPosition = new Vector3(0f, -0.5f * mapScale, 0f);
			light = obj.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = lightColor;
			light.range = lightRange * mapScale;
			light.intensity = lightIntensity * 5f;
			light.shadows = LightShadows.Soft;
		}
		else
		{
			light.type = LightType.Point;
			light.transform.localPosition = new Vector3(0f, -0.5f * mapScale, 0f);
			light.color = lightColor;
			light.range = lightRange * mapScale;
			light.intensity = lightIntensity * 5f;
			light.shadows = LightShadows.Soft;
		}
		if (light != null)
		{
			light.enabled = isCurrentlyOn;
		}
		Renderer componentInChildren = gameObject.GetComponentInChildren<Renderer>();
		if (componentInChildren != null)
		{
			Material material = componentInChildren.material;
			if (isCurrentlyOn)
			{
				material.EnableKeyword("_EMISSION");
				material.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.4f) * 2f);
				material.color = new Color(1f, 0.75f, 0.4f);
			}
			else
			{
				material.DisableKeyword("_EMISSION");
				material.SetColor("_EmissionColor", Color.black);
				material.color = Color.gray;
			}
		}
	}

	private void SpawnEntities()
	{
		_ = segmentLength;
		_ = mapScale;
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		// El catwalk está a Y=0 en el mundo. El piso del ascensor tiene un offset local de -0.05.
		// Para alinear el piso del ascensor exactamente a Y=0, el pivot de la cabina en el mundo debe ser Y = 0.05.
		Vector3 spawnPos = base.transform.TransformPoint(playerSpawnPos);
		spawnPos.y = 0.05f * mapScale;

		// El pivot del personaje (pies) está en Y=0. Lo colocamos ligeramente arriba del piso del ascensor (Y=spawnPos.y + 0.05f) para evitar clipping.
		Vector3 vector = spawnPos;
		vector.y = spawnPos.y + 0.05f;

		float num = segmentLength * mapScale;
		int num2 = Mathf.RoundToInt(playerSpawnPos.x / num);
		int num3 = Mathf.RoundToInt(playerSpawnPos.z / num);
		Vector3 forward = Vector3.forward;
		// Comprobar si la pasarela (catwalk) de la celda del jugador va de Este a Oeste
		bool catwalkRunsEastWest = (num2 - 1 >= 0 && grid[num2 - 1, num3]) || (num2 + 1 < width && grid[num2 + 1, num3]);
		
		if (catwalkRunsEastWest)
		{
			// Priorizar alineación Este/Oeste para que coincida con la rotación de la pasarela
			if (num2 + 1 < width && grid[num2 + 1, num3])
			{
				forward = Vector3.right;
			}
			else if (num2 - 1 >= 0 && grid[num2 - 1, num3])
			{
				forward = Vector3.left;
			}
			else if (num3 + 1 < height && grid[num2, num3 + 1])
			{
				forward = Vector3.forward;
			}
			else if (num3 - 1 >= 0 && grid[num2, num3 - 1])
			{
				forward = Vector3.back;
			}
		}
		else
		{
			// Priorizar alineación Norte/Sur
			if (num3 + 1 < height && grid[num2, num3 + 1])
			{
				forward = Vector3.forward;
			}
			else if (num3 - 1 >= 0 && grid[num2, num3 - 1])
			{
				forward = Vector3.back;
			}
			else if (num2 + 1 < width && grid[num2 + 1, num3])
			{
				forward = Vector3.right;
			}
			else if (num2 - 1 >= 0 && grid[num2 - 1, num3])
			{
				forward = Vector3.left;
			}
		}
		Quaternion quaternion = Quaternion.LookRotation(forward);
		GameObject playerTagObj = gameObject;
		GameObject playerRootObj = playerTagObj;
		if (playerTagObj != null)
		{
			// Obtener el root de la jerarquía para teletransportar a todo el conjunto (cámara, cápsula, etc.) juntos
			playerRootObj = playerTagObj.transform.root.gameObject;
		}

		CharacterController cc = (playerTagObj != null) ? playerTagObj.GetComponentInChildren<CharacterController>(includeInactive: true) : null;
		if (cc != null)
		{
			cc.enabled = false;
			Physics.SyncTransforms();
		}

		if (playerRootObj == null)
		{
			if (playerPrefab != null)
			{
				gameObject = Object.Instantiate(playerPrefab, vector, quaternion);
				gameObject.name = "Player";
				cc = gameObject.GetComponentInChildren<CharacterController>(includeInactive: true);
				if (cc != null)
				{
					cc.enabled = false;
					Physics.SyncTransforms();
				}
			}
		}
		else
		{
			// Calcular la relación de rotación local actual de la cápsula respecto al root
			Quaternion localRotRelation = Quaternion.Inverse(playerRootObj.transform.rotation) * playerTagObj.transform.rotation;

			// Desactivar temporalmente componentes de Cinemachine para forzar un reset de la cámara (incluyendo inactivos)
			foreach (MonoBehaviour mono in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
			{
				if (mono != null && mono.gameObject != null && mono.gameObject.scene.name != null && mono.GetType().FullName.Contains("Cinemachine"))
				{
					mono.enabled = false;
					StartCoroutine(ReenableMonoAfterFrame(mono));
				}
			}

			// Rotar el ROOT completo de manera que la cápsula quede con la rotación del objetivo
			playerRootObj.transform.rotation = quaternion * Quaternion.Inverse(localRotRelation);

			// Calcular el offset de posición en el espacio del mundo tras la rotación
			Vector3 worldOffset = playerTagObj.transform.position - playerRootObj.transform.position;

			// Posicionar el ROOT de manera que la cápsula quede exactamente en la posición del objetivo en el mundo
			playerRootObj.transform.position = vector - worldOffset;

			gameObject = playerTagObj;
		}

		if (gameObject != null)
		{
			// Resetear la rotación local de la cámara principal y del target de la cámara
			if (Camera.main != null)
			{
				Camera.main.transform.localRotation = Quaternion.identity;
			}
			Transform camTarget = gameObject.transform.Find("PlayerCameraRoot");
			if (camTarget == null) camTarget = gameObject.transform.Find("CinemachineCameraTarget");
			// Buscar también de forma recursiva por si está dentro de la cápsula o anidado
			if (camTarget == null)
			{
				var fpcComponent = gameObject.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
				if (fpcComponent != null && fpcComponent.CinemachineCameraTarget != null)
				{
					camTarget = fpcComponent.CinemachineCameraTarget.transform;
				}
			}
			if (camTarget != null)
			{
				camTarget.localRotation = Quaternion.identity;
			}

			// Resetear Cinemachine POV
			ResetCinemachineRotation(quaternion);

			// Resetear el pitch interno del FirstPersonController usando reflexión
			var fpc = gameObject.GetComponentInChildren<StarterAssets.FirstPersonController>();
			if (fpc != null)
			{
				var pitchField = fpc.GetType().GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (pitchField != null)
				{
					pitchField.SetValue(fpc, 0f);
					UnityEngine.Debug.Log("[TunnelsGenerator] _cinemachineTargetPitch del FirstPersonController reseteado a 0.");
				}
			}
		}

		if (gameObject != null && cc != null)
		{
			Physics.SyncTransforms();
			cc.enabled = true;
			Physics.SyncTransforms();
			UnityEngine.Debug.Log("[TunnelsGenerator] CharacterController del jugador habilitado con seguridad tras teletransporte.");
		}
		playerObjInstance = gameObject;

		// Registrar spawn e inicializar vidas dinámicamente en GameManager
		if (playerObjInstance != null)
		{
			if (GameManager.Instance == null)
			{
				GameObject gmObj = new GameObject("GameManager");
				gmObj.AddComponent<GameManager>();
			}

			// Calcular vidas dinámicas basadas en la escala del mapa
			int vidasTunnels = 3;
			if (mapScale >= 1.5f)
			{
				vidasTunnels = 5;
			}
			else if (mapScale >= 1.2f)
			{
				vidasTunnels = 4;
			}
			
			GameManager.Instance.InicializarVidasParaMapa(vidasTunnels);
			
			// Registrar la posición segura a nivel del suelo de la pasarela
			GameManager.Instance.RegistrarSpawnJugador(vector, quaternion);
		}
		SpawnArrivalElevator(spawnPos, quaternion);
		GameObject gameObject2 = null;
		PhenomenonAIController phenomenonAIController = Object.FindObjectOfType<PhenomenonAIController>();
		if (phenomenonAIController != null)
		{
			gameObject2 = phenomenonAIController.gameObject;
		}
		float num4 = 0.2f * mapScale;
		int index = Mathf.Clamp((int)((float)patrolPoints.Count * enemySpawnDistancePercent), 0, patrolPoints.Count - 1);
		Vector3 vector2 = base.transform.TransformPoint(patrolPoints[index] + Vector3.up * num4);
		if (gameObject2 == null && enemyPrefab != null)
		{
			if (NavMesh.SamplePosition(vector2, out var hit, 2f, -1))
			{
				vector2 = hit.position;
			}
			gameObject2 = Object.Instantiate(enemyPrefab, vector2, Quaternion.identity);
			gameObject2.name = "ThePhenomenon";
			gameObject2.transform.localScale = Vector3.one * mapScale * 1.8f;
			phenomenonAIController = gameObject2.GetComponent<PhenomenonAIController>();
		}
		else if (gameObject2 != null)
		{
			if (NavMesh.SamplePosition(vector2, out var hit2, 2f, -1))
			{
				vector2 = hit2.position;
			}
			NavMeshAgent component = gameObject2.GetComponent<NavMeshAgent>();
			if (component != null)
			{
				if (!component.Warp(vector2))
				{
					gameObject2.transform.position = vector2;
					UnityEngine.Debug.LogWarning("[TunnelsGenerator] Warp falló para el enemigo de la escena. Posicionando por transform.position.");
				}
			}
			else
			{
				gameObject2.transform.position = vector2;
			}
			gameObject2.transform.localScale = Vector3.one * mapScale * 1.8f;
		}
		if (gameObject2 != null && phenomenonAIController != null)
		{
			Transform[] array = new Transform[patrolPoints.Count];
			GameObject gameObject3 = new GameObject("PatrolPointsRoot");
			gameObject3.transform.SetParent(base.transform);
			for (int i = 0; i < patrolPoints.Count; i++)
			{
				GameObject gameObject4 = new GameObject($"PatrolPoint_{i}");
				gameObject4.transform.SetParent(gameObject3.transform);
				gameObject4.transform.position = patrolPoints[i] + Vector3.up * (0.5f * mapScale);
				array[i] = gameObject4.transform;
			}
			phenomenonAIController.patrolPoints = array;
			phenomenonAIController.player = ((gameObject != null) ? gameObject.transform : null);
			phenomenonAIController.currentState = PhenomenonAIController.PhenomenonState.Patrol;
			UnityEngine.Debug.Log($"[TunnelsGenerator] {array.Length} Puntos de patrulla asignados en el laberinto.");
		}
	}

	private void SpawnArrivalElevator(Vector3 spawnPos, Quaternion spawnRot)
	{
		// Destruir cualquier cabina existente para evitar duplicaciones (incluyendo inactivas)
		foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (go != null && go.name == "ArrivalElevatorCabin" && go.scene.name != null)
			{
				UnityEngine.Debug.LogWarning("[TunnelsGenerator] Se detectó una cabina de ascensor existente (activa o inactiva). Destruyéndola para evitar duplicados.");
				Object.DestroyImmediate(go);
			}
		}

		GameObject gameObject = new GameObject("ArrivalElevatorCabin");
		gameObject.transform.position = spawnPos;
		gameObject.transform.rotation = spawnRot;
		gameObject.transform.SetParent(navMeshHolder.transform);

		float tileSize = 2.4f * mapScale; // 7.2f
		float innerHeight = 2.6f * mapScale; // 7.8f
		float thickness = 0.08f * mapScale; // 0.24f

		// Materiales del metal de la cabina (Premium - Gris/Azulado industrial con textura)
		Material cabinaMat = Resources.Load<Material>("Materiales/Mat_Bed_Metal_01");
		if (cabinaMat == null) cabinaMat = Resources.Load<Material>("Mat_Bed_Metal_01");
		if (cabinaMat != null)
		{
			cabinaMat = Object.Instantiate(cabinaMat);
			cabinaMat.color = new Color(0.5f, 0.52f, 0.55f);
		}
		else
		{
			cabinaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			cabinaMat.color = new Color(0.2f, 0.23f, 0.25f);
			cabinaMat.SetFloat("_Metallic", 0.85f);
			cabinaMat.SetFloat("_Smoothness", 0.5f);
		}

		// Material del parachoques y carcasa de botones (Negro mate)
		Material bumperMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
		bumperMat.color = new Color(0.08f, 0.08f, 0.08f);
		bumperMat.SetFloat("_Metallic", 0.4f);
		bumperMat.SetFloat("_Smoothness", 0.15f);

		// Material de las puertas (Gris acero brillante pulido con textura)
		Material puertaMat = Resources.Load<Material>("Materiales/Mat_Bed_Metal_01");
		if (puertaMat == null) puertaMat = Resources.Load<Material>("Mat_Bed_Metal_01");
		if (puertaMat != null)
		{
			puertaMat = Object.Instantiate(puertaMat);
			puertaMat.color = new Color(0.7f, 0.72f, 0.75f);
		}
		else
		{
			puertaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			puertaMat.color = new Color(0.35f, 0.38f, 0.4f);
			puertaMat.SetFloat("_Metallic", 0.95f);
			puertaMat.SetFloat("_Smoothness", 0.65f);
		}

		// Material de luz de techo (Blanco frío con Emisión potente)
		Material lightEmissiveMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
		lightEmissiveMat.color = Color.white;
		lightEmissiveMat.EnableKeyword("_EMISSION");
		lightEmissiveMat.SetColor("_EmissionColor", new Color(1.3f, 1.45f, 1.6f) * 1.8f);

		// Material de pantalla indicadora de piso (Verde brillante emisivo)
		Material greenScreenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
		greenScreenMat.color = Color.black;
		greenScreenMat.EnableKeyword("_EMISSION");
		greenScreenMat.SetColor("_EmissionColor", new Color(0f, 1.6f, 0.25f) * 2.5f);

		// A. Suelo de la cabina
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.name = "Elevator_Floor";
		floor.transform.SetParent(gameObject.transform, false);
		floor.transform.localPosition = new Vector3(0f, 0.02f * mapScale, 0f);
		floor.transform.localScale = new Vector3(tileSize * 1.02f, thickness, tileSize * 1.02f);
		floor.GetComponent<Renderer>().sharedMaterial = cabinaMat;

		// B. Techo de la cabina
		GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
		ceiling.name = "Elevator_Ceiling";
		ceiling.transform.SetParent(gameObject.transform, false);
		ceiling.transform.localPosition = new Vector3(0f, innerHeight, 0f);
		ceiling.transform.localScale = new Vector3(tileSize * 1.02f, thickness, tileSize * 1.02f);
		ceiling.GetComponent<Renderer>().sharedMaterial = cabinaMat;

		// C. Paneles de la Pared Izquierda
		int panelsCount = 4;
		float panelWidth = tileSize / panelsCount;
		for (int i = 0; i < panelsCount; i++)
		{
			GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
			panel.name = "PanelIzq_" + i;
			panel.transform.SetParent(gameObject.transform, false);
			float offset = -0.5f * tileSize + (i * panelWidth) + (panelWidth / 2f);
			panel.transform.localPosition = new Vector3(-0.49f * tileSize, innerHeight / 2f, offset);
			panel.transform.localScale = new Vector3(thickness * 1.2f, innerHeight, panelWidth * 1.02f);
			panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
		}

		// D. Paneles de la Pared Derecha
		for (int i = 0; i < panelsCount; i++)
		{
			GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
			panel.name = "PanelDer_" + i;
			panel.transform.SetParent(gameObject.transform, false);
			float offset = -0.5f * tileSize + (i * panelWidth) + (panelWidth / 2f);
			panel.transform.localPosition = new Vector3(0.49f * tileSize, innerHeight / 2f, offset);
			panel.transform.localScale = new Vector3(thickness * 1.2f, innerHeight, panelWidth * 1.02f);
			panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
		}

		// E. Paneles de la Pared Trasera
		float panelWidthBack = tileSize / panelsCount;
		for (int i = 0; i < panelsCount; i++)
		{
			GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
			panel.name = "PanelTrasero_" + i;
			panel.transform.SetParent(gameObject.transform, false);
			float offset = -0.5f * tileSize + (i * panelWidthBack) + (panelWidthBack / 2f);
			panel.transform.localPosition = new Vector3(offset, innerHeight / 2f, -0.49f * tileSize);
			panel.transform.localScale = new Vector3(panelWidthBack * 1.02f, innerHeight, thickness * 1.2f);
			panel.GetComponent<Renderer>().sharedMaterial = cabinaMat;
		}

		// F. Parachoques / Pasamanos protectores
		float bumperHeight = 0.9f * mapScale;
		float bumperSize = 0.05f * mapScale;

		GameObject leftBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
		leftBumper.name = "BumperIzquierdo";
		leftBumper.transform.SetParent(gameObject.transform, false);
		leftBumper.transform.localPosition = new Vector3(-0.48f * tileSize, bumperHeight, 0f);
		leftBumper.transform.localScale = new Vector3(bumperSize, bumperSize * 1.5f, tileSize * 0.92f);
		leftBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

		GameObject rightBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rightBumper.name = "BumperDerecho";
		rightBumper.transform.SetParent(gameObject.transform, false);
		rightBumper.transform.localPosition = new Vector3(0.48f * tileSize, bumperHeight, 0f);
		rightBumper.transform.localScale = new Vector3(bumperSize, bumperSize * 1.5f, tileSize * 0.92f);
		rightBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

		GameObject backBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
		backBumper.name = "BumperTrasero";
		backBumper.transform.SetParent(gameObject.transform, false);
		backBumper.transform.localPosition = new Vector3(0f, bumperHeight, -0.48f * tileSize);
		backBumper.transform.localScale = new Vector3(tileSize * 0.92f, bumperSize, bumperSize * 1.5f);
		backBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;

		// G. Panel de Luz Fluorescente en el techo
		GameObject lightPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
		lightPanel.name = "PanelLuzTecho";
		lightPanel.transform.SetParent(gameObject.transform, false);
		float ceilingBottomY = innerHeight - (thickness / 2f);
		float lightHeight = 0.02f * mapScale;
		lightPanel.transform.localPosition = new Vector3(0f, ceilingBottomY - (lightHeight / 2f) - 0.005f, 0f);
		lightPanel.transform.localScale = new Vector3(tileSize * 0.45f, lightHeight, tileSize * 0.45f);
		lightPanel.GetComponent<Renderer>().sharedMaterial = lightEmissiveMat;
		Collider lpCol = lightPanel.GetComponent<Collider>();
		if (lpCol != null) DestroyImmediate(lpCol);

		// Luz PointLight en tiempo real
		GameObject pointLightObj = new GameObject("LuzAscensor");
		pointLightObj.transform.SetParent(lightPanel.transform);
		pointLightObj.transform.localPosition = new Vector3(0f, -0.15f * mapScale, 0f);
		Light pLight = pointLightObj.AddComponent<Light>();
		pLight.type = LightType.Point;
		pLight.color = new Color(0.85f, 0.95f, 1f);
		pLight.intensity = 3.5f;
		pLight.range = tileSize * 0.6f;
		pLight.shadows = LightShadows.Soft;

		// H. Pantalla indicadora de piso exterior
		// La pared frontal (sello) tiene su CENTRO en Z = 0.49*tileSize y grosor = thickness.
		// Cara exterior = 0.49*tileSize + thickness/2. Ponemos la pantalla justo encima de esa cara.
		float frontWallOuterZ = 0.49f * tileSize + (thickness / 2f) + 0.008f * mapScale;

		GameObject extScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
		extScreen.name = "PantallaPisoExterior";
		extScreen.transform.SetParent(gameObject.transform, false);
		extScreen.transform.localPosition = new Vector3(0f, innerHeight + 0.15f * mapScale, frontWallOuterZ);
		extScreen.transform.localScale = new Vector3(0.35f * mapScale, 0.15f * mapScale, 0.02f * mapScale);
		extScreen.GetComponent<Renderer>().sharedMaterial = greenScreenMat;

		// Luz de la pantalla exterior
		GameObject extScreenLightObj = new GameObject("LuzPantallaExterior");
		extScreenLightObj.transform.SetParent(extScreen.transform, false);
		extScreenLightObj.transform.localPosition = new Vector3(0f, 0f, 1f);
		Light extScreenLight = extScreenLightObj.AddComponent<Light>();
		extScreenLight.type = LightType.Point;
		extScreenLight.color = new Color(0.2f, 1f, 0.3f);
		extScreenLight.intensity = 1.5f;
		extScreenLight.range = 2f * mapScale;
		extScreenLight.shadows = LightShadows.None;

		// Texto indicador exterior
		// NOTA: localScale se expresa como fracción de mapScale para que sea proporcional siempre.
		// Valores calibrados manualmente: K_x=0.45, K_y=0.48 → a mapScale=3 queda (1.35, 1.44, 1)
		// El rectángulo verde mide 0.15*mapScale de alto → el texto ocupa ~16% de esa altura: correcto.
		GameObject extTextObj = new GameObject("TextoPisoExterior");
		extTextObj.transform.SetParent(gameObject.transform, false);
		extTextObj.transform.localPosition = new Vector3(0f, innerHeight + 0.15f * mapScale, frontWallOuterZ + 0.01f * mapScale);
		extTextObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		extTextObj.transform.localScale = new Vector3(mapScale * 0.45f, mapScale * 0.48f, 1f);
		
		TextMesh extTM = extTextObj.AddComponent<TextMesh>();
		extTM.text = "S";
		extTM.fontSize = 64;
		extTM.characterSize = 0.05f;
		extTM.alignment = TextAlignment.Center;
		extTM.anchor = TextAnchor.MiddleCenter;
		extTM.fontStyle = FontStyle.Bold;
		extTM.color = new Color(0.02f, 0.1f, 0.02f); // Verde muy oscuro — contrasta sobre verde brillante
		
		Renderer extRend = extTextObj.GetComponent<Renderer>();
		Material extTextMat = new Material(Shader.Find("Sprites/Default"));
		extTextMat.mainTexture = extTM.font.material.mainTexture;
		extRend.sharedMaterial = extTextMat;

		// H2. Botonera con pantalla "S" en la pared INTERIOR DERECHA
		float intPanelX = 0.43f * tileSize; // panel interior a salvo de la pared (cara interior ~0.441*tileSize)

		// Panel negro de la botonera — más ancho en Z, menos alto para que sea proporcional
		GameObject intButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
		intButton.name = "BotoneraInterior";
		intButton.transform.SetParent(gameObject.transform, false);
		intButton.transform.localPosition = new Vector3(intPanelX, 1.45f * mapScale, 0f);
		// X=delgado (plano contra pared), Y=moderado (altura del panel), Z=cuadrado (frente del panel)
		intButton.transform.localScale = new Vector3(0.022f * mapScale, 0.22f * mapScale, 0.18f * mapScale);
		intButton.GetComponent<Renderer>().sharedMaterial = bumperMat;

		// Pantalla verde centrada en la mitad superior del panel
		GameObject intScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
		intScreen.name = "PantallaPisoInterior";
		intScreen.transform.SetParent(gameObject.transform, false);
		intScreen.transform.localPosition = new Vector3(intPanelX - 0.015f * mapScale, 1.50f * mapScale, 0f);
		intScreen.transform.localScale = new Vector3(0.007f * mapScale, 0.09f * mapScale, 0.09f * mapScale);
		intScreen.GetComponent<Renderer>().sharedMaterial = greenScreenMat;
		Collider isCol = intScreen.GetComponent<Collider>();
		if (isCol != null) DestroyImmediate(isCol);

		// Texto "S" — Y=+90° para que la S se vea correctamente (no al revés) cuando el jugador mira hacia +X
		GameObject intTextObj = new GameObject("TextoPisoInterior");
		intTextObj.transform.SetParent(gameObject.transform, false);
		intTextObj.transform.localPosition = new Vector3(intPanelX - 0.028f * mapScale, 1.50f * mapScale, 0f);
		intTextObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // +90° = texto legible desde -X (centro cabina)
		intTextObj.transform.localScale = new Vector3(mapScale * 0.48f, mapScale * 0.48f, 1f);
		
		TextMesh intTM = intTextObj.AddComponent<TextMesh>();
		intTM.text = "S";
		intTM.fontSize = 64;
		intTM.characterSize = 0.014f;
		
		Renderer intRend = intTextObj.GetComponent<Renderer>();
		Material intTextMat = new Material(Shader.Find("Sprites/Default"));
		intTextMat.mainTexture = intTM.font.material.mainTexture;
		intTextMat.color = new Color(0.2f, 1f, 0.3f);
		intRend.sharedMaterial = intTextMat;
		intTM.color = new Color(0.2f, 1f, 0.3f);
		intTM.alignment = TextAlignment.Center;
		intTM.anchor = TextAnchor.MiddleCenter;
		intTM.fontStyle = FontStyle.Bold;

		// I. Puertas deslizantes
		GameObject lDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		lDoor.name = "Elevator_LeftDoor";
		lDoor.transform.SetParent(gameObject.transform, false);
		lDoor.transform.localPosition = new Vector3(-0.25f * tileSize, innerHeight / 2f, 0.488f * tileSize);
		lDoor.transform.localScale = new Vector3(0.5f * tileSize, innerHeight, thickness);
		lDoor.GetComponent<Renderer>().sharedMaterial = puertaMat;

		GameObject rDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rDoor.name = "Elevator_RightDoor";
		rDoor.transform.SetParent(gameObject.transform, false);
		rDoor.transform.localPosition = new Vector3(0.25f * tileSize, innerHeight / 2f, 0.488f * tileSize);
		rDoor.transform.localScale = new Vector3(0.5f * tileSize, innerHeight, thickness);
		rDoor.GetComponent<Renderer>().sharedMaterial = puertaMat;

		// J. Paredes frontales de sellado
		float ceilingWorldHeight = 3.2f * mapScale; // 9.6f
		float gapHeight = ceilingWorldHeight - innerHeight;

		GameObject frontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
		frontLeft.name = "ParedFrontalIzquierda";
		frontLeft.transform.SetParent(gameObject.transform, false);
		frontLeft.transform.localPosition = new Vector3(-0.375f * tileSize, ceilingWorldHeight / 2f, 0.49f * tileSize);
		frontLeft.transform.localScale = new Vector3(0.25f * tileSize, ceilingWorldHeight, thickness);
		frontLeft.GetComponent<Renderer>().sharedMaterial = cabinaMat;

		GameObject frontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
		frontRight.name = "ParedFrontalDerecha";
		frontRight.transform.SetParent(gameObject.transform, false);
		frontRight.transform.localPosition = new Vector3(0.375f * tileSize, ceilingWorldHeight / 2f, 0.49f * tileSize);
		frontRight.transform.localScale = new Vector3(0.25f * tileSize, ceilingWorldHeight, thickness);
		frontRight.GetComponent<Renderer>().sharedMaterial = cabinaMat;

		if (gapHeight > 0.05f)
		{
			GameObject sealWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			sealWall.name = "ParedSelladoTecho";
			sealWall.transform.SetParent(gameObject.transform, false);
			sealWall.transform.localPosition = new Vector3(0f, innerHeight + gapHeight / 2f, 0.49f * tileSize);
			sealWall.transform.localScale = new Vector3(0.5f * tileSize, gapHeight, thickness);
			sealWall.GetComponent<Renderer>().sharedMaterial = cabinaMat;
		}

		// K. Umbral metálico (Placa de piso que tapa el hueco entre el ascensor y el pasillo)
		GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
		threshold.name = "Elevator_Threshold";
		threshold.transform.SetParent(gameObject.transform, false);
		threshold.transform.localPosition = new Vector3(0f, 0.02f * mapScale, 0.495f * tileSize);
		threshold.transform.localScale = new Vector3(0.5f * tileSize, thickness * 0.5f, 0.05f * tileSize);
		threshold.GetComponent<Renderer>().sharedMaterial = puertaMat;

		// Registrar componentes en el controlador
		var controller = gameObject.AddComponent<ArrivalElevatorController>();
		controller.leftDoor = lDoor.transform;
		controller.rightDoor = rDoor.transform;
		controller.mapScale = mapScale;
	}

	private void ApplyMaterialToAllRenderers(GameObject obj, Material mat)
	{
		if (!(obj == null) && !(mat == null))
		{
			Renderer[] componentsInChildren = obj.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].sharedMaterial = mat;
			}
		}
	}

	private void Update()
	{
		// Si existe PauseMenuManager en la escena, delegarle toda la pausa
		if (FindAnyObjectByType<PauseMenuManager>() != null)
		{
			return;
		}

		if (Input.GetKeyDown(KeyCode.Escape))
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
		if (leverArmObj != null)
		{
			Quaternion b = ((escapeState == EscapeState.Idle) ? Quaternion.Euler(-45f, 0f, 0f) : Quaternion.Euler(45f, 0f, 0f));
			leverArmObj.transform.localRotation = Quaternion.Slerp(leverArmObj.transform.localRotation, b, Time.deltaTime * 6f);
		}
		if (escapeState == EscapeState.Idle)
		{
			if (Vector3.Distance(playerObjInstance.transform.position, consolePos) < 2.5f * mapScale)
			{
				if (TunnelsPowerOutageManager.isGlobalPowerOutage)
				{
					interactionTimer = 0f;
					return;
				}
				if (MobileInput.GetKey(KeyCode.E) || Input.GetKey(KeyCode.E))
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
						AudioSource.PlayClipAtPoint(audioClip, consolePos, 1f);
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
				interactionTimer = Mathf.MoveTowards(interactionTimer, 0f, Time.deltaTime);
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
			if (Mathf.CeilToInt(currentDrainageTime) % 4 == 0 && currentDrainageTime - Mathf.Floor(currentDrainageTime) < 0.05f)
			{
				AudioClip audioClip3 = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Error");
				if (audioClip3 == null) audioClip3 = Resources.Load<AudioClip>("Ascensor_Error");
				if (audioClip3 != null && playerObjInstance != null)
				{
					AudioSource.PlayClipAtPoint(audioClip3, playerObjInstance.transform.position, 0.45f);
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
			if (Vector3.Distance(playerObjInstance.transform.position, exitPointPos) < 3.5f * mapScale)
			{
				if (MobileInput.GetKey(KeyCode.E) || Input.GetKey(KeyCode.E))
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

	private IEnumerator HandleVictory()
	{
		float elapsed = 0f;
		float duration = 1.5f;
		float startVolume = ((pumpAudioSource != null) ? pumpAudioSource.volume : 0.85f);
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			victoryFadeAlpha = Mathf.Clamp01(elapsed / duration);
			if (pumpAudioSource != null)
			{
				pumpAudioSource.volume = Mathf.Lerp(startVolume, 0f, victoryFadeAlpha);
			}
			yield return null;
		}
		victoryFadeAlpha = 1f;
		if (pumpAudioSource != null)
		{
			pumpAudioSource.Stop();
		}
		Time.timeScale = 0f;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		AudioClip audioClip = Resources.Load<AudioClip>("Audio/Tuneles/SonidoEscape");
		if (audioClip == null) audioClip = Resources.Load<AudioClip>("SonidoEscape");
		if (audioClip != null && playerObjInstance != null)
		{
			AudioSource.PlayClipAtPoint(audioClip, playerObjInstance.transform.position, 1f);
		}
		displayWinText = true;
		yield return new WaitForSecondsRealtime(4f);
		Time.timeScale = 1f;
		SceneManager.LoadScene("MainMenu");
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

	private void InitPauseBgTexture()
	{
		pauseBgTex = new Texture2D(2, 2);
		Color color = new Color(0f, 0f, 0f, 0.7f);
		pauseBgTex.SetPixel(0, 0, color);
		pauseBgTex.SetPixel(0, 1, color);
		pauseBgTex.SetPixel(1, 0, color);
		pauseBgTex.SetPixel(1, 1, color);
		pauseBgTex.Apply();
	}

	private void OnGUI()
	{
		if (exitReached)
		{
			if (fadeBlackTex == null)
			{
				fadeBlackTex = MakeTex(2, 2, Color.black);
			}
			GUI.color = new Color(1f, 1f, 1f, victoryFadeAlpha);
			GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeBlackTex);
			GUI.color = Color.white;
			if (displayWinText)
			{
				GUIStyle gUIStyle = new GUIStyle(GUI.skin.label);
				gUIStyle.fontSize = 50;
				gUIStyle.fontStyle = FontStyle.Bold;
				gUIStyle.normal.textColor = Color.white;
				gUIStyle.alignment = TextAnchor.MiddleCenter;

				string winMsg = "JUEGO TERMINADO";
				if (LocalizationManager.Instance != null)
				{
					var curLang = LocalizationManager.Instance.GetIdiomaActual();
					if (curLang == LocalizationManager.Idioma.ENGLISH) winMsg = "GAME COMPLETED";
					else if (curLang == LocalizationManager.Idioma.PORTUGUES) winMsg = "JOGO CONCLUÍDO";
				}
				GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), winMsg, gUIStyle);
			}
		}
		else if (!isPaused)
		{
			if (escapeState == EscapeState.Draining)
			{
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
				float num = 330f;
				float num2 = 135f;
				float num3 = (float)Screen.width - num - 20f;
				float num4 = 80f;
				GUI.DrawTexture(new Rect(num3, num4, num, num2), alarmBgTex);
				GUI.DrawTexture(new Rect(num3, num4, num, 3f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num3, num4 + num2 - 3f, num, 3f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num3, num4, 3f, num2), alarmBorderTex);
				GUI.DrawTexture(new Rect(num3 + num - 3f, num4, 3f, num2), alarmBorderTex);
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 15,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = Color.red
					},
					alignment = TextAnchor.MiddleLeft
				}, text: ((Time.time % 0.8f < 0.4f) ? "⚠\ufe0f" : "  ") + " ALARMA DE SISTEMA", position: new Rect(num3 + 15f, num4 + 10f, num - 30f, 25f));
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = new Color(1f, 0.6f, 0.6f, 0.8f)
					},
					alignment = TextAnchor.MiddleRight
				}, position: new Rect(num3 + 15f, num4 + 28f, num - 30f, 25f), text: "BOMBA HIDRÁULICA ACTIVA");
				float num5 = num - 30f;
				float num6 = 16f;
				float num7 = num3 + 15f;
				float num8 = num4 + 52f;
				GUI.DrawTexture(new Rect(num7, num8, num5, num6), progressRemainingTex);
				float num9 = currentDrainageTime / drainageDuration;
				GUI.DrawTexture(new Rect(num7, num8, num5 * num9, num6), alarmProgressTex);
				GUI.DrawTexture(new Rect(num7, num8, num5, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num7, num8 + num6 - 1f, num5, 1f), alarmBorderTex);
				GUI.DrawTexture(new Rect(num7, num8, 1f, num6), alarmBorderTex);
				GUI.DrawTexture(new Rect(num7 + num5 - 1f, num8, 1f, num6), alarmBorderTex);
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 13,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = Color.white
					},
					alignment = TextAnchor.MiddleLeft
				}, text: "EVACUANDO AGUA" + ((Time.time % 1.2f < 0.4f) ? "." : ((Time.time % 1.2f < 0.8f) ? ".." : "...")), position: new Rect(num3 + 15f, num4 + 80f, num - 30f, 25f));
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 13,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = Color.red
					},
					alignment = TextAnchor.MiddleRight
				}, position: new Rect(num3 + 15f, num4 + 80f, num - 30f, 25f), text: $"{Mathf.CeilToInt(currentDrainageTime)}s RESTANTES");
				GUI.Label(style: new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					fontStyle = FontStyle.Italic,
					normal = 
					{
						textColor = new Color(1f, 0.3f, 0.3f, 0.9f)
					},
					alignment = TextAnchor.MiddleCenter
				}, position: new Rect(num3 + 15f, num4 + 108f, num - 30f, 20f), text: "⚠\ufe0f ACTIVIDAD PARANORMAL DETECTADA: INFESTACIÓN ⚠\ufe0f");
			}
			else if (escapeState == EscapeState.Idle && playerObjInstance != null)
			{
				if (!(Vector3.Distance(playerObjInstance.transform.position, consolePos) < 2.5f * mapScale))
				{
					return;
				}
				if (TunnelsPowerOutageManager.isGlobalPowerOutage)
				{
					GUIStyle gUIStyle2 = new GUIStyle(GUI.skin.label);
					gUIStyle2.fontSize = 22;
					gUIStyle2.fontStyle = FontStyle.Bold;
					gUIStyle2.normal.textColor = Color.red;
					gUIStyle2.alignment = TextAnchor.MiddleCenter;
					string text = "⚠\ufe0f CONSOLA SIN ENERGÍA: RESTAURA LA CORRIENTE PRIMERO ⚠\ufe0f";
					GUI.Label(new Rect(0f, (float)Screen.height * 0.65f, Screen.width, 60f), text, gUIStyle2);
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
			else if (escapeState == EscapeState.Ready && playerObjInstance != null && Vector3.Distance(playerObjInstance.transform.position, exitPointPos) < 3.5f * mapScale)
			{
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
				GUI.DrawTexture(new Rect(num25 + num23 - 1f, num26, 1f, num24), alarmBorderTex);
				GUIStyle gUIStyle4 = new GUIStyle(GUI.skin.label);
				gUIStyle4.fontSize = 15;
				gUIStyle4.fontStyle = FontStyle.Bold;
				gUIStyle4.normal.textColor = Color.green;
				gUIStyle4.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(0f, num26 + num24 + 8f, Screen.width, 30f), "MANTÉN PRESIONADO 'E' PARA ESCAPAR POR LA ESCOTILLA", gUIStyle4);
			}
		}
	}

	private void SpawnWallPipe(GameObject parent, Vector3 localPos, Quaternion localRot, string wallDir)
	{
		Random.State state = Random.state;
		int num = 0;
		num = ((!(wallDir == "West") && !(wallDir == "East")) ? ((int)(Mathf.Abs(localPos.z) * 104729f)) : ((int)(Mathf.Abs(localPos.x) * 7919f)));
		Random.InitState(num);
		float value = Random.value;
		float value2 = Random.value;
		int num2 = ((value2 < 0.4f) ? 2 : ((value2 < 0.6f) ? 3 : ((value2 < 0.8f) ? 1 : 0)));
		Random.state = state;
		if (value > wallPipeSpawnProbability)
		{
			return;
		}
		if (wallPipePrefab != null)
		{
			GameObject obj = Object.Instantiate(wallPipePrefab, parent.transform);
			obj.name = "WallPipePrefab_" + wallDir;
			obj.transform.localPosition = localPos;
			obj.transform.localRotation = localRot * Quaternion.Euler(wallPipeRotation);
			obj.transform.localScale = Vector3.Scale(wallPipeScale, new Vector3(1f, 1f, mapScale));
			Collider[] componentsInChildren = obj.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			return;
		}
		Quaternion identity = Quaternion.identity;
		identity = ((!(wallDir == "West") && !(wallDir == "East")) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f));
		float num3 = 0.08f * mapScale;
		float num4 = segmentLength * mapScale;
		switch (num2)
		{
		case 0:
		{
			GameObject gameObject3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject3.name = "WallPipeProcedural_Single_" + wallDir;
			gameObject3.transform.SetParent(parent.transform);
			gameObject3.transform.localPosition = localPos;
			gameObject3.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject3.transform.localScale = new Vector3(num3, num4 / 2f, num3);
			if (wallPipeMaterial != null)
			{
				gameObject3.GetComponent<Renderer>().material = wallPipeMaterial;
			}
			Collider component3 = gameObject3.GetComponent<Collider>();
			if (component3 != null)
			{
				component3.enabled = false;
			}
			break;
		}
		case 1:
		{
			GameObject gameObject4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject4.name = "WallPipeProcedural_Double1_" + wallDir;
			gameObject4.transform.SetParent(parent.transform);
			gameObject4.transform.localPosition = localPos + new Vector3(0f, 0.12f * mapScale, 0f);
			gameObject4.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject4.transform.localScale = new Vector3(num3 * 0.9f, num4 / 2f, num3 * 0.9f);
			GameObject gameObject5 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject5.name = "WallPipeProcedural_Double2_" + wallDir;
			gameObject5.transform.SetParent(parent.transform);
			gameObject5.transform.localPosition = localPos - new Vector3(0f, 0.12f * mapScale, 0f);
			gameObject5.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject5.transform.localScale = new Vector3(num3 * 0.9f, num4 / 2f, num3 * 0.9f);
			if (wallPipeMaterial != null)
			{
				gameObject4.GetComponent<Renderer>().material = wallPipeMaterial;
				gameObject5.GetComponent<Renderer>().material = wallPipeMaterial;
			}
			Collider component4 = gameObject4.GetComponent<Collider>();
			if (component4 != null)
			{
				component4.enabled = false;
			}
			Collider component5 = gameObject5.GetComponent<Collider>();
			if (component5 != null)
			{
				component5.enabled = false;
			}
			break;
		}
		case 2:
		{
			GameObject gameObject6 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject6.name = "WallPipeProcedural_DripMain_" + wallDir;
			gameObject6.transform.SetParent(parent.transform);
			gameObject6.transform.localPosition = localPos;
			gameObject6.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject6.transform.localScale = new Vector3(num3, num4 / 2f, num3);
			if (wallPipeMaterial != null)
			{
				gameObject6.GetComponent<Renderer>().material = wallPipeMaterial;
			}
			Collider component6 = gameObject6.GetComponent<Collider>();
			if (component6 != null)
			{
				component6.enabled = false;
			}
			GameObject gameObject7 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject7.name = "WallPipeProcedural_DripVertical_" + wallDir;
			gameObject7.transform.SetParent(parent.transform);
			float y = localPos.y;
			if (wallDir == "West" || wallDir == "East")
			{
				gameObject7.transform.localPosition = new Vector3(localPos.x, y / 2f, 0f);
			}
			else
			{
				gameObject7.transform.localPosition = new Vector3(0f, y / 2f, localPos.z);
			}
			gameObject7.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
			gameObject7.transform.localScale = new Vector3(num3 * 1.1f, y / 2f, num3 * 1.1f);
			if (wallPipeMaterial != null)
			{
				gameObject7.GetComponent<Renderer>().material = wallPipeMaterial;
			}
			Collider component7 = gameObject7.GetComponent<Collider>();
			if (component7 != null)
			{
				component7.enabled = true;
			}
			GameObject gameObject8 = GameObject.CreatePrimitive(PrimitiveType.Quad);
			gameObject8.name = "WaterPuddle_" + wallDir;
			gameObject8.transform.SetParent(parent.transform);
			float num6 = 0.85f;
			Vector3 zero = Vector3.zero;
			zero = ((!(wallDir == "West") && !(wallDir == "East")) ? new Vector3(0f, 0.005f, localPos.z * num6) : new Vector3(localPos.x * num6, 0.005f, 0f));
			if (Physics.Raycast(parent.transform.TransformPoint(new Vector3(zero.x, y, zero.z)), Vector3.down, out var hitInfo, y + 2f, -5, QueryTriggerInteraction.Ignore))
			{
				zero.y = parent.transform.InverseTransformPoint(hitInfo.point).y + 0.005f;
			}
			gameObject8.transform.localPosition = zero;
			gameObject8.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			gameObject8.transform.localScale = new Vector3(0.9f * mapScale, 0.9f * mapScale, 1f);
			if (waterPuddleMaterial != null)
			{
				gameObject8.GetComponent<Renderer>().material = waterPuddleMaterial;
			}
			Collider component8 = gameObject8.GetComponent<Collider>();
			if (component8 != null)
			{
				Object.Destroy(component8);
			}
			BoxCollider boxCollider = gameObject8.AddComponent<BoxCollider>();
			boxCollider.isTrigger = true;
			boxCollider.size = new Vector3(1.2f, 1.2f, 0.3f);
			boxCollider.center = new Vector3(0f, 0f, 0f);
			gameObject8.AddComponent<WaterPuddle>();
			GameObject obj2 = new GameObject("DripPoint");
			obj2.transform.SetParent(gameObject7.transform);
			obj2.transform.localPosition = new Vector3(0f, 0.95f, 0f);
			obj2.transform.localRotation = Quaternion.identity;
			WaterDrip waterDrip = obj2.AddComponent<WaterDrip>();
			waterDrip.dripInterval = Random.Range(0.35f, 0.6f);
			waterDrip.waterMaterial = waterPuddleMaterial;
			break;
		}
		default:
		{
			float num5 = 0.22f * mapScale;
			Vector3 vector = Vector3.zero;
			switch (wallDir)
			{
			case "West":
				vector = Vector3.right;
				break;
			case "East":
				vector = Vector3.left;
				break;
			case "South":
				vector = Vector3.forward;
				break;
			case "North":
				vector = Vector3.back;
				break;
			}
			Vector3 vector2 = localPos + vector * (num5 * 0.4f);
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject.name = "WallPipeProcedural_BigUpper_" + wallDir;
			gameObject.transform.SetParent(parent.transform);
			gameObject.transform.localPosition = vector2 + new Vector3(0f, 0.42f * mapScale, 0f);
			gameObject.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject.transform.localScale = new Vector3(num5, num4 / 2f, num5);
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			gameObject2.name = "WallPipeProcedural_BigLower_" + wallDir;
			gameObject2.transform.SetParent(parent.transform);
			gameObject2.transform.localPosition = vector2 - new Vector3(0f, 0.42f * mapScale, 0f);
			gameObject2.transform.localRotation = identity * Quaternion.Euler(wallPipeRotation);
			gameObject2.transform.localScale = new Vector3(num5, num4 / 2f, num5);
			if (wallPipeMaterial != null)
			{
				gameObject.GetComponent<Renderer>().material = wallPipeMaterial;
				gameObject2.GetComponent<Renderer>().material = wallPipeMaterial;
			}
			Collider component = gameObject.GetComponent<Collider>();
			if (component != null)
			{
				component.enabled = false;
			}
			Collider component2 = gameObject2.GetComponent<Collider>();
			if (component2 != null)
			{
				component2.enabled = false;
			}
			break;
		}
		}
	}

	private void DecorateFillerWall(GameObject fWall, Vector3 scale, string nameTag)
	{
		if (Random.value > 0.65f)
		{
			return;
		}
		bool num = scale.x > scale.z;
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		gameObject.name = "Industrial_ElectricBox";
		gameObject.transform.SetParent(fWall.transform);
		if (num)
		{
			float z = (nameTag.Contains("North") ? (-0.52f) : 0.52f);
			gameObject.transform.localPosition = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f), z);
			gameObject.transform.localScale = new Vector3(0.8f / scale.x, 1f / scale.y, 0.3f / scale.z);
			gameObject.transform.localRotation = Quaternion.identity;
		}
		else
		{
			float x = (nameTag.Contains("East") ? (-0.52f) : 0.52f);
			gameObject.transform.localPosition = new Vector3(x, Random.Range(-0.1f, 0.1f), Random.Range(-0.2f, 0.2f));
			gameObject.transform.localScale = new Vector3(0.3f / scale.x, 1f / scale.y, 0.8f / scale.z);
			gameObject.transform.localRotation = Quaternion.identity;
		}
		Renderer component = gameObject.GetComponent<Renderer>();
		if (component != null)
		{
			component.material.color = new Color(0.32f, 0.33f, 0.35f);
			if (archMaterial != null)
			{
				component.material = archMaterial;
			}
		}
		Collider component2 = gameObject.GetComponent<Collider>();
		if (component2 != null)
		{
			Object.DestroyImmediate(component2);
		}
		GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		gameObject2.name = "Red_Indicator_Light";
		gameObject2.transform.SetParent(gameObject.transform);
		if (num)
		{
			gameObject2.transform.localPosition = new Vector3(0.2f, 0.3f, nameTag.Contains("North") ? (-0.55f) : 0.55f);
		}
		else
		{
			gameObject2.transform.localPosition = new Vector3(nameTag.Contains("East") ? (-0.55f) : 0.55f, 0.3f, 0.2f);
		}
		gameObject2.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
		Collider component3 = gameObject2.GetComponent<Collider>();
		if (component3 != null)
		{
			Object.DestroyImmediate(component3);
		}
		Renderer component4 = gameObject2.GetComponent<Renderer>();
		if (component4 != null)
		{
			Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
			if (litShader == null) litShader = Shader.Find("Standard");
			Material indicatorMat = new Material(litShader);
			indicatorMat.name = "M_RedIndicator";
			indicatorMat.color = Color.red;
			indicatorMat.EnableKeyword("_EMISSION");
			indicatorMat.SetColor("_EmissionColor", Color.red * 2.5f);
			component4.sharedMaterial = indicatorMat;
		}
		gameObject2.AddComponent<TunnelsLightBlink>().blinkSpeed = Random.Range(0.8f, 2.2f);
		GameObject gameObject3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		gameObject3.name = "Vertical_Conduit_Pipe";
		gameObject3.transform.SetParent(fWall.transform);
		if (num)
		{
			float z2 = (nameTag.Contains("North") ? (-0.52f) : 0.52f);
			gameObject3.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), 0f, z2);
			gameObject3.transform.localScale = new Vector3(0.15f / scale.x, 1f, 0.15f / scale.z);
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
		}
		else
		{
			float x2 = (nameTag.Contains("East") ? (-0.52f) : 0.52f);
			gameObject3.transform.localPosition = new Vector3(x2, 0f, Random.Range(-0.4f, 0.4f));
			gameObject3.transform.localScale = new Vector3(0.15f / scale.x, 1f, 0.15f / scale.z);
			gameObject3.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
		}
		Renderer component5 = gameObject3.GetComponent<Renderer>();
		if (component5 != null && archMaterial != null)
		{
			component5.material = archMaterial;
		}
		Collider component6 = gameObject3.GetComponent<Collider>();
		if (component6 != null)
		{
			Object.DestroyImmediate(component6);
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

	private IEnumerator ReenableMonoAfterFrame(MonoBehaviour mono)
	{
		yield return null;
		if (mono != null)
		{
			mono.enabled = true;
		}
	}

	private void ResetCinemachineRotation(Quaternion targetRotation)
	{
		float targetYaw = targetRotation.eulerAngles.y;
		
		// Encontrar todos los componentes de tipo Cinemachine en la escena (activos e inactivos, excluyendo prefabs)
		foreach (MonoBehaviour mono in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
		{
			if (mono == null || mono.gameObject == null || mono.gameObject.scene.name == null) continue;
			string typeName = mono.GetType().FullName;
			if (typeName.Contains("Cinemachine"))
			{
				// --- Soporte para Cinemachine v2 (m_HorizontalAxis / m_VerticalAxis) ---
				var horizontalAxisField = mono.GetType().GetField("m_HorizontalAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var verticalAxisField = mono.GetType().GetField("m_VerticalAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				
				if (horizontalAxisField != null)
				{
					object horizAxis = horizontalAxisField.GetValue(mono);
					if (horizAxis != null)
					{
						var valueField = horizAxis.GetType().GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						if (valueField != null)
						{
							valueField.SetValue(horizAxis, targetYaw);
							horizontalAxisField.SetValue(mono, horizAxis);
							UnityEngine.Debug.Log($"[TunnelsGenerator] Cinemachine v2 POV horizontal axis (Yaw) forzado a {targetYaw} vía Reflexión.");
						}
					}
				}
				if (verticalAxisField != null)
				{
					object vertAxis = verticalAxisField.GetValue(mono);
					if (vertAxis != null)
					{
						var valueField = vertAxis.GetType().GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						if (valueField != null)
						{
							valueField.SetValue(vertAxis, 0f);
							verticalAxisField.SetValue(mono, vertAxis);
							UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v2 POV vertical axis (Pitch) forzado a 0 vía Reflexión.");
						}
					}
				}

				// --- Soporte para Cinemachine v3 (PanAngle / TiltAngle / Pan / Tilt) ---
				var panAngleProp = mono.GetType().GetProperty("PanAngle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var tiltAngleProp = mono.GetType().GetProperty("TiltAngle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				
				if (panAngleProp != null && panAngleProp.CanWrite)
				{
					panAngleProp.SetValue(mono, targetYaw);
					UnityEngine.Debug.Log($"[TunnelsGenerator] Cinemachine v3 PanAngle (propiedad) forzado a {targetYaw} vía Reflexión.");
				}
				if (tiltAngleProp != null && tiltAngleProp.CanWrite)
				{
					tiltAngleProp.SetValue(mono, 0f);
					UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v3 TiltAngle (propiedad) forzado a 0 vía Reflexión.");
				}

				var panProp = mono.GetType().GetProperty("Pan", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var tiltProp = mono.GetType().GetProperty("Tilt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				
				if (panProp != null && panProp.CanWrite)
				{
					panProp.SetValue(mono, targetYaw);
					UnityEngine.Debug.Log($"[TunnelsGenerator] Cinemachine v3 Pan (propiedad) forzado a {targetYaw} vía Reflexión.");
				}
				if (tiltProp != null && tiltProp.CanWrite)
				{
					tiltProp.SetValue(mono, 0f);
					UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v3 Tilt (propiedad) forzado a 0 vía Reflexión.");
				}

				var panAngleField = mono.GetType().GetField("PanAngle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var tiltAngleField = mono.GetType().GetField("TiltAngle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				
				if (panAngleField != null)
				{
					panAngleField.SetValue(mono, targetYaw);
					UnityEngine.Debug.Log($"[TunnelsGenerator] Cinemachine v3 PanAngle (campo) forzado a {targetYaw} vía Reflexión.");
				}
				if (tiltAngleField != null)
				{
					tiltAngleField.SetValue(mono, 0f);
					UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v3 TiltAngle (campo) forzado a 0 vía Reflexión.");
				}

				var forceMethodv3 = mono.GetType().GetMethod("ForceCameraPositionAndRotation", new System.Type[] { typeof(Vector3), typeof(Quaternion) });
				if (forceMethodv3 != null)
				{
					forceMethodv3.Invoke(mono, new object[] { mono.transform.position, targetRotation });
					UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v3 ForceCameraPositionAndRotation invocado vía Reflexión.");
				}

				var forceMethodv2 = mono.GetType().GetMethod("ForceCameraPosition", new System.Type[] { typeof(Vector3), typeof(Quaternion) });
				if (forceMethodv2 != null)
				{
					forceMethodv2.Invoke(mono, new object[] { mono.transform.position, targetRotation });
					UnityEngine.Debug.Log("[TunnelsGenerator] Cinemachine v2 ForceCameraPosition invocado vía Reflexión.");
				}
			}
		}
	}
}

