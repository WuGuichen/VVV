using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        private void RebuildStructuredRows()
        {
            _allListRows.Clear();
            _listRows.Clear();
            if (_structuredModule == null)
                return;

            IReadOnlyList<MxLevelEditorListRow> rows =
                _structuredModule.BuildRows(_context, _selectedTreeNodeId);
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                    _allListRows.Add(rows[i]);
            }

            ApplyRowSearchFilter();
            RestoreSelectedRowFromKey();
        }

        private void RebuildNavigationTreeNodes()
        {
            _navigationTreeNodes.Clear();
            _groupTreeNodes.Clear();
            for (int i = 0; i < _treeNodes.Count; i++)
            {
                MxLevelEditorTreeNode node = _treeNodes[i];
                if (node == null)
                    continue;

                if (node.Id.StartsWith("group:", StringComparison.Ordinal))
                {
                    _groupTreeNodes.Add(node);
                    continue;
                }

                if (node.Depth > 0)
                    continue;

                _navigationTreeNodes.Add(node);
            }

            if (_navigationTreeNodes.Count == 0 && _treeNodes.Count > 0)
                _navigationTreeNodes.Add(_treeNodes[0]);
        }

        private VisualElement MakeTreeNode()
        {
            var row = new VisualElement();
            row.AddToClassList("mx-level-editor__navigation-row");
            var label = CreateEllipsisLabel();
            label.name = "mx-level-editor-navigation-label";
            label.AddToClassList("mx-level-editor__navigation-label");
            row.Add(label);
            var count = new Label
            {
                name = "mx-level-editor-navigation-count"
            };
            count.AddToClassList("mx-level-editor__navigation-count");
            row.Add(count);
            return row;
        }

        private void BindTreeNode(VisualElement element, int index)
        {
            MxLevelEditorTreeNode node = index >= 0 && index < _navigationTreeNodes.Count
                ? _navigationTreeNodes[index]
                : null;
            BindNavigationNode(element, node);
        }

        private void BindGroupTreeNode(VisualElement element, int index)
        {
            MxLevelEditorTreeNode node = index >= 0 && index < _groupTreeNodes.Count
                ? _groupTreeNodes[index]
                : null;
            BindNavigationNode(element, node);
        }

        private static void BindNavigationNode(
            VisualElement element,
            MxLevelEditorTreeNode node)
        {
            Label label = element.Q<Label>("mx-level-editor-navigation-label");
            Label count = element.Q<Label>("mx-level-editor-navigation-count");
            if (label != null)
            {
                label.text = node == null
                    ? string.Empty
                    : LocalizeNavigationLabel(node.Id, node.Label);
                label.tooltip = node?.Label ?? string.Empty;
            }

            if (count != null)
                count.text = node == null || node.Count < 0 ? string.Empty : node.Count.ToString();
        }

        private static string LocalizeNavigationLabel(string id, string fallback)
        {
            switch (id)
            {
                case "document":
                    return "全部";
                case "movables":
                    return "可移动物体";
                case "static":
                    return "静态碰撞体";
                case "points":
                    return "关卡点";
                case "regions":
                    return "区域";
                case "owner:LocalPlayer":
                    return "本地玩家";
                case "owner:AiControlled":
                    return "Runtime AI Planner";
                case "owner:None":
                    return "无控制";
                default:
                    return fallback ?? string.Empty;
            }
        }

        private VisualElement CreateRowHeader()
        {
            var row = CreateRowContainer();
            row.AddToClassList("mx-level-editor__table-header");
            row.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.style.flexShrink = 0;
            row.Add(CreateColumnLabel("名称", 150));
            row.Add(CreateColumnLabel("类型", 110));
            row.Add(CreateColumnLabel("定义", 170));
            row.Add(CreateColumnLabel("场景对象", 260, 2));
            row.Add(CreateColumnLabel("状态", 84));
            return row;
        }

        private VisualElement MakeListRow()
        {
            var row = CreateRowContainer();
            row.AddToClassList("mx-level-editor__table-row");
            row.Add(CreateColumnLabel(string.Empty, 150, 0, "name"));
            row.Add(CreateColumnLabel(string.Empty, 110, 0, "kind"));
            row.Add(CreateColumnLabel(string.Empty, 170, 0, "definition"));
            row.Add(CreateColumnLabel(string.Empty, 260, 2, "transform"));
            row.Add(CreateColumnLabel(string.Empty, 84, 0, "status"));
            return row;
        }

        private void BindListRow(VisualElement element, int index)
        {
            MxLevelEditorListRow row = index >= 0 && index < _listRows.Count
                ? _listRows[index]
                : null;
            SetColumn(element, "name", ResolveRowDisplayName(row));
            SetColumn(element, "kind", row?.Kind);
            SetColumn(element, "definition", row?.Definition);
            SetColumn(
                element,
                "transform",
                ResolveTransformDisplayName(row?.Transform),
                row?.Transform);
            SetColumn(element, "status", ResolveRowDisplayStatus(row));
        }

        private static string ResolveTransformDisplayName(string transformPath)
        {
            if (string.IsNullOrWhiteSpace(transformPath))
                return "未绑定";

            string normalized = transformPath.Replace('\\', '/').TrimEnd('/');
            int separator = normalized.LastIndexOf('/');
            return separator >= 0 && separator < normalized.Length - 1
                ? normalized.Substring(separator + 1)
                : normalized;
        }

        private static string ResolveRowDisplayName(MxLevelEditorListRow row)
        {
            if (row == null)
                return string.Empty;

            string definition = row.Definition ?? string.Empty;
            int separator = definition.IndexOf(' ');
            if (separator >= 0 && separator < definition.Length - 1)
                return definition.Substring(separator + 1);
            if (!string.IsNullOrWhiteSpace(row.Id))
                return row.Id;
            return row.Kind ?? string.Empty;
        }

        private static string ResolveRowDisplayStatus(MxLevelEditorListRow row)
        {
            if (row == null)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(row.Status))
                return "● 有效";
            if (string.Equals(row.Status, "disabled", StringComparison.OrdinalIgnoreCase))
                return "○ 已禁用";
            return row.Status;
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selected)
        {
            foreach (object item in selected)
            {
                if (item is MxLevelEditorTreeNode node)
                {
                    if (node.Id.StartsWith("group:", StringComparison.Ordinal))
                        _treeView?.ClearSelection();
                    else
                        _groupTreeView?.ClearSelection();
                    _selectedTreeNodeId = node.Id;
                    RebuildStructuredRows();
                    RefreshRangeSummary();
                    _rowView?.Rebuild();
                    RestoreRowViewSelection();
                    RefreshStructuredInspector();
                    RefreshStructuredDiagnostics();
                    SceneView.RepaintAll();
                    return;
                }
            }
        }

        private void RevealStructuredRow(string treeNodeId, string rowKey)
        {
            if (_structuredModule == null || string.IsNullOrWhiteSpace(rowKey))
                return;

            string nextTreeNodeId = ContainsTreeNode(treeNodeId)
                ? NormalizeNavigationTreeNodeId(treeNodeId)
                : "document";
            _selectedTreeNodeId = nextTreeNodeId;
            _selectedListRowKey = rowKey.Trim();
            RebuildStructuredRows();
            if (_selectedListRow == null && !string.IsNullOrWhiteSpace(_rowSearchText))
            {
                _rowSearchText = string.Empty;
                _rowSearchField?.SetValueWithoutNotify(string.Empty);
                SetRowSearchStatus("已清除搜索，以显示新放置的对象。");
                RebuildStructuredRows();
            }

            RestoreNavigationSelection();

            RefreshRangeSummary();
            _rowView?.Rebuild();
            RestoreRowViewSelection();
            RefreshStructuredInspector();
            RefreshStructuredDiagnostics();
            SceneView.RepaintAll();
        }

        private VisualElement CreateRangeSummary()
        {
            var summary = new VisualElement { name = "mx-level-editor-range-summary" };
            summary.AddToClassList("mx-level-editor__main-header");
            summary.style.flexDirection = FlexDirection.Column;
            summary.style.flexShrink = 0;
            PopulateRangeSummary(summary);
            return summary;
        }

        private void RefreshRangeSummary()
        {
            if (_rangeSummaryHost == null)
                return;

            _rangeSummaryHost.Clear();
            PopulateRangeSummary(_rangeSummaryHost);
        }

        private void PopulateRangeSummary(VisualElement summary)
        {
            if (summary == null)
                return;

            MxLevelEditorTreeNode node = GetSelectedTreeNode();

            if (node != null && !string.IsNullOrWhiteSpace(node.Status))
            {
                var status = CreateEllipsisLabel();
                status.text = "状态：" + node.Status;
                status.tooltip = status.text;
                status.AddToClassList("mx-level-editor__range-status");
                summary.Add(status);
            }

            summary.Add(CreateRowSearchBar());
        }

        private VisualElement CreateNavigationModeTabs()
        {
            var tabs = new VisualElement { name = "mx-level-editor-navigation-tabs" };
            tabs.AddToClassList("mx-level-editor__navigation-tabs");

            var typeButton = new Button(() =>
            {
                SwitchNavigationMode(false);
            })
            {
                name = "mx-level-editor-navigation-type-tab",
                text = "按类型"
            };
            typeButton.EnableInClassList(
                "mx-level-editor__navigation-tab--selected",
                !_showGroupNavigation);
            tabs.Add(typeButton);

            var groupButton = new Button(() =>
            {
                if (_groupTreeNodes.Count == 0)
                    return;
                SwitchNavigationMode(true);
            })
            {
                name = "mx-level-editor-navigation-group-tab",
                text = "按分组"
            };
            groupButton.SetEnabled(_groupTreeNodes.Count > 0);
            groupButton.EnableInClassList(
                "mx-level-editor__navigation-tab--selected",
                _showGroupNavigation);
            tabs.Add(groupButton);
            return tabs;
        }

        private void SwitchNavigationMode(bool showGroups)
        {
            _showGroupNavigation = showGroups && _groupTreeNodes.Count > 0;
            if (_showGroupNavigation)
            {
                if (!_selectedTreeNodeId.StartsWith("group:", StringComparison.Ordinal))
                    _selectedTreeNodeId = _groupTreeNodes[0].Id;
            }
            else if (_selectedTreeNodeId.StartsWith("group:", StringComparison.Ordinal))
            {
                _selectedTreeNodeId = "document";
            }

            RefreshChrome();
        }

        private void UpdateNavigationModeVisibility()
        {
            if (_groupTreeNodes.Count == 0)
                _showGroupNavigation = false;

            if (_treeView != null)
                _treeView.style.display = _showGroupNavigation ? DisplayStyle.None : DisplayStyle.Flex;
            if (_groupTreeView != null)
                _groupTreeView.style.display = _showGroupNavigation ? DisplayStyle.Flex : DisplayStyle.None;

            Button typeButton =
                _contentHost?.Q<Button>("mx-level-editor-navigation-type-tab");
            Button groupButton =
                _contentHost?.Q<Button>("mx-level-editor-navigation-group-tab");
            typeButton?.EnableInClassList(
                "mx-level-editor__navigation-tab--selected",
                !_showGroupNavigation);
            groupButton?.EnableInClassList(
                "mx-level-editor__navigation-tab--selected",
                _showGroupNavigation);
        }

        private string BuildRangeCountText()
        {
            string filteredText = _allListRows.Count == _listRows.Count
                ? _listRows.Count + " 项"
                : _listRows.Count + "/" + _allListRows.Count + " 项";
            return filteredText;
        }

        private void RefreshRangeCountLabel()
        {
            Label countLabel = _rangeSummaryHost?.Q<Label>("mx-level-editor-range-count");
            if (countLabel != null)
                countLabel.text = BuildRangeCountText();
        }

        private MxLevelEditorTreeNode GetSelectedTreeNode()
        {
            for (int i = 0; i < _treeNodes.Count; i++)
            {
                if (string.Equals(_treeNodes[i].Id, _selectedTreeNodeId, StringComparison.Ordinal))
                    return _treeNodes[i];
            }

            return _treeNodes.Count > 0 ? _treeNodes[0] : null;
        }

    }
}
