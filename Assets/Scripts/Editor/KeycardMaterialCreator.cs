using UnityEngine;
using UnityEditor;

namespace ModularHospital.Editor
{
    public class KeycardMaterialCreator
    {
        [MenuItem("Tools/Crear Material para Tarjeta de Acceso de Ascensor")]
        public static void CreateKeycardMaterial()
        {
            string texPath = "Assets/Dnk_Dev/HospitalHorrorPack/Textures/T_ElevatorKeycard_Horror.jpg";
            Texture2D keycardTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            if (keycardTex == null)
            {
                Debug.LogError("KeycardMaterialCreator: No se encontró la textura T_ElevatorKeycard_Horror.jpg en Assets/Dnk_Dev/HospitalHorrorPack/Textures/");
                return;
            }

            string matPath = "Assets/Dnk_Dev/HospitalHorrorPack/Materials/Mat_ElevatorKeycard_Horror.mat";
            Material keycardMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (keycardMat == null)
            {
                keycardMat = new Material(urpShader);
                keycardMat.name = "Mat_ElevatorKeycard_Horror";
                AssetDatabase.CreateAsset(keycardMat, matPath);
            }

            keycardMat.shader = urpShader;
            keycardMat.mainTexture = keycardTex;
            if (keycardMat.HasProperty("_BaseMap")) keycardMat.SetTexture("_BaseMap", keycardTex);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("KeycardMaterialCreator: ¡Material Mat_ElevatorKeycard_Horror creado y listo para aplicar a tu cubo de tarjeta de acceso!");
        }
    }
}
