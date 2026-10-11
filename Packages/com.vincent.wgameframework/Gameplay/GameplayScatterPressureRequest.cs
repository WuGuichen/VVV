using MxFramework.Runtime;
using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Transient request consumed by <see cref="GameplayScatterPressureSystem"/>.
    /// </summary>
    public readonly struct GameplayScatterPressureRequest : IEquatable<GameplayScatterPressureRequest>
    {
        public GameplayScatterPressureRequest(
            GameplayEntityId entityId,
            int delta,
            RuntimeTraceIdentity traceId = default)
            : this(entityId, delta, 0, default(GameplayScatterPressureReason), traceId)
        {
        }

        public GameplayScatterPressureRequest(
            GameplayEntityId entityId,
            int delta,
            int sourceId,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay scatter pressure request entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public GameplayScatterPressureRequest(GameplayEntityId entityId, int delta, int sourceId,
            int reasonCode, int reasonArgumentId, RuntimeTraceIdentity traceId = default)
            : this(entityId, delta, sourceId, new GameplayScatterPressureReason(reasonCode, reasonArgumentId), traceId) { }

        public GameplayEntityId EntityId { get; }
        public int Delta { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(GameplayScatterPressureRequest other)
        {
            return EntityId.Equals(other.EntityId)
                && Delta == other.Delta
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayScatterPressureRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.GetHashCode();
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
