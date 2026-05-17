using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Transient request consumed by <see cref="GameplayPosturePressureSystem"/>.
    /// </summary>
    public readonly struct GameplayPosturePressureRequest : IEquatable<GameplayPosturePressureRequest>
    {
        public GameplayPosturePressureRequest(
            GameplayEntityId entityId,
            int delta,
            string traceId = "")
            : this(entityId, delta, 0, string.Empty, traceId)
        {
        }

        public GameplayPosturePressureRequest(
            GameplayEntityId entityId,
            int delta,
            int sourceId,
            string reason = "",
            string traceId = "")
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay posture pressure request entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public GameplayEntityId EntityId { get; }
        public int Delta { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public string TraceId { get; }

        public bool Equals(GameplayPosturePressureRequest other)
        {
            return EntityId.Equals(other.EntityId)
                && Delta == other.Delta
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayPosturePressureRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.GetHashCode();
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }
    }
}
