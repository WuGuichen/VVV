using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder
    {
        private const float RuntimeFrameEventDetailWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth;
        private const float BehaviorPlanDecisionHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        private const float DecisionContentY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY;
        internal const int RuntimeFrameEventDetailVisibleMemberCount = 5;
        internal static readonly string[] TimelineTrackNames =
        {
            "trackRevision", "trackDecision", "trackAction", "trackCombat", "trackWarnings"
        };
        private static readonly string[] TimelineTrackLabels =
        {
            "感知", "决策", "动作", "结果", "警告"
        };
        private static readonly Vector2[] TimelineSelectedBadgePolygon =
        {
            new Vector2(0f, 0f),
            new Vector2(34f, 0f),
            new Vector2(34f, 18f),
            new Vector2(21f, 18f),
            new Vector2(17f, 24f),
            new Vector2(13f, 18f),
            new Vector2(0f, 18f)
        };


        /// <summary>
        /// Converts authored sRGB palette values before assigning FairyGUI vertex colors in a
        /// Linear-color-space Unity project.
        /// </summary>
        /// <remarks>NoAlloc: presentation-only color conversion.</remarks>
        private static Color RuntimeFrameSrgb(byte red, byte green, byte blue, byte alpha = 255)
        {
            return new Color(
                Mathf.GammaToLinearSpace(red / 255f),
                Mathf.GammaToLinearSpace(green / 255f),
                Mathf.GammaToLinearSpace(blue / 255f),
                alpha / 255f);
        }
        private static readonly string[] TimelinePassiveChildren =
        {
            "timelineLoopRange", "timelineRetainedMask", "timelineFutureMask", "timelineFutureAdvanceFill",
            "timelineHistoryProgress", "timelinePlayhead", "timelinePreviewPlayhead",
            "timelinePlayheadHitArea",
            "timelineSelectedFrameBadge", "timelineSelectedFrameLabel",
            "timelinePreviewFrameBadge", "timelinePreviewFrameLabel", "timelineViewport"
        };
        private static readonly string[] LegacyTimelineRootChildren =
        {
            "bottomPanel", "timelineHeader", "timelineText",
            "timelineRevisionTrackText", "timelineDecisionTrackText", "timelineActionTrackText",
            "timelineCombatTrackText", "timelineWarningTrackText", "timelineProgressTrack",
            "timelineProgressFill", "timelineProgressThumb", "timelineScrubTrack",
            "btnLatestFrame", "jumpFrameInputBg", "jumpFrameInput",
            "btnJumpFrame", "btnLoopStart", "btnLoopEnd", "btnToggleLoop"
        };
        private static readonly string[] RuntimeFrameHiddenControls =
        {
            "timelineText", "timelineStatus",
            "btnTimelinePanLeft", "btnTimelinePanRight",
            "btnLatestFrame", "btnFirstFrame", "btnStepBack10", "btnStepBack",
            "btnTimelinePlayPause", "btnTimelinePlaybackSpeed", "btnNextFrame", "btnStepForward10",
            "jumpFrameInput", "btnJumpFrame", "btnLoopStart", "btnLoopEnd", "btnToggleLoop",
            "branchRecomputeRiskText"
        };
        private static readonly string[] BehaviorPlanHiddenRuntimeControls =
        {
            "timelineHeader", "timelineText", "timelineStatus",
            "btnTimelinePanLeft", "btnTimelinePanRight",
            "btnTimelineZoomOut", "btnTimelineZoomIn", "btnTimelineFit",
            "btnTimelineCollapse", "btnTimelineMore", "btnBranchRecompute",
            "btnLatestFrame", "btnFirstFrame", "btnStepBack10", "btnStepBack",
            "btnTimelinePlayPause", "btnTimelinePlaybackSpeed", "btnNextFrame", "btnStepForward10",
            "jumpFrameInput", "btnJumpFrame", "btnLoopStart", "btnLoopEnd",
            "btnToggleLoop", "branchRecomputeRiskText"
        };
        private static readonly string[] BehaviorPlanDetailHiddenChildren =
        {
            "detailMeta", "detailFrame", "detailEntities", "detailTrace",
            "detailEvidence", "detailParticipants", "memberScrollHint",
            "btnLocateStart", "btnLocateEnd", "btnCloseDetail"
        };
        private static readonly string[] BehaviorPlanDetailFieldNames =
        {
            "planActionBackground", "planActionTitle",
            "planActionStartLabel", "planActionStartValue",
            "planActionDurationLabel", "planActionDurationValue",
            "planActionCompleteLabel", "planActionCompleteValue",
            "planActionFailureLabel", "planActionFailureValue"
        };
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder _owner;
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly ICharacterTestRuntimeAiLabTimelineCommandSink _commands;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder _behaviorCardWorkspaceBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceSession _session = new CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceSession();
        private readonly CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder _runtimeFrameTimeline = new CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder();
        private readonly CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder _replayTimeline = new CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder();
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder _timelineDetail;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder _behaviorPlanDecisionBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorLogicDecisionBinder _behaviorLogicDecisionBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
            _behaviorTriggerEventInspector;
        private readonly CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineSurfaceBinder _runtimeFrameSurface;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder _liveTimelineRuler;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder _replayTimelineRuler;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder _liveTimelineItems;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder _replayTimelineItems;
        private readonly ICharacterTestRuntimeAiLabTimelineViewportIntentSink _timelineViewportIntentSink;
        private readonly ICharacterTestRuntimeAiLabTimelineViewportIntentSink _replayTimelineViewportIntentSink;
        private CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder _liveTimeline => _runtimeFrameTimeline.Renderer;
        private Fgui.GComponent _decisionEventDetails;
        private bool _decisionDetailInteractionsBound;
        private CharacterTestRuntimeAiLabTimelineItem _decisionDetailItem;
        private Fgui.GTextInput _activeBehaviorLogicTypedInput;
        private bool _timelineScrubActive { get => _runtimeFrameTimeline.ScrubActive; set => _runtimeFrameTimeline.ScrubActive = value; }
        private CharacterTestRuntimeAiLabTimelineViewportInputBinder _timelineViewportInputBinder => _runtimeFrameTimeline.Input;
        private bool _behaviorPlanTechnicalDetailsVisible { get => _behaviorPlanDecisionBinder.TechnicalDetailsVisible; set => _behaviorPlanDecisionBinder.TechnicalDetailsVisible = value; }
        private Fgui.GComponent _bindingRoot => _session.Root;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _boundViewModel => _session.ViewModel;

        private void BindTooltip(Fgui.GObject target, string value)
        {
            _owner.BindTooltip(target, value);
        }

        internal CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder(CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder owner, IMxUiCommandSink commandSink, MxUiViewId viewId, CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder behaviorCardWorkspaceBinder)
        {
            _owner = owner; _commandSink = commandSink; _viewId = viewId;
            _commands = new CharacterTestRuntimeAiLabTimelineCommandAdapter(commandSink, viewId);
            _behaviorCardWorkspaceBinder = behaviorCardWorkspaceBinder;
            _behaviorPlanDecisionBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder(BindTooltip);
            _behaviorLogicDecisionBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorLogicDecisionBinder(_commands, _behaviorPlanDecisionBinder);
            _behaviorTriggerEventInspector =
                new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(
                    commandSink,
                    viewId,
                    owner.PopupService,
                    timelineId => behaviorCardWorkspaceBinder?.TryLocateRecipeTimeline(timelineId) == true,
                    () => behaviorCardWorkspaceBinder?.TryClearRecipeEventSelection(),
                    (target, text) => owner.BindTooltip(target, text));
            behaviorCardWorkspaceBinder?.SetCharacterActionPicker(
                (anchor, select) =>
                    _behaviorTriggerEventInspector.ShowCharacterActionPickerForCreation(anchor, select));
            _runtimeFrameSurface = new CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineSurfaceBinder(owner, _commands, _runtimeFrameTimeline);
            _liveTimelineRuler =
                new CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder(_liveTimeline);
            _replayTimelineRuler =
                new CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder(_replayTimeline);
            _timelineViewportIntentSink = new CharacterTestRuntimeAiLabLiveTimelineViewportIntentSink(commandSink, viewId);
            _replayTimelineViewportIntentSink = new CharacterTestRuntimeAiLabReplayTimelineViewportIntentSink(commandSink, viewId);
            _timelineDetail = new CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder(
                _timelineViewportIntentSink,
                BindTooltip,
                owner.SelectRuntimeTestCausalItem);
            _liveTimelineItems = new CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder(
                _liveTimeline,
                _liveTimelineRuler,
                _timelineViewportIntentSink,
                BindTooltip);
            _replayTimelineItems = new CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder(
                _replayTimeline,
                _replayTimelineRuler,
                _replayTimelineViewportIntentSink,
                BindTooltip);
        }
        internal void Bind(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            _session.Bind(root, viewModel);
            Fgui.GComponent timeline = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
            Fgui.GComponent viewport = timeline?.GetChild("runtimeFrameFiveTrackViewport")?.asCom;
            if (timeline == null || viewport == null) return;
            HideLegacyTimeline(root);
            if ((viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    && viewModel.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null)
                || (viewModel.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null
                    && (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard || viewModel.ShowBehaviorLogic)))
            {
                SwitchMode(viewport, TimelineWorkspaceMode.BehaviorLogic);
                foreach (var child in timeline.GetChildren()) child.visible = false;
                timeline.visible = viewModel.BehaviorCardWorkspace.Timeline.TacticalPreview != null;
                var inspector = (timeline.GetChild("behaviorRecipeInspector") ?? timeline.GetChild("behaviorTriggerEventInspector"))?.asCom;
                if (inspector != null && timeline.visible) _behaviorTriggerEventInspector.Bind(inspector, viewModel.BehaviorCardWorkspace.Timeline.RecipeInspector);
                else if (inspector != null) _behaviorTriggerEventInspector.Hide(inspector);
                return;
            }
            bool runtimeWorkspace = viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
            bool showTimeline = runtimeWorkspace && viewModel.ShowTimeline;
            bool showBehaviorPlan = runtimeWorkspace && viewModel.ShowBehaviorPlan;
            bool showBehaviorLogic = runtimeWorkspace && viewModel.ShowBehaviorLogic;
            SwitchMode(viewport, showBehaviorPlan ? TimelineWorkspaceMode.BehaviorPlan : showBehaviorLogic ? TimelineWorkspaceMode.BehaviorLogic : showTimeline ? TimelineWorkspaceMode.RuntimeFrame : TimelineWorkspaceMode.Hidden);
            if (!showBehaviorPlan) _behaviorPlanTechnicalDetailsVisible = false;
            timeline.visible = showTimeline || showBehaviorPlan || showBehaviorLogic;
            if (!showBehaviorPlan && !showBehaviorLogic)
            {
                var inspector = (timeline.GetChild("behaviorRecipeInspector") ?? timeline.GetChild("behaviorTriggerEventInspector"))?.asCom;
                if (inspector != null) _behaviorTriggerEventInspector.Hide(inspector);
            }
            if (!timeline.visible) { ClearTimelinePreview(viewport); _runtimeFrameTimeline.Hide(); return; }
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace = viewModel.RuntimeFrame;
            if (showBehaviorPlan || showBehaviorLogic) { BindBehaviorPlanDecisionSurface(timeline, viewport, viewModel); return; }
            _liveTimelineItems.EnsurePreheated();
            BindText(timeline, "timelineHeader", "RuntimeFrame 历史");
            string contextText = workspace?.ContextText ?? string.Empty;
            string rangeText = workspace?.RangeText ?? viewModel.TimelineText;
            BindText(timeline, "timelineText", string.IsNullOrEmpty(contextText) ? rangeText : contextText + "  ·  " + rangeText);
            BindText(timeline, "timelineStatus", workspace?.StatusText ?? viewModel.TimelineStatusText);
            BindText(timeline, "jumpFrameInput", workspace?.JumpFrameInputText ?? string.Empty);
            _runtimeFrameSurface.Bind(timeline, viewport, viewModel, workspace);
            _liveTimelineItems.Bind(viewport, viewModel, workspace);
            ApplyTimelineExpandedVisibility(viewport, viewModel.TimelineExpanded);
            _timelineDetail.Bind(timeline, viewModel);
            BindFormalTimelineScrub(viewport, viewModel);
            CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineSurfaceBinder.BindTrackLabels(viewport);
        }
        internal void ResetForRoot(Fgui.GComponent root)
        {
            _session.ResetForRoot(root);
            _runtimeFrameTimeline.ResetForRoot();
            _replayTimeline.ResetForRoot();
            _timelineDetail.ResetForRoot();
            _decisionEventDetails = null;
            _decisionDetailInteractionsBound = false;
            _decisionDetailItem = null;
            _behaviorPlanDecisionBinder.ResetForRoot();
        }

        internal void RefreshBehaviorLogicRuntimeOverlay(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent root = _bindingRoot;
            if (root == null || viewModel?.ShowBehaviorLogic != true)
                return;

            _session.Bind(root, viewModel);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace?.Timeline;
            Fgui.GComponent runtimeFrameWorkspace = root
                .GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)
                ?.asCom;
            Fgui.GComponent recipeInspector =
                (runtimeFrameWorkspace?.GetChild("behaviorRecipeInspector")
                    ?? runtimeFrameWorkspace?.GetChild("behaviorTriggerEventInspector"))?.asCom;
            _behaviorTriggerEventInspector.RefreshRuntimeState(
                recipeInspector,
                timeline?.RecipeInspector);
            if (timeline == null
                || timeline.SelectedElementKind != CharacterTestRuntimeAiLabLogicElementKind.Stage
                || timeline.SelectedElementIndex < 0
                || timeline.SelectedElementIndex >= timeline.StageCount
                || timeline.SelectedElementIndex >= (timeline.Stages?.Length ?? 0))
            {
                return;
            }

            CharacterTestRuntimeAiLabLogicStageViewModel stage =
                timeline.Stages[timeline.SelectedElementIndex];
            if (stage?.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                return;

            Fgui.GComponent details = root
                .GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)
                ?.asCom
                ?.GetChild("timelineEventDetails")
                ?.asCom;
            Fgui.GComponent host = details?.GetChild("behaviorLogicTypedControls")?.asCom;
            if (host == null)
                return;

            bool currentCombatTarget = stage.MovementTargetKind
                == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            int distanceDecimeters = timeline.ObservedTargetDistanceDecimeters;
            bool hasLiveDistance = currentCombatTarget && distanceDecimeters >= 0;
            float currentDistance = hasLiveDistance ? distanceDecimeters * 0.1f : -1f;
            SetMovementFeedbackText(
                host.GetChild("movementFeedbackValue")?.asTextField,
                hasLiveDistance
                    ? currentDistance.ToString("0.0", CultureInfo.InvariantCulture) + " 米"
                    : "等待运行数据");
            Fgui.GTextField status = host.GetChild("movementFeedbackStatus")?.asTextField;
            SetMovementFeedbackText(
                status,
                ResolveBehaviorLogicMovementFeedbackStatus(stage, currentDistance));
            if (status != null)
                status.color = ResolveBehaviorLogicMovementFeedbackColor(stage, currentDistance);
            SetMovementFeedbackText(
                host.GetChild("movementFeedbackMeaning")?.asTextField,
                ResolveBehaviorLogicMovementFeedbackMeaning(stage, currentDistance));

            Fgui.GObject marker = host.GetChild("movementTargetMarker");
            if (marker != null)
            {
                marker.visible = hasLiveDistance;
                if (hasLiveDistance)
                {
                    float editorHeight = stage.MovementTargetKind
                            == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        || stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                            ? 196f
                            : 128f;
                    float feedbackY = 124f + editorHeight + 16f;
                    float feedbackHeight = 604f - feedbackY;
                    float axisY = feedbackY + Math.Min(154f, feedbackHeight - 104f);
                    float targetX = ResolveBehaviorLogicMovementTargetMarkerX(
                        currentDistance,
                        Math.Max(0f, stage.MoveMinDistance),
                        72f,
                        204f,
                        448f);
                    marker.SetXY(targetX - 12f, axisY - 9f);
                }
            }
        }

        private static void SetMovementFeedbackText(Fgui.GTextField field, string value)
        {
            if (field != null && !string.Equals(field.text, value, StringComparison.Ordinal))
                field.text = value ?? string.Empty;
        }
        private void SwitchMode(Fgui.GComponent viewport, TimelineWorkspaceMode nextMode)
        {
            if (!_session.TrySwitchMode(nextMode)) return;
            ClearTimelinePreview(viewport);
            _runtimeFrameTimeline.Hide();
            _liveTimelineItems.Invalidate();
            _behaviorPlanDecisionBinder.HideRows();
            _timelineDetail.Suspend();
            _decisionDetailInteractionsBound = false;
            _decisionDetailItem = null;
        }
        internal void RenderReplayTimelineProjection(Fgui.GComponent timeline, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel) { _replayTimelineItems.Bind(timeline, viewModel, viewModel.RuntimeFrame); }
        internal void HideReplayPooledTimelineItems() { _replayTimelineItems.Invalidate(); }
        internal void EnsureReplayTimelinePoolsPreheated() { _replayTimelineItems.EnsurePreheated(); }
        private void BindBehaviorPlanDecisionSurface(
            Fgui.GComponent timeline,
            Fgui.GComponent viewport,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            ClearTimelinePreview(viewport);
            _liveTimelineItems.Invalidate();
            _timelineScrubActive = false;
            _timelineViewportInputBinder.Reset();

            Fgui.GObject background = timeline.GetChild("timelineOpaqueBackground");
            if (background != null)
                background.visible = false;
            viewport.visible = false;
            for (int i = 0; i < BehaviorPlanHiddenRuntimeControls.Length; i++)
                SetTimelineChildVisible(timeline, BehaviorPlanHiddenRuntimeControls[i], false);

            Fgui.GObject details = timeline.GetChild("timelineEventDetails");
            SetObjectGeometry(
                details,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX,
                0f,
                RuntimeFrameEventDetailWidth,
                BehaviorPlanDecisionHeight);
            if (details != null)
                details.sortingOrder = 20;
            Fgui.GObject triggerInspector = timeline.GetChild("behaviorRecipeInspector")
                ?? timeline.GetChild("behaviorTriggerEventInspector");
            SetObjectGeometry(
                triggerInspector,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY,
                RuntimeFrameEventDetailWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentHeight);
            if (triggerInspector != null)
                triggerInspector.sortingOrder = 21;
            BindDecisionDetails(timeline, viewModel);
        }

        private void ApplyTimelineExpandedVisibility(Fgui.GComponent viewport, bool timelineExpanded)
        {
            if (timelineExpanded)
            {
                SetTimelineChildVisible(viewport, "timelineRuler", true);
                SetTimelineChildVisible(viewport, "timelineScrubHitArea", true);
                SetTimelineChildVisible(viewport, "timelineViewport", true);
                SetTimelineChildVisible(viewport, "timelineHistoryProgress", true);
                return;
            }

            _liveTimelineItems.Invalidate();
            for (int i = 0; i < TimelineTrackNames.Length; i++)
                SetTimelineChildVisible(viewport, TimelineTrackNames[i], false);
            SetTimelineChildVisible(viewport, "timelineRuler", false);
            SetTimelineChildVisible(viewport, "timelineScrubHitArea", false);
            SetTimelineChildVisible(viewport, "timelineViewport", false);
            SetTimelineChildVisible(viewport, "timelineHistoryProgress", false);
            SetTimelineChildVisible(viewport, "timelinePlayhead", false);
            SetTimelineChildVisible(viewport, "timelinePreviewPlayhead", false);
            SetTimelineChildVisible(viewport, "timelinePlayheadHitArea", false);
            SetTimelineChildVisible(viewport, "timelineSelectedFrameBadge", false);
            SetTimelineChildVisible(viewport, "timelineSelectedFrameLabel", false);
            SetTimelineChildVisible(viewport, "timelinePreviewFrameBadge", false);
            SetTimelineChildVisible(viewport, "timelinePreviewFrameLabel", false);
            SetTimelineChildVisible(viewport, "timelineLoopRange", false);
            SetTimelineChildVisible(viewport, "timelineRetainedMask", false);
            SetTimelineChildVisible(viewport, "timelineFutureMask", false);
            SetTimelineChildVisible(viewport, "timelineFutureAdvanceFill", false);
        }

        private static void SetObjectGeometry(
            Fgui.GObject target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null)
                return;
            target.SetSize(width, height);
            target.SetXY(x, y);
        }


        private static Color32 RuntimeFrameTrackAccent(
            CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception:
                    return new Color32(78, 219, 213, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision:
                    return new Color32(76, 190, 232, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action:
                    return new Color32(234, 190, 55, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result:
                    return new Color32(105, 199, 112, 255);
                default:
                    return new Color32(244, 178, 56, 255);
            }
        }

        internal static Color RuntimeFrameTrackAccentColor(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            byte alpha = 255)
        {
            Color32 accent = RuntimeFrameTrackAccent(kind);
            return RuntimeFrameSrgb(accent.r, accent.g, accent.b, alpha);
        }

    }
}
