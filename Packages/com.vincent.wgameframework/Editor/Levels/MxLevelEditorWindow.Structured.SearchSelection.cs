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
        private VisualElement CreateRowSearchBar()
        {
            var row = new VisualElement { name = "mx-level-editor-search-bar" };
            row.AddToClassList("mx-level-editor__search-bar");

            _rowSearchField = new TextField("搜索")
            {
                name = "mx-level-editor-search-field",
                value = _rowSearchText
            };
            _rowSearchField.AddToClassList("mx-level-editor__search-field");
            _rowSearchField.tooltip =
                "搜索名称、类型、分组、定义、标识、场景对象、状态或备注";
            _rowSearchField.style.flexGrow = 1;
            _rowSearchField.style.flexShrink = 1;
            _rowSearchField.style.minWidth = 220;
            _rowSearchField.style.maxWidth = 520;
            _rowSearchField.RegisterValueChangedCallback(evt =>
            {
                _rowSearchText = evt.newValue ?? string.Empty;
                SetRowSearchStatus(string.Empty);
                ApplyRowSearchFilter();
                RestoreSelectedRowFromKey();
                RefreshRangeCountLabel();
                _rowView?.Rebuild();
                RestoreRowViewSelection();
                RefreshStructuredInspector();
                SceneView.RepaintAll();
            });
            row.Add(_rowSearchField);

            Button clear = AddToolbarButton(row, "清除", () =>
            {
                _rowSearchText = string.Empty;
                SetRowSearchStatus(string.Empty);
                _rowSearchField.SetValueWithoutNotify(string.Empty);
                ApplyRowSearchFilter();
                RestoreSelectedRowFromKey();
                RefreshRangeCountLabel();
                _rowView?.Rebuild();
                RestoreRowViewSelection();
                RefreshStructuredInspector();
                SceneView.RepaintAll();
            });
            clear.name = "mx-level-editor-search-clear";
            clear.AddToClassList("mx-level-editor__search-clear");
            clear.style.flexShrink = 0;

            var countLabel = new Label(BuildRangeCountText())
            {
                name = "mx-level-editor-range-count"
            };
            countLabel.AddToClassList("mx-level-editor__count");
            row.Add(countLabel);

            _rowSearchStatusLabel = CreateEllipsisLabel();
            _rowSearchStatusLabel.name = "mx-level-editor-search-status";
            _rowSearchStatusLabel.text = _rowSearchStatusText;
            _rowSearchStatusLabel.tooltip = _rowSearchStatusText;
            _rowSearchStatusLabel.style.maxWidth = 220;
            _rowSearchStatusLabel.style.flexShrink = 1;
            _rowSearchStatusLabel.style.marginLeft = 6;
            row.Add(_rowSearchStatusLabel);

            var actions = new VisualElement { name = "mx-level-editor-module-toolbar" };
            actions.AddToClassList("mx-level-editor__module-actions");
            _context?.Module?.BuildModuleToolbar(this, actions);
            row.Add(actions);

            if (ModuleSupportsScenePlacement)
            {
                string placementText = _placementModule.IsScenePlacementActive
                    ? "结束放置"
                    : "放置";
                Button placement = null;
                placement = AddToolbarButton(row, placementText, () =>
                {
                    if (_placementModule.IsScenePlacementActive)
                    {
                        _placementModule.CancelScenePlacement(_context);
                        RefreshChrome();
                    }
                    else
                    {
                        TogglePlacementPopover(placement);
                    }
                });
                placement.name = "mx-level-editor-placement-mode";
                placement.AddToClassList("mx-level-editor__placement-mode-action");
                placement.EnableInClassList(
                    "mx-level-editor__placement-mode-action--active",
                    _placementModule.IsScenePlacementActive);
            }

            return row;
        }

        private void ApplyRowSearchFilter()
        {
            _listRows.Clear();
            string[] tokens = SplitSearchTokens(_rowSearchText);
            if (tokens.Length == 0)
            {
                _listRows.AddRange(_allListRows);
                return;
            }

            for (int i = 0; i < _allListRows.Count; i++)
            {
                MxLevelEditorListRow row = _allListRows[i];
                if (RowMatchesSearch(row, tokens))
                    _listRows.Add(row);
            }
        }

        private void RestoreSelectedRowFromKey()
        {
            _selectedListRow = FindVisibleRowByKey(_selectedListRowKey);
            if (_selectedListRow == null)
                _selectedListRowKey = string.Empty;
        }

        private void RestoreRowViewSelection()
        {
            if (_rowView == null)
                return;

            if (_structuredModule is IMxLevelEditorBatchSelectionModule && _selectedRowKeys.Count > 1)
            {
                var indices = _selectedRowKeys.Select(IndexOfVisibleRow).Where(i => i >= 0).ToArray();
                if (indices.Length > 0) { _rowView.SetSelectionWithoutNotify(indices); return; }
            }
            int index = IndexOfVisibleRow(_selectedListRowKey);
            if (index >= 0)
                _rowView.SetSelectionWithoutNotify(new[] { index });
            else
                _rowView.ClearSelection();
        }

        private void SyncUnitySelectionFromRow(MxLevelEditorListRow row)
        {
            if (_selectionSyncModule == null || row == null || _context == null)
                return;
            if (!_selectionSyncModule.TryResolveRowObject(_context, row, out UnityEngine.Object target)
                || target == null)
                return;

            _suppressUnitySelectionSync = true;
            try
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            finally
            {
                _suppressUnitySelectionSync = false;
            }
        }

        private void OnUnitySelectionChanged()
        {
            if (_suppressUnitySelectionSync
                || _activeWorkspace != null
                || _selectionSyncModule == null
                || _structuredModule == null
                || _context == null)
            {
                return;
            }

            if (_structuredModule is IMxLevelEditorBatchSelectionModule && Selection.objects.Length > 1)
            {
                var keys = new List<string>();
                foreach (var value in Selection.objects)
                    if (_selectionSyncModule.TryResolveSelection(_context, value, out var resolved) && resolved != null && !string.IsNullOrEmpty(resolved.RowKey)) keys.Add(resolved.RowKey);
                if (keys.Count > 1)
                {
                    _selectedRowKeys.Clear(); _selectedRowKeys.AddRange(keys.Distinct());
                    _selectedTreeNodeId = "document"; _selectedListRowKey = keys[0];
                    _rowSearchText = string.Empty; _rowSearchField?.SetValueWithoutNotify(string.Empty);
                    RebuildStructuredRows(); RestoreNavigationSelection(); _rowView?.Rebuild();
                    RestoreRowViewSelection(); RefreshStructuredInspector(); SceneView.RepaintAll(); return;
                }
            }
            _selectedRowKeys.Clear();
            UnityEngine.Object selected = Selection.activeObject;
            if (!_selectionSyncModule.TryResolveSelection(
                    _context,
                    selected,
                    out MxLevelEditorSelectionTarget target)
                || target == null
                || string.IsNullOrWhiteSpace(target.RowKey))
            {
                _selectedListRowKey = string.Empty;
                _selectedListRow = null;
                _suppressRowSelectionSync = true;
                try
                {
                    _rowView?.ClearSelection();
                    RefreshStructuredInspector();
                    SceneView.RepaintAll();
                }
                finally
                {
                    _suppressRowSelectionSync = false;
                }

                return;
            }

            string nextTreeNodeId = ResolveSelectionNavigationNode(target);
            bool treeChanged = !string.Equals(_selectedTreeNodeId, nextTreeNodeId, StringComparison.Ordinal);
            _selectedTreeNodeId = nextTreeNodeId;
            _selectedListRowKey = target.RowKey;
            RebuildStructuredRows();
            if (_selectedListRow == null && !string.IsNullOrWhiteSpace(_rowSearchText))
            {
                _selectedListRowKey = target.RowKey;
                _rowSearchText = string.Empty;
                _rowSearchField?.SetValueWithoutNotify(string.Empty);
                SetRowSearchStatus("已清除搜索，以显示当前选中对象。");
                RebuildStructuredRows();
            }

            if (_selectedListRow == null)
                return;

            if (treeChanged)
                RestoreNavigationSelection();

            _suppressRowSelectionSync = true;
            try
            {
                RefreshRangeSummary();
                _rowView?.Rebuild();
                RestoreRowViewSelection();
                RefreshStructuredInspector();
                SceneView.RepaintAll();
            }
            finally
            {
                _suppressRowSelectionSync = false;
            }
        }

        private string ResolveSelectionNavigationNode(MxLevelEditorSelectionTarget target)
        {
            // Scene selection echoes must not replace the user's current category.
            bool currentModeMatches = _selectedTreeNodeId.StartsWith("group:", StringComparison.Ordinal)
                == _showGroupNavigation;
            if (currentModeMatches && NavigationNodeContainsRow(_selectedTreeNodeId, target.RowKey))
                return _selectedTreeNodeId;

            var visibleNodes = _showGroupNavigation ? _groupTreeNodes : _navigationTreeNodes;
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                MxLevelEditorTreeNode node = visibleNodes[i];
                if (node.Id != "document" && NavigationNodeContainsRow(node.Id, target.RowKey))
                    return node.Id;
            }

            // Ungrouped objects still need a visible destination when browsing by group.
            if (_showGroupNavigation)
            {
                _showGroupNavigation = false;
                UpdateNavigationModeVisibility();
            }
            return "document";
        }

        private bool NavigationNodeContainsRow(string nodeId, string rowKey)
        {
            if (!ContainsTreeNode(nodeId))
                return false;

            IReadOnlyList<MxLevelEditorListRow> rows = _structuredModule.BuildRows(_context, nodeId);
            if (rows == null)
                return false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && string.Equals(rows[i].Key, rowKey, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private MxLevelEditorListRow FindVisibleRowByKey(string key)
        {
            int index = IndexOfVisibleRow(key);
            return index >= 0 ? _listRows[index] : null;
        }

        private int IndexOfVisibleRow(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return -1;

            for (int i = 0; i < _listRows.Count; i++)
            {
                if (string.Equals(_listRows[i].Key, key, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static bool RowMatchesSearch(MxLevelEditorListRow row, string[] tokens)
        {
            if (row == null)
                return false;

            string haystack = (row.Kind
                    + "\n"
                    + row.Group
                    + "\n"
                    + row.Definition
                    + "\n"
                    + row.Id
                    + "\n"
                    + row.Transform
                    + "\n"
                    + row.Status
                    + "\n"
                    + row.SearchText)
                .ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!haystack.Contains(tokens[i]))
                    return false;
            }

            return true;
        }

        private static string[] SplitSearchTokens(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            string[] raw = value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < raw.Length; i++)
                raw[i] = raw[i].Trim().ToLowerInvariant();
            return raw;
        }

        private void SetRowSearchStatus(string text)
        {
            _rowSearchStatusText = text ?? string.Empty;
            if (_rowSearchStatusLabel == null)
                return;

            _rowSearchStatusLabel.text = _rowSearchStatusText;
            _rowSearchStatusLabel.tooltip = _rowSearchStatusLabel.text;
        }

        private static void SetColumn(
            VisualElement row,
            string name,
            string value,
            string tooltip = null)
        {
            Label label = row.Q<Label>(name);
            if (label == null)
                return;

            string text = value ?? string.Empty;
            label.text = text;
            label.tooltip = string.IsNullOrWhiteSpace(tooltip) ? text : tooltip;
        }

        private static VisualElement CreateModuleToolbar()
        {
            var toolbar = new VisualElement { name = "mx-level-editor-module-toolbar" };
            toolbar.style.flexDirection = FlexDirection.Column;
            toolbar.style.flexShrink = 0;
            toolbar.style.minWidth = 0;
            toolbar.style.marginBottom = 6;
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;
            return toolbar;
        }

        private static Label CreateHeaderLabel(string text)
        {
            Label label = CreateEllipsisLabel();
            label.text = text ?? string.Empty;
            label.tooltip = label.text;
            label.style.flexShrink = 1;
            label.style.marginTop = 2;
            return label;
        }

        private bool ContainsTreeNode(string id)
        {
            return IndexOfTreeNode(id) >= 0;
        }

        private static string NormalizeNavigationTreeNodeId(string id)
        {
            if (string.Equals(id, "static.single", StringComparison.Ordinal)
                || string.Equals(id, "static.batches", StringComparison.Ordinal))
            {
                return "static";
            }

            return string.IsNullOrWhiteSpace(id) ? "document" : id;
        }

        private int IndexOfNavigationTreeNode(string id)
        {
            for (int i = 0; i < _navigationTreeNodes.Count; i++)
            {
                if (string.Equals(_navigationTreeNodes[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private int IndexOfGroupTreeNode(string id)
        {
            for (int i = 0; i < _groupTreeNodes.Count; i++)
            {
                if (string.Equals(_groupTreeNodes[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private void RestoreNavigationSelection()
        {
            int navigationIndex = IndexOfNavigationTreeNode(_selectedTreeNodeId);
            if (navigationIndex >= 0)
            {
                _treeView?.SetSelectionWithoutNotify(new[] { navigationIndex });
                _groupTreeView?.ClearSelection();
                return;
            }

            int groupIndex = IndexOfGroupTreeNode(_selectedTreeNodeId);
            if (groupIndex >= 0)
            {
                _treeView?.ClearSelection();
                _groupTreeView?.SetSelectionWithoutNotify(new[] { groupIndex });
            }
        }

        private int IndexOfTreeNode(string id)
        {
            for (int i = 0; i < _treeNodes.Count; i++)
            {
                if (string.Equals(_treeNodes[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private const float PlacementPopoverWidth = 360f;
        private const float PlacementPopoverEdge = 8f;
        private const float PlacementPopoverGap = 4f;

        private VisualElement _rowPanel;
        private VisualElement _placementPopover;
        private Button _placementPopoverButton;

        public bool PlacementPopoverOpenForTests => _placementPopover != null;

        public static Vector2 ResolvePlacementPopoverLayoutForTests(
            float availableWidth,
            float anchorRight)
        {
            return ResolvePlacementPopoverLayout(availableWidth, anchorRight);
        }

        public void SetPlacementPopoverOpenForTests(bool open)
        {
            if (!open)
            {
                ClosePlacementPopover();
                return;
            }

            Button trigger = rootVisualElement.Q<Button>("mx-level-editor-placement-mode");
            if (trigger != null && _placementPopover == null)
                OpenPlacementPopover(trigger);
        }

        private void TogglePlacementPopover(Button trigger)
        {
            if (_placementPopover != null)
            {
                ClosePlacementPopover();
                return;
            }

            OpenPlacementPopover(trigger);
        }

        private void OpenPlacementPopover(Button trigger)
        {
            if (trigger == null
                || _rowPanel == null
                || _placementModule == null
                || _placementModule.IsScenePlacementActive)
            {
                return;
            }

            ClosePlacementPopover();

            var popover = new VisualElement
            {
                name = "mx-level-editor-placement-popover",
                focusable = true
            };
            popover.AddToClassList("mx-level-editor__placement-popover");

            var fields = new VisualElement
            {
                name = "mx-level-editor-placement-fields"
            };
            fields.AddToClassList("mx-level-editor__placement-fields");
            popover.Add(fields);

            var footer = new VisualElement
            {
                name = "mx-level-editor-placement-footer"
            };
            footer.AddToClassList("mx-level-editor__placement-footer");
            popover.Add(footer);

            var options = new VisualElement
            {
                name = "mx-level-editor-placement-options"
            };
            options.AddToClassList("mx-level-editor__placement-options");
            footer.Add(options);

            var actions = new VisualElement
            {
                name = "mx-level-editor-placement-actions"
            };
            actions.AddToClassList("mx-level-editor__placement-actions");
            footer.Add(actions);

            var panel = new MxLevelEditorPlacementPanel(fields, options, actions);
            panel.AddIconToggle(
                "mx-level-editor-scene-edit-toggle",
                "显示场景对象标签、范围和辅助轮廓；不影响放置预览与点击确认。",
                "景",
                ResolveEditorIcon("SceneViewFx"),
                _sceneMarkersEnabled,
                enabled =>
                {
                    _sceneMarkersEnabled = enabled;
                    EditorPrefs.SetBool(SceneMarkersEnabledPrefKey, enabled);
                    SceneView.RepaintAll();
                });

            _placementPopover = popover;
            _placementPopoverButton = trigger;
            _rowPanel.Add(popover);
            trigger.EnableInClassList("mx-level-editor__placement-mode-action--open", true);

            _placementModule.BuildPlacementPanel(
                new MxLevelEditorPlacementContext(
                    _context,
                    _selectedTreeNodeId,
                    _selectedListRow,
                    _allListRows,
                    ClosePlacementPopover),
                panel);

            popover.schedule.Execute(() => PositionPlacementPopover(trigger));
            popover.Focus();
        }

        private void ClosePlacementPopover()
        {
            _placementPopoverButton?.EnableInClassList(
                "mx-level-editor__placement-mode-action--open",
                false);
            _placementPopover?.RemoveFromHierarchy();
            _placementPopover = null;
            _placementPopoverButton = null;
        }

        private void PositionPlacementPopover(VisualElement trigger)
        {
            if (_placementPopover == null || _rowPanel == null || trigger == null)
                return;

            float availableWidth = _rowPanel.resolvedStyle.width;
            if (float.IsNaN(availableWidth) || availableWidth <= 0f)
                availableWidth = Mathf.Max(PlacementPopoverWidth, position.width);

            float anchorRight = trigger.worldBound.xMax - _rowPanel.worldBound.xMin;
            Vector2 layout = ResolvePlacementPopoverLayout(availableWidth, anchorRight);

            float top = trigger.worldBound.yMax
                - _rowPanel.worldBound.yMin
                + PlacementPopoverGap;
            _placementPopover.style.width = layout.x;
            _placementPopover.style.left = layout.y;
            _placementPopover.style.top = Mathf.Max(PlacementPopoverEdge, top);
        }

        private static Vector2 ResolvePlacementPopoverLayout(
            float availableWidth,
            float anchorRight)
        {
            float safeWidth = Mathf.Max(0f, availableWidth);
            float horizontalInset = Mathf.Min(PlacementPopoverEdge, safeWidth * 0.5f);
            float maxPopoverWidth = Mathf.Max(0f, safeWidth - horizontalInset * 2f);
            float width = Mathf.Min(PlacementPopoverWidth, maxPopoverWidth);
            float maxLeft = Mathf.Max(horizontalInset, safeWidth - width - horizontalInset);
            float left = Mathf.Clamp(anchorRight - width, horizontalInset, maxLeft);
            return new Vector2(width, left);
        }

        private void RegisterPlacementPopoverCallbacks()
        {
            rootVisualElement.UnregisterCallback<PointerDownEvent>(
                OnPlacementPopoverPointerDown,
                TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(
                OnPlacementPopoverKeyDown,
                TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerDownEvent>(
                OnPlacementPopoverPointerDown,
                TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnPlacementPopoverKeyDown,
                TrickleDown.TrickleDown);
        }

        private void OnPlacementPopoverPointerDown(PointerDownEvent evt)
        {
            if (_placementPopover == null || evt.target is not VisualElement target)
                return;
            if (IsInsideVisualElement(_placementPopover, target)
                || IsInsideVisualElement(_placementPopoverButton, target))
            {
                return;
            }

            ClosePlacementPopover();
        }

        private void OnPlacementPopoverKeyDown(KeyDownEvent evt)
        {
            if (_placementPopover == null || evt.keyCode != KeyCode.Escape)
                return;

            ClosePlacementPopover();
            evt.StopPropagation();
        }

        private static bool IsInsideVisualElement(VisualElement root, VisualElement target)
        {
            if (root == null || target == null)
                return false;

            VisualElement current = target;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static Texture2D ResolveEditorIcon(string name)
        {
            GUIContent content = EditorGUIUtility.IconContent(name);
            return content?.image as Texture2D;
        }
    }
}
