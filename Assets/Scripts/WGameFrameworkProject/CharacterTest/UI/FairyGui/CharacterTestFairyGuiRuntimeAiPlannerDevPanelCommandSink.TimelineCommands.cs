using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleTimelineCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousFrame:
                    LastStatusMessage = Execute(_controller.SelectPreviousFrame, "已选择上一帧。", "没有上一帧。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FirstFrame:
                    LastStatusMessage = Execute(_controller.SelectFirstFrame, "已定位首帧。", "没有可用的首帧。");
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextFrame:
                    LastStatusMessage = Execute(_controller.SelectNextFrame, "已选择下一帧。", "没有下一帧。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame:
                    LastStatusMessage = Execute(_controller.SelectLatestFrame, "已选择最新帧。", "没有可用的最新帧。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame:
                    LastStatusMessage = ExecuteJumpFrame(command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed:
                    LastStatusMessage = Execute(
                        _controller.CyclePlaybackSpeed,
                        "播放速度已切换。",
                        "切换播放速度失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.IncreasePlaybackSpeed:
                    LastStatusMessage = Execute(
                        _controller.IncreasePlaybackSpeed,
                        "播放速度已提高。",
                        "提高播放速度失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DecreasePlaybackSpeed:
                    LastStatusMessage = Execute(
                        _controller.DecreasePlaybackSpeed,
                        "播放速度已降低。",
                        "降低播放速度失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart:
                    LastStatusMessage = Execute(_controller.SetLoopStart, "已设置循环起点 A。", "设置循环起点 A 失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd:
                    LastStatusMessage = Execute(_controller.SetLoopEnd, "已设置循环终点 B。", "设置循环终点 B 失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop:
                    LastStatusMessage = Execute(_controller.ToggleLoop, "循环状态已切换。", "切换循环状态失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FrameLimit:
                    LastStatusMessage = Execute(_controller.CycleFrameLimitPreset, "帧上限已切换。", "切换帧上限失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineToggleExpanded:
                    bool timelineExpandedToggled =
                        _controller != null && _controller.ToggleTimelineExpanded();
                    if (timelineExpandedToggled)
                        PersistActiveTimelineExpanded(_controller.TimelineExpanded);
                    LastStatusMessage = timelineExpandedToggled
                        ? "时间轴布局已切换。"
                        : "切换时间轴布局失败。 "
                            + (_controller?.StatusMessage ?? string.Empty);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace:
                    SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan:
                    SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic:
                    OpenBehaviorLogicTab();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.EditSelectedBehaviorData:
                    OpenSelectedBehaviorData();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomOut:
                    LastStatusMessage = Execute(() => _controller.ZoomTimeline(-1), "时间轴已缩小。", "时间轴缩小失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomIn:
                    LastStatusMessage = Execute(() => _controller.ZoomTimeline(1), "时间轴已放大。", "时间轴放大失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineFit:
                    LastStatusMessage = Execute(_controller.FitTimeline, "时间轴已适配。", "时间轴适配失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanLeft:
                    LastStatusMessage = Execute(() => _controller.PanTimeline(-1), "时间轴已向前移动。", "时间轴移动失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanRight:
                    LastStatusMessage = Execute(() => _controller.PanTimeline(1), "时间轴已向后移动。", "时间轴移动失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineSelectItem:
                    bool timelineItemSelected = _controller != null
                        && command.Payload is CharacterTestRuntimeAiLabTimelineItemLocator timelineLocator
                        && _controller.SelectTimelineItem(timelineLocator);
                    LastStatusMessage = timelineItemSelected
                        ? "已选择时间轴事件；可在详情中定位帧或关联对象。"
                        : "选择时间轴事件失败。 " + (_controller?.StatusMessage ?? string.Empty);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineNavigateItem:
                    LastStatusMessage = ExecuteTimelineItemNavigation(command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineClearItemSelection:
                    _controller.ClearTimelineItemSelection();
                    LastStatusMessage = "时间轴事件选择已清除。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubBegin:
                    LastStatusMessage = ExecuteTimelineScrub(command.Payload, TimelineScrubPhase.Begin);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate:
                    LastStatusMessage = ExecuteTimelineScrub(command.Payload, TimelineScrubPhase.Update);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubEnd:
                    LastStatusMessage = ExecuteTimelineScrub(command.Payload, TimelineScrubPhase.End);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubCancel:
                    _controller.CancelTimelineScrub();
                    LastStatusMessage = "时间轴拖动已取消。";
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
