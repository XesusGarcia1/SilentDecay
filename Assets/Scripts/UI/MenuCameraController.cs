using UnityEngine;

/// <summary>
/// Controla la cámara flotante atmosférica del menú principal.
/// Busca un pasillo abierto en el mapa generado y hace un paneo suave.
/// </summary>
public class MenuCameraController
{
    private MainMenuManager ctx;
    private ModularHospital.ModularHospitalGenerator modGen;
    private float startYaw = -999f;
    private Light flashlight;

    public void Init(MainMenuManager manager)
    {
        ctx = manager;
    }

    public void Tick()
    {
        if (modGen == null)
            modGen = Object.FindObjectOfType<ModularHospital.ModularHospitalGenerator>();

        if (modGen == null || !modGen.isMenuMode) return;

        if (startYaw == -999f) startYaw = 90f;

        float swayAngle = Mathf.Sin(Time.time * 0.25f) * 10f;
        float slowWalk  = Mathf.Sin(Time.time * 0.12f) * 1.0f;

        if (Camera.main == null) return;

        Vector3 openCenter = FindOpenCorridorCenter();

        PickBestYaw(openCenter);

        Vector3 corridorRight = Quaternion.Euler(0, startYaw, 0) * Vector3.right;
        Vector3 desiredPos    = openCenter + corridorRight * (slowWalk * 0.40f);

        if (IsWallNear(desiredPos))
            desiredPos = openCenter;

        Camera.main.transform.position = desiredPos;
        Camera.main.transform.rotation = Quaternion.Euler(1.5f, startYaw + swayAngle, 0f);

        UpdateFlashlight();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    Vector3 FindOpenCorridorCenter()
    {
        Vector3 center = modGen.transform.position + new Vector3(2f, 1.35f, 2f);
        if (modGen.gridMatrix == null) return center;

        int sX = modGen.gridMatrix.GetLength(0);
        int sZ = modGen.gridMatrix.GetLength(1);
        float halfW = (sX * 4f) / 2f;
        float halfD = (sZ * 4f) / 2f;

        for (int cx = 2; cx < sX - 2; cx++)
        {
            for (int cz = 2; cz < sZ - 2; cz++)
            {
                if (modGen.gridMatrix[cx, cz] == 1 &&
                    modGen.gridMatrix[cx + 1, cz] == 1 &&
                    modGen.gridMatrix[cx - 1, cz] == 1)
                {
                    float wx = (cx * 4f) - halfW + 2f;
                    float wz = (cz * 4f) - halfD + 2f;
                    return modGen.transform.position + new Vector3(wx, 1.35f, wz);
                }
            }
        }
        return center;
    }

    void PickBestYaw(Vector3 from)
    {
        if (startYaw != -999f) return;
        float maxDist = -1f;
        float best = 90f;
        foreach (float a in new[] { 0f, 90f, 180f, 270f })
        {
            Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            float d = Physics.Raycast(from, dir, out var hit, 30f) ? hit.distance : 30f;
            if (d > maxDist) { maxDist = d; best = a; }
        }
        startYaw = best;
    }

    bool IsWallNear(Vector3 pos)
    {
        foreach (Collider c in Physics.OverlapSphere(pos, 0.75f))
        {
            if (c == null) continue;
            string n = c.name.ToLower();
            if (n.Contains("wall") || n.Contains("pared") || n.Contains("pillar") || n.Contains("muro"))
                return true;
        }
        return false;
    }

    void UpdateFlashlight()
    {
        if (flashlight == null)
        {
            GameObject fObj = new GameObject("[Menu_Flashlight]");
            fObj.transform.SetParent(Camera.main.transform);
            fObj.transform.localPosition = Vector3.zero;
            fObj.transform.localRotation = Quaternion.identity;

            flashlight            = fObj.AddComponent<Light>();
            flashlight.type       = LightType.Spot;
            flashlight.range      = 38f;
            flashlight.spotAngle  = 75f;
            flashlight.color      = new Color(0.98f, 0.96f, 0.90f);

            // Luz de relleno ambiental
            GameObject aObj = new GameObject("[Menu_FillLight]");
            aObj.transform.SetParent(Camera.main.transform);
            aObj.transform.localPosition = Vector3.zero;
            Light fill  = aObj.AddComponent<Light>();
            fill.type   = LightType.Point;
            fill.range  = 15f;
            fill.intensity = 1.2f;
            fill.color  = new Color(0.25f, 0.35f, 0.30f);
        }

        float noise = Mathf.PerlinNoise(Time.time * 7f, 0f);
        float intensity = Mathf.Lerp(3.2f, 5.8f, noise);
        if (Random.value < 0.025f) intensity *= Random.Range(0.45f, 0.70f);
        flashlight.intensity = intensity;
    }
}
