using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private void SetupHideBeds()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int bedsConfigured = 0;

        string[] bedKeywords = new string[] { "cama", "bed", "camilla", "gurney" };
        string[] bedExcludes = new string[] { "spawn", "manager", "canvas", "ui", "p_note", "note", "papel", "battery", "fuse", "door", "puerta", "p_door", "bedding", "p_bedbedding" };

        // 1. Limpiar componentes Bed erróneos colocados previamente en partes de puertas
        Bed[] existingBeds = FindObjectsByType<Bed>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Bed b in existingBeds)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToLower();
            if (n.Contains("door") || n.Contains("puerta") || n.Contains("bedding") || b.transform.root.name.ToLower().Contains("door"))
            {
                Destroy(b);
            }
        }

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string tName = t.name.ToLower();

            // Evitar categorizar objetos de puertas como camas de escondite
            if (tName.Contains("door") || tName.Contains("puerta") || tName.Contains("bedding")) continue;
            if (t.GetComponentInParent<ProceduralDoorInteract>() != null) continue;

            if (IsTopLevelElement(t, bedKeywords, bedExcludes))
            {
                // Ignorar objetos demasiado pequeños que no sean camas reales de ocultarse
                if (t.lossyScale.x < 0.5f || t.lossyScale.z < 0.5f) continue;

                // Asegurar el componente Bed en la cama
                Bed bedComp = t.GetComponent<Bed>();
                if (bedComp == null) bedComp = t.gameObject.AddComponent<Bed>();

                // Asegurar el punto hijo de posición de escondite (hidePosition)
                Transform hidePos = t.Find("HidePosition");
                if (hidePos == null)
                {
                    GameObject hObj = new GameObject("HidePosition");
                    hObj.transform.SetParent(t, false);
                    hObj.transform.localPosition = new Vector3(0f, 0.15f, 0f); // Levantar un poquito del suelo
                    hObj.transform.localRotation = Quaternion.identity;
                    hidePos = hObj.transform;
                }
                bedComp.hidePosition = hidePos;

                // Asegurar colisionador trigger para que se pueda interactuar posicionado sobre la cama física
                BoxCollider box = t.GetComponent<BoxCollider>();
                if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
                ConfigureBoxColliderFromRenderers(t.gameObject, box, true, 0.9f);

                // Limitar la altura del colisionador de la cama al colchón para evitar que se extienda hacia la cabecera/pared
                Vector3 bSize = box.size;
                Vector3 bCenter = box.center;
                if (bSize.y > 0.6f)
                {
                    bCenter.y = bCenter.y - (bSize.y - 0.6f) * 0.5f;
                    bSize.y = 0.6f;
                    box.size = bSize;
                    box.center = bCenter;
                }

                bedsConfigured++;
            }
        }

        Debug.Log($"[FixedHospital] Se auto-configuraron {bedsConfigured} camas para esconderse en el mapa.");
    }
}
