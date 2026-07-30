using System.Collections.Generic;
using UnityEngine;

namespace ModularHospital
{
    public class ModuleValidator : MonoBehaviour
    {
        [Header("Configuración de Colisión")]
        public LayerMask obstacleMask;
        public float collisionMargin = 0.2f;

        public static bool CheckBoundsOverlap(Bounds boundsA, Bounds boundsB, float margin = 0.05f)
        {
            boundsA.Expand(-margin);
            boundsB.Expand(-margin);
            return boundsA.Intersects(boundsB);
        }

        public bool IsSpaceAvailable(HospitalModule newModule, List<HospitalModule> existingModules)
        {
            if (newModule == null) return false;

            Bounds newBounds = newModule.GetModuleBounds();
            newBounds.Expand(-0.1f);

            foreach (HospitalModule existing in existingModules)
            {
                if (existing == null || existing == newModule) continue;

                Bounds existingBounds = existing.GetModuleBounds();
                existingBounds.Expand(-0.1f);

                if (newBounds.Intersects(existingBounds))
                {
                    return false;
                }
            }

            // Verificación física adicional usando OverlapBox
            Collider[] colliders = Physics.OverlapBox(newBounds.center, newBounds.extents, newModule.transform.rotation, obstacleMask);
            foreach (Collider col in colliders)
            {
                HospitalModule colModule = col.GetComponentInParent<HospitalModule>();
                if (colModule != null && colModule != newModule && !existingModules.Contains(colModule))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsWithinBuildingBounds(HospitalModule newModule, Bounds buildingBounds)
        {
            if (newModule == null) return false;
            Vector3 pos = newModule.transform.position;

            float maxAllowedX = (buildingBounds.size.x / 2.0f) - 2.01f;
            float maxAllowedZ = (buildingBounds.size.z / 2.0f) - 2.01f;

            return Mathf.Abs(pos.x - buildingBounds.center.x) <= maxAllowedX &&
                   Mathf.Abs(pos.z - buildingBounds.center.z) <= maxAllowedZ;
        }
    }
}
