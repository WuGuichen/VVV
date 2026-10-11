using MxFramework.Combat.Physics;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGenesisCondensationTests
    {
        [Test]
        public void GenesisCondensation_ValidationSuiteUsesIssue631FixedBaseline()
        {
            CharacterTestGenesisCondensationValidation validation =
                CharacterTestGenesisCondensationHarness.RunValidationSuite(new RuntimeFrame(631));
            CharacterTestGenesisCondensationSample sample = validation.Sample;

            TestContext.WriteLine(validation.ToSummaryLine());

            Assert.IsTrue(validation.IsHarnessOnly);
            Assert.AreEqual(CharacterTestGenesisCondensationProbeKind.ValidationSuite, validation.Scenario);
            Assert.AreEqual(631, validation.Frame.Value);
            Assert.AreEqual("cool_slick_surface", sample.SurfaceSampleStableId);
            Assert.AreEqual(631101, sample.SurfaceSampleNumericId);
            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, sample.BaseMaterialKind);
            Assert.AreEqual("hard_slick", sample.BaseMaterialStableId);
            Assert.AreEqual("cool_mist_deposit", sample.InfluenceStableId);
            AssertTriplet(new CharacterTestGenesisCondensationTriplet(4, 18, 2), sample.Before);
            AssertTriplet(new CharacterTestGenesisCondensationTriplet(-2, 24, 0), sample.RawDelta);
            AssertTriplet(new CharacterTestGenesisCondensationTriplet(-2, 24, 0), sample.AppliedDelta);
            AssertTriplet(new CharacterTestGenesisCondensationTriplet(2, 42, 2), sample.After);
            Assert.AreEqual("P>=40&&D<=4", sample.LayerThreshold);
            Assert.IsTrue(sample.ThresholdMet);
            Assert.AreEqual(CharacterTestGenesisCondensationResultKind.CondensedLayer, sample.Result);
            Assert.AreEqual("condensation", sample.LayerSource);
        }

        [Test]
        public void GenesisCondensation_CondensedLayerChangesNextContactWithoutReplacingBaseMaterial()
        {
            CharacterTestGenesisCondensationValidation validation =
                CharacterTestGenesisCondensationHarness.RunValidationSuite(new RuntimeFrame(631));
            CharacterTestGenesisCondensationSample sample = validation.Sample;
            string summary = validation.ToSummaryLine();

            TestContext.WriteLine(summary);

            Assert.IsFalse(sample.MaterialReplacement);
            Assert.IsTrue(sample.SameContactInput);
            Assert.AreEqual(3f, sample.FrictionBefore, 0.0001f);
            Assert.AreEqual(42f, sample.LayerFriction, 0.0001f);
            Assert.AreEqual(42f, sample.FrictionAfter, 0.0001f);
            Assert.AreEqual(sample.ContactBefore.InitialSpeed, sample.ContactAfter.InitialSpeed, 0.0001f);
            Assert.AreEqual(sample.ContactBefore.TickCount, sample.ContactAfter.TickCount);
            Assert.AreEqual(sample.ContactBefore.TickRate, sample.ContactAfter.TickRate);
            Assert.Greater(sample.FrictionAfter, sample.FrictionBefore);
            Assert.Less(sample.ContactAfter.SlideDistance, sample.ContactBefore.SlideDistance);
            Assert.Greater(sample.ContactAfter.VelocityDecay, sample.ContactBefore.VelocityDecay);
            Assert.Greater(sample.SlideDistanceDelta, 0f);
            Assert.Greater(sample.VelocityDecayDelta, 0f);

            StringAssert.Contains("surfaceSample=cool_slick_surface", summary);
            StringAssert.Contains("influence=cool_mist_deposit", summary);
            StringAssert.Contains("beforeDPS=(4,18,2)", summary);
            StringAssert.Contains("rawDelta=(-2,24,0)", summary);
            StringAssert.Contains("appliedDelta=(-2,24,0)", summary);
            StringAssert.Contains("afterDPS=(2,42,2)", summary);
            StringAssert.Contains("layerThreshold=P>=40&&D<=4", summary);
            StringAssert.Contains("thresholdMet=true", summary);
            StringAssert.Contains("result=CondensedLayer", summary);
            StringAssert.Contains("layerSource=condensation", summary);
            StringAssert.Contains("materialReplacement=false", summary);
            StringAssert.Contains("sameContactInput=true", summary);
            StringAssert.Contains("frictionBefore=3", summary);
            StringAssert.Contains("frictionAfter=42", summary);
            StringAssert.Contains("slideDistanceDelta=", summary);
            StringAssert.Contains("velocityDecayDelta=", summary);
            StringAssert.Contains(
                "frictionReading=low-friction/longer-slide->higher-friction-layer/shorter-slide",
                summary);
        }

        private static void AssertTriplet(
            CharacterTestGenesisCondensationTriplet expected,
            CharacterTestGenesisCondensationTriplet actual)
        {
            Assert.AreEqual(expected.D, actual.D);
            Assert.AreEqual(expected.P, actual.P);
            Assert.AreEqual(expected.S, actual.S);
        }
    }
}
