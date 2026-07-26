using System.Collections;
using UnityEngine;

public class ArrivalElevatorController : MonoBehaviour
{
	public Transform leftDoor;
	public Transform rightDoor;
	public float mapScale = 3f;

	private Vector3 leftDoorClosedPos;
	private Vector3 rightDoorClosedPos;
	private Vector3 leftDoorOpenPos;
	private Vector3 rightDoorOpenPos;

	private bool doorsShouldOpen = false;
	private StarterAssets.FirstPersonController fpsController;

	void Start()
	{
		// Encontrar referencia del jugador
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null)
		{
			fpsController = player.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
			if (fpsController != null) fpsController.enabled = false;
		}

		// Resolver referencias de las puertas
		if (leftDoor == null) leftDoor = transform.Find("Elevator_LeftDoor");
		if (rightDoor == null) rightDoor = transform.Find("Elevator_RightDoor");

		float tileSize = 2.4f * mapScale; // 7.2f
		float innerHeight = 2.6f * mapScale; // 7.8f

		// Posiciones exactas del ascensor del hospital (escaladas)
		leftDoorClosedPos = new Vector3(-0.25f * tileSize, innerHeight / 2f, 0.488f * tileSize);
		rightDoorClosedPos = new Vector3(0.25f * tileSize, innerHeight / 2f, 0.488f * tileSize);

		float slideDistance = 0.44f * tileSize;
		leftDoorOpenPos = leftDoorClosedPos - new Vector3(slideDistance, 0f, 0f);
		rightDoorOpenPos = rightDoorClosedPos + new Vector3(slideDistance, 0f, 0f);

		// Forzar posición cerrada al inicio
		if (leftDoor != null) leftDoor.transform.localPosition = leftDoorClosedPos;
		if (rightDoor != null) rightDoor.transform.localPosition = rightDoorClosedPos;

		// Empezar secuencia cinemática
		StartCoroutine(CinematicSequence());
	}

	void Update()
	{
		// Forzar físicamente las puertas cerradas cada frame hasta que se indique su apertura
		if (!doorsShouldOpen)
		{
			if (leftDoor != null) leftDoor.transform.localPosition = leftDoorClosedPos;
			if (rightDoor != null) rightDoor.transform.localPosition = rightDoorClosedPos;
		}
	}

	private IEnumerator CinematicSequence()
	{
		// Forzar fundido a negro al iniciar
		var tunnelsGen = FindFirstObjectByType<TunnelsGenerator>();
		if (tunnelsGen != null)
		{
			tunnelsGen.VictoryFadeAlpha = 1.0f;
		}

		// Esperar unos frames a la estabilización de carga
		for (int i = 0; i < 5; i++)
		{
			if (tunnelsGen != null) tunnelsGen.VictoryFadeAlpha = 1.0f;
			yield return null;
		}

		// 2 segundos de sacudida de viaje
		float travelElapsed = 0f;
		float travelDuration = 2f;
		Vector3 originalCamLocalPos = ((Camera.main != null) ? Camera.main.transform.localPosition : Vector3.zero);
		
		AudioClip travelClip = Resources.Load<AudioClip>("Ascensor_Viaje");
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
			
			// Fading out de la pantalla negra
			if (tunnelsGen != null)
			{
				tunnelsGen.VictoryFadeAlpha = Mathf.Clamp01(1f - (travelElapsed / travelDuration));
			}

			if (Camera.main != null)
			{
				float shakeAmt = Mathf.Lerp(0.02f, 0f, travelElapsed / travelDuration);
				float x = Random.Range(-shakeAmt, shakeAmt);
				float y = Random.Range(-shakeAmt, shakeAmt);
				Camera.main.transform.localPosition = originalCamLocalPos + new Vector3(x, y, 0f);
			}
			yield return null;
		}

		if (tunnelsGen != null) tunnelsGen.VictoryFadeAlpha = 0f;
		if (Camera.main != null) Camera.main.transform.localPosition = originalCamLocalPos;

		if (humSource != null)
		{
			humSource.Stop();
			Destroy(humSource);
		}

		// Sonido Ding de llegada
		AudioClip arriveClip = Resources.Load<AudioClip>("Ascensor_Llegar");
		if (arriveClip != null)
		{
			AudioSource.PlayClipAtPoint(arriveClip, transform.position, 0.9f);
		}

		// Esperar 1 segundo antes de empezar a abrir
		float waitTime = 0f;
		while (waitTime < 1f)
		{
			waitTime += Mathf.Min(Time.deltaTime, 0.05f);
			yield return null;
		}

		// Activar apertura
		doorsShouldOpen = true;

		float openElapsed = 0f;
		float openDuration = 2f;
		while (openElapsed < openDuration)
		{
			openElapsed += Mathf.Min(Time.deltaTime, 0.05f);
			float t = Mathf.Clamp01(openElapsed / openDuration);
			if (leftDoor != null) leftDoor.transform.localPosition = Vector3.Lerp(leftDoorClosedPos, leftDoorOpenPos, t);
			if (rightDoor != null) rightDoor.transform.localPosition = Vector3.Lerp(rightDoorClosedPos, rightDoorOpenPos, t);
			yield return null;
		}

		// Forzar estado abierto al finalizar
		if (leftDoor != null) leftDoor.transform.localPosition = leftDoorOpenPos;
		if (rightDoor != null) rightDoor.transform.localPosition = rightDoorOpenPos;

		// Devolver control al jugador
		if (fpsController != null)
		{
			fpsController.enabled = true;
		}
		else
		{
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null)
			{
				var c = player.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
				if (c != null) c.enabled = true;
			}
		}
	}
}
