using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabNavigationExitIntent
    {
        WorkspaceBack = 0,
        TerminalClose = 1,
        CharacterActionReturn = 2
    }

    internal enum CharacterTestRuntimeAiLabNavigationExitDisposition
    {
        Proceed = 0,
        Busy = 1,
        SaveFailed = 2,
        CharacterActionChoiceRequired = 3,
        Cancelled = 4
    }

    internal enum CharacterTestRuntimeAiLabNavigationFeedbackAction
    {
        RetrySave = 0,
        DiscardPendingChanges = 1,
        SaveCharacterActionAndReturn = 2,
        DiscardCharacterActionAndReturn = 3,
        Cancel = 4,
        ViewDetails = 5
    }

    /// <summary>
    /// User-facing feedback stays deliberately short. Paths, stable identities, hashes and
    /// exception text belong to <see cref="TechnicalDetails"/> only.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiLabNavigationFeedback
    {
        internal static readonly CharacterTestRuntimeAiLabNavigationFeedback None =
            new CharacterTestRuntimeAiLabNavigationFeedback(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<CharacterTestRuntimeAiLabNavigationFeedbackAction>());

        internal CharacterTestRuntimeAiLabNavigationFeedback(
            string code,
            string title,
            string message,
            string impact,
            CharacterTestRuntimeAiLabNavigationFeedbackAction[] actions,
            string technicalDetails = "")
        {
            Code = code ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Impact = impact ?? string.Empty;
            Actions = actions ?? Array.Empty<CharacterTestRuntimeAiLabNavigationFeedbackAction>();
            TechnicalDetails = technicalDetails ?? string.Empty;
        }

        internal string Code { get; }
        internal string Title { get; }
        internal string Message { get; }
        internal string Impact { get; }
        internal CharacterTestRuntimeAiLabNavigationFeedbackAction[] Actions { get; }
        internal string TechnicalDetails { get; }
        internal bool IsVisible => Title.Length > 0 || Message.Length > 0;

        internal string BuildPrimaryText()
        {
            if (Title.Length == 0)
                return Message;
            if (Message.Length == 0)
                return Title;
            return Title + "\n" + Message + (Impact.Length > 0 ? "\n" + Impact : string.Empty);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabNavigationExitState
    {
        internal CharacterTestRuntimeAiLabNavigationExitState(
            bool historicalReadOnly,
            bool operationBusy,
            string busyStage,
            bool draftDirty,
            bool canWriteDraft,
            bool characterActionOpen,
            bool characterActionDirty)
        {
            HistoricalReadOnly = historicalReadOnly;
            OperationBusy = operationBusy;
            BusyStage = busyStage ?? string.Empty;
            DraftDirty = draftDirty;
            CanWriteDraft = canWriteDraft;
            CharacterActionOpen = characterActionOpen;
            CharacterActionDirty = characterActionDirty;
        }

        internal bool HistoricalReadOnly { get; }
        internal bool OperationBusy { get; }
        internal string BusyStage { get; }
        internal bool DraftDirty { get; }
        internal bool CanWriteDraft { get; }
        internal bool CharacterActionOpen { get; }
        internal bool CharacterActionDirty { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabNavigationFlushResult
    {
        internal CharacterTestRuntimeAiLabNavigationFlushResult(bool success, string diagnostic)
        {
            Success = success;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal bool Success { get; }
        internal string Diagnostic { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabNavigationExitResult
    {
        internal CharacterTestRuntimeAiLabNavigationExitResult(
            CharacterTestRuntimeAiLabNavigationExitDisposition disposition,
            bool flushAttempted,
            CharacterTestRuntimeAiLabNavigationFeedback feedback)
        {
            Disposition = disposition;
            FlushAttempted = flushAttempted;
            Feedback = feedback ?? CharacterTestRuntimeAiLabNavigationFeedback.None;
        }

        internal CharacterTestRuntimeAiLabNavigationExitDisposition Disposition { get; }
        internal bool FlushAttempted { get; }
        internal CharacterTestRuntimeAiLabNavigationFeedback Feedback { get; }
        internal bool CanProceed => Disposition == CharacterTestRuntimeAiLabNavigationExitDisposition.Proceed;
    }

    /// <summary>
    /// Low-frequency navigation policy. It uses the persistence Dirty state, never an
    /// "opened revision" comparison, and invokes the flush callback at most once per request.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabNavigationExitPolicy
    {
        internal static CharacterTestRuntimeAiLabNavigationExitResult Evaluate(
            CharacterTestRuntimeAiLabNavigationExitIntent intent,
            in CharacterTestRuntimeAiLabNavigationExitState state,
            Func<CharacterTestRuntimeAiLabNavigationFlushResult> flush)
        {
            if (state.OperationBusy)
            {
                string stage = state.BusyStage.Length > 0 ? state.BusyStage : "当前操作";
                return Result(
                    CharacterTestRuntimeAiLabNavigationExitDisposition.Busy,
                    false,
                    "NAVIGATION_OPERATION_BUSY",
                    "暂时不能离开",
                    stage + "正在进行，请等待完成。",
                    "草稿、游戏版本和当前测试都没有改变。",
                    new[]
                    {
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails,
                    },
                    "busyStage=" + stage + "; intent=" + intent);
            }

            if (intent == CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn
                && state.CharacterActionOpen
                && state.CharacterActionDirty)
            {
                CharacterTestRuntimeAiLabNavigationFeedbackAction[] actions = state.CanWriteDraft
                    ? new[]
                    {
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.SaveCharacterActionAndReturn,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardCharacterActionAndReturn,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                    }
                    : new[]
                    {
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails,
                    };
                return Result(
                    CharacterTestRuntimeAiLabNavigationExitDisposition.CharacterActionChoiceRequired,
                    false,
                    "CHARACTER_ACTION_UNSAVED",
                    "当前动作有未保存修改",
                    "请选择保存、放弃或继续编辑。",
                    "行为卡草稿、游戏版本和当前测试尚未改变。",
                    actions,
                    "intent=" + intent + "; readOnly=" + (!state.CanWriteDraft));
            }

            // A historical frame is read-only. Leaving it does not discard an editor mutation and
            // therefore must not manufacture a save prompt.
            if (state.HistoricalReadOnly || !state.DraftDirty)
                return Proceed(flushAttempted: false);

            if (!state.CanWriteDraft)
            {
                return Result(
                    CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed,
                    false,
                    "NAVIGATION_READ_ONLY_DIRTY",
                    "无法离开只读内容",
                    "当前内存状态无法写回。",
                    "草稿、游戏版本和当前测试都没有改变。",
                    new[]
                    {
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails,
                    },
                    "intent=" + intent);
            }

            CharacterTestRuntimeAiLabNavigationFlushResult flushResult = flush != null
                ? flush()
                : new CharacterTestRuntimeAiLabNavigationFlushResult(
                    false,
                    "Navigation flush callback is unavailable.");
            if (flushResult.Success)
                return Proceed(flushAttempted: true);

            return Result(
                CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed,
                true,
                "NAVIGATION_DRAFT_SAVE_FAILED",
                "草稿保存失败，仍停留在当前页面",
                "可以重试保存、放弃本次未保存修改，或取消返回。",
                "已保存草稿、游戏版本和当前测试都没有改变。",
                new[]
                {
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.RetrySave,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardPendingChanges,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails,
                },
                flushResult.Diagnostic);
        }

        private static CharacterTestRuntimeAiLabNavigationExitResult Proceed(bool flushAttempted)
        {
            return new CharacterTestRuntimeAiLabNavigationExitResult(
                CharacterTestRuntimeAiLabNavigationExitDisposition.Proceed,
                flushAttempted,
                CharacterTestRuntimeAiLabNavigationFeedback.None);
        }

        private static CharacterTestRuntimeAiLabNavigationExitResult Result(
            CharacterTestRuntimeAiLabNavigationExitDisposition disposition,
            bool flushAttempted,
            string code,
            string title,
            string message,
            string impact,
            CharacterTestRuntimeAiLabNavigationFeedbackAction[] actions,
            string technicalDetails)
        {
            return new CharacterTestRuntimeAiLabNavigationExitResult(
                disposition,
                flushAttempted,
                new CharacterTestRuntimeAiLabNavigationFeedback(
                    code,
                    title,
                    message,
                    impact,
                    actions,
                    technicalDetails));
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabStableReturnContext
    {
        internal CharacterTestRuntimeAiLabStableReturnContext(
            int authoringWorkspaceRole,
            bool hasAuthoringWorkspaceRole,
            string workspaceId,
            int setupTeamId,
            int setupSlotIndex,
            int selectedAgentIndex,
            int selectedAgentEntityId,
            string groupDraftId,
            string groupPublishedStableId,
            string entryId,
            string cardDraftId,
            string cardPublishedStableId,
            string timelineId,
            string eventId,
            int inspectorTargetKind,
            CharacterTestRuntimeAiLabTimelineMode timelineMode,
            CharacterTestRuntimeAiLabPageContext pageContext)
        {
            AuthoringWorkspaceRole = authoringWorkspaceRole;
            HasAuthoringWorkspaceRole = hasAuthoringWorkspaceRole;
            WorkspaceId = workspaceId ?? string.Empty;
            SetupTeamId = setupTeamId;
            SetupSlotIndex = setupSlotIndex;
            SelectedAgentIndex = selectedAgentIndex;
            SelectedAgentEntityId = selectedAgentEntityId;
            GroupDraftId = groupDraftId ?? string.Empty;
            GroupPublishedStableId = groupPublishedStableId ?? string.Empty;
            EntryId = entryId ?? string.Empty;
            CardDraftId = cardDraftId ?? string.Empty;
            CardPublishedStableId = cardPublishedStableId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            InspectorTargetKind = inspectorTargetKind;
            TimelineMode = timelineMode;
            PageContext = pageContext;
        }

        internal int AuthoringWorkspaceRole { get; }
        internal bool HasAuthoringWorkspaceRole { get; }
        internal string WorkspaceId { get; }
        internal int SetupTeamId { get; }
        internal int SetupSlotIndex { get; }
        internal int SelectedAgentIndex { get; }
        internal int SelectedAgentEntityId { get; }
        internal string GroupDraftId { get; }
        internal string GroupPublishedStableId { get; }
        internal string EntryId { get; }
        internal string CardDraftId { get; }
        internal string CardPublishedStableId { get; }
        internal string TimelineId { get; }
        internal string EventId { get; }
        internal int InspectorTargetKind { get; }
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode { get; }
        internal CharacterTestRuntimeAiLabPageContext PageContext { get; }
        internal bool IsValid => WorkspaceId.Length > 0
            && SetupTeamId > 0
            && SetupSlotIndex >= 0
            && GroupDraftId.Length > 0
            && EntryId.Length > 0
            && CardDraftId.Length > 0;
    }
}
