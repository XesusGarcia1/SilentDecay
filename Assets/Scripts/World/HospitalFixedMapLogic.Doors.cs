using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private void SetupDoors()
    {
        string[] doorKeywords = new string[] { "p_door_01_", "puerta", "door" };
        string[] doorExcludes = new string[] { "base", "frame", "marco", "hinge", "autohinge", "player", "canvas", "ui" };

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);

        foreach (Transform t in allTransforms)
        {
            if (t == null || t.name.Contains("_AutoHinge")) continue;

            // NUNCA procesar al jugador ni a sus objetos hijos
            Transform root = t.root;
            if (root != null)
            {
                string rName = root.name.ToLower();
                if (rName.Contains("player") || rName.Contains("nestedparent") || root.CompareTag("Player"))
                {
                    continue;
                }
            }

            string n = t.name.ToLower();
            bool matches = false;
            foreach (var kw in doorKeywords)
            {
                if (n.Contains(kw)) { matches = true; break; }
            }
            if (!matches) continue;

            bool excluded = false;
            foreach (var ex in doorExcludes)
            {
                if (n.Contains(ex)) { excluded = true; break; }
            }
            if (excluded) continue;

            // Ignorar puertas de ascensor
            Transform pCheck = t;
            bool isElevator = false;
            while (pCheck != null)
            {
                string pName = pCheck.name.ToLower();
                if (pName.Contains("ascensor") || pName.Contains("elevator") || pCheck.GetComponent<ElevatorController>() != null)
                {
                    isElevator = true;
                    break;
                }
                pCheck = pCheck.parent;
            }
            if (isElevator) continue;

            // Si ya tiene un ProceduralDoorInteract en sí misma o en su jerarquía, respetarlo
            ProceduralDoorInteract existingScript = t.GetComponent<ProceduralDoorInteract>();
            if (existingScript == null) existingScript = t.GetComponentInParent<ProceduralDoorInteract>();

            if (existingScript != null)
            {
                continue;
            }

            // Asignar el componente de interacción a la puerta para que abra hacia adentro desde el otro lado de la bisagra
            ProceduralDoorInteract doorInteract = t.gameObject.AddComponent<ProceduralDoorInteract>();
            doorInteract.autoFixCenterPivot = true;
            doorInteract.hingeOnRightSide = true; // Pivote en el otro borde de la pared
            doorInteract.openAngle = -90f; // Giro hacia adentro de la habitación
            doorInteract.interactDistance = 2.8f;

            // Bloquear automáticamente si pertenece a la Oficina del Director
            Transform p = t;
            while (p != null)
            {
                if (p.name.ToLower().Contains("director"))
                {
                    doorInteract.isLocked = true;
                    break;
                }
                p = p.parent;
            }
        }
    }
}
