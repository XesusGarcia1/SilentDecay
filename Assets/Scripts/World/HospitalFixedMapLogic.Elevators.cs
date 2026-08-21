using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private void SetupElevators()
    {
        List<ElevatorController> validElevators = new List<ElevatorController>();

        // 1. Buscar por componente preexistente
        ElevatorController[] allElevatorComps = FindObjectsByType<ElevatorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ElevatorController ec in allElevatorComps)
        {
            if (ec == null) continue;
            if (ec.name.Contains("Manager") || ec.transform.position.sqrMagnitude < 0.001f) continue;

            if (!validElevators.Contains(ec))
            {
                validElevators.Add(ec);
            }
        }

        // 2. Buscar por GameObjects en escena (ej. Props/Ascensor/Ascensor, Ascensor (1), etc.)
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string tName = t.name.ToLower();
            if ((tName.Contains("ascensor") || tName.Contains("elevator")) && !tName.Contains("manager") && !tName.Contains("canvas") && !tName.Contains("arrival"))
            {
                if (t.position.sqrMagnitude < 0.001f) continue;

                // Evitar duplicados si el grupo o un ancestro/hijo ya fue agregado
                bool alreadyInGroup = false;
                foreach (ElevatorController existing in validElevators)
                {
                    if (existing != null && (existing.transform == t || t.IsChildOf(existing.transform) || existing.transform.IsChildOf(t)))
                    {
                        alreadyInGroup = true;
                        break;
                    }
                }

                if (!alreadyInGroup)
                {
                    ElevatorController ec = t.GetComponent<ElevatorController>();
                    if (ec == null) ec = t.gameObject.AddComponent<ElevatorController>();
                    validElevators.Add(ec);
                }
            }
        }

        if (validElevators.Count == 0)
        {
            Debug.LogWarning("[FixedHospital] No se encontraron ascensores válidos en el mapa.");
            return;
        }

        // 3. Seleccionar EXACTAMENTE 1 ascensor ALEATORIO como el Elevador Real de Escape
        int realIndex = Random.Range(0, validElevators.Count);
        ElevatorController realElevator = validElevators[realIndex];
        Transform rootRealGroup = GetElevatorGroupRoot(realElevator.transform);

        // Aplicar opciones de desarrollo/pruebas configuradas en el Inspector de HospitalFixedMapLogic
        realElevator.startWithKeycard = startWithKeycard;
        realElevator.bypassKeycard = bypassKeycard;
        realElevator.bypassPower = bypassPower;

        if (startWithKeycard || bypassKeycard)
        {
            ElevatorController.hasKeycard = true;
        }

        Debug.Log($"[FixedHospital] Se detectaron {validElevators.Count} ascensores en el mapa. ELEVADOR REAL DE ESCAPE SELECCIONADO: {realElevator.name} en {realElevator.transform.position}. Se ocultaron los otros {validElevators.Count - 1}.");

        // 4. Activar el ascensor real y ocultar/desactivar al 100% los ascensores falsos
        foreach (ElevatorController ec in validElevators)
        {
            if (ec == null) continue;

            if (ec == realElevator)
            {
                ec.isFake = false;
                ec.gameObject.SetActive(true);

                Transform rootGroup = GetElevatorGroupRoot(ec.transform);
                if (rootGroup != null) rootGroup.gameObject.SetActive(true);

                // Forzar activación de renderers y luces en el ascensor real
                Renderer[] realRenderers = ec.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in realRenderers) if (r != null) r.enabled = true;

                Light[] realLights = ec.GetComponentsInChildren<Light>(true);
                foreach (Light l in realLights) if (l != null) { l.enabled = true; l.gameObject.SetActive(true); }
            }
            else
            {
                ec.isFake = true;
                ec.gameObject.SetActive(false);

                Vector3 fakePos = ec.transform.position;

                // Desactivar jerarquía de mallas del ascensor falso
                Transform rootGroup = GetElevatorGroupRoot(ec.transform);
                if (rootGroup != null && rootGroup != rootRealGroup && !realElevator.transform.IsChildOf(rootGroup))
                {
                    rootGroup.gameObject.SetActive(false);
                }

                // Ocultar mallas y luces por proximidad (3.5m) para capturar cualquier sub-objeto o hermano visual
                Renderer[] allSceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Renderer r in allSceneRenderers)
                {
                    if (r == null) continue;
                    if (r.transform.IsChildOf(realElevator.transform) || (rootRealGroup != null && r.transform.IsChildOf(rootRealGroup))) continue;

                    string rName = r.name.ToLower();
                    string pName = r.transform.parent != null ? r.transform.parent.name.ToLower() : "";

                    if (rName.Contains("ascensor") || rName.Contains("elevator") || pName.Contains("ascensor") || pName.Contains("elevator"))
                    {
                        if (Vector3.Distance(r.transform.position, fakePos) < 3.5f)
                        {
                            r.enabled = false;
                            r.gameObject.SetActive(false);
                        }
                    }
                }

                Light[] allSceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Light l in allSceneLights)
                {
                    if (l == null) continue;
                    if (l.transform.IsChildOf(realElevator.transform) || (rootRealGroup != null && l.transform.IsChildOf(rootRealGroup))) continue;

                    string lName = l.name.ToLower();
                    string pName = l.transform.parent != null ? l.transform.parent.name.ToLower() : "";

                    if (lName.Contains("ascensor") || lName.Contains("elevator") || pName.Contains("ascensor") || pName.Contains("elevator"))
                    {
                        if (Vector3.Distance(l.transform.position, fakePos) < 4.5f)
                        {
                            l.enabled = false;
                            l.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
    }

    private Transform GetElevatorGroupRoot(Transform t)
    {
        if (t == null) return null;
        Transform current = t;
        while (current.parent != null)
        {
            string pName = current.parent.name.ToLower();
            if (pName.Equals("ascensor") || pName.Equals("ascensores") || pName.Equals("props") || pName.Contains("modular"))
            {
                return current;
            }
            current = current.parent;
        }
        return current;
    }
}
