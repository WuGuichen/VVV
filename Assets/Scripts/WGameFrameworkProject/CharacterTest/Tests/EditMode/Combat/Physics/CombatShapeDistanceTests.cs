using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatShapeDistanceTests
    {
        [Test]
        public void CapsuleSphere_ReportsPositiveGapTouchAndOverlapWitnesses()
        {
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);

            CombatShapeDistanceResult separated = Distance(
                source,
                CombatPhysicsShape.Sphere(V(4, 0, 0), Fix64.One));
            CombatShapeDistanceResult touching = Distance(
                source,
                CombatPhysicsShape.Sphere(V(2, 0, 0), Fix64.One));
            CombatShapeDistanceResult overlapping = Distance(
                source,
                CombatPhysicsShape.Sphere(V(1, 0, 0), Fix64.One));

            Assert.That(separated.Status, Is.EqualTo(CombatShapeSeparationStatus.Separated));
            Assert.That(separated.SurfaceGap, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(separated.SourceWitnessPoint, Is.EqualTo(V(1, 0, 0)));
            Assert.That(separated.TargetWitnessPoint, Is.EqualTo(V(3, 0, 0)));
            Assert.That(touching.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(touching.SurfaceGap, Is.EqualTo(Fix64.Zero));
            Assert.That(overlapping.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(overlapping.SurfaceGap, Is.EqualTo(Fix64.Zero));
        }

        [Test]
        public void CapsuleAabbCapsuleAndObb_UseExactSupportedKernels()
        {
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);
            CombatShapeDistanceResult aabb = Distance(
                source,
                CombatPhysicsShape.Aabb(V(3, -1, -1), V(5, 1, 1)));
            CombatShapeDistanceResult capsule = Distance(
                source,
                CombatPhysicsShape.Capsule(V(5, -1, 0), V(5, 1, 0), Fix64.One));
            CombatShapeDistanceResult obb = Distance(
                source,
                CombatPhysicsShape.Obb(
                    V(4, 0, 0),
                    V(1, 1, 1),
                    V(1, 0, 0),
                    V(0, 1, 0),
                    V(0, 0, 1)));

            Assert.That(aabb.SurfaceGap, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(capsule.SurfaceGap, Is.EqualTo(Fix64.FromInt(3)));
            Assert.That(obb.SurfaceGap, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(aabb.HasDistance && capsule.HasDistance && obb.HasDistance, Is.True);
        }

        [Test]
        public void UnsupportedAndInvalidIdentity_FailClosed()
        {
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);
            CombatShapeDistanceResult unsupported = Distance(
                source,
                CombatPhysicsShape.Sector(V(3, 0, 0), V(1, 0, 0), Fix64.One, Fix64.Zero));
            CombatShapeDistanceResult invalid = CombatPhysicsWorld.CalculateCapsuleDistance(
                source,
                CombatEntityId.None,
                new CombatBodyId(20),
                new CombatColliderId(30),
                CombatPhysicsShape.Sphere(V(3, 0, 0), Fix64.One));

            Assert.That(unsupported.Status, Is.EqualTo(CombatShapeSeparationStatus.UnsupportedShape));
            Assert.That(unsupported.HasDistance, Is.False);
            Assert.That(invalid.Status, Is.EqualTo(CombatShapeSeparationStatus.InvalidShape));
            Assert.That(invalid.HasDistance, Is.False);
        }

        [Test]
        public void WarmedDistanceKernel_AllocatesZeroBytes()
        {
            CombatCapsuleQuery source = Query(V(0, -1, 0), V(0, 1, 0), Fix64.Half);
            CombatPhysicsShape target = CombatPhysicsShape.Capsule(
                V(4, -1, 0),
                V(4, 1, 0),
                Fix64.Half);
            _ = Distance(source, target);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
                _ = Distance(source, target);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        private static CombatShapeDistanceResult Distance(
            CombatCapsuleQuery source,
            CombatPhysicsShape target)
        {
            return CombatPhysicsWorld.CalculateCapsuleDistance(
                source,
                new CombatEntityId(10),
                new CombatBodyId(20),
                new CombatColliderId(30),
                target);
        }

        private static CombatCapsuleQuery Query(
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius)
        {
            return new CombatCapsuleQuery(
                new CombatQueryHeader(
                    1,
                    CombatQueryKind.Capsule,
                    CombatEntityId.None,
                    2,
                    3,
                    4,
                    CombatPhysicsLayerMask.FromLayer(9)),
                pointA,
                pointB,
                radius);
        }

        private static FixVector3 V(int x, int y, int z) => new FixVector3(
            Fix64.FromInt(x),
            Fix64.FromInt(y),
            Fix64.FromInt(z));
    }
}
