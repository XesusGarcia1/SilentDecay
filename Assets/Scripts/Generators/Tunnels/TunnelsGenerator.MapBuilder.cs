using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public partial class TunnelsGenerator
{
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
		// 2. Braid Connected Maze: Conectar pasillos para eliminar callejones sin salida y crear vías alternativas (40% apertura de paredes)
		for (int j = 1; j < width - 1; j++)
		{
			for (int k = 1; k < height - 1; k++)
			{
				if (grid[j, k]) continue;

				bool connectsHorizontal = (grid[j - 1, k] && grid[j + 1, k]);
				bool connectsVertical = (grid[j, k - 1] && grid[j, k + 1]);

				if ((connectsHorizontal || connectsVertical) && Random.value < 0.40f)
				{
					grid[j, k] = true;
				}
			}
		}

		// 3. Crear pasillo o cruz central principal para navegación fluida de cuadrante a cuadrante
		int centerX = width / 2;
		int centerY = height / 2;
		for (int x = 2; x < width - 2; x++)
		{
			if (Random.value < 0.75f) grid[x, centerY] = true;
		}
		for (int cy = 2; cy < height - 2; cy++)
		{
			if (Random.value < 0.75f) grid[centerX, cy] = true;
		}

		// 4. Eliminar callejones sin salida (Dead-ends removal)
		for (int j = 1; j < width - 1; j++)
		{
			for (int k = 1; k < height - 1; k++)
			{
				if (!grid[j, k]) continue;

				int openNeighbors = 0;
				if (grid[j - 1, k]) openNeighbors++;
				if (grid[j + 1, k]) openNeighbors++;
				if (grid[j, k - 1]) openNeighbors++;
				if (grid[j, k + 1]) openNeighbors++;

				// Si es un callejón sin salida (solo 1 salida), abrir una pared adyacente
				if (openNeighbors == 1)
				{
					List<Vector2Int> candidates = new List<Vector2Int>();
					if (j - 2 > 0) candidates.Add(new Vector2Int(j - 1, k));
					if (j + 2 < width - 1) candidates.Add(new Vector2Int(j + 1, k));
					if (k - 2 > 0) candidates.Add(new Vector2Int(j, k - 1));
					if (k + 2 < height - 1) candidates.Add(new Vector2Int(j, k + 1));

					if (candidates.Count > 0)
					{
						Vector2Int wallToBreak = candidates[Random.Range(0, candidates.Count)];
						grid[wallToBreak.x, wallToBreak.y] = true;
					}
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
}
