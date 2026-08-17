using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class KeySpawnManager : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoRunKeyManager()
    {
        // Solo inyectar si estamos en el juego
        if (Application.isPlaying)
        {
            GameObject manager = new GameObject("[KeySpawnManager]");
            DontDestroyOnLoad(manager);
            manager.AddComponent<KeySpawnManager>();
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

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RunKeySpawnLogic();
    }

    void RunKeySpawnLogic()
    {
        // Encontrar todas las llaves en la escena
        MetalKeyItem[] allKeys = FindObjectsByType<MetalKeyItem>(FindObjectsSortMode.None);
        
        List<MetalKeyItem> backroomKeys = new List<MetalKeyItem>();

        foreach (MetalKeyItem key in allKeys)
        {
            // La llave de recepción (Access_keys_mannequin) se deja tal cual, no la administramos aquí
            if (key.gameObject.name.Contains("Access_keys"))
            {
                continue; 
            }

            // Administrar solo las llaves llamadas R_17_key_Reception
            if (key.gameObject.name.Contains("R_17_key_Reception"))
            {
                // Identificar si la llave está en los Backrooms
                if (IsChildOfName(key.transform, "Backrooms") || IsChildOfName(key.transform, "MannequinCourtyardMap"))
                {
                    backroomKeys.Add(key);
                }
                
                // Desactivar todas inicialmente
                key.gameObject.SetActive(false);
            }
        }

        // Si encontramos llaves en los Backrooms, elegir UNA al azar y activarla
        if (backroomKeys.Count > 0)
        {
            // Agrupar por estantería (suponiendo que las 2 llaves de una estantería comparten el mismo padre 'Estanteria' o están muy cerca)
            Dictionary<Transform, List<MetalKeyItem>> shelfGroups = new Dictionary<Transform, List<MetalKeyItem>>();
            foreach (var key in backroomKeys)
            {
                Transform parentShelf = key.transform.parent; 
                if (parentShelf == null) parentShelf = key.transform; // Fallback

                if (!shelfGroups.ContainsKey(parentShelf))
                {
                    shelfGroups[parentShelf] = new List<MetalKeyItem>();
                }
                shelfGroups[parentShelf].Add(key);
            }

            // Elegir una estantería al azar
            List<Transform> shelfList = new List<Transform>(shelfGroups.Keys);
            Transform chosenShelf = shelfList[Random.Range(0, shelfList.Count)];

            // De esa estantería, elegir una de las posiciones (llaves) al azar
            List<MetalKeyItem> keysInChosenShelf = shelfGroups[chosenShelf];
            MetalKeyItem chosenKey = keysInChosenShelf[Random.Range(0, keysInChosenShelf.Count)];

            chosenKey.gameObject.SetActive(true);
            Debug.Log("[KeySpawnManager] Llave R_17_key elegida al azar en el mueble: " + chosenShelf.name);
        }
    }

    private bool IsChildOfName(Transform t, string parentName)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.Contains(parentName))
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }
}
