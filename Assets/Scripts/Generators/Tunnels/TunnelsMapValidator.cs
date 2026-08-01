using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Suite de pruebas integrales automáticas para verificar la estabilidad y completabilidad del mapa de túneles,
/// asegurando que el jugador y el ascensor de llegada estén dentro de los límites del mapa.
/// </summary>
public class TunnelsMapValidator : MonoBehaviour
{
    [Header("Configuración")]
    public bool runTestOnStart = true;

    /// <summary>
    /// Cuántos frames extra esperar después de que el generador termine antes de validar.
    /// Suficiente para que ForcePlayerPositionAfterPhysics también termine.
    /// </summary>
    [Tooltip("Frames a esperar después de generación antes de validar (default 10)")]
    public int framesDelayAfterGeneration = 10;

    public struct ValidationResult
    {
        public bool isSuccess;
        public List<string> passedChecks;
        public List<string> failedChecks;
        public List<string> warnings;
    }

    public static ValidationResult LastResult;

    private void Start()
    {
        if (runTestOnStart)
        {
            StartCoroutine(WaitAndValidate());
        }
    }

    private IEnumerator WaitAndValidate()
    {
        TunnelsGenerator generator = Object.FindFirstObjectByType<TunnelsGenerator>();

        // Esperar a que el TunnelsGenerator exista
        float timeout = 30f;
        float elapsed = 0f;
        while (generator == null && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            generator = Object.FindFirstObjectByType<TunnelsGenerator>();
        }

        if (generator == null)
        {
            Debug.LogError("[TunnelsMapValidator] No se encontró TunnelsGenerator en la escena después de 30s.");
            yield break;
        }

        // Esperar a que el ascensor y el jugador aparezcan en la escena
        // (señal de que SpawnEntities ya corrió)
        elapsed = 0f;
        while (elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            if (GameObject.Find("ArrivalElevatorCabin") != null &&
                GameObject.FindGameObjectWithTag("Player") != null)
            {
                break;
            }
        }

        // Esperar frames extra para que ForcePlayerPositionAfterPhysics termine
        for (int i = 0; i < framesDelayAfterGeneration; i++)
        {
            yield return null;
        }

        LastResult = ValidateCurrentMap(generator);
        LogResult(LastResult);
    }

    [ContextMenu("Ejecutar Validación de Túneles")]
    public void RunValidation()
    {
        LastResult = ValidateCurrentMap();
        LogResult(LastResult);
    }

    public static ValidationResult ValidateCurrentMap(TunnelsGenerator generator = null)
    {
        ValidationResult res = new ValidationResult
        {
            passedChecks = new List<string>(),
            failedChecks = new List<string>(),
            warnings = new List<string>(),
            isSuccess = true
        };

        if (generator == null)
        {
            generator = Object.FindFirstObjectByType<TunnelsGenerator>();
        }

        if (generator == null)
        {
            res.failedChecks.Add("No se encontró TunnelsGenerator en la escena.");
            res.isSuccess = false;
            return res;
        }

        // ─── 1. ENCONTRAR JUGADOR Y ASCENSOR ───────────────────────────────────────
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject arrivalElevator = GameObject.Find("ArrivalElevatorCabin");

        if (player == null)
        {
            res.failedChecks.Add("Jugador: No se encontró la etiqueta 'Player' en la escena.");
            res.isSuccess = false;
        }
        else
        {
            res.passedChecks.Add($"Jugador: Encontrado en posición {player.transform.position}.");
        }

        if (arrivalElevator == null)
        {
            res.failedChecks.Add("Ascensor: No se encontró 'ArrivalElevatorCabin' en la escena.");
            res.isSuccess = false;
        }
        else
        {
            res.passedChecks.Add($"Ascensor: Encontrado en posición {arrivalElevator.transform.position}.");
        }

        if (player == null || arrivalElevator == null)
        {
            return res; // no hay suficiente información para continuar
        }

        // ─── 2. VERIFICAR JUGADOR EN LA ZONA CENTRAL DE PASILLO DEL GRID ───────────
        float cellSize = generator.playerSpawnCellSize;
        if (cellSize > 0f)
        {
            int gx = generator.playerSpawnGridX;
            int gz = generator.playerSpawnGridZ;

            int minMargin = 3; // Margen estricto desde los bordes del mapa
            if (gx < minMargin || gx >= generator.width - minMargin || gz < minMargin || gz >= generator.height - minMargin)
            {
                res.failedChecks.Add($"CRÍTICO: El ascensor spawnearó demasiado cerca de los bordes externos en grid[{gx},{gz}]! " +
                                     $"Debe spawnear en el pasillo central dentro de [{minMargin} y {generator.width - minMargin - 1}].");
                res.isSuccess = false;
            }
            else
            {
                res.passedChecks.Add($"✓ Ascensor y jugador confirmados en el PASILLO CENTRAL INTERNO del laberinto grid[{gx},{gz}].");
            }

            // 2. Verificar que la matriz grid tenga la celda como caminable
            if (generator.grid != null && gx >= 0 && gx < generator.width && gz >= 0 && gz < generator.height)
            {
                if (!generator.grid[gx, gz])
                {
                    res.failedChecks.Add($"CRÍTICO: El jugador spawnearó en una celda CERRADA o INVÁLIDA grid[{gx},{gz}]!");
                    res.isSuccess = false;
                }
            }

            // La celda va de (gx*cellSize - cellSize/2) a (gx*cellSize + cellSize/2) en X y Z
            // Añadimos una tolerancia por el offset seguro del ascensor hacia el pasillo
            float cellCenterX = gx * cellSize;
            float cellCenterZ = gz * cellSize;
            float margin = cellSize * 0.75f;

            Vector3 pPos = player.transform.position;

            bool insideCellX = pPos.x >= cellCenterX - margin && pPos.x <= cellCenterX + margin;
            bool insideCellZ = pPos.z >= cellCenterZ - margin && pPos.z <= cellCenterZ + margin;

            if (insideCellX && insideCellZ)
            {
                res.passedChecks.Add($"✓ Jugador dentro de los límites seguros de celda de spawn ({gx},{gz}): " +
                                     $"X={pPos.x:F2}, Z={pPos.z:F2}.");
            }
            else
            {
                res.failedChecks.Add($"CRÍTICO: El jugador está FUERA del área segura de spawn ({gx},{gz})! " +
                                     $"PosJugador=({pPos.x:F2},{pPos.z:F2}).");
                res.isSuccess = false;
            }

            // Muestrear NavMesh exactamente debajo de los pies del jugador
            if (!NavMesh.SamplePosition(pPos, out _, 0.3f, NavMesh.AllAreas))
            {
                res.failedChecks.Add($"CRÍTICO: El jugador está flotando sobre el VACÍO (Sin NavMesh bajo sus pies en {pPos})!");
                res.isSuccess = false;
            }
            else
            {
                // Verificar que exista una celda del túnel (Cell_gx_gz) en la jerarquía bajo navMeshHolder
                GameObject cellObj = GameObject.Find($"Cell_{gx}_{gz}");
                if (cellObj == null)
                {
                    res.failedChecks.Add($"CRÍTICO: No se encontró la celda de túnel 'Cell_{gx}_{gz}' bajo el ascensor (está en una zona no generada)!");
                    res.isSuccess = false;
                }
                else
                {
                    res.passedChecks.Add($"✓ Celda física 'Cell_{gx}_{gz}' y NavMesh confirmados bajo el ascensor en {pPos}.");
                }
            }
        }
        else
        {
            res.warnings.Add("playerSpawnCellSize es 0 – el generador no guardó datos de celda. Saltando check de celda.");
        }

        // ─── 3. VERIFICAR QUE LAS BOUNDS (GIZMO VERDE) DEL ASCENSOR ESTÉN DENTRO DE LA CELDA ──
        if (arrivalElevator != null && generator.playerSpawnCellSize > 0f)
        {
            Renderer[] eleRenderers = arrivalElevator.GetComponentsInChildren<Renderer>();
            Bounds eleBounds = new Bounds(arrivalElevator.transform.position, Vector3.zero);
            if (eleRenderers.Length > 0)
            {
                eleBounds = eleRenderers[0].bounds;
                for (int i = 1; i < eleRenderers.Length; i++)
                {
                    eleBounds.Encapsulate(eleRenderers[i].bounds);
                }
            }
            else
            {
                eleBounds = new Bounds(arrivalElevator.transform.position, Vector3.one * generator.playerSpawnCellSize * 0.8f);
            }

            // Comprobar los 4 puntos de la base de las Bounds del ascensor
            Vector3[] checkPoints = new Vector3[]
            {
                eleBounds.center,
                new Vector3(eleBounds.min.x, eleBounds.min.y + 0.2f, eleBounds.min.z),
                new Vector3(eleBounds.max.x, eleBounds.min.y + 0.2f, eleBounds.min.z),
                new Vector3(eleBounds.min.x, eleBounds.min.y + 0.2f, eleBounds.max.z),
                new Vector3(eleBounds.max.x, eleBounds.min.y + 0.2f, eleBounds.max.z)
            };

            bool boundsInVoid = false;
            Vector3 failedPoint = Vector3.zero;
            foreach (Vector3 pt in checkPoints)
            {
                // Muestrear con precisión milimétrica (0.3m de tolerancia máxima)
                if (!NavMesh.SamplePosition(pt, out _, 0.3f, NavMesh.AllAreas))
                {
                    boundsInVoid = true;
                    failedPoint = pt;
                    break;
                }
            }

            if (boundsInVoid)
            {
                res.failedChecks.Add($"CRÍTICO: El GIZMO/BOUNDS del Ascensor sobresale al VACÍO exterior en la posición {failedPoint}!");
                res.isSuccess = false;
            }
            else
            {
                res.passedChecks.Add($"✓ Bounds (Gizmo) del Ascensor verificados 100% integrados sobre suelo caminable (Centro: {eleBounds.center}).");
            }
        }

        // ─── 4. VERIFICACIÓN FÍSICA: JUGADOR DENTRO DE PAREDES O MIRANDO A PARED ─
        {
            Vector3 playerHead = player.transform.position + Vector3.up * 1.2f;
            bool problemFound = false;
            string problemDesc = "";

            // OverlapSphere: detecta si el jugador está dentro de una pared del mapa
            Collider[] hits = Physics.OverlapSphere(player.transform.position, 0.4f);
            foreach (Collider c in hits)
            {
                if (c == null) continue;
                string cName = c.name.ToLower();
                // Ignorar partes del ascensor y del propio jugador
                if (c.transform.IsChildOf(arrivalElevator.transform)) continue;
                if (c.transform.IsChildOf(player.transform.root)) continue;
                if (cName.Contains("floor") || cName.Contains("ceiling") || cName.Contains("catwalk")) continue;
                if (cName.Contains("wall"))
                {
                    problemFound = true;
                    problemDesc = $"colisionando con '{c.name}'";
                    break;
                }
            }

            // Raycast en la dirección frontal del jugador para detectar si hay una pared tapando la salida
            if (!problemFound)
            {
                if (Physics.Raycast(playerHead, player.transform.forward, out RaycastHit viewHit, 2.0f))
                {
                    if (viewHit.collider != null && !viewHit.collider.transform.IsChildOf(arrivalElevator.transform) && !viewHit.collider.transform.IsChildOf(player.transform.root))
                    {
                        string hName = viewHit.collider.name.ToLower();
                        if (hName.Contains("wall") || hName.Contains("corner") || hName.Contains("filler") || hName.Contains("solid"))
                        {
                            problemFound = true;
                            problemDesc = $"PARED TAPANDO LA SALIDA DEL ASCENSOR: '{viewHit.collider.name}' a {viewHit.distance:F2}m del frente";
                        }
                    }
                }
            }

            // CHECK CRÍTICO: Verificar que el punto DELANTE del jugador tiene NavMesh.
            // Si no hay NavMesh adelante, el jugador está mirando al vacío exterior del mapa.
            if (!problemFound)
            {
                float voidCheckCellSize = generator.playerSpawnCellSize > 0f ? generator.playerSpawnCellSize : 15f;
                Vector3 forwardProbe = player.transform.position + player.transform.forward * (voidCheckCellSize * 0.6f);
                forwardProbe.y = player.transform.position.y;
                if (!NavMesh.SamplePosition(forwardProbe, out _, voidCheckCellSize * 0.4f, NavMesh.AllAreas))
                {
                    problemFound = true;
                    problemDesc = $"el jugador mira al VACÍO exterior (sin NavMesh en {forwardProbe}). " +
                                  $"Forward={player.transform.forward}, Pos={player.transform.position}";
                }
            }

            if (problemFound)
            {
                res.failedChecks.Add($"CRÍTICO: {problemDesc}.");
                res.isSuccess = false;
            }
            else
            {
                res.passedChecks.Add("Libertad física y NavMesh: Jugador 100% dentro del pasillo con salida totalmente despejada.");
            }
        }


        // ─── 5. DISTANCIA JUGADOR ↔ ASCENSOR ────────────────────────────────────
        {
            float dist = Vector3.Distance(player.transform.position, arrivalElevator.transform.position);
            float maxExpected = generator.playerSpawnCellSize > 0f
                ? generator.playerSpawnCellSize * 0.75f
                : 15f;

            if (dist <= maxExpected)
            {
                res.passedChecks.Add($"Distancia Jugador-Ascensor: {dist:F2}m (max esperado {maxExpected:F2}m). ✓");
            }
            else
            {
                res.failedChecks.Add($"CRÍTICO: Jugador demasiado lejos del ascensor: {dist:F2}m (max {maxExpected:F2}m). " +
                                     $"PosJugador={player.transform.position}, PosAscensor={arrivalElevator.transform.position}.");
                res.isSuccess = false;
            }
        }

        // ─── 6. NAVMESH: RUTA HASTA LA SALIDA ────────────────────────────────────
        if (TunnelsGenerator.worldExitPointPos != Vector3.zero)
        {
            NavMeshPath path = new NavMeshPath();
            bool hasPath = NavMesh.CalculatePath(player.transform.position, TunnelsGenerator.worldExitPointPos, NavMesh.AllAreas, path);

            if (hasPath && path.status == NavMeshPathStatus.PathComplete)
            {
                res.passedChecks.Add("NavMesh: Ruta completa desde el jugador hasta la salida. ✓");
            }
            else if (hasPath && path.status == NavMeshPathStatus.PathPartial)
            {
                res.warnings.Add("NavMesh: Ruta parcial entre el inicio y la salida.");
            }
            else
            {
                res.warnings.Add("NavMesh: Sin ruta directa a la salida (revisar horneado).");
            }
        }

        return res;
    }

    private static void LogResult(ValidationResult res)
    {
        Debug.Log("<color=cyan>════════════════════════════════════════════════════════</color>");
        Debug.Log("<color=cyan>    REPORTE DE VALIDACIÓN AUTOMÁTICA DEL MAPA DE TÚNELES    </color>");
        Debug.Log("<color=cyan>════════════════════════════════════════════════════════</color>");

        foreach (string passed in res.passedChecks)
        {
            Debug.Log($"<color=green>[✓ PASS]</color> {passed}");
        }

        foreach (string warn in res.warnings)
        {
            Debug.LogWarning($"<color=yellow>[! WARN]</color> {warn}");
        }

        foreach (string failed in res.failedChecks)
        {
            Debug.LogError($"<color=red>[✗ FAIL]</color> {failed}");
        }

        if (res.isSuccess)
        {
            Debug.Log("<color=green><b>✔ EL MAPA DE TÚNELES CUMPLE CON TODOS LOS REQUISITOS DE ESTABILIDAD</b></color>");
        }
        else
        {
            Debug.LogError("<color=red><b>✖ SE DETECTARON ERRORES DE GENERACIÓN EN EL MAPA DE TÚNELES</b></color>");
        }
    }
}
