using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAnimatedAppendageSoftContactTests
    {
        [Test]
        public void HeadObbAndTailCapsules_ProduceBoundedPressureWithoutDirectDisplacement()
        {
            CharacterTestCharacterEntity owner = AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f);
            CharacterTestCharacterEntity target = AnimatedAppendageSoftContactFixture.CreateEntity(2, 1.05f);
            CharacterTestCharacterEntity[] roster = { owner, target };
            CharacterTestAnimatedAppendageLeafWitness[] current =
            {
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.head.obb.0",
                    1001,
                    "head",
                    CombatPhysicsShape.Obb(
                        new FixVector3(AnimatedAppendageSoftContactFixture.Meters(0.45f), AnimatedAppendageSoftContactFixture.Meters(0.9f), Fix64.Zero),
                        new FixVector3(AnimatedAppendageSoftContactFixture.Meters(0.35f), AnimatedAppendageSoftContactFixture.Meters(0.3f), AnimatedAppendageSoftContactFixture.Meters(0.3f)),
                        new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                        new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                        new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)),
                    frame: 2),
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.tail_mid.capsule.0",
                    1002,
                    "tail_mid",
                    AnimatedAppendageSoftContactFixture.CapsuleAt(0.25f, 0.65f),
                    frame: 2),
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.tail_tip.capsule.0",
                    1003,
                    "tail_tip",
                    AnimatedAppendageSoftContactFixture.CapsuleAt(0.4f, 0.65f),
                    frame: 2)
            };

            int ownerX = owner.Transform.XMillimeters;
            int targetX = target.Transform.XMillimeters;
            var resolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                previousReady: false,
                current);

            CharacterTestAnimatedAppendageSoftContactDiagnostics diagnostics =
                resolver.CaptureAnimatedAppendageSoftContactDiagnostics();
            Assert.AreEqual(CharacterTestAnimatedAppendageSoftContactBuildStatus.Success, diagnostics.Status);
            Assert.AreEqual(3, diagnostics.ConstraintCount);
            Assert.AreEqual(3, diagnostics.PressureIterationCount);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactVelocityXMillimetersPerSecond, 0);
            Assert.LessOrEqual(Math.Abs(target.Physics.ContactVelocityXMillimetersPerSecond), 6300);
            Assert.AreEqual(ownerX, owner.Transform.XMillimeters);
            Assert.AreEqual(targetX, target.Transform.XMillimeters);
        }

        [Test]
        public void SeparatedTouchingDeepAndSameCenter_AreTypedAndStable()
        {
            Assert.AreEqual(0, RunSphereAt(3f).ConstraintCount);
            Assert.AreEqual(1, RunSphereAt(1.35f).ConstraintCount);
            Assert.AreEqual(1, RunSphereAt(0.2f).ConstraintCount);
            Assert.AreEqual(1, RunSphereAt(0f).ConstraintCount);

            CharacterTestCharacterEntity[] zeroAccelerationRoster =
            {
                AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f),
                AnimatedAppendageSoftContactFixture.CreateEntity(2, 0.2f)
            };
            var zeroAccelerationResolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                zeroAccelerationResolver,
                zeroAccelerationRoster,
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                false,
                new[] { AnimatedAppendageSoftContactFixture.DefaultWitness(2) },
                accelerationMillimetersPerSecondSquared: 0);
            Assert.Greater(
                Math.Abs(zeroAccelerationRoster[1].Physics.ContactVelocityXMillimetersPerSecond),
                0,
                "Static animated surfaces still need bounded penetration pressure when profile acceleration is zero.");
        }

        [Test]
        public void SeveredTail_DerivesOneEndedAndKeepsHeadStayed()
        {
            CharacterTestCharacterEntity[] roster =
            {
                AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f),
                AnimatedAppendageSoftContactFixture.CreateEntity(2, 1.05f)
            };
            CharacterTestAnimatedAppendageLeafWitness head =
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.head.obb.0",
                    1001,
                    "head",
                    CombatPhysicsShape.Sphere(
                        new FixVector3(AnimatedAppendageSoftContactFixture.Meters(0.5f), AnimatedAppendageSoftContactFixture.Meters(0.9f), Fix64.Zero),
                        AnimatedAppendageSoftContactFixture.Meters(0.5f)),
                    2);
            CharacterTestAnimatedAppendageLeafWitness tail =
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.tail_tip.capsule.0",
                    1003,
                    "tail_tip",
                    AnimatedAppendageSoftContactFixture.CapsuleAt(0.2f, 0.6f),
                    2);
            var resolver = new CharacterTestCharacterContactResolver(null);

            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                new[] { head, tail },
                previousReady: true,
                new[] { head, tail });
            Assert.AreEqual(0, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().PhaseFactCount,
                "Spawn/restore baseline must be silent.");

            head = AnimatedAppendageSoftContactFixture.WithFrame(head, 3);
            tail = AnimatedAppendageSoftContactFixture.WithFrame(tail, 3);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                new[] { AnimatedAppendageSoftContactFixture.WithFrame(head, 2), AnimatedAppendageSoftContactFixture.WithFrame(tail, 2) },
                previousReady: true,
                new[] { head });
            AssertPhaseCounts(resolver, stayed: 1, ended: 1);

            head = AnimatedAppendageSoftContactFixture.WithFrame(head, 4);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                new[] { AnimatedAppendageSoftContactFixture.WithFrame(head, 3) },
                previousReady: true,
                new[] { head });
            AssertPhaseCounts(resolver, stayed: 1, ended: 0);
        }

        [Test]
        public void DeadDisabledAndUnpushable_FailClosedOrObserveWithoutVelocity()
        {
            CharacterTestCharacterEntity owner = AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f);
            CharacterTestCharacterEntity target = AnimatedAppendageSoftContactFixture.CreateEntity(2, 1f);
            target.SetPhysics(target.Physics.WithRuntimeModifiers(1f, 1f, 1f, canBePushed: false));
            target.SetColliderPosture(CharacterTestColliderPostureComponent.FromPhysics(target.Physics, 0L));
            CharacterTestAnimatedAppendageLeafWitness witness = AnimatedAppendageSoftContactFixture.DefaultWitness(frame: 2);
            var resolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                new[] { owner, target },
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                false,
                new[] { witness });
            Assert.AreEqual(1, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().ConstraintCount);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.AreEqual(0, target.Physics.ContactVelocityXMillimetersPerSecond);

            owner.ComponentWorld.SetBattleState(
                owner.Id,
                new CharacterTestBattleStateComponent(
                    CharacterTestCharacterLifeState.Dead,
                    CharacterTestBattleStateDominantReason.Death,
                    CharacterTestBattleGateFlags.None,
                    1L,
                    1L,
                    default,
                    0));
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                new[] { owner, target },
                new[] { AnimatedAppendageSoftContactFixture.WithFrame(witness, 1) },
                true,
                new[] { witness });
            Assert.AreEqual(0, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().ConstraintCount);
        }

        [Test]
        public void CapacityOverflow_RejectsBeforeContactStateMutation()
        {
            CharacterTestCharacterEntity[] roster = new CharacterTestCharacterEntity[9];
            for (int i = 0; i < roster.Length; i++)
                roster[i] = AnimatedAppendageSoftContactFixture.CreateEntity(i + 1, 0f);
            var witnesses = new CharacterTestAnimatedAppendageLeafWitness[33];
            for (int i = 0; i < witnesses.Length; i++)
            {
                witnesses[i] = AnimatedAppendageSoftContactFixture.Witness(
                    1 + i / 4,
                    "hurt.capacity." + i,
                    1000 + i,
                    "part" + i,
                    CombatPhysicsShape.Sphere(
                        new FixVector3(Fix64.Zero, AnimatedAppendageSoftContactFixture.Meters(0.9f), Fix64.Zero),
                        Fix64.One),
                    2);
            }
            var resolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                false,
                witnesses);
            Assert.AreEqual(
                CharacterTestAnimatedAppendageSoftContactBuildStatus.CapacityExceeded,
                resolver.CaptureAnimatedAppendageSoftContactDiagnostics().Status);
            for (int i = 0; i < roster.Length; i++)
                Assert.AreEqual(0, roster[i].Physics.ContactCount);
        }

        private static CharacterTestAnimatedAppendageSoftContactDiagnostics RunSphereAt(float targetX)
        {
            CharacterTestCharacterEntity[] roster =
            {
                AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f),
                AnimatedAppendageSoftContactFixture.CreateEntity(2, targetX)
            };
            var resolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                false,
                new[] { AnimatedAppendageSoftContactFixture.DefaultWitness(2) });
            return resolver.CaptureAnimatedAppendageSoftContactDiagnostics();
        }

        private static void AssertPhaseCounts(
            CharacterTestCharacterContactResolver resolver,
            int stayed,
            int ended)
        {
            int stayedCount = 0;
            int endedCount = 0;
            CharacterTestAnimatedAppendageSoftContactDiagnostics diagnostics =
                resolver.CaptureAnimatedAppendageSoftContactDiagnostics();
            for (int i = 0; i < diagnostics.PhaseFactCount; i++)
            {
                Assert.IsTrue(resolver.TryGetAnimatedAppendageSoftContactPhase(i, out var fact));
                if (fact.Phase == CharacterTestAnimatedAppendageSoftContactPhase.Stayed) stayedCount++;
                if (fact.Phase == CharacterTestAnimatedAppendageSoftContactPhase.Ended) endedCount++;
            }
            Assert.AreEqual(stayed, stayedCount);
            Assert.AreEqual(ended, endedCount);
        }
    }

    internal static class AnimatedAppendageSoftContactFixture
    {
        internal static CharacterTestCharacterPhysicsRuntime CreatePhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                1,
                "test.animated-appendage-soft-contact");
        }

        internal static CharacterTestCharacterEntity CreateEntity(int id, float x)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    1,
                    "Animated Appendage Test",
                    default(ResourceKey),
                    default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(1, 1),
                new CharacterTestCharacterTransformComponent(x, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        internal static CharacterTestAnimatedAppendageLeafWitness DefaultWitness(long frame)
        {
            return Witness(
                1,
                "hurt.head.obb.0",
                1001,
                "head",
                CombatPhysicsShape.Sphere(
                    new FixVector3(Meters(0.5f), Meters(0.9f), Fix64.Zero),
                    Meters(0.5f)),
                frame);
        }

        internal static CombatPhysicsShape CapsuleAt(float x, float radius)
        {
            return CombatPhysicsShape.Capsule(
                new FixVector3(Meters(x), Meters(0.5f), Fix64.Zero),
                new FixVector3(Meters(x), Meters(1.3f), Fix64.Zero),
                Meters(radius));
        }

        internal static CharacterTestAnimatedAppendageLeafWitness Witness(
            int ownerEntityId,
            string logicalColliderId,
            int combatColliderId,
            string partId,
            CombatPhysicsShape shape,
            long frame)
        {
            return new CharacterTestAnimatedAppendageLeafWitness(
                ownerEntityId,
                ResolveLogicalColliderId(logicalColliderId),
                logicalColliderId,
                combatColliderId,
                ResolvePartId(partId),
                partId,
                shape,
                FixVector3.Zero,
                new RuntimeFrame(frame),
                hurtboxRevision: (int)frame,
                topologyRevision: 1,
                bindingRevision: 2,
                bindingContentHash: "sha256:" + new string('a', 64),
                skinMillimeters: 50,
                pressureScaleMilli: 1000);
        }

        internal static CharacterTestAnimatedAppendageLeafWitness WithFrame(
            CharacterTestAnimatedAppendageLeafWitness value,
            long frame)
        {
            return new CharacterTestAnimatedAppendageLeafWitness(
                value.OwnerEntityId,
                value.LogicalColliderId,
                value.LogicalColliderLabel,
                value.CombatColliderId,
                value.PartId,
                value.PartLabel,
                value.Shape,
                value.OwnerRootWorld,
                new RuntimeFrame(frame),
                (int)frame,
                value.TopologyRevision,
                value.BindingRevision,
                value.BindingContentHash,
                value.SkinMillimeters,
                value.PressureScaleMilli);
        }

        private static int ResolveLogicalColliderId(string label)
        {
            switch (label)
            {
                case "hurt.head.obb.0": return CharacterTestAuthoredContentIds.D400_Id150;
                case "hurt.tail_mid.capsule.0": return CharacterTestAuthoredContentIds.D400_Id148;
                case "hurt.tail_tip.capsule.0": return CharacterTestAuthoredContentIds.D400_Id149;
                default: return CharacterTestAuthoredContentIds.D400_Id36;
            }
        }

        private static int ResolvePartId(string label)
        {
            switch (label)
            {
                case "head": return CharacterTestAuthoredContentIds.D400_Id3;
                case "tail_mid": return CharacterTestAuthoredContentIds.D400_Id101;
                case "tail_tip": return CharacterTestAuthoredContentIds.D400_Id103;
                default: return CharacterTestAuthoredContentIds.D400_Id34;
            }
        }

        internal static void Resolve(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterEntity[] roster,
            CharacterTestAnimatedAppendageLeafWitness[] previous,
            bool previousReady,
            CharacterTestAnimatedAppendageLeafWitness[] current,
            int accelerationMillimetersPerSecondSquared = 18000)
        {
            resolver.Clear();
            resolver.ResolveAnimatedAppendageSoftContacts(
                roster,
                CreatePhysics(),
                previous,
                previous.Length,
                previousReady,
                current,
                current.Length,
                currentReady: true,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared,
                tickRate: 60);
            resolver.ApplyCharacterContactState(roster);
        }

        internal static Fix64 Meters(float value)
        {
            return CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                CharacterTestDeterministicUnits.MetersToMillimeters(value));
        }
    }
}
