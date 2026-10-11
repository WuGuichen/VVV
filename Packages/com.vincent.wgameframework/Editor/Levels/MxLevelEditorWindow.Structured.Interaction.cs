using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        private readonly List<string> _selectedRowKeys = new List<string>();
        private void OnRowSelectionChanged(IEnumerable<object> selected)
        {
            var rows = selected.OfType<MxLevelEditorListRow>().ToArray();
            _selectedRowKeys.Clear(); _selectedRowKeys.AddRange(rows.Select(r => r.Key));
            _selectedListRow = rows.FirstOrDefault();

            _selectedListRowKey = _selectedListRow?.Key ?? string.Empty;
            RefreshStructuredInspector();
            if (!_suppressRowSelectionSync)
            {
                if (rows.Length > 1 && _structuredModule is IMxLevelEditorBatchSelectionModule)
                {
                    var objects = new List<UnityEngine.Object>();
                    foreach (var row in rows)
                        if (_selectionSyncModule?.TryResolveRowObject(_context, row, out var target) == true && target != null) objects.Add(target);
                    _suppressUnitySelectionSync = true;
                    try { Selection.objects = objects.ToArray(); }
                    finally { _suppressUnitySelectionSync = false; }
                }
                else SyncUnitySelectionFromRow(_selectedListRow);
            }
            SceneView.RepaintAll();
        }

        private void RefreshStructuredInspector()
        {
            if (_structuredModule == null || _inspectorHost == null)
                return;

            _inspectorHost.Clear();
            var rows = _listRows.Where(r => _selectedRowKeys.Contains(r.Key)).ToArray();
            if (rows.Length > 1 && _structuredModule is IMxLevelEditorBatchSelectionModule batch)
                batch.BuildBatchInspector(_context, rows, _inspectorHost);
            else _structuredModule.BuildInspector(_context, _selectedListRow, _inspectorHost);
        }

        private void RefreshStructuredDiagnostics()
        {
            if (_structuredModule == null || _diagnosticsHost == null)
                return;

            _diagnosticsHost.Clear();
            _structuredModule.BuildDiagnostics(_context, _diagnosticsHost);
            UpdateDiagnosticsFoldoutState();
        }

        private void UpdateDiagnosticsFoldoutState()
        {
            if (_diagnosticsFoldout == null || _diagnosticsHost == null)
                return;

            string summary = FindFirstDiagnosticsText();
            bool hasIssues = !string.IsNullOrWhiteSpace(summary)
                && !((summary.Contains("errors=0") && summary.Contains("warnings=0"))
                    || (summary.Contains("错误=0") && summary.Contains("警告=0")));
            if (string.IsNullOrWhiteSpace(summary))
                _diagnosticsFoldout.text = "校验状态 · 查看详情";
            else if (!hasIssues)
                _diagnosticsFoldout.text = "● 校验通过 · 0 错误 · 0 警告 · 查看详情";
            else
                _diagnosticsFoldout.text = "● 校验发现问题 · " + summary + " · 查看详情";
            _diagnosticsFoldout.value = hasIssues;
            _diagnosticsFoldout.EnableInClassList(
                "mx-level-editor__diagnostics--issues",
                hasIssues);
        }

        private string FindFirstDiagnosticsText()
        {
            if (_diagnosticsHost == null)
                return string.Empty;

            Label label = _diagnosticsHost.Q<Label>();
            return label == null ? string.Empty : label.text;
        }

        private static VisualElement CreateRowContainer()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.maxHeight = 26;
            row.style.minWidth = 0;
            row.style.flexShrink = 0;
            return row;
        }

        private static Label CreateColumnLabel(
            string text,
            int width,
            int flexGrow = 0,
            string name = null)
        {
            Label label = CreateEllipsisLabel();
            label.text = text ?? string.Empty;
            label.name = name ?? string.Empty;
            label.style.width = width;
            label.style.flexGrow = flexGrow;
            label.style.flexShrink = flexGrow > 0 ? 1 : 0;
            label.style.paddingLeft = 5;
            label.style.paddingRight = 5;
            return label;
        }

        private static Label CreateEllipsisLabel()
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

    }
}
