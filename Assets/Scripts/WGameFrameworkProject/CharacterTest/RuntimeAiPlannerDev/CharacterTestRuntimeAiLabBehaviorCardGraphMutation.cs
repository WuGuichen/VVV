using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabDetectorFieldKind
    {
        Title = 0,
        Scope = 1,
        OwningStage = 2,
        Trigger = 3,
        Priority = 4,
        ActivationStartFrame = 5,
        WindowEndMode = 6,
        ActivationDurationFrames = 7
    }

    internal readonly struct CharacterTestRuntimeAiLabDetectorFieldValue
    {
        internal CharacterTestRuntimeAiLabDetectorFieldValue(
            int intValue = 0,
            string stringValue = null,
            CharacterTestRuntimeAiLabLogicDetectorScope scope = default,
            CharacterTestRuntimeAiLabLogicDetectorTrigger trigger = default,
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode windowEndMode = default)
        {
            IntValue = intValue;
            StringValue = stringValue ?? string.Empty;
            Scope = scope;
            Trigger = trigger;
            WindowEndMode = windowEndMode;
        }

        internal int IntValue { get; }
        internal string StringValue { get; }
        internal CharacterTestRuntimeAiLabLogicDetectorScope Scope { get; }
        internal CharacterTestRuntimeAiLabLogicDetectorTrigger Trigger { get; }
        internal CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode { get; }
    }

    internal enum CharacterTestRuntimeAiLabConditionFieldKind
    {
        Fact = 0,
        Operator = 1,
        BoolValue = 2,
        Value = 3,
        Range = 4,
        AbilityId = 5,
        CharacterActionId = 6
    }

    internal readonly struct CharacterTestRuntimeAiLabConditionFieldValue
    {
        internal CharacterTestRuntimeAiLabConditionFieldValue(
            int intValue = 0,
            float floatValue = 0f,
            float secondFloatValue = 0f,
            bool boolValue = false,
            string stringValue = null,
            CharacterTestRuntimeAiLabCardConditionFact fact = default,
            CharacterTestRuntimeAiLabCardConditionOperator conditionOperator = default)
        {
            IntValue = intValue;
            FloatValue = floatValue;
            SecondFloatValue = secondFloatValue;
            BoolValue = boolValue;
            StringValue = stringValue ?? string.Empty;
            Fact = fact;
            Operator = conditionOperator;
        }

        internal int IntValue { get; }
        internal float FloatValue { get; }
        internal float SecondFloatValue { get; }
        internal bool BoolValue { get; }
        internal string StringValue { get; }
        internal CharacterTestRuntimeAiLabCardConditionFact Fact { get; }
        internal CharacterTestRuntimeAiLabCardConditionOperator Operator { get; }
    }

    internal enum CharacterTestRuntimeAiLabTransitionFieldKind
    {
        Label = 0,
        SourceStage = 1,
        TargetStage = 2,
        Policy = 3,
        Priority = 4,
        EndReason = 5
    }

    internal readonly struct CharacterTestRuntimeAiLabTransitionFieldValue
    {
        internal CharacterTestRuntimeAiLabTransitionFieldValue(
            int intValue = 0,
            string stringValue = null,
            CharacterTestRuntimeAiLabLogicTransitionPolicy policy = default)
        {
            IntValue = intValue;
            StringValue = stringValue ?? string.Empty;
            Policy = policy;
        }

        internal int IntValue { get; }
        internal string StringValue { get; }
        internal CharacterTestRuntimeAiLabLogicTransitionPolicy Policy { get; }
    }

    /// <summary>Low-frequency authoring mutations for Detector, Condition and Transition nodes.</summary>
    internal static class CharacterTestRuntimeAiLabBehaviorCardGraphMutation
    {
        private const int MaximumFrame = CharacterTestRuntimeAiLabBehaviorCardStageMutation.MaximumFrame;

        internal static bool RequiresWindowCanonicalization(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            for (int i = 0; payload != null && i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null)
                    continue;
                if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent
                    || (detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                        && detector.ActivationDurationFrames != 0))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool TryCanonicalizeDetectorWindows(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            if (payload == null)
            {
                diagnostic = "Behavior card logic is unavailable.";
                return false;
            }
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null)
                    continue;
                if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent)
                {
                    detector.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
                    changed = true;
                }
                if (detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    && detector.ActivationDurationFrames != 0)
                {
                    detector.ActivationDurationFrames = 0;
                    changed = true;
                }
            }
            diagnostic = changed
                ? "Detector windows were canonicalized for current authoring semantics."
                : "Detector windows are already canonical.";
            return true;
        }

        internal static bool TryAddDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicDetectorScope scope,
            string owningStageId,
            out string detectorId,
            out string diagnostic)
        {
            detectorId = string.Empty;
            if (payload == null || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorScope), scope))
            {
                diagnostic = "Detector scope is invalid.";
                return false;
            }
            if (scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                && FindStage(payload, owningStageId) == null)
            {
                diagnostic = "Owning Stage does not exist.";
                return false;
            }
            int maximumPriority = 0;
            bool hasDetector = false;
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null)
                    continue;
                maximumPriority = hasDetector ? Math.Max(maximumPriority, detector.Priority) : detector.Priority;
                hasDetector = true;
            }
            if (hasDetector && maximumPriority > int.MaxValue - 10)
            {
                diagnostic = "DETECTOR_PRIORITY_EXHAUSTED: highest priority cannot be increased by 10.";
                return false;
            }
            detectorId = AllocateDetectorId(payload, "detector.new.");
            var detectorToAdd = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = detectorId,
                Title = "Target visible",
                Scope = scope,
                StageId = scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    ? owningStageId
                    : string.Empty,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = hasDetector ? maximumPriority + 10 : 100,
                ActivationStartFrame = 0,
                ActivationDurationFrames = 0,
                WindowEndMode = scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                    : CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
            };
            detectorToAdd.Conditions.Add(CreateDefaultCondition(detectorId + ".condition.1"));
            payload.Detectors.Add(detectorToAdd);
            diagnostic = "Detector created.";
            return true;
        }

        internal static bool TryDuplicateDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string sourceDetectorId,
            out string detectorId,
            out string diagnostic)
        {
            detectorId = string.Empty;
            CharacterTestRuntimeAiLabLogicDetector source = FindDetector(payload, sourceDetectorId);
            if (source == null)
            {
                diagnostic = "Detector stable ID is invalid.";
                return false;
            }
            detectorId = AllocateDetectorId(payload, sourceDetectorId + ".copy.");
            CharacterTestRuntimeAiLabLogicDetector copy = source.Clone();
            copy.DetectorId = detectorId;
            for (int i = 0; i < copy.Conditions.Count; i++)
                copy.Conditions[i].ConditionId = detectorId + ".condition." + (i + 1);
            payload.Detectors.Add(copy);
            diagnostic = "Detector copied; linked transitions were not copied.";
            return true;
        }

        internal static bool TryDeleteDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            bool cascadeConfirmed,
            out int linkedTransitionCount,
            out string diagnostic)
        {
            linkedTransitionCount = CountLinkedTransitions(payload, detectorId);
            int detectorIndex = FindDetectorIndex(payload, detectorId);
            if (detectorIndex < 0)
            {
                diagnostic = "Detector stable ID is invalid.";
                return false;
            }
            if (linkedTransitionCount > 0 && !cascadeConfirmed)
            {
                diagnostic = "DETECTOR_DELETE_CONFIRM_REQUIRED: linked transitions=" + linkedTransitionCount + ".";
                return false;
            }
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
            if (detector != null && detector.IsBehaviorRecipeV5Event)
            {
                if (!TryDeleteRecipeTriggerEvent(payload, detectorId, out diagnostic))
                    return false;
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
                diagnostic = "Behavior Recipe Trigger Event and its compatibility projection deleted.";
                return true;
            }
            for (int i = payload.Transitions.Count - 1; i >= 0; i--)
            {
                if (string.Equals(payload.Transitions[i]?.DetectorId, detectorId, StringComparison.Ordinal))
                    payload.Transitions.RemoveAt(i);
            }
            payload.Detectors.RemoveAt(detectorIndex);
            diagnostic = "Detector and linked transitions deleted.";
            return true;
        }

        private static bool TryDeleteRecipeTriggerEvent(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            out string diagnostic)
        {
            string eventId = string.Empty;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (transition != null
                    && transition.IsBehaviorRecipeV5Event
                    && string.Equals(transition.DetectorId, detectorId, StringComparison.Ordinal))
                {
                    eventId = transition.TransitionId ?? string.Empty;
                    break;
                }
            }
            if (eventId.Length == 0 && detectorId.StartsWith("event.", StringComparison.Ordinal))
                eventId = detectorId.Substring("event.".Length);
            if (eventId.Length == 0)
            {
                diagnostic = "Behavior Recipe Trigger Event identity is unavailable.";
                return false;
            }

            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                for (int eventIndex = (timeline?.TriggerEvents.Count ?? 0) - 1; eventIndex >= 0; eventIndex--)
                {
                    if (!string.Equals(
                            timeline.TriggerEvents[eventIndex]?.EventId,
                            eventId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    timeline.TriggerEvents.RemoveAt(eventIndex);
                    diagnostic = string.Empty;
                    return true;
                }
            }
            for (int eventIndex = payload.LegacySharedTriggerEvents.Count - 1; eventIndex >= 0; eventIndex--)
            {
                if (!string.Equals(
                        payload.LegacySharedTriggerEvents[eventIndex]?.EventId,
                        eventId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                payload.LegacySharedTriggerEvents.RemoveAt(eventIndex);
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = "Behavior Recipe Trigger Event stable ID is invalid: " + eventId + ".";
            return false;
        }

        internal static bool TryApplyDetectorField(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            if (detector == null)
            {
                diagnostic = "Detector stable ID is invalid.";
                return false;
            }
            switch (field)
            {
                case CharacterTestRuntimeAiLabDetectorFieldKind.Title:
                    string title = value.StringValue.Trim();
                    bool signalReason = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                        || detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected;
                    if (signalReason)
                    {
                        if (title.Length != 0 || value.IntValue < 0)
                        {
                            diagnostic = "Action signal reasons must use a predefined numeric code; text is not converted into an identity.";
                            return false;
                        }
                        if (value.IntValue == detector.ActionSignalReasonCode)
                        {
                            return NoChange(out changed, out diagnostic);
                        }
                        detector.ActionSignalReasonCode = value.IntValue;
                        detector.LegacyUnfilteredSignal = false;
                    }
                    else
                    {
                        if (title.Length == 0)
                            title = detector.DetectorId;
                        if (string.Equals(title, detector.Title, StringComparison.Ordinal))
                            return NoChange(out changed, out diagnostic);
                        detector.Title = title;
                    }
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.Scope:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorScope), value.Scope))
                    {
                        diagnostic = "Detector scope is invalid.";
                        return false;
                    }
                    if (detector.Scope == value.Scope)
                        return NoChange(out changed, out diagnostic);
                    if (value.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card)
                    {
                        if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd)
                        {
                            diagnostic = "CARD_SCOPE_REQUIRES_EXPLICIT_WINDOW: select Fixed or Infinite first.";
                            return false;
                        }
                        detector.Scope = value.Scope;
                        detector.StageId = string.Empty;
                    }
                    else
                    {
                        CharacterTestRuntimeAiLabLogicStage owner = FindStage(payload, value.StringValue);
                        if (owner == null)
                        {
                            diagnostic = "Stage scope requires a valid owning Stage.";
                            return false;
                        }
                        detector.Scope = value.Scope;
                        detector.StageId = owner.StageId;
                        RewriteLinkedSources(payload, detector.DetectorId, owner.StageId);
                    }
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.OwningStage:
                    if (detector.Scope != CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                        || FindStage(payload, value.StringValue) == null)
                    {
                        diagnostic = "Owning Stage can only be set for a Stage detector.";
                        return false;
                    }
                    if (string.Equals(detector.StageId, value.StringValue, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    detector.StageId = value.StringValue;
                    RewriteLinkedSources(payload, detector.DetectorId, value.StringValue);
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.Trigger:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorTrigger), value.Trigger))
                    {
                        diagnostic = "Detector trigger is invalid.";
                        return false;
                    }
                    if (payload.Timelines.Count > 0
                        && value.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted)
                    {
                        CharacterTestRuntimeAiLabLogicStage owner = detector.Scope ==
                                CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                            ? FindStage(payload, detector.StageId)
                            : null;
                        if (!CanBindPrimaryActionEnd(owner))
                        {
                            diagnostic = "PrimaryActionEnd requires a finite Wait, CharacterAction, or Ability duration; use a concrete predicate for MoveToTarget or Logic.";
                            return false;
                        }
                        if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                            && detector.LocalFrameAnchor ==
                                CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd)
                        {
                            return NoChange(out changed, out diagnostic);
                        }
                        detector.Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame;
                        detector.Conditions.Clear();
                        detector.ExpectedResult = true;
                        detector.LocalFrameAnchor =
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd;
                        detector.LocalFrame = 0;
                        detector.ActivationStartFrame = 0;
                        detector.ActivationDurationFrames = 0;
                        detector.WindowEndMode =
                            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
                        detector.ActionSignalReasonCode = 0;
                        detector.LegacyUnfilteredSignal = false;
                        break;
                    }
                    if (detector.Trigger == value.Trigger)
                        return NoChange(out changed, out diagnostic);
                    detector.Trigger = value.Trigger;
                    detector.Conditions.Clear();
                    if (value.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge)
                    {
                        detector.Conditions.Add(CreateDefaultCondition(detector.DetectorId + ".condition.1"));
                        detector.ExpectedResult = true;
                    }
                    else if (value.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame)
                    {
                        detector.LocalFrameAnchor =
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame;
                        detector.LocalFrame = Math.Max(1, detector.ActivationStartFrame);
                        detector.ActivationStartFrame = detector.LocalFrame;
                        detector.ActionSignalReasonCode = 0;
                        detector.LegacyUnfilteredSignal = false;
                    }
                    else
                    {
                        detector.LocalFrame = 0;
                        detector.LegacyUnfilteredSignal = false;
                        detector.ActionSignalReasonCode = value.Trigger ==
                                CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                            ? CharacterTestAiTraceReason.RUNTIME_ACTION_FAILED
                            : value.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected
                                ? CharacterTestAiTraceReason.COMMAND_REJECTED
                                : 0;
                    }
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.Priority:
                    long delta = (long)value.IntValue - detector.Priority;
                    if (delta == 0)
                        return NoChange(out changed, out diagnostic);
                    if (!CanShiftLinkedPriorities(payload, detector.DetectorId, delta))
                    {
                        diagnostic = "DETECTOR_PRIORITY_OVERFLOW: linked transition priority would overflow.";
                        return false;
                    }
                    detector.Priority = value.IntValue;
                    ShiftLinkedPriorities(payload, detector.DetectorId, delta);
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.ActivationStartFrame:
                    int minimumFrame = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                        ? 1
                        : 0;
                    if (value.IntValue < minimumFrame || value.IntValue > MaximumFrame
                        || (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                            && (long)value.IntValue + detector.ActivationDurationFrames > MaximumFrame))
                    {
                        diagnostic = "Detector activation start is outside the authoring range.";
                        return false;
                    }
                    if (detector.ActivationStartFrame == value.IntValue
                        && (detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                            || detector.LocalFrame == value.IntValue))
                        return NoChange(out changed, out diagnostic);
                    detector.ActivationStartFrame = value.IntValue;
                    if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                        && detector.LocalFrameAnchor ==
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame)
                    {
                        detector.LocalFrame = value.IntValue;
                    }
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode), value.WindowEndMode)
                        || value.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent)
                    {
                        diagnostic = "UntilEvent is legacy-only; choose Fixed, StageEnd, or Infinite.";
                        return false;
                    }
                    if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                        && value.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd)
                    {
                        diagnostic = "Card detector cannot use StageEnd.";
                        return false;
                    }
                    if (detector.WindowEndMode == value.WindowEndMode
                        && (value.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                            || detector.ActivationDurationFrames == 0))
                        return NoChange(out changed, out diagnostic);
                    detector.WindowEndMode = value.WindowEndMode;
                    if (value.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                    {
                        int requestedDuration = value.IntValue > 0 ? value.IntValue : 60;
                        detector.ActivationDurationFrames = Math.Max(
                            1,
                            Math.Min(MaximumFrame - detector.ActivationStartFrame, requestedDuration));
                    }
                    else
                    {
                        detector.ActivationDurationFrames = 0;
                    }
                    break;
                case CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames:
                    if (detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                        || value.IntValue < 1
                        || (long)detector.ActivationStartFrame + value.IntValue > MaximumFrame)
                    {
                        diagnostic = "Only Fixed detector windows accept a positive duration.";
                        return false;
                    }
                    if (detector.ActivationDurationFrames == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    detector.ActivationDurationFrames = value.IntValue;
                    break;
                default:
                    diagnostic = "Detector field is not supported.";
                    return false;
            }
            changed = true;
            diagnostic = "Detector field updated.";
            return true;
        }

        internal static bool TryMoveDetectorWindow(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            int requestedDelta,
            out int appliedDelta,
            out string diagnostic)
        {
            appliedDelta = 0;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            if (detector == null)
            {
                diagnostic = "Detector stable ID is invalid.";
                return false;
            }
            if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                && detector.LocalFrameAnchor ==
                    CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd)
            {
                diagnostic = "PrimaryActionEnd is bound to the authoritative action duration and cannot be moved.";
                return false;
            }
            int maxStart = detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                ? MaximumFrame - Math.Max(1, detector.ActivationDurationFrames)
                : MaximumFrame;
            int minimumStart = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame ? 1 : 0;
            int next = Math.Max(
                minimumStart,
                Math.Min(maxStart, detector.ActivationStartFrame + requestedDelta));
            appliedDelta = next - detector.ActivationStartFrame;
            if (appliedDelta == 0)
            {
                diagnostic = "Detector window did not move.";
                return false;
            }
            detector.ActivationStartFrame = next;
            if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame)
                detector.LocalFrame = next;
            diagnostic = "Detector window moved.";
            return true;
        }

        internal static bool TryResizeDetectorWindow(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            int requestedDelta,
            out int appliedDelta,
            out string diagnostic)
        {
            appliedDelta = 0;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            if (detector == null
                || detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                || detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
            {
                diagnostic = "Only Fixed detector windows can be resized.";
                return false;
            }
            int next = Math.Max(1, Math.Min(
                MaximumFrame - detector.ActivationStartFrame,
                detector.ActivationDurationFrames + requestedDelta));
            appliedDelta = next - detector.ActivationDurationFrames;
            if (appliedDelta == 0)
            {
                diagnostic = "Detector window duration did not change.";
                return false;
            }
            detector.ActivationDurationFrames = next;
            diagnostic = "Detector window resized.";
            return true;
        }

        internal static bool TryAddCondition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            out string conditionId,
            out string diagnostic)
        {
            conditionId = string.Empty;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            if (detector == null || detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge)
            {
                diagnostic = "Only ConditionEdge detectors accept conditions.";
                return false;
            }
            conditionId = AllocateConditionId(detector);
            detector.Conditions.Add(CreateDefaultCondition(conditionId));
            diagnostic = "Condition added.";
            return true;
        }

        internal static bool TryDeleteCondition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            string conditionId,
            out string diagnostic)
        {
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            int conditionIndex = FindConditionIndex(detector, conditionId);
            if (conditionIndex < 0)
            {
                diagnostic = "Condition stable ID is invalid.";
                return false;
            }
            detector.Conditions.RemoveAt(conditionIndex);
            diagnostic = detector.Conditions.Count == 0
                ? "Condition deleted; the Detector is DraftInvalid until a condition is added or the trigger changes."
                : "Condition deleted.";
            return true;
        }

        internal static bool TryApplyConditionField(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            string conditionId,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            in CharacterTestRuntimeAiLabConditionFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            int conditionIndex = FindConditionIndex(detector, conditionId);
            if (conditionIndex < 0)
            {
                diagnostic = "Condition stable ID is invalid.";
                return false;
            }
            CharacterTestRuntimeAiLabCardCondition condition = detector.Conditions[conditionIndex];
            switch (field)
            {
                case CharacterTestRuntimeAiLabConditionFieldKind.Fact:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), value.Fact))
                    {
                        diagnostic = "Condition fact is invalid.";
                        return false;
                    }
                    if (condition.Fact == value.Fact)
                        return NoChange(out changed, out diagnostic);
                    condition.Fact = value.Fact;
                    NormalizeConditionForFact(condition);
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.Operator:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionOperator), value.Operator)
                        || (!IsNumericFact(condition.Fact)
                            && value.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals
                            && value.Operator != CharacterTestRuntimeAiLabCardConditionOperator.NotEquals))
                    {
                        diagnostic = "Condition operator is invalid for the selected fact.";
                        return false;
                    }
                    if (condition.Operator == value.Operator)
                        return NoChange(out changed, out diagnostic);
                    condition.Operator = value.Operator;
                    if (value.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Between)
                        condition.MaxValue = 0f;
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.BoolValue:
                    if (IsNumericFact(condition.Fact))
                    {
                        diagnostic = "Numeric facts do not accept a boolean value.";
                        return false;
                    }
                    if (condition.BoolValue == value.BoolValue)
                        return NoChange(out changed, out diagnostic);
                    condition.BoolValue = value.BoolValue;
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.Value:
                    if (!IsNumericFact(condition.Fact) || !IsFinite(value.FloatValue))
                    {
                        diagnostic = "Numeric condition values must be finite.";
                        return false;
                    }
                    if (condition.Value.Equals(value.FloatValue))
                        return NoChange(out changed, out diagnostic);
                    condition.Value = value.FloatValue;
                    if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                        && condition.MaxValue < condition.Value)
                        condition.MaxValue = condition.Value;
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.Range:
                    if (!IsNumericFact(condition.Fact)
                        || !IsFinite(value.FloatValue)
                        || !IsFinite(value.SecondFloatValue)
                        || value.SecondFloatValue < value.FloatValue)
                    {
                        diagnostic = "Condition range is invalid.";
                        return false;
                    }
                    if (condition.Value.Equals(value.FloatValue) && condition.MaxValue.Equals(value.SecondFloatValue)
                        && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between)
                        return NoChange(out changed, out diagnostic);
                    condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between;
                    condition.Value = value.FloatValue;
                    condition.MaxValue = value.SecondFloatValue;
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.AbilityId:
                    if (condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                        || value.IntValue <= 0)
                    {
                        diagnostic = "AbilityAvailable requires a positive Ability ID.";
                        return false;
                    }
                    if (condition.AbilityId == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    condition.AbilityId = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabConditionFieldKind.CharacterActionId:
                    if (!CharacterActionId.TryParse(value.StringValue, out CharacterActionId actionId))
                    {
                        diagnostic = "SelfCanTransitionToAction requires a positive numeric CharacterAction ID.";
                        return false;
                    }
                    if (condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                        || !actionId.IsValid)
                    {
                        diagnostic = "SelfCanTransitionToAction requires a CharacterAction ID.";
                        return false;
                    }
                    if (condition.CharacterActionId == actionId)
                        return NoChange(out changed, out diagnostic);
                    condition.CharacterActionId = actionId;
                    break;
                default:
                    diagnostic = "Condition field is not supported.";
                    return false;
            }
            changed = true;
            diagnostic = "Condition field updated.";
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool TryAddTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            string requestedSourceStageId,
            string targetStageId,
            out string transitionId,
            out string diagnostic)
        {
            transitionId = string.Empty;
            CharacterTestRuntimeAiLabLogicDetector detector = FindDetector(payload, detectorId);
            if (detector == null)
            {
                diagnostic = "Detector stable ID is invalid.";
                return false;
            }
            string sourceStageId = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                ? detector.StageId
                : requestedSourceStageId?.Trim() ?? string.Empty;
            if (sourceStageId.Length > 0 && FindStage(payload, sourceStageId) == null)
            {
                diagnostic = "Transition source Stage does not exist.";
                return false;
            }
            string target = targetStageId?.Trim() ?? string.Empty;
            if (!string.Equals(target, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal)
                && FindStage(payload, target) == null)
            {
                diagnostic = "Transition target Stage does not exist.";
                return false;
            }

            // Native v5 Trigger Events have exactly one outcome. The legacy graph surface still
            // routes its old "connect transition" gesture through this adapter, so connecting a
            // v5 detector must replace that Event's outcome instead of creating a second static
            // Transition that the v5 canonicalizer would immediately discard.
            if (payload.Timelines.Count > 0)
            {
                CharacterTestRuntimeAiLabLogicTransition existing = null;
                for (int i = 0; i < payload.Transitions.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicTransition candidate = payload.Transitions[i];
                    if (candidate == null
                        || !string.Equals(candidate.DetectorId, detector.DetectorId, StringComparison.Ordinal)
                        || (sourceStageId.Length > 0
                            && !string.Equals(candidate.SourceStageId, sourceStageId, StringComparison.Ordinal)))
                    {
                        continue;
                    }
                    if (existing == null
                        || candidate.Priority > existing.Priority
                        || (candidate.Priority == existing.Priority
                            && string.CompareOrdinal(candidate.TransitionId, existing.TransitionId) < 0))
                    {
                        existing = candidate;
                    }
                }
                if (existing != null)
                {
                    existing.SourceStageId = sourceStageId;
                    existing.TargetStageId = target;
                    existing.Priority = detector.Priority;
                    existing.EndReason = string.Equals(
                        target,
                        CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal)
                            ? "ReturnToPlanner"
                            : string.Empty;
                    transitionId = existing.TransitionId;
                    diagnostic = "Trigger Event outcome updated.";
                    return true;
                }
            }
            transitionId = AllocateTransitionId(payload, detector.DetectorId + ".transition.");
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = transitionId,
                DetectorId = detector.DetectorId,
                SourceStageId = sourceStageId,
                TargetStageId = target,
                Priority = detector.Priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                Label = detector.Title,
                EndReason = string.Equals(target, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal)
                    ? "Completed"
                    : string.Empty
            });
            diagnostic = "Transition created.";
            return true;
        }

        internal static bool TryApplyTransitionField(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId,
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            in CharacterTestRuntimeAiLabTransitionFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            CharacterTestRuntimeAiLabLogicTransition transition = FindTransition(payload, transitionId);
            CharacterTestRuntimeAiLabLogicDetector detector = transition == null
                ? null
                : FindDetector(payload, transition.DetectorId);
            if (transition == null || detector == null)
            {
                diagnostic = "Transition stable ID or owning Detector is invalid.";
                return false;
            }
            switch (field)
            {
                case CharacterTestRuntimeAiLabTransitionFieldKind.Label:
                    string label = value.StringValue.Trim();
                    if (string.Equals(transition.Label, label, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    transition.Label = label;
                    break;
                case CharacterTestRuntimeAiLabTransitionFieldKind.SourceStage:
                    if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage)
                    {
                        if (!string.Equals(transition.SourceStageId, detector.StageId, StringComparison.Ordinal))
                        {
                            transition.SourceStageId = detector.StageId;
                            break;
                        }
                        return NoChange(out changed, out diagnostic);
                    }
                    string sourceId = value.StringValue.Trim();
                    if (sourceId.Length > 0 && FindStage(payload, sourceId) == null)
                    {
                        diagnostic = "Transition source Stage does not exist.";
                        return false;
                    }
                    if (string.Equals(transition.SourceStageId, sourceId, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    transition.SourceStageId = sourceId;
                    break;
                case CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage:
                    string targetId = value.StringValue.Trim();
                    if (!string.Equals(targetId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal)
                        && FindStage(payload, targetId) == null)
                    {
                        diagnostic = "Transition target Stage does not exist.";
                        return false;
                    }
                    if (string.Equals(transition.TargetStageId, targetId, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    transition.TargetStageId = targetId;
                    if (!string.Equals(targetId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal))
                        transition.EndReason = string.Empty;
                    break;
                case CharacterTestRuntimeAiLabTransitionFieldKind.Policy:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicTransitionPolicy), value.Policy))
                    {
                        diagnostic = "Transition policy is invalid.";
                        return false;
                    }
                    if (transition.Policy == value.Policy)
                        return NoChange(out changed, out diagnostic);
                    transition.Policy = value.Policy;
                    break;
                case CharacterTestRuntimeAiLabTransitionFieldKind.Priority:
                    if (transition.Priority == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    transition.Priority = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabTransitionFieldKind.EndReason:
                    if (!string.Equals(transition.TargetStageId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal))
                    {
                        diagnostic = "End reason is only available for an End transition.";
                        return false;
                    }
                    string reason = value.StringValue.Trim();
                    if (string.Equals(transition.EndReason, reason, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    transition.EndReason = reason;
                    break;
                default:
                    diagnostic = "Transition field is not supported.";
                    return false;
            }
            changed = true;
            diagnostic = "Transition field updated.";
            return true;
        }

        internal static bool TryDeleteTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId,
            out string diagnostic)
        {
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (!string.Equals(payload.Transitions[i]?.TransitionId, transitionId, StringComparison.Ordinal))
                    continue;
                payload.Transitions.RemoveAt(i);
                diagnostic = "Transition deleted.";
                return true;
            }
            diagnostic = "Transition stable ID is invalid.";
            return false;
        }

        private static void NormalizeConditionForFact(CharacterTestRuntimeAiLabCardCondition condition)
        {
            condition.BoolValue = true;
            condition.Value = 0f;
            condition.MaxValue = 0f;
            condition.AbilityId = 0;
            condition.CharacterActionId = CharacterActionId.None;
            condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals;
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                condition.AbilityId = 1;
        }

        private static CharacterTestRuntimeAiLabCardCondition CreateDefaultCondition(string conditionId)
        {
            return new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = conditionId,
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            };
        }

        private static bool IsNumericFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            return fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                || fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                || fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames;
        }

        private static bool CanBindPrimaryActionEnd(CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (stage == null)
                return false;
            if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                return stage.WaitFrames > 0;
            return (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                && Math.Max(stage.PlannedDurationFrames, stage.PreviewDurationFrames) > 0;
        }

        private static CharacterTestRuntimeAiLabLogicStage FindStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i]?.StageId, stageId, StringComparison.Ordinal))
                    return payload.Stages[i];
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicDetector FindDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            int index = FindDetectorIndex(payload, detectorId);
            return index >= 0 ? payload.Detectors[index] : null;
        }

        private static int FindDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; payload != null && i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i]?.DetectorId, detectorId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId)
        {
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i]?.TransitionId, transitionId, StringComparison.Ordinal))
                    return payload.Transitions[i];
            }
            return null;
        }

        private static int FindConditionIndex(
            CharacterTestRuntimeAiLabLogicDetector detector,
            string conditionId)
        {
            for (int i = 0; detector != null && i < detector.Conditions.Count; i++)
            {
                if (string.Equals(detector.Conditions[i]?.ConditionId, conditionId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int CountLinkedTransitions(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            int count = 0;
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i]?.DetectorId, detectorId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static void RewriteLinkedSources(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            string sourceStageId)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i]?.DetectorId, detectorId, StringComparison.Ordinal))
                    payload.Transitions[i].SourceStageId = sourceStageId;
            }
        }

        private static bool CanShiftLinkedPriorities(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            long delta)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (!string.Equals(transition?.DetectorId, detectorId, StringComparison.Ordinal))
                    continue;
                long candidate = (long)transition.Priority + delta;
                if (candidate < int.MinValue || candidate > int.MaxValue)
                    return false;
            }
            return true;
        }

        private static void ShiftLinkedPriorities(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            long delta)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (string.Equals(transition?.DetectorId, detectorId, StringComparison.Ordinal))
                    transition.Priority = (int)((long)transition.Priority + delta);
            }
        }

        private static string AllocateDetectorId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string prefix)
        {
            int suffix = 1;
            string candidate;
            do candidate = prefix + suffix++;
            while (FindDetector(payload, candidate) != null);
            return candidate;
        }

        private static string AllocateConditionId(CharacterTestRuntimeAiLabLogicDetector detector)
        {
            int suffix = 1;
            string candidate;
            do candidate = detector.DetectorId + ".condition." + suffix++;
            while (FindConditionIndex(detector, candidate) >= 0);
            return candidate;
        }

        private static string AllocateTransitionId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string prefix)
        {
            int suffix = 1;
            string candidate;
            do candidate = prefix + suffix++;
            while (FindTransition(payload, candidate) != null);
            return candidate;
        }

        private static bool NoChange(out bool changed, out string diagnostic)
        {
            changed = false;
            diagnostic = "Field value is unchanged.";
            return true;
        }
    }
}
