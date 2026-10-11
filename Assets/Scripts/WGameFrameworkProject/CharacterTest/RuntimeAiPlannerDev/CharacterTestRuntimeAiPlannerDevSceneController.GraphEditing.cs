using System;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool TryBuildLogicDetectorWindowCanonicalizationPreview(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out string preview,
            out string message)
        {
            preview = string.Empty;
            if (!TryBeginStageMutation(
                    entryIndex,
                    expectedIdentity,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message,
                    allowWindowCanonicalizationRequired: true))
            {
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload;
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload))
            {
                message = "当前草稿不需要规范化 Detector window。";
                return false;
            }

            var builder = new StringBuilder(256);
            builder.Append("规范化预览：");
            bool first = true;
            for (int i = 0; payload != null && i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null)
                    continue;
                bool legacyMode = detector.WindowEndMode
                    == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
                bool dormantDuration = detector.WindowEndMode
                        != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    && detector.ActivationDurationFrames != 0;
                if (!legacyMode && !dormantDuration)
                    continue;
                if (!first)
                    builder.Append("；");
                first = false;
                builder.Append('[').Append(detector.DetectorId).Append("] ");
                if (legacyMode)
                {
                    builder.Append("mode UntilEvent → Infinite");
                    if (dormantDuration)
                        builder.Append(", ");
                }
                if (dormantDuration)
                {
                    builder.Append("duration ")
                        .Append(detector.ActivationDurationFrames.ToString(CultureInfo.InvariantCulture))
                        .Append("f → 0f");
                }
            }
            builder.Append("。再次点击确认；身份或 revision 改变将自动取消。");
            preview = builder.ToString();
            message = string.Empty;
            return true;
        }

        internal bool TryCanonicalizeLogicDetectorWindows(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(
                    entryIndex,
                    expectedIdentity,
                    out var card,
                    out message,
                    allowWindowCanonicalizationRequired: true))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryCanonicalizeDetectorWindows(
                    payload,
                    out bool changed,
                    out message))
                return false;
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "Canonicalize Detector windows",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryAddLogicDetector(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            CharacterTestRuntimeAiLabLogicDetectorScope scope,
            string owningStageId,
            out string detectorId,
            out string resultingCardDraftId,
            out string message)
        {
            detectorId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                    payload, scope, owningStageId, out detectorId, out message))
                return false;
            string targetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            if (scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage)
            {
                CharacterTestRuntimeAiLabLogicStage source = null;
                for (int i = 0; i < payload.Stages.Count; i++)
                {
                    if (string.Equals(payload.Stages[i]?.StageId, owningStageId, StringComparison.Ordinal))
                    {
                        source = payload.Stages[i];
                        break;
                    }
                }
                int nextOrder = int.MaxValue;
                for (int i = 0; source != null && i < payload.Stages.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStage candidate = payload.Stages[i];
                    if (candidate == null
                        || candidate.Order <= source.Order
                        || candidate.Order >= nextOrder)
                    {
                        continue;
                    }
                    nextOrder = candidate.Order;
                    targetStageId = candidate.StageId;
                }
            }
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddTransition(
                    payload,
                    detectorId,
                    owningStageId,
                    targetStageId,
                    out string transitionId,
                    out message))
            {
                return false;
            }
            if (TryCommitStageMutation(
                    entryIndex, payload, "Add Branch Event", out resultingCardDraftId, out message))
            {
                if (payload.Timelines.Count > 0)
                    detectorId = "event." + transitionId;
                return true;
            }
            detectorId = string.Empty;
            return false;
        }

        internal bool TryAddLogicBranchEvent(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string stageId,
            int activationStartFrame,
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timingKind,
            out string detectorId,
            out string transitionId,
            out string resultingCardDraftId,
            out string message)
        {
            detectorId = string.Empty;
            transitionId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline source = null;
                for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
                {
                    if (!string.Equals(payload.Timelines[i]?.TimelineId, stageId, StringComparison.Ordinal))
                        continue;
                    source = payload.Timelines[i];
                    break;
                }
                if (source == null)
                {
                    message = "事件所属时间轴不存在。";
                    return false;
                }

                transitionId = AllocateRecipeTriggerEventId(payload);
                source.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = transitionId,
                    TimelineId = stageId,
                    Title = "新事件",
                    StartFrame = Math.Max(0, Math.Min(36000, activationStartFrame)),
                    TimingKind = timingKind,
                    DurationFrames = timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                        ? 1
                        : 30,
                    MaxExecutionsPerPass = 1,
                    CooldownFrames = 0,
                    Priority = 100,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured
                    }
                });
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                detectorId = "event." + transitionId;
                if (TryCommitStageMutation(
                        entryIndex,
                        payload,
                        "Add Event",
                        out resultingCardDraftId,
                        out message))
                {
                    return true;
                }
                detectorId = string.Empty;
                transitionId = string.Empty;
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline sourceTimeline = null;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                if (!string.Equals(payload.Timelines[i]?.TimelineId, stageId, StringComparison.Ordinal))
                    continue;
                sourceTimeline = payload.Timelines[i];
                break;
            }
            if (sourceTimeline == null)
            {
                message = "Branch source Timeline does not exist.";
                return false;
            }

            int localFrame = Math.Max(1, Math.Min(36000, activationStartFrame));
            string targetTimelineId = string.Empty;
            int bestOrder = int.MaxValue;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[i];
                if (candidate == null
                    || ReferenceEquals(candidate, sourceTimeline)
                    || candidate.Order <= sourceTimeline.Order
                    || candidate.Order > bestOrder
                    || (candidate.Order == bestOrder
                        && string.CompareOrdinal(candidate.TimelineId, targetTimelineId) >= 0))
                    continue;
                bestOrder = candidate.Order;
                targetTimelineId = candidate.TimelineId ?? string.Empty;
            }

            transitionId = AllocateRecipeTriggerEventId(payload);
            sourceTimeline.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = transitionId,
                TimelineId = stageId,
                Title = localFrame.ToString(CultureInfo.InvariantCulture) + "f",
                Priority = 100,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                ActivationStartFrame = localFrame,
                ActivationDurationFrames = 0,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                ExpectedResult = true,
                LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame,
                LocalFrame = localFrame,
                OutcomeKind = string.IsNullOrEmpty(targetTimelineId)
                    ? CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                    : CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                TargetTimelineId = targetTimelineId
            });
            detectorId = "event." + transitionId;

            if (TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "Add Branch Event",
                    out resultingCardDraftId,
                    out message))
            {
                return true;
            }
            detectorId = string.Empty;
            transitionId = string.Empty;
            return false;
        }

        internal bool TryAddCharacterActionEvent(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string timelineId,
            int startFrame,
            MxFramework.CharacterAction.CharacterActionId characterActionId,
            out string eventId,
            out string resultingCardDraftId,
            out string message)
        {
            eventId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            EnsureBehaviorCardReferences();
            if (_behaviorCardReferences == null
                || !_behaviorCardReferences.TryResolveCharacterActionDurationFrames(
                    characterActionId,
                    out int durationFrames))
            {
                message = "所选角色动作不存在或缺少权威时长。";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                message = "当前行为卡不是可编辑的 v8 行为配方。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = null;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                {
                    timeline = payload.Timelines[i];
                    break;
                }
            }
            if (timeline == null)
            {
                message = "角色动作事件所属时间轴不存在。";
                return false;
            }

            string displayName = characterActionId.ToString();
            var actions = _behaviorCardReferences.CharacterActions;
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null || action.ActionId != characterActionId)
                    continue;
                if (!string.IsNullOrWhiteSpace(action.DisplayName))
                    displayName = action.DisplayName.Trim();
                break;
            }
            eventId = AllocateRecipeTriggerEventId(payload);
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = eventId,
                TimelineId = timelineId,
                Title = "播放 " + displayName,
                StartFrame = Math.Max(0, Math.Min(36000, startFrame)),
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                DurationFrames = durationFrames,
                ConditionRoot = null,
                MaxExecutionsPerPass = 1,
                CooldownFrames = 0,
                Priority = 100,
                DurationReviewRequired = false,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                    CharacterActionId = characterActionId
                }
            });
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            if (TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "Add CharacterAction Event",
                    out resultingCardDraftId,
                    out message))
            {
                message = "已添加角色动作事件“" + displayName + "”。";
                return true;
            }
            eventId = string.Empty;
            return false;
        }

        private static string AllocateRecipeTriggerEventId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            for (int suffix = 1; suffix < int.MaxValue; suffix++)
            {
                string candidate = "event.new." + suffix.ToString(CultureInfo.InvariantCulture);
                if (!ContainsRecipeTriggerEventId(payload, candidate))
                    return candidate;
            }
            throw new InvalidOperationException("Behavior Recipe Event ID space is exhausted.");
        }

        private static bool ContainsRecipeTriggerEventId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string eventId)
        {
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                for (int eventIndex = 0; timeline != null && eventIndex < timeline.Events.Count; eventIndex++)
                {
                    if (string.Equals(
                            timeline.Events[eventIndex]?.EventId,
                            eventId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                for (int eventIndex = 0; timeline != null && eventIndex < timeline.TriggerEvents.Count; eventIndex++)
                {
                    if (string.Equals(
                            timeline.TriggerEvents[eventIndex]?.EventId,
                            eventId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            for (int i = 0; payload != null && i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                if (string.Equals(
                        payload.LegacySharedTriggerEvents[i]?.EventId,
                        eventId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal bool TrySetLogicEntryStage(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string stageId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                bool v6Exists = payload.Timelines.Exists(t => string.Equals(t.TimelineId, stageId, StringComparison.Ordinal));
                if (!v6Exists)
                {
                    message = "所选时间轴不存在。";
                    return false;
                }
                if (string.Equals(payload.EntryTimelineId, stageId, StringComparison.Ordinal))
                {
                    resultingCardDraftId = card.CardDraftId;
                    message = "该时间轴已是行为入口。";
                    return true;
                }
                payload.EntryTimelineId = stageId;
                payload.EntryStageId = stageId;
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                return TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "设置行为入口时间轴",
                    out resultingCardDraftId,
                    out message);
            }

            bool exists = false;
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i]?.StageId, stageId, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                message = "Entry Stage does not exist.";
                return false;
            }
            if (string.Equals(payload.EntryStageId, stageId, StringComparison.Ordinal))
            {
                resultingCardDraftId = card.CardDraftId;
                message = "Stage is already the behavior entry.";
                return true;
            }
            payload.EntryStageId = stageId;
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "Set Behavior Entry Stage",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryReorderLogicBranchPriority(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            int direction,
            int steps,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicDetector detector = FindLogicDetector(payload, detectorId);
            long delta = (direction < 0 ? -10L : 10L) * Math.Max(1, steps);
            long requested = detector == null ? long.MaxValue : detector.Priority + delta;
            if (detector == null || requested < int.MinValue || requested > int.MaxValue)
            {
                message = "Branch priority is outside Int32.";
                return false;
            }
            detector.Priority = (int)requested;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (transition == null
                    || !string.Equals(transition.DetectorId, detectorId, StringComparison.Ordinal))
                    continue;
                long transitionPriority = transition.Priority + delta;
                if (transitionPriority < int.MinValue || transitionPriority > int.MaxValue)
                {
                    message = "Associated result priority is outside Int32.";
                    return false;
                }
                transition.Priority = (int)transitionPriority;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "Reorder Branch Event",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryDuplicateLogicDetector(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            out string createdDetectorId,
            out string resultingCardDraftId,
            out string message)
        {
            createdDetectorId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDuplicateDetector(
                    payload, detectorId, out createdDetectorId, out message))
                return false;
            if (TryCommitStageMutation(
                    entryIndex, payload, "Duplicate Detector", out resultingCardDraftId, out message))
                return true;
            createdDetectorId = string.Empty;
            return false;
        }

        internal bool TryDuplicateLogicBranchEvent(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            out string createdDetectorId,
            out string resultingCardDraftId,
            out string message)
        {
            createdDetectorId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicDetector source = FindLogicDetector(payload, detectorId);
            if (source == null
                || !CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDuplicateDetector(
                    payload,
                    detectorId,
                    out createdDetectorId,
                    out message))
                return false;
            CharacterTestRuntimeAiLabLogicDetector created =
                FindLogicDetector(payload, createdDetectorId);
            if (created == null)
            {
                message = "Duplicated Branch detector could not be resolved.";
                return false;
            }
            if (created.Priority < int.MaxValue)
                created.Priority++;
            int originalTransitionCount = payload.Transitions.Count;
            for (int i = 0; i < originalTransitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition original = payload.Transitions[i];
                if (original == null
                    || !string.Equals(original.DetectorId, detectorId, StringComparison.Ordinal))
                    continue;
                if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddTransition(
                        payload,
                        createdDetectorId,
                        original.SourceStageId,
                        original.TargetStageId,
                        out string createdTransitionId,
                        out message))
                    return false;
                CharacterTestRuntimeAiLabLogicTransition copy =
                    FindLogicTransition(payload, createdTransitionId);
                if (copy == null)
                    continue;
                long relativePriority = (long)original.Priority - source.Priority + created.Priority;
                copy.Priority = relativePriority < int.MinValue
                    ? int.MinValue
                    : relativePriority > int.MaxValue
                        ? int.MaxValue
                        : (int)relativePriority;
                copy.Policy = original.Policy;
                copy.Label = original.Label;
                copy.EndReason = original.EndReason;
            }
            if (TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "Duplicate Branch Event",
                    out resultingCardDraftId,
                    out message))
                return true;
            createdDetectorId = string.Empty;
            return false;
        }

        internal bool TryDeleteLogicDetector(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            bool cascadeConfirmed,
            out int linkedTransitionCount,
            out string resultingCardDraftId,
            out string message)
        {
            linkedTransitionCount = 0;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteDetector(
                    payload,
                    detectorId,
                    cascadeConfirmed,
                    out linkedTransitionCount,
                    out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Delete Detector", out resultingCardDraftId, out message);
        }

        internal bool TryEditLogicDetectorField(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyDetectorField(
                    payload, detectorId, field, value, out bool changed, out message))
                return false;
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex, payload, "Edit Detector " + field, out resultingCardDraftId, out message);
        }

        internal bool TryMoveLogicDetectorWindow(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            int requestedDelta,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryMoveDetectorWindow(
                    payload, detectorId, requestedDelta, out _, out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Move Detector window", out resultingCardDraftId, out message);
        }

        internal bool TryResizeLogicDetectorWindow(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            int requestedDelta,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryResizeDetectorWindow(
                    payload, detectorId, requestedDelta, out _, out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Resize Detector window", out resultingCardDraftId, out message);
        }

        internal bool TryAddLogicDetectorCondition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            out string conditionId,
            out string resultingCardDraftId,
            out string message)
        {
            conditionId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddCondition(
                    payload, detectorId, out conditionId, out message))
                return false;
            if (TryCommitStageMutation(
                    entryIndex, payload, "Add Detector condition", out resultingCardDraftId, out message))
                return true;
            conditionId = string.Empty;
            return false;
        }

        internal bool TryDeleteLogicDetectorCondition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            string conditionId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteCondition(
                    payload, detectorId, conditionId, out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Delete Detector condition", out resultingCardDraftId, out message);
        }

        internal bool TryEditLogicDetectorCondition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            string conditionId,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            in CharacterTestRuntimeAiLabConditionFieldValue value,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyConditionField(
                    payload, detectorId, conditionId, field, value, out bool changed, out message))
                return false;
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex, payload, "Edit Detector condition " + field, out resultingCardDraftId, out message);
        }

        internal bool TryConnectLogicTransition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            string sourceStageId,
            string targetStageId,
            out string transitionId,
            out string resultingCardDraftId,
            out string message)
        {
            return TryConnectLogicTransition(
                entryIndex,
                expectedIdentity,
                detectorId,
                sourceStageId,
                targetStageId,
                null,
                null,
                out transitionId,
                out resultingCardDraftId,
                out message);
        }

        internal bool TryConnectLogicTransition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            string sourceStageId,
            string targetStageId,
            string labelOverride,
            string endReasonOverride,
            out string transitionId,
            out string resultingCardDraftId,
            out string message)
        {
            transitionId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddTransition(
                    payload,
                    detectorId,
                    sourceStageId,
                    targetStageId,
                    out transitionId,
                    out message))
                return false;
            if (labelOverride != null || endReasonOverride != null)
            {
                for (int i = payload.Transitions.Count - 1; i >= 0; i--)
                {
                    CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                    if (transition == null
                        || !string.Equals(
                            transition.TransitionId,
                            transitionId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (labelOverride != null)
                        transition.Label = labelOverride;
                    if (endReasonOverride != null)
                        transition.EndReason = endReasonOverride;
                    break;
                }
            }
            if (TryCommitStageMutation(
                    entryIndex, payload, "Connect Transition", out resultingCardDraftId, out message))
                return true;
            transitionId = string.Empty;
            return false;
        }

        internal bool TryEditLogicTransitionField(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string transitionId,
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            in CharacterTestRuntimeAiLabTransitionFieldValue value,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyTransitionField(
                    payload, transitionId, field, value, out bool changed, out message))
                return false;
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex, payload, "Edit Transition " + field, out resultingCardDraftId, out message);
        }

        internal bool TryDeleteLogicTransition(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string transitionId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteTransition(
                    payload, transitionId, out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Delete Transition", out resultingCardDraftId, out message);
        }

        internal bool TryCycleLogicDetectorFact(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicDetector detector = FindLogicDetector(payload, detectorId);
            if (detector == null)
            {
                message = "Detector stable ID is invalid.";
                return false;
            }
            if (detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                || detector.Conditions.Count == 0)
            {
                if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyDetectorField(
                        payload,
                        detectorId,
                        CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                        new CharacterTestRuntimeAiLabDetectorFieldValue(
                            trigger: CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge),
                        out _,
                        out message))
                    return false;
                detector = FindLogicDetector(payload, detectorId);
            }
            CharacterTestRuntimeAiLabCardCondition condition = detector.Conditions[0];
            CharacterTestRuntimeAiLabCardConditionFact next = condition.Fact
                == CharacterTestRuntimeAiLabCardConditionFact.TargetVisible
                    ? CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf
                    : condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf
                        ? CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable
                        : CharacterTestRuntimeAiLabCardConditionFact.TargetVisible;
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyConditionField(
                    payload,
                    detectorId,
                    condition.ConditionId,
                    CharacterTestRuntimeAiLabConditionFieldKind.Fact,
                    new CharacterTestRuntimeAiLabConditionFieldValue(fact: next),
                    out _,
                    out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Cycle Detector fact", out resultingCardDraftId, out message);
        }

        internal bool TryNudgeLogicDetectorPriority(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string detectorId,
            int direction,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicDetector detector = FindLogicDetector(payload, detectorId);
            long requested = detector == null
                ? long.MaxValue
                : (long)detector.Priority + (direction < 0 ? 10 : -10);
            if (detector == null || requested < int.MinValue || requested > int.MaxValue)
            {
                message = "DETECTOR_PRIORITY_OVERFLOW: requested priority is outside Int32.";
                return false;
            }
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyDetectorField(
                    payload,
                    detectorId,
                    CharacterTestRuntimeAiLabDetectorFieldKind.Priority,
                    new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: (int)requested),
                    out _,
                    out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Reorder Detector priority", out resultingCardDraftId, out message);
        }

        internal bool TryCycleLogicTransitionPolicy(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string transitionId,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicTransition transition = FindLogicTransition(payload, transitionId);
            if (transition == null)
            {
                message = "Transition stable ID is invalid.";
                return false;
            }
            CharacterTestRuntimeAiLabLogicTransitionPolicy next =
                (CharacterTestRuntimeAiLabLogicTransitionPolicy)(((int)transition.Policy + 1) % 3);
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyTransitionField(
                    payload,
                    transitionId,
                    CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                    new CharacterTestRuntimeAiLabTransitionFieldValue(policy: next),
                    out _,
                    out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Cycle Transition policy", out resultingCardDraftId, out message);
        }

        internal bool TryCycleLogicTransitionTarget(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string transitionId,
            int direction,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            CharacterTestRuntimeAiLabLogicTransition transition = FindLogicTransition(payload, transitionId);
            if (transition == null || payload.Stages.Count == 0)
            {
                message = "Transition stable ID is invalid or no Stage target exists.";
                return false;
            }
            int current = payload.Stages.Count;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, transition.TargetStageId,
                        System.StringComparison.Ordinal))
                {
                    current = i;
                    break;
                }
            }
            int count = payload.Stages.Count + 1;
            int nextIndex = (current + (direction < 0 ? count - 1 : 1)) % count;
            string target = nextIndex == payload.Stages.Count
                ? CharacterTestRuntimeAiLabLogicTransition.EndTarget
                : payload.Stages[nextIndex].StageId;
            if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyTransitionField(
                    payload,
                    transitionId,
                    CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage,
                    new CharacterTestRuntimeAiLabTransitionFieldValue(stringValue: target),
                    out _,
                    out message))
                return false;
            return TryCommitStageMutation(
                entryIndex, payload, "Cycle Transition target", out resultingCardDraftId, out message);
        }

        private static CharacterTestRuntimeAiLabLogicDetector FindLogicDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; payload != null && i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i]?.DetectorId, detectorId,
                        System.StringComparison.Ordinal))
                    return payload.Detectors[i];
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindLogicTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId)
        {
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i]?.TransitionId, transitionId,
                        System.StringComparison.Ordinal))
                    return payload.Transitions[i];
            }
            return null;
        }
    }
}
