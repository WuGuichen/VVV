using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        private void BuildStructuredContent(IMxLevelEditorStructuredModule module)
        {
            _treeNodes.Clear();
            IReadOnlyList<MxLevelEditorTreeNode> tree = module.BuildTree(_context);
            if (tree != null)
            {
                for (int i = 0; i < tree.Count; i++)
                {
                    if (tree[i] != null)
                        _treeNodes.Add(tree[i]);
                }
            }

            if (_treeNodes.Count == 0)
                _treeNodes.Add(new MxLevelEditorTreeNode("document", "Document"));
            if (!ContainsTreeNode(_selectedTreeNodeId))
                _selectedTreeNodeId = _treeNodes[0].Id;
            _selectedTreeNodeId = NormalizeNavigationTreeNodeId(_selectedTreeNodeId);
            RebuildNavigationTreeNodes();

            RebuildStructuredRows();

            var root = new VisualElement { name = "mx-level-editor-structured-content" };
            root.AddToClassList("mx-level-editor__structured-content");
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.minHeight = 0;
            root.style.minWidth = 0;
            _contentHost.Add(root);

            var body = new VisualElement { name = "mx-level-editor-structured-body" };
            body.AddToClassList("mx-level-editor__structured-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            body.style.minWidth = 0;
            root.Add(body);

            float availableWidth = position.width > 0f ? position.width : root.resolvedStyle.width;
            float leftPaneWidth = ReadLeftPaneWidthPreference(availableWidth);
            float inspectorPaneWidth = ReadInspectorPaneWidthPreference(availableWidth, leftPaneWidth);
            _outerSplitView = new TwoPaneSplitView(
                0,
                leftPaneWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "mx-level-editor-splitter-root"
            };
            _outerSplitView.style.flexGrow = 1;
            _outerSplitView.style.minHeight = 0;
            _outerSplitView.style.minWidth = 0;
            body.Add(_outerSplitView);

            var treePane = new VisualElement { name = "mx-level-editor-tree-pane" };
            treePane.AddToClassList("mx-level-editor__navigation");
            treePane.style.flexDirection = FlexDirection.Column;
            treePane.style.flexGrow = 1;
            treePane.style.minWidth = MinLeftPaneWidth;
            treePane.style.minHeight = 0;
            treePane.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPaneWidthPreference(LeftPaneWidthPrefKey, evt.newRect.width, MinLeftPaneWidth));
            _outerSplitView.Add(treePane);

            treePane.Add(CreateNavigationModeTabs());

            _rightSplitView = new TwoPaneSplitView(
                1,
                inspectorPaneWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "mx-level-editor-main-inspector-splitter"
            };
            _rightSplitView.style.flexGrow = 1;
            _rightSplitView.style.minWidth = MinListPaneWidth + MinInspectorPaneWidth;
            _rightSplitView.style.minHeight = 0;
            _outerSplitView.Add(_rightSplitView);

            _treeView = new ListView(_navigationTreeNodes, 38f, MakeTreeNode, BindTreeNode)
            {
                name = "mx-level-editor-tree-view",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            _treeView.AddToClassList("mx-level-editor__navigation-list");
            _treeView.style.flexGrow = 1;
            _treeView.style.minWidth = 0;
            _treeView.style.minHeight = 0;
            int treeIndex = IndexOfNavigationTreeNode(_selectedTreeNodeId);
            if (treeIndex >= 0)
                _treeView.SetSelectionWithoutNotify(new[] { treeIndex });
            _treeView.selectionChanged += OnTreeSelectionChanged;
            treePane.Add(_treeView);

            if (_groupTreeNodes.Count > 0)
            {
                _groupTreeView = new ListView(
                    _groupTreeNodes,
                    38f,
                    MakeTreeNode,
                    BindGroupTreeNode)
                {
                    name = "mx-level-editor-group-tree-view",
                    selectionType = SelectionType.Single,
                    virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
                };
                _groupTreeView.AddToClassList("mx-level-editor__group-list");
                _groupTreeView.style.flexGrow = 1;
                _groupTreeView.style.minHeight = 0;
                _groupTreeView.selectionChanged += OnTreeSelectionChanged;
                treePane.Add(_groupTreeView);
            }
            else
            {
                _groupTreeView = null;
            }
            UpdateNavigationModeVisibility();
            RestoreNavigationSelection();

            Button selectRoot = AddToolbarButton(treePane, "选择场景根节点", () =>
            {
                _context?.Module?.SelectRootSource();
                RefreshChrome();
            });
            selectRoot.name = "mx-level-editor-select-root";
            selectRoot.AddToClassList("mx-level-editor__navigation-footer-action");

            _rowPanel = new VisualElement { name = "mx-level-editor-row-panel" };
            _rowPanel.AddToClassList("mx-level-editor__row-panel");
            _rowPanel.style.flexGrow = 1;
            _rowPanel.style.minWidth = MinListPaneWidth;
            _rowPanel.style.minHeight = 0;
            _rowPanel.style.flexDirection = FlexDirection.Column;
            _rightSplitView.Add(_rowPanel);

            _rangeSummaryHost = CreateRangeSummary();
            _rowPanel.Add(_rangeSummaryHost);

            _rowPanel.Add(CreateRowHeader());
            _rowView = new ListView(_listRows, 26f, MakeListRow, BindListRow)
            {
                name = "mx-level-editor-row-view",
                selectionType = module is IMxLevelEditorBatchSelectionModule ? SelectionType.Multiple : SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            _rowView.AddToClassList("mx-level-editor__row-list");
            _rowView.style.flexGrow = 1;
            _rowView.style.minHeight = 0;
            _rowView.selectionChanged += OnRowSelectionChanged;
            _rowPanel.Add(_rowView);
            RestoreRowViewSelection();

            _inspectorHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "mx-level-editor-inspector",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _inspectorHost.AddToClassList("mx-level-editor__inspector");
            _inspectorHost.style.minWidth = MinInspectorPaneWidth;
            _inspectorHost.style.flexShrink = 0;
            _inspectorHost.style.minHeight = 0;
            _inspectorHost.style.paddingLeft = 10;
            _inspectorHost.style.paddingRight = 10;
            _inspectorHost.style.paddingTop = 6;
            _inspectorHost.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPaneWidthPreference(
                    InspectorPaneWidthPrefKey,
                    evt.newRect.width,
                    MinInspectorPaneWidth));
            _rightSplitView.Add(_inspectorHost);

            _diagnosticsFoldout = new Foldout
            {
                name = "mx-level-editor-diagnostics-foldout",
                text = "校验状态",
                value = false
            };
            _diagnosticsFoldout.AddToClassList("mx-level-editor__diagnostics");
            _diagnosticsFoldout.style.flexShrink = 0;
            _diagnosticsFoldout.style.marginLeft = 8;
            _diagnosticsFoldout.style.marginRight = 8;
            _diagnosticsFoldout.style.marginTop = 4;
            _diagnosticsFoldout.style.marginBottom = 4;

            _diagnosticsHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "mx-level-editor-diagnostics"
            };
            _diagnosticsHost.style.flexShrink = 0;
            _diagnosticsHost.style.maxHeight = 150;
            _diagnosticsHost.style.minHeight = 52;
            _diagnosticsHost.style.paddingLeft = 8;
            _diagnosticsHost.style.paddingRight = 8;
            _diagnosticsHost.style.paddingTop = 4;
            _diagnosticsFoldout.Add(_diagnosticsHost);
            root.Add(_diagnosticsFoldout);

            RefreshStructuredInspector();
            RefreshStructuredDiagnostics();
        }

    }
}
