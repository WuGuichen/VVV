using System;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAnimatedAppendageSoftContactNetworkTests
    {
        [Test]
        public void RosterAndLeafOrderPerturbation_ProducesSameAuthorityContactState()
        {
            RunResult forward = Run(reverseRoster: false, reverseLeaves: false);
            RunResult reversed = Run(reverseRoster: true, reverseLeaves: true);
            Assert.AreEqual(CharacterTestAnimatedAppendageSoftContactBuildStatus.Success, forward.Status);
            Assert.AreEqual(forward.Status, reversed.Status);
            Assert.AreEqual(forward.ConstraintCount, reversed.ConstraintCount);
            Assert.AreEqual(forward.TargetVelocityX, reversed.TargetVelocityX);
            Assert.AreEqual(forward.TargetContactCount, reversed.TargetContactCount);
        }

        [Test]
        public void RestoreSuppression_EstablishesSilentBaselineThenStayed()
        {
            CharacterTestCharacterEntity[] roster =
            {
                AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f),
                AnimatedAppendageSoftContactFixture.CreateEntity(2, 1.05f)
            };
            var resolver = new CharacterTestCharacterContactResolver(null);
            CharacterTestAnimatedAppendageLeafWitness frame1 =
                AnimatedAppendageSoftContactFixture.DefaultWitness(1);
            CharacterTestAnimatedAppendageLeafWitness frame2 =
                AnimatedAppendageSoftContactFixture.DefaultWitness(2);
            resolver.SuppressAnimatedAppendagePresentationPhases();
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                new[] { frame1 },
                true,
                new[] { frame2 });
            Assert.AreEqual(0, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().PhaseFactCount);

            CharacterTestAnimatedAppendageLeafWitness frame3 =
                AnimatedAppendageSoftContactFixture.DefaultWitness(3);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                new[] { frame2 },
                true,
                new[] { frame3 });
            Assert.AreEqual(1, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().PhaseFactCount);
            Assert.IsTrue(resolver.TryGetAnimatedAppendageSoftContactPhase(0, out var fact));
            Assert.AreEqual(CharacterTestAnimatedAppendageSoftContactPhase.Stayed, fact.Phase);
        }

        private static RunResult Run(bool reverseRoster, bool reverseLeaves)
        {
            CharacterTestCharacterEntity owner = AnimatedAppendageSoftContactFixture.CreateEntity(1, 0f);
            CharacterTestCharacterEntity target = AnimatedAppendageSoftContactFixture.CreateEntity(2, 1.05f);
            CharacterTestCharacterEntity[] roster = { owner, target };
            CharacterTestAnimatedAppendageLeafWitness[] leaves =
            {
                AnimatedAppendageSoftContactFixture.DefaultWitness(2),
                AnimatedAppendageSoftContactFixture.Witness(
                    1,
                    "hurt.tail_mid.capsule.0",
                    1002,
                    "tail_mid",
                    AnimatedAppendageSoftContactFixture.CapsuleAt(0.4f, 0.65f),
                    2)
            };
            if (reverseRoster) Array.Reverse(roster);
            if (reverseLeaves) Array.Reverse(leaves);
            var resolver = new CharacterTestCharacterContactResolver(null);
            AnimatedAppendageSoftContactFixture.Resolve(
                resolver,
                roster,
                Array.Empty<CharacterTestAnimatedAppendageLeafWitness>(),
                false,
                leaves);
            CharacterTestAnimatedAppendageSoftContactDiagnostics diagnostics =
                resolver.CaptureAnimatedAppendageSoftContactDiagnostics();
            return new RunResult(
                diagnostics.Status,
                diagnostics.ConstraintCount,
                target.Physics.ContactVelocityXMillimetersPerSecond,
                target.Physics.ContactCount);
        }

        private readonly struct RunResult
        {
            internal RunResult(
                CharacterTestAnimatedAppendageSoftContactBuildStatus status,
                int constraintCount,
                int targetVelocityX,
                int targetContactCount)
            {
                Status = status;
                ConstraintCount = constraintCount;
                TargetVelocityX = targetVelocityX;
                TargetContactCount = targetContactCount;
            }

            internal CharacterTestAnimatedAppendageSoftContactBuildStatus Status { get; }
            internal int ConstraintCount { get; }
            internal int TargetVelocityX { get; }
            internal int TargetContactCount { get; }
        }
    }
}
