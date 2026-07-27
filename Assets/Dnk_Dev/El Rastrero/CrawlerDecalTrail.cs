using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CrawlerDecalTrail : MonoBehaviour
{
    [Header("Ajustes del Rastro de Podredumbre")]
    [Tooltip("Distancia en metros que debe avanzar El Rastrero para dejar una nueva mancha")]
    public float distanceBetweenDecals = 0.9f;
    [Tooltip("Cantidad máxima de manchas activas en la escena para proteger el rendimiento")]
    public int maxDecalsInScene = 40;
    [Tooltip("Tamaño aleatorio de la mancha de moho")]
    public Vector2 decalScaleRange = new Vector2(0.8f, 1.4f);
    [Tooltip("Tiempo en segundos que tarda en desvanecerse y desaparecer una mancha")]
    public float decalLifetime = 45f;

    [Header("Texturas de Podredumbre")]
    public Texture2D moldTexture;
    public Texture2D veinsTexture;

    private Vector3 lastSpawnPos;
    private List<GameObject> activeDecals = new List<GameObject>();

    void Start()
    {
        lastSpawnPos = transform.position;

        if (moldTexture == null)
            moldTexture = Resources.Load<Texture2D>("dark_mold_decay_1") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dnk_Dev/El Rastrero/dark_mold_decay_1.jpg");
        if (veinsTexture == null)
            veinsTexture = Resources.Load<Texture2D>("organic_veins_decay_2") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dnk_Dev/El Rastrero/organic_veins_decay_2.jpg");
    }

    void Update()
    {
        float movedDist = Vector3.Distance(transform.position, lastSpawnPos);
        if (movedDist >= distanceBetweenDecals)
        {
            lastSpawnPos = transform.position;
            SpawnDecalOnSurface();
        }
    }

    void SpawnDecalOnSurface()
    {
        // 1. Manchar Suelo Completo
        RaycastHit groundHit;
        if (Physics.Raycast(transform.position + Vector3.up * 1.0f, Vector3.down, out groundHit, 2.5f))
        {
            string hitName = groundHit.collider.name.ToLower();
            if (!hitName.Contains("player") && !hitName.Contains("rastrero") && !hitName.Contains("bookhead"))
            {
                CreateCorrosionQuad(groundHit.point + Vector3.up * 0.008f, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), Random.Range(3.8f, 4.4f));
            }
        }

        // 2. Manchar Muros y Techo Adyacentes alineados a la superficie
        Vector3[] sideDirs = new Vector3[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up };
        foreach (Vector3 dir in sideDirs)
        {
            RaycastHit hit;
            float checkDist = 3.5f;

            if (Physics.Raycast(transform.position + Vector3.up * 1.0f, dir, out hit, checkDist))
            {
                string hitName = hit.collider.name.ToLower();
                if (hitName.Contains("player") || hitName.Contains("rastrero") || hitName.Contains("bookhead")) continue;

                Quaternion flatRot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                if (dir == Vector3.up) flatRot = Quaternion.Euler(-90f, Random.Range(0f, 360f), 0f);

                CreateCorrosionQuad(hit.point + hit.normal * 0.012f, flatRot, Random.Range(2.2f, 3.2f));
            }
        }
    }

    void CreateCorrosionQuad(Vector3 pos, Quaternion rot, float scale)
    {
        GameObject quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObj.name = $"[Rastrero_Corrosion_{activeDecals.Count + 1}]";

        Collider qCol = quadObj.GetComponent<Collider>();
        if (qCol != null) Destroy(qCol);

        quadObj.transform.position = pos;
        quadObj.transform.rotation = rot;
        quadObj.transform.localScale = new Vector3(scale, scale, 1f);

        // Usar Shader URP Lit o Standard con modo transparente
        Shader blendShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Transparent");
        Material instMat = new Material(blendShader);

        if (instMat.HasProperty("_Surface")) instMat.SetFloat("_Surface", 1); // Transparent
        if (instMat.HasProperty("_Blend")) instMat.SetFloat("_Blend", 0);
        if (instMat.HasProperty("_ZWrite")) instMat.SetInt("_ZWrite", 0);
        instMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;

        Texture2D chosenTex = (Random.value < 0.6f && moldTexture != null) ? moldTexture : veinsTexture;
        if (chosenTex != null)
        {
            if (instMat.HasProperty("_BaseMap")) instMat.SetTexture("_BaseMap", chosenTex);
            if (instMat.HasProperty("_MainTex")) instMat.SetTexture("_MainTex", chosenTex);
        }

        Renderer rend = quadObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = instMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        activeDecals.Add(quadObj);
        StartCoroutine(FadeAndDestroyDecal(quadObj, instMat));

        if (activeDecals.Count > maxDecalsInScene)
        {
            GameObject oldest = activeDecals[0];
            activeDecals.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }
    }

    IEnumerator FadeAndDestroyDecal(GameObject decal, Material mat)
    {
        yield return new WaitForSeconds(decalLifetime);

        float timer = 0f;
        float fadeDuration = 4.0f;

        while (timer < fadeDuration)
        {
            if (decal == null || mat == null) yield break;
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

            Color c = new Color(1f, 1f, 1f, alpha);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

            yield return null;
        }

        if (activeDecals.Contains(decal)) activeDecals.Remove(decal);
        if (decal != null) Destroy(decal);
    }
}
