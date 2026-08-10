using UnityEngine;

public partial class TunnelsGenerator
{
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

	private void SpawnCeilingLight(Transform cellRoot, bool isCurrentlyOn)
	{
		if (ceilingLightPrefab == null)
		{
			return;
		}

		// Guía visual de orientación por color de luz:
		Color finalLightColor = lightColor;
		Vector3 worldCellPos = cellRoot.position;
		float distToElevator = Vector3.Distance(worldCellPos, playerSpawnPos);
		float distToConsole = Vector3.Distance(worldCellPos, consolePos);

		if (distToElevator < 14f * mapScale)
		{
			finalLightColor = new Color(1.0f, 0.75f, 0.35f); // Ámbar cálido para zona del Elevador
		}
		else if (distToConsole < 18f * mapScale)
		{
			finalLightColor = new Color(1.0f, 0.25f, 0.15f); // Rojo industrial de advertencia para zona del Generador / Bomba
		}
		else
		{
			int gridX = Mathf.RoundToInt(cellRoot.localPosition.x / (segmentLength * mapScale));
			int gridZ = Mathf.RoundToInt(cellRoot.localPosition.z / (segmentLength * mapScale));
			if (gridX == width / 2 || gridZ == height / 2)
			{
				finalLightColor = new Color(0.4f, 0.88f, 0.72f); // Verde/Cian industrial para pasillos cruzados principales
			}
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
			light.color = finalLightColor;
			light.range = lightRange * mapScale;
			light.intensity = lightIntensity * 5f;
			light.shadows = LightShadows.Soft;
		}
		else
		{
			light.type = LightType.Point;
			light.transform.localPosition = new Vector3(0f, -0.5f * mapScale, 0f);
			light.color = finalLightColor;
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
				material.SetColor("_EmissionColor", finalLightColor * 2f);
				material.color = finalLightColor;
			}
			else
			{
				material.DisableKeyword("_EMISSION");
				material.SetColor("_EmissionColor", Color.black);
				material.color = Color.gray;

				// En lámparas apagadas de pasillos oscuros, agregar destellos de chispas guía
				gameObject.AddComponent<TunnelElectricSparks>();
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

		// Método helper local para aplicar material de tubos con UV Tiling corregido (sin estiramiento)
		void ApplyPipeMaterialToRenderer(Renderer rend, float pipeLength)
		{
			if (rend == null || wallPipeMaterial == null) return;
			Material instMat = rend.material = new Material(wallPipeMaterial);
			// Escalar UVs en Y proporcional al largo del tubo (1.2 repetidores por metro)
			instMat.mainTextureScale = new Vector2(1f, Mathf.Max(1f, pipeLength * 1.2f));
		}

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
			ApplyPipeMaterialToRenderer(gameObject3.GetComponent<Renderer>(), num4);
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
			ApplyPipeMaterialToRenderer(gameObject4.GetComponent<Renderer>(), num4);
			ApplyPipeMaterialToRenderer(gameObject5.GetComponent<Renderer>(), num4);
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
			ApplyPipeMaterialToRenderer(gameObject6.GetComponent<Renderer>(), num4);
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
			ApplyPipeMaterialToRenderer(gameObject7.GetComponent<Renderer>(), y);
			Collider component7 = gameObject7.GetComponent<Collider>();
			if (component7 != null)
			{
				component7.enabled = true;
			}
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
}
