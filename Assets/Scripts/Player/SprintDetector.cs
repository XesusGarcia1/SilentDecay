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
            // Determinar si realmente está corriendo: tiene que estar presionando correr Y además estar en movimiento
            IsRunning = input.sprint && input.move.magnitude > 0.1f;

            // Solo imprime el mensaje en el editor para evitar sobrecargar la consola en producción
#if UNITY_EDITOR
            Debug.Log(IsRunning ? "El jugador está corriendo" : "El jugador NO está corriendo");
#endif
        }
    }
}
