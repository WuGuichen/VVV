using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterActionRangeProbeSample
    {
        internal CharacterActionRangeProbeSample(int distance, CharacterTestAttackOpportunityResult evidence)
        { DistanceMillimeters = distance; Evidence = evidence; }
        internal int DistanceMillimeters { get; }
        internal CharacterTestAttackOpportunityResult Evidence { get; }
        internal bool Hit => Evidence.Status == CharacterTestAttackOpportunityStatus.Valid && Evidence.CanHitFromCurrentPose;
    }

    internal sealed class CharacterActionRangeProbeResult
    {
        internal CharacterActionRangeProbeResult(CharacterActionProberStatus status, string issue,
            CharacterActionSpatialSummary summary = null, CharacterActionRangeProbeSample[] samples = null,
            CharacterActionRangeProbeSample? midpoint = null)
        {
            Status = status;
            Issue = issue ?? string.Empty;
            Summary = summary;
            Samples = Array.AsReadOnly(samples == null ? Array.Empty<CharacterActionRangeProbeSample>() : (CharacterActionRangeProbeSample[])samples.Clone());
            MidpointEvidence = midpoint;
        }
        internal CharacterActionProberStatus Status { get; }
        internal string Issue { get; }
        internal CharacterActionSpatialSummary Summary { get; }
        internal IReadOnlyList<CharacterActionRangeProbeSample> Samples { get; }
        internal CharacterActionRangeProbeSample? MidpointEvidence { get; }
    }

    /// <summary>Deterministic reference-target measurement through the formal Combat pipeline.</summary>
    /// <remarks>LowFreqAlloc. Offline/configuration measurement only; never a gameplay permission check.</remarks>
    internal static class CharacterActionRangeProber
    {
        internal const string Version = "character-test-reference-prober-v5-unmerged-hurtbox-surface";
        internal const string PreviousHurtboxSurfaceVersion = "character-test-reference-prober-v4-hurtbox-surface";
        // Cache units are independent of the measurement algorithm revision. Decode v4
        // in its original units so the store can mark it stale and remeasure normally.
        internal static bool UsesHurtboxSurfaceMetric(string version)
            => version == Version || version == PreviousHurtboxSurfaceVersion;
        internal const int MaximumGridSamples = 4096;

        internal static CharacterActionRangeProbeResult Measure(CharacterActionId actionId, CharacterActionSpatialMeasurement measurement,
            int sourceCharacterId = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId)
        {
            if (measurement == null) return Failure(CharacterActionProberStatus.Unavailable, "MeasurementMissing");
            try
            {
                using (var session = new CharacterActionReferenceProbeSession(actionId, measurement.ReferenceDummy, sourceCharacterId))
                    return MeasureSession(session, measurement);
            }
            catch (ArgumentException error) { return Failure(CharacterActionProberStatus.Unavailable, error.Message); }
            catch (InvalidOperationException error) { return Failure(CharacterActionProberStatus.Unavailable, error.Message); }
            catch (IOException error) { return Failure(CharacterActionProberStatus.Unavailable, error.Message); }
        }

        internal static CharacterActionRangeProbeResult MeasureSession(CharacterActionReferenceProbeSession session,
            CharacterActionSpatialMeasurement measurement, Func<int, int, bool> continueMeasurement = null)
        {
            CharacterActionRangeProbeResult result = null;
            foreach (var step in MeasureSteps(session, measurement, measured => result = measured, continueMeasurement)) { }
            return result;
        }

        internal static IEnumerable<object> MeasureSteps(CharacterActionReferenceProbeSession session,
            CharacterActionSpatialMeasurement measurement, Action<CharacterActionRangeProbeResult> complete,
            Func<int, int, bool> continueMeasurement = null)
        {
            if (session == null || measurement == null) { complete(Failure(CharacterActionProberStatus.Unavailable, "MeasurementSessionMissing")); yield break; }
            if (!string.Equals(measurement.ProberVersion, Version, StringComparison.Ordinal))
                { complete(Failure(CharacterActionProberStatus.Unsupported, "ProberVersionUnsupported")); yield break; }
            long range = (long)measurement.SearchMaxMillimeters - measurement.SearchMinMillimeters;
            long sampleCount = (range + measurement.StepMillimeters - 1) / measurement.StepMillimeters + 1;
            if (sampleCount > MaximumGridSamples) { complete(Failure(CharacterActionProberStatus.Unsupported, "SampleBudgetExceeded")); yield break; }
            if (session.Action.Category != CharacterActionCategory.BasicAttack)
                { complete(Failure(CharacterActionProberStatus.Unsupported, "ActionEvaluatorUnsupported")); yield break; }
            string dummyHash = CharacterActionContentHashUtility.ComputeReferenceDummyHash(measurement.ReferenceDummy);
            if (dummyHash != CharacterActionContentHashUtility.ComputeReferenceDummyHash(session.ReferenceDummy))
                { complete(Failure(CharacterActionProberStatus.Unavailable, "ReferenceDummyMismatch")); yield break; }
            if (!session.TryComputeActionHash(out string hash, out string issue)) { complete(Failure(CharacterActionProberStatus.Unavailable, issue)); yield break; }
            string trajectoryIssue = null;
            foreach (var step in session.PrepareTrajectorySteps(error => trajectoryIssue = error,
                () => continueMeasurement?.Invoke(0, (int)sampleCount + 1) == false)) yield return null;
            if (trajectoryIssue != null) { complete(Failure(CharacterActionProberStatus.Unavailable, trajectoryIssue)); yield break; }
            var samples = new List<CharacterActionRangeProbeSample>((int)sampleCount);
            var intervals = new List<CharacterActionSpatialInterval>();
            int start = -1;
            int previousHit = -1;
            for (long d = measurement.SearchMinMillimeters; ; d = Math.Min(d + measurement.StepMillimeters, measurement.SearchMaxMillimeters))
            {
                if (continueMeasurement?.Invoke(samples.Count, (int)sampleCount + 1) == false)
                    { complete(Failure(CharacterActionProberStatus.Unavailable, "MeasurementCancelled")); yield break; }
                if (!session.TryEvaluate((int)d, out CharacterTestAttackOpportunityResult evidence, out issue))
                    { complete(Failure(evidence.Status == CharacterTestAttackOpportunityStatus.Unsupported
                        ? CharacterActionProberStatus.Unsupported : CharacterActionProberStatus.Unavailable, issue)); yield break; }
                var sample = new CharacterActionRangeProbeSample((int)d, evidence);
                samples.Add(sample);
                yield return null;
                if (sample.Hit)
                {
                    if (start < 0) start = (int)d;
                    previousHit = (int)d;
                }
                else if (start >= 0)
                {
                    intervals.Add(new CharacterActionSpatialInterval(start, previousHit));
                    start = -1;
                }
                if (d == measurement.SearchMaxMillimeters) break;
            }
            if (start >= 0) intervals.Add(new CharacterActionSpatialInterval(start, previousHit));
            bool upperOpen = intervals.Count > 0 && samples[samples.Count - 1].Hit;
            bool lowerOpen = intervals.Count > 0 && samples[0].Hit && measurement.SearchMinMillimeters > 0;
            int? optimal = null;
            CharacterActionRangeProbeSample? midpoint = null;
            issue = string.Empty;
            if (intervals.Count > 0)
            {
                long doubledMiddle = (long)intervals[0].FirstHitMillimeters + intervals[intervals.Count - 1].LastHitMillimeters;
                long nearest = long.MaxValue;
                foreach (CharacterActionRangeProbeSample sample in samples)
                {
                    if (!sample.Hit) continue;
                    long delta = Math.Abs(2L * sample.DistanceMillimeters - doubledMiddle);
                    if (delta < nearest) { nearest = delta; optimal = sample.DistanceMillimeters; }
                }
                if (!upperOpen && !lowerOpen && doubledMiddle % 2 == 0)
                {
                    if (continueMeasurement?.Invoke(samples.Count, (int)sampleCount + 1) == false)
                        { complete(Failure(CharacterActionProberStatus.Unavailable, "MeasurementCancelled")); yield break; }
                    int middle = (int)(doubledMiddle / 2);
                    if (!session.TryEvaluate(middle, out CharacterTestAttackOpportunityResult evidence, out string midpointIssue))
                        { complete(Failure(CharacterActionProberStatus.Unavailable, "MidpointQueryFailed:" + midpointIssue)); yield break; }
                    midpoint = new CharacterActionRangeProbeSample(middle, evidence);
                    yield return null;
                    if (midpoint.Value.Hit) optimal = middle;
                    else issue = "MidpointMissed:UsingVerifiedSample";
                }
                else issue = upperOpen || lowerOpen ? "OpenBoundary:UsingVerifiedSample" : "HalfMillimeterMidpoint:UsingVerifiedSample";
            }
            if (!session.TryComputeActionHash(out string after, out string hashIssue) || !string.Equals(hash, after, StringComparison.Ordinal))
                { complete(Failure(CharacterActionProberStatus.Unavailable, "MeasurementInputChanged:" + hashIssue)); yield break; }
            CharacterActionProberStatus status = intervals.Count == 0 ? CharacterActionProberStatus.NoHit
                : lowerOpen || upperOpen ? CharacterActionProberStatus.OpenBounded
                : intervals.Count > 1 ? CharacterActionProberStatus.Discontinuous : CharacterActionProberStatus.Valid;
            var summary = new CharacterActionSpatialSummary(session.Action.ActionId, hash, dummyHash, measurement, status,
                intervals.ToArray(), optimal, lowerOpen, upperOpen, CharacterActionTacticalSector.Fallback);
            complete(new CharacterActionRangeProbeResult(status, issue, summary, samples.ToArray(), midpoint));
        }

        private static CharacterActionRangeProbeResult Failure(CharacterActionProberStatus status, string issue) =>
            new CharacterActionRangeProbeResult(status, issue);

    }
}
