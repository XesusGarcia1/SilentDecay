using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private static List<GameObject> activeNoteObjects = new List<GameObject>();

    private void SetupCodeNotes(Transform[] allTransforms)
    {
        activeNoteObjects.Clear();

        // PASO 1: Buscar TODOS los objetos 'NotaCode' del mapa
        List<GameObject> allNotaObjects = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string tName = t.name.Trim();
            
            if (tName.StartsWith("NotaCode"))
            {
                // Si este es el objeto hijo (la malla), lo ignoramos para no contarlo doble
                if (t.parent != null && t.parent.name.StartsWith("NotaCode")) continue;

                // DESACTIVAR TODOS por defecto
                t.gameObject.SetActive(false);
                
                if (!allNotaObjects.Contains(t.gameObject))
                {
                    allNotaObjects.Add(t.gameObject);
                }
            }
        }

        Debug.Log($"[FixedHospital] Se encontraron {allNotaObjects.Count} objetos 'NotaCode' validos.");

        // PASO 2: Mezclar aleatoriamente
        ShuffleList(allNotaObjects);

        // PASO 3: Seleccionar EXACTAMENTE 7 para las notas de codigo y activarlas
        int codesToActivate = Mathf.Min(7, allNotaObjects.Count);
        Debug.Log($"[FixedHospital] Intentando activar {codesToActivate} notas de {allNotaObjects.Count} encontradas.");

        for (int i = 0; i < codesToActivate; i++)
        {
            GameObject nota = allNotaObjects[i];

            // ACTIVAR esta nota y todos sus hijos (malla)
            nota.SetActive(true);
            ActivateItemWithAllChildren(nota);

            // Reutilizar o agregar componente de nota de codigo
            NoteItem nComp = nota.GetComponent<NoteItem>();
            if (nComp == null) nComp = nota.AddComponent<NoteItem>();
            nComp.digitPosition = (i + 1);
            nComp.digitValue = int.Parse(correctKeypadCode[i].ToString());
            nComp.interactDistance = 4.5f;

            BoxCollider bc = nota.GetComponent<BoxCollider>();
            if (bc == null) bc = nota.AddComponent<BoxCollider>();
            ConfigureBoxColliderFromRenderers(nota, bc, true, 1.5f);

            activeNoteObjects.Add(nota);
            Debug.Log($"[FixedHospital] NOTA CODIGO {i + 1}/7 activada en {nota.name}: Digito #{nComp.digitPosition} = {nComp.digitValue}");
        }
    }

    private void SetupLoreNotes()
    {
        // PASO 1: Buscar TODOS los objetos 'NotaLore' del mapa
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> allLoreObjects = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string tName = t.name.Trim();

            if (tName.StartsWith("NotaLore"))
            {
                // Si este es el objeto hijo (la malla), lo ignoramos para no contarlo doble
                if (t.parent != null && t.parent.name.StartsWith("NotaLore")) continue;

                // DESACTIVAR TODOS los objetos NotaLore por defecto
                t.gameObject.SetActive(false);

                if (!allLoreObjects.Contains(t.gameObject))
                {
                    allLoreObjects.Add(t.gameObject);
                }
            }
        }

        Debug.Log($"[FixedHospital] Se encontraron {allLoreObjects.Count} objetos 'NotaLore' validos.");

        // PASO 3: Mezclar aleatoriamente
        ShuffleList(allLoreObjects);

        string[] loreTitles = new string[]
        {
            "Diario del Bibliotecario (BookHead)",
            "Informe de Psiquiatria (TheCreep)",
            "Memorandum de Evacuacion"
        };
        string[] loreBodies = new string[]
        {
            "<b>REGISTRO DEL DIARIO - 18 DE OCTUBRE:</b>\n\n" +
            "Ese maldito monstruo... la criatura con cabeza de libro que merodea la biblioteca principal.\n" +
            "Confirmado: <i>NO TIENE OJOS</i>. Es completamente ciego.\n" +
            "Sin embargo, su oido es increiblemente agudo.\n" +
            "Si caminas despacio, te ignorara por completo. Pero si entras en panico y corres <b>(sprint)</b>,\n" +
            "sabra exactamente donde estas al instante y te perseguira.\n" +
            "Guarda silencio si quieres conservar la cabeza.",

            "<b>EXPEDIENTE ANOMALO #09-B:</b>\n\n" +
            "Los pacientes del Pabellon Este reportan avistamientos de un ser deforme en el suelo.\n" +
            "Se arrastra como un insecto y lo llaman 'TheCreep' (El Rastrero).\n" +
            "El personal reporta que prefiere quedarse en las esquinas mas oscuras del hospital.\n" +
            "Es extremadamente agresivo. Si te encuentra, intentara acorralarte y atacarte.\n" +
            "Para escapar de el, debes correr hacia el spawn o buscar zonas iluminadas.\n" +
            "Nunca te quedes quieto en los callejones oscuros.",

            "<b>ORDEN DE EVACUACION INTERNA:</b>\n\n" +
            "A todo el personal administrativo:\n" +
            "La fuga biologica ha alcanzado los niveles subterraneos del ala oeste.\n" +
            "El ascensor de escape principal de la oficina del director ha sido bloqueado por el protocolo de cuarentena.\n" +
            "Se requiere una contrasena cifrada de 7 digitos para restablecerlo.\n" +
            "Las hojas de codigos de seguridad se han esparcido por las habitaciones para evitar que los sujetos de prueba las encuentren.\n" +
            "Busca los 7 digitos y evacua inmediatamente."
        };

        // PASO 4: Seleccionar EXACTAMENTE 3 para lore y activarlas
        int loresToActivate = Mathf.Min(3, allLoreObjects.Count);

        for (int i = 0; i < loresToActivate; i++)
        {
            GameObject lore = allLoreObjects[i];

            // ACTIVAR este papel lore y todos sus hijos (malla, luces)
            lore.SetActive(true);
            ActivateItemWithAllChildren(lore);

            LoreNoteItem lComp = lore.GetComponent<LoreNoteItem>();
            if (lComp == null) lComp = lore.AddComponent<LoreNoteItem>();

            lComp.loreId = i + 1;
            lComp.noteTitle = loreTitles[i];
            lComp.noteBody = loreBodies[i];
            lComp.interactDistance = 4.5f;

            BoxCollider bc = lore.GetComponent<BoxCollider>();
            if (bc == null) bc = lore.AddComponent<BoxCollider>();
            bc.isTrigger = true;

            activeNoteObjects.Add(lore);
            Debug.Log($"[FixedHospital] NOTA LORE #{i + 1} activada en '{lore.name}' ({lore.transform.position}).");
        }
    }
}
