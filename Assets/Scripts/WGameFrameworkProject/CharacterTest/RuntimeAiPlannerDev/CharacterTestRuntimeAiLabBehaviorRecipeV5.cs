using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind
    {
        Predicate = 0,
        LocalFrame = 1,
        PrimaryActionSignal = 2
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor
    {
        AbsoluteFrame = 0,
        PrimaryActionEnd = 1
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal
    {
        Completed = 0,
        Failed = 1,
        Rejected = 2
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind
    {
        EnterTimeline = 0,
        ReturnToPlanner = 1
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin
    {
        Timeline = 0,
        Recipe = 1
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent
    {
        public string EventId { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> Conditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();

        public CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent { EventId = EventId };
            CloneConditions(Conditions, copy.Conditions);
            return copy;
        }

        internal static void CloneConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> source,
            List<CharacterTestRuntimeAiLabCardCondition> destination)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                destination.Add(source[i]?.Clone());
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent
    {
        public string EventId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; set; } =
            CharacterTestRuntimeAiLabCardStepKind.Logic;
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; set; }
        public CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; set; }
        public float MoveTargetPositionX { get; set; }
        public float MoveTargetPositionZ { get; set; }
        public int MoveTargetEntityId { get; set; }
        public float MoveMinDistance { get; set; }
        public float MoveMaxDistance { get; set; }
        public int WaitFrames { get; set; }
        public int FallbackDurationFrames { get; set; }

        public CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent Clone()
        {
            return (CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent)MemberwiseClone();
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string LegacySharedTriggerId { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Priority { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind SourceKind { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin ClockOrigin { get; set; }
        public int ActivationStartFrame { get; set; }
        public int ActivationDurationFrames { get; set; }
        public CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode { get; set; } =
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
        public bool ExpectedResult { get; set; } = true;
        public CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor LocalFrameAnchor { get; set; }
        public int LocalFrame { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal ActionSignal { get; set; }
        public int ActionSignalReasonCode { get; set; } = 0;
        public bool LegacyUnfilteredSignal { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind OutcomeKind { get; set; }
        public string TargetTimelineId { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> Conditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();

        public CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = EventId,
                LegacySharedTriggerId = LegacySharedTriggerId,
                TimelineId = TimelineId,
                Title = Title,
                Priority = Priority,
                SourceKind = SourceKind,
                ClockOrigin = ClockOrigin,
                ActivationStartFrame = ActivationStartFrame,
                ActivationDurationFrames = ActivationDurationFrames,
                WindowEndMode = WindowEndMode,
                ExpectedResult = ExpectedResult,
                LocalFrameAnchor = LocalFrameAnchor,
                LocalFrame = LocalFrame,
                ActionSignal = ActionSignal,
                ActionSignalReasonCode = ActionSignalReasonCode,
                LegacyUnfilteredSignal = LegacyUnfilteredSignal,
                OutcomeKind = OutcomeKind,
                TargetTimelineId = TargetTimelineId
            };
            CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent.CloneConditions(Conditions, copy.Conditions);
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeTimeline
    {
        public string TimelineId { get; set; } = string.Empty;
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent EntryEvent { get; set; } =
            new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent();
        public CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent PrimaryActionEvent { get; set; } =
            new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent();
        public List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent> TriggerEvents { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>();
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode EntryConditionRoot { get; set; }
        public List<CharacterTestRuntimeAiLabBehaviorRecipeEvent> Events { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>();

        public bool ShouldSerializeEntryEvent() => false;
        public bool ShouldSerializePrimaryActionEvent() => false;
        public bool ShouldSerializeTriggerEvents() => false;

        public CharacterTestRuntimeAiLabBehaviorRecipeTimeline Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = TimelineId,
                Order = Order,
                Title = Title,
                Note = Note,
                EntryConditionRoot = EntryConditionRoot?.Clone(),
                EntryEvent = EntryEvent?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent(),
                PrimaryActionEvent = PrimaryActionEvent?.Clone()
                    ?? new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent()
            };
            for (int i = 0; i < TriggerEvents.Count; i++)
                copy.TriggerEvents.Add(TriggerEvents[i]?.Clone());
            for (int i = 0; i < Events.Count; i++)
                copy.Events.Add(Events[i]?.Clone());
            return copy;
        }
    }

    /// <summary>
    /// Converts authenticated v4 Stage/Detector/Transition content into v5 Timeline/Event authority and keeps the
    /// current authoring surface available as a non-serialized compatibility projection.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV5Migration
    {
        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
                return;
            if (payload.Timelines.Count == 0 && payload.Stages.Count > 0)
                RebuildV5FromLegacy(payload);
            else if (payload.Timelines.Count > 0 && payload.Stages.Count == 0)
                RebuildLegacyProjection(payload);
            if (RemoveSupersededGeneratedActivationGuards(payload))
                RebuildLegacyProjection(payload);
            payload.CaptureLegacyProjectionFingerprint(ComputeLegacyProjectionFingerprint(payload));
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
                return;
            if (RemoveSupersededGeneratedActivationGuards(payload))
                RebuildLegacyProjection(payload);
            long fingerprint = ComputeLegacyProjectionFingerprint(payload);
            if (payload.Timelines.Count == 0
                || (payload.LegacyProjectionFingerprintInitialized
                    && payload.LegacyProjectionFingerprint != fingerprint))
            {
                RebuildV5FromLegacy(payload);
                fingerprint = ComputeLegacyProjectionFingerprint(payload);
            }
            payload.CaptureLegacyProjectionFingerprint(fingerprint);
        }

        internal static void RebuildV5FromLegacy(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            // Legacy action placeholders must be resolved while their v4-only fields still exist.
            // Native v5 intentionally does not carry LegacyActionKind.
            CharacterTestRuntimeAiLabBehaviorCardLogicJson.MigrateLegacyStages(payload);
            payload.BehaviorRecipeVersion = 0;
            payload.Timelines.Clear();
            payload.LegacySharedTriggerEvents.Clear();
            payload.MigrationDiagnostics.Clear();
            payload.EntryTimelineId = payload.EntryStageId ?? string.Empty;

            var ordered = new List<CharacterTestRuntimeAiLabLogicStage>(payload.Stages.Count);
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (payload.Stages[i] != null)
                    ordered.Add(payload.Stages[i]);
            }
            ordered.Sort(CompareStages);

            var timelineById = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(
                StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = ordered[i];
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
                {
                    TimelineId = stage.StageId ?? string.Empty,
                    Order = i,
                    Title = stage.Title ?? string.Empty,
                    Note = stage.Note ?? string.Empty,
                    EntryEvent = new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent
                    {
                        EventId = "entry." + (stage.StageId ?? string.Empty)
                    },
                    PrimaryActionEvent = ToPrimaryAction(stage)
                };
                CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent.CloneConditions(
                    stage.EntryConditions,
                    timeline.EntryEvent.Conditions);
                payload.Timelines.Add(timeline);
                if (!string.IsNullOrEmpty(timeline.TimelineId))
                    timelineById[timeline.TimelineId] = timeline;
            }

            for (int detectorIndex = 0; detectorIndex < payload.Detectors.Count; detectorIndex++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
                if (detector == null)
                    continue;
                // Older v5 drafts projected automatically generated activation guards through the
                // legacy facade. Entry eligibility no longer implies a runtime exit Event, so do
                // not migrate those obsolete projections back into native v5 authority.
                if (detector.IsBehaviorRecipeV5Event
                    && (detector.DetectorId ?? string.Empty).StartsWith(
                        "event.guard.",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card)
                {
                    AddLegacySharedEvents(payload, detector);
                    continue;
                }
                if (!timelineById.TryGetValue(detector.StageId ?? string.Empty, out var timeline))
                    continue;
                CharacterTestRuntimeAiLabLogicTransition transition = FindBestTransition(
                    payload.Transitions,
                    detector.DetectorId,
                    detector.StageId);
                if (transition != null)
                {
                    int transitionCount = CountTransitions(
                        payload.Transitions,
                        detector.DetectorId,
                        detector.StageId);
                    if (transitionCount > 1)
                    {
                        payload.MigrationDiagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                            CharacterTestRuntimeAiLabCardDiagnosticSeverity.Warning,
                            "CARD_V5_MIGRATION_TRANSITIONS_COLLAPSED",
                            "$.detectors[" + detectorIndex + "]",
                            "Legacy detector had " + transitionCount
                                + " static results; v5 retained the deterministic highest-priority result."));
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent migrated =
                        ToTriggerEvent(detector, transition, false);
                    if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted
                        && CanMigrateCompletionToPrimaryActionEnd(timeline.PrimaryActionEvent))
                    {
                        migrated.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame;
                        migrated.LocalFrameAnchor =
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd;
                        migrated.LocalFrame = 0;
                        migrated.LegacyUnfilteredSignal = false;
                    }
                    else if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted)
                    {
                        payload.MigrationDiagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                            CharacterTestRuntimeAiLabCardDiagnosticSeverity.Warning,
                            "CARD_V5_MIGRATION_COMPLETION_SIGNAL_COMPAT",
                            "$.detectors[" + detectorIndex + "]",
                            "Legacy completion trigger could not resolve an authoritative duration during migration and remains a compatibility signal."));
                    }
                    timeline.TriggerEvents.Add(migrated);
                }
            }
        }

        internal static void RebuildLegacyProjection(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.RebuildLegacyProjection(payload);
                return;
            }
            payload.Stages.Clear();
            payload.Detectors.Clear();
            payload.Transitions.Clear();
            payload.Layout.Stages.Clear();
            payload.EntryStageId = payload.EntryTimelineId ?? string.Empty;

            var ordered = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            ordered.Sort(CompareTimelines);
            int projectedStartFrame = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = ordered[i];
                if (timeline == null)
                    continue;
                CharacterTestRuntimeAiLabLogicStage projectedStage = ToLegacyStage(timeline, i);
                projectedStage.PlannedStartFrame = projectedStartFrame;
                payload.Stages.Add(projectedStage);
                projectedStartFrame = SaturatingAdd(
                    projectedStartFrame,
                    Math.Max(1, projectedStage.PlannedDurationFrames));
                payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = timeline.TimelineId ?? string.Empty,
                    Column = i,
                    Width = 1f
                });
                var orderedEvents = new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>(
                    timeline.TriggerEvents);
                orderedEvents.Sort(CompareEvents);
                for (int eventIndex = 0; eventIndex < orderedEvents.Count; eventIndex++)
                    AddLegacyEventProjection(payload, orderedEvents[eventIndex], timeline.TimelineId);
            }
            var orderedShared = new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>(
                payload.LegacySharedTriggerEvents);
            orderedShared.Sort(CompareEvents);
            for (int i = 0; i < orderedShared.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent shared =
                    orderedShared[i];
                AddLegacyEventProjection(payload, shared, shared?.TimelineId);
            }
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent ToPrimaryAction(
            CharacterTestRuntimeAiLabLogicStage stage)
        {
            // v5 Wait is a finite span. A legacy infinite "wait or hold" has no end event and is
            // represented by the semantically equivalent persistent Logic event instead of
            // smuggling an invalid zero-length Wait into the new authority.
            CharacterTestRuntimeAiLabCardStepKind kind = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    && (stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Infinite
                        || stage.WaitFrames <= 0)
                ? CharacterTestRuntimeAiLabCardStepKind.Logic
                : stage.Kind;
            return new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent
            {
                EventId = "primary." + (stage.StageId ?? string.Empty),
                Kind = kind,
                AbilityId = stage.AbilityId,
                CharacterActionId = stage.CharacterActionId,
                MovementStrategy = stage.MovementStrategy,
                MovementTargetKind = stage.MovementTargetKind,
                MoveTargetPositionX = stage.MoveTargetPositionX,
                MoveTargetPositionZ = stage.MoveTargetPositionZ,
                MoveTargetEntityId = stage.MoveTargetEntityId,
                MoveMinDistance = stage.MoveMinDistance,
                MoveMaxDistance = stage.MoveMaxDistance,
                WaitFrames = stage.WaitFrames,
                FallbackDurationFrames = kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    ? stage.WaitFrames
                    : Math.Max(stage.PlannedDurationFrames, stage.PreviewDurationFrames)
            };
        }

        private static CharacterTestRuntimeAiLabLogicStage ToLegacyStage(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            int order)
        {
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action =
                timeline.PrimaryActionEvent ?? new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent();
            var stage = new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = timeline.TimelineId ?? string.Empty,
                Order = order,
                Kind = action.Kind,
                Title = timeline.Title ?? string.Empty,
                Note = timeline.Note ?? string.Empty,
                AbilityId = action.AbilityId,
                CharacterActionId = action.CharacterActionId,
                MovementStrategy = action.MovementStrategy,
                MovementTargetKind = action.MovementTargetKind,
                MoveTargetPositionX = action.MoveTargetPositionX,
                MoveTargetPositionZ = action.MoveTargetPositionZ,
                MoveTargetEntityId = action.MoveTargetEntityId,
                MoveMinDistance = action.MoveMinDistance,
                MoveMaxDistance = action.MoveMaxDistance,
                WaitFrames = action.WaitFrames,
                PreviewDurationFrames = action.FallbackDurationFrames,
                PlannedDurationFrames = action.FallbackDurationFrames,
                EntryEventId = timeline.EntryEvent?.EventId ?? string.Empty,
                PrimaryActionEventId = action.EventId ?? string.Empty,
                IsBehaviorRecipeV5Timeline = true,
                DurationMode = action.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    || action.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                        ? CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent
                        : CharacterTestRuntimeAiLabLogicDurationMode.Fixed
            };
            CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent.CloneConditions(
                timeline.EntryEvent?.Conditions,
                stage.EntryConditions);
            return stage;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent ToTriggerEvent(
            CharacterTestRuntimeAiLabLogicDetector detector,
            CharacterTestRuntimeAiLabLogicTransition transition,
            bool legacyShared)
        {
            var result = new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = transition.TransitionId ?? detector.DetectorId ?? string.Empty,
                LegacySharedTriggerId = legacyShared ? detector.DetectorId ?? string.Empty : string.Empty,
                TimelineId = transition.SourceStageId ?? detector.StageId ?? string.Empty,
                Title = detector.Title ?? transition.Label ?? string.Empty,
                Priority = transition.Priority,
                ClockOrigin = legacyShared
                    ? CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                    : CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                ActivationStartFrame = detector.ActivationStartFrame,
                ActivationDurationFrames = detector.ActivationDurationFrames,
                WindowEndMode = detector.WindowEndMode,
                ExpectedResult = detector.ExpectedResult,
                ActionSignalReasonCode = detector.ActionSignalReasonCode,
                LegacyUnfilteredSignal = detector.LegacyUnfilteredSignal,
                OutcomeKind = string.Equals(
                    transition.TargetStageId,
                    CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                    StringComparison.Ordinal)
                        ? CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                        : CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                TargetTimelineId = string.Equals(
                    transition.TargetStageId,
                    CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                    StringComparison.Ordinal)
                        ? string.Empty
                        : transition.TargetStageId ?? string.Empty
            };
            CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent.CloneConditions(
                detector.Conditions,
                result.Conditions);

            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    result.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal;
                    result.ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed;
                    result.LegacyUnfilteredSignal = true;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    result.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal;
                    result.ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed;
                    result.LegacyUnfilteredSignal = true;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected:
                    result.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal;
                    result.ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected;
                    result.LegacyUnfilteredSignal = true;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    result.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame;
                    result.LocalFrameAnchor = detector.LocalFrameAnchor;
                    result.LocalFrame = detector.LocalFrame;
                    break;
                default:
                    result.SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate;
                    break;
            }
            return result;
        }

        private static int CountTransitions(
            IReadOnlyList<CharacterTestRuntimeAiLabLogicTransition> transitions,
            string detectorId,
            string sourceTimelineId)
        {
            int count = 0;
            for (int i = 0; transitions != null && i < transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition candidate = transitions[i];
                if (candidate != null
                    && string.Equals(candidate.DetectorId, detectorId, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(candidate.SourceStageId)
                        || string.Equals(candidate.SourceStageId, sourceTimelineId, StringComparison.Ordinal)))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool CanMigrateCompletionToPrimaryActionEnd(
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action)
        {
            if (action == null)
                return false;
            if (action.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                return action.WaitFrames > 0;
            return (action.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || action.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                && action.FallbackDurationFrames > 0;
        }

        private static void AddLegacySharedEvents(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicDetector detector)
        {
            var bestBySource = new Dictionary<string, CharacterTestRuntimeAiLabLogicTransition>(StringComparer.Ordinal);
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition candidate = payload.Transitions[i];
                if (candidate == null
                    || !string.Equals(candidate.DetectorId, detector.DetectorId, StringComparison.Ordinal))
                {
                    continue;
                }
                string source = candidate.SourceStageId ?? string.Empty;
                if (!bestBySource.TryGetValue(source, out var current) || CompareTransitions(candidate, current) < 0)
                    bestBySource[source] = candidate;
            }
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabLogicTransition> pair in bestBySource)
                payload.LegacySharedTriggerEvents.Add(ToTriggerEvent(detector, pair.Value, true));
        }

        private static bool RemoveSupersededGeneratedActivationGuards(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            bool removed = false;
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                for (int eventIndex = (timeline?.TriggerEvents.Count ?? 0) - 1; eventIndex >= 0; eventIndex--)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent candidate =
                        timeline.TriggerEvents[eventIndex];
                    if (candidate == null
                        || !(candidate.EventId ?? string.Empty).StartsWith("guard.", StringComparison.Ordinal)
                        || candidate.Conditions.Count != 1
                        || !HasEquivalentExplicitExit(
                            timeline,
                            candidate.Conditions[0],
                            eventIndex))
                    {
                        continue;
                    }
                    timeline.TriggerEvents.RemoveAt(eventIndex);
                    removed = true;
                }
            }
            return removed;
        }

        private static bool HasEquivalentExplicitExit(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabCardCondition activationCondition,
            int exceptEventIndex)
        {
            for (int eventIndex = 0; timeline != null && eventIndex < timeline.TriggerEvents.Count; eventIndex++)
            {
                if (eventIndex == exceptEventIndex)
                    continue;
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent candidate = timeline.TriggerEvents[eventIndex];
                if (candidate == null
                    || (candidate.EventId ?? string.Empty).StartsWith("guard.", StringComparison.Ordinal)
                    || candidate.SourceKind != CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate
                    || candidate.ExpectedResult
                    || candidate.OutcomeKind != CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                    || candidate.Conditions.Count != 1
                    || !ConditionsHaveSameMeaning(candidate.Conditions[0], activationCondition))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private static bool ConditionsHaveSameMeaning(
            CharacterTestRuntimeAiLabCardCondition left,
            CharacterTestRuntimeAiLabCardCondition right)
        {
            return left != null
                && right != null
                && left.Fact == right.Fact
                && left.Operator == right.Operator
                && left.BoolValue == right.BoolValue
                && BitConverter.SingleToInt32Bits(left.Value) == BitConverter.SingleToInt32Bits(right.Value)
                && BitConverter.SingleToInt32Bits(left.MaxValue) == BitConverter.SingleToInt32Bits(right.MaxValue)
                && left.AbilityId == right.AbilityId
                && left.CharacterActionId == right.CharacterActionId;
        }

        private static void AddLegacyEventProjection(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent source,
            string timelineId)
        {
            if (source == null)
                return;
            string eventId = source.EventId ?? string.Empty;
            string detectorId = "event." + eventId;
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = detectorId,
                LegacySharedTriggerId = source.LegacySharedTriggerId ?? string.Empty,
                Title = source.Title ?? string.Empty,
                Scope = source.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                    ? CharacterTestRuntimeAiLabLogicDetectorScope.Card
                    : CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = timelineId ?? string.Empty,
                Trigger = ToLegacyTrigger(source),
                Priority = source.Priority,
                ActivationStartFrame = source.ActivationStartFrame,
                ActivationDurationFrames = source.ActivationDurationFrames,
                WindowEndMode = source.WindowEndMode,
                ExpectedResult = source.ExpectedResult,
                LocalFrameAnchor = source.LocalFrameAnchor,
                LocalFrame = source.LocalFrame,
                ActionSignalReasonCode = source.ActionSignalReasonCode,
                LegacyUnfilteredSignal = source.LegacyUnfilteredSignal,
                IsBehaviorRecipeV5Event = true
            };
            CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent.CloneConditions(source.Conditions, detector.Conditions);
            payload.Detectors.Add(detector);
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = eventId,
                DetectorId = detectorId,
                SourceStageId = timelineId ?? string.Empty,
                TargetStageId = source.OutcomeKind == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                    ? CharacterTestRuntimeAiLabLogicTransition.EndTarget
                    : source.TargetTimelineId ?? string.Empty,
                Priority = source.Priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                Label = source.Title ?? string.Empty,
                IsBehaviorRecipeV5Event = true
            });
        }

        private static CharacterTestRuntimeAiLabLogicDetectorTrigger ToLegacyTrigger(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent source)
        {
            switch (source.SourceKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame:
                    return CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal:
                    switch (source.ActionSignal)
                    {
                        case CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected:
                            return CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected;
                        case CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed:
                            return CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed;
                        default:
                            return CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted;
                    }
                default:
                    return CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
            }
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindBestTransition(
            IReadOnlyList<CharacterTestRuntimeAiLabLogicTransition> transitions,
            string detectorId,
            string sourceTimelineId)
        {
            CharacterTestRuntimeAiLabLogicTransition best = null;
            for (int i = 0; transitions != null && i < transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition candidate = transitions[i];
                if (candidate == null
                    || !string.Equals(candidate.DetectorId, detectorId, StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(candidate.SourceStageId)
                        && !string.Equals(candidate.SourceStageId, sourceTimelineId, StringComparison.Ordinal)))
                {
                    continue;
                }
                if (best == null || CompareTransitions(candidate, best) < 0)
                    best = candidate;
            }
            return best;
        }

        private static int CompareTransitions(
            CharacterTestRuntimeAiLabLogicTransition left,
            CharacterTestRuntimeAiLabLogicTransition right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : string.CompareOrdinal(left.TransitionId, right.TransitionId);
        }

        private static int CompareStages(
            CharacterTestRuntimeAiLabLogicStage left,
            CharacterTestRuntimeAiLabLogicStage right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.StageId, right.StageId);
        }

        private static int CompareTimelines(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline left,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.TimelineId, right.TimelineId);
        }

        private static int CompareEvents(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent left,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId);
        }

        private static int SaturatingAdd(int left, int right)
        {
            long result = (long)left + right;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        private static long ComputeLegacyProjectionFingerprint(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("legacy.entry", payload.EntryStageId);
            hash.AddInt("legacy.stage.count", payload.Stages.Count);
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage == null)
                    continue;
                hash.AddStringStable("legacy.stage.id", stage.StageId);
                hash.AddInt("legacy.stage.order", stage.Order);
                hash.AddStringStable("legacy.stage.title", stage.Title);
                hash.AddStringStable("legacy.stage.note", stage.Note);
                hash.AddInt("legacy.stage.kind", (int)stage.Kind);
                hash.AddInt("legacy.stage.legacyKind", (int)stage.LegacyActionKind);
                hash.AddInt("legacy.stage.ability", stage.AbilityId);
                hash.AddInt("legacy.stage.action", stage.CharacterActionId.Value);
                hash.AddInt("legacy.stage.movementStrategy", (int)stage.MovementStrategy);
                hash.AddInt("legacy.stage.movementTarget", (int)stage.MovementTargetKind);
                hash.AddInt("legacy.stage.targetX", BitConverter.SingleToInt32Bits(stage.MoveTargetPositionX));
                hash.AddInt("legacy.stage.targetZ", BitConverter.SingleToInt32Bits(stage.MoveTargetPositionZ));
                hash.AddInt("legacy.stage.targetEntity", stage.MoveTargetEntityId);
                hash.AddInt("legacy.stage.moveMin", BitConverter.SingleToInt32Bits(stage.MoveMinDistance));
                hash.AddInt("legacy.stage.moveMax", BitConverter.SingleToInt32Bits(stage.MoveMaxDistance));
                hash.AddInt("legacy.stage.wait", stage.WaitFrames);
                hash.AddInt("legacy.stage.duration", stage.PlannedDurationFrames);
                hash.AddInt("legacy.stage.previewDuration", stage.PreviewDurationFrames);
                hash.AddInt("legacy.stage.durationMode", (int)stage.DurationMode);
                hash.AddInt("legacy.stage.entry.count", stage.EntryConditions.Count);
                AddConditionsFingerprint(ref hash, stage.EntryConditions);
            }
            hash.AddInt("legacy.detector.count", payload.Detectors.Count);
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null)
                    continue;
                hash.AddStringStable("legacy.detector.id", detector.DetectorId);
                hash.AddStringStable("legacy.detector.sharedId", detector.LegacySharedTriggerId);
                hash.AddStringStable("legacy.detector.title", detector.Title);
                hash.AddInt("legacy.detector.scope", (int)detector.Scope);
                hash.AddStringStable("legacy.detector.stage", detector.StageId);
                hash.AddInt("legacy.detector.trigger", (int)detector.Trigger);
                hash.AddInt("legacy.detector.priority", detector.Priority);
                hash.AddInt("legacy.detector.start", detector.ActivationStartFrame);
                hash.AddInt("legacy.detector.duration", detector.ActivationDurationFrames);
                hash.AddInt("legacy.detector.windowEnd", (int)detector.WindowEndMode);
                hash.AddInt("legacy.detector.expected", detector.ExpectedResult ? 1 : 0);
                hash.AddInt("legacy.detector.localFrameAnchor", (int)detector.LocalFrameAnchor);
                hash.AddInt("legacy.detector.localFrame", detector.LocalFrame);
                hash.AddInt("legacy.detector.reason", detector.ActionSignalReasonCode);
                hash.AddInt("legacy.detector.unfilteredSignal", detector.LegacyUnfilteredSignal ? 1 : 0);
                AddConditionsFingerprint(ref hash, detector.Conditions);
            }
            hash.AddInt("legacy.transition.count", payload.Transitions.Count);
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (transition == null)
                    continue;
                hash.AddStringStable("legacy.transition.id", transition.TransitionId);
                hash.AddStringStable("legacy.transition.detector", transition.DetectorId);
                hash.AddStringStable("legacy.transition.source", transition.SourceStageId);
                hash.AddStringStable("legacy.transition.target", transition.TargetStageId);
                hash.AddInt("legacy.transition.priority", transition.Priority);
                hash.AddInt("legacy.transition.policy", (int)transition.Policy);
                hash.AddStringStable("legacy.transition.label", transition.Label);
                hash.AddStringStable("legacy.transition.endReason", transition.EndReason);
            }
            AddConditionsFingerprint(ref hash, payload.ActivationConditions);
            return hash.ToHash();
        }

        private static void AddConditionsFingerprint(
            ref RuntimeContentHashAccumulator hash,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            hash.AddInt("legacy.condition.count", conditions?.Count ?? 0);
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition == null)
                    continue;
                hash.AddStringStable("legacy.condition.id", condition.ConditionId);
                hash.AddInt("legacy.condition.fact", (int)condition.Fact);
                hash.AddInt("legacy.condition.op", (int)condition.Operator);
                hash.AddInt("legacy.condition.bool", condition.BoolValue ? 1 : 0);
                hash.AddInt("legacy.condition.value", (int)Math.Round(condition.Value * 1000f));
                hash.AddInt("legacy.condition.max", (int)Math.Round(condition.MaxValue * 1000f));
                hash.AddInt("legacy.condition.ability", condition.AbilityId);
                hash.AddInt("legacy.condition.action", condition.CharacterActionId.Value);
            }
        }
    }

    /// <summary>
    /// Produces the native-v5 form of formal project cards. Legacy factories remain useful as
    /// concise source fixtures, but their published authority is always Timeline/Event data.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV5Templates
    {
        internal const string FoundationApproachAndAttackStableCardId =
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                .FoundationApproachAndAttack;

        internal static void ConfigureNativeCard(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stableCardId)
        {
            if (payload == null)
                return;
            bool continuousApproach = string.Equals(
                stableCardId,
                FoundationApproachAndAttackStableCardId,
                StringComparison.Ordinal);

            // Once a payload has crossed the v8 authority boundary its legacy Stage projection is
            // intentionally lossy. Rebuilding native data from that projection would discard every
            // unified Event operation. Generated cards must therefore stay on the native path.
            if (CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.HasV8Authority(payload))
            {
                if (continuousApproach && payload.Timelines.Count > 0)
                {
                    payload.Timelines[0].Title = "持续靠近";
                    ConfigureNativeV7ContinuousApproach(payload, payload.Timelines[0].TimelineId);
                }
                else
                {
                    ConfigureGeneratedV7FixedIntervals(payload, stableCardId);
                }
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                return;
            }

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(payload);
            if (continuousApproach && payload.Timelines.Count > 0)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
                timeline.Title = "持续靠近";
                timeline.TriggerEvents.Clear();
                AddPredicateReturnEvent(
                    timeline,
                    payload.ActivationConditions,
                    CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    "event.target-distance-at-most-stop",
                    "目标距离 ≤ 1.5 米");
                AddPredicateReturnEvent(
                    timeline,
                    payload.ActivationConditions,
                    CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable,
                    "event.move-path-unavailable",
                    "可用移动路径 = 否");
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForRead(payload);
            if (continuousApproach && payload.Timelines.Count > 0)
                ConfigureNativeV7ContinuousApproach(payload, payload.Timelines[0].TimelineId);
            else
                ConfigureGeneratedV7FixedIntervals(payload, stableCardId);
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);
        }

        internal static bool ConfigureGeneratedV7FixedIntervals(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stableCardId = null)
        {
            if (payload == null)
                return false;
            bool changed = EnsureGeneratedFallbackWaitEvent(payload, stableCardId);
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                if (timeline == null)
                    continue;
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt?.Operation == null)
                        continue;
                    CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind previousKind =
                        evt.Operation.Kind;
                    int previousDuration = evt.DurationFrames;
                    int previousExecutions = evt.MaxExecutionsPerPass;
                    bool previousReview = evt.DurationReviewRequired;
                    CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode previousMode =
                        evt.DurationMode;
                    evt.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
                    evt.DurationReviewRequired = false;
                    evt.MaxExecutionsPerPass = Math.Max(1, evt.MaxExecutionsPerEntry);
                    evt.DurationFrames = Math.Max(
                        evt.DurationFrames,
                        evt.Operation.Kind is CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                            or CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                            or CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                                ? 30
                                : 1);
                    changed |= previousKind != evt.Operation.Kind
                        || previousDuration != evt.DurationFrames
                        || previousExecutions != evt.MaxExecutionsPerPass
                        || previousReview != evt.DurationReviewRequired
                        || previousMode != evt.DurationMode;
                }
            }
            return changed;
        }

        private static bool EnsureGeneratedFallbackWaitEvent(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stableCardId)
        {
            if (!string.Equals(
                    stableCardId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal))
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = null;
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate =
                    payload.Timelines[timelineIndex];
                if (candidate == null)
                    continue;
                if (candidate.Events.Count > 0)
                    return false;
                timeline ??= candidate;
            }
            if (timeline == null)
                return false;

            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.fallback.wait",
                TimelineId = timeline.TimelineId ?? string.Empty,
                Title = "Wait or hold",
                StartFrame = 0,
                DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed,
                DurationFrames = 1,
                MaxExecutionsPerEntry = 1,
                MaxExecutionsPerPass = 1,
                CooldownFrames = 0,
                Priority = 0,
                DurationReviewRequired = false,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                }
            });
            return true;
        }

        private static void ConfigureNativeV7ContinuousApproach(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = null;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                {
                    timeline = payload.Timelines[i];
                    break;
                }
            }
            if (timeline == null)
                return;

            CharacterTestRuntimeAiLabBehaviorRecipeEvent move = null;
            var exitConditions = new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>();
            for (int i = 0; i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[i];
                if (evt?.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                    move = evt.Clone();
                else if (evt?.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                    && evt.ConditionRoot != null)
                    exitConditions.Add(evt.ConditionRoot.Clone());
            }

            move ??= new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.approach",
                TimelineId = timeline.TimelineId,
                Title = "接近目标",
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    MoveMinDistance = 1.5f,
                    MoveMaxDistance = 999f
                }
            };
            move.StartFrame = 0;
            move.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
            move.DurationFrames = 30;
            move.ConditionRoot = null;
            move.MaxExecutionsPerPass = 1;
            move.DurationReviewRequired = false;

            var exitRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.loop.exit.any",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
            };
            for (int i = 0; i < exitConditions.Count; i++)
                exitRoot.Children.Add(exitConditions[i]);

            var loop = new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.approach.loop",
                TimelineId = timeline.TimelineId,
                Title = "持续接敌循环",
                StartFrame = 0,
                DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed,
                DurationFrames = 30,
                ConditionRoot = exitRoot.Children.Count > 0 ? exitRoot : null,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                    MaxLoopRepeats = 0
                }
            };
            timeline.Events.Clear();
            timeline.Events.Add(move);
            timeline.Events.Add(loop);
        }

        private static void AddPredicateReturnEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> activationConditions,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            string eventId,
            string title)
        {
            for (int i = 0; activationConditions != null && i < activationConditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition source = activationConditions[i];
                if (source == null || source.Fact != fact)
                    continue;
                CharacterTestRuntimeAiLabCardCondition condition = source.Clone();
                condition.ConditionId = eventId + ".condition";
                var trigger = new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
                {
                    EventId = eventId,
                    TimelineId = timeline.TimelineId,
                    Title = title,
                    Priority = 1000 - i,
                    SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate,
                    ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                    WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                    ExpectedResult = false,
                    OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                };
                trigger.Conditions.Add(condition);
                timeline.TriggerEvents.Add(trigger);
                return;
            }
        }
    }
}
