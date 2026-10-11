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
        public static readonly Color ButtonActive = new Color(0.30f, 0.30f, 0.35f);
        public static readonly Color ButtonPrimaryBg = new Color(0.22f, 0.22f, 0.28f);
        public static readonly Color ButtonPrimaryBorder = new Color(0.82f, 0.3f, 0.24f);
        public static readonly Color ButtonPrimaryHover = new Color(0.28f, 0.28f, 0.35f);

        // Status Indicators
        public static readonly Color StatusSuccessBg = new Color(0.15f, 0.35f, 0.18f);
        public static readonly Color StatusWarnBg = new Color(0.35f, 0.28f, 0.10f);
        public static readonly Color StatusErrorBg = new Color(0.35f, 0.15f, 0.12f);
        public static readonly Color StatusInfoBg = new Color(0.15f, 0.25f, 0.35f);

        // Timeline Enhancements (new additions only)
        public static readonly Color TimelineEmptyLane = new Color(0.12f, 0.12f, 0.12f, 0.5f);

        // Panel & Card
        public static readonly Color PanelSurface = new Color(0.18f, 0.18f, 0.18f);
        public static readonly Color PanelSurfaceAlt = new Color(0.16f, 0.16f, 0.16f);
        public static readonly Color PanelDeep = new Color(0.12f, 0.12f, 0.12f);
        public static readonly Color PanelBorder = new Color(0.26f, 0.26f, 0.26f);

        // Icons (emoji/unicode rendered as labels)
        // Icon constants are defined inline where used for flexibility
    }

    internal enum CombatAuthoringWindowMode
    {
        Inspector,
        Timeline,
    }

    public sealed partial class CombatAuthoringWindow : EditorWindow
    {
        private const string AuthoringMenuPath = "MxFramework/Combat/Combat Authoring";
        private const string TimelineMenuPath = "MxFramework/Combat/Combat Timeline";
        private const string LayoutMenuPath = "MxFramework/Combat/Open Authoring Layout";
        private const float TimelineRowHeight = 28f;
        private const float TimelineStripHeaderHeight = 32f;
        private const float TimelineStripLaneHeight = 36f;
        private const float TimelineStripLabelWidth = 180f;
        private const int TimelineStripDefaultPixelsPerFrame = 42;
        private const int TimelineStripMinPixelsPerFrame = 4;
        private const int TimelineStripMaxPixelsPerFrame = 96;
        private const float TimelineStripMinHeight = 360f;
        private const float TimelineRangeEdgeHandleWidth = 12f;
        private const float TimelineDetailsHeight = 140f;
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

        [SerializeField]
        private CombatActionAuthoringAsset _actionAsset;

        [SerializeField]
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
        private VisualElement _validationStatusIndicator;
        private SliderInt _frameSlider;
        private Button _playButton;
        private Button _speedButton;
        private Button _modeButton;
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
        private MinMaxSlider _timelineViewportSlider;
        private Label _timelineViewportLabel;
        private ListView _timelineList;
        private ListView _issueList;
        private TextField _reportPreview;
        private TextField _previewExplain;
        private int _selectedTimelineRowIndex = -1;
        private bool _ignoreNextTimelineBarClick;
        private bool _finishingTimelineDrag;
        private bool _keyboardShortcutsRegistered;
        private bool _isApplyingLocalSerializedRefresh;
        private bool _isApplyingTimelineViewport;
        private bool _isInteractingWithTimelineViewport;
        private int _observedDataRevision = -1;

        [SerializeField]
        private CombatAuthoringWindowMode _mode = CombatAuthoringWindowMode.Inspector;

        [SerializeField]
        private int _timelinePixelsPerFrame = TimelineStripDefaultPixelsPerFrame;

        [SerializeField]
        private string _persistentSelectionSection = string.Empty;

        [SerializeField]
        private int _persistentSelectionTrackId = -1;

        [SerializeField]
        private string _persistentSelectionPropertyPath = string.Empty;

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

    }
}
