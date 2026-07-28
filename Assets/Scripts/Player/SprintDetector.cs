using UnityEngine;

public class SprintDetector : MonoBehaviour
{
    private StarterAssets.StarterAssetsInputs input; // Referencia a StarterAssetsInputs
    public bool IsRunning { get; private set; }

    private void Start()
    {
        // Intentar obtener el componente StarterAssetsInputs
        input = GetComponent<StarterAssets.StarterAssetsInputs>();
        if (input == null)
        {
            Debug.LogError("No se encontró StarterAssetsInputs en el jugador. Asegúrate de tener este componente en el GameObject.");
        }
    }

    private void Update()
    {
        if (input != null)
        {
            // Usa la variable 'sprint' de StarterAssetsInputs para determinar si el jugador está corriendo
            IsRunning = input.sprint;

            // Solo imprime el mensaje en el editor para evitar sobrecargar la consola en producción
#if UNITY_EDITOR
            Debug.Log(IsRunning ? "El jugador está corriendo" : "El jugador NO está corriendo");
#endif
        }
    }
}
