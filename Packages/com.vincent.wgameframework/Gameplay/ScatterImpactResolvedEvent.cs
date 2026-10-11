using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct ScatterImpactResolvedEvent : IEquatable<ScatterImpactResolvedEvent>
    {
        public ScatterImpactResolvedEvent(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int scatterImpact,
            int debtBefore,
            int debtAfter,
            int currentCohesionLimit,
            PressureBand bandBefore,
            PressureBand bandAfter,
            int sourceId = 0,
            GameplayScatterPressureReason reason = default,
            RuntimeTraceIdentity traceId = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Scatter impact event entity id must be valid.", nameof(entityId));
            if (!PressureBandUtility.IsDefined(bandBefore))
                throw new ArgumentOutOfRangeException(nameof(bandBefore), "Previous scatter pressure band is not defined.");
            if (!PressureBandUtility.IsDefined(bandAfter))
                throw new ArgumentOutOfRangeException(nameof(bandAfter), "Current scatter pressure band is not defined.");

            Frame = frame;
            EntityId = entityId;
            ScatterImpact = scatterImpact < 0 ? 0 : scatterImpact;
            DebtBefore = debtBefore < 0 ? 0 : debtBefore;
            DebtAfter = debtAfter < 0 ? 0 : debtAfter;
            CurrentCohesionLimit = currentCohesionLimit <= 0 ? 1 : currentCohesionLimit;
            BandBefore = bandBefore;
            BandAfter = bandAfter;
            SourceId = sourceId;
            Reason = reason;
            TraceId = traceId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public int ScatterImpact { get; }
        public int DebtBefore { get; }
        public int DebtAfter { get; }
        public int CurrentCohesionLimit { get; }
        public float CohesionBreakRiskBefore => CurrentCohesionLimit <= 0 ? 0f : (float)DebtBefore / CurrentCohesionLimit;
        public float CohesionBreakRiskAfter => CurrentCohesionLimit <= 0 ? 0f : (float)DebtAfter / CurrentCohesionLimit;
        public PressureBand BandBefore { get; }
        public PressureBand BandAfter { get; }
        public bool WasSupported => DebtAfter < CurrentCohesionLimit && BandAfter != PressureBand.Broken;
        public bool TriggeredBreak => BandAfter == PressureBand.Broken && BandBefore != PressureBand.Broken;
        public int SourceId { get; }
        public GameplayScatterPressureReason Reason { get; }
        public int ReasonCode => Reason.Code;
        public int ReasonArgumentId => Reason.ArgumentId;
        public RuntimeTraceIdentity TraceId { get; }

        public bool Equals(ScatterImpactResolvedEvent other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && ScatterImpact == other.ScatterImpact
                && DebtBefore == other.DebtBefore
                && DebtAfter == other.DebtAfter
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && BandBefore == other.BandBefore
                && BandAfter == other.BandAfter
                && SourceId == other.SourceId
                && Reason == other.Reason
                && TraceId.Equals(other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is ScatterImpactResolvedEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ ScatterImpact;
                hash = (hash * 397) ^ DebtBefore;
                hash = (hash * 397) ^ DebtAfter;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ (int)BandBefore;
                hash = (hash * 397) ^ (int)BandAfter;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
