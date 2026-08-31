using UnityEngine;

public class RoomLightsManager : MonoBehaviour
{
    public Light[] roomLights; // Las luces que se van a controlar
    public bool powerOutage = false; // Estado del apagón

    // Método para activar o desactivar el apagón
    public void TriggerPowerOutage(bool outageState)
    {
        powerOutage = outageState;

        if (powerOutage)
        {
            foreach (Light light in roomLights)
            {
                if (light != null) light.enabled = false;
            }
        }
        else
        {
            // Dejamos que cada LightSwitch restaure su luz correspondiente en su propio Update()
        }
    }
}
