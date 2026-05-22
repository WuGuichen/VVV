using System.Linq;
using MxFramework.Animation;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationLocomotionCalibrationTests
    {
        [Test]
        public void FootContactWindow_EvaluatesWrappedNormalizedRanges()
        {
            var window = new MxAnimationFootContactWindow(0.8f, 0.2f, 0.75f);

            Assert.IsTrue(window.Contains(0.9f));
            Assert.IsTrue(window.Contains(0.1f));
            Assert.IsTrue(window.Contains(1.1f));
            Assert.IsFalse(window.Contains(0.5f));
            Assert.AreEqual(0.75f, window.Confidence, 0.0001f);
        }

        [Test]
        public void ClipCalibration_ReturnsFootContactConfidence()
        {
            var calibration = new MxAnimationLocomotionClipCalibration(
                "walk_f",
                ClipKey("demo.animation.walk_f"),
                0f,
                1.4f,
                leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.35f, 0.8f) },
                rightFootContacts: new[] { new MxAnimationFootContactWindow(0.55f, 0.8f, 0.9f) });

            Assert.AreEqual(0.8f, calibration.GetContactConfidence(MxAnimationLocomotionFoot.Left, 0.2f), 0.0001f);
            Assert.AreEqual(0f, calibration.GetContactConfidence(MxAnimationLocomotionFoot.Left, 0.7f), 0.0001f);
            Assert.AreEqual(0.9f, calibration.GetContactConfidence(MxAnimationLocomotionFoot.Right, 0.7f), 0.0001f);
        }

        [Test]
        public void SetDefinitionHash_ChangesWhenLocomotionCalibrationChanges()
        {
            ResourceKey walk = ClipKey("demo.animation.walk_f");
            var first = new MxAnimationSetDefinition(
                "set.base",
                1,
                walk,
                walk,
                locomotionClipCalibrations: new[]
                {
                    new MxAnimationLocomotionClipCalibration(
                        "walk_f",
                        walk,
                        0f,
                        1.2f,
                        cycleDurationSeconds: 0.9f,
                        leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.3f) },
                        rightFootContacts: new[] { new MxAnimationFootContactWindow(0.6f, 0.8f) })
                });
            var changed = new MxAnimationSetDefinition(
                "set.base",
                1,
                walk,
                walk,
                locomotionClipCalibrations: new[]
                {
                    new MxAnimationLocomotionClipCalibration(
                        "walk_f",
                        walk,
                        0f,
                        1.8f,
                        cycleDurationSeconds: 0.9f,
                        leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.3f) },
                        rightFootContacts: new[] { new MxAnimationFootContactWindow(0.6f, 0.8f) })
                });

            Assert.AreNotEqual(first.DefinitionHash, changed.DefinitionHash);
        }

        [Test]
        public void BlendNativeVelocity_CombinesWeightedClipVelocitiesAndPlaybackSpeed()
        {
            ResourceKey walk = ClipKey("demo.animation.walk_f");
            ResourceKey strafe = ClipKey("demo.animation.walk_r");
            var weights = new[]
            {
                new MxAnimationBlend2DWeight(walk, 0, 1000, 0.5f, 1.2f, true),
                new MxAnimationBlend2DWeight(strafe, 1000, 0, 0.5f, 0.5f, true)
            };
            var calibrations = new[]
            {
                new MxAnimationLocomotionClipCalibration("walk_f", walk, 0f, 1.5f),
                new MxAnimationLocomotionClipCalibration("walk_r", strafe, 2f, 0f)
            };

            MxAnimationVelocity2D velocity =
                MxAnimationLocomotionCalibrationCalculator.BlendNativeVelocity(weights, calibrations);

            Assert.AreEqual(0.5f, velocity.X, 0.0001f);
            Assert.AreEqual(0.9f, velocity.Y, 0.0001f);
            Assert.AreEqual(0.5f, MxAnimationLocomotionCalibrationCalculator.CalculateVelocityErrorRatio(
                new MxAnimationVelocity2D(2f, 0f),
                new MxAnimationVelocity2D(1f, 0f)), 0.0001f);
        }

        [Test]
        public void SlipClassifier_SeparatesOkWarningAndBad()
        {
            MxAnimationFootSlipThresholds thresholds = MxAnimationFootSlipThresholds.Default;

            Assert.AreEqual(
                MxAnimationFootSlipGrade.Ok,
                MxAnimationLocomotionCalibrationCalculator.ClassifySlip(2.9f, 2.5f, thresholds));
            Assert.AreEqual(
                MxAnimationFootSlipGrade.Warning,
                MxAnimationLocomotionCalibrationCalculator.ClassifySlip(6f, 4f, thresholds));
            Assert.AreEqual(
                MxAnimationFootSlipGrade.Bad,
                MxAnimationLocomotionCalibrationCalculator.ClassifySlip(9f, 4f, thresholds));
        }

        [Test]
        public void WeightedFootContactConfidence_UsesClipNormalizedPlaybackTimes()
        {
            ResourceKey walk = ClipKey("demo.animation.walk_f");
            ResourceKey run = ClipKey("demo.animation.run_f");
            var weights = new[]
            {
                new MxAnimationBlend2DWeight(walk, 0, 1000, 0.25f, 1f, true),
                new MxAnimationBlend2DWeight(run, 0, 2000, 0.75f, 1f, true)
            };
            var playbacks = new[]
            {
                new MxAnimationClipPlaybackDiagnostic(walk, 0.25f, 0.2f, 1f, true, true, false),
                new MxAnimationClipPlaybackDiagnostic(run, 0.75f, 0.7f, 1f, true, true, false)
            };
            var calibrations = new[]
            {
                new MxAnimationLocomotionClipCalibration(
                    "walk_f",
                    walk,
                    0f,
                    1f,
                    leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.3f, 0.8f) },
                    rightFootContacts: new[] { new MxAnimationFootContactWindow(0.6f, 0.8f, 0.5f) }),
                new MxAnimationLocomotionClipCalibration(
                    "run_f",
                    run,
                    0f,
                    2f,
                    leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.3f, 0.6f) },
                    rightFootContacts: new[] { new MxAnimationFootContactWindow(0.6f, 0.8f, 1f) })
            };

            float left = MxAnimationLocomotionCalibrationCalculator.CalculateWeightedFootContactConfidence(
                MxAnimationLocomotionFoot.Left,
                weights,
                playbacks,
                calibrations);
            float right = MxAnimationLocomotionCalibrationCalculator.CalculateWeightedFootContactConfidence(
                MxAnimationLocomotionFoot.Right,
                weights,
                playbacks,
                calibrations);

            Assert.AreEqual(0.2f, left, 0.0001f);
            Assert.AreEqual(0.75f, right, 0.0001f);
        }

        [Test]
        public void SlipCalculator_ReportsHorizontalSlipSpeedAndDistanceInCentimeters()
        {
            float slip = MxAnimationLocomotionCalibrationCalculator.CalculateSlipCmPerSecond(
                previousX: 1f,
                previousY: 2f,
                currentX: 1.03f,
                currentY: 2.04f,
                deltaTime: 0.1f);
            float distance = MxAnimationLocomotionCalibrationCalculator.CalculateSlipDistanceCm(
                anchorX: 1f,
                anchorY: 2f,
                currentX: 1.03f,
                currentY: 2.04f);

            Assert.AreEqual(50f, slip, 0.001f);
            Assert.AreEqual(5f, distance, 0.001f);
        }

        [Test]
        public void ReachabilityReport_FlagsBlendPointsOutsideControllerDomain()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceKey walk = ClipKey("demo.animation.walk_f");
            ResourceKey run = ClipKey("demo.animation.run_f");
            var blend = new MxAnimationBlend2DDefinition(
                "blend.move2d",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(0, 1000, walk),
                    new MxAnimationBlend2DPoint(0, 2000, run)
                });

            MxAnimationBlendReachabilityReport report = MxAnimationBlendReachabilityAnalyzer.Analyze(
                blend,
                new MxAnimationBlend2DControllerDomain(-1000, 1000, -1000, 1000));

            Assert.IsTrue(report.HasUnreachablePoints);
            Assert.AreEqual(2, report.ReachablePoints.Count);
            Assert.AreEqual(1, report.UnreachablePoints.Count);
            Assert.AreEqual(run, report.UnreachablePoints[0].ClipKey);
            Assert.That(report.Issues.Select(issue => issue.Code), Contains.Item(
                MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint));
        }

        [Test]
        public void ReportFormatter_IncludesReachabilityAndDraftChanges()
        {
            ResourceKey run = ClipKey("demo.animation.run_f");
            var report = new MxAnimationBlendReachabilityReport(
                "blend.move2d",
                new MxAnimationBlend2DControllerDomain(-1000, 1000, -1000, 1000),
                null,
                new[] { new MxAnimationBlendReachabilityPoint(run, 0, 2000) },
                new[]
                {
                    new MxAnimationBlendReachabilityIssue(
                        MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint,
                        run,
                        0,
                        2000,
                        "outside")
                });
            var draft = new MxAnimationLocomotionCalibrationDraft(
                "iron_vanguard",
                "set.base",
                "blend.move2d",
                new[]
                {
                    new MxAnimationLocomotionCalibrationChange(
                        "clip",
                        "walk_f",
                        "playbackSpeed",
                        "1",
                        "1.18",
                        "velocityErrorRatio=0.06")
                });

            string summary = MxAnimationLocomotionCalibrationReportFormatter.CreateSummary(report, draft);

            Assert.That(summary, Does.Contain(MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint));
            Assert.That(summary, Does.Contain("draftChanges=1"));
            Assert.That(summary, Does.Contain("playbackSpeed 1 -> 1.18"));
        }

        [Test]
        public void BlendProbeSnapshot_ReportsDominantClipAndReachability()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceKey run = ClipKey("demo.animation.run_f");
            var report = new MxAnimationBlendReachabilityReport(
                "blend.move2d",
                new MxAnimationBlend2DControllerDomain(-1000, 1000, -1000, 1000),
                new[] { new MxAnimationBlendReachabilityPoint(idle, 0, 0) },
                new[] { new MxAnimationBlendReachabilityPoint(run, 0, 2000) },
                new[]
                {
                    new MxAnimationBlendReachabilityIssue(
                        MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint,
                        run,
                        0,
                        2000,
                        "outside")
                });

            var probe = new MxAnimationLocomotionBlendProbeSnapshot(
                "blend.move2d",
                report.Domain,
                0,
                500,
                report,
                new[]
                {
                    new MxAnimationBlend2DWeight(idle, 0, 0, 0.25f, 1f, true),
                    new MxAnimationBlend2DWeight(run, 0, 2000, 0.75f, 1f, true)
                },
                weightsFromBackend: true);

            Assert.AreEqual("blend.move2d", probe.BlendId);
            Assert.AreEqual(0, probe.SampleX);
            Assert.AreEqual(500, probe.SampleY);
            Assert.IsTrue(probe.WeightsFromBackend);
            Assert.IsTrue(probe.HasDominantClip);
            Assert.AreEqual(run, probe.DominantClipKey);
            Assert.AreEqual(0.75f, probe.DominantWeight, 0.0001f);
            Assert.IsTrue(probe.ReachabilityReport.HasUnreachablePoints);
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }
    }
}
