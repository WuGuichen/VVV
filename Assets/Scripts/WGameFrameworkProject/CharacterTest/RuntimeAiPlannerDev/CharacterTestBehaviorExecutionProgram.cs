using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestBehaviorExecutionOperations
    {
        // Explicit low-level builder operation; not an extension of the persisted v11 authoring format.
        public const CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind TurnToTarget = (CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind)9;
        public const CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind CompleteBlock = (CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind)10;
    }
    internal enum CharacterTestBehaviorExecutionPredicateKind { Fact = 0, BoundActionOpportunity = 1, TargetFacing = 2, RootDistanceRange = 3, MovementArrived = 4, MovementTargetMissing = 5 }
    internal enum CharacterTestBehaviorDistanceMetric
    {
        None = 0,
        RootPlanarDistance = 1,
        HurtboxSurfaceClearance = 2,
        RootToWorldPointPlanarDistance = 3
    }

    internal readonly struct CharacterTestBehaviorExecutionTimeline
    {
        public readonly int EventStart, EventCount, DurationFrames, EntryCondition;
        public CharacterTestBehaviorExecutionTimeline(int start, int count, int duration, int condition)
        { EventStart = start; EventCount = count; DurationFrames = duration; EntryCondition = condition; }
    }

    internal readonly struct CharacterTestBehaviorExecutionEvent
    {
        public readonly int TimelineIndex, StartFrame, DurationFrames, Condition, Parameter;
        public readonly int MaxExecutionsPerPass, CooldownFrames, Priority, Rank;
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind Kind;
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind Timing;
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin Clock;
        public readonly bool Shared;
        public int EndFrame => StartFrame + DurationFrames;
        public CharacterTestBehaviorExecutionEvent(int timeline, int start, int duration, int condition, int parameter,
            int executions, int cooldown, int priority, int rank,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind,
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timing,
            CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin clock, bool shared)
        {
            TimelineIndex = timeline; StartFrame = start; DurationFrames = duration; Condition = condition;
            Parameter = parameter; MaxExecutionsPerPass = executions; CooldownFrames = cooldown;
            Priority = priority; Rank = rank; Kind = kind; Timing = timing; Clock = clock; Shared = shared;
        }
    }

    internal readonly struct CharacterTestBehaviorExecutionAction
    {
        public readonly int ActionId;
        public CharacterTestBehaviorExecutionAction(int actionId) { ActionId = actionId; }
    }

    internal readonly struct CharacterTestBehaviorExecutionMovement
    {
        public readonly CharacterTestRuntimeAiLabMovementStrategy Strategy;
        public readonly CharacterTestRuntimeAiLabMovementTargetKind Target;
        public readonly CharacterTestBehaviorDistanceMetric Metric;
        public readonly int EntityId, SideParity;
        public readonly float X, Z, Minimum, Maximum, Speed;
        public readonly bool IsOptimalRange;
        public readonly float Optimal, StopTolerance;
        public readonly CharacterTestBehaviorBehindTarget BehindTarget;
        public readonly int ExplicitSide;
        public readonly float RadialWeight;
        public readonly float DistanceTolerance;
        public float EffectiveMinimum => DistanceTolerance > 0f ? Math.Max(0f, Minimum - DistanceTolerance) : Minimum;
        public float EffectiveMaximum => Maximum + DistanceTolerance;
        public readonly bool IndependentFacing, ExplicitDistance, RequireLivingTarget;
        public const float DefaultStopTolerance = 0.001f;
        public CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy strategy,
            CharacterTestRuntimeAiLabMovementTargetKind target, CharacterTestBehaviorDistanceMetric metric,
            int entityId, float x, float z, float minimum, float maximum, float speed, int sideParity,
            bool isOptimalRange = false, float optimal = 0f, float stopTolerance = DefaultStopTolerance,
            int explicitSide = 0, float radialWeight = 0.65f, bool independentFacing = false,
            bool explicitDistance = false, bool requireLivingTarget = false, float distanceTolerance = 0f, CharacterTestBehaviorBehindTarget behindTarget = default)
        {
            Strategy = strategy; Target = target; Metric = metric; EntityId = entityId; X = x; Z = z;
            Minimum = minimum; Maximum = maximum; Speed = speed; SideParity = sideParity;
            IsOptimalRange = isOptimalRange; Optimal = optimal; StopTolerance = stopTolerance;
            ExplicitSide = explicitSide; RadialWeight = radialWeight; IndependentFacing = independentFacing;
            ExplicitDistance = explicitDistance; RequireLivingTarget = requireLivingTarget;
            DistanceTolerance = distanceTolerance; BehindTarget = behindTarget;
        }
        public static CharacterTestBehaviorExecutionMovement ApproachOptimalRange(float minimum, float maximum, float optimal,
            float stopTolerance = DefaultStopTolerance, float speed = 1f,
            CharacterTestRuntimeAiLabMovementTargetKind target = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, int entityId = 0,
            CharacterTestBehaviorDistanceMetric metric = CharacterTestBehaviorDistanceMetric.RootPlanarDistance)
            => new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget, target,
                metric, entityId, 0f, 0f, minimum, maximum, speed, 0, true, optimal, stopTolerance);
        // The old recurrence multiplies by odd constants: only source parity is needed at Runtime.
        public int ResolveSide(int entityId) => ExplicitSide != 0 ? ExplicitSide : ((entityId ^ SideParity) & 1) == 0 ? 1 : -1;
    }

    internal readonly struct CharacterTestBehaviorBehindTarget
    {
        public readonly bool Enabled, Maintain;
        public readonly float HalfAngle, ResumeHalfAngle, DistanceTolerance, ResumeDistanceTolerance, DirectionSwitchHysteresis;
        public CharacterTestBehaviorBehindTarget(CharacterTestTacticalBehindTarget source)
        {
            Enabled = true; Maintain = source.Maintain;
            HalfAngle = source.HalfAngleDegrees; ResumeHalfAngle = source.ResumeHalfAngleDegrees;
            DistanceTolerance = source.DistanceTolerance; ResumeDistanceTolerance = source.ResumeDistanceTolerance;
            DirectionSwitchHysteresis = source.DirectionSwitchHysteresisDegrees;
        }
    }

    internal readonly struct CharacterTestBehaviorExecutionTurn
    {
        public readonly CharacterTestRuntimeAiLabMovementTargetKind Target;
        public readonly int EntityId;
        public readonly float X, Z, Speed;
        public CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind target = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
            int entityId = 0, float x = 0f, float z = 0f, float speed = 1f)
        { Target = target; EntityId = entityId; X = x; Z = z; Speed = speed; }
    }

    internal readonly struct CharacterTestBehaviorExecutionFlow
    {
        public readonly int TargetTimeline, MaxLoopRepeats;
        public CharacterTestBehaviorExecutionFlow(int timeline, int repeats)
        { TargetTimeline = timeline; MaxLoopRepeats = repeats; }
    }

    internal readonly struct CharacterTestBehaviorExecutionCondition
    {
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind Kind;
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Subject;
        public readonly int Predicate, ChildStart, ChildCount, SourceEvent;
        public readonly CharacterTestRuntimeAiLabBehaviorRecipeEventSignal Signal;
        public CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind kind,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject, int predicate,
            int childStart, int childCount, int sourceEvent, CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal)
        { Kind = kind; Subject = subject; Predicate = predicate; ChildStart = childStart; ChildCount = childCount; SourceEvent = sourceEvent; Signal = signal; }
    }

    internal readonly struct CharacterTestBehaviorExecutionPredicate
    {
        public readonly CharacterTestBehaviorExecutionPredicateKind Kind;
        public readonly CharacterTestBehaviorExecutionMovement Movement;
        public readonly CharacterTestRuntimeAiLabCardConditionFact Fact;
        public readonly CharacterTestRuntimeAiLabCardConditionOperator Operator;
        public readonly bool BoolValue;
        public readonly float Value, Maximum;
        public readonly int AbilityId, ActionId;
        public readonly uint Phases, RequiredFlags, ForbiddenFlags;
        public CharacterTestBehaviorExecutionPredicate(CharacterTestRuntimeAiLabCardCondition source)
        {
            Movement = default;
            Kind = CharacterTestBehaviorExecutionPredicateKind.Fact;
            Fact = source.Fact; Operator = source.Operator; BoolValue = source.BoolValue;
            Value = source.Value; Maximum = source.MaxValue; AbilityId = source.AbilityId;
            ActionId = source.CharacterActionId.Value; Phases = source.AllowedTargetPhases;
            RequiredFlags = source.RequiredTargetFlags; ForbiddenFlags = source.ForbiddenTargetFlags;
        }
        public CharacterTestBehaviorExecutionPredicate(CharacterTestBehaviorExecutionPredicateKind kind, int actionId, float value, float maximum)
        {
            Movement = default;
            Kind = kind; Fact = default; Operator = default; BoolValue = false;
            Value = value; Maximum = maximum; AbilityId = 0; ActionId = actionId;
            Phases = 0; RequiredFlags = 0; ForbiddenFlags = 0;
        }
        public CharacterTestBehaviorExecutionPredicate(CharacterTestBehaviorExecutionMovement movement, bool missing)
            : this(missing ? CharacterTestBehaviorExecutionPredicateKind.MovementTargetMissing : CharacterTestBehaviorExecutionPredicateKind.MovementArrived, 0, 0, 0)
        { Movement = movement; }
        public CharacterTestRuntimeAiLabCompiledCondition AsFactRequest() => new CharacterTestRuntimeAiLabCompiledCondition(
            string.Empty, Fact, Operator, BoolValue, Value, Maximum, AbilityId,
            new MxFramework.CharacterAction.CharacterActionId(ActionId), Phases, RequiredFlags, ForbiddenFlags);
    }

    /// <summary>Immutable, project-owned execution definition. Contains neither Stage nor service references.</summary>
    /// <remarks>Construction is LowFreqAlloc; table reads are NoAlloc. Source identities belong to a separate debug map.</remarks>
    internal sealed class CharacterTestBehaviorExecutionProgram
    {
        public const int CurrentFormatVersion = 1;
        private readonly CharacterTestBehaviorExecutionTimeline[] _timelines;
        private readonly CharacterTestBehaviorExecutionEvent[] _events;
        private readonly int[] _timelineEvents, _children;
        private readonly CharacterTestBehaviorExecutionAction[] _actions;
        private readonly CharacterTestBehaviorExecutionMovement[] _movements;
        private readonly CharacterTestBehaviorExecutionFlow[] _flows;
        private readonly CharacterTestBehaviorExecutionTurn[] _turns;
        private readonly CharacterTestBehaviorExecutionCondition[] _conditions;
        private readonly CharacterTestBehaviorExecutionPredicate[] _predicates;
        private readonly CharacterTestBehaviorExecutionBlock[] _blocks;
        internal ReadOnlySpan<CharacterTestBehaviorExecutionBlock> Blocks => _blocks;
        private readonly bool[] _attackFactConditions;
        internal bool NeedsPrimaryAttackFact(int condition) => condition >= 0 && _attackFactConditions[condition];
        public int EntryTimeline { get; }
        public int ActivationCondition { get; }
        public int MaximumConditionDepth { get; }
        public string DependencyFingerprint { get; }
        public string ExternalDependencyIdentity { get; }
        public long Identity { get; }
        public ReadOnlySpan<CharacterTestBehaviorExecutionTimeline> Timelines => _timelines;
        public ReadOnlySpan<CharacterTestBehaviorExecutionEvent> Events => _events;
        public ReadOnlySpan<int> TimelineEvents => _timelineEvents;
        public ReadOnlySpan<int> Children => _children;
        public ReadOnlySpan<CharacterTestBehaviorExecutionAction> Actions => _actions;
        public ReadOnlySpan<CharacterTestBehaviorExecutionMovement> Movements => _movements;
        public ReadOnlySpan<CharacterTestBehaviorExecutionFlow> Flows => _flows;
        public ReadOnlySpan<CharacterTestBehaviorExecutionTurn> Turns => _turns;
        public ReadOnlySpan<CharacterTestBehaviorExecutionCondition> Conditions => _conditions;
        public ReadOnlySpan<CharacterTestBehaviorExecutionPredicate> Predicates => _predicates;

        public CharacterTestBehaviorExecutionProgram(int entryTimeline, int activationCondition, string dependencyFingerprint,
            CharacterTestBehaviorExecutionTimeline[] timelines, CharacterTestBehaviorExecutionEvent[] events, int[] timelineEvents,
            CharacterTestBehaviorExecutionAction[] actions, CharacterTestBehaviorExecutionMovement[] movements,
            CharacterTestBehaviorExecutionFlow[] flows, CharacterTestBehaviorExecutionCondition[] conditions,
            CharacterTestBehaviorExecutionPredicate[] predicates, int[] children, CharacterTestBehaviorExecutionTurn[] turns = null,
            string externalDependencyIdentity = "", CharacterTestBehaviorExecutionBlock[] blocks = null)
        {
            EntryTimeline = entryTimeline; ActivationCondition = activationCondition;
            DependencyFingerprint = dependencyFingerprint ?? string.Empty;
            ExternalDependencyIdentity = externalDependencyIdentity ?? string.Empty;
            _timelines = Copy(timelines); _events = Copy(events); _timelineEvents = Copy(timelineEvents);
            _actions = Copy(actions); _movements = Copy(movements); _flows = Copy(flows);
            _conditions = Copy(conditions); _predicates = Copy(predicates); _children = Copy(children);
            _turns = Copy(turns);
            _blocks = Copy(blocks);
            MaximumConditionDepth = Validate();
            _attackFactConditions = new bool[_conditions.Length];
            var visited = new bool[_conditions.Length];
            for (int i = 0; i < _conditions.Length; i++) CompileAttackFactRequirement(i, visited);
            Identity = ComputeIdentity();
        }

        private bool CompileAttackFactRequirement(int index, bool[] visited)
        {
            if (index < 0) return false;
            if (visited[index]) return _attackFactConditions[index];
            visited[index] = true;
            var condition = _conditions[index];
            bool needed = condition.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate && condition.Predicate >= 0
                && _predicates[condition.Predicate].Kind == CharacterTestBehaviorExecutionPredicateKind.Fact
                && _predicates[condition.Predicate].Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange;
            for (int i = 0; i < condition.ChildCount; i++)
                needed |= CompileAttackFactRequirement(_children[condition.ChildStart + i], visited);
            return _attackFactConditions[index] = needed;
        }

        private static T[] Copy<T>(T[] source) => source == null || source.Length == 0 ? Array.Empty<T>() : (T[])source.Clone();
        private static void Require(bool condition, string message)
        { if (!condition) throw new ArgumentException(message); }
        private static bool Index(int index, int count) => index >= 0 && index < count;
        private static bool Optional(int index, int count) => index == -1 || Index(index, count);
        private static bool Range(int start, int count, int length) => start >= 0 && count >= 0 && start <= length - count;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private int Validate()
        {
            Require(Index(EntryTimeline, _timelines.Length), "Entry timeline index is invalid.");
            Require(Optional(ActivationCondition, _conditions.Length), "Activation condition index is invalid.");
            for (int i = 0; i < _blocks.Length; i++)
            {
                var block = _blocks[i];
                Require(block.MaximumGuardFrames >= 0 && block.GuardActiveStartFrame >= 0
                    && (block.MaximumGuardFrames == 0 || block.GuardActiveEndFrame >= block.GuardActiveStartFrame)
                    && (block.MaximumGuardFrames == 0 || block.Completion == CharacterTestBehaviorBlockCompletion.Action && block.HasAction),
                    "Invalid ordinary guard limit.");
                Require(block.PreparationTimeoutFrames >= 0 && block.CompletionTimeoutFrames >= 0
                    && (block.PreparationTimeoutFrames > 0) == (block.CompletionTimeoutFrames > 0)
                    && (block.Completion == CharacterTestBehaviorBlockCompletion.Action
                        || block.PreparationTimeoutFrames == 0 && (!block.RequireLivingTarget || (block.Completion == CharacterTestBehaviorBlockCompletion.Condition || block.Completion == CharacterTestBehaviorBlockCompletion.Facing || block.Completion == CharacterTestBehaviorBlockCompletion.Distance)))
                    && (!block.PassiveWait || block.Completion == CharacterTestBehaviorBlockCompletion.Condition || block.Completion == CharacterTestBehaviorBlockCompletion.Delay || block.Completion == CharacterTestBehaviorBlockCompletion.Distance)
                    && (!block.ContinueToActionOnPreparationTimeout || block.Completion == CharacterTestBehaviorBlockCompletion.Action && block.PreparationTimeoutFrames > 0), "Invalid action block budgets.");
                Require(Index(block.Timeline, _timelines.Length) && block.Start >= 0 && block.Duration > 0
                    && block.Duration <= int.MaxValue - block.Start - CharacterTestBehaviorExecutionBlock.ActionBoundaryFrames
                    && block.End <= _timelines[block.Timeline].DurationFrames
                    && Enum.IsDefined(typeof(CharacterTestBehaviorBlockCompletion), block.Completion)
                    && Optional(block.SuccessCondition, _conditions.Length), "Execution block bounds or completion rule are invalid.");
                if (block.Completion != CharacterTestBehaviorBlockCompletion.Timed)
                {
                    Require(Index(block.CompletionEvent, _events.Length), "Execution block completion event is missing.");
                    var evt = _events[block.CompletionEvent];
                    Require(evt.TimelineIndex == block.Timeline && evt.StartFrame >= block.Start && evt.EndFrame <= block.End
                        && evt.Kind == (block.Completion == CharacterTestBehaviorBlockCompletion.Arrived
                            ? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                            : block.Completion == CharacterTestBehaviorBlockCompletion.Condition || block.Completion == CharacterTestBehaviorBlockCompletion.Facing
                                || block.Completion == CharacterTestBehaviorBlockCompletion.Delay || block.Completion == CharacterTestBehaviorBlockCompletion.Distance
                                ? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition : CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction),
                        "Execution block completion event is incompatible.");
                    if (block.Completion == CharacterTestBehaviorBlockCompletion.Condition || block.Completion == CharacterTestBehaviorBlockCompletion.Facing)
                        Require(!block.HasAction && block.RequireLivingTarget && evt.StartFrame == block.Start
                            && evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline
                            && evt.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                            "Window wait requires a target and an instant gate at its block start.");
                    if (block.Completion == CharacterTestBehaviorBlockCompletion.Delay || block.Completion == CharacterTestBehaviorBlockCompletion.Distance)
                        Require(block.PassiveWait && !block.HasAction && evt.StartFrame == block.Start
                            && evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline
                            && evt.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                            && (block.Completion == CharacterTestBehaviorBlockCompletion.Delay
                                ? block.Duration > 0 && (block.Duration & 1) == 0 && !block.RequireLivingTarget && evt.Condition < 0
                                : block.RequireLivingTarget && evt.Condition >= 0), "Invalid purpose wait block.");
                    if (block.Completion == CharacterTestBehaviorBlockCompletion.Delay || block.Completion == CharacterTestBehaviorBlockCompletion.Distance)
                        foreach (var concurrent in _events)
                            Require(concurrent.TimelineIndex != block.Timeline || concurrent.EndFrame <= block.Start || concurrent.StartFrame >= block.End
                                || concurrent.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                                    && concurrent.Kind != CharacterTestBehaviorExecutionOperations.TurnToTarget
                                    && concurrent.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                                "Passive purpose wait cannot actively move, turn or start an action.");
                    for (int j = 0; j < _blocks.Length; j++)
                        Require(i == j || _blocks[j].Timeline != block.Timeline || _blocks[j].End <= block.Start || _blocks[j].Start >= block.End,
                            "Early-completion blocks must not overlap other blocks.");
                }
            }
            for (int i = 0; i < _timelines.Length; i++)
            {
                var timeline = _timelines[i];
                Require(Range(timeline.EventStart, timeline.EventCount, _timelineEvents.Length), "Timeline event range is invalid.");
                Require(timeline.DurationFrames >= 0 && Optional(timeline.EntryCondition, _conditions.Length), "Timeline duration or condition is invalid.");
                int previousRank = -1;
                for (int j = 0; j < timeline.EventCount; j++)
                {
                    int index = _timelineEvents[timeline.EventStart + j];
                    Require(Index(index, _events.Length), "Timeline event index is invalid.");
                    var evt = _events[index];
                    Require(evt.Shared || evt.TimelineIndex == i, "Timeline includes an event owned by another timeline.");
                    Require(evt.Rank > previousRank, "Timeline sampling order is invalid or duplicated.");
                    previousRank = evt.Rank;
                }
            }
            for (int i = 0; i < _events.Length; i++)
            {
                var evt = _events[i];
                Require(Index(evt.TimelineIndex, _timelines.Length) && Optional(evt.Condition, _conditions.Length), "Event reference is invalid.");
                Require(evt.StartFrame >= 0 && evt.DurationFrames > 0 && evt.StartFrame <= int.MaxValue - evt.DurationFrames,
                    "Event time range is invalid.");
                Require(evt.MaxExecutionsPerPass >= 0 && evt.CooldownFrames >= 0, "Event repetition policy is invalid.");
                Require(Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind), evt.Timing)
                    && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin), evt.Clock), "Event timing kind or clock is invalid.");
                switch (evt.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                        Require(Index(evt.Parameter, _actions.Length) && _actions[evt.Parameter].ActionId > 0, "Action parameter is invalid."); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                        Require(Index(evt.Parameter, _movements.Length), "Movement parameter is invalid."); break;
                    case CharacterTestBehaviorExecutionOperations.TurnToTarget:
                        Require(Index(evt.Parameter, _turns.Length), "Turn parameter is invalid."); break;
                    case CharacterTestBehaviorExecutionOperations.CompleteBlock:
                        Require(Array.Exists(_blocks, block => block.Timeline == evt.TimelineIndex && !block.HasAction
                            && block.Completion == CharacterTestBehaviorBlockCompletion.Timed
                            && block.Start <= evt.StartFrame && block.End == evt.Parameter && evt.EndFrame <= block.End),
                            "Intent completion must reference its containing timed block without an action."); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                        Require(Index(evt.Parameter, _flows.Length), "Flow parameter is invalid.");
                        Require(evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                            || Index(_flows[evt.Parameter].TargetTimeline, _timelines.Length), "Jump target is invalid.");
                        Require(_flows[evt.Parameter].MaxLoopRepeats >= 0, "Loop repetition policy is invalid."); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner:
                        Require(evt.Parameter == -1, "Parameterless operation contains an unexpected parameter."); break;
                    default: throw new ArgumentException("Unsupported execution operation.");
                }
            }
            foreach (var movement in _movements)
            {
                Require(Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), movement.Strategy)
                    && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), movement.Target), "Movement strategy or target is invalid.");
                Require(Finite(movement.X) && Finite(movement.Z) && Finite(movement.Minimum) && Finite(movement.Maximum)
                    && Finite(movement.Speed) && movement.Speed > 0f, "Movement parameters must be finite and speed must be positive.");
                Require(movement.ExplicitSide >= -1 && movement.ExplicitSide <= 1 && Finite(movement.RadialWeight)
                    && movement.RadialWeight >= 0 && movement.RadialWeight <= 4, "Movement side or radial weight is invalid.");
                Require(Finite(movement.DistanceTolerance) && movement.DistanceTolerance >= 0f
                    && Finite(movement.EffectiveMinimum) && Finite(movement.EffectiveMaximum)
                    && (movement.DistanceTolerance == 0f || movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                        && !movement.IsOptimalRange && movement.Minimum == movement.Maximum), "Distance tolerance requires a single maintain-distance target.");
                if (movement.BehindTarget.Enabled)
                {
                    var rear = movement.BehindTarget;
                    new CharacterTestTacticalBehindTarget { Maintain = rear.Maintain,
                        HalfAngleDegrees = rear.HalfAngle, ResumeHalfAngleDegrees = rear.ResumeHalfAngle,
                        DistanceTolerance = rear.DistanceTolerance, ResumeDistanceTolerance = rear.ResumeDistanceTolerance,
                        DirectionSwitchHysteresisDegrees = rear.DirectionSwitchHysteresis }.Validate(movement.Strategy, movement.Target, movement.ExplicitSide);
                    Require(movement.IndependentFacing && movement.ExplicitDistance && movement.RequireLivingTarget
                        && !movement.IsOptimalRange, "Behind-target orbit requires independent facing and a living target.");
                }
                var expectedMetric = movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                    ? CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance
                    : movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                        ? CharacterTestBehaviorDistanceMetric.RootPlanarDistance : CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance;
                if (movement.IsOptimalRange)
                {
                    Require(movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        && (movement.Metric == CharacterTestBehaviorDistanceMetric.RootPlanarDistance
                            || movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                            && movement.Metric == CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance), "Optimal approach requires an entity target and a supported distance metric.");
                    Require(Finite(movement.Optimal) && Finite(movement.StopTolerance) && movement.Minimum >= 0f
                        && movement.Minimum <= movement.Optimal && movement.Optimal <= movement.Maximum
                        && movement.StopTolerance >= CharacterTestBehaviorExecutionMovement.DefaultStopTolerance, "Optimal approach interval, point or tolerance is invalid.");
                }
                else Require(movement.Metric == expectedMetric, "Legacy movement distance metric does not match its target.");
            }
            foreach (var turn in _turns)
                Require(Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), turn.Target)
                    && Finite(turn.X) && Finite(turn.Z) && Finite(turn.Speed) && turn.Speed > 0f && turn.Speed <= 10f, "Turn target or speed is invalid.");
            foreach (var predicate in _predicates)
            {
                Require(Enum.IsDefined(typeof(CharacterTestBehaviorExecutionPredicateKind), predicate.Kind), "Predicate kind is invalid.");
                Require(Finite(predicate.Value) && Finite(predicate.Maximum), "Predicate parameters must be finite.");
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity)
                    Require(predicate.ActionId > 0, "Bound opportunity requires an action.");
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.TargetFacing)
                    Require(predicate.Maximum >= 0f && predicate.Maximum <= 180f, "Facing tolerance is invalid.");
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange)
                    Require(predicate.Value >= 0f && predicate.Maximum >= predicate.Value, "Root distance interval is invalid.");
            }
            var colors = new byte[_conditions.Length];
            var depths = new int[_conditions.Length];
            int maximum = 0;
            for (int i = 0; i < _conditions.Length; i++) maximum = Math.Max(maximum, ValidateCondition(i, colors, depths, 0));
            Require(!ContainsAttackWaitFact(ActivationCondition), "Window wait facts cannot be used for card activation.");
            foreach (var timeline in _timelines)
                Require(!ContainsAttackWaitFact(timeline.EntryCondition), "Window wait facts cannot be used for timeline eligibility.");
            for (int i = 0; i < _events.Length; i++)
            {
                if (!ContainsAttackWaitFact(_events[i].Condition)) continue;
                bool owned = false;
                foreach (var block in _blocks)
                    if (block.Completion == CharacterTestBehaviorBlockCompletion.Condition && block.CompletionEvent == i) { owned = true; break; }
                Require(owned, "Window wait facts require their own completion gate.");
            }
            return maximum;
        }
        private bool ContainsAttackWaitFact(int index)
        {
            if (index < 0) return false;
            var node = _conditions[index];
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
                return _predicates[node.Predicate].Kind == CharacterTestBehaviorExecutionPredicateKind.Fact
                    && _predicates[node.Predicate].Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded;
            for (int i = 0; i < node.ChildCount; i++)
                if (ContainsAttackWaitFact(_children[node.ChildStart + i])) return true;
            return false;
        }
        private int ValidateCondition(int index, byte[] colors, int[] depths, int visitingDepth)
        {
            Require(visitingDepth <= 16, "Condition stack depth exceeds the execution limit.");
            Require(colors[index] != 1, "Condition table contains a cycle.");
            if (colors[index] == 2) return depths[index];
            colors[index] = 1;
            var node = _conditions[index];
            Require(Range(node.ChildStart, node.ChildCount, _children.Length), "Condition child range is invalid.");
            switch (node.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                    Require(Index(node.Predicate, _predicates.Length) && node.ChildCount == 0, "Predicate reference or arity is invalid."); break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal:
                    Require(Index(node.SourceEvent, _events.Length) && node.ChildCount == 0
                        && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal), node.Signal), "Event signal reference, signal or arity is invalid."); break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                    Require(node.ChildCount == 1, "Unary condition arity is invalid.");
                    Require(node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                        || (node.Subject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown
                            && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind), node.Subject)), "Condition subject is invalid."); break;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                    Require(node.ChildCount > 0, "Composite condition cannot be empty."); break;
                default: throw new ArgumentException("Unknown condition kind.");
            }
            int depth = 1;
            for (int i = 0; i < node.ChildCount; i++)
            {
                int child = _children[node.ChildStart + i];
                Require(Index(child, _conditions.Length), "Condition child index is invalid.");
                depth = Math.Max(depth, 1 + ValidateCondition(child, colors, depths, visitingDepth + 1));
            }
            Require(depth <= 16, "Condition tree exceeds the execution depth limit.");
            colors[index] = 2; depths[index] = depth;
            return depth;
        }
        private long ComputeIdentity()
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddInt("execution.format", CurrentFormatVersion);
            hash.AddInt("entry", EntryTimeline); hash.AddInt("activation", ActivationCondition);
            if (_blocks.Length > 0)
            {
                hash.AddInt("blocks", _blocks.Length);
                foreach (var block in _blocks)
                {
                    hash.AddInt("blockTimeline", block.Timeline); hash.AddInt("blockStart", block.Start); hash.AddInt("blockDuration", block.Duration);
                    hash.AddInt("blockCompletion", (int)block.Completion); hash.AddInt("blockEvent", block.CompletionEvent);
                    hash.AddInt("blockCondition", block.SuccessCondition); hash.AddInt("blockAction", block.HasAction ? 1 : 0);
                    if (block.ContinueToActionOnPreparationTimeout) hash.AddInt("blockContinueToActionOnPreparationTimeout", 1);
                    if (block.PassiveWait) hash.AddInt("blockPassiveWait", 1);
                    if (block.MaximumGuardFrames > 0)
                    { hash.AddInt("blockMaximumGuardFrames", block.MaximumGuardFrames); hash.AddInt("blockGuardActiveStart", block.GuardActiveStartFrame); hash.AddInt("blockGuardActiveEnd", block.GuardActiveEndFrame); }
                    if (block.PreparationTimeoutFrames > 0 || block.CompletionTimeoutFrames > 0 || block.RequireLivingTarget)
                    {
                        hash.AddInt("blockPreparationLimit", block.PreparationTimeoutFrames);
                        hash.AddInt("blockCompletionLimit", block.CompletionTimeoutFrames);
                        hash.AddInt("blockLivingTarget", block.RequireLivingTarget ? 1 : 0);
                    }
                }
            }
            hash.AddStringStable("dependencies", DependencyFingerprint);
            hash.AddInt("timelines", _timelines.Length);
            foreach (var t in _timelines) { hash.AddInt("start", t.EventStart); hash.AddInt("count", t.EventCount); hash.AddInt("duration", t.DurationFrames); hash.AddInt("condition", t.EntryCondition); }
            hash.AddInt("events", _events.Length);
            foreach (var e in _events)
            {
                hash.AddInt("timeline", e.TimelineIndex); hash.AddInt("start", e.StartFrame); hash.AddInt("duration", e.DurationFrames);
                hash.AddInt("condition", e.Condition); hash.AddInt("parameter", e.Parameter); hash.AddInt("count", e.MaxExecutionsPerPass);
                hash.AddInt("cooldown", e.CooldownFrames); hash.AddInt("priority", e.Priority); hash.AddInt("rank", e.Rank);
                hash.AddInt("kind", (int)e.Kind); hash.AddInt("timing", (int)e.Timing); hash.AddInt("clock", (int)e.Clock); hash.AddInt("shared", e.Shared ? 1 : 0);
            }
            foreach (int index in _timelineEvents) hash.AddInt("eventOrder", index);
            foreach (var action in _actions) hash.AddInt("action", action.ActionId);
            foreach (var move in _movements)
            {
                hash.AddInt("strategy", (int)move.Strategy); hash.AddInt("target", (int)move.Target); hash.AddInt("metric", (int)move.Metric);
                hash.AddInt("entity", move.EntityId); hash.AddInt("side", move.SideParity);
                if (move.ExplicitSide != 0 || move.RadialWeight != 0.65f || move.IndependentFacing || move.ExplicitDistance || move.RequireLivingTarget)
                { hash.AddInt("explicitSide", move.ExplicitSide); AddFloat(ref hash, move.RadialWeight); hash.AddInt("independentFacing", move.IndependentFacing ? 1 : 0); hash.AddInt("explicitDistance", move.ExplicitDistance ? 1 : 0); hash.AddInt("livingTarget", move.RequireLivingTarget ? 1 : 0); }
                AddFloat(ref hash, move.X); AddFloat(ref hash, move.Z); AddFloat(ref hash, move.Minimum); AddFloat(ref hash, move.Maximum); AddFloat(ref hash, move.Speed);
                if (move.DistanceTolerance > 0f) { hash.AddInt("distanceTolerance", 1); AddFloat(ref hash, move.DistanceTolerance); }
                hash.AddInt("optimalMode", move.IsOptimalRange ? 1 : 0); AddFloat(ref hash, move.Optimal); AddFloat(ref hash, move.StopTolerance);
                if (move.BehindTarget.Enabled)
                {
                    hash.AddInt("behindTarget", 1); hash.AddInt("behindMaintain", move.BehindTarget.Maintain ? 1 : 0);
                    AddFloat(ref hash, move.BehindTarget.HalfAngle); AddFloat(ref hash, move.BehindTarget.ResumeHalfAngle);
                    AddFloat(ref hash, move.BehindTarget.DistanceTolerance); AddFloat(ref hash, move.BehindTarget.ResumeDistanceTolerance);
                    AddFloat(ref hash, move.BehindTarget.DirectionSwitchHysteresis);
                }
            }
            foreach (var turn in _turns)
            { hash.AddInt("turnTarget", (int)turn.Target); hash.AddInt("turnEntity", turn.EntityId); AddFloat(ref hash, turn.X); AddFloat(ref hash, turn.Z); AddFloat(ref hash, turn.Speed); }
            foreach (var flow in _flows) { hash.AddInt("targetTimeline", flow.TargetTimeline); hash.AddInt("repeats", flow.MaxLoopRepeats); }
            foreach (var condition in _conditions)
            {
                hash.AddInt("kind", (int)condition.Kind); hash.AddInt("subject", (int)condition.Subject); hash.AddInt("predicate", condition.Predicate);
                hash.AddInt("start", condition.ChildStart); hash.AddInt("count", condition.ChildCount); hash.AddInt("source", condition.SourceEvent); hash.AddInt("signal", (int)condition.Signal);
            }
            foreach (int child in _children) hash.AddInt("child", child);
            foreach (var predicate in _predicates)
            {
                hash.AddInt("predicateKind", (int)predicate.Kind);
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.MovementArrived || predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.MovementTargetMissing)
                {
                    var m = predicate.Movement;
                    hash.AddInt("queryStrategy", (int)m.Strategy); hash.AddInt("queryTarget", (int)m.Target); hash.AddInt("queryMetric", (int)m.Metric); hash.AddInt("queryEntity", m.EntityId);
                    AddFloat(ref hash, m.X); AddFloat(ref hash, m.Z); AddFloat(ref hash, m.Minimum); AddFloat(ref hash, m.Maximum);
                    hash.AddInt("queryExplicitDistance", m.ExplicitDistance ? 1 : 0); hash.AddInt("queryLivingTarget", m.RequireLivingTarget ? 1 : 0);
                    if (m.DistanceTolerance > 0f) { hash.AddInt("queryDistanceTolerance", 1); AddFloat(ref hash, m.DistanceTolerance); }
                    hash.AddInt("queryOptimal", m.IsOptimalRange ? 1 : 0); AddFloat(ref hash, m.Optimal); AddFloat(ref hash, m.StopTolerance);
                }
                hash.AddInt("fact", (int)predicate.Fact); hash.AddInt("operator", (int)predicate.Operator); hash.AddInt("bool", predicate.BoolValue ? 1 : 0);
                AddFloat(ref hash, predicate.Value); AddFloat(ref hash, predicate.Maximum); hash.AddInt("ability", predicate.AbilityId); hash.AddInt("action", predicate.ActionId);
                hash.AddLong("phases", predicate.Phases); hash.AddLong("required", predicate.RequiredFlags); hash.AddLong("forbidden", predicate.ForbiddenFlags);
            }
            return hash.ToHash();
        }
        private static void AddFloat(ref RuntimeContentHashAccumulator hash, float value) => hash.AddInt("number", BitConverter.SingleToInt32Bits(value));
    }

    internal sealed class CharacterTestBehaviorExecutionDebugMap
    {
        private readonly string[] _timelines, _events, _conditions;
        public CharacterTestBehaviorExecutionDebugMap(string[] timelines, string[] events, string[] conditions)
        { _timelines = (string[])timelines.Clone(); _events = (string[])events.Clone(); _conditions = (string[])conditions.Clone(); }
        public string Timeline(int index) => index >= 0 && index < _timelines.Length ? _timelines[index] : string.Empty;
        public string Event(int index) => index >= 0 && index < _events.Length ? _events[index] : string.Empty;
        public string Condition(int index) => index >= 0 && index < _conditions.Length ? _conditions[index] : string.Empty;
    }
}
