using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TunnelsAmbientAudioManager : MonoBehaviour
{
    [Header("Configuracion General")]
    public float updateInterval = 0.5f;

    [Header("Goteras")]
    public int maxDripSources = 5;
    public float dripMaxHearingDistance = 7f; // Distancia de escucha reducida para las goteras
    public Vector2 dripRandomInterval = new Vector2(2f, 5f);
    
    [Header("Electricidad")]
    public int maxSparkSources = 4;
    public float sparkMaxHearingDistance = 15f;

    private Transform player;
    
    private List<Transform> allPipes = new List<Transform>();
    private List<Transform> allSparks = new List<Transform>();

    private AudioClip dripClip;
    private AudioClip sparkClip;

    private DripSource[] dripPool;
    private AudioSource[] sparkPool;

    private class DripSource
    {
        public AudioSource audioSource;
        public float timer;
        public float nextInterval;
        public Transform targetPipe;
    }

    void Start()
    {
        FindPlayer();
        LoadClips();
        FindMapEntities();
        InitializePools();
        
        StartCoroutine(UpdateClosestEntitiesRoutine());
    }

    void FindPlayer()
    {
        GameObject pTag = GameObject.FindGameObjectWithTag("Player");
        if (pTag != null) player = pTag.transform;
        else if (Camera.main != null) player = Camera.main.transform;
    }

    void LoadClips()
    {
        dripClip = Resources.Load<AudioClip>("Audio/Compartido/Gotera");
        sparkClip = Resources.Load<AudioClip>("Audio/Compartido/electricidad");
    }

    void FindMapEntities()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            string n = t.name.ToLower();
            
            // Buscar tuberias
            if (n.Contains("pipe") || n.Contains("tanque") || n.Contains("tuberia") || n.Contains("tubo"))
            {
                if (!IsTooClose(t.position, allPipes, 3.0f))
                {
                    allPipes.Add(t);
                }
            }
            
            // Buscar chispas electricas
            if (n.Contains("electric_spark"))
            {
                if (!IsTooClose(t.position, allSparks, 2.0f))
                {
                    allSparks.Add(t);
                }
            }
        }
        Debug.Log($"[TunnelsAmbientAudioManager] Encontradas {allPipes.Count} tuberias y {allSparks.Count} chispas electricas.");
    }

    bool IsTooClose(Vector3 pos, List<Transform> list, float minDistance)
    {
        foreach (Transform item in list)
        {
            if (Vector3.Distance(item.position, pos) < minDistance) return true;
        }
        return false;
    }

    void InitializePools()
    {
        // Pool de Goteras
        dripPool = new DripSource[maxDripSources];
        for (int i = 0; i < maxDripSources; i++)
        {
            GameObject obj = new GameObject($"DripAudio_{i}");
            obj.transform.SetParent(this.transform);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.spatialBlend = 1.0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1.0f; // Un poco menor también
            src.maxDistance = dripMaxHearingDistance;
            src.loop = false;
            
            dripPool[i] = new DripSource 
            { 
                audioSource = src, 
                timer = 0f, 
                nextInterval = Random.Range(dripRandomInterval.x, dripRandomInterval.y) 
            };
        }

        // Pool de Chispas
        sparkPool = new AudioSource[maxSparkSources];
        for (int i = 0; i < maxSparkSources; i++)
        {
            GameObject obj = new GameObject($"SparkAudio_{i}");
            obj.transform.SetParent(this.transform);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.clip = sparkClip;
            src.spatialBlend = 1.0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2.0f;
            src.maxDistance = sparkMaxHearingDistance;
            src.loop = true;
            src.volume = 0f; // Inicialmente silenciado hasta que se asigne
            src.Play();
            
            sparkPool[i] = src;
        }
    }

    IEnumerator UpdateClosestEntitiesRoutine()
    {
        while (true)
        {
            if (player == null)
            {
                FindPlayer();
                yield return new WaitForSeconds(updateInterval);
                continue;
            }

            UpdateClosestPipes();
            UpdateClosestSparks();

            yield return new WaitForSeconds(updateInterval);
        }
    }

    void UpdateClosestPipes()
    {
        allPipes.RemoveAll(p => p == null);
        allPipes.Sort((a, b) => Vector3.Distance(a.position, player.position).CompareTo(Vector3.Distance(b.position, player.position)));

        for (int i = 0; i < maxDripSources; i++)
        {
            if (i < allPipes.Count)
            {
                Transform closestPipe = allPipes[i];
                if (Vector3.Distance(closestPipe.position, player.position) < dripMaxHearingDistance + 5f)
                {
                    dripPool[i].targetPipe = closestPipe;
                    dripPool[i].audioSource.transform.position = closestPipe.position;
                }
                else
                {
                    dripPool[i].targetPipe = null;
                }
            }
        }
    }

    void UpdateClosestSparks()
    {
        allSparks.RemoveAll(s => s == null);
        allSparks.Sort((a, b) => Vector3.Distance(a.position, player.position).CompareTo(Vector3.Distance(b.position, player.position)));

        for (int i = 0; i < maxSparkSources; i++)
        {
            if (i < allSparks.Count)
            {
                Transform closestSpark = allSparks[i];
                float dist = Vector3.Distance(closestSpark.position, player.position);
                
                if (dist < sparkMaxHearingDistance + 5f)
                {
                    sparkPool[i].transform.position = closestSpark.position;
                    // Ajuste de volumen suave
                    sparkPool[i].volume = (dist < sparkMaxHearingDistance) ? 1.0f : 0f; 
                }
                else
                {
                    sparkPool[i].volume = 0f;
                }
            }
            else
            {
                if (sparkPool[i] != null) sparkPool[i].volume = 0f;
            }
        }
    }

    void Update()
    {
        if (player == null || dripPool == null) return;

        // Logica de goteras esporadicas
        for (int i = 0; i < maxDripSources; i++)
        {
            DripSource ds = dripPool[i];
            if (ds != null && ds.targetPipe != null)
            {
                ds.timer += Time.deltaTime;
                if (ds.timer >= ds.nextInterval)
                {
                    if (dripClip != null)
                    {
                        // Variar ligeramente el pitch para no sonar repetitivo
                        ds.audioSource.pitch = Random.Range(0.9f, 1.1f);
                        ds.audioSource.PlayOneShot(dripClip);
                    }
                    ds.timer = 0f;
                    ds.nextInterval = Random.Range(dripRandomInterval.x, dripRandomInterval.y);
                }
            }
        }
    }
}
