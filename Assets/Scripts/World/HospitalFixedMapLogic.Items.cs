using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ModularHospital;

public partial class HospitalFixedMapLogic
{
    private void ProcessGurneyItems()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Transform> gurneys = new List<Transform>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string tName = t.name.ToLower();
            if (tName.Contains("worn_hospital_gurney") || tName.Contains("gurney"))
            {
                if (t.position.sqrMagnitude < 0.001f) continue;

                if (!gurneys.Contains(t))
                {
                    gurneys.Add(t);
                }
            }
        }

        int gurneysProcessed = 0;
        foreach (Transform gurney in gurneys)
        {
            List<GameObject> childItems = new List<GameObject>();
            Transform[] children = gurney.GetComponentsInChildren<Transform>(true);
            foreach (Transform c in children)
            {
                if (c == gurney || c == null) continue;
                string cName = c.name.ToLower();
                bool isItem = cName.Contains("papel") || cName.Contains("note") || cName.Contains("lore") ||
                              cName.Contains("battery") || cName.Contains("batery") || cName.Contains("pila") ||
                              cName.Contains("fuse") || cName.Contains("fusible") ||
                              c.GetComponent<NoteItem>() != null || c.GetComponent<LoreNoteItem>() != null ||
                              c.GetComponent<BatteryItem>() != null || c.GetComponent<FuseItem>() != null;

                if (isItem)
                {
                    if (!childItems.Contains(c.gameObject))
                    {
                        childItems.Add(c.gameObject);
                    }
                }
            }

            if (childItems.Count > 1)
            {
                ShuffleList(childItems);

                // Activar SOLO 1 ítem al azar de los 3 presentes en la camilla (activando mallas hijas)
                ActivateItemWithAllChildren(childItems[0]);

                // Desactivar los demás ítems de la camilla
                for (int i = 1; i < childItems.Count; i++)
                {
                    childItems[i].SetActive(false);
                }
                gurneysProcessed++;
            }
        }

        Debug.Log($"[FixedHospital] Se procesaron {gurneys.Count} camillas (Worn_Hospital_Gurney). Se seleccionó 1 único ítem al azar en cada una.");
    }

    private void SetupItems()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        string[] batteryKeywords = new string[] { "batery", "battery", "pila" };
        string[] batteryExcludes = new string[] { "canvas", "ui", "spawn", "manager" };

        string[] fuseKeywords = new string[] { "fuse", "fusible" };
        string[] fuseExcludes = new string[] { "fusebox", "fuse_box", "powerbox", "canvas", "ui", "spawn", "manager" };

        List<GameObject> allBatteryObjects = new List<GameObject>();
        List<GameObject> allFuseObjects = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            // 1. Configurar BATERÍAS crudas
            if (IsTopLevelElement(t, batteryKeywords, batteryExcludes))
            {
                if (t.position.sqrMagnitude < 0.001f) continue;

                GameObject targetBat = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetBat = mr.gameObject;

                BatteryItem batComp = targetBat.GetComponent<BatteryItem>();
                if (batComp == null) batComp = targetBat.AddComponent<BatteryItem>();

                BoxCollider bc = targetBat.GetComponent<BoxCollider>();
                if (bc == null) bc = targetBat.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = Vector3.zero;
                
                Vector3 lossy = targetBat.transform.lossyScale;
                bc.size = new Vector3(
                    lossy.x > 0.001f ? 0.35f / lossy.x : 0.35f,
                    lossy.y > 0.001f ? 0.25f / lossy.y : 0.25f,
                    lossy.z > 0.001f ? 0.35f / lossy.z : 0.35f
                );

                batComp.interactDistance = 3.5f;

                if (!allBatteryObjects.Contains(targetBat))
                {
                    allBatteryObjects.Add(targetBat);
                }
            }

            // 2. Configurar FUSIBLES crudos
            if (IsTopLevelElement(t, fuseKeywords, fuseExcludes))
            {
                if (t.position.sqrMagnitude < 0.001f) continue;

                GameObject targetFuse = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetFuse = mr.gameObject;

                FuseItem fuseComp = targetFuse.GetComponent<FuseItem>();
                if (fuseComp == null) fuseComp = targetFuse.AddComponent<FuseItem>();

                BoxCollider bc = targetFuse.GetComponent<BoxCollider>();
                if (bc == null) bc = targetFuse.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = Vector3.zero;

                Vector3 lossy = targetFuse.transform.lossyScale;
                bc.size = new Vector3(
                    lossy.x > 0.001f ? 0.55f / lossy.x : 0.55f,
                    lossy.y > 0.001f ? 0.45f / lossy.y : 0.45f,
                    lossy.z > 0.001f ? 0.55f / lossy.z : 0.55f
                );

                fuseComp.interactDistance = 3.5f;

                if (!allFuseObjects.Contains(targetFuse))
                {
                    allFuseObjects.Add(targetFuse);
                }
            }
        }

        // Balance de Dificultad Intermedia para BATERÍAS: Activar 5 baterías de inicio
        ShuffleList(allBatteryObjects);
        int initialActiveBats = Mathf.Min(5, allBatteryObjects.Count);
        backupBatteryPool.Clear();

        int batteriesConfigured = 0;
        for (int i = 0; i < allBatteryObjects.Count; i++)
        {
            if (i < initialActiveBats)
            {
                ActivateItemWithAllChildren(allBatteryObjects[i]);
                batteriesConfigured++;
            }
            else
            {
                allBatteryObjects[i].SetActive(false);
                backupBatteryPool.Add(allBatteryObjects[i]);
            }
        }

        // Balance de Dificultad Intermedia para FUSIBLES: Activar 4 fusibles de inicio
        ShuffleList(allFuseObjects);
        int initialActiveFuses = Mathf.Min(4, allFuseObjects.Count);
        backupFusePool.Clear();

        int fusesConfigured = 0;
        for (int i = 0; i < allFuseObjects.Count; i++)
        {
            if (i < initialActiveFuses)
            {
                ActivateItemWithAllChildren(allFuseObjects[i]);
                fusesConfigured++;
            }
            else
            {
                allFuseObjects[i].SetActive(false);
                backupFusePool.Add(allFuseObjects[i]);
            }
        }

        Debug.Log($"[FixedHospital] Se configuraron {batteriesConfigured} baterías de inicio ({backupBatteryPool.Count} en reserva) y {fusesConfigured} fusibles de inicio ({backupFusePool.Count} en reserva).");
    }

    private IEnumerator CheckAndRespawnFusesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5.0f);

            PowerBox pBox = FindFirstObjectByType<PowerBox>();
            if (pBox != null && pBox.isPowerOut)
            {
                FuseItem[] activeFusesOnMap = FindObjectsByType<FuseItem>(FindObjectsSortMode.None);
                int activeCount = 0;
                foreach (var f in activeFusesOnMap)
                {
                    if (f != null && f.gameObject.activeInHierarchy) activeCount++;
                }

                if (activeCount == 0 && !isFuseRespawnTimerRunning)
                {
                    StartCoroutine(RespawnFuseAfterDelay(45.0f));
                }
            }
        }
    }

    private IEnumerator RespawnFuseAfterDelay(float delay)
    {
        isFuseRespawnTimerRunning = true;
        Debug.Log($"[FixedHospital] No quedan fusibles activos en el mapa. Temporizador de respawn iniciado ({delay}s)...");

        yield return new WaitForSeconds(delay);

        GameObject fuseToActivate = null;
        if (backupFusePool.Count > 0)
        {
            fuseToActivate = backupFusePool[0];
            backupFusePool.RemoveAt(0);
        }
        else
        {
            FuseItem[] allFuses = FindObjectsByType<FuseItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var f in allFuses)
            {
                if (f != null && !f.gameObject.activeInHierarchy)
                {
                    fuseToActivate = f.gameObject;
                    break;
                }
            }
        }

        if (fuseToActivate != null)
        {
            ActivateItemWithAllChildren(fuseToActivate);
            Debug.Log($"[FixedHospital] ¡FUSIBLE REESPAWNEADO! En {fuseToActivate.name} en posición {fuseToActivate.transform.position}");
            PlayerMonologueManager.ShowDialogue("Un fusible de repuesto ha aparecido en una de las camillas o muebles del hospital...", 5.0f);
        }

        isFuseRespawnTimerRunning = false;
    }

    private IEnumerator CheckAndRespawnBatteriesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5.0f);

            FlashlightController fc = FindFirstObjectByType<FlashlightController>();
            bool isLowBattery = fc == null || fc.currentBattery < 50f;

            BatteryItem[] activeBatsOnMap = FindObjectsByType<BatteryItem>(FindObjectsSortMode.None);
            int activeCount = 0;
            foreach (var b in activeBatsOnMap)
            {
                if (b != null && b.gameObject.activeInHierarchy) activeCount++;
            }

            if (activeCount == 0 && isLowBattery && !isBatteryRespawnTimerRunning)
            {
                StartCoroutine(RespawnBatteryAfterDelay(45.0f));
            }
        }
    }

    private IEnumerator RespawnBatteryAfterDelay(float delay)
    {
        isBatteryRespawnTimerRunning = true;
        Debug.Log($"[FixedHospital] No quedan baterías activas en el mapa y la linterna está baja. Temporizador de respawn iniciado ({delay}s)...");

        yield return new WaitForSeconds(delay);

        GameObject batToActivate = null;
        if (backupBatteryPool.Count > 0)
        {
            batToActivate = backupBatteryPool[0];
            backupBatteryPool.RemoveAt(0);
        }
        else
        {
            BatteryItem[] allBats = FindObjectsByType<BatteryItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in allBats)
            {
                if (b != null && !b.gameObject.activeInHierarchy)
                {
                    batToActivate = b.gameObject;
                    break;
                }
            }
        }

        if (batToActivate != null)
        {
            batToActivate.SetActive(true);
            ActivateItemWithAllChildren(batToActivate);
            Debug.Log($"[FixedHospital] ¡BATERÍA REESPAWNEADA! En {batToActivate.name} en posición {batToActivate.transform.position}");
            PlayerMonologueManager.ShowDialogue("Parece que ha aparecido una batería de repuesto en una de las camillas o muebles del hospital...", 5.0f);
        }

        isBatteryRespawnTimerRunning = false;
    }
}
