using System.Collections.Generic;
using System.Text;
using MxFramework.Animation;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    [CustomEditor(typeof(MxAnimationClipRegistryAsset))]
    public sealed class MxAnimationClipRegistryAssetEditor : UnityEditor.Editor
    {
        private string _lastReport = string.Empty;
        private bool _showEventTimeline = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            MxAnimationClipRegistryAsset registry = (MxAnimationClipRegistryAsset)target;
            if (GUILayout.Button("Validate Mapping Structure"))
            {
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
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

            DrawEventTimeline(registry);
        }

        private void DrawEventTimeline(MxAnimationClipRegistryAsset registry)
        {
            GUILayout.Space(8f);
            _showEventTimeline = EditorGUILayout.Foldout(_showEventTimeline, "Event Timeline Preview", true);
            if (!_showEventTimeline)
                return;

            MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
            IReadOnlyList<MxAnimationEventTimelineRow> rows =
                result.Definition != null
                    ? MxAnimationEventTimelineBuilder.BuildRows(result.Definition)
                    : new List<MxAnimationEventTimelineRow>();

            using (new EditorGUI.DisabledScope(rows.Count == 0))
            {
                if (GUILayout.Button("Copy Event Timeline Summary"))
                    EditorGUIUtility.systemCopyBuffer = CreateTimelineSummary(result, rows);
            }

            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No presentation events are defined.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationEventTimelineRow row = rows[i];
                string owner = row.SetLevel
                    ? "Set"
                    : (string.IsNullOrWhiteSpace(row.BindingId) ? row.ActionKey : row.BindingId);
                string label = owner
                    + " | "
                    + row.TimeDomain
                    + " "
                    + row.Time.ToString("0.###")
                    + " | "
                    + row.EventKind
                    + " | "
                    + row.EventId;

                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Payload", row.PayloadKey.IsValid ? row.PayloadKey.ToString() : "(none)");
                EditorGUILayout.LabelField("Replay Policy", row.ReplayPolicy.ToString());
                if (row.HasDeterministicCorrelation)
                    EditorGUILayout.LabelField("Correlation", row.CorrelationLabel);
                EditorGUI.indentLevel--;
            }
        }

        private static string CreateTimelineSummary(
            MxAnimationClipRegistryExportResult result,
            IReadOnlyList<MxAnimationEventTimelineRow> rows)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Event Timeline\n");
            builder.Append("setId: ").Append(result != null && result.Definition != null ? result.Definition.SetId : string.Empty).Append('\n');
            builder.Append("rows: ").Append(rows != null ? rows.Count : 0).Append('\n');

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationEventTimelineRow row = rows[i];
                builder.Append("- ")
                    .Append(row.SetLevel ? "set" : "binding")
                    .Append(" binding=")
                    .Append(row.BindingId)
                    .Append(" action=")
                    .Append(row.ActionKey)
                    .Append(" domain=")
                    .Append(row.TimeDomain)
                    .Append(" time=")
                    .Append(row.Time.ToString("R"))
                    .Append(" event=")
                    .Append(row.EventId)
                    .Append(" payload=")
                    .Append(row.PayloadKey)
                    .Append(" policy=")
                    .Append(row.ReplayPolicy)
                    .Append(" correlation=")
                    .Append(row.CorrelationLabel)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }
}
