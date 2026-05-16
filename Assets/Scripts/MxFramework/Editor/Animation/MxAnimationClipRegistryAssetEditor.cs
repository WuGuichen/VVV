using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    [CustomEditor(typeof(MxAnimationClipRegistryAsset))]
    public sealed class MxAnimationClipRegistryAssetEditor : UnityEditor.Editor
    {
        private string _lastReport = string.Empty;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            MxAnimationClipRegistryAsset registry = (MxAnimationClipRegistryAsset)target;
            if (GUILayout.Button("Validate Mapping"))
            {
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.Export(registry);
                _lastReport = MxAnimationClipRegistryExporter.CreateReportText(result);
                Debug.Log(_lastReport, registry);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastReport)))
            {
                if (GUILayout.Button("Copy Validation Report"))
                    EditorGUIUtility.systemCopyBuffer = _lastReport;
            }

            if (!string.IsNullOrEmpty(_lastReport))
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
        }
    }
}
