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

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }
    }
}
