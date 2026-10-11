using System;
using System.Collections.Generic;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSlopeFullMotionNoAllocTests
    {
        private const int WarmTicks = 120;
        private const int SampleTicks = 600;
        private const int ExpectedObservedTicks = SampleTicks;

        [TestCase((int)CharacterTestSlopeNoAllocCase.ThirtyUphill, TestName = "ThirtyUphill")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.ThirtyDownhillSnap, TestName = "ThirtyDownhillSnap")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.ThirtyTraverse, TestName = "ThirtyTraverse")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.ThirtyIdle, TestName = "ThirtyIdle")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.FiftyBlocked, TestName = "FiftyBlocked")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.EdgeSupportMiss, TestName = "EdgeSupportMiss")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.RootMotionStaticOnly, TestName = "RootMotionStaticOnly")]
        [TestCase((int)CharacterTestSlopeNoAllocCase.ImpulseRaw, TestName = "ImpulseRaw")]
        public void PreparedFormalFullMotionCase_IsStrictlyNoAlloc(
            int itemValue)
        {
            CharacterTestSlopeNoAllocCase item = (CharacterTestSlopeNoAllocCase)itemValue;
            Scene scene = EditorSceneManager.OpenScene(
                CharacterTestSlopePlayableFixture.ScenePath,
                OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            Assert.IsFalse(scene.isDirty);
            GameManager manager = CharacterTestSlopePlayableFixture.FindGameManager(scene);

            using (CharacterTestSlopePlayableFixture fixture =
                   CharacterTestSlopePlayableFixture.Create(manager))
            {
                AssertFormalRuntime(fixture);
                fixture.PrepareNoAllocCase(item);

                long warmLastFrame = 0L;
                for (int i = 0; i < WarmTicks; i++)
                    warmLastFrame = fixture.TickPreparedNoAllocCase().FrameValue;

                CharacterTestSlopeSemanticScalars warmSemantics =
                    fixture.GetPreparedNoAllocSemantics();
                Assert.AreEqual(WarmTicks, warmSemantics.TickCount);
                Assert.IsTrue(warmSemantics.CommandsAccepted);
                Assert.Greater(warmLastFrame, 0L);

                fixture.BeginNoAllocSampleWindow();
                CharacterTestSlopeSemanticScalars sampleStartSemantics =
                    fixture.GetPreparedNoAllocSemantics();
                Assert.AreEqual(0, sampleStartSemantics.TickCount);
                Assert.IsTrue(sampleStartSemantics.CommandsAccepted);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _ = GC.GetAllocatedBytesForCurrentThread();

                long sampleLastFrame = 0L;
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < SampleTicks; i++)
                    sampleLastFrame = fixture.TickPreparedNoAllocCase().FrameValue;
                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                CharacterTestSlopeSemanticScalars semantics =
                    fixture.GetPreparedNoAllocSemantics();
                Assert.AreEqual(0L, allocatedBytes);
                Assert.Greater(sampleLastFrame, warmLastFrame);
                Assert.AreEqual(ExpectedObservedTicks, semantics.TickCount);
                Assert.IsTrue(semantics.CommandsAccepted);
                AssertCaseSemantics(item, semantics);
            }

            Assert.IsFalse(scene.isDirty);
        }

        private static void AssertFormalRuntime(CharacterTestSlopePlayableFixture fixture)
        {
            Assert.IsNotNull(fixture);
            Assert.AreEqual(1, fixture.Slice.CharacterEntities.Count);
            Assert.IsNotNull(fixture.Physics);
            Assert.AreEqual(39, fixture.Physics.StaticColliderCount);
            Assert.AreEqual(100000L, fixture.Physics.GroundSnapDistance.RawValue);
            var obbs = new List<CombatPhysicsObbCollider>();
            fixture.Physics.World.CopyObbCollidersTo(obbs);
            Assert.AreEqual(3, obbs.Count);
            AssertSlopeObb(fixture.Physics, CharacterTestSlopePlayableFixture.Slope15BodyId);
            AssertSlopeObb(fixture.Physics, CharacterTestSlopePlayableFixture.Slope30BodyId);
            AssertSlopeObb(fixture.Physics, CharacterTestSlopePlayableFixture.Slope50BodyId);
        }

        private static void AssertSlopeObb(
            CharacterTestCharacterPhysicsRuntime physics,
            int bodyIdValue)
        {
            Assert.IsTrue(physics.World.TryGetObbCollider(
                new MxFramework.Combat.Core.CombatBodyId(bodyIdValue),
                new MxFramework.Combat.Core.CombatColliderId(
                    CharacterTestSlopePlayableFixture.SlopeColliderId),
                out CombatPhysicsObbCollider collider));
            Assert.AreEqual(1, collider.Layer);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, collider.SurfaceMaterial.Kind);
        }

        private static void AssertCaseSemantics(
            CharacterTestSlopeNoAllocCase item,
            CharacterTestSlopeSemanticScalars semantics)
        {
            switch (item)
            {
                case CharacterTestSlopeNoAllocCase.ThirtyUphill:
                    Assert.Greater(semantics.FinalY, semantics.StartY);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    Assert.Greater(semantics.GroundedCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyDownhillSnap:
                    Assert.Less(semantics.FinalY, semantics.StartY);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    Assert.Greater(semantics.SnapCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyTraverse:
                    Assert.AreNotEqual(semantics.StartZ, semantics.FinalZ);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.ThirtyIdle:
                    Assert.AreEqual(semantics.StartX, semantics.FinalX);
                    Assert.AreEqual(semantics.StartZ, semantics.FinalZ);
                    Assert.AreEqual(ExpectedObservedTicks, semantics.GroundedCount);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.FiftyBlocked:
                    Assert.LessOrEqual(
                        semantics.MaximumY,
                        semantics.StartY + CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters);
                    Assert.Greater(semantics.SteepCount, 0);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.EdgeSupportMiss:
                    Assert.Greater(semantics.EdgeBeyondGuardCount, 0);
                    Assert.Greater(semantics.EdgeAirborneCount, 0);
                    Assert.AreEqual(0, semantics.EdgeSlopeSnapCount);
                    break;
                case CharacterTestSlopeNoAllocCase.RootMotionStaticOnly:
                    Assert.AreNotEqual(semantics.StartX, semantics.FinalX);
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    Assert.Greater(semantics.NonZeroRequestedXCount, 0);
                    Assert.Greater(semantics.NonZeroAppliedOrResolvedXCount, 0);
                    break;
                case CharacterTestSlopeNoAllocCase.ImpulseRaw:
                    Assert.Greater(semantics.ExpectedSlopeContactCount, 0);
                    Assert.Greater(semantics.ExpectedSlopeInteriorCount, 0);
                    Assert.Greater(semantics.GroundedCount, 0);
                    Assert.Greater(semantics.NonZeroRequestedXCount, 0);
                    Assert.Greater(semantics.NonZeroAppliedOrResolvedXCount, 0);
                    break;
                default:
                    Assert.Fail("Unexpected prepared NoAlloc case: " + item);
                    break;
            }
        }
    }
}
