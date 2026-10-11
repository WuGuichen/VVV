using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        public void BuildInspector(
            MxLevelEditorContext context,
            MxLevelEditorListRow selectedRow,
            VisualElement inspector)
        {
            _activeContext = context;
            _inspector = inspector;
            _selectedRow = selectedRow?.Payload as Row;
            _selectedDefinitionRow = selectedRow?.Payload as DefinitionRow;
            RefreshInspector();
        }

        internal void SetRuntimeInspectorForTests(bool showRuntimeInspector)
        {
            _showRuntimeInspector = showRuntimeInspector;
        }

        private void RefreshInspector(bool preserveScrollPosition = false)
        {
            if (_inspector == null)
                return;

            VisualElement inspector = _inspector;
            ScrollView inspectorScrollView = preserveScrollPosition
                ? inspector as ScrollView
                : null;
            Vector2 scrollOffset = inspectorScrollView?.scrollOffset
                ?? Vector2.zero;
            InspectorFocusLocator? focusLocator = preserveScrollPosition
                ? CaptureInspectorFocusLocator(inspector)
                : null;
            int refreshRevision = ++_inspectorRefreshRevision;
            inspector.Clear();
            BuildInspectorContents();
            if (preserveScrollPosition)
            {
                RestoreInspectorViewState(
                    inspector,
                    inspectorScrollView,
                    scrollOffset,
                    focusLocator,
                    refreshRevision);
            }
        }

        private void BuildInspectorContents()
        {
            if (_selectedDefinitionRow != null)
            {
                _inspector.Add(CreateDefinitionWorkspaceInspector(_selectedDefinitionRow));
                return;
            }
            if (_selectedRow == null)
            {
                _inspector.Add(CreateEmptyInspectorActions());
                _inspector.Add(CreateSelectionValidationInspector(_selectedRow));
                _inspector.Add(CreateRangeSummaryInspector());
                _inspector.Add(CreateDocumentMetadataFoldout());
                return;
            }

            _inspector.Add(CreateInspectorHeader(
                ResolveDefinitionDisplayName(GetRowDefinitionId(_selectedRow)),
                IsRowEnabled(_selectedRow)));
            _inspector.Add(CreateInspectorModeTabs());
            if (_showRuntimeInspector)
            {
                if (_selectedRow.Kind == RowKind.Region)
                {
                    _inspector.Add(
                        CreateGameplayTriggerRuntimeInspector(
                            _document.Regions[_selectedRow.Index]));
                }
                _inspector.Add(CreateSelectionValidationInspector(_selectedRow));
                _inspector.Add(CreateDocumentMetadataFoldout());
                return;
            }

            switch (_selectedRow.Kind)
            {
                case RowKind.Movable:
                    DrawInstanceInspector(_document.MovableInstances[_selectedRow.Index], _document.MovableInstances);
                    break;
                case RowKind.SingleStatic:
                    DrawInstanceInspector(_document.SingleStaticColliders[_selectedRow.Index], _document.SingleStaticColliders);
                    break;
                case RowKind.StaticBatch:
                    DrawBatchInspector(_document.StaticColliderBatches[_selectedRow.Index]);
                    break;
                case RowKind.Point:
                    DrawPointInspector(_document.Points[_selectedRow.Index]);
                    break;
                case RowKind.Region:
                    DrawRegionInspector(_document.Regions[_selectedRow.Index]);
                    break;
            }

            _inspector.Add(CreateDocumentMetadataFoldout());
        }

        private void RestoreInspectorViewState(
            VisualElement inspector,
            ScrollView inspectorScrollView,
            Vector2 scrollOffset,
            InspectorFocusLocator? focusLocator,
            int refreshRevision)
        {
            if (inspectorScrollView != null)
                inspectorScrollView.scrollOffset = scrollOffset;
            inspector.schedule.Execute(
                () =>
                {
                    if (refreshRevision != _inspectorRefreshRevision
                        || !ReferenceEquals(_inspector, inspector))
                    {
                        return;
                    }

                    if (inspectorScrollView != null)
                        inspectorScrollView.scrollOffset = scrollOffset;
                    ResolveInspectorFocusTarget(
                            inspector,
                            focusLocator)
                        ?.Focus();
                });
        }

        private static InspectorFocusLocator?
            CaptureInspectorFocusLocator(
            VisualElement inspector)
        {
            VisualElement focusedElement =
                inspector?.panel?.focusController?.focusedElement
                    as VisualElement;
            VisualElement contentRoot = inspector?.contentContainer;
            if (focusedElement == null
                || contentRoot == null
                || ReferenceEquals(focusedElement, contentRoot))
            {
                return null;
            }

            VisualElement target = focusedElement;
            while (!ReferenceEquals(target, contentRoot)
                   && (!target.canGrabFocus
                       || !IsStableInspectorFocusName(target.name)))
            {
                target = target.parent;
            }
            if (target == null || ReferenceEquals(target, contentRoot))
                return null;

            string entryName = string.Empty;
            string containerName = string.Empty;
            VisualElement current = target.parent;
            while (current != null
                   && !ReferenceEquals(current, contentRoot))
            {
                if (!string.IsNullOrWhiteSpace(current.name)
                    && current.name.StartsWith(
                        "character-test-trigger-entry-",
                        StringComparison.Ordinal))
                {
                    entryName = current.name;
                    break;
                }

                if (string.IsNullOrWhiteSpace(containerName)
                    && IsStableInspectorFocusName(current.name))
                {
                    containerName = current.name;
                }

                current = current.parent;
            }

            return new InspectorFocusLocator(
                entryName,
                containerName,
                target.name);
        }

        private static VisualElement ResolveInspectorFocusTarget(
            VisualElement inspector,
            InspectorFocusLocator? focusLocator)
        {
            if (inspector == null
                || !focusLocator.HasValue)
            {
                return null;
            }

            InspectorFocusLocator locator = focusLocator.Value;
            VisualElement scope = inspector.contentContainer;
            if (!string.IsNullOrWhiteSpace(locator.EntryName))
            {
                scope = scope.Q<VisualElement>(locator.EntryName);
                if (scope == null)
                    return null;
            }

            if (!string.IsNullOrWhiteSpace(locator.ContainerName))
            {
                scope = scope.Q<VisualElement>(locator.ContainerName);
                if (scope == null)
                    return null;
            }

            VisualElement target = string.Equals(
                scope.name,
                locator.TargetName,
                StringComparison.Ordinal)
                ? scope
                : scope.Q<VisualElement>(locator.TargetName);
            return target != null && target.canGrabFocus
                ? target
                : null;
        }

        private static bool IsStableInspectorFocusName(string elementName)
        {
            return !string.IsNullOrWhiteSpace(elementName)
                && !elementName.StartsWith(
                    "unity-",
                    StringComparison.Ordinal);
        }

        private readonly struct InspectorFocusLocator
        {
            public InspectorFocusLocator(
                string entryName,
                string containerName,
                string targetName)
            {
                EntryName = entryName;
                ContainerName = containerName;
                TargetName = targetName;
            }

            public string EntryName { get; }
            public string ContainerName { get; }
            public string TargetName { get; }
        }

        private VisualElement CreateInspectorModeTabs()
        {
            var tabs = new VisualElement { name = "character-test-inspector-tabs" };
            tabs.AddToClassList("mx-level-editor__inspector-tabs");

            var properties = new Button(() =>
            {
                _showRuntimeInspector = false;
                RefreshInspector();
            })
            {
                name = "character-test-inspector-properties-tab",
                text = "属性"
            };
            properties.EnableInClassList(
                "mx-level-editor__inspector-tab--selected",
                !_showRuntimeInspector);
            tabs.Add(properties);

            var runtime = new Button(() =>
            {
                _showRuntimeInspector = true;
                RefreshInspector();
            })
            {
                name = "character-test-inspector-runtime-tab",
                text = "运行时检查"
            };
            runtime.EnableInClassList(
                "mx-level-editor__inspector-tab--selected",
                _showRuntimeInspector);
            tabs.Add(runtime);
            return tabs;
        }

        private VisualElement CreateEmptyInspectorActions()
        {
            var root = new VisualElement { name = "character-test-empty-inspector-actions" };
            root.AddToClassList("mx-level-editor__empty-inspector");
            root.Add(CreateInspectorTitle("开始编辑"));
            root.Add(new Label("在 Hierarchy 选择场景对象，可登记或检查它的运行时参与情况。"));

            var actions = new VisualElement();
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.Add(CreateSmallButton("同步当前选择", () =>
            {
                _activeContext?.Window?.RevealCurrentUnitySelection();
            }));
            actions.Add(CreateSmallButton("选择场景根节点", () =>
            {
                SelectRootSource();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            root.Add(actions);
            return root;
        }

        private VisualElement CreateDocumentFields()
        {
            var root = new VisualElement();
            root.Add(CreateInspectorTitle("文档"));
            root.Add(CreateTextField("关卡 ID", _document.LevelId, value => _document.LevelId = value));
            root.Add(CreateTextField("场景路径", _document.ScenePath, value => _document.ScenePath = value));
            root.Add(CreateTextField("根对象路径", _document.RootPath, value => _document.RootPath = value));
            return root;
        }

        private VisualElement CreateDocumentSummary()
        {
            var root = new VisualElement();
            root.style.marginTop = 8;
            root.Add(CreateInspectorTitle("模块摘要"));
            root.Add(new Label("模块 ID：" + CharacterTestWorldObjectAuthoringAdapter.WorldObjectsModuleId));
            root.Add(new Label("格式：" + (_envelope?.Format ?? MxLevelAuthoringDocument.CurrentFormat)));
            root.Add(new Label("Schema：" + (_envelope?.SchemaVersion ?? MxLevelAuthoringDocument.CurrentSchemaVersion)));
            root.Add(new Label(
                "数量：可移动物体="
                + Count(_document.MovableInstances)
                + "，单个静态碰撞体="
                + Count(_document.SingleStaticColliders)
                + "，静态批次="
                + Count(_document.StaticColliderBatches)
                + "，关卡点="
                + Count(_document.Points)
                + "，区域="
                + Count(_document.Regions)));
            return root;
        }

        private VisualElement CreateDocumentMetadataFoldout()
        {
            var foldout = new Foldout
            {
                name = "character-test-document-metadata",
                text = "文档元数据",
                value = false
            };
            foldout.style.marginTop = 8;
            foldout.Add(CreateDocumentFields());
            foldout.Add(CreateDocumentSummary());
            return foldout;
        }

        private VisualElement CreateRangeSummaryInspector()
        {
            var root = new VisualElement { name = "character-test-range-summary" };
            root.Add(CreateInspectorTitle(ResolveTreeLabel(_activeTreeNodeId)));
            root.Add(new Label("当前范围条目：" + CountRowsForTree(_activeTreeNodeId)));
            root.Add(new Label(
                "文档：可移动物体="
                + Count(_document.MovableInstances)
                + "，单个静态碰撞体="
                + Count(_document.SingleStaticColliders)
                + "，静态批次="
                + Count(_document.StaticColliderBatches)
                + "，关卡点="
                + Count(_document.Points)
                + "，区域="
                + Count(_document.Regions)));
            if (_report != null)
            {
                root.Add(new Label(
                    "校验：错误="
                    + _report.ErrorCount
                    + "，警告="
                    + _report.WarningCount));
                int shown = 0;
                for (int i = 0; i < _report.Issues.Count && shown < 3; i++)
                {
                    LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                    var label = new Label(CharacterTestLevelEditorChineseText.DiagnosticText(issue))
                    {
                        tooltip = CharacterTestLevelEditorChineseText.DiagnosticTooltip(issue)
                    };
                    root.Add(label);
                    shown++;
                }
            }

            root.Add(new HelpBox("使用列表工具栏添加当前选中的场景对象，然后点击保存。", HelpBoxMessageType.Info));
            return root;
        }

        private VisualElement CreateSelectionValidationInspector(Row selectedRow)
        {
            SelectionValidationSummary summary =
                BuildSelectionValidationSummary(selectedRow, Selection.activeObject);
            var root = new VisualElement { name = "character-test-selection-validation" };
            root.style.marginBottom = 8;
            root.Add(CreateInspectorTitle("选中对象校验"));
            root.Add(new Label("状态：" + summary.Status));

            if (!summary.IsRegistered)
            {
                if (!string.IsNullOrWhiteSpace(summary.TransformPath))
                    root.Add(new Label("场景对象路径：" + summary.TransformPath));
                if (!string.IsNullOrWhiteSpace(summary.Group))
                    root.Add(new Label("分组：" + summary.Group));
                root.Add(new Label(summary.RuntimeCache));
                if (summary.IsAddableSelection)
                    root.Add(CreateValidationAddActions());
                else
                    root.Add(new HelpBox("请选择 CharacterTest 场景对象或已登记条目，以查看运行时参与情况。", HelpBoxMessageType.Info));
                return root;
            }

            VisualElement runtime = CreateInspectorSection("运行时参与情况");
            runtime.name = "character-test-runtime-participation";
            runtime.Add(new Label("对象类型：" + summary.Role));
            runtime.Add(new Label("对象定义：" + summary.Definition));
            runtime.Add(new Label("分组：" + summary.Group));
            runtime.Add(new Label("场景对象路径：" + summary.TransformPath));
            runtime.Add(new Label("Transform 引用：" + summary.TransformStatus));
            runtime.Add(new Label("运行时缓存：" + summary.RuntimeCache));
            if (!string.IsNullOrWhiteSpace(summary.BodyIdPreview))
                runtime.Add(new Label("刚体 ID 预览：" + summary.BodyIdPreview));
            if (!string.IsNullOrWhiteSpace(summary.ColliderIdPreview))
                runtime.Add(new Label("碰撞体 ID 预览：" + summary.ColliderIdPreview));
            if (summary.IsBatchChild)
                runtime.Add(new Label("选中对象：静态批次的直接子对象"));
            root.Add(runtime);

            VisualElement definition = CreateInspectorSection("定义摘要");
            definition.Add(new Label("形状：" + summary.Shape));
            definition.Add(new Label("材质：" + summary.Material));
            definition.Add(new Label("强度 D/P/S：" + summary.Potency));
            if (!string.IsNullOrWhiteSpace(summary.IssueText))
                definition.Add(new HelpBox(summary.IssueText, HelpBoxMessageType.Warning));
            root.Add(definition);
            return root;
        }

        private VisualElement CreateValidationAddActions()
        {
            var actions = new VisualElement
            {
                name = "character-test-selection-validation-actions"
            };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 4;
            actions.Add(CreateValidationButton("添加可移动物体", "character-test-validation-add-movable", () =>
            {
                AddSelected(LevelWorldObjectRole.Movable, _selectedMovableDefinitionId);
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateValidationButton("添加静态碰撞体", "character-test-validation-add-static", () =>
            {
                AddSelected(LevelWorldObjectRole.StaticCollider, _selectedStaticDefinitionId);
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateValidationButton("创建静态批次", "character-test-validation-create-batch", () =>
            {
                AddStaticBatchFromSelection();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateValidationButton("添加关卡点", "character-test-validation-add-point", () =>
            {
                AddPointFromSelection();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateValidationButton("添加区域", "character-test-validation-add-region", () =>
            {
                AddRegionFromSelection();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            return actions;
        }

        private static Button CreateValidationButton(string text, string name, Action action)
        {
            Button button = CreateSmallButton(text, action);
            button.name = name;
            return button;
        }

        private void DrawInstanceInspector(
            LevelWorldObjectInstanceDefinition instance,
            List<LevelWorldObjectInstanceDefinition> owner)
        {
            VisualElement basicSection = CreateInspectorSection("基础属性");
            basicSection.Add(CreateToggle("启用", instance.Enabled, value => instance.Enabled = value));
            basicSection.Add(CreateInspectorDefinitionPopup(
                instance.DefinitionId,
                instance.Role == LevelWorldObjectRole.Movable
                    ? definition => definition.CanSpawnMovableNaturalObject
                    : definition => definition.CanSpawnStaticCollider,
                value => instance.DefinitionId = value));
            basicSection.Add(CreateTextField("分组", instance.Group, value => instance.Group = value));
            basicSection.Add(CreateTextField("场景对象", instance.TransformRef.Path, value =>
            {
                instance.TransformRef.Path = value;
                instance.TransformPath = value;
            }));
            basicSection.Add(CreateSelectionButtons(instance.TransformRef));
            _inspector.Add(basicSection);
            _inspector.Add(CreateQuickPlacementActions(_selectedRow));

            Foldout advancedIds = CreateAdvancedIdsFoldout();
            advancedIds.Add(CreateEnumField(
                "缩放来源",
                instance.ScaleSource,
                value => instance.ScaleSource = (LevelWorldObjectScaleSource)value));
            advancedIds.Add(CreateAdvancedIntegerField("定义 ID", instance.DefinitionId, value => instance.DefinitionId = value));
            if (instance.Role == LevelWorldObjectRole.Movable)
            {
                advancedIds.Add(CreateAdvancedIntegerField("物体 ID", instance.NumericInstanceId, value => instance.NumericInstanceId = value));
                advancedIds.Add(CreateAdvancedTextField("稳定 ID 覆盖值", instance.InstanceStableId, value => instance.InstanceStableId = value));
            }
            else
            {
                advancedIds.Add(CreateAdvancedIntegerField("刚体 ID", instance.BodyId, value => instance.BodyId = value));
                advancedIds.Add(CreateAdvancedIntegerField("碰撞体 ID", instance.ColliderId, value => instance.ColliderId = value));
            }
            advancedIds.Add(CreateDefinitionSummaryElement(instance.DefinitionId));
            advancedIds.Add(CreateTextField("制作备注", instance.Notes, value => instance.Notes = value));
            Button remove = new Button(() => RemoveSelectedRegistration(_selectedRow))
            {
                text = "移除登记"
            };
            remove.AddToClassList("mx-level-editor__danger-action");
            advancedIds.Add(remove);
            _inspector.Add(advancedIds);
        }

        private void DrawBatchInspector(LevelWorldObjectStaticColliderBatchDefinition batch)
        {
            VisualElement basicSection = CreateInspectorSection("基础属性");
            basicSection.Add(CreateToggle("启用", batch.Enabled, value => batch.Enabled = value));
            basicSection.Add(CreateInspectorDefinitionPopup(
                batch.DefinitionId,
                definition => definition.CanSpawnStaticCollider,
                value => batch.DefinitionId = value));
            basicSection.Add(CreateTextField("分组", batch.Group, value => batch.Group = value));
            basicSection.Add(CreateTextField("场景对象", batch.ContainerTransformRef.Path, value =>
            {
                batch.ContainerTransformRef.Path = value;
                batch.ContainerTransformPath = value;
            }));
            basicSection.Add(CreateSelectionButtons(batch.ContainerTransformRef));
            _inspector.Add(basicSection);

            Foldout advancedIds = CreateAdvancedIdsFoldout();
            advancedIds.Add(CreateEnumField(
                "缩放来源",
                batch.ScaleSource,
                value => batch.ScaleSource = (LevelWorldObjectScaleSource)value));
            advancedIds.Add(CreateAdvancedIntegerField("定义 ID", batch.DefinitionId, value => batch.DefinitionId = value));
            advancedIds.Add(CreateAdvancedIntegerField("起始刚体 ID", batch.BodyIdStart, value => batch.BodyIdStart = value));
            advancedIds.Add(CreateAdvancedIntegerField("碰撞体 ID", batch.ColliderId, value => batch.ColliderId = value));
            advancedIds.Add(CreateDefinitionSummaryElement(batch.DefinitionId));
            advancedIds.Add(CreateTextField("制作备注", batch.Notes, value => batch.Notes = value));
            Button remove = new Button(() => RemoveSelectedRegistration(_selectedRow))
            {
                text = "移除登记"
            };
            remove.AddToClassList("mx-level-editor__danger-action");
            advancedIds.Add(remove);
            _inspector.Add(advancedIds);
        }

        private void DrawPointInspector(LevelWorldObjectPointDefinition point)
        {
            VisualElement basicSection = CreateInspectorSection("基础属性");
            basicSection.Add(CreateToggle("启用", point.Enabled, value => point.Enabled = value));
            basicSection.Add(CreateInspectorDefinitionPopup(
                point.DefinitionId,
                definition => definition.CanSpawnPoint,
                value => point.DefinitionId = value));
            basicSection.Add(CreateTextField("分组", point.Group, value => point.Group = value));
            basicSection.Add(CreateTextField("场景对象", point.TransformRef.Path, value =>
            {
                point.TransformRef.Path = value;
                point.TransformPath = value;
            }));
            basicSection.Add(CreateSelectionButtons(point.TransformRef));
            _inspector.Add(basicSection);
            _inspector.Add(CreateQuickPlacementActions(_selectedRow));

            Foldout advancedIds = CreateAdvancedIdsFoldout();
            advancedIds.Add(CreateEnumField(
                "点位类型",
                point.PointKind,
                value => point.PointKind = (LevelWorldObjectPointKind)value));
            advancedIds.Add(CreateAdvancedIntegerField("定义 ID", point.DefinitionId, value => point.DefinitionId = value));
            advancedIds.Add(CreateAdvancedTextField("稳定 ID", point.StableId, value => point.StableId = value));
            advancedIds.Add(CreateDefinitionSummaryElement(point.DefinitionId));
            advancedIds.Add(CreateTextField("制作备注", point.Notes, value => point.Notes = value));
            Button remove = new Button(() => RemoveSelectedRegistration(_selectedRow))
            {
                text = "移除登记"
            };
            remove.AddToClassList("mx-level-editor__danger-action");
            advancedIds.Add(remove);
            _inspector.Add(advancedIds);
        }

        private void DrawRegionInspector(LevelWorldObjectRegionDefinition region)
        {
            VisualElement basicSection = CreateInspectorSection("对象设置");
            basicSection.AddToClassList(
                "mx-level-editor__region-object-settings");
            basicSection.Add(CreateToggle("启用", region.Enabled, value => region.Enabled = value));
            basicSection.Add(CreateInspectorDefinitionPopup(
                region.DefinitionId,
                definition => definition.CanSpawnRegion,
                value => region.DefinitionId = value));
            basicSection.Add(CreateTextField("分组", region.Group, value => region.Group = value));
            basicSection.Add(CreateTextField("场景对象", region.TransformRef.Path, value =>
            {
                region.TransformRef.Path = value;
                region.TransformPath = value;
            }));
            basicSection.Add(CreateRegionInspectorActions(
                region.TransformRef,
                _selectedRow));
            _inspector.Add(basicSection);
            if (region.Enabled
                && region.RegionKind == LevelWorldObjectRegionKind.TriggerRegion)
            {
                _inspector.Add(CreateGameplayTriggerLinkCard(region));
            }

            Foldout advancedIds = CreateAdvancedIdsFoldout();
            advancedIds.Add(CreateEnumField(
                "区域类型",
                region.RegionKind,
                value => region.RegionKind = (LevelWorldObjectRegionKind)value));
            advancedIds.Add(CreateEnumField(
                "缩放来源",
                region.ScaleSource,
                value => region.ScaleSource = (LevelWorldObjectScaleSource)value));
            advancedIds.Add(CreateAdvancedIntegerField("定义 ID", region.DefinitionId, value => region.DefinitionId = value));
            advancedIds.Add(CreateAdvancedTextField("稳定 ID", region.StableId, value => region.StableId = value));
            advancedIds.Add(CreateDefinitionSummaryElement(region.DefinitionId));
            advancedIds.Add(CreateTextField("制作备注", region.Notes, value => region.Notes = value));
            Button remove = new Button(() => RemoveSelectedRegistration(_selectedRow))
            {
                text = "移除登记"
            };
            remove.AddToClassList("mx-level-editor__danger-action");
            advancedIds.Add(remove);
            _inspector.Add(advancedIds);
        }

        private VisualElement CreateSelectionButtons(LevelTransformReference transformRef)
        {
            var row = new VisualElement();
            row.AddToClassList("mx-level-editor__inspector-actions");
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;
            row.style.marginBottom = 6;
            row.Add(CreateSmallButton("选择", () => SelectTransform(transformRef)));
            row.Add(CreateSmallButton("定位", () => PingTransform(transformRef)));
            row.Add(CreateSmallButton("聚焦", () => FrameTransform(transformRef)));
            return row;
        }

        private static Foldout CreateAdvancedIdsFoldout()
        {
            var foldout = new Foldout
            {
                name = "character-test-advanced-ids",
                text = "高级设置",
                value = false
            };
            foldout.AddToClassList("mx-level-editor__advanced-settings");
            foldout.style.marginTop = 4;
            foldout.style.marginBottom = 4;
            return foldout;
        }

        private static VisualElement CreateInspectorHeader(string title, bool enabled)
        {
            var row = new VisualElement
            {
                name = "character-test-inspector-header"
            };
            row.AddToClassList("mx-level-editor__inspector-header");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("mx-level-editor__inspector-title");
            row.Add(titleLabel);
            var status = new Label(enabled ? "● 有效" : "○ 已禁用");
            status.AddToClassList(enabled
                ? "mx-level-editor__inspector-status--valid"
                : "mx-level-editor__inspector-status--disabled");
            row.Add(status);
            return row;
        }

        private string ResolveDefinitionDisplayName(int definitionId)
        {
            if (ActiveDefinitionCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                return CharacterTestLevelEditorChineseText.DefinitionName(
                    definition.DefinitionId,
                    definition.DisplayName);
            }

            return "对象 " + definitionId;
        }

        private IntegerField CreateAdvancedIntegerField(string label, int value, Action<int> setter)
        {
            IntegerField field = CreateIntegerField(label, value, setter);
            field.name = "character-test-advanced-" + SanitizeElementName(label);
            return field;
        }

        private TextField CreateAdvancedTextField(string label, string value, Action<string> setter)
        {
            TextField field = CreateTextField(label, value, setter);
            field.name = "character-test-advanced-" + SanitizeElementName(label);
            return field;
        }

        private static string SanitizeElementName(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "field";

            switch (label.Trim())
            {
                case "定义 ID":
                    return "definition-id";
                case "物体 ID":
                    return "numeric-id";
                case "稳定 ID 覆盖值":
                    return "stable-id-override";
                case "稳定 ID":
                    return "stable-id";
                case "刚体 ID":
                    return "body-id";
                case "起始刚体 ID":
                    return "body-id-start";
                case "碰撞体 ID":
                    return "collider-id";
                case "缩放来源":
                    return "scale-source";
                case "点位类型":
                    return "point-kind";
                case "区域类型":
                    return "region-kind";
                default:
                    break;
            }

            return label.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static Button CreateSmallButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 4;
            button.style.height = 22;
            return button;
        }

        private static VisualElement CreateInspectorSection(string title)
        {
            var section = new VisualElement
            {
                name = "character-test-inspector-section-" + SanitizeElementName(title)
            };
            section.AddToClassList("mx-level-editor__inspector-section");
            section.style.marginTop = 8;
            section.style.marginBottom = 4;
            section.style.paddingTop = 4;
            section.style.paddingBottom = 4;
            section.Add(CreateInspectorTitle(title));
            return section;
        }

        private static Label CreateInspectorTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("mx-level-editor__inspector-section-title");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.marginBottom = 4;
            return label;
        }

        private Toggle CreateToggle(string label, bool value, Action<bool> setter)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty();
            });
            return field;
        }

        private IntegerField CreateIntegerField(string label, int value, Action<int> setter)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(Math.Max(0, evt.newValue));
                MarkDirty();
            });
            return field;
        }

        private TextField CreateTextField(string label, string value, Action<string> setter)
        {
            var field = new TextField(label) { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue ?? string.Empty);
                MarkDirty();
            });
            return field;
        }

        private PopupField<string> CreateEnumField(string label, Enum value, Action<Enum> setter)
        {
            Array rawValues = Enum.GetValues(value.GetType());
            var values = new List<Enum>(rawValues.Length);
            var labels = new List<string>(rawValues.Length);
            for (int i = 0; i < rawValues.Length; i++)
            {
                var enumValue = (Enum)rawValues.GetValue(i);
                values.Add(enumValue);
                labels.Add(CharacterTestLevelEditorChineseText.EnumValue(enumValue));
            }

            int index = Mathf.Clamp(values.IndexOf(value), 0, Math.Max(0, values.Count - 1));
            var field = new PopupField<string>(label, labels, index)
            {
                tooltip = value.GetType().Name + "；保存值仍为原枚举。"
            };
            field.RegisterValueChangedCallback(_ =>
            {
                int selected = Mathf.Clamp(labels.IndexOf(field.value), 0, Math.Max(0, values.Count - 1));
                if (values.Count > 0)
                    setter(values[selected]);
                MarkDirty();
            });
            return field;
        }

        private VisualElement CreateDefinitionSummaryElement(int definitionId)
        {
            if (!CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                return new HelpBox("未找到对象定义：" + definitionId, HelpBoxMessageType.Warning)
                {
                    name = "character-test-definition-summary"
                };
            }

            return new Label(
                "对象定义："
                + CharacterTestLevelEditorChineseText.DefinitionName(
                    definition.DefinitionId,
                    definition.DisplayName)
                + " | 稳定 ID："
                + definition.StableId
                + " | 类型："
                + CharacterTestLevelEditorChineseText.Usage(
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                        definition.DefinitionId,
                        out LevelAuthoringDefinitionSummary summary)
                        ? summary.Usage
                        : LevelAuthoringDefinitionUsage.Unknown)
                + " | 形状："
                + CharacterTestLevelEditorChineseText.Shape(definition.Shape.ShapeKind)
                + " | 材质："
                + CharacterTestLevelEditorChineseText.Material(definition.Material.Kind))
            {
                name = "character-test-definition-summary"
            };
        }

    }
}
