using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Published when armor integrity reaches zero.
    /// </summary>
    public readonly struct ArmorBreakEvent : IEquatable<ArmorBreakEvent>
    {
        public ArmorBreakEvent(GameplayEntityId entityId)
            : this(RuntimeFrame.Zero, entityId, 0, 0, 0, 0, string.Empty)
        {
        }

        public ArmorBreakEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int previousIntegrity,
            int currentIntegrity,
            int maxIntegrity,
            int incomingDamage,
            string traceId = "")
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Armor break event entity id must be valid.", nameof(entityId));

            Frame = frame;
            EntityId = entityId;
            PreviousIntegrity = previousIntegrity;
            CurrentIntegrity = currentIntegrity;
            MaxIntegrity = maxIntegrity;
            IncomingDamage = incomingDamage;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public int PreviousIntegrity { get; }
        public int CurrentIntegrity { get; }
        public int MaxIntegrity { get; }
        public int IncomingDamage { get; }
        public string TraceId { get; }

        public bool Equals(ArmorBreakEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousIntegrity == other.PreviousIntegrity
                && CurrentIntegrity == other.CurrentIntegrity
                && MaxIntegrity == other.MaxIntegrity
                && IncomingDamage == other.IncomingDamage
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ArmorBreakEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ PreviousIntegrity;
                hash = (hash * 397) ^ CurrentIntegrity;
                hash = (hash * 397) ^ MaxIntegrity;
                hash = (hash * 397) ^ IncomingDamage;
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }
    }
}
