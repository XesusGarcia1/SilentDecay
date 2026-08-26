using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class HospitalFixedMapLogic
{
    private static List<GameObject> activeNoteObjects = new List<GameObject>();

    private bool IsCodeNoteTransform(Transform t)
    {
        if (t == null) return false;
        string n = t.name.Trim().ToLower();
        return n.StartsWith("notacode");
    }

    private bool IsLoreNoteTransform(Transform t)
    {
        if (t == null) return false;
        string n = t.name.Trim().ToLower();
        return n.StartsWith("notalore");
    }

    private void SetupCodeNotes(Transform[] allTransforms)
    {
        activeNoteObjects.Clear();

        // PASO 1: Buscar TODOS los objetos de notas de código del mapa (NotaCode, Nota, Papel)
        List<GameObject> allNotaObjects = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            
            if (IsCodeNoteTransform(t))
            {
                // Si este es el objeto hijo (la malla), lo ignoramos para no contarlo doble
                if (t.parent != null && IsCodeNoteTransform(t.parent)) continue;

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

            int dPos = i + 1;
            int dVal = int.Parse(correctKeypadCode[i].ToString());

            // Configurar TODOS los componentes NoteItem presentes en el objeto y sus hijos
            NoteItem[] nComps = nota.GetComponentsInChildren<NoteItem>(true);
            if (nComps == null || nComps.Length == 0)
            {
                NoteItem newComp = nota.AddComponent<NoteItem>();
                nComps = new NoteItem[] { newComp };
            }

            foreach (var nComp in nComps)
            {
                if (nComp == null) continue;
                nComp.digitPosition = dPos;
                nComp.digitValue = dVal;
                nComp.interactDistance = 4.5f;
            }

            BoxCollider bc = nota.GetComponent<BoxCollider>();
            if (bc == null) bc = nota.AddComponent<BoxCollider>();
            ConfigureBoxColliderFromRenderers(nota, bc, true, 1.5f);

            activeNoteObjects.Add(nota);
            Debug.Log($"[FixedHospital] NOTA CODIGO {i + 1}/7 activada en {nota.name}: Digito #{dPos} = {dVal}");
        }
    }

    private void SetupLoreNotes()
    {
        // PASO 1: Buscar TODOS los objetos 'NotaLore' / 'PapelLore' del mapa
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> allLoreObjects = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsLoreNoteTransform(t))
            {
                // Si este es el objeto hijo (la malla), lo ignoramos para no contarlo doble
                if (t.parent != null && IsLoreNoteTransform(t.parent)) continue;

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
            "Registro del Dr. Vance (The Amalgam)",
            "Bitácora de Seguridad (Fenómeno de Oscuridad)",
            "Memorándum de Evacuación Médica"
        };
        string[] loreBodies = new string[]
        {
            "<b>EXPEDIENTE MÉDICO #104 - ALA DE CUARENTENA:</b>\n\n" +
            "No es un solo sujeto... es el resultado de la fusión de múltiples cuerpos. Lo llamamos <i>The Amalgam</i>.\n" +
            "Sus extremidades son deformes y alargadas, y emite un llanto lúgubre constante en la penumbra.\n" +
            "Ha desarrollado una agresividad extrema en la oscuridad.\n" +
            "Cuando las luces se apagan y entra en persecución, su velocidad es implacable.\n" +
            "No intentes huir en línea recta: busca una habitación o métete bajo la cama antes de que te vea cara a cara.",

            "<b>BITÁCORA DE SEGURIDAD - 24 DE OCTUBRE:</b>\n\n" +
            "El apagón inicial de 25 segundos no fue una falla común... fue ÉL.\n" +
            "Cada vez que el sistema eléctrico colapsa, los lamentos de la criatura retumban por el hospital.\n" +
            "Y lo peor son los espejismos... a medida que la tensión aumenta, el hospital juega con tu mente.\n" +
            "Verás siluetas sombrías de The Amalgam en los pasillos alternativos.\n" +
            "<i>¡NO TE ACERQUES A ELLAS!</i> Si te aproximas a una de sus sombras, el ser real se materializará ahí y arrancará a correr.",

            "<b>ORDEN DE EVACUACIÓN INTERNA:</b>\n\n" +
            "A todo el personal administrativo y sobrevivientes:\n" +
            "La entidad 'The Amalgam' ha tomado el control del ala principal del hospital.\n" +
            "El ascensor de escape principal de la oficina del director ha sido bloqueado por el protocolo de cuarentena.\n" +
            "Se requiere una contraseña cifrada de 7 dígitos para restablecerlo.\n" +
            "Las hojas de códigos de seguridad se han esparcido por las habitaciones para evitar que los sujetos las encuentren.\n" +
            "Busca los 7 dígitos, mantén encendidos los subgeneradores para calmar la oscuridad y evacua inmediatamente."
        };

        // PASO 4: Seleccionar EXACTAMENTE 3 para lore y activarlas
        int loresToActivate = Mathf.Min(3, allLoreObjects.Count);

        for (int i = 0; i < loresToActivate; i++)
        {
            GameObject lore = allLoreObjects[i];

            // ACTIVAR este papel lore y todos sus hijos (malla, luces)
            lore.SetActive(true);
            ActivateItemWithAllChildren(lore);

            // Configurar TODOS los componentes LoreNoteItem presentes en el objeto y sus hijos
            LoreNoteItem[] lComps = lore.GetComponentsInChildren<LoreNoteItem>(true);
            if (lComps == null || lComps.Length == 0)
            {
                LoreNoteItem newComp = lore.AddComponent<LoreNoteItem>();
                lComps = new LoreNoteItem[] { newComp };
            }

            foreach (var lComp in lComps)
            {
                if (lComp == null) continue;
                lComp.loreId = i + 1;
                lComp.noteTitle = loreTitles[i];
                lComp.noteBody = loreBodies[i];
                lComp.interactDistance = 4.5f;
            }

            BoxCollider bc = lore.GetComponent<BoxCollider>();
            if (bc == null) bc = lore.AddComponent<BoxCollider>();
            bc.isTrigger = true;

            activeNoteObjects.Add(lore);
            Debug.Log($"[FixedHospital] NOTA LORE #{i + 1} activada en '{lore.name}' ({lore.transform.position}): '{loreTitles[i]}'");
        }
    }
}
