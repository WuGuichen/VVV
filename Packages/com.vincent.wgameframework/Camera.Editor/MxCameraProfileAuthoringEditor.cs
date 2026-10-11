using System.Text;
using MxFramework.Camera.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Camera.Editor
{
    [CustomEditor(typeof(MxCameraProfileAuthoringAsset))]
    public sealed class MxCameraProfileAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (MxCameraProfileAuthoringAsset)target;
            if (GUILayout.Button("Validate Camera Profile"))
                Debug.Log(BuildValidationReport(asset), asset);

            if (GUILayout.Button("Export Runtime DTO To Console"))
            {
                MxCameraProfileDefinition profile = asset.ExportRuntimeDefinition();
                Debug.Log("Camera profile exported: id=" + profile.ProfileId
                    + " mode=" + profile.Mode
                    + " distance=" + profile.Distance
                    + " fov=" + profile.FieldOfView
                    + " ortho=" + profile.OrthographicSize, asset);
            }
        }

        [MenuItem("MxFramework/Camera/Create Profile Asset")]
        public static void CreateProfileAsset()
        {
            var asset = ScriptableObject.CreateInstance<MxCameraProfileAuthoringAsset>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/MxCameraProfile.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static string BuildValidationReport(MxCameraProfileAuthoringAsset asset)
        {
            if (asset == null)
                return MxCameraDiagnosticCodes.InvalidProfile + ": asset is null";

            var diagnostics = asset.Validate();
            if (diagnostics.Count == 0)
                return "Camera profile validation passed.";

            var builder = new StringBuilder();
            for (int i = 0; i < diagnostics.Count; i++)
            {
                MxCameraDiagnostic diagnostic = diagnostics[i];
                builder.Append(diagnostic.Code)
                    .Append(" field=")
                    .Append(diagnostic.Field)
                    .Append(" message=")
                    .Append(diagnostic.Message);
                if (i + 1 < diagnostics.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
