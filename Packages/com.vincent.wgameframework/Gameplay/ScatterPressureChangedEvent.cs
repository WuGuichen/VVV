using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct ScatterPressureChangedEvent : IEquatable<ScatterPressureChangedEvent>
    {
        public ScatterPressureChangedEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int previousDebt,
            int currentDebt,
            int currentCohesionLimit,
            PressureBand previousBand,
            PressureBand currentBand,
            int delta,
            int sourceId = 0,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Scatter pressure changed event entity id must be valid.", nameof(entityId));
            if (!PressureBandUtility.IsDefined(previousBand))
                throw new ArgumentOutOfRangeException(nameof(previousBand), "Previous scatter pressure band is not defined.");
            if (!PressureBandUtility.IsDefined(currentBand))
                throw new ArgumentOutOfRangeException(nameof(currentBand), "Current scatter pressure band is not defined.");

            Frame = frame;
            EntityId = entityId;
            PreviousDebt = previousDebt < 0 ? 0 : previousDebt;
            CurrentDebt = currentDebt < 0 ? 0 : currentDebt;
            CurrentCohesionLimit = currentCohesionLimit <= 0 ? 1 : currentCohesionLimit;
            PreviousBand = previousBand;
            CurrentBand = currentBand;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public int PreviousDebt { get; }
        public int CurrentDebt { get; }
        public int CurrentCohesionLimit { get; }
        public float CohesionBreakRisk => CurrentCohesionLimit <= 0 ? 0f : (float)CurrentDebt / CurrentCohesionLimit;
        public PressureBand PreviousBand { get; }
        public PressureBand CurrentBand { get; }
        public int Delta { get; }
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(ScatterPressureChangedEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousDebt == other.PreviousDebt
                && CurrentDebt == other.CurrentDebt
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && PreviousBand == other.PreviousBand
                && CurrentBand == other.CurrentBand
                && Delta == other.Delta
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is ScatterPressureChangedEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ PreviousDebt;
                hash = (hash * 397) ^ CurrentDebt;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ (int)PreviousBand;
                hash = (hash * 397) ^ (int)CurrentBand;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
