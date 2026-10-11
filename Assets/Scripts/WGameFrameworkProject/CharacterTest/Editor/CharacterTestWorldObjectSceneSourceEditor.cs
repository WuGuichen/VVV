using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [CustomEditor(typeof(CharacterTestWorldObjectSceneSource))]
    public sealed class CharacterTestWorldObjectSceneSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CharacterTestWorldObjectSceneSource source =
                (CharacterTestWorldObjectSceneSource)target;

            EditorGUILayout.HelpBox(
                "Generated runtime cache. Edit CharacterTest world object instances through MxFramework/Level Editor.",
                MessageType.Info);

            EditorGUILayout.LabelField("Enabled", source.Enabled.ToString());
            EditorGUILayout.LabelField("Movable Instances", source.MovableInstanceCount.ToString());
            EditorGUILayout.LabelField("Static Collider Instances", source.StaticColliderInstanceCount.ToString());
            EditorGUILayout.LabelField("Point Instances", source.PointInstanceCount.ToString());
            EditorGUILayout.LabelField("Region Instances", source.RegionInstanceCount.ToString());
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Authoring JSON", source.AuthoringJsonPath);
            EditorGUILayout.LabelField("Content Hash", source.AuthoringContentHash);
            EditorGUILayout.LabelField("Last Applied UTC", source.AuthoringLastAppliedUtc);

            if (source.LastDiagnostics != null && source.LastDiagnostics.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Last Diagnostics", EditorStyles.boldLabel);
                for (int i = 0; i < source.LastDiagnostics.Count; i++)
                    EditorGUILayout.LabelField(source.LastDiagnostics[i]);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Open Level Editor"))
                MxLevelEditorWindow.OpenWindow();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export Current Source To JSON"))
                    CharacterTestWorldObjectLevelEditorModule.ExportCurrentSceneSourceToDefaultJson();
                if (GUILayout.Button("Apply Default JSON To Source"))
                    CharacterTestWorldObjectLevelEditorModule.ApplyDefaultJsonToActiveSceneSource();
            }

            EditorGUILayout.Space(4f);
            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestWorldObjectAuthoringAdapter.ValidateDocumentForSource(
                    TryLoadDocument(),
                    source,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog);
            if (report != null && report.Issues.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Current JSON/source validation has "
                    + report.ErrorCount
                    + " error(s), "
                    + report.WarningCount
                    + " warning(s). Open Level Editor for details.",
                    report.HasErrors ? MessageType.Error : MessageType.Warning);
            }
        }

        private static LevelWorldObjectAuthoringDocument TryLoadDocument()
        {
            string path = CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath;
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (text == null)
                return null;

            return LevelWorldObjectAuthoringJson.LoadFromJson(text.text);
        }
    }
}
