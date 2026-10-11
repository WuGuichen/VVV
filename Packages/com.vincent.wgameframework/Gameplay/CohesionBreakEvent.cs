using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Published when scatter pressure crosses into the broken band.
    /// </summary>
    public readonly struct CohesionBreakEvent : IEquatable<CohesionBreakEvent>
    {
        public CohesionBreakEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            int previousValue,
            int currentScatterPressure,
            int maxScatterPressure,
            int delta,
            int sourceId = 0,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default,
            int currentCohesionLimit = 0)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Cohesion break event entity id must be valid.", nameof(entityId));
            if (!PressureBandUtility.IsDefined(previousBand))
                throw new ArgumentOutOfRangeException(nameof(previousBand), "Previous pressure band is not defined.");

            Frame = frame;
            EntityId = entityId;
            PreviousBand = previousBand;
            PreviousValue = previousValue;
            CurrentScatterPressure = currentScatterPressure;
            MaxScatterPressure = maxScatterPressure;
            CurrentCohesionLimit = currentCohesionLimit <= 0 ? maxScatterPressure : currentCohesionLimit;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public PressureBand PreviousBand { get; }
        public PressureBand OldBand => PreviousBand;
        public int PreviousValue { get; }
        public int OldPressure => PreviousValue;
        public int CurrentScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public int CurrentCohesionLimit { get; }
        public float CohesionBreakRisk => CurrentCohesionLimit <= 0 ? 0f : (float)CurrentScatterPressure / CurrentCohesionLimit;
        public int BreakValue => CurrentScatterPressure;
        public int Delta { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(CohesionBreakEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousBand == other.PreviousBand
                && PreviousValue == other.PreviousValue
                && CurrentScatterPressure == other.CurrentScatterPressure
                && MaxScatterPressure == other.MaxScatterPressure
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && Delta == other.Delta
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is CohesionBreakEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ (int)PreviousBand;
                hash = (hash * 397) ^ PreviousValue;
                hash = (hash * 397) ^ CurrentScatterPressure;
                hash = (hash * 397) ^ MaxScatterPressure;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
