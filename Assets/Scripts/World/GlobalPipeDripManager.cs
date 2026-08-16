using UnityEngine;
using System.Collections.Generic;

public class GlobalPipeDripManager : MonoBehaviour
{
    [Header("Configuración")]
    public float updateInterval = 0.5f;
    public int maxDripSources = 4;
    public float maxDripDistance = 15f;
    
    private Transform player;
    private List<Transform> allPipes = new List<Transform>();
    private AudioSource[] dripPool;
    private AudioClip dripClip;

    private float timer;

    void Start()
    {
        FindPlayer();
        dripClip = Resources.Load<AudioClip>("Audio/Compartido/Gotera");

        // Encontrar todas las tuberías/tanques en el mapa
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            string n = t.name.ToLower();
            if (n.Contains("pipe") || n.Contains("tanque") || n.Contains("pump") || n.Contains("tuberia") || n.Contains("tubo"))
            {
                // Para evitar duplicados en la misma malla, verificar si no hay otro muy cerca
                if (!IsTooCloseToAnotherPipe(t.position))
                {
                    allPipes.Add(t);
                }
            }
        }

        Debug.Log($"[GlobalPipeDripManager] Encontradas {allPipes.Count} tuberías para goteras.");

        // Inicializar el Pool de AudioSources
        dripPool = new AudioSource[maxDripSources];
        for (int i = 0; i < maxDripSources; i++)
        {
            GameObject sObj = new GameObject($"DripAudioSource_{i}");
            sObj.transform.SetParent(this.transform);
            AudioSource src = sObj.AddComponent<AudioSource>();
            src.clip = dripClip;
            src.spatialBlend = 1.0f; // 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1.5f;
            src.maxDistance = maxDripDistance;
            src.loop = true;
            src.volume = 0f; // Empieza en 0
            src.Play();
            dripPool[i] = src;
        }
    }

    bool IsTooCloseToAnotherPipe(Vector3 pos)
    {
        foreach (Transform p in allPipes)
        {
            if (Vector3.Distance(p.position, pos) < 3.0f) return true;
        }
        return false;
    }

    void FindPlayer()
    {
        if (player != null) return;
        GameObject pTag = GameObject.FindGameObjectWithTag("Player");
        if (pTag != null) player = pTag.transform;
        else if (Camera.main != null) player = Camera.main.transform;
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            UpdateClosestPipes();
        }

        // Suavizar el volumen hacia el targetVolume
        for (int i = 0; i < maxDripSources; i++)
        {
            if (dripPool[i] != null)
            {
                // Aquí calculamos target volume basado en la distancia en cada frame para que sea suave
                float dist = Vector3.Distance(dripPool[i].transform.position, player.position);
                float targetVol = 0f;
                if (dist < maxDripDistance)
                {
                    targetVol = (1.0f - (dist / maxDripDistance)) * 0.7f; // Max vol 0.7
                }
                dripPool[i].volume = Mathf.Lerp(dripPool[i].volume, targetVol, Time.deltaTime * 3f);
            }
        }
    }

    void UpdateClosestPipes()
    {
        // Quitar nulos por si alguna tubería fue destruida
        allPipes.RemoveAll(p => p == null);

        // Ordenar tuberías por distancia al jugador
        allPipes.Sort((a, b) => 
        {
            float distA = Vector3.Distance(a.position, player.position);
            float distB = Vector3.Distance(b.position, player.position);
            return distA.CompareTo(distB);
        });

        for (int i = 0; i < maxDripSources; i++)
        {
            if (i < allPipes.Count)
            {
                Transform closestPipe = allPipes[i];
                // Solo mover si el jugador no está tan lejos, de lo contrario lo dejamos donde está
                if (Vector3.Distance(closestPipe.position, player.position) < maxDripDistance * 1.5f)
                {
                    dripPool[i].transform.position = closestPipe.position;
                }
            }
        }
    }
}
