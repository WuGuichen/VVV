using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabCompiledRecipeV6Condition
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledRecipeV6Condition> _children;

        public CharacterTestRuntimeAiLabCompiledRecipeV6Condition(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind kind,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabCompiledCondition? predicate,
            int sourceEventIndex,
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal,
            CharacterTestRuntimeAiLabCompiledRecipeV6Condition[] children)
        {
            Kind = kind;
            Subject = subject;
            Predicate = predicate;
            SourceEventIndex = sourceEventIndex;
            Signal = signal;
            _children = Array.AsReadOnly(children ?? Array.Empty<CharacterTestRuntimeAiLabCompiledRecipeV6Condition>());
        }

        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind Kind { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Subject { get; }
        public CharacterTestRuntimeAiLabCompiledCondition? Predicate { get; }
        public int SourceEventIndex { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeEventSignal Signal { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledRecipeV6Condition> Children => _children;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledRecipeV6Event
    {
        public CharacterTestRuntimeAiLabCompiledRecipeV6Event(
            int index,
            int timelineIndex,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent source,
            CharacterTestRuntimeAiLabCompiledRecipeV6Condition condition,
            CharacterTestRuntimeAiLabCompiledLogicStage actionStage,
            int targetTimelineIndex)
        {
            Index = index;
            TimelineIndex = timelineIndex;
            EventId = source?.EventId ?? string.Empty;
            TimingKind = source?.TimingKind ?? CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
            StartFrame = source?.StartFrame ?? 0;
            DurationFrames = source?.DurationFrames ?? 1;
            EndFrame = StartFrame + DurationFrames;
            MaxExecutionsPerPass = source?.MaxExecutionsPerPass ?? 1;
            CooldownFrames = source?.CooldownFrames ?? 0;
            MaxLoopRepeats = source?.Operation?.MaxLoopRepeats ?? 0;
            Priority = source?.Priority ?? 0;
            ClockOrigin = source?.ClockOrigin ?? CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline;
            LegacySharedEventId = source?.LegacySharedEventId ?? string.Empty;
            OperationKind = source?.Operation?.Kind ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            Condition = condition;
            ActionStage = actionStage;
            TargetTimelineIndex = targetTimelineIndex;
        }

        public int Index { get; }
        public int TimelineIndex { get; }
        public string EventId { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind TimingKind { get; }
        public bool IsInstant => TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
        public bool IsDuration => TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
        public int StartFrame { get; }
        public int DurationFrames { get; }
        public int EndFrame { get; }
        public int MaxExecutionsPerPass { get; }
        public int CooldownFrames { get; }
        public int MaxLoopRepeats { get; }
        public int Priority { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin ClockOrigin { get; }
        public string LegacySharedEventId { get; }
        public bool IsLegacyShared => ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
            && !string.IsNullOrEmpty(LegacySharedEventId);
        public CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind OperationKind { get; }
        public CharacterTestRuntimeAiLabCompiledRecipeV6Condition Condition { get; }
        public CharacterTestRuntimeAiLabCompiledLogicStage ActionStage { get; }
        public int TargetTimelineIndex { get; }
        public bool IsTimelineControl => OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
            || OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner;
        public bool IsWaitControl => OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition;
        public bool IsLoopControl => OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion;
        public bool UsesActorControl => OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
            || OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
            || OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledRecipeV6Timeline
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledRecipeV6Event> _events;

        public CharacterTestRuntimeAiLabCompiledRecipeV6Timeline(
            int index,
            string timelineId,
            CharacterTestRuntimeAiLabCompiledRecipeV6Condition entryCondition,
            CharacterTestRuntimeAiLabCompiledLogicStage headerStage,
            CharacterTestRuntimeAiLabCompiledRecipeV6Event[] events,
            int durationFrames)
        {
            Index = index;
            TimelineId = timelineId ?? string.Empty;
            EntryCondition = entryCondition;
            HeaderStage = headerStage;
            DurationFrames = Math.Max(0, durationFrames);
            _events = Array.AsReadOnly(events ?? Array.Empty<CharacterTestRuntimeAiLabCompiledRecipeV6Event>());
        }

        public int Index { get; }
        public string TimelineId { get; }
        public CharacterTestRuntimeAiLabCompiledRecipeV6Condition EntryCondition { get; }
        public CharacterTestRuntimeAiLabCompiledLogicStage HeaderStage { get; }
        public int DurationFrames { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledRecipeV6Event> Events => _events;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledRecipeV6
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledRecipeV6Timeline> _timelines;
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledRecipeV6Event> _events;

        public CharacterTestRuntimeAiLabCompiledRecipeV6(
            int entryTimelineIndex,
            CharacterTestRuntimeAiLabCompiledRecipeV6Condition activationCondition,
            CharacterTestRuntimeAiLabCompiledRecipeV6Timeline[] timelines,
            CharacterTestRuntimeAiLabCompiledRecipeV6Event[] events)
        {
            EntryTimelineIndex = entryTimelineIndex;
            ActivationCondition = activationCondition;
            _timelines = Array.AsReadOnly(timelines ?? Array.Empty<CharacterTestRuntimeAiLabCompiledRecipeV6Timeline>());
            _events = Array.AsReadOnly(events ?? Array.Empty<CharacterTestRuntimeAiLabCompiledRecipeV6Event>());
        }

        public int EntryTimelineIndex { get; }
        public CharacterTestRuntimeAiLabCompiledRecipeV6Condition ActivationCondition { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledRecipeV6Timeline> Timelines => _timelines;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledRecipeV6Event> Events => _events;
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler
    {
        private const int MaxConditionDepth = 8;
        private const int MaxConditionLeaves = 64;

        internal static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Compile(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            bool executionOnly = false,
            bool includeDebugMap = true)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (payload == null)
            {
                Error(diagnostics, "CARD_V6_PAYLOAD_MISSING", "$", "Behavior recipe v11 payload is required.");
                return Failed(diagnostics);
            }

            // Normalization and authoritative duration synchronization must never mutate the author's object.
            payload = payload.Clone();
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            SynchronizeCharacterActionEventsForPublication(payload, references);
            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            if (timelines.Count == 0)
                Error(diagnostics, "CARD_V6_TIMELINE_MISSING", "$.timelines", "At least one Timeline is required.");

            var timelineIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var eventIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var eventOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            int eventCount = 0;
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                string path = "$.timelines[" + i + "]";
                if (timeline == null || string.IsNullOrWhiteSpace(timeline.TimelineId))
                {
                    Error(diagnostics, "CARD_V6_TIMELINE_ID_MISSING", path + ".timelineId", "TimelineId is required.");
                    continue;
                }
                if (!timelineIndices.TryAdd(timeline.TimelineId, i))
                    Error(diagnostics, "CARD_V6_TIMELINE_ID_DUPLICATE", path + ".timelineId", "TimelineId must be unique.");
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[j];
                    string eventPath = path + ".events[" + j + "]";
                    if (evt == null || string.IsNullOrWhiteSpace(evt.EventId))
                    {
                        Error(diagnostics, "CARD_V6_EVENT_ID_MISSING", eventPath + ".eventId", "EventId is required.");
                        continue;
                    }
                    if (!eventIndices.TryAdd(evt.EventId, eventCount))
                        Error(diagnostics, "CARD_V6_EVENT_ID_DUPLICATE", eventPath + ".eventId", "EventId must be unique in the recipe.");
                    else
                    {
                        eventOwners.Add(evt.EventId, timeline.TimelineId);
                        eventCount++;
                    }
                }
            }

            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                string eventPath = "$.legacySharedEvents[" + i + "]";
                if (evt == null || string.IsNullOrWhiteSpace(evt.EventId))
                {
                    Error(diagnostics, "CARD_V6_EVENT_ID_MISSING", eventPath + ".eventId", "EventId is required.");
                    continue;
                }
                if (!timelineIndices.ContainsKey(evt.TimelineId ?? string.Empty))
                {
                    Error(diagnostics, "CARD_V6_SHARED_TIMELINE_UNKNOWN", eventPath + ".timelineId", "Legacy shared Event must reference an existing Timeline.");
                    continue;
                }
                if (!eventIndices.TryAdd(evt.EventId, eventCount))
                {
                    Error(diagnostics, "CARD_V6_EVENT_ID_DUPLICATE", eventPath + ".eventId", "EventId must be unique in the recipe.");
                    continue;
                }
                eventOwners.Add(evt.EventId, evt.TimelineId);
                eventCount++;
            }

            if (!timelineIndices.TryGetValue(payload.EntryTimelineId ?? string.Empty, out int entryTimelineIndex))
                Error(diagnostics, "CARD_V6_ENTRY_TIMELINE_UNKNOWN", "$.entryTimelineId", "EntryTimelineId must reference an existing Timeline.");

            ValidateCondition(payload.ActivationConditionRoot, "$.activationConditionRoot", false, null, null, eventIndices, eventOwners, references, diagnostics);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                if (timeline == null)
                    continue;
                string path = "$.timelines[" + i + "]";
                ValidateCondition(timeline.EntryConditionRoot, path + ".entryConditionRoot", false, timeline.TimelineId, null, eventIndices, eventOwners, references, diagnostics);
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    ValidateEvent(timeline, timeline.Events[j], path + ".events[" + j + "]", timelineIndices, eventIndices, eventOwners, references, diagnostics);
                    if (executionOnly && references == null && timeline.Events[j]?.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                        Error(diagnostics, "CARD_EXECUTION_ACTION_CATALOG_MISSING", path + ".events[" + j + "].operation.characterActionId", "Execution compilation requires loaded action dependencies.");
                }
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline owner = FindTimeline(timelines, evt?.TimelineId);
                if (owner != null)
                    ValidateEvent(owner, evt, "$.legacySharedEvents[" + i + "]", timelineIndices, eventIndices, eventOwners, references, diagnostics);
            }
            ValidateReachability(payload.EntryTimelineId, timelines, payload.LegacySharedEvents, timelineIndices, diagnostics);
            ValidateSignalCycles(timelines, payload.LegacySharedEvents, eventIndices, diagnostics);
            ValidateTimeControls(timelines, diagnostics);
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            if (executionOnly)
            {
                try
                {
                    CharacterTestBehaviorExecutionProgram execution = CharacterTestBehaviorExecutionCompiler.CompileValidated(
                        payload, references, out CharacterTestBehaviorExecutionDebugMap debugMap);
                    return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                        new CharacterTestRuntimeAiLabCompiledLogicProgram(
                            CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV11(payload), -1,
                            Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>(), Array.Empty<CharacterTestRuntimeAiLabCompiledLogicStage>(),
                            Array.Empty<CharacterTestRuntimeAiLabCompiledLogicDetector>(), Array.Empty<CharacterTestRuntimeAiLabCompiledLogicTransition>(),
                            false, execution: execution, executionDebugMap: includeDebugMap ? debugMap : null), diagnostics.ToArray());
                }
                catch (ArgumentException error)
                {
                    Error(diagnostics, "CARD_EXECUTION_INVALID", "$", error.Message);
                    return Failed(diagnostics);
                }
            }

            var compiledEvents = new CharacterTestRuntimeAiLabCompiledRecipeV6Event[eventCount];
            var compiledTimelines = new CharacterTestRuntimeAiLabCompiledRecipeV6Timeline[timelines.Count];
            var headerStages = new CharacterTestRuntimeAiLabCompiledLogicStage[timelines.Count];
            for (int i = 0; i < timelines.Count; i++)
                headerStages[i] = CompileHeaderStage(timelines[i], i);

            var compiledSharedEvents = new CharacterTestRuntimeAiLabCompiledRecipeV6Event[payload.LegacySharedEvents.Count];
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent source = payload.LegacySharedEvents[i];
                int eventIndex = eventIndices[source.EventId];
                int ownerTimelineIndex = timelineIndices[source.TimelineId];
                int targetTimelineIndex = source.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                    ? timelineIndices[source.Operation.TargetTimelineId]
                    : -1;
                var compiled = new CharacterTestRuntimeAiLabCompiledRecipeV6Event(
                    eventIndex,
                    ownerTimelineIndex,
                    source,
                    CompileCondition(source.ConditionRoot, eventIndices),
                    CompileActionStage(timelines[ownerTimelineIndex], source, ownerTimelineIndex),
                    targetTimelineIndex);
                compiledEvents[eventIndex] = compiled;
                compiledSharedEvents[i] = compiled;
            }

            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                var localEvents = new CharacterTestRuntimeAiLabCompiledRecipeV6Event[
                    timeline.Events.Count + compiledSharedEvents.Length];
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent source = timeline.Events[j];
                    int eventIndex = eventIndices[source.EventId];
                    int targetTimelineIndex = source.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                        ? timelineIndices[source.Operation.TargetTimelineId]
                        : -1;
                    var compiled = new CharacterTestRuntimeAiLabCompiledRecipeV6Event(
                        eventIndex,
                        i,
                        source,
                        CompileCondition(source.ConditionRoot, eventIndices),
                        CompileActionStage(timeline, source, i),
                        targetTimelineIndex);
                    compiledEvents[eventIndex] = compiled;
                    localEvents[j] = compiled;
                }
                for (int j = 0; j < compiledSharedEvents.Length; j++)
                    localEvents[timeline.Events.Count + j] = compiledSharedEvents[j];
                Array.Sort(localEvents, CompareCompiledEvents);
                int durationFrames = 0;
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent source = timeline.Events[j];
                    if (source != null)
                        durationFrames = Math.Max(durationFrames, source.StartFrame + source.DurationFrames);
                }
                compiledTimelines[i] = new CharacterTestRuntimeAiLabCompiledRecipeV6Timeline(
                    i,
                    timeline.TimelineId,
                    CompileCondition(timeline.EntryConditionRoot, eventIndices),
                    headerStages[i],
                    localEvents,
                    durationFrames);
            }

            var recipe = new CharacterTestRuntimeAiLabCompiledRecipeV6(
                entryTimelineIndex,
                CompileCondition(payload.ActivationConditionRoot, eventIndices),
                compiledTimelines,
                compiledEvents);
            var allStages = new List<CharacterTestRuntimeAiLabCompiledLogicStage>(headerStages);
            for (int i = 0; i < compiledEvents.Length; i++)
            {
                if (compiledEvents[i]?.ActionStage != null)
                    allStages.Add(compiledEvents[i].ActionStage);
            }
            return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                new CharacterTestRuntimeAiLabCompiledLogicProgram(
                    CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV11(payload),
                    entryTimelineIndex,
                    Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>(),
                    allStages.ToArray(),
                    Array.Empty<CharacterTestRuntimeAiLabCompiledLogicDetector>(),
                    Array.Empty<CharacterTestRuntimeAiLabCompiledLogicTransition>(),
                    isBehaviorRecipeV5: false,
                    recipeV6: recipe),
                diagnostics.ToArray());
        }

        private static void ValidateEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            string path,
            IReadOnlyDictionary<string, int> timelineIndices,
            IReadOnlyDictionary<string, int> eventIndices,
            IReadOnlyDictionary<string, string> eventOwners,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (evt == null)
                return;
            if (evt.StartFrame < 0)
                Error(diagnostics, "CARD_V6_EVENT_START_INVALID", path + ".startFrame", "StartFrame cannot be negative.");
            if (evt.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                && evt.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration)
                Error(diagnostics, "CARD_V8_EVENT_TIMING_INVALID", path + ".timingKind", "Event timing must be Instant or Duration.");
            if (evt.DurationFrames < 1)
                Error(diagnostics, "CARD_V6_EVENT_DURATION_INVALID", path + ".durationFrames", "DurationFrames must be at least 1f.");
            if (evt.StartFrame >= 0 && evt.DurationFrames > 0 && evt.StartFrame > int.MaxValue - evt.DurationFrames)
                Error(diagnostics, "CARD_EXECUTION_TIME_OVERFLOW", path + ".durationFrames", "Event end frame exceeds the supported frame range.");
            if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                && (evt.DurationFrames != 1 || evt.MaxExecutionsPerPass != 1 || evt.CooldownFrames != 0))
                Error(diagnostics, "CARD_V8_INSTANT_NOT_CANONICAL", path, "Instant Events must use 1f, one execution per pass, and zero cooldown.");
            if (evt.DurationReviewRequired)
                Error(diagnostics, "CARD_V7_EVENT_DURATION_REVIEW_REQUIRED", path + ".durationReviewRequired", "This migrated Event used an open interval. Confirm its fixed duration before running or publishing.");
            if (evt.MaxExecutionsPerPass < 0)
                Error(diagnostics, "CARD_V7_EVENT_EXECUTIONS_INVALID", path + ".maxExecutionsPerPass", "Execution count cannot be negative.");
            if (evt.CooldownFrames < 0)
                Error(diagnostics, "CARD_V6_EVENT_COOLDOWN_INVALID", path + ".cooldownFrames", "Cooldown cannot be negative.");
            if (evt.Operation == null || evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured)
                Error(diagnostics, "CARD_V6_EVENT_OPERATION_MISSING", path + ".operation", "Configure an operation before saving or running this Event.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold)
                Error(diagnostics, "CARD_V9_EVENT_HOLD_REMOVED", path + ".operation", "Hold Events are no longer supported.");
            else if (!CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                evt.TimingKind,
                evt.Operation.Kind))
                Error(diagnostics, "CARD_V8_TIMING_OPERATION_INCOMPATIBLE", path + ".operation", "The selected operation is not compatible with this Event timing category.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                && !timelineIndices.ContainsKey(evt.Operation.TargetTimelineId ?? string.Empty))
                Error(diagnostics, "CARD_V6_EVENT_TARGET_UNKNOWN", path + ".operation.targetTimelineId", "Target Timeline does not exist.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                && (!evt.Operation.CharacterActionId.IsValid
                    || (references != null && !references.HasCharacterAction(evt.Operation.CharacterActionId))))
                Error(diagnostics, "CARD_V6_EVENT_ACTION_UNKNOWN", path + ".operation.characterActionId", "CharacterAction reference is unavailable.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                && (evt.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration
                    || evt.MaxExecutionsPerPass != 1
                    || evt.CooldownFrames != 0))
                Error(diagnostics, "CARD_V8_ACTION_EVENT_NOT_CANONICAL", path, "CharacterAction Events must be duration Events with one execution and zero cooldown.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                && references != null
                && (!references.TryResolveCharacterActionDurationFrames(evt.Operation.CharacterActionId, out int actionDuration)
                    || evt.DurationFrames != actionDuration))
                Error(diagnostics, "CARD_V8_ACTION_EVENT_DURATION_INVALID", path + ".durationFrames", "CharacterAction Event duration must match the referenced action timeline.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability)
                Error(diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED", path + ".operation", "Ability Events are no longer supported; reference the bound CharacterAction instead.");
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                && (float.IsNaN(evt.Operation.MoveSpeedMultiplier)
                    || float.IsInfinity(evt.Operation.MoveSpeedMultiplier)
                    || evt.Operation.MoveSpeedMultiplier < CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MinimumMoveSpeedMultiplier
                    || evt.Operation.MoveSpeedMultiplier > CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MaximumMoveSpeedMultiplier))
                Error(diagnostics, "CARD_V8_MOVE_SPEED_MULTIPLIER_INVALID", path + ".operation.moveSpeedMultiplier", "Move speed multiplier must be between 0.1 and 1.0.");

            if (evt.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                && evt.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                Error(diagnostics, "CARD_V8_WAIT_TIMING_INVALID", path + ".timingKind", "WaitForCondition must be an Instant Event.");
            if (evt.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
            {
                if (evt.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration)
                    Error(diagnostics, "CARD_V8_LOOP_TIMING_INVALID", path + ".timingKind", "LoopRegion must be a Duration Event.");
                if (evt.DurationFrames < 1)
                    Error(diagnostics, "CARD_V7_LOOP_DURATION_INVALID", path + ".durationFrames", "LoopRegion must span at least 1f.");
                if (evt.Operation.MaxLoopRepeats < 0)
                    Error(diagnostics, "CARD_V7_LOOP_REPEATS_INVALID", path + ".operation.maxLoopRepeats", "Loop repeat count cannot be negative.");
            }
            ValidateCondition(evt.ConditionRoot, path + ".conditionRoot", true, timeline.TimelineId, evt.EventId, eventIndices, eventOwners, references, diagnostics);
        }

        internal static void SynchronizeCharacterActionEventsForPublication(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            if (payload == null || references == null)
                return;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                for (int j = 0; timeline != null && j < timeline.Events.Count; j++)
                    SynchronizeCharacterActionEvent(timeline.Events[j], references);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                SynchronizeCharacterActionEvent(payload.LegacySharedEvents[i], references);
        }

        private static void SynchronizeCharacterActionEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            if (evt?.Operation?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                || !references.TryResolveCharacterActionDurationFrames(
                    evt.Operation.CharacterActionId,
                    out int durationFrames))
            {
                return;
            }
            evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
            evt.DurationFrames = durationFrames;
            evt.MaxExecutionsPerPass = 1;
            evt.CooldownFrames = 0;
            evt.DurationReviewRequired = false;
        }

        internal static void ValidateCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string path,
            bool allowEventSignal,
            string timelineId,
            string owningEventId,
            IReadOnlyDictionary<string, int> eventIndices,
            IReadOnlyDictionary<string, string> eventOwners,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            int depth = 1,
            int[] leafCounter = null,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind scopeSubject =
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown,
            ISet<string> externallyBoundPredicates = null)
        {
            if (node == null)
                return;
            if (node.MovementQuery != null)
                Error(diagnostics, "TACTICAL_MOVEMENT_BINDING", path, "Movement bindings require the tactical composition compiler.");
            leafCounter ??= new[] { 0 };
            if (depth > MaxConditionDepth)
            {
                Error(diagnostics, "CARD_V6_CONDITION_DEPTH", path, "Condition nesting cannot exceed 8 levels.");
                return;
            }
            if (string.IsNullOrWhiteSpace(node.NodeId))
                Error(diagnostics, "CARD_V6_CONDITION_ID_MISSING", path + ".nodeId", "Condition node identity is required.");
            switch (node.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                    if (node.Children.Count == 0)
                        Error(diagnostics, "CARD_V6_CONDITION_GROUP_EMPTY", path + ".children", "All/Any groups require at least one child.");
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                    if (node.Children.Count != 1)
                        Error(diagnostics, "CARD_V6_CONDITION_NOT_ARITY", path + ".children", "Not requires exactly one child.");
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                    leafCounter[0]++;
                    if (node.Predicate == null && externallyBoundPredicates?.Contains(node.NodeId) == true)
                    {
                        if (scopeSubject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget)
                            Error(diagnostics, "TACTICAL_PREDICATE_SCOPE", path, "Bound action predicates require CurrentCombatTarget scope.");
                    }
                    else if (node.Predicate == null)
                        Error(diagnostics, "CARD_V6_CONDITION_PREDICATE_MISSING", path + ".predicate", "Fact condition is not configured.");
                    else
                    {
                        if (node.Predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                            && references != null && !references.HasAbility(node.Predicate.AbilityId))
                            Error(diagnostics, "CARD_V6_CONDITION_ABILITY_UNKNOWN", path + ".predicate.abilityId", "Condition Ability reference is unavailable.");
                        if (node.Predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                            && references != null && !references.HasCharacterAction(node.Predicate.CharacterActionId))
                            Error(diagnostics, "CARD_V6_CONDITION_ACTION_UNKNOWN", path + ".predicate.characterActionId", "Condition CharacterAction reference is unavailable.");
                        if (CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                                node.Predicate.Fact,
                                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor))
                        {
                            if (node.Predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                            {
                                ValidateTargetStateMasks(node.Predicate, path, diagnostics);
                                if (scopeSubject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown
                                    && scopeSubject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget)
                                {
                                    Error(diagnostics, "CARD_V11_TARGET_STATE_SCOPE_MISMATCH", path, "TargetState may only use the target SubjectScope.");
                                }
                                if (scopeSubject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                                    && (node.Predicate.AllowedTargetPhases
                                        & (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget) != 0u)
                                {
                                    Error(diagnostics, "CARD_V11_NO_TARGET_INSIDE_SCOPE", path + ".predicate.allowedTargetPhases", "NoTarget must remain outside the target SubjectScope.");
                                }
                            }
                            else if (scopeSubject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown)
                            {
                                Error(diagnostics, "CARD_V10_FACT_SCOPE_MISSING", path, "Fact condition requires an explicit SubjectScope.");
                            }
                            else if (scopeSubject != descriptor.Subject)
                            {
                                Error(diagnostics, "CARD_V10_FACT_SCOPE_MISMATCH", path, "Fact condition does not belong to the enclosing SubjectScope.");
                            }
                        }
                        else
                        {
                            if (node.Predicate.Fact
                                    == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                                || node.Predicate.Fact
                                    == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                                || node.Predicate.Fact
                                    == CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady
                                || node.Predicate.Fact
                                    == CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard)
                            {
                                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind legacySubject =
                                    node.Predicate.Fact
                                        == CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard
                                    ? CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment
                                    : CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self;
                                if (scopeSubject
                                    == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown)
                                {
                                    Error(diagnostics, "CARD_V10_FACT_SCOPE_MISSING", path, "Legacy condition requires an explicit SubjectScope.");
                                }
                                else if (scopeSubject != legacySubject)
                                {
                                    Error(diagnostics, "CARD_V10_FACT_SCOPE_MISMATCH", path, "Legacy condition belongs to its original SubjectScope.");
                                }
                            }
                            else
                            {
                                Error(diagnostics, "CARD_V11_FACT_NOT_AUTHORABLE", path + ".predicate.fact", "Fact is migration-only or unknown in the current authoring schema.");
                            }
                        }
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal:
                    leafCounter[0]++;
                    if (scopeSubject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown)
                        Error(diagnostics, "CARD_V10_SCOPE_EVENT_SIGNAL", path, "EventSignal cannot be placed inside SubjectScope.");
                    if (!allowEventSignal)
                        Error(diagnostics, "CARD_V6_EVENT_SIGNAL_SCOPE", path, "Behavior and Timeline entry conditions may only use facts.");
                    else if (string.Equals(node.SourceEventId, owningEventId, StringComparison.Ordinal))
                        Error(diagnostics, "CARD_V6_EVENT_SIGNAL_SELF", path + ".sourceEventId", "An Event cannot wait on itself.");
                    else if (!eventIndices.ContainsKey(node.SourceEventId ?? string.Empty))
                        Error(diagnostics, "CARD_V6_EVENT_SIGNAL_UNKNOWN", path + ".sourceEventId", "Referenced Event does not exist.");
                    else if (!string.Equals(eventOwners[node.SourceEventId], timelineId, StringComparison.Ordinal))
                        Error(diagnostics, "CARD_V6_EVENT_SIGNAL_CROSS_TIMELINE", path + ".sourceEventId", "EventSignal may only reference an Event in the same Timeline.");
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                    if (scopeSubject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown)
                        Error(diagnostics, "CARD_V10_SCOPE_NESTED", path, "SubjectScope cannot be nested.");
                    if (node.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown
                        || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind), node.Subject))
                        Error(diagnostics, "CARD_V10_SCOPE_SUBJECT_INVALID", path + ".subject", "SubjectScope requires a known subject.");
                    if (node.Children.Count != 1)
                        Error(diagnostics, "CARD_V10_SCOPE_ARITY", path + ".children", "SubjectScope requires exactly one child tree.");
                    break;
            }
            if (leafCounter[0] > MaxConditionLeaves)
                Error(diagnostics, "CARD_V6_CONDITION_LEAF_LIMIT", path, "A condition tree may contain at most 64 leaves.");
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind childScope =
                node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                    ? node.Subject
                    : scopeSubject;
            int childDepth = depth + (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope ? 0 : 1);
            for (int i = 0; i < node.Children.Count; i++)
                ValidateCondition(node.Children[i], path + ".children[" + i + "]", allowEventSignal, timelineId, owningEventId, eventIndices, eventOwners, references, diagnostics, childDepth, leafCounter, childScope, externallyBoundPredicates);
        }

        private static void ValidateReachability(
            string entryTimelineId,
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> timelines,
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeEvent> sharedEvents,
            IReadOnlyDictionary<string, int> timelineIndices,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (!timelineIndices.ContainsKey(entryTimelineId ?? string.Empty))
                return;
            var reachable = new HashSet<string>(StringComparer.Ordinal) { entryTimelineId };
            for (int i = 0; sharedEvents != null && i < sharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = sharedEvents[i]?.Operation;
                if (operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                    && timelineIndices.ContainsKey(operation.TargetTimelineId ?? string.Empty))
                {
                    reachable.Add(operation.TargetTimelineId);
                }
            }
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < timelines.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                    if (timeline == null || !reachable.Contains(timeline.TimelineId))
                        continue;
                    for (int j = 0; j < timeline.Events.Count; j++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = timeline.Events[j]?.Operation;
                        if (operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                            && reachable.Add(operation.TargetTimelineId ?? string.Empty))
                            changed = true;
                    }
                }
            }
            while (changed);
            for (int i = 0; i < timelines.Count; i++)
            {
                if (timelines[i] != null && !reachable.Contains(timelines[i].TimelineId))
                    Error(diagnostics, "CARD_V6_TIMELINE_UNREACHABLE", "$.timelines[" + i + "]", "Timeline is unreachable from the behavior entry.");
            }
        }

        private static void ValidateSignalCycles(
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> timelines,
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeEvent> sharedEvents,
            IReadOnlyDictionary<string, int> eventIndices,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var edges = new List<int>[eventIndices.Count];
            for (int i = 0; i < edges.Length; i++) edges[i] = new List<int>();
            for (int i = 0; i < timelines.Count; i++)
            {
                for (int j = 0; j < timelines[i].Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timelines[i].Events[j];
                    if (evt == null || !eventIndices.TryGetValue(evt.EventId, out int target)) continue;
                    CollectSignalEdges(evt.ConditionRoot, target, eventIndices, edges);
                }
            }
            for (int i = 0; sharedEvents != null && i < sharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = sharedEvents[i];
                if (evt == null || !eventIndices.TryGetValue(evt.EventId, out int target))
                    continue;
                CollectSignalEdges(evt.ConditionRoot, target, eventIndices, edges);
            }
            var state = new byte[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                if (HasCycle(i, edges, state))
                {
                    Error(diagnostics, "CARD_V6_EVENT_SIGNAL_CYCLE", "$.timelines", "EventSignal dependencies contain a circular wait.");
                    return;
                }
            }
        }

        internal static void ValidateTimeControls(
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> timelines,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int timelineIndex = 0; timelineIndex < timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[timelineIndex];
                if (timeline == null)
                    continue;
                string timelinePath = "$.timelines[" + timelineIndex + "]";
                for (int i = 0; i < timeline.Events.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent left = timeline.Events[i];
                    if (left?.Operation == null)
                        continue;
                    bool leftLoop = left.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion;
                    bool leftWait = left.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition;
                    int leftEnd = left.StartFrame + Math.Max(1, left.DurationFrames);
                    for (int j = i + 1; j < timeline.Events.Count; j++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEvent right = timeline.Events[j];
                        if (right?.Operation == null)
                            continue;
                        bool rightLoop = right.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion;
                        bool rightWait = right.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition;
                        int rightEnd = right.StartFrame + Math.Max(1, right.DurationFrames);

                        if (leftWait && rightWait && left.StartFrame == right.StartFrame)
                            Error(diagnostics, "CARD_V7_WAIT_FRAME_CONFLICT", timelinePath + ".events[" + j + "]", "Only one wait point may exist at a local frame.");

                        if (leftLoop && rightLoop
                            && left.StartFrame < rightEnd
                            && right.StartFrame < leftEnd)
                            Error(diagnostics, "CARD_V7_LOOP_OVERLAP", timelinePath + ".events[" + j + "]", "Loop regions cannot overlap or nest.");

                        CharacterTestRuntimeAiLabBehaviorRecipeEvent loop = leftLoop ? left : rightLoop ? right : null;
                        CharacterTestRuntimeAiLabBehaviorRecipeEvent other = leftLoop ? right : rightLoop ? left : null;
                        if (loop == null || other == null)
                            continue;
                        int loopEnd = loop.StartFrame + loop.DurationFrames;
                        if (other.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                            && other.StartFrame == loopEnd - 1)
                            Error(diagnostics, "CARD_V7_WAIT_LOOP_END_CONFLICT", timelinePath + ".events[" + (ReferenceEquals(other, right) ? j : i) + "]", "A wait point cannot occupy a LoopRegion end frame.");
                    }
                }
            }
        }

        private static void CollectSignalEdges(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, int target, IReadOnlyDictionary<string, int> indices, IList<int>[] edges)
        {
            if (node == null) return;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal
                && indices.TryGetValue(node.SourceEventId ?? string.Empty, out int source))
                edges[target].Add(source);
            for (int i = 0; i < node.Children.Count; i++) CollectSignalEdges(node.Children[i], target, indices, edges);
        }

        private static bool HasCycle(int index, IReadOnlyList<int>[] edges, byte[] state)
        {
            if (state[index] == 1) return true;
            if (state[index] == 2) return false;
            state[index] = 1;
            for (int i = 0; i < edges[index].Count; i++) if (HasCycle(edges[index][i], edges, state)) return true;
            state[index] = 2;
            return false;
        }

        private static void ValidateTargetStateMasks(
            CharacterTestRuntimeAiLabCardCondition condition,
            string path,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            uint phaseMask = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.PhaseMask;
            uint flagMask = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.FlagMask;
            if (condition.AllowedTargetPhases == 0u)
                Error(diagnostics, "CARD_V11_TARGET_PHASE_EMPTY", path + ".predicate.allowedTargetPhases", "TargetState requires at least one allowed phase.");
            if ((condition.AllowedTargetPhases & ~phaseMask) != 0u)
                Error(diagnostics, "CARD_V11_TARGET_PHASE_UNKNOWN", path + ".predicate.allowedTargetPhases", "TargetState contains unknown phase bits.");
            if ((condition.RequiredTargetFlags & ~flagMask) != 0u)
                Error(diagnostics, "CARD_V11_TARGET_REQUIRED_UNKNOWN", path + ".predicate.requiredTargetFlags", "TargetState contains unknown required flag bits.");
            if ((condition.ForbiddenTargetFlags & ~flagMask) != 0u)
                Error(diagnostics, "CARD_V11_TARGET_FORBIDDEN_UNKNOWN", path + ".predicate.forbiddenTargetFlags", "TargetState contains unknown forbidden flag bits.");
            if ((condition.RequiredTargetFlags & condition.ForbiddenTargetFlags) != 0u)
                Error(diagnostics, "CARD_V11_TARGET_FLAGS_OVERLAP", path + ".predicate", "TargetState cannot require and forbid the same flag.");
            if (condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals)
                Error(diagnostics, "CARD_V11_TARGET_OPERATOR_INVALID", path + ".predicate.operator", "TargetState uses the fixed phase/required/forbidden comparison model.");
        }

        private static CharacterTestRuntimeAiLabCompiledRecipeV6Condition CompileCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            IReadOnlyDictionary<string, int> eventIndices)
        {
            if (node == null) return null;
            var children = new CharacterTestRuntimeAiLabCompiledRecipeV6Condition[node.Children.Count];
            for (int i = 0; i < children.Length; i++) children[i] = CompileCondition(node.Children[i], eventIndices);
            CharacterTestRuntimeAiLabCompiledCondition? predicate = null;
            if (node.Predicate != null)
                predicate = new CharacterTestRuntimeAiLabCompiledCondition(
                    node.Predicate.ConditionId,
                    node.Predicate.Fact,
                    node.Predicate.Operator,
                    node.Predicate.BoolValue,
                    node.Predicate.Value,
                    node.Predicate.MaxValue,
                    node.Predicate.AbilityId,
                    node.Predicate.CharacterActionId,
                    node.Predicate.AllowedTargetPhases,
                    node.Predicate.RequiredTargetFlags,
                    node.Predicate.ForbiddenTargetFlags);
            int sourceIndex = node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal
                && eventIndices.TryGetValue(node.SourceEventId ?? string.Empty, out int resolved) ? resolved : -1;
            return new CharacterTestRuntimeAiLabCompiledRecipeV6Condition(
                node.Kind,
                node.Subject,
                predicate,
                sourceIndex,
                node.Signal,
                children);
        }

        private static CharacterTestRuntimeAiLabCompiledLogicStage CompileHeaderStage(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline, int order)
        {
            return new CharacterTestRuntimeAiLabCompiledLogicStage(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = timeline.TimelineId,
                Order = order,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                Title = timeline.Title,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Infinite,
                PlannedDurationFrames = 1,
                PreviewDurationFrames = 1
            }, Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
        }

        private static CharacterTestRuntimeAiLabCompiledLogicStage CompileActionStage(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline, CharacterTestRuntimeAiLabBehaviorRecipeEvent evt, int order)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = evt.Operation;
            CharacterTestRuntimeAiLabCardStepKind kind;
            switch (operation.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget: kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget; break;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction: kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction; break;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability: kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility; break;
                default: return null;
            }
            return new CharacterTestRuntimeAiLabCompiledLogicStage(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = timeline.TimelineId + ".event." + evt.EventId,
                Order = order,
                Kind = kind,
                Title = evt.Title,
                AbilityId = operation.AbilityId,
                CharacterActionId = operation.CharacterActionId,
                MovementStrategy = operation.MovementStrategy,
                MovementTargetKind = operation.MovementTargetKind,
                MoveTargetPositionX = operation.MoveTargetPositionX,
                MoveTargetPositionZ = operation.MoveTargetPositionZ,
                MoveTargetEntityId = operation.MoveTargetEntityId,
                // Native recipe approach events do not own an implicit stop threshold. Reaching
                // a desired distance is modeled by an explicit condition Event, which can then
                // return to the planner or enter another Timeline without leaving a still-running
                // movement Event behind.
                MoveMinDistance = operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                    && operation.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                        ? 0f
                        : operation.MoveMinDistance,
                MoveMaxDistance = operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                    && operation.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                        ? 0f
                        : operation.MoveMaxDistance,
                MoveSpeedMultiplier = operation.MoveSpeedMultiplier,
                WaitFrames = evt.DurationFrames,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                PlannedDurationFrames = Math.Max(1, evt.DurationFrames),
                PreviewDurationFrames = Math.Max(1, evt.DurationFrames)
            },
            Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>(),
            frameScopedControlOperation:
                operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Failed(List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics) => new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(null, diagnostics.ToArray());
        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline FindTimeline(IReadOnlyList<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> timelines, string timelineId)
        {
            for (int i = 0; timelines != null && i < timelines.Count; i++)
            {
                if (string.Equals(timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    return timelines[i];
            }
            return null;
        }
        private static void Error(List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics, string code, string path, string message) => diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error, code, path, message));
        private static int CompareTimelines(CharacterTestRuntimeAiLabBehaviorRecipeTimeline left, CharacterTestRuntimeAiLabBehaviorRecipeTimeline right) { if (ReferenceEquals(left, right)) return 0; if (left == null) return 1; if (right == null) return -1; int order = left.Order.CompareTo(right.Order); return order != 0 ? order : string.CompareOrdinal(left.TimelineId, right.TimelineId); }
        private static int CompareCompiledEvents(CharacterTestRuntimeAiLabCompiledRecipeV6Event left, CharacterTestRuntimeAiLabCompiledRecipeV6Event right) { int priority = right.Priority.CompareTo(left.Priority); return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId); }
    }
}
