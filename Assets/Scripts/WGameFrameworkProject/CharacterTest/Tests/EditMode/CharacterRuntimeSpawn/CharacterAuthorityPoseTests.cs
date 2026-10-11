using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterAuthorityPoseTests
    {
        [Test]
        public void SecondaryPose_SameInputFrameSequenceProducesSameSnapshot()
        {
            CharacterAuthorityPoseRigDefinition definition = CreateDefinition();
            var first = new CharacterAuthorityPoseStateComponent(definition);
            var second = new CharacterAuthorityPoseStateComponent(definition);
            var request = new CharacterAuthorityPoseImpulseRequest(
                MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(4),
                6,
                new MxFramework.Runtime.RuntimeTraceIdentity(MxFramework.Tests.CharacterCoreFixtureIds.Trace_93032),
                CharacterAuthorityPoseMatchSource.PreviewCommand,
                CharacterAuthorityPoseIssue.None);

            Assert.IsTrue(first.ApplySecondaryImpulse(request, new RuntimeFrame(3), out _));
            Assert.IsTrue(second.ApplySecondaryImpulse(request, new RuntimeFrame(3), out _));

            for (int frame = 0; frame < 16; frame++)
            {
                first.Tick(new RuntimeFrame(frame));
                second.Tick(new RuntimeFrame(frame));
            }

            CharacterAuthorityPoseSnapshot a = first.CreateSnapshot();
            CharacterAuthorityPoseSnapshot b = second.CreateSnapshot();
            AssertSnapshotsEqual(a, b);
            Assert.IsTrue(first.TryGetSocketOffset(MxFramework.Tests.CharacterCoreFixtureIds.Id_12, out FixVector3 socketOffset));
            Assert.IsFalse(socketOffset.IsZero);
        }

        [Test]
        public void SecondaryPose_SnapshotRoundtripContinuesDeterministically()
        {
            CharacterAuthorityPoseRigDefinition definition = CreateDefinition();
            var original = new CharacterAuthorityPoseStateComponent(definition);
            var restored = new CharacterAuthorityPoseStateComponent(definition);
            var request = new CharacterAuthorityPoseImpulseRequest(
                MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                Fix64.FromInt(5),
                5,
                new MxFramework.Runtime.RuntimeTraceIdentity(MxFramework.Tests.CharacterCoreFixtureIds.Trace_93033),
                CharacterAuthorityPoseMatchSource.PreviewCommand,
                CharacterAuthorityPoseIssue.None);

            Assert.IsTrue(original.ApplySecondaryImpulse(request, RuntimeFrame.Zero, out _));
            for (int frame = 0; frame < 4; frame++)
                original.Tick(new RuntimeFrame(frame));

            restored.RestoreSnapshot(original.CreateSnapshot());
            for (int frame = 4; frame < 12; frame++)
            {
                original.Tick(new RuntimeFrame(frame));
                restored.Tick(new RuntimeFrame(frame));
            }

            AssertSnapshotsEqual(original.CreateSnapshot(), restored.CreateSnapshot());
        }

        [Test]
        public void SecondaryPose_CopyPoseSamplesDoesNotAllocateAfterWarmup()
        {
            CharacterAuthorityPoseRigDefinition definition = CreateDefinition();
            var state = new CharacterAuthorityPoseStateComponent(definition);
            var request = new CharacterAuthorityPoseImpulseRequest(
                MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(4),
                6,
                new MxFramework.Runtime.RuntimeTraceIdentity(MxFramework.Tests.CharacterCoreFixtureIds.Trace_93032),
                CharacterAuthorityPoseMatchSource.PreviewCommand,
                CharacterAuthorityPoseIssue.None);
            var samples = new List<CharacterAuthoritySecondaryPoseSample>();

            Assert.IsTrue(state.ApplySecondaryImpulse(request, RuntimeFrame.Zero, out _));
            for (int frame = 0; frame < 4; frame++)
            {
                state.Tick(new RuntimeFrame(frame));
                state.CopySecondaryPoseSamplesTo(samples);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 4; frame < 64; frame++)
            {
                state.Tick(new RuntimeFrame(frame));
                state.CopySecondaryPoseSamplesTo(samples);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.LessOrEqual(samples.Count, definition.SecondaryBindings.Count);
        }

        [Test]
        public void TwoBoneIk_ClampsUnreachableTargetDeterministically()
        {
            var binding = new CharacterAuthorityTwoBoneIkBinding(
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000089,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000029,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000087,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000088,
                Fix64.FromInt(1),
                Fix64.FromInt(1),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                Fix64.FromInt(2));

            Assert.IsTrue(CharacterAuthorityTwoBoneIkSolver.TrySolve(
                binding,
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero),
                out CharacterAuthorityTwoBoneIkSolveResult result));
            Assert.IsTrue(result.Solved);
            Assert.IsTrue(result.TargetClamped);
            Assert.AreEqual(Fix64.FromInt(2).RawValue, result.TipPosition.X.RawValue);
            Assert.AreEqual(Fix64.Zero.RawValue, result.TipPosition.Y.RawValue);
            Assert.AreEqual(Fix64.Zero.RawValue, result.TipPosition.Z.RawValue);
        }

        [Test]
        public void RigDefinition_KnownSecondaryRegionCountDeduplicatesAndDoesNotAllocateAfterConstruction()
        {
            var definition = new CharacterAuthorityPoseRigDefinition(new[]
            {
                new CharacterAuthoritySecondaryBinding(
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_300000090,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_12,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.One),
                new CharacterAuthoritySecondaryBinding(
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_300000091,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_300000092,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_300000093,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.One),
                new CharacterAuthoritySecondaryBinding(
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_26,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_10,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_11,
                    0,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.One)
            });

            Assert.AreEqual(2, definition.KnownSecondaryRegionCount);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int total = 0;
            for (int i = 0; i < 64; i++)
                total += definition.KnownSecondaryRegionCount;

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(128, total);
            Assert.AreEqual(0L, allocatedBytes);
        }

        private static CharacterAuthorityPoseRigDefinition CreateDefinition()
        {
            return new CharacterAuthorityPoseRigDefinition(new[]
            {
                new CharacterAuthoritySecondaryBinding(
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_26,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_10,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_11,
                    0,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.FromRatio(9, 1000)),
                new CharacterAuthoritySecondaryBinding(
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_22,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_6,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_12,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.FromRatio(9, 1000))
            });
        }

        private static void AssertSnapshotsEqual(
            CharacterAuthorityPoseSnapshot expected,
            CharacterAuthorityPoseSnapshot actual)
        {
            Assert.AreEqual(expected.ActiveSecondaryCount, actual.ActiveSecondaryCount);
            Assert.AreEqual(expected.KnownRegionCount, actual.KnownRegionCount);
            Assert.AreEqual(expected.LastResolvedRegionId, actual.LastResolvedRegionId);
            Assert.AreEqual(expected.LastMatchSource, actual.LastMatchSource);
            Assert.AreEqual(expected.LastIssue, actual.LastIssue);
            Assert.AreEqual(expected.SecondaryBindings.Length, actual.SecondaryBindings.Length);
            for (int i = 0; i < expected.SecondaryBindings.Length; i++)
            {
                CharacterAuthoritySecondarySnapshot left = expected.SecondaryBindings[i];
                CharacterAuthoritySecondarySnapshot right = actual.SecondaryBindings[i];
                Assert.AreEqual(left.BindingId, right.BindingId);
                Assert.AreEqual(left.OffsetXRaw, right.OffsetXRaw);
                Assert.AreEqual(left.OffsetYRaw, right.OffsetYRaw);
                Assert.AreEqual(left.OffsetZRaw, right.OffsetZRaw);
                Assert.AreEqual(left.VelocityXRaw, right.VelocityXRaw);
                Assert.AreEqual(left.VelocityYRaw, right.VelocityYRaw);
                Assert.AreEqual(left.VelocityZRaw, right.VelocityZRaw);
                Assert.AreEqual(left.PendingForceXRaw, right.PendingForceXRaw);
                Assert.AreEqual(left.PendingForceYRaw, right.PendingForceYRaw);
                Assert.AreEqual(left.PendingForceZRaw, right.PendingForceZRaw);
                Assert.AreEqual(left.RemainingFrames, right.RemainingFrames);
                Assert.AreEqual(left.StartFrame, right.StartFrame);
                Assert.AreEqual(left.Active, right.Active);
            }
        }
    }
}
