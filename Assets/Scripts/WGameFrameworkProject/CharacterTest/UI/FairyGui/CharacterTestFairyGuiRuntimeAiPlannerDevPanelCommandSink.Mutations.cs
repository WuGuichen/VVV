using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private string Execute(Func<bool> action, string success, string failure)
        {
            if (_controller == null || action == null)
                return "运行时智能规划开发控制器不可用。";

            return action() ? success : failure + " " + _controller.StatusMessage;
        }

        private void SetProjectCardMetadata(
            CharacterTestRuntimeAiLabProjectCardMetadataField field,
            object payload)
        {
            CharacterTestRuntimeAiLabProjectCardMetadataEditPayload edit =
                payload as CharacterTestRuntimeAiLabProjectCardMetadataEditPayload;
            int entryIndex = edit?.EntryIndex ?? SelectedWorkbenchEntryIndex;
            string value = edit?.Value ?? payload as string ?? string.Empty;
            string message = string.Empty;
            bool standalone = edit != null
                && edit.StableCardId.Length > 0
                && entryIndex < 0;
            bool changed = _controller != null && (standalone
                ? field == CharacterTestRuntimeAiLabProjectCardMetadataField.DisplayName
                    && _controller.TrySetStandaloneProjectCardDisplayName(
                        edit.StableCardId,
                        value,
                        out message)
                : _controller.TrySetCurrentProjectCardMetadata(
                    entryIndex,
                    field,
                    value,
                    out message));
            LastStatusMessage = changed
                ? message
                : "项目行为卡元数据更新失败：" + (message ?? string.Empty);
        }

        private delegate bool EntryMutation(int entryIndex, out string message);

        private string MutateSelectedEntry(EntryMutation mutation)
        {
            return MutateEntry(SelectedWorkbenchEntryIndex, mutation);
        }

        private string MutateEntry(int entryIndex, EntryMutation mutation)
        {
            TryMutateEntry(entryIndex, mutation, out string status);
            return status;
        }

        private bool TryMutateEntry(
            int entryIndex,
            EntryMutation mutation,
            out string status)
        {
            if (_controller == null || mutation == null)
            {
                status = "运行时智能实验室工作台不可用。";
                return false;
            }
            if (!CanMutateWorkbenchDraft())
            {
                status = "版本应用完成前，历史帧和运行中的行为草稿均为只读。";
                return false;
            }

            if (!mutation(entryIndex, out string message))
            {
                status = "更新失败。" + message;
                return false;
            }
            status = string.IsNullOrEmpty(message)
                ? "行为草稿已更新。"
                : "行为草稿已更新。" + message;
            return true;
        }

        private bool CanMutateWorkbenchDraft()
        {
            if (_controller == null)
                return false;

            // Behavior Logic is the runtime-facing editor for the same draft workspace.
            // It must use the controller's candidate-edit gate just like the dedicated
            // BehaviorCard workspace. Treating Diagnostics as an ordinary runtime page
            // made the visible workbench actions reject every mutation while paused.
            return IsBehaviorTestAuthoringContext()
                ? _controller.CanEditBehaviorCardDraft
                : _controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
        }

        private int ResolveEntryMutationIndex(object payload)
        {
            return payload is int entryIndex && entryIndex >= 0
                ? entryIndex
                : SelectedWorkbenchEntryIndex;
        }

        private void DeleteEntry(int entryIndex)
        {
            bool behaviorCardWasOpen = IsBehaviorTestAuthoringContext();
            bool canEdit = CanMutateWorkbenchDraft();
            if (!TryMutateEntry(
                    entryIndex,
                    _controller.TryRemoveDraftEntry,
                    out string status))
            {
                LastStatusMessage = status;
                LogWorkbenchMutation(
                    "delete-rejected",
                    entryIndex,
                    "canEdit=" + canEdit + "; " + status);
                ShowToast(status, isWarning: true);
                return;
            }
            LastStatusMessage = status;
            LogWorkbenchMutation(
                "delete-succeeded",
                entryIndex,
                "canEdit=" + canEdit + "; " + status);
            ShowToast(status, isWarning: false);

            SelectedWorkbenchEntryIndex = ResolveSelectionAfterEntryDeletion(entryIndex);
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
            if (!behaviorCardWasOpen)
                return;

            if (_controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                // Runtime Behavior Logic is hosted by RuntimeFrame rather than the standalone
                // BehaviorCard workspace. Keep that root mounted and replace only its stable card
                // context after deleting the selected entry.
                PublishBehaviorCardContextIfNeeded();
                return;
            }

            SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup);
            if (!IsLockedFreeStrategyEntry(SelectedWorkbenchEntryIndex))
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
        }

        private void DeleteProjectCard(string stableCardId)
        {
            string message = "Runtime AI Lab 项目行为卡编辑器不可用。";
            int deletedEntryIndex = -1;
            if (_controller == null
                || !_controller.TryDeleteProjectCardFromCurrentBehaviorGroup(
                    stableCardId,
                    out deletedEntryIndex,
                    out message))
            {
                LastStatusMessage = message ?? "删除项目行为卡失败。";
                LogWorkbenchMutation(
                    "delete-project-card-rejected",
                    deletedEntryIndex,
                    LastStatusMessage);
                ShowToast(LastStatusMessage, isWarning: true);
                return;
            }

            LastStatusMessage = message;
            LogWorkbenchMutation("delete-project-card-succeeded", deletedEntryIndex, message);
            ShowToast(message, isWarning: false);
            SelectedWorkbenchEntryIndex = ResolveSelectionAfterEntryDeletion(deletedEntryIndex);
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
            PublishBehaviorCardContextIfNeeded();
        }

        private void LogWorkbenchMutation(string eventName, int entryIndex, string detail)
        {
            _workbenchLogSink?.Invoke(
                "event=" + eventName
                + " team=" + (_controller?.SelectedSetupTeamId ?? 0)
                + " slot=" + (_controller?.SelectedSetupSlotIndex ?? -1)
                + " entry=" + entryIndex
                + " workspace=" + WorkspaceMode
                + " timeline=" + TimelineMode
                + " phase=" + (_controller?.Phase.ToString() ?? "Unavailable")
                + " detail=" + (detail ?? string.Empty));
        }

        private int ResolveSelectionAfterEntryDeletion(int deletedEntryIndex)
        {
            if (_controller == null
                || !_controller.DraftWorkspace.TryGetGroupForSlot(
                    _controller.SelectedSetupTeamId,
                    _controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || group.Entries.Count == 0)
            {
                return 0;
            }

            int candidate = Math.Min(Math.Max(0, deletedEntryIndex), group.Entries.Count - 1);
            if (!IsLockedFreeStrategyEntry(candidate))
                return candidate;
            for (int distance = 1; distance < group.Entries.Count; distance++)
            {
                int before = candidate - distance;
                if (before >= 0 && !IsLockedFreeStrategyEntry(before))
                    return before;
                int after = candidate + distance;
                if (after < group.Entries.Count && !IsLockedFreeStrategyEntry(after))
                    return after;
            }
            return candidate;
        }


    }
}
