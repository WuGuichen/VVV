using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleRuntimeTestAndSetupCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            switch (commandId)
            {
                case CharacterTestRuntimeAiLabCardTrialIds.OpenCommandId:
                    LastStatusMessage = Execute(_controller.OpenCardTrial, "已打开独立真实出牌测试。", "真实出牌测试未打开：" + _controller.StatusMessage);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousAgent:
                    LastStatusMessage = Execute(_controller.SelectPreviousAgent, "已选择上一个智能体。", "没有上一个智能体。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextAgent:
                    LastStatusMessage = Execute(_controller.SelectNextAgent, "已选择下一个智能体。", "没有下一个智能体。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RuntimeTestSelectAgent:
                    LastStatusMessage = SelectRuntimeTestAgent(command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate:
                    LastStatusMessage = Execute(
                        _controller.ToggleDebugEditGate,
                        "编辑锁状态已切换。",
                        "切换编辑锁失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay:
                    LastStatusMessage = Execute(
                        _controller.TogglePreviewOverlay,
                        "场景标记状态已切换。",
                        "切换场景标记失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleDetails:
                    if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
                    {
                        NavigateWorkspaceBack();
                        SelectedCompiledStepIndex = 0;
                        LastStatusMessage = "已返回上一页面。";
                        break;
                    }
                    OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.Facts);
                    SelectedCompiledStepIndex = 0;
                    LastStatusMessage = "已打开运行详情。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleStateCapture:
                    LastStatusMessage = Execute(
                        _controller.ToggleStateCapture,
                        "状态采集已切换。",
                        "切换状态采集失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ValidateState:
                    LastStatusMessage = Execute(
                        _controller.ValidateRecordedState,
                        "状态校验通过。",
                        "状态校验失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SyncRecordedState:
                    LastStatusMessage = Execute(
                        _controller.SyncRecordedState,
                        "记录状态已同步。",
                        "同步记录状态失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ColdBootstrapVerify:
                    LastStatusMessage = Execute(
                        _controller.ColdVerifySelectedFrame,
                        "冷启动校验已开始。",
                        "冷启动校验失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSetupSnap:
                    LastStatusMessage = Execute(
                        _controller.ToggleSetupSnap,
                        "位置吸附状态已切换。",
                        "切换位置吸附失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MirrorSpawns:
                    LastStatusMessage = Execute(
                        _controller.MirrorSetupSpawns,
                        "出生位置已镜像。",
                        "镜像出生位置失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ResetSpawnDefaults:
                    LastStatusMessage = Execute(
                        _controller.ResetSetupSpawnDefaults,
                        "默认出生位置已恢复。",
                        "重置出生位置失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView:
                    LastStatusMessage = Execute(
                        _controller.FitSetupView,
                        "测试视野已适配。",
                        "适配测试视野失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSelectedSetupSlot:
                    LastStatusMessage = Execute(
                        _controller.ToggleSelectedSetupSlotEnabled,
                        "测试槽位启用状态已更新。",
                        "更新测试槽位失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupCharacter:
                    LastStatusMessage = Execute(
                        _controller.SelectPreviousSetupCharacter,
                        "已选择上一个测试角色。",
                        "选择角色失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupCharacter:
                    LastStatusMessage = Execute(
                        _controller.SelectNextSetupCharacter,
                        "已选择下一个测试角色。",
                        "选择角色失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupBehaviorGroup:
                    LastStatusMessage = "请在行为组选择弹窗中使用“新建副本”。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupBehaviorGroup:
                    LastStatusMessage = Execute(
                        _controller.SelectNextSetupBehaviorGroup,
                        "已选择下一个行为组。",
                        "选择行为组失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup:
                    WorkbenchScrollOffset = 0;
                    LastStatusMessage = "已打开行为组编辑器。";
                    SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CreateSetupBehaviorGroup:
                    PendingSetupBehaviorGroupDeleteDraftId = -1;
                    LastStatusMessage = _controller.TryCreateBehaviorGroupFromSelected(
                        out string createGroupMessage)
                        ? createGroupMessage
                        : "新增行为组失败：" + createGroupMessage;
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteSetupBehaviorGroup:
                    CharacterTestArenaSlotConfig deleteSlot = _controller.MatchConfig?.TryGetSlot(
                        _controller.SelectedSetupTeamId,
                        _controller.SelectedSetupSlotIndex);
                    int deleteDraftId = deleteSlot?.BehaviorGroupDraftId ?? -1;
                    if (PendingSetupBehaviorGroupDeleteDraftId != deleteDraftId)
                    {
                        PendingSetupBehaviorGroupDeleteDraftId = deleteDraftId;
                        LastStatusMessage = "再次点击确认删除当前行为组：" +
                            ResolveCurrentBehaviorGroupName(deleteDraftId);
                    }
                    else
                    {
                        PendingSetupBehaviorGroupDeleteDraftId = -1;
                        LastStatusMessage = _controller.TryDeleteSelectedBehaviorGroup(
                            out string deleteGroupMessage)
                            ? deleteGroupMessage
                            : "删除行为组失败：" + deleteGroupMessage;
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveSetupBehaviorGroupBasicInfo:
                    CharacterTestRuntimeAiLabSetupBehaviorGroupBasicInfoPayload basicInfo =
                        command.Payload as CharacterTestRuntimeAiLabSetupBehaviorGroupBasicInfoPayload;
                    string basicInfoMessage = "保存参数不可用。";
                    bool basicInfoSaved = basicInfo != null
                        && (basicInfo.IsLocalDraft
                            ? _controller.TryConfirmSetupBehaviorGroupDraft(
                                basicInfo.GroupDraftId,
                                basicInfo.DisplayName,
                                basicInfo.Description,
                                basicInfo.TagsText,
                                out basicInfoMessage)
                            : _controller.TryPublishSetupBehaviorGroupBasicInfo(
                            basicInfo.GroupDraftId,
                            basicInfo.StableGroupId,
                            basicInfo.SourceRevision,
                            basicInfo.SourceContentHash,
                            basicInfo.DisplayName,
                            basicInfo.Description,
                            basicInfo.TagsText,
                            out basicInfoMessage));
                    LastStatusMessage = basicInfoSaved
                        ? basicInfoMessage
                        : "保存行为组基本信息失败：" + basicInfoMessage;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
