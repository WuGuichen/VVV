using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using MxFramework.Editor.CharacterAction;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    /// <summary>UI Toolkit controller for generic manual VFX composition.</summary>
    /// <remarks>Editor-only, AllocByDesign. All preview state is isolated and disposable.</remarks>
    internal sealed partial class VfxManualCompositionStudioController : IDisposable
    {
        private const int CompositionBudgetLimit = 200;
        private const string ElementPreviewLeftPreference = "MxFramework.Vfx.ElementQuickPreview.Left";
        private const string ElementPreviewTopPreference = "MxFramework.Vfx.ElementQuickPreview.Top";
        private const string LiveRefreshPreference = "MxFramework.Vfx.ManualComposition.LiveRefresh";
        private const string LayerRegionHeightPreference = "MxFramework.Vfx.ManualComposition.LayerRegionHeight";
        private const string LayerRegionCollapsedPreference = "MxFramework.Vfx.ManualComposition.LayerRegionCollapsed";
        private const string PreviewMaximizedPreference = "MxFramework.Vfx.ManualComposition.PreviewMaximized";
        private const string WorkspacePreferencePrefix = "MxFramework.Vfx.ManualComposition.Workspace.";
        private const float DefaultLayerRegionHeight = 260f;
        private const string NoSemanticChoice = "无";
        internal const int PreviewFramesPerSecond = 60;
        private static readonly string[] LifecyclePhases = { "Build", "Rebuild", "Loop", "Complete", "Dissipate" };
        private static readonly string[] SemanticRoles = { "flash", "core", "glow", "flame", "sparks", "smoke", "trail", "decal", "shockwave", "arc", "beam", "rune", "slash", "ring", "debris", "liquid", "distortion", "ribbon", "embers", "detail" };
        private static readonly string[] SemanticThemes = { "Fire", "Lightning", "Poison", "Ice", "Holy", "Dark", "Blood", "Mana", "Water", "Wind", "Nature", "Generic" };
        private static readonly string[] SemanticFamilies = { "General", "Impact", "Projectile", "Casting", "Aura", "Zone", "Telegraph", "Beam", "Melee", "Shield", "Status", "Environment", "Portal", "Healing", "Movement" };
        private static readonly string[] SemanticUseCases = { "General", "Impact", "Projectile", "Casting", "Aura", "Zone", "Telegraph", "Beam", "Melee", "Shield", "Status", "Environment", "Portal", "Healing", "Movement" };
        private static readonly string[] SemanticMotions = { "Directional", "Expanding", "Orbiting", "Rising", "Falling", "Radial", "Sustained", "Stationary" };
        private static readonly string[] SemanticTimingPhases = { "Anticipation", "Peak", "Dissipation" };
        private static readonly string[] SemanticQualityTiers = { "Normal", "GameplayImportant", "Showcase", "SourceComplex" };
        private static readonly string[] SemanticShapes = { "Point", "Sphere", "Hemisphere", "Cone", "Box", "Circle", "Edge", "Mesh", "MeshRenderer", "SkinnedMeshRenderer", "Donut", "Rectangle", "Sprite", "Trail", "Beam", "Arc", "Ground", "Volume", "Generic" };
        private static readonly string[] SemanticAnchors = { "World", "HitPoint", "CasterRoot", "TargetRoot", "Projectile", "Path", "Ground", "Socket" };
        private readonly VisualElement _root;
        private readonly Action _showSourceImportWorkspace;
        private readonly Action _showManualWorkspace;
        private readonly VfxSourceElementCurationService _curationService = new VfxSourceElementCurationService();
        private readonly VfxCompositionDraftService _draftService = new VfxCompositionDraftService();
        private readonly VfxCompositionRecoveryJournalService _recoveryJournal = new VfxCompositionRecoveryJournalService();
        private readonly VfxCompositionCandidateBaker _baker = new VfxCompositionCandidateBaker();
        private readonly VfxCharacterPreviewCatalog _characterCatalogService = new VfxCharacterPreviewCatalog();
        private readonly VfxLibraryPreviewSession _preview = new VfxLibraryPreviewSession();
        private readonly VfxLibraryPreviewSession _comparisonPreview = new VfxLibraryPreviewSession();
        private readonly VfxLibraryPreviewSession _elementPreview = new VfxLibraryPreviewSession();
        private readonly List<VfxSourceElementView> _filteredElements = new List<VfxSourceElementView>();
        private readonly List<VfxElementCardRow> _elementRows = new List<VfxElementCardRow>();
        private readonly List<int> _visibleLayerIndices = new List<int>();
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, VfxCharacterPreviewDescriptor> _charactersByLabel = new Dictionary<string, VfxCharacterPreviewDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterActionAnimationClipChoice> _animationsByLabel = new Dictionary<string, CharacterActionAnimationClipChoice>(StringComparer.Ordinal);
        private readonly Dictionary<string, VfxCharacterPreviewSocket> _socketsByLabel = new Dictionary<string, VfxCharacterPreviewSocket>(StringComparer.Ordinal);
        private readonly HashSet<string> _favoriteElementIds = LoadFavoriteElementIds();
        private bool _compactElementGrid;
        private bool _restoringWorkspace = true;
        private VfxSourceElementCatalog _catalog;
        private VfxSemanticPropertyRegistryView _propertyRegistry;
        private VfxLibraryWorkspace _workspace;
        private VfxCompositionDraftSession _session;
        private VfxSourceElementView _selectedElement;
        private VfxSourceElementView _elementPreviewElement;
        private string _soloLayerId = string.Empty;
        private VfxCompositionDraftView _comparisonDraft;
        private bool _binding;
        private float _timelineWindowSeconds = 4f;
        private int _timelineWindowFrameCount = 240;
        private IReadOnlyList<VfxCharacterPreviewDescriptor> _characterChoices = Array.Empty<VfxCharacterPreviewDescriptor>();
        private VfxCharacterPreviewDescriptor _selectedCharacter;

        private readonly ToolbarSearchField _search;
        private readonly DropdownField _sourceFilter;
        private readonly DropdownField _roleFilter;
        private readonly DropdownField _themeFilter;
        private readonly DropdownField _familyFilter;
        private readonly DropdownField _anchorFilter;
        private readonly DropdownField _qualityFilter;
        private readonly DropdownField _packageFilter;
        private readonly DropdownField _shapeFilter;
        private readonly DropdownField _lifecycleFilter;
        private readonly DropdownField _extractionFilter;
        private readonly DropdownField _useCaseFilter;
        private readonly DropdownField _motionFilter;
        private readonly DropdownField _timingFilter;
        private readonly DropdownField _spaceFilter;
        private readonly DropdownField _rendererFilter;
        private readonly DropdownField _budgetTierFilter;
        private readonly DropdownField _granularityFilter;
        private readonly ListView _elementList;
        private readonly ListView _layerList;
        private readonly Label _elementSummary;
        private readonly Label _elementVisibleCount;
        private readonly Label _elementDetail;
        private readonly VisualElement _elementIndexUnavailable;
        private readonly Label _elementIndexUnavailableStatus;
        private readonly Button _elementIndexRebuild;
        private readonly Foldout _elementSemanticEditor;
        private readonly DropdownField _elementSemanticMember;
        private readonly TextField _elementSemanticName;
        private readonly TextField _elementSemanticMemberName;
        private readonly DropdownField _elementSemanticRolePrimary;
        private readonly DropdownField _elementSemanticRoleSecondary;
        private readonly DropdownField _elementSemanticRoleTertiary;
        private readonly DropdownField _elementSemanticTheme;
        private readonly DropdownField _elementSemanticFamily;
        private readonly DropdownField _elementSemanticShape;
        private readonly DropdownField _elementSemanticAnchor;
        private readonly DropdownField _elementSemanticUseCasePrimary;
        private readonly DropdownField _elementSemanticUseCaseSecondary;
        private readonly DropdownField _elementSemanticTiming;
        private readonly DropdownField _elementSemanticMotion;
        private readonly DropdownField _elementSemanticQuality;
        private readonly Label _elementSemanticTechnical;
        private readonly Toggle _elementSemanticPreferred;
        private readonly Toggle _elementSemanticDeprecated;
        private readonly TextField _elementSemanticNote;
        private readonly Label _elementSemanticStatus;
        private readonly Button _elementSemanticSave;
        private readonly Button _elementSemanticReset;
        private readonly Label _diagnostics;
        private readonly Button _saveDraftButton;
        private readonly Label _saveDraftStatus;
        private readonly Label _draftSaveState;
        private readonly Label _draftPrefabSyncState;
        private readonly Label _draftPublicationState;
        private readonly DropdownField _previewStage;
        private readonly DropdownField _loopMode;
        private readonly DropdownField _background;
        private readonly DropdownField _postProcessingMode;
        private readonly DropdownField _cameraMode;
        private readonly Vector3Field _rootPosition;
        private readonly Toggle _gridToggle;
        private readonly VisualElement _characterPreviewContext;
        private readonly Button _characterPreviewContextToggle;
        private readonly DropdownField _characterPreviewEditTarget;
        private readonly Label _characterPreviewActivePhase;
        private readonly Label _characterPreviewOverrideSummary;
        private readonly Button _characterPreviewResetPhase;
        private readonly DropdownField _characterPreviewMode;
        private readonly DropdownField _characterPreviewCharacter;
        private readonly DropdownField _characterPreviewPose;
        private readonly DropdownField _characterPreviewAnimation;
        private readonly DropdownField _characterPreviewSocket;
        private readonly Toggle _characterAnimationLoop;
        private readonly Slider _characterAnimationSpeed;
        private readonly Toggle _characterVisible;
        private readonly Toggle _characterSocketAxes;
        private readonly Label _characterPreviewStatus;
        private readonly Button[] _cameraPresetButtons;
        private readonly Slider _compositionSpeed;
        private readonly Toggle _abToggle;
        private readonly Slider _timeline;
        private readonly Label _currentTime;
        private readonly Label _playbackStatus;
        private readonly Label _layerCount;
        private readonly Label _layerCurrentFrame;
        private readonly Label _budgetLabel;
        private readonly ProgressBar _budget;
        private readonly VisualElement _previewEmpty;
        private readonly VisualElement _temporaryElementBanner;
        private readonly Label _temporaryElementName;
        private readonly Label _temporaryElementMode;
        private readonly Label _layerEmpty;
        private readonly Button _playButton;
        private readonly Button _pauseButton;
        private readonly Button _stopButton;
        private readonly Toggle _liveRefreshToggle;
        private readonly VisualElement _manualWorkspace;
        private readonly VisualElement _layerRegion;
        private readonly VisualElement _layerSplitter;
        private readonly Button _maximizePreviewButton;
        private readonly Button _collapseLayersButton;
        private readonly IMGUIContainer _previewCanvas;
        private readonly IMGUIContainer _comparisonCanvas;
        private readonly VisualElement _elementPreviewCard;
        private readonly IMGUIContainer _elementPreviewCanvas;
        private readonly Label _elementPreviewTitle;
        private readonly Label _elementPreviewMeta;
        private readonly Label _elementPreviewStatus;
        private readonly Button _elementPreviewPlay;
        private readonly Button _elementPreviewPause;
        private readonly Toggle _elementPreviewLoop;
        private readonly TextField _draftId;
        private readonly TextField _draftName;
        private readonly TextField _brief;
        private readonly VisualElement _phaseDurationEditor;
        private readonly VisualElement _phaseSummaryStrip;
        private readonly Label _phaseDurationName;
        private readonly IntegerField _phaseDurationFrames;
        private readonly Button _phaseDurationAuto;
        private readonly Button _phaseAllButton;
        private readonly Button[] _phaseSummaryButtons;
        private readonly Label[] _phaseSummaryLabels;
        private readonly DropdownField _layerStage;
        private readonly TextField _layerGroup;
        private readonly Toggle _layerEnabled;
        private readonly Vector3Field _position;
        private readonly Vector3Field _rotation;
        private readonly Vector3Field _scale;
        private readonly FloatField _startOffset;
        private readonly Slider _playbackSpeed;
        private readonly Slider _emission;
        private readonly Slider _lifetime;
        private readonly Slider _size;
        private readonly ColorField _tint;
        private readonly IntegerField _sortingOrder;
        private readonly Button _compositionTab;
        private readonly Button _layerInspectorTab;
        private readonly VisualElement _compositionOverview;
        private readonly ScrollView _layerInspector;
        private readonly Label _summaryName;
        private readonly Label _summaryLifecycle;
        private readonly Label _summaryBudget;
        private readonly Label _summaryDiagnostics;
        private readonly Label _selectedLayerTitle;
        private readonly Button _bakeButton;
        private readonly Button _productionBakeButton;
        private readonly Label[] _timelineTicks;
        private readonly List<VisualElement> _layerTimelineElements = new List<VisualElement>();
        private readonly VfxSemanticLayerInspectorController _semanticInspector;
        private readonly Dictionary<string, VfxSourceComponentView> _semanticMembersByLabel = new Dictionary<string, VfxSourceComponentView>(StringComparer.Ordinal);
        private bool _liveRefreshEnabled;
        private bool _previewDirty;
        private bool _semanticBinding;
        private bool _recoveryPending;
        private double _recoveryDueTime;
        private string _lockedCharacterContextPhase = string.Empty;
        private string _lastAppliedCharacterContextPhase = string.Empty;
        private bool _previewMaximized;
        private bool _layerRegionCollapsed;
        private bool _characterPreviewContextExpanded;
        private float _expandedLayerRegionHeight = DefaultLayerRegionHeight;
        private bool _draggingLayerSplitter;
        private int _layerSplitterPointerId;
        private float _layerSplitterStartY;
        private float _layerSplitterStartHeight;
        private VfxCompositionBakeReceipt _lastBakeReceipt;

        public VfxManualCompositionStudioController(
            VisualElement root,
            Action showSourceImportWorkspace,
            Action showManualWorkspace)
        {
            _root = root;
            _showSourceImportWorkspace = showSourceImportWorkspace ?? throw new ArgumentNullException(nameof(showSourceImportWorkspace));
            _showManualWorkspace = showManualWorkspace ?? throw new ArgumentNullException(nameof(showManualWorkspace));
            _search = root.Q<ToolbarSearchField>("element-search");
            _draftCurrentCard = root.Q("composition-draft-current");
            _draftCurrentName = root.Q<Label>("composition-draft-current-name");
            _draftCurrentId = root.Q<Label>("composition-draft-current-id");
            _draftCurrentMeta = root.Q<Label>("composition-draft-current-meta");
            _draftBrowserOpenButton = root.Q<Button>("composition-draft-browser-open");
            _draftBrowserOverlay = root.Q("composition-draft-browser-overlay");
            _draftBrowserSearch = root.Q<ToolbarSearchField>("composition-draft-browser-search");
            _draftBrowserList = root.Q<ListView>("composition-draft-browser-list");
            _draftBrowserCount = root.Q<Label>("composition-draft-browser-count");
            _draftBrowserEmpty = root.Q<Label>("composition-draft-browser-empty");
            _sourceFilter = root.Q<DropdownField>("element-source-filter");
            _roleFilter = root.Q<DropdownField>("element-role-filter");
            _themeFilter = root.Q<DropdownField>("element-theme-filter");
            _familyFilter = root.Q<DropdownField>("element-family-filter");
            _anchorFilter = root.Q<DropdownField>("element-anchor-filter");
            _qualityFilter = root.Q<DropdownField>("element-quality-filter");
            _packageFilter = root.Q<DropdownField>("element-package-filter");
            _shapeFilter = root.Q<DropdownField>("element-shape-filter");
            _lifecycleFilter = root.Q<DropdownField>("element-lifecycle-filter");
            _extractionFilter = root.Q<DropdownField>("element-extraction-filter");
            _useCaseFilter = root.Q<DropdownField>("element-use-case-filter");
            _motionFilter = root.Q<DropdownField>("element-motion-filter");
            _timingFilter = root.Q<DropdownField>("element-timing-filter");
            _spaceFilter = root.Q<DropdownField>("element-space-filter");
            _rendererFilter = root.Q<DropdownField>("element-renderer-filter");
            _budgetTierFilter = root.Q<DropdownField>("element-budget-filter");
            _granularityFilter = root.Q<DropdownField>("element-granularity-filter");
            _elementList = root.Q<ListView>("element-list");
            _layerList = root.Q<ListView>("composition-layer-list");
            _elementSummary = root.Q<Label>("element-summary");
            _elementVisibleCount = root.Q<Label>("element-visible-count");
            _elementDetail = root.Q<Label>("element-detail");
            _elementIndexUnavailable = root.Q("element-index-unavailable");
            _elementIndexUnavailableStatus = root.Q<Label>("element-index-unavailable-status");
            _elementIndexRebuild = root.Q<Button>("element-index-rebuild");
            _elementSemanticEditor = root.Q<Foldout>("element-semantic-editor");
            _elementSemanticMember = root.Q<DropdownField>("element-semantic-member");
            _elementSemanticName = root.Q<TextField>("element-semantic-name");
            _elementSemanticMemberName = root.Q<TextField>("element-semantic-member-name");
            _elementSemanticRolePrimary = root.Q<DropdownField>("element-semantic-role-primary");
            _elementSemanticRoleSecondary = root.Q<DropdownField>("element-semantic-role-secondary");
            _elementSemanticRoleTertiary = root.Q<DropdownField>("element-semantic-role-tertiary");
            _elementSemanticTheme = root.Q<DropdownField>("element-semantic-theme");
            _elementSemanticFamily = root.Q<DropdownField>("element-semantic-family");
            _elementSemanticShape = root.Q<DropdownField>("element-semantic-shape");
            _elementSemanticAnchor = root.Q<DropdownField>("element-semantic-anchor");
            _elementSemanticUseCasePrimary = root.Q<DropdownField>("element-semantic-use-case-primary");
            _elementSemanticUseCaseSecondary = root.Q<DropdownField>("element-semantic-use-case-secondary");
            _elementSemanticTiming = root.Q<DropdownField>("element-semantic-timing");
            _elementSemanticMotion = root.Q<DropdownField>("element-semantic-motion");
            _elementSemanticQuality = root.Q<DropdownField>("element-semantic-quality");
            _elementSemanticTechnical = root.Q<Label>("element-semantic-technical");
            _elementSemanticPreferred = root.Q<Toggle>("element-semantic-preferred");
            _elementSemanticDeprecated = root.Q<Toggle>("element-semantic-deprecated");
            _elementSemanticNote = root.Q<TextField>("element-semantic-note");
            _elementSemanticStatus = root.Q<Label>("element-semantic-status");
            _elementSemanticSave = root.Q<Button>("element-semantic-save");
            _elementSemanticReset = root.Q<Button>("element-semantic-reset");
            _diagnostics = root.Q<Label>("composition-diagnostics");
            _saveDraftButton = root.Q<Button>("composition-save");
            _saveDraftStatus = root.Q<Label>("composition-save-status");
            _draftSaveState = root.Q<Label>("composition-draft-save-state");
            _draftPrefabSyncState = root.Q<Label>("composition-prefab-sync-state");
            _draftPublicationState = root.Q<Label>("composition-publication-state");
            _previewStage = root.Q<DropdownField>("composition-stage");
            _loopMode = root.Q<DropdownField>("composition-loop-mode");
            _background = root.Q<DropdownField>("composition-background");
            _postProcessingMode = root.Q<DropdownField>("composition-post-processing");
            _cameraMode = root.Q<DropdownField>("composition-camera");
            _rootPosition = root.Q<Vector3Field>("composition-root-position");
            _gridToggle = root.Q<Toggle>("composition-grid");
            _characterPreviewContext = root.Q("character-preview-context");
            _characterPreviewContextToggle = root.Q<Button>("character-preview-context-toggle");
            _characterPreviewEditTarget = root.Q<DropdownField>("character-preview-edit-target");
            _characterPreviewActivePhase = root.Q<Label>("character-preview-active-phase");
            _characterPreviewOverrideSummary = root.Q<Label>("character-preview-override-summary");
            _characterPreviewResetPhase = root.Q<Button>("character-preview-reset-phase");
            _characterPreviewMode = root.Q<DropdownField>("character-preview-mode");
            _characterPreviewCharacter = root.Q<DropdownField>("character-preview-character");
            _characterPreviewPose = root.Q<DropdownField>("character-preview-pose");
            _characterPreviewAnimation = root.Q<DropdownField>("character-preview-animation");
            _characterPreviewSocket = root.Q<DropdownField>("character-preview-socket");
            _characterAnimationLoop = root.Q<Toggle>("character-preview-animation-loop");
            _characterAnimationSpeed = root.Q<Slider>("character-preview-animation-speed");
            _characterVisible = root.Q<Toggle>("character-preview-visible");
            _characterSocketAxes = root.Q<Toggle>("character-preview-socket-axes");
            _characterPreviewStatus = root.Q<Label>("character-preview-status");
            _cameraPresetButtons = new[]
            {
                root.Q<Button>("composition-camera-perspective"), root.Q<Button>("composition-camera-top"),
                root.Q<Button>("composition-camera-front"), root.Q<Button>("composition-camera-side")
            };
            _compositionSpeed = root.Q<Slider>("composition-speed");
            _abToggle = root.Q<Toggle>("composition-ab");
            _timeline = root.Q<Slider>("composition-time");
            _currentTime = root.Q<Label>("composition-current-time");
            _playbackStatus = root.Q<Label>("composition-playback-status");
            _layerCount = root.Q<Label>("composition-layer-count");
            _layerCurrentFrame = root.Q<Label>("layer-current-frame");
            _budgetLabel = root.Q<Label>("composition-budget-label");
            _budget = root.Q<ProgressBar>("composition-budget");
            _previewEmpty = root.Q("composition-preview-empty");
            _temporaryElementBanner = root.Q("composition-temporary-element");
            _temporaryElementName = root.Q<Label>("composition-temporary-element-name");
            _temporaryElementMode = root.Q<Label>("composition-temporary-element-mode");
            _layerEmpty = root.Q<Label>("layer-stack-empty");
            _playButton = root.Q<Button>("composition-play");
            _pauseButton = root.Q<Button>("composition-pause");
            _stopButton = root.Q<Button>("composition-stop");
            _liveRefreshToggle = root.Q<Toggle>("composition-live-refresh");
            _manualWorkspace = root.Q("manual-studio-workspace");
            _layerRegion = root.Q("composition-layer-region");
            _layerSplitter = root.Q("composition-layer-splitter");
            _maximizePreviewButton = root.Q<Button>("composition-maximize-preview");
            _collapseLayersButton = root.Q<Button>("composition-collapse-layers");
            if (_liveRefreshToggle == null)
            {
                _liveRefreshToggle = new Toggle("实时刷新")
                {
                    name = "composition-live-refresh",
                    tooltip = "关闭后暂停 VFX 仿真、角色动画采样和持续重绘，并保留当前帧"
                };
                root.Q(className: "composition-transport")?.Add(_liveRefreshToggle);
            }
            _liveRefreshEnabled = EditorPrefs.GetBool(LiveRefreshPreference, true);
            _liveRefreshToggle.SetValueWithoutNotify(_liveRefreshEnabled);
            _elementPreviewCard = root.Q("element-quick-preview");
            _elementPreviewCard.style.left = EditorPrefs.GetFloat(ElementPreviewLeftPreference, 397f);
            _elementPreviewCard.style.top = EditorPrefs.GetFloat(ElementPreviewTopPreference, 64f);
            _elementPreviewTitle = root.Q<Label>("element-quick-preview-title");
            _elementPreviewMeta = root.Q<Label>("element-quick-preview-meta");
            _elementPreviewStatus = root.Q<Label>("element-quick-preview-status");
            _elementPreviewPlay = root.Q<Button>("element-quick-preview-play");
            _elementPreviewPause = root.Q<Button>("element-quick-preview-pause");
            _elementPreviewLoop = root.Q<Toggle>("element-quick-preview-loop");
            _draftId = root.Q<TextField>("composition-draft-id");
            _draftId.isReadOnly = true;
            _draftId.tooltip = "系统自动生成的草稿唯一 ID。创建后保持不变，用于恢复、候选、审批与发布绑定。";
            _draftName = root.Q<TextField>("composition-name");
            _brief = root.Q<TextField>("composition-brief");
            _phaseDurationEditor = root.Q("composition-phase-duration-editor");
            _phaseSummaryStrip = root.Q("composition-phase-summary-strip");
            _phaseDurationName = root.Q<Label>("composition-phase-duration-name");
            _phaseDurationFrames = root.Q<IntegerField>("composition-phase-duration-frames");
            _phaseDurationAuto = root.Q<Button>("composition-phase-duration-auto");
            _phaseAllButton = root.Q<Button>("composition-phase-summary-all");
            _phaseSummaryButtons = new[]
            {
                root.Q<Button>("composition-phase-summary-build"), root.Q<Button>("composition-phase-summary-rebuild"),
                root.Q<Button>("composition-phase-summary-loop"), root.Q<Button>("composition-phase-summary-complete"),
                root.Q<Button>("composition-phase-summary-dissipate")
            };
            _phaseSummaryLabels = new[]
            {
                root.Q<Label>("phase-build-summary"), root.Q<Label>("phase-rebuild-summary"),
                root.Q<Label>("phase-loop-summary"), root.Q<Label>("phase-complete-summary"),
                root.Q<Label>("phase-dissipate-summary")
            };
            _layerStage = root.Q<DropdownField>("layer-stage");
            _layerGroup = root.Q<TextField>("layer-group");
            _layerEnabled = root.Q<Toggle>("layer-enabled");
            _position = root.Q<Vector3Field>("layer-position");
            _rotation = root.Q<Vector3Field>("layer-rotation");
            _scale = root.Q<Vector3Field>("layer-scale");
            _startOffset = root.Q<FloatField>("layer-start-offset");
            _playbackSpeed = root.Q<Slider>("layer-playback-speed");
            _emission = root.Q<Slider>("layer-emission");
            _lifetime = root.Q<Slider>("layer-lifetime");
            _size = root.Q<Slider>("layer-size");
            _tint = root.Q<ColorField>("layer-tint");
            _sortingOrder = root.Q<IntegerField>("layer-sorting-order");
            _compositionTab = root.Q<Button>("composition-tab");
            _layerInspectorTab = root.Q<Button>("layer-inspector-tab");
            _compositionOverview = root.Q("composition-overview");
            _layerInspector = root.Q<ScrollView>("layer-inspector");
            _layerInspector.mode = ScrollViewMode.Vertical;
            _layerInspector.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _summaryName = root.Q<Label>("composition-summary-name");
            _summaryLifecycle = root.Q<Label>("composition-summary-lifecycle");
            _summaryBudget = root.Q<Label>("composition-summary-budget");
            _summaryDiagnostics = root.Q<Label>("composition-summary-diagnostics");
            _selectedLayerTitle = root.Q<Label>("layer-selected-title");
            _bakeButton = root.Q<Button>("composition-bake");
            _productionBakeButton = root.Q<Button>("composition-bake-production");
            _timelineTicks = new[]
            {
                root.Q<Label>("timeline-tick-0"), root.Q<Label>("timeline-tick-1"), root.Q<Label>("timeline-tick-2"),
                root.Q<Label>("timeline-tick-3"), root.Q<Label>("timeline-tick-4")
            };
            VisualElement previewHost = root.Q("composition-preview-host");
            _previewCanvas = new IMGUIContainer(() => _preview.Draw(_previewCanvas.contentRect));
            _previewCanvas.style.flexGrow = 1f;
            _previewCanvas.AddToClassList("composition-preview-canvas");
            _comparisonCanvas = new IMGUIContainer(() => _comparisonPreview.Draw(_comparisonCanvas.contentRect));
            _comparisonCanvas.style.flexGrow = 1f;
            _comparisonCanvas.style.display = DisplayStyle.None;
            _comparisonCanvas.AddToClassList("composition-preview-canvas");
            previewHost.Add(_previewCanvas);
            previewHost.Add(_comparisonCanvas);
            _temporaryElementBanner?.BringToFront();
            VisualElement elementPreviewHost = root.Q("element-quick-preview-host");
            _elementPreviewCanvas = new IMGUIContainer(() => _elementPreview.Draw(_elementPreviewCanvas.contentRect));
            _elementPreviewCanvas.AddToClassList("element-quick-preview-canvas");
            elementPreviewHost.Add(_elementPreviewCanvas);
            InitializeSourcePrefabImportWizard();
            _semanticInspector = new VfxSemanticLayerInspectorController(
                root,
                SelectedLayer,
                () => _catalog,
                ChangeLayer,
                () => _session?.RecordChange(),
                SetStatus,
                () => _liveRefreshEnabled);
            _previewEmpty?.BringToFront();
            ConfigureLists();
            ConfigureFields();
            RegisterCallbacks();
            RestorePreviewLayout();
            RestorePreviewWorkspaceState();
            ReloadCatalog();
            RestoreElementWorkspaceState();
            if (!RestoreLastDraft()) NewDraft();
        }

        public void Dispose()
        {
            FlushRecoveryJournal();
            SavePreviewWorkspaceState();
            SaveSourceImportWorkspaceStateNow();
            _semanticInspector?.Dispose();
            _preview.Dispose(); _comparisonPreview.Dispose(); _elementPreview.Dispose(); _sourceImportPreview.Dispose(); _sourceImportDerivedPreview.Dispose();
            foreach (Texture2D texture in _thumbnailCache.Values) if (texture != null) Object.DestroyImmediate(texture);
            _thumbnailCache.Clear();
            _layerTimelineElements.Clear();
        }
        public void SetWorkspace(VfxLibraryWorkspace workspace) { _workspace = workspace; RefreshElements(); }
        public void Update()
        {
            if (_recoveryPending && EditorApplication.timeSinceStartup >= _recoveryDueTime) FlushRecoveryJournal();
            UpdateSourcePrefabImportWorkspace();
            if (!_liveRefreshEnabled) return;
            string activeCharacterPhase = ResolveAppliedCharacterPreviewPhase();
            if (!string.Equals(activeCharacterPhase, _lastAppliedCharacterContextPhase, StringComparison.Ordinal))
            {
                ApplyCharacterPreviewContext(false);
                if (!IsSpecificLifecyclePhase(_previewStage.value)
                    && _characterPreviewEditTarget.value != "共享默认"
                    && string.IsNullOrWhiteSpace(_lockedCharacterContextPhase))
                    RefreshCharacterPreviewControls();
            }
            if (_preview.State == VfxLibraryPlaybackState.Playing)
            {
                _previewCanvas.MarkDirtyRepaint();
                float duration = Mathf.Max(0.01f, _preview.EstimatedDurationSeconds);
                _timeline.SetValueWithoutNotify(Mathf.Clamp01(_preview.ElapsedSeconds / duration));
                RefreshPlaybackUi();
            }
            if (_comparisonPreview.State == VfxLibraryPlaybackState.Playing)
                _comparisonCanvas.MarkDirtyRepaint();
            if (_elementPreview.State == VfxLibraryPlaybackState.Playing)
                _elementPreviewCanvas.MarkDirtyRepaint();
        }
        public void UpdateResponsiveState(float width)
        {
            VisualElement manual = _root.Q("manual-studio-workspace");
            VisualElement sourceImport = _root.Q("source-prefab-import-workspace");
            bool compact = width < 1380f;
            manual?.EnableInClassList("manual-compact", compact);
            sourceImport?.EnableInClassList("source-import-compact", width < 1500f);
            if (_compactElementGrid == compact) return;
            _compactElementGrid = compact;
            RebuildElementRows();
        }

        private void ConfigureFields()
        {
            SetChoices(_sourceFilter, new[] { "精选 200", "全部独特组件", "复合组", "已提取 Primitive", "全部原始来源" });
            SetChoices(_previewStage, new[] { "完整生命周期", "Build", "Rebuild", "Loop", "Complete", "Dissipate" });
            SetChoices(_loopMode, new[] { "OneShot", "Continuous" });
            SetChoices(_background, new[] { "暗色", "中性", "浅色" });
            SetChoices(_postProcessingMode, new[] { "原始材质", "游戏观感" });
            SetChoices(_cameraMode, new[] { "透视", "俯视", "正面", "侧面" });
            SetChoices(_layerStage, new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" });
            SetChoices(_characterPreviewMode, new[] { "纯 VFX", "角色挂载" });
            SetChoices(_characterPreviewEditTarget, new[] { "跟随播放", "共享默认" });
            SetChoices(_characterPreviewPose, VfxCharacterPreviewCatalog.PosePresets.Select(value => value.DisplayName));
            _characterPreviewCharacter.choices = new List<string>();
            _characterPreviewAnimation.choices = new List<string>();
            _characterPreviewSocket.choices = new List<string>();
            ConfigureChineseCategoryLabels();
            ApplyCameraPreset("透视");
            ShowInspectorTab(false);
            RefreshPlaybackUi();
        }

        private void ConfigureChineseCategoryLabels()
        {
            foreach (DropdownField field in new[]
                     {
                         _roleFilter, _themeFilter, _familyFilter, _anchorFilter, _qualityFilter,
                         _shapeFilter, _lifecycleFilter, _extractionFilter, _useCaseFilter, _motionFilter,
                         _timingFilter, _spaceFilter, _rendererFilter, _budgetTierFilter, _granularityFilter,
                         _elementSemanticRolePrimary, _elementSemanticRoleSecondary, _elementSemanticRoleTertiary,
                         _elementSemanticTheme, _elementSemanticFamily, _elementSemanticShape, _elementSemanticAnchor,
                         _elementSemanticUseCasePrimary, _elementSemanticUseCaseSecondary, _elementSemanticTiming,
                         _elementSemanticMotion, _elementSemanticQuality
                     })
            {
                field.formatSelectedValueCallback = CategoryLabel;
                field.formatListItemCallback = CategoryLabel;
            }
        }

        private void RegisterCallbacks()
        {
            _search.RegisterValueChangedCallback(_ => FilterElements());
            foreach (DropdownField filter in new[] { _sourceFilter, _roleFilter, _themeFilter, _familyFilter, _anchorFilter, _qualityFilter, _packageFilter, _shapeFilter, _lifecycleFilter, _extractionFilter, _useCaseFilter, _motionFilter, _timingFilter, _spaceFilter, _rendererFilter, _budgetTierFilter, _granularityFilter }) filter.RegisterValueChangedCallback(_ => FilterElements());
            _previewStage.RegisterValueChangedCallback(_ =>
            {
                EditorPrefs.SetString(WorkspacePreferencePrefix + "PreviewStage", _previewStage.value);
                _lockedCharacterContextPhase = string.Empty;
                RefreshLifecycleStageView();
                RefreshCharacterPreviewControls();
                ApplyCharacterPreviewContext(false);
            });
            _loopMode.RegisterValueChangedCallback(evt =>
            {
                ChangeDraft(draft => draft.LoopPlaybackMode = evt.newValue);
                RebuildPreview();
            });
            _background.RegisterValueChangedCallback(evt =>
            {
                _preview.SetBackground(evt.newValue);
                _comparisonPreview.SetBackground(evt.newValue);
                _elementPreview.SetBackground(evt.newValue);
                _previewCanvas.MarkDirtyRepaint();
                _comparisonCanvas.MarkDirtyRepaint();
                _elementPreviewCanvas.MarkDirtyRepaint();
            });
            _postProcessingMode.RegisterValueChangedCallback(evt =>
            {
                _preview.SetPostProcessingMode(evt.newValue);
                _comparisonPreview.SetPostProcessingMode(evt.newValue);
                _elementPreview.SetPostProcessingMode(evt.newValue);
                _previewCanvas.MarkDirtyRepaint();
                _comparisonCanvas.MarkDirtyRepaint();
                _elementPreviewCanvas.MarkDirtyRepaint();
            });
            _cameraMode.RegisterValueChangedCallback(evt => ApplyCameraPreset(evt.newValue));
            _rootPosition.RegisterValueChangedCallback(evt => { _preview.SetRootPosition(evt.newValue); _comparisonPreview.SetRootPosition(evt.newValue); MarkPreviewDirty(); });
            _gridToggle.RegisterValueChangedCallback(evt => { _preview.SetGridVisible(evt.newValue); _comparisonPreview.SetGridVisible(evt.newValue); MarkPreviewDirty(); });
            _characterPreviewMode.RegisterValueChangedCallback(OnCharacterPreviewModeChanged);
            _characterPreviewContextToggle.clicked += ToggleCharacterPreviewContextDrawer;
            _characterPreviewEditTarget.RegisterValueChangedCallback(_ =>
            {
                if (_binding) return;
                _lockedCharacterContextPhase = string.Empty;
                RefreshCharacterPreviewControls();
                ApplyCharacterPreviewContext(false);
            });
            _characterPreviewResetPhase.clicked += ResetCharacterPreviewPhase;
            _characterPreviewCharacter.RegisterValueChangedCallback(OnCharacterSelectionChanged);
            _characterPreviewPose.RegisterValueChangedCallback(OnCharacterPoseChanged);
            _characterPreviewAnimation.RegisterValueChangedCallback(OnCharacterAnimationChanged);
            _characterPreviewSocket.RegisterValueChangedCallback(OnCharacterSocketChanged);
            _characterAnimationLoop.RegisterValueChangedCallback(evt => UpdateCharacterPreviewContext(context => context.AnimationLoop = evt.newValue));
            _characterAnimationSpeed.RegisterValueChangedCallback(evt => UpdateCharacterPreviewContext(context => context.AnimationSpeed = evt.newValue));
            _characterVisible.RegisterValueChangedCallback(evt => { _preview.SetCharacterVisible(evt.newValue); _comparisonPreview.SetCharacterVisible(evt.newValue); MarkPreviewDirty(); });
            _characterSocketAxes.RegisterValueChangedCallback(evt => { _preview.SetSocketAxesVisible(evt.newValue); _comparisonPreview.SetSocketAxesVisible(evt.newValue); MarkPreviewDirty(); });
            _root.Q<Button>("character-preview-restart").clicked += () => { ResumeCharacterContextFollowing(); BeginContinuousRefresh(); _preview.Restart(); if (_abToggle.value) _comparisonPreview.Restart(); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _compositionSpeed.RegisterValueChangedCallback(evt => { _preview.SetSpeed(evt.newValue); _comparisonPreview.SetSpeed(evt.newValue); });
            _abToggle.RegisterValueChangedCallback(evt => { _comparisonCanvas.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None; RebuildPreview(); });
            _timeline.RegisterValueChangedCallback(evt => { _preview.Scrub(evt.newValue); if (_abToggle.value) _comparisonPreview.Scrub(evt.newValue); SyncCharacterContextToPreviewPhase(); _previewCanvas.MarkDirtyRepaint(); _comparisonCanvas.MarkDirtyRepaint(); RefreshPlaybackUi(); });
            _liveRefreshToggle.RegisterValueChangedCallback(evt => SetLiveRefreshEnabled(evt.newValue));
            _elementPreviewPlay.clicked += () => { BeginContinuousRefresh(); _elementPreview.Play(); RefreshElementPreviewUi(); };
            _elementPreviewPause.clicked += () => { _elementPreview.Pause(); RefreshElementPreviewUi(); };
            _root.Q<Button>("element-quick-preview-restart").clicked += () => { BeginContinuousRefresh(); _elementPreview.Restart(); RefreshElementPreviewUi(); };
            _root.Q<Button>("element-quick-preview-step").clicked += StepElementPreviewFrame;
            _root.Q<Button>("element-quick-preview-frame").clicked += () => { _elementPreview.FrameCamera(); _elementPreviewCanvas.MarkDirtyRepaint(); };
            _root.Q<Button>("element-quick-preview-set-icon").clicked += SaveElementPreviewAsIcon;
            _root.Q<Button>("element-quick-preview-close").clicked += CloseElementPreview;
            _maximizePreviewButton.clicked += ToggleMaximizedPreview;
            _collapseLayersButton.clicked += ToggleLayerRegionCollapsed;
            _root.Q<Button>("composition-reset-layout").clicked += ResetPreviewLayout;
            RegisterLayerSplitter();
            Button temporaryRestart = _root.Q<Button>("composition-temporary-element-restart");
            if (temporaryRestart != null) temporaryRestart.clicked += () =>
            {
                BeginContinuousRefresh();
                _preview.RestartTemporaryOverlay();
                _previewCanvas.MarkDirtyRepaint();
                RefreshPlaybackUi();
            };
            Button temporaryClear = _root.Q<Button>("composition-temporary-element-clear");
            if (temporaryClear != null) temporaryClear.clicked += ClearTemporaryElementPreview;
            _elementPreviewLoop.RegisterValueChangedCallback(evt => _elementPreview.SetLoop(evt.newValue));
            _elementList.RegisterCallback<KeyDownEvent>(OnElementPreviewKeyDown);
            _elementPreviewCard.RegisterCallback<KeyDownEvent>(OnElementPreviewKeyDown);
            _elementSemanticMember.RegisterValueChangedCallback(_ =>
            {
                if (!_semanticBinding) BindSelectedSemanticMember();
            });
            _elementSemanticSave.clicked += SaveSemanticOverride;
            _elementSemanticReset.clicked += ResetSemanticOverride;
            ConfigureElementPreviewDragging();
            _root.Q<Button>("element-rescan").clicked += RescanSource;
            _elementIndexRebuild.clicked += RescanSource;
            _root.Q<Button>("element-curate").clicked += CurateElements;
            _root.Q<Button>("element-generate-previews").clicked += GenerateElementPreviews;
            _root.Q<Button>("element-generate-samples").clicked += GenerateSamples;
            _root.Q<Button>("element-show-equivalents").clicked += ShowEquivalentSources;
            _playButton.clicked += () => { ResumeCharacterContextFollowing(); BeginContinuousRefresh(); _preview.Play(); if (_abToggle.value) _comparisonPreview.Play(); RefreshPlaybackUi(); };
            _pauseButton.clicked += () => { _preview.Pause(); if (_abToggle.value) _comparisonPreview.Pause(); RefreshPlaybackUi(); };
            _stopButton.clicked += () => { _preview.Stop(); if (_abToggle.value) _comparisonPreview.Stop(); RefreshPlaybackUi(); };
            _root.Q<Button>("composition-restart").clicked += () => { ResumeCharacterContextFollowing(); BeginContinuousRefresh(); _preview.Restart(); if (_abToggle.value) _comparisonPreview.Restart(); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _root.Q<Button>("composition-play-first").clicked += () => { ResumeCharacterContextFollowing(); BeginContinuousRefresh(); EnsureFullLifecyclePreview(); _preview.Restart(); if (_abToggle.value) _comparisonPreview.Restart(); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _root.Q<Button>("composition-trigger-rebuild").clicked += () => { ResumeCharacterContextFollowing(); BeginContinuousRefresh(); EnsureFullLifecyclePreview(); _preview.RebuildLifecycle(); if (_abToggle.value) _comparisonPreview.RebuildLifecycle(); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _root.Q<Button>("composition-stop-complete").clicked += () => { ResumeCharacterContextFollowing(); _preview.StopLifecycle(VfxStopMode.Complete); if (_abToggle.value) _comparisonPreview.StopLifecycle(VfxStopMode.Complete); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _root.Q<Button>("composition-stop-dissipate").clicked += () => { ResumeCharacterContextFollowing(); _preview.StopLifecycle(VfxStopMode.Dissipate); if (_abToggle.value) _comparisonPreview.StopLifecycle(VfxStopMode.Dissipate); SyncCharacterContextToPreviewPhase(); RefreshPlaybackUi(); };
            _root.Q<Toggle>("composition-loop").RegisterValueChangedCallback(evt => { _preview.SetLoop(evt.newValue); _comparisonPreview.SetLoop(evt.newValue); });
            _root.Q<Button>("composition-frame").clicked += () => { _preview.FrameCamera(); _comparisonPreview.FrameCamera(); _previewCanvas.MarkDirtyRepaint(); _comparisonCanvas.MarkDirtyRepaint(); };
            _root.Q<Button>("composition-root-reset").clicked += () => _rootPosition.value = Vector3.zero;
            _cameraPresetButtons[0].clicked += () => ApplyCameraPreset("透视");
            _cameraPresetButtons[1].clicked += () => ApplyCameraPreset("俯视");
            _cameraPresetButtons[2].clicked += () => ApplyCameraPreset("正面");
            _cameraPresetButtons[3].clicked += () => ApplyCameraPreset("侧面");
            _root.Q<Button>("composition-set-a").clicked += CaptureComparisonBaseline;
            _root.Q<Button>("composition-ab-action").clicked += ToggleComparisonMode;
            _compositionTab.clicked += () => ShowInspectorTab(false);
            _layerInspectorTab.clicked += () => ShowInspectorTab(true);
            _root.Q<Button>("layer-up").clicked += () => MoveLayer(-1);
            _root.Q<Button>("layer-down").clicked += () => MoveLayer(1);
            _root.Q<Button>("layer-copy").clicked += CopyLayer;
            _root.Q<Button>("layer-paste").clicked += PasteLayer;
            _root.Q<Button>("layer-remove").clicked += RemoveLayer;
            _root.Q<Button>("layer-solo").clicked += ToggleSolo;
            Button frameTimeline = _root.Q<Button>("layer-frame-timeline");
            if (frameTimeline != null) frameTimeline.clicked += FrameTimeline;
            _root.Q<Button>("layer-undo").clicked += () => { if (_session.Undo()) RefreshDraftUi(); };
            _root.Q<Button>("layer-redo").clicked += () => { if (_session.Redo()) RefreshDraftUi(); };
            _layerList.RegisterCallback<KeyDownEvent>(OnLayerListKeyDown);
            ConfigureDraftBrowser();
            _root.Q<Button>("composition-new").clicked += NewDraft;
            _root.Q<Button>("composition-import-source-prefab").clicked += _showSourceImportWorkspace;
            _root.Q<Button>("composition-open").clicked += OpenDraft;
            _root.Q<Button>("composition-draft-browser-import").clicked += () => { CloseDraftBrowser(); OpenDraft(); };
            _root.Q<Button>("composition-draft-browser-create").clicked += () => { CloseDraftBrowser(); NewDraft(); };
            _saveDraftButton.clicked += SaveDraft;
            _root.Q<Button>("composition-delete").clicked += DeleteCurrentDraft;
            _root.Q<Button>("composition-remap").clicked += RemapStaleDependencies;
            _root.Q<Button>("composition-bake").clicked += BakeDraft;
            _root.Q<Button>("composition-bake-production").clicked += BakeDraftAsProduction;
            Button copyDraftIdButton = _root.Q<Button>("composition-copy-draft-id");
            if (copyDraftIdButton != null) copyDraftIdButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = _session?.Draft?.DraftId ?? string.Empty;
                SetStatus("Draft ID 已复制。", false);
            };
            _draftName.RegisterValueChangedCallback(evt => ChangeDraft(draft => draft.DisplayName = evt.newValue));
            _brief.RegisterValueChangedCallback(evt => ChangeDraft(draft => draft.Brief = evt.newValue));
            _phaseDurationFrames.RegisterValueChangedCallback(OnPhaseDurationChanged);
            _phaseDurationAuto.clicked += ToggleAutomaticPhaseDuration;
            _phaseAllButton.clicked += () => _previewStage.value = "完整生命周期";
            for (int i = 0; i < _phaseSummaryButtons.Length; i++)
            {
                string phase = LifecyclePhases[i];
                _phaseSummaryButtons[i].clicked += () => _previewStage.value = phase;
            }
            _layerStage.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.Phase = evt.newValue));
            _layerGroup.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.GroupId = evt.newValue));
            _layerEnabled.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.Enabled = evt.newValue));
            _position.RegisterValueChangedCallback(evt => ChangeLayer(layer => SetLayerTransform(layer, position: Values(evt.newValue))));
            _rotation.RegisterValueChangedCallback(evt => ChangeLayer(layer => SetLayerTransform(layer, rotation: Values(evt.newValue))));
            _scale.RegisterValueChangedCallback(evt => ChangeLayer(layer => SetLayerTransform(layer, scale: Values(evt.newValue))));
            _startOffset.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.StartFrame = Mathf.Clamp(Mathf.RoundToInt(evt.newValue), 0, PreviewFramesPerSecond * 30)));
            _playbackSpeed.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.PlaybackSpeed = evt.newValue));
            _emission.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.EmissionScale = evt.newValue));
            _lifetime.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.LifetimeScale = evt.newValue));
            _size.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.SizeScale = evt.newValue));
            _tint.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.ColorTint = Values(evt.newValue)));
            _sortingOrder.RegisterValueChangedCallback(evt => ChangeLayer(layer => layer.SortingOrderOffset = evt.newValue));
        }

    }

    internal sealed class VfxElementDragManipulator : PointerManipulator
    {
        internal const string PayloadKey = "MxFramework.Vfx.SourceElement";
        private Vector2 _start;
        private bool _armed;
        protected override void RegisterCallbacksOnTarget() { target.RegisterCallback<PointerDownEvent>(OnDown); target.RegisterCallback<PointerMoveEvent>(OnMove); target.RegisterCallback<PointerUpEvent>(OnUp); }
        protected override void UnregisterCallbacksFromTarget() { target.UnregisterCallback<PointerDownEvent>(OnDown); target.UnregisterCallback<PointerMoveEvent>(OnMove); target.UnregisterCallback<PointerUpEvent>(OnUp); }
        private void OnDown(PointerDownEvent evt) { if (evt.button != 0) return; _armed = true; _start = evt.position; }
        private void OnMove(PointerMoveEvent evt)
        {
            if (!_armed || Vector2.Distance(_start, evt.position) < 6f || target.userData is not VfxSourceElementView element) return;
            _armed = false; DragAndDrop.PrepareStartDrag(); DragAndDrop.SetGenericData(PayloadKey, element); DragAndDrop.StartDrag(element.DisplayName);
        }
        private void OnUp(PointerUpEvent evt) { _armed = false; }
    }

    internal sealed class VfxLayerDragManipulator : PointerManipulator
    {
        internal const string PayloadKey = "MxFramework.Vfx.CompositionLayer";
        private Vector2 _start;
        private bool _armed;

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || target.userData is not int || evt.target is not VisualElement element || !element.ClassListContains("layer-drag-handle")) return;
            _armed = true;
            _start = evt.position;
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (!_armed || Vector2.Distance(_start, evt.position) < 6f || target.userData is not int index) return;
            _armed = false;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(PayloadKey, index);
            DragAndDrop.StartDrag("调整 VFX Layer 顺序");
        }

        private void OnUp(PointerUpEvent evt) { _armed = false; }
    }
}
