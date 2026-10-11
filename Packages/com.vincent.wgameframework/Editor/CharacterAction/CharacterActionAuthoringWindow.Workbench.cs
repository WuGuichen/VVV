using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private const string WorkbenchLeftWidthPreferenceKey =
            "MxFramework.CharacterAction.Workbench.LeftWidth";
        private const string WorkbenchInspectorWidthPreferenceKey =
            "MxFramework.CharacterAction.Workbench.InspectorWidth";
        private const string WorkbenchPreviewHeightPreferenceKey =
            "MxFramework.CharacterAction.Workbench.PreviewHeight";
        private const string WorkbenchProblemsHeightPreferenceKey =
            "MxFramework.CharacterAction.Workbench.ProblemsHeight";
        private const string WorkbenchProblemsExpandedPreferenceKey =
            "MxFramework.CharacterAction.Workbench.ProblemsExpanded";
        private const string WorkbenchPreviewVisiblePreferenceKey =
            "MxFramework.CharacterAction.Workbench.PreviewVisible";
        private const string RecentActionFilterLabel = "最近编辑";
        private const float WorkbenchProblemsDefaultHeight = 190f;
        private const float WorkbenchProblemsMinimumHeight = 152f;
        private const float WorkbenchProblemsHeaderHeight = 28f;
        private const float WorkbenchProblemsResizeHandleHeight = 4f;
        private const float WorkbenchProblemsBodyMaximumHeight = 360f;

        private enum AuthoringWorkspace
        {
            ActionComposition = 0,
            Mappings = 1,
            SharedResources = 2,
        }

        private enum MappingWorkspaceSection
        {
            ProfileComposition = 0,
            ActionSet = 1,
            CommandBindings = 2,
            AbilityBindings = 3,
            Reactions = 4,
            ReferenceSources = 5,
            Compatibility = 6,
        }

        private enum SharedResourceSection
        {
            AbilityEffectTimeline = 0,
        }

        private sealed class ActionNavigationEntry
        {
            public CharacterActionJsonDto Action;
            public int ModelIndex;
            public int TreeId;
            public string LibraryId = string.Empty;
            public string Category = string.Empty;
            public int ProblemCount;
            public int RecentRank = -1;
        }

        private AuthoringWorkspace _workspace = AuthoringWorkspace.ActionComposition;
        private MappingWorkspaceSection _mappingSection = MappingWorkspaceSection.ProfileComposition;
        private SharedResourceSection _sharedResourceSection = SharedResourceSection.AbilityEffectTimeline;
        private VisualElement _workbenchRoot;
        private VisualElement _actionWorkspaceRoot;
        private VisualElement _abilityWorkspaceRoot;
        private VisualElement _mappingWorkspaceRoot;
        private VisualElement _sharedWorkspaceRoot;
        private VisualElement _actionInspectorHeaderHost;
        private VisualElement _mappingMainHost;
        private VisualElement _mappingInspectorHost;
        private VisualElement _sharedMainHost;
        private VisualElement _sharedInspectorHost;
        private VisualElement _embeddedPreviewHost;
        private VisualElement _previewSurfaceHost;
        private VisualElement _damageComparisonHost;
        private bool _showDamageComparison;
        private CharacterActionPreviewViewport _previewViewport;
        private Label _previewFrameLabel;
        private Button _previewPlayPauseButton;
        private Button _previewSpeedButton;
        private Toggle _previewLoopToggle;
        private bool _previewLoopPlayback = true;
        private string[] _previewFrameLabels = Array.Empty<string>();
        private int _previewFrameLabelsDuration = -1;
        private TreeView _actionNavigationList;
        private Label _actionNavigationEmptyState;
        private CharacterActionRecentEditHistory _recentActionHistory;
        private readonly List<ActionNavigationEntry> _actionNavigationEntries =
            new List<ActionNavigationEntry>();
        private readonly List<TreeViewItemData<object>> _actionNavigationRoots =
            new List<TreeViewItemData<object>>();
        private readonly List<Button> _mappingNavigationButtons = new List<Button>();
        private readonly List<Button> _sharedNavigationButtons = new List<Button>();
        private string _actionCategoryFilter = "全部";
        private bool _syncingActionNavigationSelection;
        private bool _problemsExpanded;
        private bool _sceneEditMode;
        private bool _previewVisible = true;
        private long _workbenchPreviewRevision = 1L;
        private VisualElement _problemsBody;
        private VisualElement _problemsResizeHandle;
        private Label _problemsSummaryLabel;
        private ToolbarMenu _problemsScopeMenu;
        private ToolbarMenu _problemsSeverityMenu;
        private Button _problemsToggleButton;
        private CharacterActionAuthoringProblemsModel _problemsModel;
        private CharacterActionAuthoringProblemScope _problemsScope = CharacterActionAuthoringProblemScope.All;
        private CharacterActionAuthoringProblemSeverityFilter _problemsSeverity =
            CharacterActionAuthoringProblemSeverityFilter.All;
        private string _problemsQuery = string.Empty;
        private float _problemsRequestedHeight = WorkbenchProblemsDefaultHeight;
        private float _problemsResizeStartY;
        private float _problemsResizeStartHeight;
        private int _problemsResizePointerId = -1;
        private string _lastSaveFailureSource = string.Empty;
        private string _lastSaveFailureMessage = string.Empty;

        private void BuildWorkbenchShell(VisualElement root)
        {
            _workbenchRoot = new VisualElement { name = "character-action-workbench" };
            _workbenchRoot.AddToClassList("ca-workbench");
            root.Add(_workbenchRoot);

            _actionWorkspaceRoot = BuildActionCompositionWorkspace();
            _abilityWorkspaceRoot = BuildAbilityCompositionWorkspace();
            _mappingWorkspaceRoot = BuildMappingWorkspace();
            _sharedWorkspaceRoot = BuildSharedResourcesWorkspace();
            _workbenchRoot.Add(_actionWorkspaceRoot);
            _workbenchRoot.Add(_abilityWorkspaceRoot);
            _workbenchRoot.Add(_mappingWorkspaceRoot);
            _workbenchRoot.Add(_sharedWorkspaceRoot);
            ApplyWorkspaceVisibility();
        }

        private VisualElement BuildActionCompositionWorkspace()
        {
            var root = CreateWorkspaceRoot("character-action-composition-workspace");
            float maximumSideWidth = Mathf.Max(300f, (position.width - 420f) * 0.5f);
            float leftWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchLeftWidthPreferenceKey, 260f),
                220f,
                maximumSideWidth);
            float inspectorWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchInspectorWidthPreferenceKey, 360f),
                300f,
                maximumSideWidth);

            var outer = new TwoPaneSplitView(
                0,
                leftWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "character-action-left-split"
            };
            outer.AddToClassList("ca-workbench-split");
            root.Add(outer);

            VisualElement navigation = BuildActionNavigationPane();
            navigation.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPanePreference(WorkbenchLeftWidthPreferenceKey, evt.newRect.width, 220f));
            outer.Add(navigation);

            var centerRight = new TwoPaneSplitView(
                1,
                inspectorWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "character-action-inspector-split"
            };
            centerRight.AddToClassList("ca-workbench-split");
            outer.Add(centerRight);

            VisualElement center = BuildActionCenterPane();
            centerRight.Add(center);

            var inspector = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-action-context-inspector"
            };
            inspector.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspector.AddToClassList("ca-workbench-inspector");
            inspector.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPanePreference(WorkbenchInspectorWidthPreferenceKey, evt.newRect.width, 300f));
            _actionInspectorHeaderHost = new VisualElement();
            _actionInspectorHeaderHost.AddToClassList("ca-workbench-inspector__header");
            inspector.Add(_actionInspectorHeaderHost);
            if (_timelineInspectorHost == null)
                _timelineInspectorHost = new VisualElement();
            else
                _timelineInspectorHost.RemoveFromHierarchy();
            _timelineInspectorHost.AddToClassList("ca-workbench-inspector__timeline");
            inspector.Add(_timelineInspectorHost);
            centerRight.Add(inspector);

            return root;
        }

        private VisualElement BuildActionNavigationPane()
        {
            var pane = new VisualElement { name = "character-action-navigation" };
            pane.AddToClassList("ca-workbench-navigation");

            var title = new Label("动作");
            title.AddToClassList("ca-workbench-pane-title");
            pane.Add(title);

            var search = new ToolbarSearchField { value = _actionListFilter ?? string.Empty };
            search.name = "character-action-search";
            search.RegisterValueChangedCallback(evt =>
            {
                _actionListFilter = evt.newValue ?? string.Empty;
                SaveSessionState();
                RebuildWorkbenchActionNavigation();
            });
            pane.Add(search);

            var filters = new VisualElement();
            filters.AddToClassList("ca-workbench-filter-row");
            AddActionFilterButton(filters, "全部");
            AddActionFilterButton(filters, RecentActionFilterLabel, "character-action-filter-recent");
            AddActionFilterButton(filters, "战斗");
            AddActionFilterButton(filters, "移动");
            AddActionFilterButton(filters, "有问题");
            pane.Add(filters);

            _actionNavigationList = new TreeView
            {
                name = "character-action-tree",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 46f,
            };
            _actionNavigationList.makeItem = MakeActionNavigationRow;
            _actionNavigationList.bindItem = BindActionNavigationRow;
            _actionNavigationList.AddToClassList("ca-workbench-action-list");
            _actionNavigationList.selectionChanged += OnActionNavigationSelectionChanged;
            pane.Add(_actionNavigationList);

            _actionNavigationEmptyState = new Label("还没有最近编辑的动作。修改动作后会显示在这里。")
            {
                name = "character-action-recent-empty",
            };
            _actionNavigationEmptyState.AddToClassList("ca-workbench-action-empty");
            _actionNavigationEmptyState.style.display = DisplayStyle.None;
            pane.Add(_actionNavigationEmptyState);

            var createRow = new VisualElement();
            createRow.AddToClassList("ca-workbench-create-row");
            var createButton = new Button(() => AddAction(
                CharacterActionAuthoringModel.CreateBlankAction(
                    0,
                    "New Action")))
            {
                text = "＋ 新建动作"
            };
            createButton.AddToClassList("ca-workbench-primary-button");
            createRow.Add(createButton);

            var templateMenu = new ToolbarMenu { text = "从模板创建" };
            templateMenu.menu.AppendAction(
                "跳跃",
                _ => AddAction(CharacterActionAuthoringModel.CreateJumpAction()),
                DropdownMenuAction.AlwaysEnabled);
            templateMenu.menu.AppendAction(
                "轻攻击",
                _ => AddAction(CharacterActionAuthoringModel.CreateLightAttackAction()),
                DropdownMenuAction.AlwaysEnabled);
            createRow.Add(templateMenu);
            pane.Add(createRow);
            return pane;
        }

        private void AddActionFilterButton(
            VisualElement parent,
            string label,
            string name = null)
        {
            Button button = null;
            button = new Button(() =>
            {
                _actionCategoryFilter = label;
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent[i] is Button sibling)
                    {
                        sibling.EnableInClassList(
                            "ca-workbench-filter-button--selected",
                            ReferenceEquals(sibling, button));
                    }
                }
                SaveSessionState();
                RebuildWorkbenchActionNavigation();
            })
            {
                text = label,
                name = name,
            };
            button.AddToClassList("ca-workbench-filter-button");
            button.EnableInClassList(
                "ca-workbench-filter-button--selected",
                string.Equals(_actionCategoryFilter, label, StringComparison.Ordinal));
            parent.Add(button);
        }

        private VisualElement BuildActionCenterPane()
        {
            var center = new VisualElement { name = "character-action-center" };
            center.AddToClassList("ca-workbench-center");
            float defaultPreviewHeight = Mathf.Max(220f, (position.height - 130f) * 0.45f);
            float previewHeight = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchPreviewHeightPreferenceKey, defaultPreviewHeight),
                220f,
                Mathf.Max(220f, position.height - 360f));
            var split = new TwoPaneSplitView(
                0,
                previewHeight,
                TwoPaneSplitViewOrientation.Vertical)
            {
                name = "character-action-preview-timeline-split"
            };
            split.AddToClassList("ca-workbench-split");
            center.Add(split);

            _embeddedPreviewHost = BuildEmbeddedPreviewPanel();
            _embeddedPreviewHost.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPanePreference(WorkbenchPreviewHeightPreferenceKey, evt.newRect.height, 220f));
            split.Add(_embeddedPreviewHost);

            _tracksTabRoot = new VisualElement { name = "character-action-timeline-host" };
            _tracksTabRoot.AddToClassList("ca-workbench-timeline");
            split.Add(_tracksTabRoot);
            EnsureTimelineView();
            return center;
        }

        private VisualElement BuildEmbeddedPreviewPanel()
        {
            var panel = new VisualElement { name = "character-action-preview" };
            panel.AddToClassList("ca-workbench-preview");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ca-workbench-preview-toolbar");
            toolbar.Add(new Button(() => SeekPreviewFrame(0)) { text = "|◀" });
            toolbar.Add(new Button(() => SeekPreviewFrame((_timelinePlayback?.CurrentFrame ?? 0) - 1)) { text = "◀" });
            _previewPlayPauseButton = new Button(ToggleWorkbenchPreviewPlayback);
            toolbar.Add(_previewPlayPauseButton);
            toolbar.Add(new Button(() => SeekPreviewFrame((_timelinePlayback?.CurrentFrame ?? 0) + 1)) { text = "▶" });
            toolbar.Add(new Button(() => SeekPreviewFrame(Math.Max(0, (_timelinePlayback?.DurationFrames ?? 1) - 1)))
            {
                text = "▶|"
            });
            _previewSpeedButton = new Button(CycleWorkbenchPreviewSpeed);
            toolbar.Add(_previewSpeedButton);
            _previewLoopToggle = new Toggle("循环") { value = _previewLoopPlayback };
            _previewLoopToggle.RegisterValueChangedCallback(evt =>
            {
                _previewLoopPlayback = evt.newValue;
                EnsureTimelinePlayback();
                _timelinePlayback.LoopPlayback = evt.newValue;
            });
            toolbar.Add(_previewLoopToggle);
            _previewVisible = EditorPrefs.GetBool(WorkbenchPreviewVisiblePreferenceKey, true);
            var previewVisible = new Toggle("3D 预览") { value = _previewVisible };
            previewVisible.RegisterValueChangedCallback(evt => SetWorkbenchPreviewVisible(evt.newValue));
            toolbar.Add(previewVisible);
            toolbar.Add(new Button(ToggleDamageComparison) { text = "伤害事件", name = "character-action-damage-comparison-toggle" });
            var spacer = new VisualElement();
            spacer.AddToClassList("ca-toolbar-spacer");
            toolbar.Add(spacer);
            _previewFrameLabel = new Label("0 / 0 帧");
            _previewFrameLabel.AddToClassList("ca-workbench-preview-frame");
            toolbar.Add(_previewFrameLabel);
            RefreshWorkbenchTransportState();
            panel.Add(toolbar);

            _previewSurfaceHost = new VisualElement { name = "character-action-preview-surface" };
            _previewSurfaceHost.AddToClassList("ca-workbench-preview-surface");
            DisposeWorkbenchPreviewViewport();
            _previewViewport = new CharacterActionPreviewViewport();
            _previewSurfaceHost.Add(_previewViewport);
            _previewSurfaceHost.style.display = _previewVisible && !_showDamageComparison ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Add(_previewSurfaceHost);
            _damageComparisonHost = new ScrollView { name = "character-action-damage-comparison" };
            _damageComparisonHost.AddToClassList("ca-damage-comparison");
            _damageComparisonHost.style.display = DisplayStyle.None;
            panel.Add(_damageComparisonHost);
            return panel;
        }

        private void DisposeWorkbenchPreviewViewport()
        {
            DisposeAbilityPreviewViewport();
            if (_previewViewport == null)
                return;

            if (ReferenceEquals(_timelinePreviewSession, _previewViewport.Session))
                _timelinePreviewSession = null;
            _previewViewport.Dispose();
            _previewViewport = null;
        }

        private void SetWorkbenchPreviewVisible(bool visible)
        {
            _previewVisible = visible;
            EditorPrefs.SetBool(WorkbenchPreviewVisiblePreferenceKey, visible);
            if (_previewSurfaceHost != null)
                _previewSurfaceHost.style.display = visible && !_showDamageComparison ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                _previewViewport?.ClearPreview();
            else
                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
        }

        private void TouchWorkbenchPreviewRevision()
        {
            unchecked
            {
                _workbenchPreviewRevision++;
                if (_workbenchPreviewRevision == 0L)
                    _workbenchPreviewRevision = 1L;
            }
        }

        private void ToggleWorkbenchPreviewPlayback()
        {
            EnsureTimelinePlayback();
            _timelinePlayback.TogglePlay();
            _timelineView?.RefreshCommandState();
            RefreshWorkbenchTransportState();
        }

        private void CycleWorkbenchPreviewSpeed()
        {
            EnsureTimelinePlayback();
            _timelinePlayback.CyclePlaybackSpeed();
            _timelineView?.RefreshCommandState();
            RefreshWorkbenchTransportState();
        }

        private void RefreshWorkbenchTransportState()
        {
            if (_previewPlayPauseButton != null)
            {
                bool playing = _timelinePlayback?.IsPlaying == true;
                _previewPlayPauseButton.text = playing ? "Ⅱ" : "▶";
                _previewPlayPauseButton.tooltip = playing ? "暂停预览" : "播放预览";
            }

            if (_previewSpeedButton != null)
            {
                float speed = _timelinePlayback?.PlaybackSpeed ?? 1f;
                _previewSpeedButton.text = speed == 0.5f ? "0.5x"
                    : speed >= 4f ? "4x"
                    : speed >= 2f ? "2x"
                    : "1x";
            }

            if (_previewLoopToggle != null)
                _previewLoopToggle.SetValueWithoutNotify(_previewLoopPlayback);
            RefreshAbilityTransportState();
        }

        private void SeekPreviewFrame(int frame)
        {
            EnsureTimelinePlayback();
            _timelinePlayback.Seek(frame);
            UpdateWorkbenchPreviewFrameLabel();
        }

        private VisualElement BuildMappingWorkspace()
        {
            VisualElement navigation;
            VisualElement main;
            VisualElement inspector;
            VisualElement root = BuildThreePaneWorkspace(
                "character-action-mapping-workspace",
                out navigation,
                out main,
                out inspector);
            string[] labels =
            {
                "Profile 组成",
                "ActionSet 设置",
                "指令绑定",
                "能力绑定",
                "受击规则",
                "引用数据源",
                "兼容数据",
            };
            _mappingNavigationButtons.Clear();
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = new Button(() => SetMappingSection((MappingWorkspaceSection)index))
                {
                    text = labels[i],
                    userData = (MappingWorkspaceSection)index,
                };
                button.AddToClassList("ca-workbench-section-navigation__button");
                _mappingNavigationButtons.Add(button);
                navigation.Add(button);
            }
            _mappingMainHost = main;
            _mappingInspectorHost = inspector;
            RefreshMappingNavigationSelection();
            return root;
        }

        private VisualElement BuildSharedResourcesWorkspace()
        {
            VisualElement navigation;
            VisualElement main;
            VisualElement inspector;
            VisualElement root = BuildThreePaneWorkspace(
                "character-action-shared-workspace",
                out navigation,
                out main,
                out inspector);
            _sharedNavigationButtons.Clear();
            var abilityTimeline = new Button(() => SetSharedResourceSection(SharedResourceSection.AbilityEffectTimeline))
            {
                text = "Ability Effect Timeline",
                userData = SharedResourceSection.AbilityEffectTimeline,
            };
            abilityTimeline.AddToClassList("ca-workbench-section-navigation__button");
            _sharedNavigationButtons.Add(abilityTimeline);
            navigation.Add(abilityTimeline);
            _sharedMainHost = main;
            _sharedInspectorHost = inspector;
            RefreshSharedNavigationSelection();
            return root;
        }

        private void RefreshMappingNavigationSelection()
        {
            for (int i = 0; i < _mappingNavigationButtons.Count; i++)
            {
                Button button = _mappingNavigationButtons[i];
                button.EnableInClassList(
                    "ca-workbench-section-navigation__button--selected",
                    button.userData is MappingWorkspaceSection section && section == _mappingSection);
            }
        }

        private void RefreshSharedNavigationSelection()
        {
            for (int i = 0; i < _sharedNavigationButtons.Count; i++)
            {
                Button button = _sharedNavigationButtons[i];
                button.EnableInClassList(
                    "ca-workbench-section-navigation__button--selected",
                    button.userData is SharedResourceSection section && section == _sharedResourceSection);
            }
        }

        private VisualElement BuildThreePaneWorkspace(
            string name,
            out VisualElement navigation,
            out VisualElement main,
            out VisualElement inspector)
        {
            var root = CreateWorkspaceRoot(name);
            float maximumSideWidth = Mathf.Max(300f, (position.width - 420f) * 0.5f);
            float leftWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchLeftWidthPreferenceKey, 260f),
                220f,
                maximumSideWidth);
            float inspectorWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchInspectorWidthPreferenceKey, 360f),
                300f,
                maximumSideWidth);
            var outer = new TwoPaneSplitView(0, leftWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = name + "-left-split"
            };
            outer.AddToClassList("ca-workbench-split");
            root.Add(outer);
            navigation = new ScrollView(ScrollViewMode.Vertical);
            navigation.AddToClassList("ca-workbench-section-navigation");
            navigation.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPanePreference(WorkbenchLeftWidthPreferenceKey, evt.newRect.width, 220f));
            outer.Add(navigation);
            var right = new TwoPaneSplitView(1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = name + "-inspector-split"
            };
            right.AddToClassList("ca-workbench-split");
            outer.Add(right);
            main = new ScrollView(ScrollViewMode.Vertical);
            ((ScrollView)main).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            main.AddToClassList("ca-workbench-main-scroll");
            right.Add(main);
            inspector = new ScrollView(ScrollViewMode.Vertical);
            ((ScrollView)inspector).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspector.AddToClassList("ca-workbench-inspector");
            inspector.RegisterCallback<GeometryChangedEvent>(evt =>
                PersistPanePreference(WorkbenchInspectorWidthPreferenceKey, evt.newRect.width, 300f));
            right.Add(inspector);
            return root;
        }

        private static VisualElement CreateWorkspaceRoot(string name)
        {
            var root = new VisualElement { name = name };
            root.AddToClassList("ca-workbench-workspace");
            return root;
        }

        private VisualElement MakeActionNavigationRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ca-workbench-action-row");
            var icon = new Label("◆") { name = "icon" };
            icon.AddToClassList("ca-workbench-action-row__icon");
            row.Add(icon);
            var labels = new VisualElement();
            labels.AddToClassList("ca-workbench-action-row__labels");
            var title = new Label { name = "title" };
            title.AddToClassList("ca-workbench-action-row__title");
            labels.Add(title);
            var subtitle = new Label { name = "subtitle" };
            subtitle.AddToClassList("ca-workbench-action-row__subtitle");
            labels.Add(subtitle);
            row.Add(labels);
            var duration = new Label { name = "duration" };
            duration.AddToClassList("ca-workbench-action-row__duration");
            row.Add(duration);
            var problems = new Label { name = "problems" };
            problems.AddToClassList("ca-workbench-action-row__problems");
            row.Add(problems);
            return row;
        }

        private void BindActionNavigationRow(VisualElement element, int index)
        {
            object item = _actionNavigationList.GetItemDataForIndex<object>(index);
            ActionNavigationEntry entry = item as ActionNavigationEntry;
            if (entry == null)
            {
                string category = item as string ?? "未分类";
                element.Q<Label>("icon").text = "▾";
                element.Q<Label>("title").text = category;
                element.Q<Label>("subtitle").text = string.Empty;
                element.Q<Label>("duration").text = string.Empty;
                element.Q<Label>("problems").style.display = DisplayStyle.None;
                element.tooltip = "按 Category 分组";
                return;
            }
            CharacterActionJsonDto action = entry.Action;
            element.Q<Label>("icon").text = "◆";
            element.Q<Label>("title").text = string.IsNullOrWhiteSpace(action.DisplayName)
                ? "动作 #" + action.ActionId.ToString(CultureInfo.InvariantCulture)
                : action.DisplayName;
            element.Q<Label>("subtitle").text =
                "#" + action.ActionId.ToString(CultureInfo.InvariantCulture) + "  ·  " + entry.LibraryId;
            element.Q<Label>("duration").text = (action.DurationFrames ?? DefaultDurationFrames) + " 帧";
            Label problemLabel = element.Q<Label>("problems");
            problemLabel.text = entry.ProblemCount > 0 ? entry.ProblemCount.ToString() : string.Empty;
            problemLabel.style.display = entry.ProblemCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            element.tooltip = entry.Category;
        }

        private void OnActionNavigationSelectionChanged(IEnumerable selection)
        {
            if (_syncingActionNavigationSelection)
                return;
            foreach (object value in selection)
            {
                if (value is ActionNavigationEntry entry)
                {
                    SelectActionAndRefresh(entry.Action?.ActionId ?? 0, entry.ModelIndex);
                    break;
                }
            }
        }

        private void SelectActionAndRefresh(int actionId, int modelIndex)
        {
            if (actionId <= 0 || _model?.SelectedAction?.ActionId == actionId)
                return;
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
                if (_documentSession == null)
                {
                    SelectActionWithoutDocumentEdit(modelIndex);
                    return;
                }
                if (!SelectDocumentAction(actionId))
                    return;
                RecordCurrentActionRecentActivity();
                _pendingTimelineSelectedItemId = string.Empty;
                _timelineView?.SelectItem(string.Empty, notify: false);
                RebuildTracks(rebuildInspector: false);
                RebuildWorkbenchActionNavigation();
                RebuildWorkbenchActionInspector();
                UpdateWorkbenchPreviewFrameLabel();
                RefreshActionSelectionChrome();
                RefreshFooter();
                SaveSessionState();
            }
        }

        private void SelectActionWithoutDocumentEdit(int index)
        {
            if (_model == null || index < 0 || index >= _model.Actions.Count)
                return;
            if (_model.SelectedActionIndex == index)
                return;
            _timelinePlayback?.Pause();
            _model.SelectedActionIndex = index;
            RecordCurrentActionRecentActivity();
            _pendingTimelineSelectedItemId = string.Empty;
            SaveSessionState();
            if (IsRecentActionFilterActive())
                RebuildWorkbenchActionNavigation();
            _timelineView?.SelectItem(string.Empty, notify: false);
            RebuildTracks(rebuildInspector: false);
            RebuildWorkbenchActionInspector();
            UpdateWorkbenchPreviewFrameLabel();
            RefreshActionSelectionChrome();
            RefreshFooter();
        }

        private void RebuildWorkbenchActionNavigation()
        {
            if (_actionNavigationList == null || _model == null)
                return;
            EnsureRecentActionHistory();
            PruneUnavailableRecentActions();
            _actionNavigationEntries.Clear();
            string search = (_actionListFilter ?? string.Empty).Trim();
            if (_documentSession != null)
            {
                for (int i = 0; i < _documentSession.ProfileActions.Count; i++)
                {
                    CharacterActionAuthoringActionReference reference = _documentSession.ProfileActions[i];
                    AddActionNavigationEntry(
                        reference.Action,
                        reference.LibraryId,
                        IndexOfModelAction(reference.Action?.ActionId ?? 0),
                        search);
                }
            }
            else
            {
                for (int i = 0; i < _model.Actions.Count; i++)
                {
                    AddActionNavigationEntry(
                        _model.Actions[i],
                        ShortPath(_actionLibrarySourceDirectory),
                        i,
                        search);
                }
            }
            bool recentOnly = IsRecentActionFilterActive();
            _actionNavigationEntries.Sort((left, right) =>
            {
                if (recentOnly)
                    return left.RecentRank.CompareTo(right.RecentRank);
                int category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
                if (category != 0)
                    return category;
                return string.Compare(
                    left.Action?.DisplayName ?? string.Empty,
                    right.Action?.DisplayName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });
            _actionNavigationRoots.Clear();
            int nextId = 1;
            if (recentOnly)
            {
                for (int i = 0; i < _actionNavigationEntries.Count; i++)
                {
                    ActionNavigationEntry entry = _actionNavigationEntries[i];
                    entry.TreeId = nextId++;
                    _actionNavigationRoots.Add(new TreeViewItemData<object>(entry.TreeId, entry));
                }
            }
            else
            {
                int offset = 0;
                while (offset < _actionNavigationEntries.Count)
                {
                    string category = string.IsNullOrWhiteSpace(_actionNavigationEntries[offset].Category)
                        ? "未分类"
                        : _actionNavigationEntries[offset].Category;
                    var children = new List<TreeViewItemData<object>>();
                    while (offset < _actionNavigationEntries.Count)
                    {
                        ActionNavigationEntry entry = _actionNavigationEntries[offset];
                        string entryCategory = string.IsNullOrWhiteSpace(entry.Category) ? "未分类" : entry.Category;
                        if (!string.Equals(entryCategory, category, StringComparison.OrdinalIgnoreCase))
                            break;
                        entry.TreeId = nextId++;
                        children.Add(new TreeViewItemData<object>(entry.TreeId, entry));
                        offset++;
                    }
                    _actionNavigationRoots.Add(new TreeViewItemData<object>(nextId++, category, children));
                }
            }
            _syncingActionNavigationSelection = true;
            try
            {
                _actionNavigationList.SetRootItems(_actionNavigationRoots);
                if (!recentOnly)
                    _actionNavigationList.ExpandAll();
                _actionNavigationList.Rebuild();
                int selected = IndexOfNavigationEntry(
                    _documentSession?.Selection.ActionId ?? _model.SelectedAction?.ActionId ?? 0);
                if (selected >= 0)
                    _actionNavigationList.SetSelectionById(_actionNavigationEntries[selected].TreeId);
            }
            finally
            {
                _syncingActionNavigationSelection = false;
            }
            bool showRecentEmpty = recentOnly && _actionNavigationEntries.Count == 0;
            _actionNavigationList.style.display = showRecentEmpty
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (_actionNavigationEmptyState != null)
            {
                _actionNavigationEmptyState.text = string.IsNullOrWhiteSpace(search)
                    ? "还没有最近编辑的动作。修改动作后会显示在这里。"
                    : "没有符合搜索条件的最近编辑动作。";
                _actionNavigationEmptyState.style.display = showRecentEmpty
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private void AddActionNavigationEntry(
            CharacterActionJsonDto action,
            string libraryId,
            int modelIndex,
            string search)
        {
            if (action == null || !MatchesActionNavigationFilter(action, search))
                return;
            int problems = CountProblemsForAction(action.ActionId);
            int recentRank = GetRecentActionRank(libraryId, action.ActionId);
            if (IsRecentActionFilterActive() && recentRank < 0)
                return;
            if (string.Equals(_actionCategoryFilter, "有问题", StringComparison.Ordinal) && problems == 0)
                return;
            if (string.Equals(_actionCategoryFilter, "战斗", StringComparison.Ordinal)
                && !IsCombatActionCategory(action.Category))
                return;
            if (string.Equals(_actionCategoryFilter, "移动", StringComparison.Ordinal)
                && !IsMovementActionCategory(action.Category))
                return;
            _actionNavigationEntries.Add(new ActionNavigationEntry
            {
                Action = action,
                ModelIndex = modelIndex,
                LibraryId = libraryId ?? string.Empty,
                Category = action.Category ?? string.Empty,
                ProblemCount = problems,
                RecentRank = recentRank,
            });
        }

        private static bool IsCombatActionCategory(string category)
        {
            category = category ?? string.Empty;
            return string.Equals(category, CharacterActionCategory.BasicAttack.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CharacterActionCategory.Skill.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CharacterActionCategory.Guard.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CharacterActionCategory.Reaction.ToString(), StringComparison.OrdinalIgnoreCase)
                || category.IndexOf("combat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMovementActionCategory(string category)
        {
            category = category ?? string.Empty;
            return string.Equals(category, CharacterActionCategory.Movement.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CharacterActionCategory.Jump.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CharacterActionCategory.Dodge.ToString(), StringComparison.OrdinalIgnoreCase)
                || category.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("locomotion", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int IndexOfModelAction(int actionId)
        {
            if (_model == null || actionId <= 0)
                return -1;
            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (_model.Actions[i]?.ActionId == actionId)
                    return i;
            }
            return -1;
        }

        private bool MatchesActionNavigationFilter(CharacterActionJsonDto action, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
            if (ActionMatchesFilter(action, filter))
                return true;
            if ((action.Category ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string[] tags = action.Tags ?? Array.Empty<string>();
            for (int i = 0; i < tags.Length; i++)
            {
                if ((tags[i] ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            if (_documentSession != null)
            {
                string[] actionSetIds = _documentSession.CurrentProfile?.ActionSetIds ?? Array.Empty<string>();
                for (int bundleIndex = 0; bundleIndex < _documentSession.ActionSetBundles.Count; bundleIndex++)
                {
                    CharacterActionSetJsonDto[] sets =
                        _documentSession.ActionSetBundles[bundleIndex].ActionSetsDocument.ActionSets
                        ?? Array.Empty<CharacterActionSetJsonDto>();
                    for (int setIndex = 0; setIndex < sets.Length; setIndex++)
                    {
                        CharacterActionSetJsonDto set = sets[setIndex];
                        if (set == null || !ContainsId(actionSetIds, set.StableId))
                            continue;
                        if (ActionSetBindingsMatchFilter(set, action.ActionId, filter))
                            return true;
                    }
                }
                return false;
            }

            return ActionSetBindingsMatchFilter(_model.ActionSet, action.ActionId, filter);
        }

        private static bool ActionSetBindingsMatchFilter(
            CharacterActionSetJsonDto actionSet,
            int actionId,
            string filter)
        {
            CharacterActionBindingJsonDto[] command = actionSet?.CommandBindings
                ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < command.Length; i++)
            {
                if (command[i]?.ActionId == actionId
                    && CharacterActionAuthoringIds.Label(command[i]?.IntentId ?? 0, 404)
                        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            CharacterAbilityActionBindingJsonDto[] ability = actionSet?.AbilityBindings
                ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < ability.Length; i++)
            {
                if (ability[i]?.ActionId == actionId
                    && ability[i].AbilityId.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private int IndexOfNavigationEntry(int actionId)
        {
            for (int i = 0; i < _actionNavigationEntries.Count; i++)
            {
                if (_actionNavigationEntries[i].Action?.ActionId == actionId)
                    return i;
            }
            return -1;
        }

        private int CountProblemsForAction(int actionId)
        {
            if (_validation == null || actionId <= 0)
                return 0;
            string idText = actionId.ToString(CultureInfo.InvariantCulture);
            int count = 0;
            CharacterActionValidationDiagnostic[] local =
                _validation.LocalValidationReport?.Diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < local.Length; i++)
            {
                if ((local[i].StablePath ?? string.Empty).IndexOf(idText, StringComparison.Ordinal) >= 0)
                    count++;
            }
            CharacterActionValidationDiagnostic[] cross =
                _validation.ValidationReport?.Diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < cross.Length; i++)
            {
                if ((cross[i].StablePath ?? string.Empty).IndexOf(idText, StringComparison.Ordinal) >= 0)
                    count++;
            }
            return count;
        }

        private void RebuildWorkbenchActionInspector()
        {
            if (_actionInspectorHeaderHost == null)
                return;
            using IDisposable sourceKeyReadScope = CharacterContentIdentityAuthoringCatalog.BeginSourceKeyReadScope();
            _actionInspectorHeaderHost.Clear();
            CharacterActionJsonDto action = _model?.SelectedAction;
            if (action == null)
            {
                _actionInspectorHeaderHost.style.display = DisplayStyle.Flex;
                if (_timelineInspectorHost != null)
                {
                    _timelineInspectorHost.Clear();
                    _timelineInspectorHost.style.display = DisplayStyle.None;
                }
                _actionInspectorHeaderHost.Add(new HelpBox("请从左侧选择动作。", HelpBoxMessageType.Info));
                return;
            }

            ITimelineItemAdapter selectedItem = string.IsNullOrWhiteSpace(_timelineView?.SelectedItemId)
                ? null
                : _timelineView.Snapshot?.FindItem(_timelineView.SelectedItemId);
            if (selectedItem != null)
            {
                _actionInspectorHeaderHost.style.display = DisplayStyle.None;
                if (_timelineInspectorHost != null)
                    _timelineInspectorHost.style.display = DisplayStyle.Flex;
                RebuildTimelineInspector();
                return;
            }

            _actionInspectorHeaderHost.style.display = DisplayStyle.Flex;
            if (_timelineInspectorHost != null)
            {
                _timelineInspectorHost.Clear();
                _timelineInspectorHost.style.display = DisplayStyle.None;
            }
            VisualElement heading = AddCard(_actionInspectorHeaderHost, "属性");
            var title = new Label(string.IsNullOrWhiteSpace(action.DisplayName)
                ? "动作 #" + action.ActionId.ToString(CultureInfo.InvariantCulture)
                : action.DisplayName);
            title.AddToClassList("ca-workbench-inspector-title");
            heading.Add(title);
            AddTextField(heading, "显示名称", action.DisplayName, value => action.DisplayName = value);
            AddIntegerField(heading, "时长（帧）", action.DurationFrames ?? DefaultDurationFrames, value =>
            {
                action.DurationFrames = Math.Max(1, value);
                CharacterActionPhaseTimelineUtility.NormalizePhases(action, action.DurationFrames.Value);
            });
            AddEnumPopup<CharacterActionCategory>(heading, "分类", action.Category, value => action.Category = value);
            AddActionRateControls(heading, action);
            AddTextField(heading, "Tags", string.Join(",", action.Tags ?? Array.Empty<string>()), value =>
                action.Tags = SplitCsv(value));
            AddWorkbenchActionCommands(heading);
            AddActionBindingSummary(_actionInspectorHeaderHost, action);
            VisualElement rules = AddCard(_actionInspectorHeaderHost, "转换与取消规则");
            rules.Add(new Label("切换窗口: " + (action.CancelRules?.Length ?? 0)));
            rules.Add(new Label("中断规则: " + (action.InterruptRules?.Length ?? 0)));
            rules.Add(new Label("时间轴轨道中的切换窗口为当前编辑权威；选择时间轴项可编辑具体规则。")
            {
                style = { whiteSpace = WhiteSpace.Normal },
            });
            Foldout advanced = CreateSectionFoldout(
                heading,
                GetTracksFoldoutKey("action-advanced"),
                "高级",
                defaultExpanded: false);
            AddReadOnlyProfileField(
                advanced,
                "动作 ID",
                "#" + action.ActionId.ToString(CultureInfo.InvariantCulture));
            AddEnumPopup<CharacterActionTimelineAuthority>(
                advanced,
                "Timeline Authority",
                action.TimelineAuthority,
                value => action.TimelineAuthority = value);
            AddIntegerField(advanced, "Priority", action.Priority, value => action.Priority = value);
        }

        private void AddActionBindingSummary(VisualElement parent, CharacterActionJsonDto action)
        {
            VisualElement card = AddCard(parent, "绑定摘要");
            CharacterActionSetJsonDto set = _model?.ActionSet;
            int actionId = action?.ActionId ?? 0;
            int commandCount = 0;
            int abilityCount = 0;
            CharacterActionBindingJsonDto[] commands =
                set?.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < commands.Length; i++)
            {
                CharacterActionBindingJsonDto binding = commands[i];
                if (binding == null || binding.ActionId != actionId)
                    continue;
                commandCount++;
                int originalIndex = i;
                card.Add(new Button(() => OpenCommandBindingFromActionInspector(originalIndex))
                {
                    text = "Intent: " + CharacterActionAuthoringIds.Label(binding.IntentId, 404)
                        + "  ·  P" + binding.Priority,
                });
            }
            CharacterAbilityActionBindingJsonDto[] abilities =
                set?.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < abilities.Length; i++)
            {
                CharacterAbilityActionBindingJsonDto binding = abilities[i];
                if (binding == null || binding.ActionId != actionId)
                    continue;
                abilityCount++;
                int originalIndex = i;
                card.Add(new Button(() => OpenAbilityBindingFromActionInspector(originalIndex))
                {
                    text = "Ability: " + binding.AbilityId,
                });
            }
            if (commandCount == 0 && abilityCount == 0)
                card.Add(new Label("当前 ActionSet 没有映射到此动作。"));
            card.Add(new Label("指令 " + commandCount + " · 能力 " + abilityCount));
        }

        private void OpenCommandBindingFromActionInspector(int originalIndex)
        {
            _selectedCommandBindingOriginalIndex = originalIndex;
            SetWorkspace(AuthoringWorkspace.Mappings);
            SetMappingSection(MappingWorkspaceSection.CommandBindings);
        }

        private void OpenAbilityBindingFromActionInspector(int originalIndex)
        {
            _selectedAbilityBindingOriginalIndex = originalIndex;
            SetWorkspace(AuthoringWorkspace.Mappings);
            SetMappingSection(MappingWorkspaceSection.AbilityBindings);
        }

        private void SetWorkspace(AuthoringWorkspace workspace)
        {
            if (_workspace == workspace)
            {
                NormalizeEditorModeForWorkspace();
                if (workspace == AuthoringWorkspace.Mappings)
                    ApplyLegacyTabForMappingSection();
                RefreshCurrentWorkbenchWorkspace();
                RefreshChrome();
                return;
            }
            bool leavingAbilityTimeline = _workspace == AuthoringWorkspace.SharedResources
                && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline
                && workspace != AuthoringWorkspace.SharedResources;
            if (leavingAbilityTimeline)
            {
                _timelinePlayback?.Pause();
                _abilityEffectTimelineView?.SetPlaybackController(null);
            }
            if (_sceneEditMode
                && (workspace != AuthoringWorkspace.ActionComposition
                    || _editorMode == AuthoringEditorMode.AbilityEffectTimeline))
                ExitSceneEditMode(restoreEmbeddedPreview: true);
            _workspace = workspace;
            NormalizeEditorModeForWorkspace();
            if (workspace == AuthoringWorkspace.ActionComposition)
                _tab = AuthoringTab.Tracks;
            else if (workspace == AuthoringWorkspace.Mappings)
                ApplyLegacyTabForMappingSection();
            else
                _tab = AuthoringTab.Tracks;
            ApplyWorkspaceVisibility();
            RefreshChrome();
            RefreshCurrentWorkbenchWorkspace();
            SaveSessionState();
        }

        private void ToggleSceneEditMode()
        {
            if (_sceneEditMode)
                ExitSceneEditMode(restoreEmbeddedPreview: true);
            else
                EnterSceneEditMode();
            RefreshChrome();
        }

        private void EnterSceneEditMode()
        {
            if (_sceneEditMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _timelinePlayback?.Pause();
            DisposeTimelinePreviewSession();
            CharacterActionPreviewViewport activeViewport = _editorMode == AuthoringEditorMode.AbilityEffectTimeline
                ? _abilityPreviewViewport
                : _previewViewport;
            activeViewport?.SetSurfaceMode(CharacterActionPreviewSurfaceMode.SceneView);
            _sceneEditMode = true;
            EnsureTimelinePreviewSession();
            UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            SceneView.lastActiveSceneView?.Focus();
            SceneView.RepaintAll();
        }

        private void ExitSceneEditMode(bool restoreEmbeddedPreview)
        {
            if (!_sceneEditMode)
                return;

            DisposeTimelinePreviewSession();
            _sceneEditMode = false;
            CharacterActionPreviewViewport activeViewport = _editorMode == AuthoringEditorMode.AbilityEffectTimeline
                ? _abilityPreviewViewport
                : _previewViewport;
            if (restoreEmbeddedPreview && activeViewport != null)
            {
                activeViewport.SetSurfaceMode(CharacterActionPreviewSurfaceMode.Embedded);
                EnsureTimelinePreviewSession();
                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            }
            SceneView.RepaintAll();
        }

        private void SetMappingSection(MappingWorkspaceSection section)
        {
            _mappingSection = section;
            RefreshMappingNavigationSelection();
            ApplyLegacyTabForMappingSection();
            RebuildMappingWorkspaceContent();
            SaveSessionState();
        }

        private void ApplyLegacyTabForMappingSection()
        {
            switch (_mappingSection)
            {
                case MappingWorkspaceSection.ProfileComposition:
                    _tab = AuthoringTab.Profile;
                    break;
                case MappingWorkspaceSection.ReferenceSources:
                    _tab = AuthoringTab.References;
                    break;
                default:
                    _tab = AuthoringTab.ActionSet;
                    break;
            }
        }

        private void SetSharedResourceSection(SharedResourceSection section)
        {
            if (_sceneEditMode && section != _sharedResourceSection)
                ExitSceneEditMode(restoreEmbeddedPreview: true);
            if (section != SharedResourceSection.AbilityEffectTimeline)
            {
                _timelinePlayback?.Pause();
                _abilityEffectTimelineView?.SetPlaybackController(null);
            }
            else
            {
                DisposeTimelinePreviewSession();
                _timelineView?.SetPlaybackController(null);
            }
            _sharedResourceSection = section;
            RefreshSharedNavigationSelection();
            NormalizeEditorModeForWorkspace();
            _tab = AuthoringTab.Tracks;
            ApplyWorkspaceVisibility();
            RebuildSharedResourcesWorkspace();
            if (section == SharedResourceSection.AbilityEffectTimeline)
                RebuildAbilityCompositionWorkspace();
            RefreshChrome();
            SaveSessionState();
        }

        private void NormalizeEditorModeForWorkspace()
        {
            _editorMode = _workspace == AuthoringWorkspace.SharedResources
                && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline
                ? AuthoringEditorMode.AbilityEffectTimeline
                : AuthoringEditorMode.CharacterAction;
        }

        private void ApplyWorkspaceVisibility()
        {
            SetDisplayed(_actionWorkspaceRoot, _workspace == AuthoringWorkspace.ActionComposition);
            SetDisplayed(
                _abilityWorkspaceRoot,
                _workspace == AuthoringWorkspace.SharedResources
                    && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline);
            SetDisplayed(_mappingWorkspaceRoot, _workspace == AuthoringWorkspace.Mappings);
            SetDisplayed(
                _sharedWorkspaceRoot,
                _workspace == AuthoringWorkspace.SharedResources
                    && _sharedResourceSection != SharedResourceSection.AbilityEffectTimeline);
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            if (element != null)
                element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshWorkbenchAll()
        {
            if (_workbenchRoot == null && _contentHost != null)
                BuildWorkbenchShell(_contentHost);
            ApplyWorkspaceVisibility();
            RebuildWorkbenchActionNavigation();
            RebuildWorkbenchActionInspector();
            RebuildAbilityCompositionWorkspace();
            RebuildMappingWorkspaceContent();
            RebuildSharedResourcesWorkspace();
            if (_workspace == AuthoringWorkspace.ActionComposition)
            {
                _abilityEffectTimelineView?.SetPlaybackController(null);
                if (_timelineView != null && _model?.SelectedAction != null)
                    RebuildTracks();
            }
            else if (_workspace == AuthoringWorkspace.SharedResources
                && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline)
            {
                _timelineView?.SetPlaybackController(null);
            }
            else
            {
                _timelineView?.SetPlaybackController(null);
                _abilityEffectTimelineView?.SetPlaybackController(null);
            }
            RefreshProblemsWorkbench();
            UpdateWorkbenchPreviewFrameLabel();
        }

        private void RefreshCurrentWorkbenchWorkspace()
        {
            if (_workspace == AuthoringWorkspace.ActionComposition)
            {
                RebuildWorkbenchActionNavigation();
                if (_timelineView != null && _model?.SelectedAction != null)
                    RebuildTracks(rebuildInspector: false);
                RebuildWorkbenchActionInspector();
            }
            else if (_workspace == AuthoringWorkspace.Mappings)
            {
                RebuildMappingWorkspaceContent();
            }
            else if (_sharedResourceSection == SharedResourceSection.AbilityEffectTimeline)
            {
                RebuildAbilityCompositionWorkspace();
            }
            else
            {
                RebuildSharedResourcesWorkspace();
            }
            RefreshProblemsWorkbench();
        }

        private void RebuildMappingWorkspaceContent()
        {
            if (_mappingMainHost == null || _mappingInspectorHost == null)
                return;
            _mappingMainHost.Clear();
            _mappingInspectorHost.Clear();
            switch (_mappingSection)
            {
                case MappingWorkspaceSection.ProfileComposition:
                    BuildProfileTab(_mappingMainHost, _mappingInspectorHost);
                    break;
                case MappingWorkspaceSection.ActionSet:
                    BuildActionSetSummary(_mappingMainHost, includeBindings: false);
                    BuildActionSetInspector(_mappingInspectorHost);
                    break;
                case MappingWorkspaceSection.CommandBindings:
                    BuildCommandBindingWorkbench(_mappingMainHost, _mappingInspectorHost);
                    break;
                case MappingWorkspaceSection.AbilityBindings:
                    BuildAbilityBindingWorkbench(_mappingMainHost, _mappingInspectorHost);
                    break;
                case MappingWorkspaceSection.Reactions:
                    BuildReactionWorkbench(_mappingMainHost, _mappingInspectorHost);
                    break;
                case MappingWorkspaceSection.ReferenceSources:
                    BuildReferencesTab(_mappingMainHost);
                    _mappingInspectorHost.Add(new HelpBox(
                        "引用发现结果仅用于选择器和校验，不作为 Profile/ActionSet/Library 文件修改。",
                        HelpBoxMessageType.Info));
                    break;
                case MappingWorkspaceSection.Compatibility:
                    BuildCompatibilityWorkbench(_mappingMainHost);
                    break;
            }
        }

        private void BuildActionSetSummary(VisualElement parent, bool includeBindings)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
            {
                parent.Add(new HelpBox("未选择 ActionSet。", HelpBoxMessageType.Info));
                return;
            }
            VisualElement card = AddCard(parent, string.IsNullOrWhiteSpace(set.DisplayName) ? set.StableId : set.DisplayName);
            card.Add(new Label("角色: " + set.CharacterStableId));
            card.Add(new Label("装备: " + set.EquipmentStateStableId));
            card.Add(new Label("Movement: " + set.MovementProfileId));
            card.Add(new Label("Reaction: " + set.ReactionProfileId));
            card.Add(new Label("Landing: " + (string.IsNullOrWhiteSpace(set.LandingProfileId) ? "—" : set.LandingProfileId)));
            card.Add(new Label("默认动作: " + (set.DefaultActionId <= 0 ? "—" : "#" + set.DefaultActionId)));
            if (includeBindings)
            {
                card.Add(new Label("指令绑定: " + (set.CommandBindings?.Length ?? 0)));
                card.Add(new Label("能力绑定: " + (set.AbilityBindings?.Length ?? 0)));
            }
        }

        private void BuildActionSetInspector(VisualElement parent)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
                return;
            VisualElement card = AddCard(parent, "ActionSet 属性");
            AddTextField(card, "显示名称", set.DisplayName, value => set.DisplayName = value);
            AddPreviewBindingFields(card, UiRefreshScope.Content);
            AddTextField(card, "Movement Profile", set.MovementProfileId, value => set.MovementProfileId = value, domainId: 402);
            AddActionSetSearchPickerField(
                card,
                "Reaction Profile",
                set.ReactionProfileId,
                CharacterActionAuthoringSearchCatalogBuilder.BuildReactionProfiles(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.ReactionProfile,
                value => set.ReactionProfileId = value,
                refreshScope: UiRefreshScope.Content);
            AddTextField(card, "Landing Profile", set.LandingProfileId, value => set.LandingProfileId = value);
            CharacterLandingProfileJsonDto landingProfile = FindLandingProfile(set.LandingProfileId);
            if (landingProfile != null)
            {
                VisualElement landingCard = AddCard(parent, "Landing Profile");
                string previousLandingProfileId = landingProfile.StableId;
                AddTextField(landingCard, "Stable Id", landingProfile.StableId, value =>
                {
                    landingProfile.StableId = value;
                    if (string.Equals(set.LandingProfileId, previousLandingProfileId, StringComparison.Ordinal))
                        set.LandingProfileId = value;
                });
                CharacterActionAuthoringSearchCatalog actions =
                    CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model);
                AddActionSetSearchPickerField(
                    landingCard,
                    "Light Landing Action",
                    landingProfile.LightLandingActionId.ToString(CultureInfo.InvariantCulture),
                    actions,
                    CharacterActionAuthoringSearchCategory.Action,
                    value => landingProfile.LightLandingActionId = ParseActionId(value),
                    allowClear: true,
                    refreshScope: UiRefreshScope.Content);
                AddActionSetSearchPickerField(
                    landingCard,
                    "Heavy Landing Action",
                    landingProfile.HeavyLandingActionId.ToString(CultureInfo.InvariantCulture),
                    actions,
                    CharacterActionAuthoringSearchCategory.Action,
                    value => landingProfile.HeavyLandingActionId = ParseActionId(value),
                    allowClear: true,
                    refreshScope: UiRefreshScope.Content);
                AddIntegerField(
                    landingCard,
                    "Light Threshold (mm/s)",
                    landingProfile.LightLandingMinDownwardSpeedMillimetersPerSecond,
                    value => landingProfile.LightLandingMinDownwardSpeedMillimetersPerSecond = value);
                AddIntegerField(
                    landingCard,
                    "Heavy Threshold (mm/s)",
                    landingProfile.HeavyLandingMinDownwardSpeedMillimetersPerSecond,
                    value => landingProfile.HeavyLandingMinDownwardSpeedMillimetersPerSecond = value);
            }
            AddActionSetSearchPickerField(
                card,
                "默认动作",
                set.DefaultActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value => set.DefaultActionId = ParseActionId(value),
                allowClear: true,
                refreshScope: UiRefreshScope.Content);
            Foldout advanced = CreateSectionFoldout(card, "action-set-advanced", "高级", false);
            AddIntegerField(advanced, "Id", set.Id, value => set.Id = value);
            AddTextField(advanced, "Stable Id", set.StableId, value => set.StableId = value);
            AddIntegerField(advanced, "Default Slot Mask", set.DefaultActionSlotMask, value => set.DefaultActionSlotMask = value);
        }

        private CharacterLandingProfileJsonDto FindLandingProfile(string stableId)
        {
            for (int i = 0; _model != null && i < _model.LandingProfiles.Count; i++)
            {
                CharacterLandingProfileJsonDto profile = _model.LandingProfiles[i];
                if (profile != null && string.Equals(profile.StableId, stableId, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

        private void BuildCommandBindingWorkbench(VisualElement main, VisualElement inspector)
        {
            BuildCommandBindingTableWorkbench(main, inspector);
        }

        private void BuildAbilityBindingWorkbench(VisualElement main, VisualElement inspector)
        {
            BuildAbilityBindingTableWorkbench(main, inspector);
        }

        private void BuildReactionWorkbench(VisualElement main, VisualElement inspector)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
                return;
            VisualElement authority = AddCard(main, "权威受击规则");
            AddActionSetSearchPickerField(
                authority,
                "Reaction Profile",
                set.ReactionProfileId,
                CharacterActionAuthoringSearchCatalogBuilder.BuildReactionProfiles(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.ReactionProfile,
                value => set.ReactionProfileId = value,
                refreshScope: UiRefreshScope.Content);
            CharacterReactionProfileJsonDto selected = FindReactionProfile(set.ReactionProfileId);
            if (selected == null)
            {
                authority.Add(new HelpBox("当前 Reaction Profile 未加载。", HelpBoxMessageType.Warning));
                return;
            }
            authority.Add(new Label("规则数: " + (selected.Rules?.Length ?? 0)));
            VisualElement details = AddCard(inspector, "Reaction Profile");
            AddTextField(details, "显示名称", selected.DisplayName, value => selected.DisplayName = value);
            AddActionSetSearchPickerField(
                details,
                "默认动作",
                selected.DefaultActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value => selected.DefaultActionId = ParseActionId(value),
                allowClear: true,
                refreshScope: UiRefreshScope.Content);
            AddReactionProfileStableIdField(details, set, selected);
            CharacterReactionRuleJsonDto[] rules = selected.Rules ?? Array.Empty<CharacterReactionRuleJsonDto>();
            for (int i = 0; i < rules.Length; i++)
            {
                CharacterReactionRuleJsonDto rule = rules[i];
                if (rule == null)
                    continue;
                Foldout foldout = CreateSectionFoldout(details, "reaction-rule:" + i, "规则 " + i, i == 0);
                AddTextField(foldout, "Trigger", rule.Trigger, value => rule.Trigger = value);
                AddActionSetSearchPickerField(
                    foldout,
                    "目标动作",
                    rule.ActionId.ToString(CultureInfo.InvariantCulture),
                    CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                    CharacterActionAuthoringSearchCategory.Action,
                    value => rule.ActionId = ParseActionId(value),
                    refreshScope: UiRefreshScope.Content);
                AddIntegerField(foldout, "Priority", rule.Priority, value => rule.Priority = value);
                AddReactionRequiresToggle(
                    foldout,
                    "需要 Body Part",
                    rule.RequiresBodyPart,
                    value => rule.RequiresBodyPart = value);
                if (rule.RequiresBodyPart)
                AddTextField(foldout, "Body Part", rule.BodyPartId, value => rule.BodyPartId = value, domainId: 400);
                AddReactionRequiresToggle(
                    foldout,
                    "需要 Hit Zone",
                    rule.RequiresHitZone,
                    value => rule.RequiresHitZone = value);
                if (rule.RequiresHitZone)
                    AddTextField(foldout, "Hit Zone", rule.HitZoneId, value => rule.HitZoneId = value, domainId: 400);
                AddReactionRequiresToggle(
                    foldout,
                    "需要方向",
                    rule.RequiresHitDirection,
                    value => rule.RequiresHitDirection = value);
                if (rule.RequiresHitDirection)
                    AddTextField(foldout, "Hit Direction", rule.HitDirection, value => rule.HitDirection = value);
                AddReactionRequiresToggle(
                    foldout,
                    "需要伤害类型",
                    rule.RequiresDamageType,
                    value => rule.RequiresDamageType = value);
                if (rule.RequiresDamageType)
                    AddTextField(foldout, "Damage Type", rule.DamageTypeId, value => rule.DamageTypeId = value, domainId: 409);
                AddReactionRequiresToggle(
                    foldout,
                    "需要 Reaction Group",
                    rule.RequiresReactionGroup,
                    value => rule.RequiresReactionGroup = value);
                if (rule.RequiresReactionGroup)
                    AddTextField(foldout, "Reaction Group", rule.ReactionGroupId, value => rule.ReactionGroupId = value, domainId: 410);
                AddReactionRequiresToggle(
                    foldout,
                    "需要冲击力",
                    rule.RequiresImpactForce,
                    value => rule.RequiresImpactForce = value);
                if (rule.RequiresImpactForce)
                {
                    AddIntegerField(foldout, "最小冲击力", rule.MinImpactForce ?? 0, value => rule.MinImpactForce = value);
                    AddIntegerField(foldout, "最大冲击力", rule.MaxImpactForce ?? 0, value => rule.MaxImpactForce = value);
                }
            }
        }

        private void AddReactionRequiresToggle(
            VisualElement parent,
            string label,
            bool value,
            Action<bool> setter)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(evt => ApplyStructuralChange(
                () => setter?.Invoke(evt.newValue),
                UiRefreshScope.Content));
            parent.Add(toggle);
        }

        private CharacterReactionProfileJsonDto FindReactionProfile(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return null;

            if (_documentSession != null)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle =
                    _documentSession.FindActionSetBundle(
                        _documentSession.Selection.ActionSetStableId);
                CharacterReactionProfileJsonDto[] profiles =
                    bundle?.ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                for (int i = 0; i < profiles.Length; i++)
                {
                    CharacterReactionProfileJsonDto profile = profiles[i];
                    if (string.Equals(profile?.StableId, stableId, StringComparison.Ordinal))
                        return profile;
                }

                return null;
            }

            if (_model != null)
            {
                for (int i = 0; i < _model.ReactionProfiles.Count; i++)
                {
                    CharacterReactionProfileJsonDto profile = _model.ReactionProfiles[i];
                    if (string.Equals(profile?.StableId, stableId, StringComparison.Ordinal))
                        return profile;
                }
            }
            return null;
        }

        private void AddReactionProfileStableIdField(
            VisualElement parent,
            CharacterActionSetJsonDto actionSet,
            CharacterReactionProfileJsonDto profile)
        {
            var field = new TextField("Stable Id")
            {
                value = profile?.StableId ?? string.Empty,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt =>
                ApplyActionSetDocumentChange(
                    () => RenameReactionProfile(actionSet, profile, evt.newValue),
                    UiRefreshScope.Content));
            parent.Add(field);
        }

        private static void RenameReactionProfile(
            CharacterActionSetJsonDto actionSet,
            CharacterReactionProfileJsonDto profile,
            string stableId)
        {
            if (profile == null)
                return;

            string previousStableId = profile.StableId ?? string.Empty;
            string nextStableId = stableId ?? string.Empty;
            profile.StableId = nextStableId;
            if (actionSet != null
                && string.Equals(
                    actionSet.ReactionProfileId,
                    previousStableId,
                    StringComparison.Ordinal))
            {
                actionSet.ReactionProfileId = nextStableId;
            }
        }

        private void BuildCompatibilityWorkbench(VisualElement main)
        {
            VisualElement card = AddCard(main, "兼容数据（只读）");
            card.Add(new HelpBox(
                "legacy reactionBindings[] 当前不由 CharacterActionResolver 直接消费。工作台只展示并原样保存，不提供新增或编辑。",
                HelpBoxMessageType.Warning));
            CharacterReactionBindingJsonDto[] bindings = _model?.ActionSet?.ReactionBindings
                ?? Array.Empty<CharacterReactionBindingJsonDto>();
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterReactionBindingJsonDto binding = bindings[i];
                card.Add(new Label(
                    i + ": " + (binding?.ReactionProfileId ?? string.Empty)
                    + " → #" + (binding?.DefaultActionId ?? 0)));
            }
            if (bindings.Length == 0)
                card.Add(new Label("（无 legacy reactionBindings）"));
        }

        private void RebuildSharedResourcesWorkspace()
        {
            if (_sharedMainHost == null || _sharedInspectorHost == null)
                return;
            _sharedMainHost.Clear();
            _sharedInspectorHost.Clear();
            _sharedInspectorHost.Add(new HelpBox("请选择左侧共享资源类型。", HelpBoxMessageType.Info));
        }

        private void UpdateWorkbenchPreviewFrameLabel()
        {
            if (_previewFrameLabel == null)
                return;
            int frame = _timelinePlayback?.CurrentFrame ?? 0;
            int duration = Math.Max(
                1,
                _timelinePlayback?.DurationFrames ?? (_model?.SelectedAction?.DurationFrames ?? 1));
            if (_previewFrameLabelsDuration != duration)
            {
                var labels = new string[duration];
                string suffix = " / " + (duration - 1) + " 帧";
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = i + suffix;
                _previewFrameLabels = labels;
                _previewFrameLabelsDuration = duration;
            }

            _previewFrameLabel.text = _previewFrameLabels[Mathf.Clamp(frame, 0, duration - 1)];
            AbilityEffectTimelineDraft abilityTimeline = GetSelectedAbilityEffectTimeline();
            UpdateAbilityPreviewFrameLabel(
                frame,
                Math.Max(1, abilityTimeline?.DurationFrames ?? duration));
        }

        private static int ParseActionId(string value)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                && parsed > 0
                ? parsed
                : 0;
        }

        private void BuildProblemsWorkbench()
        {
            if (_footerBar == null)
                return;
            if (_problemsBody != null && ReferenceEquals(_problemsBody.parent, _footerBar))
            {
                RefreshProblemsWorkbench();
                return;
            }

            _footerBar.Clear();
            _footerBar.name = "character-action-problems";
            _footerBar.AddToClassList("ca-problems-drawer");
            _problemsExpanded = EditorPrefs.GetBool(WorkbenchProblemsExpandedPreferenceKey, false);
            _problemsRequestedHeight = EditorPrefs.GetFloat(
                WorkbenchProblemsHeightPreferenceKey,
                WorkbenchProblemsDefaultHeight);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnWorkbenchRootGeometryChanged);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnWorkbenchRootGeometryChanged);

            _problemsResizeHandle = new VisualElement { name = "character-action-problems-resize-handle" };
            _problemsResizeHandle.AddToClassList("ca-problems-resize-handle");
            _problemsResizeHandle.RegisterCallback<PointerDownEvent>(OnProblemsResizePointerDown);
            _problemsResizeHandle.RegisterCallback<PointerMoveEvent>(OnProblemsResizePointerMove);
            _problemsResizeHandle.RegisterCallback<PointerUpEvent>(OnProblemsResizePointerUp);
            _problemsResizeHandle.RegisterCallback<PointerCaptureOutEvent>(OnProblemsResizePointerCaptureOut);
            _footerBar.Add(_problemsResizeHandle);

            var header = new VisualElement();
            header.AddToClassList("ca-problems-header");
            _problemsSummaryLabel = new Label("问题");
            _problemsSummaryLabel.AddToClassList("ca-problems-title");
            header.Add(_problemsSummaryLabel);
            _problemsScopeMenu = new ToolbarMenu { text = ProblemsScopeLabel() };
            AppendProblemsScope(_problemsScopeMenu, CharacterActionAuthoringProblemScope.All, "全部");
            AppendProblemsScope(_problemsScopeMenu, CharacterActionAuthoringProblemScope.CurrentAction, "当前动作");
            AppendProblemsScope(_problemsScopeMenu, CharacterActionAuthoringProblemScope.CurrentSource, "当前来源");
            AppendProblemsScope(_problemsScopeMenu, CharacterActionAuthoringProblemScope.ReferenceDataSource, "数据源");
            header.Add(_problemsScopeMenu);
            _problemsSeverityMenu = new ToolbarMenu { text = ProblemsSeverityLabel() };
            AppendProblemsSeverity(_problemsSeverityMenu, CharacterActionAuthoringProblemSeverityFilter.All, "全部级别");
            AppendProblemsSeverity(_problemsSeverityMenu, CharacterActionAuthoringProblemSeverityFilter.Error, "仅错误");
            AppendProblemsSeverity(_problemsSeverityMenu, CharacterActionAuthoringProblemSeverityFilter.Warning, "仅警告");
            AppendProblemsSeverity(_problemsSeverityMenu, CharacterActionAuthoringProblemSeverityFilter.Info, "仅信息");
            header.Add(_problemsSeverityMenu);
            var search = new ToolbarSearchField { value = _problemsQuery };
            search.AddToClassList("ca-problems-search");
            search.RegisterValueChangedCallback(evt =>
            {
                _problemsQuery = evt.newValue ?? string.Empty;
                SaveSessionState();
                RefreshProblemsWorkbench();
            });
            header.Add(search);
            var spacer = new VisualElement();
            spacer.AddToClassList("ca-toolbar-spacer");
            header.Add(spacer);
            _problemsToggleButton = new Button(ToggleProblemsWorkbench);
            header.Add(_problemsToggleButton);
            _footerBar.Add(header);
            _problemsBody = new ScrollView(ScrollViewMode.Vertical);
            _problemsBody.AddToClassList("ca-problems-body");
            _footerBar.Add(_problemsBody);
            RefreshProblemsWorkbench();
        }

        private void ToggleProblemsWorkbench()
        {
            _problemsExpanded = !_problemsExpanded;
            EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, _problemsExpanded);
            RefreshProblemsWorkbench();
        }

        private void RefreshProblemsWorkbench()
        {
            if (_footerBar == null)
                return;
            if (_problemsBody == null || _problemsSummaryLabel == null)
            {
                BuildProblemsWorkbench();
                return;
            }
            CountValidationDiagnostics(out int errors, out int warnings);
            _problemsSummaryLabel.text = "问题   " + errors + " 错误   " + warnings + " 警告";
            if (_problemsScopeMenu != null)
                _problemsScopeMenu.text = ProblemsScopeLabel();
            if (_problemsSeverityMenu != null)
                _problemsSeverityMenu.text = ProblemsSeverityLabel();
            if (_problemsToggleButton != null)
                _problemsToggleButton.text = _problemsExpanded ? "⌄" : "⌃";
            if (_problemsResizeHandle != null)
                _problemsResizeHandle.style.display = _problemsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _problemsBody.Clear();
            _problemsBody.style.display = _problemsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_problemsExpanded)
                return;
            ApplyProblemsDrawerHeight(_problemsRequestedHeight);
            _problemsModel = CreateWorkbenchProblemsModel();
            if ((_documentSessionDraftConflictPaths?.Length ?? 0) > 0)
                AddDocumentDraftRecoveryNotice(_problemsBody);
            if (_validation == null && _problemsModel.Items.Count == 0)
            {
                _problemsBody.Add(new Label("尚未校验。"));
                return;
            }
            CharacterActionAuthoringProblemsProjection projection = _problemsModel.Project(
                new CharacterActionAuthoringProblemFilter(
                    _problemsScope,
                    _problemsSeverity,
                    _problemsQuery,
                    _model?.SelectedAction?.ActionId.ToString(CultureInfo.InvariantCulture),
                    CurrentProblemsSourcePath()));
            _problemsSummaryLabel.text = "问题   "
                + projection.TotalErrorCount + " 错误   "
                + projection.TotalWarningCount + " 警告";
            for (int i = 0; i < projection.Items.Length; i++)
                AddProblemRow(_problemsBody, projection.Items[i]);
            if (projection.Items.Length == 0)
                _problemsBody.Add(new Label("当前筛选下没有问题。"));
        }

        private void AppendProblemsScope(
            ToolbarMenu menu,
            CharacterActionAuthoringProblemScope scope,
            string label)
        {
            menu.menu.AppendAction(
                label,
                _ =>
                {
                    _problemsScope = scope;
                    SaveSessionState();
                    RefreshProblemsWorkbench();
                },
                _ => _problemsScope == scope
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private void AppendProblemsSeverity(
            ToolbarMenu menu,
            CharacterActionAuthoringProblemSeverityFilter severity,
            string label)
        {
            menu.menu.AppendAction(
                label,
                _ =>
                {
                    _problemsSeverity = severity;
                    SaveSessionState();
                    RefreshProblemsWorkbench();
                },
                _ => _problemsSeverity == severity
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private void OnWorkbenchRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (_problemsExpanded)
                ApplyProblemsDrawerHeight(_problemsRequestedHeight);
        }

        private void OnProblemsResizePointerDown(PointerDownEvent evt)
        {
            if (!_problemsExpanded || evt.button != 0 || _problemsResizeHandle == null)
                return;

            _problemsResizePointerId = evt.pointerId;
            _problemsResizeStartY = evt.position.y;
            float resolvedHeight = _footerBar == null
                ? _problemsRequestedHeight
                : _footerBar.resolvedStyle.height;
            if (float.IsNaN(resolvedHeight) || float.IsInfinity(resolvedHeight))
                resolvedHeight = _problemsRequestedHeight;
            _problemsResizeStartHeight = Mathf.Max(WorkbenchProblemsMinimumHeight, resolvedHeight);
            _problemsResizeHandle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnProblemsResizePointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _problemsResizePointerId
                || _problemsResizeHandle == null
                || !_problemsResizeHandle.HasPointerCapture(evt.pointerId))
                return;

            _problemsRequestedHeight = ClampProblemsDrawerHeight(
                _problemsResizeStartHeight + _problemsResizeStartY - evt.position.y);
            ApplyProblemsDrawerHeight(_problemsRequestedHeight);
            evt.StopPropagation();
        }

        private void OnProblemsResizePointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _problemsResizePointerId || _problemsResizeHandle == null)
                return;

            if (_problemsResizeHandle.HasPointerCapture(evt.pointerId))
                _problemsResizeHandle.ReleasePointer(evt.pointerId);
            CompleteProblemsResizeGesture();
            evt.StopPropagation();
        }

        private void OnProblemsResizePointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _problemsResizePointerId)
                CompleteProblemsResizeGesture();
        }

        private void CompleteProblemsResizeGesture()
        {
            if (_problemsResizePointerId < 0)
                return;

            _problemsResizePointerId = -1;
            EditorPrefs.SetFloat(WorkbenchProblemsHeightPreferenceKey, _problemsRequestedHeight);
        }

        private void ApplyProblemsDrawerHeight(float requestedHeight)
        {
            if (_problemsBody == null || !_problemsExpanded)
                return;

            float drawerHeight = ClampProblemsDrawerHeight(requestedHeight);
            float bodyHeight = drawerHeight
                - WorkbenchProblemsHeaderHeight
                - WorkbenchProblemsResizeHandleHeight;
            float resolvedHeight = _problemsBody.resolvedStyle.height;
            if (float.IsNaN(resolvedHeight)
                || float.IsInfinity(resolvedHeight)
                || Mathf.Abs(resolvedHeight - bodyHeight) > 0.5f)
                _problemsBody.style.height = bodyHeight;
        }

        private float ClampProblemsDrawerHeight(float requestedHeight)
        {
            float contentMaximum = WorkbenchProblemsHeaderHeight
                + WorkbenchProblemsResizeHandleHeight
                + WorkbenchProblemsBodyMaximumHeight;
            float windowMaximum = Mathf.Max(
                WorkbenchProblemsMinimumHeight,
                position.height - 510f);
            float maximum = Mathf.Min(contentMaximum, windowMaximum);
            return Mathf.Clamp(requestedHeight, WorkbenchProblemsMinimumHeight, maximum);
        }

        private string ProblemsScopeLabel()
        {
            switch (_problemsScope)
            {
                case CharacterActionAuthoringProblemScope.CurrentAction:
                    return "当前动作";
                case CharacterActionAuthoringProblemScope.CurrentSource:
                    return "当前来源";
                case CharacterActionAuthoringProblemScope.ReferenceDataSource:
                    return "数据源";
                default:
                    return "全部";
            }
        }

        private string ProblemsSeverityLabel()
        {
            switch (_problemsSeverity)
            {
                case CharacterActionAuthoringProblemSeverityFilter.Error:
                    return "仅错误";
                case CharacterActionAuthoringProblemSeverityFilter.Warning:
                    return "仅警告";
                case CharacterActionAuthoringProblemSeverityFilter.Info:
                    return "仅信息";
                default:
                    return "全部级别";
            }
        }

        private string CurrentProblemsSourcePath()
        {
            if (_workspace == AuthoringWorkspace.ActionComposition)
            {
                string contextualSource = CurrentTimelineResourceSourcePath();
                return string.IsNullOrWhiteSpace(contextualSource)
                    ? _documentSession?.CurrentActionLibrary?.Source.ActionsPath
                        ?? _actionLibrarySourceDirectory
                    : contextualSource;
            }
            if (_workspace == AuthoringWorkspace.Mappings)
                return _documentSession?.FindActionSetBundle(
                        _documentSession.Selection.ActionSetStableId)?.Source.ActionSetsPath
                    ?? _actionSetSourceDirectory;
            if (_sharedResourceSection == SharedResourceSection.AbilityEffectTimeline)
                return CurrentAbilityAuthoringSourcePath();
            return string.Empty;
        }

        private string CurrentTimelineResourceSourcePath()
        {
            ITimelineItemAdapter selectedItem = string.IsNullOrWhiteSpace(_timelineView?.SelectedItemId)
                ? null
                : _timelineView.Snapshot?.FindItem(_timelineView.SelectedItemId);
            if (selectedItem?.TrackKind != TimelineTrackKind.Gameplay
                || !selectedItem.TryGetCurrentIndex(out int eventIndex))
            {
                return string.Empty;
            }

            GameplayTrackEventJsonDto[] events = _model?.SelectedAction?.GameplayTrack?.Events
                ?? Array.Empty<GameplayTrackEventJsonDto>();
            if (eventIndex < 0 || eventIndex >= events.Length)
                return string.Empty;
            string requestId = CharacterActionAuthoringIds.Label(events[eventIndex]?.RequestId ?? 0, 408);
            if (string.Equals(
                    requestId,
                    CharacterActionMechanicTuningAuthoringStore.ArcMineGameplayRequestId,
                    StringComparison.Ordinal)
                || string.Equals(
                    requestId,
                    CharacterActionMechanicTuningAuthoringStore.RuneTurretGameplayRequestId,
                    StringComparison.Ordinal))
            {
                return CurrentAbilityAuthoringSourcePath();
            }

            return IsProjectileRequestId(requestId)
                ? FindProjectileSourcePath(requestId)
                : string.Empty;
        }

        private string FindProjectileSourcePath(string requestId)
        {
            CharacterActionProjectileAuthoringStore store = LoadProjectileAuthoringStoreForWindow();
            for (int i = 0; i < store.Records.Count; i++)
            {
                CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record =
                    store.Records[i];
                if (string.Equals(record?.RequestId, requestId, StringComparison.Ordinal))
                    return record.SourcePath ?? string.Empty;
            }
            return string.Empty;
        }

        private string CurrentAbilityAuthoringSourcePath()
        {
            return string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                ? CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath
                : _abilityAuthoringJsonPath;
        }

        private void AddProblemRow(VisualElement parent, CharacterActionAuthoringProblem problem)
        {
            var row = new Button(() => LocateProblem(problem));
            row.AddToClassList("ca-problems-row");
            string icon = problem.Severity == CharacterActionDiagnosticSeverity.Error
                ? "●"
                : problem.Severity == CharacterActionDiagnosticSeverity.Warning ? "▲" : "●";
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("ca-problems-row__icon");
            row.Add(iconLabel);
            var pathLabel = new Label(problem.ObjectPath);
            pathLabel.AddToClassList("ca-problems-row__path");
            row.Add(pathLabel);
            Label message = new Label(problem.Message);
            message.AddToClassList("ca-problems-row__message");
            row.Add(message);
            parent.Add(row);
        }

        private CharacterActionAuthoringProblemsModel CreateWorkbenchProblemsModel()
        {
            int conflictCount = _documentSessionDraftConflictPaths?.Length ?? 0;
            bool hasSaveFailure = !string.IsNullOrWhiteSpace(_lastSaveFailureMessage);
            if (!hasSaveFailure && conflictCount == 0)
                return CharacterActionAuthoringProblemsModel.Create(_validation, _referenceDiscovery);

            CharacterActionJsonDiagnostic[] current =
                _validation?.ImportResult?.Diagnostics ?? Array.Empty<CharacterActionJsonDiagnostic>();
            var importDiagnostics = new CharacterActionJsonDiagnostic[
                current.Length + (hasSaveFailure ? 1 : 0) + conflictCount];
            Array.Copy(current, importDiagnostics, current.Length);
            int writeIndex = current.Length;
            if (hasSaveFailure)
            {
                importDiagnostics[writeIndex++] = new CharacterActionJsonDiagnostic(
                    _lastSaveFailureSource,
                    "$",
                    "CA_SAVE_FAILED",
                    CharacterActionDiagnosticSeverity.Error,
                    _lastSaveFailureMessage);
            }
            for (int i = 0; i < conflictCount; i++)
            {
                string sourcePath = _documentSessionDraftConflictPaths[i] ?? string.Empty;
                importDiagnostics[writeIndex++] = new CharacterActionJsonDiagnostic(
                    sourcePath,
                    "$",
                    "CA_DRAFT_BASELINE_CONFLICT",
                    CharacterActionDiagnosticSeverity.Warning,
                    "未恢复此来源的会话草稿：源文件在基线之后已变化，工具没有自动覆盖。可定位来源并手动合并，或先导出恢复载荷。");
            }
            return CharacterActionAuthoringProblemsModel.Create(
                importDiagnostics,
                _validation?.LocalValidationReport?.Diagnostics,
                _validation?.ValidationReport?.Diagnostics,
                _referenceDiscovery?.Diagnostics);
        }

        private void AddDocumentDraftRecoveryNotice(VisualElement parent)
        {
            parent.Add(new HelpBox(
                "检测到与磁盘基线冲突的会话草稿。冲突来源未自动恢复，也不会覆盖当前文件。",
                HelpBoxMessageType.Warning));
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(ExportConflictedDocumentSessionDrafts) { text = "导出恢复载荷" });
            actions.Add(new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                        "丢弃冲突草稿",
                        "确认丢弃保存于 SessionState 的冲突恢复载荷？此操作不会修改源文件。",
                        "丢弃",
                        "取消"))
                {
                    ClearConflictedDocumentSessionDrafts();
                }
            }) { text = "丢弃恢复提示" });
            parent.Add(actions);
        }

        private void ExportConflictedDocumentSessionDrafts()
        {
            if (string.IsNullOrWhiteSpace(_conflictedDocumentSessionDraftsJson))
                return;
            string path = EditorUtility.SaveFilePanel(
                "导出 Character Action 恢复载荷",
                string.Empty,
                "character-action-recovery.json",
                "json");
            if (string.IsNullOrWhiteSpace(path))
                return;
            File.WriteAllText(path, _conflictedDocumentSessionDraftsJson);
            EditorUtility.RevealInFinder(path);
        }

        private void LocateProblem(CharacterActionAuthoringProblem problem)
        {
            CharacterActionAuthoringProblemLocation location = problem.Location;
            if (!location.HasStructuredTarget)
            {
                RevealProblemSource(location.SourcePath);
                return;
            }

            if (location.Workspace == CharacterActionAuthoringProblemWorkspace.ActionComposition)
            {
                if (!int.TryParse(
                        location.ObjectKey,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int actionId)
                    || !SelectWorkbenchAction(actionId))
                {
                    RevealProblemSource(location.SourcePath);
                    return;
                }
                SetWorkspace(AuthoringWorkspace.ActionComposition);
                if (location.TargetKind == CharacterActionAuthoringProblemTargetKind.TimelineItem)
                    rootVisualElement.schedule.Execute(() => SelectProblemTimelineItem(location)).ExecuteLater(1);
                return;
            }

            if (location.Workspace != CharacterActionAuthoringProblemWorkspace.Mappings)
                return;

            if (location.TargetKind == CharacterActionAuthoringProblemTargetKind.CommandBinding
                || location.TargetKind == CharacterActionAuthoringProblemTargetKind.AbilityBinding)
            {
                if (!CharacterActionBindingProblemNavigation.TryCreate(
                        location,
                        _documentSession,
                        _model?.ActionSet,
                        out CharacterActionBindingProblemNavigationPlan plan)
                    || !SelectProblemActionSet(plan.ActionSetStableId))
                {
                    RevealProblemSource(location.SourcePath);
                    return;
                }

                PrepareBindingProblemNavigation(plan);
                SetWorkspace(AuthoringWorkspace.Mappings);
                SetMappingSection(
                    plan.TargetKind == CharacterActionAuthoringProblemTargetKind.CommandBinding
                        ? MappingWorkspaceSection.CommandBindings
                        : MappingWorkspaceSection.AbilityBindings);
                ScheduleFocusBindingProblemInspector(plan);
                return;
            }

            if (location.TargetKind == CharacterActionAuthoringProblemTargetKind.ReactionProfile)
            {
                if (!TryResolveReactionProblemActionSet(
                        location,
                        _documentSession,
                        _model?.ActionSet,
                        out string reactionActionSetStableId)
                    || !SelectProblemActionSet(reactionActionSetStableId))
                {
                    RevealProblemSource(location.SourcePath);
                    return;
                }
            }

            if (location.TargetKind != CharacterActionAuthoringProblemTargetKind.ReactionProfile)
            {
                if (!CharacterActionBindingProblemNavigation.TryResolveActionSetStableId(
                        location.ObjectKey,
                        location.SourcePath,
                        _documentSession,
                        _model?.ActionSet,
                        out string actionSetStableId,
                        out _)
                    || !SelectProblemActionSet(actionSetStableId))
                {
                    RevealProblemSource(location.SourcePath);
                    return;
                }
            }

            SetWorkspace(AuthoringWorkspace.Mappings);
            switch (location.TargetKind)
            {
                case CharacterActionAuthoringProblemTargetKind.ReactionProfile:
                    SetMappingSection(MappingWorkspaceSection.Reactions);
                    break;
                case CharacterActionAuthoringProblemTargetKind.LegacyReactionBinding:
                    SetMappingSection(MappingWorkspaceSection.Compatibility);
                    break;
                default:
                    SetMappingSection(MappingWorkspaceSection.ActionSet);
                    break;
            }
        }

        internal static bool TryResolveReactionProblemActionSet(
            CharacterActionAuthoringProblemLocation location,
            CharacterActionAuthoringDocumentSession session,
            CharacterActionSetJsonDto fallbackActionSet,
            out string actionSetStableId)
        {
            actionSetStableId = string.Empty;
            if (location.TargetKind != CharacterActionAuthoringProblemTargetKind.ReactionProfile
                || string.IsNullOrWhiteSpace(location.ObjectKey))
            {
                return false;
            }

            if (session == null)
            {
                if (fallbackActionSet == null
                    || !string.Equals(
                        fallbackActionSet.ReactionProfileId,
                        location.ObjectKey,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                actionSetStableId = fallbackActionSet.StableId ?? string.Empty;
                return !string.IsNullOrWhiteSpace(actionSetStableId);
            }

            CharacterActionAuthoringActionSetBundleDocument ownerBundle = null;
            for (int i = 0; i < session.ActionSetBundles.Count; i++)
            {
                CharacterActionAuthoringActionSetBundleDocument candidate =
                    session.ActionSetBundles[i];
                if (!ProblemSourcePathEquals(
                        candidate?.Source.ReactionProfilesPath,
                        location.SourcePath))
                {
                    continue;
                }
                if (ownerBundle != null)
                    return false;
                ownerBundle = candidate;
            }
            if (ownerBundle == null)
                return false;

            CharacterReactionProfileJsonDto[] reactionProfiles =
                ownerBundle.ReactionProfilesDocument.ReactionProfiles
                ?? Array.Empty<CharacterReactionProfileJsonDto>();
            string reactionProfileId = location.ObjectKey;
            if (int.TryParse(
                    location.ObjectKey,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int reactionProfileIndex))
            {
                if (reactionProfileIndex < 0
                    || reactionProfileIndex >= reactionProfiles.Length
                    || reactionProfiles[reactionProfileIndex] == null)
                {
                    return false;
                }
                reactionProfileId = reactionProfiles[reactionProfileIndex].StableId ?? string.Empty;
            }
            else
            {
                bool containsReactionProfile = false;
                for (int i = 0; i < reactionProfiles.Length; i++)
                {
                    if (!string.Equals(
                            reactionProfiles[i]?.StableId,
                            reactionProfileId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    containsReactionProfile = true;
                    break;
                }
                if (!containsReactionProfile)
                    return false;
            }

            CharacterActionSetJsonDto[] actionSets =
                ownerBundle.ActionSetsDocument.ActionSets
                ?? Array.Empty<CharacterActionSetJsonDto>();
            CharacterActionSetJsonDto current = session.CurrentActionSet;
            if (ReferencesReactionProfile(current, reactionProfileId)
                && ContainsReference(actionSets, current))
            {
                actionSetStableId = current.StableId ?? string.Empty;
                return !string.IsNullOrWhiteSpace(actionSetStableId);
            }

            string[] profileActionSetIds = session.CurrentProfile?.ActionSetIds
                ?? Array.Empty<string>();
            for (int i = 0; i < profileActionSetIds.Length; i++)
            {
                for (int j = 0; j < actionSets.Length; j++)
                {
                    CharacterActionSetJsonDto candidate = actionSets[j];
                    if (!string.Equals(
                            candidate?.StableId,
                            profileActionSetIds[i],
                            StringComparison.Ordinal)
                        || !ReferencesReactionProfile(candidate, reactionProfileId))
                    {
                        continue;
                    }

                    actionSetStableId = candidate.StableId;
                    return true;
                }
            }

            return false;
        }

        private static bool ReferencesReactionProfile(
            CharacterActionSetJsonDto actionSet,
            string reactionProfileId)
        {
            return actionSet != null
                && !string.IsNullOrWhiteSpace(actionSet.StableId)
                && string.Equals(
                    actionSet.ReactionProfileId,
                    reactionProfileId,
                    StringComparison.Ordinal);
        }

        private static bool ContainsReference(
            CharacterActionSetJsonDto[] actionSets,
            CharacterActionSetJsonDto value)
        {
            for (int i = 0; i < actionSets.Length; i++)
            {
                if (ReferenceEquals(actionSets[i], value))
                    return true;
            }
            return false;
        }

        private static bool ProblemSourcePathEquals(string left, string right)
        {
            string normalizedLeft = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(left);
            string normalizedRight = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(right);
            if (string.Equals(
                    normalizedLeft,
                    normalizedRight,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(normalizedLeft)
                || string.IsNullOrWhiteSpace(normalizedRight))
            {
                return false;
            }
            try
            {
                return string.Equals(
                    CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                        Path.GetFullPath(normalizedLeft)),
                    CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                        Path.GetFullPath(normalizedRight)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool SelectProblemActionSet(string stableId)
        {
            if (_documentSession != null)
                return SelectDocumentActionSet(stableId);
            return string.Equals(_model?.ActionSet?.StableId, stableId, StringComparison.Ordinal);
        }

        private bool SelectWorkbenchAction(int actionId)
        {
            if (actionId <= 0)
                return false;
            if (_documentSession != null)
            {
                bool selected = SelectDocumentAction(actionId);
                if (selected)
                    RecordCurrentActionRecentActivity();
                return selected;
            }
            for (int i = 0; i < (_model?.Actions.Count ?? 0); i++)
            {
                if (_model.Actions[i]?.ActionId != actionId)
                    continue;
                _model.SelectedActionIndex = i;
                RecordCurrentActionRecentActivity();
                _pendingTimelineSelectedItemId = string.Empty;
                TouchWorkbenchPreviewRevision();
                return true;
            }
            return false;
        }

        private void SelectProblemTimelineItem(CharacterActionAuthoringProblemLocation location)
        {
            if (_timelineView?.Snapshot == null)
                return;
            if (!string.IsNullOrWhiteSpace(location.ItemKey)
                && _timelineView.Snapshot.FindItem(location.ItemKey) != null)
            {
                _timelineView.SelectItem(location.ItemKey);
                return;
            }

            if (!TryResolveProblemTrack(location.Section, out TimelineTrackKind trackKind)
                || !_timelineView.Snapshot.ItemsByTrack.TryGetValue(
                    trackKind,
                    out IReadOnlyList<ITimelineItemAdapter> items))
            {
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].TryGetCurrentIndex(out int index) && index == location.ItemIndex)
                {
                    _timelineView.SelectItem(items[i].Id);
                    return;
                }
            }
        }

        private static bool TryResolveProblemTrack(string section, out TimelineTrackKind trackKind)
        {
            if (string.Equals(section, "phases", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Phases;
            else if (string.Equals(section, "cancelRules", StringComparison.Ordinal)
                || string.Equals(section, "interruptRules", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.TransitionWindow;
            else if (section.StartsWith("motionTrack", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Motion;
            else if (section.StartsWith("animationTrack", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Animation;
            else if (section.StartsWith("combatTrack", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Combat;
            else if (section.StartsWith("gameplayTrack", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Gameplay;
            else if (section.StartsWith("presentationTrack", StringComparison.Ordinal))
                trackKind = TimelineTrackKind.Presentation;
            else
            {
                trackKind = TimelineTrackKind.Phases;
                return false;
            }
            return true;
        }

        private static void RevealProblemSource(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;
            string resolved = Path.IsPathRooted(sourcePath)
                ? sourcePath
                : Path.GetFullPath(sourcePath);
            EditorUtility.RevealInFinder(resolved);
        }

        private static void PersistPanePreference(string key, float value, float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
                return;
            EditorPrefs.SetFloat(key, value);
        }

        private string WorkbenchDisplayName()
        {
            switch (_workspace)
            {
                case AuthoringWorkspace.Mappings:
                    return "映射与方案";
                case AuthoringWorkspace.SharedResources:
                    return _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline
                        ? "技能编排"
                        : "共享资源";
                default:
                    return "动作编排";
            }
        }
    }
}
