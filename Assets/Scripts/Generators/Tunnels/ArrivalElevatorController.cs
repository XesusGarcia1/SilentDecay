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
    public static Vector3 InitialElevatorSpawnPosition = Vector3.zero;
    public static Quaternion InitialElevatorSpawnRotation = Quaternion.identity;
    public static bool HasElevatorSpawn = false;

    private bool doorsShouldOpen = false;

    private void Awake()
    {
        HasElevatorSpawn = false;
    }

    private void OnDisable()
    {
        HasElevatorSpawn = false;
        IsPlayerInElevator = false;
    }

    private void OnDestroy()
    {
        HasElevatorSpawn = false;
        IsPlayerInElevator = false;
    }

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
            Vector3 cabinPos = transform.position;

            // Resolver la dirección de salida hacia las puertas del ascensor
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

            // Asentar exactamente en la colisión física del suelo dentro de la cabina
            RaycastHit floorHit;
            float floorY = cabinPos.y;
            if (Physics.Raycast(cabinPos + Vector3.up * 1.5f, Vector3.down, out floorHit, 4.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                floorY = floorHit.point.y;
            }

            // Posición centrada exactamente dentro de la cabina (detrás de las puertas)
            Vector3 groundedPos = cabinPos - lookDir * 0.45f;
            groundedPos.y = floorY;

            // Guardar esta posición como el Spawn Oficial Inicial del Ascensor
            InitialElevatorSpawnPosition = groundedPos;
            InitialElevatorSpawnRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            HasElevatorSpawn = true;

            var cc = player.GetComponentInChildren<CharacterController>(true);
            if (cc != null) cc.enabled = false;

            player.transform.position = groundedPos;
            player.transform.rotation = InitialElevatorSpawnRotation;
            Physics.SyncTransforms();

            // Actualizar la posición guardada con la posición REAL del jugador ya colocado en el suelo
            InitialElevatorSpawnPosition = player.transform.position;
            Debug.Log($"[ArrivalElevator] Posición de spawn guardada: {InitialElevatorSpawnPosition}, floorY={floorY}, cabinPos={cabinPos}");

            // Desactivar controles y Cinemachine, y resetear la rotación de la cámara
            var fpc = player.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
            if (fpc != null)
            {
                fpc.ResetCameraRotation(InitialElevatorSpawnRotation.eulerAngles.y);
                fpc.enabled = false;
            }

            // Registrar esta posición exacta de reaparición en GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegistrarSpawnJugador(groundedPos, InitialElevatorSpawnRotation);
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

        // B. Sonido de viaje del ascensor y sacudida suave (3.5 segundos de viaje)
        float travelElapsed = 0f;
        float travelDuration = 3.5f;
        Vector3 originalCamLocalPos = ((Camera.main != null) ? Camera.main.transform.localPosition : Vector3.zero);

        AudioClip travelClip = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Viaje");
        if (travelClip == null) travelClip = Resources.Load<AudioClip>("Ascensor_Viaje");
        AudioSource humSource = null;

        if (travelClip != null)
        {
            humSource = gameObject.AddComponent<AudioSource>();
            humSource.clip = travelClip;
            humSource.loop = true;
            humSource.volume = 0.65f;
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
                float shakeAmt = Mathf.Lerp(0.018f, 0.002f, travelElapsed / travelDuration);
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

        // B. Reproducir pista maestra `Ascensor_Llegar.mp3`
        // Contiene la secuencia completa: 0-3s frenado de cabina, 3s timbre DING, 4-6s apertura de puertas del ascensor.
        AudioClip arriveClip = Resources.Load<AudioClip>("Audio/Tuneles/Ascensor_Llegar");
        if (arriveClip == null) arriveClip = Resources.Load<AudioClip>("Ascensor_Llegar");
        if (arriveClip != null)
        {
            AudioSource.PlayClipAtPoint(arriveClip, transform.position, 1.0f);
        }

        // Esperar 3.8s para llegar exactamente al segundo 4.0 del audio donde suenan abriéndose las puertas
        yield return new WaitForSeconds(3.8f);

        // C. Disparar el monólogo inicial en sync con la apertura de puertas del audio
        LevelIntroData.TriggerStartMonologue("tunnels");

        // D. Apertura de puertas 3D a la par con el audio de Ascensor_Llegar (deslizamiento de 2.2s)
        doorsShouldOpen = true;

        float syncDoorOpenDuration = 5.5f; // Deslizamiento lento, pesado y pausado (~5.5 segundos)
        float openElapsed = 0f;
        while (openElapsed < syncDoorOpenDuration)
        {
            openElapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(openElapsed / syncDoorOpenDuration);
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

        // F. Restaurar control del jugador
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
        Debug.Log("[ArrivalElevatorController] 🔓 Secuencia de llegada en ascensor completada en perfecta sincronización.");
    }

    private void SetFadeAlpha(TunnelsGenerator gen, TunnelsFixedMapLogic fixedLogic, float alpha)
    {
        if (gen != null) gen.VictoryFadeAlpha = alpha;
        if (fixedLogic != null) fixedLogic.VictoryFadeAlpha = alpha;
    }
}
