using UnityEngine;
using System.Collections.Generic;

public class GlobalRedLightEvent : MonoBehaviour
{
    [Header("Event Settings")]
    public float minSecondsBetweenEvents = 60f;
    public float maxSecondsBetweenEvents = 120f;
    public float eventDuration = 20f;

    [Header("Transition")]
    public float colorTransitionSpeed = 2f;
    public Color alertColor = new Color(0.8f, 0.05f, 0.05f); // Rojo intenso

    private float timer;
    private float nextEventTime;
    private bool isEventActive = false;

    // Clase para guardar el estado original de la luz
    private class LightData
    {
        public Light lightComp;
        public Color originalColor;
    }

    private List<LightData> allLights = new List<LightData>();

    void Start()
    {
        ScheduleNextEvent();
        GatherLights();
    }

    void ScheduleNextEvent()
    {
        timer = 0f;
        nextEventTime = Random.Range(minSecondsBetweenEvents, maxSecondsBetweenEvents);
    }

    void GatherLights()
    {
        allLights.Clear();
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        
        foreach (Light l in lights)
        {
            // Omitir luces de la cámara del jugador (linterna, glowstick) o la luz cálida de la guía
            string parentName = l.transform.parent != null ? l.transform.parent.name.ToLower() : "";
            string lightName = l.name.ToLower();

            if (parentName.Contains("camera") || parentName.Contains("player") || lightName.Contains("guie"))
            {
                continue;
            }

            // Omitir el Directional Light del sol si existe
            if (l.type == LightType.Directional) continue;

            allLights.Add(new LightData { lightComp = l, originalColor = l.color });
        }
        
        Debug.Log($"[GlobalRedLightEvent] Administrando {allLights.Count} luces para eventos dinámicos.");
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (!isEventActive && timer >= nextEventTime)
        {
            isEventActive = true;
            timer = 0f; // Reiniciar timer para la duración del evento
            // Opcional: Reproducir sonido de alarma aquí
        }
        else if (isEventActive && timer >= eventDuration)
        {
            isEventActive = false;
            ScheduleNextEvent();
        }

        // Lerp de colores
        Color targetColor;
        foreach (LightData ld in allLights)
        {
            if (ld.lightComp == null) continue;

            targetColor = isEventActive ? alertColor : ld.originalColor;
            
            // Suavizar la transición del color
            ld.lightComp.color = Color.Lerp(ld.lightComp.color, targetColor, Time.deltaTime * colorTransitionSpeed);
        }
    }
}
