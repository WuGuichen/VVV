using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.PresentationLod
{
    public sealed class PresentationPoseLodTests
    {
        [Test]
        public void ReductionRequiresStableObservation_AndPromotionEvaluatesImmediately()
        {
            var policy = new PresentationCadenceLodPolicy { ReductionDelay = 0.5, LowInterval = 0.1 };
            policy.Observe(0, 1, true, 0.01f, false); Assert.AreEqual(0, policy.Tier);
            policy.Observe(0.49, 1, true, 0.01f, false); Assert.AreEqual(0, policy.Tier);
            policy.Observe(0.5, 1, true, 0.01f, false); Assert.AreEqual(2, policy.Tier);
            Assert.True(policy.Evaluate(0.5)); Assert.False(policy.Evaluate(0.51));
            policy.Observe(0.52, 2, true, 0.5f, false);
            Assert.AreEqual(0, policy.Tier); Assert.True(policy.Evaluate(0.52));
        }

        [Test]
        public void UnknownShadowImportanceAndDiscontinuityRestoreFullCadence()
        {
            var policy = new PresentationCadenceLodPolicy { ReductionDelay = 0 };
            policy.Observe(0, 1, true, 0.01f, false); Assert.AreEqual(2, policy.Tier);
            policy.Observe(0.1, 1, false, 0, false); Assert.AreEqual(0, policy.Tier);
            policy.Observe(0.2, 1, true, 0.01f, false); Assert.AreEqual(2, policy.Tier);
            policy.Observe(0.21, 1, true, 0.01f, true); Assert.AreEqual(0, policy.Tier);
            policy.Observe(0.3, 1, true, 0.01f, false); policy.Invalidate();
            Assert.AreEqual(0, policy.Tier); Assert.True(policy.Evaluate(0.31));
            Assert.True(policy.NeedsObservation(0.31, 1));
        }

        [Test]
        public void CameraRevisionAndThresholdHysteresisDoNotLoseDemand()
        {
            var policy = new PresentationCadenceLodPolicy { ReductionDelay = 0 };
            policy.Observe(0, 1, true, 0.08f, false); Assert.AreEqual(1, policy.Tier);
            Assert.False(policy.NeedsObservation(0.01, 1)); Assert.True(policy.NeedsObservation(0.01, 2));
            policy.Observe(0.01, 2, true, 0.13f, false); Assert.AreEqual(1, policy.Tier);
            policy.Observe(0.02, 2, true, 0.15f, false); Assert.AreEqual(0, policy.Tier);
            policy.Observe(0.03, 2, true, float.PositiveInfinity, false); Assert.AreEqual(0, policy.Tier);
        }
    }
}
