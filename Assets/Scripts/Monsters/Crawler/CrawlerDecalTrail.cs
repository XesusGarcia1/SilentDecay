using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CrawlerDecalTrail : MonoBehaviour
{
    [Header("Ajustes del Rastro de Podredumbre")]
    [Tooltip("Distancia base en metros que debe avanzar El Rastrero para dejar una nueva mancha")]
    public float distanceBetweenDecals = 0.9f;
    [Tooltip("Cantidad máxima de manchas activas en la escena para proteger el rendimiento")]
    public int maxDecalsInScene = 40;
    [Tooltip("Escala multiplicadora base para las manchas")]
    public float baseDecalScale = 3.5f;
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
        {
            moldTexture = Resources.Load<Texture2D>("dark_mold_decay_1");
#if UNITY_EDITOR
            if (moldTexture == null) moldTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dnk_Dev/El Rastrero/dark_mold_decay_1.jpg");
#endif
        }
        if (veinsTexture == null)
        {
            veinsTexture = Resources.Load<Texture2D>("organic_veins_decay_2");
#if UNITY_EDITOR
            if (veinsTexture == null) veinsTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dnk_Dev/El Rastrero/organic_veins_decay_2.jpg");
#endif
        }
    }

    void Update()
    {
        float monsterScale = Mathf.Max(0.5f, transform.lossyScale.x);
        float effectiveDistance = distanceBetweenDecals * monsterScale;

        float movedDist = Vector3.Distance(transform.position, lastSpawnPos);
        if (movedDist >= effectiveDistance)
        {
            lastSpawnPos = transform.position;
            SpawnDecalOnSurface();
        }
    }

    void SpawnDecalOnSurface()
    {
        float monsterScale = Mathf.Max(0.5f, transform.lossyScale.x);

        // 1. Manchar el Suelo por donde pasa El Rastrero
        RaycastHit groundHit;
        Vector3 rayStart = transform.position + Vector3.up * (1.0f * monsterScale);

        if (Physics.Raycast(rayStart, Vector3.down, out groundHit, 2.5f * monsterScale))
        {
            string hitName = groundHit.collider.name.ToLower();
            if (!hitName.Contains("player") && !hitName.Contains("rastrero") && !hitName.Contains("bookhead"))
            {
                if (Vector3.Angle(groundHit.normal, Vector3.up) < 25f)
                {
                    float floorScale = Random.Range(3.5f, 4.8f) * monsterScale;
                    CreateDecal(groundHit.point + Vector3.up * 0.003f, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), floorScale, true);
                }
            }
        }

        // 2. Manchar Muros y Techos Adyacentes (Solo si es pared o techo real del edificio)
        Vector3[] sideDirs = new Vector3[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up };
        foreach (Vector3 dir in sideDirs)
        {
            RaycastHit hit;
            float maxRayDist = ((dir == Vector3.up) ? 3.0f : 1.8f) * monsterScale;

            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "Water", "UI");

            if (Physics.Raycast(rayStart, dir, out hit, maxRayDist, layerMask))
            {
                if (hit.collider == null) continue;
                string hitName = hit.collider.name.ToLower();
                if (hitName.Contains("player") || hitName.Contains("rastrero") || hitName.Contains("bookhead") || hitName.Contains("battery") || hitName.Contains("fuse") || hitName.Contains("generator")) continue;

                // Verificar si impacta una pared o techo real
                float angleToUp = Vector3.Angle(hit.normal, Vector3.up);
                float angleToDown = Vector3.Angle(hit.normal, Vector3.down);

                if (Mathf.Abs(angleToUp - 90f) < 15f || angleToDown < 20f)
                {
                    Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    if (dir == Vector3.up || angleToDown < 20f) rot = Quaternion.Euler(-90f, Random.Range(0f, 360f), 0f);

                    float wallScale = Random.Range(2.8f, 3.8f) * monsterScale;
                    CreateDecal(hit.point + hit.normal * 0.002f, rot, wallScale, false);
                }
            }
        }
    }

    void CreateDecal(Vector3 pos, Quaternion rot, float scale, bool isFloor)
    {
        GameObject decalObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decalObj.name = $"[Rastrero_Corrosion_{activeDecals.Count + 1}]";

        // Quitar el MeshCollider sólido por completo
        MeshCollider mc = decalObj.GetComponent<MeshCollider>();
        if (mc != null) Destroy(mc);

        // Si es mancha de suelo, agregar un BoxCollider en modo TRIGGER (Jamás empuja al jugador pero detecta las pisadas)
        if (isFloor)
        {
            BoxCollider bc = decalObj.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.0f, 1.0f, 0.4f);
        }

        decalObj.transform.position = pos;
        decalObj.transform.rotation = rot;
        decalObj.transform.localScale = new Vector3(scale, scale, 1f);

        // Usar Sprites/Default para recortes transparentes sin tarjetas cuadradas
        Material instMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));

        // Tintado oscuro ambiental para mimetizarse con el pasillo
        Color darkAmbientColor = new Color(0.28f, 0.26f, 0.22f, 0.95f);
        instMat.color = darkAmbientColor;

        Texture2D chosenTex = (Random.value < 0.5f && moldTexture != null) ? moldTexture : veinsTexture;
        if (chosenTex != null) instMat.mainTexture = chosenTex;

        Renderer rend = decalObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = instMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        activeDecals.Add(decalObj);
        StartCoroutine(FadeAndDestroyDecal(decalObj, instMat));

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
