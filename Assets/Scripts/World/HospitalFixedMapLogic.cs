using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic : MonoBehaviour
{
    [Header("Referencias Generales")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar y cuando muera.")]
    public Transform pointStartRespawn;

    [Tooltip("El prefab de la nota de Lore (PapelLore) que se instanciará en el suelo.")]
    public GameObject loreNotePrefab;

    [Header("Opciones de Pruebas / Dev (Elevador y Tarjeta)")]
    [Tooltip("Iniciar la partida con la tarjeta de acceso del director en el inventario para pruebas de desarrollo.")]
    public bool startWithKeycard = false;
    [Tooltip("Burlar la tarjeta de acceso para probar el ascensor sin buscar la tarjeta.")]
    public bool bypassKeycard = false;
    [Tooltip("Burlar la necesidad de energía eléctrica para probar el ascensor sin activar fusibles.")]
    public bool bypassPower = false;

    private string correctKeypadCode = "";
    private Transform playerTransform;
    private List<GameObject> backupFusePool = new List<GameObject>();
    private bool isFuseRespawnTimerRunning = false;
    private List<GameObject> backupBatteryPool = new List<GameObject>();
    private bool isBatteryRespawnTimerRunning = false;

    private void Start()
    {
        // Asegurar que el menú de pausa (PauseMenuManager) esté presente
        if (FindFirstObjectByType<PauseMenuManager>() == null)
        {
            GameObject pMenu = new GameObject("[PauseMenuManager]");
            pMenu.AddComponent<PauseMenuManager>();
        }

        SetupPlayerSpawn();
        SetupDoors();
        ProcessGurneyItems(); // Filtrar camillas Worn_Hospital_Gurney (1 ítem por camilla)
        SetupRandomElements();
        SetupHideBeds();
        SetupLoreNotes();
        SetupItems();
        SetupElevators();

        // Monitorear e iniciar respawn automático de fusibles y baterías si el jugador los necesita
        StartCoroutine(CheckAndRespawnFusesRoutine());
        StartCoroutine(CheckAndRespawnBatteriesRoutine());

        // Disparar monólogo inicial del jugador con pequeño delay
        StartCoroutine(TriggerStartMonologueDelayed());
    }

    private void SetupPlayerSpawn()
    {
        // 1. Buscar automáticamente el objeto 'StartGame' en la escena si no está asignado en el Inspector
        if (pointStartRespawn == null)
        {
            GameObject sgObj = GameObject.Find("StartGame");
            if (sgObj != null)
            {
                pointStartRespawn = sgObj.transform;
            }
        }

        if (pointStartRespawn == null)
        {
            Time.timeScale = 1.0f;
            return;
        }

        // 2. Encontrar el objeto de personaje activo (PlayerMale o PlayerFemale)
        GameObject playerObj = null;
        GameObject male = GameObject.Find("PlayerMale");
        GameObject female = GameObject.Find("PlayerFemale");

        if (male != null && male.activeInHierarchy) playerObj = male;
        else if (female != null && female.activeInHierarchy) playerObj = female;
        else if (male != null) playerObj = male;
        else if (female != null) playerObj = female;
        else playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            CharacterController cc = playerObj.GetComponentInChildren<CharacterController>(true);
            
            // Método oficial de Unity para teletransportar un CharacterController sin romper físicas ni controles
            if (cc != null) cc.enabled = false;

            playerObj.transform.position = pointStartRespawn.position;
            playerObj.transform.rotation = pointStartRespawn.rotation;

            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegistrarSpawnJugador(pointStartRespawn.position, pointStartRespawn.rotation);
            }
        }

        Time.timeScale = 1.0f;
    }

    private bool IsTopLevelElement(Transform t, string[] keywords, string[] excludeKeywords)
    {
        if (t == null) return false;

        // Jamás procesar objetos pertenecientes al jugador ni a su jerarquía
        Transform root = t.root;
        if (root != null)
        {
            string rootName = root.name.ToLower();
            if (rootName.Contains("player") || rootName.Contains("nestedparent") || root.CompareTag("Player"))
            {
                return false;
            }
        }

        string n = t.name.ToLower();
        if (n.Contains("player") || n.Contains("capsule") || n.Contains("camera")) return false;

        // Si está en el origen exacto (0,0,0), ignorar por ser plantilla deshabilitada fuera del mapa
        if (t.position.sqrMagnitude < 0.001f) return false;

        bool matches = false;
        foreach (var kw in keywords)
        {
            if (n.Contains(kw)) { matches = true; break; }
        }
        if (!matches) return false;

        foreach (var ex in excludeKeywords)
        {
            if (n.Contains(ex)) return false;
        }

        // Si un ancestro directo ya coincide con la misma palabra clave (ej: sub-mallas dentro del objeto principal), ignorar el hijo
        Transform p = t.parent;
        while (p != null)
        {
            string pName = p.name.ToLower();
            if (pName.Contains("props") || pName.Contains("rooms") || pName.Contains("hospitalgame") || pName.Contains("modular") || pName.Contains("prefabs")) 
            {
                break;
            }

            foreach (var kw in keywords)
            {
                if (pName.Contains(kw))
                {
                    return false;
                }
            }
            p = p.parent;
        }

        return true;
    }

    private void ConfigureBoxColliderFromRenderers(GameObject obj, BoxCollider box, bool isTrigger, float sizePaddingMultiplier = 1.0f)
    {
        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>(true);
        if (mfs.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
            return;
        }

        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            
            Bounds meshBounds = mf.sharedMesh.bounds;
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.min.z);
            corners[1] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.max.z);
            corners[2] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.min.z);
            corners[3] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.max.z);
            corners[4] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.min.z);
            corners[5] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.max.z);
            corners[6] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.min.z);
            corners[7] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.max.z);

            for (int i = 0; i < 8; i++)
            {
                Vector3 worldCorner = mf.transform.TransformPoint(corners[i]);
                Vector3 localCorner = obj.transform.InverseTransformPoint(worldCorner);
                
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (hasBounds)
        {
            box.isTrigger = isTrigger;
            box.center = localBounds.center;
            box.size = localBounds.size * sizePaddingMultiplier;
        }
        else
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
        }
    }

    private IEnumerator TriggerStartMonologueDelayed()
    {
        yield return new WaitForSeconds(1.2f);
        PlayerMonologueManager.ShowDialogue("¿Dónde estoy?... El hospital parece totalmente abandonado. Debo encontrar la tarjeta de acceso del director para usar el ascensor de evacuación...", 6.0f);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
