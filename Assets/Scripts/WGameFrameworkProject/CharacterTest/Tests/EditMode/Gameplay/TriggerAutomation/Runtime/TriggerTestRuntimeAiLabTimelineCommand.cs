using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using UnityEngine;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal enum TriggerTestRuntimeAiLabTimelineCommandKind
    {
        SelectHistoricalFrame = 0,
        SelectLatestFrame = 1,
        RequestBranchRecompute = 2,
        ConfirmBranchRecompute = 3,
        CancelBranchRecompute = 4,
        ZoomIn = 5,
        ZoomOut = 6,
        FitTimeline = 7,
        SelectEvent = 8,
        CloseEventDetails = 9,
        LocateEventStart = 10,
        LocateEventEnd = 11
    }

    internal sealed class TriggerTestRuntimeAiLabTimelineArguments :
        ITriggerTestFunctionArguments
    {
        internal TriggerTestRuntimeAiLabTimelineArguments(
            TriggerTestRuntimeAiLabTimelineCommandKind commandKind,
            int targetFrame,
            int targetEventIndex)
        {
            CommandKind = commandKind;
            TargetFrame = targetFrame;
            TargetEventIndex = targetEventIndex;
        }

        internal TriggerTestRuntimeAiLabTimelineCommandKind CommandKind
        {
            get;
        }

        internal int TargetFrame { get; }
        internal int TargetEventIndex { get; }
    }

    /// <summary>
    /// Executes one explicit Runtime AI Lab timeline operation.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only adapter runs
    /// only after an explicit manual Trigger request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabTimelineFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabTimelineArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-timeline");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabTimelineArguments arguments)
        {
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot before =
                controller.CaptureSnapshot();
            if (!TryExecute(
                    controller,
                    before,
                    arguments.CommandKind,
                    arguments.TargetFrame,
                    arguments.TargetEventIndex,
                    out string message))
            {
                return TriggerTestOperationResult.Failed(message);
            }

            Debug.Log(
                "[TriggerTest][RuntimeAiLabTimeline][" +
                arguments.CommandKind +
                "][PASS] " +
                message);
            return TriggerTestOperationResult.Succeeded(message);
        }

        private static bool TryExecute(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            TriggerTestRuntimeAiLabTimelineCommandKind commandKind,
            int targetFrame,
            int targetEventIndex,
            out string message)
        {
            switch (commandKind)
            {
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .SelectHistoricalFrame:
                    if (!TryRequirePausedHistory(
                            snapshot,
                            requireHistoricalSelection: false,
                            out message))
                    {
                        return false;
                    }

                    if (targetFrame < 0 ||
                        targetFrame >= snapshot.Timeline.LatestFrame ||
                        !snapshot.Timeline.TryGetFrame(targetFrame, out _))
                    {
                        message =
                            "指定帧不在当前可定位的历史窗口内：" +
                            targetFrame +
                            "。";
                        return false;
                    }
                    if (!controller.JumpToFrame(targetFrame))
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = "已请求定位到指定历史帧 " +
                              targetFrame +
                              "。";
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .SelectLatestFrame:
                    if (snapshot.Timeline == null ||
                        snapshot.Timeline.Count == 0)
                    {
                        message = "时间轴尚未捕获任何帧。";
                        return false;
                    }
                    if (!controller.SelectLatestFrame())
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = "已请求定位到最新帧 " +
                              snapshot.Timeline.LatestFrame +
                              "。";
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .RequestBranchRecompute:
                    if (!TryRequirePausedHistory(
                            snapshot,
                            requireHistoricalSelection: true,
                            out message))
                    {
                        return false;
                    }
                    if (snapshot.BranchConfirmationPending ||
                        snapshot.BranchRecomputeArmed)
                    {
                        message = "当前已经存在重算确认或已确认的重算。";
                        return false;
                    }
                    if (!controller.ToggleBranchRecompute())
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = controller.StatusMessage;
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .ConfirmBranchRecompute:
                    if (!snapshot.BranchConfirmationPending)
                    {
                        message = "当前没有等待确认的新未来请求。";
                        return false;
                    }
                    if (!controller.ToggleBranchRecompute())
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = controller.StatusMessage;
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .CancelBranchRecompute:
                    if (snapshot.BranchConfirmationPending)
                    {
                        if (!controller.CancelBranchRecomputeConfirmation())
                        {
                            message = controller.StatusMessage;
                            return false;
                        }
                    }
                    else if (snapshot.BranchRecomputeArmed)
                    {
                        if (!controller.ToggleBranchRecompute())
                        {
                            message = controller.StatusMessage;
                            return false;
                        }
                    }
                    else
                    {
                        message = "当前没有可取消的重算请求。";
                        return false;
                    }

                    message = controller.StatusMessage;
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind.ZoomIn:
                    return TryZoomTimeline(controller, 1, out message);
                case TriggerTestRuntimeAiLabTimelineCommandKind.ZoomOut:
                    return TryZoomTimeline(controller, -1, out message);
                case TriggerTestRuntimeAiLabTimelineCommandKind.FitTimeline:
                    if (!controller.FitTimeline())
                    {
                        message = controller.StatusMessage;
                        return false;
                    }
                    message = controller.StatusMessage;
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind.SelectEvent:
                    if (!TryGetTimelineEvent(
                            controller,
                            targetEventIndex,
                            out CharacterTestRuntimeAiLabTimelineItem eventItem,
                            out message))
                    {
                        return false;
                    }
                    if (!controller.SelectTimelineItem(
                            eventItem.CreateLocator()))
                    {
                        message = controller.StatusMessage;
                        return false;
                    }
                    message = "已选择事件序号 " +
                              targetEventIndex +
                              "：" +
                              eventItem.Label +
                              "。";
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .CloseEventDetails:
                    if (!controller.ClearTimelineItemSelection())
                    {
                        message = "当前没有打开的事件详情。";
                        return false;
                    }
                    message = "已关闭事件详情。";
                    return true;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .LocateEventStart:
                    return TryNavigateSelectedEvent(
                        controller,
                        useEndFrame: false,
                        out message);
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .LocateEventEnd:
                    return TryNavigateSelectedEvent(
                        controller,
                        useEndFrame: true,
                        out message);
                default:
                    message = "未知的 Runtime AI Lab 时间轴命令。";
                    return false;
            }
        }

        private static bool TryZoomTimeline(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int direction,
            out string message)
        {
            if (!controller.ZoomTimeline(direction))
            {
                message = controller.StatusMessage;
                return false;
            }
            message = controller.StatusMessage;
            return true;
        }

        private static bool TryNavigateSelectedEvent(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            bool useEndFrame,
            out string message)
        {
            if (!controller.TryGetSelectedTimelineItem(
                    out CharacterTestRuntimeAiLabTimelineItem item))
            {
                message = "请先选择一个时间轴事件。";
                return false;
            }
            if (useEndFrame && item.EndFrame <= item.StartFrame)
            {
                message = "所选事件没有独立的结束帧。";
                return false;
            }
            if (!controller.NavigateTimelineItem(
                    item.CreateLocator(),
                    useEndFrame))
            {
                message = controller.StatusMessage;
                return false;
            }
            message = "已请求定位事件" +
                      (useEndFrame ? "终点 " : "起点 ") +
                      (useEndFrame ? item.EndFrame : item.StartFrame) +
                      "。";
            return true;
        }

        internal static bool TryGetTimelineEvent(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int targetEventIndex,
            out CharacterTestRuntimeAiLabTimelineItem eventItem,
            out string diagnostic)
        {
            eventItem = null;
            if (targetEventIndex < 0)
            {
                diagnostic = "事件序号不能小于 0。";
                return false;
            }

            CharacterTestRuntimeAiLabTimelineProjection projection =
                controller.BuildTimelineProjection();
            int currentIndex = 0;
            for (int trackIndex = 0;
                 trackIndex < projection.Tracks.Count;
                 trackIndex++)
            {
                System.Collections.Generic.IReadOnlyList<
                    CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0;
                     itemIndex < items.Count;
                     itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item =
                        items[itemIndex];
                    if (item.ItemKind ==
                        CharacterTestRuntimeAiLabTimelineItemKind
                            .ZoomDensityAggregate)
                    {
                        continue;
                    }
                    if (item.ItemKind ==
                        CharacterTestRuntimeAiLabTimelineItemKind
                            .ExactFrameStack)
                    {
                        for (int memberIndex = 0;
                             memberIndex < item.Members.Count;
                             memberIndex++)
                        {
                            if (currentIndex++ != targetEventIndex)
                                continue;
                            eventItem = item.Members[memberIndex];
                            diagnostic = string.Empty;
                            return true;
                        }
                        continue;
                    }
                    if (currentIndex++ != targetEventIndex)
                        continue;
                    eventItem = item;
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "事件序号超出当前可选择范围：" +
                         targetEventIndex +
                         "。";
            return false;
        }

        private static bool TryRequirePausedHistory(
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            bool requireHistoricalSelection,
            out string diagnostic)
        {
            if (snapshot.Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                diagnostic = "该时间轴操作要求正在进行的测试会话。";
                return false;
            }
            if (!snapshot.Paused)
            {
                diagnostic = "该时间轴操作要求先暂停模拟。";
                return false;
            }
            if (snapshot.FrameSelectionPhase !=
                CharacterTestRuntimeAiLabFrameSelectionPhase.Idle)
            {
                diagnostic = "请等待当前帧定位完成。";
                return false;
            }
            if (snapshot.Timeline == null ||
                snapshot.Timeline.Count < 2)
            {
                diagnostic = "时间轴至少需要两个已捕获帧。";
                return false;
            }
            if (requireHistoricalSelection &&
                (!snapshot.Timeline.HasSelection ||
                 snapshot.Timeline.SelectedFrame >=
                 snapshot.Timeline.LatestFrame))
            {
                diagnostic = "请先定位到一个历史帧。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class TriggerTestRuntimeAiLabTimelineAssertion :
        ITriggerTestAssertion
    {
        private readonly TriggerTestRuntimeAiLabTimelineCommandKind
            _commandKind;
        private readonly int _targetFrame;
        private readonly int _targetEventIndex;

        internal TriggerTestRuntimeAiLabTimelineAssertion(
            TriggerTestRuntimeAiLabTimelineCommandKind commandKind,
            int targetFrame,
            int targetEventIndex)
        {
            _commandKind = commandKind;
            _targetFrame = targetFrame;
            _targetEventIndex = targetEventIndex;
        }

        public string AssertionId =>
            "runtime-ai-lab-timeline-" +
            _commandKind.ToString().ToLowerInvariant();

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                controller.CaptureSnapshot();
            bool succeeded;
            switch (_commandKind)
            {
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .SelectHistoricalFrame:
                    succeeded = IsSelectedOrRestoring(
                        snapshot,
                        _targetFrame);
                    diagnostic = succeeded
                        ? "已验证指定历史帧定位结果或确定性恢复目标。"
                        : "历史帧定位没有选中或提交指定目标。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .SelectLatestFrame:
                    succeeded = IsSelectedOrRestoring(
                        snapshot,
                        snapshot.Timeline.LatestFrame);
                    diagnostic = succeeded
                        ? "已验证最新帧定位结果或确定性恢复目标。"
                        : "最新帧定位没有选中或提交正确目标。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .RequestBranchRecompute:
                    succeeded =
                        snapshot.BranchConfirmationPending &&
                        !snapshot.BranchRecomputeArmed &&
                        snapshot.BranchRecomputeBaseFrame ==
                        snapshot.Timeline.SelectedFrame;
                    diagnostic = succeeded
                        ? "已验证新未来请求进入确认阶段。"
                        : "新未来请求没有进入确认阶段。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .ConfirmBranchRecompute:
                    succeeded =
                        !snapshot.BranchConfirmationPending &&
                        snapshot.BranchRecomputeArmed &&
                        snapshot.BranchRecomputeBaseFrame ==
                        snapshot.Timeline.SelectedFrame;
                    diagnostic = succeeded
                        ? "已验证重算已确认，等待推进首帧原子提交。"
                        : "重算没有进入已确认状态。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .CancelBranchRecompute:
                    succeeded =
                        !snapshot.BranchConfirmationPending &&
                        !snapshot.BranchRecomputeArmed;
                    diagnostic = succeeded
                        ? "已验证重算确认和已确认状态均已清除。"
                        : "重算状态没有完全取消。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind.ZoomIn:
                case TriggerTestRuntimeAiLabTimelineCommandKind.ZoomOut:
                    CharacterTestRuntimeAiLabTimelineProjection projection =
                        controller.BuildTimelineProjection();
                    succeeded =
                        controller.TimelineZoomLevel >= 0 &&
                        projection.Viewport.ZoomLevel ==
                        controller.TimelineZoomLevel;
                    diagnostic = succeeded
                        ? "已验证时间轴缩放级别与实际视窗一致。"
                        : "时间轴缩放级别没有应用到实际视窗。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind.FitTimeline:
                    succeeded =
                        controller.TimelineZoomLevel < 0 &&
                        controller.BuildTimelineProjection()
                            .Viewport.ZoomLevel < 0;
                    diagnostic = succeeded
                        ? "已验证时间轴恢复为完整实验适配视窗。"
                        : "时间轴仍保留手动缩放级别。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind.SelectEvent:
                    succeeded =
                        TriggerTestRuntimeAiLabTimelineFunction
                            .TryGetTimelineEvent(
                                controller,
                                _targetEventIndex,
                                out CharacterTestRuntimeAiLabTimelineItem
                                    expectedItem,
                                out _) &&
                        controller.TryGetSelectedTimelineItem(
                            out CharacterTestRuntimeAiLabTimelineItem
                                selectedItem) &&
                        string.Equals(
                            selectedItem.StableKey,
                            expectedItem.StableKey,
                            System.StringComparison.Ordinal);
                    diagnostic = succeeded
                        ? "已验证指定序号事件被选中并打开详情。"
                        : "选中的事件与指定序号不一致。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .CloseEventDetails:
                    succeeded =
                        !controller.TryGetSelectedTimelineItem(out _);
                    diagnostic = succeeded
                        ? "已验证事件详情选择已清除。"
                        : "事件详情仍保持打开。";
                    break;
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .LocateEventStart:
                case TriggerTestRuntimeAiLabTimelineCommandKind
                    .LocateEventEnd:
                    bool useEndFrame =
                        _commandKind ==
                        TriggerTestRuntimeAiLabTimelineCommandKind
                            .LocateEventEnd;
                    succeeded =
                        controller.TryGetSelectedTimelineItem(
                            out CharacterTestRuntimeAiLabTimelineItem
                                navigationItem) &&
                        IsSelectedOrRestoring(
                            snapshot,
                            useEndFrame
                                ? navigationItem.EndFrame
                                : navigationItem.StartFrame);
                    diagnostic = succeeded
                        ? "已验证事件端点成为确定性帧定位目标。"
                        : "事件端点没有成为当前帧定位目标。";
                    break;
                default:
                    succeeded = false;
                    diagnostic =
                        "未知的 Runtime AI Lab 时间轴命令。";
                    break;
            }

            return succeeded
                ? TriggerTestOperationResult.Succeeded(diagnostic)
                : TriggerTestOperationResult.Failed(diagnostic);
        }

        private static bool IsSelectedOrRestoring(
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            int targetFrame)
        {
            if (snapshot.Timeline.HasSelection &&
                snapshot.Timeline.SelectedFrame == targetFrame &&
                snapshot.FrameSelectionPhase ==
                CharacterTestRuntimeAiLabFrameSelectionPhase.Idle)
            {
                return true;
            }

            return snapshot.FrameSelectionPhase !=
                       CharacterTestRuntimeAiLabFrameSelectionPhase.Idle &&
                   snapshot.ActiveRestoreTarget.Frame == targetFrame;
        }
    }

    internal abstract class TriggerTestRuntimeAiLabTimelineScenarioBase :
        ITriggerTestScenario
    {
        private const int FunctionCallId = 1;
        private readonly TriggerTestDescriptor _descriptor;
        private readonly TriggerTestRuntimeAiLabTimelineCommandKind
            _commandKind;

        protected TriggerTestRuntimeAiLabTimelineScenarioBase(
            string scenarioId,
            string displayName,
            string operationTag,
            TriggerTestRuntimeAiLabTimelineCommandKind commandKind)
        {
            _commandKind = commandKind;
            _descriptor = new TriggerTestDescriptor(
                scenarioId,
                displayName,
                "character-test",
                TriggerTestExecutionProfile.PlayMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "playmode",
                    "manual",
                    "timeline",
                    operationTag
                });
        }

        public TriggerTestDescriptor Descriptor => _descriptor;

        protected virtual int GetTargetFrame() => -1;
        protected virtual int GetTargetEventIndex() => -1;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabTimelineFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabTimelineFunction.Id,
                new TriggerTestRuntimeAiLabTimelineArguments(
                    _commandKind,
                    GetTargetFrame(),
                    GetTargetEventIndex()));
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-timeline-" +
                    _commandKind.ToString().ToLowerInvariant(),
                    new GameplayTriggerSourceBindingId(
                        TriggerTestSignalFunctionFixtureFactory.SourceBindingId),
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(1) }));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "activation-count",
                    TriggerTestRuntimeCountKind.Activation,
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "intent-count",
                    TriggerTestRuntimeCountKind.Intent,
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeAiLabTimelineAssertion(
                    _commandKind,
                    GetTargetFrame(),
                    GetTargetEventIndex()));
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }

    internal interface ITriggerTestRuntimeAiLabHistoricalFrameScenario
    {
        int TargetFrame { get; set; }
    }

    internal interface ITriggerTestRuntimeAiLabTimelineEventScenario
    {
        int TargetEventIndex { get; set; }
    }
}
