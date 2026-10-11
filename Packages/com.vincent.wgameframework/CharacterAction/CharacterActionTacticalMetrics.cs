using System;
using System.Collections.Generic;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionProberStatus
    {
        Unavailable = 0, Valid = 1, NoHit = 2, Discontinuous = 3, OpenBounded = 4, Unsupported = 5
    }

    /// <summary>A run of successful discrete samples, not a proof of mathematical continuity.</summary>
    public readonly struct CharacterActionSpatialInterval
    {
        /// <remarks>NoAlloc.</remarks>
        public CharacterActionSpatialInterval(int firstHitMillimeters, int lastHitMillimeters)
        {
            if (firstHitMillimeters < 0 || lastHitMillimeters < firstHitMillimeters)
                throw new ArgumentOutOfRangeException(nameof(lastHitMillimeters));
            FirstHitMillimeters = firstHitMillimeters;
            LastHitMillimeters = lastHitMillimeters;
        }

        public int FirstHitMillimeters { get; }
        public int LastHitMillimeters { get; }
    }

    /// <summary>Immutable nominal reference-target evidence for approach planning and coarse filtering only.</summary>
    public sealed class CharacterActionSpatialSummary
    {
        /// <remarks>LowFreqAlloc. The producer must verify every reported hit and the recommended point through formal Combat.</remarks>
        public CharacterActionSpatialSummary(CharacterActionId actionId, string actionContentHash, string referenceDummyHash,
            CharacterActionSpatialMeasurement measurement, CharacterActionProberStatus status,
            CharacterActionSpatialInterval[] intervals, int? verifiedOptimalMillimeters,
            bool lowerBoundOpen, bool upperBoundOpen, CharacterActionTacticalSector sector)
        {
            if (!actionId.IsValid) throw new ArgumentOutOfRangeException(nameof(actionId));
            if (string.IsNullOrWhiteSpace(actionContentHash)) throw new ArgumentException("Action fingerprint is required.", nameof(actionContentHash));
            if (string.IsNullOrWhiteSpace(referenceDummyHash)) throw new ArgumentException("Dummy fingerprint is required.", nameof(referenceDummyHash));
            Measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
            if (!Enum.IsDefined(typeof(CharacterActionProberStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            var copy = intervals == null ? Array.Empty<CharacterActionSpatialInterval>() : (CharacterActionSpatialInterval[])intervals.Clone();
            bool hasHits = copy.Length != 0;
            bool measured = status == CharacterActionProberStatus.Valid || status == CharacterActionProberStatus.Discontinuous
                || status == CharacterActionProberStatus.OpenBounded;
            if (hasHits != measured) throw new ArgumentException("Status and hit intervals disagree.", nameof(status));
            if (!hasHits && (verifiedOptimalMillimeters.HasValue || lowerBoundOpen || upperBoundOpen))
                throw new ArgumentException("Unsuccessful measurements cannot supply distances or open hit boundaries.");
            for (int i = 0; i < copy.Length; i++)
            {
                var interval = copy[i];
                if (interval.FirstHitMillimeters < measurement.SearchMinMillimeters
                    || interval.LastHitMillimeters > measurement.SearchMaxMillimeters
                    || (i > 0 && interval.FirstHitMillimeters <= copy[i - 1].LastHitMillimeters))
                    throw new ArgumentException("Hit intervals must be ordered, disjoint and inside the search range.", nameof(intervals));
            }
            if (verifiedOptimalMillimeters.HasValue && (verifiedOptimalMillimeters.Value < copy[0].FirstHitMillimeters
                || verifiedOptimalMillimeters.Value > copy[copy.Length - 1].LastHitMillimeters))
                throw new ArgumentOutOfRangeException(nameof(verifiedOptimalMillimeters));
            if (hasHits)
            {
                bool expectedUpperOpen = copy[copy.Length - 1].LastHitMillimeters == measurement.SearchMaxMillimeters;
                bool expectedLowerOpen = measurement.SearchMinMillimeters > 0 && copy[0].FirstHitMillimeters == measurement.SearchMinMillimeters;
                if (upperBoundOpen != expectedUpperOpen || lowerBoundOpen != expectedLowerOpen)
                    throw new ArgumentException("Open boundaries must match the sampled search endpoints.");
                CharacterActionProberStatus expected = upperBoundOpen || lowerBoundOpen ? CharacterActionProberStatus.OpenBounded
                    : copy.Length > 1 ? CharacterActionProberStatus.Discontinuous : CharacterActionProberStatus.Valid;
                if (status != expected) throw new ArgumentException("Status must preserve continuity and open-boundary evidence.", nameof(status));
            }
            // default(struct) has no valid fallback tolerance; normalize its absence to the declared fallback.
            Sector = sector.Source == CharacterActionSectorSource.Fallback ? CharacterActionTacticalSector.Fallback : sector;
            ActionId = actionId;
            ActionContentHash = actionContentHash;
            ReferenceDummyHash = referenceDummyHash;
            ProberStatus = status;
            Intervals = Array.AsReadOnly(copy);
            VerifiedOptimalMillimeters = verifiedOptimalMillimeters;
            IsLowerBoundOpen = lowerBoundOpen;
            IsUpperBoundOpen = upperBoundOpen;
        }

        public CharacterActionId ActionId { get; }
        public string ActionContentHash { get; }
        public string ReferenceDummyHash { get; }
        public CharacterActionSpatialMeasurement Measurement { get; }
        public string ProberVersion => Measurement.ProberVersion;
        public double StepSize => Measurement.StepSize;
        public double SearchRangeMin => Measurement.SearchRangeMin;
        public double SearchRangeMax => Measurement.SearchRangeMax;
        public CharacterActionProberStatus ProberStatus { get; }
        public IReadOnlyList<CharacterActionSpatialInterval> Intervals { get; }
        public bool IsContinuousAtResolution => Intervals.Count == 1;
        public bool IsLowerBoundOpen { get; }
        public bool IsUpperBoundOpen { get; }
        public int? VerifiedOptimalMillimeters { get; }
        public double? OptimalDistance => VerifiedOptimalMillimeters / 1000d;
        public double? MinDistance => Intervals.Count == 0 || IsLowerBoundOpen ? (double?)null : Intervals[0].FirstHitMillimeters / 1000d;
        public double? MaxDistance => Intervals.Count == 0 || IsUpperBoundOpen ? (double?)null : Intervals[Intervals.Count - 1].LastHitMillimeters / 1000d;
        public CharacterActionTacticalSector Sector { get; }
    }

    /// <summary>Action timing and optional nominal spatial evidence. A null summary means missing measurement.</summary>
    public sealed class CharacterActionTacticalMetrics
    {
        /// <remarks>LowFreqAlloc. Hash generation belongs to the integrating measurement environment.</remarks>
        public CharacterActionTacticalMetrics(CharacterActionConfig action, string actionContentHash, CharacterActionSpatialSummary spatialSummary = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (string.IsNullOrWhiteSpace(actionContentHash)) throw new ArgumentException("Action fingerprint is required.", nameof(actionContentHash));
            if (spatialSummary != null && (spatialSummary.ActionId != action.ActionId
                || !string.Equals(spatialSummary.ActionContentHash, actionContentHash, StringComparison.Ordinal)))
                throw new ArgumentException("Summary belongs to a different action or content revision.", nameof(spatialSummary));
            ActionId = action.ActionId;
            ActionContentHash = actionContentHash;
            Timing = CharacterActionTacticalUtility.ExtractTiming(action);
            SpatialSummary = spatialSummary;
        }

        public CharacterActionId ActionId { get; }
        public string ActionContentHash { get; }
        public CharacterActionTacticalTiming Timing { get; }
        public long StartupFrames => Timing.StartupFrames;
        public int StartupEndFrame => Timing.StartupEndFrame;
        public long ActiveFrames => Timing.ActiveFrames;
        public int? TotalDurationFrames => Timing.TotalDurationFrames;
        public CharacterActionSpatialSummary SpatialSummary { get; }
    }
}
