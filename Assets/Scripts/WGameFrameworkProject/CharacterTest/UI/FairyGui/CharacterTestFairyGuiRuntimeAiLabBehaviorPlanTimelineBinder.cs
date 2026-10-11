using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder
    {
        private const float GameplayPlanToolbarHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.HeaderHeight;
        private const float GameplayPlanLabelWidth = 24f;
        private const float GameplayPlanCanvasRightPadding =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasRightPadding;
        private const int GameplayPlanTrackCount = 2;
        private const int GameplayPlanRulerDivisionCount = 4;
        private const int GameplayPlanFrameLabelLimit = 4096;
        private static readonly float[] GameplayPlanTrackY = { 88f, 160f };
        private static readonly float[] GameplayPlanTrackHeight = { 64f, 72f };
        private static readonly string[] GameplayPlanOpenFrameLabels =
            CreateGameplayPlanOpenFrameLabels(string.Empty);
        private static readonly string[] GameplayPlanCompactOpenFrameLabels =
            CreateGameplayPlanCompactOpenFrameLabels();
        private static readonly string[] GameplayPlanCompletedOpenFrameLabels =
            CreateGameplayPlanOpenFrameLabels("✓ ");
        private static readonly string[] GameplayPlanFailedOpenFrameLabels =
            CreateGameplayPlanOpenFrameLabels("× ");
        private static readonly Dictionary<int, GameplayPlanDeterminateFrameLabels>
            GameplayPlanDeterminateFrameLabelCache =
                new Dictionary<int, GameplayPlanDeterminateFrameLabels>(16);
        private static readonly string[] GameplayPlanTrackLabels =
        {
            "阶段", "动作"
        };
        private static readonly string[] GameplayPlanHiddenChrome =
        {
            "btnTimelineAddCardDetector", "btnTimelineCanonicalize", "btnTimelineDelete",
            "btnTimelineDuplicate", "btnTimelineUndo", "btnTimelineRedo",
            "timelineSnapState", "timelineGesturePreview", "timelineMarquee",
            "timelineEdgeScrollLeft", "timelineEdgeScrollRight",
            "btnTimelinePanLeft", "btnTimelinePanRight",
            "btnTimelineScrollUp", "btnTimelineScrollDown",
            "rulerBackground", "timelineRuntimeStatus", "timelineHorizontalScrollTrack",
            "timelineHorizontalScrollThumb", "timelineVerticalScrollTrack",
            "timelineVerticalScrollThumb"
        };
        private sealed class GameplayPlanDeterminateFrameLabels
        {
            internal string[] Running;
            internal string[] Failed;
            internal string Completed;
            internal string Overrun;
        }

        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _commands;
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly IBehaviorPlanInteractionSink _planInteractions;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private Fgui.GComponent _root;
        private bool _handlersConfigured;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineSession _session =
            new CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineSession();
        private CharacterTestRuntimeAiLabLogicElementKind _selectedKind => _session.SelectedKind;
        private string _selectedStableId => _session.SelectedStableId;
        private readonly GameplayReadablePlanProjection _gameplayPlanProjection =
            new GameplayReadablePlanProjection();
        private readonly Action _gameplayPlanDetailChanged;
        private Fgui.GComponent _gameplayPlanLayer;
        private Fgui.GGraph _gameplayPlanRulerBackground;
        private readonly Fgui.GGraph[] _gameplayPlanTrackBackgrounds =
            new Fgui.GGraph[GameplayPlanTrackCount];
        private readonly Fgui.GTextField[] _gameplayPlanTrackLabels =
            new Fgui.GTextField[GameplayPlanTrackCount];
        private readonly Fgui.GGraph[] _gameplayPlanRulerTicks =
            new Fgui.GGraph[GameplayPlanRulerDivisionCount + 1];
        private readonly Fgui.GTextField[] _gameplayPlanRulerLabels =
            new Fgui.GTextField[GameplayPlanRulerDivisionCount + 1];
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineVisualPool
            _visualPool = new CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineVisualPool();
        private List<GameplayPlanBarSlot> _gameplayPlanStageBars => _visualPool.StageBars;
        private List<GameplayPlanBarSlot> _gameplayPlanActionBars => _visualPool.ActionBars;
        private List<GameplayPlanExitSlot> _gameplayPlanExitCards => _visualPool.ExitCards;
        private List<GameplayPlanBranchSlot> _gameplayPlanBranchNodes => _visualPool.BranchNodes;
        private List<Fgui.GGraph> _gameplayPlanConnectorLines => _visualPool.ConnectorLines;
        private List<Fgui.GComponent> _gameplayPlanGlobalBands => _visualPool.GlobalBands;
        private List<GameplayPlanCandidateSlot> _gameplayPlanCandidateCards =>
            _visualPool.CandidateCards;
        private readonly List<Rect> _gameplayPlanExitGroupBounds = new List<Rect>(16);
        private Fgui.GTextField _gameplayPlanExecutionStatus;
        private readonly Fgui.GGraph[] _gameplayPlanBehaviorNodeBackgrounds =
            new Fgui.GGraph[3];
        private readonly Fgui.GGraph[] _gameplayPlanBehaviorExitBackgrounds =
            new Fgui.GGraph[2];
        private Fgui.GTextField _gameplayPlanRelationshipLabel;
        private Fgui.GTextField _gameplayPlanSuccessExitLabel;
        private Fgui.GTextField _gameplayPlanFailureExitLabel;
        private Fgui.GTextField _gameplayPlanSummaryPrefix;
        private Fgui.GTextField _gameplayPlanSummaryCurrent;
        private Fgui.GTextField _gameplayPlanSummaryFramePrefix;
        private Fgui.GTextField _gameplayPlanSummaryFrame;
        private Fgui.GTextField _gameplayPlanSummaryNextPrefix;
        private Fgui.GTextField _gameplayPlanSummaryNext;
        private Fgui.GTextField _gameplayPlanExitEmpty;
        private long _gameplayPlanGeometryKey;
        private bool _hasGameplayPlanGeometry;
        private int _gameplayPlanStageCount;
        private int _gameplayPlanActionCount;
        private int _gameplayPlanExitCount;
        private int _gameplayPlanBranchCount;
        private int _gameplayPlanConnectorCount;
        private int _gameplayPlanGlobalBandCount;
        private int _gameplayPlanCandidateCount;
        private GameplayReadablePlanDetailKind _gameplayPlanDetailKind
        {
            get => _session.DetailKind;
            set => _session.DetailKind = value;
        }
        private string _gameplayPlanDetailStableId
        {
            get => _session.DetailStableId;
            set => _session.DetailStableId = value;
        }
        private int _gameplayPlanPreviousBehaviorEntryIndex
        {
            get => _session.PreviousBehaviorEntryIndex;
            set => _session.PreviousBehaviorEntryIndex = value;
        }
        private int _gameplayPlanCommittedBehaviorEntryIndex
        {
            get => _session.CommittedBehaviorEntryIndex;
            set => _session.CommittedBehaviorEntryIndex = value;
        }
        private int _gameplayPlanNextBehaviorEntryIndex
        {
            get => _session.NextBehaviorEntryIndex;
            set => _session.NextBehaviorEntryIndex = value;
        }

        internal bool DetailVisible =>
            _gameplayPlanDetailKind != GameplayReadablePlanDetailKind.None;
        internal GameplayReadablePlanDetailKind DetailKind => _gameplayPlanDetailKind;
        internal string DetailStableId => _gameplayPlanDetailStableId;
        internal GameplayReadablePlanProjection Projection => _gameplayPlanProjection;

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler commands,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            Action gameplayPlanDetailChanged,
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            _commands = commands;
            _commandSink = commandSink;
            _viewId = viewId;
            _planInteractions = new BehaviorPlanInteractionAdapter(commandSink, viewId);
            _gameplayPlanDetailChanged = gameplayPlanDetailChanged;
            _bindTooltip = bindTooltip;
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

        /// <remarks>LowFreqAlloc only when the visible plan reaches a new high-water mark.</remarks>
        internal void Bind(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            bool visible)
        {
            if (!ReferenceEquals(_root, root))
                ResetForNewRoot(root);
            if (_root == null)
                return;
            _root.visible = visible;
            if (!visible)
            {
                HideGameplayPlan();
                return;
            }

            viewModel ??= new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel();
            EnsureToolbarHandlers();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                viewModel.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                viewModel.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                viewModel.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            int stageCount = Math.Min(viewModel.StageCount, stages.Length);
            int detectorCount = Math.Min(viewModel.DetectorCount, detectors.Length);
            int transitionCount = Math.Min(viewModel.TransitionCount, transitions.Length);
            ResolveSelectedToken(
                viewModel,
                stages,
                stageCount,
                detectors,
                detectorCount,
                transitions,
                transitionCount);
            BindGameplayPlan(
                viewModel,
                stages,
                stageCount,
                detectors,
                detectorCount,
                transitions,
                transitionCount);
        }

        internal void Hide(Fgui.GComponent root)
        {
            if (!ReferenceEquals(_root, root))
                ResetForNewRoot(root);
            HideGameplayPlan();
            _handlersConfigured = false;
        }

        /// <remarks>
        /// LowFreqAlloc only when a new projection or visible-item high-water mark is reached.
        /// Stable refresh updates the playhead and runtime styles without collection allocation.
        /// </remarks>
        private void BindGameplayPlan(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            EnsureGameplayPlanLayer();
            ConfigureGameplayPlanChrome(viewModel);
            _gameplayPlanLayer.visible = true;
            _gameplayPlanLayer.SetSize(_root.width, _root.height);
            _gameplayPlanLayer.rootContainer.clipRect = new Rect(0f, 0f, _root.width, _root.height);
            long geometryKey = ComputeGameplayPlanGeometryKey(viewModel);
            if (!_hasGameplayPlanGeometry || geometryKey != _gameplayPlanGeometryKey)
            {
                BehaviorPlanProjectionBuilder.Rebuild(_gameplayPlanProjection, viewModel);
                RenderGameplayPlanGeometry(viewModel);
                _gameplayPlanGeometryKey = geometryKey;
                _hasGameplayPlanGeometry = true;
                if (DetailVisible
                    && _gameplayPlanProjection.FindStage(_gameplayPlanDetailStableId) == null
                    && _gameplayPlanProjection.FindExit(_gameplayPlanDetailStableId) == null)
                {
                    ShowGameplayPlanOverview();
                }
            }
            else
            {
                BehaviorPlanRuntimeOverlayBuilder.Update(_gameplayPlanProjection, viewModel);
            }
            UpdateGameplayPlanRuntimeStyles(viewModel);
            SetObjectVisible("timelineEmpty", !viewModel.Available || stageCount == 0);
        }

        private long ComputeGameplayPlanGeometryKey(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            long key = viewModel.CachedStructureKey;
            key = key * 397L ^ viewModel.HorizontalStartFrame;
            key = key * 397L ^ viewModel.HorizontalEndFrame;
            key = key * 397L ^ (int)_selectedKind;
            key = key * 397L ^ StableStringHash(_selectedStableId);
            key = key * 397L ^ (int)_gameplayPlanDetailKind;
            key = key * 397L ^ StableStringHash(_gameplayPlanDetailStableId);
            key = key * 397L ^ viewModel.BehaviorCandidateCount;
            int candidateCount = Math.Min(
                viewModel.BehaviorCandidateCount,
                viewModel.BehaviorCandidates?.Length ?? 0);
            for (int i = 0; i < candidateCount; i++)
                key = key * 397L ^ StableStringHash(viewModel.BehaviorCandidates[i]?.StableId);
            key = key * 397L ^ Mathf.RoundToInt(_root.width);
            key = key * 397L ^ Mathf.RoundToInt(_root.height);
            return key;
        }

        private void ConfigureGameplayPlanChrome(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            SetObjectVisible("background", true);
            SetObjectVisible("toolbar", true);
            SetObjectVisible("timelineTitle", false);
            SetObjectVisible("btnTimelineAddStage", false);
            SetObjectVisible("btnTimelineSnap", false);
            SetObjectVisible("btnTimelineZoomOut", false);
            SetObjectVisible("btnTimelineZoomIn", false);
            SetObjectVisible("btnTimelineFit", false);
            string validationText = viewModel?.ValidationBadgeText ?? string.Empty;
            bool showValidation = !string.IsNullOrEmpty(validationText)
                && !string.Equals(validationText, "验证通过", StringComparison.Ordinal);
            SetObjectVisible("timelineValidation", showValidation);
            for (int i = 0; i < GameplayPlanHiddenChrome.Length; i++)
                SetObjectVisible(GameplayPlanHiddenChrome[i], false);

            Fgui.GObject background = _root.GetChild("background");
            background?.SetSize(_root.width, _root.height);
            Fgui.GObject toolbar = _root.GetChild("toolbar");
            if (toolbar != null)
            {
                toolbar.SetXY(0f, 0f);
                toolbar.SetSize(_root.width, GameplayPlanToolbarHeight);
            }
            Fgui.GObject title = _root.GetChild("timelineTitle");
            if (title != null)
            {
                SetGameplayPlanTextFontSize(title.asTextField, 28);
                BindTooltip(title, viewModel.CardTitleText);
            }
            Fgui.GObject validation = _root.GetChild("timelineValidation");
            if (validation != null)
            {
                validation.SetXY(1694f, 10f);
                validation.SetSize(206f, 52f);
                SetGameplayPlanTextFontSize(validation.asTextField, 24);
            }
            SetText("timelineValidation", CompactGameplayPlanValidation(validationText));
            if (validation != null)
                BindTooltip(validation, validationText);
            Fgui.GObject empty = _root.GetChild("timelineEmpty");
            if (empty != null)
            {
                empty.SetXY(GameplayPlanLabelWidth + 80f, 252f);
                empty.SetSize(_root.width - GameplayPlanLabelWidth - 160f, 96f);
                SetGameplayPlanTextFontSize(empty.asTextField, 28);
            }
        }

        private static void SetGameplayPlanTextFontSize(Fgui.GTextField label, int size)
        {
            if (label == null)
                return;
            Fgui.TextFormat format = label.textFormat;
            if (format.size == size)
                return;
            format.size = size;
            label.textFormat = format;
        }

        private void EnsureGameplayPlanLayer()
        {
            if (_gameplayPlanLayer != null)
                return;
            _gameplayPlanLayer = new Fgui.GComponent
            {
                name = "runtimeGameplayPlanLayer",
                touchable = true,
                sortingOrder = 2
            };
            _gameplayPlanLayer.onClick.Set(context =>
            {
                context.StopPropagation();
                ShowGameplayPlanOverview();
            });
            _root.AddChild(_gameplayPlanLayer);

            _gameplayPlanRulerBackground = NewGraph("planRulerBackground", false);
            _gameplayPlanLayer.AddChild(_gameplayPlanRulerBackground);
            for (int i = 0; i < GameplayPlanTrackCount; i++)
            {
                Fgui.GGraph row = NewGraph(
                    "planTrackBackground" + i.ToString(CultureInfo.InvariantCulture),
                    false);
                _gameplayPlanTrackBackgrounds[i] = row;
                _gameplayPlanLayer.AddChild(row);

                Fgui.GTextField label = NewGameplayPlanText(
                    "planTrackLabel" + i.ToString(CultureInfo.InvariantCulture),
                    GameplayPlanTrackLabels[i],
                    28,
                    new Color32(218, 231, 237, 255),
                    Fgui.AlignType.Left);
                _gameplayPlanTrackLabels[i] = label;
                _gameplayPlanLayer.AddChild(label);
            }
            for (int i = 0; i <= GameplayPlanRulerDivisionCount; i++)
            {
                Fgui.GGraph tick = NewGraph(
                    "planRulerTick" + i.ToString(CultureInfo.InvariantCulture),
                    false);
                _gameplayPlanRulerTicks[i] = tick;
                _gameplayPlanLayer.AddChild(tick);
                Fgui.GTextField label = NewGameplayPlanText(
                    "planRulerLabel" + i.ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    24,
                    new Color32(169, 188, 198, 255),
                    Fgui.AlignType.Center);
                _gameplayPlanRulerLabels[i] = label;
                _gameplayPlanLayer.AddChild(label);
            }

            _gameplayPlanExecutionStatus = NewGameplayPlanText(
                "planExecutionStatus",
                string.Empty,
                22,
                new Color32(137, 158, 169, 255),
                Fgui.AlignType.Left);
            for (int i = 0; i < _gameplayPlanBehaviorNodeBackgrounds.Length; i++)
            {
                int nodeIndex = i;
                Fgui.GGraph background = NewGraph(
                    "planBehaviorNodeBackground" + i.ToString(CultureInfo.InvariantCulture),
                    true);
                background.onClick.Set(context =>
                {
                    context.StopPropagation();
                    SelectGameplayPlanBehaviorNode(nodeIndex);
                });
                _gameplayPlanBehaviorNodeBackgrounds[i] = background;
                _gameplayPlanLayer.AddChild(background);
            }
            for (int i = 0; i < _gameplayPlanBehaviorExitBackgrounds.Length; i++)
            {
                Fgui.GGraph background = NewGraph(
                    "planBehaviorExitBackground" + i.ToString(CultureInfo.InvariantCulture),
                    false);
                _gameplayPlanBehaviorExitBackgrounds[i] = background;
                _gameplayPlanLayer.AddChild(background);
            }
            _gameplayPlanRelationshipLabel = NewGameplayPlanText(
                "planBehaviorRelationshipLabel",
                "行为关系",
                28,
                new Color32(218, 231, 237, 255),
                Fgui.AlignType.Left);
            _gameplayPlanSuccessExitLabel = NewGameplayPlanText(
                "planBehaviorSuccessExit",
                "完成",
                20,
                new Color32(225, 238, 231, 255),
                Fgui.AlignType.Center);
            _gameplayPlanFailureExitLabel = NewGameplayPlanText(
                "planBehaviorFailureExit",
                "失败",
                20,
                new Color32(241, 225, 225, 255),
                Fgui.AlignType.Center);
            _gameplayPlanLayer.AddChild(_gameplayPlanRelationshipLabel);
            _gameplayPlanLayer.AddChild(_gameplayPlanSuccessExitLabel);
            _gameplayPlanLayer.AddChild(_gameplayPlanFailureExitLabel);
            _gameplayPlanSummaryPrefix = NewGameplayPlanText(
                "planSummaryPrefix",
                string.Empty,
                20,
                new Color32(184, 201, 209, 255),
                Fgui.AlignType.Left);
            _gameplayPlanSummaryCurrent = NewGameplayPlanText(
                "planSummaryCurrent",
                string.Empty,
                20,
                new Color32(78, 229, 219, 255),
                Fgui.AlignType.Left);
            _gameplayPlanSummaryFramePrefix = NewGameplayPlanText(
                "planSummaryFramePrefix",
                string.Empty,
                20,
                new Color32(137, 158, 169, 255),
                Fgui.AlignType.Center);
            _gameplayPlanSummaryFrame = NewGameplayPlanText(
                "planSummaryFrame",
                string.Empty,
                20,
                new Color32(213, 224, 230, 255),
                Fgui.AlignType.Center);
            _gameplayPlanSummaryNextPrefix = NewGameplayPlanText(
                "planSummaryNextPrefix",
                string.Empty,
                20,
                new Color32(137, 158, 169, 255),
                Fgui.AlignType.Left);
            _gameplayPlanSummaryNext = NewGameplayPlanText(
                "planSummaryNext",
                string.Empty,
                20,
                new Color32(70, 158, 225, 255),
                Fgui.AlignType.Left);
            _gameplayPlanExitEmpty = NewGameplayPlanText(
                "planExitEmpty",
                "暂无分支",
                24,
                new Color32(137, 158, 169, 255),
                Fgui.AlignType.Left);
            _gameplayPlanLayer.AddChild(_gameplayPlanExecutionStatus);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryPrefix);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryCurrent);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryFramePrefix);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryFrame);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryNextPrefix);
            _gameplayPlanLayer.AddChild(_gameplayPlanSummaryNext);
            _gameplayPlanLayer.AddChild(_gameplayPlanExitEmpty);
        }

    }
}
