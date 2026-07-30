using UnityEngine;
using UnityEditor;

namespace ModularHospital.Editor
{
    public class DeskTextureReplacer
    {
        [MenuItem("Tools/Aplicar Escritorio de Horror en la Oficina del Director")]
        public static void ApplyHorrorDeskTextures()
        {
            string dfkDeskPath = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Prefabs/Furnitures/dfk_desk_02.prefab";
            GameObject dfkDeskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dfkDeskPath);

            if (dfkDeskPrefab == null)
            {
                // Busqueda de respaldo si la ruta variara
                string[] guids = AssetDatabase.FindAssets("dfk_desk_02 t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    dfkDeskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            if (dfkDeskPrefab == null)
            {
                Debug.LogError("DeskTextureReplacer: No se encontró el prefab de escritorio dfk_desk_02.");
                return;
            }

            // 0. Convertir Materiales de Escritorio y Silla de Standard a URP Lit para evitar color magenta rosa
            string dfkMatPath = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_desks_01.mat";
            Material dfkMat = AssetDatabase.LoadAssetAtPath<Material>(dfkMatPath);
            if (dfkMat != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                dfkMat.shader = urpShader;

                Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_desks_01_basecolor.png");
                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_desks_01_normal.png");

                if (baseTex != null)
                {
                    dfkMat.mainTexture = baseTex;
                    if (dfkMat.HasProperty("_BaseMap")) dfkMat.SetTexture("_BaseMap", baseTex);
                }
                if (normalTex != null)
                {
                    if (dfkMat.HasProperty("_BumpMap")) dfkMat.SetTexture("_BumpMap", normalTex);
                }
                EditorUtility.SetDirty(dfkMat);
            }

            // Convertir Material de la Silla (dfk_chair_01)
            string chairMatPath = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_chair_01.mat";
            Material chairMat = AssetDatabase.LoadAssetAtPath<Material>(chairMatPath);
            if (chairMat != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                chairMat.shader = urpShader;

                Texture2D chairBaseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_chair_01_basecolor.png");
                Texture2D chairNormalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RunemarkStudio/DarkFantasyKit [Free]/Meshes/Furniture/Materials/dfk_chair_01_normal.png");

                if (chairBaseTex != null)
                {
                    chairMat.mainTexture = chairBaseTex;
                    if (chairMat.HasProperty("_BaseMap")) chairMat.SetTexture("_BaseMap", chairBaseTex);
                }
                if (chairNormalTex != null)
                {
                    if (chairMat.HasProperty("_BumpMap")) chairMat.SetTexture("_BumpMap", chairNormalTex);
                }
                EditorUtility.SetDirty(chairMat);
            }

            int replacedCount = 0;

            // 1. Reemplazar escritorios (Prb)Desk en la Escena por el Escritorio de Madera Real (dfk_desk_02)
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in allObjects)
            {
                if (go != null && go.name.Contains("(Prb)Desk"))
                {
                    Transform parent = go.transform.parent;
                    Vector3 pos = go.transform.position;
                    Quaternion rot = go.transform.rotation;
                    Vector3 scale = go.transform.localScale;

                    GameObject newDesk = (GameObject)PrefabUtility.InstantiatePrefab(dfkDeskPrefab);
                    newDesk.name = "Director_Desk_Horror";
                    newDesk.transform.SetParent(parent);
                    newDesk.transform.position = pos;
                    newDesk.transform.rotation = rot;
                    newDesk.transform.localScale = scale;

                    Undo.RegisterCreatedObjectUndo(newDesk, "Instantiate Horror Desk");
                    Undo.DestroyObjectImmediate(go);
                    replacedCount++;
                }

                // Reemplazar Silla (Prb)DiningChair por dfk_chair_01
                if (go != null && (go.name.Contains("(Prb)DiningChair") || go.name.Contains("diningChair")))
                {
                    string chairPrefabPath = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Prefabs/Furnitures/dfk_chair_01.prefab";
                    GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairPrefabPath);
                    if (chairPrefab != null)
                    {
                        Transform parent = go.transform.parent;
                        Vector3 pos = go.transform.position;
                        Quaternion rot = go.transform.rotation;
                        Vector3 scale = go.transform.localScale;

                        GameObject newChair = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab);
                        newChair.name = "Director_Chair_Horror";
                        newChair.transform.SetParent(parent);
                        newChair.transform.position = pos;
                        newChair.transform.rotation = rot;
                        newChair.transform.localScale = scale;

                        Undo.RegisterCreatedObjectUndo(newChair, "Instantiate Horror Chair");
                        Undo.DestroyObjectImmediate(go);
                        replacedCount++;
                    }
                }
            }

            // 2. Reemplazar en el Prefab Module_DirectorOffice si existe
            string directorOfficePath = "Assets/Dnk_Dev/Prefabs/Module_DirectorOffice.prefab";
            GameObject directorOfficePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(directorOfficePath);
            if (directorOfficePrefab != null)
            {
                Transform oldDesk = directorOfficePrefab.transform.Find("(Prb)Desk");
                if (oldDesk != null)
                {
                    GameObject contents = PrefabUtility.LoadPrefabContents(directorOfficePath);
                    Transform deskTransform = contents.transform.Find("(Prb)Desk");
                    if (deskTransform != null)
                    {
                        Vector3 lPos = deskTransform.localPosition;
                        Quaternion lRot = deskTransform.localRotation;
                        Vector3 lScale = deskTransform.localScale;

                        Object.DestroyImmediate(deskTransform.gameObject);

                        GameObject newDeskInstance = (GameObject)PrefabUtility.InstantiatePrefab(dfkDeskPrefab);
                        newDeskInstance.name = "Director_Desk_Horror";
                        newDeskInstance.transform.SetParent(contents.transform);
                        newDeskInstance.transform.localPosition = lPos;
                        newDeskInstance.transform.localRotation = lRot;
                        newDeskInstance.transform.localScale = lScale;

                        PrefabUtility.SaveAsPrefabAsset(contents, directorOfficePath);
                        PrefabUtility.UnloadPrefabContents(contents);
                        replacedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"DeskTextureReplacer: ¡{replacedCount} escritorio(s) reemplazado(s) exitosamente por el escritorio de madera real (dfk_desk_01)!");
        }
    }
}
