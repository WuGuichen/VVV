using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalSourceLocation
    {
        internal CharacterTestTacticalSourceLocation(string card, string node, string field,
            string definition = null, int revision = 0, string evt = null, string condition = null, string block = null)
        { CardId = card; NodeId = node; Field = field; DefinitionId = definition ?? string.Empty; DefinitionRevision = revision; EventId = evt ?? string.Empty; ConditionNodeId = condition ?? string.Empty; BlockId = block ?? string.Empty; }
        public string BlockId { get; }
        public string CardId { get; }
        public string NodeId { get; }
        public string Field { get; }
        public string DefinitionId { get; }
        public int DefinitionRevision { get; }
        public string EventId { get; }
        public string ConditionNodeId { get; }
    }

    internal sealed class CharacterTestTacticalDslCompilation
    {
        internal CharacterTestTacticalDslCompilation(CharacterTestRuntimeAiLabCompiledLogicProgram program,
            string fingerprint, Dictionary<string, int> boundaries, Dictionary<string, string> sources,
            Dictionary<string, CharacterTestTacticalSourceLocation> locations = null,
            Dictionary<string, CharacterTestTacticalAuthoringCompilation> sharedCompilations = null)
        {
            Program = program; DependencyIdentity = fingerprint;
            NormalBoundaries = new ReadOnlyDictionary<string, int>(boundaries);
            EventSources = new ReadOnlyDictionary<string, string>(sources);
            SourceLocations = new ReadOnlyDictionary<string, CharacterTestTacticalSourceLocation>(locations ?? new Dictionary<string, CharacterTestTacticalSourceLocation>());
            SharedCompilations = new ReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation>(
                sharedCompilations ?? new Dictionary<string, CharacterTestTacticalAuthoringCompilation>());
        }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        internal string DependencyIdentity { get; }
        internal IReadOnlyDictionary<string, int> NormalBoundaries { get; }
        internal IReadOnlyDictionary<string, string> EventSources { get; }
        internal IReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> SourceLocations { get; }
        internal IReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation> SharedCompilations { get; }
    }

    /// <summary>Low-frequency trial lowering to the existing execution table and consumer.</summary>
    internal static class CharacterTestTacticalDslCompiler
    {
        internal static bool TryCompile(CharacterTestTacticalDslSource source, CharacterTestTacticalCardInputs inputs,
            out CharacterTestTacticalDslCompilation compilation, out string issue)
        {
            compilation = null; issue = string.Empty;
            try
            {
                if (source == null || inputs == null) throw new ArgumentException("DSL_INPUTS: Source and prepared inputs required.");
                var owned = CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(source));
                compilation = new Context(owned, inputs).Build();
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is OverflowException || error is Newtonsoft.Json.JsonException)
            { issue = error.Message; return false; }
        }

        private sealed class Context
        {
            private readonly CharacterTestTacticalDslSource _source;
            private readonly CharacterTestTacticalCardInputs _inputs;
            private readonly Dictionary<string, CharacterTestTacticalSharedDefinition> _definitions = new Dictionary<string, CharacterTestTacticalSharedDefinition>(StringComparer.Ordinal);
            private readonly Dictionary<string, CharacterTestTacticalDslNode> _referenceNodes = new(StringComparer.Ordinal);
            private readonly HashSet<string> _nodes = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, CharacterTestTacticalAuthoringCompilation> _compiled = new Dictionary<string, CharacterTestTacticalAuthoringCompilation>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _boundaries = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _sources = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, CharacterTestTacticalSourceLocation> _locations = new Dictionary<string, CharacterTestTacticalSourceLocation>(StringComparer.Ordinal);
            private CharacterTestBehaviorExecutionBuilder _builder;
            internal Context(CharacterTestTacticalDslSource source, CharacterTestTacticalCardInputs inputs) { _source = source; _inputs = inputs; }

            internal CharacterTestTacticalDslCompilation Build()
            {
                Require(Id(_source.CardId), "DSL_CARD_ID");
                Require(_source.Root?.Kind == "Sequence" && _source.Definitions != null, "DSL_ROOT");
                foreach (var definition in _source.Definitions)
                {
                    Require(definition != null && Id(definition.DefinitionId) && definition.Revision > 0 && definition.Source != null,
                        "DSL_DEFINITION");
                    Require(_definitions.TryAdd(definition.DefinitionId, definition), "DSL_DUPLICATE_DEFINITION");
                }
                Validate(_source.Root, 0);
                string fingerprint = CharacterTestTacticalDslJson.Fingerprint(_source, _used.Select(id => _definitions[id])) + ":" + _inputs.DependencyFingerprint;
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                    new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), _inputs.CompilationActions));
                _builder = new CharacterTestBehaviorExecutionBuilder(references, dependencyIdentity: fingerprint);
                _builder.ActivationCondition = Condition(_source.ActivationConditionRoot, "$activation", string.Empty, "activationConditionRoot");
                // Entry and normal completion are evaluator lifecycle, never synthetic events.
                // Null successor means natural completion of the current terminal timeline.
                string entry = Lower(_source.Root, null, new List<LocalEnd>());
                if (entry == null)
                {
                    entry = _source.Root.NodeId;
                    _builder.AddTimeline(entry, 0);
                }
                _builder.EntryTimelineId = entry;
                return new CharacterTestTacticalDslCompilation(_builder.Build(), fingerprint, _boundaries, _sources, _locations, _compiled);
            }

            private void Validate(CharacterTestTacticalDslNode node, int depth)
            {
                Require(node != null && Id(node.NodeId) && !node.NodeId.StartsWith("$", StringComparison.Ordinal), "DSL_NODE_ID");
                Require(depth <= 64 && _nodes.Count < 1024 && _nodes.Add(node.NodeId), "DSL_NODE_LIMIT_OR_DUPLICATE");
                bool group = node.Kind == "Sequence" || node.Kind == "If" || node.Kind == "Wait";
                bool conditional = node.Kind == "If" || node.Kind == "Wait";
                Require(group || node.Kind == "Reference" || node.Kind == "Exit", "DSL_KIND_UNSUPPORTED");
                Require(group ? node.Children != null : node.Children == null, "DSL_CHILDREN_SCOPE");
                Require(conditional ? node.Condition != null : node.Condition == null, "DSL_CONDITION_SCOPE");
                Require(node.Kind == "Reference" ? Id(node.DefinitionId) : node.DefinitionId == null, "DSL_REFERENCE_SCOPE");
                Require(node.EndCondition == null || node.Kind == "Sequence" || node.Kind == "Reference", "DSL_END_SCOPE");
                Require(node.MaximumWaitFrames == null || node.Kind == "Wait" && node.MaximumWaitFrames >= 2,
                    "DSL_WAIT_LIMIT: 等待时限至少为 2 帧，且只能用于等待节点。");
                if (node.Kind == "Reference")
                {
                    Require(_definitions.ContainsKey(node.DefinitionId), "DSL_MISSING_DEFINITION:" + node.DefinitionId);
                    _used.Add(node.DefinitionId);
                    _referenceNodes.Add(node.NodeId, node);
                }
                Require(node.DistanceBindings == null || node.Kind == "Reference", "DSL_DISTANCE_BINDING_SCOPE");
                if (group) foreach (var child in node.Children) Validate(child, depth + 1);
            }

            private IReadOnlyDictionary<string, CharacterTestTacticalDistanceData> ResolveDistanceBindings(CharacterTestTacticalDslNode node)
            {
                if (node.DistanceBindings == null) return null;
                var result = new Dictionary<string, CharacterTestTacticalDistanceData>(StringComparer.Ordinal);
                var consumer = _definitions[node.DefinitionId].Source;
                foreach (var pair in node.DistanceBindings)
                {
                    Require(consumer.PredicateBindings.Any(b => b?.NodeId == pair.Key && b.Kind == "IntentDistanceRange"
                        && string.IsNullOrEmpty(b.SourceBlockId) && string.IsNullOrEmpty(b.SourceActionEventId)), "DSL_DISTANCE_CONSUMER");
                    var link = pair.Value;
                    Require(link != null && link.DataKind == "Configuration" && Id(link.SourceNodeId) && Id(link.SourceBlockId)
                        && link.SourceNodeId != node.NodeId, "DSL_DISTANCE_CONFIGURATION_ONLY");
                    Require(_referenceNodes.TryGetValue(link.SourceNodeId, out var provider), "DSL_DISTANCE_SOURCE_MISSING");
                    var data = CharacterTestTacticalAuthoringCompiler.ResolveDistanceData(_definitions[provider.DefinitionId].Source,
                        _inputs.ForInstance(provider.NodeId), link.SourceBlockId);
                    result.Add(pair.Key, data);
                }
                return result;
            }

            private sealed class LocalEnd
            {
                internal string Owner, Next;
                internal int Condition;
            }

            private string Lower(CharacterTestTacticalDslNode node, string next, List<LocalEnd> scope)
            {
                if (node.EndCondition != null)
                {
                    Require(scope.Count < 4, "DSL_END_DEPTH: At most four active local end ranges are supported.");
                    scope = new List<LocalEnd>(scope) { new LocalEnd { Owner = node.NodeId, Next = next,
                        Condition = Condition(node.EndCondition, "$end:" + node.NodeId, node.NodeId, "endCondition") } };
                }
                if (node.Kind == "Sequence") return Children(node, next, scope);
                if (node.Kind == "Reference")
                {
                    var definition = _definitions[node.DefinitionId];
                    string compileKey = _inputs.HasExplicitBindings || node.BlockDurations != null || node.DistanceBindings != null ? node.NodeId : node.DefinitionId;
                    if (!_compiled.TryGetValue(compileKey, out var value))
                    {
                        Require(definition.Source.Timelines.Count == 1 && definition.Source.ActivationConditionRoot == null,
                            "DSL_SHARED_SCOPE: Extract card activation explicitly; only one internal timeline is supported.");
                        var instanceSource = definition.Source;
                        if (node.BlockDurations != null)
                        {
                            instanceSource = definition.Source.Clone();
                            foreach (var length in node.BlockDurations)
                            {
                                var block = instanceSource.Timelines.SelectMany(t => t.EventBlocks).FirstOrDefault(b => b.BlockId == length.Key);
                                Require(block?.Timing != null && length.Value >= block.Timing.MinimumFrames, "DSL_BLOCK_LENGTH: 制作长度引用缺失或小于最小持续帧。");
                                block.Timing.DurationFrames = length.Value;
                            }
                        }
                        Require(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(instanceSource, _inputs.ForInstance(node.NodeId), out value, out var errors, ResolveDistanceBindings(node)),
                            "DSL_SHARED_COMPILE: " + string.Join("; ", errors));
                        _compiled.Add(compileKey, value);
                    }
                    _boundaries.Add(node.NodeId, _builder.ImportSingleTimeline(value.Program, node.NodeId, next));
                    AddLocalEnds(node.NodeId, next == null ? _boundaries[node.NodeId] : checked(_boundaries[node.NodeId] + 1), scope);
                    foreach (var evt in value.Events)
                    {
                        _sources.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "event:" + evt.EventId),
                            _source.CardId + "/" + node.NodeId + "/" + definition.DefinitionId + "@" + definition.Revision + "/" + evt.SourcePath);
                        _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "event:" + evt.EventId),
                            new CharacterTestTacticalSourceLocation(_source.CardId, node.NodeId, "events", definition.DefinitionId, definition.Revision, evt.EventId, block: evt.BlockId));
                    }
                    if (next != null)
                        _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$next"),
                            new CharacterTestTacticalSourceLocation(_source.CardId, node.NodeId, "sequenceContinuation", definition.DefinitionId, definition.Revision));
                    for (int i = 0; i < value.Program.Execution.Conditions.Length; i++)
                    {
                        string conditionId = value.Program.ExecutionDebugMap.Condition(i);
                        var ownerEvent = definition.Source.Timelines[0].Events.FirstOrDefault(e => ContainsCondition(e.ConditionRoot, conditionId));
                        _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "condition:" + i),
                            new CharacterTestTacticalSourceLocation(_source.CardId, node.NodeId,
                                ownerEvent == null ? "entryConditionRoot" : "conditionRoot", definition.DefinitionId, definition.Revision,
                                ownerEvent?.EventId, conditionId));
                    }
                    return node.NodeId;
                }
                if (node.Kind == "Exit")
                {
                    _builder.AddTimeline(node.NodeId, 1);
                    _builder.AddReturn(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$exit"), node.NodeId, 0, 1);
                    Locate(node.NodeId, "$exit", "exit");
                    return node.NodeId;
                }
                string body = Children(node, next, scope);
                int condition = Condition(node.Condition, node.NodeId);
                int controlDuration = node.Kind == "Wait" ? node.MaximumWaitFrames ?? 60 : body != null ? 1 : 0;
                _builder.AddTimeline(node.NodeId, controlDuration);
                AddLocalEnds(node.NodeId, controlDuration, scope);
                if (node.Kind == "Wait")
                {
                    _builder.AddBlock(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$limit"), node.NodeId,
                        0, controlDuration, CharacterTestBehaviorBlockCompletion.Timed);
                    _builder.AddGate(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$wait"), node.NodeId, 0, condition);
                    if (body != null)
                    {
                        _builder.AddJump(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$next"), node.NodeId, 1, body);
                        Locate(node.NodeId, "$next", "children");
                    }
                    else _builder.AddReturn(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$done"), node.NodeId, 1, 1);
                    Locate(node.NodeId, "$wait", "condition");
                }
                else
                {
                    // The evaluator takes the first eligible jump. A lower-ranked fallback avoids
                    // evaluating the authored condition a second time through an inverted predicate.
                    if (body != null)
                    {
                        _builder.AddJump(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$yes"), node.NodeId, 0, body, condition, 1);
                        Locate(node.NodeId, "$yes", "condition");
                    }
                    if (next != null)
                    {
                        _builder.AddJump(CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "$no"), node.NodeId, 0, next);
                        Locate(node.NodeId, "$no", "condition");
                    }
                }
                return node.NodeId;
            }

            private void AddLocalEnds(string timeline, int duration, List<LocalEnd> scope)
            {
                if (duration <= 0) return;
                for (int i = 0; i < scope.Count; i++)
                {
                    string id = CharacterTestBehaviorExecutionBuilder.CompositionId(timeline, "$end:" + scope[i].Owner);
                    if (scope[i].Next == null)
                        _builder.AddReturn(id, timeline, 0, duration, scope[i].Condition, 100000 - i);
                    else
                        _builder.AddJump(id, timeline, 0, scope[i].Next, scope[i].Condition, 100000 - i, duration);
                    _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(timeline, "$end:" + scope[i].Owner),
                        new CharacterTestTacticalSourceLocation(_source.CardId, scope[i].Owner, "endCondition"));
                }
            }

            private void Locate(string node, string local, string field)
                => _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(node, local), new CharacterTestTacticalSourceLocation(_source.CardId, node, field));

            private string Children(CharacterTestTacticalDslNode node, string next, List<LocalEnd> scope)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--) next = Lower(node.Children[i], next, scope);
                return next;
            }

            private static bool ContainsCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, string id)
                => root != null && (root.NodeId == id || root.Children.Any(child => ContainsCondition(child, id)));

            private int Condition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, string owner, string nodeId = null, string field = "condition")
            {
                if (root == null) return -1;
                Require(!CharacterTestTacticalAuthoringCompiler.ContainsAttackWaitFact(root),
                    "DSL_ATTACK_WAIT_SCOPE: 当前攻击窗口结束条件只能在共享等待战机封装内部使用。");
                if (HasEventSignal(root) || HasMovementQuery(root))
                {
                    Require(owner != "$activation", "DSL_ACTIVATION_EVENT_SIGNAL: Card eligibility cannot read an execution result.");
                    return ResultCondition(root, owner, nodeId ?? owner, field);
                }
                // Activation validation already rejects EventSignal and unbound action-data leaves.
                var temporary = CharacterTestTacticalAuthoringTemplates.CreateBlank("DSL condition");
                temporary.ActivationConditionRoot = root;
                Require(CharacterTestTacticalAuthoringCompiler.TryCompile(temporary, _inputs, out var result, out var errors),
                    "DSL_CONDITION: " + string.Join("; ", errors));
                int offset = _builder.ImportConditions(result.Program, owner);
                for (int i = 0; i < result.Program.Execution.Conditions.Length; i++)
                    _locations.Add(CharacterTestBehaviorExecutionBuilder.CompositionId(owner, "condition:" + i),
                        new CharacterTestTacticalSourceLocation(_source.CardId, nodeId ?? owner, field,
                            condition: result.Program.ExecutionDebugMap.Condition(i)));
                return offset + result.Program.Execution.ActivationCondition;
            }

            private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
            private static bool HasEventSignal(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root)
                => root != null && (root.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal || root.Children.Any(HasEventSignal));

            private int ResultCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, string owner, string nodeId, string field)
            {
                string id = CharacterTestBehaviorExecutionBuilder.CompositionId(owner, "result-condition:" + root.NodeId);
                if (root.MovementQuery != null) return MovementCondition(root, id, nodeId, field);
                if (!HasEventSignal(root) && !HasMovementQuery(root)) return Condition(root, owner + ":" + root.NodeId, nodeId, field);
                _locations.Add(id, new CharacterTestTacticalSourceLocation(_source.CardId, nodeId, field, condition: root.NodeId));
                if (root.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal)
                {
                    Require(root.Children.Count == 0 && root.Predicate == null && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal), root.Signal), "DSL_RESULT_CONDITION");
                    string reference = nodeId, eventId = root.SourceEventId;
                    int separator = eventId?.IndexOf('/') ?? -1;
                    if (separator > 0) { reference = eventId.Substring(0, separator); eventId = eventId.Substring(separator + 1); }
                    var use = Nodes(_source.Root).FirstOrDefault(n => n.NodeId == reference && n.Kind == "Reference");
                    Require(use != null && _definitions.ContainsKey(use.DefinitionId), "DSL_RESULT_REFERENCE: Select an explicit card reference and event.");
                    var evt = _definitions[use.DefinitionId].Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == eventId);
                    Require(evt != null, "DSL_RESULT_EVENT: Referenced event does not exist.");
                    if (root.Signal == CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed)
                        Require(evt.Operation.Kind == "CharacterAction", "DSL_COMMIT_RESULT_ACTION: CommitFailed belongs to an action event.");
                    return _builder.AddEventSignal(id, CharacterTestBehaviorExecutionBuilder.CompositionId(reference, "event:" + eventId), root.Signal);
                }
                var children = root.Children.Select(child => ResultCondition(child, owner, nodeId, field)).ToArray();
                switch (root.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All: return _builder.AddAll(id, children);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any: return _builder.AddAny(id, children);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                        Require(children.Length == 1, "DSL_RESULT_NOT_CHILD"); return _builder.AddNot(id, children[0]);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                        Require(children.Length == 1, "DSL_RESULT_SUBJECT_CHILD"); return _builder.AddSubjectScope(id, root.Subject, children[0]);
                    default: throw new ArgumentException("DSL_RESULT_CONDITION_KIND");
                }
            }

            private static bool HasMovementQuery(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root)
                => root != null && (root.MovementQuery != null || root.Children.Any(HasMovementQuery));

            private int MovementCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, string id, string nodeId, string field)
            {
                Require(root.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate && root.Predicate == null && root.Children.Count == 0,
                    "DSL_MOVEMENT_QUERY_SHAPE: Movement query must be an explicit bound leaf.");
                Require(root.MovementQuery == "Arrived" || root.MovementQuery == "TargetMissing", "DSL_MOVEMENT_QUERY_KIND");
                string reference = nodeId, eventId = root.SourceEventId;
                int separator = eventId?.IndexOf('/') ?? -1;
                if (separator > 0) { reference = eventId.Substring(0, separator); eventId = eventId.Substring(separator + 1); }
                var use = Nodes(_source.Root).FirstOrDefault(n => n.NodeId == reference && n.Kind == "Reference");
                Require(use != null && _definitions.ContainsKey(use.DefinitionId), "DSL_MOVEMENT_REFERENCE: Select an explicit reference and movement event.");
                var source = _definitions[use.DefinitionId].Source;
                var evt = source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == eventId);
                Require(evt?.Operation.Kind == "Movement" && evt.Operation.Movement != null, "DSL_MOVEMENT_EVENT: Referenced movement no longer exists.");
                bool missing = root.MovementQuery == "TargetMissing";
                Require(missing || evt.Operation.Movement.UsesDistance, "DSL_MOVEMENT_ARRIVAL: This movement has no arrival distance; use an explicit time or condition.");
                var movement = evt.Operation.Movement.Compile(source, _inputs, evt.Operation.Speed, evt.EventId);
                _locations.Add(id, new CharacterTestTacticalSourceLocation(_source.CardId, nodeId, field,
                    use.DefinitionId, _definitions[use.DefinitionId].Revision, eventId, root.NodeId));
                return _builder.AddMovementCondition(id, movement, missing);
            }

            private static IEnumerable<CharacterTestTacticalDslNode> Nodes(CharacterTestTacticalDslNode node)
            {
                yield return node;
                foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                    foreach (var nested in Nodes(child)) yield return nested;
            }
            private static void Require(bool value, string issue) { if (!value) throw new ArgumentException(issue); }
        }
    }
}
