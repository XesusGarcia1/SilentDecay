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

        // 3. Seleccionar EXACTAMENTE 1 ascensor ALEATORIO como el Elevador Real de Escape y apagar todos los demás
        int realIndex = Random.Range(0, validElevators.Count);
        ElevatorController realElevator = validElevators[realIndex];

        // Aplicar opciones de desarrollo/pruebas configuradas en el Inspector de HospitalFixedMapLogic
        realElevator.startWithKeycard = startWithKeycard;
        realElevator.bypassKeycard = bypassKeycard;
        realElevator.bypassPower = bypassPower;

        if (startWithKeycard || bypassKeycard)
        {
            ElevatorController.hasKeycard = true;
        }

        Debug.Log($"[FixedHospital] Se detectaron {validElevators.Count} ascensores en el mapa. ELEVADOR REAL DE ESCAPE SELECCIONADO: {realElevator.name} en {realElevator.transform.position}. Se ocultaron los otros {validElevators.Count - 1}.");

        for (int i = 0; i < validElevators.Count; i++)
        {
            ElevatorController ec = validElevators[i];
            Transform rootGroup = GetElevatorGroupRoot(ec.transform);

            if (ec == realElevator)
            {
                ec.isFake = false;
                ec.gameObject.SetActive(true);
                if (rootGroup != null) rootGroup.gameObject.SetActive(true);
            }
            else
            {
                ec.isFake = true;
                ec.gameObject.SetActive(false);
                if (rootGroup != null)
                {
                    rootGroup.gameObject.SetActive(false); // Apagar todo el grupo del ascensor falso (luces, mallas, botones)
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
