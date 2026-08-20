using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ModularHospital;

public class HospitalFixedMapLogic : MonoBehaviour
{
    [Header("Referencias Generales")]
    [Tooltip("El punto donde el jugador aparecerá al iniciar y cuando muera.")]
    public Transform pointStartRespawn;

    [Tooltip("El prefab de la nota de Lore (PapelLore) que se instanciará en el suelo.")]
    public GameObject loreNotePrefab;

    private string correctKeypadCode = "";
    private Transform playerTransform;

    private void Start()
    {
        // Asegurar que el menú de pausa (PauseMenuManager) esté presente
        if (FindFirstObjectByType<PauseMenuManager>() == null)
        {
            GameObject pMenu = new GameObject("[PauseMenuManager]");
            pMenu.AddComponent<PauseMenuManager>();
        }

        SetupPlayerSpawn();
        SetupDoors();
        SetupRandomElements();
        SetupHideBeds();
        SetupLoreNotes();
        SetupItems();

        // Disparar monólogo inicial del jugador con pequeño delay
        StartCoroutine(TriggerStartMonologueDelayed());
    }

    private void SetupPlayerSpawn()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("PlayerMale");
        if (playerObj == null) playerObj = GameObject.Find("PlayerFemale");

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            
            if (pointStartRespawn != null)
            {
                // Mover al jugador usando CharacterController para evitar bugs de colisión
                CharacterController cc = playerObj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerTransform.position = pointStartRespawn.position;
                playerTransform.rotation = pointStartRespawn.rotation;

                if (cc != null) cc.enabled = true;

                // Asegurar que el script HideUnderBed esté presente en el jugador
                HideUnderBed hideScript = playerObj.GetComponent<HideUnderBed>();
                if (hideScript == null) hideScript = playerObj.AddComponent<HideUnderBed>();

                // Informar al GameManager del punto de respawn seguro
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegistrarSpawnJugador(pointStartRespawn.position, pointStartRespawn.rotation);
                }
            }
        }
    }

    private bool IsTopLevelElement(Transform t, string[] keywords, string[] excludeKeywords)
    {
        string n = t.name.ToLower();
        
        bool matches = false;
        foreach (var kw in keywords)
        {
            if (n.Contains(kw)) { matches = true; break; }
        }
        if (!matches) return false;

        foreach (var ex in excludeKeywords)
        {
            if (n.Contains(ex)) return false;
        }

        Transform p = t.parent;
        while (p != null)
        {
            string pName = p.name.ToLower();
            if (pName.Contains("props") || pName.Contains("rooms") || pName.Contains("hospitalgame")) 
            {
                break;
            }

            foreach (var kw in keywords)
            {
                if (pName.Contains(kw))
                {
                    return false; // A parent also matches, so this is a child mesh of the matching object
                }
            }
            p = p.parent;
        }

        return true;
    }

    private void ConfigureBoxColliderFromRenderers(GameObject obj, BoxCollider box, bool isTrigger, float sizePaddingMultiplier = 1.0f)
    {
        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>(true);
        if (mfs.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
            return;
        }

        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            
            Bounds meshBounds = mf.sharedMesh.bounds;
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.min.z);
            corners[1] = new Vector3(meshBounds.min.x, meshBounds.min.y, meshBounds.max.z);
            corners[2] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.min.z);
            corners[3] = new Vector3(meshBounds.min.x, meshBounds.max.y, meshBounds.max.z);
            corners[4] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.min.z);
            corners[5] = new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.max.z);
            corners[6] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.min.z);
            corners[7] = new Vector3(meshBounds.max.x, meshBounds.max.y, meshBounds.max.z);

            for (int i = 0; i < 8; i++)
            {
                // Convertir cada esquina local de la malla a espacio del mundo y luego a espacio local del objeto raíz (obj)
                // Esto anula por completo las distorsiones causadas por la rotación del mundo
                Vector3 worldCorner = mf.transform.TransformPoint(corners[i]);
                Vector3 localCorner = obj.transform.InverseTransformPoint(worldCorner);
                
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (hasBounds)
        {
            box.isTrigger = isTrigger;
            box.center = localBounds.center;
            box.size = localBounds.size * sizePaddingMultiplier;
        }
        else
        {
            box.center = Vector3.zero;
            box.size = Vector3.one * sizePaddingMultiplier;
            box.isTrigger = isTrigger;
        }
    }

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

        // Generar clave de 7 dígitos aleatoria
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < 7; i++) sb.Append(Random.Range(0, 10));
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
                    // Evitar seleccionar la tarjeta o teclados
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
                // Fallback: buscar la puerta más cercana al centro de la oficina en un radio de 12 metros
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
                // DESACTIVAR FLAG STATIC para poder animar la posición del cajón en tiempo de ejecución
                drawerTrans.gameObject.isStatic = false;

                drawerScript = drawerTrans.GetComponent<DrawerInteract>();
                if (drawerScript == null) drawerScript = drawerTrans.gameObject.AddComponent<DrawerInteract>();
                drawerScript.slideDistance = 0.35f;
                drawerScript.interactDistance = 4.5f;
            }

            if (i == 0)
            {
                // OFICINA REAL: Activar TODOS los teclados que se llamen exactamente "Key_pad" (o que lo contengan) para evitar fallos de jerarquía
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

                        // Asegurar que las mallas e hijos (como 'keypad') se enciendan
                        foreach (Transform child in kp.transform)
                        {
                            child.gameObject.SetActive(true);
                        }

                        kp.correctCode = correctKeypadCode;
                        kp.targetProceduralDoor = targetDoor;
                    }
                    else
                    {
                        kp.gameObject.SetActive(false); // Ocultar teclados viejos que no pertenezcan al Key_pad real
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
                        cardTrans.gameObject.isStatic = false; // Quitar estático a la tarjeta
                    }

                    if (cardTrans == null)
                    {
                        // Crear tarjeta dinámicamente si no existe en el prefab/escena
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
                // OFICINA FALSA: Desactivar teclados y desbloquear puerta
                foreach (var kp in keypadsInOffice)
                {
                    kp.gameObject.SetActive(false);
                }

                if (targetDoor != null)
                {
                    targetDoor.isLocked = false;
                    targetDoor.doorLockedSound = null;
                }

                // Destruir tarjeta de acceso
                if (cardTrans != null)
                {
                    Destroy(cardTrans.gameObject);
                }

                // Reemplazar colocando una batería física dentro del cajón
                if (batteryTrans != null && drawerTrans != null && drawerScript != null)
                {
                    batteryTrans.gameObject.isStatic = false; // Quitar estático a la batería
                    batteryTrans.SetParent(drawerTrans);
                    batteryTrans.localPosition = new Vector3(0f, 0.05f, -0.1f);
                    batteryTrans.localRotation = Quaternion.identity;

                    BatteryItem batComp = batteryTrans.GetComponent<BatteryItem>();
                    if (batComp == null) batComp = batteryTrans.gameObject.AddComponent<BatteryItem>();

                    drawerScript.keycardInside = null; // No enlazar como tarjeta de acceso principal
                    batteryTrans.gameObject.SetActive(true); // Dejarla activa para recoger directamente
                }
            }
        }

        // 4. NOTAS CON EL CÓDIGO - Buscar mallas crudas de notas en el mapa y configurarlas dinámicamente
        List<NoteItem> notesList = new List<NoteItem>();
        string[] noteKeywords = new string[] { "papel", "p_note", "note" };
        string[] noteExcludes = new string[] { "canvas", "ui", "spawn", "manager", "lore" };
        
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, noteKeywords, noteExcludes))
            {
                // Encontrar el objeto hijo que tiene la malla (MeshRenderer) para asegurar que el collider coincida con la geometría
                GameObject targetPaper = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetPaper = mr.gameObject;

                // Eliminar cualquier NoteItem residual y agregar uno limpio
                NoteItem oldN = targetPaper.GetComponent<NoteItem>();
                if (oldN != null) DestroyImmediate(oldN);

                // Configurar BoxCollider trigger basado en la malla física
                BoxCollider bc = targetPaper.GetComponent<BoxCollider>();
                if (bc == null) bc = targetPaper.AddComponent<BoxCollider>();
                ConfigureBoxColliderFromRenderers(targetPaper, bc, true, 1.5f);

                NoteItem nComp = targetPaper.AddComponent<NoteItem>();
                nComp.interactDistance = 3.0f;

                notesList.Add(nComp);
            }
        }

        ShuffleList(notesList);

        // Primero ocultamos todas las notas esparcidas
        foreach (var n in notesList)
        {
            n.gameObject.SetActive(false);
        }

        // Activamos solo 7 notas y les asignamos cada dígito de la clave
        int notesToSpawn = Mathf.Min(7, notesList.Count);
        for (int i = 0; i < notesToSpawn; i++)
        {
            notesList[i].gameObject.SetActive(true);
            notesList[i].digitPosition = (i + 1); // La posición de la nota (1 al 7)
            notesList[i].digitValue = int.Parse(correctKeypadCode[i].ToString()); // El valor numérico de la clave
        }

        if (notesToSpawn < 7)
        {
            Debug.LogWarning($"[FixedHospital] ATENCIÓN: Solo se encontraron {notesToSpawn} objetos tipo nota en la escena. ¡Necesitas colocar al menos 7 en el mapa!");
        }
    }

    private IEnumerator TriggerStartMonologueDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        // Puedes cambiar el tag del nivel o el ID de la voz según prefieras
        // LevelIntroData.TriggerStartMonologue("hospital");
    }

    private void SetupDoors()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int doorsConfigured = 0;

        string[] doorKeywords = new string[] { "p_door_01_", "puerta", "door" };
        string[] doorExcludes = new string[] { "base", "frame", "marco", "hinge", "handle" };

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, doorKeywords, doorExcludes))
            {
                // Evitar duplicar Hinge si ya está emparentada a una bisagra
                if (t.parent != null && t.parent.name.Contains("Hinge")) continue;

                // Desactivar Animator en la puerta para evitar conflictos físicos
                Animator anim = t.GetComponent<Animator>();
                if (anim != null) anim.enabled = false;

                // Añadir el script interactivo directamente a la hoja de la puerta
                ProceduralDoorInteract doorInteract = t.GetComponent<ProceduralDoorInteract>();
                if (doorInteract == null) doorInteract = t.gameObject.AddComponent<ProceduralDoorInteract>();
                
                doorInteract.interactDistance = 3.5f;
                doorInteract.autoFixCenterPivot = true; // Dejar que el script cree su propia bisagra automáticamente

                // Auto-detectar lado de bisagra para puertas dobles (izquierda/derecha)
                bool isRightLeaf = false;
                if (t.parent != null)
                {
                    List<Transform> siblingDoors = new List<Transform>();
                    foreach (Transform child in t.parent)
                    {
                        string cn = child.name.ToLower();
                        if (IsTopLevelElement(child, doorKeywords, doorExcludes))
                        {
                            siblingDoors.Add(child);
                        }
                    }

                    if (siblingDoors.Count == 2)
                    {
                        Transform door1 = siblingDoors[0];
                        Transform door2 = siblingDoors[1];
                        if (t == door1)
                        {
                            isRightLeaf = door1.localPosition.x > door2.localPosition.x;
                        }
                        else
                        {
                            isRightLeaf = door2.localPosition.x > door1.localPosition.x;
                        }
                    }
                }
                doorInteract.hingeOnRightSide = isRightLeaf;

                // Asegurar que la puerta tenga un BoxCollider bien posicionado basado en sus mallas hijas
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
                ConfigureBoxColliderFromRenderers(t.gameObject, bc, false);

                doorsConfigured++;
            }
        }
        Debug.Log($"[FixedHospital] Se auto-configuraron {doorsConfigured} puertas con bisagras físicas auto-calculadas.");
    }

    private void SetupHideBeds()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int bedsConfigured = 0;

        string[] bedKeywords = new string[] { "bed", "cama" };
        string[] bedExcludes = new string[] { "sheet", "pillow", "mattress", "blanket", "hide" };

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (IsTopLevelElement(t, bedKeywords, bedExcludes))
            {
                // Asegurar el componente Bed
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
        Debug.Log($"[FixedHospital] Se auto-configuraron {bedsConfigured} camas para esconderse.");
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
            Vector3 finalPos = spawnPos;
            bool hitFloor = false;

            if (Physics.Raycast(spawnPos + Vector3.up * 2.0f, Vector3.down, out hit, 5.0f))
            {
                if (!hit.collider.isTrigger)
                {
                    finalPos = hit.point + Vector3.up * 0.01f; // Levantado ligeramente del suelo
                    hitFloor = true;
                }
            }

            if (hitFloor)
            {
                // Instanciar el prefab PapelLore de forma aleatoria
                GameObject loreNote = Instantiate(loreNotePrefab, finalPos, Quaternion.Euler(0, Random.Range(0, 360), 0), chosenRoom);
                loreNote.name = $"[Hospital_LoreNote_{spawnedCount + 1}]";

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

    private void SetupItems()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int batteriesConfigured = 0;
        int fusesConfigured = 0;

        string[] batteryKeywords = new string[] { "batery", "battery", "pila" };
        string[] batteryExcludes = new string[] { "canvas", "ui", "spawn", "manager" };

        string[] fuseKeywords = new string[] { "fuse", "fusible" };
        string[] fuseExcludes = new string[] { "fusebox", "fuse_box", "powerbox", "canvas", "ui", "spawn", "manager" };

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            // 1. Configurar BATERÍAS crudas
            if (IsTopLevelElement(t, batteryKeywords, batteryExcludes))
            {
                // Encontrar el objeto real con MeshRenderer
                GameObject targetBat = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetBat = mr.gameObject;

                BatteryItem batComp = targetBat.GetComponent<BatteryItem>();
                if (batComp == null) batComp = targetBat.AddComponent<BatteryItem>();

                // Asegurar BoxCollider trigger a escala real para poder interactuar
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

                // Ajustar distancia de interacción
                batComp.interactDistance = 3.0f;

                batteriesConfigured++;
            }

            // 2. Configurar FUSIBLES crudos
            if (IsTopLevelElement(t, fuseKeywords, fuseExcludes))
            {
                // Encontrar el objeto real con MeshRenderer
                GameObject targetFuse = t.gameObject;
                MeshRenderer mr = t.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null) targetFuse = mr.gameObject;

                FuseItem fuseComp = targetFuse.GetComponent<FuseItem>();
                if (fuseComp == null) fuseComp = targetFuse.AddComponent<FuseItem>();

                // Asegurar BoxCollider trigger a escala real
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

                // Ajustar distancia de interacción
                fuseComp.interactDistance = 3.0f;

                fusesConfigured++;
            }
        }

        Debug.Log($"[FixedHospital] Se auto-configuraron {batteriesConfigured} baterías y {fusesConfigured} fusibles en el mapa.");
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
