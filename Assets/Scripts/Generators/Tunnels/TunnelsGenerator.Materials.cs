using UnityEngine;

public partial class TunnelsGenerator
{
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
}
