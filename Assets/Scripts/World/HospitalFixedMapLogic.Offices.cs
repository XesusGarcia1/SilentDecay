using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ModularHospital;

public partial class HospitalFixedMapLogic
{
    private void SetupRandomElements()
    {
        // 1. GENERADORES (SubGenerators) - Buscar mallas crudas del mapa y configurarlas
        List<SubGenerator> genList = new List<SubGenerator>();
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        string[] genKeywords = new string[] { "gen", "subgenerator", "generator" };
        string[] genExcludes = new string[] { "spawn", "manager", "lamp", "light", "director", "modular" };

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, genKeywords, genExcludes))
            {
                SubGenerator subGen = t.GetComponent<SubGenerator>();
                if (subGen == null) subGen = t.gameObject.AddComponent<SubGenerator>();

                // Asegurar BoxCollider preciso basado en sus mallas
                BoxCollider bCol = t.GetComponent<BoxCollider>();
                if (bCol == null) bCol = t.gameObject.AddComponent<BoxCollider>();
                ConfigureBoxColliderFromRenderers(t.gameObject, bCol, false, 1.1f);

                // Ajustar distancia de interacción dinámica
                subGen.interactDistance = 3.5f * Mathf.Max(1.0f, t.lossyScale.y);

                genList.Add(subGen);
            }
        }

        ShuffleList(genList);

        // Desactivar todos primero
        foreach (var g in genList)
        {
            g.gameObject.SetActive(false);
        }

        // Dejar activos solo 2 aleatorios
        int gensToActive = Mathf.Min(2, genList.Count);
        for (int i = 0; i < genList.Count; i++)
        {
            if (i < gensToActive)
            {
                genList[i].gameObject.SetActive(true);
                genList[i].generatorName = (i == 0) ? "A" : "B";
                genList[i].isOn = false;
                Debug.Log($"[FixedHospital] Generador configurado e instanciado como '{genList[i].generatorName}' en: {genList[i].transform.position}");
            }
        }

        if (gensToActive < 2)
        {
            Debug.LogWarning($"[FixedHospital] ATENCIÓN: Solo se encontraron {genList.Count} objetos tipo Generador en la escena. ¡Necesitas colocar al menos 2!");
        }

        // 2. FUSE BOXES (PowerBox) - Buscar mallas crudas del mapa y configurarlas
        List<PowerBox> fuseList = new List<PowerBox>();
        string[] fuseKeywords = new string[] { "fusebox", "fuse_box", "powerbox" };
        string[] fuseExcludes = new string[] { "spawn", "manager" };

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, fuseKeywords, fuseExcludes))
            {
                PowerBox pBox = t.GetComponent<PowerBox>();
                if (pBox == null) pBox = t.gameObject.AddComponent<PowerBox>();

                // Asegurar BoxCollider preciso basado en sus mallas
                BoxCollider bCol = t.GetComponent<BoxCollider>();
                if (bCol == null) bCol = t.gameObject.AddComponent<BoxCollider>();
                ConfigureBoxColliderFromRenderers(t.gameObject, bCol, false, 1.1f);

                fuseList.Add(pBox);
            }
        }

        ShuffleList(fuseList);

        // Desactivar todas primero
        foreach (var f in fuseList)
        {
            f.gameObject.SetActive(false);
        }

        // Activar solo la primera seleccionada
        if (fuseList.Count > 0)
        {
            fuseList[0].gameObject.SetActive(true);
            Debug.Log($"[FixedHospital] Caja de Fusibles (PowerBox) configurada y activada en: {fuseList[0].transform.position}");
        }
        else
        {
            Debug.LogWarning("[FixedHospital] ATENCIÓN: ¡No se encontró ninguna caja de fusibles (FuseBox) en la escena!");
        }

        // 3. OFICINAS DEL DIRECTOR (KeypadController) - Agrupar por oficina única para evitar conflictos de teclados duplicados
        List<Transform> uniqueOffices = new List<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            string tName = t.name.ToLower();
            if ((tName.Contains("keypad") || tName.Contains("key_pad")) && !tName.Contains("canvas") && !tName.Contains("manager"))
            {
                Transform officeRoot = t.parent;
                while (officeRoot != null)
                {
                    if (officeRoot.name.ToLower().Contains("directoroffice") || officeRoot.name.ToLower().Contains("director_office"))
                    {
                        break;
                    }
                    officeRoot = officeRoot.parent;
                }

                if (officeRoot != null && !uniqueOffices.Contains(officeRoot))
                {
                    // Ignorar si es una plantilla inactiva, si está en el origen (0,0,0) o dentro de la carpeta raíz de prefabs de diseño "Prefabs"
                    bool isTemplate = false;
                    
                    if (officeRoot.position.sqrMagnitude < 0.01f)
                    {
                        isTemplate = true;
                    }
                    else
                    {
                        Transform tempParent = officeRoot;
                        while (tempParent != null)
                        {
                            if (tempParent.name.ToLower().Contains("prefabs") && tempParent.parent == null)
                            {
                                isTemplate = true;
                                break;
                            }
                            tempParent = tempParent.parent;
                        }
                    }

                    if (!isTemplate && officeRoot.gameObject.activeInHierarchy)
                    {
                        uniqueOffices.Add(officeRoot);
                    }
                }
            }
        }

        ShuffleList(uniqueOffices);

        // Generar clave de 7 dígitos aleatoria variada (dígitos 1-9)
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < 7; i++) sb.Append(Random.Range(1, 10));
        correctKeypadCode = sb.ToString();

        for (int i = 0; i < uniqueOffices.Count; i++)
        {
            Transform officeRoot = uniqueOffices[i];
            if (officeRoot == null) continue;

            Transform[] children = officeRoot.GetComponentsInChildren<Transform>(true);

            // A. Encontrar todos los teclados de esta oficina
            List<KeypadController> keypadsInOffice = new List<KeypadController>();
            foreach (Transform child in children)
            {
                if (child == null) continue;
                string childName = child.name.ToLower();
                if ((childName.Contains("keypad") || childName.Contains("key_pad")) && !childName.Contains("canvas") && !childName.Contains("manager"))
                {
                    // Si este objeto es hijo de otro teclado (ej. 'keypad' dentro de 'Key_pad'), destruir script duplicado
                    bool isChildKeypad = false;
                    foreach (Transform p in children)
                    {
                        if (p != null && p != child && child.IsChildOf(p))
                        {
                            string pName = p.name.ToLower();
                            if ((pName.Contains("keypad") || pName.Contains("key_pad")) && !pName.Contains("canvas") && !pName.Contains("manager"))
                            {
                                isChildKeypad = true;
                                break;
                            }
                        }
                    }

                    if (isChildKeypad)
                    {
                        KeypadController dup = child.GetComponent<KeypadController>();
                        if (dup != null) Destroy(dup);
                        continue;
                    }

                    KeypadController kp = child.GetComponent<KeypadController>();
                    if (kp == null) kp = child.gameObject.AddComponent<KeypadController>();

                    BoxCollider bc = child.GetComponent<BoxCollider>();
                    if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();
                    ConfigureBoxColliderFromRenderers(child.gameObject, bc, true, 1.2f);

                    keypadsInOffice.Add(kp);
                }
            }

            // B. Buscar el objeto de la tarjeta (AccessCard)
            Transform cardTrans = null;
            foreach (Transform child in children)
            {
                if (child.name.ToLower().Contains("accesscard") || child.name.ToLower().Contains("access_card") || child.name.ToLower().Contains("keycard") || child.name.ToLower().Contains("tarjeta"))
                {
                    cardTrans = child;
                    break;
                }
            }

            // C. Buscar el cajón del escritorio
            Transform drawerTrans = null;
            foreach (Transform child in children)
            {
                if (child.name.ToLower().Contains("desk01") || child.name.ToLower().Contains("cajon") || child.name.ToLower().Contains("drawer"))
                {
                    drawerTrans = child;
                    break;
                }
            }

            if (drawerTrans == null)
            {
                foreach (Transform child in children)
                {
                    if (child.name.ToLower().Contains("desk"))
                    {
                        drawerTrans = child;
                        break;
                    }
                }
            }

            // D. Buscar una batería en la misma habitación para usarla como reemplazo
            Transform batteryTrans = null;
            foreach (Transform child in children)
            {
                if (child.name.ToLower().Contains("battery") || child.name.ToLower().Contains("batery") || child.name.ToLower().Contains("pila"))
                {
                    if (child != cardTrans && child.GetComponent<KeypadController>() == null)
                    {
                        batteryTrans = child;
                        break;
                    }
                }
            }

            // E. Encontrar la puerta de esta oficina
            ProceduralDoorInteract targetDoor = officeRoot.GetComponentInChildren<ProceduralDoorInteract>(true);
            if (targetDoor == null)
            {
                ProceduralDoorInteract[] allDoors = FindObjectsByType<ProceduralDoorInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                float bestDoorDist = float.MaxValue;
                foreach (var door in allDoors)
                {
                    if (door != null)
                    {
                        float doorDist = Vector3.Distance(officeRoot.position, door.transform.position);
                        if (doorDist < 12.0f && doorDist < bestDoorDist)
                        {
                            bestDoorDist = doorDist;
                            targetDoor = door;
                        }
                    }
                }
            }

            Debug.Log($"[KeypadDebug] Oficina {i} ({officeRoot.name}): keypadsCount={keypadsInOffice.Count}, pos={officeRoot.position}, card={cardTrans?.name ?? "null"}, drawer={drawerTrans?.name ?? "null"}, battery={batteryTrans?.name ?? "null"}, door={targetDoor?.name ?? "null"}");

            // Configurar el componente de cajón
            DrawerInteract drawerScript = null;
            if (drawerTrans != null)
            {
                drawerTrans.gameObject.isStatic = false;

                drawerScript = drawerTrans.GetComponent<DrawerInteract>();
                if (drawerScript == null) drawerScript = drawerTrans.gameObject.AddComponent<DrawerInteract>();
                drawerScript.slideDistance = 0.35f;
                drawerScript.interactDistance = 4.5f;
            }

            if (i == 0)
            {
                // OFICINA REAL
                List<KeypadController> realKeypads = new List<KeypadController>();
                foreach (var kp in keypadsInOffice)
                {
                    if (kp.name.ToLower().Contains("key_pad"))
                    {
                        realKeypads.Add(kp);
                    }
                }
                
                if (realKeypads.Count == 0 && keypadsInOffice.Count > 0)
                {
                    realKeypads.Add(keypadsInOffice[0]);
                }

                foreach (var kp in keypadsInOffice)
                {
                    bool isChildOfReal = false;
                    foreach (var realKp in realKeypads)
                    {
                        if (kp != realKp && kp.transform.IsChildOf(realKp.transform))
                        {
                            isChildOfReal = true;
                            break;
                        }
                    }

                    if (realKeypads.Contains(kp) || isChildOfReal)
                    {
                        kp.gameObject.SetActive(true);
                        
                        Debug.Log($"[FixedHospital] ACTIVANDO TECLADO REAL: {kp.name} (Padre: {kp.transform.parent.name})");

                        foreach (Transform child in kp.transform)
                        {
                            child.gameObject.SetActive(true);
                        }

                        kp.correctCode = correctKeypadCode;
                        kp.targetProceduralDoor = targetDoor;
                    }
                    else
                    {
                        kp.gameObject.SetActive(false);
                    }
                }
                
                if (targetDoor != null)
                {
                    targetDoor.isLocked = true;
                    targetDoor.doorLockedSound = Resources.Load<AudioClip>("Audio/Hospital/errorSound");
                }

                // Colocar tarjeta de acceso dentro del cajón
                if (drawerTrans != null && drawerScript != null)
                {
                    if (cardTrans != null)
                    {
                        cardTrans.gameObject.isStatic = false;
                    }

                    if (cardTrans == null)
                    {
                        GameObject cardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cardObj.name = "AccessCard_Director";

                        Material keycardMat = Resources.Load<Material>("Mat_ElevatorKeycard_Horror");
                        if (keycardMat == null) keycardMat = Resources.Load<Material>("Mat_Keycard");
                        Renderer r = cardObj.GetComponent<Renderer>();
                        if (r != null && keycardMat != null) r.material = keycardMat;

                        Collider primCol = cardObj.GetComponent<Collider>();
                        if (primCol != null) Destroy(primCol);

                        cardObj.transform.localScale = new Vector3(0.22f, 0.02f, 0.32f);
                        cardTrans = cardObj.transform;
                        Debug.Log("[FixedHospital] Tarjeta de Acceso creada dinámicamente como fallback.");
                    }

                    cardTrans.SetParent(drawerTrans);
                    cardTrans.localPosition = new Vector3(0f, -0.03f, -0.15f);
                    cardTrans.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    KeycardItem cardComp = cardTrans.GetComponent<KeycardItem>();
                    if (cardComp == null) cardComp = cardTrans.gameObject.AddComponent<KeycardItem>();
                    cardComp.interactDistance = 4.0f;

                    drawerScript.keycardInside = cardTrans.gameObject;
                    cardTrans.gameObject.SetActive(false);
                }

                Debug.Log($"[FixedHospital] Oficina Real seleccionada ({officeRoot.name} en {officeRoot.position}). Clave asignada: {correctKeypadCode}");
            }
            else
            {
                // OFICINA FALSA
                foreach (var kp in keypadsInOffice)
                {
                    kp.gameObject.SetActive(false);
                }

                if (targetDoor != null)
                {
                    targetDoor.isLocked = false;
                    targetDoor.doorLockedSound = null;
                }

                if (cardTrans != null)
                {
                    Destroy(cardTrans.gameObject);
                }

                if (batteryTrans != null && drawerTrans != null && drawerScript != null)
                {
                    batteryTrans.gameObject.isStatic = false;
                    if (batteryTrans.parent != drawerTrans)
                    {
                        batteryTrans.SetParent(drawerTrans, true); // Conservar posición y rotación real del editor de Unity
                    }

                    BatteryItem batComp = batteryTrans.GetComponent<BatteryItem>();
                    if (batComp == null) batComp = batteryTrans.gameObject.AddComponent<BatteryItem>();

                    drawerScript.keycardInside = null;
                    batteryTrans.gameObject.SetActive(true);
                }
            }
        }

        // 4. Configurar las 7 notas del código
        SetupCodeNotes(allTransforms);
    }
}
