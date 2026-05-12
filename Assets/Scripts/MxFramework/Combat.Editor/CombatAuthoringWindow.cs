using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    internal static class UiColors
    {
        // Surface & Layout
        public static readonly Color Surface = new Color(0.18f, 0.18f, 0.18f);
        public static readonly Color SurfaceAlt = new Color(0.16f, 0.16f, 0.16f);
        public static readonly Color SurfaceDeep = new Color(0.12f, 0.12f, 0.12f);
        public static readonly Color Border = new Color(0.26f, 0.26f, 0.26f);

        // Text
        public static readonly Color TextPrimary = new Color(0.88f, 0.88f, 0.88f);
        public static readonly Color TextSecondary = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color TextMuted = new Color(0.42f, 0.42f, 0.42f);

        // Semantic
        public static readonly Color TimelineActive = new Color(0.82f, 0.3f, 0.24f);
        public static readonly Color SuccessGreen = new Color(0.2f, 0.55f, 0.25f);
        public static readonly Color WarnAmber = new Color(0.85f, 0.55f, 0.1f);
        public static readonly Color ErrorRed = new Color(0.8f, 0.25f, 0.2f);

        // Timeline Strip
        public static readonly Color TimelineHeaderBg = new Color(0.12f, 0.12f, 0.12f);
        public static readonly Color TimelineHeaderText = new Color(0.82f, 0.82f, 0.82f);
        public static readonly Color TimelineLaneEven = new Color(0.105f, 0.105f, 0.105f);
        public static readonly Color TimelineLaneOdd = new Color(0.13f, 0.13f, 0.13f);
        public static readonly Color TimelineLabelBg = new Color(0.15f, 0.15f, 0.15f);
        public static readonly Color TimelineEmptyLaneText = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color TimelineGridLine = new Color(0.2f, 0.2f, 0.2f, 0.35f);
        public static readonly Color TimelineTickLine = new Color(0.42f, 0.42f, 0.42f);
        public static readonly Color TimelineTickLabel = new Color(0.74f, 0.74f, 0.74f);
        public static readonly Color TimelinePlayhead = new Color(1f, 0.92f, 0.35f);
        public static readonly Color TimelineScrollBorder = new Color(0.18f, 0.18f, 0.18f);

        // Timeline Bar Colors (semantic)
        public static readonly Color BarStartup = new Color(0.36f, 0.52f, 0.88f);
        public static readonly Color BarActive = new Color(0.82f, 0.3f, 0.24f);
        public static readonly Color BarRecovery = new Color(0.34f, 0.62f, 0.36f);
        public static readonly Color BarHitbox = new Color(0.92f, 0.36f, 0.24f);
        public static readonly Color BarHurtbox = new Color(0.22f, 0.62f, 0.86f);
        public static readonly Color BarTrace = new Color(0.86f, 0.58f, 0.18f);

        // Handle / Selection
        public static readonly Color HandleDefault = new Color(1f, 1f, 1f, 0.7f);
        public static readonly Color SelectionBorder = Color.white;

        // Interactive States
        public static readonly Color ButtonHover = new Color(0.24f, 0.24f, 0.24f);
    }

    internal enum CombatAuthoringWindowMode
    {
        Inspector,
        Timeline,
    }

    public sealed class CombatAuthoringWindow : EditorWindow
    {
        private const string AuthoringMenuPath = "MxFramework/Combat/Combat Authoring";
        private const string TimelineMenuPath = "MxFramework/Combat/Combat Timeline";
        private const string LayoutMenuPath = "MxFramework/Combat/Open Authoring Layout";
        private const float TimelineRowHeight = 24f;
        private const float TimelineStripHeaderHeight = 28f;
        private const float TimelineStripLaneHeight = 28f;
        private const float TimelineStripLabelWidth = 126f;
        private const float TimelineStripPixelsPerFrame = 18f;
        private const float TimelineStripMinTrackWidth = 360f;
        private const float TimelineStripMinHeight = 280f;
        private const float TimelineRangeEdgeHandleWidth = 12f;
        private const float TimelineDetailsHeight = 72f;
        private const float IssueRowHeight = 116f;
        private const float ValidationReportHeight = 340f;
        private const float SidePanelWidth = 220f;
        private const float SidePanelMinWidth = 200f;
        private const int MaxSceneBindingMarkers = 24;
        private const int DefaultShapeRadiusRaw = 300000;
        private const int DefaultCapsuleHeightRaw = 900000;
        private const int RawPerUnityUnit = 1000000;
        private const int MinShapeRadiusRaw = 1;
        private const int RawPresetSmall = 250000;
        private const int RawPresetMedium = 500000;
        private const int RawPresetLarge = 1000000;
        private const int MaxRadiusSliderRaw = 2000000;
        private const int MaxHeightSliderRaw = 4000000;
        private static readonly object SerializedRefreshRegistrationMarker = new object();
        private static readonly CombatAuthoringShapeKind[] ShapeKindValues =
        {
            CombatAuthoringShapeKind.Sphere,
            CombatAuthoringShapeKind.Capsule,
            CombatAuthoringShapeKind.Aabb,
            CombatAuthoringShapeKind.Sector,
        };

        private readonly List<TimelineRow> _timelineRows = new List<TimelineRow>(64);
        private readonly List<CombatAuthoringIssue> _issueRows = new List<CombatAuthoringIssue>(64);
        private readonly List<SceneMarkerCandidate> _sceneMarkerCandidates = new List<SceneMarkerCandidate>(64);
        private readonly HashSet<string> _markerIdBuffer = new HashSet<string>(StringComparer.Ordinal);
        private readonly TimelineDragState _timelineDrag = new TimelineDragState();

        private CombatActionAuthoringAsset _actionAsset;
        private CombatSceneBindingAsset _sceneBindingAsset;
        private SerializedObject _actionSerialized;
        private SerializedObject _bindingSerialized;
        private CombatAuthoringReport _lastReport;
        private string _lastReportText = string.Empty;
        private CombatAuthoringExportResult _lastExportResult;
        private string _lastExportReportText = string.Empty;
        private CombatAuthoringPreviewReport _lastPreviewReport;
        private string _lastPreviewText = string.Empty;

        private ObjectField _actionField;
        private ObjectField _bindingField;
        private Label _contextLabel;
        private Label _frameLabel;
        private Label _validationLabel;
        private Label _quickActionStatusLabel;
        private SliderInt _frameSlider;
        private VisualElement _toolbarRoot;
        private VisualElement _emptyStateRoot;
        private VisualElement _actionFieldsRoot;
        private VisualElement _bindingFieldsRoot;
        private VisualElement _detailRoot;
        private VisualElement _timelineStripEmptyRoot;
        private ScrollView _timelineStripScroll;
        private VisualElement _timelineStripContent;
        private VisualElement _timelinePlayhead;
        private Label _timelineDragStatus;
        private ListView _timelineList;
        private ListView _issueList;
        private TextField _reportPreview;
        private TextField _previewExplain;
        private int _selectedTimelineRowIndex = -1;
        private bool _ignoreNextTimelineBarClick;
        private bool _finishingTimelineDrag;
        private bool _keyboardShortcutsRegistered;
        private bool _isApplyingLocalSerializedRefresh;
        private int _observedDataRevision = -1;

        [SerializeField]
        private CombatAuthoringWindowMode _mode = CombatAuthoringWindowMode.Inspector;

        private void OnEnable()
        {
            CombatAuthoringSceneState.Changed -= OnSceneStateChanged;
            CombatAuthoringSceneState.Changed += OnSceneStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            CombatAuthoringSceneState.Changed -= OnSceneStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.delayCall -= DelayedSerializedRefresh;
            UnregisterKeyboardShortcuts();
        }

        [MenuItem(AuthoringMenuPath, priority = 300)]
        public static void Open()
        {
            OpenInspectorWindow();
        }

        [MenuItem(TimelineMenuPath, priority = 301)]
        public static void OpenTimeline()
        {
            OpenTimelineWindow();
        }

        [MenuItem(LayoutMenuPath, priority = 302)]
        public static void OpenAuthoringLayout()
        {
            OpenInspectorWindow();
            OpenTimelineWindow();
        }

        private static CombatAuthoringWindow OpenInspectorWindow()
        {
            var window = FindWindow(CombatAuthoringWindowMode.Inspector);
            if (window == null)
            {
                window = CreateInstance<CombatAuthoringWindow>();
                window._mode = CombatAuthoringWindowMode.Inspector;
            }

            window.ConfigureWindowForMode();
            window.Show();
            window.Focus();
            return window;
        }

        private static CombatAuthoringWindow OpenTimelineWindow()
        {
            var window = FindWindow(CombatAuthoringWindowMode.Timeline);
            if (window == null)
            {
                window = CreateInstance<CombatAuthoringWindow>();
                window._mode = CombatAuthoringWindowMode.Timeline;
            }

            window.ConfigureWindowForMode();
            window.Show();
            window.Focus();
            return window;
        }

        private static CombatAuthoringWindow FindWindow(CombatAuthoringWindowMode mode)
        {
            CombatAuthoringWindow[] windows = Resources.FindObjectsOfTypeAll<CombatAuthoringWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null && windows[i]._mode == mode)
                {
                    return windows[i];
                }
            }

            return null;
        }

        private void ConfigureWindowForMode()
        {
            if (_mode == CombatAuthoringWindowMode.Timeline)
            {
                titleContent = new GUIContent("Combat Timeline");
                minSize = new Vector2(900f, 360f);
            }
            else
            {
                titleContent = new GUIContent("Combat Authoring");
                minSize = new Vector2(420f, 620f);
            }
        }

        public void CreateGUI()
        {
            ConfigureWindowForMode();
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 0;
            rootVisualElement.style.paddingRight = 0;
            rootVisualElement.style.paddingTop = 0;
            rootVisualElement.style.paddingBottom = 0;

            if (_mode == CombatAuthoringWindowMode.Timeline)
            {
                CreateTimelineGui();
            }
            else
            {
                CreateInspectorGui();
            }

            RegisterKeyboardShortcuts();
            RegisterTimelineDragRecoveryCallbacks();
            ApplySharedContextToLocal();
            SetActionAsset(_actionAsset);
            SetSceneBindingAsset(_sceneBindingAsset);
            ApplySharedSelectionToLocal(false);
            _observedDataRevision = CombatAuthoringSceneState.DataRevision;
            RefreshValidation();
        }

        private void CreateInspectorGui()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            rootVisualElement.Add(scroll);

            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            content.style.minWidth = 0;
            scroll.Add(content);

            content.Add(CreateContextBar());
            content.Add(CreateToolbar());
            content.Add(CreateEmptyState());
            content.Add(CreateInspectorBody());
            content.Add(CreateIssueDrawer());
        }

        private void CreateTimelineGui()
        {
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.minHeight = 0;
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            rootVisualElement.Add(content);

            content.Add(CreateContextBar());
            content.Add(CreateTimelineBody());
        }

        private VisualElement CreateContextBar()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginBottom = 8;
            root.style.backgroundColor = UiColors.SurfaceAlt;
            root.style.borderBottomLeftRadius = 6;
            root.style.borderBottomRightRadius = 6;
            root.style.borderTopLeftRadius = 6;
            root.style.borderTopRightRadius = 6;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;

            var icon = new Label("\u2694");
            icon.style.fontSize = 16;
            icon.style.marginRight = 6;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            headerRow.Add(icon);

            var titleLabel = new Label(_mode == CombatAuthoringWindowMode.Timeline ? "Combat Timeline" : "Combat Authoring");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = UiColors.TextPrimary;
            headerRow.Add(titleLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            var modeBadge = new Label(EditorApplication.isPlaying ? "\u25B6 Play" : "\u270F Edit");
            modeBadge.style.fontSize = 10;
            modeBadge.style.color = EditorApplication.isPlaying ? UiColors.TimelineActive : UiColors.TextSecondary;
            modeBadge.style.backgroundColor = EditorApplication.isPlaying
                ? new Color(0.82f, 0.3f, 0.24f, 0.15f)
                : new Color(1f, 1f, 1f, 0.05f);
            modeBadge.style.paddingLeft = 8;
            modeBadge.style.paddingRight = 8;
            modeBadge.style.paddingTop = 2;
            modeBadge.style.paddingBottom = 2;
            modeBadge.style.borderBottomLeftRadius = 8;
            modeBadge.style.borderBottomRightRadius = 8;
            modeBadge.style.borderTopLeftRadius = 8;
            modeBadge.style.borderTopRightRadius = 8;
            headerRow.Add(modeBadge);
            root.Add(headerRow);

            var assetRow = new VisualElement();
            assetRow.style.flexDirection = FlexDirection.Row;
            assetRow.style.flexWrap = Wrap.Wrap;
            assetRow.style.alignItems = Align.Center;

            _actionField = CreateCompactObjectField("Action Asset", typeof(CombatActionAuthoringAsset));
            _actionField.RegisterValueChangedCallback(evt => SetActionAsset(evt.newValue as CombatActionAuthoringAsset));
            assetRow.Add(_actionField);

            _bindingField = CreateCompactObjectField("Scene Binding", typeof(CombatSceneBindingAsset));
            _bindingField.RegisterValueChangedCallback(evt => SetSceneBindingAsset(evt.newValue as CombatSceneBindingAsset));
            assetRow.Add(_bindingField);
            root.Add(assetRow);

            _contextLabel = new Label();
            _contextLabel.style.minWidth = 460;
            _contextLabel.style.marginTop = 4;
            _contextLabel.style.whiteSpace = WhiteSpace.Normal;
            _contextLabel.style.fontSize = 11;
            _contextLabel.style.color = UiColors.TextSecondary;
            root.Add(_contextLabel);

            return root;
        }

        private VisualElement CreateToolbar()
        {
            var root = new VisualElement();
            _toolbarRoot = root;
            root.style.marginBottom = 8;

            root.Add(CreateToolbarGroup("Asset",
                CreateButton("使用当前选择", UseSelection),
                CreateButton("创建 Action Asset", CreateActionAsset),
                CreateButton("打开 Timeline", () => OpenTimelineWindow()),
                CreateButton("定位 Action", () => PingObject(_actionAsset)),
                CreateButton("定位 Binding", () => PingObject(_sceneBindingAsset))));

            root.Add(CreateToolbarGroup("创建",
                CreateButton("创建 Binding", CreateBindingAsset),
                CreateButton("从当前场景生成", GenerateBindingFromCurrentScene),
                CreateButton("重连选中对象", RelinkSelectedTransform),
                CreateButton("添加 Hitbox", () => AddShape("Hitbox", "hitboxes")),
                CreateButton("添加 Hurtbox", () => AddShape("Hurtbox", "hurtboxes"))));

            root.Add(CreateToolbarGroup("工具",
                CreateButton("执行验证", RefreshValidation),
                CreateButton("运行 Showcase 预览", RunShowcasePreview),
                CreateButton("清除预览会话", ClearShowcasePreviewSession),
                CreateButton("复制报告", CopyReport),
                CreateButton("导出 JSON", ExportJson),
                CreateButton("复制导出报告", CopyExportReport),
                CreateButton("预览 Explain", PreviewExplain),
                CreateButton("复制 Explain", CopyExplain)));

            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 4;

            _validationLabel = new Label();
            _validationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _validationLabel.style.fontSize = 12;
            statusRow.Add(_validationLabel);

            _quickActionStatusLabel = new Label();
            _quickActionStatusLabel.style.marginLeft = 12;
            _quickActionStatusLabel.style.fontSize = 11;
            _quickActionStatusLabel.style.color = UiColors.TextSecondary;
            _quickActionStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            _quickActionStatusLabel.style.flexGrow = 1;
            statusRow.Add(_quickActionStatusLabel);
            root.Add(statusRow);

            return root;
        }

        private static VisualElement CreateToolbarGroup(string title, params VisualElement[] buttons)
        {
            var group = new VisualElement();
            group.style.marginBottom = 6;

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginBottom = 2;
            group.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            foreach (var button in buttons)
            {
                row.Add(button);
            }

            group.Add(row);
            return group;
        }

        private VisualElement CreateEmptyState()
        {
            _emptyStateRoot = CreatePanel("快速开始");
            _emptyStateRoot.style.marginBottom = 8;

            var hero = new VisualElement();
            hero.style.alignItems = Align.Center;
            hero.style.paddingTop = 16;
            hero.style.paddingBottom = 12;

            var heroIcon = new Label("\u2694");
            heroIcon.style.fontSize = 32;
            heroIcon.style.marginBottom = 8;
            hero.Add(heroIcon);

            var heroTitle = new Label("Combat Authoring");
            heroTitle.style.fontSize = 16;
            heroTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            heroTitle.style.color = UiColors.TextPrimary;
            heroTitle.style.marginBottom = 4;
            hero.Add(heroTitle);

            var heroDesc = new Label("选择或创建 Action Asset 开始编辑战斗动作。Scene Binding 可选，选择后可以校验 marker。");
            heroDesc.style.whiteSpace = WhiteSpace.Normal;
            heroDesc.style.color = UiColors.TextSecondary;
            heroDesc.style.maxWidth = 480;
            heroDesc.style.unityTextAlign = TextAnchor.MiddleCenter;
            hero.Add(heroDesc);
            _emptyStateRoot.Add(hero);

            var stepsRow = new VisualElement();
            stepsRow.style.flexDirection = FlexDirection.Row;
            stepsRow.style.justifyContent = Justify.Center;
            stepsRow.style.flexWrap = Wrap.Wrap;
            stepsRow.style.marginTop = 4;

            stepsRow.Add(CreateStepCard("1", "选择 Action", "从 Project 窗口拖入、使用当前选择或新建资产", CreateButton("创建 Action Asset", CreateActionAsset)));
            stepsRow.Add(CreateStepCard("2", "创建 Binding", "绑定场景对象到 marker", CreateButton("创建 Binding", CreateBindingAsset)));
            stepsRow.Add(CreateStepCard("3", "编辑 & 验证", "添加 Hitbox、调整帧范围、执行验证", CreateButton("执行验证", RefreshValidation)));
            _emptyStateRoot.Add(stepsRow);

            var secondaryActions = new VisualElement();
            secondaryActions.style.flexDirection = FlexDirection.Row;
            secondaryActions.style.flexWrap = Wrap.Wrap;
            secondaryActions.style.justifyContent = Justify.Center;
            secondaryActions.style.marginTop = 8;
            secondaryActions.style.paddingTop = 8;
            secondaryActions.style.borderTopWidth = 1;
            secondaryActions.style.borderTopColor = UiColors.Border;

            secondaryActions.Add(CreateButton("从当前场景生成 Binding", GenerateBindingFromCurrentScene));
            secondaryActions.Add(CreateButton("使用当前选择", UseSelection));
            secondaryActions.Add(CreateButton("重连选中对象", RelinkSelectedTransform));
            secondaryActions.Add(CreateButton("添加 Hitbox", () => AddShape("Hitbox", "hitboxes")));
            secondaryActions.Add(CreateButton("添加 Hurtbox", () => AddShape("Hurtbox", "hurtboxes")));
            secondaryActions.Add(CreateButton("运行 Showcase 预览", RunShowcasePreview));
            secondaryActions.Add(CreateButton("导出 JSON", ExportJson));
            secondaryActions.Add(CreateButton("预览 Explain", PreviewExplain));
            _emptyStateRoot.Add(secondaryActions);

            return _emptyStateRoot;
        }

        private static VisualElement CreateStepCard(string number, string title, string desc, VisualElement button)
        {
            var card = new VisualElement();
            card.style.width = 200;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.backgroundColor = UiColors.SurfaceAlt;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            var num = new Label(number);
            num.style.fontSize = 14;
            num.style.unityFontStyleAndWeight = FontStyle.Bold;
            num.style.color = UiColors.TimelineActive;
            num.style.marginRight = 6;
            header.Add(num);
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = UiColors.TextPrimary;
            header.Add(titleLabel);
            card.Add(header);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 10;
            descLabel.style.color = UiColors.TextSecondary;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 6;
            card.Add(descLabel);
            card.Add(button);
            return card;
        }

        private VisualElement CreateInspectorBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;

            var fields = CreatePanel("\u2699 基础字段");
            fields.style.marginBottom = 8;
            fields.Add(SectionTitle("Frame"));
            fields.Add(CreateFrameScrubber());
            fields.Add(SectionTitle("Action"));
            _actionFieldsRoot = new VisualElement();
            fields.Add(_actionFieldsRoot);
            fields.Add(SectionTitle("Scene Binding"));
            _bindingFieldsRoot = new VisualElement();
            fields.Add(_bindingFieldsRoot);
            body.Add(fields);

            var selected = CreatePanel("\u25B6 选中项");
            selected.style.marginBottom = 8;
            _detailRoot = new ScrollView(ScrollViewMode.Vertical);
            _detailRoot.style.flexGrow = 1;
            _detailRoot.style.minHeight = 160;
            _detailRoot.style.maxHeight = 420;
            selected.Add(_detailRoot);
            body.Add(selected);

            return body;
        }

        private VisualElement CreateTimelineBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            body.style.minWidth = 0;

            var center = CreatePanel("\u23F1 Timeline");
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            center.style.minWidth = 0;
            center.style.minHeight = 0;
            center.style.overflow = Overflow.Hidden;
            center.Add(CreateFrameScrubber());
            center.Add(CreateTimelineStripView());
            center.Add(SectionTitle("详细信息"));
            _timelineList = new ListView(_timelineRows, TimelineRowHeight, MakeTimelineRow, BindTimelineRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _timelineList.style.height = TimelineDetailsHeight;
            _timelineList.style.flexGrow = 0;
            _timelineList.style.flexShrink = 0;
            _timelineList.style.minHeight = 72;
            _timelineList.style.borderBottomLeftRadius = 4;
            _timelineList.style.borderBottomRightRadius = 4;
            _timelineList.style.borderTopLeftRadius = 4;
            _timelineList.style.borderTopRightRadius = 4;
            _timelineList.selectionChanged += OnTimelineSelectionChanged;
            _timelineList.selectedIndicesChanged += OnTimelineSelectedIndicesChanged;
            center.Add(_timelineList);
            body.Add(center);

            return body;
        }

        private VisualElement CreateFrameScrubber()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginBottom = 6;

            _frameSlider = new SliderInt("Frame", 0, 0);
            _frameSlider.tooltip = Tooltip("Frame");
            _frameSlider.style.flexGrow = 1;
            _frameSlider.style.flexShrink = 1;
            _frameSlider.style.minWidth = 0;
            _frameSlider.RegisterValueChangedCallback(evt =>
            {
                if (_frameLabel != null)
                {
                    _frameLabel.text = "当前帧 " + evt.newValue;
                }

                CombatAuthoringSceneState.SetFrame(evt.newValue);
            });
            root.Add(_frameSlider);

            _frameLabel = new Label("当前帧 0");
            _frameLabel.style.width = 96;
            _frameLabel.style.flexShrink = 0;
            _frameLabel.style.marginLeft = 8;
            root.Add(_frameLabel);

            return root;
        }

        private VisualElement CreateTimelineStripView()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexShrink = 1;
            root.style.minHeight = 100;
            root.style.marginBottom = 8;

            _timelineStripEmptyRoot = new HelpBox("请选择 Action Asset 后查看横向时间轴。", HelpBoxMessageType.Info);
            _timelineStripEmptyRoot.style.marginBottom = 4;
            root.Add(_timelineStripEmptyRoot);

            _timelineDragStatus = new Label("拖动条块主体移动范围；拖动左右边缘调整 Start / End。");
            _timelineDragStatus.tooltip = Tooltip("Timeline Range Dragging");
            _timelineDragStatus.style.marginBottom = 4;
            _timelineDragStatus.style.fontSize = 11;
            _timelineDragStatus.style.color = UiColors.TextSecondary;
            _timelineDragStatus.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_timelineDragStatus);

            _timelineStripScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _timelineStripScroll.style.flexGrow = 1;
            _timelineStripScroll.style.minHeight = 0;
            _timelineStripScroll.style.borderBottomColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderLeftColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderRightColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderTopColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderBottomWidth = 1;
            _timelineStripScroll.style.borderLeftWidth = 1;
            _timelineStripScroll.style.borderRightWidth = 1;
            _timelineStripScroll.style.borderTopWidth = 1;

            _timelineStripContent = new VisualElement();
            _timelineStripContent.style.position = Position.Relative;
            _timelineStripScroll.Add(_timelineStripContent);
            root.Add(_timelineStripScroll);

            return root;
        }

        private VisualElement CreateIssueDrawer()
        {
            var drawer = CreatePanel("Validation Report");
            drawer.tooltip = Tooltip("Validation Report");
            drawer.style.height = _mode == CombatAuthoringWindowMode.Timeline ? 0 : ValidationReportHeight;
            drawer.style.display = _mode == CombatAuthoringWindowMode.Timeline ? DisplayStyle.None : DisplayStyle.Flex;
            drawer.style.marginTop = 8;

            var split = new VisualElement();
            split.style.flexDirection = _mode == CombatAuthoringWindowMode.Inspector ? FlexDirection.Column : FlexDirection.Row;
            split.style.flexGrow = 1;
            split.style.minHeight = 0;

            var issueColumn = new VisualElement();
            issueColumn.style.flexGrow = _mode == CombatAuthoringWindowMode.Inspector ? 0 : 1;
            issueColumn.style.flexShrink = 0;
            if (_mode == CombatAuthoringWindowMode.Inspector)
            {
                issueColumn.style.height = IssueRowHeight + 42f;
            }
            else
            {
                issueColumn.style.flexBasis = 0;
            }
            issueColumn.style.minWidth = 0;
            issueColumn.style.marginRight = _mode == CombatAuthoringWindowMode.Inspector ? 0 : 8;
            issueColumn.style.marginBottom = _mode == CombatAuthoringWindowMode.Inspector ? 8 : 0;
            issueColumn.Add(SectionTitle("Issues"));

            _issueList = new ListView(_issueRows, IssueRowHeight, MakeIssueRow, BindIssueRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _issueList.style.flexGrow = 1;
            _issueList.style.height = _mode == CombatAuthoringWindowMode.Inspector ? IssueRowHeight + 12f : StyleKeyword.Auto;
            _issueList.style.minHeight = _mode == CombatAuthoringWindowMode.Inspector ? IssueRowHeight + 12f : 0f;
            issueColumn.Add(_issueList);
            split.Add(issueColumn);

            var previewColumn = new VisualElement();
            if (_mode == CombatAuthoringWindowMode.Inspector)
            {
                previewColumn.style.width = StyleKeyword.Auto;
                previewColumn.style.flexGrow = 1;
                previewColumn.style.minHeight = 0;
            }
            else
            {
                previewColumn.style.width = 340;
            }

            previewColumn.style.minWidth = 0;
            previewColumn.style.flexShrink = 0;
            previewColumn.Add(SectionTitle("Report Preview"));

            _reportPreview = new TextField
            {
                multiline = true,
                isReadOnly = true,
            };
            _reportPreview.tooltip = Tooltip("Report Preview");
            _reportPreview.style.flexGrow = 1;
            _reportPreview.style.minHeight = 0;
            previewColumn.Add(_reportPreview);

            previewColumn.Add(SectionTitle("Preview Explain"));
            _previewExplain = new TextField
            {
                multiline = true,
                isReadOnly = true,
            };
            _previewExplain.tooltip = Tooltip("Preview Explain");
            _previewExplain.style.flexGrow = 1;
            _previewExplain.style.minHeight = 0;
            _previewExplain.value = "点击“预览 Explain”生成当前帧 query / candidate / resolve 解释。";
            previewColumn.Add(_previewExplain);
            split.Add(previewColumn);

            drawer.Add(split);
            return drawer;
        }

        private void SetActionAsset(CombatActionAuthoringAsset asset)
        {
            _actionAsset = asset;
            _actionSerialized = asset == null ? null : new SerializedObject(asset);

            if (_actionField != null && _actionField.value != asset)
            {
                _actionField.SetValueWithoutNotify(asset);
            }

            RebuildActionFields();
            RefreshTimeline();
            RefreshContext();
            RefreshEmptyState();
            CombatAuthoringSceneState.SetContext(_actionAsset, _sceneBindingAsset);
            RefreshValidation();
        }

        private void SetSceneBindingAsset(CombatSceneBindingAsset asset)
        {
            _sceneBindingAsset = asset;
            _bindingSerialized = asset == null ? null : new SerializedObject(asset);

            if (_bindingField != null && _bindingField.value != asset)
            {
                _bindingField.SetValueWithoutNotify(asset);
            }

            RebuildBindingFields();
            RefreshContext();
            RefreshEmptyState();
            CombatAuthoringSceneState.SetContext(_actionAsset, _sceneBindingAsset);
            RefreshValidation();
        }

        private void RebuildActionFields()
        {
            if (_actionFieldsRoot == null)
            {
                return;
            }

            _actionFieldsRoot.Clear();
            if (_actionSerialized == null)
            {
                _actionFieldsRoot.Add(new HelpBox("请选择或创建 CombatActionAuthoringAsset。", HelpBoxMessageType.Info));
                return;
            }

            AddActionIntField(_actionFieldsRoot, "totalFrames", "Total Frames", 1);
            AddActionIntField(_actionFieldsRoot, "actionId", "Action Id", 1);
            AddActionTextField(_actionFieldsRoot, "schemaVersion", "Schema Version");
            AddActionFrameRangeField(_actionFieldsRoot, "startup", "Startup");
            AddActionFrameRangeField(_actionFieldsRoot, "active", "Active");
            AddActionFrameRangeField(_actionFieldsRoot, "recovery", "Recovery");
        }

        private void RebuildBindingFields()
        {
            if (_bindingFieldsRoot == null)
            {
                return;
            }

            _bindingFieldsRoot.Clear();
            if (_bindingSerialized == null)
            {
                _bindingFieldsRoot.Add(new HelpBox("Scene Binding 可选；未设置时 marker 校验会按缺失处理。", HelpBoxMessageType.Info));
                return;
            }

            AddProperty(_bindingFieldsRoot, _bindingSerialized, "sceneGuid", "Scene Guid");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "bindingProfileId", "Binding Profile Id");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "actors", "Actors");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "markers", "Markers");
            _bindingFieldsRoot.Bind(_bindingSerialized);
            RegisterSerializedRefresh(_bindingFieldsRoot);
        }

        private void AddActionIntField(VisualElement root, string propertyName, string label, int minValue)
        {
            SerializedProperty property = _actionSerialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.marginBottom = 6;

            var title = new Label(label);
            title.tooltip = Tooltip(label);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = UiColors.TextSecondary;
            title.style.marginBottom = 2;
            group.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;

            var field = new IntegerField()
            {
                value = Math.Max(minValue, property.intValue),
                isDelayed = true,
                tooltip = Tooltip(label),
            };
            ConfigureWideValueField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                SetActionIntValue(propertyName, minValue, evt.newValue);
            });
            row.Add(field);
            row.Add(CreateMiniButton("-", Tooltip("Frame Step Down"), () => SetActionIntValue(propertyName, minValue, property.intValue - 1)));
            row.Add(CreateMiniButton("+", Tooltip("Frame Step Up"), () => SetActionIntValue(propertyName, minValue, property.intValue + 1)));
            if (string.Equals(propertyName, "totalFrames", StringComparison.Ordinal))
            {
                row.Add(CreateMiniButton("适配", "把 Total Frames 调整到可以包含当前 Startup / Active / Recovery / Shape / Trace 的最大结束帧。", () =>
                {
                    SetActionIntValue(propertyName, minValue, GetRequiredTotalFrames());
                }));
            }

            group.Add(row);
            root.Add(group);
        }

        private void SetActionIntValue(string propertyName, int minValue, int requestedValue)
        {
            int value = Math.Max(minValue, requestedValue);
            MutateActionProperty("Edit Combat Action " + propertyName, serialized =>
            {
                SerializedProperty target = serialized.FindProperty(propertyName);
                if (target != null)
                {
                    target.intValue = value;
                }
            });
        }

        private void AddActionTextField(VisualElement root, string propertyName, string label)
        {
            SerializedProperty property = _actionSerialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new TextField(label)
            {
                value = property.stringValue ?? string.Empty,
                isDelayed = true,
                tooltip = Tooltip(label),
            };
            ConfigureCompactField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                string value = evt.newValue ?? string.Empty;
                MutateActionProperty("Edit Combat Action " + propertyName, serialized =>
                {
                    SerializedProperty target = serialized.FindProperty(propertyName);
                    if (target != null)
                    {
                        target.stringValue = value;
                    }
                });
            });
            root.Add(field);
        }

        private void AddActionFrameRangeField(VisualElement root, string propertyName, string label)
        {
            SerializedProperty range = _actionSerialized.FindProperty(propertyName);
            SerializedProperty start = range?.FindPropertyRelative("startFrame");
            SerializedProperty end = range?.FindPropertyRelative("endFrame");
            if (range == null || start == null || end == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName + ".startFrame / endFrame", HelpBoxMessageType.Warning));
                return;
            }

            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.marginBottom = 4;

            var title = new Label(label);
            title.tooltip = Tooltip(label);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = UiColors.TextSecondary;
            group.Add(title);

            group.Add(CreateActionFrameRangeRow(propertyName, "Start", start.intValue, true));
            group.Add(CreateActionFrameRangeRow(propertyName, "End", end.intValue, false));
            root.Add(group);
        }

        private VisualElement CreateActionFrameRangeRow(string propertyName, string label, int value, bool setStart)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.flexWrap = Wrap.Wrap;
            line.style.marginBottom = 2;

            var rowLabel = new Label(label);
            rowLabel.tooltip = Tooltip(setStart ? "Frame Start" : "Frame End");
            rowLabel.style.width = 42;
            rowLabel.style.flexShrink = 0;
            rowLabel.style.color = UiColors.TextSecondary;
            line.Add(rowLabel);

            var field = new IntegerField()
            {
                value = value,
                isDelayed = true,
                tooltip = Tooltip(setStart ? "Frame Start" : "Frame End"),
            };
            ConfigureWideValueField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                SetActionFrameRangeValue(propertyName, setStart, evt.newValue);
            });
            line.Add(field);

            line.Add(CreateMiniButton("-", Tooltip("Frame Step Down"), () => SetActionFrameRangeValue(propertyName, setStart, value - 1)));
            line.Add(CreateMiniButton("+", Tooltip("Frame Step Up"), () => SetActionFrameRangeValue(propertyName, setStart, value + 1)));
            return line;
        }

        private void SetActionFrameRangeValue(string propertyName, bool setStart, int requestedValue)
        {
            MutateActionProperty("Edit Combat Action Frame Range", serialized =>
            {
                SerializedProperty range = serialized.FindProperty(propertyName);
                SerializedProperty start = range?.FindPropertyRelative("startFrame");
                SerializedProperty end = range?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                if (setStart)
                {
                    start.intValue = Math.Max(0, requestedValue);
                    if (!IsEmptyFrameRange(start.intValue, end.intValue) && start.intValue > end.intValue)
                    {
                        end.intValue = start.intValue;
                    }
                }
                else
                {
                    end.intValue = Math.Max(-1, requestedValue);
                    if (!IsEmptyFrameRange(start.intValue, end.intValue) && end.intValue < start.intValue)
                    {
                        start.intValue = end.intValue;
                    }
                }
            });
        }

        private void MutateActionProperty(string undoName, Action<SerializedObject> mutate)
        {
            if (_actionAsset == null || _actionSerialized == null || mutate == null)
            {
                return;
            }

            _actionSerialized.Update();
            Undo.RecordObject(_actionAsset, undoName);
            mutate(_actionSerialized);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();

            RefreshTimeline();
            RefreshContext();
            RefreshValidation();
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            Repaint();
        }

        private static bool IsEmptyFrameRange(int startFrame, int endFrame)
        {
            return endFrame < startFrame;
        }

        private static void ConfigureCompactField<T>(BaseField<T> field)
        {
            field.style.marginBottom = 3;
            field.style.minWidth = 0;
            field.labelElement.style.minWidth = 78;
            field.labelElement.style.width = 78;
            RegisterInputFocusAssist(field);
        }

        private static void ConfigureWideValueField<T>(BaseField<T> field)
        {
            field.style.minWidth = 96;
            field.style.width = 112;
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.marginRight = 4;
            field.style.marginBottom = 2;
            field.labelElement.style.minWidth = 0;
            field.labelElement.style.width = 0;
            field.labelElement.style.maxWidth = 0;
            field.labelElement.style.opacity = 0;
            field.labelElement.pickingMode = PickingMode.Ignore;
            RegisterInputFocusAssist(field);
        }

        private static void RegisterInputFocusAssist<T>(BaseField<T> field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<PointerDownEvent>(_ =>
            {
                field.schedule.Execute(() => FocusFieldInput(field));
            }, TrickleDown.TrickleDown);
        }

        private static void FocusFieldInput<T>(BaseField<T> field)
        {
            if (field == null || field.panel == null || !field.enabledInHierarchy)
            {
                return;
            }

            VisualElement input = field.Q<VisualElement>(className: "unity-base-text-field__input")
                ?? field.Q<VisualElement>(className: "unity-text-input");
            if (input != null && input.focusable && input.enabledInHierarchy)
            {
                input.Focus();
                return;
            }

            field.Focus();
        }

        private int GetRequiredTotalFrames()
        {
            int maxEndFrame = 0;
            if (_actionAsset == null)
            {
                return 1;
            }

            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Startup));
            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Active));
            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Recovery));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxShapeEndFrame(_actionAsset.Hitboxes));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxShapeEndFrame(_actionAsset.Hurtboxes));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxTraceEndFrame(_actionAsset.WeaponTraces));
            return Math.Max(1, maxEndFrame + 1);
        }

        private static int GetRangeEnd(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? 0 : Math.Max(0, range.EndFrame);
        }

        private static int GetMaxShapeEndFrame(CombatShapeAuthoringData[] shapes)
        {
            int maxEndFrame = 0;
            if (shapes == null)
            {
                return maxEndFrame;
            }

            for (int i = 0; i < shapes.Length; i++)
            {
                maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(shapes[i].FrameRange));
            }

            return maxEndFrame;
        }

        private static int GetMaxTraceEndFrame(CombatWeaponTraceAuthoringData[] traces)
        {
            int maxEndFrame = 0;
            if (traces == null)
            {
                return maxEndFrame;
            }

            for (int i = 0; i < traces.Length; i++)
            {
                maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(traces[i].FrameRange));
            }

            return maxEndFrame;
        }

        private void RefreshTimeline(bool refreshDetail = true)
        {
            bool hadSelection = _selectedTimelineRowIndex >= 0 && _selectedTimelineRowIndex < _timelineRows.Count;
            TimelineRow previousSelection = hadSelection ? _timelineRows[_selectedTimelineRowIndex] : default;

            _timelineRows.Clear();
            if (_actionAsset != null)
            {
                _timelineRows.Add(new TimelineRow("Action", "Startup", 0, _actionAsset.Startup, "startup", TimelineRowKind.ActionProperty));
                _timelineRows.Add(new TimelineRow("Action", "Active", 0, _actionAsset.Active, "active", TimelineRowKind.ActionProperty));
                _timelineRows.Add(new TimelineRow("Action", "Recovery", 0, _actionAsset.Recovery, "recovery", TimelineRowKind.ActionProperty));
                AddShapeRows("Hitbox", _actionAsset.Hitboxes, "hitboxes");
                AddShapeRows("Hurtbox", _actionAsset.Hurtboxes, "hurtboxes");
                AddTraceRows();
            }

            if (_timelineList != null)
            {
                _timelineList.RefreshItems();
            }

            _selectedTimelineRowIndex = hadSelection ? FindTimelineRow(previousSelection) : -1;
            RefreshTimelineStrip();
            RefreshFrameSlider();
            if (refreshDetail)
            {
                RefreshDetail(_selectedTimelineRowIndex >= 0 ? _timelineRows[_selectedTimelineRowIndex] : (TimelineRow?)null);
            }
        }

        private void RegisterKeyboardShortcuts()
        {
            if (_keyboardShortcutsRegistered)
            {
                return;
            }

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _keyboardShortcutsRegistered = true;
        }

        private void UnregisterKeyboardShortcuts()
        {
            if (!_keyboardShortcutsRegistered)
            {
                return;
            }

            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _keyboardShortcutsRegistered = false;
        }

        private void RegisterTimelineDragRecoveryCallbacks()
        {
            rootVisualElement.UnregisterCallback<PointerUpEvent>(OnTimelineDragRootPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<PointerCancelEvent>(OnTimelineDragRootPointerCancel, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnTimelineDragRootPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerCancelEvent>(OnTimelineDragRootPointerCancel, TrickleDown.TrickleDown);
        }

        private void OnTimelineDragRootPointerUp(PointerUpEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
            evt.StopPropagation();
        }

        private void OnTimelineDragRootPointerCancel(PointerCancelEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: true);
            evt.StopPropagation();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape && _timelineDrag.IsDragging)
            {
                CompleteTimelineRangeDrag(cancel: true);
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
            {
                return;
            }

            if (IsKeyboardEventFromEditableField(evt))
            {
                return;
            }

            TryDeleteSelectedShape();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void OnUndoRedoPerformed()
        {
            if (HasValidSerializedTarget(_actionSerialized))
            {
                _actionSerialized.Update();
            }

            RebuildActionFields();
            RefreshTimeline();
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
        }

        private void RefreshTimelineStrip()
        {
            if (_timelineStripContent == null || _timelineStripEmptyRoot == null || _timelineStripScroll == null)
            {
                return;
            }

            _timelineStripContent.Clear();
            _timelinePlayhead = null;

            bool hasAction = _actionAsset != null;
            _timelineStripEmptyRoot.style.display = hasAction ? DisplayStyle.None : DisplayStyle.Flex;
            _timelineStripScroll.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            if (_timelineDragStatus != null)
            {
                _timelineDragStatus.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!hasAction)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            float trackWidth = Math.Max(TimelineStripMinTrackWidth, totalFrames * TimelineStripPixelsPerFrame);
            float contentWidth = TimelineStripLabelWidth + trackWidth;
            float contentHeight = TimelineStripHeaderHeight + Math.Max(1, _timelineRows.Count) * TimelineStripLaneHeight;
            _timelineStripContent.style.width = contentWidth;
            _timelineStripContent.style.height = contentHeight;

            AddTimelineRuler(totalFrames, trackWidth);
            if (_timelineRows.Count == 0)
            {
                AddTimelineEmptyLane(trackWidth);
            }
            else
            {
                for (int i = 0; i < _timelineRows.Count; i++)
                {
                    AddTimelineLane(i, totalFrames, trackWidth);
                }
            }

            _timelinePlayhead = new VisualElement();
            _timelinePlayhead.pickingMode = PickingMode.Ignore;
            _timelinePlayhead.style.position = Position.Absolute;
            _timelinePlayhead.style.top = 0;
            _timelinePlayhead.style.width = 2;
            _timelinePlayhead.style.height = contentHeight;
            _timelinePlayhead.style.backgroundColor = UiColors.TimelinePlayhead;
            _timelineStripContent.Add(_timelinePlayhead);
            UpdateTimelinePlayhead();
        }

        private void AddTimelineRuler(int totalFrames, float trackWidth)
        {
            var label = CreateTimelineCell("Frame", 0, 0, TimelineStripLabelWidth, TimelineStripHeaderHeight, UiColors.TimelineHeaderBg);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _timelineStripContent.Add(label);

            var track = CreateTimelineBlock(TimelineStripLabelWidth, 0, trackWidth, TimelineStripHeaderHeight, UiColors.SurfaceDeep);
            _timelineStripContent.Add(track);

            int tickStep = GetTimelineTickStep(totalFrames);
            for (int frame = 0; frame < totalFrames; frame += tickStep)
            {
                AddTimelineTick(frame, totalFrames, trackWidth, true);
            }

            if ((totalFrames - 1) % tickStep != 0)
            {
                AddTimelineTick(totalFrames - 1, totalFrames, trackWidth, true);
            }
        }

        private void AddTimelineTick(int frame, int totalFrames, float trackWidth, bool withLabel)
        {
            float x = TimelineStripLabelWidth + FrameToTimelineX(frame, totalFrames, trackWidth);
            var tick = CreateTimelineBlock(x, 0, 1, TimelineStripHeaderHeight, UiColors.TimelineTickLine);
            tick.pickingMode = PickingMode.Ignore;
            _timelineStripContent.Add(tick);

            if (!withLabel)
            {
                return;
            }

            var label = new Label(frame.ToString());
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = x + 3;
            label.style.top = 6;
            label.style.width = 46;
            label.style.height = 18;
            label.style.fontSize = 10;
            label.style.color = UiColors.TimelineTickLabel;
            _timelineStripContent.Add(label);
        }

        private void AddTimelineEmptyLane(float trackWidth)
        {
            float y = TimelineStripHeaderHeight;
            _timelineStripContent.Add(CreateTimelineCell("Timeline", 0, y, TimelineStripLabelWidth, TimelineStripLaneHeight, UiColors.TimelineLabelBg));
            _timelineStripContent.Add(CreateTimelineCell("当前 Action 没有 Timeline 条目。", TimelineStripLabelWidth, y, trackWidth, TimelineStripLaneHeight, UiColors.SurfaceDeep));
        }

        private void AddTimelineLane(int index, int totalFrames, float trackWidth)
        {
            TimelineRow row = _timelineRows[index];
            float y = TimelineStripHeaderHeight + index * TimelineStripLaneHeight;
            Color laneColor = index % 2 == 0 ? UiColors.TimelineLaneEven : UiColors.TimelineLaneOdd;

            string rowLabel = row.Section + " / " + row.Label;
            var header = CreateTimelineCell(rowLabel, 0, y, TimelineStripLabelWidth, TimelineStripLaneHeight, UiColors.TimelineLabelBg);
            RegisterTimelineRowClick(header, index);
            _timelineStripContent.Add(header);

            var lane = CreateTimelineBlock(TimelineStripLabelWidth, y, trackWidth, TimelineStripLaneHeight, laneColor);
            RegisterTimelineRowClick(lane, index);
            _timelineStripContent.Add(lane);

            int minorStep = GetTimelineTickStep(totalFrames);
            for (int frame = 0; frame < totalFrames; frame += minorStep)
            {
                float tickX = TimelineStripLabelWidth + FrameToTimelineX(frame, totalFrames, trackWidth);
                var grid = CreateTimelineBlock(tickX, y, 1, TimelineStripLaneHeight, UiColors.TimelineGridLine);
                grid.pickingMode = PickingMode.Ignore;
                _timelineStripContent.Add(grid);
            }

            if (row.FrameRange.IsEmpty)
            {
                var emptyLabel = new Label("empty");
                emptyLabel.pickingMode = PickingMode.Ignore;
                emptyLabel.style.position = Position.Absolute;
                emptyLabel.style.left = TimelineStripLabelWidth + 8;
                emptyLabel.style.top = y + 6;
                emptyLabel.style.width = 80;
                emptyLabel.style.height = 18;
                emptyLabel.style.fontSize = 10;
                emptyLabel.style.color = UiColors.TimelineEmptyLaneText;
                _timelineStripContent.Add(emptyLabel);
                return;
            }

            int startFrame = Math.Max(0, Math.Min(row.FrameRange.StartFrame, totalFrames - 1));
            int endFrame = Math.Max(startFrame, Math.Min(row.FrameRange.EndFrame, totalFrames - 1));
            float x = TimelineStripLabelWidth + FrameToTimelineX(startFrame, totalFrames, trackWidth);
            float width = Math.Max(4f, (endFrame - startFrame + 1) * GetTimelineFrameWidth(totalFrames, trackWidth));
            var bar = CreateTimelineBlock(x, y + 5, width, TimelineStripLaneHeight - 10, GetTimelineBarColor(row));
            bar.style.borderBottomLeftRadius = 3;
            bar.style.borderBottomRightRadius = 3;
            bar.style.borderTopLeftRadius = 3;
            bar.style.borderTopRightRadius = 3;
            bar.tooltip = BuildTimelineTooltip(row);
            bool draggable = IsTimelineRangeDraggable(row);
            var leftHandle = CreateTimelineRangeHandle(true);
            var rightHandle = CreateTimelineRangeHandle(false);
            leftHandle.style.display = draggable ? DisplayStyle.Flex : DisplayStyle.None;
            rightHandle.style.display = draggable ? DisplayStyle.Flex : DisplayStyle.None;
            bar.Add(leftHandle);
            bar.Add(rightHandle);

            if (index == _selectedTimelineRowIndex)
            {
                bar.style.borderBottomColor = UiColors.SelectionBorder;
                bar.style.borderLeftColor = UiColors.SelectionBorder;
                bar.style.borderRightColor = UiColors.SelectionBorder;
                bar.style.borderTopColor = UiColors.SelectionBorder;
                bar.style.borderBottomWidth = 1;
                bar.style.borderLeftWidth = 1;
                bar.style.borderRightWidth = 1;
                bar.style.borderTopWidth = 1;
                SetTimelineHandleOpacity(leftHandle, rightHandle, 1f);
            }
            else
            {
                SetTimelineHandleOpacity(leftHandle, rightHandle, 0.4f);
            }

            bar.RegisterCallback<ClickEvent>(OnTimelineStripBarClicked);
            var barLabel = new Label(FormatRange(row.FrameRange));
            barLabel.pickingMode = PickingMode.Ignore;
            barLabel.style.position = Position.Absolute;
            barLabel.style.left = x + 5;
            barLabel.style.top = y + 7;
            barLabel.style.width = Math.Max(36f, width - 10f);
            barLabel.style.height = 16;
            barLabel.style.fontSize = 10;
            barLabel.style.color = UiColors.SelectionBorder;
            bar.userData = new TimelineBarData(index, barLabel, leftHandle, rightHandle, totalFrames, trackWidth);
            if (draggable)
            {
                RegisterTimelineBarDragCallbacks(bar);
            }

            _timelineStripContent.Add(bar);
            _timelineStripContent.Add(barLabel);
        }

        private void RegisterTimelineRowClick(VisualElement element, int index)
        {
            element.userData = index;
            element.RegisterCallback<ClickEvent>(OnTimelineStripRowClicked);
        }

        private void OnTimelineStripRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private void OnTimelineStripBarClicked(ClickEvent evt)
        {
            if (_ignoreNextTimelineBarClick)
            {
                _ignoreNextTimelineBarClick = false;
                evt.StopPropagation();
                return;
            }

            if (evt.currentTarget is VisualElement element && TryGetTimelineBarIndex(element, out int index))
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private static bool TryGetTimelineBarIndex(VisualElement element, out int index)
        {
            if (element.userData is TimelineBarData data)
            {
                index = data.RowIndex;
                return true;
            }

            if (element.userData is int legacyIndex)
            {
                index = legacyIndex;
                return true;
            }

            index = -1;
            return false;
        }

        private static bool TryGetTimelineBarData(VisualElement element, out TimelineBarData data)
        {
            data = element.userData as TimelineBarData;
            return data != null;
        }

        private void RegisterTimelineBarDragCallbacks(VisualElement bar)
        {
            bar.RegisterCallback<PointerEnterEvent>(OnTimelineStripBarPointerEnter);
            bar.RegisterCallback<PointerLeaveEvent>(OnTimelineStripBarPointerLeave);
            bar.RegisterCallback<PointerDownEvent>(OnTimelineStripBarPointerDown);
            bar.RegisterCallback<PointerMoveEvent>(OnTimelineStripBarPointerMove);
            bar.RegisterCallback<PointerUpEvent>(OnTimelineStripBarPointerUp);
            bar.RegisterCallback<PointerCancelEvent>(OnTimelineStripBarPointerCancel);
            bar.RegisterCallback<PointerCaptureOutEvent>(OnTimelineStripBarPointerCaptureOut);
        }

        private void OnTimelineStripBarPointerEnter(PointerEnterEvent evt)
        {
            if (evt.currentTarget is VisualElement bar && TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, 1f);
            }
        }

        private void OnTimelineStripBarPointerLeave(PointerLeaveEvent evt)
        {
            if (_timelineDrag.IsDragging)
            {
                return;
            }

            if (evt.currentTarget is VisualElement bar && TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                float opacity = data.RowIndex == _selectedTimelineRowIndex ? 1f : 0.4f;
                SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, opacity);
                SetTimelineDragStatus("拖动条块主体移动范围；拖动左右边缘调整 Start / End。", UiColors.TextSecondary);
            }
        }

        private void OnTimelineStripBarPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0
                || _actionAsset == null
                || _timelineStripContent == null
                || !(evt.currentTarget is VisualElement bar)
                || !TryGetTimelineBarData(bar, out TimelineBarData data)
                || data.RowIndex < 0
                || data.RowIndex >= _timelineRows.Count)
            {
                return;
            }

            TimelineRow row = _timelineRows[data.RowIndex];
            if (!IsTimelineRangeDraggable(row))
            {
                return;
            }

            int maxFrame = GetMaxActionFrame();
            int startFrame = Clamp(row.FrameRange.StartFrame, 0, maxFrame);
            int endFrame = Clamp(row.FrameRange.EndFrame, startFrame, maxFrame);
            if (endFrame < startFrame)
            {
                return;
            }

            TimelineDragMode mode = GetTimelineDragMode(bar, evt.position);
            int pointerFrame = PanelPositionToTimelineFrame(evt.position, data.TotalFrames, data.TrackWidth);
            _timelineDrag.Begin(data.RowIndex, row, mode, startFrame, endFrame, pointerFrame, data.TotalFrames, data.TrackWidth, evt.pointerId, bar, data.RangeLabel, data.LeftHandle, data.RightHandle);

            SelectTimelineRowForTimelineDrag(data.RowIndex, bar, data);
            bar.CapturePointer(evt.pointerId);
            UpdateTimelineDragPreview(startFrame, endFrame);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerMove(PointerMoveEvent evt)
        {
            if (!(evt.currentTarget is VisualElement bar) || !TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                return;
            }

            if (!_timelineDrag.IsDragging)
            {
                TimelineDragMode hoverMode = GetTimelineDragMode(bar, evt.position);
                SetTimelineDragStatus(TimelineDragVerb(hoverMode) + "：" + FormatRange(_timelineRows[data.RowIndex].FrameRange), UiColors.TextSecondary);
                return;
            }

            if (_timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            int targetFrame = PanelPositionToTimelineFrame(evt.position, _timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            CalculateTimelineDragRange(_timelineDrag.Mode, _timelineDrag.OriginalStartFrame, _timelineDrag.OriginalEndFrame, _timelineDrag.PointerStartFrame, targetFrame, GetMaxActionFrame(), out int previewStart, out int previewEnd);
            UpdateTimelineDragPreview(previewStart, previewEnd);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerUp(PointerUpEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
            evt.StopPropagation();
        }

        private void SuppressNextTimelineBarClick()
        {
            _ignoreNextTimelineBarClick = true;
            rootVisualElement.schedule.Execute(() => _ignoreNextTimelineBarClick = false).ExecuteLater(250);
        }

        private void OnTimelineStripBarPointerCancel(PointerCancelEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: true);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_finishingTimelineDrag || !_timelineDrag.IsDragging || evt.currentTarget != _timelineDrag.Bar)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
        }

        private void CompleteTimelineRangeDrag(bool cancel)
        {
            bool changed = _timelineDrag.HasPreviewChange;
            TimelineRow row = _timelineDrag.Row;
            int rowIndex = _timelineDrag.RowIndex;
            int nextStart = _timelineDrag.PreviewStartFrame;
            int nextEnd = _timelineDrag.PreviewEndFrame;
            int originalStart = _timelineDrag.OriginalStartFrame;
            int originalEnd = _timelineDrag.OriginalEndFrame;
            int pointerId = _timelineDrag.PointerId;
            VisualElement capturedBar = _timelineDrag.Bar;

            _finishingTimelineDrag = true;
            if (capturedBar != null && capturedBar.HasPointerCapture(pointerId))
            {
                capturedBar.ReleasePointer(pointerId);
            }

            _timelineDrag.Reset();
            _finishingTimelineDrag = false;

            if (cancel)
            {
                RefreshTimelineStrip();
                CombatAuthoringSceneState.SetFrame(originalStart);
                SceneView.RepaintAll();
                SetTimelineDragStatus("已取消 timeline range 拖动。", UiColors.TextSecondary);
                return;
            }

            if (changed)
            {
                FinishTimelineRangeDrag(row, rowIndex, nextStart, nextEnd);
                SuppressNextTimelineBarClick();
            }
            else
            {
                SetTimelineDragStatus("范围未变化：" + nextStart + "-" + nextEnd, UiColors.TextSecondary);
            }
        }

        private void SelectTimelineRowForTimelineDrag(int index, VisualElement bar, TimelineBarData data)
        {
            _selectedTimelineRowIndex = index;
            TimelineRow row = _timelineRows[index];
            CombatAuthoringSceneState.SetSelection(new CombatAuthoringSelection(row.Section, row.TrackId, row.PropertyPath));
            RefreshDetail(row);

            bar.style.borderBottomColor = UiColors.SelectionBorder;
            bar.style.borderLeftColor = UiColors.SelectionBorder;
            bar.style.borderRightColor = UiColors.SelectionBorder;
            bar.style.borderTopColor = UiColors.SelectionBorder;
            bar.style.borderBottomWidth = 1;
            bar.style.borderLeftWidth = 1;
            bar.style.borderRightWidth = 1;
            bar.style.borderTopWidth = 1;
            SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, 1f);
        }

        private void UpdateTimelineDragPreview(int startFrame, int endFrame)
        {
            if (!_timelineDrag.IsDragging)
            {
                return;
            }

            int maxFrame = GetMaxActionFrame();
            startFrame = Clamp(startFrame, 0, maxFrame);
            endFrame = Clamp(endFrame, startFrame, maxFrame);
            bool previewChanged = startFrame != _timelineDrag.PreviewStartFrame || endFrame != _timelineDrag.PreviewEndFrame;
            _timelineDrag.SetPreview(startFrame, endFrame);

            float frameWidth = GetTimelineFrameWidth(_timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            float x = TimelineStripLabelWidth + FrameToTimelineX(startFrame, _timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            float width = Math.Max(4f, (endFrame - startFrame + 1) * frameWidth);
            _timelineDrag.Bar.style.left = x;
            _timelineDrag.Bar.style.width = width;
            _timelineDrag.RangeLabel.style.left = x + 5;
            _timelineDrag.RangeLabel.style.width = Math.Max(36f, width - 10f);
            _timelineDrag.RangeLabel.text = startFrame + "-" + endFrame;
            _timelineDrag.Bar.tooltip = BuildTimelineTooltip(_timelineDrag.Row) + "\n预览：" + startFrame + "-" + endFrame;
            if (previewChanged)
            {
                int previewFrame = _timelineDrag.Mode == TimelineDragMode.ResizeEnd ? endFrame : startFrame;
                CombatAuthoringSceneState.SetFrame(previewFrame);
                SceneView.RepaintAll();
            }

            SetTimelineDragStatus(TimelineDragVerb(_timelineDrag.Mode) + "：" + startFrame + "-" + endFrame, UiColors.TextPrimary);
        }

        private void ApplyTimelineRangeToAsset(
            TimelineRow row,
            int rowIndex,
            int nextStart,
            int nextEnd,
            bool recordUndo,
            bool refreshDetail)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty property = _actionSerialized.FindProperty(row.PropertyPath);
            if (property == null || !TryGetTimelineRangeProperties(property, row, out SerializedProperty start, out SerializedProperty end))
            {
                return;
            }

            if (recordUndo)
            {
                Undo.RecordObject(_actionAsset, "Drag Combat Timeline Range");
            }

            start.intValue = nextStart;
            end.intValue = nextEnd;
            if (recordUndo)
            {
                _actionSerialized.ApplyModifiedProperties();
            }
            else
            {
                _actionSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            UpdateTimelineRowFrameRange(rowIndex, nextStart, nextEnd);
            if (refreshDetail)
            {
                RefreshDetail(rowIndex >= 0 && rowIndex < _timelineRows.Count
                    ? _timelineRows[rowIndex]
                    : (TimelineRow?)null);
            }
        }

        private void FinishTimelineRangeDrag(TimelineRow row, int rowIndex, int nextStart, int nextEnd)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                return;
            }

            ApplyTimelineRangeToAsset(row, rowIndex, nextStart, nextEnd, recordUndo: true, refreshDetail: false);
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(row));
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetTimelineDragStatus("已提交范围：" + nextStart + "-" + nextEnd, UiColors.TextSecondary);
        }

        private void UpdateTimelineRowFrameRange(int rowIndex, int startFrame, int endFrame)
        {
            if (rowIndex < 0 || rowIndex >= _timelineRows.Count)
            {
                return;
            }

            TimelineRow row = _timelineRows[rowIndex];
            var frameRange = new CombatAuthoringFrameRange(startFrame, endFrame);
            _timelineRows[rowIndex] = new TimelineRow(row.Section, row.Label, row.TrackId, frameRange, row.PropertyPath, row.Kind);
        }

        private int PanelPositionToTimelineFrame(Vector3 panelPosition, int totalFrames, float trackWidth)
        {
            if (_timelineStripContent == null)
            {
                return 0;
            }

            Vector2 contentPosition = _timelineStripContent.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            float frameWidth = GetTimelineFrameWidth(totalFrames, trackWidth);
            float relativeX = contentPosition.x - TimelineStripLabelWidth;
            return Clamp(Mathf.RoundToInt(relativeX / frameWidth), 0, Math.Max(0, totalFrames - 1));
        }

        private static void CalculateTimelineDragRange(
            TimelineDragMode mode,
            int originalStart,
            int originalEnd,
            int pointerStartFrame,
            int targetFrame,
            int maxFrame,
            out int startFrame,
            out int endFrame)
        {
            originalStart = Clamp(originalStart, 0, maxFrame);
            originalEnd = Clamp(originalEnd, originalStart, maxFrame);
            if (mode == TimelineDragMode.Move)
            {
                int length = originalEnd - originalStart;
                int delta = targetFrame - pointerStartFrame;
                startFrame = Clamp(originalStart + delta, 0, Math.Max(0, maxFrame - length));
                endFrame = startFrame + length;
                return;
            }

            if (mode == TimelineDragMode.ResizeStart)
            {
                startFrame = Clamp(targetFrame, 0, originalEnd);
                endFrame = originalEnd;
                return;
            }

            startFrame = originalStart;
            endFrame = Clamp(targetFrame, originalStart, maxFrame);
        }

        private static TimelineDragMode GetTimelineDragMode(float localX, float barWidth)
        {
            float edgeZone = TimelineRangeEdgeHandleWidth + 4f;
            if (barWidth <= edgeZone * 2f)
            {
                edgeZone = barWidth * 0.5f;
            }

            if (localX <= edgeZone)
            {
                return TimelineDragMode.ResizeStart;
            }

            if (localX >= barWidth - edgeZone)
            {
                return TimelineDragMode.ResizeEnd;
            }

            return TimelineDragMode.Move;
        }

        private static TimelineDragMode GetTimelineDragMode(VisualElement bar, Vector3 panelPosition)
        {
            if (bar == null)
            {
                return TimelineDragMode.Move;
            }

            Vector2 localPosition = bar.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            return GetTimelineDragMode(localPosition.x, bar.resolvedStyle.width);
        }

        private static string TimelineDragVerb(TimelineDragMode mode)
        {
            switch (mode)
            {
                case TimelineDragMode.ResizeStart:
                    return "调整 Start";
                case TimelineDragMode.ResizeEnd:
                    return "调整 End";
                default:
                    return "移动范围";
            }
        }

        private static bool TryGetTimelineRangeProperties(SerializedProperty property, TimelineRow row, out SerializedProperty start, out SerializedProperty end)
        {
            SerializedProperty range = string.Equals(row.Section, "Action", StringComparison.Ordinal)
                ? property
                : property.FindPropertyRelative("frameRange");
            start = range?.FindPropertyRelative("startFrame");
            end = range?.FindPropertyRelative("endFrame");
            return start != null && end != null;
        }

        private static bool IsTimelineRangeDraggable(TimelineRow row)
        {
            if (row.FrameRange.IsEmpty)
            {
                return false;
            }

            return string.Equals(row.Section, "Action", StringComparison.Ordinal)
                || string.Equals(row.Section, "Hitbox", StringComparison.Ordinal)
                || string.Equals(row.Section, "Hurtbox", StringComparison.Ordinal);
        }

        private void SetTimelineDragStatus(string text, Color color)
        {
            if (_timelineDragStatus == null)
            {
                return;
            }

            _timelineDragStatus.text = text;
            _timelineDragStatus.style.color = color;
        }

        private void AddShapeRows(string section, CombatShapeAuthoringData[] shapes, string propertyRoot)
        {
            if (shapes == null)
            {
                return;
            }

            for (int i = 0; i < shapes.Length; i++)
            {
                CombatShapeAuthoringData shape = shapes[i];
                _timelineRows.Add(new TimelineRow(
                    section,
                    shape.ShapeKind + " #" + shape.TrackId,
                    shape.TrackId,
                    shape.FrameRange,
                    propertyRoot + ".Array.data[" + i + "]",
                    TimelineRowKind.ActionProperty));
            }
        }

        private void AddTraceRows()
        {
            CombatWeaponTraceAuthoringData[] traces = _actionAsset.WeaponTraces;
            if (traces == null)
            {
                return;
            }

            for (int i = 0; i < traces.Length; i++)
            {
                CombatWeaponTraceAuthoringData trace = traces[i];
                _timelineRows.Add(new TimelineRow(
                    "WeaponTrace",
                    "Trace #" + trace.TraceId,
                    trace.TraceId,
                    trace.FrameRange,
                    "weaponTraces.Array.data[" + i + "]",
                    TimelineRowKind.ActionProperty));
            }
        }

        private void RefreshFrameSlider()
        {
            int maxFrame = Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
            int frame = CombatAuthoringSceneState.Frame;
            if (_frameSlider != null)
            {
                _frameSlider.highValue = maxFrame;
                if (_frameSlider.value != frame)
                {
                    _frameSlider.SetValueWithoutNotify(frame);
                }
            }

            if (_frameLabel != null)
            {
                _frameLabel.text = "当前帧 " + frame;
            }

            CombatAuthoringSceneState.SetFrame(frame);
        }

        private void OnSceneStateChanged()
        {
            bool contextChanged = _actionAsset != CombatAuthoringSceneState.ActionAsset
                || _sceneBindingAsset != CombatAuthoringSceneState.SceneBindingAsset;
            bool dataChanged = _observedDataRevision != CombatAuthoringSceneState.DataRevision;
            if (contextChanged || dataChanged)
            {
                if (_isApplyingLocalSerializedRefresh && !contextChanged)
                {
                    _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                }
                else
                {
                    ApplySharedContextToLocal();
                    if (HasValidSerializedTarget(_actionSerialized))
                    {
                        _actionSerialized.Update();
                    }

                    if (HasValidSerializedTarget(_bindingSerialized))
                    {
                        _bindingSerialized.Update();
                    }

                    RebuildActionFields();
                    RebuildBindingFields();
                    RefreshTimeline();
                    RefreshContext();
                    RefreshEmptyState();
                    RefreshValidation();
                    _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                }
            }

            ApplySharedSelectionToLocal(true);

            int frame = CombatAuthoringSceneState.Frame;
            int maxFrame = Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
            if (_frameSlider != null)
            {
                _frameSlider.highValue = maxFrame;
                if (_frameSlider.value != frame)
                {
                    _frameSlider.SetValueWithoutNotify(frame);
                }
            }

            if (_frameLabel != null)
            {
                _frameLabel.text = "当前帧 " + frame;
            }

            UpdateTimelinePlayhead();
            Repaint();
        }

        private void ApplySharedContextToLocal()
        {
            CombatActionAuthoringAsset sharedAction = CombatAuthoringSceneState.ActionAsset;
            CombatSceneBindingAsset sharedBinding = CombatAuthoringSceneState.SceneBindingAsset;
            if (_actionAsset != sharedAction)
            {
                _actionAsset = sharedAction;
                _actionSerialized = sharedAction == null ? null : new SerializedObject(sharedAction);
                if (_actionField != null && _actionField.value != sharedAction)
                {
                    _actionField.SetValueWithoutNotify(sharedAction);
                }
            }

            if (_sceneBindingAsset != sharedBinding)
            {
                _sceneBindingAsset = sharedBinding;
                _bindingSerialized = sharedBinding == null ? null : new SerializedObject(sharedBinding);
                if (_bindingField != null && _bindingField.value != sharedBinding)
                {
                    _bindingField.SetValueWithoutNotify(sharedBinding);
                }
            }
        }

        private void ApplySharedSelectionToLocal(bool refreshIfChanged)
        {
            CombatAuthoringSelection selection = CombatAuthoringSceneState.Selection;
            int index = selection.IsEmpty ? -1 : FindTimelineRow(selection.Section, selection.TrackId);
            if (index == _selectedTimelineRowIndex)
            {
                if (_timelineList != null && index >= 0 && _timelineList.selectedIndex != index)
                {
                    _timelineList.SetSelectionWithoutNotify(new[] { index });
                }

                return;
            }

            _selectedTimelineRowIndex = index;
            if (_timelineList != null)
            {
                if (index >= 0)
                {
                    _timelineList.SetSelectionWithoutNotify(new[] { index });
                }
                else
                {
                    _timelineList.SetSelectionWithoutNotify(Array.Empty<int>());
                }
            }

            if (!refreshIfChanged)
            {
                return;
            }

            RefreshDetail(index >= 0 ? _timelineRows[index] : (TimelineRow?)null);
            RefreshTimelineStrip();
        }

        private void RefreshValidation()
        {
            _lastReport = CombatAuthoringValidator.Validate(_actionAsset, _sceneBindingAsset);
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastReportText);
        }

        private void ApplyValidationReportToUi(CombatAuthoringReport report, string reportText)
        {
            _issueRows.Clear();
            if (report != null)
            {
                for (int i = 0; i < report.IssueCount; i++)
                {
                    _issueRows.Add(report.GetIssue(i));
                }
            }

            if (_issueList != null)
            {
                _issueList.RefreshItems();
            }

            if (_reportPreview != null)
            {
                _reportPreview.value = reportText ?? string.Empty;
            }

            if (_validationLabel != null)
            {
                if (report == null || report.IssueCount == 0)
                {
                    _validationLabel.text = "\u2705 验证通过";
                    _validationLabel.style.color = UiColors.SuccessGreen;
                }
                else
                {
                    _validationLabel.text = (report.HasErrors ? "\u274C 存在错误：" : "\u26A0 存在提示：") + report.IssueCount;
                    _validationLabel.style.color = report.HasErrors ? UiColors.ErrorRed : UiColors.WarnAmber;
                }
            }
        }

        private void RefreshEmptyState()
        {
            if (_emptyStateRoot != null)
            {
                _emptyStateRoot.style.display = _actionAsset == null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_toolbarRoot != null)
            {
                _toolbarRoot.style.display = _actionAsset == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void RefreshContext()
        {
            if (_contextLabel == null)
            {
                return;
            }

            string actionName = _actionAsset == null ? "Action：未选择" : "Action：" + _actionAsset.name + " / Frames " + _actionAsset.TotalFrames;
            string bindingName = _sceneBindingAsset == null ? "Binding：未选择（可选）" : "Binding：" + _sceneBindingAsset.name;
            string mode = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
            string showcaseStatus = CombatAuthoringShowcasePlaySession.HasSession
                ? "    Showcase：" + CombatAuthoringShowcasePlaySession.LastStatus
                : string.Empty;
            _contextLabel.text = actionName + "    " + bindingName + "    " + mode + showcaseStatus;
        }

        private VisualElement MakeTimelineRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.RegisterCallback<PointerUpEvent>(OnTimelineListRowPointerUp, TrickleDown.TrickleDown);
            row.RegisterCallback<ClickEvent>(OnTimelineListRowClicked, TrickleDown.TrickleDown);

            var section = new Label { name = "section" };
            section.style.width = 90;
            section.style.flexShrink = 0;
            row.Add(section);

            var label = new Label { name = "label" };
            label.style.flexGrow = 1;
            label.style.minWidth = 120;
            row.Add(label);

            var range = new Label { name = "range" };
            range.style.width = 96;
            range.style.flexShrink = 0;
            row.Add(range);

            return row;
        }

        private void BindTimelineRow(VisualElement element, int index)
        {
            TimelineRow row = _timelineRows[index];
            element.userData = index;
            element.Q<Label>("section").text = row.Section;
            element.Q<Label>("label").text = row.Label;
            element.Q<Label>("range").text = FormatRange(row.FrameRange);
            element.tooltip = BuildTimelineTooltip(row);
        }

        private void OnTimelineListRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private void OnTimelineListRowPointerUp(PointerUpEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private VisualElement MakeIssueRow()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.Center;
            root.style.paddingTop = 3;
            root.style.paddingBottom = 3;

            var title = new Label { name = "title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            root.Add(title);

            var detail = new Label { name = "detail" };
            detail.style.whiteSpace = WhiteSpace.Normal;
            root.Add(detail);

            var actions = new VisualElement { name = "actions" };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 3;
            root.Add(actions);

            return root;
        }

        private void BindIssueRow(VisualElement element, int index)
        {
            CombatAuthoringIssue issue = _issueRows[index];
            Label titleLabel = element.Q<Label>("title");
            titleLabel.text = IssueSeverityIcon(issue.Severity) + " " + issue.Severity + " | " + issue.SourceAsset + " | " + issue.Section + " | " + issue.Field;
            titleLabel.style.color = IssueSeverityColor(issue.Severity);
            element.Q<Label>("detail").text = issue.Message + " 建议：" + issue.SuggestedFix + " Frame=" + FormatRange(issue.FrameRange);
            VisualElement actions = element.Q<VisualElement>("actions");
            CreateIssueQuickActions(actions, issue);
            element.tooltip = "严重度：" + issue.Severity
                + "\n来源：" + issue.SourceAsset
                + "\n位置：" + issue.Section + " / " + issue.Field
                + "\n帧范围：" + FormatRange(issue.FrameRange)
                + "\n建议：" + issue.SuggestedFix;
        }

        private void CreateIssueQuickActions(VisualElement root, CombatAuthoringIssue issue)
        {
            if (root == null)
            {
                return;
            }

            root.Clear();
            if (IsFrameRangeIssue(issue))
            {
                root.Add(CreateIssueActionButton("适配总帧数", "把 Action Total Frames 扩到能包含当前最大结束帧；只修改 Authoring Asset，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.FitTotalFrames));
                root.Add(CreateIssueActionButton("修正帧范围", "把该问题对应的帧范围 clamp 到 0 到 TotalFrames - 1，并修正 start > end；只修改 Authoring Asset，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.ClampFrameRange));
                root.Add(CreateIssueActionButton("定位目标", "选中该问题对应的行，并同步 Inspector、Timeline 和 Scene gizmo。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
                return;
            }

            if (IsMarkerIssue(issue))
            {
                root.Add(CreateIssueActionButton("定位目标", "选中缺失 marker 的目标行，并同步 Inspector、Timeline 和 Scene gizmo。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
                if (TryFindIssueTimelineRow(issue) >= 0)
                {
                    root.Add(CreateIssueActionButton("使用当前选择重连", "把当前场景选择登记为 Binding marker，并写回当前 Shape / Trace 的 markerId；不会静默移动或创建 scene transform，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.RelinkSelectedTransform));
                }
                else
                {
                    root.Add(CreateIssueActionButton("处理提示", "定位相关 asset，并提示需要在 Binding 字段中手动重连；不会静默修改 scene transform。", issue, CombatAuthoringQuickActionKind.CreatePreviewMarker));
                }

                return;
            }

            if (issue.QuickAction == CombatAuthoringQuickActionKind.SelectAsset)
            {
                root.Add(CreateIssueActionButton("定位 Asset", "在 Project 中定位当前 Action 或 Binding asset。", issue, CombatAuthoringQuickActionKind.SelectAsset));
                return;
            }

            root.Add(CreateIssueActionButton("定位目标", "选中该问题对应的行；无法映射到行时会定位相关 asset。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
        }

        private Button CreateIssueActionButton(string text, string tooltip, CombatAuthoringIssue issue, CombatAuthoringQuickActionKind action)
        {
            var button = CreateMiniButton(text, tooltip, () => ExecuteIssueQuickAction(issue, action));
            button.style.marginRight = 4;
            return button;
        }

        private void ExecuteIssueQuickAction(CombatAuthoringIssue issue, CombatAuthoringQuickActionKind action)
        {
            switch (action)
            {
                case CombatAuthoringQuickActionKind.FitTotalFrames:
                    FitTotalFramesForIssue(issue);
                    break;
                case CombatAuthoringQuickActionKind.ClampFrameRange:
                    ClampIssueFrameRange(issue);
                    break;
                case CombatAuthoringQuickActionKind.SelectIssueTarget:
                    SelectIssueTarget(issue, showStatus: true);
                    break;
                case CombatAuthoringQuickActionKind.SelectAsset:
                    SelectIssueAsset(issue);
                    break;
                case CombatAuthoringQuickActionKind.RelinkSelectedTransform:
                    RelinkSelectedTransformForIssue(issue);
                    break;
                case CombatAuthoringQuickActionKind.CreatePreviewMarker:
                    ShowMarkerIssueHint(issue);
                    break;
                default:
                    SetQuickActionStatus("该问题暂不支持一键修复，请先定位目标后手动处理。", UiColors.WarnAmber);
                    SelectIssueTarget(issue, showStatus: false);
                    break;
            }
        }

        private void SelectIssueTarget(CombatAuthoringIssue issue, bool showStatus)
        {
            int rowIndex = TryFindIssueTimelineRow(issue);
            if (rowIndex >= 0)
            {
                SelectTimelineRow(rowIndex);
                TimelineRow row = _timelineRows[rowIndex];
                int targetFrame = row.FrameRange.IsEmpty ? CombatAuthoringSceneState.Frame : Clamp(row.FrameRange.StartFrame, 0, GetMaxActionFrame());
                CombatAuthoringSceneState.SetFrame(targetFrame);
                RefreshFrameSlider();
                SceneView.RepaintAll();
                Repaint();
                if (showStatus)
                {
                    SetQuickActionStatus("已定位到 " + row.Section + " / " + row.Label + "。", UiColors.TextSecondary);
                }

                return;
            }

            SelectIssueAsset(issue);
            if (showStatus)
            {
                SetQuickActionStatus("该问题没有对应 timeline 行，已定位相关 asset。", UiColors.WarnAmber);
            }
        }

        private void SelectIssueAsset(CombatAuthoringIssue issue)
        {
            UnityEngine.Object target = null;
            if (_actionAsset != null && string.Equals(issue.SourceAsset, _actionAsset.name, StringComparison.Ordinal))
            {
                target = _actionAsset;
            }
            else if (_sceneBindingAsset != null && string.Equals(issue.SourceAsset, _sceneBindingAsset.name, StringComparison.Ordinal))
            {
                target = _sceneBindingAsset;
            }
            else
            {
                target = _actionAsset != null ? (UnityEngine.Object)_actionAsset : _sceneBindingAsset;
            }

            PingObject(target);
        }

        private void FitTotalFramesForIssue(CombatAuthoringIssue issue)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                SetQuickActionStatus("请先选择 Action Asset。", UiColors.WarnAmber);
                return;
            }

            int requiredFrames = GetRequiredTotalFrames();
            if (requiredFrames <= _actionAsset.TotalFrames)
            {
                SetQuickActionStatus("Total Frames 已能包含当前最大结束帧。", UiColors.TextSecondary);
                SelectIssueTarget(issue, showStatus: false);
                return;
            }

            MutateActionProperty("Fit Combat Total Frames", serialized =>
            {
                SerializedProperty totalFrames = serialized.FindProperty("totalFrames");
                if (totalFrames != null)
                {
                    totalFrames.intValue = requiredFrames;
                }
            });
            RebuildActionFields();
            SelectIssueTarget(issue, showStatus: false);
            SetQuickActionStatus("已把 Total Frames 调整为 " + requiredFrames + "；可用 Undo 恢复。", UiColors.TextSecondary);
        }

        private void ClampIssueFrameRange(CombatAuthoringIssue issue)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                SetQuickActionStatus("请先选择 Action Asset。", UiColors.WarnAmber);
                return;
            }

            int maxFrame = GetMaxActionFrame();
            bool changed = false;
            bool found = false;

            MutateActionProperty("Clamp Combat Frame Range", serialized =>
            {
                SerializedProperty range = FindIssueFrameRangeProperty(serialized, issue);
                SerializedProperty start = range?.FindPropertyRelative("startFrame");
                SerializedProperty end = range?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                found = true;
                int nextStart = Clamp(start.intValue, 0, maxFrame);
                int nextEnd = Clamp(end.intValue, 0, maxFrame);
                if (nextStart > nextEnd)
                {
                    nextEnd = nextStart;
                }

                changed = start.intValue != nextStart || end.intValue != nextEnd;
                start.intValue = nextStart;
                end.intValue = nextEnd;
            });

            RebuildActionFields();
            SelectIssueTarget(issue, showStatus: false);
            if (!found)
            {
                SetQuickActionStatus("找不到该 issue 对应的帧范围字段。", UiColors.ErrorRed);
                return;
            }

            SetQuickActionStatus(changed ? "已修正帧范围；可用 Undo 恢复。" : "帧范围已经在合法范围内。", UiColors.TextSecondary);
        }

        private void RelinkSelectedTransformForIssue(CombatAuthoringIssue issue)
        {
            if (!IsMarkerIssue(issue))
            {
                SetQuickActionStatus("该问题不是 marker 缺失问题。", UiColors.WarnAmber);
                return;
            }

            int rowIndex = TryFindIssueTimelineRow(issue);
            if (rowIndex < 0)
            {
                SelectIssueTarget(issue, showStatus: false);
                SetQuickActionStatus("请在 Binding 字段中手动重连该 marker；不会静默修改 scene transform。", UiColors.WarnAmber);
                return;
            }

            TimelineRow row = _timelineRows[rowIndex];
            if (!TryRelinkSelectedTransform(out string markerId) || string.IsNullOrEmpty(markerId))
            {
                return;
            }

            if (IsShapeSection(row.Section))
            {
                MutateShapeProperty(row.PropertyPath, "Relink Combat Shape Marker", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("markerId");
                    if (target != null)
                    {
                        target.stringValue = markerId;
                    }
                }, row, refreshTimeline: true, refreshDetail: true);
                SetQuickActionStatus("已用当前选择重连 " + row.Section + " marker；可用 Undo 恢复。", UiColors.TextSecondary);
                return;
            }

            if (string.Equals(row.Section, "WeaponTrace", StringComparison.Ordinal))
            {
                string fieldName = string.Equals(issue.Field, "tipMarkerId", StringComparison.Ordinal) ? "tipMarkerId" : "rootMarkerId";
                MutateActionProperty("Relink Combat WeaponTrace Marker", serialized =>
                {
                    SerializedProperty trace = serialized.FindProperty(row.PropertyPath);
                    SerializedProperty target = trace?.FindPropertyRelative(fieldName);
                    if (target != null)
                    {
                        target.stringValue = markerId;
                    }
                });
                SelectIssueTarget(issue, showStatus: false);
                SetQuickActionStatus("已用当前选择重连 WeaponTrace " + fieldName + "；可用 Undo 恢复。", UiColors.TextSecondary);
            }
        }

        private void ShowMarkerIssueHint(CombatAuthoringIssue issue)
        {
            SelectIssueTarget(issue, showStatus: false);
            SetQuickActionStatus("Marker 缺失：请在 Binding 的 Actors / Colliders / Markers 中手动重连，或先选择目标 row 后使用“使用当前选择重连”。不会静默修改 scene transform。", UiColors.WarnAmber);
        }

        private int TryFindIssueTimelineRow(CombatAuthoringIssue issue)
        {
            string section = GetTimelineSectionForIssue(issue);
            string property = GetActionPhasePropertyName(issue);
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (!string.Equals(row.Section, section, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(section, "Action", StringComparison.Ordinal))
                {
                    if (string.Equals(row.PropertyPath, property, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
                else if (row.TrackId == issue.TrackId)
                {
                    return i;
                }
            }

            return -1;
        }

        private SerializedProperty FindIssueFrameRangeProperty(SerializedObject serialized, CombatAuthoringIssue issue)
        {
            if (serialized == null)
            {
                return null;
            }

            string phaseProperty = GetActionPhasePropertyName(issue);
            if (!string.IsNullOrEmpty(phaseProperty))
            {
                return serialized.FindProperty(phaseProperty);
            }

            if (IsShapeSection(issue.Section))
            {
                SerializedProperty shapes = serialized.FindProperty(GetShapePropertyRoot(issue.Section));
                int index = FindElementIndexByTrackAndOrder(shapes, "trackId", issue.TrackId, issue.SourceOrder);
                SerializedProperty shape = index >= 0 ? shapes.GetArrayElementAtIndex(index) : null;
                return shape?.FindPropertyRelative("frameRange");
            }

            if (string.Equals(issue.Section, "WeaponTrace", StringComparison.Ordinal))
            {
                SerializedProperty traces = serialized.FindProperty("weaponTraces");
                int index = FindElementIndexByTrackAndOrder(traces, "traceId", issue.TrackId, issue.SourceOrder);
                SerializedProperty trace = index >= 0 ? traces.GetArrayElementAtIndex(index) : null;
                return trace?.FindPropertyRelative("frameRange");
            }

            return null;
        }

        private static int FindElementIndexByTrackAndOrder(SerializedProperty array, string idPropertyName, int id, int sourceOrder)
        {
            if (array == null || !array.isArray)
            {
                return -1;
            }

            int fallback = -1;
            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SerializedProperty trackId = element.FindPropertyRelative(idPropertyName);
                if (trackId == null || trackId.intValue != id)
                {
                    continue;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }

                SerializedProperty order = element.FindPropertyRelative("sourceOrder");
                if (order != null && order.intValue == sourceOrder)
                {
                    return i;
                }
            }

            return fallback;
        }

        private static bool IsFrameRangeIssue(CombatAuthoringIssue issue)
        {
            return issue.QuickAction == CombatAuthoringQuickActionKind.ClampFrameRange
                && (string.Equals(issue.Field, "frameRange", StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(GetActionPhasePropertyName(issue)));
        }

        private static bool IsMarkerIssue(CombatAuthoringIssue issue)
        {
            return issue.QuickAction == CombatAuthoringQuickActionKind.CreatePreviewMarker
                || issue.Field.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTimelineSectionForIssue(CombatAuthoringIssue issue)
        {
            return string.IsNullOrEmpty(GetActionPhasePropertyName(issue)) ? issue.Section : "Action";
        }

        private static string GetActionPhasePropertyName(CombatAuthoringIssue issue)
        {
            if (string.Equals(issue.Field, "startup", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Startup", StringComparison.Ordinal))
            {
                return "startup";
            }

            if (string.Equals(issue.Field, "active", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Active", StringComparison.Ordinal))
            {
                return "active";
            }

            if (string.Equals(issue.Field, "recovery", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Recovery", StringComparison.Ordinal))
            {
                return "recovery";
            }

            return string.Empty;
        }

        private void OnTimelineSelectionChanged(IEnumerable<object> selected)
        {
            int selectedIndex = -1;
            foreach (object item in selected)
            {
                if (item is TimelineRow row)
                {
                    selectedIndex = _timelineRows.IndexOf(row);
                    break;
                }
            }

            SelectTimelineRow(selectedIndex);
        }

        private void OnTimelineSelectedIndicesChanged(IEnumerable<int> selectedIndices)
        {
            int selectedIndex = -1;
            foreach (int index in selectedIndices)
            {
                selectedIndex = index;
                break;
            }

            SelectTimelineRow(selectedIndex);
        }

        private void SelectTimelineRow(int index)
        {
            if (index < 0 || index >= _timelineRows.Count)
            {
                _selectedTimelineRowIndex = -1;
                CombatAuthoringSceneState.SetSelection(default);
                RefreshDetail(null);
                RefreshTimelineStrip();
                return;
            }

            _selectedTimelineRowIndex = index;
            TimelineRow row = _timelineRows[index];
            CombatAuthoringSceneState.SetSelection(new CombatAuthoringSelection(row.Section, row.TrackId, row.PropertyPath));
            if (_timelineList != null && _timelineList.selectedIndex != index)
            {
                _timelineList.SetSelection(index);
            }

            RefreshDetail(row);
            RefreshTimelineStrip();
        }

        private void RefreshDetail(TimelineRow? row)
        {
            if (_detailRoot == null)
            {
                return;
            }

            _detailRoot.Clear();
            if (row == null || _actionSerialized == null)
            {
                _detailRoot.Add(new HelpBox("选择一条 timeline 行查看或轻编辑字段。", HelpBoxMessageType.Info));
                return;
            }

            SerializedProperty property = _actionSerialized.FindProperty(row.Value.PropertyPath);
            if (property == null)
            {
                _detailRoot.Add(new HelpBox("找不到 SerializedProperty：" + row.Value.PropertyPath, HelpBoxMessageType.Warning));
                return;
            }

            _detailRoot.Add(new HelpBox(row.Value.Section + " / " + row.Value.Label, HelpBoxMessageType.None));
            if (IsShapeSection(row.Value.Section))
            {
                AddShapeDetailFields(_detailRoot, property, row.Value);
            }
            else
            {
                var field = new PropertyField(property, row.Value.Label);
                field.tooltip = BuildTimelineTooltip(row.Value);
                _detailRoot.Add(field);
            }

            _detailRoot.Bind(_actionSerialized);
            RegisterSerializedRefresh(_detailRoot);
        }

        private void AddShapeDetailFields(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            root.Add(new HelpBox(
                "localCenter 是 marker 本地空间中心偏移；radiusRaw/heightRaw 使用 fixed raw 比例 1,000,000 = 1 Unity unit。Capsule 的 heightRaw 为总高度，需不小于直径；heightRaw 为 0 时按旧资产默认高度预览。",
                HelpBoxMessageType.Info));
            root.Add(CreateShapeQuickActions());

            AddShapeKindField(root, property, row);
            AddMarkerField(root, property, row);
            AddFrameRangeField(root, property, row);
            AddRelativeProperty(root, property, "localCenter", "本地中心");
            AddRawShapeField(root, property, row, "radiusRaw", "半径 Raw", MinShapeRadiusRaw, MaxRadiusSliderRaw, new[]
            {
                new RawPreset("小 0.25", RawPresetSmall),
                new RawPreset("中 0.5", RawPresetMedium),
                new RawPreset("大 1.0", RawPresetLarge),
            });
            AddRawShapeField(root, property, row, "heightRaw", "Capsule 高度 Raw", 0, MaxHeightSliderRaw, new[]
            {
                new RawPreset("默认 0", 0),
                new RawPreset("0.5", RawPresetMedium),
                new RawPreset("1.0", RawPresetLarge),
                new RawPreset("2.0", RawPresetLarge * 2),
            });
            AddCapsuleHeightWarning(root, property, row);
            AddRelativeProperty(root, property, "sourceOrder", "排序");
        }

        private VisualElement CreateShapeQuickActions()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = 4;
            root.style.marginBottom = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderTopColor = UiColors.Border;
            root.style.borderBottomColor = UiColors.Border;

            var label = new Label("Shape 操作");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.marginBottom = 4;
            root.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            var duplicate = CreateButton("复制 Shape", () => TryDuplicateSelectedShape());
            duplicate.tooltip = "复制当前 Shape，并为副本创建新的 trackId。";
            row.Add(duplicate);

            var delete = CreateButton("删除 Shape", () => TryDeleteSelectedShape());
            delete.tooltip = "删除当前 Shape；可用 Undo 恢复。";
            row.Add(delete);

            root.Add(row);
            return root;
        }

        private void AddShapeKindField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty shapeKind = property.FindPropertyRelative("shapeKind");
            if (shapeKind == null)
            {
                root.Add(new HelpBox("缺少字段：shapeKind", HelpBoxMessageType.Warning));
                return;
            }

            var choices = new List<string>(ShapeKindValues.Length);
            for (int i = 0; i < ShapeKindValues.Length; i++)
            {
                choices.Add(FormatShapeKindChoice(ShapeKindValues[i]));
            }

            int currentIndex = Clamp(shapeKind.enumValueIndex, 0, choices.Count - 1);
            var dropdown = new DropdownField("Shape 类型", choices, currentIndex)
            {
                tooltip = Tooltip("Shape 类型"),
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int selectedIndex = choices.IndexOf(evt.newValue);
                if (selectedIndex < 0)
                {
                    return;
                }

                MutateShapeProperty(property.propertyPath, "Change Combat Shape Kind", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("shapeKind");
                    if (target != null)
                    {
                        target.enumValueIndex = selectedIndex;
                    }
                }, row, refreshTimeline: true, refreshDetail: true);
            });
            root.Add(dropdown);
        }

        private void AddMarkerField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty markerId = property.FindPropertyRelative("markerId");
            if (markerId == null)
            {
                root.Add(new HelpBox("缺少字段：markerId", HelpBoxMessageType.Warning));
                return;
            }

            string currentMarkerId = markerId.stringValue ?? string.Empty;
            List<MarkerDropdownItem> markerItems = BuildMarkerDropdownItems(currentMarkerId);
            if (markerItems.Count > 0)
            {
                var choices = new List<string>(markerItems.Count);
                int selectedIndex = 0;
                for (int i = 0; i < markerItems.Count; i++)
                {
                    choices.Add(markerItems[i].Label);
                    if (string.Equals(markerItems[i].MarkerId, currentMarkerId, StringComparison.Ordinal))
                    {
                        selectedIndex = i;
                    }
                }

                var dropdown = new DropdownField("绑定 Marker", choices, selectedIndex)
                {
                    tooltip = Tooltip("绑定 Marker"),
                };
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int markerIndex = choices.IndexOf(evt.newValue);
                    if (markerIndex < 0 || markerIndex >= markerItems.Count || markerItems[markerIndex].IsPlaceholder)
                    {
                        return;
                    }

                    string selectedMarkerId = markerItems[markerIndex].MarkerId;
                    MutateShapeProperty(property.propertyPath, "Change Combat Shape Marker", shape =>
                    {
                        SerializedProperty target = shape.FindPropertyRelative("markerId");
                        if (target != null)
                        {
                            target.stringValue = selectedMarkerId;
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                });
                root.Add(dropdown);
            }
            else
            {
                root.Add(new HelpBox("当前没有可选 Binding marker。请先创建或选择 Scene Binding，并从场景生成 / 重连 marker。", HelpBoxMessageType.Warning));
            }

            var advanced = new TextField("Marker Id（高级）")
            {
                value = currentMarkerId,
                tooltip = Tooltip("Marker Id（高级）"),
            };
            advanced.RegisterValueChangedCallback(evt =>
            {
                MutateShapeProperty(property.propertyPath, "Edit Combat Shape Marker Id", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("markerId");
                    if (target != null)
                    {
                        target.stringValue = evt.newValue ?? string.Empty;
                    }
                }, row, refreshTimeline: false, refreshDetail: false);
            });
            root.Add(advanced);

            var relink = CreateButton("使用当前选择重连", () => RelinkSelectedMarkerForShape(property.propertyPath, row));
            root.Add(relink);
            AddMarkerWarning(root, currentMarkerId);
        }

        private void AddFrameRangeField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty frameRange = property.FindPropertyRelative("frameRange");
            if (frameRange == null)
            {
                root.Add(new HelpBox("缺少字段：frameRange", HelpBoxMessageType.Warning));
                return;
            }

            SerializedProperty startFrame = frameRange.FindPropertyRelative("startFrame");
            SerializedProperty endFrame = frameRange.FindPropertyRelative("endFrame");
            if (startFrame == null || endFrame == null)
            {
                root.Add(new HelpBox("缺少字段：frameRange.startFrame / endFrame", HelpBoxMessageType.Warning));
                return;
            }

            int maxFrame = GetMaxActionFrame();
            int startValue = startFrame.intValue;
            int endValue = endFrame.intValue;
            bool invalid = startValue < 0 || endValue < 0 || startValue > maxFrame || endValue > maxFrame || startValue > endValue;
            if (invalid)
            {
                root.Add(new HelpBox("当前帧范围超出 Action TotalFrames 或 start > end。使用下面控件会自动 clamp 到 0-" + maxFrame + "。", HelpBoxMessageType.Warning));
            }

            var rangeRoot = new VisualElement();
            rangeRoot.style.marginBottom = 4;
            rangeRoot.Add(CreateFrameStepperRow("Start", startValue, maxFrame, property.propertyPath, row));
            rangeRoot.Add(CreateFrameStepperRow("End", endValue, maxFrame, property.propertyPath, row));
            root.Add(rangeRoot);
        }

        private VisualElement CreateFrameStepperRow(string label, int value, int maxFrame, string shapePropertyPath, TimelineRow row)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginBottom = 3;

            var field = new IntegerField(label)
            {
                value = Clamp(value, 0, maxFrame),
                isDelayed = true,
                tooltip = Tooltip(label == "Start" ? "Frame Start" : "Frame End"),
            };
            field.style.flexGrow = 1;
            field.labelElement.style.minWidth = 42;
            field.labelElement.style.width = 42;
            field.RegisterValueChangedCallback(evt =>
            {
                SetShapeFrame(shapePropertyPath, row, label == "Start", evt.newValue);
            });
            line.Add(field);

            var minus = CreateMiniButton("-", Tooltip("Frame Step Down"), () => SetShapeFrame(shapePropertyPath, row, label == "Start", value - 1));
            var plus = CreateMiniButton("+", Tooltip("Frame Step Up"), () => SetShapeFrame(shapePropertyPath, row, label == "Start", value + 1));
            line.Add(minus);
            line.Add(plus);

            return line;
        }

        private void AddRawShapeField(VisualElement root, SerializedProperty property, TimelineRow row, string propertyName, string label, int minValue, int sliderDefaultMax, RawPreset[] presets)
        {
            SerializedProperty raw = property.FindPropertyRelative(propertyName);
            if (raw == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            int current = Clamp(raw.intValue, minValue, Math.Max(sliderDefaultMax, raw.intValue));
            if (raw.intValue != current)
            {
                root.Add(new HelpBox(label + " 当前值非法；下一次调整会自动修正为 " + current + "。", HelpBoxMessageType.Warning));
            }

            var unit = new Label(label + " 单位：1,000,000 raw = 1 Unity unit；当前 " + FormatRawUnit(current) + " unit");
            unit.tooltip = Tooltip(label);
            unit.style.whiteSpace = WhiteSpace.Normal;
            unit.style.marginTop = 3;
            root.Add(unit);

            int highValue = Math.Max(sliderDefaultMax, current);
            var slider = new SliderInt(label, minValue, highValue)
            {
                value = current,
                tooltip = Tooltip(label),
            };
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt =>
            {
                int clamped = Math.Max(minValue, evt.newValue);
                MutateShapeProperty(property.propertyPath, "Edit Combat Shape " + propertyName, shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative(propertyName);
                    if (target != null)
                    {
                        target.intValue = clamped;
                    }
                }, row, refreshTimeline: false, refreshDetail: false);
            });
            root.Add(slider);

            var presetRow = new VisualElement();
            presetRow.style.flexDirection = FlexDirection.Row;
            presetRow.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < presets.Length; i++)
            {
                RawPreset preset = presets[i];
                presetRow.Add(CreateMiniButton(preset.Label, Tooltip("Raw Preset"), () =>
                {
                    int clamped = Math.Max(minValue, preset.RawValue);
                    MutateShapeProperty(property.propertyPath, "Apply Combat Shape Raw Preset", shape =>
                    {
                        SerializedProperty target = shape.FindPropertyRelative(propertyName);
                        if (target != null)
                        {
                            target.intValue = clamped;
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                }));
            }

            root.Add(presetRow);
        }

        private void AddCapsuleHeightWarning(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty shapeKind = property.FindPropertyRelative("shapeKind");
            SerializedProperty radiusRaw = property.FindPropertyRelative("radiusRaw");
            SerializedProperty heightRaw = property.FindPropertyRelative("heightRaw");
            if (shapeKind == null || radiusRaw == null || heightRaw == null)
            {
                return;
            }

            if (shapeKind.enumValueIndex == (int)CombatAuthoringShapeKind.Capsule
                && radiusRaw.intValue > 0
                && heightRaw.intValue > 0
                && (long)heightRaw.intValue < (long)radiusRaw.intValue * 2L)
            {
                root.Add(new HelpBox("Capsule 高度小于直径，预览和 validation 会提示风险。", HelpBoxMessageType.Warning));
                root.Add(CreateButton("高度=直径", () =>
                {
                    MutateShapeProperty(property.propertyPath, "Fix Combat Capsule Height", shape =>
                    {
                        SerializedProperty radius = shape.FindPropertyRelative("radiusRaw");
                        SerializedProperty height = shape.FindPropertyRelative("heightRaw");
                        if (radius != null && height != null)
                        {
                            height.intValue = Math.Max(0, radius.intValue * 2);
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                }));
            }
        }

        private static void AddRelativeProperty(VisualElement root, SerializedProperty property, string propertyName, string label)
        {
            SerializedProperty relative = property.FindPropertyRelative(propertyName);
            if (relative == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new PropertyField(relative, label);
            field.tooltip = Tooltip(label);
            root.Add(field);
        }

        private void MutateShapeProperty(
            string shapePropertyPath,
            string undoName,
            Action<SerializedProperty> mutate,
            TimelineRow row,
            bool refreshTimeline,
            bool refreshDetail)
        {
            if (_actionAsset == null || _actionSerialized == null || mutate == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty shape = _actionSerialized.FindProperty(shapePropertyPath);
            if (shape == null)
            {
                return;
            }

            Undo.RecordObject(_actionAsset, undoName);
            mutate(shape);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            if (refreshTimeline)
            {
                RefreshTimeline();
                SelectTimelineRow(FindTimelineRow(row.Section, row.TrackId));
            }
            else if (refreshDetail)
            {
                RefreshDetail(row);
            }
            else
            {
                if (_timelineList != null)
                {
                    _timelineList.RefreshItems();
                }

                RefreshTimelineStrip();
            }

            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
        }

        private bool TryDeleteSelectedShape()
        {
            if (!TryGetSelectedShapeRow(out TimelineRow row))
            {
                SetQuickActionStatus("请先选择 Hitbox 或 Hurtbox。", UiColors.WarnAmber);
                return false;
            }

            if (!TryGetShapeArray(row.Section, out SerializedProperty shapes))
            {
                SetQuickActionStatus("找不到 Shape 数组：" + row.Section, UiColors.ErrorRed);
                return false;
            }

            int shapeIndex = FindShapeIndex(shapes, row);
            if (shapeIndex < 0)
            {
                SetQuickActionStatus("找不到当前选中的 Shape。", UiColors.ErrorRed);
                return false;
            }

            int nextTrackId = FindNearestShapeTrackIdAfterDelete(shapes, shapeIndex);
            Undo.RecordObject(_actionAsset, "Delete Combat Shape");
            shapes.DeleteArrayElementAtIndex(shapeIndex);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(nextTrackId >= 0 ? FindTimelineRow(row.Section, nextTrackId) : -1);
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetQuickActionStatus("已删除 " + row.Section + " Shape；可用 Undo 恢复。", UiColors.TextSecondary);
            return true;
        }

        private bool TryDuplicateSelectedShape()
        {
            if (!TryGetSelectedShapeRow(out TimelineRow row))
            {
                SetQuickActionStatus("请先选择 Hitbox 或 Hurtbox。", UiColors.WarnAmber);
                return false;
            }

            if (!TryGetShapeArray(row.Section, out SerializedProperty shapes))
            {
                SetQuickActionStatus("找不到 Shape 数组：" + row.Section, UiColors.ErrorRed);
                return false;
            }

            int shapeIndex = FindShapeIndex(shapes, row);
            if (shapeIndex < 0)
            {
                SetQuickActionStatus("找不到当前选中的 Shape。", UiColors.ErrorRed);
                return false;
            }

            ShapeSnapshot snapshot = ShapeSnapshot.Read(shapes.GetArrayElementAtIndex(shapeIndex));
            int newTrackId = GetNextShapeTrackId(shapes);
            int newSourceOrder = GetNextShapeSourceOrder(shapes);
            int insertIndex = shapes.arraySize;

            Undo.RecordObject(_actionAsset, "Duplicate Combat Shape");
            shapes.InsertArrayElementAtIndex(insertIndex);
            ShapeSnapshot.Write(shapes.GetArrayElementAtIndex(insertIndex), snapshot, newTrackId, newSourceOrder);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(row.Section, newTrackId));
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetQuickActionStatus("已复制 " + row.Section + " Shape，新 trackId=" + newTrackId + "。", UiColors.TextSecondary);
            return true;
        }

        private bool TryGetSelectedShapeRow(out TimelineRow row)
        {
            if (_selectedTimelineRowIndex >= 0 && _selectedTimelineRowIndex < _timelineRows.Count)
            {
                row = _timelineRows[_selectedTimelineRowIndex];
                return IsShapeRow(row);
            }

            row = default;
            return false;
        }

        private static bool IsShapeRow(TimelineRow row)
        {
            return IsShapeSection(row.Section);
        }

        private bool TryGetShapeArray(string section, out SerializedProperty shapes)
        {
            shapes = null;
            if (_actionAsset == null)
            {
                return false;
            }

            if (_actionSerialized == null)
            {
                _actionSerialized = new SerializedObject(_actionAsset);
            }

            _actionSerialized.Update();
            string propertyRoot = GetShapePropertyRoot(section);
            shapes = string.IsNullOrEmpty(propertyRoot) ? null : _actionSerialized.FindProperty(propertyRoot);
            return shapes != null && shapes.isArray;
        }

        private static string GetShapePropertyRoot(string section)
        {
            if (string.Equals(section, "Hitbox", StringComparison.Ordinal))
            {
                return "hitboxes";
            }

            if (string.Equals(section, "Hurtbox", StringComparison.Ordinal))
            {
                return "hurtboxes";
            }

            return string.Empty;
        }

        private static int FindShapeIndex(SerializedProperty shapes, TimelineRow row)
        {
            if (shapes == null || !shapes.isArray)
            {
                return -1;
            }

            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                SerializedProperty trackId = element.FindPropertyRelative("trackId");
                if (trackId != null && trackId.intValue == row.TrackId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindNearestShapeTrackIdAfterDelete(SerializedProperty shapes, int deletedIndex)
        {
            if (shapes == null || shapes.arraySize <= 1)
            {
                return -1;
            }

            int nextIndex = deletedIndex < shapes.arraySize - 1 ? deletedIndex + 1 : deletedIndex - 1;
            SerializedProperty trackId = shapes.GetArrayElementAtIndex(nextIndex).FindPropertyRelative("trackId");
            return trackId == null ? -1 : trackId.intValue;
        }

        private static int GetNextShapeSourceOrder(SerializedProperty shapes)
        {
            int maxSourceOrder = 0;
            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                SerializedProperty sourceOrder = element.FindPropertyRelative("sourceOrder");
                if (sourceOrder != null)
                {
                    maxSourceOrder = Math.Max(maxSourceOrder, sourceOrder.intValue);
                }
            }

            return maxSourceOrder + 1;
        }

        private void SetQuickActionStatus(string message, Color color)
        {
            if (_quickActionStatusLabel != null)
            {
                _quickActionStatusLabel.text = message ?? string.Empty;
                _quickActionStatusLabel.style.color = color;
            }

            if (_timelineDragStatus != null && !string.IsNullOrEmpty(message))
            {
                _timelineDragStatus.text = message;
                _timelineDragStatus.style.color = color;
            }
        }

        private bool IsKeyboardEventFromEditableField(KeyDownEvent evt)
        {
            return IsEditableInputElement(evt?.target as VisualElement)
                || IsEditableInputElement(rootVisualElement?.panel?.focusController?.focusedElement as VisualElement);
        }

        private static bool IsEditableInputElement(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is TextField
                    || current is IntegerField
                    || current is LongField
                    || current is FloatField
                    || current is DoubleField
                    || current is Slider
                    || current is SliderInt
                    || current is Vector2Field
                    || current is Vector3Field
                    || current is Vector4Field)
                {
                    return true;
                }

                string typeName = current.GetType().Name;
                if (current.focusable && typeName.EndsWith("Field", StringComparison.Ordinal))
                {
                    return true;
                }

                if (current.ClassListContains("unity-text-input")
                    || current.ClassListContains("unity-base-text-field__input")
                    || typeName.IndexOf("TextInput", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetShapeFrame(string shapePropertyPath, TimelineRow row, bool setStart, int requestedValue)
        {
            int maxFrame = GetMaxActionFrame();
            MutateShapeProperty(shapePropertyPath, "Edit Combat Shape Frame Range", shape =>
            {
                SerializedProperty frameRange = shape.FindPropertyRelative("frameRange");
                SerializedProperty start = frameRange?.FindPropertyRelative("startFrame");
                SerializedProperty end = frameRange?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                int nextStart = Clamp(start.intValue, 0, maxFrame);
                int nextEnd = Clamp(end.intValue, 0, maxFrame);
                int clampedRequested = Clamp(requestedValue, 0, maxFrame);
                if (setStart)
                {
                    nextStart = clampedRequested;
                    if (nextStart > nextEnd)
                    {
                        nextEnd = nextStart;
                    }
                }
                else
                {
                    nextEnd = clampedRequested;
                    if (nextEnd < nextStart)
                    {
                        nextStart = nextEnd;
                    }
                }

                start.intValue = nextStart;
                end.intValue = nextEnd;
            }, row, refreshTimeline: true, refreshDetail: true);
        }

        private void RelinkSelectedMarkerForShape(string shapePropertyPath, TimelineRow row)
        {
            if (!TryRelinkSelectedTransform(out string markerId) || string.IsNullOrEmpty(markerId))
            {
                return;
            }

            MutateShapeProperty(shapePropertyPath, "Relink Combat Shape Marker", shape =>
            {
                SerializedProperty target = shape.FindPropertyRelative("markerId");
                if (target != null)
                {
                    target.stringValue = markerId;
                }
            }, row, refreshTimeline: false, refreshDetail: true);
        }

        private List<MarkerDropdownItem> BuildMarkerDropdownItems(string currentMarkerId)
        {
            var items = new List<MarkerDropdownItem>();
            if (_sceneBindingAsset == null || _sceneBindingAsset.Markers == null || _sceneBindingAsset.Markers.Length == 0)
            {
                return items;
            }

            CombatMarkerBindingData[] markers = (CombatMarkerBindingData[])_sceneBindingAsset.Markers.Clone();
            Array.Sort(markers, ComparePreviewMarkers);
            bool foundCurrent = false;
            if (string.IsNullOrEmpty(currentMarkerId))
            {
                items.Add(new MarkerDropdownItem(string.Empty, "未选择 Marker", true));
                foundCurrent = true;
            }

            for (int i = 0; i < markers.Length; i++)
            {
                CombatMarkerBindingData marker = markers[i];
                if (string.IsNullOrEmpty(marker.MarkerId))
                {
                    continue;
                }

                if (string.Equals(marker.MarkerId, currentMarkerId, StringComparison.Ordinal))
                {
                    foundCurrent = true;
                }

                items.Add(new MarkerDropdownItem(
                    marker.MarkerId,
                    FormatMarkerDropdownLabel(marker.MarkerId, marker.TargetPath),
                    false));
            }

            if (!foundCurrent)
            {
                items.Insert(0, new MarkerDropdownItem(currentMarkerId, "当前值找不到：" + currentMarkerId, true));
            }

            return items;
        }

        private void AddMarkerWarning(VisualElement root, string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId))
            {
                root.Add(new HelpBox("Marker 为空。请选择 Binding marker，或使用当前选择重连生成 marker。", HelpBoxMessageType.Warning));
                return;
            }

            if (!MarkerExistsInCurrentBinding(markerId))
            {
                root.Add(new HelpBox("找不到 Marker：" + markerId + "。请选择下拉 marker，或使用当前选择重连。", HelpBoxMessageType.Warning));
            }
        }

        private bool MarkerExistsInCurrentBinding(string markerId)
        {
            if (string.IsNullOrEmpty(markerId) || _sceneBindingAsset == null || _sceneBindingAsset.Markers == null)
            {
                return false;
            }

            CombatMarkerBindingData[] markers = _sceneBindingAsset.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (string.Equals(markers[i].MarkerId, markerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetMaxActionFrame()
        {
            return Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
        }

        private static Button CreateMiniButton(string text, string tooltip, Action action)
        {
            var button = new Button(action)
            {
                text = text,
                tooltip = tooltip,
            };
            button.style.marginLeft = 3;
            button.style.marginRight = 0;
            button.style.marginBottom = 2;
            button.style.minWidth = 26;
            return button;
        }

        private static string FormatShapeKindChoice(CombatAuthoringShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CombatAuthoringShapeKind.Sphere:
                    return "球体 Sphere";
                case CombatAuthoringShapeKind.Capsule:
                    return "胶囊 Capsule";
                case CombatAuthoringShapeKind.Aabb:
                    return "轴对齐盒 Aabb";
                case CombatAuthoringShapeKind.Sector:
                    return "扇形 Sector";
                default:
                    return shapeKind.ToString();
            }
        }

        private static string FormatMarkerDropdownLabel(string markerId, string targetPath)
        {
            string path = string.IsNullOrEmpty(targetPath) ? "targetPath 未设置" : ShortenTargetPath(targetPath);
            return markerId + " | " + path;
        }

        private static string ShortenTargetPath(string targetPath)
        {
            const int maxLength = 44;
            if (string.IsNullOrEmpty(targetPath) || targetPath.Length <= maxLength)
            {
                return targetPath ?? string.Empty;
            }

            return "..." + targetPath.Substring(targetPath.Length - maxLength + 3);
        }

        private static string FormatRawUnit(int raw)
        {
            return (raw / (float)RawPerUnityUnit).ToString("0.###");
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static bool IsShapeSection(string section)
        {
            return string.Equals(section, "Hitbox", StringComparison.Ordinal)
                || string.Equals(section, "Hurtbox", StringComparison.Ordinal);
        }

        private static VisualElement CreatePanel(string title)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = UiColors.Surface;
            panel.style.borderBottomColor = UiColors.Border;
            panel.style.borderLeftColor = UiColors.Border;
            panel.style.borderRightColor = UiColors.Border;
            panel.style.borderTopColor = UiColors.Border;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 8;

            panel.Add(SectionTitle(title));
            return panel;
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.tooltip = Tooltip(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = UiColors.TextPrimary;
            label.style.fontSize = 12;
            label.style.marginTop = 2;
            label.style.marginBottom = 6;
            label.style.paddingBottom = 4;
            label.style.borderBottomWidth = 1;
            label.style.borderBottomColor = UiColors.Border;
            return label;
        }

        private static string Tooltip(string key)
        {
            switch (key)
            {
                case "Action Asset":
                    return "战斗动作配置资产。这里记录动作帧数、阶段、Hitbox、Hurtbox 和武器轨迹等编辑数据。";
                case "Scene Binding":
                    return "场景绑定配置。把 Authoring 数据里的 marker / actor / collider 绑定到当前场景对象，用于预览、校验和导出说明。";
                case "使用当前选择":
                    return "从 Project 当前选中的 Combat Action 或 Scene Binding 填入窗口。";
                case "打开 Timeline":
                    return "打开 Combat Timeline 专用窗口，使用更宽的横向时间轴查看和拖动帧范围。";
                case "创建 Action Asset":
                    return "创建新的 Combat Action Authoring Asset，用来编辑一个战斗动作。";
                case "创建 Binding":
                case "创建 Scene Binding":
                    return "创建新的 Combat Scene Binding Asset，用来保存场景对象与战斗 marker 的绑定关系。";
                case "从当前场景生成 Binding":
                    return "扫描当前场景中名称包含 Player、Enemy、Marker 或 Combat 的对象，自动生成 marker / actor 绑定草案。";
                case "重连选中对象":
                    return "把当前 Scene 中选中的 Transform 写入 Binding，作为一个可被 Authoring 数据引用的 marker。";
                case "使用当前选择重连":
                    return "把当前 Scene 中选中的 Transform 写入 Binding，并把当前 Shape 的 markerId 指向这个 marker。";
                case "添加 Hitbox":
                    return "在当前帧添加一个攻击判定体。创建后可在时间轴选中，并在 Scene View 用手柄编辑位置和大小。";
                case "添加 Hurtbox":
                    return "在当前帧添加一个受击判定体。创建后可在时间轴选中，并在 Scene View 用手柄编辑位置和大小。";
                case "定位 Action":
                    return "在 Project 中高亮当前 Action Asset。";
                case "定位 Binding":
                    return "在 Project 中高亮当前 Scene Binding Asset。";
                case "执行验证":
                    return "重新检查 Action 和 Binding 的结构、帧范围、marker 引用、shape 参数等问题。";
                case "运行 Showcase 预览":
                    return "进入 Combat 测试场景并把当前 Action / Binding 应用到 Runtime Showcase。这是测试预览，不是导出 Runtime 数据。";
                case "清除预览会话":
                    return "清除本次 Authoring -> Runtime Showcase 测试预览会话，不会修改 Runtime 数据或 Authoring Asset。";
                case "复制报告":
                    return "复制当前 Validation Report，便于粘贴给策划或开发定位问题。";
                case "导出 JSON":
                    return "执行 validation gate，通过后生成 JSON 包，并可选择目录写出 manifest、schema、action、binding 和报告文件。";
                case "复制导出报告":
                    return "复制最近一次导出报告；如果还没导出，会先在内存中生成一次报告但不写磁盘。";
                case "预览 Explain":
                    return "用当前帧和当前 Scene Binding 生成 Query / Candidate / Resolve 的解释报告，帮助确认判定链路。";
                case "复制 Explain":
                    return "复制最近一次 Preview Explain 文本，便于记录或发给开发排查。";
                case "Action":
                    return "动作基础数据分组，包括动作编号、总帧数和三个阶段帧范围。";
                case "基础字段":
                    return "当前 Action 和 Scene Binding 的核心字段。数组字段可以展开查看，但复杂编辑优先通过时间轴和 Scene View。";
                case "Timeline":
                    return "固定帧时间轴。横向条展示 Startup、Active、Recovery、Hitbox、Hurtbox 和 Trace 的生效帧。";
                case "详情列表":
                    return "当前时间轴条目的列表视图。选择一行后，右侧会显示可轻编辑字段。";
                case "选中项":
                    return "当前选中时间轴条目的字段详情。Shape 字段可配合 Scene View 手柄一起调整。";
                case "Validation Report":
                    return "验证报告区域。左侧是问题列表，右侧是可复制的完整报告和预览解释。";
                case "Issues":
                    return "验证发现的问题列表。Error 会阻断导出，Warning 允许导出但应尽量处理。";
                case "Report Preview":
                    return "完整验证或导出报告预览。内容可用复制按钮带走。";
                case "Preview Explain":
                    return "当前帧的战斗判定解释，包括 query、candidate、resolve 和稳定 hash。";
                case "Frame":
                    return "当前预览帧。拖动后会同步 Scene View 的 Authoring 预览。";
                case "Action Id":
                    return "动作编号。后续导出文件名和运行时索引会使用这个稳定 ID。";
                case "Total Frames":
                    return "动作总帧数。时间轴和帧滑条会按这个值限制范围。";
                case "Schema Version":
                    return "Authoring 数据结构版本。用于后续兼容和迁移。";
                case "Startup":
                    return "动作前摇帧范围。一般用于起手、准备和可被打断的阶段。";
                case "Active":
                    return "动作生效帧范围。攻击判定或关键效果通常发生在这里。";
                case "Recovery":
                    return "动作收招帧范围。一般用于硬直、恢复和后续衔接。";
                case "Scene Guid":
                    return "绑定所属场景的稳定 GUID。生成 Binding 时会自动填入当前场景。";
                case "Binding Profile Id":
                    return "绑定配置名。导出文件名会使用它生成稳定路径。";
                case "Actors":
                    return "参与预览的战斗实体绑定，例如玩家、敌人及其 body / collider。";
                case "Markers":
                    return "可被 Action 数据引用的场景 marker 列表。markerId 必须和 Hitbox、Hurtbox、Trace 引用一致。";
                case "Shape 类型":
                    return "判定体形状类型。当前优先支持 Sphere 和 Capsule 的 Scene View 手柄编辑。";
                case "生效帧范围":
                    return "该条判定体在哪些固定帧生效。起止帧都包含在内。";
                case "绑定 Marker":
                    return "该判定体跟随的 markerId。优先从 Scene Binding 的 marker 下拉选择；找不到时会在窗口内提示。";
                case "Marker Id（高级）":
                    return "高级文本入口。仅在需要手动填写稳定 markerId 时使用；普通编辑优先用 marker 下拉或重连按钮。";
                case "Frame Start":
                    return "生效帧起点。修改时自动 clamp 到 Action TotalFrames 范围内；如果超过 End 会同步修正 End。";
                case "Frame End":
                    return "生效帧终点。修改时自动 clamp 到 Action TotalFrames 范围内；如果小于 Start 会同步修正 Start。";
                case "Frame Step Down":
                    return "向前微调 1 帧，并自动 clamp 到合法范围。";
                case "Frame Step Up":
                    return "向后微调 1 帧，并自动 clamp 到合法范围。";
                case "本地中心":
                    return "相对绑定 marker 的本地空间中心偏移。可用 Scene View 移动手柄调整。";
                case "半径 Raw":
                    return "半径的 fixed raw 值，1,000,000 等于 1 Unity unit。";
                case "Capsule 高度 Raw":
                    return "胶囊总高度的 fixed raw 值，1,000,000 等于 1 Unity unit。建议不小于直径。";
                case "高度=直径":
                    return "把 Capsule heightRaw 修正为 radiusRaw * 2，消除高度小于直径的 warning。";
                case "Raw Preset":
                    return "常用 raw 预设按钮，用点击替代手动输入数值。";
                case "排序":
                    return "稳定排序用字段。用于确保验证、预览和导出结果不依赖 Unity 对象返回顺序。";
                default:
                    return string.Empty;
            }
        }

        private static string BuildTimelineTooltip(TimelineRow row)
        {
            return "类型：" + TimelineSectionName(row.Section)
                + "\n条目：" + TimelineLabelName(row.Label)
                + "\nTrackId：" + row.TrackId
                + "\n帧范围：" + FormatRange(row.FrameRange)
                + "\n字段路径：" + row.PropertyPath;
        }

        private static string TimelineSectionName(string section)
        {
            switch (section)
            {
                case "Action":
                    return "动作阶段";
                case "Hitbox":
                    return "攻击判定体";
                case "Hurtbox":
                    return "受击判定体";
                case "WeaponTrace":
                    return "武器轨迹";
                default:
                    return section;
            }
        }

        private static string TimelineLabelName(string label)
        {
            switch (label)
            {
                case "Startup":
                    return "前摇";
                case "Active":
                    return "生效";
                case "Recovery":
                    return "收招";
                default:
                    return label;
            }
        }

        private static ObjectField CreateCompactObjectField(string label, Type objectType)
        {
            var field = new ObjectField(label)
            {
                objectType = objectType,
                allowSceneObjects = false,
            };
            field.style.width = 360;
            field.style.minWidth = 280;
            field.style.height = 22;
            field.style.marginRight = 8;
            field.style.marginBottom = 4;
            field.labelElement.style.minWidth = 88;
            field.labelElement.style.width = 88;
            field.tooltip = Tooltip(label);
            return field;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.tooltip = Tooltip(text);
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.fontSize = 11;
            button.style.backgroundColor = UiColors.SurfaceAlt;
            button.style.borderBottomColor = UiColors.Border;
            button.style.borderLeftColor = UiColors.Border;
            button.style.borderRightColor = UiColors.Border;
            button.style.borderTopColor = UiColors.Border;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHover(button, true));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHover(button, false));
            return button;
        }

        private static void OnButtonHover(VisualElement button, bool hover)
        {
            button.style.backgroundColor = hover ? UiColors.ButtonHover : UiColors.SurfaceAlt;
            button.style.borderBottomColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderLeftColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderRightColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderTopColor = hover ? UiColors.TextSecondary : UiColors.Border;
        }

        private static void AddProperty(VisualElement root, SerializedObject serialized, string propertyName, string label)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new PropertyField(property, label);
            field.tooltip = Tooltip(label);
            root.Add(field);
        }

        private void RegisterSerializedRefresh(VisualElement root)
        {
            if (ReferenceEquals(root.userData, SerializedRefreshRegistrationMarker))
            {
                return;
            }

            root.userData = SerializedRefreshRegistrationMarker;
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                EditorApplication.delayCall -= DelayedSerializedRefresh;
                EditorApplication.delayCall += DelayedSerializedRefresh;
            });
        }

        private void DelayedSerializedRefresh()
        {
            if (HasValidSerializedTarget(_actionSerialized))
            {
                _actionSerialized.Update();
            }

            if (HasValidSerializedTarget(_bindingSerialized))
            {
                _bindingSerialized.Update();
            }

            RefreshTimeline(refreshDetail: false);
            RefreshContext();
            RefreshValidation();
            _isApplyingLocalSerializedRefresh = true;
            try
            {
                CombatAuthoringSceneState.NotifyDataChanged();
            }
            finally
            {
                _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                _isApplyingLocalSerializedRefresh = false;
            }
        }

        private static bool HasValidSerializedTarget(SerializedObject serialized)
        {
            return serialized != null && serialized.targetObject != null;
        }

        private void UseSelection()
        {
            if (Selection.activeObject is CombatActionAuthoringAsset action)
            {
                SetActionAsset(action);
                return;
            }

            if (Selection.activeObject is CombatSceneBindingAsset binding)
            {
                SetSceneBindingAsset(binding);
            }
        }

        private void CreateActionAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Combat Action Asset",
                "CombatActionAuthoringAsset",
                "asset",
                "选择 Combat Action Authoring Asset 保存路径。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<CombatActionAuthoringAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetActionAsset(asset);
            FocusCreatedAsset(asset);
        }

        private void CreateBindingAsset()
        {
            CombatSceneBindingAsset asset = CreateBindingAssetFromSavePanel(true);
            if (asset != null)
            {
                FocusCreatedAsset(asset);
            }
        }

        private CombatSceneBindingAsset CreateBindingAssetFromSavePanel(bool setAsCurrent)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Combat Scene Binding Asset",
                "CombatSceneBindingAsset",
                "asset",
                "选择 Combat Scene Binding Asset 保存路径。");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = CreateInstance<CombatSceneBindingAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            if (setAsCurrent)
            {
                SetSceneBindingAsset(asset);
            }

            return asset;
        }

        private void FocusCreatedAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            Show();
            Focus();
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                Show();
                Focus();
                PingObject(asset);
            };
        }

        private void KeepWindowVisible()
        {
            Show();
            Focus();
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                Show();
                Focus();
                Repaint();
            };
        }

        private CombatSceneBindingAsset EnsureSceneBindingAsset()
        {
            if (_sceneBindingAsset != null)
            {
                return _sceneBindingAsset;
            }

            return CreateBindingAssetFromSavePanel(true);
        }

        private void GenerateBindingFromCurrentScene()
        {
            CombatSceneBindingAsset binding = EnsureSceneBindingAsset();
            if (binding == null)
            {
                return;
            }

            CollectSceneMarkerCandidates(_sceneMarkerCandidates);
            if (_sceneMarkerCandidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Combat Scene Binding",
                    "当前打开场景中没有找到名字包含 Player、Enemy、Marker 或 Combat 的 Transform。",
                    "确定");
                return;
            }

            SerializedObject serialized = new SerializedObject(binding);
            serialized.Update();
            Undo.RecordObject(binding, "Generate Combat Scene Binding");

            FillBindingIdentity(serialized);
            WriteMarkers(serialized.FindProperty("markers"), _sceneMarkerCandidates);
            WriteActors(serialized.FindProperty("actors"), _sceneMarkerCandidates);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
            SetSceneBindingAsset(binding);
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            KeepWindowVisible();
        }

        private void RelinkSelectedTransform()
        {
            TryRelinkSelectedTransform(out _);
        }

        private bool TryRelinkSelectedTransform(out string markerId)
        {
            Transform selected = Selection.activeTransform;
            if (selected == null && Selection.activeGameObject != null)
            {
                selected = Selection.activeGameObject.transform;
            }

            markerId = string.Empty;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Combat Scene Binding", "请选择一个场景 Transform 或 GameObject。", "确定");
                return false;
            }

            if (EditorUtility.IsPersistent(selected))
            {
                EditorUtility.DisplayDialog("Combat Scene Binding", "请选择当前场景中的对象，不能使用 Project 资产。", "确定");
                return false;
            }

            CombatSceneBindingAsset binding = EnsureSceneBindingAsset();
            if (binding == null)
            {
                return false;
            }

            string targetPath = GetHierarchyPath(selected);
            markerId = CreateReadableMarkerId(selected.name);
            if (string.IsNullOrEmpty(markerId))
            {
                markerId = "Marker";
            }

            SerializedObject serialized = new SerializedObject(binding);
            serialized.Update();
            Undo.RecordObject(binding, "Relink Combat Marker");

            FillBindingIdentity(serialized);
            SerializedProperty markers = serialized.FindProperty("markers");
            UpsertMarker(markers, markerId, targetPath);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
            SetSceneBindingAsset(binding);
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            KeepWindowVisible();
            return true;
        }

        private void AddShape(string section, string propertyRoot)
        {
            if (_actionAsset == null)
            {
                EditorUtility.DisplayDialog("Combat Authoring", "请先选择或创建 Action Asset。", "确定");
                return;
            }

            if (_actionSerialized == null)
            {
                _actionSerialized = new SerializedObject(_actionAsset);
            }

            SerializedProperty shapes = _actionSerialized.FindProperty(propertyRoot);
            if (shapes == null || !shapes.isArray)
            {
                EditorUtility.DisplayDialog("Combat Authoring", "找不到数组字段：" + propertyRoot, "确定");
                return;
            }

            string markerId = EnsureDefaultMarkerIdForNewShape(section);
            if (string.IsNullOrEmpty(markerId))
            {
                EditorUtility.DisplayDialog(
                    "Combat Authoring",
                    "添加 " + section + " 需要先有可用的 Binding marker。请先选择 Scene Binding，并使用“从当前场景生成 Binding”或“使用当前选择重连”。",
                    "确定");
                SetQuickActionStatus("未添加 " + section + "：缺少可绑定的 marker。", UiColors.WarnAmber);
                return;
            }

            int currentFrame = CombatAuthoringSceneState.Frame;
            int index = shapes.arraySize;
            Undo.RecordObject(_actionAsset, "Add Combat " + section);
            shapes.arraySize++;

            SerializedProperty shape = shapes.GetArrayElementAtIndex(index);
            int trackId = GetNextShapeTrackId(shapes);
            shape.FindPropertyRelative("trackId").intValue = trackId;
            shape.FindPropertyRelative("shapeKind").enumValueIndex = (int)CombatAuthoringShapeKind.Sphere;
            SerializedProperty frameRange = shape.FindPropertyRelative("frameRange");
            frameRange.FindPropertyRelative("startFrame").intValue = currentFrame;
            frameRange.FindPropertyRelative("endFrame").intValue = currentFrame;
            shape.FindPropertyRelative("markerId").stringValue = markerId;
            shape.FindPropertyRelative("localCenter").vector3Value = Vector3.zero;
            shape.FindPropertyRelative("radiusRaw").intValue = DefaultShapeRadiusRaw;
            shape.FindPropertyRelative("heightRaw").intValue = DefaultCapsuleHeightRaw;
            shape.FindPropertyRelative("sourceOrder").intValue = index + 1;

            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();
            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(section, trackId));
            RefreshValidation();
            SceneView.RepaintAll();
            SetQuickActionStatus("已添加 " + section + "，绑定 Marker：" + markerId, UiColors.SuccessGreen);
        }

        private string EnsureDefaultMarkerIdForNewShape(string section)
        {
            string markerId = GetDefaultMarkerId();
            if (!string.IsNullOrEmpty(markerId))
            {
                return markerId;
            }

            if (_sceneBindingAsset == null)
            {
                return string.Empty;
            }

            CollectSceneMarkerCandidates(_sceneMarkerCandidates);
            if (_sceneMarkerCandidates.Count == 0)
            {
                return string.Empty;
            }

            SerializedObject serialized = new SerializedObject(_sceneBindingAsset);
            serialized.Update();
            Undo.RecordObject(_sceneBindingAsset, "Prepare Combat Binding Marker");

            FillBindingIdentity(serialized);
            SerializedProperty markers = serialized.FindProperty("markers");
            if (markers != null && markers.arraySize == 0)
            {
                WriteMarkers(markers, _sceneMarkerCandidates);
            }

            SerializedProperty actors = serialized.FindProperty("actors");
            if (actors != null && actors.arraySize == 0)
            {
                WriteActors(actors, _sceneMarkerCandidates);
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_sceneBindingAsset);
            _bindingSerialized = new SerializedObject(_sceneBindingAsset);
            CombatAuthoringSceneState.SetContext(_actionAsset, _sceneBindingAsset);
            SetQuickActionStatus("已为 " + section + " 自动补齐 Binding marker。", UiColors.SuccessGreen);
            return GetDefaultMarkerId();
        }

        private string GetDefaultMarkerId()
        {
            CombatSceneBindingAsset binding = _sceneBindingAsset;
            if (binding != null && binding.Actors != null && binding.Actors.Length > 0)
            {
                CombatActorBindingData actor = binding.Actors[0];
                if (!string.IsNullOrEmpty(actor.MarkerId) && MarkerExistsInBinding(binding, actor.MarkerId))
                {
                    return actor.MarkerId;
                }
            }

            if (binding != null && binding.Markers != null && binding.Markers.Length > 0)
            {
                CombatMarkerBindingData[] markers = binding.Markers;
                for (int i = 0; i < markers.Length; i++)
                {
                    if (!string.IsNullOrEmpty(markers[i].MarkerId))
                    {
                        return markers[i].MarkerId;
                    }
                }
            }

            return string.Empty;
        }

        private static bool MarkerExistsInBinding(CombatSceneBindingAsset binding, string markerId)
        {
            if (binding == null || binding.Markers == null || string.IsNullOrEmpty(markerId))
            {
                return false;
            }

            CombatMarkerBindingData[] markers = binding.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (string.Equals(markers[i].MarkerId, markerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetNextShapeTrackId(SerializedProperty shapes)
        {
            int maxTrackId = 0;
            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                maxTrackId = Math.Max(maxTrackId, element.FindPropertyRelative("trackId").intValue);
            }

            return maxTrackId + 1;
        }

        private int FindTimelineRow(string section, int trackId)
        {
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (row.TrackId == trackId && string.Equals(row.Section, section, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindTimelineRow(TimelineRow target)
        {
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (row.TrackId == target.TrackId
                    && string.Equals(row.Section, target.Section, StringComparison.Ordinal)
                    && string.Equals(row.PropertyPath, target.PropertyPath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void FillBindingIdentity(SerializedObject serialized)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SerializedProperty sceneGuid = serialized.FindProperty("sceneGuid");
            if (sceneGuid != null && string.IsNullOrEmpty(sceneGuid.stringValue) && activeScene.IsValid())
            {
                sceneGuid.stringValue = string.IsNullOrEmpty(activeScene.path)
                    ? activeScene.name
                    : AssetDatabase.AssetPathToGUID(activeScene.path);
            }

            SerializedProperty profileId = serialized.FindProperty("bindingProfileId");
            if (profileId != null && string.IsNullOrEmpty(profileId.stringValue))
            {
                profileId.stringValue = string.IsNullOrEmpty(activeScene.name) ? "default" : activeScene.name;
            }
        }

        private static void WriteMarkers(SerializedProperty markers, List<SceneMarkerCandidate> candidates)
        {
            if (markers == null)
            {
                return;
            }

            markers.arraySize = candidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                SerializedProperty marker = markers.GetArrayElementAtIndex(i);
                marker.FindPropertyRelative("markerId").stringValue = candidates[i].MarkerId;
                marker.FindPropertyRelative("targetPath").stringValue = candidates[i].TargetPath;
                marker.FindPropertyRelative("sourceOrder").intValue = i + 1;
            }
        }

        private static void WriteActors(SerializedProperty actors, List<SceneMarkerCandidate> candidates)
        {
            if (actors == null)
            {
                return;
            }

            int[] actorIndices = GetActorCandidateIndices(candidates);
            int actorCount = actorIndices.Length;
            actors.arraySize = actorCount;
            for (int i = 0; i < actorCount; i++)
            {
                int id = i + 1;
                SceneMarkerCandidate candidate = candidates[actorIndices[i]];
                SerializedProperty actor = actors.GetArrayElementAtIndex(i);
                actor.FindPropertyRelative("entityId").intValue = id;
                actor.FindPropertyRelative("displayName").stringValue = candidate.TransformName;
                actor.FindPropertyRelative("markerId").stringValue = candidate.MarkerId;
                actor.FindPropertyRelative("bodyId").intValue = id;

                SerializedProperty colliders = actor.FindPropertyRelative("colliders");
                colliders.arraySize = 1;
                SerializedProperty collider = colliders.GetArrayElementAtIndex(0);
                collider.FindPropertyRelative("colliderId").intValue = id;
                collider.FindPropertyRelative("markerId").stringValue = candidate.MarkerId;
                collider.FindPropertyRelative("sourceOrder").intValue = id;
            }
        }

        private static int[] GetActorCandidateIndices(List<SceneMarkerCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                return Array.Empty<int>();
            }

            int playerIndex = FindFirstCandidateByScore(candidates, 0);
            int enemyIndex = FindFirstCandidateByScore(candidates, 1);
            if (playerIndex >= 0 && enemyIndex >= 0 && playerIndex != enemyIndex)
            {
                return new[] { playerIndex, enemyIndex };
            }

            if (candidates.Count == 1)
            {
                return new[] { 0 };
            }

            return new[] { 0, 1 };
        }

        private static int FindFirstCandidateByScore(List<SceneMarkerCandidate> candidates, int score)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Score == score)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void UpsertMarker(SerializedProperty markers, string markerId, string targetPath)
        {
            if (markers == null)
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < markers.arraySize; i++)
            {
                SerializedProperty marker = markers.GetArrayElementAtIndex(i);
                SerializedProperty currentId = marker.FindPropertyRelative("markerId");
                if (string.Equals(currentId.stringValue, markerId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = markers.arraySize;
                markers.arraySize++;
            }

            SerializedProperty target = markers.GetArrayElementAtIndex(index);
            target.FindPropertyRelative("markerId").stringValue = markerId;
            target.FindPropertyRelative("targetPath").stringValue = targetPath;
            target.FindPropertyRelative("sourceOrder").intValue = index + 1;
        }

        private void CollectSceneMarkerCandidates(List<SceneMarkerCandidate> candidates)
        {
            candidates.Clear();
            _markerIdBuffer.Clear();

            var scenes = new List<Scene>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    scenes.Add(scene);
                }
            }

            scenes.Sort(CompareScenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                CollectSceneMarkerCandidates(scenes[i], candidates);
            }

            candidates.Sort(CompareSceneMarkerCandidates);
            if (candidates.Count > MaxSceneBindingMarkers)
            {
                candidates.RemoveRange(MaxSceneBindingMarkers, candidates.Count - MaxSceneBindingMarkers);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                SceneMarkerCandidate candidate = candidates[i];
                candidate.MarkerId = MakeUniqueMarkerId(candidate.MarkerId, _markerIdBuffer);
                candidates[i] = candidate;
            }
        }

        private static void CollectSceneMarkerCandidates(Scene scene, List<SceneMarkerCandidate> candidates)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootGameObjects);
            for (int i = 0; i < roots.Length; i++)
            {
                CollectSceneMarkerCandidates(roots[i].transform, candidates);
            }
        }

        private static void CollectSceneMarkerCandidates(Transform current, List<SceneMarkerCandidate> candidates)
        {
            if (current == null
                || EditorUtility.IsPersistent(current)
                || (current.hideFlags & HideFlags.HideInHierarchy) != 0)
            {
                return;
            }

            string targetPath = GetHierarchyPath(current);
            int score = GetMarkerCandidateScore(current.name);
            if (score < int.MaxValue)
            {
                candidates.Add(new SceneMarkerCandidate(
                    current.name,
                    targetPath,
                    CreateReadableMarkerId(current.name),
                    score));
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectSceneMarkerCandidates(current.GetChild(i), candidates);
            }
        }

        private static int GetMarkerCandidateScore(string name)
        {
            if (ContainsOrdinalIgnoreCase(name, "Player"))
            {
                return 0;
            }

            if (ContainsOrdinalIgnoreCase(name, "Enemy"))
            {
                return 1;
            }

            if (ContainsOrdinalIgnoreCase(name, "Marker"))
            {
                return 2;
            }

            return ContainsOrdinalIgnoreCase(name, "Combat") ? 3 : int.MaxValue;
        }

        private static bool ContainsOrdinalIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CreateReadableMarkerId(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            bool previousWasSeparator = false;
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousWasSeparator = false;
                    continue;
                }

                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.ToString();
        }

        private static string MakeUniqueMarkerId(string baseId, HashSet<string> usedIds)
        {
            string stableBase = string.IsNullOrEmpty(baseId) ? "Marker" : baseId;
            string markerId = stableBase;
            int suffix = 2;
            while (!usedIds.Add(markerId))
            {
                markerId = stableBase + "_" + suffix;
                suffix++;
            }

            return markerId;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static int CompareScenes(Scene left, Scene right)
        {
            int pathCompare = string.CompareOrdinal(left.path, right.path);
            return pathCompare != 0 ? pathCompare : string.CompareOrdinal(left.name, right.name);
        }

        private static int CompareRootGameObjects(GameObject left, GameObject right)
        {
            return string.CompareOrdinal(left == null ? string.Empty : left.name, right == null ? string.Empty : right.name);
        }

        private static int CompareSceneMarkerCandidates(SceneMarkerCandidate left, SceneMarkerCandidate right)
        {
            int scoreCompare = left.Score.CompareTo(right.Score);
            return scoreCompare != 0 ? scoreCompare : string.CompareOrdinal(left.TargetPath, right.TargetPath);
        }

        private static void PingObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void CopyReport()
        {
            if (string.IsNullOrEmpty(_lastReportText))
            {
                RefreshValidation();
            }

            EditorGUIUtility.systemCopyBuffer = _lastReportText;
        }

        private void ExportJson()
        {
            ExportJsonPackage(true);
        }

        private void ExportJsonPackage(bool promptForFolder)
        {
            if (_actionSerialized != null)
            {
                _actionSerialized.ApplyModifiedProperties();
            }

            if (_bindingSerialized != null)
            {
                _bindingSerialized.ApplyModifiedProperties();
            }

            string sourceGuid = GetAssetGuid(_actionAsset);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                sourceGuid = GetAssetGuid(_sceneBindingAsset);
            }

            _lastExportResult = CombatAuthoringJsonExporter.Export(
                _actionAsset,
                _sceneBindingAsset,
                BuildPackageId(_actionAsset),
                sourceGuid,
                CombatAuthoringJsonExporter.DefaultToolVersion);
            _lastExportReportText = _lastExportResult.ReportText;

            if (_lastExportResult.Success && promptForFolder)
            {
                _lastExportReportText = AppendWriteStatus(_lastExportReportText, WriteExportPackage(_lastExportResult));
            }

            _lastReport = _lastExportResult.ValidationReport;
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastExportReportText);

            if (_validationLabel != null)
            {
                if (_lastExportResult.Success)
                {
                    _validationLabel.text = "\u2705 导出 JSON 已生成：" + _lastExportResult.Package.FileCount + " files";
                    _validationLabel.style.color = _lastReport != null && _lastReport.IssueCount > 0
                        ? UiColors.WarnAmber
                        : UiColors.SuccessGreen;
                }
                else
                {
                    _validationLabel.text = "\u274C 导出 JSON 失败：validation gate";
                    _validationLabel.style.color = UiColors.ErrorRed;
                }
            }
        }

        private void CopyExportReport()
        {
            if (string.IsNullOrEmpty(_lastExportReportText))
            {
                ExportJsonPackage(false);
            }

            EditorGUIUtility.systemCopyBuffer = _lastExportReportText;
        }

        private static string AppendWriteStatus(string reportText, string writeStatus)
        {
            if (string.IsNullOrEmpty(writeStatus))
            {
                return reportText;
            }

            return (reportText ?? string.Empty) + Environment.NewLine + "Editor Write:" + Environment.NewLine + writeStatus;
        }

        private static string WriteExportPackage(CombatAuthoringExportResult result)
        {
            string selectedFolder = EditorUtility.SaveFolderPanel(
                "Export Combat Authoring JSON Package",
                Application.dataPath,
                result.Context.PackageId);
            if (string.IsNullOrEmpty(selectedFolder))
            {
                return "Skipped: no folder selected. Package exists in memory and file paths are listed above.";
            }

            try
            {
                CombatAuthoringExportFile[] files = result.Files;
                for (int i = 0; i < files.Length; i++)
                {
                    string filePath = Path.Combine(selectedFolder, files[i].Path.Replace('/', Path.DirectorySeparatorChar));
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(filePath, files[i].Content, Encoding.UTF8);
                }

                RefreshAssetDatabaseIfProjectPath(selectedFolder);
                return "Wrote " + files.Length + " files to: " + selectedFolder;
            }
            catch (Exception ex)
            {
                return "Write failed: " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void RefreshAssetDatabaseIfProjectPath(string selectedFolder)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullSelectedFolder = Path.GetFullPath(selectedFolder);
            if (fullSelectedFolder.StartsWith(projectRoot, StringComparison.Ordinal))
            {
                AssetDatabase.Refresh();
            }
        }

        private void PreviewExplain()
        {
            var resolver = new SceneMarkerResolver(_sceneBindingAsset);
            int frame = CombatAuthoringSceneState.Frame;
            _lastPreviewReport = CombatAuthoringPreviewExplainer.Explain(_actionAsset, _sceneBindingAsset, frame, resolver);
            _lastPreviewText = _lastPreviewReport.ToDisplayText();
            if (_previewExplain != null)
            {
                _previewExplain.value = _lastPreviewText;
            }
        }

        private void CopyExplain()
        {
            if (string.IsNullOrEmpty(_lastPreviewText))
            {
                PreviewExplain();
            }

            EditorGUIUtility.systemCopyBuffer = _lastPreviewText;
        }

        private void RunShowcasePreview()
        {
            if (_actionSerialized != null)
                _actionSerialized.ApplyModifiedProperties();
            if (_bindingSerialized != null)
                _bindingSerialized.ApplyModifiedProperties();

            _lastReport = CombatAuthoringValidator.Validate(_actionAsset, _sceneBindingAsset);
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastReportText);

            if (_lastReport != null && _lastReport.HasErrors)
            {
                CombatAuthoringIssue firstError = FirstIssue(CombatAuthoringSeverity.Error);
                string message = "Showcase 预览已阻断：validation 存在 Error。请先处理 "
                    + firstError.Section + " / " + firstError.Field + "，建议：" + firstError.SuggestedFix;
                SetQuickActionStatus(message, UiColors.ErrorRed);
                return;
            }

            if (CombatAuthoringShowcasePlaySession.Begin(_actionAsset, _sceneBindingAsset, _lastReport, out string status))
            {
                SetQuickActionStatus(status, UiColors.SuccessGreen);
            }
            else
            {
                SetQuickActionStatus(status, UiColors.ErrorRed);
            }

            RefreshContext();
        }

        private void ClearShowcasePreviewSession()
        {
            CombatAuthoringShowcasePlaySession.Clear();
            SetQuickActionStatus(CombatAuthoringShowcasePlaySession.LastStatus, UiColors.TextSecondary);
            RefreshContext();
        }

        private CombatAuthoringIssue FirstIssue(CombatAuthoringSeverity severity)
        {
            if (_lastReport == null)
                return default;

            for (int i = 0; i < _lastReport.IssueCount; i++)
            {
                CombatAuthoringIssue issue = _lastReport.GetIssue(i);
                if (issue.Severity == severity)
                    return issue;
            }

            return default;
        }

        private static string BuildReportText(CombatAuthoringReport report)
        {
            return CombatAuthoringExportReport.BuildValidationText(report);
        }

        private static string IssueSeverityIcon(CombatAuthoringSeverity severity)
        {
            switch (severity)
            {
                case CombatAuthoringSeverity.Error:
                    return "\u274C";
                case CombatAuthoringSeverity.Warning:
                    return "\u26A0";
                default:
                    return "\u2139";
            }
        }

        private static Color IssueSeverityColor(CombatAuthoringSeverity severity)
        {
            switch (severity)
            {
                case CombatAuthoringSeverity.Error:
                    return UiColors.ErrorRed;
                case CombatAuthoringSeverity.Warning:
                    return UiColors.WarnAmber;
                default:
                    return UiColors.TextSecondary;
            }
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static string BuildPackageId(CombatActionAuthoringAsset action)
        {
            int actionId = action == null ? 0 : action.ActionId;
            return "combat_authoring_action_" + actionId;
        }

        private static string FormatRange(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? "empty" : range.StartFrame + "-" + range.EndFrame;
        }

        private void UpdateTimelinePlayhead()
        {
            if (_timelinePlayhead == null || _actionAsset == null)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            float trackWidth = Math.Max(TimelineStripMinTrackWidth, totalFrames * TimelineStripPixelsPerFrame);
            float frameX = FrameToTimelineX(CombatAuthoringSceneState.Frame, totalFrames, trackWidth);
            _timelinePlayhead.style.left = TimelineStripLabelWidth + frameX;
        }

        private static float FrameToTimelineX(int frame, int totalFrames, float trackWidth)
        {
            int clampedFrame = Math.Max(0, Math.Min(frame, Math.Max(0, totalFrames - 1)));
            return clampedFrame * GetTimelineFrameWidth(totalFrames, trackWidth);
        }

        private static float GetTimelineFrameWidth(int totalFrames, float trackWidth)
        {
            return trackWidth / Math.Max(1, totalFrames);
        }

        private static int GetTimelineTickStep(int totalFrames)
        {
            if (totalFrames <= 12)
            {
                return 1;
            }

            if (totalFrames <= 30)
            {
                return 5;
            }

            if (totalFrames <= 90)
            {
                return 10;
            }

            return 15;
        }

        private static Label CreateTimelineCell(string text, float left, float top, float width, float height, Color backgroundColor)
        {
            var label = new Label(text);
            label.tooltip = Tooltip(text);
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.width = width;
            label.style.height = height;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 4;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.fontSize = 11;
            label.style.color = UiColors.TimelineHeaderText;
            label.style.backgroundColor = backgroundColor;
            return label;
        }

        private static VisualElement CreateTimelineBlock(float left, float top, float width, float height, Color color)
        {
            var block = new VisualElement();
            block.style.position = Position.Absolute;
            block.style.left = left;
            block.style.top = top;
            block.style.width = width;
            block.style.height = height;
            block.style.backgroundColor = color;
            return block;
        }

        private static VisualElement CreateTimelineRangeHandle(bool left)
        {
            var handle = new VisualElement();
            handle.pickingMode = PickingMode.Ignore;
            handle.style.position = Position.Absolute;
            handle.style.top = 0;
            handle.style.width = TimelineRangeEdgeHandleWidth;
            handle.style.height = Length.Percent(100);
            handle.style.backgroundColor = UiColors.HandleDefault;
            if (left)
            {
                handle.style.left = 0;
            }
            else
            {
                handle.style.right = 0;
            }

            return handle;
        }

        private static void SetTimelineHandleOpacity(VisualElement leftHandle, VisualElement rightHandle, float opacity)
        {
            if (leftHandle != null)
            {
                leftHandle.style.opacity = opacity;
            }

            if (rightHandle != null)
            {
                rightHandle.style.opacity = opacity;
            }
        }

        private static Color GetTimelineBarColor(TimelineRow row)
        {
            if (string.Equals(row.Section, "Action", StringComparison.Ordinal))
            {
                if (string.Equals(row.Label, "Startup", StringComparison.Ordinal))
                {
                    return UiColors.BarStartup;
                }

                if (string.Equals(row.Label, "Active", StringComparison.Ordinal))
                {
                    return UiColors.BarActive;
                }

                return UiColors.BarRecovery;
            }

            if (string.Equals(row.Section, "Hitbox", StringComparison.Ordinal))
            {
                return UiColors.BarHitbox;
            }

            if (string.Equals(row.Section, "Hurtbox", StringComparison.Ordinal))
            {
                return UiColors.BarHurtbox;
            }

            return UiColors.BarTrace;
        }

        private sealed class SceneMarkerResolver : ICombatAuthoringPreviewMarkerResolver
        {
            private readonly Dictionary<string, string> _markerPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<CombatMarkerBindingData> _markers = new List<CombatMarkerBindingData>(64);
            private readonly List<Transform> _transforms = new List<Transform>(64);

            public SceneMarkerResolver(CombatSceneBindingAsset binding)
            {
                if (binding?.Markers == null)
                {
                    return;
                }

                _markers.AddRange(binding.Markers);
                _markers.Sort(ComparePreviewMarkers);
                for (int i = 0; i < _markers.Count; i++)
                {
                    CombatMarkerBindingData marker = _markers[i];
                    if (string.IsNullOrEmpty(marker.MarkerId) || _markerPaths.ContainsKey(marker.MarkerId))
                    {
                        continue;
                    }

                    _markerPaths.Add(marker.MarkerId, marker.TargetPath ?? string.Empty);
                }
            }

            public bool TryResolveMarker(string markerId, out CombatAuthoringPreviewMarkerPose pose)
            {
                if (string.IsNullOrEmpty(markerId) || !_markerPaths.TryGetValue(markerId, out string targetPath))
                {
                    pose = default;
                    return false;
                }

                Transform transform = ResolveTransformPath(targetPath);
                if (transform == null)
                {
                    pose = default;
                    return false;
                }

                pose = new CombatAuthoringPreviewMarkerPose(transform.position, transform.rotation);
                return true;
            }

            private Transform ResolveTransformPath(string targetPath)
            {
                if (string.IsNullOrEmpty(targetPath))
                {
                    return null;
                }

                GameObject direct = GameObject.Find(targetPath);
                if (direct != null)
                {
                    return direct.transform;
                }

                _transforms.Clear();
                Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null
                        || EditorUtility.IsPersistent(candidate)
                        || (candidate.hideFlags & HideFlags.HideInHierarchy) != 0)
                    {
                        continue;
                    }

                    string candidatePath = GetHierarchyPath(candidate);
                    if (string.Equals(candidatePath, targetPath, StringComparison.Ordinal)
                        || string.Equals(candidate.name, targetPath, StringComparison.Ordinal))
                    {
                        _transforms.Add(candidate);
                    }
                }

                _transforms.Sort(ComparePreviewTransformsByPath);
                return _transforms.Count == 0 ? null : _transforms[0];
            }
        }

        private static int ComparePreviewMarkers(CombatMarkerBindingData left, CombatMarkerBindingData right)
        {
            int markerCompare = string.CompareOrdinal(left.MarkerId, right.MarkerId);
            return markerCompare != 0 ? markerCompare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int ComparePreviewTransformsByPath(Transform left, Transform right)
        {
            return string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right));
        }

        private sealed class TimelineBarData
        {
            public TimelineBarData(int rowIndex, Label rangeLabel, VisualElement leftHandle, VisualElement rightHandle, int totalFrames, float trackWidth)
            {
                RowIndex = rowIndex;
                RangeLabel = rangeLabel;
                LeftHandle = leftHandle;
                RightHandle = rightHandle;
                TotalFrames = totalFrames;
                TrackWidth = trackWidth;
            }

            public int RowIndex { get; }

            public Label RangeLabel { get; }

            public VisualElement LeftHandle { get; }

            public VisualElement RightHandle { get; }

            public int TotalFrames { get; }

            public float TrackWidth { get; }
        }

        private sealed class TimelineDragState
        {
            public bool IsDragging { get; private set; }

            public int RowIndex { get; private set; }

            public TimelineRow Row { get; private set; }

            public TimelineDragMode Mode { get; private set; }

            public int OriginalStartFrame { get; private set; }

            public int OriginalEndFrame { get; private set; }

            public int PointerStartFrame { get; private set; }

            public int PreviewStartFrame { get; private set; }

            public int PreviewEndFrame { get; private set; }

            public int TotalFrames { get; private set; }

            public float TrackWidth { get; private set; }

            public int PointerId { get; private set; }

            public VisualElement Bar { get; private set; }

            public Label RangeLabel { get; private set; }

            public bool HasPreviewChange => PreviewStartFrame != OriginalStartFrame || PreviewEndFrame != OriginalEndFrame;

            public void Begin(
                int rowIndex,
                TimelineRow row,
                TimelineDragMode mode,
                int originalStartFrame,
                int originalEndFrame,
                int pointerStartFrame,
                int totalFrames,
                float trackWidth,
                int pointerId,
                VisualElement bar,
                Label rangeLabel,
                VisualElement leftHandle,
                VisualElement rightHandle)
            {
                IsDragging = true;
                RowIndex = rowIndex;
                Row = row;
                Mode = mode;
                OriginalStartFrame = originalStartFrame;
                OriginalEndFrame = originalEndFrame;
                PointerStartFrame = pointerStartFrame;
                PreviewStartFrame = originalStartFrame;
                PreviewEndFrame = originalEndFrame;
                TotalFrames = totalFrames;
                TrackWidth = trackWidth;
                PointerId = pointerId;
                Bar = bar;
                RangeLabel = rangeLabel;
                SetTimelineHandleOpacity(leftHandle, rightHandle, 1f);
            }

            public void SetPreview(int startFrame, int endFrame)
            {
                PreviewStartFrame = startFrame;
                PreviewEndFrame = endFrame;
            }

            public void Reset()
            {
                IsDragging = false;
                RowIndex = -1;
                Row = default;
                Mode = TimelineDragMode.None;
                OriginalStartFrame = 0;
                OriginalEndFrame = 0;
                PointerStartFrame = 0;
                PreviewStartFrame = 0;
                PreviewEndFrame = 0;
                TotalFrames = 0;
                TrackWidth = 0f;
                PointerId = 0;
                Bar = null;
                RangeLabel = null;
            }
        }

        private enum TimelineDragMode
        {
            None,
            Move,
            ResizeStart,
            ResizeEnd,
        }

        private readonly struct ShapeSnapshot
        {
            private ShapeSnapshot(
                int shapeKindEnumIndex,
                int startFrame,
                int endFrame,
                string markerId,
                Vector3 localCenter,
                int radiusRaw,
                int heightRaw)
            {
                ShapeKindEnumIndex = shapeKindEnumIndex;
                StartFrame = startFrame;
                EndFrame = endFrame;
                MarkerId = markerId;
                LocalCenter = localCenter;
                RadiusRaw = radiusRaw;
                HeightRaw = heightRaw;
            }

            public int ShapeKindEnumIndex { get; }

            public int StartFrame { get; }

            public int EndFrame { get; }

            public string MarkerId { get; }

            public Vector3 LocalCenter { get; }

            public int RadiusRaw { get; }

            public int HeightRaw { get; }

            public static ShapeSnapshot Read(SerializedProperty source)
            {
                SerializedProperty frameRange = source.FindPropertyRelative("frameRange");
                return new ShapeSnapshot(
                    ReadEnum(source, "shapeKind"),
                    ReadInt(frameRange, "startFrame"),
                    ReadInt(frameRange, "endFrame"),
                    ReadString(source, "markerId"),
                    ReadVector3(source, "localCenter"),
                    ReadInt(source, "radiusRaw"),
                    ReadInt(source, "heightRaw"));
            }

            public static void Write(SerializedProperty target, ShapeSnapshot snapshot, int trackId, int sourceOrder)
            {
                WriteInt(target, "trackId", trackId);
                WriteEnum(target, "shapeKind", snapshot.ShapeKindEnumIndex);
                SerializedProperty frameRange = target.FindPropertyRelative("frameRange");
                WriteInt(frameRange, "startFrame", snapshot.StartFrame);
                WriteInt(frameRange, "endFrame", snapshot.EndFrame);
                WriteString(target, "markerId", snapshot.MarkerId);
                WriteVector3(target, "localCenter", snapshot.LocalCenter);
                WriteInt(target, "radiusRaw", snapshot.RadiusRaw);
                WriteInt(target, "heightRaw", snapshot.HeightRaw);
                WriteInt(target, "sourceOrder", sourceOrder);
            }

            private static int ReadInt(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? 0 : property.intValue;
            }

            private static int ReadEnum(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? 0 : property.enumValueIndex;
            }

            private static string ReadString(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? string.Empty : property.stringValue;
            }

            private static Vector3 ReadVector3(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? Vector3.zero : property.vector3Value;
            }

            private static void WriteInt(SerializedProperty parent, string name, int value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.intValue = value;
                }
            }

            private static void WriteEnum(SerializedProperty parent, string name, int value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.enumValueIndex = value;
                }
            }

            private static void WriteString(SerializedProperty parent, string name, string value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.stringValue = value ?? string.Empty;
                }
            }

            private static void WriteVector3(SerializedProperty parent, string name, Vector3 value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.vector3Value = value;
                }
            }
        }

        private readonly struct TimelineRow
        {
            public TimelineRow(
                string section,
                string label,
                int trackId,
                CombatAuthoringFrameRange frameRange,
                string propertyPath,
                TimelineRowKind kind)
            {
                Section = section;
                Label = label;
                TrackId = trackId;
                FrameRange = frameRange;
                PropertyPath = propertyPath;
                Kind = kind;
            }

            public string Section { get; }

            public string Label { get; }

            public int TrackId { get; }

            public CombatAuthoringFrameRange FrameRange { get; }

            public string PropertyPath { get; }

            public TimelineRowKind Kind { get; }
        }

        private enum TimelineRowKind
        {
            ActionProperty,
        }

        private readonly struct RawPreset
        {
            public RawPreset(string label, int rawValue)
            {
                Label = label;
                RawValue = rawValue;
            }

            public string Label { get; }

            public int RawValue { get; }
        }

        private readonly struct MarkerDropdownItem
        {
            public MarkerDropdownItem(string markerId, string label, bool isPlaceholder)
            {
                MarkerId = markerId;
                Label = label;
                IsPlaceholder = isPlaceholder;
            }

            public string MarkerId { get; }

            public string Label { get; }

            public bool IsPlaceholder { get; }
        }

        private struct SceneMarkerCandidate
        {
            public SceneMarkerCandidate(string transformName, string targetPath, string markerId, int score)
            {
                TransformName = transformName;
                TargetPath = targetPath;
                MarkerId = markerId;
                Score = score;
            }

            public string TransformName { get; }

            public string TargetPath { get; }

            public string MarkerId { get; set; }

            public int Score { get; }
        }
    }
}
