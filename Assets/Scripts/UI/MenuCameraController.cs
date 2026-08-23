using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controla la cámara atmosférica del menú principal.
/// En cada inicio, selecciona una posición aleatoria en zonas abiertas del mapa.
/// Soporta marcadores manuales ("MenuCamPos") o escaneo automático de zonas libres sin colisiones.
/// </summary>
public class MenuCameraController
{
    private MainMenuManager ctx;
    private Vector3 targetCamPos;
    private Quaternion targetCamRot;
    private bool isInitialized = false;
    private Light flashlight;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
        isInitialized = false;
    }

    public void Tick()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = Object.FindFirstObjectByType<Camera>();
        if (mainCam == null) return;

        // Seleccionar un punto aleatorio en zonas abiertas al iniciar el menú
        if (!isInitialized)
        {
            PickRandomCameraSpot(mainCam);
            isInitialized = true;
        }

        // Bamboleo cinematográfico de cámara flotante (respiración / paseo lento)
        float swayYaw   = Mathf.Sin(Time.time * 0.25f) * 2.0f;
        float swayPitch = Mathf.Cos(Time.time * 0.18f) * 1.2f;
        float slowWalk  = Mathf.Sin(Time.time * 0.10f) * 0.15f;

        Vector3 desiredPos = targetCamPos + (targetCamRot * Vector3.forward) * (slowWalk * 0.40f);
        mainCam.transform.position = desiredPos;
        mainCam.transform.rotation = targetCamRot * Quaternion.Euler(swayPitch, swayYaw, 0f);

        UpdateFlashlight(mainCam);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void PickRandomCameraSpot(Camera mainCam)
    {
        // 1. PRIMERA OPCIÓN: Buscar marcadores colocados por el usuario ("MenuCam", "CamPos", "CameraSpot")
        List<Transform> markers = new List<Transform>();
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name.ToLower();
            if (n.Contains("menucam") || n.Contains("campos") || n.Contains("cameraspot") || n.Contains("menucamera"))
            {
                // Si es un objeto contenedor con hijos (como el padre 'MenuCamPos'), lo ignoramos para elegir solo las posiciones fijadas (los hijos)
                if (t.childCount > 0) continue;

                markers.Add(t);
            }
        }

        if (markers.Count > 0)
        {
            Transform chosen = markers[Random.Range(0, markers.Count)];
            targetCamPos = chosen.position;

            // Auto-orientar automáticamente hacia la dirección del pasillo más largo (evita mirar a paredes)
            float maxDist = -1f;
            float bestAngle = chosen.eulerAngles.y;

            foreach (float angle in new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                float dist = Physics.Raycast(targetCamPos, dir, out RaycastHit hit, 40f) ? hit.distance : 40f;
                if (dist > maxDist)
                {
                    maxDist = dist;
                    bestAngle = angle;
                }
            }

            targetCamRot = Quaternion.Euler(2f, bestAngle, 0f);
            Debug.Log($"[MenuCamera] Marcador '{chosen.name}' en {targetCamPos} reorientado automáticamente hacia el pasillo más abierto ({bestAngle}°, vista libre: {maxDist:F1}m).");
            return;
        }

        // 2. SEGUNDA OPCIÓN: Escaneo inteligente de zonas abiertas sin paredes ni obstáculos
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Vector3> validOpenSpots = new List<Vector3>();

        if (allRenderers.Length > 0)
        {
            Bounds mapBounds = new Bounds(allRenderers[0].bounds.center, Vector3.zero);
            foreach (var r in allRenderers)
            {
                if (r != null && r.enabled) mapBounds.Encapsulate(r.bounds);
            }

            // Muestrear 50 posiciones dentro del área del mapa
            for (int i = 0; i < 50; i++)
            {
                float rx = Random.Range(mapBounds.min.x + 2f, mapBounds.max.x - 2f);
                float rz = Random.Range(mapBounds.min.z + 2f, mapBounds.max.z - 2f);
                Vector3 candidate = new Vector3(rx, mapBounds.min.y + 1.6f, rz);

                // Verificar que no haya paredes ni colisionadores en un radio de 1.2m (evita traspasar objetos)
                if (Physics.OverlapSphere(candidate, 1.2f).Length == 0)
                {
                    // Verificar que haya vista abierta (al menos 3 metros despejados)
                    foreach (float angle in new float[] { 0f, 90f, 180f, 270f })
                    {
                        Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                        if (!Physics.Raycast(candidate, dir, out RaycastHit hit, 3.5f))
                        {
                            validOpenSpots.Add(candidate);
                            break;
                        }
                        else if (hit.distance > 3.0f)
                        {
                            validOpenSpots.Add(candidate);
                            break;
                        }
                    }
                }
            }
        }

        if (validOpenSpots.Count > 0)
        {
            targetCamPos = validOpenSpots[Random.Range(0, validOpenSpots.Count)];
            
            // Apuntar hacia la dirección con mayor espacio libre visible
            float maxDist = -1f;
            float bestAngle = 0f;
            foreach (float angle in new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                float d = Physics.Raycast(targetCamPos, dir, out RaycastHit hit, 20f) ? hit.distance : 20f;
                if (d > maxDist)
                {
                    maxDist = d;
                    bestAngle = angle;
                }
            }
            targetCamRot = Quaternion.Euler(2f, bestAngle, 0f);
            Debug.Log($"[MenuCamera] Punto aleatorio abierto detectado automáticamente en {targetCamPos} apuntando a {bestAngle}°");
            return;
        }

        // 3. TERCEAR OPCIÓN (Fallback): Posición original del Inspector
        targetCamPos = mainCam.transform.position;
        targetCamRot = mainCam.transform.rotation;
    }

    void UpdateFlashlight(Camera cam)
    {
        if (flashlight == null && cam != null)
        {
            GameObject fObj = new GameObject("[Menu_Flashlight]");
            fObj.transform.SetParent(cam.transform);
            fObj.transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
            fObj.transform.localRotation = Quaternion.identity;

            flashlight            = fObj.AddComponent<Light>();
            flashlight.type       = LightType.Spot;
            flashlight.range      = 45f;
            flashlight.spotAngle  = 65f; // Halo central visible en medio de la pantalla
            flashlight.color      = new Color(1.0f, 0.95f, 0.85f);
            flashlight.intensity  = 6.0f;
            flashlight.shadows    = LightShadows.Soft;

            // Luz de relleno ambiental tenue para revelar sutilmente el pasillo
            GameObject aObj = new GameObject("[Menu_FillLight]");
            aObj.transform.SetParent(cam.transform);
            aObj.transform.localPosition = Vector3.zero;
            Light fill      = aObj.AddComponent<Light>();
            fill.type       = LightType.Point;
            fill.range      = 20f;
            fill.intensity  = 1.5f;
            fill.color      = new Color(0.3f, 0.4f, 0.35f);
        }

        if (flashlight != null)
        {
            // Efecto parpadeante ligero de linterna VHS en tiempo real
            float noise = Mathf.PerlinNoise(Time.time * 8f, 0f);
            float intensity = Mathf.Lerp(4.5f, 7.5f, noise);
            if (Random.value < 0.02f) intensity *= Random.Range(0.3f, 0.6f);
            flashlight.intensity = intensity;
        }
    }
}
