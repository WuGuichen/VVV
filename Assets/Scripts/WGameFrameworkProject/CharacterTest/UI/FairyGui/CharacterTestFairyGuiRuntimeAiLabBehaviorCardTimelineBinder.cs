using System;
using System.Collections.Generic;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const string StageComponentName = "RuntimeAiLabTimelineStageCard";
        private const string DetectorComponentName = "RuntimeAiLabTimelineDetectorBand";
        private const string TransitionComponentName = "RuntimeAiLabTimelineTransitionChip";
        private const float CanvasLeft = 20f;
        private const float CanvasRight = 1868f;
        private const float RulerY = 50f;
        private const float ContentTop = 92f;
        private const float ContentBottom = 644f;
        private const float ContentHeight = ContentBottom - ContentTop;
        private const float StageCardHeight = 244f;
        private const float StageLaneGap = 22f;
        private const float DetectorBandHeight = 42f;
        private const float TransitionRouteLaneHeight = 56f;
        private const float TransitionRouteHorizontalPadding = 22f;
        private const float TransitionRouteChipClearance = 14f;
        private const int StageTimelineFrameLimit = 36000;
        private const int StageGestureTooltipDeltaLimit = 600;
        private const int ConnectorSortingOrder = 1;
        private const int StageSortingOrder = 10;
        private const int DetectorSortingOrder = 20;
        private const int TransitionChipSortingOrder = 30;
        private const int BehaviorLogicSourceConnectorSortingOrder =
            TransitionChipSortingOrder - 1;
        private const int ConflictHatchCount = 6;
        private const float CompactBehaviorLogicCardWidth = 1480f;
        private const float CompactBehaviorLogicCardHeight = 278f;
        private const float CompactBehaviorLogicHorizontalPadding = 78f;
        private const float CompactBehaviorLogicHeaderHeight = 144f;
        private const float CompactBehaviorLogicLaneGap = 48f;
        private const float CompactBehaviorLogicRouteHeight = 56f;
        private const float CompactBehaviorLogicDenseHeaderHeight = 124f;
        private const float CompactBehaviorLogicDenseRouteHeight = 48f;
        private const float CompactBehaviorLogicDenseRouteGap = 8f;
        private const float CompactBehaviorLogicDenseHorizontalPadding = 30f;
        private const float BehaviorLogicStageWidth = 344f;
        private const float BehaviorLogicStageHeight = 218f;
        private const float BehaviorLogicStageGap = 20f;
        private const float BehaviorLogicStageTop = 148f;
        private const float BehaviorLogicDetectorInset = 18f;
        private const float BehaviorLogicDetectorTop = 76f;
        private const float BehaviorLogicDetectorRowGap = 4f;
        private const int BehaviorLogicVisibleDetectorRows = 2;
        private const float BehaviorLogicGlobalDetectorLeft = 100f;
        private const float BehaviorLogicGlobalDetectorTop = 62f;
        private const float BehaviorLogicGlobalDetectorWidth = 220f;
        private const float BehaviorLogicGlobalDetectorGap = 10f;
        private const int BehaviorLogicVisibleGlobalDetectors = 3;
        private const float BehaviorLogicEndRouteTop = 124f;
        private const float BehaviorLogicEndRouteLaneGap = 16f;
        private const float BehaviorLogicEndRouteEscape = 8f;
        private const float BehaviorLogicEndRouteEscapeGap = 8f;
        private const float BehaviorLogicEndRouteCornerRadius = 8f;
        private const float BehaviorLogicEndpointDockGap = 2f;
        private const float BehaviorLogicBottomRouteTop = 386f;
        private const float BehaviorLogicHorizontalScrollStep =
            BehaviorLogicStageWidth + BehaviorLogicStageGap;
        private const float BehaviorLogicToolbarMargin = 20f;
        private const float BehaviorLogicControlWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTopCommandWidth;
        private static readonly float[] StandardStageCornerRadii = { 9f, 9f, 9f, 9f };
        private static readonly float[] CompactStageFrameCornerRadii = { 16f, 16f, 16f, 16f };
        private static readonly float[] CompactStageFillCornerRadii = { 14f, 14f, 14f, 14f };

        private sealed class StageSlot
        {
            internal Fgui.GComponent Visual;
            internal string StableId = string.Empty;
            internal Fgui.GObject[] ConflictHatches = Array.Empty<Fgui.GObject>();
            internal Fgui.GGraph InputPort;
            internal Fgui.GGraph CompactFrame;
            internal Fgui.GGraph CompactHeaderDivider;
            internal Fgui.GGraph CompactLaneDivider;
            internal int DetectorScrollOffset;
        }

        private sealed class DetectorSlot
        {
            internal Fgui.GComponent Visual;
            internal string StableId = string.Empty;
            internal CharacterTestRuntimeAiLabLogicDetectorViewModel ViewModel;
            internal Fgui.GGraph ResizeHandle;
            internal Fgui.GGraph OutputPort;
        }

        private sealed class TransitionSlot
        {
            internal Fgui.GComponent Visual;
            internal string StableId = string.Empty;
            internal int RuntimeFrame = -1;
            internal CharacterTestRuntimeAiLabLogicTransitionViewModel ViewModel;
        }

        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _commands;
        private readonly Action _toggleLogicLibraryDrawer;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private bool _behaviorLogicMode;
        private bool _compactBehaviorLogicCard;
        private readonly List<StageSlot> _stagePool = new List<StageSlot>(16);
        private readonly List<DetectorSlot> _detectorPool = new List<DetectorSlot>(24);
        private readonly List<TransitionSlot> _transitionPool = new List<TransitionSlot>(24);
        private readonly List<Fgui.GGraph> _connectorPool = new List<Fgui.GGraph>(64);
        private readonly List<Fgui.GGraph> _rulerTickPool = new List<Fgui.GGraph>(16);
        private readonly List<Fgui.GTextField> _rulerLabelPool = new List<Fgui.GTextField>(16);
        private float[] _stageX = Array.Empty<float>();
        private float[] _stageY = Array.Empty<float>();
        private float[] _stageWidth = Array.Empty<float>();
        private float[] _stageHeight = Array.Empty<float>();
        private int[] _stageLane = Array.Empty<int>();
        private int[] _stageDetectorCount = Array.Empty<int>();
        private float[] _laneHeight = Array.Empty<float>();
        private float[] _laneY = Array.Empty<float>();
        private float[] _transitionChipX = Array.Empty<float>();
        private float[] _transitionChipWidth = Array.Empty<float>();
        private float[] _transitionChipHeight = Array.Empty<float>();
        private float[] _transitionRouteTopY = Array.Empty<float>();
        private float[] _transitionRouteY = Array.Empty<float>();
        private float[] _transitionRouteStartX = Array.Empty<float>();
        private float[] _transitionRouteEndX = Array.Empty<float>();
        private int[] _transitionRouteLane = Array.Empty<int>();
        private int[] _transitionLaneSpan = Array.Empty<int>();
        private int[] _transitionOrder = Array.Empty<int>();
        private int[] _transitionConnectorStart = Array.Empty<int>();
        private int[] _transitionConnectorCount = Array.Empty<int>();
        private float[] _detectorX = Array.Empty<float>();
        private float[] _detectorY = Array.Empty<float>();
        private float[] _detectorWidth = Array.Empty<float>();
        private float[] _detectorHeight = Array.Empty<float>();
        private Fgui.GComponent _root;
        private Fgui.GComponent _contentLayer;
        private float _contentExtentHeight = ContentHeight;
        private float _currentVerticalOffset;
        private float _routeBaseY;
        private float _behaviorLogicHorizontalOffset;
        private float _behaviorLogicContentWidth;
        private int _activeStageCount;
        private int _activeDetectorCount;
        private CharacterTestRuntimeAiLabLogicDetectorViewModel[] _boundDetectors =
            Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
        private CharacterTestRuntimeAiLabLogicStageViewModel[] _boundStages =
            Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
        private CharacterTestRuntimeAiLabLogicTransitionViewModel[] _boundTransitions =
            Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
        private int _boundDetectorCount;
        private int _boundTransitionCount;
        private bool _handlersConfigured;
        private bool _canEdit;
        private string _selectedStableId = string.Empty;
        private CharacterTestRuntimeAiLabLogicElementKind _selectedKind;
        private string _conditionDetectorId = string.Empty;
        private int _conditionIndex;
        private long _graphFieldEditEpoch;
        private int _graphFieldEditDomain;
        private int _graphFieldEditKind;
        private string _graphFieldEditStableId = string.Empty;
        private string _graphFieldEditConditionId = string.Empty;
        private Fgui.GTextInput _graphFieldEditInput;
        private int _lastRulerStartFrame;
        private int _lastRulerEndFrame;
        private bool _hasRulerLabels;
        private Fgui.GComponent _stageEditorPopover;
        private Fgui.GComponent _stageTypedOptionMenu;
        private Fgui.GComponent _transitionResultMenu;
        private string _transitionResultDetectorId = string.Empty;
        private bool _transitionResultTargetPickerMode;
        private int _transitionResultTargetPage;
        private readonly int[] _transitionResultTargetOption = new int[7];
        private CharacterTestRuntimeAiLabStageFieldKind _stageTypedOptionField;
        private bool _stageEditorHandlersConfigured;
        private string _stageEditorStableId = string.Empty;
        private long _stageFieldEditEpoch;
        private CharacterTestRuntimeAiLabStageFieldKind _stageFieldEditKind;
        private string _stageFieldEditStableId = string.Empty;
        private float[] _stageGestureBaseX = Array.Empty<float>();
        private float[] _stageGestureBaseWidth = Array.Empty<float>();
        private bool[] _stageGestureBaseConflict = Array.Empty<bool>();
        private float[] _detectorGestureBaseX = Array.Empty<float>();
        private float[] _transitionGestureBaseX = Array.Empty<float>();
        private float[] _connectorGestureBaseX = Array.Empty<float>();
        private readonly string[] _stageGesturePreviewTextCache = new string[StageGestureTooltipDeltaLimit * 2 + 1];
        private readonly string[] _stageGestureConflictPreviewTextCache = new string[StageGestureTooltipDeltaLimit * 2 + 1];
        private string _stageGestureNegativeOverflowText = string.Empty;
        private string _stageGesturePositiveOverflowText = string.Empty;
        private string _stageGestureNegativeConflictOverflowText = string.Empty;
        private string _stageGesturePositiveConflictOverflowText = string.Empty;
        private long _stageGestureEpoch;
        private float _stageGesturePointerStartX;
        private float _stageGesturePixelsPerFrame;
        private string _stageGesturePrimaryId = string.Empty;
        private CharacterTestRuntimeAiLabStageGestureKind _stageGestureKind;
        private bool _stageGestureCaptured;
        private bool _stageGestureMoved;
        private int _stageTransientViewportBeginFrame;
        private int _stageTransientViewportSpan;
        private int _stageTransientViewportFrameOffset;
        private bool[] _stageMarqueeMask = Array.Empty<bool>();
        private string[] _stageMarqueeSourceIds = Array.Empty<string>();
        private string[] _stageMarqueeHitIds = Array.Empty<string>();
        private int _stageMarqueeSourceCount;
        private long _stageMarqueeEpoch;
        private bool _stageMarqueeCaptured;
        private bool _stageMarqueeMoved;
        private float _stageMarqueeStartX;
        private float _stageMarqueeStartY;
        private CharacterTestRuntimeAiLabStageSelectionModifier _stageMarqueeModifier;
        private DetectorSlot _detectorGestureSlot;
        private long _detectorGestureEpoch;
        private float _detectorGestureStartX;
        private float _detectorGestureStartY;
        private float _detectorPointerBaseX;
        private float _detectorPointerBaseY;
        private float _detectorPointerBaseWidth;
        private int _detectorPointerBaseValue;
        private CharacterTestRuntimeAiLabDetectorFieldKind _detectorGestureField;
        private bool _detectorGestureCaptured;
        private bool _detectorGestureMoved;
        private long _transitionConnectEpoch;
        private DetectorSlot _transitionConnectSource;
        private bool _transitionConnectCaptured;
        private readonly Fgui.GGraph[] _transitionConnectPreview = new Fgui.GGraph[12];
        private Fgui.GGraph _endPort;
        private long _geometryKey;
        private bool _hasGeometry;

        internal int GeometryRebuildCount { get; private set; }
        internal int RuntimeStyleUpdateCount { get; private set; }

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler commands,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            Action toggleLogicLibraryDrawer = null,
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            _commands = commands;
            _commandSink = commandSink;
            _viewId = viewId;
            _toggleLogicLibraryDrawer = toggleLogicLibraryDrawer;
            _bindTooltip = bindTooltip;
            InitializeStageGesturePreviewTextCache();
        }

        private void BindTooltip(Fgui.GObject target, string text)
        {
            if (target == null)
                return;
            if (_bindTooltip != null)
                _bindTooltip(target, text ?? string.Empty);
            else
                target.tooltips = string.Empty;
        }

        /// <remarks>LowFreqAlloc only when a new stage/detector/transition high-water mark is reached.</remarks>
        internal void Bind(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            bool visible,
            bool behaviorLogic = false)
        {
            if (!ReferenceEquals(_root, root))
                ResetForNewRoot(root);
            if (_root == null)
                return;
            _behaviorLogicMode = behaviorLogic;
            _root.visible = visible;
            if (!visible)
            {
                HideRecipeTimeline();
                CancelStagePointerGesture(clearSelection: false);
                CancelGraphFieldEditVisual(deactivating: true);
                _commands?.CancelCanonicalizationConfirmation();
                HideAll();
                // HideAll clears the pooled visuals, so the next visible bind
                // must restore their geometry even when the data key is unchanged.
                _hasGeometry = false;
                return;
            }
            viewModel ??= new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel();
            if (visible)
            {
                // Plan mode has its own binder and hidden modes already returned above.
                // Therefore every visible call on this binder is the Track/Event recipe
                // editor; the retired connector graph cannot be re-entered by callers.
                BindRecipeTimeline(viewModel);
                return;
            }
            HideRecipeTimeline();
            if (_stageMarqueeCaptured)
                CancelStageMarquee();
            _commands?.ReconcileStageEditing(viewModel);
            if (_stageGestureCaptured && !(_commands?.HasActiveStageGesture ?? false))
                ClearStagePointerGestureState();
            bool canonicalizationRequired = viewModel.RequiresDetectorWindowCanonicalization;
            _canEdit = viewModel.CanEdit && !canonicalizationRequired;
            EnsureStaticHandlers();
            SetButtonEnabled(_root, "btnTimelineAddStage", _canEdit);
            SetButtonEnabled(_root, "btnTimelineAddCardDetector", _canEdit);
            SetButtonEnabled(
                _root,
                "btnTimelineCanonicalize",
                viewModel.CanEdit && canonicalizationRequired);
            SetButtonTitle(
                _root,
                "btnTimelineCanonicalize",
                _commands?.CanonicalizationConfirmationPending == true
                    ? "确认规范化"
                    : "修复旧配置");
            SetButtonEnabled(
                _root,
                "btnTimelineDelete",
                _canEdit && (_commands?.SelectedStageCount ?? 0) > 0);
            SetButtonEnabled(
                _root,
                "btnTimelineDuplicate",
                _canEdit && (_commands?.SelectedStageCount ?? 0) > 0);
            SetButtonEnabled(_root, "btnTimelineUndo", _commands?.CanUndoStageEdit ?? false);
            SetButtonEnabled(_root, "btnTimelineRedo", _commands?.CanRedoStageEdit ?? false);
            SetText("timelineSnapState", (_commands?.StageSnapEnabled ?? true) ? "吸附 15f" : "精度 1f");
            SetText(
                "timelineValidation",
                _commands?.CanonicalizationConfirmationPending == true
                    ? _commands.LastMessage
                    : viewModel.ValidationBadgeText);
            SetText("timelineRuntimeStatus", viewModel.RuntimeStatusText);
            SetText("timelineEmpty", viewModel.Available ? string.Empty : "当前行为卡没有可编辑的 Logic v2 时间轴");

            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                viewModel.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                viewModel.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                viewModel.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            int stageCount = Math.Min(viewModel.StageCount, stages.Length);
            int detectorCount = Math.Min(viewModel.DetectorCount, detectors.Length);
            int transitionCount = Math.Min(viewModel.TransitionCount, transitions.Length);
            bool revealAppendedBehaviorLogicStage = _behaviorLogicMode
                && _hasGeometry
                && stageCount > _activeStageCount;
            _boundStages = stages;
            _boundDetectors = detectors;
            _boundDetectorCount = detectorCount;
            _boundTransitions = transitions;
            _boundTransitionCount = transitionCount;
            _compactBehaviorLogicCard = CanUseCompactBehaviorLogicCard(
                stages,
                stageCount,
                detectors,
                detectorCount,
                transitions,
                transitionCount);
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                if (stage == null)
                    continue;
                stage.Selected = _commands?.IsStageSelected(stage.StageId) ?? stage.Selected;
                stage.PrimarySelection = string.Equals(
                    _commands?.PrimaryStageId,
                    stage.StageId,
                    StringComparison.Ordinal);
                stage.Expanded = string.Equals(
                    _commands?.ExpandedStageId,
                    stage.StageId,
                    StringComparison.Ordinal);
                stage.SelectionCount = stage.Selected ? _commands?.SelectedStageCount ?? 1 : 0;
            }
            ResolveSelectedToken(
                viewModel,
                stages,
                stageCount,
                detectors,
                detectorCount,
                transitions,
                transitionCount);
            ConfigureBehaviorLogicChrome(viewModel);
            EnsureLayoutScratch(stageCount, detectorCount, transitionCount);
            _activeStageCount = stageCount;
            _currentVerticalOffset = viewModel.VerticalScrollOffset;
            int cardDetectorCount = CountDetectors(detectors, detectorCount, -1);
            FillStageDetectorCounts(detectors, detectorCount, stageCount);
            int startFrame = Math.Max(0, viewModel.HorizontalStartFrame);
            int endFrame = Math.Max(startFrame + 1, viewModel.HorizontalEndFrame);
            float frameSpan = endFrame - startFrame;
            float canvasWidth = _behaviorLogicMode && _contentLayer != null
                ? Math.Max(1f, _contentLayer.width)
                : CanvasRight - CanvasLeft;
            long geometryKey = ComputeGeometryKey(viewModel, stages, stageCount);
            bool geometryChanged = !_hasGeometry || geometryKey != _geometryKey;
            if (geometryChanged)
            {
                GeometryRebuildCount++;
                if (_behaviorLogicMode)
                    HideLegacyRuler();
                else
                    DrawRuler(startFrame, endFrame, canvasWidth);
                LayoutStages(
                    stages,
                    stageCount,
                    startFrame,
                    frameSpan,
                    canvasWidth,
                    viewModel.VerticalScrollOffset,
                    cardDetectorCount,
                    revealAppendedBehaviorLogicStage);

                BindStages(stages, stageCount);
                BindDetectors(
                    detectors,
                    detectorCount,
                    stageCount,
                    startFrame,
                    frameSpan,
                    canvasWidth,
                    viewModel.VerticalScrollOffset);
                BindTransitions(transitions, transitionCount);
                if (_behaviorLogicMode)
                {
                    if (_stageEditorPopover != null)
                        _stageEditorPopover.visible = false;
                    HideStageTypedOptions();
                }
                else
                    BindStageEditorPopover(stages, stageCount);
                UpdateScrollBars(viewModel, stageCount, transitionCount);
                _geometryKey = ComputeGeometryKey(viewModel, stages, stageCount);
                _hasGeometry = true;
            }
            else
            {
                RuntimeStyleUpdateCount++;
                UpdateRuntimeVisuals(
                    stages,
                    stageCount,
                    detectors,
                    detectorCount,
                    transitions,
                    transitionCount);
            }
            if (_behaviorLogicMode)
            {
                PositionBehaviorLogicStructureControls(stageCount);
                RefreshTransitionResultMenuAnchor();
            }
            SetObjectVisible("timelineEmpty", !viewModel.Available || stageCount == 0);
        }

        internal void HideForPlan(Fgui.GComponent root)
        {
            if (!ReferenceEquals(_root, root))
                ResetForNewRoot(root);
            if (_root == null)
                return;
            HideRecipeTimeline();
            CancelStagePointerGesture(clearSelection: false);
            CancelGraphFieldEditVisual(deactivating: true);
            _commands?.CancelCanonicalizationConfirmation();
            HideAll();
            Fgui.GObject addStage = _root.GetChild("btnTimelineAddStage");
            if (addStage != null)
                addStage.alpha = 1f;
            _hasGeometry = false;
            _handlersConfigured = false;
        }

        private long ComputeGeometryKey(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            long key = viewModel.CachedStructureKey;
            key = key * 397L ^ viewModel.HorizontalStartFrame;
            key = key * 397L ^ viewModel.HorizontalEndFrame;
            key = key * 397L ^ BitConverter.SingleToInt32Bits(viewModel.VerticalScrollOffset);
            key = key * 397L ^ (_behaviorLogicMode ? 1 : 0);
            key = key * 397L ^ (_compactBehaviorLogicCard ? 1 : 0);
            key = key * 397L ^ BitConverter.SingleToInt32Bits(_behaviorLogicHorizontalOffset);
            key = key * 397L ^ (int)_selectedKind;
            key = key * 397L ^ StableStringHash(_selectedStableId);
            key = key * 397L ^ (_canEdit ? 1 : 0);
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                key = key * 397L ^ (stage?.Selected == true ? 1 : 0);
                key = key * 397L ^ (stage?.Expanded == true ? 1 : 0);
                key = key * 397L ^ (i < _stagePool.Count
                    ? _stagePool[i].DetectorScrollOffset
                    : 0);
            }
            return key;
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; value != null && i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        private void UpdateRuntimeVisuals(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            for (int i = 0; i < stageCount && i < _stagePool.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                Fgui.GComponent card = _stagePool[i].Visual;
                string runtimeStatus = _compactBehaviorLogicCard
                    ? ResolveCompactBehaviorLogicRuntimeStatus(stage)
                    : stage.RuntimeStatusText;
                SetText(card, "runtime", runtimeStatus);
                SetObjectVisible(card, "runtime", !string.IsNullOrEmpty(runtimeStatus));
                ColorCard(card, stage);
            }
            int used = 0;
            for (int i = 0; i < detectorCount && used < _detectorPool.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                if (detector == null)
                    continue;
                DetectorSlot slot = _detectorPool[used++];
                slot.ViewModel = detector;
                SetText(
                    slot.Visual,
                    "status",
                    _behaviorLogicMode
                        ? ResolveBehaviorLogicDetectorStatus(detector)
                        : detector.StatusText);
                ColorDetector(slot.Visual, detector);
                if (_compactBehaviorLogicCard)
                    ApplyCompactBehaviorLogicDetectorTone(slot.Visual, detector);
            }
            for (int orderIndex = 0; orderIndex < transitionCount && orderIndex < _transitionPool.Count; orderIndex++)
            {
                int transitionIndex = _transitionOrder[orderIndex];
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[transitionIndex];
                TransitionSlot slot = _transitionPool[orderIndex];
                slot.ViewModel = transition;
                slot.RuntimeFrame = transition.RuntimeFrame;
                SetText(slot.Visual, "status", transition.StatusText);
                ColorTransition(slot.Visual, transition);
                if (_compactBehaviorLogicCard)
                    ApplyCompactBehaviorLogicTransitionTone(slot.Visual, transition);
                Color color = TransitionColor(transition);
                int start = _transitionConnectorStart[orderIndex];
                int end = Math.Min(_connectorPool.Count, start + _transitionConnectorCount[orderIndex]);
                for (int connector = start; connector < end; connector++)
                {
                    Fgui.GGraph graph = _connectorPool[connector];
                    graph.color = color;
                    graph.alpha = transition.Pending
                        && connector - start < 12
                        && (((connector - start) + 1) & 1) == 0
                            ? 0.45f
                            : transition.PreviousCommitted ? 0.58f : 1f;
                    graph.SetSize(graph.width, TransitionLineWidth(transition));
                }
                UpdateTransitionFlowState(orderIndex, transition);
            }
            RefreshBehaviorLogicFlowAnimation();
        }

        private void ResetForNewRoot(Fgui.GComponent root)
        {
            // Every pooled visual belongs to the FairyGUI root that created it. The
            // navigator disposes that root when it remounts this non-keep-alive page,
            // so retaining the GObject references would leave DisplayObject == null
            // and make the next SetXY/SetSize fail while switching behavior cards.
            // Do not touch the old objects here; their former root owns their lifetime.
            _commands?.CancelGraphFieldEdit(deactivating: false);
            _commands?.CancelTransitionConnect();
            _root = root;
            _contentLayer = null;
            _handlersConfigured = false;
            _stagePool.Clear();
            _detectorPool.Clear();
            _transitionPool.Clear();
            _connectorPool.Clear();
            ResetTransitionFlowVisuals();
            _rulerTickPool.Clear();
            _rulerLabelPool.Clear();
            _hasRulerLabels = false;
            _detectorGestureSlot = null;
            _detectorGestureCaptured = false;
            _detectorGestureMoved = false;
            _transitionConnectSource = null;
            _transitionConnectCaptured = false;
            _endPort = null;
            for (int i = 0; i < _transitionConnectPreview.Length; i++)
                _transitionConnectPreview[i] = null;
            _hasGeometry = false;
            _geometryKey = 0L;
            GeometryRebuildCount = 0;
            RuntimeStyleUpdateCount = 0;
            _stageEditorPopover = null;
            _stageTypedOptionMenu = null;
            _transitionResultMenu = null;
            _transitionResultDetectorId = string.Empty;
            _transitionResultTargetPickerMode = false;
            _transitionResultTargetPage = 0;
            _stageEditorHandlersConfigured = false;
            _stageEditorStableId = string.Empty;
            ClearStageFieldEditVisualState();
            _lastRulerStartFrame = 0;
            _lastRulerEndFrame = 0;
            _selectedStableId = string.Empty;
            _selectedKind = CharacterTestRuntimeAiLabLogicElementKind.None;
            _conditionDetectorId = string.Empty;
            _conditionIndex = 0;
            ClearGraphFieldEditVisualState();
            ClearStagePointerGestureState();
            _contentExtentHeight = ContentHeight;
            _behaviorLogicHorizontalOffset = 0f;
            _behaviorLogicContentWidth = 0f;
            _activeStageCount = 0;
            _activeDetectorCount = 0;
            _boundDetectors = Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            _boundStages = Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            _boundTransitions = Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            _boundDetectorCount = 0;
            _boundTransitionCount = 0;
            _compactBehaviorLogicCard = false;
            ResetRecipeForNewRoot();
        }

        private void SetRootSortingOrder(string childName, int sortingOrder)
        {
            Fgui.GObject child = _root?.GetChild(childName);
            if (child != null)
                child.sortingOrder = sortingOrder;
        }

        private void PositionTimelineButton(
            string childName,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject button = _root?.GetChild(childName);
            if (button == null)
                return;
            button.SetXY(x, y);
            button.SetSize(width, height);
        }

        private void SetTimelineButtonFontSize(string childName, int size)
        {
            Fgui.GButton button = _root?.GetChild(childName)?.asButton;
            if (button != null && button.titleFontSize != size)
                button.titleFontSize = size;
        }

        private static void SetTimelineTextFontSize(Fgui.GTextField label, int size)
        {
            if (label == null)
                return;
            Fgui.TextFormat format = label.textFormat;
            if (format.size == size)
                return;
            format.size = size;
            label.textFormat = format;
        }

    }
}
