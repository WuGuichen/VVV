using System;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionAdapterContext : IEquatable<CharacterActionAdapterContext>
    {
        public CharacterActionAdapterContext(
            RuntimeFrame frame,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId combatBodyId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            Frame = frame;
            GameplayEntityId = gameplayEntityId;
            CombatEntityId = combatEntityId;
            CombatBodyId = combatBodyId;
            SourceId = sourceId;
            TraceId = traceId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CombatEntityId CombatEntityId { get; }
        public CombatBodyId CombatBodyId { get; }
        public int SourceId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public bool HasGameplayEntity => GameplayEntityId.IsValid;
        public bool HasCombatEntity => !CombatEntityId.IsNone;
        public bool HasCombatBody => !CombatBodyId.IsNone;

        public bool Equals(CharacterActionAdapterContext other)
        {
            return Frame.Equals(other.Frame)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatEntityId.Equals(other.CombatEntityId)
                && CombatBodyId.Equals(other.CombatBodyId)
                && SourceId == other.SourceId
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAdapterContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CharacterActionAdapterDispatchMetadata : IEquatable<CharacterActionAdapterDispatchMetadata>
    {
        public CharacterActionAdapterDispatchMetadata(
            RuntimeFrame frame,
            long instanceId,
            long planId,
            CharacterActionId actionId,
            int localFrame,
            int stableEventId,
            int sourceId,
            RuntimeTraceIdentity traceId,
            CharacterActionTrackLifecyclePhase lifecyclePhase = CharacterActionTrackLifecyclePhase.None,
            CharacterActionTrackLifecycleBoundary lifecycleBoundary = CharacterActionTrackLifecycleBoundary.BeforeFrame,
            CharacterActionTrackExitReason lifecycleExitReason = CharacterActionTrackExitReason.None)
        {
            if (instanceId < 0L)
                throw new ArgumentOutOfRangeException(nameof(instanceId), "Action instance id cannot be negative.");
            if (planId < 0L)
                throw new ArgumentOutOfRangeException(nameof(planId), "Action plan id cannot be negative.");
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Action local frame cannot be negative.");

            Frame = frame;
            InstanceId = instanceId;
            PlanId = planId;
            ActionId = actionId;
            LocalFrame = localFrame;
            StableEventId = stableEventId;
            SourceId = sourceId;
            TraceId = traceId;
            LifecyclePhase = lifecyclePhase;
            LifecycleBoundary = lifecycleBoundary;
            LifecycleExitReason = lifecycleExitReason;
        }

        public RuntimeFrame Frame { get; }
        public long InstanceId { get; }
        public long PlanId { get; }
        public CharacterActionId ActionId { get; }
        public int LocalFrame { get; }
        public int StableEventId { get; }
        public int SourceId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public CharacterActionTrackLifecyclePhase LifecyclePhase { get; }
        public CharacterActionTrackLifecycleBoundary LifecycleBoundary { get; }
        public CharacterActionTrackExitReason LifecycleExitReason { get; }
        public bool HasLifecycle => LifecyclePhase != CharacterActionTrackLifecyclePhase.None;

        public bool Equals(CharacterActionAdapterDispatchMetadata other)
        {
            return Frame.Equals(other.Frame)
                && InstanceId == other.InstanceId
                && PlanId == other.PlanId
                && ActionId == other.ActionId
                && LocalFrame == other.LocalFrame
                && (StableEventId == other.StableEventId)
                && SourceId == other.SourceId
                && LifecyclePhase == other.LifecyclePhase
                && LifecycleBoundary == other.LifecycleBoundary
                && LifecycleExitReason == other.LifecycleExitReason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAdapterDispatchMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ PlanId.GetHashCode();
                hash = (hash * 397) ^ ActionId.GetHashCode();
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (int)LifecyclePhase;
                hash = (hash * 397) ^ (int)LifecycleBoundary;
                hash = (hash * 397) ^ (int)LifecycleExitReason;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
