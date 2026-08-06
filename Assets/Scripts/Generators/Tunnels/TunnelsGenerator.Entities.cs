using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public partial class TunnelsGenerator
{
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
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
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

	private void SpawnEntities()
	{
		_ = segmentLength;
		_ = mapScale;
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");

		// Determinar la celda del jugador en el grid
		float num = segmentLength * mapScale;
		int num2 = Mathf.RoundToInt(playerSpawnPos.x / num);
		int num3 = Mathf.RoundToInt(playerSpawnPos.z / num);

		// Detectar la dirección real del pasillo abierto evaluando los vecinos del grid
		Vector3 openDirection = Vector3.forward;
		List<Vector3> validDirs = new List<Vector3>();

		if (num3 + 1 < height && grid[num2, num3 + 1]) validDirs.Add(Vector3.forward);
		if (num3 - 1 >= 0 && grid[num2, num3 - 1]) validDirs.Add(Vector3.back);
		if (num2 + 1 < width && grid[num2 + 1, num3]) validDirs.Add(Vector3.right);
		if (num2 - 1 >= 0 && grid[num2 - 1, num3]) validDirs.Add(Vector3.left);

		if (validDirs.Count > 0)
		{
			openDirection = validDirs[0];
		}

		// En el Prefab del Hospital, las puertas deslizantes se encuentran a -90° respecto al frontal del modelo
		Quaternion elevatorRot = Quaternion.LookRotation(openDirection) * Quaternion.Euler(0f, -90f, 0f);
		Quaternion playerRot = Quaternion.LookRotation(openDirection);

		// Posición exacta centrada en la celda del grid
		Vector3 exactCellCenter = new Vector3((float)num2 * num, 0.05f * mapScale, (float)num3 * num);
		Vector3 spawnPos = exactCellCenter;

		// El jugador se coloca en el centro exacto de la cabina
		Vector3 vector = new Vector3(spawnPos.x, spawnPos.y + 0.12f, spawnPos.z);

		// Guardar datos de la celda del jugador para el validador
		playerSpawnGridX = num2;
		playerSpawnGridZ = num3;
		playerSpawnCellSize = num;
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
				gameObject = Object.Instantiate(playerPrefab, vector, playerRot);
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
			playerRootObj.transform.rotation = playerRot * Quaternion.Inverse(localRotRelation);

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
			ResetCinemachineRotation(playerRot);

			// Resetear el pitch interno del FirstPersonController usando reflexión
			var fpc = gameObject.GetComponentInChildren<StarterAssets.FirstPersonController>();
			if (fpc != null)
			{
				var pitchField = fpc.GetType().GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (pitchField != null)
				{
					pitchField.SetValue(fpc, 0f);
				}
			}
		}

		if (gameObject != null && cc != null)
		{
			Physics.SyncTransforms();
			cc.enabled = true;
			Physics.SyncTransforms();
		}
		playerObjInstance = gameObject;

		if (playerObjInstance != null)
		{
			if (GameManager.Instance == null)
			{
				GameObject gmObj = new GameObject("GameManager");
				gmObj.AddComponent<GameManager>();
			}

			int vidasTunnels = 3;
			if (mapScale >= 1.5f) vidasTunnels = 5;
			else if (mapScale >= 1.2f) vidasTunnels = 4;
			
			GameManager.Instance.InicializarVidasParaMapa(vidasTunnels);
			GameManager.Instance.RegistrarSpawnJugador(vector, playerRot);
		}

		SpawnArrivalElevator(spawnPos, elevatorRot);

		// Guardar la posición y rotación de spawn del jugador para reposicionamiento post-física
		playerSpawnTargetPos = vector;
		playerSpawnTargetRot = playerRot;
		StartCoroutine(ForcePlayerPositionAfterPhysics(gameObject, vector, playerRot));
		GameObject gameObject2 = null;
		PhenomenonAIController phenomenonAIController = Object.FindObjectOfType<PhenomenonAIController>();
		if (phenomenonAIController != null)
		{
			gameObject2 = phenomenonAIController.gameObject;
		}
		float num4 = 0.2f * mapScale;
		int index = Mathf.Clamp((int)((float)patrolPoints.Count * enemySpawnDistancePercent), 0, patrolPoints.Count - 1);
		Vector3 vector2 = patrolPoints[index] + Vector3.up * num4;
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
			phenomenonAIController = gameObject2.GetComponent<PhenomenonAIController>();
		}

		if (phenomenonAIController != null)
		{
			gameObject2.transform.localScale = Vector3.one * mapScale * 1.8f;
			StartCoroutine(ActivatePhenomenonHuntAfterDelay(phenomenonAIController, (gameObject != null) ? gameObject.transform : null, 120f));
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

		// === GENERAR 3 NOTAS DE LORE EN LOS TÚNELES ===
		SpawnTunnelLoreNotes();

		// Disparar monólogo inicial narrativo adaptado al personaje seleccionado (Ethan o Nora)
		LevelIntroData.TriggerStartMonologue("tunnels");
	}

	private IEnumerator ActivatePhenomenonHuntAfterDelay(PhenomenonAIController ai, Transform playerTarget, float delaySeconds)
	{
		if (ai != null)
		{
			// Guardar el rango de detección original y reducirlo a 0 para impedir teletransporte y acecho
			float originalDetectionRange = ai.detectionRange;

			ai.detectionRange = 0f;
			ai.player = null;

			UnityEngine.Debug.Log($"[TunnelsGenerator] El Fenómeno en modo pacífico sin teletransporte por {delaySeconds}s.");
			yield return new WaitForSeconds(delaySeconds);

			if (ai != null)
			{
				ai.detectionRange = originalDetectionRange > 0f ? originalDetectionRange : 15f;
				ai.player = playerTarget;
				UnityEngine.Debug.Log("[TunnelsGenerator] ¡Comportamiento tipo Slender (Teletransporte y Acecho) ACTIVADO!");
			}
		}
	}

	private void SpawnArrivalElevator(Vector3 spawnPos, Quaternion spawnRot)
	{
		// Destruir cualquier cabina existente para evitar duplicaciones (incluyendo inactivas)
		foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (go != null && (go.name == "ArrivalElevatorCabin" || go.name.StartsWith("ArrivalElevatorPrefab")) && go.scene.name != null)
			{
				UnityEngine.Debug.LogWarning("[TunnelsGenerator] Se detectó una cabina de ascensor existente (activa o inactiva). Destruyéndola para evitar duplicados.");
				Object.DestroyImmediate(go);
			}
		}

		if (arrivalElevatorPrefab != null)
		{
			GameObject prefabInst = Object.Instantiate(arrivalElevatorPrefab, spawnPos, spawnRot, navMeshHolder.transform);
			prefabInst.name = "ArrivalElevatorCabin";
			prefabInst.transform.localScale = Vector3.one * mapScale;

			// DESACTIVAR/DESTRUIR EL CASCARÓN EXTERIOR (Module_CulDeSac) DEL PREFAB DEL HOSPITAL
			foreach (Transform child in prefabInst.GetComponentsInChildren<Transform>(true))
			{
				if (child != null && child.name.Contains("CulDeSac"))
				{
					Object.DestroyImmediate(child.gameObject);
					break;
				}
			}

			// CENTRAR EL MODELO BASADO EN SUS BOUNDS REALES (Corrige offsets del pivote 3D)
			Renderer[] rends = prefabInst.GetComponentsInChildren<Renderer>();
			if (rends.Length > 0)
			{
				Bounds b = rends[0].bounds;
				for (int i = 1; i < rends.Length; i++)
				{
					b.Encapsulate(rends[i].bounds);
				}

				// Offset entre el punto de spawn deseado y el centro real del modelo 3D
				Vector3 centerOffset = spawnPos - b.center;
				centerOffset.y = 0f; // Mantener la altura intacta

				// Desplazar el prefab para que el centro visual coincida exactamente con la celda del túnel
				prefabInst.transform.position += centerOffset;
			}

			// Asegurar que cuente con el componente ArrivalElevatorController
			var ctrl = prefabInst.GetComponent<ArrivalElevatorController>();
			if (ctrl == null) ctrl = prefabInst.AddComponent<ArrivalElevatorController>();
			ctrl.mapScale = mapScale;

			// Buscar referencias de puertas en cualquier nivel de jerarquía del Prefab
			if (ctrl.leftDoor == null || ctrl.rightDoor == null)
			{
				foreach (Transform t in prefabInst.GetComponentsInChildren<Transform>(true))
				{
					string tName = t.name.ToLower();
					if (ctrl.leftDoor == null && (tName.Contains("left") || tName.Contains("izq")) && tName.Contains("door"))
					{
						ctrl.leftDoor = t;
					}
					else if (ctrl.rightDoor == null && (tName.Contains("right") || tName.Contains("der")) && tName.Contains("door"))
					{
						ctrl.rightDoor = t;
					}
				}
			}

			UnityEngine.Debug.Log($"[TunnelsGenerator] Prefab de ascensor de llegada instanciado con éxito. Puertas encontradas: Izq={(ctrl.leftDoor != null)}, Der={(ctrl.rightDoor != null)}");
			return;
		}

		GameObject gameObject = new GameObject("ArrivalElevatorCabin");
		gameObject.transform.position = spawnPos;
		gameObject.transform.rotation = spawnRot;
		gameObject.transform.SetParent(navMeshHolder.transform);

		float tileSize = 2.8f * mapScale; // Tamaño perfecto y espacioso (8.4m)
		float innerHeight = 2.5f * mapScale; // Altura holgada (7.5m)
		float thickness = 0.08f * mapScale;

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
		Collider lbCol = leftBumper.GetComponent<Collider>();
		if (lbCol != null) DestroyImmediate(lbCol);

		GameObject rightBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rightBumper.name = "BumperDerecho";
		rightBumper.transform.SetParent(gameObject.transform, false);
		rightBumper.transform.localPosition = new Vector3(0.48f * tileSize, bumperHeight, 0f);
		rightBumper.transform.localScale = new Vector3(bumperSize, bumperSize * 1.5f, tileSize * 0.92f);
		rightBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;
		Collider rbCol = rightBumper.GetComponent<Collider>();
		if (rbCol != null) DestroyImmediate(rbCol);

		GameObject backBumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
		backBumper.name = "BumperTrasero";
		backBumper.transform.SetParent(gameObject.transform, false);
		backBumper.transform.localPosition = new Vector3(0f, bumperHeight, -0.48f * tileSize);
		backBumper.transform.localScale = new Vector3(tileSize * 0.92f, bumperSize, bumperSize * 1.5f);
		backBumper.GetComponent<Renderer>().sharedMaterial = bumperMat;
		Collider bbCol = backBumper.GetComponent<Collider>();
		if (bbCol != null) DestroyImmediate(bbCol);

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

	/// <summary>
	/// Espera 3 frames de física y fuerza al jugador de regreso al centro exacto del ascensor.
	/// Esto evita que el CharacterController o la física del primer frame expulse al jugador fuera.
	/// </summary>
	private IEnumerator ForcePlayerPositionAfterPhysics(GameObject playerObj, Vector3 targetPos, Quaternion targetRot)
	{
		if (playerObj == null) yield break;

		// Esperar 3 frames para que la física procese la posición inicial
		yield return null;
		yield return null;
		yield return null;

		if (playerObj == null) yield break;

		CharacterController cc = playerObj.GetComponentInChildren<CharacterController>(includeInactive: true);
		GameObject rootObj = playerObj.transform.root.gameObject;

		// Deshabilitar temporalmente para reposicionar sin resistencia de la física
		if (cc != null) cc.enabled = false;
		Physics.SyncTransforms();

		// Calcular el offset igual que en SpawnEntities
		Quaternion localRelation = Quaternion.Inverse(rootObj.transform.rotation) * playerObj.transform.rotation;
		Quaternion desiredRootRot = targetRot * Quaternion.Inverse(localRelation);
		rootObj.transform.rotation = desiredRootRot;
		Vector3 worldOffset = playerObj.transform.position - rootObj.transform.position;
		rootObj.transform.position = targetPos - worldOffset;

		Physics.SyncTransforms();
		yield return null;

		if (cc != null) cc.enabled = true;
		Physics.SyncTransforms();

		UnityEngine.Debug.Log($"[TunnelsGenerator] Post-física: Jugador reposicionado en {targetPos}. Posición real: {playerObj.transform.position}");
	}

	private IEnumerator HandleVictory()
	{
		float elapsed = 0f;
		float duration = 1.5f;
		float startVolume = ((pumpAudioSource != null) ? pumpAudioSource.volume : 0.85f);

		// 1. Fundido a negro del escenario
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

		// PASO 1: "JUEGO TERMINADO / GAME COMPLETED" (Fade in -> Mantener -> Fade out)
		victoryStep = 1;
		yield return StartCoroutine(FadeVictoryStepText(3.2f));

		// PASO 2: "¡GRACIAS POR JUGAR! / THANK YOU FOR PLAYING!"
		victoryStep = 2;
		yield return StartCoroutine(FadeVictoryStepText(3.0f));

		// PASO 3: REDES SOCIALES
		victoryStep = 3;
		yield return StartCoroutine(FadeVictoryStepText(4.5f));

		Time.timeScale = 1f;
		SceneManager.LoadScene("MainMenu");
	}

	private IEnumerator FadeVictoryStepText(float displayTime)
	{
		// Fade In del texto
		float t = 0f;
		while (t < 0.6f)
		{
			t += Time.unscaledDeltaTime;
			victoryStepAlpha = Mathf.Clamp01(t / 0.6f);
			yield return null;
		}
		victoryStepAlpha = 1f;

		// Mantener pantalla con el texto visible
		yield return new WaitForSecondsRealtime(displayTime);

		// Fade Out del texto
		t = 0f;
		while (t < 0.6f)
		{
			t += Time.unscaledDeltaTime;
			victoryStepAlpha = Mathf.Clamp01(1f - (t / 0.6f));
			yield return null;
		}
		victoryStepAlpha = 0f;
		yield return new WaitForSecondsRealtime(0.3f);
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
			if (mono == null || mono.gameObject == null || mono.gameObject.scene.name != null) continue;
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

	private void SpawnTunnelLoreNotes()
	{
		GameObject notePrefab = null;
#if UNITY_EDITOR
		notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Note.prefab");
		if (notePrefab == null) notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Papel.prefab");
		if (notePrefab == null) notePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/Prefabs/Note.prefab");
#endif

		if (patrolPoints == null || patrolPoints.Count == 0)
		{
			UnityEngine.Debug.LogWarning("[TunnelsGenerator] No hay patrolPoints para spawnear las notas de lore.");
			return;
		}

		GameObject loreRoot = new GameObject("Tunnel_LoreNotes");
		loreRoot.transform.SetParent(this.transform);

		string[] loreTitles = new string[]
		{
			"Bitácora del Operario (The Phenomenon)",
			"Informe de Incidentes - Fuga Química",
			"Nota Arrugada de Supervisor"
		};

		string[] loreBodies = new string[]
		{
			"<b>REGISTRO DE SEGURIDAD - SECTOR B-12:</b>\n\n" +
			"Hay algo más aquí abajo. No es una rata. No es una tubería rota.\n" +
			"Se teletransporta por el rabillo del ojo. Cuando lo miras de frente, parece desvanecerse...\n" +
			"Pero si te quedas inmóvil mirándolo fijamente demasiado tiempo, su presencia te consume la mente.\n" +
			"Si escuchas una estática aguda y la pantalla parpadea, ¡DATE LA VUELTA Y CORRE!\n" +
			"No dejes que se acerque.",

			"<b>EXPEDIENTE TÉCNICO DE INSTALACIONES:</b>\n\n" +
			"El sistema de drenaje principal ha sido contaminado por residuos del laboratorio de arriba.\n" +
			"Se informa de ruidos de metal doblándose y crujidos en las pasarelas (catwalks).\n" +
			"Cuidado con las chispas eléctricas expuestas; pueden dañar la linterna del camcorder.\n" +
			"Si el generador principal de los túneles se apaga por sobrecarga, usa los interruptores de los paneles eléctricos secundarios.",

			"<b>GARABATO APRESURADO:</b>\n\n" +
			"La escotilla de escape está sellada. La consola de bombeo requiere presurizar los tres tanques principales.\n" +
			"No hay energía. El interruptor principal está en la cabina del generador...\n" +
			"Pero hay una estática insoportable que se mueve por el pasillo central.\n" +
			"Si estás leyendo esto, no intentes pelear contra lo que acecha en la niebla negra. Solo corre y reza."
		};

		// Filtrar puntos para no spawnear cerca del ascensor y asegurar que estén en NavMesh y sean alcanzables (dentro del mapa)
		List<Vector3> validPoints = new List<Vector3>();
		float minDistanceToElevator = 14f * mapScale;
		UnityEngine.AI.NavMeshPath navPath = new UnityEngine.AI.NavMeshPath();

		foreach (Vector3 p in patrolPoints)
		{
			if (Vector3.Distance(p, playerSpawnPos) > minDistanceToElevator)
			{
				// Asegurar que el punto está sobre el NavMesh del túnel
				if (UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 2.0f * mapScale, UnityEngine.AI.NavMesh.AllAreas))
				{
					// Verificar si hay un camino completo desde el inicio para evitar celdas desconectadas/fuera de mapa
					if (UnityEngine.AI.NavMesh.CalculatePath(playerSpawnPos, hit.position, UnityEngine.AI.NavMesh.AllAreas, navPath))
					{
						if (navPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
						{
							validPoints.Add(hit.position);
						}
					}
				}
			}
		}

		if (validPoints.Count < 3)
		{
			// Fallback secundario si el chequeo de ruta estricta es demasiado riguroso en este layout
			validPoints.Clear();
			foreach (Vector3 p in patrolPoints)
			{
				if (Vector3.Distance(p, playerSpawnPos) > minDistanceToElevator)
				{
					if (UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 3.0f * mapScale, UnityEngine.AI.NavMesh.AllAreas))
					{
						validPoints.Add(hit.position);
					}
				}
			}
		}

		if (validPoints.Count < 3)
		{
			validPoints = new List<Vector3>(patrolPoints);
			validPoints.Sort((a, b) => Vector3.Distance(b, playerSpawnPos).CompareTo(Vector3.Distance(a, playerSpawnPos)));
		}

		int step = Mathf.Max(1, validPoints.Count / 4);
		int[] targetIndices = new int[] { step, step * 2, step * 3 };

		for (int i = 0; i < 3; i++)
		{
			int idx = targetIndices[i];
			if (idx >= validPoints.Count) idx = validPoints.Count - 1 - i;
			if (idx < 0) idx = 0;

			Vector3 basePos = validPoints[idx];
			Vector3 spawnPos = basePos; // Sin offset para evitar traspasar las estrechas pasarelas y paredes de los túneles

			Vector3 finalPos = spawnPos;
			RaycastHit hit;
			if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out hit, 5f))
			{
				finalPos = hit.point + Vector3.up * 0.015f;
			}
			else
			{
				finalPos = new Vector3(spawnPos.x, spawnPos.y + 0.015f, spawnPos.z);
			}

			GameObject noteObj;
			float scaleFactor = 1.6f * mapScale; // Ajustado para ser más grandes y visibles en los túneles

			if (notePrefab != null)
			{
				noteObj = Object.Instantiate(notePrefab, finalPos, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), loreRoot.transform);
				noteObj.transform.localScale = notePrefab.transform.localScale * scaleFactor;
			}
			else
			{
				noteObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
				noteObj.transform.position = finalPos;
				noteObj.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
				noteObj.transform.localScale = new Vector3(0.5f * scaleFactor, 0.6f * scaleFactor, 1f);
				noteObj.transform.SetParent(loreRoot.transform);
				
				Renderer rend = noteObj.GetComponent<Renderer>();
				if (rend != null)
				{
					rend.material = new Material(Shader.Find("Sprites/Default"));
					rend.material.color = new Color(0.82f, 0.68f, 0.44f, 1.0f);
				}
			}

			noteObj.name = $"[Tunnel_Lore_Note_{i + 1}]";

			NoteItem oldItem = noteObj.GetComponent<NoteItem>();
			if (oldItem != null) Object.DestroyImmediate(oldItem);

			// Configurar un BoxCollider agrandado proporcional a la escala para facilitar la interacción por raycast
			BoxCollider box = noteObj.GetComponent<BoxCollider>();
			MeshCollider meshCol = noteObj.GetComponent<MeshCollider>();
			if (meshCol != null) Object.DestroyImmediate(meshCol);

			if (box == null) box = noteObj.AddComponent<BoxCollider>();
			box.isTrigger = false;
			box.center = Vector3.zero;
			box.size = new Vector3(0.45f, 0.3f, 0.45f); // Tamaño absoluto cómodo en 3D

			LoreNoteItem loreComp = noteObj.AddComponent<LoreNoteItem>();
			loreComp.loreId = 4 + i;
			loreComp.noteTitle = loreTitles[i];
			loreComp.noteBody = loreBodies[i];
			loreComp.interactDistance = 3.5f;
		}

		UnityEngine.Debug.Log("[TunnelsGenerator] Se generaron con éxito las 3 notas de lore (IDs 4, 5, 6) en el laberinto de túneles.");
	}
}
