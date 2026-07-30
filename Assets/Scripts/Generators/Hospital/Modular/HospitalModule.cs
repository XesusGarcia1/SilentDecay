using System.Collections.Generic;
using UnityEngine;

namespace ModularHospital
{
    public enum ModuleType
    {
        StraightCorridor,
        LongCorridor,
        Curve90,
        TJunction,
        Cross4Way,
        SmallRoom,
        LargeRoom,
        MedicalRoom,
        ElectricalRoom,
        MaintenanceRoom,
        OfficeRoom,
        CulDeSac,
        Elevator,
        DirectorOffice,
        GeneratorRoom,
        FuseBox,
        Fuse,
        Generator
    }

    public class HospitalModule : MonoBehaviour
    {
        [Header("Información del Módulo")]
        public ModuleType moduleType = ModuleType.StraightCorridor;
        public Vector3 moduleSize = new Vector3(4f, 4f, 4f);
        
        [Header("Guías Visuales del Editor")]
        [Tooltip("Muestra el recuadro amarillo de 4m x 4m en la vista Scene.")]
        public bool showBoundsGizmo = false;
        
        [Header("Conectores del Módulo")]
        public List<ModuleConnector> connectors = new List<ModuleConnector>();

        private void Reset()
        {
            FindConnectorsInChildren();
        }

        public void FindConnectorsInChildren()
        {
            connectors.Clear();
            ModuleConnector[] found = GetComponentsInChildren<ModuleConnector>(true);
            
            // Auto-Recuperación: Si no se encontró el componente ModuleConnector en los hijos, buscar transforms con nombre conector y agregar el componente automáticamente
            if (found == null || found.Length == 0)
            {
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                List<ModuleConnector> autoAdded = new List<ModuleConnector>();
                foreach (Transform child in allChildren)
                {
                    if (child == transform) continue;
                    string n = child.name.ToLower();
                    if (n.Contains("connector") || n.Contains("conector") || n.Contains("norte") || n.Contains("sur") || n.Contains("este") || n.Contains("oeste") || n.Contains("north") || n.Contains("south") || n.Contains("east") || n.Contains("west"))
                    {
                        ModuleConnector mc = child.gameObject.GetComponent<ModuleConnector>();
                        if (mc == null) mc = child.gameObject.AddComponent<ModuleConnector>();
                        autoAdded.Add(mc);
                    }
                }
                found = autoAdded.ToArray();
            }

            foreach (ModuleConnector mc in found)
            {
                if (mc == null) continue;
                mc.parentModule = this;
                if (!connectors.Contains(mc))
                {
                    connectors.Add(mc);
                }
            }
        }

        public List<ModuleConnector> GetUnconnectedConnectors()
        {
            List<ModuleConnector> list = new List<ModuleConnector>();
            foreach (ModuleConnector mc in connectors)
            {
                if (mc != null && !mc.isConnected)
                {
                    list.Add(mc);
                }
            }
            return list;
        }

        public Bounds GetModuleBounds()
        {
            return new Bounds(transform.position + Vector3.up * (moduleSize.y / 2f), moduleSize);
        }

        private void OnDrawGizmos()
        {
            if (!showBoundsGizmo) return;
            if (!showBoundsGizmo) return;

            // Dibujar la caja amarilla de límites todo el tiempo para guiar el armado
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);
            Bounds bounds = GetModuleBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        private void OnDrawGizmosSelected()
        {
            // Resaltar en verde brillante la celda activa cuando se selecciona en la Hierarchy
            Gizmos.color = Color.green;
            Bounds bounds = GetModuleBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
