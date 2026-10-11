using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleNavigationReplayAndFactsCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    if (!TryPrepareWorkspaceBackNavigation())
                        break;
                    bool replayContextRestored = NavigateWorkspaceBack();
                    SelectedCompiledStepIndex = 0;
                    if (!replayContextRestored)
                        LastStatusMessage = "工作区页面已切换。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    _controller?.ClearActiveReplayFactsCausalSnapshot();
                    OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.Facts);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    if (_controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                    {
                        OpenBehaviorLogicTab();
                    }
                    else
                    {
                        SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
                        LastStatusMessage = "已返回行为卡编辑器。";
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    if (_controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                        SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
                    else
                        OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.Replay);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze:
                {
                    string freezeDiagnostic = "Replay capture is unavailable.";
                    _controller?.FreezeReplayValidationCapture(out freezeDiagnostic);
                    LastStatusMessage = freezeDiagnostic;
                    break;
                }
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate:
                {
                    string validationDiagnostic = "Replay validation could not start.";
                    _controller?.StartReplayValidation(out validationDiagnostic);
                    LastStatusMessage = validationDiagnostic;
                    break;
                }
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayCancel:
                {
                    string cancelDiagnostic = "Replay validation is not running.";
                    _controller?.CancelReplayValidation(out cancelDiagnostic);
                    LastStatusMessage = cancelDiagnostic;
                    break;
                }
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayRetry:
                {
                    string retryDiagnostic = "Replay validation retry failed.";
                    _controller?.RetryReplayValidation(out retryDiagnostic);
                    LastStatusMessage = retryDiagnostic;
                    break;
                }
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaySelectItem:
                    LastStatusMessage = _controller != null
                        && command.Payload is CharacterTestRuntimeAiLabTimelineItemLocator replayLocator
                        && _controller.SelectReplayTimelineItem(replayLocator)
                            ? "Replay evidence selected."
                            : "Replay evidence selection is stale.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayClearItemSelection:
                    _controller?.ClearReplayTimelineItemSelection();
                    LastStatusMessage = "Replay evidence selection cleared.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayNavigateItem:
                    LastStatusMessage = NavigateReplayTimelineItem(command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayLocateDivergence:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    LastStatusMessage = LocateReplayDivergence();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    LastStatusMessage = OpenReplayFactsEvidence();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenBehaviorCard:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    LastStatusMessage = OpenReplayBehaviorEvidence();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayReturn:
                    if (RejectReplayNavigationWhileBusy())
                        break;
                    if (!NavigateWorkspaceBack())
                        LastStatusMessage = "Replay source workspace restored.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomOut:
                    LastStatusMessage = Execute(
                        () => _controller.ZoomReplayTimeline(-1),
                        "Replay timeline zoomed out.",
                        "Replay timeline cannot zoom out.");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomIn:
                    LastStatusMessage = Execute(
                        () => _controller.ZoomReplayTimeline(1),
                        "Replay timeline zoomed in.",
                        "Replay timeline cannot zoom in.");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFit:
                    LastStatusMessage = Execute(
                        _controller.FitReplayTimeline,
                        "Replay timeline fitted to the frozen capture.",
                        "Replay timeline cannot be fitted.");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanLeft:
                    LastStatusMessage = Execute(
                        () => _controller.PanReplayTimeline(-1),
                        "Replay timeline moved earlier.",
                        "Replay timeline cannot move earlier.");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanRight:
                    LastStatusMessage = Execute(
                        () => _controller.PanReplayTimeline(1),
                        "Replay timeline moved later.",
                        "Replay timeline cannot move later.");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode:
                    LastStatusMessage = LocateFactsBehaviorNode();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateRuntimeFrame:
                    _controller?.CaptureFactsNavigationReturnToken();
                    CharacterTestRuntimeAiLabTimelineItemLocator factsLocator = default;
                    bool exactFactsTimelineMatch = _controller != null
                        && _controller.TryResolveFactsTimelineLocator(out factsLocator);
                    OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
                    exactFactsTimelineMatch = exactFactsTimelineMatch
                        && _controller.SelectTimelineItem(factsLocator);
                    if (exactFactsTimelineMatch)
                        PublishRuntimeFrameContextIfNeeded();
                    LastStatusMessage = exactFactsTimelineMatch
                        ? "已在 RuntimeFrame 时间轴中定位精确 typed 事件。"
                        : "已打开当前 RuntimeFrame；所选 Facts 项没有对应 typed 事件。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .BehaviorTransitionLocateRuntimeFrame:
                    LastStatusMessage = LocateBehaviorTransitionRuntimeFrame(command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsReturnSourceFrame:
                    if (_controller != null
                        && !_controller.RestoreFactsNavigationReturnToken(out string factsReturnDiagnostic))
                    {
                        LastStatusMessage = factsReturnDiagnostic;
                    }
                    else
                    {
                        LastStatusMessage = "正在恢复 Facts 来源帧。";
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSearch:
                    _controller?.SetFactsSearchText(command.Payload as string);
                    LastStatusMessage = "Facts search updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleChanged:
                    _controller?.ToggleFactsChangedOnly();
                    LastStatusMessage = "Facts change filter updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleParticipated:
                    _controller?.ToggleFactsParticipatedOnly();
                    LastStatusMessage = "Facts participation filter updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleDelta:
                    _controller?.ToggleFactsDeltaExpanded();
                    LastStatusMessage = "Facts delta panel updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowFacts:
                    _controller?.SetFactsActiveTableTab(CharacterTestRuntimeAiLabFactsTableTab.Facts);
                    LastStatusMessage = "Facts table selected.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowCandidates:
                    _controller?.SetFactsActiveTableTab(CharacterTestRuntimeAiLabFactsTableTab.Candidates);
                    LastStatusMessage = "Candidate table selected.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelect:
                    _controller?.SelectFactsStableId(command.Payload as string);
                    LastStatusMessage = "Causal selection updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsTogglePin:
                    _controller?.ToggleFactsPin(command.Payload as string);
                    LastStatusMessage = "Pinned fact set updated.";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsScroll:
                    if (_controller != null
                        && TryParseFactsScrollPayload(
                            command.Payload as string,
                            out float factOffset,
                            out float candidateOffset,
                            out float deltaOffset))
                    {
                        _controller.SetFactsScrollOffsets(factOffset, candidateOffset, deltaOffset);
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelectAgent:
                    LastStatusMessage = SelectFactsAgent(command.Payload as string);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
