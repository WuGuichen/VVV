using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Table emission after the existing authoring validator and compatibility normalization.</summary>
    internal static class CharacterTestBehaviorExecutionCompiler
    {
        internal static string ComputeDependencyFingerprint(CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            IEnumerable<int> actionIds, string externalIdentity = "", IEnumerable<int> abilityIds = null)
        {
            var dependencies = new JArray();
            var ordered = new SortedSet<int>(actionIds);
            foreach (int id in ordered)
            {
                CharacterActionConfig found = null;
                var actions = references?.CharacterActions;
                for (int i = 0; actions != null && i < actions.Count; i++)
                    if (actions[i]?.ActionId.Value == id) { found = actions[i]; break; }
                if (found == null) throw new ArgumentException("Required action dependency is unavailable: " + id);
                dependencies.Add(Builder.Canonical(JToken.FromObject(found)));
            }
            if (abilityIds != null)
            {
                foreach (int id in new SortedSet<int>(abilityIds))
                {
                    if (references == null || !references.TryGetExecutionAbilityDependency(id, out var ability))
                        throw new ArgumentException("Required ability dependency is unavailable: " + id);
                    dependencies.Add(new JObject { ["abilityId"] = id, ["definition"] = Builder.Canonical(JToken.FromObject(ability)) });
                }
            }
            dependencies.Add(externalIdentity ?? string.Empty);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(dependencies.ToString(Formatting.None)))).Replace("-", string.Empty);
        }
        internal static CharacterTestBehaviorExecutionProgram CompileValidated(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out CharacterTestBehaviorExecutionDebugMap debugMap)
        {
            var builder = new Builder(payload, references);
            CharacterTestBehaviorExecutionProgram program = builder.Build();
            debugMap = builder.CreateDebugMap();
            return program;
        }

        private sealed class Builder
        {
            private readonly CharacterTestRuntimeAiLabBehaviorCardLogicPayload _source;
            private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;
            private readonly Dictionary<string, int> _timelineIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _eventIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> _sources;
            private readonly List<CharacterTestRuntimeAiLabBehaviorRecipeEvent> _eventSources = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>();
            private readonly List<CharacterTestBehaviorExecutionCondition> _conditions = new List<CharacterTestBehaviorExecutionCondition>();
            private readonly List<CharacterTestBehaviorExecutionPredicate> _predicates = new List<CharacterTestBehaviorExecutionPredicate>();
            private readonly List<int> _children = new List<int>();
            private readonly List<CharacterTestBehaviorExecutionAction> _actions = new List<CharacterTestBehaviorExecutionAction>();
            private readonly List<CharacterTestBehaviorExecutionMovement> _movements = new List<CharacterTestBehaviorExecutionMovement>();
            private readonly List<CharacterTestBehaviorExecutionFlow> _flows = new List<CharacterTestBehaviorExecutionFlow>();
            private readonly List<string> _conditionIds = new List<string>();
            private readonly SortedSet<int> _requiredActions = new SortedSet<int>();
            private readonly SortedSet<int> _requiredAbilities = new SortedSet<int>();
            public Builder(CharacterTestRuntimeAiLabBehaviorCardLogicPayload source, CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
            {
                _source = source; _references = references;
                _sources = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(source.Timelines);
                _sources.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.TimelineId, b.TimelineId));
                for (int i = 0; i < _sources.Count; i++)
                {
                    _timelineIndices.Add(_sources[i].TimelineId, i);
                    _eventSources.AddRange(_sources[i].Events);
                }
                _eventSources.AddRange(source.LegacySharedEvents);
                _eventSources.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : string.CompareOrdinal(a.EventId, b.EventId));
                for (int i = 0; i < _eventSources.Count; i++) _eventIndices.Add(_eventSources[i].EventId, i);
            }
            public CharacterTestBehaviorExecutionProgram Build()
            {
                var events = new CharacterTestBehaviorExecutionEvent[_eventSources.Count];
                var timelines = new CharacterTestBehaviorExecutionTimeline[_sources.Count];
                var timelineEvents = new List<int>();
                int activation = AddCondition(_source.ActivationConditionRoot);
                for (int i = 0; i < events.Length; i++)
                {
                    var source = _eventSources[i];
                    int timeline = _timelineIndices[source.TimelineId];
                    int parameter = AddParameter(source, timeline);
                    events[i] = new CharacterTestBehaviorExecutionEvent(timeline, source.StartFrame, source.DurationFrames,
                        AddCondition(source.ConditionRoot), parameter, source.MaxExecutionsPerPass, source.CooldownFrames,
                        source.Priority, i, source.Operation.Kind, source.TimingKind, source.ClockOrigin,
                        source.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe && !string.IsNullOrEmpty(source.LegacySharedEventId));
                }
                for (int i = 0; i < timelines.Length; i++)
                {
                    int start = timelineEvents.Count, duration = 0;
                    for (int e = 0; e < events.Length; e++)
                    {
                        if (events[e].Shared) timelineEvents.Add(e);
                        else if (events[e].TimelineIndex == i)
                        {
                            timelineEvents.Add(e);
                            duration = Math.Max(duration, events[e].EndFrame);
                        }
                    }
                    timelines[i] = new CharacterTestBehaviorExecutionTimeline(start, timelineEvents.Count - start, duration,
                        AddCondition(_sources[i].EntryConditionRoot));
                }
                return new CharacterTestBehaviorExecutionProgram(_timelineIndices[_source.EntryTimelineId], activation,
                    BuildDependencyFingerprint(), timelines, events, timelineEvents.ToArray(), _actions.ToArray(),
                    _movements.ToArray(), _flows.ToArray(), _conditions.ToArray(), _predicates.ToArray(), _children.ToArray());
            }
            private int AddParameter(CharacterTestRuntimeAiLabBehaviorRecipeEvent source, int timeline)
            {
                var operation = source.Operation;
                switch (operation.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                        _requiredActions.Add(operation.CharacterActionId.Value);
                        _actions.Add(new CharacterTestBehaviorExecutionAction(operation.CharacterActionId.Value));
                        return _actions.Count - 1;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                        bool approach = operation.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
                        var metric = operation.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                            ? CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance
                            : operation.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                                ? CharacterTestBehaviorDistanceMetric.RootPlanarDistance
                                : CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance;
                        _movements.Add(new CharacterTestBehaviorExecutionMovement(operation.MovementStrategy, operation.MovementTargetKind,
                            metric, operation.MoveTargetEntityId, operation.MoveTargetPositionX, operation.MoveTargetPositionZ,
                            approach ? 0f : operation.MoveMinDistance, approach ? 0f : operation.MoveMaxDistance,
                            operation.MoveSpeedMultiplier, ComputeSideParity(timeline, source.TimelineId + ".event." + source.EventId)));
                        return _movements.Count - 1;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline:
                        _flows.Add(new CharacterTestBehaviorExecutionFlow(_timelineIndices[operation.TargetTimelineId], 0));
                        return _flows.Count - 1;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                        _flows.Add(new CharacterTestBehaviorExecutionFlow(-1, operation.MaxLoopRepeats));
                        return _flows.Count - 1;
                    default: return -1;
                }
            }
            private int AddCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode source)
            {
                if (source == null) return -1;
                int index = _conditions.Count, predicate = -1;
                _conditions.Add(default);
                _conditionIds.Add(source.NodeId ?? string.Empty);
                int childStart = _children.Count;
                for (int i = 0; i < source.Children.Count; i++) _children.Add(-1);
                for (int i = 0; i < source.Children.Count; i++) _children[childStart + i] = AddCondition(source.Children[i]);
                if (source.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
                {
                    predicate = _predicates.Count;
                    _predicates.Add(new CharacterTestBehaviorExecutionPredicate(source.Predicate));
                    if (source.Predicate.CharacterActionId.IsValid) _requiredActions.Add(source.Predicate.CharacterActionId.Value);
                    if (source.Predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable && source.Predicate.AbilityId > 0) _requiredAbilities.Add(source.Predicate.AbilityId);
                }
                int eventIndex = source.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal
                    ? _eventIndices[source.SourceEventId] : -1;
                _conditions[index] = new CharacterTestBehaviorExecutionCondition(source.Kind, source.Subject, predicate,
                    childStart, source.Children.Count, eventIndex, source.Signal);
                return index;
            }
            private string BuildDependencyFingerprint()
            {
                return ComputeDependencyFingerprint(_references, _requiredActions, abilityIds: _requiredAbilities);
            }
            internal static JToken Canonical(JToken token)
            {
                if (token is JObject obj)
                {
                    var properties = new List<JProperty>(obj.Properties());
                    properties.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    var result = new JObject();
                    foreach (var property in properties) result.Add(property.Name, Canonical(property.Value));
                    return result;
                }
                if (token is JArray array)
                {
                    var result = new JArray();
                    foreach (var item in array) result.Add(Canonical(item));
                    return result;
                }
                return token.DeepClone();
            }
            public CharacterTestBehaviorExecutionDebugMap CreateDebugMap()
            {
                var timelines = new string[_sources.Count];
                var events = new string[_eventSources.Count];
                for (int i = 0; i < timelines.Length; i++) timelines[i] = _sources[i].TimelineId;
                for (int i = 0; i < events.Length; i++) events[i] = _eventSources[i].EventId;
                return new CharacterTestBehaviorExecutionDebugMap(timelines, events, _conditionIds.ToArray());
            }
        }
        internal static int ComputeSideParity(int order, string sourceId)
        {
            int parity = order & 1;
            for (int i = 0; i < sourceId.Length; i++) parity ^= sourceId[i] & 1;
            return parity;
        }
    }
}
