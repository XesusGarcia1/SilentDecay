using System.Collections.Generic;
using UnityEngine;

namespace ModularHospital
{
    public class ModuleDatabase : MonoBehaviour
    {
        [Header("Módulos Iniciales / Especiales")]
        public HospitalModule startModulePrefab;
        public HospitalModule directorOfficePrefab; // Habitación del Director

        [Header("Pasillos")]
        public List<HospitalModule> straightCorridorPrefabs = new List<HospitalModule>();
        public List<HospitalModule> longCorridorPrefabs = new List<HospitalModule>();
        public List<HospitalModule> curve90Prefabs = new List<HospitalModule>();
        public List<HospitalModule> tJunctionPrefabs = new List<HospitalModule>();
        public List<HospitalModule> cross4WayPrefabs = new List<HospitalModule>();

        [Header("Habitaciones")]
        public List<HospitalModule> smallRoomPrefabs = new List<HospitalModule>();

        [Header("Callejones Sin Salida / Cierres")]
        public List<HospitalModule> culDeSacPrefabs = new List<HospitalModule>();

        private void OnValidate()
        {
            AutoAssignPrefabsIfEmpty();
        }

        private void Awake()
        {
            AutoAssignPrefabsIfEmpty();
        }

        [ContextMenu("Auto-Asignar Prefabs Desde Proyecto")]
        public void AutoAssignPrefabsIfEmpty()
        {
#if UNITY_EDITOR
            if (startModulePrefab == null)
            {
                startModulePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_Cross4Way.prefab");
            }
            if (directorOfficePrefab == null)
            {
                directorOfficePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_DirectorOffice.prefab");
            }
            if (straightCorridorPrefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_StraightCorridor.prefab");
                if (p != null) straightCorridorPrefabs.Add(p);
            }
            if (curve90Prefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_Curve90.prefab");
                if (p != null) curve90Prefabs.Add(p);
            }
            if (tJunctionPrefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_TJunction.prefab");
                if (p != null) tJunctionPrefabs.Add(p);
            }
            if (cross4WayPrefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_Cross4Way.prefab");
                if (p != null) cross4WayPrefabs.Add(p);
            }
            if (smallRoomPrefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_SmallRoom.prefab");
                if (p != null) smallRoomPrefabs.Add(p);
            }
            if (culDeSacPrefabs.Count == 0)
            {
                HospitalModule p = UnityEditor.AssetDatabase.LoadAssetAtPath<HospitalModule>("Assets/Dnk_Dev/Prefabs/Module_CulDeSac.prefab");
                if (p != null) culDeSacPrefabs.Add(p);
            }
#endif
        }

        public List<HospitalModule> GetAllStandardModules()
        {
            AutoAssignPrefabsIfEmpty();
            List<HospitalModule> all = new List<HospitalModule>();
            all.AddRange(straightCorridorPrefabs);
            all.AddRange(longCorridorPrefabs);
            all.AddRange(curve90Prefabs);
            all.AddRange(tJunctionPrefabs);
            all.AddRange(cross4WayPrefabs);
            all.AddRange(smallRoomPrefabs);
            if (directorOfficePrefab != null) all.Add(directorOfficePrefab);
            return all;
        }

        public HospitalModule GetRandomModule(List<HospitalModule> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        public HospitalModule GetRandomStandardModule()
        {
            List<HospitalModule> all = GetAllStandardModules();
            return GetRandomModule(all);
        }

        public HospitalModule GetRandomCulDeSac()
        {
            AutoAssignPrefabsIfEmpty();
            return GetRandomModule(culDeSacPrefabs);
        }
    }
}
