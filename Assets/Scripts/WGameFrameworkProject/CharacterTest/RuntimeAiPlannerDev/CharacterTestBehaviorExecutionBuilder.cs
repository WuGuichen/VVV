using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Low-frequency explicit compiler target for project authoring frontends.</summary>
    /// <remarks>Does not change persisted recipe schemas. Build owns and validates all emitted arrays.</remarks>
    internal sealed partial class CharacterTestBehaviorExecutionBuilder
    {
        private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;
        private readonly string _dependencies;
        public string EntryTimelineId { get; set; }
        private readonly List<TimelineSource> _timelines = new List<TimelineSource>();
        private readonly List<EventSource> _events = new List<EventSource>();
        private readonly Dictionary<string, (int count, int cooldown)> _repetition = new(StringComparer.Ordinal);
        internal void SetRepetition(string id, int count, int cooldown) => _repetition[id] = (count, cooldown);
        private readonly Dictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind> _timingKinds = new(StringComparer.Ordinal);
        internal void SetTiming(string id, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timing) => _timingKinds[id] = timing;
        private readonly List<CharacterTestBehaviorExecutionCondition> _conditions = new List<CharacterTestBehaviorExecutionCondition>();
        private readonly List<CharacterTestBehaviorExecutionPredicate> _predicates = new List<CharacterTestBehaviorExecutionPredicate>();
        private readonly List<int> _children = new List<int>();
        private readonly List<string> _conditionIds = new List<string>();
        private readonly Dictionary<int, string> _conditionEventReferences = new Dictionary<int, string>();
        private readonly List<CharacterTestBehaviorExecutionAction> _actions = new List<CharacterTestBehaviorExecutionAction>();
        private readonly List<CharacterTestBehaviorExecutionMovement> _movements = new List<CharacterTestBehaviorExecutionMovement>();
        private readonly List<CharacterTestBehaviorExecutionTurn> _turns = new List<CharacterTestBehaviorExecutionTurn>();
        public int ActivationCondition { get; set; } = -1;
        public CharacterTestBehaviorExecutionBuilder(CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            string entryTimelineId = "main", string dependencyIdentity = "")
        { _references = references; EntryTimelineId = entryTimelineId; _dependencies = dependencyIdentity ?? string.Empty; }

        public void AddTimeline(string id, int durationFrames, int entryCondition = -1, int order = 0)
        { _timelines.Add(new TimelineSource(id, durationFrames, entryCondition, order)); }
        public int AddMovementCondition(string id, CharacterTestBehaviorExecutionMovement movement, bool missing)
            => AddPredicate(id, new CharacterTestBehaviorExecutionPredicate(movement, missing));
        public int AddFactCondition(string id, CharacterTestRuntimeAiLabCardCondition predicate)
            => AddPredicate(id, new CharacterTestBehaviorExecutionPredicate(predicate));
        public int AddBoundActionOpportunityCondition(string id, int actionId)
            => AddPredicate(id, new CharacterTestBehaviorExecutionPredicate(CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity, actionId, 0f, 0f));
        public int AddTargetFacingCondition(string id, float centerDegrees, float toleranceDegrees)
            => AddPredicate(id, new CharacterTestBehaviorExecutionPredicate(CharacterTestBehaviorExecutionPredicateKind.TargetFacing, 0, centerDegrees, toleranceDegrees));
        public int AddRootDistanceCondition(string id, float minimum, float maximum)
            => AddPredicate(id, new CharacterTestBehaviorExecutionPredicate(CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange, 0, minimum, maximum));
        public int AddHurtboxDistanceCondition(string id, float minimum, float maximum)
            => AddFactCondition(id, new CharacterTestRuntimeAiLabCardCondition {
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between, Value = minimum, MaxValue = maximum });
        public int AddTargetAliveCondition(string id) => AddFactCondition(id, new CharacterTestRuntimeAiLabCardCondition
        {
            Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState,
            Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
            AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
        });
        private int AddPredicate(string id, CharacterTestBehaviorExecutionPredicate predicate)
        {
            int index = _conditions.Count, parameter = _predicates.Count;
            _conditionIds.Add(id); _predicates.Add(predicate);
            _conditions.Add(new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                default, parameter, _children.Count, 0, -1, default));
            return index;
        }
        public int AddAll(string id, params int[] children) => AddComposite(id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All, children);
        public int AddAny(string id, params int[] children) => AddComposite(id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any, children);
        public int AddNot(string id, int child) => AddComposite(id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not, new[] { child });
        public int AddSubjectScope(string id, CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject, int child)
        {
            int index = _conditions.Count, start = _children.Count;
            _children.Add(child); _conditionIds.Add(id);
            _conditions.Add(new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                subject, -1, start, 1, -1, default));
            return index;
        }
        public int AddEventSignal(string id, string sourceEventId, CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal)
        {
            int index = _conditions.Count;
            _conditionIds.Add(id); _conditionEventReferences.Add(index, sourceEventId);
            _conditions.Add(new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                default, -1, _children.Count, 0, -1, signal));
            return index;
        }
        private int AddComposite(string id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind kind, int[] children)
        {
            int index = _conditions.Count, start = _children.Count;
            _children.AddRange(children ?? Array.Empty<int>()); _conditionIds.Add(id);
            _conditions.Add(new CharacterTestBehaviorExecutionCondition(kind, default, -1, start, _children.Count - start, -1, default));
            return index;
        }
        public void AddAction(string id, string timelineId, int startFrame, int durationFrames, int actionId, int condition = -1, int priority = 0)
        {
            int parameter = _actions.Count; _actions.Add(new CharacterTestBehaviorExecutionAction(actionId));
            AddEvent(id, timelineId, startFrame, durationFrames, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, parameter, condition, priority);
        }
        public void AddMovement(string id, string timelineId, int startFrame, int durationFrames,
            CharacterTestBehaviorExecutionMovement movement, int condition = -1, int priority = 0)
        {
            int parameter = _movements.Count; _movements.Add(movement);
            AddEvent(id, timelineId, startFrame, durationFrames, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget, parameter, condition, priority);
        }
        public void AddTurn(string id, string timelineId, int startFrame, int durationFrames,
            CharacterTestBehaviorExecutionTurn turn, int condition = -1, int priority = 0)
        {
            int parameter = _turns.Count; _turns.Add(turn);
            AddEvent(id, timelineId, startFrame, durationFrames, CharacterTestBehaviorExecutionOperations.TurnToTarget, parameter, condition, priority);
        }
        public void AddGate(string id, string timelineId, int frame, int condition, int priority = 0)
            => AddEvent(id, timelineId, frame, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition, -1, condition, priority);
        public void AddReturn(string id, string timelineId, int startFrame, int durationFrames, int condition = -1, int priority = 0)
            => AddEvent(id, timelineId, startFrame, durationFrames, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner, -1, condition, priority);
        internal void AddCompleteBlock(string id, string timelineId, int startFrame, int durationFrames, int blockEnd,
            int condition = -1, int priority = 0)
            => AddEvent(id, timelineId, startFrame, durationFrames, CharacterTestBehaviorExecutionOperations.CompleteBlock, blockEnd, condition, priority);
        private void AddEvent(string id, string timeline, int start, int duration,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind, int parameter, int condition, int priority)
        { _events.Add(new EventSource(id, timeline, start, duration, kind, parameter, condition, priority)); }

        public CharacterTestRuntimeAiLabCompiledLogicProgram Build(bool includeDebugMap = true)
        {
            var timelines = new List<TimelineSource>(_timelines);
            timelines.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            var events = new List<EventSource>(_events);
            events.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : string.CompareOrdinal(a.Id, b.Id));
            var timelineIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var timelineNames = new string[timelines.Count];
            var eventNames = new string[events.Count];
            for (int i = 0; i < timelines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(timelines[i].Id) || !timelineIndices.TryAdd(timelines[i].Id, i))
                    throw new ArgumentException("Missing or duplicate timeline ID: " + timelines[i].Id);
                timelineNames[i] = timelines[i].Id;
            }
            var compiledEvents = new CharacterTestBehaviorExecutionEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                var source = events[i];
                if (string.IsNullOrWhiteSpace(source.Id) || !eventIds.Add(source.Id)) throw new ArgumentException("Missing or duplicate event ID: " + source.Id);
                if (!timelineIndices.TryGetValue(source.Timeline, out int timeline)) throw new ArgumentException("Unknown timeline for event: " + source.Id);
                if (source.Start < 0 || source.Duration <= 0 || source.Start > int.MaxValue - source.Duration
                    || source.Start + source.Duration > timelines[timeline].Duration)
                    throw new ArgumentException("Event range exceeds its timeline: " + source.Id);
                var repetition = _repetition.TryGetValue(source.Id, out var configured) ? configured : (count: 1, cooldown: 0);
                compiledEvents[i] = new CharacterTestBehaviorExecutionEvent(timeline, source.Start, source.Duration, source.Condition,
                    source.Parameter, repetition.count, repetition.cooldown, source.Priority, i, source.Kind,
                    _timingKinds.TryGetValue(source.Id, out var timingKind) ? timingKind : source.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                        ? CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant : CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                    CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline, false);
                eventNames[i] = source.Id;
            }
            var compiledTimelines = new CharacterTestBehaviorExecutionTimeline[timelines.Count];
            var order = new List<int>();
            for (int i = 0; i < timelines.Count; i++)
            {
                int start = order.Count;
                for (int j = 0; j < compiledEvents.Length; j++) if (compiledEvents[j].TimelineIndex == i) order.Add(j);
                compiledTimelines[i] = new CharacterTestBehaviorExecutionTimeline(start, order.Count - start, timelines[i].Duration, timelines[i].Condition);
            }
            if (!timelineIndices.TryGetValue(EntryTimelineId, out int entry)) throw new ArgumentException("Unknown entry timeline: " + EntryTimelineId);
            var dependencies = new SortedSet<int>();
            var conditions = _conditions.ToArray();
            foreach (var pair in _conditionEventReferences)
            {
                int sourceEvent = Array.IndexOf(eventNames, pair.Value);
                if (sourceEvent < 0) throw new ArgumentException("Unknown signal event: " + pair.Value);
                var condition = conditions[pair.Key];
                conditions[pair.Key] = new CharacterTestBehaviorExecutionCondition(condition.Kind, condition.Subject,
                    condition.Predicate, condition.ChildStart, condition.ChildCount, sourceEvent, condition.Signal);
            }
            var abilityDependencies = new SortedSet<int>();
            foreach (var action in _actions) dependencies.Add(action.ActionId);
            foreach (var predicate in _predicates) if (predicate.ActionId > 0) dependencies.Add(predicate.ActionId);
            foreach (var predicate in _predicates) if (predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable && predicate.AbilityId > 0) abilityDependencies.Add(predicate.AbilityId);
            var program = new CharacterTestBehaviorExecutionProgram(entry, ActivationCondition,
                CharacterTestBehaviorExecutionCompiler.ComputeDependencyFingerprint(_references, dependencies, _dependencies, abilityDependencies),
                compiledTimelines, compiledEvents, order.ToArray(), _actions.ToArray(), _movements.ToArray(),
                BuildFlows(timelineIndices), conditions, _predicates.ToArray(), _children.ToArray(), _turns.ToArray(), _dependencies, BuildBlocks(timelineIndices, eventNames));
            var debug = includeDebugMap ? new CharacterTestBehaviorExecutionDebugMap(timelineNames, eventNames, _conditionIds.ToArray()) : null;
            return new CharacterTestRuntimeAiLabCompiledLogicProgram(program.Identity, -1,
                Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>(), Array.Empty<CharacterTestRuntimeAiLabCompiledLogicStage>(),
                Array.Empty<CharacterTestRuntimeAiLabCompiledLogicDetector>(), Array.Empty<CharacterTestRuntimeAiLabCompiledLogicTransition>(), false,
                execution: program, executionDebugMap: debug);
        }
        private readonly struct TimelineSource
        {
            public readonly string Id; public readonly int Duration, Condition, Order;
            public TimelineSource(string id, int duration, int condition, int order) { Id = id; Duration = duration; Condition = condition; Order = order; }
        }
        private readonly struct EventSource
        {
            public readonly string Id, Timeline; public readonly int Start, Duration, Parameter, Condition, Priority;
            public readonly CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind Kind;
            public EventSource(string id, string timeline, int start, int duration, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind,
                int parameter, int condition, int priority)
            { Id = id; Timeline = timeline; Start = start; Duration = duration; Kind = kind; Parameter = parameter; Condition = condition; Priority = priority; }
        }
    }
}
