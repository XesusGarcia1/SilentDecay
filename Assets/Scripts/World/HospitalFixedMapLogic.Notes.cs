using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private void SetupCodeNotes(Transform[] allTransforms)
    {
        // 4. NOTAS CON EL CÓDIGO - Buscar notas en el mapa y asignar de forma limpia los 7 dígitos de la clave
        NoteItem[] existingNotes = FindObjectsByType<NoteItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (NoteItem oldN in existingNotes)
        {
            if (oldN != null)
            {
                oldN.gameObject.SetActive(false);
            }
        }

        List<GameObject> validPaperObjects = new List<GameObject>();
        string[] noteKeywords = new string[] { "papel", "p_note", "note" };
        string[] noteExcludes = new string[] { "canvas", "ui", "spawn", "manager", "lore" };
        
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, noteKeywords, noteExcludes))
            {
                if (t.position.sqrMagnitude < 0.001f) continue;

                GameObject targetPaper = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetPaper = mr.gameObject;

                if (!validPaperObjects.Contains(targetPaper))
                {
                    validPaperObjects.Add(targetPaper);
                }
            }
        }

        foreach (NoteItem oldN in existingNotes)
        {
            if (oldN != null && !validPaperObjects.Contains(oldN.gameObject))
            {
                if (oldN.transform.position.sqrMagnitude >= 0.001f)
                {
                    validPaperObjects.Add(oldN.gameObject);
                }
            }
        }

        ShuffleList(validPaperObjects);

        // Desactivar todos los objetos de papel base para evitar notas duplicadas
        foreach (GameObject p in validPaperObjects)
        {
            if (p != null) p.SetActive(false);
        }

        // Si hay menos de 7 papeles en los prefabs del mapa, crear notas adicionales en camillas/muebles hasta tener EXACTAMENTE 7 NOTAS
        if (validPaperObjects.Count < 7)
        {
            int missing = 7 - validPaperObjects.Count;
            Debug.Log($"[FixedHospital] Se encontraron {validPaperObjects.Count} papeles base. Generando {missing} notas adicionales en camillas/muebles para garantizar las 7 NOTAS DE CÓDIGO.");

            // Buscar camillas y escritorios de la escena para colocar las notas faltantes
            List<Transform> itemSurfaces = new List<Transform>();
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.name.ToLower();
                if ((n.Contains("gurney") || n.Contains("camilla") || n.Contains("desk") || n.Contains("table") || n.Contains("mueble") || n.Contains("apothecary") || n.Contains("room")) && t.position.sqrMagnitude > 0.01f)
                {
                    itemSurfaces.Add(t);
                }
            }

            ShuffleList(itemSurfaces);

            GameObject samplePaper = (validPaperObjects.Count > 0) ? validPaperObjects[0] : loreNotePrefab;
            if (samplePaper != null)
            {
                for (int m = 0; m < missing; m++)
                {
                    Transform parentSurface = (m < itemSurfaces.Count) ? itemSurfaces[m] : null;
                    Vector3 spawnPos = (parentSurface != null) ? parentSurface.position + Vector3.up * 0.85f : new Vector3(m * 2f, 0.8f, m * 2f);

                    RaycastHit surfaceHit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, out surfaceHit, 2.0f))
                    {
                        if (!surfaceHit.collider.isTrigger)
                        {
                            spawnPos = surfaceHit.point + Vector3.up * 0.02f;
                        }
                    }

                    GameObject newPaper = Instantiate(samplePaper, spawnPos, Quaternion.identity, parentSurface);
                    newPaper.name = $"[Hospital_CodeNote_Extra_{m + 1}]";
                    validPaperObjects.Add(newPaper);
                }
            }
        }

        // Activar y configurar EXACTAMENTE 7 NOTAS con los dígitos de la clave (posiciones 1 a 7)
        for (int i = 0; i < 7; i++)
        {
            if (i >= validPaperObjects.Count) break;

            GameObject paper = validPaperObjects[i];
            ActivateItemWithAllChildren(paper);

            NoteItem nComp = paper.GetComponent<NoteItem>();
            if (nComp == null) nComp = paper.AddComponent<NoteItem>();

            BoxCollider bc = paper.GetComponent<BoxCollider>();
            if (bc == null) bc = paper.AddComponent<BoxCollider>();
            ConfigureBoxColliderFromRenderers(paper, bc, true, 1.5f);

            nComp.digitPosition = (i + 1);
            nComp.digitValue = int.Parse(correctKeypadCode[i].ToString());
            nComp.interactDistance = 4.5f;

            Debug.Log($"[FixedHospital] ✅ NOTA CÓDIGO {i + 1}/7 activada en {paper.name} ({paper.transform.position}): Dígito #{nComp.digitPosition} = {nComp.digitValue} (Clave Director: {correctKeypadCode})");
        }
    }

    private void SetupLoreNotes()
    {
        if (loreNotePrefab == null)
        {
            Debug.LogWarning("[FixedHospital] No se asignó el prefab de nota de Lore en HospitalFixedMapLogic.");
            return;
        }

        // Buscar el objeto "Rooms" para encontrar las habitaciones del mapa
        GameObject roomsParent = GameObject.Find("Rooms");
        if (roomsParent == null) roomsParent = GameObject.Find("BackroomsHospital/Rooms");

        if (roomsParent == null)
        {
            Debug.LogWarning("[FixedHospital] No se encontró el objeto 'Rooms' en la escena para spawnear notas de Lore.");
            return;
        }

        List<Transform> roomList = new List<Transform>();
        for (int i = 0; i < roomsParent.transform.childCount; i++)
        {
            roomList.Add(roomsParent.transform.GetChild(i));
        }

        if (roomList.Count == 0)
        {
            Debug.LogWarning("[FixedHospital] El objeto 'Rooms' no tiene habitaciones hijas para el lore.");
            return;
        }

        // Mezclar las habitaciones para elegir aleatorias
        ShuffleList(roomList);

        // Textos de lore originales
        string[] loreTitles = new string[]
        {
            "Diario del Bibliotecario (BookHead)",
            "Informe de Psiquiatría (TheCreep)",
            "Memorándum de Evacuación"
        };
        string[] loreBodies = new string[]
        {
            "<b>REGISTRO DEL DIARIO - 18 DE OCTUBRE:</b>\n\n" +
            "Ese maldito monstruo... la criatura con cabeza de libro que merodea la biblioteca principal.\n" +
            "Confirmado: <i>NO TIENE OJOS</i>. Es completamente ciego.\n" +
            "Sin embargo, su oído es increíblemente agudo.\n" +
            "Si caminas despacio, te ignorará por completo. Pero si entras en pánico y corres <b>(sprint)</b>,\n" +
            "sabrá exactamente dónde estás al instante y te perseguirá.\n" +
            "Guarda silencio si quieres conservar la cabeza.",

            "<b>EXPEDIENTE ANÓMALO #09-B:</b>\n\n" +
            "Los pacientes del Pabellón Este reportan avistamientos de un ser deforme en el suelo.\n" +
            "Se arrastra como un insecto y lo llaman 'TheCreep' (El Rastrero).\n" +
            "El personal reporta que prefiere quedarse en las esquinas más oscuras del hospital.\n" +
            "Es extremadamente agresivo. Si te encuentra, intentará acorralarte y atacarte.\n" +
            "Para escapar de él, debes correr hacia el spawn o buscar zonas iluminadas.\n" +
            "Nunca te quedes quieto en los callejones oscuros.",

            "<b>ORDEN DE EVACUACIÓN INTERNA:</b>\n\n" +
            "A todo el personal administrativo:\n" +
            "La fuga biológica ha alcanzado los niveles subterráneos del ala oeste.\n" +
            "El ascensor de escape principal de la oficina del director ha sido bloqueado por el protocolo de cuarentena.\n" +
            "Se requiere una contraseña cifrada de 7 dígitos para restablecerlo.\n" +
            "Las hojas de códigos de seguridad se han esparcido por las habitaciones para evitar que los sujetos de prueba las encuentren.\n" +
            "Busca los 7 dígitos y evacua inmediatamente."
        };

        int spawnedCount = 0;
        int maxNotesToSpawn = Mathf.Min(3, roomList.Count);

        for (int i = 0; i < roomList.Count; i++)
        {
            if (spawnedCount >= maxNotesToSpawn) break;

            Transform chosenRoom = roomList[i];
            
            // Generar posición aleatoria dentro de la habitación
            Vector3 spawnPos = chosenRoom.position + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));

            // Raycast hacia abajo para asentarlo exactamente en el suelo
            RaycastHit hit;
            bool hitFloor = false;

            if (Physics.Raycast(spawnPos + Vector3.up * 2.0f, Vector3.down, out hit, 5.0f))
            {
                if (!hit.collider.isTrigger)
                {
                    hitFloor = true;
                }
            }

            if (hitFloor)
            {
                Vector3 spawnPosElevated = hit.point + hit.normal * 0.02f; // Asentado 2cm sobre el suelo
                GameObject loreNote = Instantiate(loreNotePrefab, spawnPosElevated, Quaternion.identity, chosenRoom);
                loreNote.name = $"[Hospital_LoreNote_{spawnedCount + 1}]";
                ActivateItemWithAllChildren(loreNote);

                // Forzar orientación horizontal plana sobre el suelo para evitar que quede clavado en vertical
                Renderer noteRen = loreNote.GetComponentInChildren<Renderer>();
                if (noteRen != null && noteRen.bounds.size.y > noteRen.bounds.size.x)
                {
                    loreNote.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                }
                else
                {
                    loreNote.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                if (hit.normal != Vector3.zero)
                {
                    loreNote.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * loreNote.transform.rotation;
                }

                LoreNoteItem lComp = loreNote.GetComponent<LoreNoteItem>();
                if (lComp == null) lComp = loreNote.AddComponent<LoreNoteItem>();

                // Configurar datos del componente
                lComp.loreId = spawnedCount + 1;
                lComp.noteTitle = loreTitles[spawnedCount];
                lComp.noteBody = loreBodies[spawnedCount];
                lComp.interactDistance = 4.5f;

                // Asegurar BoxCollider trigger en la nota clonada
                BoxCollider bc = loreNote.GetComponent<BoxCollider>();
                if (bc == null) bc = loreNote.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = Vector3.zero;
                bc.size = new Vector3(0.55f, 0.35f, 0.55f);

                spawnedCount++;
            }
        }

        Debug.Log($"[FixedHospital] Se instanciaron {spawnedCount} notas de Lore en el suelo de habitaciones aleatorias.");
    }
}
