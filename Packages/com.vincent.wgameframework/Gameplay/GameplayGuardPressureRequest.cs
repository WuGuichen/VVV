using MxFramework.Runtime;
using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Transient request consumed by <see cref="GameplayGuardPressureSystem"/>.
    /// </summary>
    public readonly struct GameplayGuardPressureRequest : IEquatable<GameplayGuardPressureRequest>
    {
        public GameplayGuardPressureRequest(
            GameplayEntityId entityId,
            int delta,
            bool isBlocking,
            RuntimeTraceIdentity traceId = default)
            : this(entityId, delta, isBlocking, 0, default(GameplayScatterPressureReason), traceId)
        {
        }

        public GameplayGuardPressureRequest(
            GameplayEntityId entityId,
            int delta,
            bool isBlocking,
            int sourceId,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay guard pressure request entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            Delta = delta;
            IsBlocking = isBlocking;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public GameplayEntityId EntityId { get; }
        public int Delta { get; }
        public bool IsBlocking { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(GameplayGuardPressureRequest other)
        {
            return EntityId.Equals(other.EntityId)
                && Delta == other.Delta
                && IsBlocking == other.IsBlocking
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayGuardPressureRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.GetHashCode();
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ (IsBlocking ? 1 : 0);
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
