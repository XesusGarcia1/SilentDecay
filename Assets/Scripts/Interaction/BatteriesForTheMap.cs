using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BatteriesForTheMap : MonoBehaviour
{
    [Header("Ajustes del Sistema")]
    [Tooltip("Tiempo en segundos para regenerar baterías en Fácil")]
    public float respawnTimeEasy = 60f;
    [Tooltip("Tiempo en segundos para regenerar baterías en Normal")]
    public float respawnTimeNormal = 120f;
    [Tooltip("Tiempo en segundos para regenerar baterías en Difícil")]
    public float respawnTimeHard = 180f;

    private List<BatteryItem> allBatteries = new List<BatteryItem>();
    private int targetActiveCount = 1;
    private float currentRespawnTimer = 0f;
    private bool isRespawning = false;
    private float selectedRespawnTime = 120f;

    void Start()
    {
        // Encontrar todas las baterías en la escena (incluso las inactivas)
        BatteryItem[] foundBatteries = Resources.FindObjectsOfTypeAll<BatteryItem>();
        
        foreach (BatteryItem bat in foundBatteries)
        {
            // Solo considerar las que son parte de la escena activa (no prefabs)
            if (bat.gameObject.scene.isLoaded)
            {
                allBatteries.Add(bat);
            }
        }

        if (allBatteries.Count == 0)
        {
            Debug.LogWarning("BatteriesForTheMap: No se encontraron pilas en la escena.");
            return;
        }

        // Obtener dificultad y calcular porcentaje
        string difficulty = PlayerPrefs.GetString("SelectedDifficulty", "NORMAL");
        
        float percentage = 0.40f; // Normal por defecto
        selectedRespawnTime = respawnTimeNormal;

        if (difficulty == "FÁCIL" || difficulty == "EASY" || difficulty == "FACIL" || difficulty == "FÁCIL" || difficulty == "ЛЕГКИЙ")
        {
            percentage = 0.70f;
            selectedRespawnTime = respawnTimeEasy;
        }
        else if (difficulty == "DIFÍCIL" || difficulty == "HARD" || difficulty == "DIFÍCEIL" || difficulty == "СЛОЖНЫЙ" || difficulty == "DIFÍCIL")
        {
            percentage = 0.15f;
            selectedRespawnTime = respawnTimeHard;
        }

        // Calcular cuántas deben estar activas (mínimo 1)
        targetActiveCount = Mathf.Max(1, Mathf.RoundToInt(allBatteries.Count * percentage));
        
        Debug.Log($"BatteriesForTheMap: Dificultad {difficulty}. Total pilas: {allBatteries.Count}. Activando {targetActiveCount} ({(percentage*100)}%). Respawn en {selectedRespawnTime}s.");

        // Iniciar la primera distribución
        ShuffleAndActivate(targetActiveCount);
    }

    void Update()
    {
        if (allBatteries.Count == 0 || isRespawning) return;

        // Contar cuántas pilas siguen activas en el mapa
        int activeCount = 0;
        foreach (BatteryItem bat in allBatteries)
        {
            if (bat != null && bat.gameObject.activeInHierarchy)
            {
                activeCount++;
            }
        }

        // Si el jugador recogió todas las pilas activas, iniciar regeneración
        if (activeCount == 0)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        currentRespawnTimer = selectedRespawnTime;

        Debug.Log($"BatteriesForTheMap: Todas las pilas fueron recogidas. Iniciando respawn en {selectedRespawnTime} segundos.");

        while (currentRespawnTimer > 0f)
        {
            currentRespawnTimer -= Time.deltaTime;
            yield return null;
        }

        // Regenerar pilas
        ShuffleAndActivate(targetActiveCount);
        isRespawning = false;
        
        Debug.Log($"BatteriesForTheMap: ¡Pilas regeneradas! ({targetActiveCount} activas).");
    }

    private void ShuffleAndActivate(int amountToActivate)
    {
        // Asegurarnos de que no activamos más de las que existen
        amountToActivate = Mathf.Min(amountToActivate, allBatteries.Count);

        // Ocultar todas primero
        foreach (BatteryItem bat in allBatteries)
        {
            if (bat != null)
            {
                bat.gameObject.SetActive(false);
            }
        }

        // Barajar la lista (Fisher-Yates shuffle)
        for (int i = 0; i < allBatteries.Count; i++)
        {
            BatteryItem temp = allBatteries[i];
            int randomIndex = Random.Range(i, allBatteries.Count);
            allBatteries[i] = allBatteries[randomIndex];
            allBatteries[randomIndex] = temp;
        }

        // Activar la cantidad deseada
        int activated = 0;
        for (int i = 0; i < allBatteries.Count; i++)
        {
            if (allBatteries[i] != null)
            {
                allBatteries[i].gameObject.SetActive(true);
                activated++;
                if (activated >= amountToActivate) break;
            }
        }
    }
}
