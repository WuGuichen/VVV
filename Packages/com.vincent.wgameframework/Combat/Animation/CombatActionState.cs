using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatActionState : IEquatable<CombatActionState>
    {
        public CombatActionState(
            CombatEntityId entityId,
            int actionId,
            int localFrame,
            CombatFrame startedAtFrame,
            CombatActionPhase phase)
            : this(entityId, actionId, localFrame, startedAtFrame, phase, actionInstanceId: 0)
        {
        }

        public CombatActionState(
            CombatEntityId entityId,
            int actionId,
            int localFrame,
            CombatFrame startedAtFrame,
            CombatActionPhase phase,
            int actionInstanceId)
        {
            if (actionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id cannot be negative.");
            }

            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            if (actionInstanceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId), "Action instance id cannot be negative.");
            }

            EntityId = entityId;
            ActionId = actionId;
            LocalFrame = localFrame;
            StartedAtFrame = startedAtFrame;
            Phase = phase;
            ActionInstanceId = actionInstanceId;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int LocalFrame { get; }

        public CombatFrame StartedAtFrame { get; }

        public CombatActionPhase Phase { get; }

        public int ActionInstanceId { get; }

        public bool IsFinished => Phase == CombatActionPhase.Finished;

        public bool Equals(CombatActionState other)
        {
            return EntityId.Equals(other.EntityId)
                && ActionId == other.ActionId
                && LocalFrame == other.LocalFrame
                && StartedAtFrame.Equals(other.StartedAtFrame)
                && Phase == other.Phase
                && ActionInstanceId == other.ActionInstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatActionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ StartedAtFrame.Value;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ ActionInstanceId;
                return hash;
            }
        }
    }
}
