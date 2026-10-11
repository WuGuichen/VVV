using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionSpatialContractTests
    {
        [Test]
        public void OpenUpperBoundaryDoesNotInventMaximumAndPreservesGaps()
        {
            var summary = Summary(CharacterActionProberStatus.OpenBounded, new[] {
                new CharacterActionSpatialInterval(500, 1000), new CharacterActionSpatialInterval(1500, 3000) }, 750, upperOpen: true);
            Assert.IsNull(summary.MaxDistance);
            Assert.AreEqual(0.5d, summary.MinDistance);
            Assert.IsFalse(summary.IsContinuousAtResolution);
            Assert.AreEqual(3000, summary.Intervals[1].LastHitMillimeters);
            Assert.AreEqual(0.75d, summary.OptimalDistance);
        }

        [Test]
        public void PositiveStartHitMarksLowerBoundaryOpen()
        {
            var summary = new CharacterActionSpatialSummary(new CharacterActionId(301), "action", "dummy",
                Measurement(100), CharacterActionProberStatus.OpenBounded,
                new[] { new CharacterActionSpatialInterval(100, 500) }, 300, true, false, default);
            Assert.IsNull(summary.MinDistance);
            Assert.AreEqual(0.5d, summary.MaxDistance);
            Assert.IsTrue(summary.IsContinuousAtResolution);
        }

        [TestCase(CharacterActionProberStatus.NoHit)]
        [TestCase(CharacterActionProberStatus.Unavailable)]
        [TestCase(CharacterActionProberStatus.Unsupported)]
        public void NonHitStatusesNeverProvideDistances(CharacterActionProberStatus status)
        {
            var summary = Summary(status, null, null);
            Assert.IsNull(summary.MinDistance);
            Assert.IsNull(summary.MaxDistance);
            Assert.IsNull(summary.OptimalDistance);
            Assert.IsFalse(summary.IsContinuousAtResolution);
            Assert.Throws<ArgumentException>(() => Summary(status, null, 0));
        }

        [Test]
        public void SummaryDefensivelyCopiesIntervals()
        {
            var intervals = new[] { new CharacterActionSpatialInterval(500, 1000) };
            var summary = Summary(CharacterActionProberStatus.Valid, intervals, 750);
            intervals[0] = new CharacterActionSpatialInterval(0, 0);
            Assert.AreEqual(500, summary.Intervals[0].FirstHitMillimeters);
            Assert.Throws<NotSupportedException>(() => ((IList<CharacterActionSpatialInterval>)summary.Intervals)[0] = default);
        }

        [Test]
        public void StatusAndEndpointInconsistencyIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Summary(CharacterActionProberStatus.Valid,
                new[] { new CharacterActionSpatialInterval(500, 3000) }, 750));
            Assert.Throws<ArgumentException>(() => Summary(CharacterActionProberStatus.Valid,
                new[] { new CharacterActionSpatialInterval(500, 1000), new CharacterActionSpatialInterval(1500, 2000) }, 750));
            Assert.Throws<ArgumentOutOfRangeException>(() => Summary(CharacterActionProberStatus.Discontinuous,
                new[] { new CharacterActionSpatialInterval(500, 1000), new CharacterActionSpatialInterval(1500, 2000) }, 2500));
            var extraVerifiedMidpoint = Summary(CharacterActionProberStatus.Discontinuous,
                new[] { new CharacterActionSpatialInterval(500, 1000), new CharacterActionSpatialInterval(1500, 2000) }, 1250);
            Assert.AreEqual(1.25d, extraVerifiedMidpoint.OptimalDistance);
            Assert.IsFalse(extraVerifiedMidpoint.IsContinuousAtResolution);
        }

        [Test]
        public void MissingSpatialEvidenceAndWrongIdentityAreExplicit()
        {
            var action = new CharacterActionConfig(new CharacterActionId(301), "test", CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored, null, 0, 20, null, null, null, null);
            var metrics = new CharacterActionTacticalMetrics(action, "action");
            Assert.IsNull(metrics.SpatialSummary);
            Assert.AreEqual(20, metrics.TotalDurationFrames);
            var summary = Summary(CharacterActionProberStatus.NoHit, null, null);
            Assert.Throws<ArgumentException>(() => new CharacterActionTacticalMetrics(action, "changed", summary));
        }

        [Test]
        public void SectorProvenanceAndExactMillimeterInputsArePreserved()
        {
            var summary = Summary(CharacterActionProberStatus.NoHit, null, null);
            Assert.AreEqual(CharacterActionSectorSource.Fallback, summary.Sector.Source);
            Assert.AreEqual(30, summary.Sector.ToleranceDegrees);
            Assert.AreEqual(0.05d, summary.StepSize);
            Assert.AreEqual(3d, summary.SearchRangeMax);
            Assert.AreEqual(1800, summary.Measurement.ReferenceDummy.HeightMillimeters);
            var authored = new CharacterActionTacticalSector(12000, 20000, CharacterActionSectorSource.Authored);
            Assert.AreEqual(12, authored.CenterDegrees);
            Assert.AreEqual(CharacterActionSectorSource.Authored, authored.Source);
            Assert.Throws<ArgumentException>(() => new CharacterActionTacticalSector(0, 0, CharacterActionSectorSource.Fallback));
        }

        [Test]
        public void InvalidDummyAndScanInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionReferenceDummy(1, 400, 300, 0, 200, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionReferenceDummy(0, 1800, 300, 0, 900, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionSpatialMeasurement("v1", 0, 0, 3000, Dummy()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionSpatialMeasurement("v1", 50, -1, 3000, Dummy()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionSpatialMeasurement("v1", 50, 3000, 3000, Dummy()));
            Assert.Throws<ArgumentException>(() => new CharacterActionSpatialMeasurement("", 50, 0, 3000, Dummy()));
        }

        private static CharacterActionReferenceDummy Dummy() => new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0);
        private static CharacterActionSpatialMeasurement Measurement(int min = 0) => new CharacterActionSpatialMeasurement("v1", 50, min, 3000, Dummy());
        private static CharacterActionSpatialSummary Summary(CharacterActionProberStatus status, CharacterActionSpatialInterval[] intervals,
            int? optimal, bool upperOpen = false) => new CharacterActionSpatialSummary(new CharacterActionId(301), "action", "dummy",
                Measurement(), status, intervals, optimal, false, upperOpen, default);
    }
}
