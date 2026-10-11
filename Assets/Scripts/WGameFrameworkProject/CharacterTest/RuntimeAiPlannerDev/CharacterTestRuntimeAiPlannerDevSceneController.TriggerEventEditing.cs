namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool TryApplyBehaviorRecipeTriggerEventField(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string timelineId,
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int conditionIndex,
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                    payload,
                    timelineId,
                    eventId,
                    field,
                    conditionIndex,
                    value,
                    out bool changed,
                    out message))
            {
                return false;
            }
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                message = "分支事件没有变化。";
                return true;
            }
            if (field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationCharacterActionId
                || field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConvertCharacterActionEvent)
            {
                EnsureBehaviorCardReferences();
                if (_behaviorCardReferences == null
                    || !_behaviorCardReferences.TryResolveCharacterActionDurationFrames(
                        ResolveCharacterActionId(payload, timelineId, eventId, value.StringValue),
                        out int authoritativeDuration))
                {
                    message = "所选角色动作不存在或缺少权威时长。";
                    return false;
                }
                if (!TryNormalizeCharacterActionEvent(
                        payload,
                        timelineId,
                        eventId,
                        authoritativeDuration))
                {
                    message = "角色动作事件已失效，请重新选择。";
                    return false;
                }
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "Edit Trigger Event " + field,
                out resultingCardDraftId,
                out message);
        }

        private static bool TryNormalizeCharacterActionEvent(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            int authoritativeDuration)
        {
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                if (timeline == null || !string.Equals(timeline.TimelineId, timelineId, System.StringComparison.Ordinal))
                    continue;
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt == null || !string.Equals(evt.EventId, eventId, System.StringComparison.Ordinal))
                        continue;
                    if (evt.Operation?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                        return false;
                    evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
                    evt.DurationFrames = System.Math.Max(1, authoritativeDuration);
                    evt.MaxExecutionsPerPass = 1;
                    evt.CooldownFrames = 0;
                    evt.DurationReviewRequired = false;
                    return true;
                }
            }
            return false;
        }

        private static MxFramework.CharacterAction.CharacterActionId ResolveCharacterActionId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            string fallback)
        {
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                if (timeline == null || !string.Equals(timeline.TimelineId, timelineId, System.StringComparison.Ordinal))
                    continue;
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt != null && string.Equals(evt.EventId, eventId, System.StringComparison.Ordinal))
                        return evt.Operation?.CharacterActionId
                            ?? (MxFramework.CharacterAction.CharacterActionId.TryParse(fallback, out var parsed)
                                ? parsed
                                : default);
                }
            }
            return MxFramework.CharacterAction.CharacterActionId.TryParse(fallback, out var result)
                ? result
                : default;
        }

        internal bool TryDeleteBehaviorRecipeTriggerEvent(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string timelineId,
            string eventId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryDelete(
                    payload,
                    timelineId,
                    eventId,
                    out bool changed,
                    out message))
            {
                return false;
            }
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "Delete Trigger Event",
                out resultingCardDraftId,
                out message);
        }
    }
}
