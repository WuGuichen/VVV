using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind
    {
        All = 0,
        Any = 1,
        Not = 2,
        FactPredicate = 3,
        EventSignal = 4,
        SubjectScope = 5
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind
    {
        Unknown = 0,
        CurrentCombatTarget = 1,
        Self = 2,
        Environment = 3
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeEventSignal
    {
        Completed = 0,
        Failed = 1,
        Rejected = 2,
        Cancelled = 3,
        CommitFailed = 4
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode
    {
        Fixed = 0,
        UntilTimelineExit = 1
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind
    {
        Instant = 0,
        Duration = 1
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind
    {
        Unconfigured = 0,
        MoveToTarget = 1,
        CharacterAction = 2,
        // Reserved for legacy behavior-recipe JSON. Native authoring and compilation reject it;
        // behavior recipes reference the bound CharacterAction instead.
        Ability = 3,
        // Reserved for legacy behavior-recipe JSON. Native authoring and compilation reject it;
        // fixed no-op spans are not part of the current behavior recipe.
        Hold = 4,
        EnterTimeline = 5,
        ReturnToPlanner = 6,
        WaitForCondition = 7,
        LoopRegion = 8
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult
    {
        None = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Rejected = 4,
        Cancelled = 5,
        CommitFailed = 6
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
    {
        public string NodeId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind Kind { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Subject { get; set; }
        public CharacterTestRuntimeAiLabCardCondition Predicate { get; set; }
        public string SourceEventId { get; set; } = string.Empty;
        public string MovementQuery { get; set; }
        public bool ShouldSerializeMovementQuery() => MovementQuery != null;
        public CharacterTestRuntimeAiLabBehaviorRecipeEventSignal Signal { get; set; }
        public List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode> Children { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>();

        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = NodeId,
                Kind = Kind,
                Subject = Subject,
                Predicate = Predicate?.Clone(),
                SourceEventId = SourceEventId, MovementQuery = MovementQuery,
                Signal = Signal
            };
            for (int i = 0; i < Children.Count; i++)
                copy.Children.Add(Children[i]?.Clone());
            return copy;
        }

        internal static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode FromAll(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string nodeId)
        {
            if (conditions == null || conditions.Count == 0)
                return null;
            var root = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = nodeId ?? string.Empty,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition predicate = conditions[i]?.Clone();
                var factNode = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = !string.IsNullOrWhiteSpace(predicate?.ConditionId)
                        ? predicate.ConditionId
                        : (nodeId ?? "condition") + "." + i,
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = predicate
                };
                if (predicate != null
                    && CharacterTestRuntimeAiLabBehaviorConditionCatalog.RequiresSubjectScope(predicate.Fact)
                    && CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                        predicate.Fact,
                        out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor))
                {
                    var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                    {
                        NodeId = "condition.scope."
                            + ((int)descriptor.Subject).ToString(CultureInfo.InvariantCulture)
                            + "." + factNode.NodeId,
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                        Subject = descriptor.Subject
                    };
                    scope.Children.Add(factNode);
                    root.Children.Add(scope);
                }
                else
                {
                    root.Children.Add(factNode);
                }
            }
            return root;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
    {
        internal const float DefaultMoveSpeedMultiplier = 1f;
        internal const float MinimumMoveSpeedMultiplier = 0.1f;
        internal const float MaximumMoveSpeedMultiplier = 1f;

        public CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind Kind { get; set; }
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; set; }
        public CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; set; }
        public float MoveTargetPositionX { get; set; }
        public float MoveTargetPositionZ { get; set; }
        public int MoveTargetEntityId { get; set; }
        public float MoveMinDistance { get; set; }
        public float MoveMaxDistance { get; set; }
        public float MoveSpeedMultiplier { get; set; } = DefaultMoveSpeedMultiplier;
        public string TargetTimelineId { get; set; } = string.Empty;
        public int MaxLoopRepeats { get; set; }

        public CharacterTestRuntimeAiLabBehaviorRecipeEventOperation Clone()
        {
            return (CharacterTestRuntimeAiLabBehaviorRecipeEventOperation)MemberwiseClone();
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind TimingKind { get; set; } =
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
        // Legacy v6 read-only compatibility. Native v7 JSON never emits this field.
        public CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode DurationMode { get; set; } =
            CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
        public int DurationFrames { get; set; } = 1;
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ConditionRoot { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeEventOperation Operation { get; set; } =
            new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
        // Legacy v6 read-only compatibility. Native v7 JSON uses MaxExecutionsPerPass.
        public int MaxExecutionsPerEntry { get; set; } = 1;
        public int MaxExecutionsPerPass { get; set; } = 1;
        public int CooldownFrames { get; set; }
        public int Priority { get; set; }
        public bool DurationReviewRequired { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin ClockOrigin { get; set; } =
            CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline;
        public string LegacySharedEventId { get; set; } = string.Empty;

        public CharacterTestRuntimeAiLabBehaviorRecipeEvent Clone()
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = EventId,
                TimelineId = TimelineId,
                Title = Title,
                StartFrame = StartFrame,
                TimingKind = TimingKind,
                DurationMode = DurationMode,
                DurationFrames = DurationFrames,
                ConditionRoot = ConditionRoot?.Clone(),
                Operation = Operation?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation(),
                MaxExecutionsPerEntry = MaxExecutionsPerEntry,
                MaxExecutionsPerPass = MaxExecutionsPerPass,
                CooldownFrames = CooldownFrames,
                Priority = Priority,
                DurationReviewRequired = DurationReviewRequired,
                ClockOrigin = ClockOrigin,
                LegacySharedEventId = LegacySharedEventId
            };
        }

        public bool ShouldSerializeDurationMode() => false;
        public bool ShouldSerializeMaxExecutionsPerEntry() => false;
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV8Migration
    {
        internal static bool HasV8Authority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return payload != null && payload.BehaviorRecipeVersion >= 8;
        }

        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.HasV7Authority(payload))
                CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForRead(payload);
            if (!HasV8Authority(payload))
                UpgradeFromV7(payload);
            Canonicalize(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.RebuildLegacyProjection(payload);
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            SynchronizeForRead(payload);
        }

        internal static void UpgradeFromV7(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null || HasV8Authority(payload))
                return;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                for (int j = 0; timeline != null && j < timeline.Events.Count; j++)
                    UpgradeEvent(timeline.Events[j]);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                UpgradeEvent(payload.LegacySharedEvents[i]);
            payload.BehaviorRecipeVersion = 9;
        }

        internal static void Canonicalize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.BehaviorRecipeVersion = 9;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                for (int j = 0; timeline != null && j < timeline.Events.Count; j++)
                    CanonicalizeEvent(timeline.Events[j], timeline.TimelineId);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                CanonicalizeEvent(evt, evt?.TimelineId);
                if (evt != null)
                    evt.ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe;
            }
        }

        internal static bool IsOperationCompatible(
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timingKind,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operationKind)
        {
            switch (operationKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                    return timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition:
                    return timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
                default:
                    return true;
            }
        }

        private static void UpgradeEvent(CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            if (evt == null)
                return;
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operationKind =
                evt.Operation?.Kind ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            switch (operationKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition:
                    evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold:
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability:
                    evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
                    break;
                default:
                    evt.TimingKind = evt.DurationFrames == 1
                        ? CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                        : CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
                    break;
            }
        }

        private static void CanonicalizeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            string timelineId)
        {
            if (evt == null)
                return;
            evt.TimelineId = timelineId ?? evt.TimelineId ?? string.Empty;
            evt.StartFrame = Math.Max(0, evt.StartFrame);
            if (evt.Operation == null)
                evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
            if (evt.Operation.MoveSpeedMultiplier <= 0f
                || float.IsNaN(evt.Operation.MoveSpeedMultiplier)
                || float.IsInfinity(evt.Operation.MoveSpeedMultiplier))
            {
                evt.Operation.MoveSpeedMultiplier =
                    CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
            }
            if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition)
                evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            else if (evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
            if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
            {
                evt.DurationFrames = 1;
                evt.MaxExecutionsPerPass = 1;
                evt.CooldownFrames = 0;
            }
            else
            {
                evt.DurationFrames = Math.Max(
                    1,
                    evt.DurationFrames);
                evt.MaxExecutionsPerPass = Math.Max(0, evt.MaxExecutionsPerPass);
                evt.CooldownFrames = Math.Max(0, evt.CooldownFrames);
            }
            evt.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
            evt.Operation.MaxLoopRepeats = Math.Max(0, evt.Operation.MaxLoopRepeats);
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV10Migration
    {
        internal static bool HasV10Authority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload) =>
            payload != null && payload.BehaviorRecipeVersion >= 10;

        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (!HasV10Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(payload);
                UpgradeFromV9(payload);
            }
            Canonicalize(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.RebuildLegacyProjection(payload);
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload) =>
            SynchronizeForRead(payload);

        internal static void UpgradeFromV9(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null || HasV10Authority(payload))
                return;
            payload.ActivationConditionRoot = WrapLegacyFacts(payload.ActivationConditionRoot);
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null)
                    continue;
                timeline.EntryConditionRoot = WrapLegacyFacts(timeline.EntryConditionRoot);
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[j];
                    if (evt != null)
                        evt.ConditionRoot = WrapLegacyFacts(evt.ConditionRoot);
                }
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                if (evt != null)
                    evt.ConditionRoot = WrapLegacyFacts(evt.ConditionRoot);
            }
            payload.BehaviorRecipeVersion = 10;
        }

        internal static void Canonicalize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.BehaviorRecipeVersion = 10;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode WrapLegacyFacts(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null)
                return null;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                return node;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
            {
                CharacterTestRuntimeAiLabCardConditionFact fact = node.Predicate?.Fact
                    ?? CharacterTestRuntimeAiLabCardConditionFact.TargetExists;
                if (!TryResolveLegacySubject(
                        fact,
                        out CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject))
                {
                    return node;
                }
                var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.scope."
                        + ((int)subject).ToString(CultureInfo.InvariantCulture)
                        + "." + (node.NodeId ?? string.Empty),
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                    Subject = subject
                };
                scope.Children.Add(node);
                return scope;
            }
            for (int i = 0; i < node.Children.Count; i++)
                node.Children[i] = WrapLegacyFacts(node.Children[i]);
            return node;
        }

        private static bool TryResolveLegacySubject(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            out CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            if (CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                    fact,
                    out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor))
            {
                subject = descriptor.Subject;
                return !descriptor.IsSubjectAvailability;
            }
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget;
                    return true;
                default:
                    subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown;
                    return false;
            }
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV7Migration
    {
        internal static bool HasV7Authority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return payload != null && payload.BehaviorRecipeVersion >= 7;
        }

        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
                CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.UpgradeFromV5(payload);
            if (!HasV7Authority(payload))
                UpgradeFromV6(payload);
            Canonicalize(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.RebuildLegacyProjection(payload);
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            SynchronizeForRead(payload);
        }

        internal static void UpgradeFromV6(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null || HasV7Authority(payload))
                return;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                for (int j = 0; timeline != null && j < timeline.Events.Count; j++)
                    UpgradeEvent(timeline.Events[j]);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                UpgradeEvent(payload.LegacySharedEvents[i]);
            payload.BehaviorRecipeVersion = 7;
        }

        internal static void Canonicalize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.BehaviorRecipeVersion = 7;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                for (int j = 0; timeline != null && j < timeline.Events.Count; j++)
                    CanonicalizeEvent(timeline.Events[j], timeline.TimelineId);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                CanonicalizeEvent(evt, evt?.TimelineId);
                if (evt != null)
                    evt.ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe;
            }
        }

        private static void UpgradeEvent(CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            if (evt == null)
                return;
            evt.MaxExecutionsPerPass = Math.Max(0, evt.MaxExecutionsPerEntry);
            evt.DurationReviewRequired = evt.DurationMode ==
                CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.UntilTimelineExit;
            evt.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
        }

        private static void CanonicalizeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            string timelineId)
        {
            if (evt == null)
                return;
            evt.TimelineId = timelineId ?? evt.TimelineId ?? string.Empty;
            evt.StartFrame = Math.Max(0, evt.StartFrame);
            evt.DurationFrames = evt.Operation?.Kind ==
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                    ? 1
                    : Math.Max(1, evt.DurationFrames);
            evt.MaxExecutionsPerPass = Math.Max(0, evt.MaxExecutionsPerPass);
            evt.CooldownFrames = Math.Max(0, evt.CooldownFrames);
            evt.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed;
            if (evt.Operation == null)
                evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
            evt.Operation.MaxLoopRepeats = Math.Max(0, evt.Operation.MaxLoopRepeats);
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV6Migration
    {
        internal static bool HasV6Authority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return false;
            if (payload.BehaviorRecipeVersion >= 6)
                return true;
            if (payload.ActivationConditionRoot != null || payload.LegacySharedEvents.Count > 0)
                return true;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline != null && (timeline.EntryConditionRoot != null || timeline.Events.Count > 0))
                    return true;
            }
            return false;
        }

        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (!HasV6Authority(payload))
                UpgradeFromV5(payload);
            Canonicalize(payload);
            RebuildLegacyProjection(payload);
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            SynchronizeForRead(payload);
        }

        internal static void UpgradeFromV5(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null || HasV6Authority(payload))
                return;

            payload.BehaviorRecipeVersion = 6;

            payload.ActivationConditionRoot =
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode.FromAll(
                    payload.ActivationConditions,
                    "condition.activation.all");

            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                if (timeline == null)
                    continue;
                timeline.EntryConditionRoot =
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode.FromAll(
                        timeline.EntryEvent?.Conditions,
                        "condition.entry." + (timeline.TimelineId ?? timelineIndex.ToString()));

                CharacterTestRuntimeAiLabBehaviorRecipeEvent primary =
                    ConvertPrimaryAction(timeline, timeline.PrimaryActionEvent);
                if (primary != null)
                    timeline.Events.Add(primary);

                for (int eventIndex = 0; eventIndex < timeline.TriggerEvents.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent converted = ConvertTrigger(
                        timeline,
                        timeline.TriggerEvents[eventIndex],
                        primary?.EventId);
                    if (converted != null)
                        timeline.Events.Add(converted);
                }
            }

            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent source =
                    payload.LegacySharedTriggerEvents[i];
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline owner = FindTimeline(
                    payload,
                    source?.TimelineId);
                CharacterTestRuntimeAiLabBehaviorRecipeEvent converted = ConvertTrigger(
                    owner,
                    source,
                    owner?.PrimaryActionEvent?.EventId);
                if (converted == null)
                    continue;
                converted.ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe;
                converted.LegacySharedEventId = source.LegacySharedTriggerId ?? string.Empty;
                payload.LegacySharedEvents.Add(converted);
            }
        }

        internal static void Canonicalize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.BehaviorRecipeVersion = 6;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null)
                    continue;
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                    CanonicalizeEvent(timeline.Events[eventIndex], timeline.TimelineId);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                CanonicalizeEvent(evt, evt?.TimelineId);
                if (evt != null)
                    evt.ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe;
            }
        }

        internal static void RebuildLegacyProjection(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.Stages.Clear();
            payload.Detectors.Clear();
            payload.Transitions.Clear();
            payload.Layout.Stages.Clear();
            payload.EntryStageId = payload.EntryTimelineId ?? string.Empty;

            var ordered = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            ordered.Sort(CompareTimelines);
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = ordered[i];
                if (timeline == null)
                    continue;
                var stage = new CharacterTestRuntimeAiLabLogicStage
                {
                    StageId = timeline.TimelineId ?? string.Empty,
                    Order = i,
                    Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                    Title = timeline.Title ?? string.Empty,
                    Note = timeline.Note ?? string.Empty,
                    EntryEventId = string.Empty,
                    PrimaryActionEventId = string.Empty,
                    IsBehaviorRecipeV5Timeline = false,
                    DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Infinite,
                    PlannedDurationFrames = 1,
                    PreviewDurationFrames = 1
                };
                CloneFactLeaves(timeline.EntryConditionRoot, stage.EntryConditions);
                payload.Stages.Add(stage);
                payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = stage.StageId,
                    Column = i,
                    Width = 1f
                });
            }
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent ConvertPrimaryAction(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent source)
        {
            if (source == null || source.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic)
                return null;
            var operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
            {
                Kind = ToOperationKind(source.Kind),
                AbilityId = source.AbilityId,
                CharacterActionId = source.CharacterActionId,
                MovementStrategy = source.MovementStrategy,
                MovementTargetKind = source.MovementTargetKind,
                MoveTargetPositionX = source.MoveTargetPositionX,
                MoveTargetPositionZ = source.MoveTargetPositionZ,
                MoveTargetEntityId = source.MoveTargetEntityId,
                MoveMinDistance = source.MoveMinDistance,
                MoveMaxDistance = source.MoveMaxDistance,
                MoveSpeedMultiplier = CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier
            };
            bool hold = operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold;
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = source.EventId ?? string.Empty,
                TimelineId = timeline?.TimelineId ?? string.Empty,
                Title = timeline?.Title ?? string.Empty,
                StartFrame = 0,
                DurationMode = hold
                    ? CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed
                    : CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.UntilTimelineExit,
                DurationFrames = hold ? Math.Max(1, source.WaitFrames) : 1,
                MaxExecutionsPerEntry = 1,
                CooldownFrames = 0,
                Priority = 0,
                Operation = operation
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent ConvertTrigger(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent source,
            string primaryEventId)
        {
            if (source == null)
                return null;
            int start = Math.Max(0, source.ActivationStartFrame);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode conditionRoot = null;
            if (source.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate)
            {
                conditionRoot = CharacterTestRuntimeAiLabBehaviorRecipeConditionNode.FromAll(
                    source.Conditions,
                    "condition.event." + (source.EventId ?? string.Empty));
                if (!source.ExpectedResult && conditionRoot != null)
                {
                    var negated = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                    {
                        NodeId = "condition.event.not." + (source.EventId ?? string.Empty),
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                    };
                    negated.Children.Add(conditionRoot);
                    conditionRoot = negated;
                }
            }
            else if (source.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame)
            {
                if (source.LocalFrameAnchor ==
                    CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame)
                {
                    start = Math.Max(0, source.LocalFrame);
                }
                else
                {
                    conditionRoot = CreateSignalCondition(
                        source.EventId,
                        primaryEventId,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed);
                }
            }
            else
            {
                conditionRoot = CreateSignalCondition(
                    source.EventId,
                    primaryEventId,
                    source.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed
                        ? CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed
                        : source.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected
                            ? CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected
                            : CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed);
            }

            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = source.EventId ?? string.Empty,
                TimelineId = timeline?.TimelineId ?? source.TimelineId ?? string.Empty,
                Title = source.Title ?? string.Empty,
                StartFrame = start,
                DurationMode = source.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    ? CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.Fixed
                    : CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.UntilTimelineExit,
                DurationFrames = source.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    ? Math.Max(1, source.ActivationDurationFrames)
                    : 1,
                ConditionRoot = conditionRoot,
                MaxExecutionsPerEntry = 1,
                CooldownFrames = 0,
                Priority = source.Priority,
                ClockOrigin = source.ClockOrigin,
                LegacySharedEventId = source.LegacySharedTriggerId ?? string.Empty,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = source.OutcomeKind == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                        ? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                        : CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner,
                    TargetTimelineId = source.TargetTimelineId ?? string.Empty
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateSignalCondition(
            string eventId,
            string sourceEventId,
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.signal." + (eventId ?? string.Empty),
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                SourceEventId = sourceEventId ?? string.Empty,
                Signal = signal
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind ToOperationKind(
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold;
                default:
                    return CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            }
        }

        private static void CanonicalizeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            string timelineId)
        {
            if (evt == null)
                return;
            evt.TimelineId = timelineId ?? evt.TimelineId ?? string.Empty;
            evt.StartFrame = Math.Max(0, evt.StartFrame);
            evt.DurationFrames = Math.Max(1, evt.DurationFrames);
            evt.MaxExecutionsPerEntry = Math.Max(0, evt.MaxExecutionsPerEntry);
            evt.CooldownFrames = Math.Max(0, evt.CooldownFrames);
            if (evt.Operation == null)
                evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline FindTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    return payload.Timelines[i];
            }
            return null;
        }

        private static void CloneFactLeaves(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            List<CharacterTestRuntimeAiLabCardCondition> destination)
        {
            if (node == null || destination == null)
                return;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
            {
                if (node.Predicate != null)
                    destination.Add(node.Predicate.Clone());
                return;
            }
            for (int i = 0; i < node.Children.Count; i++)
                CloneFactLeaves(node.Children[i], destination);
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
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
    {
        internal static long Compute(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.v6.schema", CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV6Schema);
            hash.AddStringStable("recipe.v6.entryTimeline", payload.EntryTimelineId);
            AddCondition(
                ref hash,
                "recipe.v6.activation",
                payload.ActivationConditionRoot,
                legacyStringActionIds: true);

            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.v6.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.v6.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.v6.timeline.order", timeline?.Order ?? 0);
                AddCondition(
                    ref hash,
                    "recipe.v6.timeline.entry",
                    timeline?.EntryConditionRoot,
                    legacyStringActionIds: true);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(
                    timeline?.Events ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.v6.timeline.eventCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddEvent(ref hash, events[eventIndex]);
            }
            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(payload.LegacySharedEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.v6.sharedCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddEvent(ref hash, shared[i]);
            return hash.ToHash();
        }

        internal static long ComputeV7(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.v7.schema", CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV7Schema);
            hash.AddStringStable("recipe.v7.entryTimeline", payload.EntryTimelineId);
            AddCondition(
                ref hash,
                "recipe.v7.activation",
                payload.ActivationConditionRoot,
                legacyStringActionIds: true);

            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.v7.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.v7.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.v7.timeline.order", timeline?.Order ?? 0);
                AddCondition(
                    ref hash,
                    "recipe.v7.timeline.entry",
                    timeline?.EntryConditionRoot,
                    legacyStringActionIds: true);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(
                    timeline?.Events ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.v7.timeline.eventCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddEventV7(ref hash, events[eventIndex]);
            }
            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(payload.LegacySharedEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.v7.sharedCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddEventV7(ref hash, shared[i]);
            return hash.ToHash();
        }

        internal static long ComputeV8(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeV10(payload);
        }

        internal static long ComputeV9(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeV8(payload, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV9Schema, legacyStringActionIds: false);
        }

        internal static long ComputeLegacyV8(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeV8(payload, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV8Schema, legacyStringActionIds: true);
        }

        private static long ComputeV8(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string schema,
            bool legacyStringActionIds)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.v8.schema", schema);
            hash.AddStringStable("recipe.v8.entryTimeline", payload.EntryTimelineId);
            AddCondition(
                ref hash,
                "recipe.v8.activation",
                payload.ActivationConditionRoot,
                legacyStringActionIds);

            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.v8.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.v8.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.v8.timeline.order", timeline?.Order ?? 0);
                AddCondition(
                    ref hash,
                    "recipe.v8.timeline.entry",
                    timeline?.EntryConditionRoot,
                    legacyStringActionIds);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(
                    timeline?.Events ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.v8.timeline.eventCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddEventV8(ref hash, events[eventIndex], legacyStringActionIds);
            }
            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(payload.LegacySharedEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.v8.sharedCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddEventV8(ref hash, shared[i], legacyStringActionIds);
            return hash.ToHash();
        }

        internal static long ComputeV10(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.v10.schema", CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV10Schema);
            hash.AddStringStable("recipe.v10.entryTimeline", payload.EntryTimelineId);
            AddCondition(ref hash, "recipe.v10.activation", payload.ActivationConditionRoot, false, true);

            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.v10.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.v10.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.v10.timeline.order", timeline?.Order ?? 0);
                AddCondition(ref hash, "recipe.v10.timeline.entry", timeline?.EntryConditionRoot, false, true);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(
                    timeline?.Events ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.v10.timeline.eventCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddEventV10(ref hash, events[eventIndex]);
            }
            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(payload.LegacySharedEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.v10.sharedCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddEventV10(ref hash, shared[i]);
            return hash.ToHash();
        }

        internal static long ComputeV11(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.v11.schema", CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema);
            hash.AddStringStable("recipe.v11.entryTimeline", payload.EntryTimelineId);
            AddCondition(ref hash, "recipe.v11.activation", payload.ActivationConditionRoot, false, true, true);
            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.v11.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.v11.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.v11.timeline.order", timeline?.Order ?? 0);
                AddCondition(ref hash, "recipe.v11.timeline.entry", timeline?.EntryConditionRoot, false, true, true);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(
                    timeline?.Events ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.v11.timeline.eventCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddEventV10(ref hash, events[eventIndex], true);
            }
            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(payload.LegacySharedEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.v11.sharedCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddEventV10(ref hash, shared[i], true);
            return hash.ToHash();
        }

        private static void AddEventV10(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            bool includeTargetMasks = false)
        {
            hash.AddStringStable("recipe.v10.event.id", evt?.EventId);
            hash.AddStringStable("recipe.v10.event.timeline", evt?.TimelineId);
            hash.AddInt("recipe.v10.event.timing", evt != null ? (int)evt.TimingKind : -1);
            hash.AddInt("recipe.v10.event.start", evt?.StartFrame ?? 0);
            hash.AddInt("recipe.v10.event.duration", evt?.DurationFrames ?? 0);
            hash.AddInt("recipe.v10.event.max", evt?.MaxExecutionsPerPass ?? 0);
            hash.AddInt("recipe.v10.event.cooldown", evt?.CooldownFrames ?? 0);
            hash.AddInt("recipe.v10.event.priority", evt?.Priority ?? 0);
            hash.AddInt("recipe.v10.event.review", evt != null && evt.DurationReviewRequired ? 1 : 0);
            hash.AddInt("recipe.v10.event.clock", evt != null ? (int)evt.ClockOrigin : -1);
            hash.AddStringStable("recipe.v10.event.shared", evt?.LegacySharedEventId);
            AddCondition(
                ref hash,
                includeTargetMasks ? "recipe.v11.event.condition" : "recipe.v10.event.condition",
                evt?.ConditionRoot,
                false,
                true,
                includeTargetMasks);
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = evt?.Operation;
            hash.AddInt("recipe.v10.event.operation", operation != null ? (int)operation.Kind : -1);
            hash.AddInt("recipe.v10.event.loopRepeats", operation?.MaxLoopRepeats ?? 0);
            hash.AddInt("recipe.v10.event.ability", operation?.AbilityId ?? 0);
            hash.AddInt("recipe.v10.event.action", operation?.CharacterActionId.Value ?? 0);
            hash.AddInt("recipe.v10.event.moveStrategy", operation != null ? (int)operation.MovementStrategy : 0);
            hash.AddInt("recipe.v10.event.moveTarget", operation != null ? (int)operation.MovementTargetKind : 0);
            hash.AddInt("recipe.v10.event.moveX", Quantize(operation?.MoveTargetPositionX ?? 0f));
            hash.AddInt("recipe.v10.event.moveZ", Quantize(operation?.MoveTargetPositionZ ?? 0f));
            hash.AddInt("recipe.v10.event.moveEntity", operation?.MoveTargetEntityId ?? 0);
            hash.AddInt("recipe.v10.event.moveMin", Quantize(operation?.MoveMinDistance ?? 0f));
            hash.AddInt("recipe.v10.event.moveMax", Quantize(operation?.MoveMaxDistance ?? 0f));
            float moveSpeedMultiplier = operation?.MoveSpeedMultiplier
                ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
            if (Math.Abs(moveSpeedMultiplier
                - CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier) > 0.0001f)
            {
                hash.AddInt("recipe.v10.event.moveSpeedMultiplier", Quantize(moveSpeedMultiplier));
            }
            hash.AddStringStable("recipe.v10.event.targetTimeline", operation?.TargetTimelineId);
        }

        private static void AddEventV8(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            bool legacyStringActionIds)
        {
            hash.AddStringStable("recipe.v8.event.id", evt?.EventId);
            hash.AddStringStable("recipe.v8.event.timeline", evt?.TimelineId);
            hash.AddInt("recipe.v8.event.timing", evt != null ? (int)evt.TimingKind : -1);
            hash.AddInt("recipe.v8.event.start", evt?.StartFrame ?? 0);
            hash.AddInt("recipe.v8.event.duration", evt?.DurationFrames ?? 0);
            hash.AddInt("recipe.v8.event.max", evt?.MaxExecutionsPerPass ?? 0);
            hash.AddInt("recipe.v8.event.cooldown", evt?.CooldownFrames ?? 0);
            hash.AddInt("recipe.v8.event.priority", evt?.Priority ?? 0);
            hash.AddInt("recipe.v8.event.review", evt != null && evt.DurationReviewRequired ? 1 : 0);
            hash.AddInt("recipe.v8.event.clock", evt != null ? (int)evt.ClockOrigin : -1);
            hash.AddStringStable("recipe.v8.event.shared", evt?.LegacySharedEventId);
            AddCondition(
                ref hash,
                "recipe.v8.event.condition",
                evt?.ConditionRoot,
                legacyStringActionIds);
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = evt?.Operation;
            hash.AddInt("recipe.v8.event.operation", operation != null ? (int)operation.Kind : -1);
            hash.AddInt("recipe.v8.event.loopRepeats", operation?.MaxLoopRepeats ?? 0);
            hash.AddInt("recipe.v8.event.ability", operation?.AbilityId ?? 0);
            if (legacyStringActionIds)
                hash.AddStringStable("recipe.v8.event.action", ResolveLegacyActionId(operation?.CharacterActionId ?? default));
            else
                hash.AddInt("recipe.v9.event.action", operation?.CharacterActionId.Value ?? 0);
            hash.AddInt("recipe.v8.event.moveStrategy", operation != null ? (int)operation.MovementStrategy : 0);
            hash.AddInt("recipe.v8.event.moveTarget", operation != null ? (int)operation.MovementTargetKind : 0);
            hash.AddInt("recipe.v8.event.moveX", Quantize(operation?.MoveTargetPositionX ?? 0f));
            hash.AddInt("recipe.v8.event.moveZ", Quantize(operation?.MoveTargetPositionZ ?? 0f));
            hash.AddInt("recipe.v8.event.moveEntity", operation?.MoveTargetEntityId ?? 0);
            hash.AddInt("recipe.v8.event.moveMin", Quantize(operation?.MoveMinDistance ?? 0f));
            hash.AddInt("recipe.v8.event.moveMax", Quantize(operation?.MoveMaxDistance ?? 0f));
            float moveSpeedMultiplier = operation?.MoveSpeedMultiplier
                ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
            // Preserve authenticated v8 payloads written before this optional parameter existed.
            // The default 1x behavior is byte-for-byte equivalent and therefore contributes no new hash item.
            if (Math.Abs(moveSpeedMultiplier
                - CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier) > 0.0001f)
            {
                hash.AddInt("recipe.v8.event.moveSpeedMultiplier", Quantize(moveSpeedMultiplier));
            }
            hash.AddStringStable("recipe.v8.event.targetTimeline", operation?.TargetTimelineId);
        }

        internal static bool TryResolveLegacyActionId(
            string legacyActionId,
            out CharacterActionId actionId)
        {
            switch (legacyActionId ?? string.Empty)
            {
                case "":
                    actionId = CharacterActionId.None;
                    return true;
                case "light_attack":
                    actionId = new CharacterActionId(301);
                    return true;
                case "light_attack_2":
                    actionId = new CharacterActionId(324);
                    return true;
                case "light_attack_3":
                    actionId = new CharacterActionId(325);
                    return true;
                case "codex_flow_test.empty_empty.evade":
                    actionId = new CharacterActionId(930000);
                    return true;
                case "codex_flow_test.empty_empty.reaction.death":
                    actionId = new CharacterActionId(930001);
                    return true;
                case "codex_flow_test.sword_empty.evade":
                    actionId = new CharacterActionId(930100);
                    return true;
                case "codex_flow_test.sword_empty.secondary":
                    actionId = new CharacterActionId(930101);
                    return true;
                case "codex_flow_test.sword_empty.reaction.death":
                    actionId = new CharacterActionId(930102);
                    return true;
                default:
                    actionId = CharacterActionId.None;
                    return false;
            }
        }

        internal static bool TryFormatLegacyActionId(
            CharacterActionId actionId,
            out string legacyActionId)
        {
            switch (actionId.Value)
            {
                case 0:
                    legacyActionId = string.Empty;
                    return true;
                case 301:
                    legacyActionId = "light_attack";
                    return true;
                case 324:
                    legacyActionId = "light_attack_2";
                    return true;
                case 325:
                    legacyActionId = "light_attack_3";
                    return true;
                case 930000:
                    legacyActionId = "codex_flow_test.empty_empty.evade";
                    return true;
                case 930001:
                    legacyActionId = "codex_flow_test.empty_empty.reaction.death";
                    return true;
                case 930100:
                    legacyActionId = "codex_flow_test.sword_empty.evade";
                    return true;
                case 930101:
                    legacyActionId = "codex_flow_test.sword_empty.secondary";
                    return true;
                case 930102:
                    legacyActionId = "codex_flow_test.sword_empty.reaction.death";
                    return true;
                default:
                    legacyActionId = string.Empty;
                    return false;
            }
        }

        private static string ResolveLegacyActionId(CharacterActionId actionId)
        {
            return TryFormatLegacyActionId(actionId, out string legacyActionId)
                ? legacyActionId
                : "#unsupported-legacy-character-action-"
                    + actionId.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddEventV7(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            hash.AddStringStable("recipe.v7.event.id", evt?.EventId);
            hash.AddStringStable("recipe.v7.event.timeline", evt?.TimelineId);
            hash.AddInt("recipe.v7.event.start", evt?.StartFrame ?? 0);
            hash.AddInt("recipe.v7.event.duration", evt?.DurationFrames ?? 0);
            hash.AddInt("recipe.v7.event.max", evt?.MaxExecutionsPerPass ?? 0);
            hash.AddInt("recipe.v7.event.cooldown", evt?.CooldownFrames ?? 0);
            hash.AddInt("recipe.v7.event.priority", evt?.Priority ?? 0);
            hash.AddInt("recipe.v7.event.review", evt != null && evt.DurationReviewRequired ? 1 : 0);
            hash.AddInt("recipe.v7.event.clock", evt != null ? (int)evt.ClockOrigin : -1);
            hash.AddStringStable("recipe.v7.event.shared", evt?.LegacySharedEventId);
            AddCondition(
                ref hash,
                "recipe.v7.event.condition",
                evt?.ConditionRoot,
                legacyStringActionIds: true);
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = evt?.Operation;
            hash.AddInt("recipe.v7.event.operation", operation != null ? (int)operation.Kind : -1);
            hash.AddInt("recipe.v7.event.loopRepeats", operation?.MaxLoopRepeats ?? 0);
            hash.AddInt("recipe.v7.event.ability", operation?.AbilityId ?? 0);
            hash.AddStringStable(
                "recipe.v7.event.action",
                ResolveLegacyActionId(operation?.CharacterActionId ?? default));
            hash.AddInt("recipe.v7.event.moveStrategy", operation != null ? (int)operation.MovementStrategy : 0);
            hash.AddInt("recipe.v7.event.moveTarget", operation != null ? (int)operation.MovementTargetKind : 0);
            hash.AddInt("recipe.v7.event.moveX", Quantize(operation?.MoveTargetPositionX ?? 0f));
            hash.AddInt("recipe.v7.event.moveZ", Quantize(operation?.MoveTargetPositionZ ?? 0f));
            hash.AddInt("recipe.v7.event.moveEntity", operation?.MoveTargetEntityId ?? 0);
            hash.AddInt("recipe.v7.event.moveMin", Quantize(operation?.MoveMinDistance ?? 0f));
            hash.AddInt("recipe.v7.event.moveMax", Quantize(operation?.MoveMaxDistance ?? 0f));
            hash.AddStringStable("recipe.v7.event.targetTimeline", operation?.TargetTimelineId);
        }

        private static void AddEvent(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            hash.AddStringStable("recipe.v6.event.id", evt?.EventId);
            hash.AddStringStable("recipe.v6.event.timeline", evt?.TimelineId);
            hash.AddInt("recipe.v6.event.start", evt?.StartFrame ?? 0);
            hash.AddInt("recipe.v6.event.durationMode", evt != null ? (int)evt.DurationMode : -1);
            hash.AddInt("recipe.v6.event.duration", evt?.DurationFrames ?? 0);
            hash.AddInt("recipe.v6.event.max", evt?.MaxExecutionsPerEntry ?? 0);
            hash.AddInt("recipe.v6.event.cooldown", evt?.CooldownFrames ?? 0);
            hash.AddInt("recipe.v6.event.priority", evt?.Priority ?? 0);
            hash.AddInt("recipe.v6.event.clock", evt != null ? (int)evt.ClockOrigin : -1);
            hash.AddStringStable("recipe.v6.event.shared", evt?.LegacySharedEventId);
            AddCondition(
                ref hash,
                "recipe.v6.event.condition",
                evt?.ConditionRoot,
                legacyStringActionIds: true);
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation = evt?.Operation;
            hash.AddInt("recipe.v6.event.operation", operation != null ? (int)operation.Kind : -1);
            hash.AddInt("recipe.v6.event.ability", operation?.AbilityId ?? 0);
            hash.AddStringStable(
                "recipe.v6.event.action",
                ResolveLegacyActionId(operation?.CharacterActionId ?? default));
            hash.AddInt("recipe.v6.event.moveStrategy", operation != null ? (int)operation.MovementStrategy : 0);
            hash.AddInt("recipe.v6.event.moveTarget", operation != null ? (int)operation.MovementTargetKind : 0);
            hash.AddInt("recipe.v6.event.moveX", Quantize(operation?.MoveTargetPositionX ?? 0f));
            hash.AddInt("recipe.v6.event.moveZ", Quantize(operation?.MoveTargetPositionZ ?? 0f));
            hash.AddInt("recipe.v6.event.moveEntity", operation?.MoveTargetEntityId ?? 0);
            hash.AddInt("recipe.v6.event.moveMin", Quantize(operation?.MoveMinDistance ?? 0f));
            hash.AddInt("recipe.v6.event.moveMax", Quantize(operation?.MoveMaxDistance ?? 0f));
            hash.AddStringStable("recipe.v6.event.targetTimeline", operation?.TargetTimelineId);
        }

        private static void AddCondition(
            ref RuntimeContentHashAccumulator hash,
            string prefix,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            bool legacyStringActionIds,
            bool includeSubject = false,
            bool includeTargetMasks = false)
        {
            if (node == null)
            {
                hash.AddInt(prefix + ".present", 0);
                return;
            }
            hash.AddInt(prefix + ".present", 1);
            hash.AddStringStable(prefix + ".id", node.NodeId);
            hash.AddInt(prefix + ".kind", (int)node.Kind);
            if (includeSubject && node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                hash.AddInt(prefix + ".subject", (int)node.Subject);
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
            {
                CharacterTestRuntimeAiLabCardCondition condition = node.Predicate;
                hash.AddInt(prefix + ".fact", condition != null ? (int)condition.Fact : -1);
                hash.AddInt(prefix + ".operator", condition != null ? (int)condition.Operator : -1);
                hash.AddInt(prefix + ".bool", condition != null && condition.BoolValue ? 1 : 0);
                hash.AddInt(prefix + ".value", Quantize(condition?.Value ?? 0f));
                hash.AddInt(prefix + ".max", Quantize(condition?.MaxValue ?? 0f));
                hash.AddInt(prefix + ".ability", condition?.AbilityId ?? 0);
                if (includeTargetMasks)
                {
                    hash.AddLong(prefix + ".targetPhases", condition?.AllowedTargetPhases ?? 0u);
                    hash.AddLong(prefix + ".targetRequired", condition?.RequiredTargetFlags ?? 0u);
                    hash.AddLong(prefix + ".targetForbidden", condition?.ForbiddenTargetFlags ?? 0u);
                }
                if (legacyStringActionIds)
                {
                    hash.AddStringStable(
                        prefix + ".action",
                        ResolveLegacyActionId(condition?.CharacterActionId ?? default));
                }
                else
                {
                    hash.AddInt(prefix + ".action", condition?.CharacterActionId.Value ?? 0);
                }
                return;
            }
            if (node.MovementQuery != null) { hash.AddStringStable(prefix + ".movementQuery", node.MovementQuery); hash.AddStringStable(prefix + ".movementSource", node.SourceEventId); }
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal)
            {
                hash.AddStringStable(prefix + ".sourceEvent", node.SourceEventId);
                hash.AddInt(prefix + ".signal", (int)node.Signal);
                return;
            }
            var children = new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>(node.Children);
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
            {
                children.Sort(CompareNodes);
            }
            hash.AddInt(prefix + ".childCount", children.Count);
            for (int i = 0; i < children.Count; i++)
                AddCondition(
                    ref hash,
                    prefix + ".child",
                    children[i],
                    legacyStringActionIds,
                    includeSubject,
                    includeTargetMasks);
        }

        private static int CompareTimelines(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline left,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.TimelineId, right.TimelineId);
        }

        private static int CompareEvents(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent left,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId);
        }

        private static int CompareNodes(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode left,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return string.CompareOrdinal(left.NodeId, right.NodeId);
        }

        private static int Quantize(float value) => (int)Math.Round(value * 1000f);
    }
}
