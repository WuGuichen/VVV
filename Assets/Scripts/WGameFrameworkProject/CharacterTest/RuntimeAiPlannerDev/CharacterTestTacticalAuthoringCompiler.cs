using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestTacticalResolvedEvent
    {
        internal CharacterTestTacticalResolvedEvent(string id, string path, int start, int end, int actionId = 0, string blockId = "")
        { EventId = id; SourcePath = path; StartFrame = start; EndFrame = end; ActionId = actionId; BlockId = blockId; }
        internal string BlockId { get; }
        internal int ActionId { get; }
        internal string EventId { get; }
        internal string SourcePath { get; }
        internal int StartFrame { get; }
        internal int EndFrame { get; }
    }

    internal sealed class CharacterTestTacticalAuthoringCompilation
    {
        private readonly CharacterTestTacticalResolvedEvent[] _events;
        private readonly CharacterTestTacticalResolvedEvent[] _blocks;
        internal CharacterTestTacticalAuthoringCompilation(CharacterTestRuntimeAiLabCompiledLogicProgram program,
            string sourceFingerprint, CharacterTestTacticalCardInputs inputs, CharacterTestTacticalResolvedEvent[] events,
            CharacterTestTacticalResolvedEvent[] blocks = null)
        {
            Program = program; SourceFingerprint = sourceFingerprint; DependencyFingerprint = inputs.DependencyFingerprint;
            SourceCharacterId = inputs.SourceCharacterId; ActionSetId = inputs.ActionSetId; ActionId = inputs.Action?.ActionId.Value ?? 0;
            Summary = inputs.SpatialSummary; _events = events; _blocks = blocks ?? Array.Empty<CharacterTestTacticalResolvedEvent>();
        }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        internal string SourceFingerprint { get; }
        internal string DependencyFingerprint { get; }
        internal int SourceCharacterId { get; }
        internal string ActionSetId { get; }
        internal int ActionId { get; }
        internal CharacterActionSpatialSummary Summary { get; }
        internal int EventActionId(string eventId)
        {
            foreach (var evt in _events) if (evt.EventId == eventId) return evt.ActionId;
            return 0;
        }
        internal ReadOnlySpan<CharacterTestTacticalResolvedEvent> Events => _events;
        internal ReadOnlySpan<CharacterTestTacticalResolvedEvent> Blocks => _blocks;
        internal bool TryBlockFrames(string id, out int start, out int end)
        {
            foreach (var block in _blocks) if (block.EventId == id) { start = block.StartFrame; end = block.EndFrame; return true; }
            start = end = 0; return false;
        }
    }

    /// <summary>Lowers the actual editable source to the shared execution builder, without template regeneration.</summary>
    /// <remarks>LowFreqAlloc. Input preparation and compilation are forbidden while replacing an active binding.</remarks>
    internal static partial class CharacterTestTacticalAuthoringCompiler
    {
        internal static IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> Validate(
            CharacterTestTacticalAuthoringSource source, CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
        {
            var context = new Context(source, null, references);
            context.Validate();
            return context.Issues.ToArray();
        }

        internal static IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> ValidateShared(CharacterTestTacticalAuthoringSource source)
        {
            var context = new Context(source, null, null, true); context.Validate(); return context.Issues.ToArray();
        }

        internal static bool TryCompile(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs,
            out CharacterTestTacticalAuthoringCompilation compilation,
            out IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> diagnostics)
            => TryCompileCore(source, inputs, false, out compilation, out diagnostics);

        internal static bool TryCompileSharedTrial(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs,
            out CharacterTestTacticalAuthoringCompilation compilation, out IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> diagnostics,
            IReadOnlyDictionary<string, CharacterTestTacticalDistanceData> distanceInputs = null)
            => TryCompileCore(source, inputs, true, out compilation, out diagnostics, distanceInputs);

        private static bool TryCompileCore(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs, bool allowTrial,
            out CharacterTestTacticalAuthoringCompilation compilation, out IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> diagnostics,
            IReadOnlyDictionary<string, CharacterTestTacticalDistanceData> distanceInputs = null)
        {
            compilation = null;
            var issues = new List<CharacterTestTacticalAuthoringDiagnostic>();
            diagnostics = issues;
            try
            {
                if (source == null || inputs == null)
                { issues.Add(new CharacterTestTacticalAuthoringDiagnostic("InputsMissing", "$", "Source and current prepared inputs are required.")); return false; }
                if (!inputs.TryValidatePreparedAction(out string issue))
                { issues.Add(new CharacterTestTacticalAuthoringDiagnostic("PreparedInputsStale", "$.slots", issue)); return false; }
                // Only this owned copy is used for lowering and source-map construction.
                var owned = source.Clone();
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                    new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), inputs.CompilationActions));
                var context = new Context(owned, inputs, references, allowTrial, distanceInputs);
                context.Validate();
                issues.AddRange(context.Issues);
                if (issues.Count != 0) return false;
                var required = CharacterTestTacticalDependencies.Analyze(owned);
                if (!inputs.HasExplicitBindings && (inputs.Requirements & required) != required)
                { issues.Add(new CharacterTestTacticalAuthoringDiagnostic("PreparedInputsIncomplete", "$.slots", "当前卡需要的数据未准备完整。")); return false; }
                compilation = context.Build();
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is OverflowException || error is Newtonsoft.Json.JsonException)
            {
                issues.Add(new CharacterTestTacticalAuthoringDiagnostic("CompileRejected", "$", error.Message));
                return false;
            }
        }

        private sealed class IndexedEvent
        {
            internal CharacterTestTacticalAuthoringEvent Source;
            internal string Timeline, Path;
        }

        private sealed partial class Context
        {
            private readonly CharacterTestTacticalAuthoringSource _source;
            private readonly CharacterTestTacticalCardInputs _inputs;
            private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;
            private readonly bool _allowTrial;
            private readonly Dictionary<string, IndexedEvent> _events = new Dictionary<string, IndexedEvent>(StringComparer.Ordinal);
            private readonly Dictionary<string, CharacterTestTacticalPredicateBinding> _bindings = new Dictionary<string, CharacterTestTacticalPredicateBinding>(StringComparer.Ordinal);
            private readonly HashSet<string> _nodeIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _usedBindings = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _eventIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _eventOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _positions = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> _visiting = new HashSet<string>(StringComparer.Ordinal);
            internal readonly List<CharacterTestTacticalAuthoringDiagnostic> Issues = new List<CharacterTestTacticalAuthoringDiagnostic>();
            internal Context(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references, bool allowTrial = false,
                IReadOnlyDictionary<string, CharacterTestTacticalDistanceData> distanceInputs = null)
            { _source = source; _inputs = inputs; _references = references; _allowTrial = allowTrial; _distanceInputs = distanceInputs; }

            private void Error(string code, string path, string message) => Issues.Add(new CharacterTestTacticalAuthoringDiagnostic(code, path, message));
            private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;

            internal void Validate()
            {
                bool trial = _allowTrial && (_source?.Schema == CharacterTestTacticalAuthoringSource.EventSetTrialSchema
                    || _source?.Schema == CharacterTestTacticalAuthoringSource.EventSetSchema) && _source.Version == 1;
                if (_source == null || !trial && !_source.IsSupportedVersion)
                { Error("SourceVersion", "$", "A supported tactical v2/v3 source is required."); return; }
                if (_source.Slots == null || _source.PredicateBindings == null || _source.Timelines == null || _source.Timelines.Count == 0)
                { Error("SourceCollections", "$", "Slots, bindings and at least one Timeline are required."); return; }
                if (_source.ExecutionInput != null || _source.PositionRecall != null || _source.InteractionPreparation != null)
                {
                    try { CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(_source)); }
                    catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException)
                    { Error("PointContractInvalid", "$.executionInput", error.Message); return; }
                    if (_allowTrial) { Error("PointContractCardOnly", "$.executionInput", "Per-play input is owned by a card, never a shared definition."); return; }
                }
                var slots = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < _source.Slots.Count; i++)
                {
                    var slot = _source.Slots[i];
                    if (slot == null || !ValidId(slot.SlotId) || !slots.Add(slot.SlotId))
                        Error("SlotIdentity", "$.slots[" + i + "]", "Missing or duplicate slot identity.");
                    else if (slot.Resolver != "binding" && slot.Resolver != "lightAttackOpener" && slot.Resolver != "ordinaryGuard" && !slot.IsFunctional)
                        Error("SlotUnsupported", "$.slots[" + i + "].resolver", "Use explicit binding. Legacy resolver names are read for migration only.");
                }
                var timelines = new HashSet<string>(StringComparer.Ordinal);
                for (int t = 0; t < _source.Timelines.Count; t++)
                {
                    var timeline = _source.Timelines[t];
                    string tp = "$.timelines[" + t + "]";
                    if (timeline == null || !ValidId(timeline.TimelineId) || !timelines.Add(timeline.TimelineId) || timeline.Events == null)
                    { Error("TimelineIdentity", tp, "Missing or duplicate Timeline identity or events."); continue; }
                    for (int e = 0; e < timeline.Events.Count; e++)
                    {
                        var evt = timeline.Events[e];
                        string ep = tp + ".events[" + e + "]";
                        if (evt == null || !ValidId(evt.EventId) || _events.ContainsKey(evt.EventId))
                        { Error("EventIdentity", ep, "Missing or duplicate event identity."); continue; }
                        _eventIndices.Add(evt.EventId, _eventIndices.Count);
                        _eventOwners.Add(evt.EventId, timeline.TimelineId);
                        _events.Add(evt.EventId, new IndexedEvent { Source = evt, Timeline = timeline.TimelineId, Path = ep });
                        if (evt.Operation == null) { Error("OperationMissing", ep, "An event operation is required."); continue; }
                        string kind = evt.Operation.Kind;
                        bool action = kind == "CharacterAction", gate = kind == "WaitForCondition";
                        bool retreat = (trial || _source.HasBlocksVersion) && kind == "RetreatFromTarget";
                        bool flow = _source.HasTimingVersion && (kind == "LoopRegion" || kind == "EnterTimeline" || kind == "CompleteBlock");
                        if (!action && !gate && !retreat && !flow && kind != "Movement" && kind != "ApproachOptimalRange" && kind != "TurnToTarget" && kind != "ReturnToPlanner")
                            Error("OperationUnsupported", ep + ".operation.kind", "This operation is not supported by the tactical front end.");
                        bool instant = evt.TimingKind == "Instant";
                        if (evt.TimingKind != (gate ? "Instant" : "Duration") && !(_source.HasTimingVersion && instant && (kind == "ReturnToPlanner" || kind == "EnterTimeline")))
                            Error("TimingUnsupported", ep + ".timingKind", "Unsupported time category for this operation.");
                        if ((!_source.HasTimingVersion || action || instant) && (evt.MaxExecutionsPerPass != 1 || evt.CooldownFrames != 0)
                            || evt.MaxExecutionsPerPass < 0 || evt.CooldownFrames < 0)
                            Error("RepeatUnsupported", ep, "This front end requires once per pass and zero cooldown.");
                        if (flow && kind == "EnterTimeline" && !_source.Timelines.Any(tl => tl.TimelineId == evt.Operation.TargetTimelineId)
                            || kind == "LoopRegion" && evt.Operation.MaxLoopRepeats < 0)
                            Error("FlowInvalid", ep, "跳转目标不存在或循环次数为负数。");
                        if (action && !slots.Contains(evt.Operation.SlotId))
                            Error("SlotMissing", ep + ".operation.slotId", "Select a supported functional slot.");
                        if ((action || instant) && evt.End != null)
                            Error("DerivedDuration", ep + ".end", "Action duration and Instant marker length are not editable end values.");
                        if (!action && !instant && evt.End == null)
                            Error("EndMissing", ep + ".end", "A duration event needs an explicit right boundary.");
                        if ((kind == "TurnToTarget" || retreat) && evt.Operation.Target != "currentCombatTarget")
                            Error("TargetUnsupported", ep + ".operation.target", "Only the current combat target is supported.");
                        if (float.IsNaN(evt.Operation.Speed) || float.IsInfinity(evt.Operation.Speed)
                            || evt.Operation.Speed < 0.1f || evt.Operation.Speed > 1f)
                            Error("SpeedInvalid", ep + ".operation.speed", "Speed must be finite and within [0.1,1].");
                        if (kind == "Movement")
                        {
                            try { if (evt.Operation.Movement == null) throw new ArgumentException("移动事件缺少配置。"); evt.Operation.Movement.Compile(_source, _inputs, evt.Operation.Speed, evt.EventId); }
                            catch (ArgumentException error) { Error("MovementInvalid", ep + ".operation.movement", error.Message); }
                        }
                        else if (evt.Operation.Movement != null) Error("UnusedMovement", ep, "非移动事件不能携带移动配置。");
                        if (evt.Operation.FacingSource != null)
                        {
                            if (kind != "TurnToTarget" || evt.Operation.FacingSource != "Manual" && evt.Operation.FacingSource != "ActionFacing"
                                || evt.Operation.FacingSource == "ActionFacing" && (!slots.Contains(evt.Operation.FacingActionSlotId) || evt.Operation.FacingToleranceDegrees != null)
                                || evt.Operation.FacingSource == "Manual" && (!evt.Operation.FacingToleranceDegrees.HasValue
                                    || float.IsNaN(evt.Operation.FacingToleranceDegrees.Value) || float.IsInfinity(evt.Operation.FacingToleranceDegrees.Value)
                                    || evt.Operation.FacingToleranceDegrees.Value <= 0 || evt.Operation.FacingToleranceDegrees.Value > 180
                                    || !string.IsNullOrEmpty(evt.Operation.FacingActionSlotId)))
                                Error("FacingParameters", ep, "面向参数必须明确引用装配动作，或在转向事件中配置 (0,180] 度的容差。");
                        }
                        else if (evt.Operation.FacingToleranceDegrees != null || evt.Operation.FacingActionSlotId != null)
                            Error("FacingParameters", ep, "面向参数缺少明确来源。");
                        if (!action && !string.IsNullOrEmpty(evt.Operation.SlotId)
                            || kind != "ApproachOptimalRange" && !string.IsNullOrEmpty(evt.Operation.SourceActionEventId)
                            || kind != "TurnToTarget" && !retreat && !string.IsNullOrEmpty(evt.Operation.Target)
                            || kind != "TurnToTarget" && kind != "ApproachOptimalRange" && kind != "Movement" && !retreat && evt.Operation.Speed != 1f)
                            Error("UnusedOperationParameter", ep + ".operation", "Parameters for another operation cannot be silently ignored.");
                    }
                }
                if (!timelines.Contains(_source.EntryTimelineId ?? string.Empty))
                    Error("EntryMissing", "$.entryTimelineId", "Entry Timeline does not exist.");
                foreach (var binding in _source.PredicateBindings)
                {
                    if (binding == null || !ValidId(binding.NodeId) || _bindings.ContainsKey(binding.NodeId))
                    { Error("BindingIdentity", "$.predicateBindings", "Missing or duplicate predicate binding."); continue; }
                    _bindings.Add(binding.NodeId, binding);
                    if (binding.Kind != "IntentDistanceRange" && binding.SourceBlockId != null)
                        Error("UnusedDistanceReference", "$.predicateBindings", "Block distance references belong only to explicit intent-distance bindings.");
                    if (binding.Kind == "BoundActionOpportunity")
                        Error("ActionOpportunityRemoved", "$.predicateBindings." + binding.NodeId,
                            "动作机会已停用；请删除旧条件并使用明确的距离、朝向等条件。旧源保留，不自动忽略条件。");
                    else if (binding.Kind != "RootDistanceRange" && binding.Kind != "TargetFacing" && binding.Kind != "IntentDistanceRange" && binding.Kind != "MovementArrived" && binding.Kind != "TurnAligned")
                        Error("BindingUnsupported", "$.predicateBindings", "Unsupported action data binding.");
                    if (binding.Kind != "MovementArrived" && binding.SourceMovementEventId != null)
                        Error("UnusedMovementReference", "$.predicateBindings", "Movement references belong only to movement-arrival conditions.");
                    if (binding.Kind != "TurnAligned" && binding.SourceTurnEventId != null)
                        Error("UnusedTurnReference", "$.predicateBindings", "Turn references belong only to alignment conditions.");
                }
                ValidateBlocks();
                foreach (var pair in _events)
                {
                    var item = pair.Value;
                    if (item.Source.Operation?.Kind == "CompleteBlock")
                    {
                        if (item.Source.ConditionRoot == null || !_eventBlocks.TryGetValue(item.Source.EventId, out string completionBlock)
                            || _blocks[completionBlock].Source.EventIds.Any(id => _events[id].Source.Operation?.Kind == "CharacterAction")
                            || _blocks[completionBlock].Source.Completion != "Timed" && !CharacterTestTacticalApproachActionSplit.IsImplicitArrival(_source, _blocks[completionBlock].Source))
                            Error("CompletionScope", item.Path, "结束当前意图段需要明确条件，且必须属于不含角色动作的固定时间意图段。");
                    }
                    ValidateFrame(item, item.Source.Start, item.Path + ".start");
                    if (item.Source.End != null) ValidateFrame(item, item.Source.End, item.Path + ".end");
                    if (item.Source.Operation?.Kind == "ApproachOptimalRange")
                        ValidateActionReference(item.Source.Operation.SourceActionEventId, item.Timeline, item.Path + ".operation.sourceActionEventId", requiresAttackSummary: true);
                }
                ValidateTree(_source.ActivationConditionRoot, "$.activationConditionRoot", false, _source.EntryTimelineId, null);
                foreach (var timeline in _source.Timelines)
                {
                    if (timeline == null || timeline.Events == null) continue;
                    ValidateTree(timeline.EntryConditionRoot, "$.timelines." + timeline.TimelineId + ".entryConditionRoot", false, timeline.TimelineId, null);
                    foreach (var evt in timeline.Events)
                    {
                        if (evt == null) continue;
                        if (evt.Operation?.Kind == "CharacterAction") RetainLegacyPlaybackBindings(evt.ConditionRoot);
                        else ValidateTree(evt.ConditionRoot, "$.events." + evt.EventId + ".conditionRoot", true, timeline.TimelineId, evt.EventId);
                    }
                }
                foreach (var id in _bindings.Keys)
                    if (!_usedBindings.Contains(id)) Error("UnusedBinding", "$.predicateBindings." + id, "Binding has no corresponding condition leaf.");
                if (Issues.Count == 0)
                {
                    var finished = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var pair in _events)
                    {
                        ValidateDependencyGraph(pair.Key + ".Start", new HashSet<string>(StringComparer.Ordinal), finished);
                        ValidateDependencyGraph(pair.Key + ".End", new HashSet<string>(StringComparer.Ordinal), finished);
                    }
                    foreach (var id in _blocks.Keys)
                    {
                        ValidateDependencyGraph(id + ".Start", new HashSet<string>(StringComparer.Ordinal), finished);
                        ValidateDependencyGraph(id + ".End", new HashSet<string>(StringComparer.Ordinal), finished);
                    }
                }
                if (Issues.Count == 0 && _inputs != null)
                {
                    if (_source.HasTimingVersion)
                    {
                        _timing = new CharacterTestTacticalTimingEvaluation(_source, _inputs);
                        _timing.Evaluate();
                        foreach (string issue in _timing.Issues) Error("TimingInvalid", "$.timelines", issue);
                        if (Issues.Count != 0) return;
                    }
                    var waitingPoints = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in _events.Values)
                    {
                        bool valid = TryPosition(item, "Start", out int start);
                        valid &= TryPosition(item, "End", out int end);
                        if (!valid) continue;
                        if (end <= start) Error("InvalidRange", item.Path + ".end", "Right boundary must be greater than start.");
                        if (_eventBlocks.TryGetValue(item.Source.EventId, out var owner) && start < Position(owner, "Start"))
                            Error("BeforeBlock", item.Path + ".start", "Member event starts before its block origin.");
                        if (item.Source.Operation.Kind == "WaitForCondition"
                            && !waitingPoints.Add(item.Timeline + ":" + start))
                            Error("DuplicateGate", item.Path + ".start", "Only one waiting point is allowed at the same Timeline frame.");
                    }
                }
            }

            private void ValidateFrame(IndexedEvent owner, CharacterTestTacticalFrameReference frame, string path)
            {
                if (frame == null) { Error("FrameMissing", path, "A frame reference is required."); return; }
                if (frame.Space != "Timeline" && frame.Space != "Block" || frame.TargetKind != "Event" && frame.TargetKind != "Block" && frame.TargetKind != "Builtin")
                    Error("FrameScope", path, "Unknown coordinate or target kind.");
                if (frame.Space == "Block" && (frame.Kind != "Absolute" || !_eventBlocks.ContainsKey(owner.Source.EventId)))
                    Error("BlockCoordinate", path, "Block coordinates require a member event and an absolute frame.");
                if (frame.Kind == "Absolute")
                {
                    if (frame.Frame < 0) Error("NegativeFrame", path, "Frame must be nonnegative.");
                    if (!string.IsNullOrEmpty(frame.SourceEventId) || !string.IsNullOrEmpty(frame.Anchor) || frame.OffsetFrames != 0 || frame.TargetKind != "Event")
                        Error("AmbiguousFrame", path, "Absolute frame contains anchor parameters.");
                }
                else if (frame.Kind == "Anchor" || frame.Kind == "Ratio")
                {
                    if (frame.Kind == "Ratio" && (!_source.HasTimingVersion || double.IsNaN(frame.Ratio) || double.IsInfinity(frame.Ratio) || frame.OffsetFrames != 0))
                        Error("RatioInvalid", path, "比例必须是有限数值，不能同时使用偏移；需要 v4。");
                    if (frame.Frame != 0) Error("AmbiguousFrame", path, "Anchor contains an absolute frame.");
                    if (frame.TargetKind == "Builtin")
                    {
                        if (!_source.HasTimingVersion || !_blocks.TryGetValue(frame.SourceEventId ?? "", out var builtin) || builtin.Source.Timing == null
                            || !_eventBlocks.TryGetValue(owner.Source.EventId, out string ownerBlock) || ownerBlock != frame.SourceEventId)
                            Error("BuiltinMissing", path, "固有锚点必须属于当前事件所在的命名项。");
                        if (frame.Anchor != "Minimum" && frame.Anchor != "SimulationEnd") Error("BuiltinAnchor", path, "未知固有锚点。");
                        return;
                    }
                    if (frame.TargetKind == "Block")
                    {
                        if (!_blocks.TryGetValue(frame.SourceEventId ?? string.Empty, out var block)) Error("MissingBlock", path, "Referenced block does not exist.");
                        else if (block.Timeline != owner.Timeline) Error("CrossTimelineReference", path, "Block references use the same Timeline.");
                        if (frame.Anchor != "Start" && frame.Anchor != "End") Error("BlockAnchor", path, "Blocks expose only Start and End.");
                        return;
                    }
                    if (!_events.TryGetValue(frame.SourceEventId ?? string.Empty, out var target))
                        Error("MissingEvent", path, "Referenced event does not exist: " + frame.SourceEventId);
                    else if (target.Timeline != owner.Timeline)
                        Error("CrossTimelineReference", path, "Time references use the same local Timeline.");
                    if (frame.Anchor != "Start" && frame.Anchor != "End" && frame.Anchor != "ActionStartupEnd" && frame.Anchor != "ActionEnd")
                        Error("AnchorUnsupported", path, "Unknown anchor.");
                    else if (frame.Anchor == "ActionStartupEnd" || frame.Anchor == "ActionEnd")
                        ValidateActionReference(frame.SourceEventId, owner.Timeline, path);
                }
                else Error("FrameKind", path, "Unknown frame reference kind.");
            }

            private void ValidateActionReference(string id, string timeline, string path, bool requiresAttackSummary = false)
            {
                if (!_events.TryGetValue(id ?? string.Empty, out var evt) || evt.Source.Operation?.Kind != "CharacterAction")
                    Error("ActionReferenceMissing", path, "Reference must select an existing CharacterAction event.");
                else if (evt.Timeline != timeline)
                    Error("CrossTimelineReference", path, "Action data must come from the same Timeline.");
                else if (requiresAttackSummary && _source.Slots.FirstOrDefault(s => s?.SlotId == evt.Source.Operation.SlotId)?.IsFunctional == true)
                    Error("ActionSummaryUnsupported", path, "动作距离／朝向摘要只能读取攻击动作，不能借用攻击摘要解释后撤动作。");
            }

            private void RetainLegacyPlaybackBindings(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null) return;
                if (node.NodeId != null && _bindings.ContainsKey(node.NodeId)) _usedBindings.Add(node.NodeId);
                if (node.Children != null) foreach (var child in node.Children) RetainLegacyPlaybackBindings(child);
            }

            private void ValidateTree(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, string path,
                bool allowSignal, string timeline, string owner)
            {
                if (root == null) return;
                int issueCount = Issues.Count;
                Walk(root, path, timeline, 0);
                if (Issues.Count != issueCount) return;
                var native = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
                var boundIds = new HashSet<string>(_bindings.Keys, StringComparer.Ordinal);
                CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.ValidateCondition(root, path, allowSignal, timeline, owner,
                    _eventIndices, _eventOwners, _references, native, externallyBoundPredicates: boundIds);
                foreach (var issue in native) Error(issue.Code, issue.Path, issue.Message);
            }

            private void Walk(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, string path, string timeline, int depth)
            {
                if (node == null) { Error("ConditionMissing", path, "Null child condition."); return; }
                if (depth > 32) { Error("ConditionDepth", path, "Condition structure exceeds its supported depth."); return; }
                if (!ValidId(node.NodeId) || !_nodeIds.Add(node.NodeId))
                    Error("ConditionIdentity", path, "Missing or duplicate node identity.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind), node.Kind))
                    Error("ConditionKind", path, "Unknown condition kind.");
                bool leaf = node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                    || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal;
                if (leaf && node.Children.Count != 0) Error("ConditionArity", path, "A leaf cannot contain child conditions.");
                if (_bindings.TryGetValue(node.NodeId ?? string.Empty, out var binding))
                {
                    _usedBindings.Add(node.NodeId);
                    if (node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate || node.Predicate != null)
                        Error("AmbiguousPredicateSource", path, "Bound leaf must be a FactPredicate without a literal predicate.");
                    if (binding.Kind == "TurnAligned")
                    {
                        if (!string.IsNullOrEmpty(binding.SourceActionEventId) || !_events.TryGetValue(binding.SourceTurnEventId ?? string.Empty, out var turn)
                            || turn.Timeline != timeline || turn.Source.Operation?.Kind != "TurnToTarget" || turn.Source.Operation.FacingSource == null)
                            Error("TurnAlignmentReference", path, "面向达标条件必须引用同一时间轴中已配置面向参数的转向事件。");
                    }
                    else if (binding.Kind == "MovementArrived")
                    {
                        if (!string.IsNullOrEmpty(binding.SourceActionEventId) || !_events.TryGetValue(binding.SourceMovementEventId ?? string.Empty, out var move)
                            || move.Timeline != timeline || move.Source.Operation?.Kind != "Movement" || move.Source.Operation.Movement == null
                            || !move.Source.Operation.Movement.UsesDistance || move.Source.Operation.Movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget)
                            Error("MovementArrivalReference", path, "移动到位条件必须引用同一时间轴中针对当前目标、有距离配置的移动事件。");
                    }
                    else if (binding.Kind == "IntentDistanceRange")
                    {
                        if (!string.IsNullOrEmpty(binding.SourceActionEventId)) Error("AmbiguousDistanceSource", path, "Intent distance must explicitly reference configuration data, not an implicit action.");
                        if (_inputs != null || !string.IsNullOrEmpty(binding.SourceBlockId))
                            try { BoundIntentDistance(binding, timeline); }
                            catch (ArgumentException error) { Error("IntentDistanceReference", path, error.Message); }
                    }
                    else ValidateActionReference(binding.SourceActionEventId, timeline, path, requiresAttackSummary: true);
                }
                for (int i = 0; i < node.Children.Count; i++) Walk(node.Children[i], path + ".children[" + i + "]", timeline, depth + 1);
            }

            private IEnumerable<string> Dependencies(string id, string side)
            {
                if (_blocks.TryGetValue(id, out var block))
                {
                    if (side == "End" && block.Source.Timing == null)
                    {
                        yield return id + ".Start";
                        foreach (string member in block.Source.EventIds) yield return member + ".End";
                    }
                    else if (side != "Start") yield return id + ".Start";
                    else if (block.Source.Start.Kind == "Anchor" || block.Source.Start.Kind == "Ratio") yield return block.Source.Start.SourceEventId + "." + block.Source.Start.Anchor;
                    yield break;
                }
                var evt = _events[id].Source;
                if (side == "ActionEnd" || side == "ActionStartupEnd" || side == "End"
                    && (evt.Operation.Kind == "CharacterAction" || evt.TimingKind == "Instant"))
                { yield return id + ".Start"; yield break; }
                var frame = side == "Start" ? evt.Start : evt.End;
                if (frame?.Kind == "Anchor" || frame?.Kind == "Ratio") yield return frame.SourceEventId + "." + (frame.Anchor == "SimulationEnd" ? "End" : frame.Anchor);
                else if (frame?.Space == "Block" && _eventBlocks.TryGetValue(id, out string owner)) yield return owner + ".Start";
            }

            private void ValidateDependencyGraph(string key, HashSet<string> active, HashSet<string> finished)
            {
                if (finished.Contains(key)) return;
                if (!active.Add(key)) { Error("AnchorCycle", "$.events", "Cyclic endpoint: " + key); return; }
                int dot = key.LastIndexOf('.');
                string id = key.Substring(0, dot), side = key.Substring(dot + 1);
                foreach (string dependency in Dependencies(id, side)) ValidateDependencyGraph(dependency, active, finished);
                active.Remove(key); finished.Add(key);
            }

            private CharacterActionConfig EventAction(CharacterTestTacticalAuthoringEvent evt)
            {
                var slot = _source.Slots.FirstOrDefault(s => s.SlotId == evt.Operation.SlotId);
                if (slot == null) throw new InvalidOperationException("Missing action slot: " + evt.Operation.SlotId);
                var action = _inputs.ResolveSlot(_inputs.HasExplicitBindings && !slot.IsFunctional || slot.Resolver == "binding" ? slot.SlotId : slot.Resolver);
                if (slot.Resolver == "ordinaryGuard" && !CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(action, out string guardIssue))
                    throw new InvalidOperationException("ActionSlot: 普通防御需要具有真实 Blocking 窗口的 Guard 动作。" + guardIssue);
                return action;
            }

            private CharacterTestTacticalActionRange EventRange(string eventId)
            {
                if (!_events.TryGetValue(eventId ?? string.Empty, out var evt))
                    throw new InvalidOperationException("ActionSlot: 动作事件不存在 " + eventId);
                return _inputs.SlotRange(evt.Source.Operation.SlotId);
            }

            private int Position(string id, string side)
            {
                if (_timing != null) return _timing.Position(id, side);
                string key = id + "." + side;
                if (_positions.TryGetValue(key, out int value)) return value;
                if (!_visiting.Add(key)) throw new InvalidOperationException("AnchorCycle:" + key);
                try
                {
                    if (_blocks.TryGetValue(id, out var block))
                    {
                        if (side == "Start") value = ResolveFrame(block.Source.Start, null);
                        else
                        {
                            value = Position(id, "Start");
                            foreach (string member in block.Source.EventIds) value = Math.Max(value, Position(member, "End"));
                        }
                        if (value < 0) throw new ArgumentException("Negative block frame: " + key);
                        _positions.Add(key, value); return value;
                    }
                    var evt = _events[id].Source;
                    if (side == "ActionStartupEnd")
                    {
                        CharacterActionPhase? startup = null;
                        foreach (var phase in EventAction(evt).Phases) if (phase.Kind == CharacterActionPhaseKind.Startup) { startup = phase; break; }
                        if (!startup.HasValue) throw new InvalidOperationException("Action has no startup phase: " + id);
                        value = checked(Position(id, "Start") + startup.Value.EndFrame + 1);
                    }
                    else if (side == "ActionEnd" || side == "End" && evt.Operation.Kind == "CharacterAction")
                        value = checked(Position(id, "Start") + EventAction(evt).DurationFrames.Value);
                    else if (side == "End" && evt.TimingKind == "Instant") value = checked(Position(id, "Start") + 1);
                    else
                    {
                        var frame = side == "Start" ? evt.Start : evt.End;
                        value = ResolveFrame(frame, id);
                    }
                    if (value < 0) throw new ArgumentException("Negative resolved frame: " + key);
                    _positions.Add(key, value);
                    return value;
                }
                finally { _visiting.Remove(key); }
            }

            private bool TryPosition(IndexedEvent item, string side, out int position)
            {
                position = 0;
                try { position = Position(item.Source.EventId, side); return true; }
                catch (Exception error) when (error is ArgumentException || error is OverflowException || error is InvalidOperationException)
                {
                    string code = error.Message.StartsWith("ActionSlot:", StringComparison.Ordinal) ? "ActionSlotUnavailable"
                        : error is OverflowException ? "FrameOverflow"
                        : error is InvalidOperationException ? "AnchorCycle" : "NegativeResolvedFrame";
                    Error(code, item.Path + (side == "Start" ? ".start" : ".end"), error.Message);
                    return false;
                }
            }

            internal CharacterTestTacticalAuthoringCompilation Build()
            {
                string fingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(_source);
                var builder = new CharacterTestBehaviorExecutionBuilder(_references, _source.EntryTimelineId,
                    fingerprint + ":" + _inputs.DependencyFingerprint);
                builder.ActivationCondition = CompileCondition(builder, _source.ActivationConditionRoot);
                var resolved = new List<CharacterTestTacticalResolvedEvent>();
                var resolvedBlocks = new List<CharacterTestTacticalResolvedEvent>();
                var eventConditions = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var timeline in _source.Timelines)
                {
                    int duration = 0;
                    foreach (var evt in timeline.Events) duration = Math.Max(duration, Position(evt.EventId, "End"));
                    foreach (var block in timeline.EventBlocks)
                    {
                        int start = Position(block.BlockId, "Start"), end = Position(block.BlockId, "End");
                        duration = Math.Max(duration, end);
                        resolvedBlocks.Add(new CharacterTestTacticalResolvedEvent(block.BlockId, "eventBlocks." + block.BlockId, start, end));
                    }
                    builder.AddTimeline(timeline.TimelineId, duration, CompileCondition(builder, timeline.EntryConditionRoot), timeline.Order);
                    foreach (var evt in timeline.Events)
                    {
                        var item = _events[evt.EventId];
                        int start = Position(evt.EventId, "Start"), end = Position(evt.EventId, "End");
                        int condition = evt.Operation.Kind == "CharacterAction" ? -1 : CompileCondition(builder, evt.ConditionRoot);
                        eventConditions.Add(evt.EventId, condition);
                        resolved.Add(new CharacterTestTacticalResolvedEvent(evt.EventId, item.Path, start, end,
                            evt.Operation.Kind == "CharacterAction" ? EventAction(evt).ActionId.Value : 0,
                            _eventBlocks.TryGetValue(evt.EventId, out string blockId) ? blockId : string.Empty));
                        switch (evt.Operation.Kind)
                        {
                            case "EnterTimeline": builder.AddJump(evt.EventId, timeline.TimelineId, start, evt.Operation.TargetTimelineId, condition, evt.Priority, end - start); break;
                            case "CompleteBlock": builder.AddCompleteBlock(evt.EventId, timeline.TimelineId, start, end - start,
                                Position(_eventBlocks[evt.EventId], "End"), condition, evt.Priority); break;
                            case "LoopRegion": builder.AddLoop(evt.EventId, timeline.TimelineId, start, end - start, evt.Operation.MaxLoopRepeats, condition, evt.Priority); break;
                            case "CharacterAction": builder.AddAction(evt.EventId, timeline.TimelineId, start, end - start, EventAction(evt).ActionId.Value, condition, evt.Priority); break;
                            case "Movement":
                                builder.AddMovement(evt.EventId, timeline.TimelineId, start, end - start,
                                    evt.Operation.Movement.Compile(_source, _inputs, evt.Operation.Speed, evt.EventId), condition, evt.Priority); break;
                            case "ApproachOptimalRange":
                                var summary = EventRange(evt.Operation.SourceActionEventId);
                                builder.AddMovement(evt.EventId, timeline.TimelineId, start, end - start,
                                    CharacterTestBehaviorExecutionMovement.ApproachOptimalRange((float)summary.MinDistance.Value,
                                        (float)summary.MaxDistance.Value, (float)summary.OptimalDistance.Value, speed: evt.Operation.Speed,
                                        metric: CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance), condition, evt.Priority); break;
                            case "TurnToTarget":
                                builder.AddTurn(evt.EventId, timeline.TimelineId, start, end - start,
                                    new CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, speed: evt.Operation.Speed), condition, evt.Priority); break;
                            case "WaitForCondition": builder.AddGate(evt.EventId, timeline.TimelineId, start, condition, evt.Priority); break;
                            case "RetreatFromTarget":
                                builder.AddMovement(evt.EventId, timeline.TimelineId, start, end - start,
                                    new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard,
                                        CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance,
                                        0, 0f, 0f, 0f, 0f, evt.Operation.Speed, 0), condition, evt.Priority); break;
                            case "ReturnToPlanner": builder.AddReturn(evt.EventId, timeline.TimelineId, start, end - start, condition, evt.Priority); break;
                        }
                        builder.SetRepetition(evt.EventId, evt.MaxExecutionsPerPass, evt.CooldownFrames);
                        builder.SetTiming(evt.EventId, evt.TimingKind == "Instant" ? CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant : CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration);
                    }
                }
                foreach (var timeline in _source.Timelines)
                    foreach (var block in timeline.EventBlocks)
                    {
                        int start = Position(block.BlockId, "Start"), end = Position(block.BlockId, "End");
                        if (end <= start) continue;
                        bool implicitArrival = CharacterTestTacticalApproachActionSplit.IsImplicitArrival(_source, block);
                        builder.AddBlock(block.BlockId, timeline.TimelineId, start, end - start,
                            implicitArrival ? CharacterTestBehaviorBlockCompletion.Timed : Enum.Parse<CharacterTestBehaviorBlockCompletion>(block.Completion),
                            implicitArrival ? null : block.CompletionEventId,
                            implicitArrival || string.IsNullOrEmpty(block.SuccessConditionEventId) ? -1 : eventConditions[block.SuccessConditionEventId],
                            block.EventIds.Any(id => _events[id].Source.Operation.Kind == "CharacterAction"),
                            block.PreparationTimeoutFrames, block.CompletionTimeoutFrames, block.RequireLivingTarget, block.ContinueToActionOnPreparationTimeout, block.PassiveWait,
                            block.MaximumGuardFrames, block.MaximumGuardFrames > 0 ? EventAction(_events[block.CompletionEventId].Source).CombatTrack.Events
                                .Where(e => e.Kind == CharacterActionTrackEventKind.SetHitTargetState).Min(e => e.Frame) : 0,
                            block.MaximumGuardFrames > 0 ? EventAction(_events[block.CompletionEventId].Source).CombatTrack.Events
                                .Where(e => e.Kind == CharacterActionTrackEventKind.SetHitTargetState).Max(e => e.EndFrame) - 1 : -1);
                    }
                var program = builder.Build();
                var nativeIssues = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
                CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.ValidateTimeControls(CharacterTestNativeExecutionView.Get(program).Payload.Timelines, nativeIssues);
                if (nativeIssues.Count > 0) throw new ArgumentException(string.Join("; ", nativeIssues.Select(i => i.Message)));
                return new CharacterTestTacticalAuthoringCompilation(program, fingerprint, _inputs, resolved.ToArray(), resolvedBlocks.ToArray());
            }

            private int CompileCondition(CharacterTestBehaviorExecutionBuilder builder, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null) return -1;
                if (_bindings.TryGetValue(node.NodeId, out var binding))
                {
                    if (binding.Kind == "TurnAligned")
                    {
                        var parameters = _events[binding.SourceTurnEventId].Source.Operation.ResolveFacing(_inputs);
                        return builder.AddTargetFacingCondition(node.NodeId, parameters.Center, parameters.Tolerance);
                    }
                    if (binding.Kind == "MovementArrived")
                    {
                        var evt = _events[binding.SourceMovementEventId].Source;
                        return builder.AddMovementCondition(node.NodeId, evt.Operation.Movement.Compile(_source, _inputs, evt.Operation.Speed, evt.EventId), false);
                    }
                    if (binding.Kind == "IntentDistanceRange")
                    {
                        var distance = BoundIntentDistance(binding, null);
                        return distance.Metric == "HurtboxSurfaceClearance"
                            ? builder.AddHurtboxDistanceCondition(node.NodeId, distance.Minimum, distance.Maximum)
                            : builder.AddRootDistanceCondition(node.NodeId, distance.Minimum, distance.Maximum);
                    }
                    var summary = EventRange(binding.SourceActionEventId);
                    switch (binding.Kind)
                    {
                        // Historical binding identifier retained for authored-card compatibility; it reads the current action-distance contract.
                        case "RootDistanceRange": return builder.AddHurtboxDistanceCondition(node.NodeId, (float)summary.MinDistance.Value, (float)summary.MaxDistance.Value);
                        case "TargetFacing": return builder.AddTargetFacingCondition(node.NodeId, summary.Sector.CenterMilliDegrees / 1000f, summary.Sector.ToleranceMilliDegrees / 1000f);
                    }
                }
                switch (node.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate: return builder.AddFactCondition(node.NodeId, node.Predicate);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal: return builder.AddEventSignal(node.NodeId, node.SourceEventId, node.Signal);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope: return builder.AddSubjectScope(node.NodeId, node.Subject, CompileCondition(builder, node.Children[0]));
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not: return builder.AddNot(node.NodeId, CompileCondition(builder, node.Children[0]));
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All: return builder.AddAll(node.NodeId, node.Children.Select(child => CompileCondition(builder, child)).ToArray());
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any: return builder.AddAny(node.NodeId, node.Children.Select(child => CompileCondition(builder, child)).ToArray());
                    default: throw new ArgumentException("Unknown condition kind.");
                }
            }
        }
    }
}
