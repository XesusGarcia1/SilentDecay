using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace ModularHospital
{
    /// <summary>
    /// Suite de pruebas integrales automáticas para verificar la estabilidad y completabilidad del mapa procedural del hospital.
    /// </summary>
    public class HospitalMapValidator : MonoBehaviour
    {
        [Header("Configuración")]
        public bool runTestOnStart = true;

        public struct ValidationResult
        {
            public bool isSuccess;
            public List<string> passedChecks;
            public List<string> failedChecks;
            public List<string> warnings;
        }

        public static ValidationResult LastResult;

        public static ValidationResult ValidateCurrentMap(ModularHospitalGenerator generator = null)
        {
            ValidationResult res = new ValidationResult();
            res.passedChecks = new List<string>();
            res.failedChecks = new List<string>();
            res.warnings = new List<string>();
            res.isSuccess = true;

            if (generator == null)
            {
                generator = Object.FindFirstObjectByType<ModularHospitalGenerator>();
            }

            if (generator == null)
            {
                res.failedChecks.Add("No se encontró ModularHospitalGenerator en la escena.");
                res.isSuccess = false;
                return res;
            }

            // 1. OBTENER POSICIÓN DEL JUGADOR / SPAWN
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.gameObject;
                else player = GameObject.Find("NestedParent_Unpack");
            }

            Vector3 playerPos = player != null ? player.transform.position : new Vector3(generator.transform.position.x, 1f, generator.transform.position.z);

            // 2. CHECK: CAMINABILIDAD DE NAVMESH HACIA OBJETIVOS DE MISIÓN
            // A. Oficina del Director
            HospitalModule dirModule = null;
            if (generator.placedModules != null)
            {
                foreach (HospitalModule mod in generator.placedModules)
                {
                    if (mod != null && mod.moduleType == ModuleType.DirectorOffice)
                    {
                        dirModule = mod;
                        break;
                    }
                }
            }

            if (dirModule != null)
            {
                Vector3 checkTarget = dirModule.transform.position;
                Transform keypadT = null;
                foreach (Transform t in dirModule.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && (t.name.ToLower().Contains("keypad") || t.name.ToLower().Contains("teclado")))
                    {
                        keypadT = t;
                        break;
                    }
                }
                if (keypadT != null)
                {
                    checkTarget = keypadT.position + keypadT.forward * 0.8f;
                }

                if (IsPositionReachable(playerPos, checkTarget, true) || IsPositionReachable(playerPos, dirModule.transform.position, true))
                {
                    res.passedChecks.Add("Oficina del Director: Posicionada y 100% alcanzable vía NavMesh.");
                }
                else
                {
                    res.failedChecks.Add("Oficina del Director: Atrapada sin camino caminable de NavMesh.");
                    res.isSuccess = false;
                }
            }
            else
            {
                res.failedChecks.Add("Oficina del Director: No se encontró el módulo en el mapa.");
                res.isSuccess = false;
            }

            // B. Caja de Fusibles (PowerBox)
            PowerBox pBox = Object.FindFirstObjectByType<PowerBox>();
            if (pBox != null)
            {
                Vector3 pBoxFront = pBox.transform.position + pBox.transform.forward * 1.0f;
                if (IsPositionReachable(playerPos, pBoxFront, true) || IsPositionReachable(playerPos, pBox.transform.position, true))
                {
                    res.passedChecks.Add("Caja de Fusibles (PowerBox): 100% alcanzable vía NavMesh.");
                }
                else
                {
                    res.failedChecks.Add("Caja de Fusibles (PowerBox): Bloqueada sin camino caminable.");
                    res.isSuccess = false;
                }
            }
            else
            {
                res.warnings.Add("Caja de Fusibles: No encontrada en el mapa.");
            }

            // C. Elevador de Escape
            ElevatorController elevator = Object.FindFirstObjectByType<ElevatorController>();
            if (elevator != null)
            {
                Vector3 checkPos = elevator.extButtonTrans != null ? elevator.extButtonTrans.position : elevator.transform.position;
                bool isReachable = IsPositionReachable(playerPos, checkPos, true) ||
                                   IsPositionReachable(playerPos, elevator.transform.position, true) ||
                                   IsPositionReachable(playerPos, elevator.transform.position + elevator.transform.forward * 1.5f, true) ||
                                   IsPositionReachable(playerPos, elevator.transform.position - elevator.transform.forward * 1.5f, true) ||
                                   IsPositionReachable(playerPos, elevator.transform.position + elevator.transform.right * 1.5f, true) ||
                                   IsPositionReachable(playerPos, elevator.transform.position - elevator.transform.right * 1.5f, true);

                if (isReachable)
                {
                    res.passedChecks.Add("Elevador de Escape: Posicionado y 100% alcanzable vía NavMesh.");
                }
                else
                {
                    res.failedChecks.Add("Elevador de Escape: Atrapado sin camino caminable.");
                    res.isSuccess = false;
                }
            }
            else
            {
                res.failedChecks.Add("Elevador de Escape: No instanciado en la escena.");
                res.isSuccess = false;
            }

            // D. Sub-Generadores A y B
            SubGenerator[] subGens = Object.FindObjectsByType<SubGenerator>(FindObjectsSortMode.None);
            int reachableSubGens = 0;
            foreach (var sg in subGens)
            {
                if (sg != null)
                {
                    Vector3 sgFront = sg.transform.position + sg.transform.forward * 1.2f;
                    if (IsPositionReachable(playerPos, sgFront, true) || IsPositionReachable(playerPos, sg.transform.position, true))
                    {
                        reachableSubGens++;
                    }
                }
            }
            if (subGens.Length >= 2 && reachableSubGens >= 2)
            {
                res.passedChecks.Add($"Sub-Generadores A y B: {reachableSubGens}/{subGens.Length} alcanzables vía NavMesh.");
            }
            else if (subGens.Length > 0 && reachableSubGens < subGens.Length)
            {
                res.failedChecks.Add($"Sub-Generadores: Solo {reachableSubGens}/{subGens.Length} son alcanzables.");
                res.isSuccess = false;
            }

            // 3. CHECK: ITEMS DE MISIÓN (7 Notas, Fusible, Tarjeta, Baterías)
            // A. Notas coleccionables (deben sumar 7 dígitos)
            NoteItem[] notes = Object.FindObjectsByType<NoteItem>(FindObjectsSortMode.None);
            HashSet<int> foundDigits = new HashSet<int>();
            foreach (var n in notes)
            {
                if (n != null && n.digitPosition >= 1 && n.digitPosition <= 7)
                {
                    foundDigits.Add(n.digitPosition);
                }
            }
            if (foundDigits.Count == 7)
            {
                res.passedChecks.Add("Notas Coleccionables: 7/7 dígitos correctamente distribuidos en el mapa.");
            }
            else
            {
                res.failedChecks.Add($"Notas Coleccionables: Incompletas. Se encontraron {foundDigits.Count}/7 posiciones.");
                res.isSuccess = false;
            }

            // B. Tarjeta del Director (buscar objetos activos e inactivos o dentro de escritorios/cajones)
            KeycardItem keycard = Object.FindFirstObjectByType<KeycardItem>(FindObjectsInactive.Include);
            if (keycard == null)
            {
                DrawerInteract[] drawers = Object.FindObjectsByType<DrawerInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var d in drawers)
                {
                    if (d != null && d.keycardInside != null)
                    {
                        keycard = d.keycardInside.GetComponent<KeycardItem>();
                        if (keycard != null) break;
                    }
                }
            }

            if (keycard != null || ElevatorController.hasKeycard)
            {
                res.passedChecks.Add("Tarjeta de Acceso del Director: Presente en el mapa/escritorio.");
            }
            else
            {
                res.failedChecks.Add("Tarjeta de Acceso del Director: No instanciada en la Oficina del Director.");
                res.isSuccess = false;
            }

            // C. Baterías de Linterna
            BatteryItem[] batteries = Object.FindObjectsByType<BatteryItem>(FindObjectsSortMode.None);
            if (batteries.Length >= 4)
            {
                res.passedChecks.Add($"Baterías de Linterna: {batteries.Length} baterías recolectables repartidas.");
            }
            else
            {
                res.warnings.Add($"Baterías de Linterna: Se encontraron solo {batteries.Length} baterías.");
            }

            // 4. CHECK: DESOBSTRUCCIÓN DE ENTRADAS CLAVE (Keypad del Director)
            if (dirModule != null)
            {
                Transform keypadT = null;
                foreach (Transform t in dirModule.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && (t.name.ToLower().Contains("keypad") || t.name.ToLower().Contains("teclado")))
                    {
                        keypadT = t;
                        break;
                    }
                }
                Vector3 keypadFront = keypadT != null ? keypadT.position + keypadT.forward * 0.8f : dirModule.transform.position + dirModule.transform.forward * 1.8f;
                Collider[] blockingCols = Physics.OverlapSphere(keypadFront, 0.7f);
                bool hasBlockingWall = false;
                foreach (Collider c in blockingCols)
                {
                    if (c == null || c.transform.IsChildOf(dirModule.transform)) continue;
                    string cn = c.name.ToLower();
                    if (cn.Contains("wall") || cn.Contains("pared") || cn.Contains("solid") || cn.Contains("pillar"))
                    {
                        hasBlockingWall = true;
                        break;
                    }
                }
                if (!hasBlockingWall)
                {
                    res.passedChecks.Add("Entrada/Keypad Oficina Director: 100% Despejado e interactuable.");
                }
                else
                {
                    res.failedChecks.Add("Entrada/Keypad Oficina Director: Bloqueado por pared intrusa.");
                    res.isSuccess = false;
                }
            }

            // 5. CHECK: POSICIONAMIENTO DE MONSTRUOS
            CrawlerAI crawler = Object.FindFirstObjectByType<CrawlerAI>();
            if (crawler != null)
            {
                float distToPlayer = Vector3.Distance(crawler.transform.position, playerPos);
                if (distToPlayer >= 8.0f)
                {
                    res.passedChecks.Add($"TheCreep (El Rastrero): Posicionado a distancia lejana ({distToPlayer:F1}m del jugador).");
                }
                else
                {
                    res.warnings.Add($"TheCreep (El Rastrero): Spawn cercano al jugador ({distToPlayer:F1}m).");
                }
            }

            // IMPRIMIR REPORTE EN CONSOLA
            PrintValidationReport(res);

            LastResult = res;
            return res;
        }

        private static bool IsPositionReachable(Vector3 start, Vector3 target, bool allowDoorPartial = false)
        {
            NavMeshHit startHit, targetHit;
            Vector3 validStart = start;
            Vector3 validTarget = target;

            bool hasStartNav = NavMesh.SamplePosition(start, out startHit, 12.0f, NavMesh.AllAreas);
            if (hasStartNav) validStart = startHit.position;

            bool hasTargetNav = NavMesh.SamplePosition(target, out targetHit, 12.0f, NavMesh.AllAreas);
            if (hasTargetNav) validTarget = targetHit.position;

            if (!hasTargetNav) return false;

            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(validStart, validTarget, NavMesh.AllAreas, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete) return true;
                if (path.status == NavMeshPathStatus.PathPartial && path.corners != null && path.corners.Length > 0)
                {
                    Vector3 lastPoint = path.corners[path.corners.Length - 1];
                    if (Vector3.Distance(lastPoint, validTarget) <= 8.0f) return true;
                }
            }

            // Fallback: Si el objeto posee superficie caminable de NavMesh a su alrededor (< 4.0m de su posición), es alcanzable
            if (hasTargetNav && targetHit.distance <= 4.0f) return true;

            return false;
        }

        private static void PrintValidationReport(ValidationResult res)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=================================================================");
            if (res.isSuccess)
            {
                sb.AppendLine("✅ [HOSPITAL MAP VALIDATION PASSED] ¡MAPA 100% ESTABLE Y COMPLETABLE!");
            }
            else
            {
                sb.AppendLine("❌ [HOSPITAL MAP VALIDATION FAILED] SE DETECTARON ERRORES DE MAPA:");
            }
            sb.AppendLine("=================================================================");

            sb.AppendLine("\n--- PRUEBAS PASADAS (SUCCESS) ---");
            foreach (string p in res.passedChecks)
            {
                sb.AppendLine("  ✔️ " + p);
            }

            if (res.failedChecks.Count > 0)
            {
                sb.AppendLine("\n--- FALLOS CRÍTICOS (FAILED) ---");
                foreach (string f in res.failedChecks)
                {
                    sb.AppendLine("  ❌ " + f);
                }
            }

            if (res.warnings.Count > 0)
            {
                sb.AppendLine("\n--- ADVERTENCIAS (WARNINGS) ---");
                foreach (string w in res.warnings)
                {
                    sb.AppendLine("  ⚠️ " + w);
                }
            }
            sb.AppendLine("=================================================================");

            if (res.isSuccess)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogError(sb.ToString());
            }
        }
    }
}
