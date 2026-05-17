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
            string traceId = "")
            : this(entityId, delta, isBlocking, 0, string.Empty, traceId)
        {
        }

        public GameplayGuardPressureRequest(
            GameplayEntityId entityId,
            int delta,
            bool isBlocking,
            int sourceId,
            string reason = "",
            string traceId = "")
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay guard pressure request entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            Delta = delta;
            IsBlocking = isBlocking;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public GameplayEntityId EntityId { get; }
        public int Delta { get; }
        public bool IsBlocking { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public string TraceId { get; }

        public bool Equals(GameplayGuardPressureRequest other)
        {
            return EntityId.Equals(other.EntityId)
                && Delta == other.Delta
                && IsBlocking == other.IsBlocking
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
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
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }
    }
}
