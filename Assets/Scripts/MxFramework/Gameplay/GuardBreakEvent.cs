using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Published when guard pressure crosses into the broken band.
    /// </summary>
    public readonly struct GuardBreakEvent : IEquatable<GuardBreakEvent>
    {
        public GuardBreakEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            int previousValue,
            int currentPressure,
            int maxPressure,
            int delta,
            int sourceId = 0,
            string reason = "",
            string traceId = "")
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Guard break event entity id must be valid.", nameof(entityId));
            if (!Enum.IsDefined(typeof(PressureBand), previousBand))
                throw new ArgumentOutOfRangeException(nameof(previousBand), "Previous guard pressure band is not defined.");

            Frame = frame;
            EntityId = entityId;
            PreviousBand = previousBand;
            PreviousValue = previousValue;
            CurrentPressure = currentPressure;
            MaxPressure = maxPressure;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public PressureBand PreviousBand { get; }
        public PressureBand OldBand => PreviousBand;
        public int PreviousValue { get; }
        public int OldPressure => PreviousValue;
        public int CurrentPressure { get; }
        public int MaxPressure { get; }
        public int BreakValue => CurrentPressure;
        public int Delta { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public string TraceId { get; }

        public bool Equals(GuardBreakEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousBand == other.PreviousBand
                && PreviousValue == other.PreviousValue
                && CurrentPressure == other.CurrentPressure
                && MaxPressure == other.MaxPressure
                && Delta == other.Delta
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GuardBreakEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ (int)PreviousBand;
                hash = (hash * 397) ^ PreviousValue;
                hash = (hash * 397) ^ CurrentPressure;
                hash = (hash * 397) ^ MaxPressure;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }
    }
}
