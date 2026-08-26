using UnityEngine;
using System.Collections;

public class EnemyActivator : MonoBehaviour
{
    public GameObject enemyToActivate;
    private PowerBox powerBox;
    private bool wasOutage = false;
    private bool initialized = false;

    private void Start()
    {
        // Buscar el enemigo automáticamente (The Amalgam) si no está asignado
        if (enemyToActivate == null)
        {
            Monsters.Amalgam.AmalgamAIController amalgam = FindFirstObjectByType<Monsters.Amalgam.AmalgamAIController>(FindObjectsInactive.Include);
            if (amalgam != null)
                enemyToActivate = amalgam.gameObject;
        }

        if (enemyToActivate == null)
        {
            Debug.LogError("EnemyActivator: No se ha asignado ni encontrado el enemigo.");
            return;
        }

        // Buscar PowerBox: primero en este mismo objeto (si EnemyActivator esta en CajaFusibles)
        powerBox = GetComponent<PowerBox>();
        if (powerBox == null)
            powerBox = FindObjectOfType<PowerBox>();

        if (powerBox == null)
            Debug.LogError("EnemyActivator: No se encontro PowerBox en la escena.");

        // Esperar 2 frames para que el generador inyecte los patrol points primero
        StartCoroutine(InitializeDelayed());
    }

    private IEnumerator InitializeDelayed()
    {
        yield return null;
        yield return null;

        bool initialOutage = powerBox != null && powerBox.isPowerOut;
        wasOutage = initialOutage;

        if (initialOutage)
        {
            ActivateEnemy();
        }
        else
        {
            enemyToActivate.SetActive(false);
            Debug.Log("EnemyActivator: Enemigo en espera. Se activara en el primer apagon.");
        }

        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        if (powerBox == null || enemyToActivate == null) return;

        bool currentOutage = powerBox.isPowerOut;

        if (currentOutage != wasOutage)
        {
            wasOutage = currentOutage;

            if (currentOutage)
            {
                ActivateEnemy();
                Debug.Log("EnemyActivator: Apagon detectado! Enemigo activado.");
            }
            else
            {
                enemyToActivate.SetActive(false);
                Debug.Log("EnemyActivator: Energia restaurada. Enemigo desactivado.");
            }
        }
    }

    private void ActivateEnemy()
    {
        // ORDEN CRITICO:
        // 1. Activar el enemigo PRIMERO para que NavMeshAgent este disponible
        enemyToActivate.SetActive(true);

        // 2. Reposicionar DESPUES de activar (NavMeshAgent activo = puede recibir SetDestination)
        // LEGACY REMOVED: HospitalMazeGenerator.RespawnEnemyNearPlayer() no longer exists
        // Fallback: colocar al lado del jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)
            ).normalized * Random.Range(8f, 14f);
            enemyToActivate.transform.position = player.transform.position + offset;
        }
    }
}
