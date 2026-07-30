using UnityEngine;

public class RoomLightsManager : MonoBehaviour
{
    public Light[] roomLights; // Las luces que se van a controlar
    public bool powerOutage = false; // Estado del apagón

    // Método para activar o desactivar el apagón
    public void TriggerPowerOutage(bool outageState)
    {
        powerOutage = outageState;
        Debug.Log("powerOutage cambiado a: " + powerOutage); // Imprime el valor para ver si se está actualizando correctamente

        if (powerOutage)
        {
            foreach (Light light in roomLights)
            {
                if (light != null) light.enabled = false;
            }
            Debug.Log("?? ¡Fallo eléctrico! Las luces se apagan.");
        }
        else
        {
            Debug.Log("? ¡Energía restaurada! Sincronizando interruptores.");
            // Dejamos que cada LightSwitch restaure su luz correspondiente en su propio Update()
        }
    }
}
