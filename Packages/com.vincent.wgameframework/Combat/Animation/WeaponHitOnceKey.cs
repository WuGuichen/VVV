using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public readonly struct WeaponHitOnceKey : IEquatable<WeaponHitOnceKey>
    {
        public WeaponHitOnceKey(int actionInstanceId, int traceId, CombatEntityId targetEntityId)
        {
            if (actionInstanceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId), "Action instance id cannot be negative.");
            }

            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            }

            ActionInstanceId = actionInstanceId;
            TraceId = traceId;
            TargetEntityId = targetEntityId;
        }

        public int ActionInstanceId { get; }

        public int TraceId { get; }

        public CombatEntityId TargetEntityId { get; }

        public bool Equals(WeaponHitOnceKey other)
        {
            return ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && TargetEntityId.Equals(other.TargetEntityId);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponHitOnceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ TargetEntityId.Value;
                return hash;
            }
        }
    }
}
