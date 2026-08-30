using System.Collections;
using UnityEngine;

public class ArrivalElevatorController : MonoBehaviour
{
    [Header("Puertas del Ascensor de Llegada")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Ajustes de Desplazamiento")]
    public Vector3 leftSlideOffset = new Vector3(-0.75f, 0f, 0f);
    public Vector3 rightSlideOffset = new Vector3(0.75f, 0f, 0f);
    public float doorOpenDuration = 5.5f;

    private Vector3 leftDoorClosedPos;
    private Vector3 rightDoorClosedPos;
    private Vector3 leftDoorOpenPos;
    private Vector3 rightDoorOpenPos;

    public static bool IsPlayerInElevator = true;
    private bool doorsShouldOpen = false;

    void Start()
    {
        IsPlayerInElevator = true;

        // 1. Deshabilitar controles del jugador durante la intro en ascensor
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerMale");
        if (player == null) player = GameObject.Find("PlayerFemale");

        // 2. Resolver referencias de puertas
        ResolveDoors();

        if (player != null)
        {
            // 1. Obtener la posición del suelo de la cabina usando raycast desde el centro del ascensor
            Vector3 cabinPos = transform.position;
            RaycastHit floorHit;
            float floorY = cabinPos.y;
            if (Physics.Raycast(cabinPos + Vector3.up * 1.5f, Vector3.down, out floorHit, 5.0f))
            {
                floorY = floorHit.point.y; // Asentar directamente en la colisión física para no flotar
            }

            // 2. Resolver la dirección de las puertas de manera dinámica y robusta (por si el prefab está rotado)
            Vector3 lookDir = transform.forward;
            if (leftDoor != null && rightDoor != null)
            {
                Vector3 doorCenter = (leftDoor.position + rightDoor.position) * 0.5f;
                Vector3 dir = doorCenter - cabinPos;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    lookDir = dir.normalized;
                }
            }

            // 3. Colocar al jugador centrado y en la parte de atrás del ascensor (0.55m opuesto a la dirección de la puerta)
            Vector3 groundedPos = cabinPos - lookDir * 0.55f;
            groundedPos.y = floorY;

            player.transform.position = groundedPos;

            // 4. Forzar rotación mirando exactamente hacia la salida (las puertas)
            player.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            // 5. Desactivar controles y Cinemachine, y resetear la rotación de la cámara usando ResetCameraRotation
            var fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
            if (fpc != null)
            {
                fpc.ResetCameraRotation(Quaternion.LookRotation(lookDir, Vector3.up).eulerAngles.y);
                fpc.enabled = false;
            }

            var cc = player.GetComponentInChildren<CharacterController>(true);
            if (cc != null) cc.enabled = false;

            // 6. Registrar esta posición exacta de reaparición segura en el GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegistrarSpawnJugador(groundedPos, Quaternion.LookRotation(lookDir, Vector3.up));
            }
        }

        // 3. Registrar posiciones iniciales cerradas y posiciones abiertas
        if (leftDoor != null)
        {
            leftDoorClosedPos = leftDoor.localPosition;
            leftDoorOpenPos = leftDoorClosedPos + leftSlideOffset;
            leftDoor.localPosition = leftDoorClosedPos;
        }

        if (rightDoor != null)
        {
            rightDoorClosedPos = rightDoor.localPosition;
            rightDoorOpenPos = rightDoorClosedPos + rightSlideOffset;
            rightDoor.localPosition = rightDoorClosedPos;
        }

        // 4. Iniciar secuencia cinemática del ascensor
        StartCoroutine(CinematicSequence());
    }

    private void ResolveDoors()
    {
        if (leftDoor == null) leftDoor = transform.Find("Elevator_LeftDoor");
        if (leftDoor == null) leftDoor = transform.Find("LeftDoor");
        if (leftDoor == null) leftDoor = transform.Find("Door_Left");
        if (leftDoor == null) leftDoor = transform.Find("PanelIzq_0");

        if (rightDoor == null) rightDoor = transform.Find("Elevator_RightDoor");
        if (rightDoor == null) rightDoor = transform.Find("RightDoor");
        if (rightDoor == null) rightDoor = transform.Find("Door_Right");
        if (rightDoor == null) rightDoor = transform.Find("PanelDer_0");

        if (leftDoor == null || rightDoor == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name.ToLower();
                if (leftDoor == null && (n.Contains("left") || n.Contains("izq")) && n.Contains("door"))
                    leftDoor = t;
                else if (rightDoor == null && (n.Contains("right") || n.Contains("der")) && n.Contains("door"))
                    rightDoor = t;
            }
        }
    }

    void Update()
    {
        if (!doorsShouldOpen)
        {
            if (leftDoor != null) leftDoor.localPosition = leftDoorClosedPos;
            if (rightDoor != null) rightDoor.localPosition = rightDoorClosedPos;
        }
    }

    private IEnumerator CinematicSequence()
    {
        var tunnelsGen = FindFirstObjectByType<TunnelsGenerator>();
        var fixedLogic = FindFirstObjectByType<TunnelsFixedMapLogic>();

        SetFadeAlpha(tunnelsGen, fixedLogic, 1.0f);

        for (int i = 0; i < 5; i++)
        {
            SetFadeAlpha(tunnelsGen, fixedLogic, 1.0f);
            yield return null;
        }

        // B. Sonido de viaje del ascensor y sacudida suave
        float travelElapsed = 0f;
        float travelDuration = 2.0f;
        Vector3 originalCamLocalPos = ((Camera.main != null) ? Camera.main.transform.localPosition : Vector3.zero);

        AudioClip travelClip = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Viaje");
        if (travelClip == null) travelClip = Resources.Load<AudioClip>("Ascensor_Viaje");
        AudioSource humSource = null;

        if (travelClip != null)
        {
            humSource = gameObject.AddComponent<AudioSource>();
            humSource.clip = travelClip;
            humSource.loop = true;
            humSource.volume = 0.5f;
            humSource.spatialBlend = 0f;
            humSource.Play();
        }

        while (travelElapsed < travelDuration)
        {
            travelElapsed += Mathf.Min(Time.deltaTime, 0.05f);
            float fadeProgress = Mathf.Clamp01(1f - (travelElapsed / travelDuration));
            SetFadeAlpha(tunnelsGen, fixedLogic, fadeProgress);

            if (Camera.main != null)
            {
                float shakeAmt = Mathf.Lerp(0.015f, 0f, travelElapsed / travelDuration);
                float x = Random.Range(-shakeAmt, shakeAmt);
                float y = Random.Range(-shakeAmt, shakeAmt);
                Camera.main.transform.localPosition = originalCamLocalPos + new Vector3(x, y, 0f);
            }
            yield return null;
        }

        SetFadeAlpha(tunnelsGen, fixedLogic, 0f);
        if (Camera.main != null) Camera.main.transform.localPosition = originalCamLocalPos;

        if (humSource != null)
        {
            humSource.Stop();
            Destroy(humSource);
        }

        // C. Sonido Ding de llegada
        AudioClip arriveClip = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Llegar");
        if (arriveClip == null) arriveClip = Resources.Load<AudioClip>("Ascensor_Llegar");
        if (arriveClip != null)
        {
            AudioSource.PlayClipAtPoint(arriveClip, transform.position, 0.9f);
        }

        yield return new WaitForSeconds(0.4f);

        // D. Apertura suave de puertas
        doorsShouldOpen = true;

        AudioClip doorOpenClip = Resources.Load<AudioClip>("Audio/Hospital/hospital-opening-door");
        if (doorOpenClip == null) doorOpenClip = Resources.Load<AudioClip>("Audio/Hospital/doorOpenSound2");
        if (doorOpenClip != null)
        {
            AudioSource.PlayClipAtPoint(doorOpenClip, transform.position, 0.85f);
        }

        float openElapsed = 0f;
        while (openElapsed < doorOpenDuration)
        {
            openElapsed += Mathf.Min(Time.deltaTime, 0.05f);
            float rawT = Mathf.Clamp01(openElapsed / doorOpenDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, rawT);

            if (leftDoor != null)
            {
                leftDoor.localPosition = Vector3.Lerp(leftDoorClosedPos, leftDoorOpenPos, smoothT);
            }

            if (rightDoor != null)
            {
                rightDoor.localPosition = Vector3.Lerp(rightDoorClosedPos, rightDoorOpenPos, smoothT);
            }

            yield return null;
        }

        // Desactivar colisionadores de las puertas para que NO bloqueen el paso del jugador
        if (leftDoor != null)
        {
            leftDoor.localPosition = leftDoorOpenPos;
            foreach (Collider c in leftDoor.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
                c.enabled = false;
            }
        }

        if (rightDoor != null)
        {
            rightDoor.localPosition = rightDoorOpenPos;
            foreach (Collider c in rightDoor.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
                c.enabled = false;
            }
        }

        // E. Restaurar control del jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerMale");
        if (player == null) player = GameObject.Find("PlayerFemale");

        if (player != null)
        {
            var fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
            if (fpc != null) fpc.enabled = true;

            var cc = player.GetComponentInChildren<CharacterController>(true);
            if (cc != null) cc.enabled = true;
        }

        IsPlayerInElevator = false;
        Debug.Log("[ArrivalElevatorController] 🔓 Colisionadores de puertas desactivados y control restaurado al 100%. El jugador ya puede salir.");
    }

    private void SetFadeAlpha(TunnelsGenerator gen, TunnelsFixedMapLogic fixedLogic, float alpha)
    {
        if (gen != null) gen.VictoryFadeAlpha = alpha;
        if (fixedLogic != null) fixedLogic.VictoryFadeAlpha = alpha;
    }
}
