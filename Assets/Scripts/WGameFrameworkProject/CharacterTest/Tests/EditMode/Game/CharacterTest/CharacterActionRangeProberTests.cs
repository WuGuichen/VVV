using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.IO;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionRangeProberTests
    {
        [Test]
        public void Real301ScanMatchesIndependentFormalQueriesAndRechecksMidpoint()
        {
            var measurement = Measurement(0, 5000, 50);
            var result = CharacterActionRangeProber.Measure(new CharacterActionId(301), measurement);
            Assert.IsNotNull(result.Summary, result.Issue);
            Assert.AreEqual(CharacterActionProberStatus.Valid, result.Status, result.Issue);
            Assert.AreEqual(101, result.Samples.Count);
            Assert.IsTrue(result.Summary.IsContinuousAtResolution);
            Assert.IsTrue(result.MidpointEvidence.HasValue);
            Assert.IsTrue(result.MidpointEvidence.Value.Hit);
            Assert.AreEqual(result.MidpointEvidence.Value.DistanceMillimeters, result.Summary.VerifiedOptimalMillimeters);
            using (var independent = new CharacterActionReferenceProbeSession(new CharacterActionId(301), measurement.ReferenceDummy))
            {
                foreach (var sample in result.Samples)
                {
                    Assert.IsTrue(independent.TryEvaluate(sample.DistanceMillimeters, out var formal, out string issue), issue);
                    Assert.AreEqual(formal.CanHitFromCurrentPose, sample.Hit, "distance=" + sample.DistanceMillimeters);
                    if (!sample.Hit)
                        foreach (var interval in result.Summary.Intervals)
                            Assert.IsFalse(sample.DistanceMillimeters >= interval.FirstHitMillimeters
                                && sample.DistanceMillimeters <= interval.LastHitMillimeters,
                                "A missed sample must never be bridged into a reported hit run.");
                }
                var repeated = CharacterActionRangeProber.MeasureSession(independent, measurement);
                Assert.IsNotNull(repeated.Summary, repeated.Issue);
                Assert.AreEqual(result.Summary.ActionContentHash, repeated.Summary.ActionContentHash);
                Assert.AreEqual(result.Summary.MinDistance, repeated.Summary.MinDistance);
                Assert.AreEqual(result.Summary.MaxDistance, repeated.Summary.MaxDistance);
                Assert.AreEqual(result.Summary.OptimalDistance, repeated.Summary.OptimalDistance);
            }
            TestContext.WriteLine("301 nominal meters: min=" + result.Summary.MinDistance + ", max=" + result.Summary.MaxDistance
                + ", optimal=" + result.Summary.OptimalDistance + ", hash=" + result.Summary.ActionContentHash);
        }

        [Test]
        public void FormalDisjointHitVolumesPreserveGapAndRejectUnverifiedMidpoint()
        {
            var measurement = Measurement(0, 3500, 50, 100);
            using (var session = CreateConfiguredSession(measurement, 0.9f, jump: true))
            {
                var result = CharacterActionRangeProber.MeasureSession(session, measurement);
                Assert.IsNotNull(result.Summary, result.Issue);
                Assert.AreEqual(CharacterActionProberStatus.Discontinuous, result.Status);
                Assert.AreEqual(2, result.Summary.Intervals.Count);
                Assert.IsFalse(result.Summary.IsContinuousAtResolution);
                Assert.IsTrue(result.MidpointEvidence.HasValue);
                Assert.IsFalse(result.MidpointEvidence.Value.Hit);
                Assert.IsTrue(session.TryEvaluate(result.Summary.VerifiedOptimalMillimeters.Value, out var verified, out string issue), issue);
                Assert.IsTrue(verified.CanHitFromCurrentPose);
                StringAssert.Contains("MidpointMissed", result.Issue);
            }
        }

        [Test]
        public void FormalNoHitIncludesNonAlignedLastSampleAndHasNoDefaultDistance()
        {
            var measurement = Measurement(0, 1001, 50);
            using (var session = CreateConfiguredSession(measurement, 10f, jump: false))
            {
                var result = CharacterActionRangeProber.MeasureSession(session, measurement);
                Assert.IsNotNull(result.Summary, result.Issue);
                Assert.AreEqual(CharacterActionProberStatus.NoHit, result.Status);
                Assert.AreEqual(1001, result.Samples[result.Samples.Count - 1].DistanceMillimeters);
                Assert.IsNull(result.Summary.OptimalDistance);
                Assert.IsNull(result.Summary.MaxDistance);
            }
        }

        [Test]
        public void FormalHitAtSearchLimitLeavesMaximumOpen()
        {
            var measurement = Measurement(0, 500, 50, 100);
            using (var session = CreateConfiguredSession(measurement, 0.9f, jump: false))
            {
                var result = CharacterActionRangeProber.MeasureSession(session, measurement);
                Assert.IsNotNull(result.Summary, result.Issue);
                Assert.AreEqual(CharacterActionProberStatus.OpenBounded, result.Status);
                Assert.IsTrue(result.Summary.IsUpperBoundOpen);
                Assert.IsNull(result.Summary.MaxDistance);
                Assert.IsFalse(result.MidpointEvidence.HasValue);
                Assert.IsTrue(session.TryEvaluate(result.Summary.VerifiedOptimalMillimeters.Value, out var verified, out string issue), issue);
                Assert.IsTrue(verified.CanHitFromCurrentPose);
            }
        }

        [Test]
        public void UnsupportedVersionAndExcessiveGridNeverProduceSummaries()
        {
            var measurement = Measurement(0, 5000, 1);
            var result = CharacterActionRangeProber.Measure(new CharacterActionId(301), measurement);
            Assert.AreEqual(CharacterActionProberStatus.Unsupported, result.Status);
            Assert.IsNull(result.Summary);
            result = CharacterActionRangeProber.Measure(new CharacterActionId(301),
                new CharacterActionSpatialMeasurement("not-this-algorithm", 50, 0, 5000, measurement.ReferenceDummy));
            Assert.AreEqual(CharacterActionProberStatus.Unsupported, result.Status);
            Assert.IsNull(result.Summary);
        }

        private static CharacterActionSpatialMeasurement Measurement(int min, int max, int step, int radius = 300) =>
            new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, step, min, max,
                new CharacterActionReferenceDummy(1, 1800, radius, 0, 900, 0));

        private static CharacterActionReferenceProbeSession CreateConfiguredSession(
            CharacterActionSpatialMeasurement measurement,
            float height,
            bool jump)
        {
            // The formal resolver merges separate Profiles, ActionSets and Libraries.
            // A library directory is no longer a complete import bundle.
            var resolve = typeof(CharacterTestCharacterActionConfigSource).GetMethod("ResolvePayload",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(resolve);
            object payload = resolve.Invoke(null, new object[] { CharacterTestCharacterActionConfigSourceOptions.CreateDefault() });
            string Text(string name) => (string)payload.GetType().GetProperty(name).GetValue(payload);
            string actionSetsPath = Text("ActionSetsSourcePath"), actionsPath = Text("ActionsSourcePath");
            string reactionProfilesPath = Text("ReactionProfilesSourcePath"), landingProfilesPath = Text("LandingProfilesSourcePath");
            var actions = JObject.Parse(Text("ActionsJson"));
            foreach (JObject action in (JArray)actions["actions"])
            {
                if ((int)action["actionId"] != 301)
                    continue;
                JObject combatEvent = (JObject)((JArray)action["combatTrack"]["events"])[0];
                combatEvent["hitVolume"] = new JObject
                {
                    ["shapeKind"] = "Capsule",
                    ["radius"] = 0.05f,
                    ["localPointA"] = new JObject { ["x"] = 0, ["y"] = height, ["z"] = jump ? 0.5f : 1f },
                    ["localPointB"] = new JObject { ["x"] = 0, ["y"] = height, ["z"] = jump ? 0.5f : 1f }
                };
                if (jump)
                {
                    // Two separated volumes stay active over the same continuous frames.
                    // Keep the source still: movement sweep would legitimately hit the gap.
                    var later = (JObject)combatEvent.DeepClone();
                    later["stableEventId"] = 162; later["stableEventLabel"] = "range_fixture.second_window";
                    later["stableEventOrder"] = (int)combatEvent["stableEventOrder"] + 1;
                    later["hitEvent"]["stableEventId"] = 162;
                    later["hitEvent"]["stableEventLabel"] = "range_fixture.second_window";
                    later["hitVolume"]["localPointA"]["z"] = 2.5f;
                    later["hitVolume"]["localPointB"]["z"] = 2.5f;
                    ((JArray)action["combatTrack"]["events"]).Add(later);
                }
                foreach (JObject frame in (JArray)action["motionTrack"]["rootMotion"]["frames"])
                {
                    frame["x"] = 0f;
                    frame["y"] = 0f;
                    frame["z"] = 0f;
                }
                break;
            }

            CharacterActionJsonImportResult import = CharacterActionJsonImporter.Import(
                Text("ActionSetsJson"),
                actions.ToString(),
                Text("ReactionProfilesJson"),
                Text("LandingProfilesJson"),
                actionSetsPath,
                actionsPath,
                reactionProfilesPath,
                landingProfilesPath);
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                actionSetsPath,
                actionsPath,
                import.ReactionProfiles,
                reactionProfilesPath,
                import.LandingProfiles,
                landingProfilesPath);
            var validation = CharacterActionValidationReport.Validate(
                registry,
                new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Ignore));
            Assert.IsNotEmpty(registry.ActionSets);
            var runtime = new CharacterTestCharacterActionRuntime(
                import,
                registry,
                validation,
                null, // Current catalogs resolve the spawned entity's ActionSet, not a legacy global default.
                CharacterTestCharacterActionConfigSourceKind.ExternalJson,
                "range-prober-imported-fixture",
                actionSetsPath,
                actionsPath,
                Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>());
            var session = new CharacterActionReferenceProbeSession(
                new CharacterActionId(301),
                measurement.ReferenceDummy,
                actionRuntime: runtime);
            Assert.IsTrue(session.Runtime.ActionRuntime.TryGetEntityActionSet(session.Source.Id, out _),
                "The measured character must resolve its actual imported ActionSet.");
            return session;
        }
    }
}
