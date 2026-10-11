using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Published when a pressure component moves between pressure bands.
    /// </summary>
    public readonly struct PressureBandChangedEvent : IEquatable<PressureBandChangedEvent>
    {
        public PressureBandChangedEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            PressureBand newBand,
            int previousValue,
            int newValue,
            int delta,
            int sourceId = 0,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default,
            int currentLimit = 0)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Pressure band changed event entity id must be valid.", nameof(entityId));
            if (!PressureBandUtility.IsDefined(previousBand))
                throw new ArgumentOutOfRangeException(nameof(previousBand), "Previous pressure band is not defined.");
            if (!PressureBandUtility.IsDefined(newBand))
                throw new ArgumentOutOfRangeException(nameof(newBand), "New pressure band is not defined.");

            Frame = frame;
            EntityId = entityId;
            PreviousBand = previousBand;
            NewBand = newBand;
            PreviousValue = previousValue;
            NewValue = newValue;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
            CurrentLimit = currentLimit <= 0 ? newValue : currentLimit;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public PressureBand PreviousBand { get; }
        public PressureBand OldBand => PreviousBand;
        public PressureBand NewBand { get; }
        public int PreviousValue { get; }
        public int OldPressure => PreviousValue;
        public int NewValue { get; }
        public int CurrentPressure => NewValue;
        public int CurrentLimit { get; }
        public float PressureRatio => CurrentLimit <= 0 ? 0f : (float)NewValue / CurrentLimit;
        public int Delta { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(PressureBandChangedEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousBand == other.PreviousBand
                && NewBand == other.NewBand
                && PreviousValue == other.PreviousValue
                && NewValue == other.NewValue
                && CurrentLimit == other.CurrentLimit
                && Delta == other.Delta
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is PressureBandChangedEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ (int)PreviousBand;
                hash = (hash * 397) ^ (int)NewBand;
                hash = (hash * 397) ^ PreviousValue;
                hash = (hash * 397) ^ NewValue;
                hash = (hash * 397) ^ CurrentLimit;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
