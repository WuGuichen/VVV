using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGenesisSurfaceMaterialTests
    {
        [Test]
        public void GenesisSurfaceMaterialSamples_ExposeMaterialContactAndKnockbackSummary()
        {
            CharacterTestGenesisSurfaceMaterialSample hardSlick =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisSurfaceMaterialSample rough =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.Rough);
            CharacterTestGenesisSurfaceMaterialSample elastic =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.Elastic);

            TestContext.WriteLine(hardSlick.ToSummaryLine());
            TestContext.WriteLine(rough.ToSummaryLine());
            TestContext.WriteLine(elastic.ToSummaryLine());

            AssertMaterialSummary(hardSlick, CombatSurfaceMaterial.HardSlick);
            AssertMaterialSummary(rough, CombatSurfaceMaterial.Rough);
            AssertMaterialSummary(elastic, CombatSurfaceMaterial.Elastic);
            Assert.AreEqual(CharacterTestGenesisSurfaceMaterialHarness.DefaultMassKg, rough.MassKg, 0.0001f);
            Assert.AreEqual(
                CharacterTestGenesisSurfaceMaterialHarness.DefaultKnockbackSpeed,
                rough.KnockbackSpeed,
                0.0001f);
            Assert.Greater(rough.ContactDisplacementMeters, 0f);
            Assert.Greater(rough.PeakContactSpeed, 0f);
            Assert.IsTrue(rough.ToSummaryLine().Contains("contactVelocity="));
            Assert.IsTrue(rough.ToSummaryLine().Contains("contactDisplacement="));
            Assert.IsTrue(rough.ToSummaryLine().Contains("massKg="));
            Assert.IsTrue(rough.ToSummaryLine().Contains("knockbackSpeed="));
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_HardSlickReportsLowTractionMaterial()
        {
            CharacterTestGenesisSurfaceMaterialSample hardSlick =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisSurfaceMaterialSample rough =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.Rough);

            Assert.AreEqual(ToFloat(CombatSurfaceMaterial.HardSlick.Friction), hardSlick.ResolvedSurfaceFriction, 0.0001f);
            Assert.AreEqual(60f, rough.ResolvedSurfaceFriction, 0.0001f);
            Assert.IsTrue(rough.ReboundGrounded);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_HardSlickAcceleratesSlowerThanRough()
        {
            CharacterTestGenesisSurfaceAccelerationSample hardSlick =
                CharacterTestGenesisSurfaceMaterialHarness.RunAccelerationSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisSurfaceAccelerationSample rough =
                CharacterTestGenesisSurfaceMaterialHarness.RunAccelerationSample(CombatSurfaceMaterial.Rough);

            TestContext.WriteLine(hardSlick.ToSummaryLine());
            TestContext.WriteLine(rough.ToSummaryLine());

            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, hardSlick.MaterialKind);
            Assert.AreEqual(ToFloat(CombatSurfaceMaterial.HardSlick.Friction), hardSlick.SurfaceFriction, 0.0001f);
            Assert.Less(hardSlick.Speed, rough.Speed - 1.5f);
            Assert.Less(hardSlick.Speed, 2f);
            Assert.Greater(rough.Speed, 3.5f);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_HardSlickKeepsMomentumDuringReverseTurn()
        {
            CharacterTestGenesisSurfaceTurnSample hardSlick =
                CharacterTestGenesisSurfaceMaterialHarness.RunTurnSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisSurfaceTurnSample rough =
                CharacterTestGenesisSurfaceMaterialHarness.RunTurnSample(CombatSurfaceMaterial.Rough);

            TestContext.WriteLine(hardSlick.ToSummaryLine());
            TestContext.WriteLine(rough.ToSummaryLine());

            Assert.Greater(hardSlick.VelocityZ, 1f, "Ice should keep sliding in the original forward direction.");
            Assert.Greater(rough.ReverseSpeed, 3.5f, "High-traction ground should reverse within the same time window.");
            Assert.Greater(rough.ReverseSpeed, hardSlick.ReverseSpeed + 3f);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_ElasticGroundReboundsAfterFallingImpact()
        {
            CharacterTestGenesisSurfaceMaterialSample hardSlick =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisSurfaceMaterialSample elastic =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.Elastic);

            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, elastic.MaterialKind);
            Assert.AreEqual("elastic", elastic.MaterialStableId);
            Assert.AreEqual(1f, elastic.SurfaceElasticity, 0.0001f);
            Assert.Greater(elastic.ReboundVelocityY, 1f);
            Assert.Greater(elastic.ReboundVelocityY, hardSlick.ReboundVelocityY + 1f);
            Assert.LessOrEqual(elastic.ReboundVelocityY, 20.01f);
            Assert.IsFalse(elastic.ReboundGrounded);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_BouncePadUsesFixedBounceHeight()
        {
            CharacterTestGenesisSurfaceMaterialSample bouncePad =
                CharacterTestGenesisSurfaceMaterialHarness.RunSample(CombatSurfaceMaterial.BouncePad);

            float expectedVelocity = (float)Math.Sqrt(2f * 30f * ToFloat(CombatSurfaceMaterial.BouncePad.FixedBounceHeight));
            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, bouncePad.MaterialKind);
            Assert.AreEqual("bounce_pad", bouncePad.MaterialStableId);
            Assert.AreEqual(0f, bouncePad.SurfaceElasticity, 0.0001f);
            Assert.AreEqual(expectedVelocity, bouncePad.ReboundVelocityY, 0.05f);
            Assert.IsFalse(bouncePad.ReboundGrounded);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_ElasticBounceSequenceCapsEffectiveImpactSpeed()
        {
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> frames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.Elastic,
                    bounceCount: 3,
                    maxTicks: 600,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate);

            Assert.AreEqual(3, frames.Count);
            for (int i = 0; i < frames.Count; i++)
                TestContext.WriteLine(frames[i].ToSummaryLine());

            Assert.AreEqual(20f, frames[0].EffectiveImpactSpeed, 0.05f);
            Assert.AreEqual(frames[0].EffectiveImpactSpeed, frames[0].ReboundSpeed, 0.05f);
            for (int i = 1; i < frames.Count; i++)
            {
                Assert.Greater(
                    frames[i].RawImpactSpeed,
                    frames[i - 1].ReboundSpeed + 0.05f,
                    "CharacterTest fall gravity can make raw impact speed larger than the previous rebound.");
                Assert.AreEqual(frames[i - 1].ReboundSpeed, frames[i].EffectiveImpactSpeed, 0.05f);
                Assert.AreEqual(frames[i].EffectiveImpactSpeed, frames[i].ReboundSpeed, 0.05f);
                Assert.LessOrEqual(frames[i].ReboundSpeed, frames[i - 1].ReboundSpeed + 0.05f);
            }
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_SurfaceBounceIgnoresJumpReleaseShortHopCut()
        {
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> baselineElasticFrames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.Elastic,
                    bounceCount: 2,
                    maxTicks: 360,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate);
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> elasticFrames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.Elastic,
                    bounceCount: 2,
                    maxTicks: 360,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    jumpImpulseOnBounceIndex: -1,
                    jumpReleaseAfterBounceIndex: 0);
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> baselineBouncePadFrames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.BouncePad,
                    bounceCount: 2,
                    maxTicks: 360,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate);
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> bouncePadFrames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.BouncePad,
                    bounceCount: 2,
                    maxTicks: 360,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    jumpImpulseOnBounceIndex: -1,
                    jumpReleaseAfterBounceIndex: 0);

            Assert.AreEqual(2, elasticFrames.Count);
            Assert.AreEqual(baselineElasticFrames[1].PeakY, elasticFrames[1].PeakY, 0.05f);
            Assert.AreEqual(elasticFrames[0].ReboundSpeed, elasticFrames[1].ReboundSpeed, 0.05f);

            float expectedBouncePadVelocity = (float)Math.Sqrt(2f * 30f * ToFloat(CombatSurfaceMaterial.BouncePad.FixedBounceHeight));
            Assert.AreEqual(2, bouncePadFrames.Count);
            Assert.AreEqual(baselineBouncePadFrames[1].PeakY, bouncePadFrames[1].PeakY, 0.05f);
            Assert.AreEqual(expectedBouncePadVelocity, bouncePadFrames[0].ReboundSpeed, 0.05f);
            Assert.AreEqual(expectedBouncePadVelocity, bouncePadFrames[1].ReboundSpeed, 0.05f);
        }

        [Test]
        public void GenesisSurfaceMaterialSamples_ElasticBounceIgnoresContactFrameJumpImpulse()
        {
            IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> frames =
                CharacterTestGenesisSurfaceMaterialHarness.RunBounceSequence(
                    CombatSurfaceMaterial.Elastic,
                    bounceCount: 3,
                    maxTicks: 600,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    jumpImpulseOnBounceIndex: 1);

            Assert.AreEqual(3, frames.Count);
            Assert.AreEqual(20f, frames[1].EffectiveImpactSpeed, 0.05f);
            Assert.AreEqual(20f, frames[1].ReboundSpeed, 0.05f);
            Assert.AreEqual(20f, frames[2].ReboundSpeed, 0.05f);
        }

        private static void AssertMaterialSummary(
            CharacterTestGenesisSurfaceMaterialSample sample,
            CombatSurfaceMaterial material)
        {
            Assert.AreEqual(material.Kind, sample.MaterialKind);
            Assert.AreEqual(material.StableId, sample.MaterialStableId);
            Assert.AreEqual(ToFloat(material.Friction), sample.SurfaceFriction, 0.0001f);
            Assert.AreEqual(ToFloat(material.Elasticity), sample.SurfaceElasticity, 0.0001f);
            Assert.AreEqual(
                CharacterTestGenesisSurfaceMaterialHarness.DefaultSlideTicks,
                sample.TickCount);
            Assert.AreEqual(
                CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                sample.TickRate);
        }

        private static float ToFloat(MxFramework.Core.Math.Fix64 value)
        {
            return (float)((double)value.RawValue / MxFramework.Core.Math.Fix64.Scale);
        }
    }

    public sealed class CharacterTestGenesisMovableNaturalObjectTests
    {
        [Test]
        public void GenesisMovableNaturalObjects_ValidationSuiteCoversPushPressureReboundAndObjectContact()
        {
            CharacterTestGenesisMovableNaturalObjectValidation validation =
                CharacterTestGenesisMovableNaturalObjectHarness.RunValidationSuite();

            TestContext.WriteLine(validation.ToSummaryLine());

            Assert.IsTrue(validation.IsHarnessOnly);
            Assert.AreEqual(
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                validation.PushHardSlick.ForceOrigin);
            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, validation.PushHardSlick.MaterialKind);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, validation.PushRough.MaterialKind);
            Assert.Greater(
                validation.PushHardSlick.DisplacementMeters,
                validation.PushRough.DisplacementMeters + 0.5f);
            Assert.Greater(
                Math.Abs(validation.PushHardSlick.VelocityX),
                Math.Abs(validation.PushRough.VelocityX) + 1f);
            Assert.AreEqual("harness-only", validation.PushHardSlick.StateModel);
            Assert.Greater(validation.PushHardSlick.PhysicsQueryHitCount, 0);
            Assert.Greater(validation.PushRough.PhysicsQueryHitCount, 0);
            Assert.AreEqual(2, validation.PushHardSlick.PhysicsBodyCount);
            Assert.AreEqual(2, validation.PushHardSlick.PhysicsColliderCount);

            Assert.Greater(validation.PressureLight.DisplacementX, validation.PressureHeavy.DisplacementX + 0.005f);
            Assert.Greater(validation.PressureLight.ContactPressure, 0f);
            Assert.Greater(
                validation.PressureLight.ContactDisplacementMeters,
                validation.PressureHeavy.ContactDisplacementMeters);
            Assert.AreEqual(
                CharacterTestGenesisForceOriginKind.ContinuousExternalForce,
                validation.PressureLight.ForceOrigin);
            Assert.Greater(validation.PressureLight.PhysicsQueryHitCount, 0);
            Assert.AreEqual(2, validation.PressureLight.PhysicsBodyCount);
            Assert.AreEqual(2, validation.PressureLight.PhysicsColliderCount);

            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, validation.ReboundElastic.MaterialKind);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, validation.ReboundRough.MaterialKind);
            Assert.Greater(validation.ReboundElastic.VelocityX, validation.ReboundRough.VelocityX + 2f);
            Assert.Greater(validation.ReboundElastic.ContactPressure, 0f);
            Assert.AreEqual("rebound-returned-force", validation.ReboundElastic.BehaviorState);
            Assert.Greater(validation.ReboundElastic.PhysicsQueryHitCount, 0);
            Assert.Greater(validation.ReboundRough.PhysicsQueryHitCount, 0);

            Assert.AreEqual(
                "movable.natural.contact.a->movable.natural.contact.b",
                validation.ContactB.ContactPair);
            Assert.Greater(validation.ContactB.ContactSpeed, 0.5f);
            Assert.Greater(validation.ContactB.DisplacementMeters, 0.1f);
            Assert.AreEqual("object-contact-received", validation.ContactB.BehaviorState);
            Assert.AreEqual(2, validation.ContactB.PhysicsBodyCount);
            Assert.AreEqual(2, validation.ContactB.PhysicsColliderCount);
            Assert.Greater(validation.ContactB.PhysicsQueryHitCount, 0);

            string summary = validation.ToSummaryLine();
            StringAssert.Contains("objectStableId=movable.natural.contact.b", summary);
            StringAssert.Contains("category=movable-natural-object", summary);
            StringAssert.Contains("massKg=", summary);
            StringAssert.Contains("material=Elastic/elastic", summary);
            StringAssert.Contains("contactPair=movable.natural.contact.a->movable.natural.contact.b", summary);
            StringAssert.Contains("stateModel=harness-only", summary);
            StringAssert.Contains("aabbQueryHits=", summary);
        }

        [Test]
        public void GenesisMovableNaturalObjects_CommandBoundaryRunsHarnessOnlyAdapter()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false))
            {
                RuntimeFrame frame = slice.CurrentFrame;
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                long hashBefore = participant.ComputeStateHash(frame);

                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbe(
                        frame,
                        CharacterTestRuntimeCommandSources.GenesisProbe,
                        CharacterTestGenesisMovableObjectProbeKind.ValidationSuite,
                        CharacterTestGenesisForceOriginKind.InstantImpulse,
                        traceId: new RuntimeTraceIdentity(993973)));

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / CharacterTestGenesisMovableNaturalObjectHarness.DefaultTickRate);

                CharacterTestGenesisMovableNaturalObjectValidation validation =
                    slice.LastGenesisMovableObjectValidation;
                long hashAfter = participant.ComputeStateHash(frame);

                Assert.IsTrue(validation.IsHarnessOnly);
                Assert.AreEqual(hashBefore, hashAfter, "Harness-only object samples must stay out of state hash.");
                Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                    command => command.CommandId == CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe
                        && command.SourceId == CharacterTestRuntimeCommandSources.GenesisProbe));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.GenesisMovableObjectProbeApplied));
                StringAssert.Contains("stateModel=harness-only", slice.LastGenesisMovableObjectSummary);
                StringAssert.Contains("forceOrigin=InstantImpulse", slice.LastGenesisMovableObjectSummary);
                Assert.Greater(validation.ContactB.ContactSpeed, 0.5f);
            }
        }
    }

    public sealed class CharacterTestGenesisCombatPotencyReceptionTests
    {
        [Test]
        public void GenesisCombatPotencyReception_ValidationSuiteExposesLegacyAndSemanticSummary()
        {
            CharacterTestGenesisCombatPotencyReceptionValidation validation =
                CharacterTestGenesisCombatPotencyReceptionHarness.RunValidationSuite();

            string summary = validation.ToSummaryLine();
            TestContext.WriteLine(summary);

            Assert.IsTrue(validation.IsHarnessOnly);
            Assert.IsTrue(validation.HeavyHammer.IsHarnessOnly);
            Assert.IsTrue(validation.LightBlade.IsHarnessOnly);
            Assert.IsTrue(validation.ShieldCharge.IsHarnessOnly);
            Assert.IsTrue(validation.Flame.IsHarnessOnly);
            AssertReceptionEventCounts(validation.HeavyHammer);
            AssertReceptionEventCounts(validation.LightBlade);
            AssertReceptionEventCounts(validation.ShieldCharge);
            AssertReceptionEventCounts(validation.Flame);

            StringAssert.Contains("runtime.ScatterImpact=scatterInput", summary);
            StringAssert.Contains("runtime.ScatterPressure=scatterAccumulation", summary);
            StringAssert.Contains("runtime.CohesionLimit=cohesionLimit", summary);
            StringAssert.Contains("runtime.CohesionSupport=cohesionSupport", summary);
            StringAssert.Contains("runtime.CohesionSupportWindow=cohesionSupport", summary);
            StringAssert.Contains("runtime.PressureBandChangedEvent=scatterPhaseChange", summary);
            StringAssert.Contains("runtime.CohesionBreak=breakCohesion", summary);
            StringAssert.Contains("runtime.Recovery=recoverCohesion", summary);
            StringAssert.Contains("inputD=", summary);
            StringAssert.Contains("inputS=", summary);
            StringAssert.Contains("sourceMassKg=", summary);
            StringAssert.Contains("hitZoneScale=", summary);
            StringAssert.Contains("knockbackDistance=", summary);
        }

        [Test]
        public void GenesisCombatPotencyReception_HeavyHammerBreaksFasterThanLightBlade()
        {
            CharacterTestGenesisCombatPotencyReceptionValidation validation =
                CharacterTestGenesisCombatPotencyReceptionHarness.RunValidationSuite();
            CharacterTestGenesisCombatPotencyReceptionSample heavy = validation.HeavyHammer;
            CharacterTestGenesisCombatPotencyReceptionSample light = validation.LightBlade;

            TestContext.WriteLine(heavy.ToSummaryLine());
            TestContext.WriteLine(light.ToSummaryLine());

            Assert.AreEqual(CharacterTestGenesisCombatPotencyReceptionKind.HeavyHammer, heavy.Kind);
            Assert.AreEqual(CharacterTestGenesisCombatPotencyReceptionKind.LightBlade, light.Kind);
            Assert.Greater(heavy.ScatterImpact, light.ScatterImpact);
            Assert.Greater(heavy.PreviewKnockbackDistanceMeters, light.PreviewKnockbackDistanceMeters);
            Assert.Greater(heavy.KnockbackSpeed, light.KnockbackSpeed);
            Assert.IsTrue(heavy.TriggeredCohesionBreak);
            Assert.AreEqual(2, heavy.HitCountToBreak);
            Assert.AreEqual(PressureBand.Broken, heavy.BandAfterInputs);
            Assert.IsFalse(light.TriggeredCohesionBreak);
            Assert.AreEqual(0, light.HitCountToBreak);
            Assert.AreEqual(light.ScatterImpact, light.ScatterPressureAfterFirstHit);
            Assert.Less(light.ScatterPressureAfterFirstHit, light.CohesionLimit);
            Assert.Greater(light.ScatterPressureAfterInputs, light.ScatterPressureAfterFirstHit);
            Assert.GreaterOrEqual(light.CohesionBreakRiskAfterInputs, 0.65f);
            Assert.AreEqual(PressureBand.Cracked, light.BandAfterInputs);
        }

        [Test]
        public void GenesisCombatPotencyReception_ShieldChargeRaisesCohesionLimitForSameScatterInput()
        {
            CharacterTestGenesisCombatPotencyReceptionValidation validation =
                CharacterTestGenesisCombatPotencyReceptionHarness.RunValidationSuite();
            CharacterTestGenesisCombatPotencyReceptionSample heavy = validation.HeavyHammer;
            CharacterTestGenesisCombatPotencyReceptionSample shield = validation.ShieldCharge;

            TestContext.WriteLine(shield.ToSummaryLine());

            Assert.AreEqual(CharacterTestGenesisCombatPotencyReceptionKind.ShieldCharge, shield.Kind);
            Assert.AreEqual(heavy.ScatterImpact, shield.ScatterImpact);
            Assert.AreEqual(heavy.HitCount, shield.HitCount);
            Assert.Greater(shield.CohesionLimit, heavy.CohesionLimit);
            Assert.AreEqual(50, shield.CohesionLimitDelta);
            Assert.IsTrue(shield.CohesionSupportActive);
            Assert.IsTrue(shield.CohesionSupportWindowActive);
            Assert.AreEqual(1.5f, shield.CohesionSupportRate, 0.0001f);
            Assert.IsFalse(shield.TriggeredCohesionBreak);
            Assert.AreEqual(PressureBand.Critical, shield.BandAfterInputs);
            Assert.Less(shield.CohesionBreakRiskAfterInputs, heavy.CohesionBreakRiskAfterInputs);
            StringAssert.Contains("runtime.CohesionLimit=cohesionLimit:150", shield.ToSummaryLine());
        }

        [Test]
        public void GenesisCombatPotencyReception_FlameReportsContinuousScatterAndRecoveryHook()
        {
            CharacterTestGenesisCombatPotencyReceptionSample flame =
                CharacterTestGenesisCombatPotencyReceptionHarness.RunFlameSample();

            TestContext.WriteLine(flame.ToSummaryLine());

            Assert.AreEqual(CharacterTestGenesisCombatPotencyReceptionKind.Flame, flame.Kind);
            Assert.IsTrue(flame.IsContinuousInput);
            Assert.AreEqual(18, flame.InputD);
            Assert.AreEqual(22, flame.InputS);
            Assert.AreEqual(18, flame.ScatterImpact);
            Assert.AreEqual(4, flame.HitCount);
            Assert.AreEqual(72, flame.ScatterPressureAfterInputs);
            Assert.AreEqual(PressureBand.Cracked, flame.BandAfterInputs);
            Assert.IsFalse(flame.TriggeredCohesionBreak);
            Assert.Greater(flame.RecoveryEventCount, 0);
            Assert.Greater(flame.RecoveryAmount, 0);
            StringAssert.Contains("continuous=True", flame.ToSummaryLine());
            StringAssert.Contains("laterHook=material-transformation-burning", flame.ToSummaryLine());
        }

        private static void AssertReceptionEventCounts(
            CharacterTestGenesisCombatPotencyReceptionSample sample)
        {
            Assert.AreEqual(sample.HitCount, sample.ScatterImpactResolvedEventCount);
            Assert.Greater(sample.PressureChangedEventCount, 0);
            Assert.Greater(sample.PressureBandChangedEventCount, 0);
            Assert.Greater(sample.RecoveryEventCount, 0);
            Assert.GreaterOrEqual(sample.ScatterPressureAfterInputs, sample.ScatterPressureAfterFirstHit);
            Assert.GreaterOrEqual(sample.RecoveryAmount, 0);
        }
    }

    public sealed class CharacterTestGenesisLivingBeingTests
    {
        [Test]
        public void GenesisLivingBeing_ValidationSuiteCoversWholeBodyActionsStateLadderAndMaterialReturn()
        {
            CharacterTestGenesisLivingBeingValidation validation =
                CharacterTestGenesisLivingBeingHarness.RunValidationSuite();

            TestContext.WriteLine(validation.ToSummaryLine());

            Assert.IsTrue(validation.IsHarnessOnly);
            AssertLivingBeingSample(validation.Approach);
            AssertLivingBeingSample(validation.Avoid);
            AssertLivingBeingSample(validation.Brace);
            AssertLivingBeingSample(validation.Unbalanced);
            AssertLivingBeingSample(validation.CohesionBroken);
            AssertLivingBeingSample(validation.Restabilize);
            Assert.IsTrue(validation.Inactive.IsHarnessOnly);
            Assert.IsTrue(validation.Inactive.UsesWholeBody);
            Assert.IsTrue(validation.Inactive.SelfMaintainsBoundaryBefore);

            Assert.AreEqual(CharacterTestGenesisLivingBeingActionVerb.Approach, validation.Approach.ActionVerb);
            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Stable, validation.Approach.StateAfter);
            Assert.Less(validation.Approach.DistanceAfterMeters, validation.Approach.DistanceBeforeMeters);
            Assert.Greater(validation.Approach.BodyDisplacementMeters, 0.5f);
            Assert.IsTrue(validation.Approach.CanActAfter);

            Assert.AreEqual(CharacterTestGenesisLivingBeingActionVerb.Avoid, validation.Avoid.ActionVerb);
            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Stable, validation.Avoid.StateAfter);
            Assert.Greater(validation.Avoid.DistanceAfterMeters, validation.Avoid.DistanceBeforeMeters);
            Assert.Greater(validation.Avoid.ExternalS, 0);

            Assert.AreEqual(CharacterTestGenesisLivingBeingActionVerb.Brace, validation.Brace.ActionVerb);
            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Stable, validation.Brace.StateAfter);
            Assert.Greater(validation.Brace.CohesionAfter, validation.Brace.CohesionLimit);
            Assert.Greater(validation.Brace.ResistedDisplacementMeters, validation.Brace.BodyDisplacementMeters);
            Assert.IsTrue(validation.Brace.SelfMaintainsBoundaryAfter);

            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Unbalanced, validation.Unbalanced.StateAfter);
            Assert.IsTrue(validation.Unbalanced.UnbalancedThresholdMet);
            Assert.IsFalse(validation.Unbalanced.BreakThresholdMet);
            Assert.IsFalse(validation.Unbalanced.CanActAfter);

            Assert.AreEqual(CharacterTestGenesisLivingBeingState.CohesionBroken, validation.CohesionBroken.StateAfter);
            Assert.IsTrue(validation.CohesionBroken.BreakThresholdMet);
            Assert.IsFalse(validation.CohesionBroken.InactiveThresholdMet);
            Assert.IsFalse(validation.CohesionBroken.CanActAfter);

            Assert.AreEqual(CharacterTestGenesisLivingBeingActionVerb.Restabilize, validation.Restabilize.ActionVerb);
            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Stable, validation.Restabilize.StateAfter);
            Assert.IsTrue(validation.Restabilize.RecoveredToStable);
            Assert.Greater(validation.Restabilize.ScatterClearedByRecovery, 0);
            Assert.Greater(validation.Restabilize.RecoveryTicks, 0);
            Assert.IsTrue(validation.Restabilize.CanActAfter);

            Assert.AreEqual(CharacterTestGenesisLivingBeingState.Inactive, validation.Inactive.StateAfter);
            Assert.IsFalse(validation.Inactive.SelfMaintainsBoundaryAfter);
            Assert.IsFalse(validation.Inactive.CanActAfter);
            Assert.IsTrue(validation.Inactive.InactiveThresholdMet);
            Assert.IsTrue(validation.Inactive.InactiveRecoveryAttempted);
            Assert.IsFalse(validation.Inactive.InactiveRecoverySucceeded);
            Assert.AreEqual(
                CharacterTestGenesisLivingBeingMaterialReading.MaterialSystem,
                validation.Inactive.MaterialReadingAfter);

            string summary = validation.ToSummaryLine();
            StringAssert.Contains("stateModel=harness-only", summary);
            StringAssert.Contains("wholeBody=scale:", summary);
            StringAssert.Contains("verb=Approach", summary);
            StringAssert.Contains("verb=Avoid", summary);
            StringAssert.Contains("verb=Brace", summary);
            StringAssert.Contains("verb=Restabilize", summary);
            StringAssert.Contains("state=Stable->Unbalanced", summary);
            StringAssert.Contains("state=Stable->CohesionBroken", summary);
            StringAssert.Contains("state=Stable->Inactive", summary);
            StringAssert.Contains("materialReadingAfter=MaterialSystem", summary);
            StringAssert.Contains("laterHook=return-to-material-system", summary);
        }

        [Test]
        public void GenesisLivingBeing_CommandBoundaryRunsHarnessOnlyAdapter()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false))
            {
                RuntimeFrame frame = slice.CurrentFrame;
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                long hashBefore = participant.ComputeStateHash(frame);

                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbe(
                        frame,
                        CharacterTestRuntimeCommandSources.GenesisProbe,
                        CharacterTestGenesisLivingBeingProbeKind.ValidationSuite,
                        traceId: new RuntimeTraceIdentity(919263)));

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / CharacterTestGenesisLivingBeingHarness.DefaultTickRate);

                CharacterTestGenesisLivingBeingValidation validation =
                    slice.LastGenesisLivingBeingValidation;
                long hashAfter = participant.ComputeStateHash(frame);

                Assert.IsTrue(validation.IsHarnessOnly);
                Assert.AreEqual(hashBefore, hashAfter, "Harness-only living-being samples must stay out of state hash.");
                Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                    command => command.CommandId == CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe
                        && command.SourceId == CharacterTestRuntimeCommandSources.GenesisProbe));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.GenesisLivingBeingProbeApplied));
                StringAssert.Contains("stateModel=harness-only", slice.LastGenesisLivingBeingSummary);
                StringAssert.Contains("materialReadingAfter=MaterialSystem", slice.LastGenesisLivingBeingSummary);
                Assert.AreEqual(CharacterTestGenesisLivingBeingState.Stable, validation.Restabilize.StateAfter);
                Assert.AreEqual(CharacterTestGenesisLivingBeingState.Inactive, validation.Inactive.StateAfter);
            }
        }

        private static void AssertLivingBeingSample(CharacterTestGenesisLivingBeingSample sample)
        {
            Assert.IsTrue(sample.IsHarnessOnly);
            Assert.IsTrue(sample.UsesWholeBody);
            Assert.AreEqual(CharacterTestGenesisLivingBeingMaterialReading.LivingBody, sample.MaterialReadingAfter);
            Assert.IsTrue(sample.SelfMaintainsBoundaryBefore);
        }
    }

    public sealed class CharacterTestGenesisLivingNaturalAttributesTests
    {
        [Test]
        public void GenesisLivingNaturalAttributes_ValidationSuiteCoversEmbodySelfMaintainRecoverAndMaterialReturn()
        {
            CharacterTestGenesisLivingNaturalAttributesValidation validation =
                CharacterTestGenesisLivingNaturalAttributesHarness.RunValidationSuite();

            TestContext.WriteLine(validation.ToSummaryLine());

            Assert.IsTrue(validation.IsHarnessOnly);
            AssertLivingNaturalAttributeSample(validation.Embody);
            AssertLivingNaturalAttributeSample(validation.SelfMaintain);
            AssertLivingNaturalAttributeSample(validation.Recover);
            Assert.IsTrue(validation.InactiveMaterial.IsHarnessOnly);
            Assert.IsTrue(validation.InactiveMaterial.UsesWholeBody);

            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeJudgement.Embody,
                validation.Embody.Judgement);
            Assert.IsTrue(validation.Embody.CanEmbody);
            Assert.IsTrue(validation.Embody.BodyBoundaryAfterInput);
            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                validation.Embody.MaterialReadingAfter);

            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeJudgement.SelfMaintain,
                validation.SelfMaintain.Judgement);
            Assert.IsTrue(validation.SelfMaintain.CanSelfMaintain);
            Assert.Greater(validation.SelfMaintain.ExternalS, 0);
            Assert.GreaterOrEqual(validation.SelfMaintain.SelfCohesion, validation.SelfMaintain.ScatterAfterInput);
            Assert.IsTrue(validation.SelfMaintain.BodyBoundaryAfterInput);

            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeJudgement.Recover,
                validation.Recover.Judgement);
            Assert.IsTrue(validation.Recover.CanRecover);
            Assert.IsTrue(validation.Recover.RecoveredToStable);
            Assert.Greater(validation.Recover.RecoveryTicks, 0);
            Assert.Greater(validation.Recover.ScatterClearedByRecovery, 0);
            Assert.Less(validation.Recover.ScatterAfterRecovery, validation.Recover.ScatterAfterInput);

            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeJudgement.ReturnToMaterial,
                validation.InactiveMaterial.Judgement);
            Assert.IsTrue(validation.InactiveMaterial.ReturnedToMaterial);
            Assert.IsFalse(validation.InactiveMaterial.CanActAfter);
            Assert.IsFalse(validation.InactiveMaterial.BodyBoundaryAfterRecovery);
            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem,
                validation.InactiveMaterial.MaterialReadingAfter);

            string summary = validation.ToSummaryLine();
            StringAssert.Contains("stateModel=harness-only", summary);
            StringAssert.Contains("judgement=Embody", summary);
            StringAssert.Contains("judgement=SelfMaintain", summary);
            StringAssert.Contains("judgement=Recover", summary);
            StringAssert.Contains("judgement=ReturnToMaterial", summary);
            StringAssert.Contains("canEmbody=true", summary);
            StringAssert.Contains("canSelfMaintain=true", summary);
            StringAssert.Contains("canRecover=true", summary);
            StringAssert.Contains("materialReadingAfter=MaterialSystem", summary);
            StringAssert.Contains("returnedToMaterial=true", summary);
        }

        [Test]
        public void GenesisLivingNaturalAttributes_CommandBoundaryRunsHarnessOnlyAdapter()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false))
            {
                RuntimeFrame frame = slice.CurrentFrame;
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                long hashBefore = participant.ComputeStateHash(frame);

                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbe(
                        frame,
                        CharacterTestRuntimeCommandSources.GenesisProbe,
                        CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite,
                        traceId: new RuntimeTraceIdentity(941652)));

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / CharacterTestGenesisLivingNaturalAttributesHarness.DefaultTickRate);

                CharacterTestGenesisLivingNaturalAttributesValidation validation =
                    slice.LastGenesisLivingNaturalAttributesValidation;
                long hashAfter = participant.ComputeStateHash(frame);

                Assert.IsTrue(validation.IsHarnessOnly);
                Assert.AreEqual(
                    hashBefore,
                    hashAfter,
                    "Harness-only natural attribute samples must stay out of state hash.");
                Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                    command => command.CommandId == CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe
                        && command.SourceId == CharacterTestRuntimeCommandSources.GenesisProbe));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type ==
                        CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesProbeApplied));
                StringAssert.Contains("stateModel=harness-only", slice.LastGenesisLivingNaturalAttributesSummary);
                StringAssert.Contains("judgement=SelfMaintain", slice.LastGenesisLivingNaturalAttributesSummary);
                StringAssert.Contains("judgement=Recover", slice.LastGenesisLivingNaturalAttributesSummary);
                StringAssert.Contains("returnedToMaterial=true", slice.LastGenesisLivingNaturalAttributesSummary);
                Assert.IsTrue(validation.Embody.CanEmbody);
                Assert.IsTrue(validation.SelfMaintain.CanSelfMaintain);
                Assert.IsTrue(validation.Recover.CanRecover);
                Assert.IsTrue(validation.InactiveMaterial.ReturnedToMaterial);
            }
        }

        private static void AssertLivingNaturalAttributeSample(
            CharacterTestGenesisLivingNaturalAttributesSample sample)
        {
            Assert.IsTrue(sample.IsHarnessOnly);
            Assert.IsTrue(sample.UsesWholeBody);
            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                sample.MaterialReadingAfter);
        }
    }

    public sealed class CharacterTestGenesisPotencyMechanicsTests
    {
        [Test]
        public void GenesisPotencyMechanics_ValidationSuiteCoversFireHammerImmunityAndThresholds()
        {
            CharacterTestGenesisPotencyMechanicsValidation validation =
                CharacterTestGenesisPotencyMechanicsHarness.RunValidationSuite();

            TestContext.WriteLine(validation.ToSummaryLine());

            Assert.IsTrue(validation.IsHarnessOnly);
            Assert.IsTrue(validation.FireDryGrass.IsHarnessOnly);
            Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Burning, validation.FireDryGrass.Result);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                validation.FireDryGrass.Before);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 0, 22),
                validation.FireDryGrass.RawDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 0, 22),
                validation.FireDryGrass.AppliedDelta);
            Assert.AreEqual(
                CharacterTestGenesisPotencyTriplet.Zero,
                validation.FireDryGrass.IgnoredDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 20, 22),
                validation.FireDryGrass.After);
            Assert.IsTrue(validation.FireDryGrass.BurningThresholdMet);
            Assert.IsFalse(validation.FireDryGrass.ExternalTriggeredDirectly);
            Assert.AreEqual("threshold", validation.FireDryGrass.ResultSource);
            Assert.IsTrue(validation.FireDryGrass.NextContact.BurningResultActive);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(0, -12, 0),
                validation.FireDryGrass.NextContact.ResultDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 8, 22),
                validation.FireDryGrass.NextContact.ResultDps);
            Assert.Less(
                validation.FireDryGrass.NextContact.SupportAfter,
                validation.FireDryGrass.NextContact.SupportBefore);
            Assert.Less(
                validation.FireDryGrass.NextContact.StructureAfter,
                validation.FireDryGrass.NextContact.StructureBefore);
            Assert.Less(
                validation.FireDryGrass.NextContact.BlockResistanceAfter,
                validation.FireDryGrass.NextContact.BlockResistanceBefore);
            Assert.Greater(
                validation.FireDryGrass.NextContact.ContactDisplacementAfter,
                validation.FireDryGrass.NextContact.ContactDisplacementBefore);
            Assert.IsTrue(validation.FireDryGrass.NextContact.HasMeasurableDifference);

            Assert.AreEqual(CharacterTestGenesisPotencyResultKind.None, validation.FireStone.Result);
            Assert.AreEqual("S", validation.FireStone.ImmunitySummary);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                validation.FireStone.Before);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 0, 22),
                validation.FireStone.RawDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 0, 0),
                validation.FireStone.AppliedDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(0, 0, 22),
                validation.FireStone.IgnoredDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(18, 90, 0),
                validation.FireStone.After);
            Assert.IsFalse(validation.FireStone.BurningThresholdMet);
            Assert.AreEqual("threshold-not-met", validation.FireStone.ResultSource);
            Assert.IsFalse(validation.FireStone.NextContact.BurningResultActive);
            Assert.AreEqual(
                CharacterTestGenesisPotencyTriplet.Zero,
                validation.FireStone.NextContact.ResultDelta);
            Assert.AreEqual(validation.FireStone.After, validation.FireStone.NextContact.ResultDps);
            Assert.AreEqual(
                validation.FireStone.NextContact.SupportBefore,
                validation.FireStone.NextContact.SupportAfter);
            Assert.AreEqual(
                validation.FireStone.NextContact.StructureBefore,
                validation.FireStone.NextContact.StructureAfter);
            Assert.IsFalse(validation.FireStone.NextContact.HasMeasurableDifference);

            Assert.AreEqual(
                CharacterTestGenesisPotencyResultKind.CrushedOrBroken,
                validation.HammerDryGrass.Result);
            Assert.AreEqual(CharacterTestGenesisPotencyInputKind.Impact, validation.HammerDryGrass.InputKind);
            Assert.AreEqual(
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                validation.HammerDryGrass.ForceOrigin);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, -50, 6),
                validation.HammerDryGrass.RawDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, -50, 6),
                validation.HammerDryGrass.AppliedDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, -30, 6),
                validation.HammerDryGrass.After);
            Assert.IsTrue(validation.HammerDryGrass.CrushThresholdMet);
            Assert.Greater(validation.HammerDryGrass.After.D, validation.HammerDryGrass.After.P);
            Assert.Greater(validation.HammerDryGrass.ContactSpeed, 5f);
            Assert.Greater(validation.HammerDryGrass.ContactDisplacement, 0f);
            Assert.IsFalse(validation.HammerDryGrass.NextContact.BurningResultActive);
            Assert.AreEqual(
                CharacterTestGenesisPotencyTriplet.Zero,
                validation.HammerDryGrass.NextContact.ResultDelta);
            Assert.AreEqual(validation.HammerDryGrass.After, validation.HammerDryGrass.NextContact.ResultDps);
            Assert.IsFalse(validation.HammerDryGrass.NextContact.HasMeasurableDifference);

            Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Cracked, validation.HammerStone.Result);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, -50, 6),
                validation.HammerStone.RawDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, -50, 0),
                validation.HammerStone.AppliedDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(0, 0, 6),
                validation.HammerStone.IgnoredDelta);
            Assert.AreEqual(
                new CharacterTestGenesisPotencyTriplet(70, 40, 0),
                validation.HammerStone.After);
            Assert.IsTrue(validation.HammerStone.CrackThresholdMet);
            Assert.Greater(validation.HammerStone.After.D, validation.HammerStone.After.P);
            Assert.Greater(validation.HammerStone.ContactSpeed, 5f);

            AssertSampleUsesCombatAabb(validation.FireDryGrass);
            AssertSampleUsesCombatAabb(validation.FireStone);
            AssertSampleUsesCombatAabb(validation.HammerDryGrass);
            AssertSampleUsesCombatAabb(validation.HammerStone);

            string summary = validation.ToSummaryLine();
            StringAssert.Contains("stateModel=harness-only", summary);
            StringAssert.Contains("sample=fire->dry_grass", summary);
            StringAssert.Contains("sample=fire->stone", summary);
            StringAssert.Contains("sample=hammer->dry_grass", summary);
            StringAssert.Contains("sample=hammer->stone", summary);
            StringAssert.Contains("rawDelta=(18,0,22)", summary);
            StringAssert.Contains("rawDelta=(70,-50,6)", summary);
            StringAssert.Contains("afterDPS=(70,40,0)", summary);
            StringAssert.Contains("ignoredDelta=(0,0,22)", summary);
            StringAssert.Contains("ignoredDelta=(0,0,6)", summary);
            StringAssert.Contains("thresholds=burning:true", summary);
            StringAssert.Contains("result=Burning", summary);
            StringAssert.Contains("nextContact=[burningResult=true", summary);
            StringAssert.Contains("resultDelta=(0,-12,0)", summary);
            StringAssert.Contains("resultDPS=(18,8,22)", summary);
            StringAssert.Contains("support=20->8", summary);
            StringAssert.Contains("weakened=true", summary);
            StringAssert.Contains("sample=fire->stone", summary);
            StringAssert.Contains("burningResult=false", summary);
            StringAssert.Contains("weakened=false", summary);
            StringAssert.Contains("aabbQueryHits=", summary);
        }

        [Test]
        public void GenesisPotencyMechanics_CommandBoundaryRunsHarnessOnlyAdapter()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false))
            {
                RuntimeFrame frame = slice.CurrentFrame;
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                long hashBefore = participant.ComputeStateHash(frame);

                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbe(
                        frame,
                        CharacterTestRuntimeCommandSources.GenesisProbe,
                        CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite,
                        traceId: new RuntimeTraceIdentity(970478)));

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / CharacterTestGenesisPotencyMechanicsHarness.DefaultTickRate);

                CharacterTestGenesisPotencyMechanicsValidation validation =
                    slice.LastGenesisPotencyMechanicsValidation;
                long hashAfter = participant.ComputeStateHash(frame);

                Assert.IsTrue(validation.IsHarnessOnly);
                Assert.AreEqual(hashBefore, hashAfter, "Harness-only D/P/S samples must stay out of state hash.");
                Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                    command => command.CommandId == CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe
                        && command.SourceId == CharacterTestRuntimeCommandSources.GenesisProbe));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.GenesisPotencyMechanicsProbeApplied));
                StringAssert.Contains("stateModel=harness-only", slice.LastGenesisPotencyMechanicsSummary);
                StringAssert.Contains("sample=hammer->stone", slice.LastGenesisPotencyMechanicsSummary);
                Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Burning, validation.FireDryGrass.Result);
                Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Cracked, validation.HammerStone.Result);
            }
        }

        private static void AssertSampleUsesCombatAabb(CharacterTestGenesisPotencyMechanicsSample sample)
        {
            Assert.AreEqual(2, sample.PhysicsBodyCount);
            Assert.AreEqual(2, sample.PhysicsColliderCount);
            Assert.Greater(sample.PhysicsQueryHitCount, 0);
            Assert.Greater(sample.PhysicsExplainHitCount, 0);
            Assert.Greater(sample.PhysicsCandidateCount, 0);
            Assert.Greater(sample.PhysicsPostFilterCandidateCount, 0);
            Assert.IsTrue(sample.HasContact);
        }
    }

    public sealed class CharacterTestGenesisBoundaryFormsTests
    {
        [Test]
        public void GenesisBoundaryForms_GroundSamplesExposeMaterialContactMetrics()
        {
            CharacterTestGenesisBoundaryFormsSample hardSlick =
                CharacterTestGenesisBoundaryFormsHarness.RunGroundSample(CombatSurfaceMaterial.HardSlick);
            CharacterTestGenesisBoundaryFormsSample rough =
                CharacterTestGenesisBoundaryFormsHarness.RunGroundSample(CombatSurfaceMaterial.Rough);
            CharacterTestGenesisBoundaryFormsSample elastic =
                CharacterTestGenesisBoundaryFormsHarness.RunGroundSample(CombatSurfaceMaterial.Elastic);

            TestContext.WriteLine(hardSlick.ToSummaryLine());
            TestContext.WriteLine(rough.ToSummaryLine());
            TestContext.WriteLine(elastic.ToSummaryLine());

            AssertBoundarySummary(hardSlick, CharacterTestGenesisBoundaryKind.Ground, CombatSurfaceMaterialKind.HardSlick);
            AssertBoundarySummary(rough, CharacterTestGenesisBoundaryKind.Ground, CombatSurfaceMaterialKind.Rough);
            AssertBoundarySummary(elastic, CharacterTestGenesisBoundaryKind.Ground, CombatSurfaceMaterialKind.Elastic);
            Assert.IsTrue(hardSlick.StartGrounded);
            Assert.IsTrue(hardSlick.FinalGrounded);
            Assert.IsTrue(hardSlick.ObservedGrounded);
            Assert.Greater(hardSlick.ContactDisplacementX, 0f);
            Assert.Greater(rough.ContactDisplacementX, 0f);
            Assert.Greater(elastic.ContactDisplacementX, 0f);
            Assert.AreEqual("hard_slick", hardSlick.BoundaryMaterialStableId);
            Assert.AreEqual("rough", rough.BoundaryMaterialStableId);
            Assert.AreEqual("elastic", elastic.BoundaryMaterialStableId);
            Assert.AreEqual(0f, hardSlick.BoundaryNormalX, 0.0001f);
            Assert.Greater(hardSlick.BoundaryNormalY, 0.99f);
            Assert.IsTrue(hardSlick.ToSummaryLine().Contains("contactDisplacement="));
        }

        [Test]
        public void GenesisBoundaryForms_WallSamplesExposeBlockedAxisNormalAndElasticMaterial()
        {
            CharacterTestGenesisBoundaryFormsSample defaultWall =
                CharacterTestGenesisBoundaryFormsHarness.RunWallSample(CombatSurfaceMaterial.Default);
            CharacterTestGenesisBoundaryFormsSample elasticWall =
                CharacterTestGenesisBoundaryFormsHarness.RunWallSample(CombatSurfaceMaterial.Elastic);

            TestContext.WriteLine(defaultWall.ToSummaryLine());
            TestContext.WriteLine(elasticWall.ToSummaryLine());

            AssertBoundarySummary(defaultWall, CharacterTestGenesisBoundaryKind.Wall, CombatSurfaceMaterialKind.Default);
            AssertBoundarySummary(elasticWall, CharacterTestGenesisBoundaryKind.Wall, CombatSurfaceMaterialKind.Elastic);
            Assert.IsTrue(defaultWall.ObservedWall);
            Assert.IsTrue(defaultWall.ObservedBlockedX);
            Assert.Less(defaultWall.BoundaryNormalX, -0.99f);
            Assert.AreEqual(0f, defaultWall.BoundaryNormalY, 0.0001f);
            Assert.AreEqual(0f, defaultWall.FinalVelocityX, 0.0001f);
            Assert.AreEqual("elastic", elasticWall.BoundaryMaterialStableId);
            Assert.Less(elasticWall.FinalVelocityX, -0.5f);
            Assert.Less(elasticWall.FinalVelocityX, defaultWall.FinalVelocityX - 0.5f);
            Assert.Less(defaultWall.ContactDisplacementX, 1.6f);
        }

        [Test]
        public void GenesisBoundaryForms_LowStepClimbsButHighStepRemainsWall()
        {
            CharacterTestGenesisBoundaryFormsSample lowStep =
                CharacterTestGenesisBoundaryFormsHarness.RunLowStepSample();
            CharacterTestGenesisBoundaryFormsSample highStep =
                CharacterTestGenesisBoundaryFormsHarness.RunHighStepBlockedSample();

            TestContext.WriteLine(lowStep.ToSummaryLine());
            TestContext.WriteLine(highStep.ToSummaryLine());

            AssertBoundarySummary(lowStep, CharacterTestGenesisBoundaryKind.LowStep, CombatSurfaceMaterialKind.Default);
            AssertBoundarySummary(highStep, CharacterTestGenesisBoundaryKind.LowStep, CombatSurfaceMaterialKind.Default);
            Assert.IsTrue(lowStep.ObservedStepUp);
            Assert.IsTrue(lowStep.FinalGrounded);
            Assert.Greater(lowStep.FinalPositionX, 1f);
            Assert.Greater(lowStep.FinalPositionY, lowStep.StartPositionY + 0.1f);
            Assert.IsFalse(lowStep.ObservedBlockedX);

            Assert.IsFalse(highStep.ObservedStepUp);
            Assert.IsTrue(highStep.ObservedWall);
            Assert.IsTrue(highStep.ObservedBlockedX);
            Assert.Less(highStep.FinalPositionX, 1f);
            Assert.AreEqual(highStep.StartPositionY, highStep.FinalPositionY, 0.001f);
        }

        [Test]
        public void GenesisBoundaryForms_EdgeStartsFullySupportedThenLeavesGroundForConsecutiveFrames()
        {
            CharacterTestGenesisBoundaryFormsSample edge =
                CharacterTestGenesisBoundaryFormsHarness.RunEdgeSample();

            TestContext.WriteLine(edge.ToSummaryLine());

            AssertBoundarySummary(edge, CharacterTestGenesisBoundaryKind.Edge, CombatSurfaceMaterialKind.Default);
            Assert.IsTrue(edge.EdgeStartFullySupported);
            Assert.IsTrue(edge.StartGrounded);
            Assert.IsTrue(edge.ObservedGrounded);
            Assert.IsFalse(edge.FinalGrounded);
            Assert.Greater(edge.FirstAirborneFrame, 0);
            Assert.GreaterOrEqual(edge.AirborneFrames, 2);
            Assert.GreaterOrEqual(edge.MaxConsecutiveAirborneFrames, 2);
            Assert.Less(edge.FinalVelocityY, 0f);
            Assert.Less(edge.FinalPositionY, edge.StartPositionY);
            Assert.Less(edge.MinPositionY, edge.StartPositionY);
            Assert.Greater(edge.ContactDisplacementX, 2.5f);
            Assert.Less(edge.ContactDisplacementY, 0f);
            Assert.IsTrue(edge.ToSummaryLine().Contains("finalVelocity="));
        }

        private static void AssertBoundarySummary(
            CharacterTestGenesisBoundaryFormsSample sample,
            CharacterTestGenesisBoundaryKind boundaryKind,
            CombatSurfaceMaterialKind materialKind)
        {
            Assert.AreEqual(boundaryKind, sample.BoundaryKind);
            Assert.AreEqual(materialKind, sample.BoundaryMaterialKind);
            Assert.AreEqual(CharacterTestGenesisBoundaryFormsHarness.DefaultTickRate, sample.TickRate);
            Assert.Greater(sample.TickCount, 0);
            Assert.AreNotEqual(CombatMotionCollisionFlags.IterationLimit, sample.ObservedFlags & CombatMotionCollisionFlags.IterationLimit);
            Assert.IsTrue(sample.ToSummaryLine().Contains("boundary=" + boundaryKind));
        }
    }
}
