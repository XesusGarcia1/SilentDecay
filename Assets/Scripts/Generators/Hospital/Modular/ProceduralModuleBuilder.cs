using UnityEngine;

namespace ModularHospital
{
    public static class ProceduralModuleBuilder
    {
        public static HospitalModule CreateProceduralElevatorModule(Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject moduleObj = new GameObject("Module_ProceduralElevator");
            moduleObj.transform.SetParent(parent);
            moduleObj.transform.position = position;
            moduleObj.transform.rotation = rotation;

            HospitalModule module = moduleObj.AddComponent<HospitalModule>();
            module.moduleType = ModuleType.Elevator;
            module.moduleSize = new Vector3(4f, 4f, 4f);

            // Crear el conector de entrada (SouthConnector)
            GameObject connectorObj = new GameObject("SouthConnector");
            connectorObj.transform.SetParent(moduleObj.transform);
            connectorObj.transform.localPosition = new Vector3(0f, 0f, -2f);
            connectorObj.transform.localRotation = Quaternion.identity;

            ModuleConnector connector = connectorObj.AddComponent<ModuleConnector>();
            connector.direction = ConnectorDirection.South;
            connector.parentModule = module;

            module.connectors.Add(connector);

            // Construir cabina de ascensor procedural (Paredes, Luz, Puertas)
            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "CabinaAscensor";
            cabin.transform.SetParent(moduleObj.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 1.5f, 0.5f);
            cabin.transform.localScale = new Vector3(3f, 3f, 3f);

            // Luz interior del ascensor
            GameObject lightObj = new GameObject("LuzAscensor");
            lightObj.transform.SetParent(cabin.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.85f, 0.95f, 1f);
            l.intensity = 3.5f;
            l.range = 5f;

            Debug.Log("ModularHospital: Módulo procedural del ascensor creado exitosamente.");
            return module;
        }
    }
}
