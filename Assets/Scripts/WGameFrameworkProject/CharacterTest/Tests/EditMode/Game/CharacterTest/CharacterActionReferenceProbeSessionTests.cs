using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionReferenceProbeSessionTests
    {
        [Test]
        public void ReferenceTargetIsFormalCapsuleWithExactRootLocalOffsets()
        {
            var dummy = new CharacterActionReferenceDummy(1, 1800, 300, 100, 900, 200);
            using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), dummy))
            {
                Assert.IsTrue(session.TryEvaluate(2000, out _, out string issue), issue);
                Assert.IsTrue(session.Runtime.TryGetDamageGeometrySnapshot(session.Target.Id.Value, out var geometry));
                Assert.AreEqual(1, geometry.ColliderCount);
                var collider = geometry.GetCollider(0);
                Assert.AreEqual("reference-capsule", collider.LogicalColliderLabel);
                Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id36, collider.LogicalColliderId);
                Assert.AreEqual(CombatPhysicsShapeKind.Capsule, collider.LocalShape.Kind);
                Assert.AreEqual(Fix64.FromRatio(3, 10), collider.LocalShape.Radius);
                Assert.AreEqual(new FixVector3(Fix64.FromRatio(1, 10), Fix64.FromRatio(3, 10), Fix64.FromRatio(2, 10)), collider.LocalShape.PointA);
                Assert.AreEqual(new FixVector3(Fix64.FromRatio(1, 10), Fix64.FromRatio(15, 10), Fix64.FromRatio(2, 10)), collider.LocalShape.PointB);
                Assert.AreEqual(Fix64.FromRatio(19, 10), collider.WorldMin.Z);
                Assert.AreEqual(Fix64.FromRatio(25, 10), collider.WorldMax.Z);
                Assert.AreEqual(session.Runtime.PhysicsRuntime.World.Revision, geometry.WorldRevision);
                Assert.Throws<InvalidOperationException>(() => session.Runtime.ConfigureReferenceDummyForMeasurement(session.Target.Id.Value, dummy));
            }
        }

        [Test]
        public void Real301FormalPreviewIsRepeatableAndReadsRequiredBakes()
        {
            var dummy = new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0);
            using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), dummy))
            {
                Assert.IsTrue(session.TryComputeActionHash(out string before, out string issue), issue);
                int hitDistance = -1;
                for (int distance = 500; distance <= 3000; distance += 250)
                {
                    Assert.IsTrue(session.TryEvaluate(distance, out var result, out issue), issue);
                    Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
                    if (result.CanHitFromCurrentPose) { hitDistance = distance; break; }
                }
                Assert.GreaterOrEqual(hitDistance, 0, "301 must produce a formal capsule hit in the reference scan.");
                Assert.IsTrue(session.TryEvaluate(hitDistance, out var first, out issue), issue);
                Assert.IsTrue(session.TryEvaluate(hitDistance, out var second, out issue), issue);
                Assert.IsTrue(first.TraceCanResolveTargetHit);
                Assert.IsTrue(second.TraceCanResolveTargetHit);
                Assert.AreEqual(first.EarliestCandidateHitTick, second.EarliestCandidateHitTick);
                Assert.AreEqual(first.SelectedHurtboxId, second.SelectedHurtboxId);
                Assert.AreEqual(301, first.ActionId);
                Assert.IsTrue(session.TryComputeActionHash(out string after, out issue), issue);
                Assert.AreEqual(before, after);
            }
        }

        [Test]
        public void DisposedOrNegativeQueriesFailWithoutProducingEvidence()
        {
            var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301),
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            try { Assert.IsFalse(session.TryEvaluate(-1, out _, out _)); }
            finally { session.Dispose(); }
            Assert.IsFalse(session.TryEvaluate(1000, out _, out _));
            Assert.IsFalse(session.TryComputeActionHash(out string hash, out _));
            Assert.IsEmpty(hash);
        }
    }
}
