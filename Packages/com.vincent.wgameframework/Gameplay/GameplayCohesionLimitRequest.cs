using MxFramework.Runtime;
using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayCohesionLimitRequest : IEquatable<GameplayCohesionLimitRequest>
    {
        public GameplayCohesionLimitRequest(
            GameplayEntityId entityId,
            int cohesionLimit,
            int sourceId = 0,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay cohesion limit request entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            CohesionLimit = cohesionLimit <= 0 ? 1 : cohesionLimit;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public GameplayEntityId EntityId { get; }
        public int CohesionLimit { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(GameplayCohesionLimitRequest other)
        {
            return EntityId.Equals(other.EntityId)
                && CohesionLimit == other.CohesionLimit
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayCohesionLimitRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.GetHashCode();
                hash = (hash * 397) ^ CohesionLimit;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
