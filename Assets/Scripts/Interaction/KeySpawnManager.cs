using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Administrador dinámico para la distribución aleatoria de llaves, piezas de escalera e ítems clave en las escenas.
/// Garantiza que de múltiples puntos de spawn colocados en el mapa para un mismo ítem
/// (como EXITKEY_01, Access_keys_mannequin, LadderComponent_1, LadderComponent_2),
/// solo se active uno por partida y los demás permanezcan ocultos.
/// </summary>
public class KeySpawnManager : MonoBehaviour
{
    private static KeySpawnManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoRunKeyManager()
    {
        if (Application.isPlaying && instance == null)
        {
            GameObject manager = new GameObject("[KeySpawnManager]");
            DontDestroyOnLoad(manager);
            instance = manager.AddComponent<KeySpawnManager>();
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Ejecutar si la escena ya estaba cargada cuando se inició el componente
        RunSpawnLogic();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RunSpawnLogic();
    }

    /// <summary>
    /// Ejecuta la lógica de distribución aleatoria para llaves y piezas de escalera en la escena.
    /// </summary>
    public void RunSpawnLogic()
    {
        // Asegurar que cualquier contenedor de pruebas 'KeysPruebas' permanezca completamente desactivado
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            if (root.name.Contains("KeysPruebas") || root.name.Contains("KeysPrueba"))
            {
                root.SetActive(false);
            }
        }

        SpawnKeys();
        SpawnLadderParts();
    }

    // Compatibilidad hacia atrás
    public void RunKeySpawnLogic()
    {
        RunSpawnLogic();
    }

    /// <summary>
    /// Identifica todas las llaves en la escena activa, las agrupa por tipo/ID y activa solo una por grupo.
    /// Excluye permanentemente cualquier llave que sea hija de 'KeysPruebas'.
    /// </summary>
    private void SpawnKeys()
    {
        // Encontrar todas las llaves en la escena activa (incluyendo las inactivas)
        List<MetalKeyItem> sceneKeys = new List<MetalKeyItem>();
        MetalKeyItem[] allKeys = Resources.FindObjectsOfTypeAll<MetalKeyItem>();

        foreach (MetalKeyItem key in allKeys)
        {
            if (key != null && key.gameObject.scene.isLoaded)
            {
                // Si la llave está bajo el GameObject de pruebas 'KeysPruebas', desactivarla siempre y no incluirla en el pool
                if (IsUnderIgnoredParent(key.transform))
                {
                    key.gameObject.SetActive(false);
                    continue;
                }

                sceneKeys.Add(key);
            }
        }

        if (sceneKeys.Count == 0)
        {
            return;
        }

        // Agrupar llaves por su tipo o identificador canónico
        Dictionary<string, List<MetalKeyItem>> keyGroups = new Dictionary<string, List<MetalKeyItem>>();

        foreach (MetalKeyItem key in sceneKeys)
        {
            string groupID = IdentifyKeyGroup(key);

            if (!keyGroups.ContainsKey(groupID))
            {
                keyGroups[groupID] = new List<MetalKeyItem>();
            }
            keyGroups[groupID].Add(key);
        }

        // Procesar cada grupo de llaves
        foreach (var pair in keyGroups)
        {
            string groupName = pair.Key;
            List<MetalKeyItem> candidates = pair.Value;

            if (candidates.Count == 0) continue;

            // Desactivar todas las posiciones candidatas del grupo
            foreach (var keyItem in candidates)
            {
                if (keyItem != null)
                {
                    keyItem.gameObject.SetActive(false);
                }
            }

            // Elegir una posición aleatoria dentro de los candidatos
            int selectedIndex = Random.Range(0, candidates.Count);
            MetalKeyItem chosenKey = candidates[selectedIndex];

            if (chosenKey != null)
            {
                chosenKey.gameObject.SetActive(true);
                Debug.Log($"[KeySpawnManager] Grupo de llave '{groupName}': Activada llave aleatoria en {chosenKey.transform.position} (Objeto: '{chosenKey.gameObject.name}'). Total de ubicaciones candidatas válidas: {candidates.Count}.");
            }
        }
    }

    /// <summary>
    /// Identifica todas las piezas de escalera en la escena activa, las agrupa por tipo y activa solo una por grupo.
    /// Excluye permanentemente cualquier pieza que sea hija de 'KeysPruebas'.
    /// </summary>
    private void SpawnLadderParts()
    {
        // Encontrar todas las piezas de escalera en la escena activa (incluyendo las inactivas)
        List<LadderPartItem> sceneParts = new List<LadderPartItem>();
        LadderPartItem[] allParts = Resources.FindObjectsOfTypeAll<LadderPartItem>();

        foreach (LadderPartItem part in allParts)
        {
            if (part != null && part.gameObject.scene.isLoaded)
            {
                // Si la pieza está bajo un contenedor de pruebas 'KeysPruebas', desactivarla y no incluirla
                if (IsUnderIgnoredParent(part.transform))
                {
                    part.gameObject.SetActive(false);
                    continue;
                }

                sceneParts.Add(part);
            }
        }

        if (sceneParts.Count == 0)
        {
            return;
        }

        // Agrupar piezas por su tipo o identificador canónico
        Dictionary<string, List<LadderPartItem>> partGroups = new Dictionary<string, List<LadderPartItem>>();

        foreach (LadderPartItem part in sceneParts)
        {
            string groupID = IdentifyLadderPartGroup(part);

            if (!partGroups.ContainsKey(groupID))
            {
                partGroups[groupID] = new List<LadderPartItem>();
            }
            partGroups[groupID].Add(part);
        }

        // Procesar cada grupo de piezas de escalera
        foreach (var pair in partGroups)
        {
            string groupName = pair.Key;
            List<LadderPartItem> candidates = pair.Value;

            if (candidates.Count == 0) continue;

            // Desactivar todas las posiciones candidatas del grupo
            foreach (var partItem in candidates)
            {
                if (partItem != null)
                {
                    partItem.gameObject.SetActive(false);
                }
            }

            // Elegir una posición aleatoria dentro de los candidatos
            int selectedIndex = Random.Range(0, candidates.Count);
            LadderPartItem chosenPart = candidates[selectedIndex];

            if (chosenPart != null)
            {
                chosenPart.gameObject.SetActive(true);
                Debug.Log($"[KeySpawnManager] Grupo de escalera '{groupName}': Activada pieza aleatoria en {chosenPart.transform.position} (Objeto: '{chosenPart.gameObject.name}'). Total de ubicaciones candidatas válidas: {candidates.Count}.");
            }
        }
    }

    /// <summary>
    /// Comprueba si un objeto o cualquiera de sus ancestros pertenecen a un contenedor de pruebas como 'KeysPruebas'.
    /// </summary>
    private bool IsUnderIgnoredParent(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.Contains("KeysPruebas") || current.name.Contains("KeysPrueba"))
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// Determina el identificador de grupo canónico de una llave analizando su keyID y nombre de GameObject.
    /// </summary>
    private string IdentifyKeyGroup(MetalKeyItem key)
    {
        string rawName = key.gameObject.name;
        string keyID = key.keyID != null ? key.keyID.Trim() : "";

        // 1. Detección específica para EXITKEY_01 y variantes
        if (keyID.Contains("EXITKEY_01") || rawName.Contains("EXITKEY_01") || rawName.StartsWith("EXITKEY"))
        {
            return "EXITKEY_01";
        }

        // 2. Detección específica para Access_keys_mannequin y variantes
        if (keyID.Contains("Access_keys") || rawName.Contains("Access_keys"))
        {
            return "Access_keys_mannequin";
        }

        // 3. Detección para llaves de recepción / Backrooms
        if (keyID.Contains("R_17_key") || rawName.Contains("R_17_key"))
        {
            return "R_17_key_Reception";
        }

        // 4. Detección para Llave_industrial y variantes
        if (keyID.Contains("Llave_industrial") || rawName.Contains("Llave_industrial"))
        {
            return "Llave_industrial";
        }

        // 5. Fallback genérico
        if (!string.IsNullOrEmpty(keyID))
        {
            return CleanIdentifier(keyID);
        }

        return CleanIdentifier(rawName);
    }

    /// <summary>
    /// Determina el identificador de grupo canónico de una pieza de escalera.
    /// </summary>
    private string IdentifyLadderPartGroup(LadderPartItem part)
    {
        string rawName = part.gameObject.name;
        string partID = part.partID != null ? part.partID.Trim() : "";

        // 1. Detección específica para LadderComponent_1 y variantes
        if (partID.Contains("LadderComponent_1") || rawName.Contains("LadderComponent_1"))
        {
            return "LadderComponent_1";
        }

        // 2. Detección específica para LadderComponent_2 y variantes
        if (partID.Contains("LadderComponent_2") || rawName.Contains("LadderComponent_2"))
        {
            return "LadderComponent_2";
        }

        // 3. Fallback genérico
        if (!string.IsNullOrEmpty(partID))
        {
            return CleanIdentifier(partID);
        }

        return CleanIdentifier(rawName);
    }

    /// <summary>
    /// Limpia sufijos generados automáticamente por Unity como " (1)", " (8)", "(Clone)", "Variant", espacios extra, etc.
    /// </summary>
    private string CleanIdentifier(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName)) return "UnknownItem";

        // Remover (Clone) y Variant
        string cleaned = sourceName.Replace("(Clone)", "").Replace("Variant", "").Trim();

        // Remover sufijos numéricos entre paréntesis como " (1)", " (2)", "(8)", "(123)"
        cleaned = Regex.Replace(cleaned, @"\s*\(\d+\)$", "").Trim();

        return cleaned;
    }
}
